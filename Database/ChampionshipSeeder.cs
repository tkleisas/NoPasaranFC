using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;
using NoPasaranFC.Models;

namespace NoPasaranFC.Database
{
    /// <summary>
    /// Describes a championship available for selection: id, display name and
    /// the list of teams (with optional kit/logo overrides) that make up the league.
    /// </summary>
    public class ChampionshipDefinition
    {
        public string Id { get; set; }
        public string Name { get; set; }
        public string PlayerTeam { get; set; }
        public List<ChampionshipTeamEntry> Teams { get; set; }
    }

    public class ChampionshipTeamEntry
    {
        public string Name { get; set; }
        public string KitName { get; set; }
        public string Logo { get; set; }
    }

    public static class ChampionshipSeeder
    {
        private class ChampionshipSeedData
        {
            public List<ChampionshipDefinitionData> championships { get; set; }
        }

        private class ChampionshipDefinitionData
        {
            public string id { get; set; }
            public string name { get; set; }
            public string playerTeam { get; set; }
            public List<TeamEntryData> teams { get; set; }
        }

        private class TeamEntryData
        {
            public string name { get; set; }
            public string kitName { get; set; }
            public string logo { get; set; }
        }

        public static List<ChampionshipDefinition> LoadChampionshipsFromJson(string jsonPath)
        {
            string jsonString = File.ReadAllText(jsonPath, System.Text.Encoding.UTF8);
            return LoadChampionshipsFromJsonString(jsonString);
        }

        public static List<ChampionshipDefinition> LoadChampionshipsFromJsonString(string jsonString)
        {
            var result = new List<ChampionshipDefinition>();

            try
            {
                var seedData = JsonSerializer.Deserialize<ChampionshipSeedData>(jsonString);
                if (seedData?.championships == null)
                    return result;

                foreach (var def in seedData.championships)
                {
                    var championship = new ChampionshipDefinition
                    {
                        Id = def.id,
                        Name = def.name,
                        PlayerTeam = def.playerTeam,
                        Teams = new List<ChampionshipTeamEntry>()
                    };

                    if (def.teams != null)
                    {
                        foreach (var team in def.teams)
                        {
                            championship.Teams.Add(new ChampionshipTeamEntry
                            {
                                Name = team.name,
                                KitName = team.kitName,
                                Logo = team.logo
                            });
                        }
                    }

                    result.Add(championship);
                }
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error loading championships from JSON: {ex.Message}");
            }

            return result;
        }
    }
}
