# AI Parameter Search — run v3 (2026-07-24)

Methodology artifacts for the utility-AI tuning paper.

## Setup

- **Search**: (1+λ) evolution strategy, λ=8, σ=0.12 multiplicative perturbation,
  30 generations, `Scripts/param_search.py` over `Harness/search_space.json` (24 knobs
  of `Gameplay/UtilityAI/UtilityTuning.cs`).
- **Evaluator**: headless deterministic harness (`Harness/HarnessRunner.cs`),
  `kickoff` scenario, 150 simulated seconds per match (~4.5s wall), 3 rotating seeds
  per generation, fair within-generation comparison.
- **Fitness**: `3.0·goalDiff + 0.5·shotDiff + 0.2·boxEntryDiff + 0.02·attThirdDiff
  + 0.5·possDiff − 0.01·reversals − 0.1·transitionsPerSec − 0.1·maxStationary`.

## Key findings

1. **Legacy-constant trap (run v1–v2)**: tuning `AIConstants` produced bit-identical
   matches — since the v2.4.0 utility-AI rewrite those constants are dead code.
   The live decision layer (`UtilityBrain`) had inline literals, exposed as
   `UtilityTuning` for this work.
2. **Structural pass suppression**: the pass action score (`30 + BestPassScore/50`,
   max ≈ 80) could never beat dribbling (≈95–114). Frame census of a 150s match
   showed **zero** Pass/Shoot actions — the AI only ever dribbled or held position.
   Fixed with the tunable `PassScoreScale`.
3. **Fitness must target outcomes, not proxies**: run v2 optimized attacking-third
   time and "improved" it without a single goal (territorial camping). Adding shot
   and box-entry terms made goals emerge immediately (3–7 per generation vs 0–1).

## Results (held-out seeds, 150s matches)

| Metric | Old defaults | Evolved (as shipped) |
|---|---|---|
| Goals (6-8 matches) | 0–1 | 6–9 |
| Shots | 0 | 6+ |
| Box entries diff | 2 | 9–15 |
| Attacking-third diff | 279s | 494–616s |
| Direction reversals | 277 | 219–322 |

## Files

- `search_log.csv` — every evaluation: generation, candidate, fitness, all
  components, full parameter vector.
- `best_params.json` — winning vector (harness `--params` format); shipped as the
  new `UtilityTuning` defaults (lightly rounded).
