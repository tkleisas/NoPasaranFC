using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Models;
using NoPasaranFC.Database;
using Microsoft.Xna.Framework.Content;

namespace NoPasaranFC.Screens
{
    public class MenuScreen : Screen
    {
        private Championship _championship;
        private DatabaseManager _database;
        private ScreenManager _screenManager;
        private ContentManager  _contentManager;
        private GraphicsDevice _graphicsDevice;
        private Gameplay.InputHelper _input;
        private KeyboardState _previousKeyState; // Still needed for some menu controls
        private int _selectedOption;
        private string[] GetMenuOptions()
        {
            var loc = Models.Localization.Instance;
            return new string[]
            {
                loc.Get("menu.standings"),
                loc.Get("menu.nextMatch"),
                loc.Get("menu.newSeason"),
                loc.Get("menu.settings"),
                loc.Get("menu.exit")
            };
        }
        private bool _inOptionsMenu = false;
        private int _selectedResolution = 2; // Default to 1280x720
        private bool _tempFullscreen = false;
        private Texture2D _grassTexture;
        private float _grassScrollOffset = 0f;
        private int _optionsSelectedItem = 0; // 0=resolution, 1=fullscreen, 2=speed, 3=match duration, 4=sprite tests
        private Texture2D _logo;
        
        public bool ShouldExit { get; private set; }
        
        public MenuScreen(Championship championship, DatabaseManager database, ScreenManager screenManager, ContentManager content, GraphicsDevice graphicsDevice = null)
        {
            _championship = championship;
            _database = database;
            _screenManager = screenManager;
            _contentManager = content;
            _graphicsDevice = graphicsDevice;
            _selectedOption = 0;
            ShouldExit = false;
            _input = new Gameplay.InputHelper();
            
            // Load logo
            _logo = content.Load<Texture2D>("Sprites/no_pasaran_fc_logo");
            
            // Initialize temp fullscreen to current state
            _tempFullscreen = Game1.IsFullscreen;
            
            // Find current resolution index
            var resolutions = Game1.GetAvailableResolutions();
            for (int i = 0; i < resolutions.Length; i++)
            {
                if (resolutions[i].X == Game1.ScreenWidth && resolutions[i].Y == Game1.ScreenHeight)
                {
                    _selectedResolution = i;
                    break;
                }
            }
        }
        
        public override void OnActivated()
        {
            // Reset input state when returning to menu to prevent key bleed-through
            _input = new Gameplay.InputHelper();
        }
        
        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            CreateGrassTexture();
        }
        
        public override void Update(GameTime gameTime)
        {
            _input.Update();
            var keyState = Keyboard.GetState(); // Still needed for some menu controls
            
            // Update grass scroll
            _grassScrollOffset += 50f * (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_grassScrollOffset >= 200f) // Texture size
            {
                _grassScrollOffset -= 200f;
            }
            
            if (!_inOptionsMenu)
            {
                // Main menu navigation (keyboard or gamepad)
                var menuOptions = GetMenuOptions();
                if (_input.IsMenuDownPressed())
                {
                    _selectedOption = (_selectedOption + 1) % menuOptions.Length;
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                }
                
                if (_input.IsMenuUpPressed())
                {
                    _selectedOption = (_selectedOption - 1 + menuOptions.Length) % menuOptions.Length;
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                }
                
                // Confirm selection (Enter or A button)
                if (_input.IsConfirmPressed())
                {
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_select");
                    HandleSelection();
                }
                
                // Exit (Escape or B button)
                if (_input.IsBackPressed())
                {
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_back");
                    ShouldExit = true;
                }
            }
            else
            {
                // Options menu navigation (keyboard or gamepad)
                if (_input.IsMenuUpPressed())
                {
                    _optionsSelectedItem = (_optionsSelectedItem - 1 + 5) % 5;
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                }
                
                if (_input.IsMenuDownPressed())
                {
                    _optionsSelectedItem = (_optionsSelectedItem + 1) % 5;
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                }
                
                // Left/Right still use keyboard for now (TODO: add gamepad support)
                if (keyState.IsKeyDown(Keys.Left) && !_previousKeyState.IsKeyDown(Keys.Left))
                {
                    HandleOptionsChange(-1);
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                }
                
                if (keyState.IsKeyDown(Keys.Right) && !_previousKeyState.IsKeyDown(Keys.Right))
                {
                    HandleOptionsChange(1);
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                }
                
                if (keyState.IsKeyDown(Keys.F) && !_previousKeyState.IsKeyDown(Keys.F))
                {
                    _tempFullscreen = !_tempFullscreen;
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                }
                
                // Confirm (Enter or A button)
                if (_input.IsConfirmPressed())
                {
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_select");
                    if (_optionsSelectedItem == 4)
                    {
                        // Launch sprite test screen
                        _screenManager.PushScreen(new SpriteTestScreen(_screenManager, _contentManager, _graphicsDevice));
                    }
                    else
                    {
                        // Apply resolution
                        var resolution = Game1.GetAvailableResolutions()[_selectedResolution];
                        _screenManager.SetResolution(resolution.X, resolution.Y, _tempFullscreen);
                        _inOptionsMenu = false;
                    }
                }
                
                // Back (Escape or B button)
                if (_input.IsBackPressed())
                {
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_back");
                    _inOptionsMenu = false;
                }
            }
            
            _previousKeyState = keyState;
        }
        
