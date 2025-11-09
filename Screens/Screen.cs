using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Screens
{
    public abstract class Screen
    {
        public bool IsActive { get; set; }
        
        public abstract void Update(GameTime gameTime);
        public abstract void Draw(SpriteBatch spriteBatch, SpriteFont font);
    }
}
