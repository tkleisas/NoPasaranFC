using Microsoft.Xna.Framework;
using NoPasaranFC.Database;
using NoPasaranFC.Graphics3D;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Localization coverage, settings persistence + migrations, and the
/// pure pixel pipelines (face composer, kit recolor, number stamp).</summary>
public class ContentAndPersistenceTests : IDisposable
{
    private readonly string _tempDb = Path.Combine(Path.GetTempPath(), $"nopasaran_test_{Guid.NewGuid():N}.db");

    public void Dispose()
    {
        if (File.Exists(_tempDb)) File.Delete(_tempDb);
    }

    // ---------- Localization ----------

    [Fact]
    public void Localization_EnglishAndGreek_HaveSameKeys()
    {
        var loc = new Localization();
        var en = loc.GetEnglishStrings();
        var el = loc.GetGreekStrings();

        var missingInEl = en.Keys.Where(k => !el.ContainsKey(k)).ToList();
        var missingInEn = el.Keys.Where(k => !en.ContainsKey(k)).ToList();

        Assert.True(missingInEl.Count == 0, $"keys missing in Greek: {string.Join(", ", missingInEl.Take(10))}");
        Assert.True(missingInEn.Count == 0, $"keys missing in English: {string.Join(", ", missingInEn.Take(10))}");
    }

    [Fact]
    public void Localization_RequiredKeysExist_AndValuesNonEmpty()
    {
        var loc = new Localization();
        var en = loc.GetEnglishStrings();
        var el = loc.GetGreekStrings();
        foreach (string key in new[]
        {
            "settings.venue", "settings.venue.bahramis", "settings.venue.sperchogeia",
            "settings.venue.sfageia", "settings.venue.random",
            "settings.ballControl", "settings.ballControl.easy", "settings.ballControl.classic",
            "match.halftime", "menu.back",
        })
        {
            Assert.True(en.TryGetValue(key, out var vEn) && !string.IsNullOrWhiteSpace(vEn), $"en missing {key}");
            Assert.True(el.TryGetValue(key, out var vEl) && !string.IsNullOrWhiteSpace(vEl), $"el missing {key}");
        }
    }

    // ---------- Settings persistence ----------

    [Fact]
    public void Settings_SaveLoad_RoundTripsAllFields()
    {
        var db = new DatabaseManager(_tempDb);
        var settings = new GameSettings(true)
        {
            Venue = "Sfageia",
            BallControl = "Classic",
            CameraMode = "TopDown",
            PlayerSpeedMultiplier = 2.5f,
            AIDecisionInterval = 0.33f,
            TimeOfDay = "Night",
            Weather = "Rain",
        };
        db.SaveSettings(settings);

        var loaded = new DatabaseManager(_tempDb).LoadSettings();
        Assert.Equal("Sfageia", loaded.Venue);
        Assert.Equal("Classic", loaded.BallControl);
        Assert.Equal("TopDown", loaded.CameraMode);
        Assert.Equal(2.5f, loaded.PlayerSpeedMultiplier, 3);
        Assert.Equal(0.33f, loaded.AIDecisionInterval, 3);
        Assert.Equal("Night", loaded.TimeOfDay);
        Assert.Equal("Rain", loaded.Weather);
    }

    [Fact]
    public void Migrations_CreateVenueAndBallControlColumns()
    {
        // Fresh DB: migrations must bring it to the latest schema
        var db = new DatabaseManager(_tempDb);
        var loaded = db.LoadSettings(); // must not throw on missing columns
        Assert.Equal("Bahramis", loaded.Venue);     // migration 7 default
        Assert.Equal("Easy", loaded.BallControl);   // migration 8 default
    }

    // ---------- Face composer (pure pixels) ----------

    private static Color[] MakeBaseAtlas()
    {
        // 512x512: skin quadrant flesh + a hair block + eye/mouth dots, rest gray
        var px = new Color[512 * 512];
        var gray = new Color(120, 120, 120);
        for (int i = 0; i < px.Length; i++) px[i] = gray;
        var flesh = new Color(255, 205, 160);
        for (int y = 256; y < 512; y++)
            for (int x = 256; x < 448; x++)
                px[y * 512 + x] = flesh;
        for (int y = 256; y < 320; y++)
            for (int x = 448; x < 512; x++)
                px[y * 512 + x] = new Color(120, 75, 40); // hair block
        return px;
    }

    [Fact]
    public void FaceComposer_Upscales512To1024()
    {
        var px = FaceComposer.ComposePixels(MakeBaseAtlas(), 512, 512,
            new FaceComposer.Appearance(0, 0, FaceComposer.Expression.Smile, FaceComposer.Feature.None));
        Assert.Equal(1024 * 1024, px.Length);
        // A source flesh pixel survives at its scaled position
        Assert.Equal(new Color(255, 205, 160).PackedValue, px[600 * 1024 + 600].PackedValue);
    }

