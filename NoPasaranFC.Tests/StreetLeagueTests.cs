using System;
using System.IO;
using System.Linq;
using Microsoft.Data.Sqlite;
using NoPasaranFC.Database;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Antifa competitions: 11v11 league, separate 5v5 street league,
/// database persistence/reset migration and the championship-to-rules mapping.</summary>
public class StreetLeagueTests
{
    [Fact]
    public void AntifaLeagueSeed_IsStandard11v11()
    {
        string path = FindRepoFile(Path.Combine("Database", "championships_seed.json"));
        var definitions = ChampionshipSeeder.LoadChampionshipsFromJson(path);

        var antifa = definitions.First(d => d.Id == "antifa_league");
        Assert.Equal("standard", antifa.Mode);
        Assert.Equal(11, antifa.TeamSize);
    }

    [Fact]
    public void AntifaStreetLeagueSeed_IsStreet5v5()
    {
        string path = FindRepoFile(Path.Combine("Database", "championships_seed.json"));
        var definitions = ChampionshipSeeder.LoadChampionshipsFromJson(path);

        var street = definitions.First(d => d.Id == "antifa_street_league");
        Assert.Equal("street", street.Mode);
        Assert.Equal(5, street.TeamSize);
    }

    [Fact]
    public void Migration16_RestoresAndResetsAccidentalAntifaStreetSeason()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nopasaran_antifa_{Guid.NewGuid():N}.db");
        try
        {
            var db = new DatabaseManager(path);
            var championship = new Championship
            {
                Id = "antifa_league",
                Name = "ANTIFA LEAGUE",
                Mode = "street",
                TeamSize = 5,
            };
            for (int i = 0; i < 4; i++)
            {
                var team = TestHelper.MakeTeam(i == 0 ? "NO PASARAN!" : $"ANTIFA {i}", i + 1,
                    playerControlled: i == 0);
                team.Wins = 2;
                team.GoalsFor = 6;
                foreach (var player in team.Players)
                {
                    player.SeasonGoals = 1;
                    player.IsStarting = false;
                }
                foreach (var player in team.Players.Take(5))
                    player.IsStarting = true;
                championship.Teams.Add(team);
            }
            championship.GenerateFixtures();
            db.SaveChampionship(championship);

            DowngradeSchemaTo15(path);

            var loaded = new DatabaseManager(path).LoadChampionship();
            Assert.Equal("standard", loaded.Mode);
            Assert.Equal(11, loaded.TeamSize);
            Assert.Equal(0, loaded.CurrentMatchweek);
            Assert.Equal(12, loaded.Matches.Count);
            Assert.All(loaded.Teams, t =>
            {
                Assert.Equal(0, t.Wins);
                Assert.Equal(0, t.GoalsFor);
                Assert.Equal(11, t.Players.Count(p => p.IsStarting));
            });
            Assert.All(loaded.Teams.SelectMany(t => t.Players), p => Assert.Equal(0, p.SeasonGoals));
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void Migration16_LeavesOtherStreetChampionshipsAlone()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nopasaran_street_keep_{Guid.NewGuid():N}.db");
        try
        {
            var db = new DatabaseManager(path);
            var championship = new Championship
            {
                Id = "street_test",
                Name = "STREET TEST",
                Mode = "street",
                TeamSize = 5,
            };
            championship.Teams.Add(TestHelper.MakeTeam("HOME", 1, playerControlled: true));
            championship.Teams.Add(TestHelper.MakeTeam("AWAY", 2));
            championship.Teams[0].Wins = 3;
            championship.GenerateFixtures();
            db.SaveChampionship(championship);

            DowngradeSchemaTo15(path);

            var loaded = new DatabaseManager(path).LoadChampionship();
            Assert.Equal("street", loaded.Mode);
            Assert.Equal(5, loaded.TeamSize);
            Assert.Equal(3, loaded.Teams[0].Wins);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    private static void DowngradeSchemaTo15(string path)
    {
        using var connection = new SqliteConnection($"Data Source={path}");
        connection.Open();
        var command = connection.CreateCommand();
        command.CommandText = "DELETE FROM SchemaVersion; INSERT INTO SchemaVersion (Version) VALUES (15);";
        command.ExecuteNonQuery();
    }

    [Fact]
    public void ChampionshipMode_RoundTripsThroughDatabase()
    {
        string path = Path.Combine(Path.GetTempPath(), $"nopasaran_street_{Guid.NewGuid():N}.db");
        try
        {
            var db = new DatabaseManager(path);
            var championship = new Championship
            {
                Id = "street_test",
                Name = "STREET TEST",
                Mode = "street",
                TeamSize = 5,
            };
            db.SaveChampionship(championship);

            var loaded = new DatabaseManager(path).LoadChampionship();
            Assert.Equal("street", loaded.Mode);
            Assert.Equal(5, loaded.TeamSize);
            Assert.True(loaded.IsStreet);
        }
        finally
        {
            if (File.Exists(path)) File.Delete(path);
        }
    }

    [Fact]
    public void MatchRules_MapStreetChampionshipToConfiguredVenue()
    {
        var street = new Championship { Mode = "street", TeamSize = 5 };
        var standard = new Championship { Mode = "standard", TeamSize = 11 };
        string previousVenue = GameSettings.Instance.StreetVenue;

        try
        {
            GameSettings.Instance.StreetVenue = "Cage";
            Assert.Same(MatchRules.Street5v5, MatchRules.ForChampionship(street));
            Assert.True(MatchRules.ForChampionship(street).WallBounce);

            GameSettings.Instance.StreetVenue = "Prosfygika";
            Assert.Same(MatchRules.Prosfygika5v5, MatchRules.ForChampionship(street));
            Assert.False(MatchRules.ForChampionship(street).WallBounce);

            Assert.Same(MatchRules.Standard, MatchRules.ForChampionship(standard));
        }
        finally
        {
            GameSettings.Instance.StreetVenue = previousVenue;
        }
    }

    [Fact]
    public void ForStreetVenue_ResolvesCageAndProsfygika()
    {
        Assert.Same(MatchRules.Street5v5, MatchRules.ForStreetVenue("Cage"));
        Assert.Same(MatchRules.Prosfygika5v5, MatchRules.ForStreetVenue("Prosfygika"));
        Assert.Same(MatchRules.Prosfygika5v5, MatchRules.ForStreetVenue(null));
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relative);
    }
}
