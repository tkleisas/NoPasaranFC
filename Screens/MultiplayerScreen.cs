using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Database;
using NoPasaranFC.Debugging;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using NoPasaranFC.Networking;

namespace NoPasaranFC.Screens
{
    /// <summary>
    /// LAN/online lobby: host an in-process server or join one by address. Once the
    /// server starts the match, ownership transfers to a network-mode MatchScreen.
    /// </summary>
    public class MultiplayerScreen : Screen
    {
        private enum Mode { Menu, EnterAddress, Connecting, Waiting, Error }

        private readonly Championship _championship;
        private readonly DatabaseManager _database;
        private readonly ScreenManager _screenManager;
        private readonly ContentManager _content;
        private readonly GraphicsDevice _graphicsDevice;
        private readonly InputHelper _input;

        private Mode _mode = Mode.Menu;
        private int _selected;
        private string _address = "127.0.0.1";
        private string _status = string.Empty;
        private KeyboardState _previousKeys;
        private Texture2D _pixel;

        private NetworkClient _client;
        private MatchServer _server;
        private Task<NetworkClient> _connectTask;
        private bool _started;

        private static Localization Loc => Localization.Instance;

        public MultiplayerScreen(Championship championship, DatabaseManager database,
            ScreenManager screenManager, ContentManager content, GraphicsDevice graphicsDevice)
            : base(content, graphicsDevice)
        {
            _championship = championship;
            _database = database;
            _screenManager = screenManager;
            _content = content;
            _graphicsDevice = graphicsDevice;
            _input = new InputHelper();
        }

        public override void Update(GameTime gameTime)
        {
            if (_started)
            {
                IsFinished = true;
                return;
            }

            _input.Update();
            PollConnect();
            PollLobby();
            if (_started) return;

            var keys = DebugInput.GetState();

            switch (_mode)
            {
                case Mode.Menu:
                    UpdateMenu();
                    break;
                case Mode.EnterAddress:
                    UpdateAddress(keys);
                    break;
                case Mode.Connecting:
                case Mode.Waiting:
                case Mode.Error:
                    if (_input.IsBackPressed())
                        CancelAndReturnToMenu();
                    break;
            }

            _previousKeys = keys;
        }

        private void UpdateMenu()
        {
            if (_input.IsMenuDownPressed())
            {
                _selected = (_selected + 1) % 3;
                AudioManager.Instance.PlaySoundEffect("menu_move");
            }
            if (_input.IsMenuUpPressed())
            {
                _selected = (_selected + 2) % 3;
                AudioManager.Instance.PlaySoundEffect("menu_move");
            }
            if (_input.IsBackPressed())
            {
                IsFinished = true;
                return;
            }
            if (!_input.IsConfirmPressed()) return;

            AudioManager.Instance.PlaySoundEffect("menu_select");
            switch (_selected)
            {
                case 0:
                    StartHosting();
                    break;
                case 1:
                    _mode = Mode.EnterAddress;
                    _status = Loc.Get("multiplayer.enterIp");
                    break;
                default:
                    IsFinished = true;
                    break;
            }
        }

        private void UpdateAddress(KeyboardState keys)
        {
            if (_input.IsBackPressed())
            {
                _mode = Mode.Menu;
                _status = string.Empty;
                return;
            }

            if (Pressed(keys, Keys.Back) && _address.Length > 0)
                _address = _address.Substring(0, _address.Length - 1);

            for (int d = 0; d <= 9; d++)
            {
                if (Pressed(keys, Keys.D0 + d)) AppendAddress((char)('0' + d));
                if (Pressed(keys, Keys.NumPad0 + d)) AppendAddress((char)('0' + d));
            }
            if (Pressed(keys, Keys.OemPeriod) || Pressed(keys, Keys.Decimal))
                AppendAddress('.');
            if (Pressed(keys, Keys.OemSemicolon))
                AppendAddress(':');

            if (_input.IsConfirmPressed())
            {
                var (host, port) = ParseAddress(_address);
                BeginConnect(host, port);
            }
        }

        private void AppendAddress(char c)
        {
            if (_address.Length >= 40) return;
            _address += c;
        }

