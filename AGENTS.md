Create a top-down 2d soccer game using C# .net 8 and monogame with some management elements.
The game should show a simular view to sensible football or tecmo world cup video games.
The game is a championship with 8 teams. The only playable team is a team called "NO PASARAN!"
The other teams are "BARTSELIOMA", "KTEL", "NONAME", "MIHANIKOI", "ASALAGITOS", "ASTERAS EXARXION", "TIGANITIS".
We should use simple placeholder bitmap graphics (sprites) with routines for loading the appropriate resources. 
Data should persist using a SQLite database.
The player can control one football player at a time while the other players of the team are controlled by the computer.
Create the appropriate c# classes for the players and teams. Also track score and league score.

## Completed Tasks:

1. ✅ Created Models folder with classes:
   - Player.cs - Player model with position, stats (Speed, Shooting, Passing, Defending, Agility, Technique, Stamina), and animation state
   - Team.cs - Team model with championship stats and player roster
   - Match.cs - Match model for fixtures
   - Championship.cs - Championship manager with fixture generation and standings

2. ✅ Created Database layer:
   - DatabaseManager.cs - SQLite persistence for all game data with INSERT OR REPLACE logic

3. ✅ Created Gameplay mechanics:
   - MatchEngine.cs - Advanced match simulation with:
     * Ball physics (velocity, friction, bouncing)
     * Position-based AI (different behaviors for GK/DEF/MID/FWD)
     * Tackle system with stat-based success probability
     * Smart player switching (visible players only)
     * Proper goal detection
   - ChampionshipInitializer.cs - Initializes 8 teams with generated players
   - Camera.cs - Smooth scrolling camera that follows the ball
   - Minimap.cs - Strategic overview showing entire field

4. ✅ Created Screens system:
   - Screen.cs - Base screen class
   - ScreenManager.cs - Screen stack management
   - MenuScreen.cs - Main menu with navigation
   - StandingsScreen.cs - League table display
   - MatchScreen.cs - Playable match view with:
     * Top-down scrolling graphics
     * Stadium stands rendering
     * Field markings (center circle, penalty areas)
     * Sprite animation system
     * Minimap display
     * Player shadows

5. ✅ Updated Game1.cs - Main game loop integration
6. ✅ Created Font.spritefont - Basic font for UI
7. ✅ Project builds successfully
8. ✅ Created SPRITE_GUIDE.md - Instructions for adding sprite assets

## Current Features:
- Championship mode with 8 teams
- Player-controlled team: "NO PASARAN!"
- SQLite database persistence (auto-save/load)
- Menu system for navigation
- League standings display
- Playable matches with advanced controls:
  * Arrows: Move controlled player
  * Space: Switch to nearest visible teammate
  * X: Shoot (when near ball) / Tackle (when near opponent)
  * Escape: Exit match
- **Scrolling camera system** - Follows ball smoothly, shows portion of large field (zoom: 0.8)
- **Large playing field** - 3200x2400 with 200px stadium margins
- **Strategic minimap** - Always visible, shows entire field, players, ball, and camera viewport
- **Position-based AI** - Players behave according to their role (GK/DEF/MID/FWD)
- **Ball physics** - Independent ball with velocity, friction, and realistic bouncing
- **Tackle system** - Stat-based tackling with success probability formula
- **Sprite animation system** - Ready for sprite sheets (currently using placeholders)
- Visual enhancements: shadows, field markings, stadium stands
- Goal scoring with proper detection and match results tracking

## Next Steps (Future Enhancements):
- Add actual sprite graphics (see SPRITE_GUIDE.md)
- Add sound effects (kicks, tackles, goals, crowd)
- Add background music
- Improve AI with formation awareness
- Add more match events (fouls, corners, throw-ins, offsides)
- Add player substitutions
- Add detailed match statistics
- Add team management features (transfers, training)
- Add tournament mode (knockout stages)
- Add weather effects
- Add replay/highlights system
- Add save game slots
- Add difficulty levels
- Improve UI with better graphics and animations
 