# NO PASARAN! Football Championship

A top-down 2D soccer game built with C# .NET and MonoGame, inspired by classic games like Sensible Soccer and Tecmo World Cup.

## Game Overview

Manage and play as **NO PASARAN!** in an 8-team championship. Control one player at a time while AI manages your teammates and opponents.

### Teams
- NO PASARAN! (Player-controlled)
- BARTSELIOMA
- KTEL
- NONAME
- MIHANIKOI
- ASALAGITOS
- ASTERAS EXARXION
- TIGANITIS

## Features

- **Championship Mode**: Play through a full league season
- **Top-down Match View**: Classic 2D football perspective
- **Player Control**: Switch between your team's players during matches
- **AI Teammates**: Computer-controlled teammates support your play
- **League Standings**: Track your progress and rival teams
- **SQLite Database**: All progress is saved automatically

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

## Controls

### Menu Navigation
- **Up/Down Arrow Keys**: Navigate menu options
- **Enter**: Select menu option
- **Escape**: Exit game (from main menu)

### During Match
- **Arrow Keys**: Move controlled player
- **Space**: Switch to another player on your team
- **X**: Shoot/Pass the ball
- **Escape**: Return to menu (match will end)

## Game Structure

### Models
- **Player**: Individual player attributes (speed, shooting, passing, defending)
- **Team**: Team roster and championship statistics
- **Match**: Match fixtures and results
- **Championship**: League management and standings

### Screens
- **Menu Screen**: Main navigation hub
- **Standings Screen**: View league table
- **Match Screen**: Play matches with top-down view

### Database
- Automatic save/load using SQLite
- Database file: `nopasaran.db`
- Tracks all teams, players, matches, and championship progress

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

## Technical Details

- **Framework**: .NET 9.0 (compatible with .NET 8.0+)
- **Game Engine**: MonoGame 3.8
- **Database**: SQLite (Microsoft.Data.Sqlite)
- **Graphics**: Simple colored rectangles (placeholder for sprites)

## Current Limitations

- Graphics are placeholder colored rectangles
- Match duration is 90 seconds (simulated time)
- AI is basic - players chase the ball
- No advanced football mechanics (offsides, fouls, etc.)

## Future Enhancements

See AGENTS.md for a complete list of planned features and improvements.

## License

This is a game project. All rights reserved.
