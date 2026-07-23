using NoPasaranFC.Debugging;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Database;
using NoPasaranFC.Graphics3D;
using NoPasaranFC.Graphics3D.Skinning;
using NoPasaranFC.Models;

namespace NoPasaranFC.Screens
{
    public class LineupScreen : Screen
    {
        private readonly Team _team;
        private readonly Match _match;
        private readonly Championship _championship;
        private readonly DatabaseManager _database;
        private readonly ScreenManager _screenManager;
        private readonly ContentManager _contentManager;

        private List<Player> _allPlayers;
        private int _selectedIndex = 0;
        private int _scrollOffset = 0;
        // Player rows are 52px tall plus 26px section headers; 8 rows + worst-case
        // headers always fit the left panel at 720p. Navigation/scroll windows by this.
        private const int MaxVisiblePlayers = 8;

        private Gameplay.InputHelper _input;
        private Texture2D _pixel;

        // 3D portraits: shared models + per-player render cache.
        // A null cache entry means rendering failed -> generated fallback avatar.
        private SkinnedModel _playerModel;
        private SkinnedModel _playerModelF;
        private readonly Dictionary<Player, Texture2D> _portraitCache = new Dictionary<Player, Texture2D>();
        private Texture2D _circleTexture;
        private Color _accentColor = Color.White;
        private bool _accentComputed;

        public LineupScreen(Team team, Match match, Championship championship, DatabaseManager database,
            ScreenManager screenManager, ContentManager content, GraphicsDevice graphicsDevice)
            : base(content, graphicsDevice)
        {
            _team = team;
            _match = match;
            _championship = championship;
            _database = database;
            _screenManager = screenManager;
            _contentManager = content;

            // Roster grouped by position (GK/DEF/MID/FWD), shirt number within a group
            _allPlayers = _team.Players
                .OrderBy(p => GetPositionOrder(p.Position))
                .ThenBy(p => p.ShirtNumber)
                .ToList();
            foreach (var p in _allPlayers)
                p.Team ??= _team; // kit lookup reads player.Team?.KitName

            // Initialize input helper
            _input = new Gameplay.InputHelper();

            CreatePixelTexture();
            TryLoadPlayerModels();
            PreRenderPortraits();
        }

        public override void OnActivated()
        {
            // Reset input state when screen becomes active to prevent key bleed-through
            _input = new Gameplay.InputHelper();
        }

        private void CreatePixelTexture()
        {
            _pixel = new Texture2D(GraphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
        }
        private float _joystickMenuCooldown = 0f;

        #region Model loading & portraits

        /// <summary>Loads the rigged player models (same paths/pattern as MatchRenderer3D).</summary>
        private void TryLoadPlayerModels()
        {
            try
            {
#if ANDROID
                var context = global::Android.App.Application.Context;
                using (var stream = context.Assets.Open("Content/Models3D/Player.glb"))
                    _playerModel = SkinnedModel.Load(GraphicsDevice, stream);
#else
                string glbPath = PlatformHelper.GetAssetPath(Path.Combine("Content", "Models3D", "Player.glb"));
                if (File.Exists(glbPath))
                    _playerModel = SkinnedModel.Load(GraphicsDevice, glbPath);
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LineupScreen: failed to load Player.glb ({ex.Message}).");
                _playerModel = null;
            }

            try
            {
#if ANDROID
                var context = global::Android.App.Application.Context;
                using (var stream = context.Assets.Open("Content/Models3D/PlayerF.glb"))
                    _playerModelF = SkinnedModel.Load(GraphicsDevice, stream);
#else
                string glbPathF = PlatformHelper.GetAssetPath(Path.Combine("Content", "Models3D", "PlayerF.glb"));
                if (File.Exists(glbPathF))
                    _playerModelF = SkinnedModel.Load(GraphicsDevice, glbPathF);
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LineupScreen: failed to load PlayerF.glb ({ex.Message}).");
                _playerModelF = null;
            }
        }

        /// <summary>Deterministic per-player model choice, same rule as MatchRenderer3D
        /// (~1 in 4 female when available) so portraits match the on-pitch look.</summary>
        private SkinnedModel GetModelForPlayer(Player player)
        {
            if (_playerModelF != null && player.Name != null && (player.Name.GetHashCode() & 3) == 0)
                return _playerModelF;
            return _playerModel;
        }

        /// <summary>Renders every roster portrait once up front so scrolling never hitches.</summary>
        private void PreRenderPortraits()
        {
            foreach (var player in _allPlayers)
                GetPortrait(player);
        }

        /// <summary>Cached 3D portrait; null when models/rendering are unavailable.</summary>
        private Texture2D GetPortrait(Player player)
        {
            if (_portraitCache.TryGetValue(player, out var cached))
                return cached;
            if (_playerModel == null)
                return null;

            try
            {
                var model = GetModelForPlayer(player);
                var overrides = BuildKitOverrides(player, model);
                var portrait = PortraitRenderer.RenderPlayerPortrait(GraphicsDevice, model, overrides, 128);
                _portraitCache[player] = portrait;
                return portrait;
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"LineupScreen: portrait failed for {player.Name} ({ex.Message}).");
                _portraitCache[player] = null; // don't retry every frame
                return null;
            }
        }

