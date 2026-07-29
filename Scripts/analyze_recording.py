#!/usr/bin/env python3
"""Anomaly analyzer for NoPasaranFC match recordings / harness logs.

Parses a JSONL log (meta + frame lines + optional event lines, same schema as
Scripts/trajectory_plot.py), flags AI behavior anomalies during Playing
frames, writes a machine-readable report.json plus one annotated trajectory
PNG per anomaly (reusing trajectory_plot's renderer).

Usage:
    python3 Scripts/analyze_recording.py <log.jsonl> <outdir>
        [--window 8] [--min-severity low|medium|high] [--max-diagrams 20]

Anomaly types (thresholds are module constants, tuned on the sample
recordings; times in seconds, distances in engine px, 73px = 1m):

  idle_near_ball (medium): AI player (not human-controlled) with speed <
      IDLE_SPEED within IDLE_BALL_DIST of a loose ball (ball speed <
      IDLE_BALL_SPEED, no pass/shot/kick event within +-EVENT_GRACE),
      sustained >= IDLE_MIN_DURATION. One anomaly per contiguous episode.
  oscillation (medium/high): >= OSC_MIN_EVENTS AI-state changes OR velocity
      direction reversals (dot(v, v_prev) < 0, both speeds > OSC_MIN_SPEED)
      within any OSC_WINDOW sliding window, per player. Overlapping episodes
      merged. High severity when the peak window count >= OSC_HIGH_COUNT or
      the episode lasts >= OSC_HIGH_DURATION.
  box_passivity (high): a team carriers the ball (nearest player within
      CARRIER_DIST of the ball) inside its attacking penalty box
      (AIConstants.GKPenaltyAreaDepth x GKPenaltyAreaWidth) for >
      BOX_MIN_DURATION and the episode ends with no shot/goal event from
      that team. Attacking box per team is derived from shot directions
      (mean vx sign); a team with no shots inherits the opposite of its
      opponent's side; only with zero shots in the whole log (e.g. harness
      logs, which record no events) does it fall back to GK position (the
      GK defends his own goal, so the team attacks the opposite box). CAVEAT: the log
      has no half marker, so a halftime side switch can attribute the wrong
      box (false positives possible).
  decision_regret (low, only when verbose "dec" blocks are present): a
      player's chosen utility action scores within REGRET_MARGIN of the best
      rejected alternative, sustained >= REGRET_MIN_DURATION (borderline
      decisions flapping). REGRET_MARGIN is 0.15 rather than 0.05 because
      the recorder rounds scores to 0.1 (F1 formatting).
"""

import argparse
import json
import math
import os
import sys

sys.path.insert(0, os.path.dirname(os.path.abspath(__file__)))
import trajectory_plot as tp

# --- Thresholds (documented above) ---
IDLE_SPEED = 15.0            # px/s
IDLE_BALL_DIST = 120.0       # px
# Ball slower than ~2.7 m/s counts as "loose-ish". The spec's 60 px/s (~0.8
# m/s, nearly stationary) never fires on real recordings - a nearby ball is
# almost always being dribbled (moving with the carrier) or was just kicked.
IDLE_BALL_SPEED = 200.0      # px/s
IDLE_MIN_DURATION = 2.0      # s
EVENT_GRACE = 1.0            # s around pass/shot/kick events

OSC_WINDOW = 2.0             # s sliding window
OSC_MIN_EVENTS = 4
OSC_MIN_SPEED = 40.0         # px/s both vectors for a reversal
OSC_HIGH_COUNT = 6           # peak events in a window -> high
OSC_HIGH_DURATION = 3.0      # s episode -> high

BOX_DEPTH = 1205.0           # AIConstants.GKPenaltyAreaDepth
BOX_WIDTH = 2942.0           # AIConstants.GKPenaltyAreaWidth
CARRIER_DIST = 100.0         # px
BOX_MIN_DURATION = 1.5       # s
BOX_EVENT_GRACE_AFTER = 0.5  # a shot just as the episode ends still counts

