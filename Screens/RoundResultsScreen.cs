using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Database;
using NoPasaranFC.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NoPasaranFC.Screens
{
    public class RoundResultsScreen : Screen
    {
        private Championship _championship;
        private DatabaseManager _database;
        private ScreenManager _screenManager;
        private ContentManager _contentManager;
        private GraphicsDevice _graphicsDevice;
        
        private SpriteFont _font;
        private int _matchweek;
        private List<Match> _matchweekMatches;
        private KeyboardState _previousKeyboardState;
        private Texture2D _trophySprite;
        public RoundResultsScreen(Championship championship, int matchweek, DatabaseManager database, 
            ScreenManager screenManager, ContentManager contentManager, GraphicsDevice graphicsDevice)
        {
            _championship = championship;
            _matchweek = matchweek;
            _database = database;
            _screenManager = screenManager;
            _contentManager = contentManager;
            _graphicsDevice = graphicsDevice;
            
            _matchweekMatches = championship.GetMatchesForMatchweek(matchweek);
            _trophySprite = _contentManager.Load<Texture2D>("Sprites/trophy");
        }
        
        public override void Update(GameTime gameTime)
        {
            // Load font on first update if not loaded
            if (_font == null)
            {
                _font = _contentManager.Load<SpriteFont>("Font");
            }
            
            KeyboardState keyboardState = Keyboard.GetState();
            
            // Press Enter or Space to continue
            if ((keyboardState.IsKeyDown(Keys.Enter) && _previousKeyboardState.IsKeyUp(Keys.Enter)) ||
                (keyboardState.IsKeyDown(Keys.Space) && _previousKeyboardState.IsKeyUp(Keys.Space)))
            {
                // Pop back to menu
                _screenManager.PopScreen();
            }
            
            _previousKeyboardState = keyboardState;
        }
        
        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (_font == null) return; // Not loaded yet
            
            string title = Localization.Instance.Get("round_results_title").Replace("{0}", _matchweek.ToString());
            Vector2 titleSize = _font.MeasureString(title);
            Vector2 titlePos = new Vector2(
                (_graphicsDevice.Viewport.Width - titleSize.X) / 2,
                50
            );
            spriteBatch.DrawString(_font, title, titlePos, Color.White);
            
            // Draw all matches for this matchweek
            float yOffset = 150;
            float lineHeight = 40;
            if(_championship.IsChampionshipOver())
            {
                var championTeam = _championship.GetChampionTeam();
                // Draw trophy if player is champion
                Vector2 trophyPos = new Vector2(
                    (_graphicsDevice.Viewport.Width - _trophySprite.Width) / 2,
                    yOffset
                );
                yOffset = yOffset + _trophySprite.Height + lineHeight;
                spriteBatch.Draw(_trophySprite, trophyPos, Color.White);
                string championText = Localization.Instance.Get("round_results_champion")
                    .Replace("{0}", championTeam.Name);
                Vector2 championSize = _font.MeasureString(championText);
                Vector2 championPos = new Vector2(
                    (_graphicsDevice.Viewport.Width - championSize.X) / 2,
                    yOffset
                );
                spriteBatch.DrawString(_font, championText, championPos, Color.Gold);
                yOffset = yOffset + lineHeight;

            }
            foreach (var match in _matchweekMatches.OrderBy(m => m.Id))
            {
                var homeTeam = _championship.Teams.Find(t => t.Id == match.HomeTeamId);
                var awayTeam = _championship.Teams.Find(t => t.Id == match.AwayTeamId);
                
                if (homeTeam != null && awayTeam != null)
                {
                    string matchText;
                    Color textColor = Color.White;
                    
                    if (match.IsPlayed)
                    {
                        string leftString = homeTeam.Name;
                        string rightString = awayTeam.Name;
                        string middlestring = $"{match.HomeScore} - {match.AwayScore}";
                        leftString = leftString.PadLeft(20);
                        rightString = rightString.PadRight(20);
                        middlestring = middlestring.PadLeft(6);
                        middlestring = middlestring.PadRight(7);
                        //matchText = $"{homeTeam.Name}  {match.HomeScore} - {match.AwayScore}  {awayTeam.Name}";
                        matchText = $"{leftString}{middlestring}{rightString}";
                        // Highlight player's team match
                        if (homeTeam.IsPlayerControlled || awayTeam.IsPlayerControlled)
                        {
                            textColor = Color.Yellow;
                        }
                    }
                    else
                    {
                        matchText = $"{homeTeam.Name}  vs  {awayTeam.Name}";
                        textColor = Color.Gray;
                    }
                    
                    Vector2 matchSize = _font.MeasureString(matchText);
                    Vector2 matchPos = new Vector2(
                        (_graphicsDevice.Viewport.Width - matchSize.X) / 2,
                        yOffset
                    );
                    
                    spriteBatch.DrawString(_font, matchText, matchPos, textColor);
                    yOffset += lineHeight;
                }
            }
            
            // Instructions at bottom
            string instructions = Localization.Instance.Get("round_results_continue");
            Vector2 instructionsSize = _font.MeasureString(instructions);
            Vector2 instructionsPos = new Vector2(
                (_graphicsDevice.Viewport.Width - instructionsSize.X) / 2,
                _graphicsDevice.Viewport.Height - 80
            );
            spriteBatch.DrawString(_font, instructions, instructionsPos, Color.LightGray);
        }
    }
}
