using Microsoft.Xna.Framework;
using NoPasaranFC.Database;
using NoPasaranFC.Graphics3D;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Player/kit editor backbone: seed kit blocks, catalog overlay merge,
/// SaveCatalog round-trip, appearance override precedence, pattern/paint pixel
/// functions, and DB persistence of the new fields.</summary>
public class EditorTests
{
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

    [Fact]
    public void SeedJson_NamedTeams_HaveKitBlocks()
    {
        var teams = TeamSeeder.LoadTeamsFromJson(FindRepoFile(Path.Combine("Database", "teams_seed.json")));
        var noPasaran = teams.First(t => t.Name.Contains("NO PASARAN"));
        // Kit block present and plausible (users customize these in the editor)
        Assert.NotEqual(0, noPasaran.ShirtColor);
        Assert.NotEqual(0, noPasaran.ShortsColor);
        Assert.NotEqual(0, noPasaran.SocksColor);

        // Unnamed teams stay 0 (renderer falls back to home/away defaults)
        var unnamed = teams.First(t => string.IsNullOrEmpty(t.KitName));
        Assert.Equal(0, unnamed.ShirtColor);
    }

    [Fact]
    public void Catalog_OverlayMerge_OverrideWinsByName()
    {
        string basePath = Path.Combine(Path.GetTempPath(), $"base_{Guid.NewGuid():N}.json");
        string overlayPath = Path.Combine(Path.GetTempPath(), $"overlay_{Guid.NewGuid():N}.json");
        try
        {
            File.WriteAllText(basePath, @"{""teams"":[
                {""name"":""TEAM A"",""players"":[]},
                {""name"":""TEAM B"",""players"":[]}]}");
            var custom = new Team("TEAM B") { ShirtColor = 0x112233 };
            TeamSeeder.SaveCatalog(new List<Team> { custom }, overlayPath);

            var merged = TeamSeeder.LoadCatalog(basePath, overlayPath);
            Assert.Equal(2, merged.Count);
            Assert.Equal(0, merged.First(t => t.Name == "TEAM A").ShirtColor);
            Assert.Equal(0x112233, merged.First(t => t.Name == "TEAM B").ShirtColor);
        }
        finally
        {
            File.Delete(basePath);
            File.Delete(overlayPath);
        }
    }

    [Fact]
    public void SaveCatalog_RoundTrip_KitAndAppearanceIntact()
    {
        string path = Path.Combine(Path.GetTempPath(), $"rt_{Guid.NewGuid():N}.json");
        try
        {
            var team = new Team("RT TEAM")
            {
                ShirtColor = 0xA0B0C0,
                ShortsColor = 0x101010,
                SocksColor = 0x202020,
                GkShirtColor = 0x303030,
                ShirtPattern = 2,
                PatternColor = 0xFF0000,
                ShirtPaint = new string('0', 1023) + '3'
            };
            var player = new Player("Testos", PlayerPosition.Forward)
            {
                ShirtNumber = 9,
                Speed = 88,
                GenderOverride = 2,
                SkinToneOverride = 3,
                HairColorOverride = 4,
                ExpressionOverride = 1,
                FeatureOverride = 4
            };
            team.AddPlayer(player);
            TeamSeeder.SaveCatalog(new List<Team> { team }, path);

            var loaded = TeamSeeder.LoadTeamsFromJson(path).Single();
            Assert.Equal(0xA0B0C0, loaded.ShirtColor);
            Assert.Equal(0x303030, loaded.GkShirtColor);
            Assert.Equal(2, loaded.ShirtPattern);
            Assert.Equal(0xFF0000, loaded.PatternColor);
            Assert.Equal(team.ShirtPaint, loaded.ShirtPaint);

            var p = loaded.Players.Single();
            Assert.Equal(88, p.Speed);
            Assert.Equal(2, p.GenderOverride);
            Assert.Equal(3, p.SkinToneOverride);
            Assert.Equal(4, p.HairColorOverride);
            Assert.Equal(1, p.ExpressionOverride);
            Assert.Equal(4, p.FeatureOverride);
        }
        finally
        {
            File.Delete(path);
        }
    }

    [Fact]
    public void AppearanceFor_OverrideFields_WinOverHash()
    {
        var player = new Player("Hash Dude", PlayerPosition.Midfielder) { ShirtNumber = 8 };
        var auto = FaceComposer.AppearanceFor(player);

        player.SkinToneOverride = 4;
        player.HairColorOverride = 1;
        player.ExpressionOverride = 2; // Sad
        player.FeatureOverride = 1;    // Beard
        var custom = FaceComposer.AppearanceFor(player);

        Assert.Equal(4, custom.SkinTone);
        Assert.Equal(1, custom.HairColor);
        Assert.Equal(FaceComposer.Expression.Sad, custom.Expr);
        Assert.Equal(FaceComposer.Feature.Beard, custom.Feat);
        Assert.NotEqual(auto.SkinTone == custom.SkinTone && auto.Expr == custom.Expr
            && auto.HairColor == custom.HairColor && auto.Feat == custom.Feat, true);
    }

    [Fact]
    public void IsFemalePlayer_GenderOverrideWins()
    {
        var player = new Player("Whoever", PlayerPosition.Midfielder);
        player.GenderOverride = 2;
        Assert.True(FaceComposer.IsFemalePlayer(player));
        player.GenderOverride = 1;
        Assert.False(FaceComposer.IsFemalePlayer(player));
    }

    [Fact]
    public void StampPatternPixels_StripesV_ProducesTwoColors()
    {
        int w = 64, h = 64;
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.Red;

        KitTextureFactory.StampPatternPixels(pixels, w, new Rectangle(0, 0, w, h),
            (int)KitTextureFactory.ShirtPattern.StripesV, Color.Blue);

        Assert.Contains(pixels, p => p == Color.Red);
        Assert.Contains(pixels, p => p == Color.Blue);
    }

    [Fact]
    public void StampPatternPixels_Solid_IsNoOp()
    {
        var pixels = new Color[16];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.Green;
        var before = pixels.ToArray();
        KitTextureFactory.StampPatternPixels(pixels, 4, new Rectangle(0, 0, 4, 4),
            (int)KitTextureFactory.ShirtPattern.Solid, Color.Blue);
        Assert.Equal(before, pixels);
    }

    [Fact]
    public void ApplyPaintPixels_PaintsCells_EmptyIsNoOp()
    {
        int w = 64, h = 64;
        var pixels = new Color[w * h];
        for (int i = 0; i < pixels.Length; i++) pixels[i] = Color.Red;

        // Empty grid: nothing changes
        KitTextureFactory.ApplyPaintPixels(pixels, w, new Rectangle(0, 0, w, h), new string('0', 1024));
        Assert.All(pixels, p => Assert.Equal(Color.Red, p));

        // Cell (0,0) with palette index 3 (red) and (1,1) with 4 (blue)
        var grid = new string('0', 1024).ToCharArray();
        grid[0] = '3';           // palette[2] = red
        grid[32 + 1] = '4';      // palette[3] = blue
        KitTextureFactory.ApplyPaintPixels(pixels, w, new Rectangle(0, 0, w, h), new string(grid));

        var expectedRed = KitTextureFactory.PaintPalette[2];
        var expectedBlue = KitTextureFactory.PaintPalette[3];
        Assert.Equal(expectedRed, pixels[0]);                    // cell (0,0)
        Assert.Equal(expectedBlue, pixels[2 * w + 2]);           // cell (1,1)
        Assert.Equal(Color.Red, pixels[63 * w + 63]);            // untouched corner
    }

    [Fact]
    public void DbRoundTrip_TeamKitAndPlayerAppearance()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"nopasaran_test_{Guid.NewGuid():N}.db");
        try
        {
            var db = new DatabaseManager(tempDb);
            var team = new Team("DB KIT TEAM")
            {
                ShirtColor = 0x123456,
                GkShirtColor = 0x654321,
                ShirtPattern = 1,
                PatternColor = 0xABCDEF,
                ShirtPaint = new string('1', 1024)
            };
            db.SaveTeam(team);
            var player = new Player("DB Guy", PlayerPosition.Defender)
            {
                TeamId = team.Id,
                GenderOverride = 2,
                SkinToneOverride = 1,
                FeatureOverride = 3
            };
            db.SavePlayer(player);

            var loaded = db.LoadAllTeams().Single(t => t.Name == "DB KIT TEAM");
            Assert.Equal(0x123456, loaded.ShirtColor);
            Assert.Equal(0x654321, loaded.GkShirtColor);
            Assert.Equal(1, loaded.ShirtPattern);
            Assert.Equal(0xABCDEF, loaded.PatternColor);
            Assert.Equal(new string('1', 1024), loaded.ShirtPaint);

            var p = loaded.Players.Single();
            Assert.Equal(2, p.GenderOverride);
            Assert.Equal(1, p.SkinToneOverride);
            Assert.Equal(3, p.FeatureOverride);

            // Untouched players default to -1 (auto)
            var plain = new Player("Plain Jane", PlayerPosition.Midfielder) { TeamId = team.Id };
            db.SavePlayer(plain);
            var loadedPlain = db.LoadPlayersForTeam(team.Id).Single(x => x.Name == "Plain Jane");
            Assert.Equal(-1, loadedPlain.GenderOverride);
            Assert.Equal(-1, loadedPlain.ExpressionOverride);
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }
}
