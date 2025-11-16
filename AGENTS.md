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
9. ✅ **Added sprite sheet support** - Implemented animated player sprites:
   - Loaded player_blue.png and player_red.png sprite sheets (4x4 grid, 64x64 frames, 256x256 total)
   - **Working animation system**: 8 fps walking animation with proper delta time
   - 4 directions (down, up, left, right) with 4 frames each cycling smoothly
   - **Double-sized rendering**: Players rendered at 128x128 (2x sprite size) for better visibility
   - **Diagonal rotation**: Sprites rotate when moving diagonally for realistic movement
   - Proper source rectangle extraction from sprite sheets
   - Yellow tint for controlled player, team colors for others
   - **Animated ball**: Loaded ball.png sprite sheet (8x8 grid, 32x32 frames, 64 total frames)
     * Ball animation speed based on velocity (faster rolling = faster animation)
     * Smooth rolling effect when ball is moving
   - Ball scaled to 32x32 (proportional to 128x128 players)

10. ✅ **Added New Season feature**:
   - New menu option to reset the championship
   - Resets all match results and team statistics
   - "Season Complete" indicator when all matches are played
   - Grayed out "Play Next Match" option when season is complete

11. ✅ **Fixed standings table alignment**:
   - Changed from fixed-width string formatting to column-based positioning
   - Properly aligned columns work with proportional fonts
   - Clean, readable table layout

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
- **Sprite animation system** - Active sprite sheet support:
  * 4-directional movement animation (down, up, left, right)
  * 4 frames per direction for smooth walking animation at 8 fps
  * Automatic direction and frame updates based on player velocity
  * Delta-time based animation (frame-rate independent)
  * **Double-scale rendering**: 64x64 sprite frames rendered at 128x128 for clarity
  * **Diagonal rotation**: Players rotate smoothly when moving diagonally
  * Separate sprite sheets for home (blue) and away (red) teams
  * Visual tints for controlled player (yellow) and knocked down players (gray)
  * Properly scaled ball (32x32) and referee (120x120) relative to players (128x128)
- **Animated ball sprite sheet**:
  * 64-frame rolling animation (8x8 grid of 32x32 frames)
  * Animation speed tied to ball velocity for realistic rolling effect
  * Smooth transitions between frames
- Visual enhancements: shadows, field markings, stadium stands
- Goal scoring with proper detection and match results tracking
- **Season management**:
  * "New Season" option to reset championship
  * Visual indicator when all matches are complete
  * Automatic detection of unplayed matches

## Next Steps (Future Enhancements):
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
 