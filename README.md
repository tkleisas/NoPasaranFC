# NO PASARAN! Football Championship ⚽

A soccer game built with C# (.NET 9) and MonoGame 3.8, inspired by classics like Sensible Soccer — with a full **3D match view**: rigged, animated players, real venues, day/night, weather, and TV-style goal replays.

**Available on Windows, Linux, macOS, and Android!**

![NO PASARAN! menu](docs/screenshots/menu.png)

## Game Overview

Manage and play as **NO PASARAN!** in an 8-team championship. Control one player at a time while AI manages your teammates and opponents. Two match view modes, selectable in Settings:

- **3D** (default): perspective 3D view with skinned, animated players, municipal stadiums, animated fans, day/sunset/night lighting, rain, and slow-motion goal replays.
- **2D**: the original top-down sprite view (Sensible Soccer style), fully preserved.

### Teams
- **NO PASARAN!** (Player-controlled)
- BARTSELIOMA (ΜΠΑΡΤΣΕΛΙΩΜΑ)
- KTEL (ΚΤΕΛ)
- NONAME
- CHANDRINAIKOS (ΧΑΝΔΡΙΝΑΪΚΟΣ)
- ASALAGITOS (ΑΣΑΛΑΓΗΤΟΣ)
- ASTERAS EXARXION (ΑΣΤΕΡΑΣ ΕΞΑΡΧΙΩΝ)
- TIGANITIS (ΤΗΓΑΝΙΤΗΣ)

## 📸 Screenshots

### 3D mode — ΓΗΠΕΔΟ ΣΠΕΡΧΟΓΕΙΑΣ (Sperchogeia)
Olive grove ring, Taygetos backdrop, fence sponsor banners, floodlight pylons:

![Sperchogeia venue by day](docs/screenshots/sperchogeia_day.png)

Night match in the rain:

![Night rain match](docs/screenshots/night_rain.png)

### 3D mode — Παναγιώτης Μπαχράμης (Bahramis)
Yellow-seat stand with animated NO PASARAN! supporters waving Palestinian flags:

![Bahramis venue](docs/screenshots/bahramis.png)

### 3D mode — Γήπεδο Σφαγείων (Sfageia)
Seaside football: beach and sea behind the fence, palms, and the supporters' banners:

![Sfageia venue](docs/screenshots/sfageia.png)

### Goal replays
Every goal is re-shown over the post-goal countdown: the build-up at 1.4x from a high sideline camera, then the payoff in 0.5x **slow motion** from a goal-side camera — with the cloth net deforming around the ball, exactly like the live goal. Hold X to skip.

![Replay build-up, high sideline camera](docs/screenshots/replay_buildup.png)
![Replay slow-motion payoff with deforming net](docs/screenshots/replay_slowmo.png)

### Goal celebrations
Scoring teams celebrate with distinct choreographed routines, the camera follows them, and the fans go wild:

![Goal celebration](docs/screenshots/goal_celebration.png)

### Camera modes & the classic 2D view
Broadcast / High / TopDown cameras in 3D, plus the original 2D sprite mode:

![TopDown 3D camera](docs/screenshots/topdown_3d.png)
![Classic 2D mode](docs/screenshots/mode_2d.png)

## ✨ Features

### 🎮 Core Gameplay
- **Championship Mode**: Full round-robin league season with all 8 teams, standings, round results, seasons
- **Match Statistics**: Detailed per-player and per-team stats (goals, assists, shots, on target, passes and accuracy, tackles, fouls, saves, cards, possession, corners, offsides) with a post-match screen; season-long top scorers with assists and cards (TAB in Standings) — simulated matches attribute goals too
- **Two view modes**: full 3D (default) and classic top-down 2D, selectable in Settings
- **Ball Physics**: Velocity, friction, bouncing, and aerial trajectories (height simulated separately)
- **Tackle System**: Stat-based success probability with knockdowns
- **Fouls, Cards & Penalties**: Referee whistles fouls (free kick with a defensive wall, or a penalty inside the box), books players with yellow/red cards — with a walk-over cutscene, close-up camera and the offender's sad/surprised face
- **Offsides** (optional, default off): snapshot on the pass, whistle on the touch, linesman raises the flag
- **Real halves**: teams switch sides at halftime, the second-half kickoff goes to the other team, and halftime brings a substitution screen
- **Event banners**: big center-screen GOAL / FOUL / OFFSIDE / PENALTY callouts (localized)
- **Goal Detection**: Goal-line crossing with crossbar/post ricochets
- **Cloth Nets**: Physics-based goal nets that deform on ball impact and sway in the wind
- **Set Pieces**: Throw-ins, corner kicks, goal kicks, free kicks and penalties with charge aiming; proper last-touch detection
- **Match Simulation**: Realistic simulation of all non-player matches based on team strength
- **Stamina System**: Players tire during the match, affecting speed and performance
- **Ball Control**: Easy (glued dribbling with skill-based miscontrols) or Classic, same rules for human and AI
- **Difficulty System**: Easy/Normal/Hard affects AI reaction speed and accuracy
- **Match Duration**: Configurable 1-10 minutes
- **Local Co-op**: Player 2 can join mid-match (distinct indicators)