        private void HandleOptionsChange(int direction)
        {
            switch (_optionsSelectedItem)
            {
                case 0: // Resolution
                    _selectedResolution = (_selectedResolution + direction + Game1.GetAvailableResolutions().Length) % Game1.GetAvailableResolutions().Length;
                    break;
                case 1: // Fullscreen
                    if (direction != 0)
                        _tempFullscreen = !_tempFullscreen;
                    break;
                case 2: // Player speed
                    GameSettings.Instance.PlayerSpeedMultiplier = Math.Clamp(GameSettings.Instance.PlayerSpeedMultiplier + direction * 0.1f, 0.5f, 2.0f);
                    break;
                case 3: // Match duration
                    float[] durations = { 3f, 5f, 10f, 15f, 30f, 45f, 90f };
                    int currentIndex = Array.FindIndex(durations, d => Math.Abs(d - GameSettings.Instance.MatchDurationMinutes) < 0.1f);
                    if (currentIndex == -1) currentIndex = 0;
                    currentIndex = (currentIndex + direction + durations.Length) % durations.Length;
                    GameSettings.Instance.MatchDurationMinutes = durations[currentIndex];
                    break;
            }
        }
        
        private void HandleSelection()
        {
            switch (_selectedOption)
            {
                case 0: // View Standings
                    _screenManager.PushScreen(new StandingsScreen(_championship, _screenManager));
                    break;
                    
                case 1: // Play Next Match
                    var playerTeam = _championship.Teams.Find(t => t.IsPlayerControlled);
                    if (playerTeam != null)
                    {
                        var nextMatch = _championship.Matches.Find(m => !m.IsPlayed && 
                            (m.HomeTeamId == playerTeam.Id || m.AwayTeamId == playerTeam.Id));
                        
                        if (nextMatch != null)
                        {
                            var homeTeam = _championship.Teams.Find(t => t.Id == nextMatch.HomeTeamId);
                            var awayTeam = _championship.Teams.Find(t => t.Id == nextMatch.AwayTeamId);
                            
                            if (homeTeam != null && awayTeam != null)
                            {
                                // Show lineup screen before match if it's the player's team
                                if (playerTeam.Id == homeTeam.Id || playerTeam.Id == awayTeam.Id)
                                {
                                    var lineupScreen = new LineupScreen(playerTeam, nextMatch, _championship, 
                                        _database, _screenManager, _contentManager, _graphicsDevice);
                                    _screenManager.PushScreen(lineupScreen);
                                }
                                else
                                {
                                    // Non-player match - go directly to match screen
                                    var matchScreen = new MatchScreen(homeTeam, awayTeam, nextMatch, _championship, _database, _screenManager,_contentManager);
                                    if (_graphicsDevice != null)
                                    {
                                        matchScreen.SetGraphicsDevice(_graphicsDevice);
                                    }
                                    _screenManager.PushScreen(matchScreen);
                                }
                            }
                            else
                            {
                                // Debug: teams not found
                                System.IO.File.WriteAllText("match_debug.txt", 
                                    $"Η ΟΜΑΔΑ ΔΕ ΒΡΕΘΗΚΕ! homeTeam={homeTeam}, awayTeam={awayTeam}, HomeId={nextMatch.HomeTeamId}, AwayId={nextMatch.AwayTeamId}");
                            }
                        }
                        // If no next match found, user will see "Season Complete!" message in menu
                    }
                    else
                    {
                        // Debug: player team not found
                        System.IO.File.WriteAllText("match_debug.txt", 
                            $"Η ΟΜΑΔΑ ΤΟΥ ΠΑΙΧΤΗ ΔΕ ΒΡΕΘΗΚΕ! ΣΥΝΟΛΙΚΕΣ ΟΜΑΔΕΣ: {_championship.Teams.Count}");
                    }
                    break;
                
                case 2: // New Season
                    StartNewSeason();
                    break;
                    
                case 3: // Options
                    var settingsScreen = new SettingsScreen(_database, (Game1)_screenManager.Game, _contentManager, _graphicsDevice);
                    _screenManager.PushScreen(settingsScreen);
                    break;
                    
                case 4: // Exit
                    ShouldExit = true;
                    break;
            }
        }
        
