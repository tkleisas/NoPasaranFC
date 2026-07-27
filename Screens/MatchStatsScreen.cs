using NoPasaranFC.Debugging;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NoPasaranFC.Screens
{
    /// <summary>
    /// Post-match statistics screen, pushed on top of RoundResultsScreen.
    /// ENTER/ESC finishes it, revealing the round results underneath.
    /// </summary>
    public class MatchStatsScreen : Screen
    {
        private const int MaxPlayerRows = 11;

        private readonly MatchEngine _engine;
        private readonly ScreenManager _screenManager;
        private readonly ContentManager _contentManager;
        private readonly GraphicsDevice _graphicsDevice;

        private SpriteFont _font;
        private Texture2D _pixel;
        private KeyboardState _previousKeyboardState;
        private Gameplay.InputHelper _input = new Gameplay.InputHelper();

        private readonly Color _homeColor;
        private readonly Color _awayColor;
        private readonly List<(Player player, PlayerMatchStats stats)> _homeRows;
        private readonly List<(Player player, PlayerMatchStats stats)> _awayRows;

        public MatchStatsScreen(MatchEngine engine, ScreenManager screenManager,
            ContentManager content, GraphicsDevice graphicsDevice)
            : base(content, graphicsDevice)
        {
            _engine = engine;
            _screenManager = screenManager;
            _contentManager = content;
            _graphicsDevice = graphicsDevice;

            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            _homeRows = BuildPlayerRows(engine.HomeTeam);
            _awayRows = BuildPlayerRows(engine.AwayTeam);

            // Bar colors from each team's shirt (fall back to blue/red)
            _homeColor = GetShirtColor(engine.HomeTeam, new Color(80, 130, 220));
            _awayColor = GetShirtColor(engine.AwayTeam, new Color(210, 70, 60));
        }

        private List<(Player player, PlayerMatchStats stats)> BuildPlayerRows(Team team)
        {
            if (team == null) return new List<(Player player, PlayerMatchStats stats)>();
            return team.Players
                .Select(p => (player: p, stats: _engine.Stats.For(p)))
                .OrderByDescending(r => r.stats.HasActivity)
                .ThenByDescending(r => r.player.IsStarting)
                .ThenByDescending(r => r.stats.Goals + r.stats.Assists)
                .ThenBy(r => r.player.ShirtNumber)
                .Take(MaxPlayerRows)
                .ToList();
        }

        private Color GetShirtColor(Team team, Color fallback)
        {
            if (team?.Players == null || team.Players.Count == 0) return fallback;
            var player = team.Players.FirstOrDefault(p => p.Position != PlayerPosition.Goalkeeper)
                ?? team.Players[0];
            int homeTeamId = _engine.HomeTeam?.Id ?? 0;
            Graphics3D.MatchRenderer3D.GetKitColors(player, homeTeamId, out Color shirt, out _, out _);
            return shirt;
        }

        public override void Update(GameTime gameTime)
        {
            _input.Update();
            var touchUI = Gameplay.TouchUI.Instance;

            if (_font == null)
            {
                _font = _contentManager.Load<SpriteFont>("Font");
            }

            KeyboardState keyboardState = DebugInput.GetState();

            // ENTER or ESC dismisses the stats, revealing the round results underneath
            if ((keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter)) ||
                (keyboardState.IsKeyDown(Keys.Escape) && _previousKeyboardState.IsKeyUp(Keys.Escape)) ||
                (keyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space)) ||
                _input.IsConfirmPressed() || _input.IsBackPressed() ||
                touchUI.IsActionJustPressed || touchUI.IsBackJustPressed)
            {
                IsFinished = true;
            }

            _previousKeyboardState = keyboardState;
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (_font == null) return; // Not loaded yet

            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            float scale = Game1.UIScale;

            // Title
            string title = Localization.Instance.Get("stats.title");
            DrawCentered(spriteBatch, title, 30 * scale, Color.Yellow, scale);

            // Final score
            string homeName = _engine.HomeTeam?.Name ?? "HOME";
            string awayName = _engine.AwayTeam?.Name ?? "AWAY";
            string score = $"{homeName} {_engine.HomeScore} - {_engine.AwayScore} {awayName}";
            DrawCentered(spriteBatch, score, 65 * scale, Color.White, scale);

            // Team comparison block
            var home = _engine.HomeTeam;
            var away = _engine.AwayTeam;
            float homePoss = _engine.Stats.For(home).PossessionSeconds;
            float awayPoss = _engine.Stats.For(away).PossessionSeconds;
            float possTotal = homePoss + awayPoss;
            int homePossPct = (int)Math.Round(possTotal > 0 ? homePoss / possTotal * 100f : 50f);
            int awayPossPct = 100 - homePossPct;

            int homeShots = SumPlayers(home, s => s.Shots);
            int awayShots = SumPlayers(away, s => s.Shots);
            int homeOnTarget = SumPlayers(home, s => s.ShotsOnTarget);
            int awayOnTarget = SumPlayers(away, s => s.ShotsOnTarget);
            int homePasses = SumPlayers(home, s => s.Passes);
            int awayPasses = SumPlayers(away, s => s.Passes);
            int homePassesDone = SumPlayers(home, s => s.PassesCompleted);
            int awayPassesDone = SumPlayers(away, s => s.PassesCompleted);
            int homeFouls = SumPlayers(home, s => s.FoulsCommitted);
            int awayFouls = SumPlayers(away, s => s.FoulsCommitted);
            int homeSaves = SumPlayers(home, s => s.Saves);
            int awaySaves = SumPlayers(away, s => s.Saves);

            float y = 110 * scale;
            float rowHeight = 30 * scale;
            DrawStatRow(spriteBatch, y, "stats.possession", homePossPct, awayPossPct,
                $"{homePossPct}%", $"{awayPossPct}%", scale); y += rowHeight;
            DrawStatRow(spriteBatch, y, "stats.shots", homeShots, awayShots,
                homeShots.ToString(), awayShots.ToString(), scale); y += rowHeight;
            DrawStatRow(spriteBatch, y, "stats.onTarget", homeOnTarget, awayOnTarget,
                homeOnTarget.ToString(), awayOnTarget.ToString(), scale); y += rowHeight;
            DrawStatRow(spriteBatch, y, "stats.passes", homePasses, awayPasses,
                homePasses.ToString(), awayPasses.ToString(), scale); y += rowHeight;
            float homeAcc = homePasses > 0 ? (float)homePassesDone / homePasses : 0f;
            float awayAcc = awayPasses > 0 ? (float)awayPassesDone / awayPasses : 0f;
            DrawStatRow(spriteBatch, y, "stats.passAcc", homeAcc, awayAcc,
                $"{homeAcc * 100f:0}%", $"{awayAcc * 100f:0}%", scale); y += rowHeight;
            DrawStatRow(spriteBatch, y, "stats.fouls", homeFouls, awayFouls,
                homeFouls.ToString(), awayFouls.ToString(), scale); y += rowHeight;
            DrawStatRow(spriteBatch, y, "stats.corners", _engine.Stats.For(home).Corners,
                _engine.Stats.For(away).Corners, _engine.Stats.For(home).Corners.ToString(),
                _engine.Stats.For(away).Corners.ToString(), scale); y += rowHeight;
            DrawStatRow(spriteBatch, y, "stats.offsides", _engine.Stats.For(home).Offsides,
                _engine.Stats.For(away).Offsides, _engine.Stats.For(home).Offsides.ToString(),
                _engine.Stats.For(away).Offsides.ToString(), scale); y += rowHeight;
            DrawStatRow(spriteBatch, y, "stats.saves", homeSaves, awaySaves,
                homeSaves.ToString(), awaySaves.ToString(), scale); y += rowHeight;

            // Player tables (home left half, away right half)
            float tableY = y + 20 * scale;
            DrawPlayerTable(spriteBatch, home, _homeRows, _homeColor, screenWidth * 0.03f, tableY, scale);
            DrawPlayerTable(spriteBatch, away, _awayRows, _awayColor, screenWidth * 0.5f + screenWidth * 0.03f, tableY, scale);

            // Bottom hint
            string hint = Localization.Instance.Get("stats.continue");
            DrawCentered(spriteBatch, hint, screenHeight - 40 * scale, Color.LightGray, scale);
        }

        private int SumPlayers(Team team, Func<PlayerMatchStats, int> selector)
        {
            if (team == null) return 0;
            return team.Players.Sum(p => selector(_engine.Stats.For(p)));
        }

        private void DrawCentered(SpriteBatch spriteBatch, string text, float y, Color color, float scale)
        {
            Vector2 size = _font.MeasureString(text) * scale;
            spriteBatch.DrawString(_font, text,
                new Vector2((Game1.ScreenWidth - size.X) / 2, y), color,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        /// <summary>One comparison row: home value left, centered label, away value right, two-sided bar.</summary>
        private void DrawStatRow(SpriteBatch spriteBatch, float y, string labelKey,
            float homeValue, float awayValue, string homeText, string awayText, float scale)
        {
            float centerX = Game1.ScreenWidth / 2f;
            string label = Localization.Instance.Get(labelKey);
            Vector2 labelSize = _font.MeasureString(label) * scale;

            // Two-sided bar first (label draws over it): home left, away right
            float total = homeValue + awayValue;
            if (total > 0f)
            {
                float barMax = 90 * scale;
                int barHeight = (int)(12 * scale);
                int barY = (int)(y + (labelSize.Y - barHeight) / 2);
                int homeLen = (int)(homeValue / total * barMax);
                int awayLen = (int)(awayValue / total * barMax);
                if (homeLen > 0)
                    spriteBatch.Draw(_pixel, new Rectangle((int)(centerX - 20 * scale) - homeLen, barY, homeLen, barHeight), _homeColor * 0.65f);
                if (awayLen > 0)
                    spriteBatch.Draw(_pixel, new Rectangle((int)(centerX + 20 * scale), barY, awayLen, barHeight), _awayColor * 0.65f);
            }

            spriteBatch.DrawString(_font, label, new Vector2(centerX - labelSize.X / 2, y), Color.White,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            // Home value right-aligned left of the bar area
            Vector2 homeSize = _font.MeasureString(homeText) * scale;
            spriteBatch.DrawString(_font, homeText,
                new Vector2(centerX - 140 * scale - homeSize.X, y), _homeColor,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            // Away value left-aligned right of the bar area
            spriteBatch.DrawString(_font, awayText,
                new Vector2(centerX + 140 * scale, y), _awayColor,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }

        private void DrawPlayerTable(SpriteBatch spriteBatch, Team team,
            List<(Player player, PlayerMatchStats stats)> rows, Color teamColor, float x, float y, float scale)
        {
            float colNum = x;
            float colName = x + 35 * scale;
            float colStats = x + 190 * scale;
            float statStep = 38 * scale;
            float rowHeight = 22 * scale;

            // Team name above the table
            string teamName = team?.Name ?? "";
            spriteBatch.DrawString(_font, teamName, new Vector2(x, y), teamColor,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            y += 30 * scale;

            // Column headers
            spriteBatch.DrawString(_font, "#", new Vector2(colNum, y), Color.Red,
                0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            string[] headers = { "stats.col.g", "stats.col.a", "stats.col.sh", "stats.col.ot",
                "stats.col.ps", "stats.col.tk", "stats.col.fc", "stats.col.yc", "stats.col.rc" };
            for (int i = 0; i < headers.Length; i++)
            {
                spriteBatch.DrawString(_font, Localization.Instance.Get(headers[i]),
                    new Vector2(colStats + i * statStep, y), Color.Red,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
            }
            y += rowHeight;

            float nameMaxWidth = colStats - colName - 5 * scale;
            foreach (var (player, stats) in rows)
            {
                Color color = player.IsControlled ? Color.Yellow : Color.White;
                spriteBatch.DrawString(_font, player.ShirtNumber.ToString(), new Vector2(colNum, y), color,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                spriteBatch.DrawString(_font, Truncate(player.Name, nameMaxWidth, scale), new Vector2(colName, y), color,
                    0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                int[] values = { stats.Goals, stats.Assists, stats.Shots, stats.ShotsOnTarget,
                    stats.Passes, stats.Tackles, stats.FoulsCommitted, stats.YellowCards, stats.RedCards };
                for (int i = 0; i < values.Length; i++)
                {
                    spriteBatch.DrawString(_font, values[i].ToString(),
                        new Vector2(colStats + i * statStep, y), color,
                        0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }
                y += rowHeight;
            }
        }

        private string Truncate(string text, float maxWidth, float scale)
        {
            if (string.IsNullOrEmpty(text)) return "";
            if (_font.MeasureString(text).X * scale <= maxWidth) return text;
            while (text.Length > 1 && _font.MeasureString(text + ".").X * scale > maxWidth)
                text = text.Substring(0, text.Length - 1);
            return text + ".";
        }
    }
}
