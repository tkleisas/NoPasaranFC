using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using NoPasaranFC.Models;

namespace NoPasaranFC.Networking
{
    /// <summary>Console entry point for the dedicated server.</summary>
    public static class ServerHost
    {
        public static void Run(string[] args)
        {
            int port = NetProtocol.DefaultPort;
            int? seed = null;
            string homeName = null;
            string awayName = null;

            for (int i = 0; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--port":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int p)) port = p;
                        break;
                    case "--seed":
                        if (i + 1 < args.Length && int.TryParse(args[++i], out int s)) seed = s;
                        break;
                    case "--home":
                        if (i + 1 < args.Length) homeName = args[++i];
                        break;
                    case "--away":
                        if (i + 1 < args.Length) awayName = args[++i];
                        break;
                }
            }

            var teams = NetworkCatalog.LoadSeedTeams();
            if (teams.Count < 2)
            {
                Console.Error.WriteLine("Could not load teams (expected Database/teams_seed.json next to the executable).");
                return;
            }

            var home = FindTeam(teams, homeName) ?? teams[0];
            var away = FindTeam(teams, awayName) ?? teams.FirstOrDefault(t => t != home);
            if (away == null)
            {
                Console.Error.WriteLine("Could not pick two distinct teams.");
                return;
            }

            var server = new MatchServer(home, away, port, seed ?? Environment.TickCount);
            server.Log += message => Console.WriteLine($"[server] {message}");
            server.Start();

            Console.WriteLine($"[server] {home.Name} vs {away.Name}");
            foreach (string ip in NetworkCatalog.GetLocalIPv4Addresses())
                Console.WriteLine($"[server] join at {ip}:{server.Port}");
            Console.WriteLine("[server] Press Ctrl+C to stop.");

            using var quit = new ManualResetEventSlim(false);
            Console.CancelKeyPress += (_, e) =>
            {
                e.Cancel = true;
                quit.Set();
            };
            quit.Wait();
            server.Stop();
        }

        private static Team FindTeam(List<Team> teams, string name)
        {
            if (string.IsNullOrWhiteSpace(name)) return null;
            return teams.FirstOrDefault(t => t.Name.Equals(name, StringComparison.OrdinalIgnoreCase))
                ?? teams.FirstOrDefault(t => t.Name.Contains(name, StringComparison.OrdinalIgnoreCase));
        }

    }
}
