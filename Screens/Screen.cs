using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Input;

namespace NoPasaranFC.Screens
{
    public abstract class Screen
    {
        public bool IsActive { get; set; }
        public bool IsFinished { get; set; }

        // Dismiss re-arm: when screens are stacked (round results -> stats ->
        // champion), the press that dismisses the top screen is still held on
        // the next screen's first frames, where the edge-trigger state is stale
        // (never updated while covered), so the held key reads as a fresh press
        // and the freshly revealed screen pops itself unseen. Dismiss is only
        // accepted after the dismiss keys/buttons have been seen fully released.
        private bool _dismissArmed;

        /// <summary>
        /// Returns true once the dismiss inputs (Enter/Space/Escape, left click)
        /// have been observed fully released at least once since the screen was
        /// shown. Gate dismiss handling on this to block held-key fall-through.
        /// </summary>
        protected bool DismissReArmed(KeyboardState keys, MouseState mouse)
        {
            if (_dismissArmed) return true;
            if (keys.IsKeyUp(Keys.Enter) && keys.IsKeyUp(Keys.Space) &&
                keys.IsKeyUp(Keys.Escape) && mouse.LeftButton == ButtonState.Released)
                _dismissArmed = true;
            return false;
        }
        
        protected ContentManager Content { get; }
        protected GraphicsDevice GraphicsDevice { get; }
        
        protected Screen()
        {
        }
        
        protected Screen(ContentManager content, GraphicsDevice graphicsDevice)
        {
            Content = content;
            GraphicsDevice = graphicsDevice;
        }
        
        public virtual void OnActivated()
        {
            // Override in derived classes to handle screen activation
        }
        
        public abstract void Update(GameTime gameTime);
        public abstract void Draw(SpriteBatch spriteBatch, SpriteFont font);
    }
}
