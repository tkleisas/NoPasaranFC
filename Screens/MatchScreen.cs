using System;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Models;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Database;

namespace NoPasaranFC.Screens
{
    public class MatchScreen : Screen
    {
        private MatchEngine _matchEngine;
        private Team _homeTeam;
        private Team _awayTeam;
        private Match _match;
        private Championship _championship;
        private DatabaseManager _database;
        private ScreenManager _screenManager;
        private KeyboardState _previousKeyState;
        private Texture2D _pixel;
        private bool _graphicsInitialized;
        private Minimap _minimap;
        
        // Sprite support
        private Texture2D _playerSprite; // Default player sprite
        private Texture2D _ballSprite;
        private Texture2D _grassTexture;
        private SpriteFont _playerNameFont;
        
        // Per-player sprite cache
        private System.Collections.Generic.Dictionary<string, Texture2D> _playerSpriteCache;
        
        public MatchScreen(Team homeTeam, Team awayTeam, Match match, Championship championship, 
            DatabaseManager database, ScreenManager screenManager)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _match = match;
            _championship = championship;
            _database = database;
            _screenManager = screenManager;
            _matchEngine = new MatchEngine(homeTeam, awayTeam, Game1.ScreenWidth, Game1.ScreenHeight);
            _graphicsInitialized = false;
            _minimap = new Minimap(Game1.ScreenWidth, Game1.ScreenHeight, 150, 100); // Minimap 150x100 pixels
            _playerSpriteCache = new System.Collections.Generic.Dictionary<string, Texture2D>();
        }
        
        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            if (!_graphicsInitialized && graphicsDevice != null)
            {
                _pixel = new Texture2D(graphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
                
                // Create grass texture (simple pattern)
                _grassTexture = CreateGrassTexture(graphicsDevice, 64, 64);
                
                // Create default placeholder sprite (64x64 for players, 32x32 for ball)
                _playerSprite = new Texture2D(graphicsDevice, 64, 64);
                Color[] playerData = new Color[64 * 64];
                for (int i = 0; i < playerData.Length; i++)
                    playerData[i] = Color.White;
                _playerSprite.SetData(playerData);
                
                _ballSprite = new Texture2D(graphicsDevice, 32, 32);
                Color[] ballData = new Color[32 * 32];
                for (int i = 0; i < ballData.Length; i++)
                    ballData[i] = Color.White;
                _ballSprite.SetData(ballData);
                
                // Load custom player sprites
                LoadPlayerSprites(graphicsDevice);
                
                _graphicsInitialized = true;
            }
        }
        
        private void LoadPlayerSprites(GraphicsDevice graphicsDevice)
        {
            // Try to load custom sprites for each player from Content/Sprites/Players/
            // If not found, will use default sprite
            
            var allPlayers = _homeTeam.Players.Concat(_awayTeam.Players);
            foreach (var player in allPlayers)
            {
                if (!string.IsNullOrEmpty(player.SpriteFileName) && 
                    !_playerSpriteCache.ContainsKey(player.SpriteFileName))
                {
                    try
                    {
                        // Try to load the sprite from Content folder
                        string spritePath = $"Content/Sprites/Players/{System.IO.Path.GetFileNameWithoutExtension(player.SpriteFileName)}";
                        
                        // For now, we'll create different colored sprites as placeholders
                        // In a real implementation, you would load from file:
                        // var texture = Texture2D.FromFile(graphicsDevice, spritePath);
                        
                        // Create a unique sprite for this player
                        var sprite = CreatePlayerSprite(graphicsDevice, player);
                        _playerSpriteCache[player.SpriteFileName] = sprite;
                    }
                    catch
                    {
                        // If loading fails, the player will use the default sprite
                        // Silently fail to avoid crashes
                    }
                }
            }
        }
        
