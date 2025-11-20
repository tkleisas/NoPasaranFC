using System;
using System.Collections.Generic;
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
        private const int MaxVisiblePlayers = 15;
        
        private Gameplay.InputHelper _input;
        private Texture2D _pixel;
        
        // Formation positions for preview (simple 4-4-2)
        private readonly Vector2[] _formationPositions = new[]
        {
            new Vector2(160, 380), // GK
            new Vector2(260, 320), new Vector2(260, 360), new Vector2(260, 400), new Vector2(260, 440), // DEF (4)
            new Vector2(360, 320), new Vector2(360, 360), new Vector2(360, 400), new Vector2(360, 440), // MID (4)
            new Vector2(460, 360), new Vector2(460, 400) // FWD (2)
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

        public override void Update(GameTime gameTime)
        {
            _input.Update();
            
            // Navigation
            if (_input.IsMenuDownPressed())
            {
                _selectedIndex = (_selectedIndex + 1) % _allPlayers.Count;
                
                // Auto-scroll
                if (_selectedIndex - _scrollOffset >= MaxVisiblePlayers)
                {
                    _scrollOffset++;
                }
                
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
            }
            else if (_input.IsMenuUpPressed())
            {
                _selectedIndex = (_selectedIndex - 1 + _allPlayers.Count) % _allPlayers.Count;
                
                // Auto-scroll
                if (_selectedIndex < _scrollOffset)
                {
                    _scrollOffset--;
                }
                
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
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
            
            // Toggle starting status (Space or X button)
            if (_input.IsSwitchPlayerPressed())
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
            
            // Confirm lineup and start match (Enter or A button)
            if (_input.IsConfirmPressed())
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
                    _screenManager.PushScreen(matchScreen);
                }
            }
            
            // Cancel (Escape or B button)
            if (_input.IsBackPressed())
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
            string title = $"ΕΠΙΛΟΓΗ ΣΥΝΘΕΣΗΣ - {_team.Name}";
            Vector2 titleSize = font.MeasureString(title);
            spriteBatch.DrawString(font, title, new Vector2((screenWidth - titleSize.X) / 2, 20), Color.Yellow);
            
            // Starting count indicator
            int startingCount = _allPlayers.Count(p => p.IsStarting);
            string countText = $"ΒΑΣΙΚΟΙ: {startingCount}/11";
            Color countColor = startingCount == 11 ? Color.LightGreen : (startingCount < 11 ? Color.Yellow : Color.Red);
            Vector2 countSize = font.MeasureString(countText);
            spriteBatch.DrawString(font, countText, new Vector2((screenWidth - countSize.X) / 2, 60), countColor);
            
            // Draw player list (left side)
            DrawPlayerList(spriteBatch, font);
            
            // Draw formation preview (right side)
            DrawFormationPreview(spriteBatch, font, screenWidth, screenHeight);
            
            // Draw instructions
            DrawInstructions(spriteBatch, font, screenHeight);
        }

        private void DrawPlayerList(SpriteBatch spriteBatch, SpriteFont font)
        {
            int startY = 120;
            int lineHeight = 35;
            int xPos = 50;
            
            // Column headers
            spriteBatch.DrawString(font, "#", new Vector2(xPos, startY - 30), Color.Gray);
            spriteBatch.DrawString(font, "NAME", new Vector2(xPos + 60, startY - 30), Color.Gray);
            spriteBatch.DrawString(font, "POS", new Vector2(xPos + 270, startY - 30), Color.Gray);
            spriteBatch.DrawString(font, "STATUS", new Vector2(xPos + 360, startY - 30), Color.Gray);
            
            // Scroll indicators
            if (_scrollOffset > 0)
            {
                spriteBatch.DrawString(font, "^ MORE", new Vector2(xPos + 180, startY - 30), Color.Gray);
            }
            
            int endIndex = Math.Min(_scrollOffset + MaxVisiblePlayers, _allPlayers.Count);
            
            for (int i = _scrollOffset; i < endIndex; i++)
            {
                var player = _allPlayers[i];
                int yPos = startY + (i - _scrollOffset) * lineHeight;
                
                Color bgColor = i == _selectedIndex ? new Color(80, 120, 80, 200) : new Color(40, 60, 40, 100);
                spriteBatch.Draw(_pixel, new Rectangle(xPos - 5, yPos - 5, 550, lineHeight), bgColor);
                
                Color textColor = i == _selectedIndex ? Color.Yellow : Color.White;
                
                // Shirt number
                spriteBatch.DrawString(font, player.ShirtNumber.ToString(), new Vector2(xPos, yPos), textColor);
                
                // Name (truncated if too long)
                string displayName = player.Name.Length > 15 ? player.Name.Substring(0, 15) : player.Name;
                spriteBatch.DrawString(font, displayName, new Vector2(xPos + 60, yPos), textColor);
                
                // Position
                string posText = GetPositionAbbreviation(player.Position);
                spriteBatch.DrawString(font, posText, new Vector2(xPos + 270, yPos), textColor);
                
                // Starting status
                string statusText = player.IsStarting ? "[STARTER]" : "[BENCH]";
                Color statusColor = player.IsStarting ? Color.LightGreen : Color.Gray;
                if (i == _selectedIndex) statusColor = Color.Yellow;
                spriteBatch.DrawString(font, statusText, new Vector2(xPos + 360, yPos), statusColor);
            }
            
            if (endIndex < _allPlayers.Count)
            {
                int yPos = startY + MaxVisiblePlayers * lineHeight;
                spriteBatch.DrawString(font, "v MORE", new Vector2(xPos + 180, yPos), Color.Gray);
            }
        }

        private void DrawFormationPreview(SpriteBatch spriteBatch, SpriteFont font, int screenWidth, int screenHeight)
        {
            int previewX = screenWidth - 350;
            int previewY = 120;
            int previewWidth = 300;
            int previewHeight = 400;
            
            // Preview box
            spriteBatch.Draw(_pixel, new Rectangle(previewX, previewY, previewWidth, previewHeight), 
                new Color(30, 50, 30, 200));
            
            // Title
            string previewTitle = "FORMATION PREVIEW";
            Vector2 titleSize = font.MeasureString(previewTitle);
            spriteBatch.DrawString(font, previewTitle, 
                new Vector2(previewX + (previewWidth - titleSize.X) / 2, previewY + 10), Color.White);
            
            // Draw starting players on formation
            var startingPlayers = _allPlayers.Where(p => p.IsStarting).OrderBy(p => GetPositionOrder(p.Position)).ToList();
            
            for (int i = 0; i < Math.Min(startingPlayers.Count, 11); i++)
            {
                var player = startingPlayers[i];
                Vector2 pos = new Vector2(previewX + _formationPositions[i].X - 140, 
                                         previewY + _formationPositions[i].Y - 310);
                
                // Player circle
                spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 12, (int)pos.Y - 12, 24, 24), Color.DarkBlue);
                spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 10, (int)pos.Y - 10, 20, 20), Color.LightBlue);
                
                // Shirt number
                string numText = player.ShirtNumber.ToString();
                Vector2 numSize = font.MeasureString(numText);
                spriteBatch.DrawString(font, numText, 
                    new Vector2(pos.X - numSize.X / 2, pos.Y - numSize.Y / 2 - 2), 
                    Color.White, 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
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
            string instructions = "UP/DOWN: Navigate | SPACE: Toggle Starter | ENTER: Confirm | ESC: Back";
            Vector2 instrSize = font.MeasureString(instructions);
            spriteBatch.DrawString(font, instructions, 
                new Vector2((Game1.ScreenWidth - instrSize.X) / 2, screenHeight - 30), 
                Color.LightGray);
        }

        private string GetPositionAbbreviation(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Goalkeeper => "GK",
                PlayerPosition.Defender => "DEF",
                PlayerPosition.Midfielder => "MID",
                PlayerPosition.Forward => "FWD",
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
