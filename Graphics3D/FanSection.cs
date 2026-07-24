using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Graphics3D.Skinning;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Animated supporters on the Bahramis main stand: a handful of adults and
    /// children (scaled down) in NO PASARAN red. Seated fans use Sit_Chair_Idle,
    /// standing ones Idle; everybody Cheers during goal celebrations.
    /// </summary>
    public class FanSection
    {
        private class Fan
        {
            public SkinnedModelInstance Instance;
            public Vector3 Position;
            public float Yaw;
            public float Scale;
            public bool Seated;
            public string IdleClip;
        }
        
        private readonly List<Fan> _fans = new List<Fan>();
        private bool _wasCelebrating;
        
        // Waving flags held by some standing fans
        private class WavingFlag
        {
            public Vector3 Base;   // pole base (at the fan's side)
            public float Phase;    // wave phase offset
            public float Height;   // pole height (children get shorter poles)
        }
        private readonly List<WavingFlag> _flags = new List<WavingFlag>();
        private BasicEffect _flagEffect;
        private Texture2D _flagTexture;
        private float _waveTime;
        private bool _celebrating;
        
        // Stand layout (must match World3D.BuildMainStand)
        private const float StandFrontZ = -40.0f;
        private const float StandStepDepth = 0.85f;
        private const float StandStepHeight = 0.4f;
        private const int Rows = 3;
        
        public FanSection(GraphicsDevice device, SkinnedModel playerModel, SkinnedModel femaleModel = null,
            bool alongFence = false)
        {
            var rng = new Random(4242);
            
            // NO PASARAN supporters: mostly red shirts, some white/black, kids too
            Color[] shirtColors =
            {
                new Color(224, 0, 0), new Color(224, 0, 0), new Color(200, 20, 20),
                new Color(240, 240, 240), new Color(35, 35, 40),
            };
            
            // Two seated rows + a standing row in front of the stand
            // (alongFence: everybody stands at ground level by the fence, wider spread)
            int fanCount = 0;
            for (int row = 0; row < Rows; row++)
            {
                float stepTopY = alongFence ? 0f : (row + 1) * StandStepHeight;
                float rowZ = alongFence
                    ? StandFrontZ - 0.4f - row * 0.9f
                    : StandFrontZ - 0.45f - row * StandStepDepth;
                
                int seatsInRow = alongFence ? 8 + rng.Next(3) : 5 + rng.Next(3);
                for (int s = 0; s < seatsInRow; s++)
                {
                    float x = alongFence
                        ? -18f + fanCount * 2.1f + (float)rng.NextDouble() * 0.8f
                        : -12f + fanCount * 2.1f + (float)rng.NextDouble() * 0.8f;
                    if (x > (alongFence ? 19f : 13f)) continue;
                    
                    bool isChild = rng.NextDouble() < 0.25;
                    bool seated = !alongFence && row > 0; // front row stands, upper rows sit
                    // ~40% female fans when the female model is available
                    var fanModel = femaleModel != null && rng.NextDouble() < 0.4 ? femaleModel : playerModel;
                    var atlas = fanModel.Parts[0].Texture;
                    
                    var fan = new Fan
                    {
                        Instance = new SkinnedModelInstance(fanModel),
                        // Seated fans sit on the 0.45m seat boxes; standing ones on the step
                        Position = new Vector3(x, stepTopY + (seated ? 0.45f : 0f), rowZ),
                        Yaw = 0f, // face +Z (the pitch)
                        Scale = isChild ? 0.55f : 0.72f + (float)rng.NextDouble() * 0.06f,
                        Seated = seated,
                        IdleClip = seated ? "Sit_Chair_Idle" : "Idle",
                    };
                    
                    // Team-colored casual clothes
                    Color shirt = shirtColors[rng.Next(shirtColors.Length)];
                    fan.Instance.SetPartTexture("Soccer_Shirt",
                        KitTextureFactory.GetKitTexture(device, atlas, shirt, new Rectangle(0, 0, 256, 256)));
                    fan.Instance.SetPartTexture("Soccer_Shorts",
                        KitTextureFactory.GetKitTexture(device, atlas,
                            KitTextureFactory.Darken(shirt, 0.4f), new Rectangle(256, 0, 256, 256)));
                    
                    // Randomize clip phase so they don't move in sync
                    fan.Instance.Play(fan.IdleClip);
                    fan.Instance.Update((float)rng.NextDouble() * 3f);
                    
                    // Some standing fans wave Palestinian flags
                    if (!seated && _fans.Count % 3 == 0 && _flags.Count < 6)
                    {
                        _flags.Add(new WavingFlag
                        {
                            Base = fan.Position + new Vector3(0.25f, 0f, 0.1f),
                            Phase = (float)rng.NextDouble() * MathHelper.TwoPi,
                            Height = isChild ? 1.7f : 2.2f,
                        });
                    }
                    
                    _fans.Add(fan);
                    fanCount++;
                }
            }
            
            // Flag rendering resources
            _flagTexture = CreatePalestinianFlagTexture(device);
            _flagEffect = new BasicEffect(device)
            {
                TextureEnabled = true,
                Texture = _flagTexture,
                LightingEnabled = false,
            };
        }
        
        /// <summary>Small generated Palestinian flag: black/white/green stripes, red chevron.</summary>
        private static Texture2D CreatePalestinianFlagTexture(GraphicsDevice device)
        {
            const int w = 64, h = 40;
            var texture = new Texture2D(device, w, h);
            var pixels = new Color[w * h];
            Color black = new Color(20, 20, 20);
            Color white = new Color(245, 245, 245);
            Color green = new Color(0, 122, 61);
            Color red = new Color(206, 17, 38);
            
            for (int y = 0; y < h; y++)
            {
                for (int x = 0; x < w; x++)
                {
                    Color c;
                    // Red chevron from the left edge, apex at 1/3 width
                    float rel = x / (w / 3f) + Math.Abs(y - h / 2f) / (h / 2f);
                    if (rel <= 1f)
                        c = red;
                    else if (y < h / 3)
                        c = black;
                    else if (y < 2 * h / 3)
                        c = white;
                    else
                        c = green;
                    pixels[y * w + x] = c;
                }
            }
            texture.SetData(pixels);
            return texture;
        }
        
        public void Update(float dt, MatchEngine engine)
        {
            bool celebrating = engine.CurrentState == MatchEngine.MatchState.GoalCelebration;
            _celebrating = celebrating;
            _waveTime += dt * (celebrating ? 2.2f : 1f); // flags wave harder during celebrations
            
            foreach (var fan in _fans)
            {
                if (celebrating && !_wasCelebrating)
                {
                    fan.Instance.Play("Cheer");
                }
                else if (!celebrating && _wasCelebrating)
                {
                    fan.Instance.Play(fan.IdleClip);
                }
                fan.Instance.Update(dt);
            }
            
            _wasCelebrating = celebrating;
        }
        
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            foreach (var fan in _fans)
            {
                fan.Instance.Environment = environment;
                Matrix world = Matrix.CreateScale(fan.Scale)
                    * Matrix.CreateRotationY(fan.Yaw)
                    * Matrix.CreateTranslation(fan.Position);
                fan.Instance.Draw(device, world, view, projection);
            }
            
            DrawFlags(device, view, projection, environment);
        }
        
        /// <summary>
        /// Draws the waving flags: a pole plus a cloth made of segments displaced
        /// by a sine wave (amplitude grows toward the free edge).
        /// </summary>
        private void DrawFlags(GraphicsDevice device, Matrix view, Matrix projection, MatchEnvironment environment)
        {
            if (_flags.Count == 0) return;
            
            environment.ApplyTo(_flagEffect, false);
            _flagEffect.View = view;
            _flagEffect.Projection = projection;
            _flagEffect.World = Matrix.Identity;
            
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.Default;
            device.BlendState = BlendState.Opaque;
            
            const int segX = 6, segY = 3;
            const float clothW = 2.2f, clothH = 1.4f;
            
            foreach (var flag in _flags)
            {
                var verts = new List<VertexPositionTexture>();
                var indices = new List<int>();
                
                // Pole (thin vertical quad, textured from the dark top stripe)
                Vector3 poleBottom = flag.Base;
                Vector3 poleTop = flag.Base + new Vector3(0f, flag.Height, 0f);
                AddTexturedQuad(verts, indices,
                    poleBottom + new Vector3(-0.012f, 0f, 0f), new Vector2(0.99f, 0.02f),
                    poleBottom + new Vector3(0.012f, 0f, 0f), new Vector2(0.99f, 0.02f),
                    poleTop + new Vector3(0.012f, 0f, 0f), new Vector2(0.99f, 0f),
                    poleTop + new Vector3(-0.012f, 0f, 0f), new Vector2(0.99f, 0f));
                
                // Cloth: hangs from the pole top, extends in +X, waves with sine
                Vector3 clothTopLeft = poleTop + new Vector3(0f, -0.02f, 0f);
                for (int y = 0; y <= segY; y++)
                {
                    for (int x = 0; x <= segX; x++)
                    {
                        float u = x / (float)segX;
                        float v = y / (float)segY;
                        float wave = (float)Math.Sin(_waveTime * 4f + flag.Phase + u * 5f) * 0.25f * u;
                        var pos = clothTopLeft + new Vector3(u * clothW, -v * clothH, wave);
                        verts.Add(new VertexPositionTexture(pos, new Vector2(u, v)));
                    }
                }
                for (int y = 0; y < segY; y++)
                {
                    for (int x = 0; x < segX; x++)
                    {
                        int a = y * (segX + 1) + x + 4; // +4 pole verts
                        int b = a + segX + 1;
                        indices.Add(a);
                        indices.Add(b);
                        indices.Add(a + 1);
                        indices.Add(a + 1);
                        indices.Add(b);
                        indices.Add(b + 1);
                    }
                }
                
                foreach (var pass in _flagEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                        verts.ToArray(), 0, verts.Count, indices.ToArray(), 0, indices.Count / 3);
                }
            }
        }
        
        private static void AddTexturedQuad(List<VertexPositionTexture> verts, List<int> indices,
            Vector3 a, Vector2 uvA, Vector3 b, Vector2 uvB, Vector3 c, Vector2 uvC, Vector3 d, Vector2 uvD)
        {
            int baseIndex = verts.Count;
            verts.Add(new VertexPositionTexture(a, uvA));
            verts.Add(new VertexPositionTexture(b, uvB));
            verts.Add(new VertexPositionTexture(c, uvC));
            verts.Add(new VertexPositionTexture(d, uvD));
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }
    }
}
