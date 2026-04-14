using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using NoPasaranFC.Models;
using NoPasaranFC.Database;

namespace NoPasaranFC.Gameplay
{
    public static class ChampionshipInitializer
    {
        private static readonly string[] TeamNames = new[]
        {
            "NO PASARAN!",
            "ΜΠΑΡΤΣΕΛΙΩΜΑ",
            "ΚΤΕΛ",
            "NO NAME",
            "ΧΑΝΔΡΙΝΑΪΚΟΣ",
            "ΑΣΑΛΑΓΗΤΟΣ",
            "ΑΣΤΕΡΑΣ ΕΞΑΡΧΕΙΩΝ",
            "ΤΗΓΑΝΙΤΗΣ"
        };

        private static readonly string[] FirstNames = new[]
        {
            "Juan", "Carlos", "Miguel", "Pedro", "Jose", "Luis", "Antonio", "Fernando",
            "Diego", "Manuel", "Roberto", "Pablo", "Rafael", "Jorge", "Alejandro"
        };

        private static readonly string[] LastNames = new[]
        {
            "Garcia", "Rodriguez", "Martinez", "Hernandez", "Lopez", "Gonzalez",
            "Perez", "Sanchez", "Ramirez", "Torres", "Flores", "Rivera", "Cruz"
        };
        private static readonly string[] NoPasaranNames = new[]
        {
            "Wacaby", "Dablo", "Lamougio", "Donat", "Petros",
            "Super Fan", "Tonis", "Lefty", "Mihalis",
            "Giannis", "Kostas"
        };
        private static readonly PlayerPosition[] NoPasaranPositions = new[]
        {
            PlayerPosition.Goalkeeper,
            PlayerPosition.Defender,
            PlayerPosition.Defender,
            PlayerPosition.Defender,
            PlayerPosition.Defender,
            PlayerPosition.Midfielder,
            PlayerPosition.Midfielder,
            PlayerPosition.Midfielder,
            PlayerPosition.Midfielder,
            PlayerPosition.Forward,
            PlayerPosition.Forward,
        };

        /// <summary>
        /// Load the list of available championships from championships_seed.json.
        /// Returns an empty list if the file cannot be found or parsed.
        /// </summary>
        public static List<ChampionshipDefinition> GetAvailableChampionships()
        {
#if ANDROID
            try
            {
                var context = global::Android.App.Application.Context;
                using var stream = context.Assets.Open("Database/championships_seed.json");
                using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
                string jsonString = reader.ReadToEnd();
                return ChampionshipSeeder.LoadChampionshipsFromJsonString(jsonString);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load championships from Android assets: {ex.Message}");
                return new List<ChampionshipDefinition>();
            }
#else
            string jsonPath = PlatformHelper.GetAssetPath(Path.Combine("Database", "championships_seed.json"));
            if (File.Exists(jsonPath))
            {
                return ChampionshipSeeder.LoadChampionshipsFromJson(jsonPath);
            }
            return new List<ChampionshipDefinition>();
#endif
        }

        /// <summary>
        /// Create a championship using the default (legacy) roster loaded from teams_seed.json.
        /// Used as a fallback when no specific championship is selected.
        /// </summary>
        public static Championship CreateNewChampionship()
        {
            var championship = new Championship
            {
                Id = "default",
                Name = "NO PASARAN! CUP"
            };

            var teams = LoadCatalogTeams() ?? GenerateTeamsLegacy();

            AssignIdsAndAdd(championship, teams);
            championship.GenerateFixtures();
            return championship;
        }

        /// <summary>
        /// Create a championship from a specific definition loaded from championships_seed.json.
        /// Teams listed in the definition are looked up in teams_seed.json by exact name; any team
        /// that isn't present in the catalog is created with an auto-generated roster and the
        /// optional kit/logo overrides provided in the definition.
        /// </summary>
        public static Championship CreateNewChampionship(string championshipId)
        {
            var available = GetAvailableChampionships();
            var definition = available.FirstOrDefault(c => c.Id == championshipId);
            if (definition == null)
            {
                // Unknown id — fall back to the default championship.
                return CreateNewChampionship();
            }

            return CreateNewChampionship(definition);
        }

        public static Championship CreateNewChampionship(ChampionshipDefinition definition)
        {
            var championship = new Championship
            {
                Id = definition.Id,
                Name = definition.Name
            };

            var catalog = LoadCatalogTeams() ?? new List<Team>();
            var catalogByName = new Dictionary<string, Team>(StringComparer.OrdinalIgnoreCase);
            foreach (var team in catalog)
            {
                if (!catalogByName.ContainsKey(team.Name))
                    catalogByName[team.Name] = team;
            }

            var teams = new List<Team>();
            foreach (var entry in definition.Teams)
            {
                bool isPlayer = !string.IsNullOrEmpty(definition.PlayerTeam)
                    && string.Equals(entry.Name, definition.PlayerTeam, StringComparison.OrdinalIgnoreCase);

                if (catalogByName.TryGetValue(entry.Name, out var catalogTeam))
                {
                    // Re-use the catalog entry but force player-controlled flag to match the definition,
                    // and honor any kit/logo overrides from the championship definition.
                    catalogTeam.IsPlayerControlled = isPlayer;
                    if (!string.IsNullOrEmpty(entry.KitName))
                        catalogTeam.KitName = entry.KitName;
                    if (!string.IsNullOrEmpty(entry.Logo))
                        catalogTeam.Logo = entry.Logo;
                    teams.Add(catalogTeam);
                }
                else
                {
                    // Team not in catalog — synthesize one with an auto-generated roster.
                    var synthesized = TeamSeeder.CreateTeamWithDefaultRoster(
                        entry.Name,
                        isPlayer,
                        entry.KitName,
                        entry.Logo);
                    teams.Add(synthesized);
                }
            }

            AssignIdsAndAdd(championship, teams);
            championship.GenerateFixtures();
            return championship;
        }

