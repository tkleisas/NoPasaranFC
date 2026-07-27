using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Database;
using NoPasaranFC.Debugging;
using NoPasaranFC.Graphics3D;
using NoPasaranFC.Graphics3D.Skinning;
using NoPasaranFC.Models;

namespace NoPasaranFC.Screens
{
    /// <summary>
    /// In-game player/kit editor (desktop only). Browses both championships from
    /// championships_seed.json against the shared team catalog (teams_seed.json +
    /// teams_seed.custom.json overlay): Championship -> Teams -> Team -> Player/Kit/Paint.
    /// Every change writes through immediately to the overlay JSON and, when the team
    /// name matches a team in the ACTIVE championship, to the SQLite database.
    /// </summary>
    public class PlayerEditorScreen : Screen
    {
        private enum EditorPage { Championships, Teams, Team, Player, Kit, Paint }

        // ---- Row model (label + optional value + left/right adjust + confirm) ----
        private class Row
        {
            public Func<string> Label;
            public Func<string> Value;
            public Action<int> Adjust;
            public Action Confirm;
            public Func<Color?> Swatch;
        }

        private const int MaxVisibleRows = 16;
        private const int RowHeight = 32;
        private const int PaintGridSize = 32;
        private const int PaintCellPx = 12;
        private const int PortraitSize = 256;

        private readonly DatabaseManager _database;
        private readonly Gameplay.InputHelper _input = new Gameplay.InputHelper();
        private KeyboardState _previousKeyState;
        private float _joystickMenuCooldown = 0f;

        // ---- Catalog data (loaded once at construction) ----
        private readonly List<ChampionshipDefinition> _championships;
        private readonly List<Team> _catalog;
        private readonly List<Team> _dbTeams;
        private readonly string _overlayPath;

        // ---- Navigation state ----
        private EditorPage _page = EditorPage.Championships;
        private readonly List<Row> _rows = new List<Row>();
        private int _selected = 0;
        private int _scrollOffset = 0;
        private ChampionshipDefinition _championship;
        private List<Team> _champTeams = new List<Team>();
        private Team _team;
        private Player _player;

        // ---- Preview + paint state ----
        private Texture2D _pixel;
        private Texture2D _portrait;
        private int _paintCursorX = 0;
        private int _paintCursorY = 0;
        private int _paintColor = 1; // palette index 1..15 (hex-encodable)
        private string _status;
        private float _statusTimer = 0f;

