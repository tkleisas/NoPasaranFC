using System;
using System.Buffers.Binary;
using System.Collections.Generic;
using System.IO;
using Microsoft.Xna.Framework;
using NoPasaranFC.Database;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;

namespace NoPasaranFC.Networking
{
    /// <summary>Wire message tags. Keep in sync with the server/client switches.</summary>
    public enum NetMessageType : byte
    {
        Hello = 1,
        Welcome = 2,
        MatchStart = 3,
        Input = 4,
        Tick = 5,
        MatchEnd = 6,
        Ping = 7,
        Pong = 8,
        Error = 9,
    }

    public enum NetSide : byte
    {
        Home = 0,
        Away = 1,
    }

    /// <summary>One tick's player input (P1 = home human, P2 = away human).</summary>
    public readonly struct NetInput
    {
        public readonly float MoveX;
        public readonly float MoveY;
        public readonly bool Shoot;
        public readonly bool Pass;

        public NetInput(float moveX, float moveY, bool shoot, bool pass)
        {
            MoveX = moveX;
            MoveY = moveY;
            Shoot = shoot;
            Pass = pass;
        }

        public Vector2 Move => new Vector2(MoveX, MoveY);

        public static readonly NetInput Zero = new NetInput(0f, 0f, false, false);
    }

    /// <summary>Gameplay settings both peers must share for deterministic simulation.</summary>
    public sealed class NetworkSettings
    {
        public int Difficulty { get; set; } = 1;
        public float PlayerSpeedMultiplier { get; set; } = 2.0f;
        public float AIDecisionInterval { get; set; } = 0.1f;
        public string BallControl { get; set; } = "Easy";
        public bool OffsidesEnabled { get; set; }
        public float MatchDurationMinutes { get; set; } = 3.0f;

        public static NetworkSettings Capture()
        {
            var s = GameSettings.Instance;
            return new NetworkSettings
            {
                Difficulty = s.Difficulty,
                PlayerSpeedMultiplier = s.PlayerSpeedMultiplier,
                AIDecisionInterval = s.AIDecisionInterval,
                BallControl = s.BallControl,
                OffsidesEnabled = s.OffsidesEnabled,
                MatchDurationMinutes = s.MatchDurationMinutes,
            };
        }

        public void Apply()
        {
            var s = GameSettings.Instance;
            s.Difficulty = Difficulty;
            s.PlayerSpeedMultiplier = PlayerSpeedMultiplier;
            s.AIDecisionInterval = AIDecisionInterval;
            s.BallControl = BallControl;
            s.OffsidesEnabled = OffsidesEnabled;
            s.MatchDurationMinutes = MatchDurationMinutes;
        }
    }

    /// <summary>Everything a client needs to recreate the exact match the server runs.</summary>
    public sealed class MatchStartInfo
    {
        public int ProtocolVersion { get; set; } = NetProtocol.Version;
        public int TickRate { get; set; } = NetProtocol.TickRate;
        public int Seed { get; set; }
        public NetSide LocalSide { get; set; }
        public NetworkSettings Settings { get; set; } = new NetworkSettings();
        public Team HomeTeam { get; set; }
        public Team AwayTeam { get; set; }
    }

    /// <summary>Server-ordered input bundle. Both clients simulate this exact tick.</summary>
    public sealed class TickInfo
    {
        public int Tick { get; set; }
        public NetInput HomeInput { get; set; }
        public NetInput AwayInput { get; set; }
        public bool ForceStartSecondHalf { get; set; }
        public float MatchTime { get; set; }
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
        public MatchEngine.MatchState State { get; set; }
        public int Half { get; set; }
    }

    public sealed class MatchEndInfo
    {
        public int HomeScore { get; set; }
        public int AwayScore { get; set; }
    }

    public enum NetEventKind
    {
        Welcome,
        Start,
        Tick,
        MatchEnd,
        Disconnected,
        Error,
    }

    /// <summary>Inbound event handed from the receive thread to the game thread.</summary>
    public sealed class NetEvent
    {
        public NetEventKind Kind { get; init; }
        public NetSide Side { get; init; }
        public MatchStartInfo Start { get; init; }
        public TickInfo Tick { get; init; }
        public MatchEndInfo End { get; init; }
        public string Message { get; init; }