        /// <summary>Kit-colored per-part textures (numbered shirt / shorts / socks),
        /// mirroring MatchRenderer3D.ApplyKit for the soccer player atlas layout.</summary>
        private Dictionary<string, Texture2D> BuildKitOverrides(Player player, SkinnedModel model)
        {
            MatchRenderer3D.GetKitColors(player, _match.HomeTeamId, out Color shirt, out Color shorts, out Color socks);
            Texture2D baseTexture = model.Parts[0].Texture;

            // player_atlas layout (512x512): quadrants shirt / shorts / socks / skin+extras
            Texture2D shirtTexture = KitTextureFactory.GetKitTexture(GraphicsDevice, baseTexture, shirt,
                new Rectangle(0, 0, 256, 256));
            Texture2D shortsTexture = KitTextureFactory.GetKitTexture(GraphicsDevice, baseTexture, shorts,
                new Rectangle(256, 0, 256, 256));
            Texture2D socksTexture = KitTextureFactory.GetKitTexture(GraphicsDevice, baseTexture, socks,
                new Rectangle(0, 256, 256, 256));
            Texture2D numberedShirt = KitTextureFactory.GetNumberedShirtTexture(GraphicsDevice, shirtTexture,
                player.ShirtNumber, KitTextureFactory.ContrastFor(shirt));

            var overrides = new Dictionary<string, Texture2D>();
            foreach (var part in model.Parts)
            {
                string name = part.Name ?? "";
                if (name == "Soccer_Shirt")
                    overrides[part.Name] = numberedShirt;
                else if (name == "Soccer_Shorts")
                    overrides[part.Name] = shortsTexture;
                else if (name.StartsWith("Soccer_Sock"))
                    overrides[part.Name] = socksTexture;
            }
            return overrides;
        }

        /// <summary>Team-color accent: the outfield shirt color of the player team.</summary>
        private Color GetAccentColor()
        {
            if (!_accentComputed)
            {
                var outfield = _allPlayers.FirstOrDefault(p => p.Position != PlayerPosition.Goalkeeper)
                    ?? _allPlayers.FirstOrDefault();
                if (outfield != null)
                {
                    MatchRenderer3D.GetKitColors(outfield, _match.HomeTeamId, out Color shirt, out _, out _);
                    _accentColor = shirt;
                }
                _accentComputed = true;
            }
            return _accentColor;
        }

        private Color GetShirtColor(Player player)
        {
            MatchRenderer3D.GetKitColors(player, _match.HomeTeamId, out Color shirt, out _, out _);
            return shirt;
        }

