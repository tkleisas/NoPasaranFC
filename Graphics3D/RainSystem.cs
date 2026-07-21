using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Cheap rain: ~800 falling streak particles in a volume centered on the
    /// camera target, respawning at the top when they hit the ground. Drawn as
    /// a LineList of short velocity-aligned streaks with a slight wind slant.
    /// No per-frame allocations - the line vertex buffer is preallocated.
    /// </summary>
    public class RainSystem
    {
        private const int DropCount = 800;
        private const float VolumeX = 60f;  // Volume size around the camera target (m)
        private const float VolumeY = 30f;
        private const float VolumeZ = 40f;
        private const float StreakLength = 0.5f;
        private static readonly Vector3 WindSlant = new Vector3(-2f, 0f, 1f);
        private static readonly Color RainColor = new Color(170, 180, 200, 90); // ~0.35 alpha
        
        private readonly BasicEffect _effect;
        private readonly Vector3[] _positions = new Vector3[DropCount];
        private readonly float[] _speeds = new float[DropCount];
        private readonly VertexPositionColor[] _lineVertices = new VertexPositionColor[DropCount * 2];
        private readonly Random _random = new Random();
        private Vector3 _center;
        private bool _initialized;
        
        public RainSystem(GraphicsDevice device)
        {
            _effect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
        }
        
        /// <summary>Advance the rain. The volume follows the camera target.</summary>
        public void Update(float dt, Vector3 cameraTarget)
        {
            if (!_initialized)
            {
                _initialized = true;
                _center = cameraTarget;
                for (int i = 0; i < DropCount; i++)
                    Respawn(i, anywhereInVolume: true);
            }
            
            // Keep the volume centered on the camera target
            Vector3 shift = cameraTarget - _center;
            _center = cameraTarget;
            
            for (int i = 0; i < DropCount; i++)
            {
                _positions[i] += shift;
                _positions[i] += (WindSlant + new Vector3(0f, -_speeds[i], 0f)) * dt;
                
                if (_positions[i].Y <= 0f)
                    Respawn(i, anywhereInVolume: false);
            }
        }
        
        private void Respawn(int index, bool anywhereInVolume)
        {
            _positions[index] = _center + new Vector3(
                ((float)_random.NextDouble() - 0.5f) * VolumeX,
                anywhereInVolume ? (float)_random.NextDouble() * VolumeY : VolumeY,
                ((float)_random.NextDouble() - 0.5f) * VolumeZ);
            _speeds[index] = 16f + (float)_random.NextDouble() * 6f;
        }
        
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
        {
            Vector3 streakDir = Vector3.Normalize(WindSlant + new Vector3(0f, -18f, 0f));
            
            for (int i = 0; i < DropCount; i++)
            {
                _lineVertices[i * 2] = new VertexPositionColor(_positions[i], RainColor);
                _lineVertices[i * 2 + 1] = new VertexPositionColor(_positions[i] - streakDir * StreakLength, RainColor);
            }
            
            _effect.View = view;
            _effect.Projection = projection;
            _effect.World = Matrix.Identity;
            
            device.BlendState = BlendState.NonPremultiplied;
            device.DepthStencilState = DepthStencilState.DepthRead; // Last pass: don't write depth
            device.RasterizerState = RasterizerState.CullNone;
            
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList,
                    _lineVertices, 0, DropCount);
            }
        }
    }
}
