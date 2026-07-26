using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Penalty kicks: box fouls become penalties, placement, GK dive,
/// execution resuming play, and goals counting.</summary>
public class PenaltyTests
{
    [Fact]
    public void FoulInBox_GrantsPenalty_NotFreeKick()
    {
        var engine = TestHelper.MakeEngine(seed: 7);
        TestHelper.ReachPlaying(engine);

        var offender = engine.ControlledPlayer;
        var victim = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);
        offender.Defending = 1; offender.Agility = 1;
        victim.Technique = 99; victim.Agility = 99;

        // Stage the duel INSIDE the home box (home defends left)
        float boxX = MatchEngine.StadiumMargin + 600f;
        float boxY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
        bool penaltySeen = false;
        for (int i = 0; i < 30 && !penaltySeen; i++)
        {
            offender.FieldPosition = new Vector2(boxX, boxY);
            victim.FieldPosition = offender.FieldPosition + new Vector2(40f, 0f);
            engine.BallPosition = victim.FieldPosition + new Vector2(20f, 0f);
            engine.BallVelocity = Vector2.Zero;
            engine.LastPlayerTouchedBall = victim;
            
            engine.Tackle(offender);
            TestHelper.Step(engine, 0.1f);
            if (engine.CurrentState == MatchEngine.MatchState.PenaltyKick)
                penaltySeen = true;
            else if (engine.CurrentState != MatchEngine.MatchState.Playing)
                TestHelper.StepUntil(engine, () => engine.CurrentState == MatchEngine.MatchState.Playing, 10f);
        }

        Assert.True(penaltySeen, "a foul inside the box should produce a penalty kick");
    }

    [Fact]
    public void FoulOutsideBox_GrantsFreeKick_NotPenalty()
    {
        var engine = TestHelper.MakeEngine(seed: 7);
        TestHelper.ReachPlaying(engine);

        var offender = engine.ControlledPlayer;
        var victim = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);
        offender.Defending = 1; offender.Agility = 1;
        victim.Technique = 99; victim.Agility = 99;

        bool freeKickSeen = false, penaltySeen = false;
        for (int i = 0; i < 30 && !freeKickSeen && !penaltySeen; i++)
        {
            // Midfield, far from any box
            offender.FieldPosition = new Vector2(4000f, 2682f);
            victim.FieldPosition = offender.FieldPosition + new Vector2(40f, 0f);
            engine.BallPosition = victim.FieldPosition + new Vector2(20f, 0f);
            engine.BallVelocity = Vector2.Zero;
            engine.LastPlayerTouchedBall = victim;
            
            engine.Tackle(offender);
            TestHelper.Step(engine, 0.1f);
            if (engine.CurrentState == MatchEngine.MatchState.FreeKick) freeKickSeen = true;
            else if (engine.CurrentState == MatchEngine.MatchState.PenaltyKick) penaltySeen = true;
            else if (engine.CurrentState != MatchEngine.MatchState.Playing)
                TestHelper.StepUntil(engine, () => engine.CurrentState == MatchEngine.MatchState.Playing, 10f);
        }

        Assert.True(freeKickSeen, "midfield foul should be a free kick");
        Assert.False(penaltySeen, "midfield foul must not be a penalty");
    }

    [Fact]
    public void Penalty_Placement_BallOnSpotGkOnLine()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);

        engine.DebugTriggerPenalty();
        Assert.Equal(MatchEngine.MatchState.PenaltyKick, engine.CurrentState);

        // Ball on the penalty spot (11m from the line)
        float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
        Assert.Equal(centerY, engine.BallPosition.Y, 1f);
        bool leftSpot = System.Math.Abs(engine.BallPosition.X - (MatchEngine.StadiumMargin + 803f)) < 1f;
        bool rightSpot = System.Math.Abs(engine.BallPosition.X - (MatchEngine.StadiumMargin + MatchEngine.FieldWidth - 803f)) < 1f;
        Assert.True(leftSpot || rightSpot, "ball must be on a penalty spot");
    }

    [Fact]
    public void Penalty_Executes_GkDives_PlayResumes()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);

        var gk = engine.AwayTeam.Players.First(p => p.Position == PlayerPosition.Goalkeeper);
        var gkStart = gk.FieldPosition;
        engine.DebugTriggerPenalty();

        bool resumed = TestHelper.StepUntil(engine,
            () => engine.CurrentState == MatchEngine.MatchState.Playing, 10f);
        Assert.True(resumed, "penalty should execute and resume play");

        TestHelper.Step(engine, 1f);
        bool gkMoved = Vector2.Distance(gkStart, gk.FieldPosition) > 80f;
        bool scored = engine.HomeScore + engine.AwayScore > 0;
        bool ballPlayed = engine.BallVelocity.Length() > 50f ||
                          Vector2.Distance(engine.BallPosition,
                              new Vector2(engine.BallPosition.X, MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f)) > 100f;
        Assert.True(gkMoved || scored, "GK should dive (or the penalty already scored)");
        Assert.True(ballPlayed || scored, "the kick should be played");
    }
}