# The recorder serializes scores with F1 formatting (0.1 resolution), so the
# spec's 0.05 margin can never fire on live recordings; 0.15 = one
# quantization step + slack, i.e. "chosen is (nearly) tied with a rejected
# alternative".
REGRET_MARGIN = 0.15
REGRET_MIN_DURATION = 1.5    # s

EPISODE_GAP = 0.35           # s; frames closer than this are one episode

SEVERITY_RANK = {"low": 0, "medium": 1, "high": 2}
RING_COLOR = (255, 220, 40)
RING_COLOR2 = (255, 60, 60)


def _speed(p):
    return math.hypot(p.get("vx", 0.0), p.get("vy", 0.0))


def _dist(ax, ay, bx, by):
    return math.hypot(ax - bx, ay - by)


def _episodes(flags, times, min_duration, gap=EPISODE_GAP):
    """flags: bool per sample; times: sample times. Yield (t_start, t_end, i0, i1)
    for contiguous True runs lasting >= min_duration. Short False runs (time
    span <= gap) between True samples are bridged, so a one-frame blip or a
    brief state change does not split an episode."""
    n = len(flags)
    if n == 0:
        return
    dt = (times[1] - times[0]) if n > 1 else 0.1
    bridged = list(flags)
    i = 0
    while i < n:
        if flags[i]:
            i += 1
            continue
        j = i
        while j + 1 < n and not flags[j + 1]:
            j += 1
        # False run [i..j]; bridge it if True on both sides and short enough
        if (i > 0 and j + 1 < n and flags[i - 1] and flags[j + 1] and
                times[j + 1] - times[i - 1] <= gap):
            for k in range(i, j + 1):
                bridged[k] = True
        i = j + 1
    i = 0
    while i < n:
        if not bridged[i]:
            i += 1
            continue
        j = i
        while j + 1 < n and bridged[j + 1]:
            j += 1
        t0, t1 = times[i], times[j]
        if t1 - t0 + dt >= min_duration:  # +dt: a sample covers one interval
            yield (t0, t1, i, j)
        i = j + 1


def detect_idle_near_ball(frames, events):
    """AI player standing next to a loose ball, sustained."""
    event_times = [e["t"] for e in events if e.get("ev") in ("pass", "shot", "kick")]

    def near_event(t):
        return any(abs(t - et) <= EVENT_GRACE for et in event_times)

    anomalies = []
    if not frames:
        return anomalies
    n_players = len(frames[0]["players"])
    times = [f["t"] for f in frames]
    for idx in range(n_players):
        flags = []
        for f in frames:
            p = f["players"][idx]
            b = f["ball"]
            if p.get("controlled"):
                flags.append(False)
                continue
            cond = (_speed(p) < IDLE_SPEED and
                    _dist(p["x"], p["y"], b["x"], b["y"]) < IDLE_BALL_DIST and
                    math.hypot(b["vx"], b["vy"]) < IDLE_BALL_SPEED and
                    not near_event(f["t"]))
            flags.append(cond)
        for t0, t1, i0, i1 in _episodes(flags, times, IDLE_MIN_DURATION):
            p = frames[i0]["players"][idx]
            anomalies.append({
                "type": "idle_near_ball",
                "severity": "medium",
                "t_start": round(t0, 1),
                "t_end": round(t1, 1),
                "duration": round(t1 - t0, 1),
                "players": [p["name"]],
                "player_indices": [idx],
                "details": {
                    "team": p["team"],
                    "note": (f"AI player idle (<{IDLE_SPEED:.0f}px/s) within "
                             f"{IDLE_BALL_DIST:.0f}px of a loose ball"),
                },
            })
    return anomalies