        private Texture2D CreatePlayerSprite(GraphicsDevice device, Player player)
        {
            // Create a 64x64 sprite with unique characteristics based on player
            var texture = new Texture2D(device, 64, 64);
            Color[] data = new Color[64 * 64];
            
            // Different base colors for different positions
            Color baseColor = player.Position switch
            {
                PlayerPosition.Goalkeeper => new Color(255, 200, 0), // Gold/Yellow
                PlayerPosition.Defender => new Color(70, 130, 180), // Steel Blue
                PlayerPosition.Midfielder => new Color(60, 179, 113), // Medium Sea Green
                PlayerPosition.Forward => new Color(220, 20, 60), // Crimson
                _ => Color.White
            };
            
            // Draw a simple player shape (circle body + smaller head)
            int centerX = 32, centerY = 32;
            
            for (int y = 0; y < 64; y++)
            {
                for (int x = 0; x < 64; x++)
                {
                    int idx = y * 64 + x;
                    float dx = x - centerX;
                    float dy = y - centerY;
                    float distance = (float)Math.Sqrt(dx * dx + dy * dy);
                    
                    // Head (upper circle)
                    if (dy < -8 && distance < 12)
                    {
                        data[idx] = new Color(255, 220, 180); // Skin tone
                    }
                    // Body (larger circle)
                    else if (dy >= -8 && distance < 22)
                    {
                        data[idx] = baseColor;
                    }
                    // Border/outline
                    else if (distance < 24 && distance > 20)
                    {
                        data[idx] = Color.Black;
                    }
                    else
                    {
                        data[idx] = Color.Transparent;
                    }
                }
            }
            
            texture.SetData(data);
            return texture;
        }
        
        private Texture2D CreateGrassTexture(GraphicsDevice device, int width, int height)
        {
            Texture2D texture = new Texture2D(device, width, height);
            Color[] data = new Color[width * height];
            Random rand = new Random(42); // Fixed seed for consistent pattern
            
            // Create grass-like pattern with slight variation
            Color grassBase = new Color(34, 139, 34); // Forest green
            for (int i = 0; i < data.Length; i++)
            {
                int variation = rand.Next(-15, 15);
                data[i] = new Color(
                    Math.Max(0, Math.Min(255, grassBase.R + variation)),
                    Math.Max(0, Math.Min(255, grassBase.G + variation)),
                    Math.Max(0, Math.Min(255, grassBase.B + variation))
                );
            }
            
            texture.SetData(data);
            return texture;
        }
        
        public override void Update(GameTime gameTime)
        {
            if (_matchEngine.IsMatchOver)
            {
                EndMatch();
                return;
            }
            
            var keyState = Keyboard.GetState();
            Vector2 moveDirection = Vector2.Zero;
            
            // Movement controls (arrows)
            if (keyState.IsKeyDown(Keys.Up)) moveDirection.Y -= 1;
            if (keyState.IsKeyDown(Keys.Down)) moveDirection.Y += 1;
            if (keyState.IsKeyDown(Keys.Left)) moveDirection.X -= 1;
            if (keyState.IsKeyDown(Keys.Right)) moveDirection.X += 1;
            
            if (moveDirection != Vector2.Zero)
                moveDirection.Normalize();
            
            // Shoot/Tackle with X
            if (keyState.IsKeyDown(Keys.X) && !_previousKeyState.IsKeyDown(Keys.X))
            {
                _matchEngine.Shoot();
            }
            
            // Switch player with Space
            if (keyState.IsKeyDown(Keys.Space) && !_previousKeyState.IsKeyDown(Keys.Space))
            {
                _matchEngine.SwitchControlledPlayer();
            }
            
            _matchEngine.Update(gameTime, moveDirection);
            _previousKeyState = keyState;
        }
        
        private void EndMatch()
        {
            _match.HomeScore = _matchEngine.HomeScore;
            _match.AwayScore = _matchEngine.AwayScore;
            _match.IsPlayed = true;
            
            // Update team stats
            if (_matchEngine.HomeScore > _matchEngine.AwayScore)
            {
                _homeTeam.Wins++;
                _awayTeam.Losses++;
            }
            else if (_matchEngine.HomeScore < _matchEngine.AwayScore)
            {
                _awayTeam.Wins++;
                _homeTeam.Losses++;
            }
            else
            {
                _homeTeam.Draws++;
                _awayTeam.Draws++;
            }
            
            _homeTeam.GoalsFor += _matchEngine.HomeScore;
            _homeTeam.GoalsAgainst += _matchEngine.AwayScore;
            _awayTeam.GoalsFor += _matchEngine.AwayScore;
            _awayTeam.GoalsAgainst += _matchEngine.HomeScore;
            
            // Save to database
            _database.SaveChampionship(_championship);
            
            // Return to menu
            _screenManager.PopScreen();
        }
        
        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (_pixel == null) return;
            
            var cameraMatrix = _matchEngine.Camera.GetTransformMatrix();
            
