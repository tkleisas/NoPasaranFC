using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>5v5 street rules: lineup trimming, pitch/goal dimensions and
/// goal detection. Standard 11v11 must remain the default for all other tests.</summary>
public class StreetModeTests
{
    [Fact]
    public void Street5v5_TrimsToFiveAndUsesStreetDimensions()
    {
        var home = TestHelper.MakeTeam("HOME", 1, playerControlled: true);
        var away = TestHelper.MakeTeam("AWAY", 2);

        var engine = new MatchEngine(home, away, 1280, 720, MatchRules.Street5v5);
        try
        {
            Assert.Equal(5, home.Players.Count(p => p.IsStarting));
            Assert.Equal(5, away.Players.Count(p => p.IsStarting));
            Assert.Equal(10, engine.GetAllPlayers().Count);

            Assert.Equal(MatchRules.Street5v5.FieldWidth, MatchEngine.FieldWidth);
            Assert.Equal(MatchRules.Street5v5.FieldHeight, MatchEngine.FieldHeight);
            Assert.Equal(MatchRules.Street5v5.GoalWidth, MatchEngine.GoalWidth);

            var starters = home.Players
                .Where(p => p.IsStarting)
                .OrderBy(p => p.Position)
                .ToList();
            Assert.Equal(PlayerPosition.Goalkeeper, starters[0].Position);
            Assert.Equal(PlayerPosition.Defender, starters[1].Position);
            Assert.Equal(2, starters.Count(p => p.Position == PlayerPosition.Midfielder));
            Assert.Equal(PlayerPosition.Forward, starters[4].Position);
        }
        finally
        {
            MatchEngine.ApplyRules(MatchRules.Standard);
        }
    }

    [Fact]
    public void Street5v5_GoalInSmallGoalScores()
    {
        var engine = TestHelper.MakeEngine(rules: MatchRules.Street5v5);
        try
        {
            TestHelper.ReachPlaying(engine);

            engine.BallPosition = new Vector2(
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth + 30,
                MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
            engine.BallVelocity = Vector2.Zero;
            TestHelper.Step(engine, 1.2f);

            Assert.Equal(1, engine.HomeScore);
            Assert.Equal(0, engine.AwayScore);
        }
        finally
        {
            MatchEngine.ApplyRules(MatchRules.Standard);
        }
    }

    [Fact]
    public void Street5v5_LowBallBouncesOffTheCage()
    {
        var engine = TestHelper.MakeEngine(rules: MatchRules.Street5v5);
        try
        {
            TestHelper.ReachPlaying(engine);
            engine.BallHeight = 0f;
            engine.BallVerticalVelocity = 0f;
            engine.BallPosition = new Vector2(
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f,
                MatchEngine.StadiumMargin + 1f);
            engine.BallVelocity = new Vector2(0f, -300f);

            TestHelper.Step(engine, 0.15f);

            Assert.True(engine.BallPosition.Y >= MatchEngine.StadiumMargin - 0.01f);
            Assert.True(engine.BallVelocity.Y > 0f, "the cage should reflect the ball back in");
        }
        finally
        {
            MatchEngine.ApplyRules(MatchRules.Standard);
        }
    }

    [Fact]
    public void Street5v5_HighBallOut_IsAKickIn()
    {
        var engine = TestHelper.MakeEngine(rules: MatchRules.Street5v5);
        string restartEvent = null;
        engine.MatchEvent += (kind, team, player, velocity, power) =>
        {
            if (kind == "kick_in" || kind == "throw_in") restartEvent = kind;
        };
        try
        {
            TestHelper.ReachPlaying(engine);
            engine.BallHeight = 1000f; // clears the 3 m cage
            engine.BallVerticalVelocity = 0f;
            engine.BallPosition = new Vector2(
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f,
                MatchEngine.StadiumMargin - 1f);
            engine.BallVelocity = new Vector2(0f, -300f);

            TestHelper.Step(engine, 0.5f);

            Assert.Equal("kick_in", restartEvent);
            Assert.Equal(MatchEngine.MatchState.ThrowIn, engine.CurrentState);
        }
        finally
        {
            MatchEngine.ApplyRules(MatchRules.Standard);
        }
    }

    [Fact]
    public void Prosfygika_NoNetBounceBehindGoalLine()
    {
        var engine = TestHelper.MakeEngine(rules: MatchRules.Prosfygika5v5);
        try
        {
            TestHelper.ReachPlaying(engine);
            float netBack = MatchEngine.StadiumMargin - MatchEngine.GoalDepth - 20f;
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
            engine.BallHeight = 0f;
            engine.BallVerticalVelocity = 0f;
            engine.BallPosition = new Vector2(netBack, centerY);
            engine.BallVelocity = new Vector2(-300f, 0f);

            TestHelper.Step(engine, 0.05f);

            Assert.True(engine.BallVelocity.X < 0f,
                "the ball must keep travelling through the open flower-pot goal");
            Assert.True(engine.BallPosition.X < netBack);
            Assert.Equal(1, engine.HomeScore + engine.AwayScore);
        }
        finally
        {
            MatchEngine.ApplyRules(MatchRules.Standard);
        }
    }

    [Fact]
    public void Prosfygika_FlowerPotPostDoesNotRicochet()
    {
        var engine = TestHelper.MakeEngine(rules: MatchRules.Prosfygika5v5);
        try
        {
            TestHelper.ReachPlaying(engine);
            float goalTop = MatchEngine.StadiumMargin +
                (MatchEngine.FieldHeight - MatchEngine.GoalWidth) / 2f;
            engine.BallHeight = 0f;
            engine.BallVerticalVelocity = 0f;
            engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + 1f, goalTop);
            engine.BallVelocity = new Vector2(-300f, 0f);

            TestHelper.Step(engine, 0.05f);

            Assert.Equal(1, engine.HomeScore + engine.AwayScore);
        }
        finally
        {
            MatchEngine.ApplyRules(MatchRules.Standard);
        }
    }

