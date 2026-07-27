namespace NoPasaranFC.Models
{
    /// <summary>
    /// Default packed-RGB (0xRRGGBB) kit colors per KitName, replicating the
    /// legacy hardcoded mapping that used to live in MatchRenderer3D.GetKitColors.
    /// Shared by the DB migration backfill, seed data, and the renderer fallback.
    /// </summary>
    public static class KitDefaults
    {
        public const int Black = 0x232328;
        public const int White = 0xF0F0F0;

        /// <summary>Outfield colors for a named kit; all 0 for unnamed kits
        /// (those keep the dynamic home-blue/away-red fallback).</summary>
        public static void ForKitName(string kitName, out int shirt, out int shorts, out int socks)
        {
            switch (kitName)
            {
                case "no_pasaran_kit": shirt = 0xE00000; shorts = White; socks = 0xE00000; break;
                case "asalagitos_kit": shirt = 0x8060A0; shorts = 0x322846; socks = 0x8060A0; break;
                case "asteras_exarcheion_kit": shirt = Black; shorts = Black; socks = Black; break;
                case "chandrinaikos_kit": shirt = 0x0040A0; shorts = White; socks = 0x0040A0; break;
                case "tiganitis_kit": shirt = White; shorts = Black; socks = 0xE0A000; break;
                default: shirt = 0; shorts = 0; socks = 0; break;
            }
        }
    }
}