        public PlayerEditorScreen(DatabaseManager database, ContentManager content, GraphicsDevice graphicsDevice)
            : base(content, graphicsDevice)
        {
            _database = database;

            string basePath = PlatformHelper.GetAssetPath(Path.Combine("Database", "teams_seed.json"));
            _overlayPath = Path.Combine(Path.GetDirectoryName(basePath), "teams_seed.custom.json");
            string champsPath = PlatformHelper.GetAssetPath(Path.Combine("Database", "championships_seed.json"));

            try { _championships = ChampionshipSeeder.LoadChampionshipsFromJson(champsPath); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayerEditorScreen: championships load failed: {ex.Message}");
                _championships = new List<ChampionshipDefinition>();
            }

            try { _catalog = TeamSeeder.LoadCatalog(basePath, _overlayPath); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayerEditorScreen: catalog load failed: {ex.Message}");
                _catalog = new List<Team>();
            }

            try { _dbTeams = database.LoadAllTeams(); }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayerEditorScreen: DB teams load failed: {ex.Message}");
                _dbTeams = new List<Team>();
            }

            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });

            BuildRows();
        }

        #region Page construction

        private void GoTo(EditorPage page)
        {
            _page = page;
            _selected = 0;
            _scrollOffset = 0;
            BuildRows();
        }

        private void GoBack()
        {
            switch (_page)
            {
                case EditorPage.Championships:
                    IsFinished = true;
                    break;
                case EditorPage.Teams:
                    GoTo(EditorPage.Championships);
                    break;
                case EditorPage.Team:
                    GoTo(EditorPage.Teams);
                    break;
                case EditorPage.Player:
                case EditorPage.Kit:
                    GoTo(EditorPage.Team);
                    break;
                case EditorPage.Paint:
                    Persist();
                    GoTo(EditorPage.Kit);
                    break;
            }
        }

        private void BuildRows()
        {
            _rows.Clear();
            var loc = Localization.Instance;

            switch (_page)
            {
                case EditorPage.Championships:
                    foreach (var champ in _championships)
                    {
                        var c = champ;
                        _rows.Add(new Row { Label = () => c.Name, Confirm = () => SelectChampionship(c) });
                    }
                    break;

                case EditorPage.Teams:
                    foreach (var team in _champTeams)
                    {
                        var t = team;
                        _rows.Add(new Row { Label = () => t.Name, Confirm = () => { _team = t; GoTo(EditorPage.Team); } });
                    }
                    _rows.Add(new Row { Label = () => loc.Get("editor.back"), Confirm = () => GoTo(EditorPage.Championships) });
                    break;

                case EditorPage.Team:
                    foreach (var player in _team.Players)
                    {
                        var p = player;
                        _rows.Add(new Row
                        {
                            Label = () => $"#{p.ShirtNumber} {p.Name}",
                            Value = () => PositionAbbrev(p.Position),
                            Confirm = () => { _player = p; GoTo(EditorPage.Player); RegeneratePortrait(); }
                        });
                    }
                    _rows.Add(new Row
                    {
                        Label = () => loc.Get("editor.kit"),
                        Confirm = () =>
                        {
                            _player = _team.Players.FirstOrDefault(p => p.Position != PlayerPosition.Goalkeeper)
                                      ?? _team.Players.FirstOrDefault();
                            GoTo(EditorPage.Kit);
                            RegeneratePortrait();
                        }
                    });
                    _rows.Add(new Row { Label = () => loc.Get("editor.back"), Confirm = () => GoTo(EditorPage.Teams) });
                    break;

                case EditorPage.Player:
                    BuildPlayerRows();
                    _rows.Add(new Row { Label = () => loc.Get("editor.back"), Confirm = () => GoTo(EditorPage.Team) });
                    break;

                case EditorPage.Kit:
                    BuildKitRows();
                    _rows.Add(new Row { Label = () => loc.Get("editor.back"), Confirm = () => GoTo(EditorPage.Team) });
                    break;
            }
        }

        private void SelectChampionship(ChampionshipDefinition champ)
        {
            _championship = champ;
            _champTeams = new List<Team>();
            if (champ.Teams != null)
            {
                foreach (var entry in champ.Teams)
                {
                    // Resolve against the catalog by exact name; kitName/logo overrides
                    // in the championship entry are ignored - we edit the catalog team.
                    var team = _catalog.FirstOrDefault(t =>
                        string.Equals(t.Name, entry.Name, StringComparison.OrdinalIgnoreCase));
                    if (team == null)
                    {
                        // Teams absent from the catalog are synthesized at
                        // championship creation - do the same here and add them,
                        // so every team of both championships is editable.
                        bool isPlayer = !string.IsNullOrEmpty(champ.PlayerTeam) &&
                            string.Equals(entry.Name, champ.PlayerTeam, StringComparison.OrdinalIgnoreCase);
                        team = TeamSeeder.CreateTeamWithDefaultRoster(entry.Name, isPlayer,
                            entry.KitName, entry.Logo);
                        _catalog.Add(team);
                    }
                    _champTeams.Add(team);
                }
            }
            GoTo(EditorPage.Teams);
        }

        private void BuildPlayerRows()
        {
            var loc = Localization.Instance;

            void StatRow(string labelKey, Func<Player, int> get, Action<Player, int> set)
            {
                _rows.Add(new Row
                {
                    Label = () => loc.Get(labelKey),
                    Value = () => get(_player).ToString(),
                    Adjust = dir => { set(_player, Math.Clamp(get(_player) + dir, 0, 99)); AfterChange(); }
                });
            }

            StatRow("lineup.stat.spd", p => p.Speed, (p, v) => p.Speed = v);
            StatRow("lineup.stat.sht", p => p.Shooting, (p, v) => p.Shooting = v);
            StatRow("lineup.stat.pas", p => p.Passing, (p, v) => p.Passing = v);
            StatRow("lineup.stat.def", p => p.Defending, (p, v) => p.Defending = v);
            StatRow("lineup.stat.agi", p => p.Agility, (p, v) => p.Agility = v);
            StatRow("lineup.stat.tec", p => p.Technique, (p, v) => p.Technique = v);

            // Gender: -1 auto, 1 male, 2 female
            _rows.Add(new Row
            {
                Label = () => loc.Get("editor.gender"),
                Value = () => _player.GenderOverride switch
                {
                    1 => loc.Get("editor.male"),
                    2 => loc.Get("editor.female"),
                    _ => loc.Get("editor.auto")
                },
                Adjust = dir =>
                {
                    int[] values = { -1, 1, 2 };
                    int idx = Array.IndexOf(values, _player.GenderOverride);
                    if (idx < 0) idx = 0;
                    _player.GenderOverride = values[(idx + dir + values.Length) % values.Length];
                    AfterChange();
                }
            });

            // Skin tone: auto + 0-4
            _rows.Add(new Row
            {
                Label = () => loc.Get("editor.skin"),
                Value = () => _player.SkinToneOverride < 0 ? loc.Get("editor.auto") : _player.SkinToneOverride.ToString(),
                Adjust = dir => { _player.SkinToneOverride = CycleInt(_player.SkinToneOverride, -1, 4, dir); AfterChange(); }
            });

            // Hair color: auto + 0-5
            _rows.Add(new Row
            {
                Label = () => loc.Get("editor.hair"),
                Value = () => _player.HairColorOverride < 0 ? loc.Get("editor.auto") : _player.HairColorOverride.ToString(),
                Adjust = dir => { _player.HairColorOverride = CycleInt(_player.HairColorOverride, -1, 5, dir); AfterChange(); }
            });

            // Expression: auto + 0=Smile 1=Neutral 2=Sad 3=Wow
            string[] exprKeys = { "editor.expr.smile", "editor.expr.neutral", "editor.expr.sad", "editor.expr.wow" };
            _rows.Add(new Row
            {
                Label = () => loc.Get("editor.expression"),
                Value = () => _player.ExpressionOverride < 0 || _player.ExpressionOverride >= exprKeys.Length
                    ? loc.Get("editor.auto") : loc.Get(exprKeys[_player.ExpressionOverride]),
                Adjust = dir => { _player.ExpressionOverride = CycleInt(_player.ExpressionOverride, -1, 3, dir); AfterChange(); }
            });

            // Feature: auto + 0=None 1=Beard 2=Goatee 3=Sideburns 4=Eyelashes
            string[] featureKeys = { "editor.feature.none", "editor.feature.beard", "editor.feature.goatee",
                "editor.feature.sideburns", "editor.feature.eyelashes" };
            _rows.Add(new Row
            {
                Label = () => loc.Get("editor.feature"),
                Value = () => _player.FeatureOverride < 0 || _player.FeatureOverride >= featureKeys.Length
                    ? loc.Get("editor.auto") : loc.Get(featureKeys[_player.FeatureOverride]),
                Adjust = dir => { _player.FeatureOverride = CycleInt(_player.FeatureOverride, -1, 4, dir); AfterChange(); }
            });
        }

        private void BuildKitRows()
        {
            var loc = Localization.Instance;

            void ColorRow(string labelKey, Func<Team, int> get, Action<Team, int> set)
            {
                _rows.Add(new Row
                {
                    Label = () => loc.Get(labelKey),
                    Value = () => get(_team) == 0 ? loc.Get("editor.auto") : $"#{get(_team):X6}",
                    Swatch = () => get(_team) == 0 ? (Color?)null : ColorFromPacked(get(_team)),
                    Adjust = dir => { set(_team, CycleColor(get(_team), dir)); AfterChange(); }
                });
            }

            ColorRow("editor.shirtColor", t => t.ShirtColor, (t, v) => t.ShirtColor = v);
            ColorRow("editor.shortsColor", t => t.ShortsColor, (t, v) => t.ShortsColor = v);
            ColorRow("editor.socksColor", t => t.SocksColor, (t, v) => t.SocksColor = v);
            ColorRow("editor.gkShirtColor", t => t.GkShirtColor, (t, v) => t.GkShirtColor = v);
            ColorRow("editor.gkShortsColor", t => t.GkShortsColor, (t, v) => t.GkShortsColor = v);
            ColorRow("editor.gkSocksColor", t => t.GkSocksColor, (t, v) => t.GkSocksColor = v);

            // Pattern: 0=Solid 1=StripesV 2=Hoops 3=Halves 4=Sash
            string[] patternKeys = { "editor.pattern.solid", "editor.pattern.stripes", "editor.pattern.hoops",
                "editor.pattern.halves", "editor.pattern.sash" };
            _rows.Add(new Row
            {
                Label = () => loc.Get("editor.pattern"),
                Value = () => _team.ShirtPattern >= 0 && _team.ShirtPattern < patternKeys.Length
                    ? loc.Get(patternKeys[_team.ShirtPattern]) : loc.Get(patternKeys[0]),
                Adjust = dir => { _team.ShirtPattern = CycleInt(_team.ShirtPattern, 0, 4, dir); AfterChange(); }
            });

            ColorRow("editor.patternColor", t => t.PatternColor, (t, v) => t.PatternColor = v);

            _rows.Add(new Row { Label = () => loc.Get("editor.paint"), Confirm = EnterPaint });

            _rows.Add(new Row
            {
                Label = () => loc.Get("editor.resetKit"),
                Confirm = () =>
                {
                    _team.ShirtColor = 0;
                    _team.ShortsColor = 0;
                    _team.SocksColor = 0;
                    _team.GkShirtColor = 0;
                    _team.GkShortsColor = 0;
                    _team.GkSocksColor = 0;
                    _team.ShirtPattern = 0;
                    _team.PatternColor = 0;
                    _team.ShirtPaint = null;
                    AfterChange();
                }
            });

            _rows.Add(new Row
            {
                Label = () => loc.Get("editor.export"),
                Confirm = () =>
                {
                    Persist();
                    _status = $"{loc.Get("editor.exportDone")} {_overlayPath}";
                    _statusTimer = 6f;
                }
            });
        }

        #endregion

        #region Persistence

        /// <summary>
        /// Writes the whole catalog to the overlay JSON, then mirrors the edited
        /// team's kit fields + its players' stats/appearance onto the DB team with
        /// the same name (case-insensitive), i.e. the ACTIVE championship's copy.
        /// </summary>
        private void Persist()
        {
            try
            {
                TeamSeeder.SaveCatalog(_catalog, _overlayPath);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayerEditorScreen: overlay save failed: {ex.Message}");
                _status = ex.Message;
                _statusTimer = 5f;
                return;
            }

            if (_team == null || _dbTeams == null) return;

            var dbTeam = _dbTeams.FirstOrDefault(t =>
                string.Equals(t.Name, _team.Name, StringComparison.OrdinalIgnoreCase));
            if (dbTeam == null) return;

            try
            {
                dbTeam.ShirtColor = _team.ShirtColor;
                dbTeam.ShortsColor = _team.ShortsColor;
                dbTeam.SocksColor = _team.SocksColor;
                dbTeam.GkShirtColor = _team.GkShirtColor;
                dbTeam.GkShortsColor = _team.GkShortsColor;
                dbTeam.GkSocksColor = _team.GkSocksColor;
                dbTeam.ShirtPattern = _team.ShirtPattern;
                dbTeam.PatternColor = _team.PatternColor;
                dbTeam.ShirtPaint = _team.ShirtPaint;
                _database.SaveTeam(dbTeam);

                foreach (var catPlayer in _team.Players)
                {
                    var dbPlayer = dbTeam.Players.FirstOrDefault(p =>
                        string.Equals(p.Name, catPlayer.Name, StringComparison.OrdinalIgnoreCase) &&
                        p.ShirtNumber == catPlayer.ShirtNumber);
                    if (dbPlayer == null) continue;

                    dbPlayer.Speed = catPlayer.Speed;
                    dbPlayer.Shooting = catPlayer.Shooting;
                    dbPlayer.Passing = catPlayer.Passing;
                    dbPlayer.Defending = catPlayer.Defending;
                    dbPlayer.Agility = catPlayer.Agility;
                    dbPlayer.Technique = catPlayer.Technique;
                    dbPlayer.GenderOverride = catPlayer.GenderOverride;
                    dbPlayer.SkinToneOverride = catPlayer.SkinToneOverride;
                    dbPlayer.HairColorOverride = catPlayer.HairColorOverride;
                    dbPlayer.ExpressionOverride = catPlayer.ExpressionOverride;
                    dbPlayer.FeatureOverride = catPlayer.FeatureOverride;
                    _database.SavePlayer(dbPlayer);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayerEditorScreen: DB write-through failed: {ex.Message}");
                _status = ex.Message;
                _statusTimer = 5f;
            }
        }

        private void AfterChange()
        {
            Persist();
            RegeneratePortrait();
        }

        #endregion

        #region Portrait preview

        private void RegeneratePortrait()
        {
            _portrait?.Dispose();
            _portrait = null;

            if (_player == null || _team == null || GraphicsDevice == null) return;

            try
            {
                var model = ModelCache.TryGet(GraphicsDevice,
                    FaceComposer.IsFemalePlayer(_player) ? "PlayerF.glb" : "Player.glb");
                if (model == null) return;

                var composed = FaceComposer.Compose(GraphicsDevice, model.Parts[0].Texture,
                    FaceComposer.AppearanceFor(_player));
                var parts = KitBake.BakePartTextures(GraphicsDevice, model, composed, _team, _player, _team.Id);
                _portrait = PortraitRenderer.RenderPlayerPortrait(GraphicsDevice, model, parts, PortraitSize);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"PlayerEditorScreen: portrait failed: {ex.Message}");
            }
        }

        #endregion

        #region Paint page

        private void EnterPaint()
        {
            EnsurePaint();
            _paintCursorX = 0;
            _paintCursorY = 0;
            GoTo(EditorPage.Paint);
        }

        /// <summary>ShirtPaint is exactly 32x32 cells of 4-bit palette indices (1024 hex chars; '0' = empty).</summary>
        private void EnsurePaint()
        {
            if (string.IsNullOrEmpty(_team.ShirtPaint) || _team.ShirtPaint.Length < PaintGridSize * PaintGridSize)
                _team.ShirtPaint = new string('0', PaintGridSize * PaintGridSize);
        }

        private int GetPaintCell(int x, int y)
        {
            if (string.IsNullOrEmpty(_team.ShirtPaint)) return 0;
            char c = _team.ShirtPaint[y * PaintGridSize + x];
            return c >= '0' && c <= '9' ? c - '0' :
                   c >= 'a' && c <= 'f' ? c - 'a' + 10 :
                   c >= 'A' && c <= 'F' ? c - 'A' + 10 : 0;
        }

        private void SetPaintCell(int x, int y, int paletteIndex)
        {
            EnsurePaint();
            var chars = _team.ShirtPaint.ToCharArray();
            chars[y * PaintGridSize + x] = paletteIndex <= 0 ? '0' : paletteIndex.ToString("X")[0];
            _team.ShirtPaint = new string(chars);
        }

        private void UpdatePaint(KeyboardState keyState)
        {
            bool JustPressed(Keys k) => keyState.IsKeyDown(k) && !_previousKeyState.IsKeyDown(k);

            if (JustPressed(Keys.Left)) _paintCursorX = Math.Max(0, _paintCursorX - 1);
            if (JustPressed(Keys.Right)) _paintCursorX = Math.Min(PaintGridSize - 1, _paintCursorX + 1);
            if (JustPressed(Keys.Up)) _paintCursorY = Math.Max(0, _paintCursorY - 1);
            if (JustPressed(Keys.Down)) _paintCursorY = Math.Min(PaintGridSize - 1, _paintCursorY + 1);

            // X paints with the selected color
            if (JustPressed(Keys.X))
                SetPaintCell(_paintCursorX, _paintCursorY, _paintColor);

            // C cycles the selected palette color (1..15 - the hex-encodable range)
            if (JustPressed(Keys.C))
                _paintColor = _paintColor % 15 + 1;

            // Z or Delete erases the cell
            if (JustPressed(Keys.Z) || JustPressed(Keys.Delete))
                SetPaintCell(_paintCursorX, _paintCursorY, 0);

            if (_input.IsBackPressed())
                GoBack(); // persists the paint and returns to the kit page
        }

        #endregion

        #region Update

        public override void Update(GameTime gameTime)
        {
            _input.Update();
            var keyState = DebugInput.GetState();
            var touchUI = Gameplay.TouchUI.Instance;

            if (_statusTimer > 0)
                _statusTimer -= (float)gameTime.ElapsedGameTime.TotalSeconds;
            if (_joystickMenuCooldown > 0)
                _joystickMenuCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            if (_page == EditorPage.Paint)
            {
                UpdatePaint(keyState);
                _previousKeyState = keyState;
                return;
            }

            if (_rows.Count == 0)
            {
                if (_input.IsBackPressed() || touchUI.IsBackJustPressed)
                    GoBack();
                _previousKeyState = keyState;
                return;
            }

            Vector2 joystickDir = touchUI.JoystickDirection;
            bool menuDown = (keyState.IsKeyDown(Keys.Down) && !_previousKeyState.IsKeyDown(Keys.Down)) ||
                            _input.IsMenuDownPressed() ||
                            (touchUI.Enabled && joystickDir.Y > 0.3f && _joystickMenuCooldown <= 0);
            bool menuUp = (keyState.IsKeyDown(Keys.Up) && !_previousKeyState.IsKeyDown(Keys.Up)) ||
                          _input.IsMenuUpPressed() ||
                          (touchUI.Enabled && joystickDir.Y < -0.3f && _joystickMenuCooldown <= 0);
            bool menuLeft = (keyState.IsKeyDown(Keys.Left) && !_previousKeyState.IsKeyDown(Keys.Left)) ||
                            (touchUI.Enabled && joystickDir.X < -0.3f && _joystickMenuCooldown <= 0);
            bool menuRight = (keyState.IsKeyDown(Keys.Right) && !_previousKeyState.IsKeyDown(Keys.Right)) ||
                             (touchUI.Enabled && joystickDir.X > 0.3f && _joystickMenuCooldown <= 0);

            if (menuDown)
            {
                _selected = (_selected + 1) % _rows.Count;
                if (_selected - _scrollOffset >= MaxVisibleRows)
                    _scrollOffset++;
                _joystickMenuCooldown = 0.15f;
            }
            else if (menuUp)
            {
                _selected = (_selected - 1 + _rows.Count) % _rows.Count;
                if (_selected < _scrollOffset)
                    _scrollOffset--;
                _joystickMenuCooldown = 0.15f;
            }
            else if (menuLeft && _rows[_selected].Adjust != null)
            {
                _rows[_selected].Adjust(-1);
                _joystickMenuCooldown = 0.15f;
            }
            else if (menuRight && _rows[_selected].Adjust != null)
            {
                _rows[_selected].Adjust(1);
                _joystickMenuCooldown = 0.15f;
            }
            else if ((keyState.IsKeyDown(Keys.Enter) && !_previousKeyState.IsKeyDown(Keys.Enter)) ||
                     _input.IsConfirmPressed() || touchUI.IsActionJustPressed)
            {
                _rows[_selected].Confirm?.Invoke();
            }
            else if (keyState.IsKeyDown(Keys.PageDown) && !_previousKeyState.IsKeyDown(Keys.PageDown))
            {
                _selected = Math.Min(_selected + MaxVisibleRows, _rows.Count - 1);
                _scrollOffset = Math.Max(0, _selected - MaxVisibleRows + 1);
            }
            else if (keyState.IsKeyDown(Keys.PageUp) && !_previousKeyState.IsKeyDown(Keys.PageUp))
            {
                _selected = Math.Max(_selected - MaxVisibleRows, 0);
                _scrollOffset = Math.Max(0, _selected);
            }

            if (_input.IsBackPressed() || touchUI.IsBackJustPressed)
                GoBack();

            _previousKeyState = keyState;
        }

        #endregion

        #region Draw

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            var loc = Localization.Instance;
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;

            // Dim backdrop so the editor reads over whatever is behind it
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), new Color(20, 30, 24, 235));

            if (_page == EditorPage.Paint)
            {
                DrawPaint(spriteBatch, font, screenWidth, screenHeight);
                return;
            }

            // Title
            string title = PageTitle();
            var titleSize = font.MeasureString(title);
            spriteBatch.DrawString(font, title,
                new Vector2((screenWidth - titleSize.X) / 2, 30), Color.Yellow);

            // Rows
            int endIndex = Math.Min(_scrollOffset + MaxVisibleRows, _rows.Count);
            float labelX = screenWidth / 2f - 320;
            float valueX = screenWidth / 2f + 60;

            if (_scrollOffset > 0)
            {
                var upSize = font.MeasureString("▲");
                spriteBatch.DrawString(font, "▲", new Vector2((screenWidth - upSize.X) / 2, 62), Color.Gray);
            }

            for (int i = _scrollOffset; i < endIndex; i++)
            {
                float y = 80 + (i - _scrollOffset) * RowHeight;
                var color = i == _selected ? Color.Yellow : Color.White;
                var row = _rows[i];

                spriteBatch.DrawString(font, row.Label(), new Vector2(labelX, y), color);

                string value = row.Value?.Invoke();
                if (!string.IsNullOrEmpty(value))
                    spriteBatch.DrawString(font, value, new Vector2(valueX, y), color);

                var swatch = row.Swatch?.Invoke();
                if (swatch.HasValue)
                {
                    var rect = new Rectangle((int)valueX + 120, (int)y + 4, 40, 20);
                    spriteBatch.Draw(_pixel, rect, Color.Black);
                    spriteBatch.Draw(_pixel, new Rectangle(rect.X + 1, rect.Y + 1, rect.Width - 2, rect.Height - 2), swatch.Value);
                }
            }

            if (endIndex < _rows.Count)
            {
                var downSize = font.MeasureString("▼");
                spriteBatch.DrawString(font, "▼",
                    new Vector2((screenWidth - downSize.X) / 2, 80 + MaxVisibleRows * RowHeight), Color.Gray);
            }

            // Live portrait preview on the player/kit pages
            if (_portrait != null && (_page == EditorPage.Player || _page == EditorPage.Kit))
            {
                int px = screenWidth - PortraitSize - 40;
                int py = 100;
                spriteBatch.Draw(_pixel, new Rectangle(px - 2, py - 2, PortraitSize + 4, PortraitSize + 4), Color.Yellow);
                spriteBatch.Draw(_portrait, new Rectangle(px, py, PortraitSize, PortraitSize), Color.White);
            }

            // Status message (export confirmation / errors)
            if (_statusTimer > 0 && !string.IsNullOrEmpty(_status))
            {
                var statusSize = font.MeasureString(_status);
                spriteBatch.DrawString(font, _status,
                    new Vector2((screenWidth - statusSize.X) / 2, screenHeight - 90), Color.LightGreen);
            }

            // Instructions
            string instructions = loc.Get("editor.instructions");
            var instrSize = font.MeasureString(instructions);
            spriteBatch.DrawString(font, instructions,
                new Vector2((screenWidth - instrSize.X) / 2, screenHeight - 50), Color.Gray);
        }

        private string PageTitle()
        {
            var loc = Localization.Instance;
            return _page switch
            {
                EditorPage.Championships => loc.Get("editor.title"),
                EditorPage.Teams => _championship?.Name ?? loc.Get("editor.title"),
                EditorPage.Team => _team?.Name ?? "",
                EditorPage.Player => $"{_team?.Name} - {_player?.Name}",
                EditorPage.Kit => $"{_team?.Name} - {loc.Get("editor.kit")}",
                _ => loc.Get("editor.title")
            };
        }

        private void DrawPaint(SpriteBatch spriteBatch, SpriteFont font, int screenWidth, int screenHeight)
        {
            var loc = Localization.Instance;

            string title = $"{_team?.Name} - {loc.Get("editor.paint")}";
            var titleSize = font.MeasureString(title);
            spriteBatch.DrawString(font, title, new Vector2((screenWidth - titleSize.X) / 2, 20), Color.Yellow);

            int gridPx = PaintGridSize * PaintCellPx;
            int originX = (screenWidth - gridPx) / 2;
            int originY = 60;

            // Approximate shirt background: current shirt color (gray when unset)
            Color shirtBg = _team.ShirtColor != 0 ? ColorFromPacked(_team.ShirtColor) : new Color(120, 120, 130);
            spriteBatch.Draw(_pixel, new Rectangle(originX - 2, originY - 2, gridPx + 4, gridPx + 4), Color.Black);

            for (int y = 0; y < PaintGridSize; y++)
            {
                for (int x = 0; x < PaintGridSize; x++)
                {
                    int v = GetPaintCell(x, y);
                    Color cell = v > 0 && v <= KitTextureFactory.PaintPalette.Length
                        ? KitTextureFactory.PaintPalette[v - 1]
                        : shirtBg;
                    spriteBatch.Draw(_pixel,
                        new Rectangle(originX + x * PaintCellPx, originY + y * PaintCellPx, PaintCellPx - 1, PaintCellPx - 1),
                        cell);
                }
            }

            // Cursor highlight
            var cursor = new Rectangle(originX + _paintCursorX * PaintCellPx - 1,
                originY + _paintCursorY * PaintCellPx - 1, PaintCellPx + 1, PaintCellPx + 1);
            spriteBatch.Draw(_pixel, new Rectangle(cursor.X, cursor.Y, cursor.Width, 2), Color.Yellow);
            spriteBatch.Draw(_pixel, new Rectangle(cursor.X, cursor.Bottom - 2, cursor.Width, 2), Color.Yellow);
            spriteBatch.Draw(_pixel, new Rectangle(cursor.X, cursor.Y, 2, cursor.Height), Color.Yellow);
            spriteBatch.Draw(_pixel, new Rectangle(cursor.Right - 2, cursor.Y, 2, cursor.Height), Color.Yellow);

            // Selected color swatch
            int swatchY = originY + gridPx + 16;
            spriteBatch.DrawString(font, "C:", new Vector2(originX, swatchY), Color.White);
            var swatchRect = new Rectangle(originX + 36, swatchY - 2, 60, 24);
            spriteBatch.Draw(_pixel, swatchRect, Color.Black);
            spriteBatch.Draw(_pixel,
                new Rectangle(swatchRect.X + 1, swatchRect.Y + 1, swatchRect.Width - 2, swatchRect.Height - 2),
                KitTextureFactory.PaintPalette[_paintColor - 1]);

            string instructions = loc.Get("editor.paintInstructions");
            var instrSize = font.MeasureString(instructions);
            spriteBatch.DrawString(font, instructions,
                new Vector2((screenWidth - instrSize.X) / 2, screenHeight - 50), Color.Gray);
        }

        #endregion

        #region Helpers

        private static int CycleInt(int current, int min, int max, int dir)
        {
            int range = max - min + 1;
            return min + (((current - min + dir) % range) + range) % range;
        }

        /// <summary>Cycles 0 (unset) + the 16 PaintPalette colors as packed RGB ints.</summary>
        private static int CycleColor(int current, int dir)
        {
            var candidates = new int[KitTextureFactory.PaintPalette.Length + 1];
            candidates[0] = 0;
            for (int i = 0; i < KitTextureFactory.PaintPalette.Length; i++)
                candidates[i + 1] = PackColor(KitTextureFactory.PaintPalette[i]);

            int idx = Array.IndexOf(candidates, current);
            if (idx < 0) idx = 0;
            return candidates[(idx + dir + candidates.Length) % candidates.Length];
        }

        private static int PackColor(Color c) => (c.R << 16) | (c.G << 8) | c.B;

        private static Color ColorFromPacked(int packed) =>
            new Color((packed >> 16) & 0xFF, (packed >> 8) & 0xFF, packed & 0xFF);

        private static string PositionAbbrev(PlayerPosition position)
        {
            var loc = Localization.Instance;
            return position switch
            {
                PlayerPosition.Goalkeeper => loc.Get("lineup.position.gk"),
                PlayerPosition.Defender => loc.Get("lineup.position.def"),
                PlayerPosition.Midfielder => loc.Get("lineup.position.mid"),
                PlayerPosition.Forward => loc.Get("lineup.position.fwd"),
                _ => ""
            };
        }

        #endregion
    }
}
