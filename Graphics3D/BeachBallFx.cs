using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg: a stray beach ball drifts onto the pitch and gets nudged
    /// around by players and the match ball, like a real pitch interruption.
    /// Rolls back out after a while.
    /// </summary>
    public class BeachBallFx
    {
        private const float Radius = 0.35f;
        private const float Duration = 40f;

        private readonly Random _random;
        private Vector3 _position;
        private Vector3 _velocity;
        private float _time;
        private bool _leaving;
        private BasicEffect _effect;
        private Texture2D _texture;

        public bool IsDone => _time > Duration + 10f;

        public BeachBallFx(Random random)
        {
            _random = random ?? new Random();
            float halfW = WorldUnits.PitchWidthMeters / 2f;
            float side = _random.NextDouble() < 0.5 ? -1f : 1f;
            _position = new Vector3((float)(_random.NextDouble() * 2 - 1) * 20f, Radius, side * (halfW + 4f));
            _velocity = new Vector3((float)(_random.NextDouble() - 0.5) * 3f, 3f, -side * 2.5f);
        }

        public void Update(float dt, MatchEngine engine)
        {
            _time += dt;
            if (_time > Duration && !_leaving)
            {
                _leaving = true;
                _velocity = new Vector3(_velocity.X, 4f, Math.Sign(_position.Z) * 3f);
            }

            // Physics: gravity + ground bounce + drag
            _velocity.Y -= 12f * dt;
            _position += _velocity * dt;
            if (_position.Y < Radius)
            {
                _position.Y = Radius;
                _velocity.Y = Math.Abs(_velocity.Y) * 0.55f;
                if (Math.Abs(_velocity.Y) < 0.8f) _velocity.Y = 0f;
                _velocity.X *= 0.97f;
                _velocity.Z *= 0.97f;
            }

            // Players and the match ball nudge it
            Vector3 ballWorld = WorldUnits.ToWorld(engine.BallPosition, engine.BallHeight);
            NudgeIfClose(ballWorld, 2.5f);
            foreach (var p in engine.GetAllPlayers())
            {
                NudgeIfClose(WorldUnits.ToWorld(p.FieldPosition), 1.5f);
            }
        }

        private void NudgeIfClose(Vector3 other, float strength)
        {
            var delta = _position - other;
            delta.Y = 0f;
            float d = delta.Length();
            if (d < Radius + 0.3f && d > 0.01f)
            {
                delta /= d;
                _velocity += delta * strength + Vector3.Up * strength * 0.7f;
            }
        }

        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            _effect ??= new BasicEffect(device)
            {
                TextureEnabled = true,
                VertexColorEnabled = false,
                LightingEnabled = false
            };
            _texture ??= CreateBeachBallTexture(device);
            _effect.Texture = _texture;
            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            if (environment != null)
                environment.ApplyTo(_effect, false);

            // Camera-facing billboard quad
            Vector3 right, up;
            var viewInv = Matrix.Invert(view);
            right = viewInv.Right; up = viewInv.Up;
            Vector3 p0 = _position - right * Radius - up * Radius;
            Vector3 p1 = _position + right * Radius - up * Radius;
            Vector3 p2 = _position + right * Radius + up * Radius;
            Vector3 p3 = _position - right * Radius + up * Radius;

            var verts = new[]
            {
                new VertexPositionTexture(p0, Vector2.Zero),
                new VertexPositionTexture(p1, Vector2.UnitX),
                new VertexPositionTexture(p2, Vector2.One),
                new VertexPositionTexture(p0, Vector2.Zero),
                new VertexPositionTexture(p2, Vector2.One),
                new VertexPositionTexture(p3, new Vector2(0f, 1f)),
            };

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, 2);
            }
        }

        /// <summary>Beach ball: white disc with red/blue/yellow wedges.</summary>
        private static Texture2D CreateBeachBallTexture(GraphicsDevice device)
        {
            const int size = 64;
            var texture = new Texture2D(device, size, size);
            var pixels = new Color[size * size];
            var colors = new[] { Color.Red, Color.White, Color.RoyalBlue, Color.White, Color.Gold, Color.White };
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dx = x - size / 2f, dy = y - size / 2f;
                    float r = MathF.Sqrt(dx * dx + dy * dy);
                    if (r > size / 2f) { pixels[y * size + x] = Color.Transparent; continue; }
                    float angle = MathF.Atan2(dy, dx) + MathF.PI;
                    int wedge = (int)(angle / (MathF.PI / 3f)) % 6;
                    pixels[y * size + x] = colors[wedge];
                }
            }
            texture.SetData(pixels);
            return texture;
        }
    }
}