### 🏟️ 3D Mode
- **Rigged, animated players**: skinned GLB models (male + female bodies) with the KayKit clip library — running, walking, tackling, celebrations, knockdowns
- **Per-team kits**: shirt/shorts/socks recolored from the player atlas (luminance-normalized), back numbers, distinct goalkeeper kits
- **Appearance variety**: face expressions (neutral/smile/sad/wow — the booked player looks the part), facial hair, hair colors, male + female bodies
- **Five venues + Random**, selectable in Settings:
  - **Παναγιώτης Μπαχράμης** — municipal ground with chain-link fence, yellow bucket-seat stand, scoreboard arch, trees and houses
  - **ΓΗΠΕΔΟ ΣΠΕΡΧΟΓΕΙΑΣ** — rural ground in an olive grove with the Taygetos ridge behind, sponsor banners on the fence, floodlight pylons, dirt road
  - **Γήπεδο Σφαγείων** — seaside ground: beach and sea with foam lines, rock breakwaters, palms, tennis courts, clubhouse
  - **Στάδιο Κερασούλιας** — mountain ground on Taygetos: dense conifer ring, tall fence, blue goal frames, covered stone stand, red running track, basketball hoop, standing terrace for the ultras
  - **Γήπεδο Σουληναρίου** — village ground near Pylos: raised metal bleacher with standing fans, green container, changing-rooms building, olive grove, red-roof village houses, basketball court
- **Animated fans**: adults and children in team colors, seated and standing, waving Palestinian flags; they celebrate goals; NO PASARAN! supporters' banners on the fence (FREE PALESTINE, ΛΕΥΤΕΡΙΑ ΣΤΗ ΠΑΛΑΙΣΤΙΝΗ, ΤΕΜΠΗ - ΠΥΛΟΣ - ΠΑΛΑΙΣΤΙΝΗ, ΔΙΚΑΙΩΣΗ ΓΙΑ ΤΟ ΘΟΔΩΡΗ)
- **Match atmosphere**: team benches with substitutes and animated coaches directing play, referee and linesmen, corner flags
- **Easter eggs** (rolled per match): a wandering fox (10%), a ball-chasing dog that's faster than the players and dribbles the ball toward NO PASARAN's goal until it scores (5%, with barking), crow flocks (10%), seagulls at Sfageia (50%), and a giant whirlwind in the rain (5%) that roams the pitch all match and shoves players around
- **Day/Sunset/Night + weather**: clear or rain (random by default), floodlights at night, environment-aware lighting on every object
- **Goal replays**: two-angle replay (high sideline build-up → slow-motion goal-side payoff) with cloth-net deformation, skippable
- **Celebration camera**: follows the celebrating players after every goal
- **Camera modes**: Broadcast / High / TopDown, with configurable zoom and follow speed

### 🧠 AI
- **Role-based behavior**: distinct logic for Goalkeepers, Defenders, Midfielders, Forwards
- **Dynamic passing**: pass corridor analysis, aerial passes to switch play, forward-progress bias
- **Smart dribbling**: ball shielding, sideline-aware attacking runs
- **Defensive coordination**: team-aware pressing and emergency goal protection
- **Anti-oscillation**: target inertia + start/stop hysteresis (no jittery state flipping)
- **Tuneable decision interval** (0.1-0.5s) in Settings

