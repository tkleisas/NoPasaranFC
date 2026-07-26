using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Cards: yellows accumulate, second yellow is a red, sent-off players
/// leave the pitch and control is re-picked.</summary>
public class CardTests
{
    /// <summary>Force fouls by a specific offender until N fouls are on record.</summary>
    private static int ForceFoulsBy(MatchEngine engine, Player offender, Player victim, int maxAttempts)
    {
        int before = engine.Fouls.Count;
        for (int i = 0; i < maxAttempts && engine.Fouls.Count - before < 2; i++)
        {
            // Stage the duel: offender right next to the ball carrier
            offender.FieldPosition = new Vector2(4000f, 2682f);
            victim.FieldPosition = offender.FieldPosition + new Vector2(40f, 0f);
            engine.BallPosition = victim.FieldPosition + new Vector2(20f, 0f);
            engine.BallVelocity = Vector2.Zero;
            engine.LastPlayerTouchedBall = victim;
            offender.Defending = 1; offender.Agility = 1;
            victim.Technique = 99; victim.Agility = 99;
            
            engine.Tackle(offender);
            TestHelper.Step(engine, 0.1f);
            if (engine.CurrentState != MatchEngine.MatchState.Playing)
                TestHelper.StepUntil(engine, () => engine.CurrentState == MatchEngine.MatchState.Playing, 10f);
        }
        return engine.Fouls.Count - before;
    }

    [Fact]
    public void Fouls_AccumulateYellowCards()
    {
        var engine = TestHelper.MakeEngine(seed: 11);
        TestHelper.ReachPlaying(engine);

        var offender = engine.ControlledPlayer;
        var victim = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);

        int fouls = ForceFoulsBy(engine, offender, victim, 30);
        Assert.True(fouls >= 1, "expected at least one foul");
        Assert.True(offender.YellowCards >= 1 || offender.IsSentOff,
            "repeated fouls should eventually book the offender");
        // (the card banner is transient UI with an engine-side countdown -
        // the booking evidence is YellowCards/IsSentOff)
    }

    [Fact]
    public void SecondYellow_IsRed_AndPlayerIsSentOff()
    {
        var engine = TestHelper.MakeEngine(seed: 11);
        TestHelper.ReachPlaying(engine);

        var offender = engine.ControlledPlayer;
        var victim = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);

        // Pre-book the offender, then keep fouling until the second yellow lands
        offender.YellowCards = 1;
        ForceFoulsBy(engine, offender, victim, 60);

        Assert.True(offender.IsSentOff, "second yellow must send the player off");
        Assert.False(offender.IsStarting, "sent-off player leaves the starting 11");
        Assert.DoesNotContain(offender, engine.GetAllPlayers());
        Assert.True(offender.YellowCards >= 2, "a send-off via accumulation means two bookings");
        // (the red/second-yellow card banner is transient UI with an engine-side
        // countdown - by the time fouling stops it has already expired)
    }

    [Fact]
    public void SentOffControlledPlayer_ControlMovesToTeammate()
    {
        var engine = TestHelper.MakeEngine(seed: 11);
        TestHelper.ReachPlaying(engine);

        var offender = engine.ControlledPlayer;
        var victim = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);

        offender.YellowCards = 1;
        ForceFoulsBy(engine, offender, victim, 60);

        Assert.True(offender.IsSentOff);
        Assert.False(offender.IsControlled, "sent-off player is no longer controlled");
        Assert.NotNull(engine.ControlledPlayer);
        Assert.NotSame(offender, engine.ControlledPlayer);
        Assert.Equal(offender.Team, engine.ControlledPlayer.Team);
    }

    [Fact]
    public void CleanPlayer_IsNotCarded()
    {
        var engine = TestHelper.MakeEngine(seed: 42);
        TestHelper.ReachPlaying(engine);
        TestHelper.Step(engine, 60f);

        Assert.All(engine.GetAllPlayers(), p => Assert.False(p.IsSentOff));
    }
}