            // Begin with camera transformation for world objects
            spriteBatch.End();
            spriteBatch.Begin(transformMatrix: cameraMatrix);
            
            // Draw stadium stands (outside field)
            DrawStadium(spriteBatch);
            
            // Draw field with grass texture
            var fieldRect = new Rectangle(
                (int)MatchEngine.StadiumMargin,
                (int)MatchEngine.StadiumMargin,
                (int)MatchEngine.FieldWidth,
                (int)MatchEngine.FieldHeight
            );
            
            // Tile the grass texture across the field
            int tilesX = (int)(MatchEngine.FieldWidth / 64) + 1;
            int tilesY = (int)(MatchEngine.FieldHeight / 64) + 1;
            for (int x = 0; x < tilesX; x++)
            {
                for (int y = 0; y < tilesY; y++)
                {
                    Rectangle destRect = new Rectangle(
                        (int)MatchEngine.StadiumMargin + x * 64,
                        (int)MatchEngine.StadiumMargin + y * 64,
                        64, 64
                    );
                    spriteBatch.Draw(_grassTexture, destRect, Color.White);
                }
            }
            
            // Draw field markings
            DrawFieldMarkings(spriteBatch);
            
            // Draw goals
            DrawGoals(spriteBatch);
            
            // Draw players
            foreach (var player in _matchEngine.GetAllPlayers())
            {
                DrawPlayer(spriteBatch, player, font);
            }
            
            // Draw referee
            DrawReferee(spriteBatch);
            
            // Draw ball
            var ballPos = _matchEngine.BallPosition;
            DrawBall(spriteBatch, ballPos);
            
            // End camera-transformed drawing
            spriteBatch.End();
            
            // Begin UI drawing (no camera transform)
            spriteBatch.Begin();
            
            // Draw minimap
            _minimap.Draw(spriteBatch, _pixel, _matchEngine, _homeTeam, _awayTeam);
            