### 👥 Team & Player Management
- **Player & Kit Editor** (desktop): launch with `--editor` to open the editor for **both championships** — edit player stats, gender, skin tone, hair color, face expression and features; design kits (shirt/shorts/socks + goalkeeper colors), pick shirt patterns (stripes, hoops, halves, sash), and **paint directly on the shirt** on a 32×32 grid. Edits write through to `teams_seed.custom.json` (auto-merged as the new defaults) and the live database
- **Flexible Rosters**: Full squads (minimum 11, no upper limit)
- **Lineup Selection**: Pre-match screen with formation preview and stat display
- **Player Attributes**: Speed, Shooting, Passing, Defending, Agility, Technique, Stamina
- **In-game stat editing** via the debug console for gameplay experiments
- **JSON Seeding**: Load teams from `teams_seed.json` with UTF-8 support

### 🔊 Audio
- **Music**: Menu, match, and victory tracks — No Pasaran main theme ("Εμπρός Νό Πασαράν!") by comrade Kyriakos
- **Sound Effects**: Whistles, kicks, tackles, goals, crowd cheers
- **Volume Controls**: Separate music/SFX sliders, master mute

### ⚙️ Settings (all persisted)
- **Video**: Resolution, Fullscreen, VSync
- **Audio**: Master/Music/SFX volumes, mute
- **Gameplay**: Difficulty, match duration, player speed (0.5x-4.0x), AI decision interval, ball control (Easy/Classic), offsides (on/off)
- **Display**: Minimap, player names, stamina bars
- **View**: 3D/2D mode, camera mode (Broadcast/High/TopDown), zoom, follow speed
- **Atmosphere**: Venue (Bahramis/Sperchogeia/Sfageia/Random), time of day (Day/Sunset/Night/Random), weather (Clear/Rain/Random)
- **Language**: English / Ελληνικά (Greek default on first run)

### 💾 Data
- **SQLite persistence**: rosters, fixtures, results, standings, settings
- **Schema migrations**: settings DB upgrades automatically on update
- **New Season**: reset the championship while keeping teams

## How to Run

### Prerequisites
- **Desktop** (Windows/Linux/macOS): .NET 9.0 SDK
- **Android**: .NET 9.0 SDK with the Android workload, Android SDK, device or emulator

### Desktop
```bash
dotnet build NoPasaranFC.csproj
dotnet run --project NoPasaranFC.csproj
```
> Build the project, not the solution, unless you have the Android workload installed.

### Android
```bash
dotnet build NoPasaranFC.Android/NoPasaranFC.Android.csproj -t:Install -c Debug
```
Or use `clean-and-build-android.ps1` / `build-apk.ps1` on Windows. The APK can be sideloaded onto any Android device.

## 🎮 Controls

**Keyboard, Xbox-compatible gamepads, and touch (Android) are supported.**

### Menus
| Action | Keyboard | GamePad | Touch |
|--------|----------|---------|-------|
| Navigate | Up/Down Arrows | D-Pad / Left Stick | Virtual Joystick |
| Confirm | Enter | A / Start | A Button |
| Back | Escape | B | B Button |

### During Match
| Action | Keyboard | GamePad | Touch |
|--------|----------|---------|-------|
| Move Player | Arrow Keys | Left Stick / D-Pad | Virtual Joystick |
| Shoot (hold to charge) | X | A | A Button |
| Switch Player | Space | X | X Button |
| Skip celebration / replay | X (after 5s) | A | A Button |
| Pause/Exit | Escape | B | B Button |
| Player 2 join (local co-op) | Right Shift / Right Alt | — | — |

See **GAMEPAD_SUPPORT.md** for controller details.

## 📁 Project Structure

```
NoPasaranFC/
├── Models/             # Player, Team, Match, Championship, GameSettings, Localization, Version
├── Database/           # SQLite manager + JSON seeders
├── Gameplay/           # MatchEngine (pure simulation!), AI (UtilityAI/), celebrations, 2D camera, audio
├── Graphics3D/         # 3D renderer: Camera3D, World3D (venues), Ball3D, MatchRenderer3D,
│                       # PlayerAnimator, GoalNet3D, FanSection, TeamBench, MatchOfficials,
│                       # ReplayBuffer, MatchEnvironment, RainSystem, Skinning/ (GLB loader)
├── Screens/            # Menu, Match, Lineup, Standings, Settings, RoundResults, ...
├── Debugging/          # Debug TCP console, input seam, screen capture
├── Harness/            # Headless deterministic match simulation for AI evaluation
├── Content/            # Fonts, sprites, audio, Models3D/ (GLB + .blend sources)
└── NoPasaranFC.Android/
```