        /// <summary>White disc texture, tinted per use (portrait backdrops, fallback avatars).</summary>
        private Texture2D GetCircleTexture()
        {
            if (_circleTexture != null)
                return _circleTexture;

            const int size = 64;
            _circleTexture = new Texture2D(GraphicsDevice, size, size);
            var data = new Color[size * size];
            float radius = size / 2f - 1f;
            var center = new Vector2(size / 2f, size / 2f);
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float d = Vector2.Distance(new Vector2(x + 0.5f, y + 0.5f), center);
                    data[y * size + x] = d <= radius ? Color.White : Color.Transparent;
                }
            }
            _circleTexture.SetData(data);
            return _circleTexture;
        }

        /// <summary>Draws the 3D portrait over a dark disc, or a generated fallback
        /// (kit-colored disc + shirt number) when no portrait is available.</summary>
        private void DrawPortraitOrFallback(SpriteBatch spriteBatch, SpriteFont font, Player player, Rectangle dest)
        {
            spriteBatch.Draw(GetCircleTexture(),
                new Rectangle(dest.X - 2, dest.Y - 2, dest.Width + 4, dest.Height + 4),
                new Color(15, 40, 20, 220));

            Texture2D portrait = GetPortrait(player);
            if (portrait != null)
            {
                spriteBatch.Draw(portrait, dest, Color.White);
                return;
            }

            Color shirt = GetShirtColor(player);
            spriteBatch.Draw(GetCircleTexture(), dest, shirt);
            string numText = player.ShirtNumber.ToString();
            Vector2 numSize = font.MeasureString(numText) * 0.7f;
            spriteBatch.DrawString(font, numText,
                new Vector2(dest.Center.X - numSize.X / 2, dest.Center.Y - numSize.Y / 2),
                KitTextureFactory.ContrastFor(shirt), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        }

        #endregion

        public override void Update(GameTime gameTime)
        {
            _input.Update();
            var touchUI = Gameplay.TouchUI.Instance;

            // Touch/Joystick navigation with cooldown (threshold 0.3 for responsiveness)
            Vector2 joystickDir = touchUI.JoystickDirection;
            bool menuDown = _input.IsMenuDownPressed() || (touchUI.Enabled && joystickDir.Y > 0.3f && _joystickMenuCooldown <= 0);
            bool menuUp = _input.IsMenuUpPressed() || (touchUI.Enabled && joystickDir.Y < -0.3f && _joystickMenuCooldown <= 0);

            // Update cooldown
            if (_joystickMenuCooldown > 0)
                _joystickMenuCooldown -= (float)gameTime.ElapsedGameTime.TotalSeconds;

            // Navigation
            if (menuDown)
            {
                _selectedIndex = (_selectedIndex + 1) % _allPlayers.Count;

                // Auto-scroll
                if (_selectedIndex - _scrollOffset >= MaxVisiblePlayers-1)
                {
                    _scrollOffset++;
                }

                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                _joystickMenuCooldown = 0.15f;
            }
            else if (menuUp)
            {
                _selectedIndex = (_selectedIndex - 1 + _allPlayers.Count) % _allPlayers.Count;

                // Auto-scroll
                if (_selectedIndex < _scrollOffset)
                {
                    _scrollOffset--;
                }

                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
                _joystickMenuCooldown = 0.15f;
            }

            // PageUp/PageDown still use raw keyboard (TODO: add to InputHelper)
            var keyState = DebugInput.GetState();

            if (keyState.IsKeyDown(Keys.PageDown))
            {
                _selectedIndex = Math.Min(_allPlayers.Count - 1, _selectedIndex + 15);
                _scrollOffset = Math.Max(0, Math.Min(_scrollOffset + 15, _allPlayers.Count - MaxVisiblePlayers));
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
            }
            else if (keyState.IsKeyDown(Keys.PageUp))
            {
                _selectedIndex = Math.Max(0, _selectedIndex - 15);
                _scrollOffset = Math.Max(0, _scrollOffset - 15);
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_move");
            }

            // Toggle starting status (Space, X button, or touch X)
            if (_input.IsSwitchPlayerPressed() || touchUI.IsSwitchJustPressed)
            {
                var player = _allPlayers[_selectedIndex];
                int currentStartingCount = _allPlayers.Count(p => p.IsStarting);

                if (player.IsStarting)
                {
                    // Always allow removal (user might be swapping players)
                    player.IsStarting = false;
                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_select");
                }
                else
                {
                    // Can add to starting lineup only if less than 11
                    if (currentStartingCount < 11)
                    {
                        player.IsStarting = true;
                        Gameplay.AudioManager.Instance.PlaySoundEffect("menu_select");
                    }
                }
            }

            // Confirm lineup and start match (Enter, A button, or touch A)
            if (_input.IsConfirmPressed() || touchUI.IsActionJustPressed)
            {
                int startingCount = _allPlayers.Count(p => p.IsStarting);

                if (startingCount == 11)
                {
                    // Save lineup to database
                    foreach (var player in _allPlayers)
                    {
                        _database.SavePlayer(player);
                    }

                    Gameplay.AudioManager.Instance.PlaySoundEffect("menu_select");

                    // Start the match
                    var homeTeam = _championship.Teams.Find(t => t.Id == _match.HomeTeamId);
                    var awayTeam = _championship.Teams.Find(t => t.Id == _match.AwayTeamId);

                    var matchScreen = new MatchScreen(homeTeam, awayTeam, _match, _championship,
                        _database, _screenManager, _contentManager);
                    matchScreen.SetGraphicsDevice(GraphicsDevice);
                    _screenManager.PushScreen(matchScreen);
                }
            }

            // Cancel (Escape, B button, or touch B)
            if (_input.IsBackPressed() || touchUI.IsBackJustPressed)
            {
                Gameplay.AudioManager.Instance.PlaySoundEffect("menu_back");
                IsFinished = true;
            }
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            int screenWidth = Game1.ScreenWidth;
            int screenHeight = Game1.ScreenHeight;

            // Draw background
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, screenWidth, screenHeight), new Color(20, 40, 20));

            // Title
            string title = $"{Localization.Instance.Get("lineup.title")} - {_team.Name}";
            Vector2 titleSize = font.MeasureString(title);
            spriteBatch.DrawString(font, title, new Vector2((screenWidth - titleSize.X) / 2, 20), Color.Yellow);

            // Roster list (left), formation preview (right)
            DrawPlayerList(spriteBatch, font, screenHeight);
            DrawFormationPreview(spriteBatch, font, screenWidth, screenHeight);

            // Draw instructions
            DrawInstructions(spriteBatch, font, screenHeight);
        }

        #region Left panel: roster list

        private void DrawPlayerList(SpriteBatch spriteBatch, SpriteFont font, int screenHeight)
        {
            int panelX = 16;
            int panelY = 84;
            int panelWidth = 600;
            int panelBottom = screenHeight - 44;
            const int rowHeight = 52;
            const int headerHeight = 26;

            // Panel background
            spriteBatch.Draw(_pixel,
                new Rectangle(panelX - 6, panelY - 6, panelWidth + 12, panelBottom - panelY + 12),
                new Color(25, 42, 25, 180));

            if (_scrollOffset > 0)
            {
                spriteBatch.DrawString(font, $"^ {Localization.Instance.Get("lineup.more")}",
                    new Vector2(panelX + panelWidth - 110, panelY - 2), Color.Gray,
                    0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            }

            Color accent = GetAccentColor();
            int y = panelY + 10;
            PlayerPosition? lastPosition = null;
            int i;
            for (i = _scrollOffset; i < _allPlayers.Count; i++)
            {
                var player = _allPlayers[i];

                // Section header when the position group changes. Headers are visual
                // only — navigation moves strictly between player rows.
                if (lastPosition != player.Position)
                {
                    if (y + headerHeight + rowHeight > panelBottom) break;
                    DrawSectionHeader(spriteBatch, font, player.Position, panelX, y, panelWidth, accent);
                    y += headerHeight;
                    lastPosition = player.Position;
                }

                if (y + rowHeight > panelBottom) break;
                DrawPlayerRow(spriteBatch, font, player, i, panelX, y, panelWidth, rowHeight, accent);
                y += rowHeight;
            }

            if (i < _allPlayers.Count)
            {
                spriteBatch.DrawString(font, $"v {Localization.Instance.Get("lineup.more")}",
                    new Vector2(panelX + panelWidth - 110, panelBottom - 16), Color.Gray,
                    0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            }
        }

        private void DrawSectionHeader(SpriteBatch spriteBatch, SpriteFont font, PlayerPosition position,
            int x, int y, int width, Color accent)
        {
            spriteBatch.DrawString(font, GetPositionGroupHeader(position), new Vector2(x + 8, y + 5),
                Lighten(accent), 0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            spriteBatch.Draw(_pixel, new Rectangle(x + 8, y + 22, width - 16, 1), new Color(90, 120, 90));
        }

        private void DrawPlayerRow(SpriteBatch spriteBatch, SpriteFont font, Player player, int index,
            int x, int y, int width, int height, Color accent)
        {
            bool selected = index == _selectedIndex;
            Color bgColor = selected ? new Color(85, 125, 85, 230)
                : player.IsStarting ? new Color(48, 78, 52, 200)
                : new Color(28, 44, 28, 150);
            spriteBatch.Draw(_pixel, new Rectangle(x, y, width, height - 2), bgColor);

            // Starters: team-color accent bar on the left edge
            if (player.IsStarting)
                spriteBatch.Draw(_pixel, new Rectangle(x, y, 4, height - 2), accent);
            if (selected)
                DrawRectOutline(spriteBatch, new Rectangle(x, y, width, height - 2), Color.Yellow, 1);

            // Portrait (or fallback avatar)
            DrawPortraitOrFallback(spriteBatch, font, player, new Rectangle(x + 12, y + 3, 44, 44));

            Color textColor = player.IsStarting ? Color.White : new Color(170, 170, 170);

            // Shirt number + name
            spriteBatch.DrawString(font, $"#{player.ShirtNumber}", new Vector2(x + 64, y + 7),
                selected ? Color.Yellow : Lighten(accent), 0f, Vector2.Zero, 0.75f, SpriteEffects.None, 0f);
            string name = player.Name.Length > 16 ? player.Name.Substring(0, 15) + "..." : player.Name;
            spriteBatch.DrawString(font, name, new Vector2(x + 110, y + 6),
                selected ? Color.Yellow : textColor, 0f, Vector2.Zero, 0.9f, SpriteEffects.None, 0f);

            // Position tag + starter/bench status
            spriteBatch.DrawString(font, GetPositionAbbreviation(player.Position), new Vector2(x + 64, y + 30),
                Color.LightGray, 0f, Vector2.Zero, 0.65f, SpriteEffects.None, 0f);
            string statusText = player.IsStarting
                ? Localization.Instance.Get("lineup.starter")
                : Localization.Instance.Get("lineup.benchPlayer");
            spriteBatch.DrawString(font, statusText, new Vector2(x + 110, y + 32),
                player.IsStarting ? Color.LightGreen : Color.Gray,
                0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

            // Mini stat bars (SPD / SHT / PAS / DEF)
            int statsX = x + 300;
            int statsY = y + 27;
            DrawMiniStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.spd"), player.Speed,
                statsX, statsY, new Color(220, 70, 70));
            DrawMiniStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.sht"), player.Shooting,
                statsX + 72, statsY, new Color(230, 150, 50));
            DrawMiniStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.pas"), player.Passing,
                statsX + 144, statsY, new Color(80, 150, 235));
            DrawMiniStatBar(spriteBatch, font, Localization.Instance.Get("lineup.stat.def"), player.Defending,
                statsX + 216, statsY, new Color(90, 205, 110));
        }

        private void DrawMiniStatBar(SpriteBatch spriteBatch, SpriteFont font, string label, int value,
            int x, int y, Color color)
        {
            const int barWidth = 26;
            spriteBatch.DrawString(font, label, new Vector2(x, y - 5), Color.LightGray,
                0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);

            int barX = x + 24;
            spriteBatch.Draw(_pixel, new Rectangle(barX, y + 1, barWidth, 6), new Color(15, 25, 15));
            int fillWidth = (int)(barWidth * Math.Clamp(value / 100f, 0f, 1f));
            spriteBatch.Draw(_pixel, new Rectangle(barX, y + 1, fillWidth, 6), color);

            spriteBatch.DrawString(font, value.ToString(), new Vector2(barX + barWidth + 4, y - 5),
                Color.White, 0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }

        #endregion

        #region Right panel: formation preview

        private void DrawFormationPreview(SpriteBatch spriteBatch, SpriteFont font, int screenWidth, int screenHeight)
        {
            int panelWidth = 604;
            int previewX = screenWidth - panelWidth - 16;
            // On Android shift the panel left to avoid the touch buttons
            previewX = PlatformHelper.IsAndroid ? previewX - 150 : previewX;
            int previewY = 84;
            int panelHeight = screenHeight - previewY - 48;

            // Title
            string previewTitle = Localization.Instance.Get("lineup.formationPreview");
            Vector2 titleSize = font.MeasureString(previewTitle);
            spriteBatch.DrawString(font, previewTitle,
                new Vector2(previewX + (panelWidth - titleSize.X) / 2, previewY - 30), Color.White);

            // Panel background
            spriteBatch.Draw(_pixel, new Rectangle(previewX, previewY, panelWidth, panelHeight),
                new Color(25, 42, 25, 180));

            // Pitch area (footer below it holds formation label + starter count)
            const int footerHeight = 64;
            var pitch = new Rectangle(previewX + 12, previewY + 12, panelWidth - 24, panelHeight - 24 - footerHeight);
            DrawPitch(spriteBatch, pitch);

            // Starting players on position lines (FWD top, GK bottom), evenly spaced per line
            var startingPlayers = _allPlayers.Where(p => p.IsStarting).ToList();
            var lines = new[]
            {
                startingPlayers.Where(p => p.Position == PlayerPosition.Forward).ToList(),
                startingPlayers.Where(p => p.Position == PlayerPosition.Midfielder).ToList(),
                startingPlayers.Where(p => p.Position == PlayerPosition.Defender).ToList(),
                startingPlayers.Where(p => p.Position == PlayerPosition.Goalkeeper).ToList(),
            };
            float[] rowY = { 0.14f, 0.38f, 0.62f, 0.86f };
            for (int line = 0; line < lines.Length; line++)
            {
                int cy = pitch.Y + (int)(pitch.Height * rowY[line]);
                for (int i = 0; i < lines[line].Count; i++)
                {
                    int cx = pitch.X + (int)(pitch.Width * (i + 1) / (float)(lines[line].Count + 1));
                    DrawFormationChip(spriteBatch, font, lines[line][i], cx, cy);
                }
            }

            // Formation label + starter count (existing validation colors)
            int defCount = lines[2].Count;
            int midCount = lines[1].Count;
            int fwdCount = lines[0].Count;
            string formationText = $"{defCount}-{midCount}-{fwdCount}";
            int startingCount = startingPlayers.Count;
            string countText = $"{Localization.Instance.Get("lineup.starting")}: {startingCount}/11";
            Color countColor = startingCount == 11 ? Color.LightGreen : (startingCount < 11 ? Color.Yellow : Color.Red);

            int footerY = pitch.Bottom + 8;
            Vector2 formSize = font.MeasureString(formationText);
            spriteBatch.DrawString(font, formationText,
                new Vector2(previewX + (panelWidth - formSize.X) / 2, footerY), Color.Yellow);
            Vector2 countSize = font.MeasureString(countText) * 0.8f;
            spriteBatch.DrawString(font, countText,
                new Vector2(previewX + (panelWidth - countSize.X) / 2, footerY + 30), countColor,
                0f, Vector2.Zero, 0.8f, SpriteEffects.None, 0f);
        }

        private void DrawPitch(SpriteBatch spriteBatch, Rectangle pitch)
        {
            // Striped grass (vertical pitch: attack up, own goal down)
            const int stripes = 8;
            for (int s = 0; s < stripes; s++)
            {
                int stripeY = pitch.Y + s * pitch.Height / stripes;
                int stripeHeight = pitch.Height / stripes + (s == stripes - 1 ? pitch.Height % stripes : 0);
                spriteBatch.Draw(_pixel, new Rectangle(pitch.X, stripeY, pitch.Width, stripeHeight),
                    s % 2 == 0 ? new Color(38, 105, 48) : new Color(32, 92, 42));
            }

            var lineColor = new Color(230, 230, 230, 180);
            DrawRectOutline(spriteBatch, pitch, lineColor, 2);
            spriteBatch.Draw(_pixel, new Rectangle(pitch.X, pitch.Y + pitch.Height / 2 - 1, pitch.Width, 2), lineColor);

            // Penalty boxes on both ends
            int boxWidth = pitch.Width / 3;
            int boxHeight = pitch.Height / 7;
            DrawRectOutline(spriteBatch,
                new Rectangle(pitch.X + (pitch.Width - boxWidth) / 2, pitch.Y, boxWidth, boxHeight), lineColor, 2);
            DrawRectOutline(spriteBatch,
                new Rectangle(pitch.X + (pitch.Width - boxWidth) / 2, pitch.Bottom - boxHeight, boxWidth, boxHeight), lineColor, 2);
        }

        private void DrawFormationChip(SpriteBatch spriteBatch, SpriteFont font, Player player, int centerX, int centerY)
        {
            const int chipSize = 46;
            var dest = new Rectangle(centerX - chipSize / 2, centerY - chipSize / 2, chipSize, chipSize);
            DrawPortraitOrFallback(spriteBatch, font, player, dest);

            // Shirt number badge in kit color (bottom-right corner of the chip)
            Color shirt = GetShirtColor(player);
            string numText = player.ShirtNumber.ToString();
            Vector2 numSize = font.MeasureString(numText) * 0.6f;
            var badge = new Rectangle(dest.Right - 16, dest.Bottom - 12, 18, 14);
            spriteBatch.Draw(_pixel, badge, shirt);
            spriteBatch.DrawString(font, numText,
                new Vector2(badge.Center.X - numSize.X / 2, badge.Center.Y - numSize.Y / 2),
                KitTextureFactory.ContrastFor(shirt), 0f, Vector2.Zero, 0.6f, SpriteEffects.None, 0f);

            // Name under the chip
            string name = player.Name.Length > 10 ? player.Name.Substring(0, 10) : player.Name;
            Vector2 nameSize = font.MeasureString(name) * 0.55f;
            spriteBatch.DrawString(font, name,
                new Vector2(centerX - nameSize.X / 2, dest.Bottom + 2), Color.White,
                0f, Vector2.Zero, 0.55f, SpriteEffects.None, 0f);
        }

        #endregion

        private void DrawInstructions(SpriteBatch spriteBatch, SpriteFont font, int screenHeight)
        {
            string instructions = Localization.Instance.Get("lineup.instructions");
            Vector2 instrSize = font.MeasureString(instructions);
            spriteBatch.DrawString(font, instructions,
                new Vector2((Game1.ScreenWidth - instrSize.X) / 2, screenHeight - 30),
                Color.LightGray);
        }

        private void DrawRectOutline(SpriteBatch spriteBatch, Rectangle rect, Color color, int thickness)
        {
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, rect.Width, thickness), color);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Bottom - thickness, rect.Width, thickness), color);
            spriteBatch.Draw(_pixel, new Rectangle(rect.X, rect.Y, thickness, rect.Height), color);
            spriteBatch.Draw(_pixel, new Rectangle(rect.Right - thickness, rect.Y, thickness, rect.Height), color);
        }

        private static Color Lighten(Color color, int amount = 90)
        {
            return new Color(
                Math.Min(255, color.R + amount),
                Math.Min(255, color.G + amount),
                Math.Min(255, color.B + amount));
        }

        private string GetPositionAbbreviation(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Goalkeeper => Localization.Instance.Get("lineup.position.gk"),
                PlayerPosition.Defender => Localization.Instance.Get("lineup.position.def"),
                PlayerPosition.Midfielder => Localization.Instance.Get("lineup.position.mid"),
                PlayerPosition.Forward => Localization.Instance.Get("lineup.position.fwd"),
                _ => "?"
            };
        }

        private string GetPositionGroupHeader(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Goalkeeper => Localization.Instance.Get("lineup.goalkeepers"),
                PlayerPosition.Defender => Localization.Instance.Get("lineup.defenders"),
                PlayerPosition.Midfielder => Localization.Instance.Get("lineup.midfielders"),
                PlayerPosition.Forward => Localization.Instance.Get("lineup.forwards"),
                _ => "?"
            };
        }

        private int GetPositionOrder(PlayerPosition position)
        {
            return position switch
            {
                PlayerPosition.Goalkeeper => 0,
                PlayerPosition.Defender => 1,
                PlayerPosition.Midfielder => 2,
                PlayerPosition.Forward => 3,
                _ => 4
            };
        }
    }
}
