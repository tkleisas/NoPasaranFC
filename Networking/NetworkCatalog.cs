using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Net.Sockets;
using NoPasaranFC.Database;
using NoPasaranFC.Models;

namespace NoPasaranFC.Networking
{
    /// <summary>Team/catalog helpers shared by hosting, joining and the dedicated server.</summary>
    public static class NetworkCatalog
    {
        /// <summary>
        /// Teams for a network friendly: prefer the live database (custom rosters
        /// included), fall back to the shipped seed catalog for headless servers.
        /// </summary>
        public static List<Team> LoadFriendlyTeams(DatabaseManager database)
        {
            if (database != null)
            {
                try
                {
                    var teams = database.LoadAllTeams();
                    if (teams.Count >= 2) return teams;
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"NetworkCatalog: DB teams unavailable ({ex.Message})");
                }
            }
            return LoadSeedTeams();
        }

        public static List<Team> LoadSeedTeams()
        {
            string path = FindSeedFile();
            if (path == null) return new List<Team>();
            try
            {
                return TeamSeeder.LoadTeamsFromJson(path);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"NetworkCatalog: failed to load seed teams ({ex.Message})");
                return new List<Team>();
            }
        }

        /// <summary>Non-loopback IPv4 addresses, for "join at ip:port" hints.</summary>
        public static IEnumerable<string> GetLocalIPv4Addresses()
        {
            IPAddress[] addresses;
            try
            {
                addresses = Dns.GetHostAddresses(Dns.GetHostName());
            }
            catch
            {
                yield break;
            }

            foreach (var address in addresses)
            {
                if (address.AddressFamily == AddressFamily.InterNetwork &&
                    !IPAddress.IsLoopback(address))
                {
                    yield return address.ToString();
                }
            }
        }

        private static string FindSeedFile()
        {
            var candidates = new[]
            {
                Path.Combine(AppContext.BaseDirectory, "Database", "teams_seed.json"),
                Path.Combine(Directory.GetCurrentDirectory(), "Database", "teams_seed.json"),
            };
            foreach (var candidate in candidates)
                if (File.Exists(candidate)) return candidate;

            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                string candidate = Path.Combine(dir.FullName, "Database", "teams_seed.json");
                if (File.Exists(candidate)) return candidate;
                dir = dir.Parent;
            }
            return null;
        }
    }
}
