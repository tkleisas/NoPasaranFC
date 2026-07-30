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
            public string kitName { get; set; }
            public string logo { get; set; }
            public List<string> celebrationIds { get; set; }
            public KitData kit { get; set; }
        }

        /// <summary>Optional editable kit block (packed RGB ints; null = unset).</summary>
        private class KitData
        {
            public int? shirtColor { get; set; }
            public int? shortsColor { get; set; }
            public int? socksColor { get; set; }
            public int? gkShirtColor { get; set; }
            public int? gkShortsColor { get; set; }
            public int? gkSocksColor { get; set; }
            public int? shirtPattern { get; set; }
            public int? patternColor { get; set; }
            public string shirtPaint { get; set; }
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
            public List<string> celebrationIds { get; set; }
            // Appearance overrides (null = auto from hash)
            public int? gender { get; set; }
            public int? skinTone { get; set; }
            public int? hairColor { get; set; }
            public int? expression { get; set; }
            public int? feature { get; set; }
        }
        
        public static List<Team> LoadTeamsFromJson(string jsonPath)
        {
            string jsonString = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
            return LoadTeamsFromJsonString(jsonString);
        }
        
        public static List<Team> LoadTeamsFromJsonString(string jsonString)
        {
            var teams = new List<Team>();
            
            try
            {
                var seedData = JsonSerializer.Deserialize<TeamSeedData>(jsonString);
                
                foreach (var teamData in seedData.teams)
                {
                    var team = new Team(teamData.name, teamData.isPlayerControlled);
                    team.KitName = teamData.kitName;
                    team.Logo = teamData.logo;
                    team.CelebrationIds = teamData.celebrationIds;

                    if (teamData.kit != null)
                    {
                        team.ShirtColor = teamData.kit.shirtColor ?? 0;
                        team.ShortsColor = teamData.kit.shortsColor ?? 0;
                        team.SocksColor = teamData.kit.socksColor ?? 0;
                        team.GkShirtColor = teamData.kit.gkShirtColor ?? 0;
                        team.GkShortsColor = teamData.kit.gkShortsColor ?? 0;
                        team.GkSocksColor = teamData.kit.gkSocksColor ?? 0;
                        team.ShirtPattern = teamData.kit.shirtPattern ?? 0;
                        team.PatternColor = teamData.kit.patternColor ?? 0;
                        team.ShirtPaint = teamData.kit.shirtPaint;
                    }

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
                                Stamina = playerData.stamina,
                                CelebrationIds = playerData.celebrationIds,
                                GenderOverride = playerData.gender ?? -1,
                                SkinToneOverride = playerData.skinTone ?? -1,
                                HairColorOverride = playerData.hairColor ?? -1,
                                ExpressionOverride = playerData.expression ?? -1,
                                FeatureOverride = playerData.feature ?? -1
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
        
        /// <summary>
        /// Loads the shared team catalog: the base seed plus an optional overlay
        /// (teams_seed.custom.json) merged by team name - the overlay wins, so
        /// editor customizations become the defaults for new seasons.
        /// </summary>
        public static List<Team> LoadCatalog(string basePath, string overlayPath = null)
        {
            var teams = LoadTeamsFromJson(basePath);
            if (overlayPath != null && File.Exists(overlayPath))
            {
                var overlay = LoadTeamsFromJson(overlayPath);
                foreach (var overlayTeam in overlay)
                {
                    int idx = teams.FindIndex(t =>
                        string.Equals(t.Name, overlayTeam.Name, StringComparison.OrdinalIgnoreCase));
                    if (idx >= 0) teams[idx] = overlayTeam;
                    else teams.Add(overlayTeam);
                }
            }
            return teams;
        }

        /// <summary>
        /// Serializes teams back to the teams_seed.json schema (UTF-8, Greek names
        /// intact, indented). Used for the editor's write-through overlay and EXPORT.
        /// </summary>
        public static void SaveCatalog(List<Team> teams, string path)
        {
            var data = new TeamSeedData { teams = new List<TeamData>() };
            foreach (var team in teams)
            {
                var td = new TeamData
                {
                    name = team.Name,
                    isPlayerControlled = team.IsPlayerControlled,
                    kitName = team.KitName,
                    logo = team.Logo,
                    celebrationIds = team.CelebrationIds,
                    players = new List<PlayerData>()
                };
                if (team.ShirtColor != 0 || team.ShirtPattern != 0 || !string.IsNullOrEmpty(team.ShirtPaint))
                {
                    td.kit = new KitData
                    {
                        shirtColor = team.ShirtColor,
                        shortsColor = team.ShortsColor,
                        socksColor = team.SocksColor,
                        gkShirtColor = team.GkShirtColor,
                        gkShortsColor = team.GkShortsColor,
                        gkSocksColor = team.GkSocksColor,
                        shirtPattern = team.ShirtPattern,
                        patternColor = team.PatternColor,
                        shirtPaint = team.ShirtPaint
                    };
                }
                foreach (var p in team.Players)
                {
                    td.players.Add(new PlayerData
                    {
                        name = p.Name,
                        position = p.Position.ToString(),
                        shirtNumber = p.ShirtNumber,
                        isStarting = p.IsStarting,
                        speed = p.Speed,
                        shooting = p.Shooting,
                        passing = p.Passing,
                        defending = p.Defending,
                        agility = p.Agility,
                        technique = p.Technique,
                        stamina = (int)p.Stamina,
                        celebrationIds = p.CelebrationIds,
                        gender = p.GenderOverride >= 0 ? p.GenderOverride : null,
                        skinTone = p.SkinToneOverride >= 0 ? p.SkinToneOverride : null,
                        hairColor = p.HairColorOverride >= 0 ? p.HairColorOverride : null,
                        expression = p.ExpressionOverride >= 0 ? p.ExpressionOverride : null,
                        feature = p.FeatureOverride >= 0 ? p.FeatureOverride : null
                    });
                }
                data.teams.Add(td);
            }

            var options = new JsonSerializerOptions
            {
                WriteIndented = true,
                Encoder = System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping,
                DefaultIgnoreCondition = System.Text.Json.Serialization.JsonIgnoreCondition.WhenWritingNull
            };
            File.WriteAllText(path, JsonSerializer.Serialize(data, options),
                new System.Text.UTF8Encoding(false));
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
        
        /// <summary>
        /// Create a new team with a procedurally-generated roster.
        /// Used for championship teams that aren't present in teams_seed.json.
        /// </summary>
        public static Team CreateTeamWithDefaultRoster(string name, bool isPlayerControlled, string kitName = null, string logo = null)
        {
            var team = new Team(name, isPlayerControlled)
            {
                KitName = kitName ?? string.Empty,
                Logo = logo ?? string.Empty
            };
            GenerateDefaultRoster(team);
            return team;
        }

        /// <summary>
        /// When set (headless harness), default rosters are generated from this seed plus a
        /// stable hash of the team name instead of the process-randomized string.GetHashCode().
        /// Null keeps the original behavior.
        /// </summary>
        public static int? DeterministicRosterSeed = null;

        private static void GenerateDefaultRoster(Team team, int rosterSize = 25)
        {
            var random = new Random(DeterministicRosterSeed.HasValue
                ? DeterministicRosterSeed.Value + StableNameHash(team.Name)
                : team.Name.GetHashCode()); // Consistent generation per team
            
            // Generate players with varied positions (default 25)
            // Distribute positions: ~2 GK, ~8 DEF, ~10 MID, ~5 FWD (for 25 players)
            int gkCount = Math.Max(2, rosterSize / 12);
            int defCount = Math.Max(4, rosterSize / 3);
            int midCount = Math.Max(4, rosterSize / 2 - defCount);
            int fwdCount = Math.Max(2, rosterSize - gkCount - defCount - midCount);
            
            int[] positionCounts = { gkCount, defCount, midCount, fwdCount };
            int shirtNumber = 1;
            int startingCount = 0;
            int playerIndex = 0;
            var usedNames = new HashSet<string>();
            
            for (int posIdx = 0; posIdx < 4; posIdx++)
            {
                var position = (PlayerPosition)posIdx;
                int count = positionCounts[posIdx];
                
                for (int i = 0; i < count; i++)
                {
                    string playerName = GeneratePlayerName(team.Name, playerIndex++, usedNames);
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
        
        /// <summary>Stable (process-independent) FNV-1a hash, for deterministic harness runs.</summary>
        internal static int StableNameHash(string s)
        {
            unchecked
            {
                uint hash = 2166136261u;
                foreach (char c in s)
                {
                    hash = (hash ^ c) * 16777619u;
                }
                return (int)hash;
            }
        }

        private static string GeneratePlayerName(string teamName, int index, HashSet<string> usedNames)
        {
            string[] firstNames = { "Κώστας", "Γιώργος", "Δημήτρης", "Νίκος", "Μιχάλης", "Σωτήρης",
                                    "Ανδρέας", "Παναγιώτης", "Θανάσης", "Βασίλης", "Χρήστος", "Αλέξανδρος",
                                    "Σπύρος", "Λευτέρης", "Γιάννης", "Στέλιος", "Μάνος", "Πέτρος",
                                    "Κυριάκος", "Ηλίας", "Τάσος", "Φώτης" };

            string[] lastNames = { "Παπαδόπουλος", "Νικολάου", "Αθανασίου", "Βασιλείου", "Γεωργίου",
                                   "Χριστοδούλου", "Ιωάννου", "Κωνσταντίνου", "Δημητρίου", "Μιχαηλίδης",
                                   "Σταυρίδης", "Παύλου", "Αντωνίου", "Πετρίδης", "Μαρίνος", "Θεοδώρου",
                                   "Σαββίδης", "Φιλίππου", "Ανδρέου", "Χαραλάμπους", "Λουκά", "Χατζηγεωργίου" };

            int nameHash = DeterministicRosterSeed.HasValue ? StableNameHash(teamName) : teamName.GetHashCode();
            int firstIdx = Math.Abs(nameHash + index * 3) % firstNames.Length;
            int lastIdx = Math.Abs(nameHash + index * 7) % lastNames.Length;

            // Guarantee uniqueness within the roster: the deterministic pick
            // collides across position groups (per-position indices) and for
            // rosters larger than the pool period. Walk the pools (deterministically)
            // until an unused combination is found.
            string name;
            int guard = 0;
            do
            {
                name = $"{firstNames[firstIdx]} {lastNames[lastIdx]}";
                lastIdx = (lastIdx + 1) % lastNames.Length;
                if (++guard % lastNames.Length == 0)
                    firstIdx = (firstIdx + 1) % firstNames.Length;
            } while (!usedNames.Add(name) && guard < firstNames.Length * lastNames.Length);

            return name;
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
