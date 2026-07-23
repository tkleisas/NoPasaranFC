using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using NoPasaranFC.Database;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;

namespace NoPasaranFC.Harness
{
    /// <summary>
    /// Headless AI test harness. Runs the MatchEngine simulation without any rendering,
    /// logs per-frame state to JSONL and computes behavior metrics (AI state churn,
    /// velocity direction reversals, target tracking, possession).
    ///
    /// Usage: dotnet run --project NoPasaranFC.csproj -- harness &lt;scenario&gt; [--seconds N] [--seed N] [--out &lt;prefix&gt;]
    /// Scenarios: kickoff | center_line_dribble | corner_home | gk_ball
    /// </summary>
    public static class HarnessRunner
    {
        private const float FixedDeltaTime = 1f / 60f;
        private const float ReversalMinSpeed = 30f;   // px/s, both frames
        private const float ReversalMinAngleDeg = 120f;
        private const float BallStationarySpeed = 5f; // px/s

        public static int Run(string[] args)
        {
            if (args.Length < 1)
            {
                Console.Error.WriteLine("usage: harness <scenario> [--seconds N] [--seed N] [--out <prefix>]");
                Console.Error.WriteLine("scenarios: kickoff, center_line_dribble, corner_home, gk_ball");
                return 1;
            }

            string scenario = args[0];
            int seconds = 60;
            int seed = 42;
            string outPrefix = null;

            for (int i = 1; i < args.Length; i++)
            {
                switch (args[i])
                {
                    case "--seconds": seconds = int.Parse(args[++i]); break;
                    case "--seed": seed = int.Parse(args[++i]); break;
                    case "--out": outPrefix = args[++i]; break;
                    default:
                        Console.Error.WriteLine($"unknown argument: {args[i]}");
                        return 1;
                }
            }

            outPrefix ??= $"harness_{scenario}_seed{seed}";

            // Build two full-roster teams straight from the JSON seeder (no database).
            TeamSeeder.DeterministicRosterSeed = seed;
            var teams = TeamSeeder.LoadTeamsFromJson(FindSeedJsonPath());
            var homeTeam = teams.FirstOrDefault(t => t.Name.Contains("NO PASARAN"))
                ?? throw new InvalidOperationException("NO PASARAN! team not found in teams_seed.json");
            var awayTeam = teams.First(t => t != homeTeam);

            // Deterministic per-player AI randoms must be set BEFORE the engine
            // constructor creates the AIControllers.
            AIController.DeterministicSeedBase = seed;
            var engine = new MatchEngine(homeTeam, awayTeam, 1280, 720);
            engine.SetRandomSeed(seed);

            var gameTime = new GameTime();
            void Step()
            {
                gameTime = new GameTime(gameTime.TotalGameTime + TimeSpan.FromSeconds(FixedDeltaTime),
                                        TimeSpan.FromSeconds(FixedDeltaTime));
                engine.Update(gameTime, Vector2.Zero, false, false);
            }

            // Pre-roll the camera init + countdown so the logged duration is all live play.
            int guard = 0;
            while (engine.CurrentState != MatchEngine.MatchState.Playing && guard++ < 60 * 30)
                Step();
            if (engine.CurrentState != MatchEngine.MatchState.Playing)
                throw new InvalidOperationException("Engine never reached Playing state during pre-roll");

            ApplyScenario(scenario, engine);

            var players = engine.GetAllPlayers();
            var logPath = outPrefix + ".log.jsonl";
            var metrics = new MetricsCollector(players, homeTeam, awayTeam);

            using (var writer = new StreamWriter(logPath, false, new UTF8Encoding(false)))
            {
                writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object>
                {
                    ["meta"] = true,
                    ["scenario"] = scenario,
                    ["seed"] = seed,
                    ["seconds"] = seconds,
                    ["fps"] = 60,
                    ["fieldWidth"] = MatchEngine.FieldWidth,
                    ["fieldHeight"] = MatchEngine.FieldHeight,
                    ["stadiumMargin"] = MatchEngine.StadiumMargin,
                    ["homeTeam"] = homeTeam.Name,
                    ["awayTeam"] = awayTeam.Name
                }));

