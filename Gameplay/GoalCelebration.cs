using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using System;
using System.Collections.Generic;

namespace NoPasaranFC.Gameplay
{
    public class GoalCelebration
    {
        private bool _isActive;
        private float _timer;
        private const float VisualDuration = 2.5f; // Ball animation and text duration
        private const float CelebrationDuration = 60.0f; // Default total celebration duration (1 minute, can be skipped with any key)
        private float _activeCelebrationDuration; // Actual duration for current celebration
        private string _text;
        private SpriteFont _font;
        private GraphicsDevice _graphicsDevice;
        
        // Ball animation particles
        private struct BallParticle
        {
            public Vector2 Position;
            public Vector2 TargetPosition;
            public float Progress;
            public float Delay;
        }
        
        private BallParticle[] _ballParticles;
        
        public float Timer => _timer;
        public bool IsActive => _isActive;
        
        public void Start(string text, SpriteFont font, GraphicsDevice graphicsDevice, float? customDuration = null)
        {
            _isActive = true;
            _timer = 0f;
            _text = text;
            _font = font;
            _graphicsDevice = graphicsDevice;
            _activeCelebrationDuration = customDuration ?? CelebrationDuration;
            InitializeBallParticles();
        }
        
        public void Start()
        {
            // Backward compatibility - use default "GOAL!"
            if (_font != null && _graphicsDevice != null)
            {
                Start("ΓΚΟΛ!", _font, _graphicsDevice);
            }
        }
        
        private void InitializeBallParticles()
        {
            if (_font == null || _graphicsDevice == null || string.IsNullOrEmpty(_text))
            {
                _ballParticles = new BallParticle[0];
                return;
            }
            
            // Render text to bitmap and extract pixel positions
            List<Vector2> pixelPositions = RenderTextToBitmap(_text, _font, _graphicsDevice);
            
            // Create ball particles for each pixel
            _ballParticles = new BallParticle[pixelPositions.Count];
            Random random = new Random();
            
            for (int i = 0; i < pixelPositions.Count; i++)
            {
                // Start from random positions off-screen
                float angle = (float)random.NextDouble() * MathF.PI * 2;
                float distance = 800f + (float)random.NextDouble() * 400f;
                
                Vector2 startPos = new Vector2(
                    MathF.Cos(angle) * distance,
                    MathF.Sin(angle) * distance
                );
                
                _ballParticles[i] = new BallParticle
                {
                    Position = startPos,
                    TargetPosition = pixelPositions[i],
                    Progress = 0f,
                    Delay = (i / (float)pixelPositions.Count) * 0.5f // Stagger based on position
                };
            }
        }
        
        private List<Vector2> RenderTextToBitmap(string text, SpriteFont font, GraphicsDevice graphicsDevice)
        {
            List<Vector2> positions = new List<Vector2>();
            
            try
            {
                // Render at smaller scale for fewer balls
                float renderScale = 8.0f; // Render text larger (smaller bitmap, fewer balls)
                Vector2 textSize = font.MeasureString(text) * renderScale;
                int width = (int)Math.Ceiling(textSize.X);
                int height = (int)Math.Ceiling(textSize.Y);
                
                if (width <= 0 || height <= 0)
                    return positions;
                
                // Create render target at scaled size
                using (RenderTarget2D renderTarget = new RenderTarget2D(
                    graphicsDevice, 
                    width, 
                    height, 
                    false, 
                    SurfaceFormat.Color, 
                    DepthFormat.None))
                {
                    // Render text to target
                    graphicsDevice.SetRenderTarget(renderTarget);
                    graphicsDevice.Clear(Color.Transparent);
                    
                    using (SpriteBatch spriteBatch = new SpriteBatch(graphicsDevice))
                    {
                        // Use scale matrix to render text larger with point sampling (no AA)
                        spriteBatch.Begin(SpriteSortMode.Deferred, BlendState.AlphaBlend, 
                            SamplerState.PointClamp, // Point sampling = no anti-aliasing
                            null, null, null, Matrix.CreateScale(renderScale));
                        spriteBatch.DrawString(font, text, Vector2.Zero, Color.White);
                        spriteBatch.End();
                    }
                    
                    // Reset render target before reading pixels
                    graphicsDevice.SetRenderTarget(null);
                    
                    // Extract pixels
                    Color[] pixels = new Color[width * height];
                    renderTarget.GetData(pixels);
                    
                    // Render one ball per visible pixel with spacing
                    float ballSpacingScale = 1.5f; // Scale to add space between balls
                    
                    for (int y = 0; y < height; y++)
                    {
                        for (int x = 0; x < width; x++)
                        {
                            int index = y * width + x;
                            
                            // If this pixel is visible, place a ball
                            if (index < pixels.Length && pixels[index].A > 200)
                            {
                                // Convert to centered coordinates and apply spacing scale
                                float posX = (x - width / 2f) * ballSpacingScale;
                                float posY = (y - height / 2f) * ballSpacingScale;
                                positions.Add(new Vector2(posX, posY));
                            }
                        }
                    }
                }
            }
            catch (Exception)
            {
                // Fallback to simple grid if render target fails on some devices
                return CreateSimpleTextGrid(text, font);
            }
            
            return positions;
        }
        
