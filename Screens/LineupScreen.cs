using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Database;
using NoPasaranFC.Models;

namespace NoPasaranFC.Screens
{
    public class LineupScreen : Screen
    {
        private readonly Team _team;
        private readonly Match _match;
        private readonly Championship _championship;
        private readonly DatabaseManager _database;
        private readonly ScreenManager _screenManager;
        private readonly ContentManager _contentManager;
        
        private List<Player> _allPlayers;
        private int _selectedIndex = 0;
        private int _scrollOffset = 0;
        private const int MaxVisiblePlayers = 12; // Reduced to make room for stats panel
        
        private Gameplay.InputHelper _input;
        private Texture2D _pixel;
        
        // Player picture cache
        private Dictionary<int, Texture2D> _playerPictureCache = new Dictionary<int, Texture2D>();
        private Texture2D _defaultPlayerPicture;
        
        // Formation positions for preview (simple 4-4-2)
        private readonly Vector2[] _formationPositions = new[]
        {
            new Vector2(160, 380), // GK
            new Vector2(240, 320), new Vector2(240, 360), new Vector2(240, 400), new Vector2(240, 440), // DEF (4)
            new Vector2(320, 320), new Vector2(320, 360), new Vector2(320, 400), new Vector2(320, 440), // MID (4)
            new Vector2(400, 360), new Vector2(400, 400) // FWD (2)
        };

        public LineupScreen(Team team, Match match, Championship championship, DatabaseManager database, 
            ScreenManager screenManager, ContentManager content, GraphicsDevice graphicsDevice)
            : base(content, graphicsDevice)
        {
            _team = team;
            _match = match;
            _championship = championship;
            _database = database;
            _screenManager = screenManager;
            _contentManager = content;
            
            // Get all players sorted by shirt number
            _allPlayers = _team.Players.OrderBy(p => p.ShirtNumber).ToList();
            
            // Initialize input helper
            _input = new Gameplay.InputHelper();
            
            CreatePixelTexture();
        }
        
        public override void OnActivated()
        {
            // Reset input state when screen becomes active to prevent key bleed-through
            _input = new Gameplay.InputHelper();
        }

