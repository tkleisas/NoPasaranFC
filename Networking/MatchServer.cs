using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using Microsoft.Xna.Framework;
using NoPasaranFC.Database;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;

namespace NoPasaranFC.Networking
{
    /// <summary>
    /// Headless authoritative match server. It owns the tick clock, bundles both
    /// players' inputs and broadcasts them, and runs the same deterministic
    /// MatchEngine as the clients. Clients simulate the exact bundled ticks; this
    /// avoids shipping full snapshots while still giving the server authority over
    /// timing/end-of-match.
    /// </summary>
    public sealed class MatchServer
    {
        private readonly Team _sourceHome;
        private readonly Team _sourceAway;
        private int _port;
        private readonly int _seed;
        private readonly NetworkSettings _settings;
        private readonly string _teamsJson;

        private readonly object _lock = new object();
        private readonly List<ClientSession> _sessions = new List<ClientSession>();

        private TcpListener _listener;
        private Thread _acceptThread;
        private Thread _tickThread;
        private volatile bool _running;
        private volatile bool _matchEnded;

        private MatchEngine _engine;
        private ClientSession _homeSession;
        private ClientSession _awaySession;
        private int _tick;
        private int _halfTimeTicks;
        private TimeSpan _elapsed;

        public int Port => _port;
        public int Seed => _seed;
        public bool IsRunning => _running;
        public bool MatchEnded => _matchEnded;
        public event Action<string> Log;

        public MatchServer(Team home, Team away, int port = NetProtocol.DefaultPort,
            int? seed = null, NetworkSettings settings = null)
        {
            _sourceHome = home ?? throw new ArgumentNullException(nameof(home));
            _sourceAway = away ?? throw new ArgumentNullException(nameof(away));
            _port = port;
            _seed = seed ?? Environment.TickCount;
            _settings = settings ?? NetworkSettings.Capture();

            // Freeze the rosters now: the engine mutates positions/roles once it
            // starts, and the editor/DB must not leak into a running match.
            _teamsJson = TeamSeeder.SerializeTeams(new List<Team> { home, away }, indented: false);
        }

        public void Start()
        {
            if (_running) return;
            _running = true;
            _listener = new TcpListener(IPAddress.Any, _port);
            _listener.Start();
            if (_port == 0)
                _port = ((IPEndPoint)_listener.LocalEndpoint).Port;

            _acceptThread = new Thread(AcceptLoop)
            {
                IsBackground = true,
                Name = "npfc-net-accept",
            };
            _acceptThread.Start();

            Log?.Invoke($"NO PASARAN! server listening on port {_port} (seed {_seed})");
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient tcp;
                try
                {
                    tcp = _listener.AcceptTcpClient();
                }
                catch (Exception)
                {
                    break;
                }

                tcp.NoDelay = true;
                tcp.SendTimeout = 5000;
                var session = new ClientSession(tcp, this);

                lock (_lock)
                {
                    if (_sessions.Count >= 2)
                    {
                        session.SendError("Match is full");
                        session.Close();
                        continue;
                    }

                    // Fill whichever side is free (robust to a pre-match disconnect).
                    bool hasHome = _sessions.Exists(s => s.Side == NetSide.Home);
                    session.Side = hasHome ? NetSide.Away : NetSide.Home;
                    _sessions.Add(session);
                    session.Send(NetMessageType.Welcome, NetProtocol.EncodeWelcome(session.Side));
                    Log?.Invoke($"Client connected: {session.Remote} -> {session.Side}");

                    if (_sessions.Count == 2)
                        StartMatchLocked();
                }

                session.StartReading();
            }
        }

