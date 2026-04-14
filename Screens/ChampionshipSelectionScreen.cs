using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Database;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;

namespace NoPasaranFC.Screens
{
    /// <summary>
    /// Lets the player pick which championship (game mode) to play.
    /// On confirmation, the chosen championship is built, saved to the database,
    /// and the currently-active championship object is mutated in place so that
    /// references held by MenuScreen / Game1 stay valid.
    /// </summary>
    public class ChampionshipSelectionScreen : Screen
    {
        private readonly Championship _activeChampionship;
        private readonly DatabaseManager _database;
        private readonly ScreenManager _screenManager;
        private readonly List<ChampionshipDefinition> _championships;
        private readonly bool _canCancel;
        private readonly InputHelper _input = new InputHelper();
        private int _selectedIndex;
        private float _joystickMenuCooldown;

        public ChampionshipSelectionScreen(
            Championship activeChampionship,
            DatabaseManager database,
            ScreenManager screenManager,
            bool canCancel)
        {
            _activeChampionship = activeChampionship;
            _database = database;
            _screenManager = screenManager;
            _canCancel = canCancel;
            _championships = ChampionshipInitializer.GetAvailableChampionships() ?? new List<ChampionshipDefinition>();
            _selectedIndex = 0;

            // Pre-select the currently-active championship if it matches one of the options.
            if (!string.IsNullOrEmpty(activeChampionship?.Id))
            {
                for (int i = 0; i < _championships.Count; i++)
                {
                    if (string.Equals(_championships[i].Id, activeChampionship.Id, StringComparison.OrdinalIgnoreCase))
                    {
                        _selectedIndex = i;
                        break;
                    }
                }
            }
        }

        public override void OnActivated()
        {
            // Reset input when returning from a nested screen to prevent key bleed-through.
        }

        public override void Update(GameTime gameTime)
        {
            _input.Update();
            var touchUI = TouchUI.Instance;

            if (_championships.Count == 0)
            {
                // Nothing we can do — allow going back if permitted.
                if (_canCancel && (_input.IsBackPressed() || touchUI.IsBackJustPressed))
                {
                    AudioManager.Instance.PlaySoundEffect("menu_back");
                    _screenManager.PopScreen();
                }
                return;
            }

            Vector2 joystickDir = touchUI.JoystickDirection;
            bool menuDown = _input.IsMenuDownPressed() ||
                            (touchUI.Enabled && joystickDir.Y > 0.3f && _joystickMenuCooldown <= 0);
            bool menuUp = _input.IsMenuUpPressed() ||
                          (touchUI.Enabled && joystickDir.Y < -0.3f && _joystickMenuCooldown <= 0);

            if (menuDown)
            {
                _selectedIndex = (_selectedIndex + 1) % _championships.Count;
                AudioManager.Instance.PlaySoundEffect("menu_move");
                _joystickMenuCooldown = 0.15f;
            }

            if (menuUp)
            {
                _selectedIndex = (_selectedIndex - 1 + _championships.Count) % _championships.Count;
                AudioManager.Instance.PlaySoundEffect("menu_move");
                _joystickMenuCooldown = 0.15f;
            }

            if (_joystickMenuCooldown > 0)
                _joystickMenuCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Touch tap on an item selects + confirms
            var tap = touchUI.GetTapPosition();
            if (tap.HasValue)
            {
                int screenWidth = Game1.ScreenWidth;
                int screenHeight = Game1.ScreenHeight;
                float scale = Game1.UIScale;
                float itemHeight = 60f * scale;
                float menuStartY = screenHeight * 0.35f;
                for (int i = 0; i < _championships.Count; i++)
                {
                    float itemY = menuStartY + i * itemHeight;
                    var rect = new Rectangle(0, (int)itemY, screenWidth, (int)itemHeight);
                    if (rect.Contains(new Point((int)tap.Value.X, (int)tap.Value.Y)))
                    {
                        _selectedIndex = i;
                        AudioManager.Instance.PlaySoundEffect("menu_select");
                        ConfirmSelection();
                        return;
                    }
                }
            }

            if (_input.IsConfirmPressed() || touchUI.IsActionJustPressed)
            {
                AudioManager.Instance.PlaySoundEffect("menu_select");
                ConfirmSelection();
                return;
            }

            if (_canCancel && (_input.IsBackPressed() || touchUI.IsBackJustPressed))
            {
                AudioManager.Instance.PlaySoundEffect("menu_back");
                _screenManager.PopScreen();
            }
        }

        private void ConfirmSelection()
        {
            var definition = _championships[_selectedIndex];
            var newChampionship = ChampionshipInitializer.CreateNewChampionship(definition);

            // Wipe previous championship data before saving the new one so stale teams,
            // players and matches from the old mode don't leak into the new save.
            _database.ClearDatabase();

            // Mutate the active championship in place so existing references (MenuScreen,
            // Game1._championship) pick up the new data automatically.
            _activeChampionship.Id = newChampionship.Id;
            _activeChampionship.Name = newChampionship.Name;
            _activeChampionship.CurrentMatchweek = 0;
            _activeChampionship.Teams = newChampionship.Teams;
            _activeChampionship.Matches = newChampionship.Matches;

            _database.SaveChampionship(_activeChampionship);
            _screenManager.PopScreen();
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;
            float scale = Game1.UIScale;

            // Title
            string title = Localization.Instance.Get("championship.selectTitle");
            Vector2 titleSize = font.MeasureString(title) * scale;
            spriteBatch.DrawString(font, title,
                new Vector2((screenWidth - titleSize.X) / 2, screenHeight * 0.15f),
                Color.Yellow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (_championships.Count == 0)
            {
                string empty = Localization.Instance.Get("championship.none");
                Vector2 emptySize = font.MeasureString(empty) * scale;
                spriteBatch.DrawString(font, empty,
                    new Vector2((screenWidth - emptySize.X) / 2, screenHeight * 0.5f),
                    Color.White, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                return;
            }

            // Championship list
            float itemHeight = 60f * scale;
            float menuStartY = screenHeight * 0.35f;
            for (int i = 0; i < _championships.Count; i++)
            {
                var def = _championships[i];
                bool selected = i == _selectedIndex;
                string prefix = selected ? "> " : "  ";
                string suffix = selected ? " <" : "  ";
                string label = prefix + def.Name + suffix;
                Color color = selected ? Color.Yellow : Color.White;

                Vector2 size = font.MeasureString(label) * scale;
                Vector2 pos = new Vector2((screenWidth - size.X) / 2, menuStartY + i * itemHeight);
                spriteBatch.DrawString(font, label, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

                // Show team count underneath the selected championship
                if (selected)
                {
                    string sub = string.Format(Localization.Instance.Get("championship.teamCount"), def.Teams.Count);
                    Vector2 subSize = font.MeasureString(sub) * (scale * 0.75f);
                    Vector2 subPos = new Vector2((screenWidth - subSize.X) / 2, pos.Y + size.Y);
                    spriteBatch.DrawString(font, sub, subPos, Color.LightGray,
                        0f, Vector2.Zero, scale * 0.75f, SpriteEffects.None, 0f);
                }
            }

            // Instructions
            string instructions = _canCancel
                ? Localization.Instance.Get("championship.instructionsCancel")
                : Localization.Instance.Get("championship.instructions");
            Vector2 instrSize = font.MeasureString(instructions) * scale;
            spriteBatch.DrawString(font, instructions,
                new Vector2((screenWidth - instrSize.X) / 2, screenHeight * 0.85f),
                Color.Gray, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
