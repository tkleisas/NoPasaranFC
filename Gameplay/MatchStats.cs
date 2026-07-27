using System.Collections.Generic;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay
{
    /// <summary>Per-player counters for a single match.</summary>
    public class PlayerMatchStats
    {
        public int Goals;
        public int Assists;
        public int Shots;
        public int ShotsOnTarget;
        public int Passes;
        public int PassesCompleted;
        public int Tackles;
        public int FoulsCommitted;
        public int FoulsSuffered;
        public int Saves;
        public int Offsides;
        public int YellowCards;
        public int RedCards;

        /// <summary>True when the player did anything noteworthy (table filtering).</summary>
        public bool HasActivity =>
            Goals + Assists + Shots + Passes + Tackles + Saves + FoulsCommitted +
            YellowCards + RedCards + Offsides > 0;
    }

    /// <summary>Per-team counters for a single match.</summary>
    public class TeamMatchStats
    {
        public float PossessionSeconds;
        public int Corners;
        public int ThrowIns;
        public int FreeKicks;
        public int Penalties;
        public int Offsides;
    }

    /// <summary>
    /// Match statistics collector owned by MatchEngine. Counters are plain public
    /// fields - the engine increments them at the event sites; screens read them.
    /// </summary>
    public class MatchStats
    {
        private readonly Dictionary<Player, PlayerMatchStats> _players = new Dictionary<Player, PlayerMatchStats>();
        private readonly Dictionary<Team, TeamMatchStats> _teams = new Dictionary<Team, TeamMatchStats>();

        public PlayerMatchStats For(Player player)
        {
            if (player == null) return _nullPlayer;
            if (!_players.TryGetValue(player, out var stats))
            {
                stats = new PlayerMatchStats();
                _players[player] = stats;
            }
            return stats;
        }

        public TeamMatchStats For(Team team)
        {
            if (team == null) return _nullTeam;
            if (!_teams.TryGetValue(team, out var stats))
            {
                stats = new TeamMatchStats();
                _teams[team] = stats;
            }
            return stats;
        }

        public IReadOnlyDictionary<Player, PlayerMatchStats> Players => _players;
        public IReadOnlyDictionary<Team, TeamMatchStats> Teams => _teams;

        // Shared null-objects so event sites never need null checks
        private static readonly PlayerMatchStats _nullPlayer = new PlayerMatchStats();
        private static readonly TeamMatchStats _nullTeam = new TeamMatchStats();
    }
}
