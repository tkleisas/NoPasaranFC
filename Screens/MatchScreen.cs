using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Database;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using System;
using System.Linq;
using System.Reflection.Metadata;

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
        private InputHelper _input;
        private Texture2D _pixel;
        private bool _graphicsInitialized;
        private Minimap _minimap;
        
        // Sprite support
        private Texture2D _playerSpriteRed;  // Away team sprite sheet (OLD)
        private Texture2D _playerSpriteBlue; // Home team sprite sheet (OLD)
        private Texture2D _ballSprite;
        private Texture2D _grassTexture;
        private SpriteFont _playerNameFont;
        private ContentManager _content;
        
        // New animation system
        private PlayerAnimationSystem _playerAnimSystem;
        
        // Sprite sheet configuration
        private const int SpriteFrameSize = 64; // Each frame is 64x64 in the sprite sheet
        private const int SpritesheetColumns = 4; // 4 animation frames per direction
        private const int SpritesheetRows = 4;    // 4 directions
        
        // Ball sprite sheet configuration
        private const int BallFrameSize = 32;     // Each ball frame is 32x32
        private const int BallSpritesheetColumns = 8; // 8 frames per row
        private const int BallSpritesheetRows = 8;    // 8 rows = 64 total frames
        private float _ballAnimationFrame = 0f;
        
        public MatchScreen(Team homeTeam, Team awayTeam, Match match, Championship championship, 
            DatabaseManager database, ScreenManager screenManager,ContentManager content)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _match = match;
            _championship = championship;
            _database = database;
            _screenManager = screenManager;
            _input = new Gameplay.InputHelper();
            _matchEngine = new MatchEngine(homeTeam, awayTeam, Game1.ScreenWidth, Game1.ScreenHeight);
            _graphicsInitialized = false;
            _minimap = new Minimap(Game1.ScreenWidth, Game1.ScreenHeight, 150, 100); // Minimap 150x100 pixels
            _content = content;
            
            // Switch to match music
            Gameplay.AudioManager.Instance.PlayMusic("match_music");
        }
        
        public void SetGraphicsDevice(GraphicsDevice graphicsDevice)
        {
            if (!_graphicsInitialized && graphicsDevice != null)
            {
                try
                {
                    _pixel = new Texture2D(graphicsDevice, 1, 1);
                    _pixel.SetData(new[] { Color.White });
                    
                    // Create grass texture (simple pattern)
                    _grassTexture = CreateGrassTexture(graphicsDevice, 64, 64);

                    // Load sprite sheets
                    _playerSpriteBlue = _content.Load<Texture2D>("Sprites/player_blue");
                    _playerSpriteRed = _content.Load<Texture2D>("Sprites/player_red");
                    _ballSprite = _content.Load<Texture2D>("Sprites/ball");
                    
                    // Load font for celebration
                    var font = _content.Load<SpriteFont>("Font");
                    
                    // Set celebration resources in match engine
                    _matchEngine.SetCelebrationResources(font, graphicsDevice);
                    
                    // Initialize new animation system (load shared resources once)
                    PlayerAnimationSystem.LoadSharedResources(_content);
                    
                    // Initialize all players with their own animation system instance
                    foreach (var player in _homeTeam.Players.Concat(_awayTeam.Players))
                    {
                        player.AnimationSystem = new PlayerAnimationSystem();
                        player.CurrentAnimationState = "walk";
                    }
                    
                    _graphicsInitialized = true;
                }
                catch (Exception ex)
                {
                    // Log error - for debugging
                    System.IO.File.WriteAllText("sprite_load_error.txt", 
                        $"Error loading sprites: {ex.Message}\n{ex.StackTrace}");
                    throw; // Re-throw to see the actual error
                }
            }
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
            if (_matchEngine.CurrentState == MatchEngine.MatchState.Ended)
            {
                EndMatch();
                return;
            }
            
            _input.Update();
            
            // Get movement direction (supports keyboard arrows/WASD and gamepad left stick/D-pad)
            Vector2 moveDirection = _input.GetMovementDirection();
            
            // Shoot/Tackle (X key or A button)
            bool isShootKeyDown = _input.IsShootButtonDown();
            
            _matchEngine.Update(gameTime, moveDirection, isShootKeyDown);
            
            // Switch player (Space or X button)
            if (_input.IsSwitchPlayerPressed())
            {
                _matchEngine.SwitchControlledPlayer();
            }
            
            // Back to menu (Escape or B button)
            if (_input.IsBackPressed())
            {
                IsFinished = true;
            }
            
            // Update player animations
            UpdatePlayerAnimations(gameTime);
            
            // Update ball animation
            UpdateBallAnimation(gameTime);
        }
        
        private void UpdatePlayerAnimations(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            foreach (var player in _matchEngine.GetAllPlayers())
            {
                // Update new animation system
                if (player.AnimationSystem != null)
                {
                    // Check if shoot/tackle animation is playing
                    if (player.CurrentAnimationState == "shoot" || player.CurrentAnimationState == "tackle")
                    {
                        // Play the animation
                        player.AnimationSystem.PlayAnimation(player.CurrentAnimationState);
                        
                        // Update animation
                        player.AnimationSystem.Update(deltaTime);
                        
                        // Check if animation finished
                        if (player.AnimationSystem.IsAnimationFinished())
                        {
                            // Reset to walk/idle
                            player.CurrentAnimationState = "walk";
                        }
                    }
                    else
                    {
                        // Determine animation state based on movement
                        string newState = "idle";
                        
                        if (player.IsKnockedDown)
                        {
                            newState = "fall";
                        }
                        else if (player.Velocity.LengthSquared() > 0.1f)
                        {
                            newState = "walk";
                        }
                        else
                        {
                            newState = "idle";
                        }
                        
                        player.CurrentAnimationState = newState;
                        
                        // Play the appropriate animation
                        player.AnimationSystem.PlayAnimation(newState);
                        
                        // Calculate rotation based on velocity direction
                        if (player.Velocity.LengthSquared() > 0.1f)
                        {
                            // Get angle from velocity 
                            // Atan2 gives: right=0°, down=90°, left=180°/-180°, up=-90°
                            float angle = (float)Math.Atan2(player.Velocity.Y, player.Velocity.X);
                            
                            // Our sprite rotation mapping:
                            // 0 = up, 1 = up-right, 2 = right, 3 = down-right,
                            // 4 = down, 5 = down-left, 6 = left, 7 = up-left
                            
                            // To map correctly:
                            // velocity up (angle=-90°) should give rotation 0
                            // velocity right (angle=0°) should give rotation 2
                            // velocity down (angle=90°) should give rotation 4
                            // velocity left (angle=180°) should give rotation 6
                            
                            // Add 90 degrees (PiOver2) to shift: up becomes 0°, right becomes 90°, etc.
                            float adjustedAngle = angle + MathHelper.PiOver2;
                            
                            // Normalize to 0-2π range
                            if (adjustedAngle < 0) adjustedAngle += MathHelper.TwoPi;
                            
                            // Convert to 0-7 steps (each step is 45°)
                            // Divide by Pi/4 (45°) and round to nearest
                            int rotation = (int)Math.Round(adjustedAngle / (MathHelper.Pi / 4f)) % 8;
                            
                            player.AnimationSystem.SetRotation(rotation);
                        }
                        
                        // Update the animation
                        player.AnimationSystem.Update(deltaTime);
                    }
                }
                
                // OLD SYSTEM - Update animation based on movement (kept for fallback)
                if (player.Velocity.LengthSquared() > 0.1f)
                {
                    // Moving - animate (advance frames based on time)
                    // AnimationSpeed is frames per second (e.g., 8 fps = 8 frames per second)
                    player.AnimationFrame += 8f * deltaTime; // 8 frames per second animation
                    if (player.AnimationFrame >= SpritesheetColumns)
                        player.AnimationFrame -= SpritesheetColumns; // Loop back
                    
                    // Update direction based on velocity
                    Vector2 vel = player.Velocity;
                    if (Math.Abs(vel.X) > Math.Abs(vel.Y))
                    {
                        // Horizontal movement dominant
                        player.SpriteDirection = vel.X > 0 ? 3 : 2; // Right : Left
                    }
                    else
                    {
                        // Vertical movement dominant
                        player.SpriteDirection = vel.Y > 0 ? 0 : 1; // Down : Up
                    }
                }
                else
                {
                    // Idle - show first frame
                    player.AnimationFrame = 0;
                }
            }
        }
        
        private void UpdateBallAnimation(GameTime gameTime)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Animate ball based on velocity (rolling effect)
            float ballSpeed = _matchEngine.BallVelocity.Length();
            
            if (ballSpeed > 1f)
            {
                // Ball is moving - animate faster based on speed
                // Faster movement = faster animation
                float animationSpeed = 20f * (ballSpeed / 100f); // Adjust speed multiplier
                _ballAnimationFrame += animationSpeed * deltaTime;
                
                // Loop through 64 frames
                if (_ballAnimationFrame >= 64f)
                    _ballAnimationFrame -= 64f;
            }
            else
            {
                // Ball is stationary - show first frame or slow rotation
                _ballAnimationFrame = 0f;
            }
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
            
            // Return to menu music (whistle already played in MatchEngine)
            Gameplay.AudioManager.Instance.PlayMusic("menu_music");
            
            // Return to menu - pop back to MenuScreen (skipping LineupScreen if present)
            _screenManager.PopToScreen<MenuScreen>();
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
            
            // Check if ball is inside goal area (behind goal line)
            var ballPos = _matchEngine.BallPosition;
            float leftGoalLine = MatchEngine.StadiumMargin;
            float rightGoalLine = MatchEngine.StadiumMargin + MatchEngine.FieldWidth;
            float goalTop = MatchEngine.StadiumMargin + (MatchEngine.FieldHeight - MatchEngine.GoalWidth) / 2;
            float goalBottom = goalTop + MatchEngine.GoalWidth;
            
            bool ballInsideLeftGoal = ballPos.X < leftGoalLine && ballPos.Y >= goalTop && ballPos.Y <= goalBottom;
            bool ballInsideRightGoal = ballPos.X > rightGoalLine && ballPos.Y >= goalTop && ballPos.Y <= goalBottom;
            bool ballInsideGoal = ballInsideLeftGoal || ballInsideRightGoal;
            
            // Draw ball BEFORE goalposts if inside goal (so it appears behind the net)
            if (ballInsideGoal)
            {
                DrawBall(spriteBatch, ballPos);
            }
            
            // Draw goals
            DrawGoals(spriteBatch);
            
            // Draw players
            foreach (var player in _matchEngine.GetAllPlayers())
            {
                DrawPlayer(spriteBatch, player, font);
            }
            
            // Draw ball AFTER goalposts if outside goal (normal rendering)
            if (!ballInsideGoal)
            {
                DrawBall(spriteBatch, ballPos);
            }
            
            // End camera-transformed drawing
            spriteBatch.End();
            
            // Begin UI drawing (no camera transform)
            spriteBatch.Begin();
            
            // Draw minimap
            _minimap.Draw(spriteBatch, _pixel, _matchEngine, _homeTeam, _awayTeam);
            
            // Draw HUD
            DrawHUD(spriteBatch, font);
            
            // Draw countdown if in countdown or camera init state
            if (_matchEngine.CurrentState == MatchEngine.MatchState.Countdown)
            {
                DrawCountdown(spriteBatch, font);
            }
            
            // Draw goal celebration
            if (_matchEngine.CurrentState == MatchEngine.MatchState.GoalCelebration)
            {
                DrawGoalCelebration(spriteBatch, font);
            }
            
            // Draw final score overlay
            if (_matchEngine.CurrentState == MatchEngine.MatchState.FinalScore)
            {
                DrawFinalScoreOverlay(spriteBatch, font);
            }
        }
        
        private void DrawGoalCelebration(SpriteBatch spriteBatch, SpriteFont font)
        {
            // Draw ball particles forming "GOAL!"
            _matchEngine.GoalCelebration.Draw(spriteBatch, _ballSprite, Game1.ScreenWidth, Game1.ScreenHeight);
            
            // Draw "GOAL!" text
            if (_matchEngine.GoalCelebration.ShouldDrawGoalText())
            {
                string goalText = Models.Localization.Instance.Get("match.goal");
                float scale = _matchEngine.GoalCelebration.GetGoalTextScale();
                Vector2 textSize = font.MeasureString(goalText);
                Vector2 position = new Vector2(
                    Game1.ScreenWidth / 2 - (textSize.X * scale) / 2,
                    Game1.ScreenHeight / 2 + 150 // Below the ball formation
                );
                
                // Draw text with shadow
                spriteBatch.DrawString(font, goalText, position + new Vector2(4, 4) * scale, 
                    Color.Black * 0.5f, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, goalText, position, 
                    Color.Yellow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
        
        private void DrawFinalScoreOverlay(SpriteBatch spriteBatch, SpriteFont font)
        {
            // Draw semi-transparent background overlay
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, Game1.ScreenWidth, Game1.ScreenHeight), 
                Color.Black * 0.7f);
            
            // Title: Final Score
            string titleText = Models.Localization.Instance.Get("match.finalScore");
            float titleScale = 2.5f;
            Vector2 titleSize = font.MeasureString(titleText);
            Vector2 titlePos = new Vector2(
                Game1.ScreenWidth / 2 - (titleSize.X * titleScale) / 2,
                Game1.ScreenHeight / 2 - 150
            );
            
            // Draw title with shadow
            spriteBatch.DrawString(font, titleText, titlePos + new Vector2(4, 4) * titleScale,
                Color.Black * 0.8f, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, titleText, titlePos,
                Color.Yellow, 0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);
            
            // Score display
            string scoreText = $"{_homeTeam.Name}  {_matchEngine.HomeScore} - {_matchEngine.AwayScore}  {_awayTeam.Name}";
            float scoreScale = 1.8f;
            Vector2 scoreSize = font.MeasureString(scoreText);
            Vector2 scorePos = new Vector2(
                Game1.ScreenWidth / 2 - (scoreSize.X * scoreScale) / 2,
                Game1.ScreenHeight / 2
            );
            
            // Draw score with shadow
            spriteBatch.DrawString(font, scoreText, scorePos + new Vector2(3, 3) * scoreScale,
                Color.Black * 0.8f, 0f, Vector2.Zero, scoreScale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, scoreText, scorePos,
                Color.White, 0f, Vector2.Zero, scoreScale, SpriteEffects.None, 0f);
            
            // Timer indicator (small text showing remaining time)
            string timerText = $"{_matchEngine.FinalScoreTimer:F1}s";
            Vector2 timerSize = font.MeasureString(timerText);
            Vector2 timerPos = new Vector2(
                Game1.ScreenWidth / 2 - timerSize.X / 2,
                Game1.ScreenHeight / 2 + 100
            );
            spriteBatch.DrawString(font, timerText, timerPos, Color.Gray);
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
            int lineThickness = 9; // FIFA regulation: 12cm = ~9px at 73px/m scale
            
            // Outer boundary
            DrawRectangleOutline(spriteBatch, _pixel, 
                new Rectangle((int)margin, (int)margin, (int)MatchEngine.FieldWidth, (int)MatchEngine.FieldHeight), 
                lineColor, lineThickness);
            
            // Center line
            spriteBatch.Draw(_pixel, new Rectangle((int)centerX - 2, (int)margin, lineThickness, (int)MatchEngine.FieldHeight), lineColor);
            
            // Center circle (FIFA: 9.15m radius = 668px @ 73px/m)
            DrawCircle(spriteBatch, new Vector2(centerX, centerY), 668f, lineColor, lineThickness);
            
            // Center spot
            DrawCircle(spriteBatch, new Vector2(centerX, centerY), 10f, lineColor, 10);
            
            // Penalty areas (FIFA: 40.3m wide × 16.5m deep)
            float penaltyWidth = 2942f;   // 40.3m × 73px/m
            float penaltyDepth = 1205f;   // 16.5m × 73px/m
            float penaltyTop = centerY - penaltyWidth / 2;
            
            // Left penalty area
            DrawRectangleOutline(spriteBatch, _pixel,
                new Rectangle((int)margin, (int)penaltyTop, (int)penaltyDepth, (int)penaltyWidth),
                lineColor, lineThickness);
            
            // Left penalty spot (11m from goal line = 803px)
            Vector2 leftPenaltySpot = new Vector2(margin + 803f, centerY);
            DrawCircle(spriteBatch, leftPenaltySpot, 10f, lineColor, 10);
            
            // Right penalty area
            DrawRectangleOutline(spriteBatch, _pixel,
                new Rectangle((int)(margin + MatchEngine.FieldWidth - penaltyDepth), (int)penaltyTop, (int)penaltyDepth, (int)penaltyWidth),
                lineColor, lineThickness);
            
            // Right penalty spot (11m from goal line = 803px)
            Vector2 rightPenaltySpot = new Vector2(margin + MatchEngine.FieldWidth - 803f, centerY);
            DrawCircle(spriteBatch, rightPenaltySpot, 10f, lineColor, 10);
            
            // Goal areas / 6-yard box (FIFA: 18.3m wide × 5.5m deep)
            float goalAreaWidth = 1336f;  // 18.3m × 73px/m
            float goalAreaDepth = 402f;   // 5.5m × 73px/m
            float goalAreaTop = centerY - goalAreaWidth / 2;
            
            // Left goal area
            DrawRectangleOutline(spriteBatch, _pixel,
                new Rectangle((int)margin, (int)goalAreaTop, (int)goalAreaDepth, (int)goalAreaWidth),
                lineColor, lineThickness);
            
            // Right goal area
            DrawRectangleOutline(spriteBatch, _pixel,
                new Rectangle((int)(margin + MatchEngine.FieldWidth - goalAreaDepth), (int)goalAreaTop, (int)goalAreaDepth, (int)goalAreaWidth),
                lineColor, lineThickness);
            
            // Corner arcs (FIFA: 1m radius = 73px)
            float cornerRadius = 73f;
            
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
            
            // Use properly scaled goal dimensions from MatchEngine
            float goalWidth = MatchEngine.GoalWidth;   // 534px (7.32m)
            float goalDepth = MatchEngine.GoalDepth;   // 60px (visual depth)
            float goalHeight = MatchEngine.GoalPostHeight; // 200px (2.44m)
            
            // === LEFT GOAL ===
            DrawGoalStructure(spriteBatch, margin - goalDepth, centerY - goalWidth / 2, 
                goalDepth, goalWidth, goalHeight, true);
            
            // === RIGHT GOAL ===
            DrawGoalStructure(spriteBatch, margin + MatchEngine.FieldWidth, centerY - goalWidth / 2, 
                goalDepth, goalWidth, goalHeight, false);
        }
        
        private void DrawGoalStructure(SpriteBatch spriteBatch, float x, float y, 
            float depth, float width, float height, bool facingRight)
        {
            // Draw goal net with mesh pattern
            DrawGoalNet(spriteBatch, x, y, depth, width, height, facingRight);
            
            // Draw goal posts and crossbar (in front of net)
            // Real goalposts: ~12cm diameter = ~9px at our scale
            Color postColor = Color.White;
            int postThickness = 10;
            
            float goalLineX = facingRight ? x + depth : x;
            
            // In top-down view, we only see the crossbar (horizontal line connecting the two posts)
            // The "posts" are represented as circles at each end
            
            // Left/Top post (circle at top of goal)
            DrawCircle(spriteBatch, new Vector2(goalLineX, y), postThickness / 2f, postColor, postThickness);
            
            // Right/Bottom post (circle at bottom of goal)
            DrawCircle(spriteBatch, new Vector2(goalLineX, y + width), postThickness / 2f, postColor, postThickness);
            
            // Crossbar (horizontal line connecting posts)
            spriteBatch.Draw(_pixel, new Rectangle(
                (int)goalLineX - postThickness / 2, 
                (int)y, 
                postThickness, 
                (int)width), postColor);
        }
        
        private void DrawGoalNet(SpriteBatch spriteBatch, float x, float y, 
            float depth, float width, float height, bool facingRight)
        {
            Color netColor = new Color(220, 220, 220, 150); // Light gray, semi-transparent
            Color netLineColor = new Color(180, 180, 180, 200); // Slightly darker lines
            
            // Draw back panel (solid background)
            Rectangle backPanel = new Rectangle(
                (int)x, 
                (int)y, 
                (int)depth, 
                (int)width);
            spriteBatch.Draw(_pixel, backPanel, new Color(100, 100, 100, 100));
            
            // Draw mesh pattern - vertical lines
            int meshSpacing = 20;
            for (int i = 0; i < depth; i += meshSpacing)
            {
                float netX = facingRight ? x + i : x + depth - i;
                spriteBatch.Draw(_pixel, new Rectangle(
                    (int)netX, 
                    (int)y, 
                    2, 
                    (int)width), netLineColor);
            }
            
            // Draw mesh pattern - horizontal lines
            for (int i = 0; i < width; i += meshSpacing)
            {
                spriteBatch.Draw(_pixel, new Rectangle(
                    (int)x, 
                    (int)(y + i), 
                    (int)depth, 
                    2), netLineColor);
            }
            
            // Draw diagonal mesh for more realistic net appearance
            for (int i = 0; i < width; i += meshSpacing * 2)
            {
                for (int j = 0; j < depth; j += meshSpacing * 2)
                {
                    float x1 = facingRight ? x + j : x + depth - j;
                    float y1 = y + i;
                    float x2 = facingRight ? x + j + meshSpacing : x + depth - j - meshSpacing;
                    float y2 = y + i + meshSpacing;
                    
                    DrawLine(spriteBatch, new Vector2(x1, y1), new Vector2(x2, y2), netLineColor, 1);
                }
            }
        }
        
        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color, int thickness)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            
            spriteBatch.Draw(_pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)edge.Length(), thickness),
                null,
                color,
                angle,
                new Vector2(0, 0.5f),
                SpriteEffects.None,
                0);
        }
        
        private void DrawPlayer(SpriteBatch spriteBatch, Player player, SpriteFont font)
        {
            Color tintColor;
            
            if (player.IsKnockedDown)
            {
                // Knocked down players are semi-transparent gray
                tintColor = new Color(180, 180, 180, 180);
            }
            else if (player.IsControlled)
            {
                // Controlled player has bright yellow tint
                tintColor = new Color(255, 255, 150); // Light yellow tint
            }
            else
            {
                // Normal white tint (shows sprite colors as-is)
                tintColor = Color.White;
            }
            
            var pos = player.FieldPosition;
            
            // Render size
            float scale = player.IsControlled ? 2.125f : 2.0f; // Controlled player slightly larger
            int renderSize = player.IsControlled ? 136 : 128; // For UI elements
            
            // Use new animation system if available
            if (player.AnimationSystem != null)
            {
                bool isHomeTeam = player.TeamId == _homeTeam.Id;
                player.AnimationSystem.Draw(spriteBatch, pos, isHomeTeam, tintColor, scale);
            }
            else
            {
                // FALLBACK TO OLD SYSTEM
                Texture2D spriteSheet = player.TeamId == _homeTeam.Id ? _playerSpriteBlue : _playerSpriteRed;
                
                // Calculate source rectangle from sprite sheet
                int frameIndex = (int)player.AnimationFrame % SpritesheetColumns;
                int directionRow = player.SpriteDirection % SpritesheetRows;
                
                Rectangle sourceRect = new Rectangle(
                    frameIndex * SpriteFrameSize,
                    directionRow * SpriteFrameSize,
                    SpriteFrameSize,
                    SpriteFrameSize
                );
                
                // Calculate rotation for diagonal movement
                float rotation = 0f;
                Vector2 origin = new Vector2(SpriteFrameSize / 2, SpriteFrameSize / 2);
                
                if (player.Velocity.LengthSquared() > 0.1f)
                {
                    Vector2 vel = player.Velocity;
                    bool isMovingDiagonally = Math.Abs(vel.X) > 10f && Math.Abs(vel.Y) > 10f;
                    
                    if (isMovingDiagonally)
                    {
                        rotation = (float)Math.Atan2(vel.Y, vel.X);
                        if (vel.X > 0)
                            directionRow = 3;
                        else
                            directionRow = 2;
                        
                        sourceRect = new Rectangle(
                            frameIndex * SpriteFrameSize,
                            directionRow * SpriteFrameSize,
                            SpriteFrameSize,
                            SpriteFrameSize
                        );
                    }
                }
                
                // Draw player sprite
                if (player.IsKnockedDown)
                {
                    Rectangle destRect = new Rectangle((int)pos.X - renderSize / 2, (int)pos.Y - 20, renderSize, 40);
                    spriteBatch.Draw(spriteSheet, destRect, sourceRect, tintColor);
                }
                else
                {
                    Vector2 drawPos = new Vector2(pos.X, pos.Y);
                    spriteBatch.Draw(
                        spriteSheet,
                        drawPos,
                        sourceRect,
                        tintColor,
                        rotation,
                        origin,
                        renderSize / (float)SpriteFrameSize,
                        SpriteEffects.None,
                        0f
                    );
                }
            }
            
            // Draw selection indicator for controlled player
            if (player.IsControlled && !player.IsKnockedDown)
            {
                // Draw a circle around the controlled player
                DrawCircleOutline(spriteBatch, pos, renderSize / 2 + 10, Color.Yellow, 4);
                
                // Draw shot power indicator if charging
                if (_matchEngine.IsChargingShot())
                {
                    float power = _matchEngine.GetShotPower();
                    int barWidth = 60;
                    int barHeight = 8;
                    int barX = (int)(pos.X - barWidth / 2);
                    int barY = (int)(pos.Y - renderSize / 2 - 50); // Above player
                    
                    // Background
                    spriteBatch.Draw(_pixel, new Rectangle(barX, barY, barWidth, barHeight), new Color(0, 0, 0, 180));
                    
                    // Power fill
                    Color powerColor = power < 0.5f ? Color.Yellow : (power < 0.8f ? Color.Orange : Color.Red);
                    spriteBatch.Draw(_pixel, new Rectangle(barX, barY, (int)(barWidth * power), barHeight), powerColor);
                    
                    // Border
                    DrawRectangleOutline(spriteBatch, _pixel, new Rectangle(barX, barY, barWidth, barHeight), Color.White, 2);
                }
            }
            
            // Draw player name above sprite (only if not knocked down)
            if (!player.IsKnockedDown && !string.IsNullOrEmpty(player.Name))
            {
                try
                {
                    // Safely truncate name to max 12 characters
                    string displayName = player.Name;
                    if (displayName.Length > 12)
                    {
                        displayName = displayName.Substring(0, 12);
                    }
                    
                    Vector2 nameSize = font.MeasureString(displayName);
                    Vector2 namePos = new Vector2(pos.X - nameSize.X / 2, pos.Y - renderSize / 2 - 32);
                    
                    // Draw name background
                    spriteBatch.Draw(_pixel, new Rectangle((int)(namePos.X - 4), (int)(namePos.Y - 2), 
                        (int)(nameSize.X + 8), (int)(nameSize.Y + 4)), new Color(0, 0, 0, 150));
                    
                    // Draw name text
                    spriteBatch.DrawString(font, displayName, namePos, Color.White);
                }
                catch (Exception)
                {
                    // If name rendering fails (e.g., unsupported characters), skip it
                    System.Diagnostics.Debug.WriteLine($"Failed to render name: {player.Name}");
                }
            }
            
            // Draw stamina bar below player (if ShowStamina is enabled)
            if (GameSettings.Instance.ShowStamina && !player.IsKnockedDown)
            {
                int staminaBarWidth = 50;
                int staminaBarHeight = 6; // Increased from 4 to 6 for better visibility
                int staminaBarX = (int)(pos.X - staminaBarWidth / 2);
                int staminaBarY = (int)(pos.Y + renderSize / 2 + 8); // Below player
                
                float staminaPercent = player.Stamina / 100f;
                
                // Background (dark gray)
                spriteBatch.Draw(_pixel, new Rectangle(staminaBarX, staminaBarY, staminaBarWidth, staminaBarHeight), 
                    new Color(40, 40, 40, 200));
                
                // Stamina fill (color based on stamina level)
                Color staminaColor;
                if (staminaPercent > 0.6f)
                    staminaColor = new Color(0, 255, 0); // Green
                else if (staminaPercent > 0.3f)
                    staminaColor = new Color(255, 200, 0); // Yellow/Orange
                else
                    staminaColor = new Color(255, 0, 0); // Red (low stamina)
                
                int fillWidth = (int)(staminaBarWidth * staminaPercent);
                if (fillWidth > 0)
                {
                    spriteBatch.Draw(_pixel, new Rectangle(staminaBarX, staminaBarY, fillWidth, staminaBarHeight), staminaColor);
                }
                
                // Border
                DrawRectangleOutline(spriteBatch, _pixel, new Rectangle(staminaBarX, staminaBarY, staminaBarWidth, staminaBarHeight), 
                    Color.White, 1);
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
            // Don't draw referee during countdown to avoid visual artifacts
            if (_matchEngine.CurrentState == MatchEngine.MatchState.Countdown)
                return;
            
            var pos = _matchEngine.RefereePosition;
            int size = 120; // Double size to match player scale
            
            // Draw shadow
            Rectangle shadowRect = new Rectangle((int)pos.X - size / 2, (int)pos.Y + size / 2 - 6, size, 12);
            spriteBatch.Draw(_pixel, shadowRect, new Color(0, 0, 0, 100));
            
            // Draw referee using sprite sheet (black tint for referee uniform)
            // Use first frame, facing down
            Rectangle sourceRect = new Rectangle(0, 0, SpriteFrameSize, SpriteFrameSize);
            Vector2 drawPos = new Vector2(pos.X, pos.Y);
            Vector2 origin = new Vector2(SpriteFrameSize / 2, SpriteFrameSize / 2);
            
            spriteBatch.Draw(_playerSpriteBlue, drawPos, sourceRect, new Color(40, 40, 40), 0f, origin, 
                size / (float)SpriteFrameSize, SpriteEffects.None, 0f);
            
            // Add white stripes for referee shirt
            spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 30, (int)pos.Y - 35, 60, 12), Color.White);
            spriteBatch.Draw(_pixel, new Rectangle((int)pos.X - 30, (int)pos.Y - 15, 60, 12), Color.White);
        }
        
        private void DrawBall(SpriteBatch spriteBatch, Vector2 ballPos)
        {
            int baseBallSize = 32; // Base size
            
            // Calculate scale based on height (ball appears bigger when higher)
            float heightScale = 1f + (_matchEngine.BallHeight / 400f); // Grows up to 2x at max height
            int ballSize = (int)(baseBallSize * heightScale);
            
            // Draw ball shadow (always on ground, scaled based on height)
            int shadowSize = (int)(baseBallSize * (1f + _matchEngine.BallHeight / 800f)); // Shadow grows when ball is higher
            float shadowAlpha = Math.Max(50, 120 - _matchEngine.BallHeight * 0.3f); // Fainter when ball is higher
            Rectangle shadowRect = new Rectangle(
                (int)ballPos.X - shadowSize / 2, 
                (int)ballPos.Y + shadowSize / 2 - 3, 
                shadowSize, 
                6
            );
            spriteBatch.Draw(_pixel, shadowRect, new Color(0, 0, 0, (int)shadowAlpha));
            
            // Calculate source rectangle from ball sprite sheet (8x8 grid)
            int frameIndex = (int)_ballAnimationFrame % 64; // 0-63
            int frameCol = frameIndex % BallSpritesheetColumns;
            int frameRow = frameIndex / BallSpritesheetColumns;
            
            Rectangle sourceRect = new Rectangle(
                frameCol * BallFrameSize,
                frameRow * BallFrameSize,
                BallFrameSize,
                BallFrameSize
            );
            
            // Draw ball with animation frame (adjusted Y position for height)
            Vector2 drawPos = new Vector2(ballPos.X, ballPos.Y - _matchEngine.BallHeight);
            Vector2 origin = new Vector2(BallFrameSize / 2, BallFrameSize / 2);
            
            spriteBatch.Draw(_ballSprite, drawPos, sourceRect, Color.White, 0f, origin, 
                ballSize / (float)BallFrameSize, SpriteEffects.None, 0f);
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
            
            // Draw score centered at top with yellow shadow for visibility
            string scoreText = $"{_homeTeam.Name} {_matchEngine.HomeScore} - {_matchEngine.AwayScore} {_awayTeam.Name}";
            Vector2 scoreSize = font.MeasureString(scoreText);
            Vector2 scorePos = new Vector2((screenWidth - scoreSize.X) / 2, 10);
            
            // Yellow shadow (offset by 2 pixels in all directions for better visibility)
            spriteBatch.DrawString(font, scoreText, scorePos + new Vector2(-2, -2), Color.Yellow);
            spriteBatch.DrawString(font, scoreText, scorePos + new Vector2(2, -2), Color.Yellow);
            spriteBatch.DrawString(font, scoreText, scorePos + new Vector2(-2, 2), Color.Yellow);
            spriteBatch.DrawString(font, scoreText, scorePos + new Vector2(2, 2), Color.Yellow);
            
            // Red text on top
            spriteBatch.DrawString(font, scoreText, scorePos, Color.Red);
            
            // Draw time centered
            string timeText = $"Time: {(int)_matchEngine.MatchTime}'";
            Vector2 timeSize = font.MeasureString(timeText);
            spriteBatch.DrawString(font, timeText, new Vector2((screenWidth - timeSize.X) / 2, screenHeight - 50), Color.White);
            
            // Draw controls centered at bottom
            string controls = "Arrows: Move | Space: Switch | X: Hold to Charge Shot";
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
                countdownText = "ΠΑΜΕ!";
            }
            
            // Large text in center of screen
            Vector2 textSize = font.MeasureString(countdownText);
            float scale = 3.0f; // Make it 3x larger
            Vector2 scaledSize = textSize * scale;
            Vector2 position = new Vector2((screenWidth - scaledSize.X) / 2, (screenHeight - scaledSize.Y) / 2);
            
            // Draw text
            Color countdownColor = _matchEngine.CountdownNumber > 0 ? Color.Yellow : Color.LightGreen;
            spriteBatch.DrawString(font, countdownText, position, countdownColor, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
