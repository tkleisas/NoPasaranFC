using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Text;
using System.Text.Json;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Opt-in live match recorder (Settings: RecordMatches / RecordVerbose).
    /// Observes a MatchEngine read-only - the engine never knows it exists; the
    /// recorder is driven from MatchScreen.Update and listens to the engine's
    /// MatchEvent hook (the same sites as the stats counters).
    ///
    /// Output: recordings/match_&lt;yyyyMMdd_HHmmss&gt;.log.jsonl under the current
    /// working directory (the project root when launched via `dotnet run` - the
    /// same convention as the harness logs). First line is a meta object, then one
    /// JSON object per sampled frame (10 Hz) in the HarnessRunner.WriteFrame schema,
    /// so Scripts/trajectory_plot.py renders it unchanged. Event lines
    /// ({"t":..,"ev":"kick",...}) are an additive extension; tools that only read
    /// meta/frames skip them. With RecordVerbose on, each player entry gets a
    /// "dec" block (chosen utility action + score + top-2 rejected alternatives).
    /// </summary>
    public sealed class MatchRecorder : IDisposable
    {
        /// <summary>Frame sampling rate (every 6th update at 60 fps).</summary>
        public const int SampleHz = 10;
        private const float SampleInterval = 1f / SampleHz;
        private const float FlushInterval = 1f; // seconds of game time between flushes

        private readonly MatchEngine _engine;
        private readonly Team _homeTeam;
        private readonly bool _verbose;
        private readonly List<Player> _players; // captured once (plot needs stable indices)
        private readonly StreamWriter _writer;
        private float _elapsed;
        private float _timeSinceSample;
        private float _timeSinceFlush;
        private bool _failed; // I/O error: disable silently, never break the match
        private bool _disposed;

        /// <summary>Full path of the recording file (for tests / diagnostics).</summary>
        public string FilePath { get; }

        public MatchRecorder(MatchEngine engine, bool verbose, string directory = null)
        {
            _engine = engine ?? throw new ArgumentNullException(nameof(engine));
            _homeTeam = engine.HomeTeam;
            _verbose = verbose;
            _players = engine.GetAllPlayers();

            directory ??= "recordings";
            Directory.CreateDirectory(directory);
            FilePath = Path.Combine(directory,
                $"match_{DateTime.Now:yyyyMMdd_HHmmss}.log.jsonl");

            _writer = new StreamWriter(FilePath, false, new UTF8Encoding(false));
            _writer.WriteLine(JsonSerializer.Serialize(new Dictionary<string, object>
            {
                ["meta"] = true,
                ["scenario"] = "live",
                ["seed"] = 0,
                ["seconds"] = (int)GameSettings.Instance.GetMatchDurationSeconds(),
                ["fps"] = SampleHz, // frames actually logged per second (plot tick cadence)
                ["sampleHz"] = SampleHz,
                ["fieldWidth"] = MatchEngine.FieldWidth,
                ["fieldHeight"] = MatchEngine.FieldHeight,
                ["stadiumMargin"] = MatchEngine.StadiumMargin,
                ["homeTeam"] = _homeTeam.Name,
                ["awayTeam"] = engine.AwayTeam.Name
            }));

            _engine.MatchEvent += OnMatchEvent;
        }

        /// <summary>Called every MatchScreen.Update with the real elapsed seconds.</summary>
        public void Update(float deltaTime)
        {
            if (_disposed || _failed) return;
            _elapsed += deltaTime;
            _timeSinceSample += deltaTime;
            _timeSinceFlush += deltaTime;

            if (_timeSinceSample >= SampleInterval)
            {
                _timeSinceSample -= SampleInterval;
                try
                {
                    WriteFrame();
                }
                catch (Exception)
                {
                    _failed = true; // recording is optional - never crash the match
                    return;
                }
            }

            // Periodic flush: a crash loses at most ~1s of recording
            if (_timeSinceFlush >= FlushInterval)
            {
                _timeSinceFlush = 0f;
                try { _writer.Flush(); }
                catch (Exception) { _failed = true; }
            }
        }

        // Engine event hook (kicks, tackles, fouls, cards, offsides, goals,
        // restarts) - fired from inside MatchEngine.Update on the same thread
        private void OnMatchEvent(string kind, Team team, Player player, Vector2 velocity, float power)
        {
            if (_disposed || _failed) return;
            try
            {
                var sb = new StringBuilder(160);
                sb.Append("{\"t\":").Append(F(_elapsed));
                sb.Append(",\"ev\":\"").Append(kind).Append('\"');
                if (player != null)
                    sb.Append(",\"player\":\"").Append(player.Name.Replace("\"", "'")).Append('\"');
                if (team != null)
                    sb.Append(",\"team\":\"").Append(team == _homeTeam ? "home" : "away").Append('\"');
                sb.Append(",\"x\":").Append(F(_engine.BallPosition.X))
                  .Append(",\"y\":").Append(F(_engine.BallPosition.Y))
                  .Append(",\"vx\":").Append(F(velocity.X))
                  .Append(",\"vy\":").Append(F(velocity.Y))
                  .Append(",\"power\":").Append(F(power)).Append('}');
                _writer.WriteLine(sb.ToString());
            }
            catch (Exception)
            {
                _failed = true;
            }
        }

        // Same field names/formats as HarnessRunner.WriteFrame (plus "controlled"
        // and the verbose "dec" block) so trajectory_plot.py works unchanged
        private void WriteFrame()
        {
            var sb = new StringBuilder(2048);
            sb.Append("{\"t\":").Append(F(_elapsed));
            sb.Append(",\"state\":\"").Append(_engine.CurrentState).Append('\"');
            sb.Append(",\"ball\":{\"x\":").Append(F(_engine.BallPosition.X))
              .Append(",\"y\":").Append(F(_engine.BallPosition.Y))
              .Append(",\"h\":").Append(F(_engine.BallHeight))
              .Append(",\"vx\":").Append(F(_engine.BallVelocity.X))
              .Append(",\"vy\":").Append(F(_engine.BallVelocity.Y)).Append('}');
            sb.Append(",\"players\":[");
            for (int i = 0; i < _players.Count; i++)
            {
                var p = _players[i];
                if (i > 0) sb.Append(',');
                string aiState = (p.AIController as AIController)?.GetCurrentStateName() ?? "N/A";
                sb.Append("{\"i\":").Append(i)
                  .Append(",\"team\":\"").Append(p.Team == _homeTeam ? "home" : "away").Append('\"')
                  .Append(",\"name\":\"").Append(p.Name.Replace("\"", "'")).Append('\"')
                  .Append(",\"x\":").Append(F(p.FieldPosition.X))
                  .Append(",\"y\":").Append(F(p.FieldPosition.Y))
                  .Append(",\"vx\":").Append(F(p.Velocity.X))
                  .Append(",\"vy\":").Append(F(p.Velocity.Y))
                  .Append(",\"state\":\"").Append(aiState).Append('\"')
                  .Append(",\"tx\":").Append(p.AITargetPositionSet ? F(p.AITargetPosition.X) : "null")
                  .Append(",\"ty\":").Append(p.AITargetPositionSet ? F(p.AITargetPosition.Y) : "null");
                if (p.IsControlled) // human-controlled (P1, and P2 in local co-op/versus)
                    sb.Append(",\"controlled\":true");
                if (_verbose)
                    AppendDecision(sb, p);
                sb.Append('}');
            }
            sb.Append("]}");
            _writer.WriteLine(sb.ToString());
        }

        // Verbose: the utility brain's most recent decision (chosen + runners-up)
        private static void AppendDecision(StringBuilder sb, Player p)
        {
            var brain = (p.AIController as AIController)?.Brain;
            if (brain == null) return;
            var dec = brain.LastDecision;
            if (dec.Action == null) return;
            sb.Append(",\"dec\":{\"action\":\"").Append(dec.Action)
              .Append("\",\"score\":").Append(F(dec.Score));
            bool hasAlt = false;
            AppendAlt(sb, dec.Alt1Action, dec.Alt1Score, ref hasAlt);
            AppendAlt(sb, dec.Alt2Action, dec.Alt2Score, ref hasAlt);
            if (hasAlt) sb.Append(']');
            sb.Append('}');
        }

        private static void AppendAlt(StringBuilder sb, string action, float score, ref bool hasAlt)
        {
            if (action == null || !float.IsFinite(score)) return;
            sb.Append(hasAlt ? "," : ",\"alt\":[");
            sb.Append("{\"action\":\"").Append(action)
              .Append("\",\"score\":").Append(F(score)).Append('}');
            hasAlt = true;
        }

        public void Dispose()
        {
            if (_disposed) return;
            _disposed = true;
            _engine.MatchEvent -= OnMatchEvent;
            try
            {
                _writer.Flush();
                _writer.Dispose();
            }
            catch (Exception)
            {
                // Nothing to recover - the match is already over
            }
        }

        private static string F(float v) => v.ToString("F1", CultureInfo.InvariantCulture);
    }
}