        private bool Pressed(KeyboardState keys, Keys key) =>
            keys.IsKeyDown(key) && _previousKeys.IsKeyUp(key);

        private void StartHosting()
        {
            var teams = NetworkCatalog.LoadFriendlyTeams(_database);
            if (teams.Count < 2)
            {
                _mode = Mode.Error;
                _status = "No teams available to host a match.";
                return;
            }

            try
            {
                _server = new MatchServer(teams[0], teams[1], NetProtocol.DefaultPort,
                    Environment.TickCount, NetworkSettings.Capture());
                _server.Log += message => _status = message;
                _server.Start();
                BeginConnect("127.0.0.1", NetProtocol.DefaultPort);
            }
            catch (Exception ex)
            {
                _mode = Mode.Error;
                _status = ex.Message;
                StopServer();
            }
        }

        private void BeginConnect(string host, int port)
        {
            if (_connectTask != null) return;

            _mode = Mode.Connecting;
            _status = Loc.Get("multiplayer.connecting");
            string name = Environment.UserName;
            _connectTask = Task.Run(() => NetworkClient.Connect(host, port, name));
        }

        private void PollConnect()
        {
            if (_connectTask == null || !_connectTask.IsCompleted) return;

            var task = _connectTask;
            _connectTask = null;

            if (task.IsFaulted)
            {
                _mode = Mode.Error;
                _status = task.Exception?.InnerException?.Message
                    ?? Loc.Get("multiplayer.error");
                StopServer();
                return;
            }

            _client = task.Result;
            _mode = Mode.Waiting;
            _status = Loc.Get("multiplayer.waiting");
        }

        private void PollLobby()
        {
            if (_client == null) return;

            while (_client.TryDequeue(out var evt))
            {
                switch (evt.Kind)
                {
                    case NetEventKind.Welcome:
                        _status = $"{Loc.Get("multiplayer.waiting")} ({evt.Side})";
                        break;

                    case NetEventKind.Start:
                        StartMatch(evt.Start);
                        return;

                    case NetEventKind.Error:
                    case NetEventKind.Disconnected:
                        _status = evt.Message ?? Loc.Get("multiplayer.error");
                        _mode = Mode.Error;
                        _client.Dispose();
                        _client = null;
                        StopServer();
                        return;
                }
            }
        }

        private void StartMatch(MatchStartInfo start)
        {
            _started = true;

            var home = start.HomeTeam;
            var away = start.AwayTeam;
            var match = new Match(home.Id, away.Id);

            var client = _client;
            _client = null;

            var server = _server;
            _server = null;
            Action onFinished = server != null
                ? () => { try { server.Stop(); } catch { /* ignore */ } }
                : (Action)null;

            var matchScreen = new MatchScreen(home, away, match, _championship,
                _database, _screenManager, _content, client, start, onFinished);
            if (_graphicsDevice != null)
                matchScreen.SetGraphicsDevice(_graphicsDevice);

            _screenManager.PushScreen(matchScreen);
            IsFinished = true;
        }

        private void CancelAndReturnToMenu()
        {
            _client?.Dispose();
            _client = null;

            // A connect attempt may still be in flight; dispose the client it
            // eventually produces instead of leaking a live socket.
            if (_connectTask != null)
            {
                var pending = _connectTask;
                _connectTask = null;
                pending.ContinueWith(t =>
                {
                    if (t.Status == TaskStatus.RanToCompletion)
                        t.Result.Dispose();
                }, TaskScheduler.Default);
            }

            StopServer();
            _mode = Mode.Menu;
            _status = string.Empty;
        }

        private void StopServer()
        {
            try { _server?.Stop(); } catch { /* ignore */ }
            _server = null;
        }

        private static (string Host, int Port) ParseAddress(string text)
        {
            text = (text ?? string.Empty).Trim();
            if (string.IsNullOrEmpty(text)) return ("127.0.0.1", NetProtocol.DefaultPort);

            int colon = text.LastIndexOf(':');
            if (colon > 0 && int.TryParse(text.Substring(colon + 1), out int port) &&
                port > 0 && port <= 65535)
            {
                return (text.Substring(0, colon), port);
            }
            return (text, NetProtocol.DefaultPort);
        }

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (_pixel == null && GraphicsDevice != null)
            {
                _pixel = new Texture2D(GraphicsDevice, 1, 1);
                _pixel.SetData(new[] { Color.White });
            }