def detect_oscillation(frames):
    """Rapid AI-state changes or velocity direction reversals per player."""
    anomalies = []
    if len(frames) < 2:
        return anomalies
    n_players = len(frames[0]["players"])
    times = [f["t"] for f in frames]
    for idx in range(n_players):
        if frames[0]["players"][idx].get("controlled"):
            continue
        # Event instants: state changes and direction reversals
        change_times = []
        transitions = {}
        reversal_times = []
        prev_state = None
        prev_v = None
        for f in frames:
            p = f["players"][idx]
            st = p.get("state", "?")
            if prev_state is not None and st != prev_state:
                change_times.append(f["t"])
                key = (prev_state, st) if prev_state <= st else (st, prev_state)
                transitions[key] = transitions.get(key, 0) + 1
            v = (p.get("vx", 0.0), p.get("vy", 0.0))
            if prev_v is not None:
                dot = v[0] * prev_v[0] + v[1] * prev_v[1]
                if (dot < 0 and math.hypot(*v) > OSC_MIN_SPEED and
                        math.hypot(*prev_v) > OSC_MIN_SPEED):
                    reversal_times.append(f["t"])
            prev_state = st
            prev_v = v

        hot = [False] * len(frames)  # frame covered by a hot window

        def mark_windows(evt_times):
            peak = 0
            for i, f in enumerate(frames):
                lo = f["t"] - OSC_WINDOW
                c = sum(1 for et in evt_times if lo <= et <= f["t"])
                if c >= OSC_MIN_EVENTS:
                    peak = max(peak, c)
                    for j in range(i, -1, -1):
                        if frames[j]["t"] < lo:
                            break
                        hot[j] = True
            return peak

        peak_changes = mark_windows(change_times)
        peak_reversals = mark_windows(reversal_times)
        peak = max(peak_changes, peak_reversals)
        if peak == 0:
            continue
        for t0, t1, i0, i1 in _episodes(hot, times, 0.0, gap=OSC_WINDOW):
            p = frames[i0]["players"][idx]
            n_changes = sum(1 for ct in change_times if t0 <= ct <= t1)
            n_reversals = sum(1 for rt in reversal_times if t0 <= rt <= t1)
            severity = ("high" if peak >= OSC_HIGH_COUNT or
                        t1 - t0 >= OSC_HIGH_DURATION else "medium")
            trans_str = ", ".join(f"{a}<->{b}x{c}" for (a, b), c in
                                  sorted(transitions.items(), key=lambda kv: -kv[1])[:3])
            anomalies.append({
                "type": "oscillation",
                "severity": severity,
                "t_start": round(t0, 1),
                "t_end": round(t1, 1),
                "duration": round(t1 - t0, 1),
                "players": [p["name"]],
                "player_indices": [idx],
                "details": {
                    "team": p["team"],
                    "state_changes": n_changes,
                    "reversals": n_reversals,
                    "peak_in_window": peak,
                    "transitions": trans_str,
                },
            })
    return anomalies


