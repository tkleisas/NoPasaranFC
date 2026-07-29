using Microsoft.Xna.Framework;
using NoPasaranFC.Graphics3D;
using Xunit;

namespace NoPasaranFC.Tests;

/// <summary>Easter egg rolls: per-match probabilities, venue/weather gating,
/// forced triggers. Rendering is device-side; the roll/schedule logic is not.</summary>
public class EasterEggTests
{
    [Fact]
    public void Rolls_RespectProbabilities_OverManySeeds()
    {
        int seagulls = 0, tornados = 0, foxes = 0, dogs = 0, crows = 0;
        for (int seed = 0; seed < 400; seed++)
        {
            var m = new EasterEggManager(null, null, Venue.Sfageia, isRaining: true, seed: seed);
            string s = m.RolledSummary;
            if (s.Contains("Seagulls")) seagulls++;
            if (s.Contains("Tornado")) tornados++;
            if (s.Contains("Fox")) foxes++;
            if (s.Contains("Dog")) dogs++;
            if (s.Contains("Crows")) crows++;
        }

        Assert.InRange(seagulls, 150, 250); // 50% at Sfageia
        Assert.InRange(tornados, 5, 60);    // 5% in rain
        Assert.InRange(foxes, 15, 70);      // 10%
        Assert.InRange(dogs, 5, 45);        // 5%
        Assert.InRange(crows, 15, 70);      // 10%
    }

    [Fact]
    public void Seagulls_OnlyAtSfageia_TornadoOnlyInRain()
    {
        for (int seed = 0; seed < 100; seed++)
        {
            var dry = new EasterEggManager(null, null, Venue.Sfageia, isRaining: false, seed: seed);
            Assert.DoesNotContain("Tornado", dry.RolledSummary);

            var bahramis = new EasterEggManager(null, null, Venue.Bahramis, isRaining: true, seed: seed);
            Assert.DoesNotContain("Seagulls", bahramis.RolledSummary);
        }
    }

    [Fact]
    public void Trigger_ForcesEvent_RegardlessOfRoll()
    {
        var m = new EasterEggManager(null, null, Venue.Bahramis, isRaining: false, seed: 1);
        Assert.Equal("OK tornado", m.Trigger("tornado"));
        Assert.Equal("OK crows", m.Trigger("crows"));
        Assert.StartsWith("ERR", m.Trigger("dragon"));

        // Update must not throw with active events (null model/engine tolerated)
        m.Update(0.5f, null);
        m.Update(0.5f, null);
    }

    [Fact]
    public void ScheduledEvents_StartOnlyAfterTheirTime()
    {
        // Seed a manager that rolled something, then verify "Started" marks
        // appear in the summary only after enough simulated time
        for (int seed = 0; seed < 20; seed++)
        {
            var m = new EasterEggManager(null, null, Venue.Sfageia, isRaining: true, seed: seed);
            string before = m.RolledSummary;
            if (before == "none") continue;
            Assert.DoesNotContain("*", before); // nothing started at t=0 (except fox@0)
            for (int i = 0; i < 60 * 200; i++)
                m.Update(1f / 60f, null);
            Assert.Contains("*", m.RolledSummary); // everything started by t=200s
        }
    }

    [Fact]
    public void UfoAndBlackout_OnlyRollAtNight()
    {
        var night = new MatchEnvironment(null, "Night", "Clear");
        var day = new MatchEnvironment(null, "Day", "Clear");
        int ufos = 0, blackouts = 0, cats = 0;
        for (int seed = 0; seed < 400; seed++)
        {
            var mn = new EasterEggManager(null, null, Venue.Bahramis, isRaining: false,
                environment: night, seed: seed);
            string s = mn.RolledSummary;
            if (s.Contains("Ufo")) ufos++;
            if (s.Contains("Blackout")) blackouts++;
            if (s.Contains("Cats")) cats++;

            var md = new EasterEggManager(null, null, Venue.Bahramis, isRaining: false,
                environment: day, seed: seed);
            Assert.DoesNotContain("Ufo", md.RolledSummary);
            Assert.DoesNotContain("Blackout", md.RolledSummary);
        }

        Assert.InRange(ufos, 2, 30);      // 3% at night
        Assert.InRange(blackouts, 0, 25); // 2% at night
        Assert.InRange(cats, 2, 30);      // 3%, any conditions
    }

    [Fact]
    public void Trigger_NewEggs_ForceRegardlessOfConditions()
    {
        var m = new EasterEggManager(null, null, Venue.Bahramis, isRaining: false, seed: 1);
        Assert.Equal("OK ufo", m.Trigger("ufo"));
        Assert.Equal("OK blackout", m.Trigger("blackout")); // no environment: no-op, still OK
        Assert.Equal("OK cats", m.Trigger("cats"));

        // Update must not throw with active events (null device/model/engine tolerated)
        m.Update(0.5f, null);
        m.Update(0.5f, null);
    }

    [Fact]
    public void BlackoutFactor_DimsAndRestores_Lighting()
    {
        var env = new MatchEnvironment(null, "Night", "Clear");
        Vector3 tint = env.UnlitTint;
        Color sky = env.SkyColor;
        Assert.Equal(1f, env.BlackoutFactor);
        Assert.Equal(tint, env.ApplyTint(Vector3.One));

        // Lights out: the tint vanishes and the sky goes black
        env.SetBlackout(0f);
        Assert.Equal(Vector3.Zero, env.ApplyTint(Vector3.One));
        Assert.Equal(new Color(0, 0, 0), env.EffectiveSkyColor);

        // Restoring the factor fully restores the lighting (fields never mutated)
        env.SetBlackout(1f);
        Assert.Equal(tint, env.ApplyTint(Vector3.One));
        Assert.Equal(sky, env.EffectiveSkyColor);
        Assert.Equal(tint, env.UnlitTint);
    }

    [Fact]
    public void BlackoutFx_RunsTimeline_AndRestoresLights()
    {
        var env = new MatchEnvironment(null, "Night", "Clear");
        var fx = new BlackoutFx(env, new System.Random(7));
        for (int i = 0; i < 60 * 3; i++) fx.Update(1f / 60f); // t=3s: dark phase
        Assert.False(fx.IsDone);
        Assert.True(env.BlackoutFactor < 0.5f, $"expected dark, got {env.BlackoutFactor}");
        for (int i = 0; i < 60 * 10; i++) fx.Update(1f / 60f); // t=13s: past the end
        Assert.True(fx.IsDone);
        Assert.Equal(1f, env.BlackoutFactor);
    }

    [Fact]
    public void UfoFx_FinishesAfterFlyover()
    {
        var fx = new UfoFx(new System.Random(3));
        for (int i = 0; i < 60 * 10; i++) fx.Update(1f / 60f);
        Assert.False(fx.IsDone); // still crossing/hovering at t=10s
        for (int i = 0; i < 60 * 30; i++) fx.Update(1f / 60f);
        Assert.True(fx.IsDone); // gone by t=40s
    }
}
