using System;
using System.IO;
using System.Linq;
using NoPasaranFC.Database;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Antifa League configuration: street mode in the seed, database
/// persistence and the championship-to-rules mapping.</summary>
public class StreetLeagueTests
{
    [Fact]
    public void AntifaLeagueSeed_IsStreet5v5()
    {
        string path = FindRepoFile(Path.Combine("Database", "championships_seed.json"));
        var definitions = ChampionshipSeeder.LoadChampionshipsFromJson(path);

        var antifa = definitions.First(d => d.Id == "antifa_league");
        Assert.Equal("street", antifa.Mode);
        Assert.Equal(5, antifa.TeamSize);
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
