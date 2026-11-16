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
        private readonly float[] _sizes;
        private readonly float[] _bankPhase;

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
            _sizes = new float[BirdCount];
            _bankPhase = new float[BirdCount];
            for (int i = 0; i < BirdCount; i++)
            {
                // Loose V formation behind the leader
                _offsets[i] = -i * (1.2f + (float)_random.NextDouble() * 0.8f);
                _lateral[i] = (i % 2 == 0 ? 1f : -1f) * (i + 1) * 0.55f;
                _flapPhase[i] = (float)_random.NextDouble() * MathF.PI * 2f;
                _sizes[i] = 0.82f + (float)_random.NextDouble() * 0.36f;
                _bankPhase[i] = (float)_random.NextDouble() * MathF.PI * 2f;
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

        private static readonly Vector3 BirdSunDir =
            Vector3.Normalize(new Vector3(0.35f, 0.8f, -0.25f));

        private static Color ShadeBird(Vector3 normal, Color color)
        {
            if (normal.LengthSquared() > 1e-8f) normal.Normalize();
            float ndl = Math.Abs(Vector3.Dot(normal, BirdSunDir));
            float f = 0.72f + 0.34f * ndl;
            return new Color(
                Math.Clamp((int)(color.R * f), 0, 255),
                Math.Clamp((int)(color.G * f), 0, 255),
                Math.Clamp((int)(color.B * f), 0, 255),
                color.A);
        }

        private static void AddTri(System.Collections.Generic.List<VertexPositionColor> verts,
            Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Color shaded = ShadeBird(normal, color);
            verts.Add(new VertexPositionColor(a, shaded));
            verts.Add(new VertexPositionColor(b, shaded));
            verts.Add(new VertexPositionColor(c, shaded));
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

            Color bodyColor = _seagull ? new Color(240, 242, 238) : new Color(34, 32, 38);
            Color wingColor = _seagull ? new Color(216, 223, 227) : new Color(48, 46, 58);
            Color tipColor = _seagull ? new Color(80, 86, 92) : new Color(22, 21, 26);
            Color beakColor = _seagull ? new Color(235, 170, 45) : new Color(24, 22, 26);

            var verts = new System.Collections.Generic.List<VertexPositionColor>(BirdCount * 36);
            float speed = Vector3.Distance(_start, _end) / Duration;

            for (int i = 0; i < BirdCount; i++)
            {
                float travel = speed * _time + _offsets[i];
                if (travel < 0f) continue; // bird hasn't entered yet

                Vector3 pos = BirdPosition(i, out Vector3 forward);
                Vector3 side = Vector3.Normalize(Vector3.Cross(forward, Vector3.Up));
                Vector3 up = Vector3.Cross(side, forward);
                float size = _sizes[i];

                // Bank into the formation's path; seagulls bank much harder
                float bank = MathF.Sin(_time * 0.8f + _bankPhase[i]) * (_seagull ? 0.45f : 0.18f);
                Vector3 bankedSide = side * MathF.Cos(bank) + up * MathF.Sin(bank);
                Vector3 bankedUp = up * MathF.Cos(bank) - side * MathF.Sin(bank);

                // Crows flap continuously; seagulls glide with occasional bursts
                float flapSpeed = _seagull ? 7f : 11f;
                float phase = _time * flapSpeed + _flapPhase[i];
                float flap = MathF.Sin(phase);
                float flapLag = MathF.Sin(phase - 0.35f);
                float amplitude;
                if (_seagull)
                {
                    float glide = 0.5f + 0.5f * MathF.Sin(_time * 0.55f + _flapPhase[i]);
                    amplitude = glide < 0.55f ? 0.10f : 0.55f;
                }
                else
                {
                    amplitude = 0.55f;
                }

                Vector3 nose = pos + forward * (0.42f * size);
                Vector3 tail = pos - forward * (0.55f * size);
                Vector3 top = pos + bankedUp * (0.11f * size);
                Vector3 bottom = pos - bankedUp * (0.08f * size);
                Vector3 left = pos + bankedSide * (0.12f * size);
                Vector3 right = pos - bankedSide * (0.12f * size);

                // Faceted body
                AddTri(verts, nose, top, left, bodyColor);
                AddTri(verts, nose, left, bottom, bodyColor);
                AddTri(verts, nose, bottom, right, bodyColor);
                AddTri(verts, nose, right, top, bodyColor);
                AddTri(verts, tail, left, top, bodyColor);
                AddTri(verts, tail, bottom, left, bodyColor);
                AddTri(verts, tail, right, bottom, bodyColor);
                AddTri(verts, tail, top, right, bodyColor);

                // Tail fan
                Vector3 tailMid = tail - forward * (0.22f * size);
                AddTri(verts, tail,
                    tail + bankedSide * (0.20f * size) - forward * (0.04f * size),
                    tailMid, tipColor);
                AddTri(verts, tail, tailMid,
                    tail - bankedSide * (0.20f * size) - forward * (0.04f * size), tipColor);

                // Beak
                Vector3 beakTip = nose + forward * (0.14f * size);
                AddTri(verts, nose, nose + bankedUp * (0.035f * size), beakTip, beakColor);
                AddTri(verts, nose, beakTip, nose - bankedUp * (0.025f * size), beakColor);

                // Wings: swept quad plus a root triangle, with lagging tips
                float span = _seagull ? 1.15f : 0.95f;
                for (int w = 0; w < 2; w++)
                {
                    float s = w == 0 ? 1f : -1f;
                    Vector3 root = s > 0 ? left : right;
                    Vector3 shoulder = pos + bankedSide * s * (0.10f * size) + bankedUp * (0.03f * size);
                    Vector3 elbow = pos + bankedSide * s * (0.45f * size) +
                        bankedUp * (0.03f + flap * amplitude * 0.28f) * size;
                    Vector3 tip = pos + bankedSide * s * (span * size) +
                        bankedUp * (flapLag * amplitude * 0.75f) * size - forward * (0.06f * size);
                    Vector3 trailing = pos + bankedSide * s * (0.38f * size) -
                        forward * (0.30f * size) - bankedUp * (0.02f * size);

                    AddTri(verts, shoulder, elbow, tip, wingColor);
                    AddTri(verts, shoulder, tip, trailing, wingColor);
                    AddTri(verts, shoulder, trailing, root, wingColor);
                }
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