        /// <summary>Must be called while holding <see cref="_lock"/>.</summary>
        private void StartMatchLocked()
        {
            if (_engine != null) return;

            var teams = TeamSeeder.LoadTeamsFromJsonString(_teamsJson);
            if (teams.Count != 2)
            {
                BroadcastError("Server failed to load the match rosters");
                _running = false;
                return;
            }

            var home = teams[0];
            var away = teams[1];
            NetworkMatchSetup.PrepareTeams(home, away);
            _settings.Apply();
            AIController.DeterministicSeedBase = _seed;

            _homeSession = _sessions.Find(s => s.Side == NetSide.Home);
            _awaySession = _sessions.Find(s => s.Side == NetSide.Away);
            if (_homeSession == null || _awaySession == null)
            {
                BroadcastError("Server could not assign both sides");
                _running = false;
                return;
            }

            // Encode BEFORE the engine mutates the rosters.
            _homeSession.Send(NetMessageType.MatchStart, NetProtocol.EncodeMatchStart(new MatchStartInfo
            {
                Seed = _seed,
                TickRate = NetProtocol.TickRate,
                LocalSide = NetSide.Home,
                Settings = _settings,
                HomeTeam = home,
                AwayTeam = away,
            }));
            _awaySession.Send(NetMessageType.MatchStart, NetProtocol.EncodeMatchStart(new MatchStartInfo
            {
                Seed = _seed,
                TickRate = NetProtocol.TickRate,
                LocalSide = NetSide.Away,
                Settings = _settings,
                HomeTeam = home,
                AwayTeam = away,
            }));

            _engine = new MatchEngine(home, away, 1280, 720);
            _engine.SetRandomSeed(_seed);
            _engine.CelebrationDurationOverride = NetProtocol.CelebrationDurationSeconds;
            _engine.JoinPlayer2(true);

            Log?.Invoke($"Match started: {home.Name} vs {away.Name}");

            _tickThread = new Thread(TickLoop)
            {
                IsBackground = true,
                Name = "npfc-net-tick",
            };
            _tickThread.Start();
        }

        private void TickLoop()
        {
            var clock = Stopwatch.StartNew();
            double nextTick = 0;

            while (_running && !_matchEnded)
            {
                double now = clock.Elapsed.TotalSeconds;
                if (now < nextTick)
                {
                    Thread.Sleep(1);
                    continue;
                }

                nextTick += 1.0 / NetProtocol.TickRate;
                if (now - nextTick > 1.0)
                    nextTick = now; // recover from a long stall without spiralling

                var homeInput = _homeSession?.GetInput() ?? NetInput.Zero;
                var awayInput = _awaySession?.GetInput() ?? NetInput.Zero;

                bool forceStartSecondHalf = false;
                if (_engine.CurrentState == MatchEngine.MatchState.HalfTime)
                {
                    _halfTimeTicks++;
                    if (_halfTimeTicks >= NetProtocol.TickRate * 2)
                    {
                        _engine.StartSecondHalf();
                        forceStartSecondHalf = true;
                        _halfTimeTicks = 0;
                    }
                }
                else
                {
                    _halfTimeTicks = 0;
                }

                _elapsed += NetProtocol.FixedDelta;
                _engine.Update(new GameTime(_elapsed, NetProtocol.FixedDelta),
                    homeInput.Move, homeInput.Shoot, homeInput.Pass,
                    awayInput.Move, awayInput.Shoot, awayInput.Pass);
                _tick++;

                var tickInfo = new TickInfo
                {
                    Tick = _tick,
                    HomeInput = homeInput,
                    AwayInput = awayInput,
                    ForceStartSecondHalf = forceStartSecondHalf,
                    MatchTime = _engine.MatchTime,
                    HomeScore = _engine.HomeScore,
                    AwayScore = _engine.AwayScore,
                    State = _engine.CurrentState,
                    Half = _engine.Half,
                };
                Broadcast(NetMessageType.Tick, NetProtocol.EncodeTick(tickInfo));

                if (_engine.CurrentState == MatchEngine.MatchState.Ended)
                {
                    _matchEnded = true;
                    var end = new MatchEndInfo
                    {
                        HomeScore = _engine.HomeScore,
                        AwayScore = _engine.AwayScore,
                    };
                    Broadcast(NetMessageType.MatchEnd, NetProtocol.EncodeMatchEnd(end));
                    Log?.Invoke($"Match ended {end.HomeScore}-{end.AwayScore}");
                }
            }
        }

        internal void OnSessionClosed(ClientSession session)
        {
            bool matchWasRunning;
            lock (_lock)
            {
                if (!_sessions.Remove(session)) return;
                matchWasRunning = _engine != null && !_matchEnded;

                if (matchWasRunning)
                {
                    _matchEnded = true;
                    var end = new MatchEndInfo
                    {
                        HomeScore = _engine.HomeScore,
                        AwayScore = _engine.AwayScore,
                    };
                    var payload = NetProtocol.EncodeMatchEnd(end);
                    foreach (var s in _sessions)
                        s.Send(NetMessageType.MatchEnd, payload);
                }
            }

            Log?.Invoke(matchWasRunning
                ? "Client disconnected - match ended"
                : "Client disconnected");
        }