        private void CreatePixelTexture()
        {
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
        private float _joystickMenuCooldown = 0f;

        public override void Update(GameTime gameTime)
        {
            _input.Update();
            var touchUI = Gameplay.TouchUI.Instance;
            
            // Touch/Joystick navigation with cooldown
            Vector2 joystickDir = touchUI.JoystickDirection;
            bool menuDown = _input.IsMenuDownPressed() || (touchUI.Enabled && joystickDir.Y > 0.5f && _joystickMenuCooldown <= 0);
            bool menuUp = _input.IsMenuUpPressed() || (touchUI.Enabled && joystickDir.Y < -0.5f && _joystickMenuCooldown <= 0);
            
            // Update cooldown
            if (_joystickMenuCooldown > 0)
                _joystickMenuCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Navigation
            if (menuDown)
            {
                _selectedIndex = (_selectedIndex + 1) % _allPlayers.Count;
                
                // Auto-scroll
                if (_selectedIndex - _scrollOffset >= MaxVisiblePlayers-1)
                {
                    _scrollOffset++;
                }
                
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                _joystickMenuCooldown = 0.15f;
            }
            else if (menuUp)
            {
                _selectedIndex = (_selectedIndex - 1 + _allPlayers.Count) % _allPlayers.Count;
                
                // Auto-scroll
                if (_selectedIndex < _scrollOffset)
                {
                    _scrollOffset--;
                }
                
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                _joystickMenuCooldown = 0.15f;
            }
            
            // PageUp/PageDown still use raw keyboard (TODO: add to InputHelper)
            var keyState = Keyboard.GetState();
            
            if (keyState.IsKeyDown(Keys.PageDown))
            {
                _selectedIndex = Math.Min(_allPlayers.Count - 1, _selectedIndex + 15);
                _scrollOffset = Math.Max(0, Math.Min(_scrollOffset + 15, _allPlayers.Count - MaxVisiblePlayers));
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
            }
            else if (keyState.IsKeyDown(Keys.PageUp))
            {
                _selectedIndex = Math.Max(0, _selectedIndex - 15);
                _scrollOffset = Math.Max(0, _scrollOffset - 15);
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
            }
            
            // Toggle starting status (Space, X button, or touch X)
            if (_input.IsSwitchPlayerPressed() || touchUI.IsSwitchJustPressed)
            {
                var player = _allPlayers[_selectedIndex];
                int currentStartingCount = _allPlayers.Count(p => p.IsStarting);
                
                if (player.IsStarting)
                {
                    // Always allow removal (user might be swapping players)
                    player.IsStarting = false;
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_select");
                }
                else
                {
                    // Can add to starting lineup only if less than 11
                    if (currentStartingCount < 11)
                    {
                        player.IsStarting = true;
                        Gameplay.AudioManager.Instance.PlaySoundEffect("menu_select");
                    }
                }
            }
            
            // Confirm lineup and start match (Enter, A button, or touch A)
            if (_input.IsConfirmPressed() || touchUI.IsActionJustPressed)
            {
                int startingCount = _allPlayers.Count(p => p.IsStarting);
                
                if (startingCount == 11)
                {
                    // Save lineup to database
                    foreach (var player in _allPlayers)
                    {
                        _database.SavePlayer(player);
                    }
                    
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_select");
                    
                    // Start the match
                    var homeTeam = _championship.Teams.Find(t => t.Id == _match.HomeTeamId);
                    var awayTeam = _championship.Teams.Find(t => t.Id == _match.AwayTeamId);
                    
                    var matchScreen = new MatchScreen(homeTeam, awayTeam, _match, _championship, 
                        _database, _screenManager, _contentManager);
                    matchScreen.SetGraphicsDevice(GraphicsDevice);
                    _screenManager.PushScreen(matchScreen);
                }
            }
            
            // Cancel (Escape, B button, or touch B)
            if (_input.IsBackPressed() || touchUI.IsBackJustPressed)
            {
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_back");
                IsFinished = true;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            
            // Draw background
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), new Color(20, 40, 20));
            
            // Title
            string title = $"{Localization.Instance.Get("lineup.title")} - {_team.Name}";
            Vector2 titleSize = font.MeasureString(title);
            spriteBatch.DrawString(font, title, new Vector2((screenWidth - titleSize.X) / 2, 20), Color.Yellow);
            
            // Starting count indicator
            int startingCount = _allPlayers.Count(p => p.IsStarting);
            string countText = $"{Localization.Instance.Get("lineup.starting")}: {startingCount}/11";
            Color countColor = startingCount == 11 ? Color.LightGreen : (startingCount < 11 ? Color.Yellow : Color.Red);
            Vector2 countSize = font.MeasureString(countText);
            spriteBatch.DrawString(font, countText, new Vector2((screenWidth - countSize.X) / 2, 50), countColor);
            
            // Draw player list (left side)
            DrawPlayerList(spriteBatch, font);
            
            // Draw selected player details (middle)
            DrawPlayerDetails(spriteBatch, font, screenWidth, screenHeight);
            
            // Draw formation preview (right side)
            DrawFormationPreview(spriteBatch, font, screenWidth, screenHeight);
            
            // Draw instructions
            DrawInstructions(spriteBatch, font, screenHeight);
        }

