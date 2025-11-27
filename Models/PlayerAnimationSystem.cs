using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace NoPasaranFC.Models
{
    public class PlayerAnimationSystem
    {
        private Dictionary<string, SpriteAnimation> _animations;
        private Dictionary<string, Texture2D> _spriteSheets;
        private SpriteAnimation _currentAnimation;
        private float _animationTimer;
        private int _currentFrameIndex;
        private int _additionalRotation; // For directional rotation
        
        private const int SpriteSize = 64;
        private const int SpritesPerRow = 4;
        
        // Shared sprite sheets (static)
        private static Dictionary<string, Texture2D> _sharedSpriteSheets;
        private static Dictionary<string, SpriteAnimation> _sharedAnimations;
        
        public PlayerAnimationSystem()
        {
            _animationTimer = 0f;
            _currentFrameIndex = 0;
            _additionalRotation = 0;
        }
        
        public static void LoadSharedResources(ContentManager content)
        {
            if (_sharedSpriteSheets == null)
            {
                _sharedSpriteSheets = new Dictionary<string, Texture2D>();
                _sharedSpriteSheets["player_red_multi"] = content.Load<Texture2D>("Sprites/player_red_multi");
                _sharedSpriteSheets["player_blue_multi"] = content.Load<Texture2D>("Sprites/player_blue_multi");
                _sharedSpriteSheets["no_pasaran_kit"] = content.Load<Texture2D>("Sprites/no_pasaran_kit");
                _sharedSpriteSheets["asalagitos_kit"] = content.Load<Texture2D>("Sprites/asalagitos_kit");
                _sharedSpriteSheets["tiganitis_kit"] = content.Load<Texture2D>("Sprites/tiganitis_kit");
                _sharedSpriteSheets["asteras_exarchion_kit"] = content.Load<Texture2D>("Sprites/asteras_exarchion_kit");
                _sharedSpriteSheets["chandrinaikos_kit"] = content.Load<Texture2D>("Sprites/chandrinaikos_kit");
            }
            
            if (_sharedAnimations == null)
            {
                _sharedAnimations = new Dictionary<string, SpriteAnimation>();
                InitializeSharedAnimations();
            }
        }
        
        private static void InitializeSharedAnimations()
        {
            // Idle animation - looping (standing still)
            var idle = new SpriteAnimation("idle", 0.2f, true);
            idle.AddFrame("dummy", 0, 0, 0);
            idle.AddFrame("dummy", 1, 0, 0);
            idle.AddFrame("dummy", 2, 0, 0);
            idle.AddFrame("dummy", 3, 0, 0);
            _sharedAnimations["idle"] = idle;
            
            // Walk animation - looping
            var walk = new SpriteAnimation("walk", 0.12f, true);
            walk.AddFrame("dummy", 8, 0, 0);
            walk.AddFrame("dummy", 9, 0, 0);
            walk.AddFrame("dummy", 10, 0, 0);
            walk.AddFrame("dummy", 11, 0, 0);
            _sharedAnimations["walk"] = walk;
            // Fall animation - NOT looping
            var fall = new SpriteAnimation("fall", 0.15f, false);
            fall.AddFrame("dummy", 16, 0, 0);
            fall.AddFrame("dummy", 17, 0, 0);
            fall.AddFrame("dummy", 18, 0, 0);
            fall.AddFrame("dummy", 19, 0, 0);
            _sharedAnimations["fall"] = fall;
            
            // Shoot animation - NOT looping
            var shoot = new SpriteAnimation("shoot", 0.1f, false);
            shoot.AddFrame("dummy", 12, 0, 0);
            shoot.AddFrame("dummy", 13, 0, 0);
            shoot.AddFrame("dummy", 14, 0, 0);
            shoot.AddFrame("dummy", 15, 0, 0);
            _sharedAnimations["shoot"] = shoot;
            
            // Tackle animation - NOT looping
            var tackle = new SpriteAnimation("tackle", 0.1f, false);
            tackle.AddFrame("dummy", 28, 0, 0);
            tackle.AddFrame("dummy", 29, 0, 0);
            tackle.AddFrame("dummy", 30, 0, 0);
            tackle.AddFrame("dummy", 31, 0, 0);
            _sharedAnimations["tackle"] = tackle;

            // Celebrate animation - looping (arms extended to sides)
            var celebrate = new SpriteAnimation("celebrate", 0.15f, true);
            celebrate.AddFrame("dummy", 32, 0, 0);
            celebrate.AddFrame("dummy", 33, 0, 0);
            celebrate.AddFrame("dummy", 34, 0, 0);
            celebrate.AddFrame("dummy", 35, 0, 0);
            _sharedAnimations["celebrate"] = celebrate;
        }
        
        public void PlayAnimation(string animationName)
        {
            if (_sharedAnimations != null && _sharedAnimations.ContainsKey(animationName) && 
                (_currentAnimation == null || _currentAnimation.Name != animationName))
            {
                _currentAnimation = _sharedAnimations[animationName];
                _animationTimer = 0f;
                _currentFrameIndex = 0;
            }
        }
        
        public void SetRotation(int rotation)
        {
            _additionalRotation = rotation % 8;
        }
        
        public void Update(float deltaTime)
        {
            if (_currentAnimation == null) return;
            
            _animationTimer += deltaTime;
            
            if (_animationTimer >= _currentAnimation.FrameDuration)
            {
                _animationTimer -= _currentAnimation.FrameDuration;
                _currentFrameIndex++;
                
                if (_currentFrameIndex >= _currentAnimation.Frames.Count)
                {
                    if (_currentAnimation.Loop)
                    {
                        _currentFrameIndex = 0;
                    }
                    else
                    {
                        _currentFrameIndex = _currentAnimation.Frames.Count - 1;
                    }
                }
            }
        }
        
        public bool IsAnimationFinished()
        {
            if (_currentAnimation == null || _currentAnimation.Loop) return false;
            return _currentFrameIndex >= _currentAnimation.Frames.Count - 1;
        }
        
        public void Draw(SpriteBatch spriteBatch, Vector2 position, bool isHomeTeam, Color tint, float scale = 2f, string kitname = null)
        {
            if (_currentAnimation == null || _currentAnimation.Frames.Count == 0) return;
            if (_sharedSpriteSheets == null) return;
            
            var frame = _currentAnimation.Frames[_currentFrameIndex];
            
            string sheetName = (!string.IsNullOrEmpty(kitname)?kitname: (isHomeTeam ? "player_blue_multi" : "player_red_multi"));
            
            if (!_sharedSpriteSheets.ContainsKey(sheetName)) return;
            
            var spriteSheet = _sharedSpriteSheets[sheetName];
            
            // Calculate source rectangle
            int col = frame.SpriteIndex % SpritesPerRow;
            int row = frame.SpriteIndex / SpritesPerRow;
            
            Rectangle sourceRect = new Rectangle(
                col * SpriteSize,
                row * SpriteSize,
                SpriteSize,
                SpriteSize
            );
            
            // Calculate total rotation
            int totalRotation = (frame.Rotation + _additionalRotation) % 8;
            float rotation = totalRotation * (MathHelper.Pi / 4f);
            
            // Calculate mirror effect
            SpriteEffects effects = frame.Mirror == 1 ? SpriteEffects.FlipHorizontally : SpriteEffects.None;
            
            // Draw sprite
            Vector2 origin = new Vector2(SpriteSize / 2, SpriteSize / 2);
            
            spriteBatch.Draw(
                spriteSheet,
                position,
                sourceRect,
                tint,
                rotation,
                origin,
                scale,
                effects,
                0f
            );
        }
    }
}