            // Draw HUD
            DrawHUD(spriteBatch, font);
        }
        
        private void DrawStadium(SpriteBatch spriteBatch)
        {
            // Draw stadium background
            var stadiumRect = new Rectangle(0, 0, (int)MatchEngine.TotalWidth, (int)MatchEngine.TotalHeight);
            spriteBatch.Draw(_pixel, stadiumRect, new Color(40, 40, 40)); // Dark grey stands
            
            // Draw crowd sections (simplified)
            Color crowdColor = new Color(80, 60, 60);
            // Top stands
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, (int)MatchEngine.TotalWidth, (int)MatchEngine.StadiumMargin), crowdColor);
            // Bottom stands
            spriteBatch.Draw(_pixel, new Rectangle(0, (int)(MatchEngine.StadiumMargin + MatchEngine.FieldHeight), 
                (int)MatchEngine.TotalWidth, (int)MatchEngine.StadiumMargin), crowdColor);
            // Left stands
            spriteBatch.Draw(_pixel, new Rectangle(0, (int)MatchEngine.StadiumMargin, 
                (int)MatchEngine.StadiumMargin, (int)MatchEngine.FieldHeight), crowdColor);
            // Right stands
            spriteBatch.Draw(_pixel, new Rectangle((int)(MatchEngine.StadiumMargin + MatchEngine.FieldWidth), 
                (int)MatchEngine.StadiumMargin, (int)MatchEngine.StadiumMargin, (int)MatchEngine.FieldHeight), crowdColor);
        }
        
        private void DrawFieldMarkings(SpriteBatch spriteBatch)
        {
            float margin = MatchEngine.StadiumMargin;
            float centerX = margin + MatchEngine.FieldWidth / 2;
            float centerY = margin + MatchEngine.FieldHeight / 2;
            Color lineColor = Color.White;
            int lineThickness = 4;
            
            // Outer boundary
            DrawRectangleOutline(spriteBatch, _pixel, 
                new Rectangle((int)margin, (int)margin, (int)MatchEngine.FieldWidth, (int)MatchEngine.FieldHeight), 
                lineColor, lineThickness);
            
            // Center line
            spriteBatch.Draw(_pixel, new Rectangle((int)centerX - 2, (int)margin, lineThickness, (int)MatchEngine.FieldHeight), lineColor);
            
            // Center circle (radius 200)
            DrawCircle(spriteBatch, new Vector2(centerX, centerY), 200f, lineColor, lineThickness);
            
            // Center spot
            DrawCircle(spriteBatch, new Vector2(centerX, centerY), 8f, lineColor, 8);
            
            // Penalty areas
            float penaltyWidth = 800f;   // Width of penalty area
            float penaltyDepth = 300f;   // Depth into field
            float penaltyTop = centerY - penaltyWidth / 2;
            
            // Left penalty area
            DrawRectangleOutline(spriteBatch, _pixel,
                new Rectangle((int)margin, (int)penaltyTop, (int)penaltyDepth, (int)penaltyWidth),
                lineColor, lineThickness);
            
            // Left penalty spot
            Vector2 leftPenaltySpot = new Vector2(margin + 180f, centerY);
            DrawCircle(spriteBatch, leftPenaltySpot, 8f, lineColor, 8);
            
            // Right penalty area
            DrawRectangleOutline(spriteBatch, _pixel,
                new Rectangle((int)(margin + MatchEngine.FieldWidth - penaltyDepth), (int)penaltyTop, (int)penaltyDepth, (int)penaltyWidth),
                lineColor, lineThickness);
            
            // Right penalty spot
            Vector2 rightPenaltySpot = new Vector2(margin + MatchEngine.FieldWidth - 180f, centerY);
            DrawCircle(spriteBatch, rightPenaltySpot, 8f, lineColor, 8);
            
            // Goal areas (6-yard box)
            float goalAreaWidth = 400f;
            float goalAreaDepth = 150f;
            float goalAreaTop = centerY - goalAreaWidth / 2;
            
            // Left goal area
            DrawRectangleOutline(spriteBatch, _pixel,
                new Rectangle((int)margin, (int)goalAreaTop, (int)goalAreaDepth, (int)goalAreaWidth),
                lineColor, lineThickness);
            
            // Right goal area
            DrawRectangleOutline(spriteBatch, _pixel,
                new Rectangle((int)(margin + MatchEngine.FieldWidth - goalAreaDepth), (int)goalAreaTop, (int)goalAreaDepth, (int)goalAreaWidth),
                lineColor, lineThickness);
            
            // Corner arcs
            float cornerRadius = 80f;
            
            // Top-left corner
            DrawArc(spriteBatch, new Vector2(margin, margin), cornerRadius, 0, MathHelper.PiOver2, lineColor, lineThickness);
            
            // Top-right corner
            DrawArc(spriteBatch, new Vector2(margin + MatchEngine.FieldWidth, margin), cornerRadius, MathHelper.PiOver2, MathHelper.Pi, lineColor, lineThickness);
            
            // Bottom-left corner
            DrawArc(spriteBatch, new Vector2(margin, margin + MatchEngine.FieldHeight), cornerRadius, -MathHelper.PiOver2, 0, lineColor, lineThickness);
            
            // Bottom-right corner
            DrawArc(spriteBatch, new Vector2(margin + MatchEngine.FieldWidth, margin + MatchEngine.FieldHeight), cornerRadius, MathHelper.Pi, MathHelper.Pi + MathHelper.PiOver2, lineColor, lineThickness);
        }
        
        private void DrawRectangleOutline(SpriteBatch spriteBatch, Texture2D pixel, Rectangle rect, Color color, int thickness)
        {
            // Top
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            // Bottom
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y + rect.Height - thickness, rect.Width, thickness), color);
            // Left
            spriteBatch.Draw(pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            // Right
            spriteBatch.Draw(pixel, new Rectangle(rect.X + rect.Width - thickness, rect.Y, thickness, rect.Height), color);
        }
        
        private void DrawArc(SpriteBatch spriteBatch, Vector2 center, float radius, float startAngle, float endAngle, Color color, float thickness)
        {
            int segments = 16;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = startAngle + (endAngle - startAngle) * i / segments;
                float angle2 = startAngle + (endAngle - startAngle) * (i + 1) / segments;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;
                
                DrawLine(spriteBatch, p1, p2, color, thickness);
            }
        }
        
        private void DrawGoals(SpriteBatch spriteBatch)
        {
            float margin = MatchEngine.StadiumMargin;
            float centerY = margin + MatchEngine.FieldHeight / 2;
            float goalWidth = 400f;
            float goalDepth = 60f;
            
            // Left goal
            var leftGoal = new Rectangle((int)(margin - goalDepth), (int)(centerY - goalWidth / 2), 
                (int)goalDepth, (int)goalWidth);
            spriteBatch.Draw(_pixel, leftGoal, new Color(200, 200, 200, 200)); // Semi-transparent gray
            
            // Left goal posts
            spriteBatch.Draw(_pixel, new Rectangle((int)margin, (int)(centerY - goalWidth / 2) - 5, 8, 10), Color.White);
            spriteBatch.Draw(_pixel, new Rectangle((int)margin, (int)(centerY + goalWidth / 2) - 5, 8, 10), Color.White);
            
            // Right goal
            var rightGoal = new Rectangle((int)(margin + MatchEngine.FieldWidth), (int)(centerY - goalWidth / 2), 
                (int)goalDepth, (int)goalWidth);
            spriteBatch.Draw(_pixel, rightGoal, new Color(200, 200, 200, 200));
            
            // Right goal posts
            spriteBatch.Draw(_pixel, new Rectangle((int)(margin + MatchEngine.FieldWidth) - 8, (int)(centerY - goalWidth / 2) - 5, 8, 10), Color.White);
            spriteBatch.Draw(_pixel, new Rectangle((int)(margin + MatchEngine.FieldWidth) - 8, (int)(centerY + goalWidth / 2) - 5, 8, 10), Color.White);
        }
        
        private void DrawPlayer(SpriteBatch spriteBatch, Player player, SpriteFont font)
        {
            // Get the appropriate sprite for this player
            Texture2D sprite = _playerSprite; // Default
            if (!string.IsNullOrEmpty(player.SpriteFileName) && 
                _playerSpriteCache.ContainsKey(player.SpriteFileName))
            {
                sprite = _playerSpriteCache[player.SpriteFileName];
            }
            
            Color tintColor;
            
            if (player.IsKnockedDown)
            {
                // Knocked down players are semi-transparent gray
                tintColor = new Color(128, 128, 128, 180);
            }
            else if (player.IsControlled)
            {
                // Controlled player has bright yellow highlight
                tintColor = Color.Yellow;
            }
            else
            {
                // Use team color (Blue for home, Red for away) mixed with sprite color
                Color teamColor = player.TeamId == _homeTeam.Id ? Color.Blue : Color.Red;
                // Blend team color with player's custom sprite color
                tintColor = new Color(
                    (teamColor.R + player.SpriteColor.R) / 2,
                    (teamColor.G + player.SpriteColor.G) / 2,
                    (teamColor.B + player.SpriteColor.B) / 2
                );
            }
            
            var pos = player.FieldPosition;
            
            // 64x64 sprites for more detail
            int size = player.IsControlled ? 68 : 64; // Controlled player slightly larger
            
            // Draw shadow underneath
            Rectangle shadowRect = new Rectangle((int)pos.X - size / 2, (int)pos.Y + size / 2 - 4, size, 6);
            spriteBatch.Draw(_pixel, shadowRect, new Color(0, 0, 0, 100));
            
            // Draw player sprite
            if (player.IsKnockedDown)
            {
                // Draw knocked down player as lying down (flattened)
                Rectangle destRect = new Rectangle((int)pos.X - size / 2, (int)pos.Y - 10, size, 20);
                spriteBatch.Draw(sprite, destRect, tintColor);
            }
            else
            {
                Rectangle destRect = new Rectangle((int)pos.X - size / 2, (int)pos.Y - size / 2, size, size);
                spriteBatch.Draw(sprite, destRect, tintColor);
            }
            
            // Draw selection indicator for controlled player
            if (player.IsControlled && !player.IsKnockedDown)
            {
                // Draw a circle around the controlled player
                DrawCircleOutline(spriteBatch, pos, size / 2 + 5, Color.Yellow, 3);
            }
            
            // Draw player name above sprite (only if not knocked down)
            if (!player.IsKnockedDown)
            {
                string displayName = player.Name.Length > 12 ? player.Name.Substring(0, 12) : player.Name;
                Vector2 nameSize = font.MeasureString(displayName);
                Vector2 namePos = new Vector2(pos.X - nameSize.X / 2, pos.Y - size / 2 - 25);
                
                // Draw name background
                spriteBatch.Draw(_pixel, new Rectangle((int)(namePos.X - 4), (int)(namePos.Y - 2), 
                    (int)(nameSize.X + 8), (int)(nameSize.Y + 4)), new Color(0, 0, 0, 150));
                
                // Draw name text
                spriteBatch.DrawString(font, displayName, namePos, Color.White);
            }
        }
        
        private void DrawCircleOutline(SpriteBatch spriteBatch, Vector2 center, float radius, Color color, float thickness)
        {
            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;
                
                DrawLine(spriteBatch, p1, p2, color, thickness);
            }
        }
        
        private void DrawReferee(SpriteBatch spriteBatch)
        {
            var pos = _matchEngine.RefereePosition;
            int size = 60;
            
            // Draw shadow
            Rectangle shadowRect = new Rectangle((int)pos.X - size / 2, (int)pos.Y + size / 2 - 4, size, 6);
            spriteBatch.Draw(_pixel, shadowRect, new Color(0, 0, 0, 100));
            
            // Draw referee (black and white)
            Rectangle destRect = new Rectangle((int)pos.X - size / 2, (int)pos.Y - size / 2, size, size);
            spriteBatch.Draw(_playerSprite, destRect, Color.Black);
            
            // Add a white stripe pattern for referee shirt
            spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 15, (int)pos.Y - 20, 30, 8), Color.White);
        }
        
        private void DrawBall(SpriteBatch spriteBatch, Vector2 ballPos)
        {
            int ballSize = 32;
            
            // Draw ball shadow
            Rectangle shadowRect = new Rectangle((int)ballPos.X - ballSize / 2, (int)ballPos.Y + ballSize / 2 - 3, ballSize, 6);
            spriteBatch.Draw(_pixel, shadowRect, new Color(0, 0, 0, 120));
            
            // Draw ball (32x32)
            Rectangle ballRect = new Rectangle((int)ballPos.X - ballSize / 2, (int)ballPos.Y - ballSize / 2, ballSize, ballSize);
            spriteBatch.Draw(_ballSprite, ballRect, Color.White);
        }
        
        private void DrawCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color, float thickness)
        {
            // Simple circle drawing using lines (approximate)
            int segments = 32;
            for (int i = 0; i < segments; i++)
            {
                float angle1 = (float)i / segments * MathHelper.TwoPi;
                float angle2 = (float)(i + 1) / segments * MathHelper.TwoPi;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;
                
                DrawLine(spriteBatch, p1, p2, color, thickness);
            }
        }
        
        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, float thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            
            spriteBatch.Draw(_pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), (int)thickness),
                null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
        }
        
        private void DrawHUD(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            
            // Draw score centered at top
            string scoreText = $"{_homeTeam.Name} {_matchEngine.HomeScore} - {_matchEngine.AwayScore} {_awayTeam.Name}";
            Vector2 scoreSize = font.MeasureString(scoreText);
            spriteBatch.DrawString(font, scoreText, new Vector2((screenWidth - scoreSize.X) / 2, 10), Color.White);
            
            // Draw time centered
            string timeText = $"Time: {(int)_matchEngine.MatchTime}'";
            Vector2 timeSize = font.MeasureString(timeText);
            spriteBatch.DrawString(font, timeText, new Vector2((screenWidth - timeSize.X) / 2, screenHeight - 50), Color.White);
            
            // Draw controls centered at bottom
            string controls = "Arrows: Move | Space: Switch | X: Shoot/Tackle";
            Vector2 controlsSize = font.MeasureString(controls);
            spriteBatch.DrawString(font, controls, new Vector2((screenWidth - controlsSize.X) / 2, screenHeight - 25), Color.LightGray);
        }
        
        private void DrawCountdown(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            
            string countdownText;
            if (_matchEngine.CountdownNumber > 0)
            {
                countdownText = _matchEngine.CountdownNumber.ToString();
            }
            else
            {
                countdownText = "GO!";
            }
            
            // Large text in center of screen
            Vector2 textSize = font.MeasureString(countdownText);
            float scale = 3.0f; // Make it 3x larger
            Vector2 scaledSize = textSize * scale;
            Vector2 position = new Vector2((screenWidth - scaledSize.X) / 2, (screenHeight - scaledSize.Y) / 2);
            
            // Draw shadow
            spriteBatch.DrawString(font, countdownText, position + new Vector2(4, 4), Color.Black, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            
            // Draw text
            Color countdownColor = _matchEngine.CountdownNumber > 0 ? Color.Yellow : Color.LightGreen;
            spriteBatch.DrawString(font, countdownText, position, countdownColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