        private void Broadcast(NetMessageType type, byte[] payload)
        {
            ClientSession[] copy;
            lock (_lock) copy = _sessions.ToArray();
            foreach (var session in copy)
                session.Send(type, payload);
        }

        private void BroadcastError(string message)
        {
            var payload = NetProtocol.EncodeError(message);
            Broadcast(NetMessageType.Error, payload);
        }

        public void Stop()
        {
            if (!_running && _sessions.Count == 0) return;
            _running = false;

            try { _listener?.Stop(); } catch { /* ignore */ }

            ClientSession[] sessions;
            lock (_lock)
            {
                sessions = _sessions.ToArray();
                _sessions.Clear();
            }
            foreach (var session in sessions)
                session.Close();

            if (_acceptThread != null && _acceptThread.IsAlive && Thread.CurrentThread != _acceptThread)
                _acceptThread.Join(500);
            if (_tickThread != null && _tickThread.IsAlive && Thread.CurrentThread != _tickThread)
                _tickThread.Join(500);

            Log?.Invoke("Server stopped");
        }

        /// <summary>One connected peer. Inputs are written by its read thread and
        /// read by the tick thread; both directions are lock-guarded.</summary>
        internal sealed class ClientSession
        {
            private readonly MatchServer _server;
            private readonly TcpClient _tcp;
            private readonly NetworkStream _stream;
            private readonly object _sendLock = new object();
            private readonly object _inputLock = new object();
            private NetInput _input = NetInput.Zero;
            private Thread _readThread;
            private volatile bool _open = true;

            public NetSide Side { get; set; }
            public string Remote => _tcp.Client?.RemoteEndPoint?.ToString() ?? "?";

            public ClientSession(TcpClient tcp, MatchServer server)
            {
                _tcp = tcp;
                _server = server;
                _stream = tcp.GetStream();
            }

            public void StartReading()
            {
                _readThread = new Thread(ReadLoop)
                {
                    IsBackground = true,
                    Name = "npfc-net-session",
                };
                _readThread.Start();
            }

            public void SetInput(NetInput input)
            {
                lock (_inputLock) _input = input;
            }

            public NetInput GetInput()
            {
                lock (_inputLock) return _input;
            }

            public void Send(NetMessageType type, byte[] payload)
            {
                if (!_open) return;
                lock (_sendLock)
                {
                    try
                    {
                        NetProtocol.WriteFrame(_stream, type, payload);
                    }
                    catch (Exception)
                    {
                        Close();
                    }
                }
            }

            public void SendError(string message)
            {
                Send(NetMessageType.Error, NetProtocol.EncodeError(message));
            }

            private void ReadLoop()
            {
                try
                {
                    while (_open && NetProtocol.TryReadFrame(_stream, out var type, out var payload))
                    {
                        switch (type)
                        {
                            case NetMessageType.Hello:
                            {
                                var (version, name) = NetProtocol.DecodeHello(payload);
                                if (version != NetProtocol.Version)
                                {
                                    SendError($"Protocol mismatch: server {NetProtocol.Version}, client {version}");
                                    Close();
                                    return;
                                }
                                _server.Log?.Invoke($"{Remote} hello: {name}");
                                break;
                            }

                            case NetMessageType.Input:
                                SetInput(NetProtocol.DecodeInput(payload));
                                break;

                            case NetMessageType.Ping:
                                Send(NetMessageType.Pong, Array.Empty<byte>());
                                break;
                        }
                    }
                }
                catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException)
                {
                    // normal disconnect
                }
                catch (Exception ex)
                {
                    _server.Log?.Invoke($"Session error ({Remote}): {ex.Message}");
                }
                finally
                {
                    Close();
                    _server.OnSessionClosed(this);
                }
            }

            public void Close()
            {
                if (!_open) return;
                _open = false;
                try { _tcp.Close(); } catch { /* ignore */ }
            }
        }
    }
}
