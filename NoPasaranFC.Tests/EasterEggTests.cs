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
}
