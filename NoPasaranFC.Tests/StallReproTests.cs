using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>
/// Regression: live, the renderer (MatchOfficials) drives RefereePosition and
/// stops ~2m (146px) from the booked player. The engine's arrival check used to
/// demand &lt;40px, so CardPhase stayed Going forever, RestartTimer stayed frozen
/// and the set piece deadlocked until a human pressed skip (observed: a free
/// kick stalled ~5 minutes with an AFK player). The arrival threshold now
/// accepts the renderer standoff, and a walk-time timeout forces the show.
/// </summary>
public class StallReproTests
{
    [Fact]
    public void CardCutscene_RefStandoff_CutsceneStillCompletes()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var offender = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        var victim = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        engine.DebugForceCard(offender, red: false);
        engine.DebugTriggerFreeKick(victim);

        // Simulate the renderer: every frame, park the ref 2m (146px) from the
        // offender - inside the new 160px threshold, beyond the old 40px one
        bool playing = TestHelper.StepUntil(engine, () =>
        {
            engine.RefereePosition = offender.FieldPosition + new Vector2(146f, 0f);
            return engine.CurrentState == MatchEngine.MatchState.Playing;
        }, 20f);

        Assert.True(playing, "free kick should execute despite the renderer standoff");
        Assert.Equal(MatchEngine.RefCardPhase.None, engine.CardPhase);
    }

    [Fact]
    public void CardCutscene_RefNeverArrives_TimeoutForcesShow()
    {
        var engine = TestHelper.MakeEngine();
        TestHelper.ReachPlaying(engine);

        var offender = engine.AwayTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        var victim = engine.HomeTeam.Players.First(p => p.IsStarting && p.Position != PlayerPosition.Goalkeeper);
        engine.DebugForceCard(offender, red: false);
        engine.DebugTriggerFreeKick(victim);

        // Simulate a ref that never converges (any renderer behavior): pinned
        // 600px away - arrival can never fire, only the timeout can save us
        bool playing = TestHelper.StepUntil(engine, () =>
        {
            engine.RefereePosition = offender.FieldPosition + new Vector2(600f, 0f);
            return engine.CurrentState == MatchEngine.MatchState.Playing;
        }, 30f);

        Assert.True(playing, "walk-time timeout should force the card show and release the free kick");
        Assert.Equal(MatchEngine.RefCardPhase.None, engine.CardPhase);
    }
}
