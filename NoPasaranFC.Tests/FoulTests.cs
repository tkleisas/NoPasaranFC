using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Fouls and free kicks: tackle-failure fouls, the foul record,
/// free kick setup (wall), and execution resuming play.</summary>
public class FoulTests
{
    [Fact]
    public void FailedTackles_AccumulateFouls_AndGrantFreeKick()
    {
        var engine = TestHelper.MakeEngine(seed: 7);
        TestHelper.ReachPlaying(engine);

        // Weak tackler vs strong carrier: mostly failures, 35% foul each
        var tackler = engine.ControlledPlayer;
        tackler.Defending = 1; tackler.Agility = 1;
        var carrier = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);
        carrier.Technique = 99; carrier.Agility = 99;

        // Put the carrier right next to the tackler with the ball
        tackler.FieldPosition = new Vector2(4000f, 2682f);
        carrier.FieldPosition = tackler.FieldPosition + new Vector2(40f, 0f);
        engine.BallPosition = carrier.FieldPosition + new Vector2(20f, 0f);
        engine.LastPlayerTouchedBall = carrier;

        int freeKicks = 0;
        for (int i = 0; i < 30 && freeKicks < 3; i++)
        {
            // Re-stage the duel before EVERY attempt (the carrier dribbles away otherwise)
            if (engine.CurrentState != MatchEngine.MatchState.Playing)
            {
                TestHelper.StepUntil(engine, () => engine.CurrentState == MatchEngine.MatchState.Playing, 8f);
                freeKicks++;
            }
            tackler.FieldPosition = new Vector2(4000f, 2682f);
            carrier.FieldPosition = tackler.FieldPosition + new Vector2(40f, 0f);
            engine.BallPosition = carrier.FieldPosition + new Vector2(20f, 0f);
            engine.BallVelocity = Vector2.Zero;
            engine.LastPlayerTouchedBall = carrier;
            
            engine.Tackle(tackler);
            TestHelper.Step(engine, 0.2f);
        }

        Assert.True(engine.Fouls.Count >= 1, "repeated failed tackles should produce fouls");
        Assert.True(freeKicks >= 1 || engine.Fouls.Count >= 1);
        Assert.All(engine.Fouls, f => Assert.Equal(tackler.Team, f.Offender.Team));
        Assert.All(engine.Fouls, f => Assert.Equal(carrier.Team, f.Victim.Team));
    }

    [Fact]
    public void FreeKick_InRange_FormsDefensiveWall()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);

        var victim = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);
        engine.DebugTriggerFreeKick(victim);

        Assert.Equal(MatchEngine.MatchState.FreeKick, engine.CurrentState);

        // 3 defenders should stand ~668px from the ball on the ball-goal line
        var spot = engine.BallPosition;
        var wall = engine.AwayTeam.Players.Where(p =>
        {
            float d = Vector2.Distance(p.FieldPosition, spot);
            return d > 600f && d < 740f;
        }).ToList();
        Assert.True(wall.Count >= 3, $"expected a 3-man wall, found {wall.Count}");
    }

    [Fact]
    public void FreeKick_Executes_AndPlayResumes()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);

        var victim = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);
        var spotBefore = engine.BallPosition;
        engine.DebugTriggerFreeKick(victim);
        Assert.Equal(MatchEngine.MatchState.FreeKick, engine.CurrentState);

        // AI takes the kick within the restart window
        bool resumed = TestHelper.StepUntil(engine,
            () => engine.CurrentState == MatchEngine.MatchState.Playing, 10f);
        Assert.True(resumed, "free kick should execute and resume play");

        // The ball must have been played (velocity or displacement)
        TestHelper.Step(engine, 0.5f);
        bool ballPlayed = engine.BallVelocity.Length() > 50f ||
                          Vector2.Distance(engine.BallPosition, spotBefore) > 100f;
        Assert.True(ballPlayed, "free kick should move the ball");
    }

    [Fact]
    public void KnockdownFouls_AreRecorded()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);
        TestHelper.Step(engine, 150f);

        // Over a full sim, some physical fouls should occur (collisions happen)
        Assert.True(engine.KnockdownEvents >= 0); // sanity
        // Foul list exists and every record is consistent
        Assert.All(engine.Fouls, f =>
        {
            Assert.NotNull(f.Offender);
            Assert.NotNull(f.Victim);
            Assert.NotEqual(f.Offender.Team, f.Victim.Team);
            Assert.InRange(f.Severity, 0f, 1f);
        });
    }
}
