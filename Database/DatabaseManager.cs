using System;
using System.Collections.Generic;
using Microsoft.Data.Sqlite;
using NoPasaranFC.Models;

namespace NoPasaranFC.Database
{
    public class DatabaseManager
    {
        private static string GetDatabasePath()
        {
            // Use AppDomain.CurrentDomain.BaseDirectory to get consistent path
            // whether running from VS or command line
            var baseDir = AppDomain.CurrentDomain.BaseDirectory;
            return System.IO.Path.Combine(baseDir, "nopasaran.db");
        }
        
        private readonly string ConnectionString;
        
        public DatabaseManager()
        {
            ConnectionString = $"Data Source={GetDatabasePath()}";
            InitializeDatabase();
        }
        
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS Teams (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    IsPlayerControlled INTEGER NOT NULL,
                    Wins INTEGER DEFAULT 0,
                    Draws INTEGER DEFAULT 0,
                    Losses INTEGER DEFAULT 0,
                    GoalsFor INTEGER DEFAULT 0,
                    GoalsAgainst INTEGER DEFAULT 0
                );
                
                CREATE TABLE IF NOT EXISTS Players (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    TeamId INTEGER NOT NULL,
                    Name TEXT NOT NULL,
                    Position INTEGER NOT NULL,
                    Speed INTEGER DEFAULT 50,
                    Shooting INTEGER DEFAULT 50,
                    Passing INTEGER DEFAULT 50,
                    Defending INTEGER DEFAULT 50,
                    Agility INTEGER DEFAULT 50,
                    Technique INTEGER DEFAULT 50,
                    Stamina INTEGER DEFAULT 100,
                    FOREIGN KEY(TeamId) REFERENCES Teams(Id)
                );
                
                CREATE TABLE IF NOT EXISTS Matches (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    HomeTeamId INTEGER NOT NULL,
                    AwayTeamId INTEGER NOT NULL,
                    HomeScore INTEGER DEFAULT 0,
                    AwayScore INTEGER DEFAULT 0,
                    IsPlayed INTEGER DEFAULT 0,
                    FOREIGN KEY(HomeTeamId) REFERENCES Teams(Id),
                    FOREIGN KEY(AwayTeamId) REFERENCES Teams(Id)
                );
                
                CREATE TABLE IF NOT EXISTS Championship (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CurrentMatchweek INTEGER DEFAULT 0
                );
            ";
            command.ExecuteNonQuery();
        }
        
