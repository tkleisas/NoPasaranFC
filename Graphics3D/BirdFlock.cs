using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg: a small flock of birds crossing the stadium sky.
    /// Crows (dark, straight flight) or seagulls (white, gentle circling).
    /// Each bird is two flapping wing triangles - no textures needed.
    /// </summary>
    public class BirdFlock
    {
        private const int BirdCount = 6;
        private const float Duration = 14f; // seconds to cross the sky

        private readonly bool _seagull;
        private readonly Random _random;
        private readonly Vector3 _start;
        private readonly Vector3 _end;
        private readonly float _baseHeight;
        private readonly float[] _offsets;   // per-bird trail offset along the path
        private readonly float[] _lateral;   // per-bird lateral offset
        private readonly float[] _flapPhase;

        private float _time;
        private BasicEffect _effect;

        public bool IsDone => _time > Duration + 3f; // last bird clears the far side

        public BirdFlock(bool seagull, Random random)
        {
            _seagull = seagull;
            _random = random ?? new Random();

            float halfL = WorldUnits.PitchLengthMeters / 2f;
            float halfW = WorldUnits.PitchWidthMeters / 2f;
            // Low enough to stay inside the broadcast camera frame
            _baseHeight = 7f + (float)_random.NextDouble() * 3f;

            // Cross the stadium from a random side to the opposite one
            bool alongX = _random.NextDouble() < 0.5;
            float from = _random.NextDouble() < 0.5 ? -1f : 1f;
            if (alongX)
            {
                _start = new Vector3(from * (halfL + 15f), _baseHeight,
                    (float)(_random.NextDouble() * 2 - 1) * halfW);
                _end = new Vector3(-from * (halfL + 15f), _baseHeight,
                    (float)(_random.NextDouble() * 2 - 1) * halfW);
            }
            else
            {
                _start = new Vector3((float)(_random.NextDouble() * 2 - 1) * halfL,
                    _baseHeight, from * (halfW + 15f));
                _end = new Vector3((float)(_random.NextDouble() * 2 - 1) * halfL,
                    _baseHeight, -from * (halfW + 15f));
            }

            _offsets = new float[BirdCount];
            _lateral = new float[BirdCount];
            _flapPhase = new float[BirdCount];
            for (int i = 0; i < BirdCount; i++)
            {
                // Loose V formation behind the leader
                _offsets[i] = -i * (1.2f + (float)_random.NextDouble() * 0.8f);
                _lateral[i] = (i % 2 == 0 ? 1f : -1f) * (i + 1) * 0.55f;
                _flapPhase[i] = (float)_random.NextDouble() * MathF.PI * 2f;
            }
        }

        public void Update(float dt)
        {
            _time += dt;
        }

        private Vector3 BirdPosition(int i, out Vector3 forward)
        {
            float speed = Vector3.Distance(_start, _end) / Duration;
            float travel = speed * _time + _offsets[i];

            Vector3 path = _end - _start;
            float pathLen = path.Length();
            forward = path / pathLen;

            Vector3 pos = _start + forward * travel;

            // Lateral offset perpendicular to the flight direction
            var side = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
            pos += side * _lateral[i];

            // Wobble; seagulls also swing in slow circles
            pos.Y += MathF.Sin(_time * 1.7f + i * 1.3f) * 0.4f;
            if (_seagull)
                pos += side * MathF.Sin(_time * 0.6f + i) * 2.5f;

            return pos;
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

            var color = _seagull ? new Color(235, 235, 230) : new Color(35, 32, 36);

            // Two triangles per bird (wings), flapping around the body axis
            var verts = new System.Collections.Generic.List<VertexPositionColor>(BirdCount * 6);
            float speed = Vector3.Distance(_start, _end) / Duration;
            for (int i = 0; i < BirdCount; i++)
            {
                float travel = speed * _time + _offsets[i];
                if (travel < 0f) continue; // bird hasn't entered yet

                Vector3 pos = BirdPosition(i, out Vector3 forward);
                var side = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
                Vector3 tail = pos - forward * 0.45f;

                float flap = MathF.Sin(_time * 9f + _flapPhase[i]) * 0.35f;
                float span = _seagull ? 0.9f : 0.7f;
                Vector3 wingL = pos + side * span + Vector3.Up * flap;
                Vector3 wingR = pos - side * span + Vector3.Up * flap;

                verts.Add(new VertexPositionColor(pos, color));
                verts.Add(new VertexPositionColor(wingL, color));
                verts.Add(new VertexPositionColor(tail, color));
                verts.Add(new VertexPositionColor(pos, color));
                verts.Add(new VertexPositionColor(tail, color));
                verts.Add(new VertexPositionColor(wingR, color));
            }

            if (verts.Count == 0) return;

            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone; // wings are single-sided
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts.ToArray(), 0, verts.Count / 3);
            }
        }
    }
}
