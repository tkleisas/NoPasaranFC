using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Graphics3D.Skinning;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg: a fox wandering casually around the pitch apron.
    /// Uses the Khronos Fox.glb (Survey/Walk/Run clips). Waypoint wandering
    /// with idle pauses; stays outside the playing area.
    /// </summary>
    public class FoxWalker
    {
        private readonly SkinnedModel _model;
        private readonly SkinnedModelInstance _instance;
        private readonly Random _random = new Random();
        
        // Fox.glb is huge (~155 units long); scale to a small dog size
        private const float Scale = 0.007f;
        private const float WalkSpeed = 1.1f; // m/s
        
        private Vector3 _position;
        private Vector3 _target;
        private float _yaw;
        private float _idleTimer = 2f;
        
        public FoxWalker(SkinnedModel model)
        {
            _model = model;
            _instance = new SkinnedModelInstance(model);
            // Start on the pitch near the center line (visible at kickoff), then wander
            _position = new Vector3(6f, 0f, -10f);
            _target = _position;
            _instance.Play("Survey");
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
        
        public void Update(float dt)
        {
            Vector3 toTarget = _target - _position;
            toTarget.Y = 0f;
            float distance = toTarget.Length();
            
            if (distance > 0.3f)
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
            Matrix world = Matrix.CreateScale(Scale)
                * Matrix.CreateRotationY(_yaw)
                * Matrix.CreateTranslation(_position);
            _instance.Draw(device, world, view, projection);
        }
    }
}