Key architectural rule: `MatchEngine` is pure 2D simulation (73 px = 1 m). The 3D renderer only reads engine state — never the reverse.

## 🔧 Debug Tooling

- **Debug TCP console** (`NOPASARAN_DEBUG=1`): screenshots, input injection, state dumps, ball/player teleports (`ball`, `ppos`), forced kicks/touches (`kick`, `touch`), staged scenarios (`corner`, `freekick`, `penalty`, `card`, `halftime`), match-clock freeze (`hold`), player stat editing, match jumping. Client: `python3 Scripts/dbg.py "state" "shot /tmp/x.png 3"`
- **AI harness**: `dotnet run --project NoPasaranFC.csproj -- harness <scenario> --seconds N --seed 42 --out <prefix>` — headless deterministic matches with per-frame logs and trajectory plots (`Scripts/trajectory_plot.py`)
- **Blender pipeline**: `python3 Scripts/blender_exec.py <script.py>` runs scripts inside a running Blender (blender-mcp) for asset authoring

## 📝 Documentation

- **AGENTS.md**: project conventions, architecture rules, feature summary
- **GOAL_CELEBRATION_SYSTEM.md**, **BALL_OUT_SYSTEM.md**, **DIFFICULTY_STAMINA_SYSTEM.md**, **LOCALIZATION.md**, **GAMEPAD_SUPPORT.md**
- **AUDIO_SYSTEM.md**, **LINEUP_SCREEN.md**, **ROSTER_SYSTEM.md**, **SPRITE_GUIDE.md**, **SETTINGS_USAGE.md**, **FONT_CHARACTER_SUPPORT.md**
- **docs/param-search-v3/**, **docs/param-search-v4/**: AI evolution-strategy methodology, search logs and findings
- **docs/screenshots/**: README captures; **docs/harness-*/**: AI harness trajectory studies
- Older design/fix notes live in git history

## 🚀 Current Status (v2.13.0)

**Fully playable on desktop and Android**, with the 3D view as the default experience. 60+ automated tests guard the match engine.

Recent highlights:
- **v2.13.0**: Offsides (optional, linesman flag), teams switch sides at halftime (+ second-half kickoff to the other team), big GOAL/FOUL/OFFSIDE/PENALTY banners, card cutscene (ref walk-over, close-up, sad/surprised face), celebration attribution fix
- **v2.12.0**: Fouls, free kicks with defensive walls, yellow/red cards, penalty kicks with GK dive
- **v2.11.0**: Halftime substitutions, appearance system (expressions, facial hair), test suite
- **v2.10.0**: Seaside Γήπεδο Σφαγείων venue, Random venue option, fan banners
- **v2.9.1**: Real goalkeepers — shot dives, angle play, distribution
- **v2.8.0**: ES-tuned attacking AI + offline parameter search
- **v2.7.0**: Ball Control setting (Easy/Classic)
- **v2.6.0**: ΓΗΠΕΔΟ ΣΠΕΡΧΟΓΕΙΑΣ venue; two-angle slow-motion goal replays with deforming nets
- **v2.0.0**: Full 3D match view — skinned players, kits, Bahramis venue, fans, day/night, weather, celebrations
- **v1.2.0**: Android port with touch controls

## 🎯 Future Enhancements

- [ ] More venues; venue selection per home team
- [ ] In-game substitutions (injuries), transfers/training
- [ ] Tournament/knockout modes
- [ ] Detailed match statistics
- [ ] More easter eggs (dog, crows, seagulls at Sfageia, tornado in rain)
- [ ] iOS support

## 👥 Credits

**Engineering**
- [tkleisas](https://github.com/tkleisas) — project creator & lead developer
- Stathis — goal celebration system
- [Kimi](https://www.kimi.com/code) (AI coding agent) — 3D match view, venues, skinned animation, replays, Blender asset pipeline, debug tooling

**Assets**
- [KayKit](https://kaylousberg.com) (CC0) — character skeleton & animation library
- [Khronos glTF Sample Models](https://github.com/KhronosGroup/glTF-Sample-Models) — the stadium fox
- Players, ball, and venue assets generated with [Blender](https://www.blender.org) via the blender-mcp pipeline

## License

This game is provided under an MIT License. The license text can be found in LICENSE.txt
