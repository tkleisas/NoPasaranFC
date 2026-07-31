# AI Parameter Search — run v5 (2026-07-31)

First search over the post-oscillation-arc knob set. Methodology as v3/v4
((1+λ) ES, multiplicative perturbation, rotating seed sets, 150s kickoff
matches) but scaled up: 61 parameters (was 38), 120 generations, λ=16,
10 rotating seeds, 10 workers (~5h wall, 2040 evaluations, master seed 1979).

## What changed vs v4

- **Search space** (`Harness/search_space.json`): 38 → 61 knobs — all the
  v2.22–v2.26 arcs exposed (commitment layer, chase designation, dribble/post-pass
  commits, pass-failure memory, scramble discipline, cover/pass-offer coordination,
  shape). Dead knobs removed (`CommitmentBonus`, `GKDistributionMinScore` —
  verified zero consumers).
- **Crash tolerance**: a gen-11 candidate killed the first attempt —
  `PlacePenaltyKick` crashed on `First()` when the GK had been sent off
  (real engine bug, fixed + regression test in v2.27.0). The search script now
  logs crashing candidates to `crashes/` and scores them worst-fitness instead
  of aborting.
- **Resume**: relaunched from the gen-10 best vector (`--resume`).

## Results

Best fitness 253.5 (gen-0 parent ≈ 105). Held-out validation, 10 fresh seeds
(777001–777010), 150s kickoff:

| metric | shipped | v5best | adopted mix |
|---|---|---|---|
| goals/match | 0.8 | 4.4 | **3.2** |
| shots/match | 5.2 | 27.4 | **21.3** |
| raw churn /s | 1.45 | 2.84 | 3.02 |
| reversals | 220 | 526 | 434 |
| analyzer anomalies (seed 777001) | 17 / 65.3s | 16 / 52.8s | **12 / 45.4s** |

## Adoption decision (user-reviewed)

v5best bought goals by selling stability (ActionCommitMargin 8→3.9, commits
halved, HoldBaseScore 47→90). Instead of full adoption, a **curated subset**
was shipped as v2.27.0 defaults: all shooting/dribbling/passing/role/depth/
shape/coordination/GK values from v5best; all stability knobs kept at shipped
values (commitment margins, scramble/chase designation, pass-failure memory,
hold/chase base scores).

The remaining raw churn is scale-separation mode switching (adopted carrier
scale 100–140 vs kept chase/hold scale 47–94) — legitimate, bounded, and the
analyzer-visible oscillation is *better than shipped* (12 episodes / 45.4s).
Side effect: matches are higher-scoring overall (both teams use these knobs).
