using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Gameplay.UtilityAI;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Team-coordination arc: SECOND defender (cover), pass offers,
/// attacking mutual spacing. Engine-based for the designations (stability
/// margins), brain-level for the target adjustments.</summary>
public class TeamCoordinationTests
{
    private static Player MakePlayer(int id, Vector2 pos, PlayerPosition position = PlayerPosition.Midfielder)
    {
        return new Player
        {
            Id = id,
            Name = $"P{id}",
            ShirtNumber = id,
            Position = position,
            Role = position == PlayerPosition.Forward ? PlayerRole.Striker : PlayerRole.CentralMidfielder,
            TeamId = 1,
            IsStarting = true,
            Speed = 70, Shooting = 70, Passing = 70, Defending = 70,
            Agility = 70, Technique = 70, Stamina = 95f,
            FieldPosition = pos,
            HomePosition = pos,
        };
    }

    private static AIContext MakeContext(Player player, Vector2 ballPos, float matchTime)
    {
        return new AIContext
        {
            BallPosition = ballPos,
            BallVelocity = Vector2.Zero,
            DistanceToBall = Vector2.Distance(player.FieldPosition, ballPos),
            OwnGoalCenter = new Vector2(150f, 300f),
            OpponentGoalCenter = new Vector2(3150f, 300f),
            AttackSign = 1f,
            MatchTime = matchTime,
            KickoffTaken = true,
            Teammates = new List<Player>(),
            Opponents = new List<Player>(),
            Random = new Random(1),
        };
    }

