using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Models;
using NoPasaranFC.Database;

namespace NoPasaranFC.Screens
{
    public class MenuScreen : Screen
    {
        private Championship _championship;
        private DatabaseManager _database;
        private ScreenManager _screenManager;
        private GraphicsDevice _graphicsDevice;
        private KeyboardState _previousKeyState;
        private int _selectedOption;
        private readonly string[] _menuOptions = { "View Standings", "Play Next Match", "Options", "Exit" };
        private bool _inOptionsMenu = false;
        private int _selectedResolution = 2; // Default to 1280x720
        private bool _tempFullscreen = false;
        
        public bool ShouldExit { get; private set; }
        
        public MenuScreen(Championship championship, DatabaseManager database, ScreenManager screenManager, GraphicsDevice graphicsDevice = null)
        {
            _championship = championship;
            _database = database;
            _screenManager = screenManager;
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
                                var matchScreen = new MatchScreen(homeTeam, awayTeam, nextMatch, _championship, _database, _screenManager);
                                if (_graphicsDevice != null)
                                {
                                    matchScreen.SetGraphicsDevice(_graphicsDevice);
                                }
                                _screenManager.PushScreen(matchScreen);
                            }
                        }
                    }
                    break;
                
                case 2: // Options
                    _inOptionsMenu = true;
                    break;
                    
                case 3: // Exit
                    ShouldExit = true;
                    break;
            }
        }
        
        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            
            if (!_inOptionsMenu)
            {
                // Draw main menu centered
                string title = "NO PASARAN! - Championship Menu";
                Vector2 titleSize = font.MeasureString(title);
                Vector2 titlePos = new Vector2((screenWidth - titleSize.X) / 2, screenHeight * 0.2f);
                spriteBatch.DrawString(font, title, titlePos, Color.Yellow);
                
                // Draw menu options centered
                float menuStartY = screenHeight * 0.4f;
                for (int i = 0; i < _menuOptions.Length; i++)
                {
                    var prefix = i == _selectedOption ? "> " : "  ";
                    var text = prefix + _menuOptions[i];
                    var color = i == _selectedOption ? Color.Yellow : Color.White;
                    
                    Vector2 textSize = font.MeasureString(text);
                    Vector2 textPos = new Vector2((screenWidth - textSize.X) / 2, menuStartY + i * 50);
                    spriteBatch.DrawString(font, text, textPos, color);
                }
            }
            else
            {
                // Draw options menu centered
                string title = "OPTIONS";
                Vector2 titleSize = font.MeasureString(title);
                Vector2 titlePos = new Vector2((screenWidth - titleSize.X) / 2, screenHeight * 0.2f);
                spriteBatch.DrawString(font, title, titlePos, Color.Yellow);
                
                // Draw resolution options
                var resolutions = Game1.GetAvailableResolutions();
                float menuStartY = screenHeight * 0.35f;
                
                spriteBatch.DrawString(font, "Resolution:", new Vector2(screenWidth * 0.3f, menuStartY), Color.White);
                
                for (int i = 0; i < resolutions.Length; i++)
                {
                    var prefix = i == _selectedResolution ? "> " : "  ";
                    var text = $"{prefix}{resolutions[i].X}x{resolutions[i].Y}";
                    var color = i == _selectedResolution ? Color.Yellow : Color.White;
                    
                    spriteBatch.DrawString(font, text, new Vector2(screenWidth * 0.35f, menuStartY + 40 + i * 35), color);
                }
                
                // Draw fullscreen toggle
                string fsText = _tempFullscreen ? "Fullscreen: ON (Press F)" : "Fullscreen: OFF (Press F)";
                Vector2 fsSize = font.MeasureString(fsText);
                spriteBatch.DrawString(font, fsText, new Vector2((screenWidth - fsSize.X) / 2, menuStartY + 40 + resolutions.Length * 35 + 40), Color.Cyan);
                
                // Draw instructions
                string instructions = "Press ENTER to apply, ESC to cancel";
                Vector2 instrSize = font.MeasureString(instructions);
                spriteBatch.DrawString(font, instructions, new Vector2((screenWidth - instrSize.X) / 2, screenHeight * 0.85f), Color.Gray);
            }
        }
    }
}
