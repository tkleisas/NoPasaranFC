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
dotnet build NoPasaranFC.Android/NoPasaranFC.Android.csproj  # Android (needs android workload)
```

- Do NOT build the solution file locally without the Android workload installed — build projects individually.
- Desktop DB lives at `bin/Debug/net9.0/nopasaran.db` (settings + championship, quick to inspect with sqlite3/python).
- Content: XNB files are pre-built with the MGCB CLI and copied to output; raw assets (GLB/PNG) in `Content/Models3D/` are loaded at runtime (no pipeline).

## Debug tooling (use it for verification!)

- **Debug TCP console**: launch with `NOPASARAN_DEBUG=1` (port via `NOPASARAN_DEBUG_PORT`, default 7777).
  Commands: `shot <path> [delayFrames]` (screenshot), `key|down|up <Keys name>` (inject input),
  `state` (screen, fps, match + animation census), `match` (jump to next match), `quit`.
  Client: `python3 Scripts/dbg.py "state" "shot /tmp/x.png 3"`.
- **Blender pipeline**: `python3 Scripts/blender_exec.py <script.py>` runs a Python script inside a
  running Blender instance (blender-mcp addon on 127.0.0.1:9876). Asset sources: `Content/Models3D/*.blend`.
- **Spike**: `Spikes/SkinnedSpike` loads any skinned GLB standalone:
  `SPIKE_SHOT=/tmp/x.png SPIKE_CLIP=Running_A dotnet run --no-build -- model.glb`

## Project layout

- `Models/` — Player, Team, Match, Championship, GameSettings, Localization, Version
- `Database/` — SQLite manager + JSON seeders (`teams_seed.json`, `championships_seed.json`)
- `Gameplay/` — MatchEngine (simulation, no drawing!), AI states (`AIStates/`), Camera (2D), Minimap, audio
- `Graphics3D/` — the 3D renderer: Camera3D, World3D (venue geometry), Ball3D, MatchRenderer3D,
  PlayerAnimator, FanSection, FoxWalker, GoalNet3D, MatchEnvironment (lighting/weather), RainSystem,
  KitTextureFactory, `Skinning/` (GLB loader + skinned playback, SharpGLTF + SkinnedEffect)
- `Screens/` — Screen system: Menu, Match, Lineup, Standings, Settings, RoundResults, etc.
- `Debugging/` — DebugInput (input seam), DebugServer (TCP), ScreenCapture
- `Content/Models3D/` — GLB models + atlases + `.blend` sources (Player, PlayerF, Knight, Rogue, Fox, SoccerBall)

## Conventions

- **World scale**: 73 px = 1 meter everywhere (engine px ↔ 3D meters via `Graphics3D/WorldUnits.cs`).
- **Sim/render split**: `MatchEngine` is pure simulation (Vector2, ball height simulated separately).
  Renderers read engine state; never the reverse. Keep it that way.
- **Additive 3D**: the 2D mode must keep working; new 3D features go in `Graphics3D/`, minimal seams in `MatchScreen`.
- **Settings**: add to `GameSettings` + a numbered migration in `DatabaseManager` + a `SettingsScreen`
  row + `Localization` keys (en + el). Defaults apply to fresh installs only.
- **Kits**: team shirt/shorts/socks via `KitTextureFactory` (region recolor of the player atlas,
  luminance-normalized). Kit colors live in `MatchRenderer3D.GetKitColors`.
- **Animations**: KayKit clips on all humanoids (same skeleton). State→clip mapping in `PlayerAnimator`.
- **Anti-oscillation**: AI uses target inertia + start/stop hysteresis (`AIConstants`), animations use
  hysteresis (`PlayerAnimator`). Don't reintroduce raw per-frame target/state flipping.

## Feature summary

- Championship: round-robin fixtures, match simulation for non-player matches, standings, round results, seasons
- Match gameplay: ball physics (incl. height/aerial), tackling, stamina, set pieces (throw-ins, corners,
  goal kicks with charge aiming), goal detection with crossbar/post ricochets, cloth nets
- AI: role-based states (GK/DEF/MID/FWD), passing/shooting/dribbling decisions, sideline avoidance
- 3D mode: skinned players (male + female bodies), per-team kits with back numbers, GK distinct kits,
  Bahramis venue (fence, yellow-seat stand, scoreboard, trees, houses), animated fans (+ children,
  Palestinian flags), corner flags, easter-egg fox, rain, day/sunset/night, celebration camera
- 2D mode: sprite players with kit sheets, scrolling camera, minimap (minimap present in both modes)
- Local co-op: Player 2 can join (distinct indicators)
- Settings: video/audio/gameplay/camera/language, persisted; debug console for automation

## Next steps (candidates)

- Penalty kicks (needs foul system)
- AI balance/oscillation tuning follow-up (knobs: `AIConstants`, `PlayerAnimator`)
- More venues; venue selection per home team
- Tournament mode, substitutions, transfers/training
- Detailed match statistics, replays

Detailed design/fix documents live in the repo root (`AI_*.md`, `*_SYSTEM.md`, etc.).
