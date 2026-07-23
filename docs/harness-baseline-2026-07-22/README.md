# AI Behavior Baseline (pre-rewrite)

Captured 2026-07-22 with the headless harness (`dotnet run -- harness <scenario> --seconds 60 --seed 42 --out <prefix>`),
**before** the utility-AI rewrite. These diagrams and metrics are the "before" evidence
for the paper on harness-based AI behavior debugging.

## How to read the diagrams

- Dashed blue path: ball trajectory (dashes at 0.5s, circles every 5s)
- Red/blue trails: home/away players, numbered circles at 1s ticks with direction ticks
- Metrics JSON per scenario: AI state transitions/sec, velocity direction reversals,
  mean distance-to-target, possession, ball travel

## Key baseline findings (seed 42, 60s)

| Scenario | Result |
|---|---|
| `gk_ball` | Pathological: ball pinned at home GK for the full 60s; **636 direction reversals** (584 from one player). Players jitter in a cluster around the ball. |
| `center_line_dribble` | **35.9 AI state transitions/sec** team-wide; worst player 16.4/s — the midfield oscillation, visible as zigzag trails. |
| `corner_home` | Calm (11.7 trans/s); corner flow works. |
| `kickoff` | Identical to center_line_dribble (kickoff init = ball at center). |

Reproduce: `dotnet run -- harness gk_ball --seconds 60 --seed 42 --out /tmp/harness/gk_ball`
then `python3 Scripts/trajectory_plot.py /tmp/harness/gk_ball.log.jsonl /tmp/harness/gk_ball.png`
