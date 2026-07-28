using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg (December): Santa flies his sleigh across the stadium sky and
    /// drops gifts on the players. A gift that lands on a player knocks him down.
    /// </summary>
    public class SantaSleigh
    {
        private class Gift
        {
            public Vector3 Position;
            public float VelocityY;
            public bool Landed;
        }

        private const float Duration = 16f;
        private const int GiftCount = 5;

        private readonly Random _random;
        private readonly Vector3 _start;
        private readonly Vector3 _end;
        private readonly System.Collections.Generic.List<Gift> _gifts = new System.Collections.Generic.List<Gift>();
        private float _time;
        private float _nextGiftAt;
        private BasicEffect _effect;

        public bool IsDone => _time > Duration + 4f;

        public SantaSleigh(Random random)
        {
            _random = random ?? new Random();
            float halfL = WorldUnits.PitchLengthMeters / 2f;
            float halfW = WorldUnits.PitchWidthMeters / 2f;
            float from = _random.NextDouble() < 0.5 ? -1f : 1f;
            float z = (float)(_random.NextDouble() * 2 - 1) * halfW * 0.7f;
            _start = new Vector3(from * (halfL + 25f), 14f, z);
            _end = new Vector3(-from * (halfL + 25f), 14f, z + (float)(_random.NextDouble() - 0.5f) * 10f);
            _nextGiftAt = 2f;
        }

        public void Update(float dt, MatchEngine engine)
        {
            _time += dt;

            // Drop gifts along the path
            if (_gifts.Count < GiftCount && _time >= _nextGiftAt)
            {
                _gifts.Add(new Gift { Position = SleighPosition() });
                _nextGiftAt = _time + 1.5f + (float)_random.NextDouble() * 2f;
            }

            // Gifts fall with gravity; landing on a player knocks him down
            foreach (var gift in _gifts)
            {
                if (gift.Landed) continue;
                gift.VelocityY += 20f * dt;
                gift.Position = new Vector3(gift.Position.X, gift.Position.Y - gift.VelocityY * dt, gift.Position.Z);

                if (gift.Position.Y <= 0.2f)
                {
                    gift.Position = new Vector3(gift.Position.X, 0.2f, gift.Position.Z);
                    gift.Landed = true;
                    foreach (var p in engine.GetAllPlayers())
                    {
                        var pw = WorldUnits.ToWorld(p.FieldPosition);
                        if (Vector2.Distance(new Vector2(pw.X, pw.Z),
                                new Vector2(gift.Position.X, gift.Position.Z)) < 0.8f)
                        {
                            engine.EasterEggKnockdown(p);
                            break;
                        }
                    }
                }
            }
        }

        private Vector3 SleighPosition() => Vector3.Lerp(_start, _end, Math.Min(1f, _time / Duration));

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

            var verts = new System.Collections.Generic.List<VertexPositionColor>();
            var indices = new System.Collections.Generic.List<int>();
            Vector3 pos = SleighPosition();

            // Sleigh: brown box + runners
            AddBox(verts, indices, pos + new Vector3(-1.2f, 0.2f, -0.6f), pos + new Vector3(1.2f, 0.9f, 0.6f), new Color(110, 60, 30));
            AddBox(verts, indices, pos + new Vector3(-1.3f, 0f, -0.7f), pos + new Vector3(1.3f, 0.2f, 0.7f), new Color(160, 40, 40));
            // Santa: red body + white trim
            AddBox(verts, indices, pos + new Vector3(-0.35f, 0.9f, -0.35f), pos + new Vector3(0.35f, 1.7f, 0.35f), new Color(200, 25, 25));
            AddBox(verts, indices, pos + new Vector3(-0.3f, 1.7f, -0.3f), pos + new Vector3(0.3f, 2.0f, 0.3f), new Color(240, 240, 240));
            // Reindeer blobs ahead (4)
            Vector3 dir = Vector3.Normalize(new Vector3(_end.X - _start.X, 0f, _end.Z - _start.Z));
            for (int i = 0; i < 4; i++)
            {
                var deerPos = pos + dir * (1.8f + (i % 2) * 1.2f) + new Vector3(0f, 0.5f, (i < 2 ? -0.7f : 0.7f));
                AddBox(verts, indices, deerPos + new Vector3(-0.5f, -0.3f, -0.3f), deerPos + new Vector3(0.5f, 0.3f, 0.3f), new Color(120, 85, 55));
            }
            // Gifts
            foreach (var gift in _gifts)
            {
                Color wrap = _gifts.IndexOf(gift) % 2 == 0 ? new Color(200, 30, 30) : new Color(220, 180, 40);
                AddBox(verts, indices,
                    gift.Position + new Vector3(-0.25f, -0.2f, -0.25f),
                    gift.Position + new Vector3(0.25f, 0.3f, 0.25f), wrap);
                AddBox(verts, indices,
                    gift.Position + new Vector3(-0.26f, 0.05f, -0.26f),
                    gift.Position + new Vector3(0.26f, 0.15f, 0.26f), new Color(240, 240, 240));
            }

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
                0,1,2, 0,2,3, 4,6,5, 4,7,6,
                0,4,5, 0,5,1, 1,5,6, 1,6,2,
                2,6,7, 2,7,3, 3,7,4, 3,4,0,
            };
            foreach (int i in quads) indices.Add(i0 + i);
        }
    }
}
