# AI Parameter Search — run v4 (2026-07-24)

Defense-inclusive evolution, following run v3 (attack). See `docs/param-search-v3/README.md`
for the methodology; identical setup (25 generations, λ=8, σ=0.12, 3 rotating seeds,
150s kickoff matches).

## What changed vs v3

- **Defensive knobs exposed** (`UtilityTuning`): defensive line depths by role,
  defensive ball pull, GK chase distances / line offset / ball tracking.
- **Parent**: the v3-evolved attacking vector (shipped defaults), not the original
  hand-tuned one — the search balanced defense around the proven attack.
- **Fitness**: unchanged (goal/shot/box-entry differentials are two-sided by
  construction, so conceding is priced in).

## Results (held-out seeds, 6 × 150s)

| Metric | v3 defaults | v4 evolved |
|---|---|---|
| Goals | 5–5 | **19–0** |
| Shots for–against | 11–31 | **61–2** |
| Box entries diff | −23 | **+22** |
| Attacking-third diff | −338s | **+302s** |
| Knockdowns | 2 | 1 |

Notable evolved behaviors: the GK comes out much further (720px vs 500) and tracks
ball Y aggressively (0.48 vs 0.25); forwards defend deeper (0.80); midfield holds a
lower line (0.34); shot commitment at range up (score 136 within 1600px ≈ 22m).

## Files

- `search_log.csv` — every evaluation of the run.
- `best_params.json` — winning vector, shipped (lightly rounded) as the new
  `UtilityTuning` defaults.

Related engine work validated by the same harness run family: dribble glue quality
(carrier-ball distance ~60px), contest steals (carrier changes), skill miscontrols,
and the knockdown-rate fix (per-challenge rolls instead of per-frame: ~1 knockdown
per 150s match).
