using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Screens
{
    public class ScreenManager
    {
        private Stack<Screen> _screens;
        private Game1 _game;
        
        public int ScreenWidth => Game1.ScreenWidth;
        public int ScreenHeight => Game1.ScreenHeight;
        
        public ScreenManager(Game1 game)
        {
            _screens = new Stack<Screen>();
            _game = game;
        }
        
        public void SetResolution(int width, int height, bool fullscreen)
        {
            _game.ApplyResolution(width, height, fullscreen);
        }
        
        public void PushScreen(Screen screen)
        {
            if (_screens.Count > 0)
            {
                _screens.Peek().IsActive = false;
            }
            _screens.Push(screen);
            screen.IsActive = true;
        }
        
        public void PopScreen()
        {
            if (_screens.Count > 0)
            {
                _screens.Pop();
            }
            
            if (_screens.Count > 0)
            {
                _screens.Peek().IsActive = true;
            }
        }
        
        public Screen CurrentScreen => _screens.Count > 0 ? _screens.Peek() : null;
        
        public void Update(GameTime gameTime)
        {
            CurrentScreen?.Update(gameTime);
        }
        
        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            CurrentScreen?.Draw(spriteBatch, font);
        }
    }
}
