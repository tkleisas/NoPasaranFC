using NoPasaranFC.Models;
using System;
using System.Collections.Generic;
using System.Linq;

namespace NoPasaranFC.Gameplay
{
    public static class MatchSimulator
    {
        private static Random _random = new Random();
        
        /// <summary>
        /// Simulates all unplayed matches in a given matchweek
        /// </summary>
        public static void SimulateMatchweek(Championship championship, int matchweek)
        {
            var matchesToSimulate = championship.GetMatchesForMatchweek(matchweek)
                .Where(m => !m.IsPlayed)
                .ToList();
            
            System.Diagnostics.Debug.WriteLine($"Simulating matchweek {matchweek}: {matchesToSimulate.Count} matches to simulate");
            
            foreach (var match in matchesToSimulate)
            {
                System.Diagnostics.Debug.WriteLine($"  - Simulating match: Team {match.HomeTeamId} vs Team {match.AwayTeamId}");
                SimulateMatch(championship, match);
            }
        }
        
        /// <summary>
        /// Simulates a single match between two teams
        /// </summary>
        public static void SimulateMatch(Championship championship, Match match)
        {
            var homeTeam = championship.Teams.Find(t => t.Id == match.HomeTeamId);
            var awayTeam = championship.Teams.Find(t => t.Id == match.AwayTeamId);
            
            if (homeTeam == null || awayTeam == null) return;
            
            // Calculate team strengths based on player stats
            double homeStrength = CalculateTeamStrength(homeTeam);
            double awayStrength = CalculateTeamStrength(awayTeam);
            
            // Home advantage
            homeStrength *= 1.15;
            
            // Simulate goals based on team strength
            int homeGoals = SimulateGoals(homeStrength, awayStrength);
            int awayGoals = SimulateGoals(awayStrength, homeStrength);
            
            // Update match result
            match.HomeScore = homeGoals;
            match.AwayScore = awayGoals;
            match.IsPlayed = true;
            
            // Update team statistics
            UpdateTeamStats(homeTeam, awayTeam, homeGoals, awayGoals);
        }
        
        private static double CalculateTeamStrength(Team team)
        {
            if (team.Players == null || team.Players.Count == 0)
                return 50.0; // Default strength
            
            // Average key stats of starting players
            var startingPlayers = team.Players.Where(p => p.IsStarting).ToList();
            if (startingPlayers.Count == 0)
                startingPlayers = team.Players.Take(11).ToList();
            
            double avgSpeed = startingPlayers.Average(p => p.Speed);
            double avgShooting = startingPlayers.Average(p => p.Shooting);
            double avgPassing = startingPlayers.Average(p => p.Passing);
            double avgDefending = startingPlayers.Average(p => p.Defending);
            
            // Weight different attributes
            return (avgSpeed * 0.2 + avgShooting * 0.3 + avgPassing * 0.25 + avgDefending * 0.25);
        }
        
        private static int SimulateGoals(double attackingStrength, double defendingStrength)
        {
            // Expected goals based on strength difference
            double strengthRatio = attackingStrength / Math.Max(defendingStrength, 1.0);
            double expectedGoals = strengthRatio * 1.5; // Base scaling
            
            // Clamp between 0 and 6 expected goals
            expectedGoals = Math.Max(0, Math.Min(6.0, expectedGoals));
            
            // Use Poisson distribution approximation
            int goals = 0;
            for (int i = 0; i < 10; i++)
            {
                if (_random.NextDouble() < (expectedGoals / 10.0))
                    goals++;
            }
            
            return Math.Min(goals, 8); // Cap at 8 goals
        }
        
        private static void UpdateTeamStats(Team homeTeam, Team awayTeam, int homeGoals, int awayGoals)
        {
            // Update home team
            homeTeam.GoalsFor += homeGoals;
            homeTeam.GoalsAgainst += awayGoals;
            
            if (homeGoals > awayGoals)
            {
                homeTeam.Wins++;
            }
            else if (homeGoals == awayGoals)
            {
                homeTeam.Draws++;
            }
            else
            {
                homeTeam.Losses++;
            }
            
            // Update away team
            awayTeam.GoalsFor += awayGoals;
            awayTeam.GoalsAgainst += homeGoals;
            
            if (awayGoals > homeGoals)
            {
                awayTeam.Wins++;
            }
            else if (awayGoals == homeGoals)
            {
                awayTeam.Draws++;
            }
            else
            {
                awayTeam.Losses++;
            }
        }
    }
}