def detect_box_passivity(meta, frames, events):
    """Carrier holds the ball in the attacking box, episode ends without a shot."""
    anomalies = []
    if not frames:
        return anomalies
    margin = float(meta.get("stadiumMargin", 400))
    fw = float(meta.get("fieldWidth", 7665))
    fh = float(meta.get("fieldHeight", 4964))
    cy = margin + fh / 2.0
    y0, y1 = cy - BOX_WIDTH / 2, cy + BOX_WIDTH / 2
    left_box = (margin, margin + BOX_DEPTH)
    right_box = (margin + fw - BOX_DEPTH, margin + fw)

    def in_box(x, y, box):
        return box[0] <= x <= box[1] and y0 <= y <= y1

    # Attacking side per team: mean sign of shot vx (positive -> attacks right).
    # A team with no shots inherits the opposite of its opponent's side; only
    # if neither team has a shot do we fall back to box possession time.
    # CAVEAT: the log has no half marker, so a halftime side switch in a
    # recording with shots in only one half can still misattribute (false
    # positives possible - noted in the module docstring).
    attacks_right = {}
    for team in ("home", "away"):
        vxs = [e.get("vx", 0.0) for e in events
               if e.get("ev") == "shot" and e.get("team") == team]
        if vxs:
            attacks_right[team] = (sum(vxs) / len(vxs)) > 0
    if "home" in attacks_right and "away" not in attacks_right:
        attacks_right["away"] = not attacks_right["home"]
    elif "away" in attacks_right and "home" not in attacks_right:
        attacks_right["home"] = not attacks_right["away"]

    # Carrier per frame: nearest player within CARRIER_DIST of the ball.
    carriers = []
    for f in frames:
        b = f["ball"]
        best, best_d = None, CARRIER_DIST
        for p in f["players"]:
            d = _dist(p["x"], p["y"], b["x"], b["y"])
            if d < best_d:
                best, best_d = p, d
        carriers.append(best)

    # Last-resort attacking side (no shots in the whole log, e.g. harness
    # logs which have no event lines at all): each team's GK defends the goal
    # on his own side, so the team attacks the opposite box. The GK is the
    # player whose mean x is farthest from the pitch center line.
    for team in ("home", "away"):
        if team in attacks_right:
            continue
        sum_x = {}
        for f in frames:
            for p in f["players"]:
                if p["team"] == team:
                    sx, n = sum_x.get(p["i"], (0.0, 0))
                    sum_x[p["i"]] = (sx + p["x"], n + 1)
        if not sum_x:
            attacks_right[team] = True
            continue
        center_x = margin + fw / 2.0
        gk_mean_x = max((sx / n for sx, n in sum_x.values()),
                        key=lambda mx: abs(mx - center_x))
        attacks_right[team] = gk_mean_x < center_x  # GK left -> attacks right

    no_event_lines = not events

    times = [f["t"] for f in frames]
    for team in ("home", "away"):
        box = right_box if attacks_right[team] else left_box
        side = "right" if attacks_right[team] else "left"
        flags = []
        for f, c in zip(frames, carriers):
            b = f["ball"]
            flags.append(c is not None and c["team"] == team and
                         in_box(b["x"], b["y"], box))
        for t0, t1, i0, i1 in _episodes(flags, times, BOX_MIN_DURATION):
            had_shot = any(e.get("ev") in ("shot", "goal") and e.get("team") == team and
                           t0 - 0.2 <= e["t"] <= t1 + BOX_EVENT_GRACE_AFTER
                           for e in events)
            if had_shot:
                continue
            names = sorted({carriers[k]["name"] for k in range(i0, i1 + 1)
                            if carriers[k] is not None})
            idxs = sorted({carriers[k]["i"] for k in range(i0, i1 + 1)
                           if carriers[k] is not None})
            note = ("possession in attacking box ended without a shot or goal")
            if no_event_lines:
                note += (" (CAVEAT: log has no event lines - shot/goal "
                         "suppression unavailable, a real shot still flags)")
            anomalies.append({
                "type": "box_passivity",
                "severity": "high",
                "t_start": round(t0, 1),
                "t_end": round(t1, 1),
                "duration": round(t1 - t0, 1),
                "players": names,
                "player_indices": idxs,
                "details": {
                    "team": team,
                    "box": side,
                    "note": note,
                    "attack_side_heuristic": ("shot direction" if any(
                        e.get("ev") == "shot" and e.get("team") == team
                        for e in events) else
                        ("opponent shot side" if any(
                            e.get("ev") == "shot" for e in events)
                         else "GK position (no shots in log)")),
                },
            })
    return anomalies


