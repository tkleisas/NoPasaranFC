# Utility AI Results (post-rewrite)

Captured 2026-07-23 with the same harness scenarios/seeds as the baseline
(`docs/harness-baseline-2026-07-22`), after the utility-AI rewrite
(utility scoring + steering movement, `Gameplay/UtilityAI/`).

## Before/after (seed 42, 60s)

| Scenario | Metric | Baseline (FSM) | Utility AI |
|---|---|---|---|
| gk_ball | direction reversals | **636** | **18** |
| gk_ball | AI transitions/s | 25.7 | 1.45 |
| gk_ball | ball travel | stuck at GK | 9401 m |
| center_line | transitions/s | **35.9** | **1.33** |
| center_line | reversals | 76 | 20 |
| center_line | max ball stationary | 53.5 s (freeze) | 2.9 s |
| corner_home | transitions/s | 11.7 | 2.08 |

Notable bugs found via harness during the rewrite:
- Frozen kickoff (no chase at kickoff distances, hold/chase score tie)
- Pass-blocked infinite loop (kick gate without range check in decision)
- Far kicks between decision ticks (near-ball gate in execution)
