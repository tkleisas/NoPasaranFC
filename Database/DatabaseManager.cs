using System;
using System.Collections.Generic;
using System.Text.Json;
using Microsoft.Data.Sqlite;
using NoPasaranFC.Models;

namespace NoPasaranFC.Database
{
    public class DatabaseManager
    {
        private const int CurrentSchemaVersion = 2; // Increment when adding migrations
        
        private static string GetDatabasePath()
        {
            // Use platform-specific path for cross-platform compatibility
            return PlatformHelper.GetDatabasePath();
        }
        
        private readonly string ConnectionString;
        
        public DatabaseManager()
        {
            // Initialize SQLite for the current platform (required for Android)
            PlatformHelper.InitializeSQLite();
            
            ConnectionString = $"Data Source={GetDatabasePath()};Mode=ReadWriteCreate;";
            InitializeDatabase();
            RunMigrations();
        }
        
        private void InitializeDatabase()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            // Enable UTF-8 encoding
            var pragmaCommand = connection.CreateCommand();
            pragmaCommand.CommandText = "PRAGMA encoding = 'UTF-8';";
            pragmaCommand.ExecuteNonQuery();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                CREATE TABLE IF NOT EXISTS SchemaVersion (
                    Version INTEGER PRIMARY KEY
                );
                
                CREATE TABLE IF NOT EXISTS Teams (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    Name TEXT NOT NULL,
                    IsPlayerControlled INTEGER NOT NULL,
                    Wins INTEGER DEFAULT 0,
                    Draws INTEGER DEFAULT 0,
                    Losses INTEGER DEFAULT 0,
                    GoalsFor INTEGER DEFAULT 0,
                    GoalsAgainst INTEGER DEFAULT 0,
                    KitName TEXT,
                    Logo TEXT,
                    CelebrationIds TEXT
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
                    IsStarting INTEGER DEFAULT 0,
                    ShirtNumber INTEGER DEFAULT 0,
                    CelebrationIds TEXT,
                    FOREIGN KEY(TeamId) REFERENCES Teams(Id)
                );
                
                CREATE TABLE IF NOT EXISTS Matches (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    HomeTeamId INTEGER NOT NULL,
                    AwayTeamId INTEGER NOT NULL,
                    HomeScore INTEGER DEFAULT 0,
                    AwayScore INTEGER DEFAULT 0,
                    IsPlayed INTEGER DEFAULT 0,
                    Matchweek INTEGER DEFAULT 0,
                    FOREIGN KEY(HomeTeamId) REFERENCES Teams(Id),
                    FOREIGN KEY(AwayTeamId) REFERENCES Teams(Id)
                );
                
                CREATE TABLE IF NOT EXISTS Championship (
                    Id INTEGER PRIMARY KEY AUTOINCREMENT,
                    CurrentMatchweek INTEGER DEFAULT 0
                );
                
                CREATE TABLE IF NOT EXISTS Settings (
                    Id INTEGER PRIMARY KEY CHECK (Id = 1),
                    ResolutionWidth INTEGER DEFAULT 1280,
                    ResolutionHeight INTEGER DEFAULT 720,
                    IsFullscreen INTEGER DEFAULT 0,
                    VSync INTEGER DEFAULT 1,
                    MasterVolume REAL DEFAULT 1.0,
                    MusicVolume REAL DEFAULT 0.7,
                    SfxVolume REAL DEFAULT 0.8,
                    MuteAll INTEGER DEFAULT 0,
                    Difficulty INTEGER DEFAULT 1,
                    MatchDurationMinutes REAL DEFAULT 3.0,
                    PlayerSpeedMultiplier REAL DEFAULT 1.0,
                    ShowMinimap INTEGER DEFAULT 1,
                    ShowPlayerNames INTEGER DEFAULT 1,
                    ShowStamina INTEGER DEFAULT 1,
                    CameraZoom REAL DEFAULT 0.8,
                    CameraSpeed REAL DEFAULT 0.1,
                    Language TEXT DEFAULT 'en'
                );
            ";
            command.ExecuteNonQuery();
        }
        
        private void RunMigrations()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            int currentVersion = GetSchemaVersion(connection);
            
            // Apply migrations in order
            if (currentVersion < 1)
            {
                // Migration 1: Initial schema (already created by InitializeDatabase)
                SetSchemaVersion(connection, 1);
                currentVersion = 1;
            }
            
            if (currentVersion < 2)
            {
                // Migration 2: Add PlayerPicture column to Players table
                ApplyMigration2_AddPlayerPicture(connection);
                SetSchemaVersion(connection, 2);
                currentVersion = 2;
            }
            
