namespace NoPasaranFC.Models
{
    public class GameSettings
    {
        private static GameSettings _instance;
        
        public static GameSettings Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new GameSettings();
                }
                return _instance;
            }
        }
        
        public float PlayerSpeedMultiplier { get; set; } = 1.0f;
        public float MatchDurationMinutes { get; set; } = 3.0f; // Real-time duration in minutes
        
        public float GetMatchDurationSeconds()
        {
            return MatchDurationMinutes * 60f;
        }
        
        public float GetGameTimeFromRealTime(float realTimeElapsed, float totalRealTime)
        {
            return (realTimeElapsed / totalRealTime) * 90f;
        }
        
        private GameSettings()
        {
        }
    }
}
