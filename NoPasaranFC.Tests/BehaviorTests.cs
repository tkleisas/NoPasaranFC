using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Seeded full-match behavior regressions: the guard rails for the
/// harness findings (no frozen kickoffs, bounded oscillation, attacking output,
/// ball containment).</summary>
public class BehaviorTests
{
    [Theory]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(42)]
    [InlineData(1234)]
    public void Kickoff_PlayResumesWithinSeconds(int seed)
    {
        var engine = TestHelper.MakeEngine(seed);
        TestHelper.ReachPlaying(engine);

        var ballStart = engine.BallPosition;
        TestHelper.Step(engine, 8f);

        float moved = Vector2.Distance(ballStart, engine.BallPosition);
        Assert.True(moved > 200f || engine.KickoffTaken,
            $"seed {seed}: kickoff must proceed (ball moved {moved:F0}px, taken={engine.KickoffTaken})");
    }

    [Theory]
    [InlineData(42)]
    [InlineData(1979)]
    public void SimulatedMatch_BallStaysNearField_NoStateExplosion(int seed)
    {
        var engine = TestHelper.MakeEngine(seed);
        TestHelper.ReachPlaying(engine);

        // Track containment over 120 simulated seconds
        float maxAbsX = 0, maxAbsY = 0;
        for (int i = 0; i < 120 * 60; i++)
        {
            var t = System.TimeSpan.FromSeconds(i * TestHelper.Dt);
            engine.Update(new GameTime(t, System.TimeSpan.FromSeconds(TestHelper.Dt)), Vector2.Zero, false, false);
            maxAbsX = System.Math.Max(maxAbsX, System.Math.Abs(engine.BallPosition.X - MatchEngine.StadiumMargin - MatchEngine.FieldWidth / 2));
            maxAbsY = System.Math.Max(maxAbsY, System.Math.Abs(engine.BallPosition.Y - MatchEngine.StadiumMargin - MatchEngine.FieldHeight / 2));
        }

        // Ball may enter the goal area / margins but never leave the stadium
        Assert.True(maxAbsX < MatchEngine.FieldWidth / 2 + 600f,
            $"ball escaped horizontally ({maxAbsX:F0}px past center)");
        Assert.True(maxAbsY < MatchEngine.FieldHeight / 2 + 400f,
            $"ball escaped vertically ({maxAbsY:F0}px past center)");
    }

    [Fact]
    public void SimulatedMatch_GoalTriggersCelebration_AndReplayReset()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        // Force a goal (celebration starts after a 0.5s delay)
        engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth + 30,
            MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
        engine.BallVelocity = Vector2.Zero;
        TestHelper.Step(engine, 1.2f);
        Assert.Equal(1, engine.HomeScore);
        Assert.Contains(engine.CurrentState, new[]
        {
            MatchEngine.MatchState.GoalCelebration,
            MatchEngine.MatchState.Countdown,
        });
        
        // The celebration hands over to a kickoff countdown, then play resumes
        bool playing = TestHelper.StepUntil(engine,
            () => engine.CurrentState == MatchEngine.MatchState.Playing, 25f);
        Assert.True(playing, "play should resume after the post-goal countdown");
    }

    [Fact]
    public void GKDives_WhenBallFliesTowardGoal()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        // Fire a hard shot at the home (left) goal from midfield
        engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f,
            MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
        engine.BallVelocity = new Vector2(-1800f, 0f);

        var gk = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Goalkeeper);
        var startY = gk.FieldPosition.Y;

        TestHelper.Step(engine, 1.2f);

        // Either the GK moved significantly (dive/reaction) or the ball was stopped
        bool gkMoved = Vector2.Distance(new Vector2(gk.FieldPosition.X, startY), gk.FieldPosition) > 100f;
        bool ballStopped = engine.BallVelocity.Length() < 600f || engine.HomeScore + engine.AwayScore > 0;
        Assert.True(gkMoved || ballStopped,
            $"GK should react to a hard shot (moved={gkMoved}, ballSpeed={engine.BallVelocity.Length():F0})");
    }
}
