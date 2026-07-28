using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg: the sprinkler system suddenly fires up mid-match for ~15
    /// seconds — fan-shaped water jets arcing across the pitch from both
    /// touchlines, soaking everyone.
    /// </summary>
    public class SprinklersFx
    {
        private const float Duration = 15f;
        private const int JetsPerHead = 14;
        private const float JetHeight = 2.2f;

        private readonly Vector3[] _heads;
        private float _time;
        private BasicEffect _effect;

        public bool IsDone => _time > Duration;

        public SprinklersFx(Random random)
        {
            float halfL = WorldUnits.PitchLengthMeters / 2f;
            float halfW = WorldUnits.PitchWidthMeters / 2f;
            var heads = new System.Collections.Generic.List<Vector3>();
            for (float x = -halfL + 8f; x <= halfL - 8f; x += 12f)
            {
                heads.Add(new Vector3(x, 0f, -halfW - 1f));
                heads.Add(new Vector3(x + 6f, 0f, halfW + 1f));
            }
            _heads = heads.ToArray();
        }

        public void Update(float dt)
        {
            _time += dt;
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

            var water = new Color(150, 190, 235, 130);
            var metal = new Color(60, 60, 65);

            var headTris = new System.Collections.Generic.List<VertexPositionColor>();
            var jetLines = new System.Collections.Generic.List<VertexPositionColor>();
            foreach (var head in _heads)
            {
                // Sprinkler head (small dark box)
                AddQuad(headTris, head + new Vector3(-0.1f, 0f, -0.1f), head + new Vector3(0.1f, 0.25f, 0.1f), metal);

                // Fan of parabolic jets sweeping back and forth
                float sweep = MathF.Sin(_time * 1.8f + head.X * 0.3f) * 0.9f;
                float toCenter = head.Z > 0f ? -1f : 1f;
                for (int j = 0; j < JetsPerHead; j++)
                {
                    float spread = (j / (float)(JetsPerHead - 1) - 0.5f) * 1.6f + sweep;
                    float range = 2.5f + j * 0.35f;
                    for (int s = 0; s < 6; s++)
                    {
                        float t = s / 5f;
                        float t2 = (s + 1) / 5f;
                        Vector3 dir = new Vector3(MathF.Sin(spread), 0f, toCenter * MathF.Cos(spread));
                        Vector3 a = head + dir * (range * t) + Vector3.Up * Parabola(t) ;
                        Vector3 b = head + dir * (range * t2) + Vector3.Up * Parabola(t2);
                        jetLines.Add(new VertexPositionColor(a, water));
                        jetLines.Add(new VertexPositionColor(b, water));
                    }
                }
            }

            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.DepthRead;
            device.RasterizerState = RasterizerState.CullNone;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                if (headTris.Count > 0)
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, headTris.ToArray(), 0, headTris.Count / 3);
                if (jetLines.Count > 0)
                    device.DrawUserPrimitives(PrimitiveType.LineList, jetLines.ToArray(), 0, jetLines.Count / 2);
            }
            device.DepthStencilState = DepthStencilState.Default;
        }

        private static float Parabola(float t) => JetHeight * 4f * t * (1f - t) + 0.2f;

        private static void AddQuad(System.Collections.Generic.List<VertexPositionColor> verts,
            Vector3 min, Vector3 max, Color color)
        {
            // Simple vertical box for the sprinkler head (4 side quads)
            Vector3[] corners =
            {
                new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z),
            };
            for (int i = 0; i < 4; i++)
            {
                var a = corners[i];
                var b = corners[(i + 1) % 4];
                var aTop = a + Vector3.Up * (max.Y - min.Y);
                var bTop = b + Vector3.Up * (max.Y - min.Y);
                verts.Add(new VertexPositionColor(a, color));
                verts.Add(new VertexPositionColor(b, color));
                verts.Add(new VertexPositionColor(bTop, color));
                verts.Add(new VertexPositionColor(a, color));
                verts.Add(new VertexPositionColor(bTop, color));
                verts.Add(new VertexPositionColor(aTop, color));
            }
        }
    }
}
