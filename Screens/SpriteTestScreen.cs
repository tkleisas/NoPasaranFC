using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Content;
using NoPasaranFC.Models;

namespace NoPasaranFC.Screens
{
    public class SpriteTestScreen : Screen
    {
        private ScreenManager _screenManager;
        private ContentManager _contentManager;
        private GraphicsDevice _graphicsDevice;
        private KeyboardState _previousKeyState;
        
        private Dictionary<string, Texture2D> _spriteSheets;
        private List<SpriteAnimation> _testAnimations;
        private int _currentAnimationIndex = 0;
        private float _animationTimer = 0f;
        private int _currentFrameIndex = 0;
        private int _additionalRotation = 0; // Additional rotation to apply to entire animation (0-7)
        
        private Texture2D _pixel;
        
        // Spritesheet properties
        private const int SpriteSize = 64;
        private const int SpritesPerRow = 4;
        private const int SpriteRows = 12;
        private const int TotalSprites = 48;
        
        public SpriteTestScreen(ScreenManager screenManager, ContentManager content, GraphicsDevice graphicsDevice)
        {
            _screenManager = screenManager;
            _contentManager = content;
            _graphicsDevice = graphicsDevice;
            
            _spriteSheets = new Dictionary<string, Texture2D>();
            _testAnimations = new List<SpriteAnimation>();
            
            // Create pixel texture
            _pixel = new Texture2D(_graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            
            InitializeTestAnimations();
        }
        
        private void InitializeTestAnimations()
        {
            // Test Animation 1: Simple forward sequence with no rotation/mirror
            var anim1 = new SpriteAnimation("walk", 0.2f, true);
            anim1.AddFrame("player_red_multi", 8, 0, 0);
            anim1.AddFrame("player_red_multi", 9, 0, 0);
            anim1.AddFrame("player_red_multi", 10, 0, 0);
            anim1.AddFrame("player_red_multi", 11, 0, 0);
            _testAnimations.Add(anim1);
            
            // Test Animation 2: Using rotation
            var anim2 = new SpriteAnimation("fall", 0.15f, true);
            anim2.AddFrame("player_red_multi", 16, 0, 0); 
            anim2.AddFrame("player_red_multi", 17, 0, 0); 
            anim2.AddFrame("player_red_multi", 18, 0, 0); 
            anim2.AddFrame("player_red_multi", 19, 0, 0); 
            _testAnimations.Add(anim2);
            
            // Test Animation 3: Using mirror
            var anim3 = new SpriteAnimation("shoot", 0.2f, false);
            anim3.AddFrame("player_red_multi", 12, 0, 0); 
            anim3.AddFrame("player_red_multi", 13, 0, 0); 
            anim3.AddFrame("player_red_multi", 14, 0, 0); 
            anim3.AddFrame("player_red_multi", 15, 0, 0); 
            _testAnimations.Add(anim3);
            
            // Test Animation 4: Complex - rotation + mirror
            var anim4 = new SpriteAnimation("tackle", 0.1f, false);
            anim4.AddFrame("player_red_multi", 28, 0, 0);
            anim4.AddFrame("player_red_multi", 29, 0, 0); 
            anim4.AddFrame("player_red_multi", 30, 0, 0); 
            anim4.AddFrame("player_red_multi", 31, 0, 0); 
            
            _testAnimations.Add(anim4);
            
            // Test Animation 5: Diagonal movement simulation
            var anim5 = new SpriteAnimation("Diagonal Walk", 0.12f, true);
            anim5.AddFrame("player_red_multi", 8, 1, 0);
            anim5.AddFrame("player_red_multi", 9, 1, 0); // 45° tilt
            anim5.AddFrame("player_red_multi", 10, 1, 0);
            anim5.AddFrame("player_red_multi", 11, 1, 0); // 315° tilt
            _testAnimations.Add(anim5);
        }
        
        public void LoadSpriteSheet(string name, string path)
        {
            if (!_spriteSheets.ContainsKey(name))
            {
                try
                {
                    _spriteSheets[name] = _contentManager.Load<Texture2D>(path);
                }
                catch
                {
                    // Spritesheet not found - will be handled in draw
                }
            }
        }
        
        public override void Update(GameTime gameTime)
        {
            var keyState = Keyboard.GetState();
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Navigate animations
            if (keyState.IsKeyDown(Keys.Left) && !_previousKeyState.IsKeyDown(Keys.Left))
            {
                _currentAnimationIndex = (_currentAnimationIndex - 1 + _testAnimations.Count) % _testAnimations.Count;
                _currentFrameIndex = 0;
                _animationTimer = 0f;
            }
            
            if (keyState.IsKeyDown(Keys.Right) && !_previousKeyState.IsKeyDown(Keys.Right))
            {
                _currentAnimationIndex = (_currentAnimationIndex + 1) % _testAnimations.Count;
                _currentFrameIndex = 0;
                _animationTimer = 0f;
            }
            
            // Adjust additional rotation
            if (keyState.IsKeyDown(Keys.Up) && !_previousKeyState.IsKeyDown(Keys.Up))
            {
                _additionalRotation = (_additionalRotation + 1) % 8;
            }
            
            if (keyState.IsKeyDown(Keys.Down) && !_previousKeyState.IsKeyDown(Keys.Down))
            {
                _additionalRotation = (_additionalRotation - 1 + 8) % 8;
            }
            
            // Reset rotation
            if (keyState.IsKeyDown(Keys.R) && !_previousKeyState.IsKeyDown(Keys.R))
            {
                _additionalRotation = 0;
            }
            
            // Exit to menu
            if (keyState.IsKeyDown(Keys.Escape) && !_previousKeyState.IsKeyDown(Keys.Escape))
            {
                _screenManager.PopScreen();
            }
            
            // Update animation
            var currentAnim = _testAnimations[_currentAnimationIndex];
            _animationTimer += deltaTime;
            
            if (_animationTimer >= currentAnim.FrameDuration)
            {
                _animationTimer -= currentAnim.FrameDuration;
                _currentFrameIndex++;
                
                if (_currentFrameIndex >= currentAnim.Frames.Count)
                {
                    if (currentAnim.Loop)
                    {
                        _currentFrameIndex = 0;
                    }
                    else
                    {
                        _currentFrameIndex = currentAnim.Frames.Count - 1;
                    }
                }
            }
            
            _previousKeyState = keyState;
        }
        
        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            
            // Draw background
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), new Color(40, 40, 40));
            