                int totalFrames = seconds * 60;
                for (int frame = 0; frame < totalFrames; frame++)
                {
                    Step();
                    float t = (frame + 1) * FixedDeltaTime;
                    metrics.RecordFrame(engine, players, FixedDeltaTime);
                    WriteFrame(writer, engine, players, homeTeam, t);
                }
            }

            var result = metrics.Finalize(engine, seconds);
            result.Scenario = scenario;
            result.Seed = seed;
            result.Seconds = seconds;
            File.WriteAllText(outPrefix + ".metrics.json",
                JsonSerializer.Serialize(result, new JsonSerializerOptions { WriteIndented = true }),
                new UTF8Encoding(false));
            PrintSummary(result, scenario, seed, seconds, logPath);
            return 0;
        }

        private static string FindSeedJsonPath()
        {
            // dotnet run sets CWD to the project root; fall back to walking up from the
            // output directory so the harness also works from bin/Debug/net9.0.
            var candidates = new List<string> { Path.Combine("Database", "teams_seed.json") };
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir != null)
            {
                candidates.Add(Path.Combine(dir.FullName, "Database", "teams_seed.json"));
                dir = dir.Parent;
            }
            foreach (var path in candidates)
            {
                if (File.Exists(path)) return path;
            }
            throw new FileNotFoundException("teams_seed.json not found (run from the project root)");
        }

        private static void ApplyScenario(string scenario, MatchEngine engine)
        {
            float centerX = MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2;
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;

            switch (scenario)
            {
                case "kickoff":
                    // Plain match start: default initialization is already kickoff.
                    break;

                case "center_line_dribble":
                    // Ball stationary exactly on the center line, players at kickoff positions.
                    engine.BallPosition = new Vector2(centerX, centerY);
                    engine.BallVelocity = Vector2.Zero;
                    engine.BallHeight = 0f;
                    engine.BallVerticalVelocity = 0f;
                    break;

                case "corner_home":
                    // Attacking corner for the home (player-controlled) team.
                    engine.DebugTriggerCornerKick();
                    if (engine.CurrentState != MatchEngine.MatchState.CornerKick)
                        throw new InvalidOperationException("Failed to trigger home corner kick");
                    break;

                case "gk_ball":
                    // Ball stationary 300px in front of the home GK (toward midfield).
                    engine.BallPosition = new Vector2(MatchEngine.StadiumMargin + 50f + 300f, centerY);
                    engine.BallVelocity = Vector2.Zero;
                    engine.BallHeight = 0f;
                    engine.BallVerticalVelocity = 0f;
                    break;

                default:
                    throw new ArgumentException($"unknown scenario: {scenario}");
            }
        }

        private static void WriteFrame(StreamWriter writer, MatchEngine engine, List<Player> players, Team homeTeam, float t)
        {
            var sb = new StringBuilder(2048);
            sb.Append("{\"t\":").Append(F(t));
            sb.Append(",\"state\":\"").Append(engine.CurrentState).Append('\"');
            sb.Append(",\"ball\":{\"x\":").Append(F(engine.BallPosition.X))
              .Append(",\"y\":").Append(F(engine.BallPosition.Y))
              .Append(",\"h\":").Append(F(engine.BallHeight))
              .Append(",\"vx\":").Append(F(engine.BallVelocity.X))
              .Append(",\"vy\":").Append(F(engine.BallVelocity.Y)).Append('}');
            sb.Append(",\"players\":[");
            for (int i = 0; i < players.Count; i++)
            {
                var p = players[i];
                if (i > 0) sb.Append(',');
                string aiState = (p.AIController as AIController)?.GetCurrentStateName() ?? "N/A";
                sb.Append("{\"i\":").Append(i)
                  .Append(",\"team\":\"").Append(p.Team == homeTeam ? "home" : "away").Append('\"')
                  .Append(",\"name\":\"").Append(p.Name.Replace("\"", "'")).Append('\"')
                  .Append(",\"x\":").Append(F(p.FieldPosition.X))
                  .Append(",\"y\":").Append(F(p.FieldPosition.Y))
                  .Append(",\"vx\":").Append(F(p.Velocity.X))
                  .Append(",\"vy\":").Append(F(p.Velocity.Y))
                  .Append(",\"state\":\"").Append(aiState).Append('\"')
                  .Append(",\"tx\":").Append(p.AITargetPositionSet ? F(p.AITargetPosition.X) : "null")
                  .Append(",\"ty\":").Append(p.AITargetPositionSet ? F(p.AITargetPosition.Y) : "null")
                  .Append('}');
            }
            sb.Append("]}");
            writer.WriteLine(sb.ToString());
        }

        private static string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);

        private static void PrintSummary(HarnessMetrics m, string scenario, int seed, int seconds, string logPath)
        {
            Console.WriteLine($"=== Harness: {scenario} (seed {seed}, {seconds}s) ===");
            Console.WriteLine($"Log: {logPath}");
            Console.WriteLine($"Score: {m.HomeScore}-{m.AwayScore}   Possession: home {m.PossessionHome:P0} / away {m.PossessionAway:P0}");
            Console.WriteLine($"Ball distance traveled: {m.BallDistanceTraveled:F0}px ({m.BallDistanceTraveled / 73f:F1}m)   " +
                              $"Max stationary: {m.MaxBallStationarySeconds:F1}s");
            Console.WriteLine();
            Console.WriteLine($"{"#",-3}{"Player",-28}{"Team",-6}{"Transitions",-12}{"Trans/s",-9}{"Reversals",-10}{"MeanDistToTarget",-16}");
            foreach (var p in m.Players)
            {
                Console.WriteLine($"{p.Index,-3}{Trunc(p.Name, 27),-28}{p.Team,-6}{p.StateTransitions,-12}" +
                    $"{p.TransitionsPerSecond,-9:F2}{p.DirectionReversals,-10}{p.MeanDistanceToTarget,-16:F0}");
            }
            Console.WriteLine();
            Console.WriteLine($"TOTAL: transitions {m.TotalStateTransitions} ({m.TotalTransitionsPerSecond:F2}/s), reversals {m.TotalDirectionReversals}");
        }

        private static string Trunc(string s, int max) => s.Length <= max ? s : s.Substring(0, max - 1) + "…";

        // ---------- Metrics ----------

        private class PlayerMetrics
        {
            public int Index;
            public string Name;
            public string Team;
            public int StateTransitions;
            public int DirectionReversals;
            public double TargetDistanceSum;
            public int TargetDistanceSamples;
            public string LastState;
            public Vector2 LastVelocity;
            public bool HasLastVelocity;
        }

        private class HarnessMetrics
        {
            public string Scenario { get; set; }
            public int Seed { get; set; }
            public int Seconds { get; set; }
            public int HomeScore { get; set; }
            public int AwayScore { get; set; }
            public double PossessionHome { get; set; }
            public double PossessionAway { get; set; }
            public double BallDistanceTraveled { get; set; }
            public double MaxBallStationarySeconds { get; set; }
            public int TotalStateTransitions { get; set; }
            public double TotalTransitionsPerSecond { get; set; }
            public int TotalDirectionReversals { get; set; }
            public List<PlayerMetricsRow> Players { get; set; }
        }

        private class PlayerMetricsRow
        {
            public int Index { get; set; }
            public string Name { get; set; }
            public string Team { get; set; }
            public int StateTransitions { get; set; }
            public double TransitionsPerSecond { get; set; }
            public int DirectionReversals { get; set; }
            public double MeanDistanceToTarget { get; set; }
        }

        private class MetricsCollector
        {
            private readonly List<PlayerMetrics> _players;
            private readonly Team _homeTeam;
            private readonly Team _awayTeam;
            private double _homePossessionTime;
            private double _awayPossessionTime;
            private double _ballDistance;
            private double _currentStationaryTime;
            private double _maxStationaryTime;
            private Vector2 _lastBallPosition;
            private bool _hasLastBallPosition;

            public MetricsCollector(List<Player> players, Team homeTeam, Team awayTeam)
            {
                _homeTeam = homeTeam;
                _awayTeam = awayTeam;
                _players = players.Select((p, i) => new PlayerMetrics
                {
                    Index = i,
                    Name = p.Name,
                    Team = p.Team == homeTeam ? "home" : "away",
                    LastState = (p.AIController as AIController)?.GetCurrentStateName() ?? "N/A"
                }).ToList();
            }

            public void RecordFrame(MatchEngine engine, List<Player> players, float dt)
            {
                // Ball metrics
                if (_hasLastBallPosition)
                    _ballDistance += Vector2.Distance(engine.BallPosition, _lastBallPosition);
                _lastBallPosition = engine.BallPosition;
                _hasLastBallPosition = true;

                if (engine.BallVelocity.Length() < BallStationarySpeed)
                {
                    _currentStationaryTime += dt;
                    _maxStationaryTime = Math.Max(_maxStationaryTime, _currentStationaryTime);
                }
                else
                {
                    _currentStationaryTime = 0;
                }

                // Possession: team of the nearest player to the ball
                Player nearest = null;
                float nearestDistSq = float.MaxValue;
                foreach (var p in players)
                {
                    float dSq = Vector2.DistanceSquared(p.FieldPosition, engine.BallPosition);
                    if (dSq < nearestDistSq) { nearestDistSq = dSq; nearest = p; }
                }
                if (nearest != null)
                {
                    if (nearest.Team == _homeTeam) _homePossessionTime += dt;
                    else _awayPossessionTime += dt;
                }

                // Per-player metrics
                for (int i = 0; i < players.Count; i++)
                {
                    var p = players[i];
                    var m = _players[i];

                    string state = (p.AIController as AIController)?.GetCurrentStateName() ?? "N/A";
                    if (state != m.LastState)
                    {
                        m.StateTransitions++;
                        m.LastState = state;
                    }

                    if (m.HasLastVelocity &&
                        m.LastVelocity.Length() > ReversalMinSpeed &&
                        p.Velocity.Length() > ReversalMinSpeed)
                    {
                        float dot = Vector2.Dot(m.LastVelocity, p.Velocity);
                        float cos = dot / (m.LastVelocity.Length() * p.Velocity.Length());
                        cos = Math.Clamp(cos, -1f, 1f);
                        float angleDeg = MathHelper.ToDegrees((float)Math.Acos(cos));
                        if (angleDeg > ReversalMinAngleDeg)
                            m.DirectionReversals++;
                    }
                    m.LastVelocity = p.Velocity;
                    m.HasLastVelocity = true;

                    if (p.AITargetPositionSet)
                    {
                        m.TargetDistanceSum += Vector2.Distance(p.FieldPosition, p.AITargetPosition);
                        m.TargetDistanceSamples++;
                    }
                }
            }

            public HarnessMetrics Finalize(MatchEngine engine, int seconds)
            {
                double totalPossession = _homePossessionTime + _awayPossessionTime;
                var rows = _players.Select(m => new PlayerMetricsRow
                {
                    Index = m.Index,
                    Name = m.Name,
                    Team = m.Team,
                    StateTransitions = m.StateTransitions,
                    TransitionsPerSecond = m.StateTransitions / (double)seconds,
                    DirectionReversals = m.DirectionReversals,
                    MeanDistanceToTarget = m.TargetDistanceSamples > 0
                        ? m.TargetDistanceSum / m.TargetDistanceSamples : 0
                }).ToList();

                return new HarnessMetrics
                {
                    HomeScore = engine.HomeScore,
                    AwayScore = engine.AwayScore,
                    PossessionHome = totalPossession > 0 ? _homePossessionTime / totalPossession : 0,
                    PossessionAway = totalPossession > 0 ? _awayPossessionTime / totalPossession : 0,
                    BallDistanceTraveled = _ballDistance,
                    MaxBallStationarySeconds = _maxStationaryTime,
                    TotalStateTransitions = rows.Sum(r => r.StateTransitions),
                    TotalTransitionsPerSecond = rows.Sum(r => r.StateTransitions) / (double)seconds,
                    TotalDirectionReversals = rows.Sum(r => r.DirectionReversals),
                    Players = rows
                };
            }
        }
    }
}
