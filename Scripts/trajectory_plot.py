#!/usr/bin/env python3
"""Trajectory diagram for the NoPasaranFC headless AI harness.

Reads a harness JSONL log and renders the pitch, ball path (dashed, blue,
0.5s dashes, time circles every 5s) and per-player trails (home red, away
blue, numbered circles at 1s ticks with a direction tick).

Usage: python3 Scripts/trajectory_plot.py <out.log.jsonl> <out.png>
"""

import json
import math
import sys

from PIL import Image, ImageDraw, ImageFont

WIDTH, HEIGHT = 1400, 1000
TOP_MARGIN = 70      # title + legend
SIDE_MARGIN = 40
BOTTOM_MARGIN = 30

HOME_COLOR = (220, 50, 50)
AWAY_COLOR = (100, 150, 235)
BALL_COLOR = (30, 60, 200)
PITCH_LINE = (240, 240, 240)
GRASS = (34, 94, 44)
GRASS_DARK = (30, 84, 40)


def load_log(path):
    meta = {}
    frames = []
    with open(path, encoding="utf-8-sig") as f:
        for line in f:
            line = line.strip()
            if not line:
                continue
            rec = json.loads(line)
            if rec.get("meta"):
                meta = rec
            else:
                frames.append(rec)
    return meta, frames