        private static List<Team> LoadCatalogTeams()
        {
#if ANDROID
            try
            {
                return LoadTeamsFromAndroidAssets();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load teams from Android assets: {ex.Message}");
                return null;
            }
#else
            string jsonPath = PlatformHelper.GetAssetPath(Path.Combine("Database", "teams_seed.json"));
            if (File.Exists(jsonPath))
            {
                return TeamSeeder.LoadTeamsFromJson(jsonPath);
            }
            return null;
#endif
        }

        private static void AssignIdsAndAdd(Championship championship, List<Team> teams)
        {
            int teamId = 1;
            foreach (var team in teams)
            {
                team.Id = teamId++;

                foreach (var player in team.Players)
                {
                    player.TeamId = team.Id;
                }

                championship.Teams.Add(team);
            }
        }

#if ANDROID
        private static List<Team> LoadTeamsFromAndroidAssets()
        {
            // Use Android's AssetManager to read the JSON file
            var context = global::Android.App.Application.Context;
            using var stream = context.Assets.Open("Database/teams_seed.json");
            using var reader = new StreamReader(stream, System.Text.Encoding.UTF8);
            string jsonString = reader.ReadToEnd();
            return TeamSeeder.LoadTeamsFromJsonString(jsonString);
        }
#endif

        private static Player CreatePlayer(Random random, PlayerPosition position)
        {
            string firstName = FirstNames[random.Next(FirstNames.Length)];
            string lastName = LastNames[random.Next(LastNames.Length)];
            string name = $"{firstName} {lastName}";

            var player = new Player(name, position);

            // Generate random stats based on position
            switch (position)
            {
                case PlayerPosition.Goalkeeper:
                    player.Speed = random.Next(30, 50);
                    player.Shooting = random.Next(20, 40);
                    player.Passing = random.Next(40, 60);
                    player.Defending = random.Next(60, 90);
                    player.Agility = random.Next(60, 85);
                    player.Technique = random.Next(40, 65);
                    player.Stamina = random.Next(70, 95);
                    break;

                case PlayerPosition.Defender:
                    player.Speed = random.Next(40, 65);
                    player.Shooting = random.Next(20, 45);
                    player.Passing = random.Next(50, 70);
                    player.Defending = random.Next(65, 90);
                    player.Agility = random.Next(45, 70);
                    player.Technique = random.Next(40, 65);
                    player.Stamina = random.Next(65, 90);
                    break;

                case PlayerPosition.Midfielder:
                    player.Speed = random.Next(50, 75);
                    player.Shooting = random.Next(45, 70);
                    player.Passing = random.Next(60, 90);
                    player.Defending = random.Next(45, 70);
                    player.Agility = random.Next(60, 85);
                    player.Technique = random.Next(60, 85);
                    player.Stamina = random.Next(70, 95);
                    break;

                case PlayerPosition.Forward:
                    player.Speed = random.Next(60, 90);
                    player.Shooting = random.Next(60, 95);
                    player.Passing = random.Next(45, 70);
                    player.Defending = random.Next(20, 45);
                    player.Agility = random.Next(65, 90);
                    player.Technique = random.Next(60, 85);
                    player.Stamina = random.Next(60, 85);
                    break;
            }

            return player;
        }

        private static List<Team> GenerateTeamsLegacy()
        {
            var teams = new List<Team>();
            var random = new Random();

            foreach (var teamName in TeamNames)
            {
                bool isPlayerControlled = teamName == "NO PASARAN!";
                var team = new Team(teamName, isPlayerControlled);

                if(teamName == "NO PASARAN!")
                {
                    // Create 11 specific players for NO PASARAN!
                    int index = 0;
                    foreach (var playerName in NoPasaranNames)
                    {
                        var position = NoPasaranPositions[index++];
                        var player = new Player(playerName, position);
                        // Assign random stats
                        player.Speed = random.Next(40, 90);
                        player.Shooting = random.Next(30, 95);
                        player.Passing = random.Next(30, 90);
                        player.Defending = random.Next(20, 90);
                        player.Agility = random.Next(40, 90);
                        player.Technique = random.Next(40, 90);
                        player.Stamina = random.Next(50, 95);
                        player.IsStarting = true;
                        player.ShirtNumber = index;

                        team.AddPlayer(player);
                    }
                    teams.Add(team);
                    continue;
                }

                // Create 11 players for each team
                int shirtNum = 1;

                // 1 Goalkeeper
                var gk = CreatePlayer(random, PlayerPosition.Goalkeeper);
                gk.IsStarting = true;
                gk.ShirtNumber = shirtNum++;
                team.AddPlayer(gk);

                // 4 Defenders
                for (int i = 0; i < 4; i++)
                {
                    var def = CreatePlayer(random, PlayerPosition.Defender);
                    def.IsStarting = true;
                    def.ShirtNumber = shirtNum++;
                    team.AddPlayer(def);
                }

                // 4 Midfielders
                for (int i = 0; i < 4; i++)
                {
                    var mid = CreatePlayer(random, PlayerPosition.Midfielder);
                    mid.IsStarting = true;
                    mid.ShirtNumber = shirtNum++;
                    team.AddPlayer(mid);
                }

                // 2 Forwards
                for (int i = 0; i < 2; i++)
                {
                    var fwd = CreatePlayer(random, PlayerPosition.Forward);
                    fwd.IsStarting = true;
                    fwd.ShirtNumber = shirtNum++;
                    team.AddPlayer(fwd);
                }

                teams.Add(team);
            }

            return teams;
        }
    }
}