        public static NetEvent Welcome(NetSide side) => new NetEvent { Kind = NetEventKind.Welcome, Side = side };
        public static NetEvent StartMatch(MatchStartInfo start) => new NetEvent { Kind = NetEventKind.Start, Start = start };
        public static NetEvent TickEvent(TickInfo tick) => new NetEvent { Kind = NetEventKind.Tick, Tick = tick };
        public static NetEvent MatchEnded(MatchEndInfo end) => new NetEvent { Kind = NetEventKind.MatchEnd, End = end };
        public static NetEvent DisconnectedEvent(string reason = null) => new NetEvent { Kind = NetEventKind.Disconnected, Message = reason };
        public static NetEvent ErrorEvent(string message) => new NetEvent { Kind = NetEventKind.Error, Message = message };
    }

    public static class NetProtocol
    {
        public const int Version = 1;
        public const int DefaultPort = 7778;
        public const int TickRate = 60;
        public const int MaxPayloadBytes = 8 * 1024 * 1024;

        /// <summary>
        /// Fixed celebration length for network matches. It is part of the
        /// simulation (gates the restart), so every peer must use the same value.
        /// </summary>
        public const float CelebrationDurationSeconds = 6f;
        public static readonly TimeSpan FixedDelta = TimeSpan.FromSeconds(1.0 / TickRate);

        // ---------- framing ----------

        public static void WriteFrame(Stream stream, NetMessageType type, byte[] payload)
        {
            Span<byte> header = stackalloc byte[5];
            header[0] = (byte)type;
            BinaryPrimitives.WriteInt32LittleEndian(header.Slice(1), payload.Length);
            stream.Write(header);
            stream.Write(payload);
            stream.Flush();
        }

        public static bool TryReadFrame(Stream stream, out NetMessageType type, out byte[] payload)
        {
            Span<byte> header = stackalloc byte[5];
            int read = ReadUpTo(stream, header);
            if (read == 0)
            {
                type = default;
                payload = null;
                return false;
            }
            if (read != header.Length)
                throw new EndOfStreamException("Truncated frame header");

            int length = BinaryPrimitives.ReadInt32LittleEndian(header.Slice(1));
            if (length < 0 || length > MaxPayloadBytes)
                throw new InvalidDataException($"Invalid frame length {length}");

            payload = new byte[length];
            if (ReadUpTo(stream, payload) != length)
                throw new EndOfStreamException("Truncated frame payload");

            type = (NetMessageType)header[0];
            return true;
        }

        private static int ReadUpTo(Stream stream, Span<byte> buffer)
        {
            int total = 0;
            while (total < buffer.Length)
            {
                int n = stream.Read(buffer.Slice(total));
                if (n <= 0) break;
                total += n;
            }
            return total;
        }

        public static byte[] Build(Action<BinaryWriter> write)
        {
            using var ms = new MemoryStream();
            using (var writer = new BinaryWriter(ms, System.Text.Encoding.UTF8, leaveOpen: true))
                write(writer);
            return ms.ToArray();
        }

        // ---------- payload codecs ----------

        public static void WriteInput(BinaryWriter writer, NetInput input)
        {
            writer.Write(input.MoveX);
            writer.Write(input.MoveY);
            writer.Write(input.Shoot);
            writer.Write(input.Pass);
        }

        public static NetInput ReadInput(BinaryReader reader)
        {
            float x = reader.ReadSingle();
            float y = reader.ReadSingle();
            bool shoot = reader.ReadBoolean();
            bool pass = reader.ReadBoolean();
            return new NetInput(x, y, shoot, pass);
        }

        public static byte[] EncodeHello(string playerName) =>
            Build(w =>
            {
                w.Write(Version);
                w.Write(playerName ?? string.Empty);
            });

        public static (int Version, string PlayerName) DecodeHello(byte[] payload)
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            return (reader.ReadInt32(), reader.ReadString());
        }

        public static byte[] EncodeWelcome(NetSide side) =>
            Build(w =>
            {
                w.Write(Version);
                w.Write((byte)side);
            });