        private void DrawPlayerList(SpriteBatch spriteBatch, SpriteFont font)
        {
            int startY = 90;
            int lineHeight = 28;
            int xPos = 30;
            bool skipFirst = false;
            // Column headers
            spriteBatch.DrawString(font, "#", new Vector2(xPos, startY - 25), Color.Gray);
            spriteBatch.DrawString(font, Localization.Instance.Get("lineup.name"), new Vector2(xPos + 40, startY - 25), Color.Gray);
            spriteBatch.DrawString(font, Localization.Instance.Get("lineup.position"), new Vector2(xPos + 230, startY - 25), Color.Gray);
            spriteBatch.DrawString(font, Localization.Instance.Get("lineup.status"), new Vector2(xPos + 330, startY - 25), Color.Gray);
            
            // Scroll indicators
            if (_scrollOffset > 0)
            {
                spriteBatch.DrawString(font, $"^ {Localization.Instance.Get("lineup.more")}", new Vector2(xPos + 120, startY), Color.Gray);

            }
            
            int endIndex = Math.Min(_scrollOffset + MaxVisiblePlayers, _allPlayers.Count);
            startY = startY + 25;
            for (int i = _scrollOffset; i < endIndex; i++)
            {
                var player = _allPlayers[i];
                int yPos = startY + (i - _scrollOffset) * lineHeight;
                
                Color bgColor = i == _selectedIndex ? new Color(80, 120, 80, 200) : new Color(40, 60, 40, 100);
                spriteBatch.Draw(_pixel, new Rectangle(xPos - 5, yPos+4, 480, lineHeight), bgColor);
                
                Color textColor = i == _selectedIndex ? Color.Yellow : Color.White;
                
                // Shirt number
                spriteBatch.DrawString(font, player.ShirtNumber.ToString(), new Vector2(xPos, yPos), textColor);
                
                // Name (truncated if too long)
                string displayName = player.Name.Length > 12 ? player.Name.Substring(0, 12) : player.Name;
                spriteBatch.DrawString(font, displayName, new Vector2(xPos + 40, yPos), textColor);
                
                // Position
                string posText = GetPositionAbbreviation(player.Position);
                spriteBatch.DrawString(font, posText, new Vector2(xPos + 240, yPos), textColor);
                
                // Starting status
                string statusText = player.IsStarting ? Localization.Instance.Get("lineup.starter") : Localization.Instance.Get("lineup.benchPlayer");
                Color statusColor = player.IsStarting ? Color.LightGreen : Color.Gray;
                if (i == _selectedIndex) statusColor = Color.Yellow;
                spriteBatch.DrawString(font, statusText, new Vector2(xPos + 330, yPos), statusColor);
            }
            
            if (endIndex < _allPlayers.Count)
            {
                int yPos = startY + MaxVisiblePlayers * lineHeight;
                spriteBatch.DrawString(font, $"v {Localization.Instance.Get("lineup.more")}", new Vector2(xPos + 120, yPos), Color.Gray);
            }
        }

