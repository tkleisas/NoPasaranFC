using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Graphics3D.Skinning;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg: a fox wandering casually around the pitch apron.
    /// Uses the Khronos Fox.glb (Survey/Walk/Run clips). Waypoint wandering
    /// with idle pauses; stays outside the playing area.
    /// With a tint and shrunken tail bones it doubles as a dog.
    /// </summary>
    public class FoxWalker
    {
        private readonly SkinnedModel _model;
        private readonly SkinnedModelInstance _instance;
        private readonly Random _random = new Random();
        private readonly float _scale;

        // Fox.glb is huge (~155 units long); scale to a small dog size
        private const float Scale = 0.007f;
        private const float WalkSpeed = 1.1f; // m/s

        private Vector3 _position;
        private Vector3 _target;
        private float _yaw;
        private float _idleTimer = 2f;
        private bool _leaving;
        private readonly bool _chaseBall;
        private bool _chaseRunning;   // run/walk hysteresis state (anti-oscillation)
        private bool _atBall;         // contact hysteresis: push < 0.5m, chase > 0.7m
        private bool _waypointIdle;   // stand-still hysteresis at the waypoint
        private float _pushCooldown;  // dribble pulses, not per-frame shoves

        /// <summary>Goal the dog pushes toward: +1 right, -1 left, 0 = nearest.</summary>
        public int GoalSign { get; set; }

        /// <summary>True once Leave() has carried the walker off the pitch.</summary>
        public bool IsGone { get; private set; }

        public FoxWalker(SkinnedModel model, Texture2D textureOverride = null, float scale = Scale,
            float tailScale = 1f, bool chaseBall = false)
        {
            _model = model;
            _scale = scale;
            _chaseBall = chaseBall;
            _instance = new SkinnedModelInstance(model);
            if (textureOverride != null)
            {
                foreach (var part in model.Parts)
                    _instance.SetPartTexture(part.Name, textureOverride);
            }
            if (tailScale != 1f)
            {
                _instance.SetBoneScale("b_Tail01", tailScale);
                _instance.SetBoneScale("b_Tail02", tailScale);
                _instance.SetBoneScale("b_Tail03", tailScale);
            }
            // Start on the pitch near the center line (visible at kickoff), then wander
            _position = new Vector3(6f, 0f, -10f);
            _target = _position;
            _instance.Play("Survey");
        }

        /// <summary>Walks off the pitch (waypoint far beyond the apron); IsGone when done.</summary>
        public void Leave()
        {
            _leaving = true;
            float halfL = WorldUnits.PitchLengthMeters / 2f;
            float halfW = WorldUnits.PitchWidthMeters / 2f;
            // Exit through the nearest touchline, well past the apron
            _target = Math.Abs(_position.X) > Math.Abs(_position.Z) * (halfL / halfW)
                ? new Vector3(Math.Sign(_position.X) * (halfL + 25f), 0f, _position.Z)
                : new Vector3(_position.X, 0f, Math.Sign(_position.Z) * (halfW + 25f));
        }
        
        /// <summary>
        /// A random wander point: mostly on the pitch itself (the fox casually
        /// crosses the field - it's an easter egg), sometimes the apron.
        /// </summary>
        private Vector3 PickWaypoint()
        {
            float halfL = WorldUnits.PitchLengthMeters / 2f;
            float halfW = WorldUnits.PitchWidthMeters / 2f;
            
            if (_random.NextDouble() < 0.75)
            {
                // On the pitch, with a small margin from the lines
                return new Vector3(
                    (float)(_random.NextDouble() * 2 - 1) * (halfL - 4f),
                    0f,
                    (float)(_random.NextDouble() * 2 - 1) * (halfW - 4f));
            }
            
            // Apron band around the pitch (2.5-4.5m beyond the lines)
            int side = _random.Next(4);
            float x = (float)(_random.NextDouble() * 2 - 1) * (halfL + 4.5f);
            float z = (float)(_random.NextDouble() * 2 - 1) * (halfW + 4.5f);
            switch (side)
            {
                case 0: z = -halfW - 2.5f - (float)_random.NextDouble() * 2f; break;
                case 1: z = halfW + 2.5f + (float)_random.NextDouble() * 2f; break;
                case 2: x = -halfL - 2.5f - (float)_random.NextDouble() * 2f; break;
                default: x = halfL + 2.5f + (float)_random.NextDouble() * 2f; break;
            }
            return new Vector3(x, 0f, z);
        }
        
        public void Update(float dt, MatchEngine engine)
        {
            Vector3 toTarget = _target - _position;
            toTarget.Y = 0f;
            float distance = toTarget.Length();

            // Walking off: no pauses, no new waypoints
            if (_leaving)
            {
                if (distance < 1.5f)
                {
                    IsGone = true;
                    return;
                }
                Vector3 exitDir = toTarget / distance;
                _position += exitDir * WalkSpeed * 1.6f * dt;
                _yaw = (float)Math.Atan2(exitDir.X, exitDir.Z);
                _instance.Play("Run");
                _instance.Update(dt);
                return;
            }

            // Ball-chasing dog: circles behind the ball (relative to the nearest
            // goal), runs when far, and head-pushes the ball TOWARD the goal -
            // it keeps dribbling until the ball goes in
            if (_chaseBall && engine != null)
            {
                Vector3 ballWorld = WorldUnits.ToWorld(engine.BallPosition);
                float halfL = WorldUnits.PitchLengthMeters / 2f;
                int sign = GoalSign != 0 ? GoalSign : (ballWorld.X < 0f ? -1 : 1);
                Vector3 goalCenter = new Vector3(sign * halfL, 0f, 0f);
                Vector3 toGoal = goalCenter - ballWorld;
                toGoal.Y = 0f;
                if (toGoal.LengthSquared() > 0.001f) toGoal.Normalize();

                // Waypoint: behind the ball relative to the goal, so the push
                // always sends the ball forward
                _target = ballWorld - toGoal * 0.45f;

                Vector3 dogFlat = new Vector3(_position.X, 0f, _position.Z);
                Vector3 ballFlat = new Vector3(ballWorld.X, 0f, ballWorld.Z);
                float ballDist = Vector3.Distance(dogFlat, ballFlat);
                _pushCooldown -= dt;

                // Contact hysteresis: engage the push inside 0.5m, release past
                // 0.7m - without it the dog flip-flops clips at the boundary
                if (_atBall && ballDist > 0.7f) _atBall = false;
                else if (!_atBall && ballDist < 0.5f) _atBall = true;

                if (!_atBall)
                {
                    Vector3 dir = _target - _position;
                    dir.Y = 0f;
                    float dist = dir.Length();

                    // Stand-still hysteresis at the waypoint (enter < 0.15, exit > 0.3)
                    if (_waypointIdle && dist > 0.3f) _waypointIdle = false;
                    else if (!_waypointIdle && dist < 0.15f) _waypointIdle = true;

                    if (!_waypointIdle)
                    {
                        dir /= dist;
                        // Run/walk hysteresis: start running past 3.8m, stop under 3.2m
                        if (ballDist > 3.8f) _chaseRunning = true;
                        else if (ballDist < 3.2f) _chaseRunning = false;
                        // The dog is faster than any player (~7 m/s sprint)
                        _position += dir * WalkSpeed * (_chaseRunning ? 6.5f : 1.8f) * dt;
                        _yaw = (float)Math.Atan2(dir.X, dir.Z);
                        _instance.Play(_chaseRunning ? "Run" : "Walk");
                    }
                    else
                    {
                        _instance.Play("Survey");
                    }
                }
                else
                {
                    // Head push toward the goal in pulses (~2.8 m/s), then chase again
                    if (_pushCooldown <= 0f)
                    {
                        engine.BallVelocity = new Vector2(toGoal.X, toGoal.Z) *
                            WorldUnits.PixelsPerMeter * 2.8f;
                        _pushCooldown = 0.25f;
                    }
                    _instance.Play("Walk");
                }
                _instance.Update(dt);
                return;
            }

            // Don't stroll through players: pause while one is close
            bool playerNearby = false;
            if (engine != null)
            {
                foreach (var player in engine.GetAllPlayers())
                {
                    Vector3 playerWorld = WorldUnits.ToWorld(player.FieldPosition);
                    if (Vector3.DistanceSquared(playerWorld, _position) < 1.8f * 1.8f)
                    {
                        playerNearby = true;
                        break;
                    }
                }
            }

            if (playerNearby)
            {
                _instance.Play("Survey");
            }
            else if (distance > 0.3f)
            {
                // Walking toward the waypoint
                Vector3 direction = toTarget / distance;
                _position += direction * WalkSpeed * dt;
                _yaw = (float)Math.Atan2(direction.X, direction.Z);
                _instance.Play("Walk");
            }
            else
            {
                // Idle pause, then pick a new waypoint
                _idleTimer -= dt;
                if (_idleTimer <= 0f)
                {
                    _target = PickWaypoint();
                    _idleTimer = 3f + (float)_random.NextDouble() * 5f;
                }
                else
                {
                    _instance.Play("Survey");
                }
            }

            _instance.Update(dt);
        }

        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            _instance.Environment = environment;
            Matrix world = Matrix.CreateScale(_scale)
                * Matrix.CreateRotationY(_yaw)
                * Matrix.CreateTranslation(_position);
            _instance.Draw(device, world, view, projection);
        }
    }
}
