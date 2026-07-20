using System;
using System.Collections.Concurrent;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;

namespace NoPasaranFC.Debugging
{
    /// <summary>
    /// Minimal line-based TCP debug console, bound to loopback only.
    /// Transport only: accepted lines are queued and executed on the game thread
    /// by Game1, which writes responses back through DebugCommand.Respond().
    ///
    /// Protocol (one command per line, one response line per command):
    ///   shot &lt;path&gt; [delayFrames]  - capture frame to PNG
    ///   key &lt;name&gt;                 - tap a key for one frame (XNA Keys enum name)
    ///   down &lt;name&gt; / up &lt;name&gt;        - hold/release a key
    ///   state                    - current screen, fps, match info
    ///   match                    - jump straight to the next unplayed match
    ///   quit                     - exit the game
    /// </summary>
    public class DebugServer
    {
        public class DebugCommand
        {
            public string Line;
            public StreamWriter Responder;

            public void Respond(string text)
            {
                try
                {
                    if (text != null)
                    {
                        Responder?.WriteLine(text);
                        Responder?.Flush();
                    }
                }
                catch { /* client disconnected */ }
            }
        }

        private readonly ConcurrentQueue<DebugCommand> _commands = new ConcurrentQueue<DebugCommand>();
        private TcpListener _listener;
        private Thread _acceptThread;
        private volatile bool _running;

        public int Port { get; private set; }

        public void Start(int port)
        {
            Port = port;
            _running = true;
            _listener = new TcpListener(IPAddress.Loopback, port);
            _listener.Start();
            _acceptThread = new Thread(AcceptLoop) { IsBackground = true, Name = "DebugServer" };
            _acceptThread.Start();
        }

        private void AcceptLoop()
        {
            while (_running)
            {
                TcpClient client;
                try { client = _listener.AcceptTcpClient(); }
                catch { break; } // listener stopped

                var thread = new Thread(() => ClientLoop(client)) { IsBackground = true };
                thread.Start();
            }
        }

        private void ClientLoop(TcpClient client)
        {
            try
            {
                using (client)
                {
                    var stream = client.GetStream();
                    using var reader = new StreamReader(stream, Encoding.UTF8);
                    var writer = new StreamWriter(stream, Encoding.UTF8) { AutoFlush = false };
                    writer.WriteLine("NoPasaranFC debug console ready");
                    writer.Flush();

                    string line;
                    while (_running && (line = reader.ReadLine()) != null)
                    {
                        line = line.Trim();
                        if (line.Length > 0)
                            _commands.Enqueue(new DebugCommand { Line = line, Responder = writer });
                    }
                }
            }
            catch { /* client disconnected */ }
        }

        public bool TryDequeue(out DebugCommand command) => _commands.TryDequeue(out command);

        public void Stop()
        {
            _running = false;
            try { _listener?.Stop(); } catch { }
        }
    }
}