        private void DrawPlayerDetails(SpriteBatch spriteBatch, SpriteFont font, int screenWidth, int screenHeight)
        {
            if (_selectedIndex < 0 || _selectedIndex >= _allPlayers.Count)
                return;
                
            var player = _allPlayers[_selectedIndex];
            
            // Panel position (middle of screen)
            int panelX = 520;
            int panelY = 90;
            int panelWidth = 240;
            int panelHeight = 380;
            
            // Panel background
            spriteBatch.Draw(_pixel, new Rectangle(panelX, panelY, panelWidth, panelHeight), new Color(40, 60, 40, 220));
            spriteBatch.Draw(_pixel, new Rectangle(panelX + 2, panelY + 2, panelWidth - 4, panelHeight - 4), new Color(30, 50, 30, 200));
            
            // Player picture (128x128)
            int pictureX = panelX + (panelWidth - 128) / 2;
            int pictureY = panelY + 10;
            
            Texture2D playerPicture = GetPlayerPicture(player);
            if (playerPicture != null)
            {
                spriteBatch.Draw(playerPicture, new Rectangle(pictureX, pictureY, 128, 128), Color.White);
            }
            else
            {
                // Draw placeholder
                spriteBatch.Draw(_pixel, new Rectangle(pictureX, pictureY, 128, 128), new Color(60, 80, 60));
                string numText = player.ShirtNumber.ToString();
                Vector2 numSize = font.MeasureString(numText);
                spriteBatch.DrawString(font, numText, 
                    new Vector2(pictureX + 64 - numSize.X, pictureY + 64 - numSize.Y), 
                    Color.White, 0f, Vector2.Zero, 2f, SpriteEffects.None, 0f);
            }
            
            // Player name
            int textY = pictureY + 140;
            string name = player.Name.Length > 16 ? player.Name.Substring(0, 16) + "..." : player.Name;
            Vector2 nameSize = font.MeasureString(name);
            spriteBatch.DrawString(font, name, new Vector2(panelX + (panelWidth - nameSize.X) / 2, textY), Color.Yellow);
            
            // Position
            textY += 25;
            string positionText = GetPositionFullName(player.Position);
            Vector2 posSize = font.MeasureString(positionText);
            spriteBatch.DrawString(font, positionText, new Vector2(panelX + (panelWidth - posSize.X) / 2, textY), Color.LightGray);
            
            // Stats header
            textY += 35;
            string statsHeader = Localization.Instance.Get("lineup.stats");
            Vector2 statsHeaderSize = font.MeasureString(statsHeader);
            spriteBatch.DrawString(font, statsHeader, new Vector2(panelX + (panelWidth - statsHeaderSize.X) / 2, textY), Color.White);
            
            // Stats bars
            textY += 25;
            int barX = panelX + 15;
            int barWidth = panelWidth - 30;
            int barHeight = 16;
            int barSpacing = 22;
            
            DrawStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.spd"), player.Speed, barX, textY, barWidth, barHeight);
            textY += barSpacing;
            DrawStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.sht"), player.Shooting, barX, textY, barWidth, barHeight);
            textY += barSpacing;
            DrawStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.pas"), player.Passing, barX, textY, barWidth, barHeight);
            textY += barSpacing;
            DrawStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.def"), player.Defending, barX, textY, barWidth, barHeight);
            textY += barSpacing;
            DrawStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.agi"), player.Agility, barX, textY, barWidth, barHeight);
            textY += barSpacing;
            DrawStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.tec"), player.Technique, barX, textY, barWidth, barHeight);
        }
        
        private void DrawStatBar(SpriteBatch spriteBatch, SpriteFont font, string label, int value, int x, int y, int width, int height)
        {
            // Label
            spriteBatch.DrawString(font, label, new Vector2(x, y), Color.LightGray, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
            
            // Bar background
            int barStartX = x + 45;
            int barWidth = width - 75;
            spriteBatch.Draw(_pixel, new Rectangle(barStartX, y + 2, barWidth, height - 4), new Color(20, 30, 20));
            
            // Bar fill
            float fillPercent = Math.Clamp(value / 100f, 0f, 1f);
            int fillWidth = (int)(barWidth * fillPercent);
            Color barColor = value >= 80 ? Color.LightGreen : (value >= 60 ? Color.Yellow : (value >= 40 ? Color.Orange : Color.Red));
            spriteBatch.Draw(_pixel, new Rectangle(barStartX, y + 2, fillWidth, height - 4), barColor);
            
            // Value text
            spriteBatch.DrawString(font, value.ToString(), new Vector2(barStartX + barWidth + 5, y), Color.White, 0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        }
        
        private Texture2D GetPlayerPicture(Player player)
        {
            if (string.IsNullOrEmpty(player.PlayerPicture))
                return _defaultPlayerPicture;
                
            // Check cache
            if (_playerPictureCache.TryGetValue(player.Id, out var cached))
                return cached;
            
            try
            {
                // Decode base64 to texture
                byte[] imageData = Convert.FromBase64String(player.PlayerPicture);
                using var stream = new MemoryStream(imageData);
                var texture = Texture2D.FromStream(GraphicsDevice, stream);
                _playerPictureCache[player.Id] = texture;
                return texture;
            }
            catch
            {
                return _defaultPlayerPicture;
            }
        }
        
        private string GetPositionFullName(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Goalkeeper => Localization.Instance.Get("lineup.position.goalkeeper"),
                PlayerPosition.Defender => Localization.Instance.Get("lineup.position.defender"),
                PlayerPosition.Midfielder => Localization.Instance.Get("lineup.position.midfielder"),
                PlayerPosition.Forward => Localization.Instance.Get("lineup.position.forward"),
                _ => "?"
            };
        }

        private void DrawFormationPreview(SpriteBatch spriteBatch, SpriteFont font, int screenWidth, int screenHeight)
        {
            int previewX = screenWidth - 420;
            int previewY = 90;
            int previewWidth = 330;
            int previewHeight = 380;
            
            // Preview box
            spriteBatch.Draw(_pixel, new Rectangle(previewX, previewY, previewWidth, previewHeight), 
                new Color(30, 50, 30, 200));
            
            // Title
            string previewTitle = Localization.Instance.Get("lineup.formationPreview");
            Vector2 titleSize = font.MeasureString(previewTitle);
            spriteBatch.DrawString(font, previewTitle, 
                new Vector2(previewX + (previewWidth - titleSize.X) / 2, previewY -30), Color.White);
            
            // Draw starting players on formation
            var startingPlayers = _allPlayers.Where(p => p.IsStarting).OrderBy(p => GetPositionOrder(p.Position)).ToList();
            
            for (int i = 0; i < Math.Min(startingPlayers.Count, 11); i++)
            {
                var player = startingPlayers[i];
                Vector2 pos = new Vector2(previewX + _formationPositions[i].X - 120, 
                                         previewY + _formationPositions[i].Y - 280);
                
                // Player circle
                spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 32, (int)pos.Y - 32, 32, 32), Color.DarkBlue);
                spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 30, (int)pos.Y - 30, 28, 28), Color.Green);
                