        public void SaveTeam(Team team)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            
            if (team.Id == 0)
            {
                // New team without ID - let database assign one
                command.CommandText = @"
                    INSERT INTO Teams (Name, IsPlayerControlled, Wins, Draws, Losses, GoalsFor, GoalsAgainst)
                    VALUES (@name, @isPlayerControlled, @wins, @draws, @losses, @goalsFor, @goalsAgainst);
                    SELECT last_insert_rowid();
                ";
                command.Parameters.AddWithValue("@name", team.Name);
                command.Parameters.AddWithValue("@isPlayerControlled", team.IsPlayerControlled ? 1 : 0);
                command.Parameters.AddWithValue("@wins", team.Wins);
                command.Parameters.AddWithValue("@draws", team.Draws);
                command.Parameters.AddWithValue("@losses", team.Losses);
                command.Parameters.AddWithValue("@goalsFor", team.GoalsFor);
                command.Parameters.AddWithValue("@goalsAgainst", team.GoalsAgainst);
                
                team.Id = Convert.ToInt32(command.ExecuteScalar());
            }
            else
            {
                // Team with ID - use INSERT OR REPLACE to handle both new and existing
                command.CommandText = @"
                    INSERT OR REPLACE INTO Teams (Id, Name, IsPlayerControlled, Wins, Draws, Losses, GoalsFor, GoalsAgainst)
                    VALUES (@id, @name, @isPlayerControlled, @wins, @draws, @losses, @goalsFor, @goalsAgainst);
                ";
                command.Parameters.AddWithValue("@id", team.Id);
                command.Parameters.AddWithValue("@name", team.Name);
                command.Parameters.AddWithValue("@isPlayerControlled", team.IsPlayerControlled ? 1 : 0);
                command.Parameters.AddWithValue("@wins", team.Wins);
                command.Parameters.AddWithValue("@draws", team.Draws);
                command.Parameters.AddWithValue("@losses", team.Losses);
                command.Parameters.AddWithValue("@goalsFor", team.GoalsFor);
                command.Parameters.AddWithValue("@goalsAgainst", team.GoalsAgainst);
                
                command.ExecuteNonQuery();
            }
        }
        
        public void SavePlayer(Player player)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            
            if (player.Id == 0)
            {
                // New player without ID
                command.CommandText = @"
                    INSERT INTO Players (TeamId, Name, Position, Speed, Shooting, Passing, Defending, Agility, Technique, Stamina)
                    VALUES (@teamId, @name, @position, @speed, @shooting, @passing, @defending, @agility, @technique, @stamina);
                    SELECT last_insert_rowid();
                ";
                command.Parameters.AddWithValue("@teamId", player.TeamId);
                command.Parameters.AddWithValue("@name", player.Name);
                command.Parameters.AddWithValue("@position", (int)player.Position);
                command.Parameters.AddWithValue("@speed", player.Speed);
                command.Parameters.AddWithValue("@shooting", player.Shooting);
                command.Parameters.AddWithValue("@passing", player.Passing);
                command.Parameters.AddWithValue("@defending", player.Defending);
                command.Parameters.AddWithValue("@agility", player.Agility);
                command.Parameters.AddWithValue("@technique", player.Technique);
                command.Parameters.AddWithValue("@stamina", player.Stamina);
                
                player.Id = Convert.ToInt32(command.ExecuteScalar());
            }
            else
            {
                // Player with ID - use INSERT OR REPLACE
                command.CommandText = @"
                    INSERT OR REPLACE INTO Players (Id, TeamId, Name, Position, Speed, Shooting, Passing, Defending, Agility, Technique, Stamina)
                    VALUES (@id, @teamId, @name, @position, @speed, @shooting, @passing, @defending, @agility, @technique, @stamina);
                ";
                command.Parameters.AddWithValue("@id", player.Id);
                command.Parameters.AddWithValue("@teamId", player.TeamId);
                command.Parameters.AddWithValue("@name", player.Name);
                command.Parameters.AddWithValue("@position", (int)player.Position);
                command.Parameters.AddWithValue("@speed", player.Speed);
                command.Parameters.AddWithValue("@shooting", player.Shooting);
                command.Parameters.AddWithValue("@passing", player.Passing);
                command.Parameters.AddWithValue("@defending", player.Defending);
                command.Parameters.AddWithValue("@agility", player.Agility);
                command.Parameters.AddWithValue("@technique", player.Technique);
                command.Parameters.AddWithValue("@stamina", player.Stamina);
                
                command.ExecuteNonQuery();
            }
        }
        
        public List<Team> LoadAllTeams()
        {
            var teams = new List<Team>();
            
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Teams";
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var team = new Team(reader.GetString(1), reader.GetInt32(2) == 1)
                {
                    Id = reader.GetInt32(0),
                    Wins = reader.GetInt32(3),
                    Draws = reader.GetInt32(4),
                    Losses = reader.GetInt32(5),
                    GoalsFor = reader.GetInt32(6),
                    GoalsAgainst = reader.GetInt32(7)
                };
                
                team.Players = LoadPlayersForTeam(team.Id);
                teams.Add(team);
            }
            
            return teams;
        }
        
        public List<Player> LoadPlayersForTeam(int teamId)
        {
            var players = new List<Player>();
            
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Players WHERE TeamId = @teamId";
            command.Parameters.AddWithValue("@teamId", teamId);
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var player = new Player
                {
                    Id = reader.GetInt32(0),
                    TeamId = reader.GetInt32(1),
                    Name = reader.GetString(2),
                    Position = (PlayerPosition)reader.GetInt32(3),
                    Speed = reader.GetInt32(4),
                    Shooting = reader.GetInt32(5),
                    Passing = reader.GetInt32(6),
                    Defending = reader.GetInt32(7),
                    Agility = reader.IsDBNull(8) ? 50 : reader.GetInt32(8),
                    Technique = reader.IsDBNull(9) ? 50 : reader.GetInt32(9),
                    Stamina = reader.IsDBNull(10) ? 100 : reader.GetInt32(10)
                };
                players.Add(player);
            }
            
            return players;
        }
        
        public void SaveMatch(Match match)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            
            if (match.Id == 0)
            {
                // New match without ID
                command.CommandText = @"
                    INSERT INTO Matches (HomeTeamId, AwayTeamId, HomeScore, AwayScore, IsPlayed)
                    VALUES (@homeTeamId, @awayTeamId, @homeScore, @awayScore, @isPlayed);
                    SELECT last_insert_rowid();
                ";
                command.Parameters.AddWithValue("@homeTeamId", match.HomeTeamId);
                command.Parameters.AddWithValue("@awayTeamId", match.AwayTeamId);
                command.Parameters.AddWithValue("@homeScore", match.HomeScore);
                command.Parameters.AddWithValue("@awayScore", match.AwayScore);
                command.Parameters.AddWithValue("@isPlayed", match.IsPlayed ? 1 : 0);
                
                match.Id = Convert.ToInt32(command.ExecuteScalar());
            }
            else
            {
                // Match with ID - use INSERT OR REPLACE
                command.CommandText = @"
                    INSERT OR REPLACE INTO Matches (Id, HomeTeamId, AwayTeamId, HomeScore, AwayScore, IsPlayed)
                    VALUES (@id, @homeTeamId, @awayTeamId, @homeScore, @awayScore, @isPlayed);
                ";
                command.Parameters.AddWithValue("@id", match.Id);
                command.Parameters.AddWithValue("@homeTeamId", match.HomeTeamId);
                command.Parameters.AddWithValue("@awayTeamId", match.AwayTeamId);
                command.Parameters.AddWithValue("@homeScore", match.HomeScore);
                command.Parameters.AddWithValue("@awayScore", match.AwayScore);
                command.Parameters.AddWithValue("@isPlayed", match.IsPlayed ? 1 : 0);
                
                command.ExecuteNonQuery();
            }
        }
        
        public List<Match> LoadAllMatches()
        {
            var matches = new List<Match>();
            
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Matches";
            
            using var reader = command.ExecuteReader();
            while (reader.Read())
            {
                var match = new Match(reader.GetInt32(1), reader.GetInt32(2))
                {
                    Id = reader.GetInt32(0),
                    HomeScore = reader.GetInt32(3),
                    AwayScore = reader.GetInt32(4),
                    IsPlayed = reader.GetInt32(5) == 1
                };
                matches.Add(match);
            }
            
            return matches;
        }
        
        public void SaveChampionship(Championship championship)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            // Save current matchweek
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Championship (Id, CurrentMatchweek)
                VALUES (1, @currentMatchweek);
            ";
            command.Parameters.AddWithValue("@currentMatchweek", championship.CurrentMatchweek);
            command.ExecuteNonQuery();
            
            // Save all teams
            foreach (var team in championship.Teams)
            {
                SaveTeam(team);
                foreach (var player in team.Players)
                {
                    SavePlayer(player);
                }
            }
            
            // Save all matches
            foreach (var match in championship.Matches)
            {
                SaveMatch(match);
            }
        }
        
        public Championship LoadChampionship()
        {
            var championship = new Championship();
            
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            // Load current matchweek
            var command = connection.CreateCommand();
            command.CommandText = "SELECT CurrentMatchweek FROM Championship WHERE Id = 1";
            var result = command.ExecuteScalar();
            if (result != null)
            {
                championship.CurrentMatchweek = Convert.ToInt32(result);
            }
            
            // Load teams and matches
            championship.Teams = LoadAllTeams();
            championship.Matches = LoadAllMatches();
            
            return championship;
        }
        
        public void ClearDatabase()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                DELETE FROM Players;
                DELETE FROM Matches;
                DELETE FROM Teams;
                DELETE FROM Championship;
            ";
            command.ExecuteNonQuery();
        }
    }
}
