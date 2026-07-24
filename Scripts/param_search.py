#!/usr/bin/env python3
"""
(1+lambda) evolution strategy over AIConstants, using the headless harness
as the fitness evaluator.

Each candidate is a full parameter vector (see Harness/search_space.json).
Children perturb the parent multiplicatively: v' = clamp(v * exp(N(0, sigma))).
Within a generation every candidate plays the same seed set (fair comparison);
the seed set rotates between generations to avoid overfitting specific seeds.

Fitness per candidate (aggregated over seeds):
    3.0 * (homeGoals - awayGoals)
  + 0.03 * (homeAttThird - awayAttThird)     [territorial pressure, seconds]
  + 0.5  * (possessionHome - possessionAway)
  - 0.01 * totalDirectionReversals           [oscillation penalty]
  - 0.1  * transitionsPerSecond              [state churn penalty]
  - 0.1  * maxBallStationarySeconds          [dead-ball penalty]

Every evaluation is logged to a CSV (paper trail), and the best vector is
kept in best_params.json (AIConstants overrides, harness --params format).

Usage:
  python3 Scripts/param_search.py [--generations N] [--lambda N] [--seeds N]
                                  [--seconds N] [--sigma X] [--workers N]
                                  [--outdir DIR] [--resume best_params.json]
"""

import argparse
import csv
import json
import math
import os
import random
import subprocess
import sys
import tempfile
import time
from concurrent.futures import ThreadPoolExecutor

PROJECT_ROOT = os.path.dirname(os.path.dirname(os.path.abspath(__file__)))
SEARCH_SPACE_PATH = os.path.join(PROJECT_ROOT, "Harness", "search_space.json")

# Fitness weights (documented above; keep in sync)
W_GOAL_DIFF = 3.0
W_SHOT_DIFF = 0.5
W_BOX_DIFF = 0.2
W_ATT_THIRD = 0.02
W_POSSESSION = 0.5
W_REVERSALS = 0.01
W_TRANSITIONS = 0.1
W_STATIONARY = 0.1


def load_search_space():
    with open(SEARCH_SPACE_PATH) as f:
        doc = json.load(f)
    return doc["parameters"]


def run_harness(seed, seconds, params, tag, outdir):
    """Run one harness match; returns the parsed metrics dict."""
    params_path = os.path.join(outdir, f"params_{tag}.json")
    with open(params_path, "w") as f:
        json.dump(params, f)
    out_prefix = os.path.join(outdir, f"run_{tag}")
    cmd = [
        "dotnet", "run", "--project", "NoPasaranFC.csproj", "--no-build", "--",
        "harness", "kickoff",
        "--seconds", str(seconds),
        "--seed", str(seed),
        "--out", out_prefix,
        "--params", params_path,
        "--nolog",
    ]
    result = subprocess.run(cmd, cwd=PROJECT_ROOT, capture_output=True, text=True, timeout=600)
    if result.returncode != 0:
        raise RuntimeError(f"harness failed for {tag}: {result.stderr[-500:]}")
    with open(out_prefix + ".metrics.json") as f:
        metrics = json.load(f)
    os.remove(out_prefix + ".metrics.json")
    os.remove(params_path)
    return metrics


def evaluate(params, seeds, seconds, tag, outdir, workers):
    """Evaluate a candidate on all seeds (in parallel); returns (fitness, component sums)."""
    with ThreadPoolExecutor(max_workers=workers) as pool:
        all_metrics = list(pool.map(
            lambda s: run_harness(s, seconds, params, f"{tag}_s{s}", outdir), seeds))

    comp = {
        "goalDiff": 0.0, "shotDiff": 0.0, "boxDiff": 0.0, "attThirdDiff": 0.0,
        "possDiff": 0.0, "reversals": 0.0, "transPerSec": 0.0, "stationary": 0.0,
        "homeGoals": 0.0, "awayGoals": 0.0,
    }
    for m in all_metrics:
        comp["homeGoals"] += m["HomeScore"]
        comp["awayGoals"] += m["AwayScore"]
        comp["goalDiff"] += m["HomeScore"] - m["AwayScore"]
        comp["shotDiff"] += m.get("HomeShots", 0) - m.get("AwayShots", 0)
        comp["boxDiff"] += m.get("HomeBoxEntries", 0) - m.get("AwayBoxEntries", 0)
        comp["attThirdDiff"] += (m["BallInHomeAttackingThirdSeconds"]
                                 - m["BallInAwayAttackingThirdSeconds"])
        comp["possDiff"] += m["PossessionHome"] - m["PossessionAway"]
        comp["reversals"] += m["TotalDirectionReversals"]
        comp["transPerSec"] += m["TotalTransitionsPerSecond"]
        comp["stationary"] += m["MaxBallStationarySeconds"]

    fitness = (W_GOAL_DIFF * comp["goalDiff"]
               + W_SHOT_DIFF * comp["shotDiff"]
               + W_BOX_DIFF * comp["boxDiff"]
               + W_ATT_THIRD * comp["attThirdDiff"]
               + W_POSSESSION * comp["possDiff"]
               - W_REVERSALS * comp["reversals"]
               - W_TRANSITIONS * comp["transPerSec"]
               - W_STATIONARY * comp["stationary"])
    return fitness, comp


