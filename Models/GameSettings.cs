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
        
        // Video settings
        public int ResolutionWidth { get; set; } = 1280;
        public int ResolutionHeight { get; set; } = 720;
        public bool IsFullscreen { get; set; } = false;
        public bool VSync { get; set; } = true;
        
        // Audio settings
        public float MasterVolume { get; set; } = 1.0f;
        public float MusicVolume { get; set; } = 0.7f;
        public float SfxVolume { get; set; } = 0.8f;
        public bool MuteAll { get; set; } = false;
        public bool MusicEnabled { get; set; } = true;
        public bool SfxEnabled { get; set; } = true;
        
        // Gameplay settings
        public int Difficulty { get; set; } = 1; // 0=Easy, 1=Normal, 2=Hard
        public float MatchDurationMinutes { get; set; } = 3.0f;
        public float PlayerSpeedMultiplier { get; set; } = 1.0f;
        public bool ShowMinimap { get; set; } = true;
        public bool ShowPlayerNames { get; set; } = true;
        public bool ShowStamina { get; set; } = true;
        
        // Camera settings
        public float CameraZoom { get; set; } = 0.8f;
        public float CameraSpeed { get; set; } = 0.1f;
        
        // Language settings
        public string Language { get; set; } = "en";
        
        public float GetMatchDurationSeconds()
        {
            return MatchDurationMinutes * 60f;
        }
        
        public float GetGameTimeFromRealTime(float realTimeElapsed, float totalRealTime)
        {
            return (realTimeElapsed / totalRealTime) * 90f;
        }
        
        public static void SetInstance(GameSettings settings)
        {
            _instance = settings;
        }
        
        private GameSettings()
        {
        }
        
        public GameSettings(bool dummy) // Public constructor for database loading
        {
        }
    }
}
