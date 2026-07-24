using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Records the last ~10 seconds of live play into a 60 Hz ring buffer
    /// (ball + per-player position/velocity/knockdown, engine pixel space) so
    /// the 3D renderer can replay the build-up to a goal over the post-goal
    /// countdown. The simulation is never touched - this is a read-only tap.
    /// </summary>
    public class ReplayBuffer
    {
        public const int FramesPerSecond = 60;
        private const int CapacitySeconds = 10;
        private const int CapacityFrames = FramesPerSecond * CapacitySeconds;

        public struct BallFrame
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public float Height;
        }

        public struct PlayerFrame
        {
            public Vector2 Position;
            public Vector2 Velocity;
            public bool KnockedDown;
        }

        private readonly BallFrame[] _ballFrames = new BallFrame[CapacityFrames];
        private PlayerFrame[] _playerFrames; // CapacityFrames * playerCount, frame-major
        private int _playerCount;
        private int _head;   // next write index (oldest frame once full)
        private int _count;

        public float RecordedSeconds => _count / (float)FramesPerSecond;

        /// <summary>Appends one frame of engine state. Call once per update while Playing.</summary>
        public void Record(MatchEngine engine)
        {
            var players = engine.GetAllPlayers();
            if (_playerFrames == null || _playerCount != players.Count)
            {
                _playerCount = players.Count;
                _playerFrames = new PlayerFrame[CapacityFrames * _playerCount];
                _head = 0;
                _count = 0;
            }

            _ballFrames[_head] = new BallFrame
            {
                Position = engine.BallPosition,
                Velocity = engine.BallVelocity,
                Height = engine.BallHeight,
            };

            int baseIndex = _head * _playerCount;
            for (int i = 0; i < _playerCount; i++)
            {
                var p = players[i];
                _playerFrames[baseIndex + i] = new PlayerFrame
                {
                    Position = p.FieldPosition,
                    Velocity = p.Velocity,
                    KnockedDown = p.IsKnockedDown,
                };
            }

            _head = (_head + 1) % CapacityFrames;
            if (_count < CapacityFrames) _count++;
        }

        /// <summary>Drops all recorded frames (after a snapshot: the next replay starts fresh).</summary>
        public void Reset()
        {
            _head = 0;
            _count = 0;
        }

        /// <summary>
        /// Immutable, time-ordered copy of the most recent maxSeconds of recorded
        /// play (or everything recorded when the buffer holds less). Allocates -
        /// call once per goal, never per frame.
        /// </summary>
        public ReplaySequence Snapshot(float maxSeconds)
        {
            int keep = Math.Min(_count, (int)(maxSeconds * FramesPerSecond));
            var ball = new BallFrame[keep];
            var players = new PlayerFrame[keep * _playerCount];

            // _head points one past the newest frame; walk back `keep` frames
            int first = (_head - keep + CapacityFrames) % CapacityFrames;
            for (int f = 0; f < keep; f++)
            {
                int src = (first + f) % CapacityFrames;
                ball[f] = _ballFrames[src];
                Array.Copy(_playerFrames, src * _playerCount, players, f * _playerCount, _playerCount);
            }

            return new ReplaySequence(ball, players, _playerCount);
        }
    }

    /// <summary>
    /// A captured, time-ordered slice of recorded play (t = 0..Duration).
    /// GetInterpolated writes into a caller-owned array - no per-frame allocation.
    /// </summary>
    public class ReplaySequence
    {
        private readonly ReplayBuffer.BallFrame[] _ball;
        private readonly ReplayBuffer.PlayerFrame[] _players; // frame-major

        public int PlayerCount { get; }
        public int FrameCount => _ball.Length;
        public float Duration => FrameCount / (float)ReplayBuffer.FramesPerSecond;

        public ReplaySequence(ReplayBuffer.BallFrame[] ball, ReplayBuffer.PlayerFrame[] players, int playerCount)
        {
            _ball = ball;
            _players = players;
            PlayerCount = playerCount;
        }

        /// <summary>Linearly interpolated frame at the given time, written into `players`.</summary>
        public void GetInterpolated(float time, out ReplayBuffer.BallFrame ball, ReplayBuffer.PlayerFrame[] players)
        {
            float framePos = Math.Clamp(time, 0f, Duration) * ReplayBuffer.FramesPerSecond;
            int i0 = Math.Min((int)framePos, FrameCount - 1);
            int i1 = Math.Min(i0 + 1, FrameCount - 1);
            float t = framePos - i0;

            var b0 = _ball[i0];
            var b1 = _ball[i1];
            ball = new ReplayBuffer.BallFrame
            {
                Position = Vector2.Lerp(b0.Position, b1.Position, t),
                Velocity = Vector2.Lerp(b0.Velocity, b1.Velocity, t),
                Height = MathHelper.Lerp(b0.Height, b1.Height, t),
            };

            int count = Math.Min(PlayerCount, players.Length);
            for (int i = 0; i < count; i++)
            {
                var p0 = _players[i0 * PlayerCount + i];
                var p1 = _players[i1 * PlayerCount + i];
                players[i] = new ReplayBuffer.PlayerFrame
                {
                    Position = Vector2.Lerp(p0.Position, p1.Position, t),
                    Velocity = Vector2.Lerp(p0.Velocity, p1.Velocity, t),
                    KnockedDown = t < 0.5f ? p0.KnockedDown : p1.KnockedDown,
                };
            }
        }
    }
}
