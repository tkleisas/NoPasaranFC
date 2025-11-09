namespace NoPasaranFC.Models
{
    public class Match
    {
        public int Id { get; set; }
        public int HomeTeamId { get; set; }
        public int AwayTeamId { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public bool IsPlayed { get; set; }
        
        public Match(int homeTeamId, int awayTeamId)
        {
            HomeTeamId = homeTeamId;
            AwayTeamId = awayTeamId;
            HomeScore = 0;
            AwayScore = 0;
            IsPlayed = false;
        }
    }
}
