Create a top-down 2d soccer game using C# .net 8 and monogame with some management elements.
The game should show a simular view to sensible football or tecmo world cup video games.
The game is a championship with 8 teams. The only playable team is a team called "NO PASARAN!"
The other teams are "BARTSELIOMA", "KTEL", "NONAME", "MIHANIKOI", "ASALAGITOS", "ASTERAS EXARXION", "TIGANITIS".
We should use simple placeholder bitmap graphics (sprites) with routines for loading the appropriate resources. 
Data should persist using a SQLite database.
The player can control one football player at a time while the other players of the team are controlled by the computer.
Create the appropriate c# classes for the players and teams. Also track score and league score.
Use UTF8 encoding for all text data as there is multilingual support.
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

12. ✅ **Settings persistence system**:
   - Created comprehensive GameSettings model with video, audio, gameplay, and camera settings
   - Added Settings table to SQLite database
   - Implemented SaveSettings() and LoadSettings() methods in DatabaseManager
   - Settings automatically loaded on game start
   - Created SettingsScreen with scrolling support for all settings:
     * **Video Settings**: Resolution (800x600 to 1920x1080), Fullscreen, VSync
     * **Audio Settings**: Master Volume, Music Volume, SFX Volume, Mute All
     * **Gameplay Settings**: Difficulty (Easy/Normal/Hard), Match Duration (1-10 min), Player Speed (0.5x-2.0x)
     * **Display Settings**: Show Minimap, Show Player Names, Show Stamina
     * **Camera Settings**: Camera Zoom (0.5x-2.0x), Camera Speed (0.05-0.5)
     * **Language**: English/Greek (en/el)
   - Scrollable interface with visual indicators (▲ MORE / ▼ MORE)
   - Keyboard controls: Arrow keys for navigation, Left/Right for adjustment, PgUp/PgDn for quick scrolling
   - All 17 settings persist across game restarts
   - Settings accessible from main menu

13. ✅ **Extended font character support**:
   - Updated Font.spritefont with 9 comprehensive character ranges
   - Added support for arrows (←→↑↓) and geometric shapes (▲▼◄►)
   - Extended Greek character support (7936-8191)
   - Latin extended characters (160-255) for European languages
   - Mathematical operators and symbols (8704-8959)
   - Box drawing and UI elements (9472-9727)
   - General punctuation and currency symbols (8192-8303)
   - Miscellaneous symbols (9728-9983)
   - Created FONT_CHARACTER_SUPPORT.md documentation

14. ✅ **Goalpost and goal detection improvements** + **Dynamic text celebration**:
   - **Mesh net appearance**: Goals now have realistic net with grid/mesh pattern
     * Vertical, horizontal, and diagonal lines create net texture
     * Semi-transparent gray netting behind goal posts
     * White goal posts rendered in front of net
   - **Improved goal detection**: Ball must now cross the goal line (not just enter goal area)
     * Left goal: Ball X < goal line AND within goal width
     * Right goal: Ball X > goal line AND within goal width
     * Proper height checking (ball below 200px height)
     * Modified ClampBallToField to allow ball through goal area
   - **Ball over crossbar detection**: Ball going over goalposts registers as "out"
   - **Goal celebration animation** (✨ Enhanced - renders any text!): 
     * Refactored to use bitmap-based rendering system
     * **Can display ANY text string** - not limited to "GOAL!"
     * Renders text 4x larger with point sampling (no anti-aliasing)
     * Scans pixels line by line to extract visible pixels (alpha > 200)
     * Ball spacing: 24px in rendered bitmap (doubled for clarity)
     * Normal ball size: 32px (grows 0.5x to 1.5x during animation)
     * Number of balls automatically determined by text complexity
     * Ease-out animation with staggered delays (based on position)
     * 2.5 second celebration duration
     * Large text overlay displayed with ball formation
     * Semi-transparent background overlay during celebration
     * Match state pauses during celebration (GoalCelebration state)
     * Font-accurate rendering - uses actual SpriteFont glyphs
     * Usage: `GoalCelebration.Start("ANY TEXT", font, graphicsDevice)`
   - **Complete ball-out handling system**:
     * Ball over crossbar → placed near corner for restart
     * Ball out on goal line → corner kick or goal kick positioning
     * Ball out on sideline → throw-in positioning
     * Nearest player automatically positioned near restart ball
     * Different placement logic for top/bottom and left/right sides
     * Ball placed with appropriate margin from field boundaries

