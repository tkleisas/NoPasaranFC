using System.Collections.Generic;

namespace NoPasaranFC.Models
{
    public class Team
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public bool IsPlayerControlled { get; set; }
        public List<Player> Players { get; set; }
        
        // Championship stats
        public int Wins { get; set; }
        public int Draws { get; set; }
        public int Losses { get; set; }
        public int GoalsFor { get; set; }
        public int GoalsAgainst { get; set; }
        public int Points => Wins * 3 + Draws;
        public int GoalDifference => GoalsFor - GoalsAgainst;
        
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
            Players.Add(player);
        }
    }
}
