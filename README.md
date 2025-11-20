# NO PASARAN! Football Championship ⚽

A top-down 2D soccer game built with C# .NET and MonoGame, inspired by classic games like Sensible Soccer and Tecmo World Cup.

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
- **Position-based AI**: Goalkeepers, defenders, midfielders, and forwards behave differently
- **Ball Physics**: Realistic velocity, friction, bouncing, and aerial trajectories
- **Tackle System**: Stat-based success probability
- **Goal Detection**: Proper goal line crossing with mesh net visualization
- **Ball Out Handling**: Corner kicks, goal kicks, throw-ins with automatic positioning
- **Match Duration**: Configurable 1-10 minutes (default: 90 seconds game time)

### 👥 Team & Player Management
- **Flexible Rosters**: Full squads with any number of players (minimum 11, default 25)
- **Lineup Selection**: Pre-match screen to choose your starting 11
  - Interactive formation preview (4-4-2)
  - Real-time validation
  - Scrollable player list with stats
- **Player Attributes**: Speed, Shooting, Passing, Defending, Agility, Technique, Stamina
- **Position System**: Goalkeeper, Defender, Midfielder, Forward
- **Shirt Numbers**: Each player has unique sequential number
- **JSON Seeding**: Load teams from `teams_seed.json` with UTF-8 support (any roster size)
- **Auto-Generation**: Teams without JSON data get procedurally generated players

### 🎨 Graphics & Animation
- **Animated Sprites**: 4-directional player movement with 4-frame walking cycles
- **Sprite Sheets**: Separate home (blue) and away (red) team sprites
- **Ball Animation**: 64-frame rolling animation tied to velocity
- **Double-Scale Rendering**: Players at 128x128, ball at 32x32
- **Visual Effects**: Shadows, field markings, stadium stands, goal nets
- **Goal Celebration**: Dynamic ball-particle text formation system
- **Greek Language Support**: Full multilingual UI with UTF-8 encoding

### 🔊 Audio System
- **Music Tracks**: Menu, match, and victory music (looping)
- **Sound Effects**: 
  - Menu navigation (move, select, back)
  - Match sounds (whistle start/end, kick, tackle, goal)
  - Crowd reactions (cheer)
- **Volume Controls**: Separate music and SFX sliders (0-100%)
- **Smart Playback**: Non-retriggerable sounds to prevent overlapping
- **Mute Option**: Master audio toggle

### ⚙️ Settings & Customization
- **Video Settings**: Resolution (800x600 to 1920x1080), Fullscreen, VSync
- **Audio Settings**: Master/Music/SFX volumes, Mute all
- **Gameplay Settings**: Difficulty, match duration, player speed multiplier
- **Display Options**: Show/hide minimap, player names, stamina bars
- **Camera Settings**: Zoom level (0.5x-2.0x), camera follow speed
- **Language**: English/Greek (ελληνικά)
- **Persistent Storage**: All settings saved to database

### 💾 Data Management
- **SQLite Database**: Automatic save/load for all game data
- **Persistent Rosters**: Teams with unlimited roster sizes saved
- **Match Results**: Complete match history and statistics
- **Championship Progress**: Current matchweek, standings, fixtures
- **Settings Persistence**: All configuration saved across sessions
- **New Season**: Reset championship while keeping teams

### 📊 Statistics & UI
- **League Standings**: Sortable table with wins, draws, losses, goals, points
- **Live Match HUD**: Score, time, controls display
- **Formation Preview**: Visual representation in lineup screen
- **Season Completion**: Indicator when all matches played
- **Final Score Overlay**: 5-second display after match ("ΤΕΛΙΚΟ ΣΚΟΡ")
- **Countdown System**: 3-2-1 countdown before kickoff ("ΠΑΜΕ!")

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

### Menu Navigation
- **Up/Down Arrows**: Navigate menu options
- **Enter**: Select/confirm
- **Escape**: Back/exit

### Lineup Selection
- **Up/Down Arrows**: Navigate player list
- **Space**: Toggle player starter/bench status
- **Page Up/Down**: Quick scroll through roster
- **Enter**: Confirm lineup (requires 11 starters)
- **Escape**: Cancel and return to menu

### During Match
- **Arrow Keys**: Move controlled player
- **Space**: Switch to nearest visible teammate
- **X**: 
  - Tap to pass/shoot quickly
  - Hold to charge shot (power bar)
  - Tackle when near opponent with ball
- **Escape**: Pause/return to menu

### Settings Screen
- **Up/Down Arrows**: Navigate settings
- **Left/Right Arrows**: Adjust values
- **Page Up/Down**: Quick scroll
- **Enter**: Save and exit
- **Escape**: Cancel changes

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
- **SETTINGS_*.md**: Settings system documentation

## 🚀 Current Status

**Fully Playable!** All core features implemented:
- ✅ Championship mode with 8 teams
- ✅ Match gameplay with physics and AI
- ✅ Flexible rosters (11+) with lineup selection
- ✅ Database persistence
- ✅ Audio system (music + SFX)
- ✅ Animated sprites
- ✅ Settings system
- ✅ Greek language support

## 🎯 Future Enhancements

Potential improvements:
- [ ] Substitution system during matches
- [ ] Player fatigue/stamina depletion
- [ ] Fouls and yellow/red cards
- [ ] Offsides detection
- [ ] Advanced formations (4-3-3, 3-5-2, etc.)
- [ ] Player transfers and training
- [ ] Tournament/knockout modes
- [ ] Multiplayer support
- [ ] Match replays and highlights
- [ ] Weather effects
- [ ] Custom team creation

See **AGENTS.md** for complete development roadmap.

## License

This is a game project. All rights reserved.