16. ✅ **Team Roster System**:
   - **22-player rosters**: Each team now has full squad (11 starters + 11 substitutes)
   - **Database persistence**: IsStarting and ShirtNumber fields added to Players table
   - **JSON seeding system**:
     * Load teams from `Database/teams_seed.json` file
     * UTF-8 support for Greek/multilingual names
     * Auto-generate 22 players if `players` array is empty
     * Position-appropriate stat generation
   - **Starting lineup management**:
     * Match engine uses only starting XI (IsStarting = true)
     * Auto-designates first 11 as starting if none marked
     * Proper formation (1 GK, 4 DEF, 4 MID, 2 FWD)
   - **TeamSeeder class**: Handles JSON parsing and auto-generation
   - **Enhanced ChampionshipInitializer**: Supports both JSON and legacy generation
   - **Default roster**: NO PASARAN! team with 22 Greek-named players fully defined
   - Created ROSTER_SYSTEM.md documentation

17. ✅ **Audio System Improvements**:
   - **Non-retriggerable sounds**: Added `allowRetrigger` parameter to PlaySoundEffect
   - **Sound instance tracking**: Prevents kick_ball from overlapping
   - **Automatic cleanup**: Finished sound instances disposed in Update loop
   - Ball kick sounds now play cleanly without stuttering

18. ✅ **Match End Overlay**:
   - **5-second final score display**: Shows "ΤΕΛΙΚΟ ΣΚΟΡ" (Final Score in Greek)
   - **FinalScore match state**: New state between playing and ended
   - **Countdown timer**: Shows remaining seconds before return to menu
   - **Score display**: Team names with final score
   - **Semi-transparent overlay**: Dark background for visibility

19. ✅ **Visual Improvements**:
   - **Referee removed**: No more distracting horizontal stripes during gameplay
   - **Clean countdown**: Simplified match start countdown display

20. ✅ **Lineup Selection Screen**:
   - **Pre-match lineup editor**: Select starting XI before each match
   - **Full roster view**: See all 22 players with shirt numbers
   - **Interactive selection**:
     * Up/Down arrows to navigate player list
     * Space to toggle starter/bench status
     * Must have exactly 11 starters to proceed
     * PageUp/PageDown for quick scrolling
   - **Formation preview**: Visual representation of selected lineup
     * Shows player positions on field
     * Displays shirt numbers on formation
     * Shows formation (e.g., 4-4-2)
     * Position breakdown (GK/DEF/MID/FWD counts)
   - **Real-time validation**: Color-coded starter count indicator
   - **Database persistence**: Lineup choices saved automatically
   - **Player information**: Name, position, shirt number, starter status
   - Greek language support for UI text

## Current Features:
- Championship mode with 8 teams
- Player-controlled team: "NO PASARAN!"
- SQLite database persistence (auto-save/load)
- **Persistent settings system** - All video, audio, gameplay, and camera settings saved to database
- Menu system for navigation
- Settings screen with comprehensive options
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

15. ✅ **Complete Audio System**:
   - **AudioManager singleton**: Manages all music and SFX
   - **Music system**: Menu music, match music, victory music (looping)
   - **Sound effects**: Menu navigation, ball kicks, tackles, goals, whistles, crowd
   - **Volume controls**: Separate music and SFX volumes (0.0-1.0)
   - **Settings integration**: Audio settings persist in database
   - **Graceful handling**: Missing audio files don't crash the game
   - **Menu sounds**: 
     * `menu_move` - Arrow key navigation
     * `menu_select` - Enter key confirmation
     * `menu_back` - Escape key
   - **Match sounds**:
     * `whistle_start` - Match/kickoff starts
     * `whistle_end` - Match ends
     * `kick_ball` - Ball kicked (volume varies with power)
     * `tackle` - Player collision/tackle
     * `goal` - Goal scored
     * `crowd_cheer` - Crowd celebration (louder)
   - **Music transitions**: Menu ↔ Match music automatically
   - **Created AUDIO_SYSTEM.md**: Complete documentation with asset guidelines

## Next Steps (Future Enhancements):
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
 