# NO PASARAN! Football Championship ⚽

A top-down 2D soccer game built with C# .NET and MonoGame, inspired by classic games like Sensible Soccer and Tecmo World Cup.

## 🎥 Gameplay Video

[![No Pasaran FC v1.0.4 Gameplay](https://img.youtube.com/vi/0NB9AkLI7O0/0.jpg)](https://www.youtube.com/watch?v=https://youtu.be/0NB9AkLI7O0)

*Watch gameplay footage from version 1.0.4 - Replace YOUR_VIDEO_ID with actual YouTube video ID*

## Game Overview

Manage and play as **NO PASARAN!** in an 8-team championship. Control one player at a time while AI manages your teammates and opponents. Features full roster management, animated sprites, audio system, and comprehensive match gameplay.

### Teams
- **NO PASARAN!** (Player-controlled)
- BARTSELIOMA
- KTEL
- NONAME
- MIHANIKOI
- ASALAGITOS
- ASTERAS EXARXION
- TIGANITIS

## ✨ Features

### 🎮 Core Gameplay
- **Championship Mode**: Full round-robin league season with all 8 teams
- **Top-down Match View**: Classic 2D scrolling football perspective (3200x2400 field)
- **Smooth Camera System**: Follows ball with zoom controls (0.5x-2.0x)
- **Strategic Minimap**: Shows entire field with player positions and camera viewport
- **Enhanced AI System**: 
  - Position-based behavior (Goalkeepers, Defenders, Midfielders, Forwards)
  - Intelligent goalkeeper positioning (moves to intercept shots)
  - Anti-clustering system (prevents player bunching)
  - Team-aware tackling (no friendly fire)
  - Smart ball control with directional awareness
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
  - Realistic goalposts with mesh netting
  - Stadium stands rendering
  - Single-player yellow selection indicator
- **Score Display**: Red text with yellow shadow for high visibility
- **Goal Celebration**: Dynamic ball-particle text formation system
- **Ball Depth Rendering**: Ball draws behind goalposts when scored
- **Localization**: Full Greek/English UI with UTF-8 encoding

### 🔊 Audio System
- **Music Tracks**: Menu, match, and victory music (looping)
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
- **Countdown System**: 3-2-1 countdown before kickoff ("ΠΑΜΕ!")
- **Single Whistle**: Match end whistle plays only once

## How to Run

### Prerequisites
- .NET 9.0 SDK (or .NET 8.0+)
- Windows, Linux, or macOS

### Build and Run

#### From Command Line (Recommended)
```bash
cd NoPasaranFC
dotnet build
dotnet run
```

#### From Visual Studio
1. Open `NoPasaranFC.csproj` in Visual Studio
2. Press F5 or click Run
3. **Note:** The game works fine in Visual Studio - it just doesn't show console debug output since it's configured as a Windows application (WinExe)

## 🎮 Controls

**Supports both Keyboard and Xbox-compatible GamePads!**

### Menu Navigation
| Action | Keyboard | GamePad |
|--------|----------|---------|
| Navigate | Up/Down Arrows | D-Pad / Left Stick |
| Confirm | Enter | A Button / Start |
| Back/Exit | Escape | B Button |

### Lineup Selection
| Action | Keyboard | GamePad |
|--------|----------|---------|
| Navigate | Up/Down Arrows | D-Pad / Left Stick |
| Toggle Starter | Space | X Button |
| Quick Scroll | Page Up/Down | — |
| Confirm Lineup | Enter | A Button |
| Cancel | Escape | B Button |

### During Match
| Action | Keyboard | GamePad |
|--------|----------|---------|
| Move Player | Arrow Keys / WASD | Left Stick / D-Pad |
| Shoot/Pass | X (tap/hold) | A Button (tap/hold) |
| Switch Player | Space | X Button |
| Pause/Exit | Escape | B Button |

### Settings Screen
| Action | Keyboard | GamePad |
|--------|----------|---------|
| Navigate | Up/Down Arrows | D-Pad / Left Stick |
| Adjust Values | Left/Right Arrows | — |
| Quick Scroll | Page Up/Down | — |
| Save | Enter | A Button |
| Cancel | Escape | B Button |

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

### Gameplay Systems
- **MatchEngine**: Ball physics, AI, collisions, goal detection
- **Camera**: Smooth scrolling with configurable zoom
- **Minimap**: Strategic overview of entire field
- **AudioManager**: Music and sound effect management
- **GoalCelebration**: Dynamic text rendering with ball particles
- **TeamSeeder**: JSON-based team/player loading

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
└── Game1.cs          # Main game loop
```

## 🔧 Technical Details

### Technology Stack
- **Framework**: .NET 9.0 (compatible with .NET 8.0+)
- **Game Engine**: MonoGame 3.8 (DesktopGL)
- **Database**: SQLite 9.0 (Microsoft.Data.Sqlite)
- **Graphics**: 2D sprite sheets with animation
- **Audio**: .wav (SFX), .mp3/.ogg (music)

### Performance
- **Field Size**: 3200x2400 pixels with 200px margins
- **Camera Viewport**: Configurable zoom (0.5x-2.0x)
- **Animation**: 8 FPS sprite animation with delta-time
- **Ball Physics**: 60 FPS physics with friction and gravity
- **AI Update**: Position-based behavior for all non-controlled players

### Asset Pipeline
- **Sprites**: 64x64 frames in 4x4 grids (256x256 total)
- **Ball**: 32x32 frames in 8x8 grid (64 frames)
- **Font**: Consolas Bold 24pt with Greek character support
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
    └── SettingsScreen.cs   # Options
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
- **SETTINGS_*.md**: Settings system documentation

## 🚀 Current Status (v1.0.4)

### What's New in v1.0.4
- 🤖 **Enhanced AI**: Intelligent goalkeeper positioning, anti-clustering, team-aware tackling
- ⚽ **Realistic Physics**: Goalpost ricochets (sides + crossbar), goal net collision, improved ball control
- 🎮 **Better Controls**: Dribbling power boost, kick cooldown system, single-player selection
- 🎨 **Visual Polish**: FIFA-accurate field lines, stamina bar thickness, high-visibility score display
- 🌍 **Full Localization**: Complete Greek/English translation including settings menu
- ⚙️ **Difficulty System**: Three difficulty levels affecting AI reaction and accuracy
- 💪 **Stamina System**: Players tire during matches, affecting performance
- 🕹️ **Gamepad Support**: Full Xbox-compatible controller integration
- 🎵 **Audio Improvements**: Fixed double whistle bug, improved kick sound timing
- 🔄 **Corner/Goal Kick Logic**: Proper last-touch detection for corner vs goal kick decisions

**Status:** Fully Playable! All core features implemented and polished:
- ✅ Championship mode with 8 teams
- ✅ Advanced match gameplay with enhanced AI
- ✅ Flexible rosters (11+) with pre-match lineup selection
- ✅ Database persistence with UTF-8 support
- ✅ Complete audio system (music + SFX with smart playback)
- ✅ Animated sprites with stamina visualization
- ✅ Comprehensive settings system (17 options)
- ✅ Full Greek/English localization
- ✅ Gamepad support (Xbox-compatible controllers)
- ✅ Difficulty levels with stamina system
- ✅ Realistic field dimensions and goalposts
- ✅ Corner/goal kick logic with last-touch detection
- ✅ Enhanced goalkeeper AI and anti-clustering system

## 🎯 Future Enhancements

Potential improvements for future versions:
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

## License

This is a game project. All rights reserved.