    [Fact]
    public void Prosfygika_HighBallOverLineScoresWithoutCrossbar()
    {
        var engine = TestHelper.MakeEngine(rules: MatchRules.Prosfygika5v5);
        try
        {
            TestHelper.ReachPlaying(engine);
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
            engine.BallHeight = MatchEngine.GoalPostHeight + 100f;
            engine.BallVerticalVelocity = 0f;
            engine.BallPosition = new Vector2(MatchEngine.StadiumMargin - 1f, centerY);
            engine.BallVelocity = new Vector2(-300f, 0f);

            TestHelper.Step(engine, 0.05f);

            Assert.Equal(1, engine.HomeScore + engine.AwayScore);
        }
        finally
        {
            MatchEngine.ApplyRules(MatchRules.Standard);
        }
    }

    [Fact]
    public void Cage_HighBallOverCrossbarIsGoalKick()
    {
        var engine = TestHelper.MakeEngine(rules: MatchRules.Street5v5);
        try
        {
            TestHelper.ReachPlaying(engine);
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
            engine.BallHeight = MatchEngine.GoalPostHeight + 100f;
            engine.BallVerticalVelocity = 0f;
            engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + 1f, centerY);
            engine.BallVelocity = new Vector2(-300f, 0f);

            TestHelper.Step(engine, 0.05f);

            Assert.Equal(0, engine.HomeScore + engine.AwayScore);
            Assert.Equal(MatchEngine.MatchState.GoalKick, engine.CurrentState);
        }
        finally
        {
            MatchEngine.ApplyRules(MatchRules.Standard);
        }
    }

    [Fact]
    public void StreetLineup_IsOneTwoOne()
    {
        var team = TestHelper.MakeTeam("STREET XI", 1);
        MatchEngine.SelectStreetLineup(team);

        var starters = team.Players.Where(p => p.IsStarting).ToList();
        Assert.Equal(5, starters.Count);
        Assert.Single(starters, p => p.Position == PlayerPosition.Goalkeeper);
        Assert.Single(starters, p => p.Position == PlayerPosition.Defender);
        Assert.Equal(2, starters.Count(p => p.Position == PlayerPosition.Midfielder));
        Assert.Single(starters, p => p.Position == PlayerPosition.Forward);
    }

    [Fact]
    public void StandardMatchAfterStreet_ResetsPitchAndLineup()
    {
        var streetHome = TestHelper.MakeTeam("STREET", 1);
        var streetAway = TestHelper.MakeTeam("STREET2", 2);
        _ = new MatchEngine(streetHome, streetAway, 1280, 720, MatchRules.Street5v5);

        var standard = TestHelper.MakeEngine();
        try
        {
            Assert.Equal(11, standard.HomeTeam.Players.Count(p => p.IsStarting));
            Assert.Equal(7665f, MatchEngine.FieldWidth);
            Assert.Equal(534f, MatchEngine.GoalWidth);
        }
        finally
        {
            MatchEngine.ApplyRules(MatchRules.Standard);
        }
    }
}