def detect_decision_regret(frames):
    """Chosen utility action barely beats a rejected alternative, sustained."""
    if not any("dec" in p for f in frames[::max(1, len(frames) // 50)] for p in f["players"]):
        return []  # no verbose decision data in this log - skip silently
    anomalies = []
    n_players = len(frames[0]["players"]) if frames else 0
    times = [f["t"] for f in frames]
    for idx in range(n_players):
        flags = []
        samples = []
        for f in frames:
            dec = f["players"][idx].get("dec")
            regret = False
            sample = None
            if dec and dec.get("alt"):
                best_alt = max(a["score"] for a in dec["alt"])
                if best_alt >= dec["score"] - REGRET_MARGIN:
                    regret = True
                    best = max(dec["alt"], key=lambda a: a["score"])
                    sample = (dec["action"], dec["score"],
                              best["action"], best["score"])
            flags.append(regret)
            samples.append(sample)
        for t0, t1, i0, i1 in _episodes(flags, times, REGRET_MIN_DURATION):
            p = frames[i0]["players"][idx]
            mid = samples[(i0 + i1) // 2] or samples[i0]
            anomalies.append({
                "type": "decision_regret",
                "severity": "low",
                "t_start": round(t0, 1),
                "t_end": round(t1, 1),
                "duration": round(t1 - t0, 1),
                "players": [p["name"]],
                "player_indices": [idx],
                "details": {
                    "team": p["team"],
                    "note": (f"chosen '{mid[0]}' ({mid[1]:.2f}) within "
                             f"{REGRET_MARGIN} of rejected '{mid[2]}' "
                             f"({mid[3]:.2f})"),
                },
            })
    return anomalies


def render_anomaly(meta, frames, anomaly, out_path, window):
    """Trajectory diagram of [t_start - window/2, t_end + window/2] with the
    offending player(s) ringed at the anomaly midpoint."""
    t0 = anomaly["t_start"] - window / 2
    t1 = anomaly["t_end"] + window / 2
    t_mid = (anomaly["t_start"] + anomaly["t_end"]) / 2
    slice_frames = [f for f in frames if t0 <= f["t"] <= t1]
    if not slice_frames:
        return False

    idxs = set(anomaly.get("player_indices", []))
    mid_frame = min(slice_frames, key=lambda f: abs(f["t"] - t_mid))
    ring_pts = [(p["x"], p["y"], p["name"]) for p in mid_frame["players"]
                if p["i"] in idxs]

    def annotate(draw, P, scale, fonts):
        for x, y, name in ring_pts:
            px, py = P(x, y)
            for r, col, w in ((22, RING_COLOR2, 4), (30, RING_COLOR, 3)):
                draw.ellipse([px - r, py - r, px + r, py + r], outline=col, width=w)
            label = f"{name} @t={mid_frame['t']:.1f}s"
            tw = draw.textlength(label, font=fonts["font"])
            lx = min(max(px - tw / 2, 5), tp.WIDTH - tw - 5)
            ly = max(py - 48, tp.TOP_MARGIN + 5)
            draw.rectangle([lx - 3, ly - 2, lx + tw + 3, ly + 16], fill=(18, 18, 22))
            draw.text((lx, ly), label, fill=RING_COLOR, font=fonts["font"])
        if anomaly["type"] == "box_passivity":
            margin = float(meta.get("stadiumMargin", 400))
            fw = float(meta.get("fieldWidth", 7665))
            fh = float(meta.get("fieldHeight", 4964))
            cy = margin + fh / 2.0
            if anomaly["details"].get("box") == "right":
                bx0 = margin + fw - BOX_DEPTH
                bx1 = margin + fw
            else:
                bx0 = margin
                bx1 = margin + BOX_DEPTH
            q0 = P(bx0, cy - BOX_WIDTH / 2)
            q1 = P(bx1, cy + BOX_WIDTH / 2)
            draw.rectangle([q0[0], q0[1], q1[0], q1[1]], outline=RING_COLOR2, width=4)

    names = ", ".join(anomaly["players"])
    title = (f"ANOMALY: {anomaly['type']} [{anomaly['severity']}]  "
             f"t={anomaly['t_start']:.1f}-{anomaly['t_end']:.1f}s  "
             f"({anomaly['duration']:.1f}s)  {names}")
    tp.render(meta, slice_frames, out_path, title=title, annotate=annotate)
    return True


def main():
    ap = argparse.ArgumentParser(description="NoPasaranFC recording anomaly analyzer")
    ap.add_argument("log")
    ap.add_argument("outdir")
    ap.add_argument("--window", type=float, default=8.0,
                    help="diagram time span in seconds (default 8)")
    ap.add_argument("--min-severity", choices=("low", "medium", "high"),
                    default="low")
    ap.add_argument("--max-diagrams", type=int, default=20)
    args = ap.parse_args()

    meta, all_frames, events = tp.load_log(args.log)
    if not all_frames:
        print("no frames found in log")
        return 1
    frames = [f for f in all_frames if f.get("state") == "Playing"]
    print(f"loaded {len(all_frames)} frames ({len(frames)} Playing), "
          f"{len(events)} events from {args.log}")

    anomalies = []
    anomalies += detect_idle_near_ball(frames, events)
    anomalies += detect_oscillation(frames)
    anomalies += detect_box_passivity(meta, frames, events)
    anomalies += detect_decision_regret(frames)

    min_rank = SEVERITY_RANK[args.min_severity]
    anomalies = [a for a in anomalies if SEVERITY_RANK[a["severity"]] >= min_rank]
    anomalies.sort(key=lambda a: (-SEVERITY_RANK[a["severity"]], -a["duration"],
                                  a["t_start"]))

    os.makedirs(args.outdir, exist_ok=True)

    # --- report.json ---
    report = {
        "source": os.path.abspath(args.log),
        "teams": {"home": meta.get("homeTeam"), "away": meta.get("awayTeam")},
        "frames": len(all_frames),
        "playing_frames": len(frames),
        "events": len(events),
        "min_severity": args.min_severity,
        "thresholds": {
            "idle_near_ball": {"speed": IDLE_SPEED, "ball_dist": IDLE_BALL_DIST,
                               "ball_speed": IDLE_BALL_SPEED,
                               "min_duration": IDLE_MIN_DURATION},
            "oscillation": {"window": OSC_WINDOW, "min_events": OSC_MIN_EVENTS,
                            "min_speed": OSC_MIN_SPEED,
                            "high_count": OSC_HIGH_COUNT,
                            "high_duration": OSC_HIGH_DURATION},
            "box_passivity": {"box_depth": BOX_DEPTH, "box_width": BOX_WIDTH,
                              "carrier_dist": CARRIER_DIST,
                              "min_duration": BOX_MIN_DURATION},
            "decision_regret": {"margin": REGRET_MARGIN,
                                "min_duration": REGRET_MIN_DURATION},
        },
        "counts": {},
        "anomalies": anomalies,
    }
    for a in anomalies:
        report["counts"][a["type"]] = report["counts"].get(a["type"], 0) + 1
    report_path = os.path.join(args.outdir, "report.json")
    with open(report_path, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)

    # --- Console summary ---
    print(f"\n=== Anomaly report: {os.path.basename(args.log)} ===")
    if not anomalies:
        print("no anomalies flagged")
    by_type = {}
    for a in anomalies:
        by_type.setdefault(a["type"], []).append(a)
    for atype, items in sorted(by_type.items(),
                               key=lambda kv: -max(SEVERITY_RANK[a["severity"]]
                                                   for a in kv[1])):
        sev = max((a["severity"] for a in items), key=lambda s: SEVERITY_RANK[s])
        total = sum(a["duration"] for a in items)
        print(f"  {atype:<18} x{len(items):<3} [{sev}]  total {total:.1f}s")
        offenders = {}
        for a in items:
            for n in a["players"]:
                offenders[n] = offenders.get(n, 0) + a["duration"]
        worst = sorted(offenders.items(), key=lambda kv: -kv[1])[:3]
        print(f"    worst: {', '.join(f'{n} ({d:.1f}s)' for n, d in worst)}")
    print(f"report: {report_path}")

    # --- Diagrams ---
    n_diagrams = 0
    for a in anomalies[: max(0, args.max_diagrams)]:
        n_diagrams += 1
        fname = (f"anomaly_{n_diagrams:03d}_{a['type']}_"
                 f"t{int(a['t_start'])}s.png")
        out_path = os.path.join(args.outdir, fname)
        if render_anomaly(meta, all_frames, a, out_path, args.window):
            a["diagram"] = fname
    if n_diagrams:
        print(f"diagrams: {n_diagrams} PNG(s) in {args.outdir}")
    # Rewrite report with diagram filenames included
    with open(report_path, "w", encoding="utf-8") as f:
        json.dump(report, f, ensure_ascii=False, indent=2)
    return 0


if __name__ == "__main__":
    sys.exit(main())