            int width = Game1.ScreenWidth;
            int height = Game1.ScreenHeight;
            if (_pixel != null)
                spriteBatch.Draw(_pixel, new Rectangle(0, 0, width, height), new Color(8, 16, 26) * 0.94f);

            float scale = Game1.UIScale;
            string title = Loc.Get("multiplayer.title");
            Vector2 titleSize = font.MeasureString(title) * scale;
            spriteBatch.DrawString(font, title,
                new Vector2((width - titleSize.X) / 2f, height * 0.12f),
                Color.Yellow, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);

            if (_mode == Mode.Menu)
            {
                string[] options =
                {
                    Loc.Get("multiplayer.host"),
                    Loc.Get("multiplayer.join"),
                    Loc.Get("menu.back"),
                };
                for (int i = 0; i < options.Length; i++)
                {
                    Vector2 size = font.MeasureString(options[i]) * scale;
                    Vector2 pos = new Vector2((width - size.X) / 2f, height * 0.32f + i * 52f * scale);
                    Color color = i == _selected ? Color.Gold : Color.White;
                    if (i == _selected)
                        spriteBatch.DrawString(font, ">", pos - new Vector2(34f * scale, 0f), Color.Gold, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    spriteBatch.DrawString(font, options[i], pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                }

                Vector2 hintSize = font.MeasureString(Loc.Get("multiplayer.hostHint")) * (scale * 0.7f);
                spriteBatch.DrawString(font, Loc.Get("multiplayer.hostHint"),
                    new Vector2((width - hintSize.X) / 2f, height * 0.78f),
                    Color.LightGray, 0f, Vector2.Zero, scale * 0.7f, SpriteEffects.None, 0f);
                spriteBatch.DrawString(font, $"{Loc.Get("multiplayer.port")}: {NetProtocol.DefaultPort}",
                    new Vector2((width - hintSize.X) / 2f, height * 0.83f),
                    Color.LightGray, 0f, Vector2.Zero, scale * 0.7f, SpriteEffects.None, 0f);
            }
            else
            {
                string addressLine = _mode == Mode.EnterAddress ? _address + "_" : _address;
                if (_mode == Mode.EnterAddress)
                {
                    DrawCentered(spriteBatch, font, Loc.Get("multiplayer.enterIp"), 0.30f, Color.White, scale * 0.8f);
                    DrawCentered(spriteBatch, font, addressLine, 0.40f, Color.Gold, scale * 1.2f);
                }

                Color statusColor = _mode == Mode.Error ? Color.OrangeRed : Color.LightGreen;
                DrawCentered(spriteBatch, font, _status, 0.56f, statusColor, scale);

                // Hosting: show the addresses the other player can join.
                if (_server != null && _mode != Mode.Error)
                {
                    float y = 0.64f;
                    foreach (string ip in NetworkCatalog.GetLocalIPv4Addresses())
                    {
                        DrawCentered(spriteBatch, font, $"Host: {ip}:{NetProtocol.DefaultPort}",
                            y, Color.LightBlue, scale * 0.75f);
                        y += 0.045f;
                    }
                }

                if (_mode == Mode.Connecting || _mode == Mode.Waiting)
                    DrawCentered(spriteBatch, font, Loc.Get("menu.back") + " (ESC)", 0.86f, Color.Gray, scale * 0.7f);
            }
        }

        private void DrawCentered(SpriteBatch spriteBatch, SpriteFont font, string text, float yFraction, Color color, float scale)
        {
            if (string.IsNullOrEmpty(text)) return;
            Vector2 size = font.MeasureString(text) * scale;
            spriteBatch.DrawString(font, text,
                new Vector2((Game1.ScreenWidth - size.X) / 2f, Game1.ScreenHeight * yFraction),
                color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
    }
}
