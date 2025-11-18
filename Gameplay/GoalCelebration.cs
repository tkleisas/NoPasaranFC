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
        private const float Duration = 2.5f;
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
        
        public bool IsActive => _isActive;
        
        public void Start(string text, SpriteFont font, GraphicsDevice graphicsDevice)
        {
            _isActive = true;
            _timer = 0f;
            _text = text;
            _font = font;
            _graphicsDevice = graphicsDevice;
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
            
            // Render at smaller scale for fewer balls
            float renderScale = 8.0f; // Render text 1.5x larger (smaller bitmap, fewer balls)
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
                
                // Debug output
                System.IO.File.WriteAllText("celebration_debug.txt", 
                    $"Text: '{text}'\n" +
                    $"Render Scale: {renderScale}\n" +
                    $"Bitmap Size: {width}×{height}\n" +
                    $"Ball Count: {positions.Count}\n");
            }
            
            return positions;
        }
        
        public void Update(float deltaTime)
        {
            if (!_isActive) return;
            
            _timer += deltaTime;
            
            // Update ball particles
            for (int i = 0; i < _ballParticles.Length; i++)
            {
                float effectiveTime = Math.Max(0f, _timer - _ballParticles[i].Delay);
                _ballParticles[i].Progress = Math.Min(1f, effectiveTime * 2.5f); // Animation speed
            }
            
            if (_timer >= Duration)
            {
                _isActive = false;
            }
        }
        
        public void Draw(SpriteBatch spriteBatch, Texture2D ballTexture, int screenWidth, int screenHeight)
        {
            if (!_isActive) return;
            
            Vector2 screenCenter = new Vector2(screenWidth / 2, screenHeight / 2);
            
            // Draw background overlay
            float bgAlpha = Math.Min(1f, _timer * 3f) * 0.7f;
            if (_timer > Duration - 0.5f)
            {
                bgAlpha *= (Duration - _timer) / 0.5f; // Fade out
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
                
                // Fade out at end
                if (_timer > Duration - 0.5f)
                {
                    alpha *= (Duration - _timer) / 0.5f;
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
            if (_timer > 0.5f && _timer < Duration - 0.3f)
            {
                // This will be drawn by MatchScreen using the font
            }
        }
        
        public bool ShouldDrawGoalText()
        {
            return _isActive && _timer > 0.5f && _timer < Duration - 0.3f;
        }
        
        public float GetGoalTextScale()
        {
            if (!_isActive) return 0f;
            
            float t = Math.Min(1f, (_timer - 0.5f) * 3f);
            float scale = MathF.Pow(t, 0.5f);
            
            if (_timer > Duration - 0.3f)
            {
                scale *= (Duration - _timer) / 0.3f;
            }
            
            return scale * 3f;
        }
    }
}