def perturb(parent, space, sigma, rng):
    child = {}
    for name, spec in space.items():
        v = parent[name] * math.exp(rng.gauss(0.0, sigma))
        child[name] = min(max(v, spec["min"]), spec["max"])
    return child


def main():
    ap = argparse.ArgumentParser()
    ap.add_argument("--generations", type=int, default=30)
    ap.add_argument("--lambda", dest="lam", type=int, default=8)
    ap.add_argument("--seeds", type=int, default=3)
    ap.add_argument("--seconds", type=int, default=150)
    ap.add_argument("--sigma", type=float, default=0.12)
    ap.add_argument("--workers", type=int, default=8)
    ap.add_argument("--outdir", default=None)
    ap.add_argument("--resume", default=None, help="JSON with the parent vector")
    ap.add_argument("--master-seed", type=int, default=1979)
    args = ap.parse_args()

    space = load_search_space()
    rng = random.Random(args.master_seed)
    outdir = args.outdir or tempfile.mkdtemp(prefix="param_search_")
    os.makedirs(outdir, exist_ok=True)

    if args.resume:
        with open(args.resume) as f:
            parent = json.load(f)
        # keep only keys present in the search space
        parent = {k: parent[k] for k in space if k in parent}
    else:
        parent = {name: spec["default"] for name, spec in space.items()}

    csv_path = os.path.join(outdir, "search_log.csv")
    best_path = os.path.join(outdir, "best_params.json")
    param_names = list(space.keys())
    comp_names = ["homeGoals", "awayGoals", "goalDiff", "shotDiff", "boxDiff",
                  "attThirdDiff", "possDiff", "reversals", "transPerSec", "stationary"]

    with open(csv_path, "w", newline="") as csvfile:
        writer = csv.writer(csvfile)
        writer.writerow(["gen", "candidate", "kind", "fitness"] + comp_names + param_names)

        def log(gen, cand_id, kind, fitness, comp, params):
            writer.writerow([gen, cand_id, kind, f"{fitness:.4f}"]
                            + [f"{comp[c]:.3f}" for c in comp_names]
                            + [f"{params[p]:.4g}" for p in param_names])
            csvfile.flush()

        best_ever = None
        best_fitness = -1e18
        t0 = time.time()

        for gen in range(args.generations):
            # Rotating seed set: fair within the generation, fresh across generations
            seeds = [rng.randrange(1, 1_000_000) for _ in range(args.seeds)]

            candidates = [("parent", parent)]
            children = [perturb(parent, space, args.sigma, rng) for _ in range(args.lam)]
            candidates += [(f"child{i}", c) for i, c in enumerate(children)]

            results = []
            for cand_id, params in candidates:
                fitness, comp = evaluate(params, seeds, args.seconds,
                                         f"g{gen}_{cand_id}", outdir, args.workers)
                results.append((fitness, cand_id, params, comp))
                kind = "parent" if cand_id == "parent" else "child"
                log(gen, cand_id, kind, fitness, comp, params)

            results.sort(key=lambda r: r[0], reverse=True)
            top_fitness, top_id, top_params, top_comp = results[0]
            parent = top_params  # (1+lambda): best becomes the next parent

            if top_fitness > best_fitness:
                best_fitness = top_fitness
                best_ever = dict(top_params)
                with open(best_path, "w") as f:
                    json.dump(best_ever, f, indent=2)

            elapsed = time.time() - t0
            print(f"gen {gen:3d} | best={top_id} fit={top_fitness:7.3f} "
                  f"goals={top_comp['homeGoals']:.0f}-{top_comp['awayGoals']:.0f} "
                  f"att3rd={top_comp['attThirdDiff']:6.1f}s rev={top_comp['reversals']:.0f} "
                  f"| seeds={seeds} | {elapsed:6.1f}s", flush=True)

    print(f"\nDone. Best fitness {best_fitness:.3f}")
    print(f"Best params: {best_path}")
    print(f"Full log:    {csv_path}")


if __name__ == "__main__":
    sys.exit(main())
