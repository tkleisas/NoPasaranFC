using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg (1% of penalties): a grand piano falls from the sky onto the
    /// penalty taker right after the kick, flattening him. Looney Tunes rules.
    /// </summary>
    public class PianoFx
    {
        private readonly Player _victim;
        private Vector3 _position;
        private float _velocity;
        private bool _landed;
        private float _lingerTimer = 3f;
        private BasicEffect _effect;

        public bool IsDone => _landed && _lingerTimer <= 0f;

        public PianoFx(Player victim)
        {
            _victim = victim;
            var ground = WorldUnits.ToWorld(victim.FieldPosition);
            _position = new Vector3(ground.X, 25f, ground.Z); // 25m up
        }

        public void Update(float dt, MatchEngine engine)
        {
            if (!_landed)
            {
                _velocity += 30f * dt; // acceleration
                _position.Y -= _velocity * dt;
                // Track the victim slightly (he just kicked, may stumble forward)
                var ground = WorldUnits.ToWorld(_victim.FieldPosition);
                _position.X = MathHelper.Lerp(_position.X, ground.X, dt * 2f);
                _position.Z = MathHelper.Lerp(_position.Z, ground.Z, dt * 2f);

                if (_position.Y <= 0.3f)
                {
                    _position.Y = 0.3f;
                    _landed = true;
                    engine.EasterEggKnockdown(_victim);
                    AudioManager.Instance.PlaySoundEffect("piano_crash", 0.9f);
                }
            }
            else
            {
                _lingerTimer -= dt;
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
            if (environment != null)
                environment.ApplyTo(_effect, false);

            var verts = new System.Collections.Generic.List<VertexPositionColor>();
            var indices = new System.Collections.Generic.List<int>();

            Color body = new Color(18, 16, 20);
            Color lid = new Color(28, 25, 30);
            Color keys = new Color(235, 232, 225);

            // Grand-piano-ish box: body + slightly open lid + white key strip
            Vector3 b0 = _position + new Vector3(-0.9f, 0f, -0.6f);
            Vector3 b1 = _position + new Vector3(0.9f, 0.5f, 0.6f);
            AddBox(verts, indices, b0, b1, body);
            AddBox(verts, indices,
                _position + new Vector3(-0.9f, 0.5f, -0.65f),
                _position + new Vector3(0.9f, 0.65f, 0.65f), lid);
            // Keys strip on the front
            AddBox(verts, indices,
                _position + new Vector3(-0.8f, 0.15f, 0.6f),
                _position + new Vector3(0.8f, 0.3f, 0.7f), keys);
            // Three legs
            foreach (var (lx, lz) in new[] { (-0.7f, -0.4f), (0.7f, -0.4f), (0f, 0.5f) })
                AddBox(verts, indices,
                    _position + new Vector3(lx - 0.05f, -0.6f, lz - 0.05f),
                    _position + new Vector3(lx + 0.05f, 0f, lz + 0.05f), body);

            _effect.World = Matrix.Identity;
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    verts.ToArray(), 0, verts.Count, indices.ToArray(), 0, indices.Count / 3);
            }
        }

        private static void AddBox(System.Collections.Generic.List<VertexPositionColor> verts,
            System.Collections.Generic.List<int> indices, Vector3 min, Vector3 max, Color color)
        {
            int i0 = verts.Count;
            Vector3[] c =
            {
                new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z),
                new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z),
                new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z),
            };
            foreach (var v in c) verts.Add(new VertexPositionColor(v, color));
            int[] quads =
            {
                0,1,2, 0,2,3, 4,6,5, 4,7,6, // bottom, top
                0,4,5, 0,5,1, 1,5,6, 1,6,2, // sides
                2,6,7, 2,7,3, 3,7,4, 3,4,0,
            };
            foreach (int i in quads) indices.Add(i0 + i);
        }
    }
}