        private void StartNewSeason()
        {
            // Reset all match results
            foreach (var match in _championship.Matches)
            {
                match.IsPlayed = false;
                match.HomeScore = 0;
                match.AwayScore = 0;
            }
            
            // Reset all team stats
            foreach (var team in _championship.Teams)
            {
                team.Wins = 0;
                team.Draws = 0;
                team.Losses = 0;
                team.GoalsFor = 0;
                team.GoalsAgainst = 0;
            }
            
            // Save to database
            _database.SaveChampionship(_championship);
        }
        
        private void DrawGrassBackground(SpriteBatch spriteBatch, int screenWidth, int screenHeight)
        {
            if (_grassTexture != null)
            {
                int tilesX = (int)Math.Ceiling(screenWidth / 200f) + 1;
                int tilesY = (int)Math.Ceiling(screenHeight / 200f) + 1;
                
                for (int y = -1; y < tilesY; y++)
                {
                    for (int x = -1; x < tilesX; x++)
                    {
                        Vector2 pos = new Vector2(x * 200 - _grassScrollOffset, y * 200);
                        spriteBatch.Draw(_grassTexture, pos, Color.White);
                    }
                }
            }
        }
        
        private void CreateGrassTexture()
        {
            if (_graphicsDevice == null) return;
            
            _grassTexture?.Dispose();
            
            int textureSize = 200;
            _grassTexture = new Texture2D(_graphicsDevice, textureSize, textureSize);
            Color[] data = new Color[textureSize * textureSize];
            
            Random random = new Random(42);
            int grassSize = 10; // Smaller tiles for less blocky look
            
            for (int y = 0; y < textureSize; y += grassSize)
            {
                for (int x = 0; x < textureSize; x += grassSize)
                {
                    int greenVariation = random.Next(-15, 15);
                    Color grassColor = new Color(34 + greenVariation, 139 + greenVariation, 34 + greenVariation);
                    
                    for (int py = 0; py < grassSize && y + py < textureSize; py++)
                    {
                        for (int px = 0; px < grassSize && x + px < textureSize; px++)
                        {
                            data[(y + py) * textureSize + (x + px)] = grassColor;
                        }
                    }
                }
            }
            
            _grassTexture.SetData(data);
        }
        
        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            
            DrawGrassBackground(spriteBatch, screenWidth, screenHeight);
            
