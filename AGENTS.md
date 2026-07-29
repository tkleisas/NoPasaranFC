# NO PASARAN! Football Championship

A soccer game built with C# (.NET 9) and MonoGame 3.8, for desktop (Windows/Linux/macOS) and Android.
Championship with 8 teams; the player controls one player of "NO PASARAN!" at a time, AI controls
everything else. SQLite persistence. English/Greek localization (UTF-8 throughout).

The game has TWO match view modes, selectable in Settings:
- **3D** (default): perspective 3D view with rigged, animated players (skinned GLB models),
  the "Bahramis" municipal stadium, day/night/weather, and Broadcast/High/TopDown cameras.
- **2D**: the original top-down sprite view (Sensible Soccer style). Fully preserved.

## Build & Run

```bash
dotnet build NoPasaranFC.csproj          # desktop (net9.0)
dotnet run --project NoPasaranFC.csproj  # run desktop
dotnet test NoPasaranFC.Tests/NoPasaranFC.Tests.csproj  # xUnit suite (60+ tests, serialized)
dotnet build NoPasaranFC.Android/NoPasaranFC.Android.csproj  # Android (needs android workload)
```

- Do NOT build the solution file locally without the Android workload installed — build projects individually.
- Desktop DB lives at `bin/Debug/net9.0/nopasaran.db` (settings + championship, quick to inspect with sqlite3/python).
- Content: XNB files are pre-built with the MGCB CLI and copied to output; raw assets (GLB/PNG) in `Content/Models3D/` are loaded at runtime (no pipeline).

## Debug tooling (use it for verification!)

- **Debug TCP console**: launch with `NOPASARAN_DEBUG=1` (port via `NOPASARAN_DEBUG_PORT`, default 7777).
  Commands: `shot <path> [delayFrames]` (screenshot), `key|down|up <Keys name>` (inject input),
  `state` (screen, fps, match + animation census + replay diagnostics), `match` (jump to next match),
  `players`, `setstat <name> <stat> <value>`, `ball <x> <y> [vx vy]` (teleport ball),
  `ballopp` (give ball to nearest opponent, stages a tackle), `corner` (force a corner),
  `freekick` / `penalty` / `card` (stage set piece / card), `halftime` (jump to 2nd half, sides switch),
  `easter <fox|dog|crows|seagulls|tornado|ufo|blackout|cats>` (force an easter egg),
  `hold [on|off]` (freeze the match clock), `ppos <name> <x> <y>` (teleport player),
  `kick <name> [vx vy]` (force a deliberate kick), `touch <name>` (register a touch), `quit`.
  Client: `python3 Scripts/dbg.py "state" "shot /tmp/x.png 3"`.
- **Blender pipeline**: `python3 Scripts/blender_exec.py <script.py>` runs a Python script inside a
  running Blender instance (blender-mcp addon on 127.0.0.1:9876). Asset sources: `Content/Models3D/*.blend`.
- **Spike**: `Spikes/SkinnedSpike` loads any skinned GLB standalone:
  `SPIKE_SHOT=/tmp/x.png SPIKE_CLIP=Running_A dotnet run --no-build -- model.glb`
- **AI harness** (`Harness/`): headless, deterministic match simulation for AI evaluation:
  `dotnet run --project NoPasaranFC.csproj -- harness <kickoff|center_line_dribble|corner_home|gk_ball|in_box> [--seconds N] [--seed N] [--out <prefix>] [--params file.json] [--nolog]`
  writes `<prefix>.log.jsonl` (per-frame positions/AI states) + `<prefix>.metrics.json`
  (state churn, direction reversals, possession, attacking-third time);
  `python3 Scripts/trajectory_plot.py <log.jsonl> <out.png>` renders trajectory diagrams.
  Determinism hooks: `MatchEngine.SetRandomSeed`, `AIController.DeterministicSeedBase`,
  `TeamSeeder.DeterministicRosterSeed` (all null = original behavior).
  `--params` overrides `AIConstants`/`UtilityTuning` fields (mutable statics for this reason);
  `--nolog` skips the frame log (metrics only).
