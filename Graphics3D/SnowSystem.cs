using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Cheap snow (winter easter egg): ~500 drifting snowflakes in a volume
    /// centered on the camera target. Slower and floatier than rain, with a
    /// side-to-side sway. No per-frame allocations.
    /// </summary>
    public class SnowSystem
    {
        private const int FlakeCount = 500;
        private const float VolumeX = 60f;
        private const float VolumeY = 25f;
        private const float VolumeZ = 40f;
        private static readonly Color SnowColor = new Color(245, 248, 252, 220);

        private readonly BasicEffect _effect;
        private readonly Vector3[] _positions = new Vector3[FlakeCount];
        private readonly float[] _speeds = new float[FlakeCount];
        private readonly float[] _phases = new float[FlakeCount];
        private readonly VertexPositionColor[] _lineVertices = new VertexPositionColor[FlakeCount * 2];
        private readonly Random _random = new Random();
        private Vector3 _center;
        private float _time;
        private bool _initialized;

        public SnowSystem(GraphicsDevice device)
        {
            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
        }

        public void Update(float dt, Vector3 cameraTarget)
        {
            _time += dt;
            if (!_initialized)
            {
                _initialized = true;
                _center = cameraTarget;
                for (int i = 0; i < FlakeCount; i++)
                    Respawn(i, anywhereInVolume: true);
            }

            Vector3 shift = cameraTarget - _center;
            _center = cameraTarget;

            for (int i = 0; i < FlakeCount; i++)
            {
                // Slow fall + gentle sway
                _positions[i] += shift;
                _positions[i].Y -= _speeds[i] * dt;
                _positions[i].X += MathF.Sin(_time * 1.5f + _phases[i]) * 0.6f * dt;
                _positions[i].Z += MathF.Cos(_time * 1.2f + _phases[i]) * 0.6f * dt;

                if (_positions[i].Y < 0f)
                    Respawn(i, anywhereInVolume: false);
            }

            // Flakes as small bright streaks
            for (int i = 0; i < FlakeCount; i++)
            {
                var p = _positions[i];
                _lineVertices[i * 2] = new VertexPositionColor(p, SnowColor);
                _lineVertices[i * 2 + 1] = new VertexPositionColor(
                    p + new Vector3(0f, 0.12f, 0f), SnowColor);
            }
        }

        private void Respawn(int i, bool anywhereInVolume)
        {
            _positions[i] = new Vector3(
                _center.X + (float)(_random.NextDouble() * 2 - 1) * VolumeX / 2f,
                anywhereInVolume ? (float)_random.NextDouble() * VolumeY : VolumeY * 0.9f,
                _center.Z + (float)(_random.NextDouble() * 2 - 1) * VolumeZ / 2f);
            _speeds[i] = 1.2f + (float)_random.NextDouble() * 1.2f; // much slower than rain
            _phases[i] = (float)_random.NextDouble() * MathF.PI * 2f;
        }

        public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
        {
            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList, _lineVertices, 0, FlakeCount);
            }
            device.DepthStencilState = DepthStencilState.Default;
        }
    }
}
