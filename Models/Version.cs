namespace NoPasaranFC.Models
{
    public static partial class Version
    {
        // MAJOR/MINOR/PATCH are generated from Directory.Build.props (Version.g.cs),
        // and COMMIT_HASH from the build's git revision (GitCommitHash.cs).
        
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
