using System.Collections.Generic;
using System.Linq;

namespace NoPasaranFC.Models
{
    public class Championship
    {
        public List<Team> Teams { get; set; }
        public List<Match> Matches { get; set; }
        public int CurrentMatchweek { get; set; }
        
        public Championship()
        {
            Teams = new List<Team>();
            Matches = new List<Match>();
            CurrentMatchweek = 0;
        }
        
        public void GenerateFixtures()
        {
            Matches.Clear();
            int teamCount = Teams.Count;
            
            // Generate round-robin fixtures
            for (int round = 0; round < (teamCount - 1) * 2; round++)
            {
                for (int i = 0; i < teamCount / 2; i++)
                {
                    int home = (round + i) % teamCount;
                    int away = (teamCount - 1 - i + round) % teamCount;
                    
                    if (round < teamCount - 1)
                        Matches.Add(new Match(Teams[home].Id, Teams[away].Id));
                    else
                        Matches.Add(new Match(Teams[away].Id, Teams[home].Id));
                }
            }
        }
        
        public List<Team> GetStandings()
        {
            return Teams.OrderByDescending(t => t.Points)
                       .ThenByDescending(t => t.GoalDifference)
                       .ThenByDescending(t => t.GoalsFor)
                       .ToList();
        }
    }
}
