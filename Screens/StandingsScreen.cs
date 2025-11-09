using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Models;

namespace NoPasaranFC.Screens
{
    public class StandingsScreen : Screen
    {
        private Championship _championship;
        private ScreenManager _screenManager;
        private KeyboardState _previousKeyState;
        
        public StandingsScreen(Championship championship, ScreenManager screenManager)
        {
            _championship = championship;
            _screenManager = screenManager;
        }
        
        public override void Update(GameTime gameTime)
        {
            var keyState = Keyboard.GetState();
            
            if (keyState.IsKeyDown(Keys.Escape) && !_previousKeyState.IsKeyDown(Keys.Escape))
            {
                _screenManager.PopScreen();
            }
            
            _previousKeyState = keyState;
        }
        
        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            
            // Draw title centered
            string title = "Championship Standings";
            Vector2 titleSize = font.MeasureString(title);
            spriteBatch.DrawString(font, title, new Vector2((screenWidth - titleSize.X) / 2, 50), Color.Yellow);
            
            // Draw header
            string header = "Team                 W  D  L  GF GA Pts";
            Vector2 headerSize = font.MeasureString(header);
            float tableX = (screenWidth - headerSize.X) / 2;
            spriteBatch.DrawString(font, header, new Vector2(tableX, 100), Color.Gray);
            
            var standings = _championship.GetStandings();
            for (int i = 0; i < standings.Count; i++)
            {
                var team = standings[i];
                string line = $"{i + 1}. {team.Name,-18} {team.Wins,2} {team.Draws,2} {team.Losses,2} {team.GoalsFor,3} {team.GoalsAgainst,2} {team.Points,3}";
                var color = team.IsPlayerControlled ? Color.Yellow : Color.White;
                spriteBatch.DrawString(font, line, new Vector2(tableX, 130 + i * 30), color);
            }
            
            string instructions = "Press ESC to return";
            Vector2 instrSize = font.MeasureString(instructions);
            spriteBatch.DrawString(font, instructions, new Vector2((screenWidth - instrSize.X) / 2, screenHeight - 60), Color.Gray);
        }
    }
}
