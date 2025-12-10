namespace NoPasaranFC.Models
{
    public static partial class Version
    {
        public const string MAJOR = "1";
        public const string MINOR = "2";
        public const string PATCH = "2";
        
        // COMMIT_HASH is defined in the auto-generated GitCommitHash.cs file
        
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
            return COMMIT_HASH == "unknown" 
                ? $"v{GetVersion()}" 
                : $"v{GetVersion()}-{COMMIT_HASH}";
        }
    }
}
