# Team Roster System Documentation

## Overview
The game now supports full 22-player rosters for each team, with starting lineups (11 players) and substitutes. Teams and players can be seeded from a JSON file or auto-generated.

## Database Schema Changes

### Player Table
Added two new columns:
- `IsStarting` (INTEGER): 1 if player is in starting lineup, 0 otherwise
- `ShirtNumber` (INTEGER): Player's shirt number (1-22)

### Team Storage
- All teams are now fully persisted in the database
- Teams are automatically loaded from the database on game start
- Player rosters (all 22 players) are saved and loaded

## JSON Seeding System

### Location
Place your team data file at: `Database/teams_seed.json`

### Format
```json
{
  "teams": [
    {
      "name": "TEAM NAME",
      "isPlayerControlled": true/false,
      "players": [
        {
          "name": "Player Name",
          "position": "Goalkeeper|Defender|Midfielder|Forward",
          "shirtNumber": 1,
          "isStarting": true/false,
          "speed": 50,
          "shooting": 50,
          "passing": 50,
          "defending": 50,
          "agility": 50,
          "technique": 50,
          "stamina": 90
        }
      ]
    }
  ]
}
```

### Seeding Behavior
1. **With JSON file**: If `teams_seed.json` exists, the game loads teams from it
2. **Auto-generation**: If a team has empty `players` array, 22 players are auto-generated with:
   - 2 Goalkeepers (1 starting)
   - 8 Defenders (4 starting)
   - 8 Midfielders (4 starting)
   - 4 Forwards (2 starting)
3. **Fallback**: If JSON loading fails, uses legacy generation (11 players only)

### Player Generation
Auto-generated players have:
- Position-appropriate stats (e.g., forwards have high shooting/speed, defenders have high defending)
- Consistent names based on team name hash
- Proper shirt numbers (1-22)
- Greek names (Κώστας, Γιώργος, Δημήτρης, etc.)

## Match Behavior

### Starting Lineup Only
- **Only players with `IsStarting = true` appear in matches**
- Each team must have exactly 11 starting players
- If no starting players are marked, the first 11 are automatically designated as starting
- Substitutes (players 12-22) are stored but not used in matches (yet)

### Team Composition
A proper starting lineup should have:
- 1 Goalkeeper
- 4-5 Defenders
- 3-5 Midfielders  
- 1-3 Forwards

## Current Implementation Status

### ✅ Implemented
- [x] 22-player rosters per team
- [x] Starting lineup vs substitutes distinction
- [x] JSON seeding system with UTF-8 support
- [x] Auto-generation for teams without player data
- [x] Database persistence for all players
- [x] ShirtNumber tracking
- [x] Position-appropriate stat generation
- [x] Match engine filters to starting players only
- [x] Greek character support in player names

### 🚧 Future Enhancements
- [ ] Lineup selection screen for player before match
- [ ] Substitution system during matches
- [ ] Player fatigue affecting stamina
- [ ] Injury system
- [ ] Player form/morale
- [ ] Squad rotation suggestions
- [ ] Reserve/youth team system

## Example: NO PASARAN! Team
The default `teams_seed.json` includes a fully defined NO PASARAN! roster with:
- 22 players with Greek names
- Detailed stats for each player
- Proper starting XI (players 1-11)
- Reserves with varied positions

## Technical Details

### Classes
- **TeamSeeder**: Handles JSON loading and auto-generation
- **ChampionshipInitializer**: Orchestrates team creation
- **DatabaseManager**: Updated with IsStarting and ShirtNumber fields
- **Player model**: Extended with roster management properties

### File Copy
The `teams_seed.json` file is automatically copied to the build output directory via `.csproj` configuration.

## Usage

### To Customize Teams
1. Edit `Database/teams_seed.json`
2. Modify player stats, names, positions, or shirt numbers
3. Set `isStarting: true` for your desired starting XI
4. Rebuild the project
5. Delete `nopasaran.db` to force reseeding
6. Run the game

### To Let Game Auto-Generate
1. In JSON, set `"players": []` for a team
2. Game will generate 22 players automatically
3. First 11 will be marked as starting
4. Stats will be position-appropriate

## Notes
- UTF-8 encoding is used throughout for multilingual support
- Player stats range from 0-100
- Stamina typically ranges from 75-95
- Each position has appropriate stat ranges (e.g., GK has high defending, low speed)
