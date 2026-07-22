# NO PASARAN! Football Championship ⚽

A top-down 2D soccer game built with C# .NET and MonoGame, inspired by classic games like Sensible Soccer and Tecmo World Cup.

**Now available on Windows, Linux, macOS, and Android!**

## 🎥 Gameplay Video

[![No Pasaran FC v1.0.4 Gameplay](https://img.youtube.com/vi/0NB9AkLI7O0/0.jpg)](https://www.youtube.com/watch?v=0NB9AkLI7O0)

*Watch gameplay footage from version 1.0.4*

## Game Overview

Manage and play as **NO PASARAN!** in an 8-team championship. Control one player at a time while AI manages your teammates and opponents. Features full roster management, animated sprites, audio system, and comprehensive match gameplay.

### Teams
- **NO PASARAN!** (Player-controlled)
- BARTSELIOMA (ΜΠΑΡΤΣΕΛΙΩΜΑ)
- KTEL (ΚΤΕΛ)
- NONAME
- CHANDRINAIKOS (ΧΑΝΔΡΙΝΑΪΚΟΣ)
- ASALAGITOS (ΑΣΑΛΑΓΗΤΟΣ)
- ASTERAS EXARXION (ΑΣΤΕΡΑΣ ΕΞΑΡΧΙΩΝ)
- TIGANITIS (ΤΗΓΑΝΙΤΗΣ)

## ✨ Features

### 🎮 Core Gameplay
- **Championship Mode**: Full round-robin league season with all 8 teams
- **Top-down Match View**: Classic 2D scrolling football perspective (3200x2400 field)
- **Smooth Camera System**: Follows ball with zoom controls (0.5x-2.0x)
- **Strategic Minimap**: Shows entire field with player positions and camera viewport
- **Advanced AI State Machine**: 
  - **Position-Aware Behavior**: Distinct roles for Goalkeepers, Defenders, Midfielders, and Forwards
  - **Dynamic Passing**: AI analyzes pass corridors and teammates' positions
  - **Aerial Passing**: Intelligent lofted passes to bypass defenders or switch play
  - **Smart Dribbling**: Direct movement with automatic ball kicking and shielding
  - **Defensive Coordination**: Team-aware pressing and goal protection
- **Match Simulation**: 
  - Full simulation of all non-player matches
  - Realistic results based on team strength stats
  - Detailed "Round Results" screen after every matchweek
- **Ball Physics**: Realistic velocity, friction, bouncing, and aerial trajectories
- **Tackle System**: Stat-based success probability (enemy players only)
- **Goal Detection**: Proper goal line crossing with mesh net visualization
  - Realistic goalposts with side and crossbar ricochets
  - Goal net back collision and ball depth rendering
  - Delayed celebration trigger for realistic scoring
- **Ball Out Handling**: Corner kicks, goal kicks, throw-ins with automatic positioning
  - Proper last-touch detection for corners vs goal kicks
- **Match Duration**: Configurable 1-10 minutes (default: 90 seconds game time)
- **Difficulty System**: Easy/Normal/Hard affects AI reaction speed and accuracy
- **Stamina System**: Players tire during match, affecting speed and performance

### 👥 Team & Player Management
- **Flexible Rosters**: Full squads with any number of players (minimum 11, no upper limit)
- **Lineup Selection**: Pre-match screen to choose your starting 11
  - Interactive formation preview (4-4-2)
  - Real-time validation with color-coded status
  - Scrollable player list with stats
  - Keyboard-friendly navigation with debouncing
  - ESC returns to menu without exiting game
- **Player Attributes**: Speed, Shooting, Passing, Defending, Agility, Technique, Stamina
- **Position System**: Goalkeeper, Defender, Midfielder, Forward
- **Shirt Numbers**: Each player has unique sequential number
- **JSON Seeding**: Load teams from `teams_seed.json` with UTF-8 support (any roster size)
- **Auto-Generation**: Teams without JSON data get procedurally generated players
- **Stamina Bars**: Visual indicators showing player fatigue (configurable thickness)

### 🎨 Graphics & Animation
- **Animated Sprites**: 4-directional player movement with 4-frame walking cycles
- **Sprite Sheets**: Separate home (blue) and away (red) team sprites
- **Ball Animation**: 64-frame rolling animation tied to velocity
- **Double-Scale Rendering**: Players at 128x128, ball at 32x32
- **Visual Effects**: 
  - Player shadows and stamina bars
  - FIFA-accurate field markings with proper line thickness
  - Realistic goalposts with mesh netting and wind animation
  - Stadium stands rendering
  - Single-player yellow selection indicator
- **Score Display**: Red text with yellow shadow for high visibility
- **Goal Celebration**: Dynamic ball-particle text formation system
- **Ball Depth Rendering**: Ball draws behind goalposts when scored
- **Localization**: Full Greek/English UI with UTF-8 encoding

### 🔊 Audio System
- **Music Tracks**: Menu, match, and victory music (looping)
No Pasaran main theme ("Εμπρός Νό Πασαράν!") by comrade Kyriakos
- **Sound Effects**: 
  - Menu navigation (move, select, back)
  - Match sounds (whistle start/end, kick, tackle, goal)
  - Crowd reactions (cheer)
- **Volume Controls**: Separate music and SFX sliders (0-100%)
- **Smart Playback**: 
  - Non-retriggerable sounds to prevent overlapping
  - Kick cooldown system (0.1s) prevents rapid-fire audio
  - Volume-based kick intensity
- **Mute Option**: Master audio toggle
- **Graceful Handling**: Missing audio files don't crash game

### ⚙️ Settings & Customization
- **Video Settings**: Resolution (800x600 to 1920x1080), Fullscreen, VSync
- **Audio Settings**: Master/Music/SFX volumes, Mute all
- **Gameplay Settings**: 
  - Difficulty (Easy/Normal/Hard) - affects AI behavior
  - Match duration (1-10 minutes)
  - Player speed multiplier (0.5x-2.0x)
- **Display Options**: Show/hide minimap, player names, stamina bars
- **Camera Settings**: 
  - Zoom level (0.5x-2.0x) - affects sprites and field view
  - Camera follow speed (0.05-0.5)
- **Language**: English/Greek (ελληνικά) - fully localized menus
- **Persistent Storage**: All settings saved to database

### 💾 Data Management
- **SQLite Database**: Automatic save/load for all game data
- **Persistent Rosters**: Teams with unlimited roster sizes saved
- **Match Results**: Complete match history and statistics
- **Championship Progress**: Current matchweek, standings, fixtures
- **Settings Persistence**: All configuration saved across sessions
- **New Season**: Reset championship while keeping teams

### 📊 Statistics & UI
- **League Standings**: Column-aligned table with wins, draws, losses, goals, points
- **Live Match HUD**: 
  - High-visibility red score display with yellow shadow
  - Match time and controls display
  - Stamina bars for all players (when enabled)
- **Formation Preview**: Visual representation in lineup screen with shirt numbers
- **Season Completion**: Indicator when all matches played
- **Final Score Overlay**: 5-second display after match ("ΤΕΛΙΚΟ ΣΚΟΡ")
- **Round Results**: Summary screen showing scores from all matches in the week
- **Countdown System**: 3-2-1 countdown before kickoff ("ΠΑΜΕ!")
- **Single Whistle**: Match end whistle plays only once

## How to Run

### Prerequisites

#### Desktop (Windows/Linux/macOS)
- .NET 9.0 SDK (or .NET 8.0+)

#### Android
- .NET 9.0 SDK with Android workload
- Android SDK (included with Visual Studio or Android Studio)
- Android device with USB debugging enabled, or Android emulator

### Build and Run

#### Desktop - From Command Line (Recommended)
```bash
cd NoPasaranFC
dotnet build
dotnet run
```

#### Desktop - From Visual Studio
1. Open `NoPasaranFC.sln` in Visual Studio
2. Set `NoPasaranFC` as startup project
3. Press F5 or click Run

#### Android - Build and Deploy
```powershell
# Clean build and deploy to connected device
.\clean-and-build-android.ps1
```

Or manually:
```bash
dotnet build NoPasaranFC.Android\NoPasaranFC.Android.csproj -t:Install -c Debug
```

#### Android - Build APK for Distribution
```powershell
# Creates NoPasaranFC.apk in project root
.\build-apk.ps1
```

The APK can be sideloaded onto any Android device (enable "Install from unknown sources" in device settings).

## 🎮 Controls

**Supports Keyboard, Xbox-compatible GamePads, and Touch Controls (Android)!**

### Menu Navigation
| Action | Keyboard | GamePad | Touch (Android) |
|--------|----------|---------|-----------------|
| Navigate | Up/Down Arrows | D-Pad / Left Stick | Virtual Joystick |
| Confirm | Enter | A Button / Start | A Button |
| Back/Exit | Escape | B Button | B Button |

### Lineup Selection
| Action | Keyboard | GamePad | Touch (Android) |
|--------|----------|---------|-----------------|
| Navigate | Up/Down Arrows | D-Pad / Left Stick | Virtual Joystick |
| Toggle Starter | Space | X Button | X Button |
| Quick Scroll | Page Up/Down | — | — |
| Confirm Lineup | Enter | A Button | A Button |
| Cancel | Escape | B Button | B Button |

### During Match
| Action | Keyboard | GamePad | Touch (Android) |
|--------|----------|---------|-----------------|
| Move Player | Arrow Keys / WASD | Left Stick / D-Pad | Virtual Joystick |
| Shoot/Pass | X (tap/hold) | A Button (tap/hold) | A Button |
| Switch Player | Space | X Button | X Button |
| Pause/Exit | Escape | B Button | B Button |

### Settings Screen
| Action | Keyboard | GamePad | Touch (Android) |
|--------|----------|---------|-----------------|
| Navigate | Up/Down Arrows | D-Pad / Left Stick | Virtual Joystick |
| Adjust Values | Left/Right Arrows | — | Joystick Left/Right |
| Quick Scroll | Page Up/Down | — | — |
| Save | Enter | A Button | A Button |
| Cancel | Escape | B Button | B Button |

See **GAMEPAD_SUPPORT.md** for detailed controller information.

## 📁 Game Structure

### Models
- **Player**: Attributes, position, shirt number, starting status, animation state
- **Team**: 22-player roster, championship statistics, player-controlled flag
- **Match**: Fixtures, scores, played status
- **Championship**: League management, fixtures, standings
- **GameSettings**: Video, audio, gameplay, camera, language settings

### Screens
- **MenuScreen**: Main navigation with Greek language support
- **LineupScreen**: Pre-match starting XI selection
- **StandingsScreen**: League table with detailed statistics
- **MatchScreen**: Playable matches with scrolling camera
- **SettingsScreen**: Comprehensive configuration options
- **RoundResultsScreen**: Matchweek summary display

### Gameplay Systems
- **MatchEngine**: Ball physics, AI, collisions, goal detection
- **Camera**: Smooth scrolling with configurable zoom
- **Minimap**: Strategic overview of entire field
- **AudioManager**: Music and sound effect management
- **GoalCelebration**: Dynamic text rendering with ball particles
- **TeamSeeder**: JSON-based team/player loading
- **MatchSimulator**: Simulates results for non-player matches

### Database
- **File**: `nopasaran.db` (SQLite)
- **Tables**: Teams, Players, Matches, Championship, Settings
- **Features**: Auto-save/load, UTF-8 encoding, foreign key constraints
- **Schema**: Supports flexible roster sizes, starting lineups, shirt numbers

## Project Structure

```
NoPasaranFC/
├── Models/           # Game data models
├── Database/         # SQLite persistence layer
├── Gameplay/         # Match engine and game logic
├── Screens/          # UI screens and navigation
├── Content/          # Game assets (fonts, sprites)
├── Game1.cs          # Main game loop
└── NoPasaranFC.Android/  # Android-specific project
    ├── Activity1.cs      # Android main activity
    └── AndroidManifest.xml
```

## 🔧 Technical Details

### Technology Stack
- **Framework**: .NET 9.0 (compatible with .NET 8.0+)
- **Game Engine**: MonoGame 3.8 (DesktopGL for desktop, Android for mobile)
- **Database**: SQLite 9.0 (Microsoft.Data.Sqlite)
- **Graphics**: 2D sprite sheets with animation
- **Audio**: .wav (SFX), .mp3/.ogg (music)
- **Platforms**: Windows, Linux, macOS, Android

### Android-Specific Features
- **Touch Controls**: Virtual joystick and buttons (A, B, X)
- **Adaptive UI**: Scaled for different screen densities
- **Platform-Aware Settings**: Android settings screen excludes desktop-only options (Resolution, Fullscreen, VSync)
- **Database Path**: Uses Android's internal storage for SQLite database
- **Immersive Mode**: Full-screen experience with hidden system bars

### Performance
- **Field Size**: 3200x2400 pixels with 200px margins
- **Camera Viewport**: Configurable zoom (0.5x-2.0x)
- **Animation**: 8 FPS sprite animation with delta-time
- **Ball Physics**: 60 FPS physics with friction and gravity
- **AI Update**: Position-based behavior for all non-controlled players

### Asset Pipeline
- **Sprites**: 64x64 frames in 4x4 grids (256x256 total)
- **Ball**: 32x32 frames in 8x8 grid (64 frames)
- **Font**: Inconsolata LGC Bold 24pt with Greek character support
- **Content Build**: MonoGame Content Pipeline (.mgcb)

### File Structure
```
NoPasaranFC/
├── Content/
│   ├── Audio/
│   │   ├── Music/          # .mp3 music files
│   │   └── SFX/            # .wav sound effects
│   ├── Sprites/            # Player and ball sprite sheets
│   └── Font.spritefont     # UI font with Greek support
├── Database/
│   ├── DatabaseManager.cs  # SQLite persistence
│   ├── TeamSeeder.cs       # JSON loading system
│   └── teams_seed.json     # Team/player data
├── Gameplay/
│   ├── MatchEngine.cs      # Core match logic
│   ├── Camera.cs           # Scrolling camera
│   ├── AudioManager.cs     # Sound management
│   └── GoalCelebration.cs  # Goal effects
├── Models/
│   ├── Player.cs           # Player data & animation
│   ├── Team.cs             # Team & roster
│   ├── Championship.cs     # League management
│   └── GameSettings.cs     # Configuration
└── Screens/
    ├── MenuScreen.cs       # Main menu
    ├── LineupScreen.cs     # Squad selection
    ├── MatchScreen.cs      # Match gameplay
    ├── StandingsScreen.cs  # League table
    ├── SettingsScreen.cs   # Options
    └── RoundResultsScreen.cs # Matchweek summary
```

## 📝 Documentation

- **AGENTS.md**: Complete development history and feature list
- **ROSTER_SYSTEM.md**: Team and player management guide
- **LINEUP_SCREEN.md**: Lineup selection screen documentation
- **AUDIO_SYSTEM.md**: Audio implementation details
- **SPRITE_GUIDE.md**: Sprite asset creation guide
- **FONT_CHARACTER_SUPPORT.md**: Font configuration reference
- **GOAL_CELEBRATION_SYSTEM.md**: Goal celebration mechanics
- **BALL_OUT_SYSTEM.md**: Ball out-of-bounds handling
- **DIFFICULTY_STAMINA_SYSTEM.md**: Difficulty and stamina mechanics
- **LOCALIZATION.md**: Translation and language system
- **GAMEPAD_SUPPORT.md**: Controller configuration guide
- **AI_*.md**: Comprehensive AI documentation (Passing, Positioning, State Machine)
- **SETTINGS_*.md**: Settings system documentation

## 🚀 Current Status (v1.2.0)

### What's New in v1.2.0
- 📱 **Android Support**: Full mobile port with touch controls
- 🕹️ **Virtual Controls**: Joystick and buttons optimized for touchscreen
- 📐 **Adaptive UI**: Automatic scaling for different screen sizes
- ⚙️ **Platform-Aware Settings**: Android excludes desktop-only options
- 🎬 **Animation Fixes**: Resolved stuck tackle animation issue
- 🗃️ **Database Improvements**: Platform-specific storage paths

### What's New in v1.1.0
- 🧠 **Advanced AI State Machine**: Complete overhaul of AI with position-aware roles (GK/DEF/MID/FWD)
- ✈️ **Aerial Passing**: Intelligent lofted passes to switch play and bypass defenders
- 🏟️ **Match Simulation**: Full simulation of league matches with realistic results
- 📊 **Round Results**: New screen showing scores from all matches in the week
- 🥅 **Dynamic Goal Nets**: Physics-based nets that react to ball impact and wind
- 👟 **Improved Dribbling**: Smoother ball control with automatic kicking
- 🛡️ **Defensive Tactics**: Team-aware pressing and emergency goal protection
- 🔤 **Font Update**: Switched to Inconsolata LGC for better cross-platform support

### Status
**Fully Playable on Desktop and Android!** All core features implemented and polished:
- ✅ Championship mode with 8 teams and full season tracking
- ✅ Advanced match gameplay with state-machine AI
- ✅ Flexible rosters (11+) with pre-match lineup selection
- ✅ Database persistence with UTF-8 support
- ✅ Complete audio system (music + SFX with smart playback)
- ✅ Animated sprites with stamina visualization
- ✅ Comprehensive settings system (platform-aware)
- ✅ Full Greek/English localization
- ✅ Gamepad support (Xbox-compatible controllers)
- ✅ Touch controls for Android
- ✅ Difficulty levels with stamina system
- ✅ Realistic field dimensions and goalposts
- ✅ Corner/goal kick logic with last-touch detection

## 🎯 Future Enhancements

Potential improvements for future versions:
- [ ] iOS support
- [ ] Substitution system during matches
- [ ] Fouls and yellow/red cards
- [ ] Offsides detection
- [ ] Advanced formations (4-3-3, 3-5-2, etc.)
- [ ] Player transfers and training modes
- [ ] Tournament/knockout competition modes
- [ ] Local multiplayer (2-player matches)
- [ ] Match replays and highlights system
- [ ] Weather effects (rain, snow)
- [ ] Custom team creation and editing
- [ ] Advanced AI tactics and strategies
- [ ] Player morale and form system

See **AGENTS.md** for complete development roadmap.

## 👥 Credits

**Engineering**
- [tkleisas](https://github.com/tkleisas) — project creator & lead developer
- Stathis — goal celebration system
- [Kimi](https://www.kimi.com/code) (AI coding agent) — 3D match view, skinned animation system, Blender asset pipeline, debug tooling

**Assets**
- [KayKit](https://kaylousberg.com) (CC0) — character skeleton & animation library
- [Khronos glTF Sample Models](https://github.com/KhronosGroup/glTF-Sample-Models) — the stadium fox
- Players, ball, and venue assets generated with [Blender](https://www.blender.org) via the blender-mcp pipeline

## License

This game is provided under an MIT License. The license text can be found in LICENSE.txt
 
