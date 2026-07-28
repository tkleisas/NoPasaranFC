using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg: a bee swarm (5% of matches). A dozen bees harass a random
    /// player for a few seconds, then move to another. A bee that reaches its
    /// target stings him: 30 seconds of confusion (drunk wobble), once per visit.
    /// </summary>
    public class BeeSwarm
    {
        private const int BeeCount = 12;
        private const float Duration = 20f;

        private readonly Random _random;
        private Player _target;
        private float _retargetTimer;
        private float _time;
        private Vector3 _center; // swarm centroid
        private bool _stung;
        private BasicEffect _effect;

        public bool IsDone => _time > Duration;

        public BeeSwarm(Random random, MatchEngine engine)
        {
            _random = random ?? new Random();
            _target = PickTarget(engine);
            if (_target != null)
                _center = WorldUnits.ToWorld(_target.FieldPosition) + Vector3.Up * 1.2f;
            _retargetTimer = 5f;
        }

        private Player PickTarget(MatchEngine engine)
        {
            Player chosen = null;
            int best = int.MaxValue;
            foreach (var p in engine.GetAllPlayers())
            {
                if (p.Position == PlayerPosition.Goalkeeper) continue;
                int roll = _random.Next();
                if (roll < best) { best = roll; chosen = p; }
            }
            return chosen;
        }

        public void Update(float dt, MatchEngine engine)
        {
            _time += dt;
            _retargetTimer -= dt;
            if (_retargetTimer <= 0f || _target == null)
            {
                _target = PickTarget(engine);
                _retargetTimer = 4f + (float)_random.NextDouble() * 4f;
                _stung = false; // new visit, new sting
            }

            if (_target != null)
            {
                Vector3 targetPos = WorldUnits.ToWorld(_target.FieldPosition) + Vector3.Up * 1.2f;
                _center = Vector3.Lerp(_center, targetPos, Math.Min(1f, dt * 1.5f));

                // The sting: swarm centroid reaches the player
                if (!_stung && Vector3.Distance(_center, targetPos) < 0.6f)
                {
                    _target.ConfusedRemaining = 30f;
                    _stung = true;
                    _retargetTimer = 1.5f; // off to the next victim
                }
            }
        }

        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            _effect ??= new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            if (environment != null)
                environment.ApplyTo(_effect, false);

            var color = new Color(30, 26, 20);
            var verts = new System.Collections.Generic.List<VertexPositionColor>(BeeCount * 6);
            for (int i = 0; i < BeeCount; i++)
            {
                // Erratic flight around the centroid: lissajous-ish wobble per bee
                float t = _time * 6f + i * 1.7f;
                var pos = _center + new Vector3(
                    MathF.Sin(t * 1.3f + i) * (0.5f + (i % 3) * 0.25f),
                    MathF.Sin(t * 2.1f + i * 2f) * 0.35f,
                    MathF.Cos(t * 1.7f + i) * (0.5f + (i % 4) * 0.2f));

                // Tiny fluttering quad (fast size pulse = wing beat)
                float size = 0.07f + 0.03f * MathF.Sin(_time * 40f + i);
                var up = Vector3.Up * size;
                var side = Vector3.UnitX * size;
                verts.Add(new VertexPositionColor(pos - side - up, color));
                verts.Add(new VertexPositionColor(pos + side + up, color));
                verts.Add(new VertexPositionColor(pos - side + up, color));
                verts.Add(new VertexPositionColor(pos - side - up, color));
                verts.Add(new VertexPositionColor(pos + side - up, color));
                verts.Add(new VertexPositionColor(pos + side + up, color));
            }

            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts.ToArray(), 0, verts.Count / 3);
            }
        }
    }
}
