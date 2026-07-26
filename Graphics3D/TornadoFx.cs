using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg (rain only): a whirlwind funnel wandering the pitch.
    /// Spiral of translucent quads - tight and dark at the ground, wide and
    //  faint aloft, with a swaying top and a dust ring at the base.
    /// </summary>
    public class TornadoFx
    {
        private const int ParticleCount = 220;
        private const int DustCount = 24;
        private const float FunnelHeight = 32f;          // 4x the original funnel
        private const float Duration = float.MaxValue;   // roams for the whole match

        private readonly Random _random;
        private Vector3 _center;
        private Vector3 _drift;
        private float _time;
        private BasicEffect _effect;
        private readonly float _halfL;
        private readonly float _halfW;
        private float _driftChangeTimer = 2f;

        public bool IsDone => _time > Duration;

        public TornadoFx(Random random)
        {
            _random = random ?? new Random();
            _halfL = WorldUnits.PitchLengthMeters / 2f;
            _halfW = WorldUnits.PitchWidthMeters / 2f;

            // Appear from afar (well beyond a random side) and head for the pitch
            int side = _random.Next(4);
            float along = (float)(_random.NextDouble() * 2 - 1);
            float far = 30f + (float)_random.NextDouble() * 15f;
            _center = side switch
            {
                0 => new Vector3(-_halfL - far, 0f, along * _halfW),
                1 => new Vector3(_halfL + far, 0f, along * _halfW),
                2 => new Vector3(along * _halfL, 0f, -_halfW - far),
                _ => new Vector3(along * _halfL, 0f, _halfW + far),
            };
            // Aim at a random spot on the pitch
            var target = new Vector3(
                (float)(_random.NextDouble() * 2 - 1) * _halfL * 0.6f, 0f,
                (float)(_random.NextDouble() * 2 - 1) * _halfW * 0.6f);
            var toTarget = target - _center;
            toTarget.Y = 0f;
            _drift = Vector3.Normalize(toTarget) * 4.5f; // m/s approach
        }

        private bool OnPitch => Math.Abs(_center.X) < _halfL - 1f && Math.Abs(_center.Z) < _halfW - 1f;

        public void Update(float dt, Gameplay.MatchEngine engine = null)
        {
            _time += dt;
            _center += _drift * dt;

            if (!OnPitch) return; // still approaching

            // On the pitch: gentle meander + bounce off the lines
            _driftChangeTimer -= dt;
            if (_driftChangeTimer <= 0f)
            {
                float turn = (float)(_random.NextDouble() - 0.5) * 1.4f;
                float c = MathF.Cos(turn), s = MathF.Sin(turn);
                _drift = new Vector3(_drift.X * c - _drift.Z * s, 0f, _drift.X * s + _drift.Z * c);
                _driftChangeTimer = 1.5f + (float)_random.NextDouble() * 2f;
            }
            if (Math.Abs(_center.X) > _halfL - 1f) _drift.X = -_drift.X;
            if (Math.Abs(_center.Z) > _halfW - 1f) _drift.Z = -_drift.Z;

            // The funnel shoves players caught in it: outward + tangential swirl
            if (engine != null)
            {
                foreach (var p in engine.GetAllPlayers())
                {
                    Vector3 pw = WorldUnits.ToWorld(p.FieldPosition);
                    var to = pw - _center;
                    to.Y = 0f;
                    float d = to.Length();
                    const float influence = 8f;
                    if (d < influence && d > 0.05f)
                    {
                        var dir = to / d;
                        var tangent = new Vector3(-dir.Z, 0f, dir.X);
                        float strength = (1f - d / influence) * 12f; // m/s, strongest at the core
                        Vector3 move = (dir * 0.6f + tangent) * strength * dt;
                        p.FieldPosition += new Vector2(move.X, move.Z) * WorldUnits.PixelsPerMeter;
                    }
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

            // Fade the whole funnel in/out over its lifetime
            float lifeFade = Math.Min(1f, Math.Min(_time, Duration - _time) / 1.5f);

            var verts = new System.Collections.Generic.List<VertexPositionColor>(ParticleCount * 6 + DustCount * 6);

            // Funnel: spiral particles, spin faster low, sway grows with height
            for (int i = 0; i < ParticleCount; i++)
            {
                float cycle = (i / (float)ParticleCount + _time * 0.45f) % 1f;
                float h = cycle * FunnelHeight;

                float radius = 1.0f + cycle * cycle * 8.8f; // narrow base, cone opens aloft
                float spinSpeed = 9f - cycle * 4.5f;         // faster near the ground
                float angle = i * 2.39996f + _time * spinSpeed;

                // Sway: the top wobbles around the base
                float swayX = MathF.Sin(_time * 2.1f + h * 0.75f) * h * 0.10f;
                float swayZ = MathF.Cos(_time * 1.7f + h * 0.75f) * h * 0.10f;

                var pos = new Vector3(
                    _center.X + swayX + MathF.Cos(angle) * radius,
                    h,
                    _center.Z + swayZ + MathF.Sin(angle) * radius);

                // Dark slate at the base, faint mist at the top
                byte shade = (byte)(105 + cycle * 110);
                byte alpha = (byte)(210 * (1f - cycle * 0.8f) * lifeFade);
                var color = new Color(shade, (byte)(shade + 6), (byte)(shade + 14), alpha);

                float size = 0.45f + cycle * 0.75f;
                var tangent = new Vector3(-MathF.Sin(angle), 0f, MathF.Cos(angle)) * size;
                var up = Vector3.Up * size * 1.3f;

                verts.Add(new VertexPositionColor(pos - tangent - up, color));
                verts.Add(new VertexPositionColor(pos + tangent + up, color));
                verts.Add(new VertexPositionColor(pos - tangent + up, color));
                verts.Add(new VertexPositionColor(pos - tangent - up, color));
                verts.Add(new VertexPositionColor(pos + tangent - up, color));
                verts.Add(new VertexPositionColor(pos + tangent + up, color));
            }

            // Dust ring: ground debris whipping around the base
            for (int i = 0; i < DustCount; i++)
            {
                float angle = i * (MathF.PI * 2f / DustCount) + _time * 5.5f;
                float r = 2.8f + (i % 3) * 1.8f + MathF.Sin(_time * 6f + i) * 0.6f;
                var pos = new Vector3(
                    _center.X + MathF.Cos(angle) * r,
                    0.12f + (i % 2) * 0.15f,
                    _center.Z + MathF.Sin(angle) * r);

                byte alpha = (byte)(120 * lifeFade);
                var color = new Color((byte)120, (byte)112, (byte)100, alpha);
                float size = 0.45f;
                var up = Vector3.Up * size;
                var sideV = new Vector3(-MathF.Sin(angle), 0f, MathF.Cos(angle)) * size;

                verts.Add(new VertexPositionColor(pos - sideV - up, color));
                verts.Add(new VertexPositionColor(pos + sideV + up, color));
                verts.Add(new VertexPositionColor(pos - sideV + up, color));
                verts.Add(new VertexPositionColor(pos - sideV - up, color));
                verts.Add(new VertexPositionColor(pos + sideV - up, color));
                verts.Add(new VertexPositionColor(pos + sideV + up, color));
            }

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead; // translucent: test but don't write
            device.RasterizerState = RasterizerState.CullNone;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts.ToArray(), 0, verts.Count / 3);
            }
            device.DepthStencilState = DepthStencilState.Default;
        }
    }
}
