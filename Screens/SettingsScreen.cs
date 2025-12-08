using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using NoPasaranFC.Models;
using NoPasaranFC.Database;

namespace NoPasaranFC.Screens
{
    public class SettingsScreen : Screen
    {
        private readonly DatabaseManager _database;
        private readonly Game1 _game;
        private readonly GameSettings _settings;
        
        private int _selectedOption = 0;
        private int _scrollOffset = 0;
        private const int MaxVisibleOptions = 11;
        
        private string[] GetMenuOptions()
        {
            var loc = Models.Localization.Instance;
            return new[]
            {
                loc.Get("settings.resolution"),
                loc.Get("settings.fullscreen"),
                loc.Get("settings.vsync"),
                loc.Get("settings.masterVolume"),
                loc.Get("settings.musicVolume"),
                loc.Get("settings.sfxVolume"),
                loc.Get("settings.muteAll"),
                loc.Get("settings.difficulty"),
                loc.Get("settings.matchDuration"),
                loc.Get("settings.playerSpeed"),
                loc.Get("settings.showMinimap"),
                loc.Get("settings.showNames"),
                loc.Get("settings.showStamina"),
                loc.Get("settings.cameraZoom"),
                loc.Get("settings.cameraSpeed"),
                loc.Get("settings.languageSelect"),
                "Back" // Keep Back in English for now
            };
        }
        
        private KeyboardState _previousKeyState;
        private int _resolutionIndex;
        private readonly Point[] _resolutions;
        private readonly string[] _languages = new[] { "en", "el" };
        private int _languageIndex;

        public SettingsScreen(DatabaseManager database, Game1 game, ContentManager content, GraphicsDevice graphicsDevice)
            : base(content, graphicsDevice)
        {
            System.Diagnostics.Debug.WriteLine("SettingsScreen: Constructor start");
            _database = database;
            _game = game;
            _settings = GameSettings.Instance;
            _resolutions = Game1.GetAvailableResolutions();
            
            // Find current resolution index
            _resolutionIndex = 2; // Default to 1280x720
            for (int i = 0; i < _resolutions.Length; i++)
            {
                if (_resolutions[i].X == _settings.ResolutionWidth && 
                    _resolutions[i].Y == _settings.ResolutionHeight)
                {
                    _resolutionIndex = i;
                    break;
                }
            }
            
            // Find current language index
            _languageIndex = Array.IndexOf(_languages, _settings.Language);
            if (_languageIndex == -1) _languageIndex = 0;
            System.Diagnostics.Debug.WriteLine("SettingsScreen: Constructor complete");
        }
        private Gameplay.InputHelper _input = new Gameplay.InputHelper();
        private float _joystickMenuCooldown = 0f;

        public override void Update(GameTime gameTime)
        {
            _input.Update();
            var keyState = Keyboard.GetState();
            var touchUI = Gameplay.TouchUI.Instance;
            
            // Touch/Joystick navigation with cooldown
            Vector2 joystickDir = touchUI.JoystickDirection;
            bool menuDown = (keyState.IsKeyDown(Keys.Down) && !_previousKeyState.IsKeyDown(Keys.Down)) || 
                           _input.IsMenuDownPressed() || 
                           (touchUI.Enabled && joystickDir.Y > 0.5f && _joystickMenuCooldown <= 0);
            bool menuUp = (keyState.IsKeyDown(Keys.Up) && !_previousKeyState.IsKeyDown(Keys.Up)) || 
                         _input.IsMenuUpPressed() || 
                         (touchUI.Enabled && joystickDir.Y < -0.5f && _joystickMenuCooldown <= 0);
            bool menuLeft = (keyState.IsKeyDown(Keys.Left) && !_previousKeyState.IsKeyDown(Keys.Left)) ||
                           (touchUI.Enabled && joystickDir.X < -0.5f && _joystickMenuCooldown <= 0);
            bool menuRight = (keyState.IsKeyDown(Keys.Right) && !_previousKeyState.IsKeyDown(Keys.Right)) ||
                            (touchUI.Enabled && joystickDir.X > 0.5f && _joystickMenuCooldown <= 0);
            
            // Update cooldown
            if (_joystickMenuCooldown > 0)
                _joystickMenuCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            if (menuDown)
            {
                int menuLength = GetMenuOptions().Length;
                _selectedOption = (_selectedOption + 1) % menuLength;
                
                // Auto-scroll
                if (_selectedOption - _scrollOffset >= MaxVisibleOptions)
                {
                    _scrollOffset++;
                }
                _joystickMenuCooldown = 0.15f;
            }
            else if (menuUp)
            {
                int menuLength = GetMenuOptions().Length;
                _selectedOption = (_selectedOption - 1 + menuLength) % menuLength;
                
                // Auto-scroll
                if (_selectedOption < _scrollOffset)
                {
                    _scrollOffset--;
                }
                _joystickMenuCooldown = 0.15f;
            }
            else if ((keyState.IsKeyDown(Keys.Enter) && !_previousKeyState.IsKeyDown(Keys.Enter)) || 
                     _input.IsConfirmPressed() || touchUI.IsActionJustPressed)
            {
                HandleSelection();
            }
            else if (menuLeft)
            {
                AdjustValue(-1);
                _joystickMenuCooldown = 0.15f;
            }
            else if (menuRight)
            {
                AdjustValue(1);
                _joystickMenuCooldown = 0.15f;
            }
            else if (keyState.IsKeyDown(Keys.PageDown) && !_previousKeyState.IsKeyDown(Keys.PageDown))
            {
                // Jump down by visible amount
                int menuLength = GetMenuOptions().Length;
                _selectedOption = Math.Min(_selectedOption + MaxVisibleOptions, menuLength - 1);
                _scrollOffset = Math.Max(0, _selectedOption - MaxVisibleOptions + 1);
            }
            else if (keyState.IsKeyDown(Keys.PageUp) && !_previousKeyState.IsKeyDown(Keys.PageUp))
            {
                // Jump up by visible amount
                _selectedOption = Math.Max(_selectedOption - MaxVisibleOptions, 0);
                _scrollOffset = Math.Max(0, _selectedOption);
            }
            
            // Back button (Escape, B, or touch B)
            if (_input.IsBackPressed() || touchUI.IsBackJustPressed)
            {
                HandleSelection(); // When on "Back" option, this will exit
                if (_selectedOption != GetMenuOptions().Length - 1)
                {
                    // If not on Back option, go back anyway
                    IsFinished = true;
                }
            }
            
            _previousKeyState = keyState;
        }