            // Draw title
            string title = "SPRITE TEST SCREEN";
            Vector2 titleSize = font.MeasureString(title);
            spriteBatch.DrawString(font, title, new Vector2((screenWidth - titleSize.X) / 2, 20), Color.Yellow);
            
            // Draw current animation info
            var currentAnim = _testAnimations[_currentAnimationIndex];
            string animInfo = $"Animation: {currentAnim.Name} ({_currentAnimationIndex + 1}/{_testAnimations.Count})";
            spriteBatch.DrawString(font, animInfo, new Vector2(20, 60), Color.White);
            
            string frameInfo = $"Frame: {_currentFrameIndex + 1}/{currentAnim.Frames.Count}";
            spriteBatch.DrawString(font, frameInfo, new Vector2(20, 90), Color.White);
            
            // Draw additional rotation info
            string rotInfo = $"Additional Rotation: {_additionalRotation} ({_additionalRotation * 45}d)";
            spriteBatch.DrawString(font, rotInfo, new Vector2(20, 120), Color.LightGreen);
            
            // Draw the current frame
            if (currentAnim.Frames.Count > 0)
            {
                var frame = currentAnim.Frames[_currentFrameIndex];
                
                // Load spritesheet if needed
                LoadSpriteSheet(frame.SpriteSheet, $"Sprites/{frame.SpriteSheet}");
                
                if (_spriteSheets.ContainsKey(frame.SpriteSheet))
                {
                    DrawSpriteFrame(spriteBatch, font, frame, new Vector2(screenWidth / 2, screenHeight / 2), _additionalRotation);
                }
                else
                {
                    string error = $"Spritesheet not found: {frame.SpriteSheet}";
                    spriteBatch.DrawString(font, error, new Vector2(20, 150), Color.Red);
                }
            }
            
            // Draw controls
            string controls = "<=/=>: Change Animation | ESC: Back to Menu";
            Vector2 controlsSize = font.MeasureString(controls);
            spriteBatch.DrawString(font, controls, new Vector2((screenWidth - controlsSize.X) / 2, screenHeight - 30), Color.Gray);
        }
        
        private void DrawSpriteFrame(SpriteBatch spriteBatch, SpriteFont font, SpriteFrame frame, Vector2 position, int additionalRotation)
        {
            var spriteSheet = _spriteSheets[frame.SpriteSheet];
            
            // Calculate source rectangle
            int col = frame.SpriteIndex % SpritesPerRow;
            int row = frame.SpriteIndex / SpritesPerRow;
            
            Rectangle sourceRect = new Rectangle(
                col * SpriteSize,
                row * SpriteSize,
                SpriteSize,
                SpriteSize
            );
            
            // Calculate rotation (combine frame rotation with additional rotation)
            int totalRotation = (frame.Rotation + additionalRotation) % 8;
            float rotation = totalRotation * (MathHelper.Pi / 4f);
            
            // Calculate mirror effect
            SpriteEffects effects = frame.Mirror == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            
            // Draw sprite (scaled 3x for visibility)
            float scale = 3f;
            Vector2 origin = new Vector2(SpriteSize / 2, SpriteSize / 2);
            
            // Draw crosshair at center
            spriteBatch.Draw(_pixel, new Rectangle((int)position.X - 10, (int)position.Y, 20, 2), Color.Red);
            spriteBatch.Draw(_pixel, new Rectangle((int)position.X, (int)position.Y - 10, 2, 20), Color.Red);
            
            // Draw sprite
            spriteBatch.Draw(
                spriteSheet,
                position,
                sourceRect,
                Color.White,
                rotation,
                origin,
                scale,
                effects,
                0f
            );
            
            // Draw frame details
            int detailsX = 20;
            int detailsY = 180;
            spriteBatch.DrawString(font, $"Sprite Index: {frame.SpriteIndex}", new Vector2(detailsX, detailsY), Color.Cyan);
            spriteBatch.DrawString(font, $"Frame Rotation: {frame.Rotation} ({frame.Rotation * 45}d)", new Vector2(detailsX, detailsY + 30), Color.Cyan);
            spriteBatch.DrawString(font, $"Total Rotation: {totalRotation} ({totalRotation * 45}d)", new Vector2(detailsX, detailsY + 60), Color.Yellow);
            spriteBatch.DrawString(font, $"Mirror: {(frame.Mirror == 1 ? "Yes" : "No")}", new Vector2(detailsX, detailsY + 90), Color.Cyan);
            spriteBatch.DrawString(font, $"Source: Col {col}, Row {row}", new Vector2(detailsX, detailsY + 120), Color.Cyan);
        }
    }
}
