using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>Match venue: the generic bowl stadium, the Bahramis municipal ground, or the Sperchogeia ground.</summary>
    public enum Venue { Bowl, Bahramis, Sperchogeia }
    
    /// <summary>
    /// Static 3D world geometry for the match view: pitch, field markings,
    /// goals with nets, and the venue surroundings. All dimensions in meters,
    /// derived from the same MatchEngine pixel constants the 2D view uses.
    /// </summary>
    public class World3D
    {
        private readonly BasicEffect _colorEffect;
        private readonly BasicEffect _pitchEffect;
        private readonly Texture2D _grassTexture;
        private readonly Venue _venue;
        
        // Opaque geometry (apron, ground, markings, posts, stands, ad boards)
        private VertexPositionColor[] _opaqueVertices;
        private int[] _opaqueIndices;
        
        // Textured pitch plane
        private VertexPositionTexture[] _pitchVertices;
        private int[] _pitchIndices;
        
        // Crowd (animated stand fronts, tiled texture) - Bowl venue only
        private BasicEffect _crowdEffect;
        private Texture2D[] _crowdTextures;
        private readonly List<VertexPositionTexture> _crowdVertList = new List<VertexPositionTexture>();
        private readonly List<int> _crowdIndexList = new List<int>();
        private VertexPositionTexture[] _crowdVertices;
        private int[] _crowdIndices;
        private float _crowdTimer;
        private int _crowdTextureIndex;
        private const float CrowdFrameDuration = 0.4f;
        
        // Bahramis venue: chain-link fence (alpha-blended diamond-grid texture)
        private BasicEffect _fenceEffect;
        private Texture2D _fenceTexture;
        private readonly List<VertexPositionTexture> _fenceVertList = new List<VertexPositionTexture>();
        private readonly List<int> _fenceIndexList = new List<int>();
        private VertexPositionTexture[] _fenceVertices;
        private int[] _fenceIndices;
        
        // Bahramis venue: scoreboard panel (text rendered to a texture)
        private BasicEffect _scoreboardEffect;
        private Texture2D _scoreboardTexture;
        private VertexPositionTexture[] _scoreboardVertices;
        private int[] _scoreboardIndices;
        
        // Sperchogeia venue: sponsor banners hung on the fence (text baked to an atlas)
        private BasicEffect _bannerEffect;
        private Texture2D _bannerTexture;
        private readonly List<VertexPositionTexture> _bannerVertList = new List<VertexPositionTexture>();
        private readonly List<int> _bannerIndexList = new List<int>();
        private VertexPositionTexture[] _bannerVertices;
        private int[] _bannerIndices;
        
        // Field dimensions in meters (same constants as MatchScreen.DrawFieldMarkings)
        private readonly float _halfLength = WorldUnits.PitchLengthMeters / 2f;   // 52.5
        private readonly float _halfWidth = WorldUnits.PitchWidthMeters / 2f;     // 34
        private const float LineY = 0.02f;
        private const float LineWidth = 0.12f; // FIFA ~12cm lines
        
        public World3D(GraphicsDevice device, ContentManager content = null, Venue venue = Venue.Bahramis)
        {
            _venue = venue;
            
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
            
            if (_venue == Venue.Bowl)
            {
                _crowdTextures = CreateCrowdTextures(device);
                _crowdEffect = new BasicEffect(device)
                {
                    VertexColorEnabled = false,
                    TextureEnabled = true,
                    Texture = _crowdTextures[0],
                    LightingEnabled = false
                };
            }
            else
            {
                _fenceTexture = CreateChainLinkTexture(device);
                _fenceEffect = new BasicEffect(device)
                {
                    VertexColorEnabled = false,
                    TextureEnabled = true,
                    Texture = _fenceTexture,
                    LightingEnabled = false
                };
                
                string signText = _venue == Venue.Sperchogeia
                    ? "ΓΗΠΕΔΟ ΣΠΕΡΧΟΓΕΙΑΣ"
                    : "ΠΑΝΑΓΙΩΤΗΣ ΜΠΑΧΡΑΜΗΣ";
                _scoreboardTexture = CreateScoreboardTexture(device, content, signText);
                _scoreboardEffect = new BasicEffect(device)
                {
                    VertexColorEnabled = false,
                    TextureEnabled = true,
                    Texture = _scoreboardTexture,
                    LightingEnabled = false
                };
                
                if (_venue == Venue.Sperchogeia)
                {
                    _bannerTexture = CreateBannerTexture(device, content);
                    _bannerEffect = new BasicEffect(device)
                    {
                        VertexColorEnabled = false,
                        TextureEnabled = true,
                        Texture = _bannerTexture,
                        LightingEnabled = false
                    };
                }
            }
            
            BuildPitch();
            BuildOpaqueGeometry();
            
            if (_venue == Venue.Bowl)
            {
                _crowdVertices = _crowdVertList.ToArray();
                _crowdIndices = _crowdIndexList.ToArray();
            }
            else
            {
                _fenceVertices = _fenceVertList.ToArray();
                _fenceIndices = _fenceIndexList.ToArray();
                if (_bannerVertList.Count > 0)
                {
                    _bannerVertices = _bannerVertList.ToArray();
                    _bannerIndices = _bannerIndexList.ToArray();
                }
            }
        }
        
        /// <summary>Applies the environment tint to all world effects.</summary>
        public void ApplyEnvironment(MatchEnvironment environment)
        {
            environment.ApplyTo(_colorEffect, false);
            environment.ApplyTo(_pitchEffect, false);
            if (_crowdEffect != null) environment.ApplyTo(_crowdEffect, false);
            if (_fenceEffect != null) environment.ApplyTo(_fenceEffect, false);
            if (_scoreboardEffect != null) environment.ApplyTo(_scoreboardEffect, false);
            if (_bannerEffect != null) environment.ApplyTo(_bannerEffect, false);
        }
        
        /// <summary>Cycles the crowd textures for a shimmering crowd effect (Bowl only).</summary>
        public void Update(float dt)
        {
            if (_venue != Venue.Bowl) return;
            
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
            
            if (_venue == Venue.Bowl)
            {
                // Animated crowd on the stand fronts (tiled horizontally)
                _crowdEffect.View = view;
                _crowdEffect.Projection = projection;
                _crowdEffect.World = Matrix.Identity;
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
            else
            {
                // Scoreboard panel with the rendered name texture
                _scoreboardEffect.View = view;
                _scoreboardEffect.Projection = projection;
                _scoreboardEffect.World = Matrix.Identity;
                device.SamplerStates[0] = SamplerState.LinearClamp;
                foreach (var pass in _scoreboardEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                        _scoreboardVertices, 0, _scoreboardVertices.Length,
                        _scoreboardIndices, 0, _scoreboardIndices.Length / 3);
                }
                
                // Sponsor banners on the fence (Sperchogeia only)
                if (_bannerVertices != null)
                {
                    _bannerEffect.View = view;
                    _bannerEffect.Projection = projection;
                    _bannerEffect.World = Matrix.Identity;
                    foreach (var pass in _bannerEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                            _bannerVertices, 0, _bannerVertices.Length,
                            _bannerIndices, 0, _bannerIndices.Length / 3);
                    }
                }
                
                // See-through chain-link fence (alpha-blended wires, no depth
                // write: transparent texels must not hide players drawn later)
                _fenceEffect.View = view;
                _fenceEffect.Projection = projection;
                _fenceEffect.World = Matrix.Identity;
                device.BlendState = BlendState.AlphaBlend;
                device.DepthStencilState = DepthStencilState.DepthRead;
                device.SamplerStates[0] = SamplerState.LinearWrap;
                foreach (var pass in _fenceEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                        _fenceVertices, 0, _fenceVertices.Length,
                        _fenceIndices, 0, _fenceIndices.Length / 3);
                }
                device.BlendState = BlendState.Opaque;
                device.DepthStencilState = DepthStencilState.Default;
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
            
            // Dark ground plane far beyond the venue
            AddGroundQuad(verts, indices, -150f, -110f, 150f, 110f, -0.02f, new Color(35, 35, 35));
            
            if (_venue == Venue.Bahramis)
            {
                // Dry grass/dirt further out, asphalt road ring against the fence
                AddGroundQuad(verts, indices, -100f, -85f, 100f, 85f, -0.015f, new Color(150, 140, 80));
                Color asphalt = new Color(50, 50, 55);
                AddGroundQuad(verts, indices, -65f, -46f, 65f, -39f, -0.01f, asphalt);  // North (-Z)
                AddGroundQuad(verts, indices, -65f, 39f, 65f, 44f, -0.01f, asphalt);    // South (+Z)
                AddGroundQuad(verts, indices, -64f, -46f, -57.5f, 44f, -0.01f, asphalt); // West (-X)
                AddGroundQuad(verts, indices, 57.5f, -46f, 62f, 44f, -0.01f, asphalt);   // East (+X)
            }
            else if (_venue == Venue.Sperchogeia)
            {
                // Olive-grove floor out to the mountain feet, dirt road on the west
                AddGroundQuad(verts, indices, -180f, -160f, 180f, 130f, -0.018f, new Color(96, 100, 58));
                AddGroundQuad(verts, indices, -100f, -85f, 100f, 85f, -0.015f, new Color(122, 118, 70));
                AddGroundQuad(verts, indices, -78f, -90f, -70f, 90f, -0.012f, new Color(110, 95, 75)); // West dirt road
            }
            
            // Green apron around the pitch
            float apron = 6f;
            AddGroundQuad(verts, indices,
                -_halfLength - apron, -_halfWidth - apron,
                _halfLength + apron, _halfWidth + apron,
                0f, new Color(24, 100, 24));
            
            BuildFieldMarkings(verts, indices);
            BuildGoals(verts, indices);
            BuildCornerFlags(verts, indices);
            
            if (_venue == Venue.Bowl)
            {
                BuildStands(verts, indices);
                BuildAdBoards(verts, indices);
            }
            else if (_venue == Venue.Bahramis)
            {
                BuildBahramisVenue(verts, indices);
            }
            else
            {
                BuildSperchogeiaVenue(verts, indices);
            }
            
            _opaqueVertices = verts.ToArray();
            _opaqueIndices = indices.ToArray();
        }
        
        /// <summary>
        /// Corner flags at the four pitch corners: thin yellow poles with a
        /// small red pennant pointing diagonally away from the pitch.
        /// </summary>
        private void BuildCornerFlags(List<VertexPositionColor> verts, List<int> indices)
        {
            Color poleColor = new Color(240, 200, 40);
            Color flagColor = new Color(200, 30, 30);
            const float poleHeight = 1.5f;
            const float poleHalf = 0.025f;
            const float flagLen = 0.4f;
            const float flagHeight = 0.3f;
            
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    float cx = sx * _halfLength;
                    float cz = sz * _halfWidth;
                    
                    // Pole
                    AddBox(verts, indices,
                        new Vector3(cx - poleHalf, 0f, cz - poleHalf),
                        new Vector3(cx + poleHalf, poleHeight, cz + poleHalf), poleColor);
                    
                    // Pennant: triangle from the pole top, pointing away from center
                    Vector3 poleTop = new Vector3(cx, poleHeight, cz);
                    Vector3 flagDir = Vector3.Normalize(new Vector3(sx, 0f, sz));
                    Vector3 tip = poleTop + flagDir * flagLen + new Vector3(0f, -flagHeight, 0f);
                    Vector3 bottom = poleTop + new Vector3(0f, -flagHeight, 0f);
                    
                    int baseIndex = verts.Count;
                    verts.Add(new VertexPositionColor(poleTop, flagColor));
                    verts.Add(new VertexPositionColor(bottom, flagColor));
                    verts.Add(new VertexPositionColor(tip, flagColor));
                    indices.Add(baseIndex + 0);
                    indices.Add(baseIndex + 1);
                    indices.Add(baseIndex + 2);
                }
            }
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
        
        #region Bahramis municipal venue (Kalamata)
        
        // Fence ring: 5m outside the pitch lines
        private float FenceX => _halfLength + 5f;  // 57.5
        private float FenceZ => _halfWidth + 5f;   // 39
        private const float FenceHeight = 2.1f;
        
        // Main stand on the -Z long side (outside the fence, centered on x=0)
        public const float StandHalfWidth = 15f;    // 30m wide: x -15..15
        public const float StandFrontZ = -40f;      // 1m outside the fence
        public const float StandStepDepth = 0.85f;  // 3 steps, ~2.55m deep total
        public const float StandStepHeight = 0.4f;  // Step tops at 0.4 / 0.8 / 1.2
        
        /// <summary>
        /// Builds the whole Bahramis ground: chain-link fence ring, the small
        /// main stand with yellow bucket seats, the scoreboard arch, a ring of
        /// trees and the urban surroundings. Deterministic (seeded random).
        /// </summary>
        private void BuildBahramisVenue(List<VertexPositionColor> verts, List<int> indices)
        {
            var random = new Random(1979);
            
            BuildFence(verts, indices);
            BuildMainStand(verts, indices, random);
            BuildScoreboard(verts, indices);
            BuildTrees(verts, indices, random);
            BuildHouses(verts, indices, random);
        }
        
        #region Sperchogeia venue (olive grove, Taygetos backdrop)
        
        /// <summary>
        /// ΓΗΠΕΔΟ ΣΠΕΡΧΟΓΕΙΑΣ: a rural ground near Kalamata. Same fence and sign
        /// arch pattern as Bahramis, but no bucket-seat stand — instead a dense
        /// olive grove ring, sponsor banners on the fence, floodlight pylons,
        /// a few red-roofed houses to the NE and the Taygetos ridge behind.
        /// </summary>
        private void BuildSperchogeiaVenue(List<VertexPositionColor> verts, List<int> indices)
        {
            var random = new Random(1934);
            
            BuildFence(verts, indices);
            BuildScoreboard(verts, indices);
            BuildSponsorBanners();
            BuildOliveGrove(verts, indices, random);
            BuildFloodlights(verts, indices);
            BuildMountains(verts, indices, random);
            
            // Red-roofed houses on the NE side
            for (int i = 0; i < 5; i++)
                AddHouse(verts, indices, random,
                    62f + 30f * (float)random.NextDouble(),
                    -52f - 28f * (float)random.NextDouble());
        }
        
        /// <summary>
        /// Sponsor banners hung on the pitch side of the fence: 4 baked designs
        /// in one texture atlas, repeated along the far touchline and the ends.
        /// </summary>
        private void BuildSponsorBanners()
        {
            const float bannerW = 8f, bannerH = 1.2f, bannerY0 = 0.45f;
            float y1 = bannerY0 + bannerH;
            float zFar = -FenceZ + 0.08f;   // pitch side of the far fence
            float zNear = FenceZ - 0.08f;
            float xEast = FenceX - 0.08f;
            float xWest = -FenceX + 0.08f;
            int design = 0;
            
            // Far touchline (-Z): 10 banners, facing the pitch (+Z)
            for (int i = 0; i < 10; i++)
            {
                float x0 = -45f + i * 10f;
                float x1 = x0 + bannerW;
                float u0 = (design % 4) * 0.25f, u1 = u0 + 0.25f;
                AddTexturedQuad(_bannerVertList, _bannerIndexList,
                    new Vector3(x0, bannerY0, zFar), new Vector2(u0, 1f),
                    new Vector3(x1, bannerY0, zFar), new Vector2(u1, 1f),
                    new Vector3(x1, y1, zFar), new Vector2(u1, 0f),
                    new Vector3(x0, y1, zFar), new Vector2(u0, 0f));
                design++;
            }
            
            // Near touchline (+Z): a few banners away from the center camera zone
            for (int i = 0; i < 3; i++)
            {
                float x0 = -44f + i * 10f;
                float x1 = x0 + bannerW;
                float u0 = (design % 4) * 0.25f, u1 = u0 + 0.25f;
                // Facing -Z (toward the pitch), so U runs right-to-left
                AddTexturedQuad(_bannerVertList, _bannerIndexList,
                    new Vector3(x1, bannerY0, zNear), new Vector2(u0, 1f),
                    new Vector3(x0, bannerY0, zNear), new Vector2(u1, 1f),
                    new Vector3(x0, y1, zNear), new Vector2(u1, 0f),
                    new Vector3(x1, y1, zNear), new Vector2(u0, 0f));
                design++;
            }
            
            // East end (+X): 3 banners facing -X (skip the west end: sign arch there)
            for (int i = 0; i < 3; i++)
            {
                float z0 = -12f + i * 11f;
                float z1 = z0 + bannerW;
                float u0 = (design % 4) * 0.25f, u1 = u0 + 0.25f;
                AddTexturedQuad(_bannerVertList, _bannerIndexList,
                    new Vector3(xEast, bannerY0, z1), new Vector2(u0, 1f),
                    new Vector3(xEast, bannerY0, z0), new Vector2(u1, 1f),
                    new Vector3(xEast, y1, z0), new Vector2(u1, 0f),
                    new Vector3(xEast, y1, z1), new Vector2(u0, 0f));
                design++;
            }
            
            // West end (-X), away from the sign arch (arch spans z -6..6)
            for (int i = 0; i < 2; i++)
            {
                float z0 = 12f + i * 11f;
                float z1 = z0 + bannerW;
                float u0 = (design % 4) * 0.25f, u1 = u0 + 0.25f;
                AddTexturedQuad(_bannerVertList, _bannerIndexList,
                    new Vector3(xWest, bannerY0, z0), new Vector2(u0, 1f),
                    new Vector3(xWest, bannerY0, z1), new Vector2(u1, 1f),
                    new Vector3(xWest, y1, z1), new Vector2(u1, 0f),
                    new Vector3(xWest, y1, z0), new Vector2(u0, 0f));
                design++;
            }
        }
        
        /// <summary>
        /// Bakes the 4-design sponsor atlas (2048x256, 512px per design) with
        /// bright backgrounds and dark Greek/Latin text in the game font.
        /// </summary>
        private static Texture2D CreateBannerTexture(GraphicsDevice device, ContentManager content)
        {
            const int width = 2048, height = 256, segW = 512;
            var target = new RenderTarget2D(device, width, height);
            device.SetRenderTarget(target);
            device.Clear(Color.White);
            
            var designs = new[]
            {
                ("NO PASARAN FC", new Color(190, 25, 30), Color.White),
                ("ΚΑΛΑΜΑΤΑ", new Color(25, 60, 160), Color.White),
                ("ΣΠΕΡΧΟΓΕΙΑ", new Color(30, 110, 45), Color.White),
                ("ΕΛΙΑ & ΚΡΑΣΙ", new Color(235, 225, 200), new Color(60, 50, 35)),
            };
            
            var spriteBatch = new SpriteBatch(device);
            spriteBatch.Begin();
            try
            {
                // 1x1 white pixel for the background fills
                var pixel = new Texture2D(device, 1, 1);
                pixel.SetData(new[] { Color.White });
                
                SpriteFont font = null;
                if (content != null)
                {
                    try { font = content.Load<SpriteFont>("Font"); }
                    catch (Exception) { font = null; }
                }
                
                for (int i = 0; i < designs.Length; i++)
                {
                    var (text, bg, fg) = designs[i];
                    spriteBatch.Draw(pixel, new Rectangle(i * segW, 0, segW, height), bg);
                    if (font != null)
                    {
                        Vector2 size = font.MeasureString(text);
                        float scale = Math.Min(segW * 0.85f / size.X, height * 0.55f / size.Y);
                        Vector2 position = new Vector2(i * segW + segW / 2f, height / 2f) - size * scale / 2f;
                        spriteBatch.DrawString(font, text, position, fg,
                            0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    }
                }
            }
            finally
            {
                spriteBatch.End();
                device.SetRenderTarget(null);
            }
            return target;
        }
        
        /// <summary>
        /// Dense olive grove ring in rough rows around the venue (the ground
        /// sits inside cultivated olive fields). Skips the road, sign arch
        /// and floodlight footprints.
        /// </summary>
        private void BuildOliveGrove(List<VertexPositionColor> verts, List<int> indices, Random random)
        {
            for (int row = 0; row < 6; row++)
            {
                float offset = 8f + row * 7.5f;
                for (float t = -95f; t <= 95f; t += 8.5f)
                {
                    float jx = t + (float)(random.NextDouble() * 2.0 - 1.0) * 2.5f;
                    float jz = offset + (float)(random.NextDouble() * 2.0 - 1.0) * 2.5f;
                    
                    // North and South rows
                    TryAddOlive(verts, indices, random, jx, -(FenceZ + jz));
                    TryAddOlive(verts, indices, random, jx, FenceZ + jz);
                    
                    // East and West rows (only within the grove's z span)
                    if (Math.Abs(t) <= 60f)
                    {
                        TryAddOlive(verts, indices, random, FenceX + jz, jx * 0.6f);
                        TryAddOlive(verts, indices, random, -(FenceX + jz), jx * 0.6f);
                    }
                }
            }
        }
        
        private void TryAddOlive(List<VertexPositionColor> verts, List<int> indices, Random random, float x, float z)
        {
            // Keep the west dirt road clear
            if (x > -80f && x < -68f) return;
            // Keep the sign arch surroundings clear
            if (x < -FenceX - 1f && x > -FenceX - 14f && Math.Abs(z) < 10f) return;
            // Keep the floodlight footprints clear
            if (Math.Abs(Math.Abs(x) - (FenceX + 3f)) < 2.5f && Math.Abs(Math.Abs(z) - (FenceZ + 3f)) < 2.5f) return;
            AddOliveTree(verts, indices, random, x, z);
        }
        
        /// <summary>Low-poly olive: short gnarled trunk + 2-4 silvery-green blobs.</summary>
        private static void AddOliveTree(List<VertexPositionColor> verts, List<int> indices, Random random, float x, float z)
        {
            float scale = 0.7f + 0.6f * (float)random.NextDouble();
            Color trunk = new Color(105, 85, 60);
            AddBox(verts, indices,
                new Vector3(x - 0.17f, 0f, z - 0.17f), new Vector3(x + 0.17f, 1.1f * scale + 0.3f, z + 0.17f), trunk);
            
            Color foliage = new Color(100, 118, 72); // Silvery olive green
            int blobs = 2 + random.Next(3);
            for (int i = 0; i < blobs; i++)
            {
                float ox = (float)(random.NextDouble() * 2.0 - 1.0) * 1.0f * scale;
                float oz = (float)(random.NextDouble() * 2.0 - 1.0) * 1.0f * scale;
                float oy = (1.3f + (float)random.NextDouble() * 0.9f) * scale + 0.3f;
                float r = (1.0f + 0.7f * (float)random.NextDouble()) * scale;
                int jitter = random.Next(-12, 13);
                Color c = new Color(
                    Math.Clamp(foliage.R + jitter, 0, 255),
                    Math.Clamp(foliage.G + jitter, 0, 255),
                    Math.Clamp(foliage.B + jitter, 0, 255));
                AddSphere(verts, indices, new Vector3(x + ox, oy, z + oz), r, c, 6, 4);
            }
        }
        
        /// <summary>
        /// Four corner floodlight pylons: gray lattice pole, head frame with
        /// six pale lamp boxes aimed over the pitch.
        /// </summary>
        private void BuildFloodlights(List<VertexPositionColor> verts, List<int> indices)
        {
            Color pole = new Color(115, 118, 122);
            Color lamp = new Color(255, 244, 190);
            const float h = 17f;
            
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    float x = sx * (FenceX + 3f);
                    float z = sz * (FenceZ + 3f);
                    
                    // Pole
                    AddBox(verts, indices,
                        new Vector3(x - 0.2f, 0f, z - 0.2f), new Vector3(x + 0.2f, h, z + 0.2f), pole);
                    // Head frame (2.4m wide, 1.4m tall) facing the pitch
                    float hw = 1.2f;
                    AddBox(verts, indices,
                        new Vector3(x - hw, h, z - 0.1f), new Vector3(x + hw, h + 0.12f, z + 0.1f), pole);
                    AddBox(verts, indices,
                        new Vector3(x - hw, h + 1.4f, z - 0.1f), new Vector3(x + hw, h + 1.52f, z + 0.1f), pole);
                    // Six lamps in two rows of three
                    for (int row = 0; row < 2; row++)
                    {
                        for (int col = 0; col < 3; col++)
                        {
                            float lx = x - 0.8f + col * 0.8f;
                            float ly = h + 0.25f + row * 0.75f;
                            AddBox(verts, indices,
                                new Vector3(lx - 0.22f, ly, z - 0.18f),
                                new Vector3(lx + 0.22f, ly + 0.5f, z + 0.18f), lamp);
                        }
                    }
                }
            }
        }
        
        /// <summary>
        /// Taygetos ridge on the northern horizon: a dark olive foothill ridge
        /// plus taller gray peaks with white snow caps (stacked triangles).
        /// </summary>
        private void BuildMountains(List<VertexPositionColor> verts, List<int> indices, Random random)
        {
            // Foothills: low dark ridge at z ~ -100
            Color foothill = new Color(72, 82, 55);
            float x = -190f;
            while (x < 190f)
            {
                float w = 40f + 30f * (float)random.NextDouble();
                float h = 10f + 8f * (float)random.NextDouble();
                AddTriangle(verts, indices,
                    new Vector3(x, 0f, -100f), new Vector3(x + w, 0f, -100f),
                    new Vector3(x + w * 0.5f, h, -108f), foothill);
                x += w * 0.7f;
            }
            
            // Main ridge: tall gray peaks with snow caps at z ~ -135
            Color rock = new Color(120, 118, 110);
            Color rockDark = new Color(95, 95, 88);
            Color snow = new Color(235, 238, 240);
            x = -200f;
            while (x < 200f)
            {
                float w = 55f + 35f * (float)random.NextDouble();
                float h = 28f + 16f * (float)random.NextDouble();
                float px = x + w * 0.5f;
                Color c = random.Next(2) == 0 ? rock : rockDark;
                AddTriangle(verts, indices,
                    new Vector3(x, 0f, -135f), new Vector3(x + w, 0f, -135f),
                    new Vector3(px, h, -145f), c);
                // Snow cap: smaller triangle on the upper part, slightly in front
                float snowBase = h * 0.62f;
                float halfAtSnow = (w * 0.5f) * (1f - 0.62f);
                AddTriangle(verts, indices,
                    new Vector3(px - halfAtSnow, snowBase, -134.5f),
                    new Vector3(px + halfAtSnow, snowBase, -134.5f),
                    new Vector3(px, h, -144.5f), snow);
                x += w * 0.75f;
            }
        }
        
        #endregion
        
        #region Fence
        
        private void BuildFence(List<VertexPositionColor> verts, List<int> indices)
        {
            Color postColor = new Color(60, 60, 65);
            Color railColor = new Color(40, 80, 160); // Blue top + mid rails
            float fx = FenceX, fz = FenceZ;
            const float postSize = 0.06f;
            const float postSpacing = 4f;
            const float railSize = 0.08f;
            
            // Posts along all four sides (corners shared)
            for (float x = -fx; x <= fx + 0.01f; x += postSpacing)
            {
                float px = Math.Min(x, fx);
                AddFencePost(verts, indices, new Vector3(px, 0f, -fz), postSize, postColor);
                AddFencePost(verts, indices, new Vector3(px, 0f, fz), postSize, postColor);
            }
            for (float z = -fz + postSpacing; z <= fz - postSpacing + 0.01f; z += postSpacing)
            {
                AddFencePost(verts, indices, new Vector3(-fx, 0f, z), postSize, postColor);
                AddFencePost(verts, indices, new Vector3(fx, 0f, z), postSize, postColor);
            }
            
            // Blue top rail (y ~2.02-2.1) and mid rail (y ~0.95-1.03) per side
            foreach (float y0 in new[] { FenceHeight - railSize, 0.95f })
            {
                AddBox(verts, indices,
                    new Vector3(-fx, y0, -fz - railSize / 2f), new Vector3(fx, y0 + railSize, -fz + railSize / 2f), railColor);
                AddBox(verts, indices,
                    new Vector3(-fx, y0, fz - railSize / 2f), new Vector3(fx, y0 + railSize, fz + railSize / 2f), railColor);
                AddBox(verts, indices,
                    new Vector3(-fx - railSize / 2f, y0, -fz), new Vector3(-fx + railSize / 2f, y0 + railSize, fz), railColor);
                AddBox(verts, indices,
                    new Vector3(fx - railSize / 2f, y0, -fz), new Vector3(fx + railSize / 2f, y0 + railSize, fz), railColor);
            }
            
            // Chain-link panels into the alpha-blended textured list.
            // One 64x64 texture tile per ~1.5m of fence, full height per tile.
            const float tileMeters = 1.5f;
            float uX = fx * 2f / tileMeters;
            float uZ = fz * 2f / tileMeters;
            const float y0p = 0.02f, y1p = FenceHeight - 0.1f;
            
            AddTexturedQuad(_fenceVertList, _fenceIndexList,
                new Vector3(-fx, y0p, -fz), new Vector2(0f, 1f),
                new Vector3(fx, y0p, -fz), new Vector2(uX, 1f),
                new Vector3(fx, y1p, -fz), new Vector2(uX, 0f),
                new Vector3(-fx, y1p, -fz), new Vector2(0f, 0f));
            AddTexturedQuad(_fenceVertList, _fenceIndexList,
                new Vector3(-fx, y0p, fz), new Vector2(0f, 1f),
                new Vector3(fx, y0p, fz), new Vector2(uX, 1f),
                new Vector3(fx, y1p, fz), new Vector2(uX, 0f),
                new Vector3(-fx, y1p, fz), new Vector2(0f, 0f));
            AddTexturedQuad(_fenceVertList, _fenceIndexList,
                new Vector3(-fx, y0p, -fz), new Vector2(0f, 1f),
                new Vector3(-fx, y0p, fz), new Vector2(uZ, 1f),
                new Vector3(-fx, y1p, fz), new Vector2(uZ, 0f),
                new Vector3(-fx, y1p, -fz), new Vector2(0f, 0f));
            AddTexturedQuad(_fenceVertList, _fenceIndexList,
                new Vector3(fx, y0p, -fz), new Vector2(0f, 1f),
                new Vector3(fx, y0p, fz), new Vector2(uZ, 1f),
                new Vector3(fx, y1p, fz), new Vector2(uZ, 0f),
                new Vector3(fx, y1p, -fz), new Vector2(0f, 0f));
        }
        
        private void AddFencePost(List<VertexPositionColor> verts, List<int> indices,
            Vector3 basePos, float size, Color color)
        {
            float h = size / 2f;
            AddBox(verts, indices,
                new Vector3(basePos.X - h, 0f, basePos.Z - h),
                new Vector3(basePos.X + h, FenceHeight, basePos.Z + h), color);
        }
        
        /// <summary>
        /// Generates the 64x64 chain-link texture: two diagonal wire grids
        /// (semi-transparent gray) crossing into a diamond pattern, gaps fully
        /// transparent. Tiled horizontally along the fence panels.
        /// </summary>
        private static Texture2D CreateChainLinkTexture(GraphicsDevice device)
        {
            const int size = 64;
            const int cell = 12;   // Diamond cell size in texels
            const int wire = 2;    // Wire thickness in texels
            var data = new Color[size * size];
            Color wireColor = new Color(150, 150, 155, 128); // ~0.5 alpha wires
            
            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int d1 = (x + y) % cell;
                    int d2 = ((x - y) % cell + cell) % cell;
                    data[y * size + x] = (d1 < wire || d2 < wire) ? wireColor : Color.Transparent;
                }
            }
            
            var texture = new Texture2D(device, size, size);
            texture.SetData(data);
            return texture;
        }
        
        #endregion
        
        #region Main stand (yellow bucket seats)
        
        private void BuildMainStand(List<VertexPositionColor> verts, List<int> indices, Random random)
        {
            Color concrete = new Color(170, 168, 160);
            Color wallColor = new Color(198, 194, 184);  // Weathered grayish-white
            Color seatYellow = new Color(240, 200, 30);
            Color seatEmpty = new Color(90, 85, 70);     // Weathered / missing seat
            Color railColor = new Color(40, 40, 45);
            
            float hw = StandHalfWidth;
            
            // 3 stepped concrete tiers (front step nearest the pitch)
            for (int step = 0; step < 3; step++)
            {
                float zFront = StandFrontZ - step * StandStepDepth;
                float zBack = zFront - StandStepDepth;
                float top = (step + 1) * StandStepHeight;
                AddBox(verts, indices,
                    new Vector3(-hw, 0f, zBack), new Vector3(hw, top, zFront), concrete);
            }
            
            // 3 rows of yellow bucket seats, one per step (~0.6m spacing)
            const float seatW = 0.45f, seatH = 0.45f, seatD = 0.4f;
            const float seatSpacing = 0.6f;
            for (int row = 0; row < 3; row++)
            {
                float baseY = (row + 1) * StandStepHeight;
                float zCenter = StandFrontZ - row * StandStepDepth - 0.45f;
                for (float x = -hw + 0.4f; x + seatW <= hw - 0.4f + 0.01f; x += seatSpacing)
                {
                    Color seatColor = random.Next(100) < 8 ? seatEmpty : seatYellow;
                    // Seat shell
                    AddBox(verts, indices,
                        new Vector3(x, baseY, zCenter - seatD / 2f),
                        new Vector3(x + seatW, baseY + seatH, zCenter + seatD / 2f), seatColor);
                    // Thin backrest on the rear (away from the pitch)
                    AddBox(verts, indices,
                        new Vector3(x, baseY + seatH, zCenter - seatD / 2f),
                        new Vector3(x + seatW, baseY + seatH + 0.3f, zCenter - seatD / 2f + 0.08f), seatColor);
                }
            }
            
            // Low weathered side walls at both ends of the stand
            float wallBackZ = StandFrontZ - 3f * StandStepDepth - 0.3f;
            AddBox(verts, indices,
                new Vector3(-hw - 0.3f, 0f, wallBackZ), new Vector3(-hw, 1.7f, StandFrontZ), wallColor);
            AddBox(verts, indices,
                new Vector3(hw, 0f, wallBackZ), new Vector3(hw + 0.3f, 1.7f, StandFrontZ), wallColor);
            
            // Graffiti smudge on the inner face of the west side wall
            Color graffiti = new Color(35, 30, 45);
            AddQuad(verts, indices,
                new Vector3(-hw + 0.01f, 0.5f, -42.2f), new Vector3(-hw + 0.01f, 0.55f, -41.1f),
                new Vector3(-hw + 0.01f, 1.15f, -41.0f), new Vector3(-hw + 0.01f, 1.05f, -42.1f), graffiti);
            AddQuad(verts, indices,
                new Vector3(-hw + 0.01f, 0.6f, -41.6f), new Vector3(-hw + 0.01f, 0.65f, -41.2f),
                new Vector3(-hw + 0.01f, 1.3f, -41.15f), new Vector3(-hw + 0.01f, 1.2f, -41.55f),
                new Color(60, 40, 80));
            
            // Dark metal railing behind the last row
            float railZ = StandFrontZ - 3f * StandStepDepth - 0.15f;
            for (float x = -hw; x <= hw + 0.01f; x += 2.5f)
            {
                float px = Math.Min(x, hw);
                AddBox(verts, indices,
                    new Vector3(px - 0.025f, 0f, railZ - 0.025f),
                    new Vector3(px + 0.025f, 1.1f, railZ + 0.025f), railColor);
            }
            foreach (float y0 in new[] { 0.55f, 1.05f })
            {
                AddBox(verts, indices,
                    new Vector3(-hw, y0, railZ - 0.03f), new Vector3(hw, y0 + 0.05f, railZ + 0.03f), railColor);
            }
        }
        
        #endregion
        
        #region Scoreboard arch
        
        /// <summary>
        /// White scoreboard arch behind the -X goal: two white posts and a
        /// white panel with a shallow arched top carrying the navy Greek name.
        /// The panel lives in its own textured draw (text baked at load).
        /// </summary>
        private void BuildScoreboard(List<VertexPositionColor> verts, List<int> indices)
        {
            float postX = -_halfLength - 8f;   // ~8m past the goal line: -60.5
            float panelX = postX + 0.3f;       // Panel on the pitch side of the posts
            Color postColor = new Color(240, 240, 240);
            
            // Two white posts (0.3m sq, 6m tall)
            AddBox(verts, indices,
                new Vector3(postX - 0.15f, 0f, -6.15f), new Vector3(postX + 0.15f, 6f, -5.85f), postColor);
            AddBox(verts, indices,
                new Vector3(postX - 0.15f, 0f, 5.85f), new Vector3(postX + 0.15f, 6f, 6.15f), postColor);
            
            // Panel: 12m wide along Z, base y=2.6, arched top in 5 segments
            // (peak 0.3m above the edges). UVs map the whole name texture.
            float[] segZ = { -6f, -3.6f, -1.2f, 1.2f, 3.6f, 6f };
            float[] segTop = { 5.3f, 5.5f, 5.6f, 5.5f, 5.3f };
            const float panelBase = 2.6f;
            const float panelHalfWidth = 6f;
            
            var vertList = new List<VertexPositionTexture>();
            var indexList = new List<int>();
            for (int i = 0; i < segTop.Length; i++)
            {
                float z0 = segZ[i], z1 = segZ[i + 1];
                // U mirrored: the panel faces the pitch (+X), so plain mapping
                // would show the text reversed
                float u0 = 1f - (z0 + panelHalfWidth) / (panelHalfWidth * 2f);
                float u1 = 1f - (z1 + panelHalfWidth) / (panelHalfWidth * 2f);
                AddTexturedQuad(vertList, indexList,
                    new Vector3(panelX, panelBase, z0), new Vector2(u0, 1f),
                    new Vector3(panelX, panelBase, z1), new Vector2(u1, 1f),
                    new Vector3(panelX, segTop[i], z1), new Vector2(u1, 0f),
                    new Vector3(panelX, segTop[i], z0), new Vector2(u0, 0f));
            }
            _scoreboardVertices = vertList.ToArray();
            _scoreboardIndices = indexList.ToArray();
        }
        
        /// <summary>
        /// Renders the scoreboard name texture at load: white background, dark
        /// navy venue name in the game font (has Greek glyphs).
        /// Falls back to a blank white panel if the font can't load.
        /// </summary>
        private static Texture2D CreateScoreboardTexture(GraphicsDevice device, ContentManager content, string text)
        {
            const int width = 1024;
            const int height = 256;
            var target = new RenderTarget2D(device, width, height);
            device.SetRenderTarget(target);
            device.Clear(Color.White);
            try
            {
                if (content != null)
                {
                    var font = content.Load<SpriteFont>("Font");
                    Vector2 size = font.MeasureString(text);
                    float scale = Math.Min(width * 0.85f / size.X, height * 0.55f / size.Y);
                    Vector2 position = new Vector2(width / 2f, height / 2f) - size * scale / 2f;
                    var spriteBatch = new SpriteBatch(device);
                    spriteBatch.Begin();
                    spriteBatch.DrawString(font, text, position, new Color(20, 30, 80),
                        0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
                    spriteBatch.End();
                }
            }
            catch (Exception)
            {
                // Blank white panel fallback
            }
            finally
            {
                device.SetRenderTarget(null);
            }
            return target;
        }
        
        #endregion
        
        #region Trees and houses
        
        private void BuildTrees(List<VertexPositionColor> verts, List<int> indices, Random random)
        {
            int placed = 0;
            int attempts = 0;
            while (placed < 30 && attempts < 200)
            {
                attempts++;
                // Ring around the venue, 8-25m outside the fence
                float angle = MathHelper.TwoPi * (float)random.NextDouble();
                float dist = 8f + 17f * (float)random.NextDouble();
                float x = (float)Math.Cos(angle) * (FenceX + dist);
                float z = (float)Math.Sin(angle) * (FenceZ + dist);
                if (KeepTree(x, z))
                {
                    AddTree(verts, indices, random, x, z);
                    placed++;
                }
            }
            // Denser planting on the +X and +Z sides
            for (int i = 0; i < 8; i++)
            {
                float x = FenceX + 6f + 15f * (float)random.NextDouble();
                float z = (float)(random.NextDouble() * 2.0 - 1.0) * FenceZ;
                AddTree(verts, indices, random, x, z);
            }
            for (int i = 0; i < 6; i++)
            {
                float x = (float)(random.NextDouble() * 2.0 - 1.0) * FenceX;
                float z = FenceZ + 6f + 15f * (float)random.NextDouble();
                AddTree(verts, indices, random, x, z);
            }
        }
        
        /// <summary>Skips spots blocked by the stand, the scoreboard or the fence itself.</summary>
        private bool KeepTree(float x, float z)
        {
            if (Math.Abs(x) < FenceX + 1f && Math.Abs(z) < FenceZ + 1f) return false;
            if (Math.Abs(x) < StandHalfWidth + 4f && z < -FenceZ && z > StandFrontZ - 8f) return false; // Stand
            if (x < -FenceX - 1f && x > -FenceX - 12f && Math.Abs(z) < 10f) return false;              // Scoreboard
            return true;
        }
        
        private void AddTree(List<VertexPositionColor> verts, List<int> indices, Random random, float x, float z)
        {
            float scale = 0.8f + 0.5f * (float)random.NextDouble();
            Color trunk = new Color(90, 60, 40);
            
            if (random.Next(2) == 0)
            {
                // Cypress: thin trunk + tall dark-green stacked foliage
                AddBox(verts, indices,
                    new Vector3(x - 0.12f, 0f, z - 0.12f), new Vector3(x + 0.12f, 1.2f, z + 0.12f), trunk);
                Color foliage = new Color(30, 70, 35);
                float y = 0.8f;
                float w = 0.8f * scale;
                for (int i = 0; i < 3; i++)
                {
                    float h = (1.6f - i * 0.3f) * scale;
                    AddBox(verts, indices,
                        new Vector3(x - w, y, z - w), new Vector3(x + w, y + h, z + w), foliage);
                    y += h * 0.85f;
                    w *= 0.65f;
                }
            }
            else
            {
                // Olive: short trunk + 2-3 mid-green blobby low-poly spheres
                AddBox(verts, indices,
                    new Vector3(x - 0.17f, 0f, z - 0.17f), new Vector3(x + 0.17f, 1.1f, z + 0.17f), trunk);
                Color foliage = new Color(85, 110, 60);
                int blobs = 2 + random.Next(2);
                for (int i = 0; i < blobs; i++)
                {
                    float ox = (float)(random.NextDouble() * 2.0 - 1.0) * 0.9f * scale;
                    float oz = (float)(random.NextDouble() * 2.0 - 1.0) * 0.9f * scale;
                    float oy = 1.4f * scale + (float)random.NextDouble() * 0.8f;
                    float r = (1.1f + 0.6f * (float)random.NextDouble()) * scale;
                    AddSphere(verts, indices, new Vector3(x + ox, oy, z + oz), r, foliage, 6, 4);
                }
            }
        }
        
        private void BuildHouses(List<VertexPositionColor> verts, List<int> indices, Random random)
        {
            // Houses beyond the trees on the -Z, -X and +X sides
            for (int i = 0; i < 8; i++)
                AddHouse(verts, indices, random,
                    -70f + 140f * (float)random.NextDouble(),
                    -55f - 25f * (float)random.NextDouble());
            for (int i = 0; i < 6; i++)
                AddHouse(verts, indices, random,
                    -75f - 20f * (float)random.NextDouble(),
                    -50f + 100f * (float)random.NextDouble());
            for (int i = 0; i < 6; i++)
                AddHouse(verts, indices, random,
                    70f + 25f * (float)random.NextDouble(),
                    -50f + 100f * (float)random.NextDouble());
            
            // A couple of bigger flat-roof university blocks with window rows
            AddUniversityBlock(verts, indices, 40f, -70f, 16f, 10f, 9f);
            AddUniversityBlock(verts, indices, 85f, -20f, 14f, 12f, 11f);
        }
        
        private void AddHouse(List<VertexPositionColor> verts, List<int> indices, Random random, float x, float z)
        {
            float w = 6f + 4f * (float)random.NextDouble();
            float d = 5f + 3f * (float)random.NextDouble();
            float h = 3f + 3f * (float)random.NextDouble();
            Color body = random.Next(2) == 0
                ? new Color(235, 230, 215)
                : new Color(215, 200, 175);
            int roofJitter = random.Next(-15, 16);
            Color roof = new Color(
                Math.Clamp(181 + roofJitter, 0, 255),
                Math.Clamp(83 + roofJitter, 0, 255),
                Math.Clamp(60 + roofJitter, 0, 255));
            
            AddBox(verts, indices,
                new Vector3(x - w / 2f, 0f, z - d / 2f), new Vector3(x + w / 2f, h, z + d / 2f), body);
            
            // Terracotta pyramid roof (4 triangles to a central apex)
            var apex = new Vector3(x, h + Math.Min(w, d) * 0.35f, z);
            var c0 = new Vector3(x - w / 2f, h, z - d / 2f);
            var c1 = new Vector3(x + w / 2f, h, z - d / 2f);
            var c2 = new Vector3(x + w / 2f, h, z + d / 2f);
            var c3 = new Vector3(x - w / 2f, h, z + d / 2f);
            AddTriangle(verts, indices, c0, c1, apex, roof);
            AddTriangle(verts, indices, c1, c2, apex, roof);
            AddTriangle(verts, indices, c2, c3, apex, roof);
            AddTriangle(verts, indices, c3, c0, apex, roof);
        }
        
        private void AddUniversityBlock(List<VertexPositionColor> verts, List<int> indices,
            float x, float z, float w, float d, float h)
        {
            Color body = new Color(205, 200, 190);
            Color windowColor = new Color(60, 70, 85);
            
            AddBox(verts, indices,
                new Vector3(x - w / 2f, 0f, z - d / 2f), new Vector3(x + w / 2f, h, z + d / 2f), body);
            
            // Window rows suggested by darker inset quads on both long faces
            const int rows = 3;
            const int cols = 6;
            foreach (float faceZ in new[] { z - d / 2f - 0.02f, z + d / 2f + 0.02f })
            {
                for (int r = 0; r < rows; r++)
                {
                    float y0 = 1.2f + r * (h - 2.2f) / rows;
                    for (int c = 0; c < cols; c++)
                    {
                        float x0 = x - w / 2f + 1f + c * (w - 2f) / cols;
                        AddQuad(verts, indices,
                            new Vector3(x0, y0, faceZ),
                            new Vector3(x0 + 1.2f, y0, faceZ),
                            new Vector3(x0 + 1.2f, y0 + 1.4f, faceZ),
                            new Vector3(x0, y0 + 1.4f, faceZ), windowColor);
                    }
                }
            }
        }
        
        #endregion
        
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
        
        /// <summary>Adds a single triangle (used for pyramid roofs).</summary>
        private static void AddTriangle(List<VertexPositionColor> verts, List<int> indices,
            Vector3 a, Vector3 b, Vector3 c, Color color)
        {
            int baseIndex = verts.Count;
            verts.Add(new VertexPositionColor(a, color));
            verts.Add(new VertexPositionColor(b, color));
            verts.Add(new VertexPositionColor(c, color));
            indices.Add(baseIndex + 0);
            indices.Add(baseIndex + 1);
            indices.Add(baseIndex + 2);
        }
        
        /// <summary>Adds a low-poly UV sphere (used for blobby tree foliage).</summary>
        private static void AddSphere(List<VertexPositionColor> verts, List<int> indices,
            Vector3 center, float radius, Color color, int lonSegments, int latSegments)
        {
            int baseIndex = verts.Count;
            
            // Rings from bottom pole to top pole (poles duplicated per segment
            // for simplicity - a few extra vertices don't matter here)
            for (int lat = 0; lat <= latSegments; lat++)
            {
                float theta = MathHelper.Pi * lat / latSegments;
                float y = (float)Math.Cos(theta) * radius;
                float ringRadius = (float)Math.Sin(theta) * radius;
                for (int lon = 0; lon <= lonSegments; lon++)
                {
                    float phi = MathHelper.TwoPi * lon / lonSegments;
                    verts.Add(new VertexPositionColor(
                        center + new Vector3(
                            (float)Math.Cos(phi) * ringRadius, y, (float)Math.Sin(phi) * ringRadius),
                        color));
                }
            }
            
            int ringStride = lonSegments + 1;
            for (int lat = 0; lat < latSegments; lat++)
            {
                for (int lon = 0; lon < lonSegments; lon++)
                {
                    int i0 = baseIndex + lat * ringStride + lon;
                    int i1 = i0 + 1;
                    int i2 = i0 + ringStride;
                    int i3 = i2 + 1;
                    indices.Add(i0);
                    indices.Add(i2);
                    indices.Add(i1);
                    indices.Add(i1);
                    indices.Add(i2);
                    indices.Add(i3);
                }
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