            // Add future migrations here:
            // if (currentVersion < 3) { ... }
        }
        
        private int GetSchemaVersion(SqliteConnection connection)
        {
            var command = connection.CreateCommand();
            command.CommandText = "SELECT MAX(Version) FROM SchemaVersion";
            
            try
            {
                var result = command.ExecuteScalar();
                if (result == null || result == DBNull.Value)
                    return 0;
                return Convert.ToInt32(result);
            }
            catch
            {
                return 0;
            }
        }
        
        private void SetSchemaVersion(SqliteConnection connection, int version)
        {
            var command = connection.CreateCommand();
            command.CommandText = "INSERT OR REPLACE INTO SchemaVersion (Version) VALUES (@version)";
            command.Parameters.AddWithValue("@version", version);
            command.ExecuteNonQuery();
        }
        
        private void ApplyMigration2_AddPlayerPicture(SqliteConnection connection)
        {
            // Check if column already exists
            var checkCommand = connection.CreateCommand();
            checkCommand.CommandText = "PRAGMA table_info(Players)";
            
            bool columnExists = false;
            using (var reader = checkCommand.ExecuteReader())
            {
                while (reader.Read())
                {
                    if (reader.GetString(1) == "PlayerPicture")
                    {
                        columnExists = true;
                        break;
                    }
                }
            }
            
            if (!columnExists)
            {
                var alterCommand = connection.CreateCommand();
                alterCommand.CommandText = "ALTER TABLE Players ADD COLUMN PlayerPicture TEXT";
                alterCommand.ExecuteNonQuery();
            }
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
                    INSERT INTO Teams (Name, IsPlayerControlled, Wins, Draws, Losses, GoalsFor, GoalsAgainst, KitName, Logo, CelebrationIds)
                    VALUES (@name, @isPlayerControlled, @wins, @draws, @losses, @goalsFor, @goalsAgainst, @kitName, @logo, @celebrationIds);
                    SELECT last_insert_rowid();
                ";
                command.Parameters.AddWithValue("@name", team.Name);
                command.Parameters.AddWithValue("@isPlayerControlled", team.IsPlayerControlled ? 1 : 0);
                command.Parameters.AddWithValue("@kitName", team.KitName ?? string.Empty);
                command.Parameters.AddWithValue("@logo", team.Logo ?? string.Empty);
                command.Parameters.AddWithValue("@wins", team.Wins);
                command.Parameters.AddWithValue("@draws", team.Draws);
                command.Parameters.AddWithValue("@losses", team.Losses);
                command.Parameters.AddWithValue("@goalsFor", team.GoalsFor);
                command.Parameters.AddWithValue("@goalsAgainst", team.GoalsAgainst);
                command.Parameters.AddWithValue("@celebrationIds",
                    team.CelebrationIds != null ? JsonSerializer.Serialize(team.CelebrationIds) : (object)DBNull.Value);

                team.Id = Convert.ToInt32(command.ExecuteScalar());
            }
            else
            {
                // Team with ID - use INSERT OR REPLACE to handle both new and existing
                command.CommandText = @"
                    INSERT OR REPLACE INTO Teams (Id, Name, IsPlayerControlled, Wins, Draws, Losses, GoalsFor, GoalsAgainst, KitName, Logo, CelebrationIds)
                    VALUES (@id, @name, @isPlayerControlled, @wins, @draws, @losses, @goalsFor, @goalsAgainst, @kitName, @logo, @celebrationIds);
                ";
                command.Parameters.AddWithValue("@id", team.Id);
                command.Parameters.AddWithValue("@name", team.Name);
                command.Parameters.AddWithValue("@isPlayerControlled", team.IsPlayerControlled ? 1 : 0);
                command.Parameters.AddWithValue("@wins", team.Wins);
                command.Parameters.AddWithValue("@draws", team.Draws);
                command.Parameters.AddWithValue("@losses", team.Losses);
                command.Parameters.AddWithValue("@goalsFor", team.GoalsFor);
                command.Parameters.AddWithValue("@goalsAgainst", team.GoalsAgainst);
                command.Parameters.AddWithValue("@kitName", team.KitName ?? string.Empty);
                command.Parameters.AddWithValue("@logo", team.Logo ?? string.Empty);
                command.Parameters.AddWithValue("@celebrationIds",
                    team.CelebrationIds != null ? JsonSerializer.Serialize(team.CelebrationIds) : (object)DBNull.Value);
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
                    INSERT INTO Players (TeamId, Name, Position, Speed, Shooting, Passing, Defending, Agility, Technique, Stamina, IsStarting, ShirtNumber, CelebrationIds, PlayerPicture)
                    VALUES (@teamId, @name, @position, @speed, @shooting, @passing, @defending, @agility, @technique, @stamina, @isStarting, @shirtNumber, @celebrationIds, @playerPicture);
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
                command.Parameters.AddWithValue("@isStarting", player.IsStarting ? 1 : 0);
                command.Parameters.AddWithValue("@shirtNumber", player.ShirtNumber);
                command.Parameters.AddWithValue("@celebrationIds",
                    player.CelebrationIds != null ? JsonSerializer.Serialize(player.CelebrationIds) : (object)DBNull.Value);
                command.Parameters.AddWithValue("@playerPicture", 
                    string.IsNullOrEmpty(player.PlayerPicture) ? (object)DBNull.Value : player.PlayerPicture);

                player.Id = Convert.ToInt32(command.ExecuteScalar());
            }
            else
            {
                // Player with ID - use INSERT OR REPLACE
                command.CommandText = @"
                    INSERT OR REPLACE INTO Players (Id, TeamId, Name, Position, Speed, Shooting, Passing, Defending, Agility, Technique, Stamina, IsStarting, ShirtNumber, CelebrationIds, PlayerPicture)
                    VALUES (@id, @teamId, @name, @position, @speed, @shooting, @passing, @defending, @agility, @technique, @stamina, @isStarting, @shirtNumber, @celebrationIds, @playerPicture);
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
                command.Parameters.AddWithValue("@isStarting", player.IsStarting ? 1 : 0);
                command.Parameters.AddWithValue("@shirtNumber", player.ShirtNumber);
                command.Parameters.AddWithValue("@celebrationIds",
                    player.CelebrationIds != null ? JsonSerializer.Serialize(player.CelebrationIds) : (object)DBNull.Value);
                command.Parameters.AddWithValue("@playerPicture", 
                    string.IsNullOrEmpty(player.PlayerPicture) ? (object)DBNull.Value : player.PlayerPicture);

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
                    GoalsAgainst = reader.GetInt32(7),
                    KitName = reader.IsDBNull(8) ? string.Empty : reader.GetString(8),
                    Logo = reader.IsDBNull(9) ? string.Empty : reader.GetString(9),
                    CelebrationIds = reader.IsDBNull(10) ? null :
                        JsonSerializer.Deserialize<List<string>>(reader.GetString(10))
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
                    Stamina = reader.IsDBNull(10) ? 100 : reader.GetInt32(10),
                    IsStarting = reader.IsDBNull(11) ? false : reader.GetInt32(11) == 1,
                    ShirtNumber = reader.IsDBNull(12) ? 0 : reader.GetInt32(12),
                    CelebrationIds = reader.IsDBNull(13) ? null :
                        JsonSerializer.Deserialize<List<string>>(reader.GetString(13)),
                    PlayerPicture = reader.FieldCount > 14 && !reader.IsDBNull(14) ? reader.GetString(14) : null
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
                    INSERT INTO Matches (HomeTeamId, AwayTeamId, HomeScore, AwayScore, IsPlayed, Matchweek)
                    VALUES (@homeTeamId, @awayTeamId, @homeScore, @awayScore, @isPlayed, @matchweek);
                    SELECT last_insert_rowid();
                ";
                command.Parameters.AddWithValue("@homeTeamId", match.HomeTeamId);
                command.Parameters.AddWithValue("@awayTeamId", match.AwayTeamId);
                command.Parameters.AddWithValue("@homeScore", match.HomeScore);
                command.Parameters.AddWithValue("@awayScore", match.AwayScore);
                command.Parameters.AddWithValue("@isPlayed", match.IsPlayed ? 1 : 0);
                command.Parameters.AddWithValue("@matchweek", match.Matchweek);
                
                match.Id = Convert.ToInt32(command.ExecuteScalar());
            }
            else
            {
                // Match with ID - use INSERT OR REPLACE
                command.CommandText = @"
                    INSERT OR REPLACE INTO Matches (Id, HomeTeamId, AwayTeamId, HomeScore, AwayScore, IsPlayed, Matchweek)
                    VALUES (@id, @homeTeamId, @awayTeamId, @homeScore, @awayScore, @isPlayed, @matchweek);
                ";
                command.Parameters.AddWithValue("@id", match.Id);
                command.Parameters.AddWithValue("@homeTeamId", match.HomeTeamId);
                command.Parameters.AddWithValue("@awayTeamId", match.AwayTeamId);
                command.Parameters.AddWithValue("@homeScore", match.HomeScore);
                command.Parameters.AddWithValue("@awayScore", match.AwayScore);
                command.Parameters.AddWithValue("@isPlayed", match.IsPlayed ? 1 : 0);
                command.Parameters.AddWithValue("@matchweek", match.Matchweek);
                
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
                    IsPlayed = reader.GetInt32(5) == 1,
                    Matchweek = reader.GetInt32(6)
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
        
        public void SaveSettings(GameSettings settings)
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = @"
                INSERT OR REPLACE INTO Settings (
                    Id, ResolutionWidth, ResolutionHeight, IsFullscreen, VSync,
                    MasterVolume, MusicVolume, SfxVolume, MuteAll,
                    Difficulty, MatchDurationMinutes, PlayerSpeedMultiplier,
                    ShowMinimap, ShowPlayerNames, ShowStamina,
                    CameraZoom, CameraSpeed, Language
                ) VALUES (
                    1, @resWidth, @resHeight, @fullscreen, @vsync,
                    @masterVol, @musicVol, @sfxVol, @muteAll,
                    @difficulty, @matchDuration, @speedMulti,
                    @showMap, @showNames, @showStamina,
                    @camZoom, @camSpeed, @language
                );
            ";
            
            command.Parameters.AddWithValue("@resWidth", settings.ResolutionWidth);
            command.Parameters.AddWithValue("@resHeight", settings.ResolutionHeight);
            command.Parameters.AddWithValue("@fullscreen", settings.IsFullscreen ? 1 : 0);
            command.Parameters.AddWithValue("@vsync", settings.VSync ? 1 : 0);
            command.Parameters.AddWithValue("@masterVol", settings.MasterVolume);
            command.Parameters.AddWithValue("@musicVol", settings.MusicVolume);
            command.Parameters.AddWithValue("@sfxVol", settings.SfxVolume);
            command.Parameters.AddWithValue("@muteAll", settings.MuteAll ? 1 : 0);
            command.Parameters.AddWithValue("@difficulty", settings.Difficulty);
            command.Parameters.AddWithValue("@matchDuration", settings.MatchDurationMinutes);
            command.Parameters.AddWithValue("@speedMulti", settings.PlayerSpeedMultiplier);
            command.Parameters.AddWithValue("@showMap", settings.ShowMinimap ? 1 : 0);
            command.Parameters.AddWithValue("@showNames", settings.ShowPlayerNames ? 1 : 0);
            command.Parameters.AddWithValue("@showStamina", settings.ShowStamina ? 1 : 0);
            command.Parameters.AddWithValue("@camZoom", settings.CameraZoom);
            command.Parameters.AddWithValue("@camSpeed", settings.CameraSpeed);
            command.Parameters.AddWithValue("@language", settings.Language);
            
            command.ExecuteNonQuery();
        }
        
        public GameSettings LoadSettings()
        {
            using var connection = new SqliteConnection(ConnectionString);
            connection.Open();
            
            var command = connection.CreateCommand();
            command.CommandText = "SELECT * FROM Settings WHERE Id = 1";
            
            using var reader = command.ExecuteReader();
            if (reader.Read())
            {
                var settings = new GameSettings(true)
                {
                    ResolutionWidth = reader.GetInt32(1),
                    ResolutionHeight = reader.GetInt32(2),
                    IsFullscreen = reader.GetInt32(3) == 1,
                    VSync = reader.GetInt32(4) == 1,
                    MasterVolume = reader.GetFloat(5),
                    MusicVolume = reader.GetFloat(6),
                    SfxVolume = reader.GetFloat(7),
                    MuteAll = reader.GetInt32(8) == 1,
                    Difficulty = reader.GetInt32(9),
                    MatchDurationMinutes = reader.GetFloat(10),
                    PlayerSpeedMultiplier = reader.GetFloat(11),
                    ShowMinimap = reader.GetInt32(12) == 1,
                    ShowPlayerNames = reader.GetInt32(13) == 1,
                    ShowStamina = reader.GetInt32(14) == 1,
                    CameraZoom = reader.GetFloat(15),
                    CameraSpeed = reader.GetFloat(16),
                    Language = reader.GetString(17)
                };
                return settings;
            }
            
            // Return default settings if none exist
            return new GameSettings(true);
        }
    }
}
