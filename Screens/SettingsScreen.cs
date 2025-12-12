using System;
using System.Collections.Generic;
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
        
        // Setting types enum for platform filtering
        private enum SettingType
        {
            Resolution,
            Fullscreen,
            VSync,
            MasterVolume,
            MusicVolume,
            SfxVolume,
            MuteAll,
            Difficulty,
            MatchDuration,
            PlayerSpeed,
            ShowMinimap,
            ShowNames,
            ShowStamina,
            CameraZoom,
            CameraSpeed,
            Language,
            Back
        }
        
        private List<SettingType> _availableSettings;
        
        private List<SettingType> GetAvailableSettings()
        {
            var settings = new List<SettingType>();
            
#if ANDROID
            // Android-specific settings (no resolution, fullscreen, vsync)
            settings.Add(SettingType.MasterVolume);
            settings.Add(SettingType.MusicVolume);
            settings.Add(SettingType.SfxVolume);
            settings.Add(SettingType.MuteAll);
            settings.Add(SettingType.Difficulty);
            settings.Add(SettingType.MatchDuration);
            settings.Add(SettingType.PlayerSpeed);
            settings.Add(SettingType.ShowMinimap);
            settings.Add(SettingType.ShowNames);
            settings.Add(SettingType.ShowStamina);
            settings.Add(SettingType.CameraZoom);
            settings.Add(SettingType.CameraSpeed);
            settings.Add(SettingType.Language);
            settings.Add(SettingType.Back);
#else
            // Desktop settings (all options)
            settings.Add(SettingType.Resolution);
            settings.Add(SettingType.Fullscreen);
            settings.Add(SettingType.VSync);
            settings.Add(SettingType.MasterVolume);
            settings.Add(SettingType.MusicVolume);
            settings.Add(SettingType.SfxVolume);
            settings.Add(SettingType.MuteAll);
            settings.Add(SettingType.Difficulty);
            settings.Add(SettingType.MatchDuration);
            settings.Add(SettingType.PlayerSpeed);
            settings.Add(SettingType.ShowMinimap);
            settings.Add(SettingType.ShowNames);
            settings.Add(SettingType.ShowStamina);
            settings.Add(SettingType.CameraZoom);
            settings.Add(SettingType.CameraSpeed);
            settings.Add(SettingType.Language);
            settings.Add(SettingType.Back);
#endif
            return settings;
        }
        
        private string[] GetMenuOptions()
        {
            var loc = Models.Localization.Instance;
            var options = new List<string>();
            
            foreach (var setting in _availableSettings)
            {
                options.Add(setting switch
                {
                    SettingType.Resolution => loc.Get("settings.resolution"),
                    SettingType.Fullscreen => loc.Get("settings.fullscreen"),
                    SettingType.VSync => loc.Get("settings.vsync"),
                    SettingType.MasterVolume => loc.Get("settings.masterVolume"),
                    SettingType.MusicVolume => loc.Get("settings.musicVolume"),
                    SettingType.SfxVolume => loc.Get("settings.sfxVolume"),
                    SettingType.MuteAll => loc.Get("settings.muteAll"),
                    SettingType.Difficulty => loc.Get("settings.difficulty"),
                    SettingType.MatchDuration => loc.Get("settings.matchDuration"),
                    SettingType.PlayerSpeed => loc.Get("settings.playerSpeed"),
                    SettingType.ShowMinimap => loc.Get("settings.showMinimap"),
                    SettingType.ShowNames => loc.Get("settings.showNames"),
                    SettingType.ShowStamina => loc.Get("settings.showStamina"),
                    SettingType.CameraZoom => loc.Get("settings.cameraZoom"),
                    SettingType.CameraSpeed => loc.Get("settings.cameraSpeed"),
                    SettingType.Language => loc.Get("settings.languageSelect"),
                    SettingType.Back => loc.Get("menu.back"),
                    _ => ""
                });
            }
            
            return options.ToArray();
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
            _availableSettings = GetAvailableSettings();
            
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
            
            // Touch/Joystick navigation with cooldown (threshold 0.3 for responsiveness)
            Vector2 joystickDir = touchUI.JoystickDirection;
            bool menuDown = (keyState.IsKeyDown(Keys.Down) && !_previousKeyState.IsKeyDown(Keys.Down)) || 
                           _input.IsMenuDownPressed() || 
                           (touchUI.Enabled && joystickDir.Y > 0.3f && _joystickMenuCooldown <= 0);
            bool menuUp = (keyState.IsKeyDown(Keys.Up) && !_previousKeyState.IsKeyDown(Keys.Up)) || 
                         _input.IsMenuUpPressed() || 
                         (touchUI.Enabled && joystickDir.Y < -0.3f && _joystickMenuCooldown <= 0);
            bool menuLeft = (keyState.IsKeyDown(Keys.Left) && !_previousKeyState.IsKeyDown(Keys.Left)) ||
                           (touchUI.Enabled && joystickDir.X < -0.3f && _joystickMenuCooldown <= 0);
            bool menuRight = (keyState.IsKeyDown(Keys.Right) && !_previousKeyState.IsKeyDown(Keys.Right)) ||
                            (touchUI.Enabled && joystickDir.X > 0.3f && _joystickMenuCooldown <= 0);
            
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
            if (_selectedOption < 0 || _selectedOption >= _availableSettings.Count)
                return;
                
            var setting = _availableSettings[_selectedOption];
            
            switch (setting)
            {
                case SettingType.Resolution:
                    _resolutionIndex = Math.Clamp(_resolutionIndex + direction, 0, _resolutions.Length - 1);
                    _settings.ResolutionWidth = _resolutions[_resolutionIndex].X;
                    _settings.ResolutionHeight = _resolutions[_resolutionIndex].Y;
                    _game.ApplyResolution(_settings.ResolutionWidth, _settings.ResolutionHeight, _settings.IsFullscreen);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.MasterVolume:
                    _settings.MasterVolume = Math.Clamp(_settings.MasterVolume + direction * 0.1f, 0f, 1f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.MusicVolume:
                    _settings.MusicVolume = Math.Clamp(_settings.MusicVolume + direction * 0.1f, 0f, 1f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.SfxVolume:
                    _settings.SfxVolume = Math.Clamp(_settings.SfxVolume + direction * 0.1f, 0f, 1f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.Difficulty:
                    _settings.Difficulty = Math.Clamp(_settings.Difficulty + direction, 0, 2);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.MatchDuration:
                    _settings.MatchDurationMinutes = Math.Clamp(_settings.MatchDurationMinutes + direction * 0.5f, 1f, 10f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.PlayerSpeed:
                    _settings.PlayerSpeedMultiplier = Math.Clamp(_settings.PlayerSpeedMultiplier + direction * 0.1f, 0.5f, 2f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.CameraZoom:
                    _settings.CameraZoom = Math.Clamp(_settings.CameraZoom + direction * 0.1f, 0.1f, 2f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.CameraSpeed:
                    _settings.CameraSpeed = Math.Clamp(_settings.CameraSpeed + direction * 0.05f, 0.05f, 0.5f);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.Language:
                    _languageIndex = (_languageIndex + direction + _languages.Length) % _languages.Length;
                    _settings.Language = _languages[_languageIndex];
                    _database.SaveSettings(_settings);
                    Models.Localization.ReloadLanguage();
                    break;
            }
        }

        private void HandleSelection()
        {
            if (_selectedOption < 0 || _selectedOption >= _availableSettings.Count)
                return;
                
            var setting = _availableSettings[_selectedOption];
            
            switch (setting)
            {
                case SettingType.Fullscreen:
                    _settings.IsFullscreen = !_settings.IsFullscreen;
                    _game.ApplyResolution(_settings.ResolutionWidth, _settings.ResolutionHeight, _settings.IsFullscreen);
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.VSync:
                    _settings.VSync = !_settings.VSync;
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.MuteAll:
                    _settings.MuteAll = !_settings.MuteAll;
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.ShowMinimap:
                    _settings.ShowMinimap = !_settings.ShowMinimap;
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.ShowNames:
                    _settings.ShowPlayerNames = !_settings.ShowPlayerNames;
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.ShowStamina:
                    _settings.ShowStamina = !_settings.ShowStamina;
                    _database.SaveSettings(_settings);
                    break;
                    
                case SettingType.Back:
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
            if (optionIndex < 0 || optionIndex >= _availableSettings.Count)
                return "";
                
            var loc = Models.Localization.Instance;
            string on = loc.Get("settings.on");
            string off = loc.Get("settings.off");
            
            var setting = _availableSettings[optionIndex];
            
            return setting switch
            {
                SettingType.Resolution => $"{_settings.ResolutionWidth}x{_settings.ResolutionHeight}",
                SettingType.Fullscreen => _settings.IsFullscreen ? on : off,
                SettingType.VSync => _settings.VSync ? on : off,
                SettingType.MasterVolume => $"{_settings.MasterVolume:P0}",
                SettingType.MusicVolume => $"{_settings.MusicVolume:P0}",
                SettingType.SfxVolume => $"{_settings.SfxVolume:P0}",
                SettingType.MuteAll => _settings.MuteAll ? on : off,
                SettingType.Difficulty => _settings.Difficulty switch 
                { 
                    0 => loc.Get("settings.difficulty.easy"), 
                    1 => loc.Get("settings.difficulty.normal"), 
                    2 => loc.Get("settings.difficulty.hard"), 
                    _ => loc.Get("settings.difficulty.normal") 
                },
                SettingType.MatchDuration => $"{_settings.MatchDurationMinutes:F1} min",
                SettingType.PlayerSpeed => $"{_settings.PlayerSpeedMultiplier:F1}x",
                SettingType.ShowMinimap => _settings.ShowMinimap ? on : off,
                SettingType.ShowNames => _settings.ShowPlayerNames ? on : off,
                SettingType.ShowStamina => _settings.ShowStamina ? on : off,
                SettingType.CameraZoom => $"{_settings.CameraZoom:F1}x",
                SettingType.CameraSpeed => $"{_settings.CameraSpeed:F2}",
                SettingType.Language => _settings.Language.ToUpper(),
                SettingType.Back => "",
                _ => ""
            };
        }
    }
}