    [Fact]
    public void FaceComposer_ExpressionsDiffer_InFaceRegion()
    {
        var smile = FaceComposer.ComposePixels(MakeBaseAtlas(), 512, 512,
            new FaceComposer.Appearance(0, 0, FaceComposer.Expression.Smile, FaceComposer.Feature.None));
        var sad = FaceComposer.ComposePixels(MakeBaseAtlas(), 512, 512,
            new FaceComposer.Appearance(0, 0, FaceComposer.Expression.Sad, FaceComposer.Feature.None));
        Assert.NotEqual(smile.Select(c => c.PackedValue), sad.Select(c => c.PackedValue));
    }

    [Fact]
    public void FaceComposer_Deterministic()
    {
        var a = new FaceComposer.Appearance(2, 3, FaceComposer.Expression.Wow, FaceComposer.Feature.Goatee);
        var p1 = FaceComposer.ComposePixels(MakeBaseAtlas(), 512, 512, a);
        var p2 = FaceComposer.ComposePixels(MakeBaseAtlas(), 512, 512, a);
        Assert.Equal(p1.Select(c => c.PackedValue), p2.Select(c => c.PackedValue));
    }

    [Fact]
    public void FaceComposer_SkinToneShift_AppliesToFlesh()
    {
        var px = FaceComposer.ComposePixels(MakeBaseAtlas(), 512, 512,
            new FaceComposer.Appearance(4, 0, FaceComposer.Expression.Neutral, FaceComposer.Feature.None));
        // Flesh quadrant must now be the darkest tone (120,75,50), not the base
        Assert.Equal(new Color(120, 75, 50).PackedValue, px[600 * 1024 + 600].PackedValue);
    }

    [Fact]
    public void FaceComposer_HairBlock_GetsHairColor()
    {
        var px = FaceComposer.ComposePixels(MakeBaseAtlas(), 512, 512,
            new FaceComposer.Appearance(0, 1, FaceComposer.Expression.Neutral, FaceComposer.Feature.None));
        // Hair block (448..512, 256..320) at 2x scale = (896..1024, 512..640)
        Assert.Equal(new Color(35, 30, 28).PackedValue, px[520 * 1024 + 960].PackedValue);
    }

    [Fact]
    public void AppearanceFor_StableForSamePlayer()
    {
        var p = new Player { Name = "Lamougio", ShirtNumber = 5 };
        var a1 = FaceComposer.AppearanceFor(p);
        var a2 = FaceComposer.AppearanceFor(p);
        Assert.Equal(a1.Expr, a2.Expr);
        Assert.Equal(a1.Feat, a2.Feat);
        Assert.Equal(a1.SkinTone, a2.SkinTone);
        Assert.Equal(a1.HairColor, a2.HairColor);
    }

    // ---------- Kit texture factory (pure pixels) ----------

    [Fact]
    public void RecolorPixels_BrightestShadeMapsToKitColor()
    {
        var px = new Color[4 * 4];
        for (int i = 0; i < px.Length; i++) px[i] = new Color(60, 60, 60, 255);
        px[1] = new Color(240, 240, 240, 255); // brightest shade in the region

        KitTextureFactory.RecolorPixels(px, 4, new Rectangle(0, 0, 4, 4), new Color(200, 0, 0));

        Assert.Equal(new Color(200, 0, 0).PackedValue, px[1].PackedValue); // brightest -> full kit color
        Assert.True(px[0].R < 200, "darker shades keep the gradient");
        Assert.Equal(255, px[0].A);
    }

    [Fact]
    public void StampNumberPixels_WritesDigitsInBackZone()
    {
        int w = 512, h = 512;
        var px = new Color[w * h];
        for (int i = 0; i < px.Length; i++) px[i] = Color.White;

        KitTextureFactory.StampNumberPixels(px, w, h, 7, Color.Black);

        // Digit zone centered at (184,128): some pixels must be black now
        int blackCount = px.Count(c => c.PackedValue == Color.Black.PackedValue);
        Assert.True(blackCount > 100, $"digit pixels should be stamped (got {blackCount})");
        // The glyph for 1 vs 7 must differ
        var px2 = new Color[w * h];
        for (int i = 0; i < px2.Length; i++) px2[i] = Color.White;
        KitTextureFactory.StampNumberPixels(px2, w, h, 1, Color.Black);
        Assert.NotEqual(
            px.Where(c => c.PackedValue == Color.Black.PackedValue).Count(),
            px2.Where(c => c.PackedValue == Color.Black.PackedValue).Count());
    }
}
