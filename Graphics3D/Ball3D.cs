using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// 3D ball: a generated UV-sphere mesh with basic lighting that rolls
    /// based on ball velocity, plus a circular blob shadow on the ground
    /// that shrinks and fades as the ball gains height.
    /// </summary>
    public class Ball3D
    {
        // Stylized ball size: roughly the chibi player head diameter (~0.4m),
        // matching the game's chibi proportions (a realistic 22cm ball reads
        // tiny next to the oversized heads). Real football would be 0.11f.
        public static readonly float RadiusMeters = 0.19f;
        
        private readonly BasicEffect _sphereEffect;
        private readonly BasicEffect _shadowEffect;
        private VertexPositionNormalTexture[] _sphereVertices;
        private int[] _sphereIndices;
        private VertexPositionTexture[] _shadowVertices;
        private Vector3[] _shadowBase; // Untransformed shadow quad positions
        private int[] _shadowIndices;
        private Texture2D _shadowTexture; // Radial-fade circle for a soft round shadow
        
        private Matrix _rotation = Matrix.Identity;
        private Vector3 _position;
        private float _heightMeters;
        
        public Ball3D(GraphicsDevice device)
        {
            _sphereEffect = new BasicEffect(device);
            _sphereEffect.EnableDefaultLighting();
            _sphereEffect.DiffuseColor = Color.White.ToVector3();
            _sphereEffect.SpecularColor = new Vector3(0.2f);
            
            // Real soccer-ball panels (generated in Blender, Content/Models3D).
            // Optional: if the file is missing the ball stays plain white.
            var panelTexture = TryLoadPanelTexture(device);
            if (panelTexture != null)
            {
                _sphereEffect.Texture = panelTexture;
                _sphereEffect.TextureEnabled = true;
            }
            
            _shadowEffect = new BasicEffect(device)
            {
                VertexColorEnabled = false,
                TextureEnabled = true,
                LightingEnabled = false
            };
            _shadowTexture = CreateRadialShadowTexture(device, 64);
            _shadowEffect.Texture = _shadowTexture;
            
            BuildSphere(12, 12);
            BuildShadow();
        }
        
        private static Texture2D TryLoadPanelTexture(GraphicsDevice device)
        {
            try
            {
#if ANDROID
                var context = global::Android.App.Application.Context;
                using (var stream = context.Assets.Open("Content/Models3D/ball_texture.png"))
                    return Texture2D.FromStream(device, stream);
#else
                string path = PlatformHelper.GetAssetPath(System.IO.Path.Combine("Content", "Models3D", "ball_texture.png"));
                using (var stream = System.IO.File.OpenRead(path))
                    return Texture2D.FromStream(device, stream);
#endif
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Ball3D: no panel texture ({ex.Message}) - plain white ball.");
                return null;
            }
        }
        
        /// <summary>Applies the match environment lighting to the ball sphere.</summary>
        public void ApplyEnvironment(MatchEnvironment environment)
        {
            environment.ApplyTo(_sphereEffect, true);
        }
        
        /// <summary>Soft black circle with smooth alpha falloff from center to edge.</summary>
        private static Texture2D CreateRadialShadowTexture(GraphicsDevice device, int size)
        {
            var texture = new Texture2D(device, size, size);
            var pixels = new Color[size * size];
            float center = (size - 1) / 2f;
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    float dist = (float)Math.Sqrt((x - center) * (x - center) + (y - center) * (y - center)) / center;
                    float t = Math.Clamp(1f - dist, 0f, 1f);
                    float alpha = t * t * (3f - 2f * t); // smoothstep falloff
                    pixels[y * size + x] = new Color(0, 0, 0, (int)(alpha * 255));
                }
            }
            texture.SetData(pixels);
            return texture;
        }
        
        /// <summary>
        /// Update ball position (engine pixels), roll rotation from velocity
        /// (engine px/s) and current height (engine px).
        /// </summary>
        public void Update(Vector2 ballPosPx, Vector2 ballVelocityPxPerSec, float ballHeightPx, float dt)
        {
            _heightMeters = WorldUnits.PxToM(ballHeightPx);
            _position = WorldUnits.ToWorld(ballPosPx, ballHeightPx) + new Vector3(0f, RadiusMeters, 0f);
            
            // Roll the ball around the axis perpendicular to its movement direction
            Vector2 vel = ballVelocityPxPerSec;
            if (vel.LengthSquared() > 1f)
            {
                Vector3 velMeters = new Vector3(vel.X, 0f, vel.Y) / WorldUnits.PixelsPerMeter;
                float speed = velMeters.Length();
                Vector3 axis = Vector3.Normalize(Vector3.Cross(Vector3.Up, velMeters));
                float angle = (speed / RadiusMeters) * dt;
                _rotation *= Matrix.CreateFromAxisAngle(axis, angle);
            }
        }
        
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
        {
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            
            // Blob shadow first (semi-transparent, on the ground below the ball)
            float heightFactor = Math.Clamp(_heightMeters / 4f, 0f, 1f);
            float shadowScale = 1f + heightFactor * 0.5f;  // Shadow grows slightly when ball is higher
            _shadowEffect.Alpha = Math.Max(0.15f, 0.5f - _heightMeters * 0.12f); // Fades with height
            
            Vector3 shadowCenter = new Vector3(_position.X, 0.025f, _position.Z);
            for (int i = 0; i < _shadowVertices.Length; i++)
            {
                // Shadow quad is built as a base quad centered at origin; scale + translate it
                Vector3 v = _shadowBase[i];
                _shadowVertices[i].Position = shadowCenter + new Vector3(v.X * shadowScale, 0f, v.Z * shadowScale);
            }
            
            _shadowEffect.View = view;
            _shadowEffect.Projection = projection;
            _shadowEffect.World = Matrix.Identity;
            
            device.BlendState = BlendState.AlphaBlend;
            foreach (var pass in _shadowEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _shadowVertices, 0, _shadowVertices.Length,
                    _shadowIndices, 0, _shadowIndices.Length / 3);
            }
            device.BlendState = BlendState.Opaque;
            
            // Sphere
            _sphereEffect.View = view;
            _sphereEffect.Projection = projection;
            _sphereEffect.World = _rotation * Matrix.CreateTranslation(_position);
            
            foreach (var pass in _sphereEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _sphereVertices, 0, _sphereVertices.Length,
                    _sphereIndices, 0, _sphereIndices.Length / 3);
            }
        }
        
        private void BuildSphere(int stacks, int slices)
        {
            var verts = new List<VertexPositionNormalTexture>();
            var indices = new List<int>();
            
            for (int stack = 0; stack <= stacks; stack++)
            {
                float phi = MathHelper.Pi * stack / stacks; // 0 (top) to PI (bottom)
                float y = (float)Math.Cos(phi);
                float r = (float)Math.Sin(phi);
                
                for (int slice = 0; slice <= slices; slice++)
                {
                    float theta = MathHelper.TwoPi * slice / slices;
                    Vector3 normal = new Vector3(
                        r * (float)Math.Cos(theta),
                        y,
                        r * (float)Math.Sin(theta));
                    Vector2 uv = new Vector2(slice / (float)slices, stack / (float)stacks);
                    verts.Add(new VertexPositionNormalTexture(normal * RadiusMeters, normal, uv));
                }
            }
            
            for (int stack = 0; stack < stacks; stack++)
            {
                for (int slice = 0; slice < slices; slice++)
                {
                    int a = stack * (slices + 1) + slice;
                    int b = a + slices + 1;
                    
                    indices.Add(a);
                    indices.Add(b);
                    indices.Add(a + 1);
                    
                    indices.Add(a + 1);
                    indices.Add(b);
                    indices.Add(b + 1);
                }
            }
            
            _sphereVertices = verts.ToArray();
            _sphereIndices = indices.ToArray();
        }
        
        private void BuildShadow()
        {
            // Base quad in the XZ plane centered at origin (scaled/translated per frame)
            float s = RadiusMeters * 2f;
            _shadowBase = new[]
            {
                new Vector3(-s, 0f, -s),
                new Vector3(s, 0f, -s),
                new Vector3(s, 0f, s),
                new Vector3(-s, 0f, s),
            };
            _shadowVertices = new[]
            {
                new VertexPositionTexture(Vector3.Zero, new Vector2(0, 0)),
                new VertexPositionTexture(Vector3.Zero, new Vector2(1, 0)),
                new VertexPositionTexture(Vector3.Zero, new Vector2(1, 1)),
                new VertexPositionTexture(Vector3.Zero, new Vector2(0, 1)),
            };
            _shadowIndices = new[] { 0, 1, 2, 0, 2, 3 };
        }
    }
}
