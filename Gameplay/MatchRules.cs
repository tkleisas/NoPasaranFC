using System;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Per-match pitch/ruleset. The engine and renderer both read the active
    /// rules (one match runs at a time), so the 11v11 championship and the
    /// 5v5 street mode can share the same simulation and rendering code.
    /// </summary>
    public sealed class MatchRules
    {
        public string Name { get; init; } = "Standard";
        public bool Street { get; init; }

        /// <summary>Street venue id: "Cage" or "Prosfygika". Resolved once per match.</summary>
        public string StreetVenue { get; init; } = "Cage";

        // Engine pixel dimensions (73 px = 1 m).
        public float FieldWidth { get; init; } = 7665f;   // 105 m
        public float FieldHeight { get; init; } = 4964f;  // 68 m
        public float StadiumMargin { get; init; } = 400f;
        public float GoalWidth { get; init; } = 534f;     // 7.32 m
        public float GoalDepth { get; init; } = 146f;     // 2 m
        public float GoalPostHeight { get; init; } = 178f; // 2.44 m

        public int TeamSize { get; init; } = 11;

        /// <summary>Honor GameSettings.OffsidesEnabled (street turns it off).</summary>
        public bool UseOffsideSetting { get; init; } = true;

        /// <summary>Cage walls reflect the ball instead of letting it leave the court.</summary>
        public bool WallBounce { get; init; }

        /// <summary>Out-of-play restarts are kicked rather than thrown.</summary>
        public bool KickIns { get; init; }

        /// <summary>Wall height in engine px; higher balls can clear it.</summary>
        public float WallHeight { get; init; }

        /// <summary>Scale for AI distance heuristics (pass/shoot/positioning ranges).</summary>
        public float AiDistanceScale { get; init; } = 1f;

        public static MatchRules ForChampionship(Championship championship)
            => championship != null && championship.IsStreet
                ? ForStreetVenue(GameSettings.Instance.StreetVenue)
                : Standard;

        /// <summary>Resolve the configured street venue once, so engine and renderer agree.</summary>
        public static MatchRules ForStreetVenue(string venueSetting)
        {
            switch ((venueSetting ?? "Prosfygika").ToLowerInvariant())
            {
                case "cage":
                    return Street5v5;
                case "random":
                    return new Random().Next(2) == 0 ? Street5v5 : Prosfygika5v5;
                default:
                    return Prosfygika5v5;
            }
        }

        public static MatchRules Standard { get; } = new MatchRules();

        /// <summary>Fenced asphalt cage: the ball rebounds off the walls.</summary>
        public static MatchRules Street5v5 { get; } = CreateStreet("Cage", wallBounce: true);

        /// <summary>Open Athens neighborhood dirt court: no cage; touchline out = kick-in.</summary>
        public static MatchRules Prosfygika5v5 { get; } = CreateStreet("Prosfygika", wallBounce: false);

        private static MatchRules CreateStreet(string venue, bool wallBounce) => new MatchRules
        {
            Name = "Antifa League Street",
            Street = true,
            StreetVenue = venue,
            FieldWidth = 3212f,   // 44 m
            FieldHeight = 1752f,  // 24 m
            StadiumMargin = 200f, // ~2.7 m apron
            GoalWidth = 219f,     // 3 m
            GoalDepth = 110f,     // 1.5 m
            GoalPostHeight = 146f,// 2 m
            TeamSize = 5,
            UseOffsideSetting = false,
            WallBounce = wallBounce,
            KickIns = true,
            WallHeight = wallBounce ? 219f : 0f,
            AiDistanceScale = 0.42f,
        };
    }
}