        /// <summary>
        /// Pixel-based text representation for Android (no render targets)
        /// Creates ball positions based on predefined letter pixel patterns
        /// </summary>
        private List<Vector2> CreateSimpleTextGrid(string text, SpriteFont font)
        {
            List<Vector2> positions = new List<Vector2>();
            
            float spacing = 6f; // Space between balls
            float charSpacing = 42f; // Space between characters
            float startX = -(text.Length - 1) * charSpacing / 2f;
            
            for (int charIndex = 0; charIndex < text.Length; charIndex++)
            {
                float charCenterX = startX + charIndex * charSpacing;
                char c = text[charIndex];
                
                // Get pixel pattern for this character
                bool[,] pattern = GetCharacterPattern(c);
                if (pattern == null) continue;
                
                int height = pattern.GetLength(0);
                int width = pattern.GetLength(1);
                
                for (int y = 0; y < height; y++)
                {
                    for (int x = 0; x < width; x++)
                    {
                        if (pattern[y, x])
                        {
                            positions.Add(new Vector2(
                                charCenterX + (x - width / 2f) * spacing,
                                (y - height / 2f) * spacing
                            ));
                        }
                    }
                }
            }
            
            return positions;
        }
        
        /// <summary>
        /// Returns a pixel pattern for common characters used in goal celebrations
        /// Each pattern is a 7-row bitmap (true = ball, false = empty)
        /// </summary>
        private bool[,] GetCharacterPattern(char c)
        {
            // 7x5 pixel patterns for letters
            return c switch
            {
                // Greek ΓΚΟΛ! (Goal in Greek)
                'Γ' => new bool[,] {
                    { true, true, true, true, true },
                    { true, false, false, false, false },
                    { true, false, false, false, false },
                    { true, false, false, false, false },
                    { true, false, false, false, false },
                    { true, false, false, false, false },
                    { true, false, false, false, false }
                },
                'Κ' or 'K' => new bool[,] {
                    { true, false, false, false, true },
                    { true, false, false, true, false },
                    { true, false, true, false, false },
                    { true, true, false, false, false },
                    { true, false, true, false, false },
                    { true, false, false, true, false },
                    { true, false, false, false, true }
                },
                'Ο' or 'O' => new bool[,] {
                    { false, true, true, true, false },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { false, true, true, true, false }
                },
                'Λ' => new bool[,] {
                    { false, false, true, false, false },
                    { false, false, true, false, false },
                    { false, true, false, true, false },
                    { false, true, false, true, false },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true }
                },
                // English GOAL!
                'G' => new bool[,] {
                    { false, true, true, true, false },
                    { true, false, false, false, true },
                    { true, false, false, false, false },
                    { true, false, true, true, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { false, true, true, true, false }
                },
                'A' => new bool[,] {
                    { false, false, true, false, false },
                    { false, true, false, true, false },
                    { true, false, false, false, true },
                    { true, true, true, true, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true }
                },
                'L' => new bool[,] {
                    { true, false, false, false, false },
                    { true, false, false, false, false },
                    { true, false, false, false, false },
                    { true, false, false, false, false },
                    { true, false, false, false, false },
                    { true, false, false, false, false },
                    { true, true, true, true, true }
                },
                '!' => new bool[,] {
                    { true },
                    { true },
                    { true },
                    { true },
                    { true },
                    { false },
                    { true }
                },
                // Space or unknown
                ' ' => null,
                _ => new bool[,] {
                    { true, true, true, true, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { true, false, false, false, true },
                    { true, true, true, true, true }
                }
            };
        }
        
        public void Update(float deltaTime)
        {
            if (!_isActive) return;

            _timer += deltaTime;

            // Update ball particles (only during visual duration)
            if (_timer <= VisualDuration)
            {
                for (int i = 0; i < _ballParticles.Length; i++)
                {
                    float effectiveTime = Math.Max(0f, _timer - _ballParticles[i].Delay);
                    _ballParticles[i].Progress = Math.Min(1f, effectiveTime * 2.5f); // Animation speed
                }
            }

            // End celebration after full duration
            if (_timer >= _activeCelebrationDuration)
            {
                _isActive = false;
            }
        }

        public void Stop()
        {
            _isActive = false;
        }
        
        public void Draw(SpriteBatch spriteBatch, Texture2D ballTexture, int screenWidth, int screenHeight)
        {
            // Only draw visual effects during the visual duration
            if (!_isActive || _timer > VisualDuration) return;

            // Position at center of screen for initial animation
            Vector2 screenCenter = new Vector2(screenWidth / 2, screenHeight / 2);

            // Draw background overlay
            float bgAlpha = Math.Min(1f, _timer * 3f) * 0.7f;
            if (_timer > VisualDuration - 0.5f)
            {
                bgAlpha *= (VisualDuration - _timer) / 0.5f; // Fade out
            }
            
            // Draw each ball particle
            foreach (var particle in _ballParticles)
            {
                // Ease-out interpolation
                float t = particle.Progress;
                float easeT = 1f - MathF.Pow(1f - t, 3f);
                
                Vector2 position = Vector2.Lerp(particle.Position, particle.TargetPosition, easeT);
                position += screenCenter;
                
                // Scale and alpha based on progress
                float scale = 0.5f + easeT * 1.0f; // Balls grow from 0.5x to 1.5x
                float alpha = Math.Min(1f, particle.Progress * 3f);

                // Fade out at end of visual duration
                if (_timer > VisualDuration - 0.5f)
                {
                    alpha *= (VisualDuration - _timer) / 0.5f;
                }
                
                // Draw ball at normal size
                int ballBaseSize = 32; // Default ball size
                Rectangle destRect = new Rectangle(
                    (int)(position.X - ballBaseSize / 2 * scale),
                    (int)(position.Y - ballBaseSize / 2 * scale),
                    (int)(ballBaseSize * scale),
                    (int)(ballBaseSize * scale)
                );
                
                spriteBatch.Draw(ballTexture, destRect, 
                    new Rectangle(0, 0, 32, 32), // First frame of ball sprite
                    Color.White * alpha);
            }
            
            // Draw "GOAL!" text underneath (using font)
            if (_timer > 0.5f && _timer < VisualDuration - 0.3f)
            {
                // This will be drawn by MatchScreen using the font
            }
        }

        public bool ShouldDrawGoalText()
        {
            // Show text throughout the entire celebration
            return _isActive && _timer > 0.5f;
        }

        public float GetGoalTextScale()
        {
            if (!_isActive) return 0f;

            // During visual animation (0-2.5s): scale up to full size
            if (_timer <= VisualDuration)
            {
                float t = Math.Min(1f, (_timer - 0.5f) * 3f);
                float scale = MathF.Pow(t, 0.5f);
                return scale * 3f;
            }

            // After visual animation: keep at medium size at top
            return 1.5f;
        }

        public float GetGoalTextYPosition(int screenHeight)
        {
            if (!_isActive) return 0f;

            // During visual animation: text below center
            if (_timer <= VisualDuration)
            {
                return screenHeight / 2 + 150;
            }

            // Transition period: move from center to top (0.5 seconds)
            float transitionDuration = 0.5f;
            if (_timer <= VisualDuration + transitionDuration)
            {
                float t = (_timer - VisualDuration) / transitionDuration;
                // Smooth ease-out transition
                t = 1f - MathF.Pow(1f - t, 3f);
                float startY = screenHeight / 2 + 150;
                float endY = 50;
                return MathHelper.Lerp(startY, endY, t);
            }

            // After transition: stay at top
            return 50;
        }

        public bool ShouldShowSkipMessage()
        {
            // Show skip message only after 5 seconds (when skipping is allowed)
            return _isActive && _timer > 5.0f;
        }
    }
}