def main():
    if len(sys.argv) != 3:
        print(__doc__)
        return 1

    meta, frames = load_log(sys.argv[1])
    out_path = sys.argv[2]

    margin = float(meta.get("stadiumMargin", 400))
    fw = float(meta.get("fieldWidth", 7665))
    fh = float(meta.get("fieldHeight", 4964))
    fps = int(meta.get("fps", 60))

    # World area: pitch plus the stadium margin, so out-of-bounds play shows.
    world_x0, world_y0 = 0.0, 0.0
    world_x1, world_y1 = fw + 2 * margin, fh + 2 * margin

    avail_w = WIDTH - 2 * SIDE_MARGIN
    avail_h = HEIGHT - TOP_MARGIN - BOTTOM_MARGIN
    scale = min(avail_w / (world_x1 - world_x0), avail_h / (world_y1 - world_y0))
    off_x = SIDE_MARGIN + (avail_w - (world_x1 - world_x0) * scale) / 2
    off_y = TOP_MARGIN + (avail_h - (world_y1 - world_y0) * scale) / 2

    def P(x, y):
        return (off_x + (x - world_x0) * scale, off_y + (y - world_y0) * scale)

    img = Image.new("RGB", (WIDTH, HEIGHT), (18, 18, 22))
    draw = ImageDraw.Draw(img)
    font = ImageFont.load_default()
    try:
        font = ImageFont.truetype("DejaVuSans.ttf", 13)
        title_font = ImageFont.truetype("DejaVuSans-Bold.ttf", 18)
        small_font = ImageFont.truetype("DejaVuSans.ttf", 10)
    except OSError:
        title_font = font
        small_font = font

    # --- Pitch (mowed stripes, outline, center line, center circle, boxes) ---
    pitch_x0, pitch_y0 = P(margin, margin)
    pitch_x1, pitch_y1 = P(margin + fw, margin + fh)
    draw.rectangle([pitch_x0, pitch_y0, pitch_x1, pitch_y1], fill=GRASS)
    n_stripes = 10
    for i in range(0, n_stripes, 2):
        sx0 = pitch_x0 + (pitch_x1 - pitch_x0) * i / n_stripes
        sx1 = pitch_x0 + (pitch_x1 - pitch_x0) * (i + 1) / n_stripes
        draw.rectangle([sx0, pitch_y0, sx1, pitch_y1], fill=GRASS_DARK)
    draw.rectangle([pitch_x0, pitch_y0, pitch_x1, pitch_y1], outline=PITCH_LINE, width=2)

    cx0, _ = P(margin + fw / 2, 0)
    draw.line([cx0, pitch_y0, cx0, pitch_y1], fill=PITCH_LINE, width=2)
    ccx, ccy = P(margin + fw / 2, margin + fh / 2)
    r = 670 * scale  # 9.15m center circle
    draw.ellipse([ccx - r, ccy - r, ccx + r, ccy + r], outline=PITCH_LINE, width=2)

    box_w, box_h = 1204 * scale, 2930 * scale  # 16.5m x 40.3m penalty boxes
    mid_y = (pitch_y0 + pitch_y1) / 2
    draw.rectangle([pitch_x0, mid_y - box_h / 2, pitch_x0 + box_w, mid_y + box_h / 2],
                   outline=PITCH_LINE, width=2)
    draw.rectangle([pitch_x1 - box_w, mid_y - box_h / 2, pitch_x1, mid_y + box_h / 2],
                   outline=PITCH_LINE, width=2)

    # --- Player trails ---
    n_players = len(frames[0]["players"]) if frames else 0
    trails = [[] for _ in range(n_players)]
    for rec in frames:
        for p in rec["players"]:
            trails[p["i"]].append((p["x"], p["y"], p.get("vx", 0), p.get("vy", 0)))

    for idx, trail in enumerate(trails):
        team = frames[0]["players"][idx]["team"]
        color = HOME_COLOR if team == "home" else AWAY_COLOR
        pts = [P(x, y) for x, y, _, _ in trail]
        if len(pts) > 1:
            draw.line(pts, fill=color, width=1)

        # Numbered circle + direction tick at every 1s tick
        for tick in range(0, len(trail), fps):
            x, y, vx, vy = trail[tick]
            px, py = P(x, y)
            speed = math.hypot(vx, vy)
            if speed > 1:
                dx, dy = vx / speed, vy / speed
            elif tick + 1 < len(trail):
                nx, ny, _, _ = trail[tick + 1]
                d = math.hypot(nx - x, ny - y) or 1
                dx, dy = (nx - x) / d, (ny - y) / d
            else:
                dx, dy = 1, 0
            draw.line([px, py, px + dx * 11, py + dy * 11], fill=color, width=2)
            r_c = 7
            draw.ellipse([px - r_c, py - r_c, px + r_c, py + r_c], fill=color,
                         outline=(255, 255, 255))
            label = str(idx)
            tw = draw.textlength(label, font=small_font)
            draw.text((px - tw / 2, py - 5), label, fill=(255, 255, 255), font=small_font)

    # --- Ball path: dashed polyline (0.5s on / 0.5s off), circle every 5s ---
    ball_pts = [(rec["ball"]["x"], rec["ball"]["y"], rec["t"]) for rec in frames]
    dash_frames = max(1, int(0.5 * fps))
    for i in range(len(ball_pts) - 1):
        if (i // dash_frames) % 2 == 0:
            draw.line([P(*ball_pts[i][:2]), P(*ball_pts[i + 1][:2])],
                      fill=BALL_COLOR, width=2)
    for x, y, t in ball_pts:
        if abs(t % 5.0) < (1.0 / fps) / 2 or abs(t % 5.0 - 5.0) < (1.0 / fps) / 2:
            px, py = P(x, y)
            r_b = 9
            draw.ellipse([px - r_b, py - r_b, px + r_b, py + r_b],
                         outline=BALL_COLOR, width=2)
            draw.text((px + 11, py - 7), f"{t:.0f}s", fill=(140, 170, 255), font=small_font)

    # --- Title + legend ---
    title = (f"Scenario: {meta.get('scenario', '?')}   seed={meta.get('seed', '?')}   "
             f"duration={meta.get('seconds', '?')}s   "
             f"{meta.get('homeTeam', 'home')} (red) vs {meta.get('awayTeam', 'away')} (blue)")
    draw.text((SIDE_MARGIN, 12), title, fill=(240, 240, 240), font=title_font)

    ly = 42
    draw.line([SIDE_MARGIN, ly, SIDE_MARGIN + 24, ly], fill=HOME_COLOR, width=3)
    draw.text((SIDE_MARGIN + 30, ly - 6), "home players", fill=(220, 220, 220), font=font)
    draw.line([SIDE_MARGIN + 130, ly, SIDE_MARGIN + 154, ly], fill=AWAY_COLOR, width=3)
    draw.text((SIDE_MARGIN + 160, ly - 6), "away players", fill=(220, 220, 220), font=font)
    for k in range(3):
        x0 = SIDE_MARGIN + 260 + k * 8
        draw.line([x0, ly, x0 + 5, ly], fill=BALL_COLOR, width=3)
    draw.text((SIDE_MARGIN + 290, ly - 6), "ball (circles every 5s)", fill=(220, 220, 220), font=font)

    img.save(out_path)
    print(f"wrote {out_path} ({len(frames)} frames, {n_players} players)")
    return 0


if __name__ == "__main__":
    sys.exit(main())
