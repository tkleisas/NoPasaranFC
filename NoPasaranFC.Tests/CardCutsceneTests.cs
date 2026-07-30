using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Card cutscene: the ref walks to the offender, the banner shows on
/// arrival (not before), the restart waits, and the skip works.</summary>
public class CardCutsceneTests
{
    private static (MatchEngine engine, Player offender, Player victim) StageCard(int seed)
    {
        var engine = TestHelper.MakeEngine(seed);
        TestHelper.ReachPlaying(engine);
        var offender = engine.ControlledPlayer;
        var victim = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);
        offender.YellowCards = 1; // force a red on the next booking
        engine.DebugForceCard(offender, red: true);
        return (engine, offender, victim);
    }

    [Fact]
    public void Ref_WalksToOffender_BannerShowsOnArrival()
    {
        var (engine, offender, _) = StageCard(11);

        Assert.Equal(MatchEngine.RefCardPhase.Going, engine.CardPhase);
        Assert.Null(engine.LastCardShown); // no banner before the ref arrives
        Assert.Same(offender, engine.CardPlayer);

        var refStart = engine.RefereePosition;
        float walkDistance = Vector2.Distance(refStart, offender.FieldPosition);

        // The ref closes the distance
        TestHelper.Step(engine, walkDistance / 550f + 0.5f);
        Assert.Equal(MatchEngine.RefCardPhase.Showing, engine.CardPhase);
        Assert.NotNull(engine.LastCardShown);
        Assert.True(engine.LastCardShown.Value.IsRed);
        Assert.True(Vector2.Distance(engine.RefereePosition, offender.FieldPosition) < 200f,
            "ref should be at the offender (160px standoff ≈ the renderer's 2m, was 40px)");
    }

    [Fact]
    public void RestartTimer_WaitsForCutscene()
    {
        var engine = TestHelper.MakeEngine(seed: 11);
        TestHelper.ReachPlaying(engine);

        // Stage a foul that produces a free kick AND a card
        var offender = engine.ControlledPlayer;
        var victim = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);
        offender.Defending = 1; offender.Agility = 1;
        victim.Technique = 99; victim.Agility = 99;
        offender.YellowCards = 1;

        bool cutsceneSeen = false;
        for (int i = 0; i < 40 && !cutsceneSeen; i++)
        {
            offender.FieldPosition = new Vector2(4000f, 2682f);
            victim.FieldPosition = offender.FieldPosition + new Vector2(40f, 0f);
            engine.BallPosition = victim.FieldPosition + new Vector2(20f, 0f);
            engine.BallVelocity = Vector2.Zero;
            engine.LastPlayerTouchedBall = victim;
            engine.Tackle(offender);
            TestHelper.Step(engine, 0.1f);
            if (engine.CardPhase != MatchEngine.RefCardPhase.None)
                cutsceneSeen = true;
        }

        Assert.True(cutsceneSeen, "a booking foul should start the cutscene");
        if (engine.CurrentState == MatchEngine.MatchState.FreeKick)
        {
            float timer = engine.RestartTimer;
            TestHelper.Step(engine, 0.5f);
            Assert.True(engine.RestartTimer >= timer,
                "restart must wait while the cutscene plays");
        }
    }

    [Fact]
    public void Skip_MovesRefToPlayer_Immediately()
    {
        var (engine, offender, _) = StageCard(11);

        engine.SkipCardCutscene();
        Assert.Equal(MatchEngine.RefCardPhase.Showing, engine.CardPhase);
        Assert.NotNull(engine.LastCardShown);
    }

    [Fact]
    public void CardedFoul_FreeKickEventuallyExecutes_Headless()
    {
        var engine = TestHelper.MakeEngine(seed: 11);
        TestHelper.ReachPlaying(engine);

        // Stage a booking foul that produces a free kick (midfield)
        var offender = engine.ControlledPlayer;
        var victim = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position == PlayerPosition.Forward);
        offender.Defending = 1; offender.Agility = 1;
        victim.Technique = 99; victim.Agility = 99;
        offender.YellowCards = 1; // guarantee a booking (second yellow -> red)

        bool freeKickWithCard = false;
        for (int i = 0; i < 40 && !freeKickWithCard; i++)
        {
            offender.FieldPosition = new Vector2(4000f, 2682f);
            victim.FieldPosition = offender.FieldPosition + new Vector2(40f, 0f);
            engine.BallPosition = victim.FieldPosition + new Vector2(20f, 0f);
            engine.BallVelocity = Vector2.Zero;
            engine.LastPlayerTouchedBall = victim;
            engine.Tackle(offender);
            TestHelper.Step(engine, 0.1f);
            freeKickWithCard = engine.CurrentState == MatchEngine.MatchState.FreeKick
                && engine.CardPhase != MatchEngine.RefCardPhase.None;
        }
        Assert.True(freeKickWithCard, "expected a free kick with a card cutscene");

        // Regression: without a renderer consuming the banner, the cutscene used
        // to never finish - the frozen RestartTimer deadlocked the set piece
        bool resumed = TestHelper.StepUntil(engine,
            () => engine.CurrentState == MatchEngine.MatchState.Playing, 30f);
        Assert.True(resumed, "free kick must execute headless after the card cutscene");
        Assert.Equal(MatchEngine.RefCardPhase.None, engine.CardPhase);
    }

    [Fact]
    public void Cutscene_Ends_RefResumesNormalDuty()
    {
        var (engine, offender, _) = StageCard(11);

        // Play through the whole cutscene
        engine.SkipCardCutscene();
        // Banner timer is 2.5s; let it expire via the screen-side countdown
        engine.LastCardShown = null; // simulate the UI consuming the banner
        TestHelper.Step(engine, 0.5f);

        Assert.Equal(MatchEngine.RefCardPhase.None, engine.CardPhase);
        // The ref is moving again (or back on duty)
        TestHelper.Step(engine, 2f);
        Assert.Equal(MatchEngine.RefCardPhase.None, engine.CardPhase);
    }
}
