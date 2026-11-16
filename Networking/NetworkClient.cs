using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net.Sockets;
using System.Threading;

namespace NoPasaranFC.Networking
{
    /// <summary>
    /// Thin TCP client for a NO PASARAN! network match. The receive loop runs on a
    /// background thread and pushes ordered events into an internal queue;
    /// gameplay code drains that queue on the main thread.
    /// </summary>
    public sealed class NetworkClient : IDisposable
    {
        private readonly TcpClient _tcp;
        private readonly NetworkStream _stream;
        private readonly Thread _readThread;
        private readonly object _sendLock = new object();
        private readonly ConcurrentQueue<NetEvent> _incoming = new ConcurrentQueue<NetEvent>();
        private volatile bool _running = true;

        public NetSide LocalSide { get; private set; } = NetSide.Home;
        public bool IsConnected => _running && _tcp.Connected;
        public string PlayerName { get; }

        private NetworkClient(TcpClient tcp, string playerName)
        {
            _tcp = tcp;
            _stream = tcp.GetStream();
            PlayerName = string.IsNullOrWhiteSpace(playerName) ? "Player" : playerName;

            _readThread = new Thread(ReadLoop)
            {
                IsBackground = true,
                Name = "npfc-net-client",
            };
            _readThread.Start();
            SendHello();
        }

        /// <summary>
        /// Connects with a timeout. Blocking; run on a worker thread from UI code.
        /// </summary>
        public static NetworkClient Connect(string host, int port, string playerName, int timeoutMs = 5000)
        {
            var tcp = new TcpClient { NoDelay = true, SendTimeout = 5000 };
            try
            {
                var task = tcp.ConnectAsync(host, port);
                if (!task.Wait(timeoutMs))
                    throw new TimeoutException($"Timed out connecting to {host}:{port}");
                return new NetworkClient(tcp, playerName);
            }
            catch
            {
                tcp.Close();
                throw;
            }
        }

        public bool TryDequeue(out NetEvent evt) => _incoming.TryDequeue(out evt);

        public void SendInput(NetInput input)
        {
            Send(NetMessageType.Input, NetProtocol.EncodeInput(input));
        }

        public void SendPing()
        {
            Send(NetMessageType.Ping, Array.Empty<byte>());
        }

        private void SendHello()
        {
            Send(NetMessageType.Hello, NetProtocol.EncodeHello(PlayerName));
        }

        private void Send(NetMessageType type, byte[] payload)
        {
            if (!_running) return;
            lock (_sendLock)
            {
                try
                {
                    NetProtocol.WriteFrame(_stream, type, payload);
                }
                catch (Exception ex)
                {
                    Fail(ex.Message);
                }
            }
        }

        private void ReadLoop()
        {
            try
            {
                while (_running && NetProtocol.TryReadFrame(_stream, out var type, out var payload))
                {
                    switch (type)
                    {
                        case NetMessageType.Welcome:
                            LocalSide = NetProtocol.DecodeWelcome(payload);
                            _incoming.Enqueue(NetEvent.Welcome(LocalSide));
                            break;

                        case NetMessageType.MatchStart:
                        {
                            var start = NetProtocol.DecodeMatchStart(payload);
                            LocalSide = start.LocalSide;
                            _incoming.Enqueue(NetEvent.StartMatch(start));
                            break;
                        }

                        case NetMessageType.Tick:
                            _incoming.Enqueue(NetEvent.TickEvent(NetProtocol.DecodeTick(payload)));
                            break;

                        case NetMessageType.MatchEnd:
                            _incoming.Enqueue(NetEvent.MatchEnded(NetProtocol.DecodeMatchEnd(payload)));
                            break;

                        case NetMessageType.Error:
                            _incoming.Enqueue(NetEvent.ErrorEvent(NetProtocol.DecodeError(payload)));
                            break;

                        case NetMessageType.Ping:
                            Send(NetMessageType.Pong, Array.Empty<byte>());
                            break;

                        case NetMessageType.Pong:
                            break;
                    }
                }
            }
            catch (Exception ex) when (ex is IOException || ex is SocketException || ex is ObjectDisposedException)
            {
                if (_running) _incoming.Enqueue(NetEvent.DisconnectedEvent(ex.Message));
            }
            catch (Exception ex)
            {
                if (_running) _incoming.Enqueue(NetEvent.ErrorEvent(ex.Message));
            }
            finally
            {
                bool wasRunning = _running;
                _running = false;
                if (wasRunning) _incoming.Enqueue(NetEvent.DisconnectedEvent());
            }
        }

        private void Fail(string reason)
        {
            if (!_running) return;
            _running = false;
            _incoming.Enqueue(NetEvent.DisconnectedEvent(reason));
            try { _tcp.Close(); } catch { /* ignore */ }
        }

        public void Disconnect()
        {
            if (!_running) return;
            _running = false;
            try { _tcp.Close(); } catch { /* ignore */ }
        }

        public void Dispose() => Disconnect();
    }
}
