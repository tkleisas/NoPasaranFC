using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Core MatchEngine simulation rules: goals, attribution, boundaries,
/// halftime, kickoff safety, dribble glue, charged shots.</summary>
public class MatchEngineTests
{
    [Fact]
    public void GoalAtRightGoal_ScoresForHome()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth + 30,
            MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
        engine.BallVelocity = Vector2.Zero;
        TestHelper.Step(engine, 1.2f); // goal celebration starts after a 0.5s delay

        Assert.Equal(1, engine.HomeScore);
        Assert.Equal(0, engine.AwayScore);
        Assert.Contains(engine.CurrentState, new[]
        {
            MatchEngine.MatchState.GoalCelebration,
            MatchEngine.MatchState.Countdown, // no celebration routines -> instant handoff
        });
    }

    [Fact]
    public void GoalAttribution_UsesKicker_NotToucher()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var homePlayer = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        var awayPlayer = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);

        // Away player KICKS toward the home goal; a home defender merely touches it on the way in
        engine.RegisterKick(awayPlayer);
        engine.LastPlayerTouchedBall = homePlayer; // contact after the kick
        // Move the GK out of the play so the shot crosses unopposed
        var gk = engine.HomeTeam.Players.First(p => p.Position == PlayerPosition.Goalkeeper);
        gk.FieldPosition = new Vector2(gk.FieldPosition.X, MatchEngine.StadiumMargin + 300f);
        engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + 300f,
            MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
        engine.BallVelocity = new Vector2(-3000f, 0f); // crosses within ~2 frames
        TestHelper.Step(engine, 0.5f);

        Assert.Equal(1, engine.AwayScore);
        Assert.Equal(awayPlayer.Team, engine.LastKicker.Team); // kicker's team, not the toucher's
        Assert.NotEqual(homePlayer.Team, engine.LastKicker.Team); // not an own goal
    }

    [Fact]
    public void BallOutOffDefender_GivesCornerKick()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        // Home defender touches last, ball exits over the home (left) goal line, wide of the goal
        engine.LastPlayerTouchedBall = engine.HomeTeam.Players.First(p => p.IsStarting);
        engine.BallPosition = new Vector2(-100f, MatchEngine.StadiumMargin + 300f);
        engine.BallVelocity = Vector2.Zero;
        TestHelper.Step(engine, 0.5f);

        Assert.Equal(MatchEngine.MatchState.CornerKick, engine.CurrentState);
    }

    [Fact]
    public void BallOutOffAttacker_GivesGoalKick()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        // Away attacker touches last, ball exits over the home (left) goal line, wide
        engine.LastPlayerTouchedBall = engine.AwayTeam.Players.First(p => p.IsStarting);
        engine.BallPosition = new Vector2(-100f, MatchEngine.StadiumMargin + 300f);
        engine.BallVelocity = Vector2.Zero;
        TestHelper.Step(engine, 0.5f);

        Assert.Equal(MatchEngine.MatchState.GoalKick, engine.CurrentState);
    }

    [Fact]
    public void Halftime_TriggersAt45_AndSecondHalfResumes()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        // Fast-forward: hold MatchTime just before halftime via many small steps
        bool reachedHalftime = TestHelper.StepUntil(engine,
            () => engine.CurrentState == MatchEngine.MatchState.HalfTime, timeoutSeconds: 120f);
        Assert.True(reachedHalftime, "halftime should trigger at 45'");
        float halftimeTime = engine.MatchTime;
        Assert.InRange(halftimeTime, 45f, 46f);

        // Frozen while in HalfTime
        TestHelper.Step(engine, 1f);
        Assert.Equal(halftimeTime, engine.MatchTime);

        // Resume: second half starts with a countdown at the same MatchTime
        engine.StartSecondHalf();
        Assert.Equal(MatchEngine.MatchState.Countdown, engine.CurrentState);
        TestHelper.Step(engine, 4.5f);
        Assert.Equal(MatchEngine.MatchState.Playing, engine.CurrentState);
        Assert.True(engine.MatchTime > halftimeTime);
    }

    [Fact]
    public void SecondHalf_TeamsSwitchSides()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        float centerX = MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f;
        var homeGk = engine.HomeTeam.Players.First(p => p.Position == PlayerPosition.Goalkeeper);
        var awayGk = engine.AwayTeam.Players.First(p => p.Position == PlayerPosition.Goalkeeper);

        // Half 1: home defends left, away defends right
        Assert.Equal(1, engine.Half);
        Assert.Equal(1, engine.AttackSign(engine.HomeTeam));
        Assert.Equal(-1, engine.AttackSign(engine.AwayTeam));
        Assert.True(homeGk.FieldPosition.X < centerX, "home GK should start on the left");
        Assert.True(awayGk.FieldPosition.X > centerX, "away GK should start on the right");

        engine.StartSecondHalf();

        // Half 2: sides switched
        Assert.Equal(2, engine.Half);
        Assert.Equal(-1, engine.AttackSign(engine.HomeTeam));
        Assert.Equal(1, engine.AttackSign(engine.AwayTeam));
        Assert.True(homeGk.FieldPosition.X > centerX, "home GK should defend the right in half 2");
        Assert.True(awayGk.FieldPosition.X < centerX, "away GK should defend the left in half 2");
    }

    [Fact]
    public void SecondHalf_LeftGoal_ScoresForHome()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);
        engine.StartSecondHalf();
        TestHelper.Step(engine, 4.5f); // countdown -> Playing
        Assert.Equal(MatchEngine.MatchState.Playing, engine.CurrentState);

        // The away team defends the LEFT goal in half 2 - move their GK away
        var awayGk = engine.AwayTeam.Players.First(p => p.Position == PlayerPosition.Goalkeeper);
        awayGk.FieldPosition = new Vector2(awayGk.FieldPosition.X, MatchEngine.StadiumMargin + 300f);
        engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + 300f,
            MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
        engine.BallVelocity = new Vector2(-3000f, 0f);
        TestHelper.Step(engine, 1.0f);

        Assert.Equal(1, engine.HomeScore); // home attacks the left goal in half 2
        Assert.Equal(0, engine.AwayScore);
        Assert.Equal(engine.HomeTeam, engine.LastCelebratingTeam);
    }

    [Fact]
    public void SecondHalf_KickoffGoesToTheOtherTeam()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        int firstHalfKickoff = engine.KickoffTeamId;
        Assert.Equal(engine.ControlledPlayer.TeamId, firstHalfKickoff); // player's team kicks off half 1

        engine.StartSecondHalf();

        Assert.NotEqual(firstHalfKickoff, engine.KickoffTeamId);
        Assert.Contains(engine.KickoffTeamId, new[] { engine.HomeTeam.Id, engine.AwayTeam.Id });
    }

    [Fact]
    public void KickoffSafetyNet_ReleasesStuckKickoffAfter4Seconds()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        // Fresh kickoff positions reset KickoffTaken
        engine.StartSecondHalf();
        TestHelper.Step(engine, 4.5f); // countdown done -> Playing, kickoff "not taken"
        Assert.Equal(MatchEngine.MatchState.Playing, engine.CurrentState);

        // With no touches at all, the safety net must release within ~4s
        bool released = TestHelper.StepUntil(engine, () => engine.KickoffTaken, timeoutSeconds: 6f);
        Assert.True(released, "kickoff safety net should force-release a stuck kickoff");
    }

    [Fact]
    public void DribbleGlue_BallTracksCarrier_AndCarrierIsSlower()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);
        GameSettings.Instance.BallControl = "Easy";

        var cp = engine.ControlledPlayer;
        Assert.NotNull(cp);

        // Far top-left corner: no AI chaser can interfere within the test window
        var openSpot = new Vector2(MatchEngine.StadiumMargin + 250f, MatchEngine.StadiumMargin + 250f);
        cp.FieldPosition = openSpot;
        engine.BallPosition = openSpot;
        engine.BallVelocity = Vector2.Zero;
        engine.LastPlayerTouchedBall = cp;

        var runRight = new Vector2(1, 0);
        TestHelper.Step(engine, 1.0f, move: runRight);

        float dist = Vector2.Distance(cp.FieldPosition, engine.BallPosition);
        Assert.True(dist < 100f, $"glued ball should stay near the carrier (dist={dist:F0}px)");
        Assert.True(engine.BallPosition.X > openSpot.X + 150f, "ball should travel with the carrier");

        // Carrier slowdown: covered by the 75% factor - compare speed vs free run
        float withBallX = cp.FieldPosition.X;
        engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + 100f, openSpot.Y); // ball left behind, out of range
        TestHelper.Step(engine, 1f, move: runRight);
        float freeRun = cp.FieldPosition.X - withBallX;
        float gluedRun = withBallX - openSpot.X;
        Assert.True(gluedRun < freeRun * 0.9f,
            $"carrying should be slower (with ball {gluedRun:F0}px/s vs free {freeRun:F0}px/s)");
    }

    [Fact]
    public void ChargedShot_FiresWithPower()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);
        GameSettings.Instance.BallControl = "Easy";

        var cp = engine.ControlledPlayer;
        cp.FieldPosition = new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f,
            MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
        engine.BallPosition = cp.FieldPosition + new Vector2(30f, 0f);
        engine.BallVelocity = Vector2.Zero;
        engine.LastPlayerTouchedBall = cp;

        // Charge ~0.6s while standing, then release
        TestHelper.Step(engine, 0.6f, move: Vector2.Zero, shoot: true);
        TestHelper.Step(engine, 0.1f, move: Vector2.Zero, shoot: false);

        float speed = engine.BallVelocity.Length();
        Assert.True(speed > 600f, $"charged shot should be fast (speed={speed:F0}px/s)");
    }

    [Fact]
    public void SimulatedMatch_150s_ProducesShots_NoFreeze_NoNaN()
    {
        var engine = TestHelper.MakeEngineFromSeedJson(seed: 42);
        TestHelper.ReachPlaying(engine);
        TestHelper.Step(engine, 150f);

        Assert.False(float.IsNaN(engine.BallPosition.X) || float.IsNaN(engine.BallPosition.Y));
        Assert.True(engine.MatchTime > 0f);
        // The tuned AI must produce attacking output within 150s of open play
        int shots = engine.GetAllPlayers().Sum(p =>
            (p.AIController as AIController)?.ShotsAttempted ?? 0);
        Assert.True(shots > 0, "AI should attempt shots within 150 simulated seconds");
        // No wild oscillation: bounded direction reversals per player-second
        Assert.True(engine.KnockdownEvents < 30, "knockdown rate should stay sane");
    }

    [Fact]
    public void OwnGoal_BenefitingTeamCelebrates()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        // A home player kicks the ball into his OWN (left) net -> away team benefits
        var homePlayer = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        engine.RegisterKick(homePlayer);
        // Move the home GK out of the play so the ball crosses unopposed
        var gk = engine.HomeTeam.Players.First(p => p.Position == PlayerPosition.Goalkeeper);
        gk.FieldPosition = new Vector2(gk.FieldPosition.X, MatchEngine.StadiumMargin + 300f);
        engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + 300f,
            MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
        engine.BallVelocity = new Vector2(-3000f, 0f);
        TestHelper.Step(engine, 1.0f); // goal + 0.5s delay -> celebration running

        Assert.Equal(1, engine.AwayScore);
        Assert.Equal(engine.AwayTeam, engine.LastCelebratingTeam);
        Assert.NotNull(engine.LastCelebrationScorer);
        Assert.Equal(engine.AwayTeam, engine.LastCelebrationScorer.Team); // NOT the home own-goal kicker
    }

    [Fact]
    public void StaleKicker_BenefitingTeamCelebrates()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        // An away player kicked long ago (stale LastKicker); the ball still ends
        // up in the away (right) net -> HOME benefits and must celebrate
        var awayPlayer = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        engine.RegisterKick(awayPlayer);
        // Move the away GK out of the play
        var gk = engine.AwayTeam.Players.First(p => p.Position == PlayerPosition.Goalkeeper);
        gk.FieldPosition = new Vector2(gk.FieldPosition.X, MatchEngine.StadiumMargin + 300f);
        engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth - 300f,
            MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
        engine.BallVelocity = new Vector2(3000f, 0f);
        TestHelper.Step(engine, 1.0f);

        Assert.Equal(1, engine.HomeScore);
        Assert.Equal(engine.HomeTeam, engine.LastCelebratingTeam);
        Assert.NotNull(engine.LastCelebrationScorer);
        Assert.Equal(engine.HomeTeam, engine.LastCelebrationScorer.Team); // NOT the stale away kicker
    }
}
