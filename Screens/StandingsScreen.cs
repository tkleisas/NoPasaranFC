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
            
            // Define column positions
            float tableStartX = screenWidth * 0.2f;
            float posCol = tableStartX;
            float teamCol = tableStartX + 40;
            float wCol = tableStartX + 280;
            float dCol = tableStartX + 320;
            float lCol = tableStartX + 360;
            float gfCol = tableStartX + 400;
            float gaCol = tableStartX + 450;
            float ptsCol = tableStartX + 500;
            
            float headerY = 100;
            
            // Draw header
            spriteBatch.DrawString(font, "Pos", new Vector2(posCol, headerY), Color.Gray);
            spriteBatch.DrawString(font, "Team", new Vector2(teamCol, headerY), Color.Gray);
            spriteBatch.DrawString(font, "W", new Vector2(wCol, headerY), Color.Gray);
            spriteBatch.DrawString(font, "D", new Vector2(dCol, headerY), Color.Gray);
            spriteBatch.DrawString(font, "L", new Vector2(lCol, headerY), Color.Gray);
            spriteBatch.DrawString(font, "GF", new Vector2(gfCol, headerY), Color.Gray);
            spriteBatch.DrawString(font, "GA", new Vector2(gaCol, headerY), Color.Gray);
            spriteBatch.DrawString(font, "Pts", new Vector2(ptsCol, headerY), Color.Gray);
            
            // Draw standings rows
            var standings = _championship.GetStandings();
            for (int i = 0; i < standings.Count; i++)
            {
                var team = standings[i];
                float rowY = 130 + i * 30;
                var color = team.IsPlayerControlled ? Color.Yellow : Color.White;
                
                // Draw each column
                spriteBatch.DrawString(font, $"{i + 1}.", new Vector2(posCol, rowY), color);
                spriteBatch.DrawString(font, team.Name, new Vector2(teamCol, rowY), color);
                spriteBatch.DrawString(font, team.Wins.ToString(), new Vector2(wCol, rowY), color);
                spriteBatch.DrawString(font, team.Draws.ToString(), new Vector2(dCol, rowY), color);
                spriteBatch.DrawString(font, team.Losses.ToString(), new Vector2(lCol, rowY), color);
                spriteBatch.DrawString(font, team.GoalsFor.ToString(), new Vector2(gfCol, rowY), color);
                spriteBatch.DrawString(font, team.GoalsAgainst.ToString(), new Vector2(gaCol, rowY), color);
                spriteBatch.DrawString(font, team.Points.ToString(), new Vector2(ptsCol, rowY), color);
            }
            
            string instructions = "Press ESC to return";
            Vector2 instrSize = font.MeasureString(instructions);
            spriteBatch.DrawString(font, instructions, new Vector2((screenWidth - instrSize.X) / 2, screenHeight - 60), Color.Gray);
        }
    }
}