        private void AdjustValue(int direction)
        {
            switch (_selectedOption)
            {
                case 0: // Resolution
                    _resolutionIndex = Math.Clamp(_resolutionIndex + direction, 0, _resolutions.Length - 1);
                    _settings.ResolutionWidth = _resolutions[_resolutionIndex].X;
                    _settings.ResolutionHeight = _resolutions[_resolutionIndex].Y;
                    _game.ApplyResolution(_settings.ResolutionWidth, _settings.ResolutionHeight, _settings.IsFullscreen);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 3: // Master Volume
                    _settings.MasterVolume = Math.Clamp(_settings.MasterVolume + direction * 0.1f, 0f, 1f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 4: // Music Volume
                    _settings.MusicVolume = Math.Clamp(_settings.MusicVolume + direction * 0.1f, 0f, 1f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 5: // SFX Volume
                    _settings.SfxVolume = Math.Clamp(_settings.SfxVolume + direction * 0.1f, 0f, 1f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 7: // Difficulty
                    _settings.Difficulty = Math.Clamp(_settings.Difficulty + direction, 0, 2);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 8: // Match Duration
                    _settings.MatchDurationMinutes = Math.Clamp(_settings.MatchDurationMinutes + direction * 0.5f, 1f, 10f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 9: // Player Speed
                    _settings.PlayerSpeedMultiplier = Math.Clamp(_settings.PlayerSpeedMultiplier + direction * 0.1f, 0.5f, 2f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 13: // Camera Zoom
                    _settings.CameraZoom = Math.Clamp(_settings.CameraZoom + direction * 0.1f, 0.1f, 2f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 14: // Camera Speed
                    _settings.CameraSpeed = Math.Clamp(_settings.CameraSpeed + direction * 0.05f, 0.05f, 0.5f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 15: // Language
                    _languageIndex = (_languageIndex + direction + _languages.Length) % _languages.Length;
                    _settings.Language = _languages[_languageIndex];
                    _database.SaveSettings(_settings);
                    Models.Localization.ReloadLanguage(); // Reload strings for new language
                    break;
            }
        }

        private void HandleSelection()
        {
            switch (_selectedOption)
            {
                case 1: // Fullscreen toggle
                    _settings.IsFullscreen = !_settings.IsFullscreen;
                    _game.ApplyResolution(_settings.ResolutionWidth, _settings.ResolutionHeight, _settings.IsFullscreen);
                    _database.SaveSettings(_settings);
                    break;
                    
                case 2: // VSync toggle
                    _settings.VSync = !_settings.VSync;
                    _database.SaveSettings(_settings);
                    break;
                    
                case 6: // Mute All toggle
                    _settings.MuteAll = !_settings.MuteAll;
                    _database.SaveSettings(_settings);
                    break;
                    
                case 10: // Show Minimap toggle
                    _settings.ShowMinimap = !_settings.ShowMinimap;
                    _database.SaveSettings(_settings);
                    break;
                    
                case 11: // Show Player Names toggle
                    _settings.ShowPlayerNames = !_settings.ShowPlayerNames;
                    _database.SaveSettings(_settings);
                    break;
                    
                case 12: // Show Stamina toggle
                    _settings.ShowStamina = !_settings.ShowStamina;
                    _database.SaveSettings(_settings);
                    break;
                    
                case 16: // Back
                    IsFinished = true;
                    break;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            System.Diagnostics.Debug.WriteLine("SettingsScreen: Draw start");
            var screenCenter = new Vector2(Game1.ScreenWidth / 2, Game1.ScreenHeight / 2);
            
            // Draw title
            var title = Models.Localization.Instance.Get("settings.title");
            System.Diagnostics.Debug.WriteLine("SettingsScreen: About to MeasureString");
            var titleSize = font.MeasureString(title);
            System.Diagnostics.Debug.WriteLine("SettingsScreen: MeasureString complete");
            spriteBatch.DrawString(font, title, 
                new Vector2(screenCenter.X - titleSize.X / 2, 50), Color.Yellow);
            
            // Calculate visible range
            var menuOptions = GetMenuOptions();
            int startIndex = _scrollOffset;
            int endIndex = Math.Min(startIndex + MaxVisibleOptions, menuOptions.Length);
            
            // Draw scroll indicators
            if (startIndex > 0)
            {
                var upArrow = "▲ MORE";
                var upSize = font.MeasureString(upArrow);
                spriteBatch.DrawString(font, upArrow, 
                    new Vector2(screenCenter.X - upSize.X / 2, 100), Color.Gray);
            }
            
            // Draw menu options (only visible ones)
            for (int i = startIndex; i < endIndex; i++)
            {
                var yPos = 150 + (i - startIndex) * 40;
                var color = i == _selectedOption ? Color.Yellow : Color.White;
                
                string optionText = menuOptions[i];
                string valueText = GetValueText(i);
                
                // Draw option name
                spriteBatch.DrawString(font, optionText, 
                    new Vector2(screenCenter.X - 300, yPos), color);
                
                // Draw value
                if (!string.IsNullOrEmpty(valueText))
                {
                    spriteBatch.DrawString(font, valueText, 
                        new Vector2(screenCenter.X + 50, yPos), color);
                }
            }
            
            // Draw down scroll indicator
            if (endIndex < menuOptions.Length)
            {
                var downArrow = "▼ MORE";
                var downSize = font.MeasureString(downArrow);
                spriteBatch.DrawString(font, downArrow, 
                    new Vector2(screenCenter.X - downSize.X / 2, 150 + MaxVisibleOptions * 40), Color.Gray);
            }
            
            // Draw instructions
            var instructions = "1312";
            var instrSize = font.MeasureString(instructions);
            spriteBatch.DrawString(font, instructions, 
                new Vector2(screenCenter.X - instrSize.X / 2, Game1.ScreenHeight - 50), Color.Gray);
        }

        private string GetValueText(int optionIndex)
        {
            var loc = Models.Localization.Instance;
            string on = loc.Get("settings.on");
            string off = loc.Get("settings.off");
            
            return optionIndex switch
            {
                0 => $"{_settings.ResolutionWidth}x{_settings.ResolutionHeight}",
                1 => _settings.IsFullscreen ? on : off,
                2 => _settings.VSync ? on : off,
                3 => $"{_settings.MasterVolume:P0}",
                4 => $"{_settings.MusicVolume:P0}",
                5 => $"{_settings.SfxVolume:P0}",
                6 => _settings.MuteAll ? on : off,
                7 => _settings.Difficulty switch 
                { 
                    0 => loc.Get("settings.difficulty.easy"), 
                    1 => loc.Get("settings.difficulty.normal"), 
                    2 => loc.Get("settings.difficulty.hard"), 
                    _ => loc.Get("settings.difficulty.normal") 
                },
                8 => $"{_settings.MatchDurationMinutes:F1} min",
                9 => $"{_settings.PlayerSpeedMultiplier:F1}x",
                10 => _settings.ShowMinimap ? on : off,
                11 => _settings.ShowPlayerNames ? on : off,
                12 => _settings.ShowStamina ? on : off,
                13 => $"{_settings.CameraZoom:F1}x",
                14 => $"{_settings.CameraSpeed:F2}",
                15 => _settings.Language.ToUpper(),
                16 => "",
                _ => ""
            };
        }
    }
}
