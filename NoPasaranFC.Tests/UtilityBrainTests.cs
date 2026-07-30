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
        Assert.NotEqual("Dribble", brain.CurrentActionName); // ...and never a carrier action on his own kicked ball

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

    [Fact]
    public void DribbleCommit_KeepsCollectorThroughGlueFlicker_LostBallLatchesOff()
    {
        var player = MakePlayer(5, new Vector2(1000f, 300f));
        var opponent = MakePlayer(21, new Vector2(1100f, 300f));
        opponent.TeamId = 2;
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        // Clean control: he enters Dribble (midfielder, no pressure -> 79.3)
        var ctx = MakeContext(player, new Vector2(1030f, 300f), 1.0f);
        ctx.HasBallPossession = true;
        ctx.BallCarrier = player;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("Dribble", brain.CurrentActionName);

        // Glue flicker: the registration drops (BallCarrier null) while the
        // ball rolls nearby. The commitment must keep him collecting, not
        // bounce him back to ChaseBall.
        for (int i = 0; i < 3; i++)
        {
            ctx = MakeContext(player, new Vector2(1150f, 300f), 1.3f + i * 0.2f);
            ctx.BallCarrier = null;
            ctx.ShouldChaseBall = true; // chase 85.25 would win without the commit
            brain.Update(player, ctx, 0.2f);
            Assert.Equal("Dribble", brain.CurrentActionName);
        }

        // An opponent takes the ball: clearly lost -> latch off, chase resumes
        ctx = MakeContext(player, new Vector2(1150f, 300f), 2.0f);
        ctx.BallCarrier = opponent;
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);
    }

    [Fact]
    public void DribbleEnterMargin_MarginalTouchDoesNotYankChaser()
    {
        var player = MakePlayer(5, new Vector2(1000f, 300f));
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        // Chasing a loose ball (chase 86.5 at 100px: 69 - 2.5 + 20 close)
        var ctx = MakeContext(player, new Vector2(1100f, 300f), 1.0f);
        ctx.BallCarrier = null;
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);

        // A marginal touch: he becomes the registered carrier through the
        // control radius, but the ball is NOT at his feet (deflection).
        // Dribble 79.3 does not clear chase 86.5 + enter margin 6 -> stay chasing
        ctx = MakeContext(player, new Vector2(1100f, 300f), 1.2f);
        ctx.BallCarrier = player;
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);

        // Clean control (ball at his feet) always enters immediately
        ctx = MakeContext(player, new Vector2(1030f, 300f), 1.4f);
        ctx.HasBallPossession = true;
        ctx.BallCarrier = player;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("Dribble", brain.CurrentActionName);
    }

    [Fact]
    public void KickFollowUp_NeverDribblesHisOwnKickedBall()
    {
        // Defender under pressure with two converging opponents: Clear wins
        // (dribble suppressed by the dead-end factor, no pass option)
        var player = MakePlayer(4, new Vector2(1000f, 300f), PlayerPosition.Defender);
        player.Role = PlayerRole.CenterBack;
        var o1 = MakePlayer(21, new Vector2(1100f, 300f));
        var o2 = MakePlayer(22, new Vector2(950f, 350f));
        o1.TeamId = 2; o2.TeamId = 2;
        int clears = 0;
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => clears++);

        var ctx = MakeContext(player, new Vector2(1030f, 300f), 1.0f);
        ctx.HasBallPossession = true;
        ctx.BallCarrier = player;
        ctx.NearestOpponent = o1;
        ctx.Opponents = new List<Player> { o1, o2 };
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal(1, clears);

        // The immediate follow-up must NOT be Dribble: the ball he just
        // cleared is leaving at kick speed (the one-frame Dribble blip was
        // the dominant ChaseBall<->Dribble flap source in the harness)
        Assert.NotEqual("Dribble", brain.CurrentActionName);

        // Next eval, fresh context with the ball gone: he chases it
        ctx = MakeContext(player, new Vector2(1600f, 300f), 1.2f);
        ctx.BallCarrier = null;
        ctx.BallVelocity = new Vector2(2200f, 100f);
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);
    }

    [Fact]
    public void DribbleEnterMargin_FastBallTouchIsADeflectionNotAReception()
    {
        var player = MakePlayer(5, new Vector2(1000f, 300f));
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        // Chasing a ball flying past at shot speed
        var ctx = MakeContext(player, new Vector2(1100f, 300f), 1.0f);
        ctx.BallCarrier = null;
        ctx.BallVelocity = new Vector2(2400f, 300f);
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);

        // It clips him at 60px on the way past: registered possession, but a
        // ball at 2400px/s is a deflection, not a reception - stay chasing
        // (this exact pattern produced 0.1-0.2s Dribble flaps in the harness)
        ctx = MakeContext(player, new Vector2(1060f, 300f), 1.2f);
        ctx.BallCarrier = player;
        ctx.HasBallPossession = true;
        ctx.BallVelocity = new Vector2(2400f, 300f);
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);

        // Once the ball slows below the reception threshold, he takes it
        ctx = MakeContext(player, new Vector2(1030f, 300f), 1.6f);
        ctx.BallCarrier = player;
        ctx.HasBallPossession = true;
        ctx.BallVelocity = new Vector2(300f, 100f);
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("Dribble", brain.CurrentActionName);
    }

    [Fact]
    public void DribbleEnterMargin_StrongDribbleStillEntersOnMarginalTouch()
    {
        // Forward: roleAttack 1.38 -> dribble ~168.6, clears any chase score
        var player = MakePlayer(9, new Vector2(1000f, 300f), PlayerPosition.Forward);
        player.Role = PlayerRole.Striker;
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        var ctx = MakeContext(player, new Vector2(1100f, 300f), 1.0f);
        ctx.BallCarrier = null;
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);

        ctx = MakeContext(player, new Vector2(1100f, 300f), 1.2f);
        ctx.BallCarrier = player;
        ctx.ShouldChaseBall = true;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("Dribble", brain.CurrentActionName);
    }

    private static List<Player> CrowdAround(Vector2 ball, int teammates, int opponents)
    {
        // Crowd members standing on top of the ball (scramble bodies)
        var list = new List<Player>();
        for (int i = 0; i < teammates + opponents; i++)
        {
            var p = new Player
            {
                Id = 100 + i, Name = $"C{i}", ShirtNumber = 90 + i,
                Position = PlayerPosition.Midfielder, Role = PlayerRole.CentralMidfielder,
                TeamId = i < teammates ? 1 : 2, IsStarting = true,
                Speed = 70, Shooting = 70, Passing = 70, Defending = 70,
                Agility = 70, Technique = 70, Stamina = 95f,
                FieldPosition = ball + new Vector2(30f + i * 20f, 20f),
            };
            list.Add(p);
        }
        return list;
    }

    [Fact]
    public void ContestCommit_HoldsThroughRicochet_ReleasesWhenWon()
    {
        var player = MakePlayer(5, new Vector2(2000f, 300f));
        var mate = MakePlayer(8, new Vector2(2280f, 300f)); // on the ball: genuine control
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        AIContext At(Vector2 ball, float t)
        {
            var c = MakeContext(player, ball, t);
            c.ShouldChaseBall = true;
            c.Teammates = CrowdAround(ball, 2, 0);
            c.Opponents = CrowdAround(ball, 0, 1); // 1 + 2 + 1 = scramble
            return c;
        }

        // Designated contestor engages the loose ball in the crowd
        var ctx = At(new Vector2(2300f, 300f), 1.0f);
        ctx.BallCarrier = null;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);

        // Ricochet: a teammate brushes the ball (would normally force
        // hold+10 and close the chase gate) - the commit holds through it
        for (int i = 0; i < 2; i++)
        {
            ctx = At(new Vector2(2300f, 300f), 1.2f + i * 0.3f);
            ctx.BallCarrier = mate;
            ctx.ShouldChaseBall = false;
            brain.Update(player, ctx, 0.2f);
            Assert.Equal("ChaseBall", brain.CurrentActionName);
        }

        // The teammate genuinely wins it (window expires) -> release to hold
        ctx = At(new Vector2(2300f, 300f), 2.2f);
        ctx.BallCarrier = mate;
        ctx.ShouldChaseBall = false;
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("HoldPosition", brain.CurrentActionName);
    }

    [Fact]
    public void ScrambleDiscipline_NonDesignatedHoldsAnticipation()
    {
        var player = MakePlayer(5, new Vector2(2000f, 300f));

        AIContext At(float t, bool crowded, bool designated)
        {
            var c = MakeContext(player, new Vector2(2200f, 300f), t);
            c.BallCarrier = null;
            c.ShouldChaseBall = designated;
            if (crowded)
            {
                c.Teammates = CrowdAround(new Vector2(2200f, 300f), 2, 0);
                c.Opponents = CrowdAround(new Vector2(2200f, 300f), 0, 1);
            }
            return c;
        }
        UtilityBrain FreshBrain() => new UtilityBrain(new Random(1), (p, x, power) => { }, (p, x, power) => { });

        // Pounce conditions (attacking third, loose, close) but a crowd and no
        // designation: discipline says hold anticipation, don't pile in
        var brain1 = FreshBrain();
        brain1.Update(player, At(1.0f, crowded: true, designated: false), 0.2f);
        Assert.Equal("HoldPosition", brain1.CurrentActionName);

        // Same ball, no crowd: the pounce fires normally
        var brain2 = FreshBrain();
        brain2.Update(player, At(1.0f, crowded: false, designated: false), 0.2f);
        Assert.Equal("ChaseBall", brain2.CurrentActionName);

        // The designated contestor is not disciplined
        var brain3 = FreshBrain();
        brain3.Update(player, At(1.0f, crowded: true, designated: true), 0.2f);
        Assert.Equal("ChaseBall", brain3.CurrentActionName);

        // The closest teammate to the ball still pounces in a scramble (the
        // one rebound man - attacking output is preserved)
        var brain4 = FreshBrain();
        var ctx = MakeContext(player, new Vector2(2150f, 300f), 1.0f);
        ctx.BallCarrier = null;
        ctx.ShouldChaseBall = false;
        var far1 = MakePlayer(31, new Vector2(2350f, 250f)); // crowd: near the ball
        var far2 = MakePlayer(32, new Vector2(2350f, 350f)); // but farther than the
        var far3 = MakePlayer(33, new Vector2(2300f, 300f)); // player (dist 150)
        far3.TeamId = 2;
        ctx.Teammates = new List<Player> { far1, far2 };
        ctx.Opponents = new List<Player> { far3 };
        brain4.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain4.CurrentActionName);
    }

    [Fact]
    public void ScrambleDiscipline_ForcedPounceOverrides()
    {
        var player = MakePlayer(5, new Vector2(2000f, 300f));
        var brain = new UtilityBrain(new Random(1), (p, t, power) => { }, (p, t, power) => { });

        var ctx = MakeContext(player, new Vector2(2200f, 300f), 1.0f);
        ctx.BallCarrier = null;
        ctx.ShouldChaseBall = false;
        ctx.ForcedPounce = true; // stall watchdog beats discipline
        ctx.Teammates = CrowdAround(new Vector2(2200f, 300f), 2, 0);
        ctx.Opponents = CrowdAround(new Vector2(2200f, 300f), 0, 1);
        brain.Update(player, ctx, 0.2f);
        Assert.Equal("ChaseBall", brain.CurrentActionName);
    }
}
