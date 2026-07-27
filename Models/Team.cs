using System.Collections.Generic;

namespace NoPasaranFC.Models
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsPlayerControlled { get; set; }
        public string KitName { get; set; }
        public string Logo { get; set; }
        public List<Player> Players { get; set; }
        
        // Championship stats
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points => Wins * 3 + Draws;
        public int GoalDifference => GoalsFor - GoalsAgainst;

        // Celebration system
        public List<string> CelebrationIds { get; set; } // Team-specific celebration IDs (null/empty = use generic)

        // ---- Editable kit (editor + seed catalog; 0 = unset/legacy fallback) ----
        // Packed RGB (0xRRGGBB) colors for the outfield kit and the goalkeeper kit
        public int ShirtColor { get; set; }
        public int ShortsColor { get; set; }
        public int SocksColor { get; set; }
        public int GkShirtColor { get; set; }
        public int GkShortsColor { get; set; }
        public int GkSocksColor { get; set; }
        /// <summary>Shirt pattern: 0=Solid, 1=StripesV, 2=Hoops, 3=Halves, 4=Sash.</summary>
        public int ShirtPattern { get; set; }
        /// <summary>Secondary color for the shirt pattern (packed RGB).</summary>
        public int PatternColor { get; set; }
        /// <summary>Freehand paint over the shirt: 32x32 cells of 4-bit palette
        /// indices (hex string, 512 chars; 0 = empty cell). Null/empty = no paint.</summary>
        public string ShirtPaint { get; set; }

        public Team(string name, bool isPlayerControlled = false)
        {
            Name = name;
            IsPlayerControlled = isPlayerControlled;
            Players = new List<Player>();
            Wins = 0;
            Draws = 0;
            Losses = 0;
            GoalsFor = 0;
            GoalsAgainst = 0;
        }
        
        public void AddPlayer(Player player)
        {
            player.TeamId = Id;
            player.Team = this;
            Players.Add(player);
        }
    }
}
