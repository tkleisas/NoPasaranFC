using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;

namespace NoPasaranFC.Tests;

/// <summary>Builders for deterministic engine tests: programmatic teams and a
/// seeded MatchEngine stepped at a fixed 60 Hz.</summary>
public static class TestHelper
{
    public const float Dt = 1f / 60f;

    public static Team MakeTeam(string name, int id, bool playerControlled = false)
    {
        var team = new Team(name, playerControlled) { Id = id, KitName = name };
        var positions = new[]
        {
            PlayerPosition.Goalkeeper,
            PlayerPosition.Defender, PlayerPosition.Defender, PlayerPosition.Defender, PlayerPosition.Defender,
            PlayerPosition.Midfielder, PlayerPosition.Midfielder, PlayerPosition.Midfielder, PlayerPosition.Midfielder,
            PlayerPosition.Forward, PlayerPosition.Forward,
        };
        int number = 1;
        foreach (var pos in positions)
        {
            team.Players.Add(MakePlayer($"{name.Substring(0, Math.Min(4, name.Length))}#{number}", number, pos, team, true));
            number++;
        }
        // Four bench players
        for (int i = 0; i < 4; i++)
        {
            team.Players.Add(MakePlayer($"{name.Substring(0, Math.Min(4, name.Length))}B{i + 1}", number, PlayerPosition.Midfielder, team, false));
            number++;
        }
        return team;
    }

    private static Player MakePlayer(string name, int shirtNumber, PlayerPosition pos, Team team, bool starting)
    {
        return new Player
        {
            Name = name,
            ShirtNumber = shirtNumber,
            Position = pos,
            Role = pos switch
            {
                PlayerPosition.Goalkeeper => PlayerRole.Goalkeeper,
                PlayerPosition.Defender => PlayerRole.CenterBack,
                PlayerPosition.Midfielder => PlayerRole.CentralMidfielder,
                _ => PlayerRole.Striker,
            },
            Team = team,
            TeamId = team.Id,
            IsStarting = starting,
            // Realistic roster-level stats (weak squads never produce shots in sims)
            Speed = 70, Shooting = 70, Passing = 70, Defending = 70,
            Agility = 70, Technique = 70, Stamina = 95f,
        };
    }

    /// <summary>Seeded engine with programmatic teams (no DB, no content).</summary>
    public static MatchEngine MakeEngine(int seed = 42, bool playerControlled = true)
    {
        AIController.DeterministicSeedBase = seed;
        var home = MakeTeam("HOME", 1, playerControlled);
        var away = MakeTeam("AWAY", 2, false);
        var engine = new MatchEngine(home, away, 1280, 720);
        engine.SetRandomSeed(seed);
        return engine;
    }

    /// <summary>Seeded engine with the REAL rosters from teams_seed.json
    /// (NO PASARAN home vs the next team). Mirrors the harness setup.</summary>
    public static MatchEngine MakeEngineFromSeedJson(int seed = 42)
    {
        string jsonPath = FindRepoFile(Path.Combine("Database", "teams_seed.json"));
        Database.TeamSeeder.DeterministicRosterSeed = seed;
        var teams = Database.TeamSeeder.LoadTeamsFromJson(jsonPath);
        var home = teams.First(t => t.Name.Contains("NO PASARAN"));
        var away = teams.First(t => t != home);
        AIController.DeterministicSeedBase = seed;
        var engine = new MatchEngine(home, away, 1280, 720);
        engine.SetRandomSeed(seed);
        return engine;
    }

    private static string FindRepoFile(string relative)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir != null)
        {
            string candidate = Path.Combine(dir.FullName, relative);
            if (File.Exists(candidate)) return candidate;
            dir = dir.Parent;
        }
        throw new FileNotFoundException(relative);
    }

    private static readonly TimeSpan _total = TimeSpan.Zero;

    /// <summary>Advance the engine `seconds` at 60 Hz with the given input.</summary>
    public static void Step(MatchEngine engine, float seconds,
        Vector2 move = default, bool shoot = false, bool pass = false)
    {
        int frames = (int)(seconds / Dt);
        var total = TimeSpan.Zero;
        for (int i = 0; i < frames; i++)
        {
            total += TimeSpan.FromSeconds(Dt);
            engine.Update(new GameTime(total, TimeSpan.FromSeconds(Dt)), move, shoot, pass);
        }
    }

    /// <summary>Step until a predicate holds or the timeout expires; returns the outcome.</summary>
    public static bool StepUntil(MatchEngine engine, Func<bool> predicate, float timeoutSeconds,
        Vector2 move = default, bool shoot = false, bool pass = false)
    {
        int frames = (int)(timeoutSeconds / Dt);
        var total = TimeSpan.Zero;
        for (int i = 0; i < frames; i++)
        {
            if (predicate()) return true;
            total += TimeSpan.FromSeconds(Dt);
            engine.Update(new GameTime(total, TimeSpan.FromSeconds(Dt)), move, shoot, pass);
        }
        return predicate();
    }

    /// <summary>Pre-roll through camera init + countdown into Playing.</summary>
    public static void ReachPlaying(MatchEngine engine, float timeout = 30f)
    {
        bool ok = StepUntil(engine, () => engine.CurrentState == MatchEngine.MatchState.Playing, timeout);
        if (!ok) throw new InvalidOperationException("engine never reached Playing");
    }
}
