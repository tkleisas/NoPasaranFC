using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Gameplay.UtilityAI;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Focused UtilityBrain anti-oscillation regressions: the post-pass
/// commitment window and the chase/hold boundary hysteresis, driven with
/// hand-built contexts (no engine needed).</summary>
public class UtilityBrainTests
{
    private static Player MakePlayer(int id, Vector2 pos, PlayerPosition position = PlayerPosition.Midfielder)
    {
        return new Player
        {
            Id = id,
            Name = $"P{id}",
            ShirtNumber = id,
            Position = position,
            Role = PlayerRole.CentralMidfielder,
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

    [Fact]
    public void PostPassCommit_UnderHitPass_RunAfterPassOwnsThePasser()
    {
        var player = MakePlayer(5, new Vector2(1000f, 300f));
        var receiver = MakePlayer(8, new Vector2(1400f, 300f));
        int passes = 0;
        var brain = new UtilityBrain(new Random(1), (p, t, power) => passes++, (p, t, power) => { });

        // Carrier with the ball at his feet and a great pass option: he passes
        var ctx = MakeContext(player, new Vector2(1010f, 300f), 1.0f);
        ctx.HasBallPossession = true;
        ctx.BallCarrier = player;
        ctx.BestPassTarget = receiver;
        ctx.BestPassScore = 3000f;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal(1, passes);
        Assert.NotEqual("Idle", brain.CurrentActionName); // follow-up decided immediately, no Idle parking

        // The under-hit pass dies 300px away while he is still the registered
        // carrier. The commit window bans Pass/Shoot/Clear on his own kick, so
        // he DRIBBLE-COLLECTS it instead of flapping Pass->Idle->re-eval -
        // and the ball never goes dead.
        for (int i = 0; i < 3; i++)
        {
            ctx = MakeContext(player, new Vector2(1300f, 300f), 1.3f + i * 0.2f);
            ctx.BallCarrier = player;
            ctx.BestPassTarget = receiver;
            ctx.BestPassScore = 3000f;
            brain.Update(player, ctx, 0.2f);
            Assert.Equal("Dribble", brain.CurrentActionName);
        }

        // Once the ball is no longer his (loose, nobody touched it yet), the
        // give-and-go run owns him as before
        ctx = MakeContext(player, new Vector2(1300f, 300f), 2.0f);
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("RunAfterPass", brain.CurrentActionName);

        Assert.Equal(1, passes); // no re-kick of his own pass
    }

    [Fact]
    public void PostPassCommit_EndsWhenSomeoneElseControls_ForcedPounceStillWins()
    {
        var player = MakePlayer(5, new Vector2(1000f, 300f));
        var receiver = MakePlayer(8, new Vector2(1400f, 300f));
        var opponent = MakePlayer(21, new Vector2(1250f, 300f));
        opponent.TeamId = 2;
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        // He passes (same setup as above)
        var ctx = MakeContext(player, new Vector2(1010f, 300f), 1.0f);
        ctx.HasBallPossession = true;
        ctx.BallCarrier = player;
        ctx.BestPassTarget = receiver;
        ctx.BestPassScore = 3000f;
        brain.Update(player, ctx, 0.2f);

        // Inside the window: the stall watchdog must still override the commit
        ctx = MakeContext(player, new Vector2(1300f, 300f), 1.4f);
        ctx.ForcedPounce = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);

        // An opponent takes the ball: the commit latches off (someone else
        // touched it). The give-and-go run itself still owns the passer (its
        // 5s window is the pre-existing behavior), but the carrier/chase
        // suppression is over.
        ctx = MakeContext(player, new Vector2(1150f, 300f), 1.6f);
        ctx.BallCarrier = opponent;
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("RunAfterPass", brain.CurrentActionName);

        // ...and the latch holds: the ball coming back to him (carrier==player
        // again) must NOT resurrect the commit - he is evaluated as a carrier
        ctx = MakeContext(player, new Vector2(1030f, 300f), 1.8f);
        ctx.HasBallPossession = true;
        ctx.BallCarrier = player;
        ctx.BestPassTarget = receiver;
        ctx.BestPassScore = 3000f;
        brain.Update(player, ctx, 0.2f);
        Assert.NotEqual("RunAfterPass", brain.CurrentActionName);
    }

    [Fact]
    public void ChaseHoldBoundary_HysteresisBlocksScoreNoiseFlapping()
    {
        // HoldBaseScore is 47.3; enter needs chase > 53.3, exit needs chase < 43.3
        var player = MakePlayer(5, new Vector2(2000f, 300f));
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        AIContext At(float ballX, float t)
        {
            var c = MakeContext(player, new Vector2(ballX, 300f), t);
            c.BallCarrier = null; // loose ball: the hysteresis applies here
            c.ShouldChaseBall = true;
            return c;
        }

        // Far ball: chase 29 < hold -> HoldPosition
        brain.Update(player, At(400f, 1.0f), 0.2f);
        Assert.Equal("HoldPosition", brain.CurrentActionName);

        // chase 50: above hold but inside the enter margin -> stays holding
        brain.Update(player, At(1240f, 1.2f), 0.2f);
        Assert.Equal("HoldPosition", brain.CurrentActionName);

        // chase 59: clears the enter margin -> ChaseBall
        brain.Update(player, At(1600f, 1.4f), 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);

        // chase 45: below hold but inside the exit margin -> keeps chasing
        brain.Update(player, At(1040f, 1.6f), 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);

        // chase 39: below hold - exit margin -> drops back to HoldPosition
        brain.Update(player, At(800f, 1.8f), 0.2f);
        Assert.Equal("HoldPosition", brain.CurrentActionName);
    }
}
