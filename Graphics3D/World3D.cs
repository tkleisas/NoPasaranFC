using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Static 3D world geometry for the match view: pitch, field markings,
    /// goals with nets, and stadium stands. All dimensions in meters,
    /// derived from the same MatchEngine pixel constants the 2D view uses.
    /// </summary>
    public class World3D
    {
        private readonly BasicEffect _colorEffect;
        private readonly BasicEffect _pitchEffect;
        private readonly Texture2D _grassTexture;
        
        // Opaque geometry (apron, ground, markings, posts, stands, ad boards)
        private VertexPositionColor[] _opaqueVertices;
        private int[] _opaqueIndices;
        
        // Textured pitch plane
        private VertexPositionTexture[] _pitchVertices;
        private int[] _pitchIndices;
        
        // Crowd (animated stand fronts, tiled texture)
        private BasicEffect _crowdEffect;
        private Texture2D[] _crowdTextures;
        private readonly List<VertexPositionTexture> _crowdVertList = new List<VertexPositionTexture>();
        private readonly List<int> _crowdIndexList = new List<int>();
        private VertexPositionTexture[] _crowdVertices;
        private int[] _crowdIndices;
        private float _crowdTimer;
        private int _crowdTextureIndex;
        private const float CrowdFrameDuration = 0.4f;
        
        // Field dimensions in meters (same constants as MatchScreen.DrawFieldMarkings)
        private readonly float _halfLength = WorldUnits.PitchLengthMeters / 2f;   // 52.5
        private readonly float _halfWidth = WorldUnits.PitchWidthMeters / 2f;     // 34
        private const float LineY = 0.02f;
        private const float LineWidth = 0.12f; // FIFA ~12cm lines
        
        public World3D(GraphicsDevice device)
        {
            _colorEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
            
            _grassTexture = CreateGrassTexture(device);
            _pitchEffect = new BasicEffect(device)
            {
                VertexColorEnabled = false,
                TextureEnabled = true,
                Texture = _grassTexture,
                LightingEnabled = false
            };
            
            _crowdTextures = CreateCrowdTextures(device);
            _crowdEffect = new BasicEffect(device)
            {
                VertexColorEnabled = false,
                TextureEnabled = true,
                Texture = _crowdTextures[0],
                LightingEnabled = false
            };
            
            BuildPitch();
            BuildOpaqueGeometry();
            
            _crowdVertices = _crowdVertList.ToArray();
            _crowdIndices = _crowdIndexList.ToArray();
        }
        
        /// <summary>Applies the environment tint to all world effects.</summary>
        public void ApplyEnvironment(MatchEnvironment environment)
        {
            environment.ApplyTo(_colorEffect, false);
            environment.ApplyTo(_pitchEffect, false);
            environment.ApplyTo(_crowdEffect, false);
        }
        
        /// <summary>Cycles the crowd textures for a shimmering crowd effect.</summary>
        public void Update(float dt)
        {
            _crowdTimer += dt;
            if (_crowdTimer >= CrowdFrameDuration)
            {
                _crowdTimer = 0f;
                _crowdTextureIndex = (_crowdTextureIndex + 1) % _crowdTextures.Length;
            }
        }
        
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
        {
            _colorEffect.View = view;
            _colorEffect.Projection = projection;
            _colorEffect.World = Matrix.Identity;
            _pitchEffect.View = view;
            _pitchEffect.Projection = projection;
            _pitchEffect.World = Matrix.Identity;
            _crowdEffect.View = view;
            _crowdEffect.Projection = projection;
            _crowdEffect.World = Matrix.Identity;
            
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            
            // Textured striped pitch
            foreach (var pass in _pitchEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _pitchVertices, 0, _pitchVertices.Length,
                    _pitchIndices, 0, _pitchIndices.Length / 3);
            }
            
            // Opaque vertex-colored geometry
            foreach (var pass in _colorEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _opaqueVertices, 0, _opaqueVertices.Length,
                    _opaqueIndices, 0, _opaqueIndices.Length / 3);
            }
            
            // Animated crowd on the stand fronts (tiled horizontally)
            _crowdEffect.Texture = _crowdTextures[_crowdTextureIndex];
            device.SamplerStates[0] = SamplerState.LinearWrap;
            foreach (var pass in _crowdEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _crowdVertices, 0, _crowdVertices.Length,
                    _crowdIndices, 0, _crowdIndices.Length / 3);
            }
        }
        
        #region Pitch + grass texture
        
        private Texture2D CreateGrassTexture(GraphicsDevice device)
        {
            // Mowing stripes: 5m wide stripes across the 105m length
            const int size = 512;
            const int stripeCount = 21; // 105m / 5m
            Texture2D texture = new Texture2D(device, size, size);
            Color[] data = new Color[size * size];
            Color grassA = new Color(34, 139, 34);
            Color grassB = new Color(26, 112, 26);
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int stripe = x * stripeCount / size;
                    data[y * size + x] = (stripe % 2 == 0) ? grassA : grassB;
                }
            }
            
            texture.SetData(data);
            return texture;
        }
        
        private void BuildPitch()
        {
            float y = 0.01f; // Slightly above the apron to avoid z-fighting
            _pitchVertices = new[]
            {
                new VertexPositionTexture(new Vector3(-_halfLength, y, -_halfWidth), new Vector2(0, 0)),
                new VertexPositionTexture(new Vector3(_halfLength, y, -_halfWidth), new Vector2(1, 0)),
                new VertexPositionTexture(new Vector3(_halfLength, y, _halfWidth), new Vector2(1, 1)),
                new VertexPositionTexture(new Vector3(-_halfLength, y, _halfWidth), new Vector2(0, 1)),
            };
            _pitchIndices = new[] { 0, 1, 2, 0, 2, 3 };
        }
        
        #endregion
        
        #region Opaque geometry (apron, ground, markings, goals, stands)
        
        private void BuildOpaqueGeometry()
        {
            var verts = new List<VertexPositionColor>();
            var indices = new List<int>();
            
            // Dark ground plane far beyond the stadium
            AddGroundQuad(verts, indices, -150f, -110f, 150f, 110f, -0.02f, new Color(35, 35, 35));
            
            // Green apron around the pitch
            float apron = 6f;
            AddGroundQuad(verts, indices,
                -_halfLength - apron, -_halfWidth - apron,
                _halfLength + apron, _halfWidth + apron,
                0f, new Color(24, 100, 24));
            
            BuildFieldMarkings(verts, indices);
            BuildGoals(verts, indices);
            BuildStands(verts, indices);
            BuildAdBoards(verts, indices);
            
            _opaqueVertices = verts.ToArray();
            _opaqueIndices = indices.ToArray();
        }
        
        private void BuildFieldMarkings(List<VertexPositionColor> verts, List<int> indices)
        {
            Color lineColor = Color.White;
            
            // Outer boundary (touchlines + goal lines)
            AddRectOutline(verts, indices, -_halfLength, -_halfWidth, _halfLength, _halfWidth, LineWidth, lineColor);
            
            // Halfway line
            AddLineQuad(verts, indices, new Vector2(0, -_halfWidth), new Vector2(0, _halfWidth), LineWidth, lineColor);
            
            // Center circle (FIFA: 9.15m radius) + center spot
            AddRing(verts, indices, Vector2.Zero, 9.15f, LineWidth, 32, lineColor);
            AddGroundQuad(verts, indices, -0.15f, -0.15f, 0.15f, 0.15f, LineY, lineColor);
            
            // Penalty areas (FIFA: 40.3m wide x 16.5m deep)
            float penaltyHalfWidth = 40.3f / 2f;
            float penaltyDepth = 16.5f;
            AddRectOutline(verts, indices, -_halfLength, -penaltyHalfWidth, -_halfLength + penaltyDepth, penaltyHalfWidth, LineWidth, lineColor);
            AddRectOutline(verts, indices, _halfLength - penaltyDepth, -penaltyHalfWidth, _halfLength, penaltyHalfWidth, LineWidth, lineColor);
            
            // Penalty spots (11m from goal line)
            AddGroundQuad(verts, indices, -_halfLength + 11f - 0.15f, -0.15f, -_halfLength + 11f + 0.15f, 0.15f, LineY, lineColor);
            AddGroundQuad(verts, indices, _halfLength - 11f - 0.15f, -0.15f, _halfLength - 11f + 0.15f, 0.15f, LineY, lineColor);
            
            // Goal areas / 6-yard boxes (FIFA: 18.3m wide x 5.5m deep)
            float goalAreaHalfWidth = 18.3f / 2f;
            float goalAreaDepth = 5.5f;
            AddRectOutline(verts, indices, -_halfLength, -goalAreaHalfWidth, -_halfLength + goalAreaDepth, goalAreaHalfWidth, LineWidth, lineColor);
            AddRectOutline(verts, indices, _halfLength - goalAreaDepth, -goalAreaHalfWidth, _halfLength, goalAreaHalfWidth, LineWidth, lineColor);
            
            // Corner arcs (FIFA: 1m radius)
            float cornerRadius = 1f;
            AddArc(verts, indices, new Vector2(-_halfLength, -_halfWidth), cornerRadius, 0, MathHelper.PiOver2, 8, lineColor);
            AddArc(verts, indices, new Vector2(_halfLength, -_halfWidth), cornerRadius, MathHelper.PiOver2, MathHelper.Pi, 8, lineColor);
            AddArc(verts, indices, new Vector2(_halfLength, _halfWidth), cornerRadius, MathHelper.Pi, MathHelper.Pi + MathHelper.PiOver2, 8, lineColor);
            AddArc(verts, indices, new Vector2(-_halfLength, _halfWidth), cornerRadius, -MathHelper.PiOver2, 0, 8, lineColor);
        }
        
        private void BuildGoals(List<VertexPositionColor> verts, List<int> indices)
        {
            float goalHalfWidth = WorldUnits.PxToM(MatchEngine.GoalWidth) / 2f; // 7.32m / 2
            float goalHeight = WorldUnits.PxToM(MatchEngine.GoalPostHeight);    // 2.44m
            const float postThickness = 0.12f;
            Color postColor = Color.White;
            
            BuildGoal(verts, indices, -_halfLength, -1f, goalHalfWidth, goalHeight, postThickness, postColor);
            BuildGoal(verts, indices, _halfLength, 1f, goalHalfWidth, goalHeight, postThickness, postColor);
        }
        
        private void BuildGoal(List<VertexPositionColor> verts, List<int> indices,
            float goalLineX, float direction, float halfWidth, float height, float thickness, Color color)
        {
            float x0 = goalLineX - thickness / 2f;
            float x1 = goalLineX + thickness / 2f;
            
            // Two upright posts
            AddBox(verts, indices,
                new Vector3(x0, 0f, -halfWidth - thickness / 2f),
                new Vector3(x1, height, -halfWidth + thickness / 2f), color);
            AddBox(verts, indices,
                new Vector3(x0, 0f, halfWidth - thickness / 2f),
                new Vector3(x1, height, halfWidth + thickness / 2f), color);
            
            // Crossbar
            AddBox(verts, indices,
                new Vector3(x0, height - thickness, -halfWidth - thickness / 2f),
                new Vector3(x1, height, halfWidth + thickness / 2f), color);
        }
        
        private void BuildStands(List<VertexPositionColor> verts, List<int> indices)
        {
            // Bowl stadium: 3 stepped tiers per side, starting just past the
            // stadium margin. Tier fronts get the animated crowd texture
            // (built into the crowd lists), everything else is plain concrete.
            Color concrete = new Color(80, 60, 60);
            float gap = WorldUnits.StadiumMarginMeters;
            const int tierCount = 3;
            const float tierDepth = 7f;
            float totalDepth = tierCount * tierDepth;
            // Tier heights (base, top) per tier
            float[] tierBase = { 0f, 4.5f, 9.5f };
            float[] tierTop = { 4.5f, 9.5f, 15f };
            
            // 4 sides: local coords (lateral l, height y, outward o) -> world
            BuildStandSide(verts, indices, tierCount, tierDepth, tierBase, tierTop, concrete,
                _halfLength + gap + totalDepth,
                (l, y, o) => new Vector3(l, y, -_halfWidth - gap - o)); // North (camera side)
            BuildStandSide(verts, indices, tierCount, tierDepth, tierBase, tierTop, concrete,
                _halfLength + gap + totalDepth,
                (l, y, o) => new Vector3(l, y, _halfWidth + gap + o));  // South
            BuildStandSide(verts, indices, tierCount, tierDepth, tierBase, tierTop, concrete,
                _halfWidth + gap + totalDepth,
                (l, y, o) => new Vector3(-_halfLength - gap - o, y, l)); // West
            BuildStandSide(verts, indices, tierCount, tierDepth, tierBase, tierTop, concrete,
                _halfWidth + gap + totalDepth,
                (l, y, o) => new Vector3(_halfLength + gap + o, y, l));  // East
        }
        
        /// <summary>
        /// Builds one side of the bowl as stepped tiers. The N/S sides span the
        /// full X width (including corners) like the old flat stands did.
        /// </summary>
        private void BuildStandSide(List<VertexPositionColor> verts, List<int> indices,
            int tierCount, float tierDepth,
            float[] tierBase, float[] tierTop, Color concrete,
            float lateralHalfSpan,
            Func<float, float, float, Vector3> toWorld)
        {
            const float crowdTileMeters = 3f; // One crowd texture tile per 3m
            
            for (int tier = 0; tier < tierCount; tier++)
            {
                float frontO = tier * tierDepth;
                float backO = frontO + tierDepth;
                float y0 = tierBase[tier];
                float y1 = tierTop[tier];
                
                // Concrete tier block (toWorld can flip an axis per side, so
                // compute the true component-wise min/max for AddBox)
                Vector3 corner0 = toWorld(-lateralHalfSpan, y0, frontO);
                Vector3 corner1 = toWorld(lateralHalfSpan, y1, backO);
                AddBox(verts, indices,
                    Vector3.Min(corner0, corner1),
                    Vector3.Max(corner0, corner1), concrete);
                
                // Crowd-textured front face, floating 2cm in front of the block
                float uMax = lateralHalfSpan * 2f / crowdTileMeters;
                AddTexturedQuad(_crowdVertList, _crowdIndexList,
                    toWorld(-lateralHalfSpan, y0, frontO - 0.02f), new Vector2(0f, 1f),
                    toWorld(lateralHalfSpan, y0, frontO - 0.02f), new Vector2(uMax, 1f),
                    toWorld(lateralHalfSpan, y1, frontO - 0.02f), new Vector2(uMax, 0f),
                    toWorld(-lateralHalfSpan, y1, frontO - 0.02f), new Vector2(0f, 0f));
            }
        }
        
        /// <summary>
        /// Ring of thin ad boards around the pitch apron (~0.9m tall), alternating
        /// bright colors with a contrasting stripe band (fake branding, no text).
        /// </summary>
        private void BuildAdBoards(List<VertexPositionColor> verts, List<int> indices)
        {
            Color[] palette =
            {
                new Color(240, 240, 240),
                new Color(0, 180, 220),
                new Color(250, 210, 40),
                new Color(230, 40, 140),
                new Color(250, 130, 30),
                new Color(60, 200, 80),
            };
            const float boardHeight = 0.9f;
            const float stripeTop = 0.62f; // Main color below, stripe above
            const float boardWidth = 4f;
            const float boardGap = 0.15f;
            float sideOffset = _halfWidth + 2.0f;  // Touchline boards
            float endOffset = _halfLength + 2.6f;  // Behind the goal nets (2m deep)
            
            int colorIndex = 0;
            
            // Touchline boards (run along X at both Z sides)
            for (float x = -_halfLength; x < _halfLength - 0.01f; x += boardWidth + boardGap)
            {
                float x1 = Math.Min(x + boardWidth, _halfLength);
                AddAdBoard(verts, indices,
                    new Vector3(x, 0f, -sideOffset), new Vector3(x1, 0f, -sideOffset),
                    stripeTop, boardHeight, palette, ref colorIndex);
                AddAdBoard(verts, indices,
                    new Vector3(x, 0f, sideOffset), new Vector3(x1, 0f, sideOffset),
                    stripeTop, boardHeight, palette, ref colorIndex);
            }
            
            // Goal-end boards (run along Z at both X ends)
            for (float z = -_halfWidth; z < _halfWidth - 0.01f; z += boardWidth + boardGap)
            {
                float z1 = Math.Min(z + boardWidth, _halfWidth);
                AddAdBoard(verts, indices,
                    new Vector3(-endOffset, 0f, z), new Vector3(-endOffset, 0f, z1),
                    stripeTop, boardHeight, palette, ref colorIndex);
                AddAdBoard(verts, indices,
                    new Vector3(endOffset, 0f, z), new Vector3(endOffset, 0f, z1),
                    stripeTop, boardHeight, palette, ref colorIndex);
            }
        }
        
        private static void AddAdBoard(List<VertexPositionColor> verts, List<int> indices,
            Vector3 from, Vector3 to, float stripeTop, float boardHeight,
            Color[] palette, ref int colorIndex)
        {
            Color main = palette[colorIndex % palette.Length];
            colorIndex++;
            // Contrasting stripe: white on colored boards, cyan on the white board
            Color stripe = main == palette[0] ? palette[1] : palette[0];
            
            Vector3 fromStripe = from + new Vector3(0f, stripeTop, 0f);
            Vector3 toStripe = to + new Vector3(0f, stripeTop, 0f);
            Vector3 fromTop = from + new Vector3(0f, boardHeight, 0f);
            Vector3 toTop = to + new Vector3(0f, boardHeight, 0f);
            
            AddQuad(verts, indices, from, to, toStripe, fromStripe, main);
            AddQuad(verts, indices, fromStripe, toStripe, toTop, fromTop, stripe);
        }
        
        #endregion
        
        #region Crowd textures
        
        /// <summary>
        /// Generates 3 crowd texture variants (256x64) for the shimmering crowd
        /// animation. Dense random pixels: mostly bright shirt colors with some
        /// skin tones over dark seats. The home kit color isn't available here,
        /// so the left half of the texture is biased toward red/yellow and the
        /// right half toward blue/white.
        /// </summary>
        private static Texture2D[] CreateCrowdTextures(GraphicsDevice device)
        {
            const int width = 256;
            const int height = 64;
            var random = new Random();
            var textures = new Texture2D[3];
            
            Color[] homeShirts =
            {
                new Color(200, 30, 30), new Color(230, 60, 40),
                new Color(240, 200, 30), new Color(210, 170, 20),
            };
            Color[] awayShirts =
            {
                new Color(30, 60, 200), new Color(60, 120, 230),
                new Color(230, 230, 230), new Color(160, 160, 170),
            };
            Color[] skinTones =
            {
                new Color(224, 184, 144), new Color(196, 154, 116), new Color(160, 120, 88),
            };
            Color emptySeat = new Color(45, 45, 55);
            Color gapColor = new Color(35, 35, 42);
            
            // Fans are blocks of texels (one block = one seated fan) so the crowd
            // reads as people from broadcast distance instead of per-pixel noise.
            const int fanW = 5;
            const int fanH = 6;
            
            for (int t = 0; t < textures.Length; t++)
            {
                var data = new Color[width * height];
                for (int i = 0; i < data.Length; i++) data[i] = gapColor;
                
                for (int fy = 0; fy + fanH <= height; fy += fanH)
                {
                    for (int fx = 0; fx + fanW <= width; fx += fanW)
                    {
                        int roll = random.Next(100);
                        Color fanColor;
                        if (roll < 10)
                        {
                            fanColor = emptySeat;
                        }
                        else
                        {
                            if (roll < 22)
                                fanColor = skinTones[random.Next(skinTones.Length)];
                            else
                            {
                                var palette = fx < width / 2 ? homeShirts : awayShirts;
                                fanColor = palette[random.Next(palette.Length)];
                            }
                            
                            int jitter = random.Next(-15, 16);
                            fanColor = new Color(
                                Math.Clamp(fanColor.R + jitter, 0, 255),
                                Math.Clamp(fanColor.G + jitter, 0, 255),
                                Math.Clamp(fanColor.B + jitter, 0, 255));
                        }
                        
                        // Fill the fan block with a 1px gap on the right and bottom
                        for (int y = fy; y < fy + fanH - 1; y++)
                            for (int x = fx; x < fx + fanW - 1; x++)
                                data[y * width + x] = fanColor;
                    }
                }
                
                textures[t] = new Texture2D(device, width, height);
                textures[t].SetData(data);
            }
            
            return textures;
        }
        
        #endregion
        
        #region Geometry helpers
        
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
        
        private static void AddQuad(List<VertexPositionColor> verts, List<int> indices,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color)
        {
            int baseIndex = verts.Count;
            verts.Add(new VertexPositionColor(a, color));
            verts.Add(new VertexPositionColor(b, color));
            verts.Add(new VertexPositionColor(c, color));
            verts.Add(new VertexPositionColor(d, color));
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 2);
            indices.Add(baseIndex + 3);
        }
        
        private static void AddGroundQuad(List<VertexPositionColor> verts, List<int> indices,
            float x0, float z0, float x1, float z1, float y, Color color)
        {
            AddQuad(verts, indices,
                new Vector3(x0, y, z0),
                new Vector3(x1, y, z0),
                new Vector3(x1, y, z1),
                new Vector3(x0, y, z1), color);
        }
        
        /// <summary>Adds a flat line strip (quad) between two XZ points at marking height.</summary>
        private static void AddLineQuad(List<VertexPositionColor> verts, List<int> indices,
            Vector2 from, Vector2 to, float width, Color color)
        {
            Vector2 dir = to - from;
            if (dir.LengthSquared() < 0.0001f) return;
            dir.Normalize();
            Vector2 perp = new Vector2(-dir.Y, dir.X) * (width / 2f);
            
            AddQuad(verts, indices,
                new Vector3(from.X - perp.X, LineY, from.Y - perp.Y),
                new Vector3(to.X - perp.X, LineY, to.Y - perp.Y),
                new Vector3(to.X + perp.X, LineY, to.Y + perp.Y),
                new Vector3(from.X + perp.X, LineY, from.Y + perp.Y), color);
        }
        
        private static void AddRectOutline(List<VertexPositionColor> verts, List<int> indices,
            float x0, float z0, float x1, float z1, float width, Color color)
        {
            AddLineQuad(verts, indices, new Vector2(x0, z0), new Vector2(x1, z0), width, color);
            AddLineQuad(verts, indices, new Vector2(x1, z0), new Vector2(x1, z1), width, color);
            AddLineQuad(verts, indices, new Vector2(x1, z1), new Vector2(x0, z1), width, color);
            AddLineQuad(verts, indices, new Vector2(x0, z1), new Vector2(x0, z0), width, color);
        }
        
        /// <summary>Adds a ring (circle outline) as a strip of quads in the XZ plane.</summary>
        private static void AddRing(List<VertexPositionColor> verts, List<int> indices,
            Vector2 center, float radius, float width, int segments, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;
                AddLineQuad(verts, indices, p1, p2, width, color);
            }
        }
        
        /// <summary>Adds an arc as a strip of quads in the XZ plane.</summary>
        private static void AddArc(List<VertexPositionColor> verts, List<int> indices,
            Vector2 center, float radius, float startAngle, float endAngle, int segments, Color color)
        {
            for (int i = 0; i < segments; i++)
            {
                float angle1 = startAngle + (endAngle - startAngle) * i / segments;
                float angle2 = startAngle + (endAngle - startAngle) * (i + 1) / segments;
                
                Vector2 p1 = center + new Vector2((float)Math.Cos(angle1), (float)Math.Sin(angle1)) * radius;
                Vector2 p2 = center + new Vector2((float)Math.Cos(angle2), (float)Math.Sin(angle2)) * radius;
                AddLineQuad(verts, indices, p1, p2, LineWidth, color);
            }
        }
        
        /// <summary>Adds an axis-aligned box (6 faces) with outward-facing quads.</summary>
        private static void AddBox(List<VertexPositionColor> verts, List<int> indices,
            Vector3 min, Vector3 max, Color color)
        {
            // Bottom
            AddQuad(verts, indices,
                new Vector3(min.X, min.Y, min.Z), new Vector3(min.X, min.Y, max.Z),
                new Vector3(max.X, min.Y, max.Z), new Vector3(max.X, min.Y, min.Z), color);
            // Top
            AddQuad(verts, indices,
                new Vector3(min.X, max.Y, min.Z), new Vector3(max.X, max.Y, min.Z),
                new Vector3(max.X, max.Y, max.Z), new Vector3(min.X, max.Y, max.Z), color);
            // North (-Z)
            AddQuad(verts, indices,
                new Vector3(min.X, min.Y, min.Z), new Vector3(max.X, min.Y, min.Z),
                new Vector3(max.X, max.Y, min.Z), new Vector3(min.X, max.Y, min.Z), color);
            // South (+Z)
            AddQuad(verts, indices,
                new Vector3(max.X, min.Y, max.Z), new Vector3(min.X, min.Y, max.Z),
                new Vector3(min.X, max.Y, max.Z), new Vector3(max.X, max.Y, max.Z), color);
            // West (-X)
            AddQuad(verts, indices,
                new Vector3(min.X, min.Y, max.Z), new Vector3(min.X, min.Y, min.Z),
                new Vector3(min.X, max.Y, min.Z), new Vector3(min.X, max.Y, max.Z), color);
            // East (+X)
            AddQuad(verts, indices,
                new Vector3(max.X, min.Y, min.Z), new Vector3(max.X, min.Y, max.Z),
                new Vector3(max.X, max.Y, max.Z), new Vector3(max.X, max.Y, min.Z), color);
        }
        
        #endregion
    }
}
