using System;
using System.Collections.Generic;
using System.IO;
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
        public static Championship CreateNewChampionship()
        {
            var championship = new Championship();
            
            // Try to load teams from JSON seed file
            List<Team> teams = null;
            
#if ANDROID
            // On Android, load from embedded assets
            try
            {
                teams = LoadTeamsFromAndroidAssets();
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Failed to load teams from Android assets: {ex.Message}");
            }
#else
            // On desktop, load from file system
            string jsonPath = PlatformHelper.GetAssetPath(Path.Combine("Database", "teams_seed.json"));
            if (File.Exists(jsonPath))
            {
                teams = TeamSeeder.LoadTeamsFromJson(jsonPath);
            }
#endif
            
            // Fallback to legacy generation if JSON loading failed
            if (teams == null || teams.Count == 0)
            {
                teams = GenerateTeamsLegacy();
            }
            
            // Assign team IDs and update player TeamIds
            int teamId = 1;
            foreach (var team in teams)
            {
                team.Id = teamId++;
                
                // Update all player TeamIds to match the assigned team ID
                foreach (var player in team.Players)
                {
                    player.TeamId = team.Id;
                }
                
                championship.Teams.Add(team);
            }
            
            championship.GenerateFixtures();
            
            return championship;
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