    private static (MatchEngine engine, AIBehaviorManager mgr, Team home, Team away, Player carrier)
        StagedEngine()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);
        var home = engine.HomeTeam;
        var away = engine.AwayTeam;
        var carrier = away.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        carrier.FieldPosition = new Vector2(4000f, 2882f);
        engine.BallPosition = carrier.FieldPosition + new Vector2(10f, 0f);
        engine.LastPlayerTouchedBall = carrier;
        // Everyone else far away so the ranking is deterministic
        foreach (var p in home.Players.Where(p => p.IsStarting))
            p.FieldPosition = new Vector2(500f, 1000f);
        foreach (var p in away.Players.Where(p => p.IsStarting && p != carrier))
            p.FieldPosition = new Vector2(7500f, 1000f);
        return (engine, new AIBehaviorManager(engine), home, away, carrier);
    }

    private static Player ByShirt(Team team, int shirt) =>
        team.Players.First(p => p.IsStarting && p.ShirtNumber == shirt);

    [Fact]
    public void SecondDefender_CoversGoalSideOfCarrier_DoesNotDiveIn()
    {
        var (engine, mgr, home, away, carrier) = StagedEngine();
        var h1 = ByShirt(home, 5);
        var h2 = ByShirt(home, 7);
        var h3 = ByShirt(home, 8);
        h1.FieldPosition = new Vector2(4200f, 2882f); // closest -> FIRST defender (press)
        h2.FieldPosition = new Vector2(4400f, 2882f); // second -> cover
        h3.FieldPosition = new Vector2(4450f, 2882f);

        var ctxPress = mgr.BuildAIContext(h1);
        Assert.True(ctxPress.ShouldChaseBall);        // FIRST defender presses the carrier
        Assert.False(ctxPress.IsCoverDefender);

        var ctxCover = mgr.BuildAIContext(h2);
        Assert.True(ctxCover.IsCoverDefender);
        Assert.False(ctxCover.ShouldChaseBall);       // cover does NOT also dive in

        // The contain point sits between the carrier and the covered goal
        Vector2 ownGoal = engine.GetOwnGoalCenter(home);
        float offset = Vector2.Distance(ctxCover.CoverPoint, carrier.FieldPosition);
        Assert.True(System.Math.Abs(offset - 400f) < 2f, $"cover offset {offset:F0} != 400");
        Assert.True(System.Math.Abs(ctxCover.CoverPoint.X - ownGoal.X)
            < System.Math.Abs(carrier.FieldPosition.X - ownGoal.X),
            "cover point must be goal-side of the carrier");
    }

    [Fact]
    public void SecondDefender_RoleStableAcrossNoise()
    {
        var (engine, mgr, home, away, carrier) = StagedEngine();
        var h1 = ByShirt(home, 5);
        var h2 = ByShirt(home, 7);
        var h3 = ByShirt(home, 8);
        h1.FieldPosition = new Vector2(4200f, 2882f);
        h2.FieldPosition = new Vector2(4400f, 2882f);
        h3.FieldPosition = new Vector2(4450f, 2882f);

        Assert.True(mgr.BuildAIContext(h2).IsCoverDefender); // h2 designated

        // Noise: h2 drifts a little farther than h3 - NOT a swap (margin 200)
        h2.FieldPosition = new Vector2(4550f, 2882f); // 540 vs h3's 440
        Assert.True(mgr.BuildAIContext(h2).IsCoverDefender);

        // Clearly beaten: h2 much farther (beyond the chaser-stickiness
        // discount too) -> the role moves to h3
        h2.FieldPosition = new Vector2(5500f, 2882f);
        Assert.False(mgr.BuildAIContext(h2).IsCoverDefender);
        Assert.True(mgr.BuildAIContext(h3).IsCoverDefender);
    }

    [Fact]
    public void PassOffers_DesignatedOnCleanControl_Only()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);
        var home = engine.HomeTeam;
        var mgr = new AIBehaviorManager(engine);
        var carrier = ByShirt(home, 9);
        carrier.FieldPosition = new Vector2(4000f, 2882f);
        engine.BallPosition = carrier.FieldPosition + new Vector2(10f, 0f);
        engine.LastPlayerTouchedBall = carrier;

        (int offers, int opposite) CountOffers()
        {
            int o = 0, opp = 0;
            foreach (var p in home.Players.Where(p => p.IsStarting && p != carrier))
            {
                var c = mgr.BuildAIContext(p);
                if (c.IsPassOffer) { o++; if (c.PassOfferOppositeSide) opp++; }
            }
            return (o, opp);
        }

        // Clean control: exactly two offers, the secondary on the opposite lane
        var (offers, opposite) = CountOffers();
        Assert.Equal(2, offers);
        Assert.Equal(1, opposite);

        // Abandoned ball (carrier 300px away): no offers at all
        engine.BallPosition = carrier.FieldPosition + new Vector2(300f, 0f);
        (offers, opposite) = CountOffers();
        Assert.Equal(0, offers);
    }

    [Fact]
    public void AttackingSpacing_AntiStackNudgePushesAwayFromTeammate()
    {
        var player = MakePlayer(5, new Vector2(2000f, 300f));
        var carrier = MakePlayer(8, new Vector2(2100f, 300f)); // 100px away, has the ball
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        var ctx = MakeContext(player, new Vector2(2100f, 300f), 1.0f);
        ctx.BallCarrier = carrier; // teammate in clean control -> attacking branch
        ctx.Teammates = new List<Player> { carrier };
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("HoldPosition", brain.CurrentActionName);

        // The anti-stack nudge pushes the hold target AWAY from the close
        // teammate (target ends up farther from the mate than the player is)
        Assert.True(player.AITargetPositionSet);
        float targetDist = Vector2.Distance(player.AITargetPosition, carrier.FieldPosition);
        float playerDist = Vector2.Distance(player.FieldPosition, carrier.FieldPosition);
        Assert.True(targetDist > playerDist + 100f,
            $"target should be pushed away from the mate ({targetDist:F0} vs {playerDist:F0})");
    }

    [Fact]
    public void PassOfferRun_GoesAheadDiagonal_IntoEmptierLane()
    {
        var player = MakePlayer(5, new Vector2(2000f, 300f));
        var carrier = MakePlayer(8, new Vector2(2100f, 300f));
        var marker = MakePlayer(21, new Vector2(2150f, 250f)); // marks the right lane
        marker.TeamId = 2;
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        var ctx = MakeContext(player, new Vector2(2100f, 300f), 1.0f);
        ctx.BallCarrier = carrier;
        ctx.Teammates = new List<Player> { carrier };
        ctx.Opponents = new List<Player> { marker };
        ctx.IsPassOffer = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("HoldPosition", brain.CurrentActionName);

        // Run goes ahead toward the opponent goal and into the empty (bottom) lane
        Assert.True(player.AITargetPosition.X > 2100f + 500f,
            $"offer run should go ahead: {player.AITargetPosition.X:F0}");
        Assert.True(player.AITargetPosition.Y > 300f + 300f,
            $"offer run should take the emptier lane: {player.AITargetPosition.Y:F0}");

        // The secondary offer takes the OTHER lane (toward the far touchline,
        // clamped to the pitch edge) - strictly on the opposite side from the
        // primary's run
        var brain2 = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });
        var ctx2 = MakeContext(player, new Vector2(2100f, 300f), 1.0f);
        ctx2.BallCarrier = carrier;
        ctx2.Teammates = new List<Player> { carrier };
        ctx2.Opponents = new List<Player> { marker };
        ctx2.IsPassOffer = true;
        ctx2.PassOfferOppositeSide = true;
        brain2.Update(player, ctx2, 0.2f);
        Assert.True(player.AITargetPosition.X > 2100f + 500f,
            $"secondary offer should also run ahead: {player.AITargetPosition.X:F0}");
        Assert.True(player.AITargetPosition.Y < 800f - 300f,
            $"secondary offer should take the opposite lane: {player.AITargetPosition.Y:F0}");
    }
}