            if (!_inOptionsMenu)
            {
                // Draw logo at top center
                if (_logo != null)
                {
                    float logoScale = 1.0f; // Adjust scale as needed
                    int logoWidth = (int)(_logo.Width * logoScale);
                    int logoHeight = (int)(_logo.Height * logoScale);
                    Vector2 logoPos = new Vector2((screenWidth - logoWidth) / 2, screenHeight * 0.01f);

                    spriteBatch.Draw(_logo, logoPos, null, Color.White, 0f, Vector2.Zero, logoScale, SpriteEffects.None, 0f);
                }
                else
                {
                    // Draw main menu centered
                    string title = "NO PASARAN! - ΜΕΝΟΥ ΑΓΩΝΩΝ";
                    Vector2 titleSize = font.MeasureString(title);
                    Vector2 titlePos = new Vector2((screenWidth - titleSize.X) / 2, screenHeight * 0.5f);
                    spriteBatch.DrawString(font, title, titlePos, Color.Yellow);
                }
                // Check if season is complete
                var playerTeam = _championship.Teams.Find(t => t.IsPlayerControlled);
                bool seasonComplete = false;
                if (playerTeam != null)
                {
                    var nextMatch = _championship.Matches.Find(m => !m.IsPlayed && 
                        (m.HomeTeamId == playerTeam.Id || m.AwayTeamId == playerTeam.Id));
                    seasonComplete = nextMatch == null;
                }
                
                // Show season complete message if all matches are played
                if (seasonComplete)
                {
                    string completeMsg = "*** ΤΟ ΠΡΩΤΑΘΛΗΜΑ ΕΧΕΙ ΟΛΟΚΛΗΡΩΘΕΙ! ***";
                    Vector2 msgSize = font.MeasureString(completeMsg);
                    Vector2 msgPos = new Vector2((screenWidth - msgSize.X) / 2, screenHeight * 0.6f);
                    spriteBatch.DrawString(font, completeMsg, msgPos, Color.LightGreen);
                    
                    string useNewSeason = "ΕΠΙΛΕΞΤΕ 'ΝΕΟ ΠΡΩΤΑΘΛΗΜΑ' ΓΙΑ ΝΕΟ ΞΕΚΙΝΗΜΑ";
                    Vector2 useSize = font.MeasureString(useNewSeason);
                    Vector2 usePos = new Vector2((screenWidth - useSize.X) / 2, screenHeight * 0.6f + 30);
                    spriteBatch.DrawString(font, useNewSeason, usePos, Color.Gray);
                }
                
                // Draw menu options centered
                var menuOptions = GetMenuOptions();
                float menuStartY = screenHeight * 0.62f;
                for (int i = 0; i < menuOptions.Length; i++)
                {
                    var prefix = i == _selectedOption ? "> " : "  ";
                    var text = prefix + menuOptions[i] + (i == _selectedOption ? " <" : "  ");
                    
                    // Dim "Play Next Match" if season is complete
                    var color = i == _selectedOption ? Color.Yellow : Color.White;
                    if (i == 1 && seasonComplete) // Play Next Match option
                    {
                        color = new Color(100, 100, 100); // Dark gray
                    }
                    
                    Vector2 textSize = font.MeasureString(text);
                    Vector2 textPos = new Vector2((screenWidth - textSize.X) / 2, menuStartY + i * 50);
                    spriteBatch.DrawString(font, text, textPos, color);
                }
                
                // Draw version in bottom left corner
                string versionText = Models.Version.GetFullVersion();
                Vector2 versionSize = font.MeasureString(versionText);
                Vector2 versionPos = new Vector2(10, screenHeight - versionSize.Y - 10);
                spriteBatch.DrawString(font, versionText, versionPos, Color.Red);
            }
            else
            {
                // Draw options menu centered
                string title = "ΕΠΙΛΟΓΕΣ";
                Vector2 titleSize = font.MeasureString(title);
                Vector2 titlePos = new Vector2((screenWidth - titleSize.X) / 2, screenHeight * 0.15f);
                spriteBatch.DrawString(font, title, titlePos, Color.Yellow);
                
                float menuStartY = screenHeight * 0.3f;
                float lineHeight = 50f;
                
                // Resolution
                var resolutions = Game1.GetAvailableResolutions();
                string resText = $"ΑΝΑΛΥΣΗ: {resolutions[_selectedResolution].X}x{resolutions[_selectedResolution].Y}";
                var resColor = _optionsSelectedItem == 0 ? Color.Yellow : Color.White;
                Vector2 resSize = font.MeasureString(resText);
                spriteBatch.DrawString(font, resText, new Vector2((screenWidth - resSize.X) / 2, menuStartY), resColor);
                
                // Fullscreen
                string fsText = $"ΠΛΗΡΗΣ ΟΘΟΝΗ: {(_tempFullscreen ? "ΕΝΕΡΓΟ" : "ΑΝΕΝΕΡΓΟ")}";
                var fsColor = _optionsSelectedItem == 1 ? Color.Yellow : Color.White;
                Vector2 fsSize = font.MeasureString(fsText);
                spriteBatch.DrawString(font, fsText, new Vector2((screenWidth - fsSize.X) / 2, menuStartY + lineHeight), fsColor);
                
                // Player speed
                string speedText = $"ΤΑΧΥΤΗΤΑ ΠΑΙΚΤΩΝ: {GameSettings.Instance.PlayerSpeedMultiplier:F1}x";
                var speedColor = _optionsSelectedItem == 2 ? Color.Yellow : Color.White;
                Vector2 speedSize = font.MeasureString(speedText);
                spriteBatch.DrawString(font, speedText, new Vector2((screenWidth - speedSize.X) / 2, menuStartY + lineHeight * 2), speedColor);
                
                // Match duration
                string durationText = $"ΔΙΑΡΚΕΙΑ ΑΓΩΝΑ: {GameSettings.Instance.MatchDurationMinutes:F0} λεπτά";
                var durationColor = _optionsSelectedItem == 3 ? Color.Yellow : Color.White;
                Vector2 durationSize = font.MeasureString(durationText);
                spriteBatch.DrawString(font, durationText, new Vector2((screenWidth - durationSize.X) / 2, menuStartY + lineHeight * 3), durationColor);
                
                // Sprite tests
                string spriteText = "ΔΟΚΙΜΗ SPRITES";
                var spriteColor = _optionsSelectedItem == 4 ? Color.Yellow : Color.White;
                Vector2 spriteSize = font.MeasureString(spriteText);
                spriteBatch.DrawString(font, spriteText, new Vector2((screenWidth - spriteSize.X) / 2, menuStartY + lineHeight * 4), spriteColor);
                
                // Draw instructions
                string instructions = "<=/=> ΓΙΑ ΑΛΛΑΓΗ, ENTER ΓΙΑ ΕΦΑΡΜΟΓΗ, ESC ΓΙΑ ΕΠΙΣΤΡΟΦΗ";
                Vector2 instrSize = font.MeasureString(instructions);
                spriteBatch.DrawString(font, instructions, new Vector2((screenWidth - instrSize.X) / 2, screenHeight * 0.85f), Color.Gray);
            }
        }
    }
}
