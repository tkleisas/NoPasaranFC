using NoPasaranFC.Debugging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Models;
using System.Linq;

namespace NoPasaranFC.Screens
{
    public class StandingsScreen : Screen
    {
        private Championship _championship;
        private ScreenManager _screenManager;
        private KeyboardState _previousKeyState;
        private Gameplay.InputHelper _input = new Gameplay.InputHelper();
        private bool _showScorers;
        
        public StandingsScreen(Championship championship, ScreenManager screenManager)
        {
            _championship = championship;
            _screenManager = screenManager;
        }
        
        public override void Update(GameTime gameTime)
        {
            _input.Update();
            var keyState = DebugInput.GetState();
            var touchUI = Gameplay.TouchUI.Instance;
            
            // Back (Escape, B button, or touch B/A)
            if ((keyState.IsKeyDown(Keys.Escape) && !_previousKeyState.IsKeyDown(Keys.Escape)) ||
                _input.IsBackPressed() || touchUI.IsBackJustPressed || touchUI.IsActionJustPressed)
            {
                _screenManager.PopScreen();
            }

            // Toggle standings / top scorers page (TAB or Left/Right arrows)
            if ((keyState.IsKeyDown(Keys.Tab) && !_previousKeyState.IsKeyDown(Keys.Tab)) ||
                (keyState.IsKeyDown(Keys.Left) && !_previousKeyState.IsKeyDown(Keys.Left)) ||
                (keyState.IsKeyDown(Keys.Right) && !_previousKeyState.IsKeyDown(Keys.Right)))
            {
                _showScorers = !_showScorers;
            }

            _previousKeyState = keyState;
        }
        
        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            float scale = Game1.UIScale;
            
            // Draw title centered
            string title = Localization.Instance.Get(_showScorers ? "stats.scorers" : "standings.title");
            Vector2 titleSize = font.MeasureString(title) * scale;
            spriteBatch.DrawString(font, title, new Vector2((screenWidth - titleSize.X) / 2, 50), Color.Yellow,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Define column positions (scaled)
            float tableStartX = screenWidth * 0.05f;

            if (_showScorers)
            {
                DrawScorers(spriteBatch, font, tableStartX, scale);
            }
            else
            {
            float posCol = tableStartX;
            float teamCol = tableStartX + 110 * scale;
            float wCol = tableStartX + 460 * scale;
            float dCol = tableStartX + 570 * scale;
            float lCol = tableStartX + 680 * scale;
            float gfCol = tableStartX + 790 * scale;
            float gaCol = tableStartX + 900 * scale;
            float ptsCol = tableStartX + 1010 * scale;

            float headerY = 100;

            // Draw header
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.pos"), new Vector2(posCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.team"), new Vector2(teamCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.wins"), new Vector2(wCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.draws"), new Vector2(dCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.losses"), new Vector2(lCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.gf"), new Vector2(gfCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.ga"), new Vector2(gaCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.points"), new Vector2(ptsCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Draw standings rows
            var standings = _championship.GetStandings();
            float rowHeight = 30 * scale;
            for (int i = 0; i < standings.Count; i++)
            {
                var team = standings[i];
                float rowY = 130 + i * rowHeight;
                var color = team.IsPlayerControlled ? Color.Yellow : Color.White;

                // Draw each column
                spriteBatch.DrawString(font, $"{i + 1}.", new Vector2(posCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, team.Name, new Vector2(teamCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, team.Wins.ToString(), new Vector2(wCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, team.Draws.ToString(), new Vector2(dCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, team.Losses.ToString(), new Vector2(lCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, team.GoalsFor.ToString(), new Vector2(gfCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, team.GoalsAgainst.ToString(), new Vector2(gaCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, team.Points.ToString(), new Vector2(ptsCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            }

            string instructions = Localization.Instance.Get("standings.toggleHint") +
                "  |  " + Localization.Instance.Get("standings.back");
            Vector2 instrSize = font.MeasureString(instructions) * scale;
            spriteBatch.DrawString(font, instructions, new Vector2((screenWidth - instrSize.X) / 2, screenHeight - 60), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>Top scorers across all teams, sorted by goals then assists (top 20).</summary>
        private void DrawScorers(SpriteBatch spriteBatch, SpriteFont font, float tableStartX, float scale)
        {
            float rankCol = tableStartX;
            float playerCol = tableStartX + 60 * scale;
            float teamCol = tableStartX + 420 * scale;
            float gCol = tableStartX + 680 * scale;
            float aCol = tableStartX + 750 * scale;
            float ycCol = tableStartX + 820 * scale;
            float rcCol = tableStartX + 890 * scale;

            float headerY = 100;
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.pos"), new Vector2(rankCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("stats.player"), new Vector2(playerCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("standings.team"), new Vector2(teamCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("stats.col.g"), new Vector2(gCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("stats.col.a"), new Vector2(aCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("stats.col.yc"), new Vector2(ycCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            spriteBatch.DrawString(font, Localization.Instance.Get("stats.col.rc"), new Vector2(rcCol, headerY), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            var scorers = _championship.Teams
                .SelectMany(t => t.Players.Select(p => (player: p, team: t)))
                .OrderByDescending(r => r.player.SeasonGoals)
                .ThenByDescending(r => r.player.SeasonAssists)
                .Take(20)
                .ToList();

            float rowHeight = 26 * scale;
            for (int i = 0; i < scorers.Count; i++)
            {
                var (player, team) = scorers[i];
                float rowY = headerY + 40 * scale + i * rowHeight;
                var color = team.IsPlayerControlled ? Color.Yellow : Color.White;
                string playerName = player.Name.Length > 18 ? player.Name.Substring(0, 17) + "…" : player.Name;
                string teamName = team.Name.Length > 16 ? team.Name.Substring(0, 15) + "…" : team.Name;

                spriteBatch.DrawString(font, $"{i + 1}.", new Vector2(rankCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, playerName, new Vector2(playerCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, teamName, new Vector2(teamCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, player.SeasonGoals.ToString(), new Vector2(gCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, player.SeasonAssists.ToString(), new Vector2(aCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, player.SeasonYellowCards.ToString(), new Vector2(ycCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, player.SeasonRedCards.ToString(), new Vector2(rcCol, rowY), color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
        }
    }
}
