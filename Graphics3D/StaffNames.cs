using System;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Humorous names for match staff (referee, linesmen, coaches), picked
    /// deterministically from football-pun Greek surnames. Coaches are stable
    /// per team (same manager every match); the officiating crew varies per fixture.
    /// </summary>
    public static class StaffNames
    {
        private static readonly string[] FirstNames =
        {
            "Τάσος", "Μήτσος", "Λάμπρος", "Κώστας", "Στέλιος", "Νίκος", "Μπάμπης", "Γιώργος"
        };

        private static readonly string[] RefereeLastNames =
        {
            "Σφυριχτόπουλος", // Whistleson
            "Καρτόπουλος",    // Cardsley
            "Πεναλτόπουλος",  // Penaltyson
            "Βαρβατσάκης",    // grumpy
        };

        private static readonly string[] LinesmanLastNames =
        {
            "Οφσαϊδέλης",   // Mr Offside
            "Σημαιάκης",    // little flag
            "Πλαγιόπουλος", // sideline-son
            "Κορνεράκης",   // corner
        };

        private static readonly string[] CoachLastNames =
        {
            "Τακτικάκης",   // tactical genius
            "Νευρικάκης",   // the nervous one
            "Φωνακλάς",     // the shouter
            "Καφέδης",      // sips coffee, unbothered
            "Στρατηγίδης",  // the strategist
            "Παγκοσμίου",   // self-proclaimed world-class
            "Μπριζόπουλος", // sizzler
            "Τρελοπάγκας",  // mad-dog
        };

        /// <summary>Referee name for a fixture (varies with the pairing).</summary>
        public static string Referee(int seed) =>
            Pick(FirstNames, seed) + " " + Pick(RefereeLastNames, seed * 31 + 7);

        /// <summary>Linesman name for a fixture (index 0/1 for the two of them).</summary>
        public static string Linesman(int seed) =>
            Pick(FirstNames, seed) + " " + Pick(LinesmanLastNames, seed * 31 + 13);

        /// <summary>Coach name for a team (stable per team - their recurring manager).</summary>
        public static string Coach(string teamName) =>
            Pick(FirstNames, StableHash(teamName)) + " " + Pick(CoachLastNames, StableHash(teamName) * 31 + 5);

        /// <summary>Fixture seed from both team names (deterministic crew per pairing).</summary>
        public static int FixtureSeed(string homeName, string awayName) =>
            StableHash(homeName + "|" + awayName);

        private static string Pick(string[] pool, int seed) => pool[Math.Abs(seed) % pool.Length];

        public static int StableHash(string s)
        {
            int h = 0;
            foreach (char c in s ?? "")
                h = h * 31 + c;
            return h & 0x7FFFFFFF;
        }
    }
}
