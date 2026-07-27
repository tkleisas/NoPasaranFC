using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Match statistics: goal/assist attribution, shots & on-target,
/// pass completion, saves, set pieces, offsides, possession, simulator
/// distribution, and season-stats DB persistence.</summary>
public class MatchStatsTests
{
        private static void BlastIntoGoal(MatchEngine engine, bool rightGoal)
    {
        // Move the defending GK out of the play, then blast the ball in
        float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
        var gk = (rightGoal ? engine.AwayTeam : engine.HomeTeam).Players
            .First(p => p.Position == PlayerPosition.Goalkeeper);
        gk.FieldPosition = new Vector2(gk.FieldPosition.X, MatchEngine.StadiumMargin + 300f);
        engine.BallPosition = new Vector2(
            rightGoal ? MatchEngine.StadiumMargin + MatchEngine.FieldWidth - 300f
                      : MatchEngine.StadiumMargin + 300f, centerY);
        engine.BallVelocity = new Vector2(rightGoal ? 3000f : -3000f, 0f);
    }

    [Fact]
    public void Goal_ScoredByKicker_AssistForPreviousKicker()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var passer = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Midfielder);
        var scorer = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);

        engine.RegisterKick(passer);   // the "assist"
        engine.MarkShot(scorer);       // the shot...
        engine.RegisterKick(scorer);   // ...and its kick
        BlastIntoGoal(engine, rightGoal: true);
        TestHelper.Step(engine, 1.0f);

        Assert.Equal(1, engine.HomeScore);
        Assert.Equal(1, engine.Stats.For(scorer).Goals);
        Assert.Equal(1, engine.Stats.For(scorer).ShotsOnTarget);
        Assert.Equal(1, engine.Stats.For(passer).Assists);
    }

    [Fact]
    public void OwnGoal_CountsForNobody()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var homePlayer = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        engine.RegisterKick(homePlayer);
        BlastIntoGoal(engine, rightGoal: false); // into his OWN (left) net
        TestHelper.Step(engine, 1.0f);

        Assert.Equal(1, engine.AwayScore);
        Assert.Equal(0, engine.Stats.For(homePlayer).Goals);
    }

    [Fact]
    public void PassCompletion_TeammateTouch_Completes_OpponentTouch_DoesNot()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var passer = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Midfielder);
        var mate = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward && p != passer);
        var opponent = engine.AwayTeam.Players.First(p => p.IsStarting);

        engine.MarkPass(passer);
        engine.LastPlayerTouchedBall = mate; // teammate gets it
        Assert.Equal(1, engine.Stats.For(passer).Passes);
        Assert.Equal(1, engine.Stats.For(passer).PassesCompleted);

        engine.MarkPass(passer);
        engine.LastPlayerTouchedBall = opponent; // intercepted
        Assert.Equal(2, engine.Stats.For(passer).Passes);
        Assert.Equal(1, engine.Stats.For(passer).PassesCompleted);
    }

    [Fact]
    public void GkSave_Counted_WhenFastBallStoppedInBox()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var gk = engine.HomeTeam.Players.First(p => p.Position == PlayerPosition.Goalkeeper);
        float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
        engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + 450f, centerY); // inside the home box
        engine.BallVelocity = new Vector2(-1500f, 0f); // flying at the home (left) goal
        engine.MarkShot(engine.AwayTeam.Players.First(p => p.IsStarting));
        engine.LastPlayerTouchedBall = gk; // the GK stops it

        Assert.Equal(1, engine.Stats.For(gk).Saves);
    }

    [Fact]
    public void SetPieces_CornerAndPenaltyCounted()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        // Real out-of-bounds off a home defender -> corner for AWAY
        engine.LastPlayerTouchedBall = engine.HomeTeam.Players.First(p => p.IsStarting);
        engine.BallPosition = new Vector2(-100f, MatchEngine.StadiumMargin + 300f);
        engine.BallVelocity = Vector2.Zero;
        TestHelper.Step(engine, 0.5f);
        Assert.Equal(MatchEngine.MatchState.CornerKick, engine.CurrentState);
        Assert.Equal(1, engine.Stats.For(engine.AwayTeam).Corners);

        // Return to open play (the corner executes) before staging the penalty
        TestHelper.StepUntil(engine, () => engine.CurrentState == MatchEngine.MatchState.Playing, 10f);
        engine.DebugTriggerPenalty();
        Assert.Equal(1, engine.Stats.For(engine.HomeTeam).Penalties);
    }

    [Fact]
    public void Offside_CountedPerPlayerAndTeam()
    {
        var (engine, offside, _, _) = OffsideTests.StageOffsidePass();
        engine.RegisterKick(engine.HomeTeam.Players.First(p =>
            p.Position == PlayerPosition.Midfielder && p.IsStarting && p != offside));
        engine.BallPosition = offside.FieldPosition + new Vector2(10f, 0f);
        engine.LastPlayerTouchedBall = offside;
        TestHelper.Step(engine, 0.1f);

        Assert.Equal(1, engine.Stats.For(offside).Offsides);
        Assert.Equal(1, engine.Stats.For(engine.HomeTeam).Offsides);
    }

    [Fact]
    public void Possession_AccumulatesForTeamNearBall()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var home = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
        // Park everyone far away except one home player next to the ball
        foreach (var p in engine.GetAllPlayers())
            p.FieldPosition = new Vector2(MatchEngine.StadiumMargin + 100f, MatchEngine.StadiumMargin + 100f);
        home.FieldPosition = new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f, centerY);
        engine.BallPosition = home.FieldPosition + new Vector2(50f, 0f);
        engine.BallVelocity = Vector2.Zero;

        TestHelper.Step(engine, 1.0f);
        Assert.True(engine.Stats.For(engine.HomeTeam).PossessionSeconds > 0.5f,
            $"home should hold possession (got {engine.Stats.For(engine.HomeTeam).PossessionSeconds:F2}s)");
    }

    [Fact]
    public void Simulator_GoalsDistributed_SumMatches_ForwardsLead()
    {
        var championship = new Championship();
        var home = TestHelper.MakeTeam("SIM HOME", 1);
        var away = TestHelper.MakeTeam("SIM AWAY", 2);
        championship.Teams.Add(home);
        championship.Teams.Add(away);

        int fwdGoals = 0, defGoals = 0, totalGoals = 0;
        for (int i = 0; i < 60; i++)
        {
            var match = new Match(1, 2);
            int beforeH = home.Players.Sum(p => p.SeasonGoals);
            int beforeA = away.Players.Sum(p => p.SeasonGoals);
            MatchSimulator.SimulateMatch(championship, match);
            totalGoals += (home.Players.Sum(p => p.SeasonGoals) - beforeH)
                        + (away.Players.Sum(p => p.SeasonGoals) - beforeA);
            Assert.Equal(match.HomeScore + match.AwayScore,
                (home.Players.Sum(p => p.SeasonGoals) - beforeH) +
                (away.Players.Sum(p => p.SeasonGoals) - beforeA));
        }
        fwdGoals = home.Players.Concat(away.Players)
            .Where(p => p.Position == PlayerPosition.Forward).Sum(p => p.SeasonGoals);
        defGoals = home.Players.Concat(away.Players)
            .Where(p => p.Position == PlayerPosition.Defender).Sum(p => p.SeasonGoals);
        Assert.True(fwdGoals > defGoals, $"forwards should outscore defenders ({fwdGoals} vs {defGoals})");
    }

    [Fact]
    public void DbRoundTrip_SeasonStats()
    {
        string tempDb = Path.Combine(Path.GetTempPath(), $"nopasaran_test_{Guid.NewGuid():N}.db");
        try
        {
            var db = new Database.DatabaseManager(tempDb);
            var team = new Team("STATS FC");
            db.SaveTeam(team);
            var player = new Player("Top Scorer", PlayerPosition.Forward)
            {
                TeamId = team.Id,
                SeasonGoals = 7,
                SeasonAssists = 3,
                SeasonYellowCards = 2,
                SeasonRedCards = 1
            };
            db.SavePlayer(player);

            var loaded = db.LoadPlayersForTeam(team.Id).Single();
            Assert.Equal(7, loaded.SeasonGoals);
            Assert.Equal(3, loaded.SeasonAssists);
            Assert.Equal(2, loaded.SeasonYellowCards);
            Assert.Equal(1, loaded.SeasonRedCards);
        }
        finally
        {
            if (File.Exists(tempDb)) File.Delete(tempDb);
        }
    }
}
