using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Content;

namespace NoPasaranFC.Screens
{
    public abstract class Screen
    {
        public bool IsActive { get; set; }
        public bool IsFinished { get; set; }
        
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
        
        public abstract void Update(GameTime gameTime);
        public abstract void Draw(SpriteBatch spriteBatch, SpriteFont font);
    }
}