- **Match recorder** (`Gameplay/MatchRecorder.cs`, opt-in via Settings `RecordMatches` /
  `RecordVerbose`): records real gameplay to `recordings/match_<yyyyMMdd_HHmmss>.log.jsonl`
  under the current working directory (project root via `dotnet run`). Same JSONL schema as
  the harness log (`scenario:"live"`, frames sampled at 10 Hz — `fps:10`/`sampleHz:10` in the
  meta line), so `Scripts/trajectory_plot.py` renders it unchanged. Adds `{"t":..,"ev":...}`
  event lines (kicks, tackles, fouls, cards, offsides, goals, restarts, kickoffs —
  the `kickoff` event marks the post-goal/halftime reposition, payload = half number;
  sourced from the engine's `MatchEvent` hook at the stats sites) and, with `RecordVerbose`, a per-player
  `"dec"` block (chosen utility action + score + top-2 rejected alternatives, from
  `UtilityBrain.LastDecision`). Driven from `MatchScreen.Update`; the engine stays unaware.
- **Anomaly analyzer**: `python3 Scripts/analyze_recording.py <log.jsonl> <outdir>
  [--window 8] [--min-severity low|medium|high] [--max-diagrams 20]` — works on recordings
  and harness logs (same JSONL schema; Playing frames only). Flags AI behavior anomalies:
  `idle_near_ball` (AI player parked next to a loose ball 2s+), `oscillation` (≥4 AI-state
  changes or direction reversals in a 2s window), `box_passivity` (attacking-box possession
  >1.5s ending without a shot), `decision_regret` (chosen utility action tied with a rejected
  alternative 1.5s+, needs `RecordVerbose` dec blocks). Writes `report.json` (machine-readable,
  with thresholds) + one annotated trajectory PNG per anomaly (offender ringed at the anomaly
  moment, box highlighted for box_passivity), reusing `trajectory_plot.py`'s renderer.
- **AI parameter search**: `python3 Scripts/param_search.py` — (1+λ) evolution strategy over
  `Harness/search_space.json` (UtilityTuning knobs) using the harness as fitness evaluator
  (goals, shots, box entries, territory, oscillation); writes a full CSV log +
  `best_params.json` to its outdir. Run v3 artifacts + findings: `docs/param-search-v3/`.

## Project layout

- `Models/` — Player, Team, Match, Championship, GameSettings, Localization, Version
- `Database/` — SQLite manager + JSON seeders (`teams_seed.json`, `championships_seed.json`)
- `Gameplay/` — MatchEngine (simulation, no drawing!), utility AI (`UtilityAI/`, the live path) +
  legacy FSM (`AIStates/`), celebrations (`Celebrations/`), Camera (2D), Minimap, audio
- `Graphics3D/` — the 3D renderer: Camera3D, World3D (venue geometry), Ball3D, MatchRenderer3D,
  PlayerAnimator, FanSection, FoxWalker, GoalNet3D, TeamBench, MatchOfficials, ReplayBuffer,
  FaceComposer, MatchEnvironment (lighting/weather), RainSystem,
  KitTextureFactory, `Skinning/` (GLB loader + skinned playback, SharpGLTF + SkinnedEffect)
- `Screens/` — Screen system: Menu, Match, Lineup, Standings, Settings, RoundResults, etc.
- `Debugging/` — DebugInput (input seam), DebugServer (TCP), ScreenCapture
- `Content/Models3D/` — GLB models + atlases + `.blend` sources (Player, PlayerF, Knight, Rogue, Fox, SoccerBall)

## Conventions

- **World scale**: 73 px = 1 meter everywhere (engine px ↔ 3D meters via `Graphics3D/WorldUnits.cs`).
- **Sim/render split**: `MatchEngine` is pure simulation (Vector2, ball height simulated separately).
  Renderers read engine state; never the reverse. Keep it that way.
- **Sides switch at halftime** (`MatchEngine.Half` 1→2). NEVER hardcode home=left/away=right.
  Use `MatchEngine.AttackSign(team)` (+1 attacks right), `GetOwnGoalCenter`/`GetOpponentGoalCenter`,
  `DefendedGoalLineX`/`AttackedGoalLineX`, `LeftDefendingTeam`/`RightDefendingTeam`.
  AI consumes `AIContext.AttackSign` + `OwnGoalCenter`/`OpponentGoalCenter` (built half-aware
  in `AIBehaviorManager.BuildAIContext`).
- **Additive 3D**: the 2D mode must keep working; new 3D features go in `Graphics3D/`, minimal seams in `MatchScreen`.
- **Settings**: add to `GameSettings` + a numbered migration in `DatabaseManager` + a `SettingsScreen`
  row + `Localization` keys (en + el). Defaults apply to fresh installs only.
- **Kits**: team shirt/shorts/socks via `KitTextureFactory` (region recolor of the player atlas,
  luminance-normalized). Kit colors live in `MatchRenderer3D.GetKitColors`.
- **Asset caching** (v2.13.1+, Android ANR fix): GLB models load once per process via
  `Graphics3D/Skinning/ModelCache` (preloaded in `Game1.LoadContent`); parts sharing an atlas
  share one texture (`SkinnedModel.LoadCore` dedup); venue procedural textures are static-cached
  per venue in `World3D`. `FaceComposer.AtlasScale` is 2 on desktop, 1 on Android.
  Never reload GLBs per match — stable atlas identity is what keeps the
  FaceComposer/KitTextureFactory caches (keyed on texture instance) hitting instead of leaking.
- **Animations**: KayKit clips on all humanoids (same skeleton). State→clip mapping in `PlayerAnimator`.
- **Anti-oscillation**: AI uses target inertia + start/stop hysteresis (`AIConstants`), animations use
  hysteresis (`PlayerAnimator`). Don't reintroduce raw per-frame target/state flipping.

- **Player/kit editor** (desktop only): `dotnet run --project NoPasaranFC.csproj -- --editor` opens
  straight into the editor (both championships → teams → roster/kit). Edits players (stats, gender,
  skin, hair, expression, feature) and kits (shirt/shorts/socks + GK colors, pattern, pattern color,
  freehand 32×32 shirt paint). Everything writes through to `teams_seed.custom.json` (overlay merged
  over `teams_seed.json` by name at catalog load — edits become the defaults for new seasons) and to
  the SQLite DB for teams in the active championship. Teams missing from the catalog are synthesized
  on the fly (same rule as championship creation). Kit colors/patterns live on `Team` (packed RGB ints,
  ShirtPaint = 1024 hex chars), appearance overrides on `Player` (-1 = hash auto); bake pipeline is
  shared via `Graphics3D/KitBake.cs`.

## Feature summary

- Championship: round-robin fixtures, match simulation for non-player matches, standings, round results, seasons
- Match statistics (`Gameplay/MatchStats.cs`, engine hooks): per-player goals/assists/shots/on-target/
  passes/completion/tackles/fouls/saves/offsides/cards + per-team possession/corners/throw-ins/free kicks/
  penalties; post-match `MatchStatsScreen` (before round results); season accumulation (migration 12:
  SeasonGoals/Assists/YellowCards/RedCards), simulated goals distributed by Shooting×position weight;
  top scorers page in StandingsScreen (TAB)
- Match gameplay: ball physics (incl. height/aerial), tackling, stamina, set pieces (throw-ins, corners,
  goal kicks, free kicks with a defensive wall, penalties with GK dive), fouls with yellow/red cards
  (ref walk-over cutscene, close-up camera, sad/surprised face), optional offsides (snapshot-on-kick,
  whistle-on-touch, linesman flag; setting default off), goal detection with crossbar/post ricochets,
  cloth nets; big center-screen banners for GOAL/FOUL/OFFSIDE/PENALTY
- Halves: teams switch sides at halftime, second-half kickoff goes to the other team,
  halftime substitution screen
- AI: utility-scoring brain (`UtilityAI/UtilityBrain`, the live path; legacy role FSM kept as fallback),
  passing/shooting/dribbling decisions, sideline avoidance.
  v2.18 additions: **ball-stall watchdog** (`MatchEngine.UpdateStallWatchdog` — a loose ball stationary
  2.5s+ forces the nearest players of BOTH teams to pounce via `AIContext.ForcedPounce`; kills idle
  dead zones), dribble dead-end detection + dribble-failure decay (no ball-hogging), offside awareness
  when enabled (`MatchEngine.WouldBeOffside` pass penalty + hold-the-line clamp in GetTacticalPoint),
  shot aim at far post with distance-scaled power, far-post cross runs.
  **GK**: near-post discipline (seal ball-side post by flankness), sweeper-keeper rushes through-balls
  in the box, cross claiming on descending aerials, forward distribution (`DecideGkDistribution` —
  open teammate first, punt to emptier flank when covered).
  **Officials**: single referee authority is renderer-side `MatchOfficials` (engine patrol deleted;
  engine keeps only the card-cutscene walk). Ref goes to foul spots, stands ~9m behind free kicks,
  behind the taker for penalties, sprints on breakaways. Linesmen track the offside line (second-last
  defender per half) with sprint + Wave on offside. Coaches react: Cheer/Hit_Reaction on goals,
  anger on cards against, pacing when losing late, directing when the ball is in their third
- 3D mode: skinned players (male + female bodies) with appearance variety (face expressions
  neutral/smile/sad/wow, facial hair, hair colors), per-team kits with back numbers, GK distinct kits,
  five venues selectable in Settings (+Random): Bahramis (fence, yellow-seat stand, scoreboard,
  trees, houses), Sperchogeia (olive grove ring, Taygetos backdrop, fence sponsor banners,
  floodlight pylons), seaside Sfageia (beach + sea with foam lines, rock breakwaters, palms,
  tennis courts, clubhouse), mountain Kerasoulia (dense conifer ring, Taygetos backdrop, tall fence,
  blue goal frames, covered stone stand, red running track + road, basketball hoop, standing
  terrace for the ultras) and village Soulinari (raised metal bleacher with standing fans,
  green container, changing-rooms building, olive grove west, red-roof village houses east,
  basketball court), fan banners on the fence (FREE PALESTINE etc.), animated fans
  (+ children, Palestinian flags; placement per venue via FanSection.FanPlacement), corner flags,
  rain, day/sunset/night,
  easter eggs (`EasterEggManager`, per-match rolls): fox (10%), ball-chasing dog (5% —
  `Dog.glb`, fox mesh with a repainted atlas; faster than players, dribbles the ball
  toward NO PASARAN's goal until one is scored, bark sample), crow flock (10%, craw.wav),
  seagull flock (50% at Sfageia, seagulls.wav), giant whirlwind (5% in rain — approaches
  from afar, roams the pitch all match, shoves players, whirlwind.wav), bees (5% — swarm
  harasses players, sting = 30s confusion wobble), fog (2% — Carpenter "The Fog" gray
  soup via BasicEffect fog), snow (5% in Dec/Jan/Feb — frosted pitch overlay, snowflakes,
  slippery `engine.IsSnowing` friction), Santa (5% in December — sleigh crossing the sky
  dropping gifts, gift hit = knockdown), piano (1% of penalties — falls on the taker,
  Looney Tunes flattening), thunder (1% in rain — lightning strikes a random outfield
  player: jagged bolt, knockdown, victim renders as charcoal (`Player.CharcoalRemaining`)
  and crumbles charcoal dust), beach ball (4% — drifts in, nudged around), sprinklers (3% —
  sweeping water jets ~15s), UFO (3% at night — saucer flyover that hovers over midfield
  with a rotating ring of blinking lights, then accelerates away, ufo.wav), floodlight
  blackout (2% at night — the lights flicker and die for 5-8s, then flicker back;
  `MatchEnvironment.BlackoutFactor`/`SetBlackout`, driven by `BlackoutFx`), cat invasion
  (3% — a clowder of 4-6 cats, fox mesh at cat size with repainted gray/black/ginger/cream
  atlases, mills about for ~a minute then leaves),
  celebration camera, goal replays (build-up at 1.4x from a high sideline camera, then the last 2s of
  footage in 0.5x slow motion from a goal-side camera with live cloth-net deformation, over the
  extended post-goal countdown, hold X to skip;
  recording in `Graphics3D/ReplayBuffer.cs`, playback in `MatchRenderer3D.DrawReplay`)
- 2D mode: sprite players with kit sheets, scrolling camera, minimap (minimap present in both modes)
- Local co-op: Player 2 can join (distinct indicators)
- Settings: video/audio/gameplay/camera/language, persisted; debug console for automation

## Next steps (candidates)

- AI balance/oscillation tuning follow-up (knobs: `AIConstants`, `UtilityTuning`, `PlayerAnimator`)
- More venues; venue selection per home team (venue is a Settings option since v2.6.0)
- Tournament mode, in-game substitutions (injuries), transfers/training
- Detailed match statistics
- Easter eggs beyond the fox (dog, crows, seagulls at Sfageia, tornado in rain — audio samples TBD)

Detailed design/fix documents live in the repo root (`AI_*.md`, `*_SYSTEM.md`, etc.).
