using System.Reflection;
using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Gameplay.UtilityAI;
using NoPasaranFC.Graphics3D;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>ReplayBuffer ring-buffer math and the tuning-override plumbing that
/// the parameter search relies on (regression guard for the dead-knob incident).</summary>
public class ReplayAndTuningTests
{
    [Fact]
    public void ReplayBuffer_SnapshotPreservesOrder()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var buffer = new ReplayBuffer();
        for (int i = 0; i < 120; i++)
        {
            engine.BallPosition = new Vector2(1000f + i * 10f, 2000f);
            buffer.Record(engine);
        }

        var seq = buffer.Snapshot(1.5f); // 90 frames
        Assert.Equal(90, seq.FrameCount);

        var players = new ReplayBuffer.PlayerFrame[seq.PlayerCount];
        seq.GetInterpolated(0f, out var first, players);
        seq.GetInterpolated(seq.Duration, out var last, players);

        Assert.Equal(1300f, first.Position.X, 1f);  // frames 30..119 of 1000+i*10
        Assert.True(last.Position.X > first.Position.X, "frames must be time-ordered");
    }

    [Fact]
    public void ReplayBuffer_InterpolationIsLinear()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var buffer = new ReplayBuffer();
        for (int i = 0; i < 60; i++)
        {
            engine.BallPosition = new Vector2(i * 100f, 0f);
            buffer.Record(engine);
        }

        var seq = buffer.Snapshot(1f);
        var players = new ReplayBuffer.PlayerFrame[seq.PlayerCount];
        seq.GetInterpolated(seq.Duration / 2f, out var mid, players);

        Assert.Equal(seq.FrameCount / 2f * 100f - 50f, mid.Position.X, 60f);
    }

    [Fact]
    public void UtilityTuning_ApplyOverrides_HandlesTypes()
    {
        var defaults = UtilityTuning.SnapshotDefaults();
        try
        {
            UtilityTuning.ApplyOverrides(new Dictionary<string, float>
            {
                ["ShootRangeNear"] = 1234f,
                ["RoleAttackForward"] = 1.55f,
                ["NoSuchKnob"] = 999f, // unknown names must be ignored
            });
            Assert.Equal(1234f, UtilityTuning.ShootRangeNear);
            Assert.Equal(1.55f, UtilityTuning.RoleAttackForward, 3);
        }
        finally
        {
            UtilityTuning.ApplyOverrides(defaults); // restore
        }
    }

    [Fact]
    public void AIConstants_ApplyOverrides_HandlesFloatDoubleInt()
    {
        var defaults = AIConstants.SnapshotDefaults();
        try
        {
            AIConstants.ApplyOverrides(new Dictionary<string, float>
            {
                ["ShootCloseChance"] = 0.5f,   // double field
                ["OrbitWaypoints"] = 9f,        // int field
                ["BaseSpeedMultiplier"] = 3.3f, // float field
            });
            Assert.Equal(0.5, AIConstants.ShootCloseChance, 3);
            Assert.Equal(9, AIConstants.OrbitWaypoints);
            Assert.Equal(3.3f, AIConstants.BaseSpeedMultiplier, 3);
        }
        finally
        {
            AIConstants.ApplyOverrides(defaults);
        }
    }

    /// <summary>Every knob in the search space must resolve to a live field of
    /// UtilityTuning or AIConstants (catches tuning dead code like the v1/v2 runs).</summary>
    [Fact]
    public void SearchSpace_AllKnobsResolveToLiveFields()
    {
        string path = FindRepoFile(Path.Combine("Harness", "search_space.json"));
        var doc = System.Text.Json.JsonDocument.Parse(File.ReadAllText(path));
        var tuningFields = typeof(UtilityTuning).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name).ToHashSet();
        var constantFields = typeof(AIConstants).GetFields(BindingFlags.Public | BindingFlags.Static)
            .Select(f => f.Name).ToHashSet();

        foreach (var prop in doc.RootElement.GetProperty("parameters").EnumerateObject())
        {
            Assert.True(tuningFields.Contains(prop.Name) || constantFields.Contains(prop.Name),
                $"search-space knob '{prop.Name}' does not exist in UtilityTuning/AIConstants");
        }
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
