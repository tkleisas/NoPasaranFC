namespace NoPasaranFC.Models
{
    public static class Version
    {
        public const string MAJOR = "1";
        public const string MINOR = "0";
        public const string PATCH = "5";
        
        // This will be set at compile time by MSBuild
        private const string COMMIT_HASH = 
#if GITCOMMITHASH
            $"{GITCOMMITHASH}";
#else
            "unknown";
#endif
        
        public static string GetVersion()
        {
            return $"{MAJOR}.{MINOR}.{PATCH}";
        }
        
        public static string GetCommitHash()
        {
            return COMMIT_HASH;
        }
        
        public static string GetFullVersion()
        {
            if (COMMIT_HASH == "unknown")
                return $"v{GetVersion()}";
            return $"v{GetVersion()}-{COMMIT_HASH}";
        }
    }
}