                // Shirt number
                string numText = player.ShirtNumber.ToString();
                Vector2 numSize = font.MeasureString(numText);
                spriteBatch.DrawString(font, numText, 
                    new Vector2(pos.X-8 - numSize.X / 2, pos.Y-8 - numSize.Y / 2 - 2), 
                    Color.Red, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            }
            
            // Formation info
            int gkCount = startingPlayers.Count(p => p.Position == PlayerPosition.Goalkeeper);
            int defCount = startingPlayers.Count(p => p.Position == PlayerPosition.Defender);
            int midCount = startingPlayers.Count(p => p.Position == PlayerPosition.Midfielder);
            int fwdCount = startingPlayers.Count(p => p.Position == PlayerPosition.Forward);
            
            string formationText = $"{defCount}-{midCount}-{fwdCount}";
            Vector2 formSize = font.MeasureString(formationText);
            spriteBatch.DrawString(font, formationText, 
                new Vector2(previewX + (previewWidth - formSize.X) / 2, previewY + previewHeight - 40), 
                Color.Yellow);
        }

        private void DrawInstructions(SpriteBatch spriteBatch, SpriteFont font, int screenHeight)
        {
            string instructions = Localization.Instance.Get("lineup.instructions");
            Vector2 instrSize = font.MeasureString(instructions);
            spriteBatch.DrawString(font, instructions, 
                new Vector2((Game1.ScreenWidth - instrSize.X) / 2, screenHeight - 30), 
                Color.LightGray);
        }

        private string GetPositionAbbreviation(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Goalkeeper => Localization.Instance.Get("lineup.position.gk"),
                PlayerPosition.Defender => Localization.Instance.Get("lineup.position.def"),
                PlayerPosition.Midfielder => Localization.Instance.Get("lineup.position.mid"),
                PlayerPosition.Forward => Localization.Instance.Get("lineup.position.fwd"),
                _ => "?"
            };
        }

        private int GetPositionOrder(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Goalkeeper => 0,
                PlayerPosition.Defender => 1,
                PlayerPosition.Midfielder => 2,
                PlayerPosition.Forward => 3,
                _ => 4
            };
        }
    }
}
