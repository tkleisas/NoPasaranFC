using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NoPasaranFC.Models;

namespace NoPasaranFC.Database
{
    public class TeamSeeder
    {
        private class TeamSeedData
        {
            public List<TeamData> teams { get; set; }
        }
        
        private class TeamData
        {
            public string name { get; set; }
            public bool isPlayerControlled { get; set; }
            public List<PlayerData> players { get; set; }
        }
        
        private class PlayerData
        {
            public string name { get; set; }
            public string position { get; set; }
            public int shirtNumber { get; set; }
            public bool isStarting { get; set; }
            public int speed { get; set; }
            public int shooting { get; set; }
            public int passing { get; set; }
            public int defending { get; set; }
            public int agility { get; set; }
            public int technique { get; set; }
            public int stamina { get; set; }
        }
        
        public static List<Team> LoadTeamsFromJson(string jsonPath)
        {
            var teams = new List<Team>();
            
            try
            {
                string jsonString = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
                var seedData = JsonSerializer.Deserialize<TeamSeedData>(jsonString);
                
                foreach (var teamData in seedData.teams)
                {
                    var team = new Team(teamData.name, teamData.isPlayerControlled);
                    
                    // If no players specified, generate default roster
                    if (teamData.players == null || teamData.players.Count == 0)
                    {
                        GenerateDefaultRoster(team);
                    }
                    else
                    {
                        // Load players from JSON
                        foreach (var playerData in teamData.players)
                        {
                            var position = ParsePosition(playerData.position);
                            var player = new Player(playerData.name, position)
                            {
                                ShirtNumber = playerData.shirtNumber,
                                IsStarting = playerData.isStarting,
                                Speed = playerData.speed,
                                Shooting = playerData.shooting,
                                Passing = playerData.passing,
                                Defending = playerData.defending,
                                Agility = playerData.agility,
                                Technique = playerData.technique,
                                Stamina = playerData.stamina
                            };
                            team.AddPlayer(player);
                        }
                    }
                    
                    teams.Add(team);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading teams from JSON: {ex.Message}");
            }
            
            return teams;
        }
        
        private static PlayerPosition ParsePosition(string position)
        {
            return position switch
            {
                "Goalkeeper" => PlayerPosition.Goalkeeper,
                "Defender" => PlayerPosition.Defender,
                "Midfielder" => PlayerPosition.Midfielder,
                "Forward" => PlayerPosition.Forward,
                _ => PlayerPosition.Midfielder
            };
        }
        
        private static void GenerateDefaultRoster(Team team)
        {
            var random = new Random(team.Name.GetHashCode()); // Consistent generation per team
            
            // Generate 22 players with varied positions
            int[] positionCounts = { 2, 8, 8, 4 }; // GK, DEF, MID, FWD
            int shirtNumber = 1;
            int startingCount = 0;
            
            for (int posIdx = 0; posIdx < 4; posIdx++)
            {
                var position = (PlayerPosition)posIdx;
                int count = positionCounts[posIdx];
                
                for (int i = 0; i < count; i++)
                {
                    string playerName = GeneratePlayerName(team.Name, position, i, random);
                    var player = new Player(playerName, position)
                    {
                        ShirtNumber = shirtNumber++,
                        IsStarting = startingCount < 11, // First 11 are starting
                        Speed = GenerateStatForPosition(position, "Speed", random),
                        Shooting = GenerateStatForPosition(position, "Shooting", random),
                        Passing = GenerateStatForPosition(position, "Passing", random),
                        Defending = GenerateStatForPosition(position, "Defending", random),
                        Agility = GenerateStatForPosition(position, "Agility", random),
                        Technique = GenerateStatForPosition(position, "Technique", random),
                        Stamina = random.Next(75, 95)
                    };
                    
                    team.AddPlayer(player);
                    if (player.IsStarting) startingCount++;
                }
            }
        }
        
        private static string GeneratePlayerName(string teamName, PlayerPosition position, int index, Random random)
        {
            string[] firstNames = { "Κώστας", "Γιώργος", "Δημήτρης", "Νίκος", "Μιχάλης", "Σωτήρης", 
                                    "Ανδρέας", "Παναγιώτης", "Θανάσης", "Βασίλης", "Χρήστος", "Αλέξανδρος",
                                    "Σπύρος", "Λευτέρης", "Γιάννης", "Στέλιος", "Μάνος", "Πέτρος",
                                    "Κυριάκος", "Ηλίας", "Τάσος", "Φώτης" };
            
            string[] lastNames = { "Παπαδόπουλος", "Νικολάου", "Αθανασίου", "Βασιλείου", "Γεωργίου", 
                                   "Χριστοδούλου", "Ιωάννου", "Κωνσταντίνου", "Δημητρίου", "Μιχαηλίδης",
                                   "Σταυρίδης", "Παύλου", "Αντωνίου", "Πετρίδης", "Μαρίνος", "Θεοδώρου",
                                   "Σαββίδης", "Φιλίππου", "Ανδρέου", "Χαραλάμπους", "Λουκά", "Χατζηγεωργίου" };
            
            int firstIdx = (teamName.GetHashCode() + index * 3) % firstNames.Length;
            int lastIdx = (teamName.GetHashCode() + index * 7) % lastNames.Length;
            
            return $"{firstNames[Math.Abs(firstIdx)]} {lastNames[Math.Abs(lastIdx)]}";
        }
        
        private static int GenerateStatForPosition(PlayerPosition position, string statName, Random random)
        {
            // Generate stats with position-appropriate ranges
            return position switch
            {
                PlayerPosition.Goalkeeper => statName switch
                {
                    "Speed" => random.Next(40, 50),
                    "Shooting" => random.Next(25, 35),
                    "Passing" => random.Next(55, 65),
                    "Defending" => random.Next(75, 90),
                    "Agility" => random.Next(65, 75),
                    "Technique" => random.Next(50, 60),
                    _ => 50
                },
                PlayerPosition.Defender => statName switch
                {
                    "Speed" => random.Next(48, 60),
                    "Shooting" => random.Next(30, 50),
                    "Passing" => random.Next(60, 75),
                    "Defending" => random.Next(70, 90),
                    "Agility" => random.Next(50, 65),
                    "Technique" => random.Next(55, 70),
                    _ => 50
                },
                PlayerPosition.Midfielder => statName switch
                {
                    "Speed" => random.Next(60, 75),
                    "Shooting" => random.Next(55, 75),
                    "Passing" => random.Next(70, 85),
                    "Defending" => random.Next(55, 70),
                    "Agility" => random.Next(65, 80),
                    "Technique" => random.Next(70, 85),
                    _ => 50
                },
                PlayerPosition.Forward => statName switch
                {
                    "Speed" => random.Next(70, 88),
                    "Shooting" => random.Next(75, 92),
                    "Passing" => random.Next(60, 80),
                    "Defending" => random.Next(30, 45),
                    "Agility" => random.Next(70, 90),
                    "Technique" => random.Next(70, 90),
                    _ => 50
                },
                _ => random.Next(40, 70)
            };
        }
    }
}
