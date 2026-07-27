using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Offsides: detection on the pass, whistle on the touch, free kick
/// for the defending team, flag raised, and the setting gate (default off).</summary>
public class OffsideTests
{
    /// <summary>Stage an offside pass: home attacker deep behind the away line,
    /// a home midfielder passes to him.</summary>
    internal static (MatchEngine engine, Player offsideAttacker, Player passer, Player defender) StageOffsidePass()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);
        GameSettings.Instance.OffsidesEnabled = true;

        float goalLineX = MatchEngine.StadiumMargin + MatchEngine.FieldWidth;
        // Away defenders hold a high line at ~70% of the field
        int i = 0;
        foreach (var p in engine.AwayTeam.Players.Where(p => p.IsStarting))
        {
            p.FieldPosition = new Vector2(goalLineX - 2500f + i * 30f, 2682f);
            i++;
        }
        var gk = engine.AwayTeam.Players.First(p => p.Position == PlayerPosition.Goalkeeper);
        gk.FieldPosition = new Vector2(goalLineX - 100f, 2682f);

        // Deep home attacker, past the second-last defender and ahead of the ball
        var offside = engine.HomeTeam.Players.First(p => p.Position == PlayerPosition.Forward && p.IsStarting);
        offside.FieldPosition = new Vector2(goalLineX - 500f, 2682f);

        // Passer on the halfway line with the ball
        var passer = engine.HomeTeam.Players.First(p =>
            p.Position == PlayerPosition.Midfielder && p.IsStarting && p != offside);
        passer.FieldPosition = new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f, 2682f);
        engine.BallPosition = passer.FieldPosition + new Vector2(30f, 0f);
        engine.BallVelocity = Vector2.Zero;
        engine.LastPlayerTouchedBall = passer;

        var defender = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Midfielder);
        return (engine, offside, passer, defender);
    }

    [Fact]
    public void OffsideTouch_WhistlesFreeKickForDefense_RaisesFlag()
    {
        var (engine, offside, _, _) = StageOffsidePass();

        // Snapshot via a pass, then the offside attacker gains possession
        // directly (no flight - avoids a defender wiping the candidate list)
        engine.RegisterKick(engine.HomeTeam.Players.First(p =>
            p.Position == PlayerPosition.Midfielder && p.IsStarting && p != offside));
        engine.BallPosition = offside.FieldPosition + new Vector2(10f, 0f);
        engine.LastPlayerTouchedBall = offside;
        TestHelper.Step(engine, 0.1f);

        Assert.True(engine.OffsideFlagRaised, "flag should be up");
        Assert.Equal(MatchEngine.MatchState.FreeKick, engine.CurrentState);
        // The restart goes to the DEFENDING (away) team
        Assert.Equal(engine.AwayTeam, engine.RestartPlayer.Team);
        // Big on-screen banner fired
        Assert.Equal(Localization.Instance.Get("match.offside"), engine.EventBannerText);
        Assert.True(engine.EventBannerTimer > 0f);
    }

    [Fact]
    public void OnsidePass_NoWhistle()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);
        GameSettings.Instance.OffsidesEnabled = true;

        // All attackers onside (behind the second-last defender)
        float goalLineX = MatchEngine.StadiumMargin + MatchEngine.FieldWidth;
        foreach (var p in engine.HomeTeam.Players.Where(p => p.IsStarting))
            p.FieldPosition = new Vector2(goalLineX - 4000f, p.FieldPosition.Y);
        foreach (var p in engine.AwayTeam.Players.Where(p => p.IsStarting))
            p.FieldPosition = new Vector2(goalLineX - 1500f, p.FieldPosition.Y);

        var passer = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Midfielder);
        engine.BallPosition = passer.FieldPosition + new Vector2(30f, 0f);
        engine.RegisterKick(passer);
        TestHelper.Step(engine, 2f);

        Assert.False(engine.OffsideFlagRaised);
        Assert.Equal(MatchEngine.MatchState.Playing, engine.CurrentState);
    }

    [Fact]
    public void OffsidesDisabled_ByDefault_NoWhistle()
    {
        var (engine, offside, _, _) = StageOffsidePass();
        GameSettings.Instance.OffsidesEnabled = false;

        engine.RegisterKick(engine.HomeTeam.Players.First(p =>
            p.Position == PlayerPosition.Midfielder && p.IsStarting && p != offside));
        engine.BallPosition = offside.FieldPosition + new Vector2(10f, 0f);
        engine.LastPlayerTouchedBall = offside;
        TestHelper.Step(engine, 0.2f);

        Assert.False(engine.OffsideFlagRaised);
        Assert.NotEqual(MatchEngine.MatchState.FreeKick, engine.CurrentState);
    }

    [Fact]
    public void DefenderIntercept_NoOffsideWhistle()
    {
        var (engine, offside, _, defender) = StageOffsidePass();

        engine.RegisterKick(engine.HomeTeam.Players.First(p =>
            p.Position == PlayerPosition.Midfielder && p.IsStarting && p != offside));

        // The DEFENDER plays the ball instead - no offside
        defender.FieldPosition = new Vector2(engine.BallPosition.X + 400f, engine.BallPosition.Y);
        engine.BallPosition = defender.FieldPosition + new Vector2(10f, 0f);
        engine.LastPlayerTouchedBall = defender;
        TestHelper.Step(engine, 0.2f);

        Assert.False(engine.OffsideFlagRaised);
    }

    [Fact]
    public void SettingsScreen_OffsidesDefault_IsOff()
    {
        var fresh = new GameSettings(true);
        Assert.False(fresh.OffsidesEnabled);
    }

    [Fact]
    public void Migration_OffsidesColumn_DefaultsOff()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"nopasaran_test_{Guid.NewGuid():N}.db");
        try
        {
            var db = new Database.DatabaseManager(tempDb);
            Assert.False(db.LoadSettings().OffsidesEnabled);
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }
}
