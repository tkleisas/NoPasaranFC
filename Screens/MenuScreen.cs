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
        private KeyboardState _previousKeyState;
        private int _selectedOption;
        private readonly string[] _menuOptions = { "ΠΡΟΒΟΛΗ ΑΠΟΤΕΛΕΣΜΑΤΩΝ", "ΕΠΟΜΕΝΟΣ ΑΓΩΝΑΣ", "ΝΕΟ ΠΡΩΤΑΘΛΗΜΑ", "ΕΠΙΛΟΓΕΣ", "ΕΞΟΔΟΣ" };
        private bool _inOptionsMenu = false;
        private int _selectedResolution = 2; // Default to 1280x720
        private bool _tempFullscreen = false;
        private Texture2D _grassTexture;
        
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
        
        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            _graphicsDevice = graphicsDevice;
            CreateGrassTexture();
        }
        
        public override void Update(GameTime gameTime)
        {
            var keyState = Keyboard.GetState();
            
            if (!_inOptionsMenu)
            {
                // Main menu navigation
                if (keyState.IsKeyDown(Keys.Down) && !_previousKeyState.IsKeyDown(Keys.Down))
                {
                    _selectedOption = (_selectedOption + 1) % _menuOptions.Length;
                }
                
                if (keyState.IsKeyDown(Keys.Up) && !_previousKeyState.IsKeyDown(Keys.Up))
                {
                    _selectedOption = (_selectedOption - 1 + _menuOptions.Length) % _menuOptions.Length;
                }
                
                if (keyState.IsKeyDown(Keys.Enter) && !_previousKeyState.IsKeyDown(Keys.Enter))
                {
                    HandleSelection();
                }
            }
            else
            {
                // Options menu navigation
                if (keyState.IsKeyDown(Keys.Up) && !_previousKeyState.IsKeyDown(Keys.Up))
                {
                    _selectedResolution = (_selectedResolution - 1 + Game1.GetAvailableResolutions().Length) % Game1.GetAvailableResolutions().Length;
                }
                
                if (keyState.IsKeyDown(Keys.Down) && !_previousKeyState.IsKeyDown(Keys.Down))
                {
                    _selectedResolution = (_selectedResolution + 1) % Game1.GetAvailableResolutions().Length;
                }
                
                if (keyState.IsKeyDown(Keys.F) && !_previousKeyState.IsKeyDown(Keys.F))
                {
                    _tempFullscreen = !_tempFullscreen;
                }
                
                if (keyState.IsKeyDown(Keys.Enter) && !_previousKeyState.IsKeyDown(Keys.Enter))
                {
                    // Apply resolution
                    var resolution = Game1.GetAvailableResolutions()[_selectedResolution];
                    _screenManager.SetResolution(resolution.X, resolution.Y, _tempFullscreen);
                    _inOptionsMenu = false;
                }
                
                if (keyState.IsKeyDown(Keys.Escape) && !_previousKeyState.IsKeyDown(Keys.Escape))
                {
                    _inOptionsMenu = false;
                }
            }
            
            _previousKeyState = keyState;
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
                                var matchScreen = new MatchScreen(homeTeam, awayTeam, nextMatch, _championship, _database, _screenManager,_contentManager);
                                if (_graphicsDevice != null)
                                {
                                    matchScreen.SetGraphicsDevice(_graphicsDevice);
                                }
                                _screenManager.PushScreen(matchScreen);
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
                    _inOptionsMenu = true;
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
                spriteBatch.Draw(_grassTexture, new Rectangle(0, 0, screenWidth, screenHeight), Color.White);
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
            int grassSize = 20;
            
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
                // Draw main menu centered
                string title = "NO PASARAN! - ΜΕΝΟΥ ΑΓΩΝΩΝ";
                Vector2 titleSize = font.MeasureString(title);
                Vector2 titlePos = new Vector2((screenWidth - titleSize.X) / 2, screenHeight * 0.2f);
                spriteBatch.DrawString(font, title, titlePos, Color.Yellow);
                
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
                    Vector2 msgPos = new Vector2((screenWidth - msgSize.X) / 2, screenHeight * 0.3f);
                    spriteBatch.DrawString(font, completeMsg, msgPos, Color.LightGreen);
                    
                    string useNewSeason = "ΕΠΙΛΕΞΤΕ 'ΝΕΟ ΠΡΩΤΑΘΛΗΜΑ' ΓΙΑ ΝΕΟ ΞΕΚΙΝΗΜΑ";
                    Vector2 useSize = font.MeasureString(useNewSeason);
                    Vector2 usePos = new Vector2((screenWidth - useSize.X) / 2, screenHeight * 0.3f + 30);
                    spriteBatch.DrawString(font, useNewSeason, usePos, Color.Gray);
                }
                
                // Draw menu options centered
                float menuStartY = screenHeight * 0.45f;
                for (int i = 0; i < _menuOptions.Length; i++)
                {
                    var prefix = i == _selectedOption ? "> " : "  ";
                    var text = prefix + _menuOptions[i]+ (i == _selectedOption ?" <":"  ");
                    
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
            }
            else
            {
                // Draw options menu centered
                string title = "ΕΠΙΛΟΓΕΣ";
                Vector2 titleSize = font.MeasureString(title);
                Vector2 titlePos = new Vector2((screenWidth - titleSize.X) / 2, screenHeight * 0.2f);
                spriteBatch.DrawString(font, title, titlePos, Color.Yellow);
                
                // Draw resolution options
                var resolutions = Game1.GetAvailableResolutions();
                float menuStartY = screenHeight * 0.35f;
                
                spriteBatch.DrawString(font, "ΑΝΑΛΥΣΗ:", new Vector2(screenWidth * 0.3f, menuStartY), Color.White);
                
                for (int i = 0; i < resolutions.Length; i++)
                {
                    var prefix = i == _selectedResolution ? "> " : "  ";
                    var text = $"{prefix}{resolutions[i].X}x{resolutions[i].Y}";
                    var color = i == _selectedResolution ? Color.Yellow : Color.White;
                    
                    spriteBatch.DrawString(font, text, new Vector2(screenWidth * 0.35f, menuStartY + 40 + i * 35), color);
                }
                
                // Draw fullscreen toggle
                string fsText = _tempFullscreen ? "ΠΛΗΡΗΣ ΟΘΟΝΗ: ΕΝΕΡΓΟ (ΠΑΤΑ F)" : "ΠΛΗΡΗΣ ΟΘΟΝΗ: ΑΝΕΝΕΡΓΟ (ΠΑΤΑ F)";
                Vector2 fsSize = font.MeasureString(fsText);
                spriteBatch.DrawString(font, fsText, new Vector2((screenWidth - fsSize.X) / 2, menuStartY + 40 + resolutions.Length * 35 + 40), Color.Cyan);
                
                // Draw instructions
                string instructions = "ΠΑΤΑ ENTER ΓΙΑ ΕΦΑΡΜΟΓΗ, ESC ΓΙΑ ΑΚΥΡΩΣΗ";
                Vector2 instrSize = font.MeasureString(instructions);
                spriteBatch.DrawString(font, instructions, new Vector2((screenWidth - instrSize.X) / 2, screenHeight * 0.85f), Color.Gray);
            }
        }
    }
}