        public static NetSide DecodeWelcome(byte[] payload)
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            int version = reader.ReadInt32();
            if (version != Version) throw new InvalidDataException($"Protocol mismatch: server {version}, client {Version}");
            return (NetSide)reader.ReadByte();
        }

        public static byte[] EncodeMatchStart(MatchStartInfo info) =>
            Build(w =>
            {
                w.Write(Version);
                w.Write(info.TickRate);
                w.Write(info.Seed);
                w.Write((byte)info.LocalSide);

                var s = info.Settings;
                w.Write(s.Difficulty);
                w.Write(s.PlayerSpeedMultiplier);
                w.Write(s.AIDecisionInterval);
                w.Write(s.BallControl ?? "Easy");
                w.Write(s.OffsidesEnabled);
                w.Write(s.MatchDurationMinutes);

                // Teams are shipped in the teams_seed.json schema so clients do not
                // depend on their local DB/catalog being identical.
                w.Write(TeamSeeder.SerializeTeams(
                    new List<Team> { info.HomeTeam, info.AwayTeam }, indented: false));
            });

        public static MatchStartInfo DecodeMatchStart(byte[] payload)
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            int version = reader.ReadInt32();
            if (version != Version) throw new InvalidDataException($"Protocol mismatch: server {version}, client {Version}");

            var start = new MatchStartInfo
            {
                ProtocolVersion = version,
                TickRate = reader.ReadInt32(),
                Seed = reader.ReadInt32(),
                LocalSide = (NetSide)reader.ReadByte(),
                Settings = new NetworkSettings
                {
                    Difficulty = reader.ReadInt32(),
                    PlayerSpeedMultiplier = reader.ReadSingle(),
                    AIDecisionInterval = reader.ReadSingle(),
                    BallControl = reader.ReadString(),
                    OffsidesEnabled = reader.ReadBoolean(),
                    MatchDurationMinutes = reader.ReadSingle(),
                },
            };

            string teamJson = reader.ReadString();
            var teams = TeamSeeder.LoadTeamsFromJsonString(teamJson);
            if (teams.Count != 2)
                throw new InvalidDataException($"Match setup expected 2 teams, got {teams.Count}");

            start.HomeTeam = teams[0];
            start.AwayTeam = teams[1];
            return start;
        }

        public static byte[] EncodeInput(NetInput input) =>
            Build(w => WriteInput(w, input));

        public static NetInput DecodeInput(byte[] payload)
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            return ReadInput(reader);
        }

        public static byte[] EncodeTick(TickInfo tick) =>
            Build(w =>
            {
                w.Write(tick.Tick);
                WriteInput(w, tick.HomeInput);
                WriteInput(w, tick.AwayInput);
                w.Write(tick.ForceStartSecondHalf);
                w.Write(tick.MatchTime);
                w.Write((short)tick.HomeScore);
                w.Write((short)tick.AwayScore);
                w.Write((byte)tick.State);
                w.Write((byte)tick.Half);
            });

        public static TickInfo DecodeTick(byte[] payload)
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            return new TickInfo
            {
                Tick = reader.ReadInt32(),
                HomeInput = ReadInput(reader),
                AwayInput = ReadInput(reader),
                ForceStartSecondHalf = reader.ReadBoolean(),
                MatchTime = reader.ReadSingle(),
                HomeScore = reader.ReadInt16(),
                AwayScore = reader.ReadInt16(),
                State = (MatchEngine.MatchState)reader.ReadByte(),
                Half = reader.ReadByte(),
            };
        }

        public static byte[] EncodeMatchEnd(MatchEndInfo end) =>
            Build(w =>
            {
                w.Write((short)end.HomeScore);
                w.Write((short)end.AwayScore);
            });

        public static MatchEndInfo DecodeMatchEnd(byte[] payload)
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            return new MatchEndInfo { HomeScore = reader.ReadInt16(), AwayScore = reader.ReadInt16() };
        }

        public static byte[] EncodeError(string message) =>
            Build(w => w.Write(message ?? string.Empty));

        public static string DecodeError(byte[] payload)
        {
            using var reader = new BinaryReader(new MemoryStream(payload));
            return reader.ReadString();
        }
    }

    /// <summary>
    /// Normalizes both teams so every peer (server + clients) builds an engine with
    /// identical ids, ownership flags and controlled-player selection.
    /// </summary>
    public static class NetworkMatchSetup
    {
        /// <summary>
        /// Home is always the engine's P1 side and away the P2 side, on every peer.
        /// This is what lets each client feed home/away inputs into the same engine
        /// slots regardless of which team the local human chose.
        /// </summary>
        public static void PrepareTeams(Team home, Team away)
        {
            home.Id = 1;
            away.Id = 2;
            home.IsPlayerControlled = true;
            away.IsPlayerControlled = false;

            PrepareTeam(home);
            PrepareTeam(away);
        }

        private static void PrepareTeam(Team team)
        {
            for (int i = 0; i < team.Players.Count; i++)
            {
                var player = team.Players[i];
                player.Team = team;
                player.TeamId = team.Id;
                player.Id = team.Id * 1000 + i + 1;
            }
        }
    }
}
