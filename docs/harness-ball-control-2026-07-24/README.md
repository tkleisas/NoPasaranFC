# Ball Control Fix Results (2026-07-24)

Third evidence round (after `harness-baseline-2026-07-22` and `harness-utility-ai-2026-07-23`).
Changes in this round: gentle contact touches (ball control), anti keep-ball pass-back tax,
corridor-aware dribbling, carrier shielding, forward-first pass scoring, first-touch grace,
stronger passes, Shoot action selection fix.

## Key improvements vs baseline (seed 42)

| Metric | Baseline | Utility AI | This round |
|---|---|---|---|
| Ball loose time | ~45% | ~45% | **4%** |
| Two-player keep-ball loops | frequent | frequent | **eliminated** |
| Carrier dribbles (open lanes) | rare | none | **regular** |
| Goals in real matches | rare | rare | **confirmed by user playtest** |

The structural breakthrough: contact push was pushing the ball up to 200 px/s on every
touch, making sustained possession impossible. Gentle touches (60 px/s cap) made the ball
playable, which cascaded into working carries, passes, and eventually goals.
