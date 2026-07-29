using System.Text.Json;
using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>MatchRecorder: harness-compatible JSONL schema, event lines, the
/// verbose decision block, and the 10 Hz sampling cadence.</summary>
public class MatchRecorderTests : IDisposable
{
    private readonly string _dir;

    public MatchRecorderTests()
    {
        _dir = Path.Combine(Path.GetTempPath(), "npf_rec_" + Guid.NewGuid().ToString("N"));
    }

    public void Dispose()
    {
        if (Directory.Exists(_dir)) Directory.Delete(_dir, true);
    }

    /// <summary>Steps the engine and the recorder together at 60 Hz.</summary>
    private static void StepBoth(MatchEngine engine, MatchRecorder recorder, float seconds)
    {
        int frames = (int)(seconds / TestHelper.Dt);
        var total = TimeSpan.Zero;
        for (int i = 0; i < frames; i++)
        {
            total += TimeSpan.FromSeconds(TestHelper.Dt);
            engine.Update(new GameTime(total, TimeSpan.FromSeconds(TestHelper.Dt)), Vector2.Zero, false, false);
            recorder.Update(TestHelper.Dt);
        }
    }

    private string[] RunAndRead(MatchEngine engine, bool verbose, float seconds)
    {
        var recorder = new MatchRecorder(engine, verbose, _dir);
        TestHelper.ReachPlaying(engine);
        StepBoth(engine, recorder, seconds);
        string path = recorder.FilePath;
        recorder.Dispose();
        return File.ReadAllLines(path);
    }

    [Fact]
    public void MetaAndFrames_MatchHarnessSchema()
    {
        var engine = TestHelper.MakeEngine(42);
        string[] lines = RunAndRead(engine, verbose: false, 3f);

        Assert.True(lines.Length > 10, $"expected meta + frames, got {lines.Length} lines");

        // Meta line
        using var meta = JsonDocument.Parse(lines[0]);
        Assert.True(meta.RootElement.GetProperty("meta").GetBoolean());
        Assert.Equal("live", meta.RootElement.GetProperty("scenario").GetString());
        Assert.Equal(10, meta.RootElement.GetProperty("sampleHz").GetInt32());
        Assert.Equal((int)MatchEngine.FieldWidth, meta.RootElement.GetProperty("fieldWidth").GetInt32());
        Assert.Equal((int)MatchEngine.FieldHeight, meta.RootElement.GetProperty("fieldHeight").GetInt32());
        Assert.Equal((int)MatchEngine.StadiumMargin, meta.RootElement.GetProperty("stadiumMargin").GetInt32());
        Assert.Equal("HOME", meta.RootElement.GetProperty("homeTeam").GetString());
        Assert.Equal("AWAY", meta.RootElement.GetProperty("awayTeam").GetString());

        // First frame line: the exact HarnessRunner.WriteFrame field names
        string frameLine = lines.First(l => !l.Contains("\"meta\"") && !l.Contains("\"ev\""));
        using var frame = JsonDocument.Parse(frameLine);
        var root = frame.RootElement;
        Assert.True(root.TryGetProperty("t", out _));
        Assert.True(root.TryGetProperty("state", out _));
        var ball = root.GetProperty("ball");
        foreach (var key in new[] { "x", "y", "h", "vx", "vy" })
            Assert.True(ball.TryGetProperty(key, out _), $"ball.{key} missing");

        var players = root.GetProperty("players");
        Assert.Equal(22, players.GetArrayLength());
        var p0 = players[0];
        foreach (var key in new[] { "i", "team", "name", "x", "y", "vx", "vy", "state", "tx", "ty" })
            Assert.True(p0.TryGetProperty(key, out _), $"player.{key} missing");
        Assert.Matches("^(home|away)$", p0.GetProperty("team").GetString());

        // The human-controlled player is flagged (home team is player-controlled)
        Assert.Contains(players.EnumerateArray(),
            p => p.TryGetProperty("controlled", out var c) && c.GetBoolean());
    }

    [Fact]
    public void Sampling_ProducesAbout10FramesPerSecond()
    {
        var engine = TestHelper.MakeEngine(42);
        string[] lines = RunAndRead(engine, verbose: false, 10f);

        int frameLines = lines.Count(l => !l.Contains("\"meta\"") && !l.Contains("\"ev\""));
        Assert.InRange(frameLines, 95, 105); // 10s at 10 Hz
    }

    [Fact]
    public void EventLines_SerializeWithPayload()
    {
        var engine = TestHelper.MakeEngine(42);
        string[] lines = RunAndRead(engine, verbose: false, 30f);

        // 30s of AI play guarantees deliberate kicks (dribble taps, passes, shots)
        var eventLines = lines.Where(l => l.Contains("\"ev\"")).ToList();
        Assert.NotEmpty(eventLines);

        using var ev = JsonDocument.Parse(eventLines.First(l => l.Contains("\"ev\":\"kick\"")
            || l.Contains("\"ev\":\"pass\"") || l.Contains("\"ev\":\"shot\"")));
        var root = ev.RootElement;
        Assert.True(root.TryGetProperty("t", out _));
        Assert.True(root.TryGetProperty("player", out _));
        Assert.Matches("^(home|away)$", root.GetProperty("team").GetString());
        foreach (var key in new[] { "x", "y", "vx", "vy", "power" })
            Assert.True(root.TryGetProperty(key, out _), $"event.{key} missing");
    }

    [Fact]
    public void Verbose_DecisionBlockOnlyWhenEnabled()
    {
        var engineV = TestHelper.MakeEngine(42);
        string[] verboseLines = RunAndRead(engineV, verbose: true, 5f);

        var withDec = verboseLines.Where(l => l.Contains("\"dec\"")).ToList();
        Assert.NotEmpty(withDec);

        // Parse a decision block: chosen action + score (+ optional alternatives)
        var frameLine = withDec.First(l => !l.Contains("\"ev\""));
        using var frame = JsonDocument.Parse(frameLine);
        var dec = frame.RootElement.GetProperty("players").EnumerateArray()
            .First(p => p.TryGetProperty("dec", out _)).GetProperty("dec");
        Assert.False(string.IsNullOrEmpty(dec.GetProperty("action").GetString()));
        Assert.True(dec.TryGetProperty("score", out _));
        if (dec.TryGetProperty("alt", out var alt))
        {
            Assert.True(alt.GetArrayLength() is 1 or 2);
            Assert.True(alt[0].TryGetProperty("action", out _));
            Assert.True(alt[0].TryGetProperty("score", out _));
        }

        // Same run without the setting: no decision detail at all
        var engineP = TestHelper.MakeEngine(42);
        string[] plainLines = RunAndRead(engineP, verbose: false, 5f);
        Assert.DoesNotContain(plainLines, l => l.Contains("\"dec\""));
    }
}
