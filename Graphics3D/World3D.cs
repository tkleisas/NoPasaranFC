using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>Match venue: the generic bowl stadium, the Bahramis municipal ground, the Sperchogeia ground, or the seaside Sfageia ground.</summary>
    public enum Venue { Bowl, Bahramis, Sperchogeia, Sfageia, Kerasoulia, Soulinari, Street, Prosfygika }
    
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

        // Soft ground shadows under trees (alpha-blended pass)
        private BasicEffect _shadowEffect;
        private VertexPositionColor[] _shadowVertices;
        private int[] _shadowIndices;
        private readonly List<VertexPositionColor> _shadowVertList = new List<VertexPositionColor>();
        private readonly List<int> _shadowIndexList = new List<int>();
        private const float ShadowY = 0.03f;

        // Prosfygika: aged plaster facade atlas (textured front walls)
        private BasicEffect _facadeEffect;
        private Texture2D _facadeTexture;
        private readonly List<VertexPositionTexture> _facadeVertList = new List<VertexPositionTexture>();
        private readonly List<int> _facadeIndexList = new List<int>();
        private VertexPositionTexture[] _facadeVertices;
        private int[] _facadeIndices;

        // Static baked key-light for the low-poly world (matches the day sun).
        private static readonly Vector3 BakeSunDir =
            Vector3.Normalize(new Vector3(0.35f, 0.8f, -0.25f));
        
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
        
        // Fan-made bed-sheet banners on the fence (FREE PALESTINE etc.), all fence venues
        private BasicEffect _fanBannerEffect;
        private Texture2D _fanBannerTexture;
        private readonly List<VertexPositionTexture> _fanBannerVertList = new List<VertexPositionTexture>();
        private readonly List<int> _fanBannerIndexList = new List<int>();
        private VertexPositionTexture[] _fanBannerVertices;
        private int[] _fanBannerIndices;
        
        // Field dimensions in meters (same constants as MatchScreen.DrawFieldMarkings)
        private readonly float _halfLength = WorldUnits.PitchLengthMeters / 2f;   // 52.5
        private readonly float _halfWidth = WorldUnits.PitchWidthMeters / 2f;     // 34
        private const float LineY = 0.02f;
        private const float LineWidth = 0.12f; // FIFA ~12cm lines
        
        // Procedural/baked textures are invariant per venue - cache them for the
        // process lifetime instead of rebuilding (RenderTarget2D text bakes and
        // CPU texture loops stall match entry, especially on Android GLES).
        // Note: the random fan-banner selection is now made once per process.
        private static readonly Dictionary<string, Texture2D> _textureCache =
            new Dictionary<string, Texture2D>();

        private static Texture2D CachedTexture(string key, Func<Texture2D> create)
        {
            if (_textureCache.TryGetValue(key, out var cached))
                return cached;
            var texture = create();
            _textureCache[key] = texture;
            return texture;
        }

        public World3D(GraphicsDevice device, ContentManager content = null, Venue venue = Venue.Bahramis)
        {
            _venue = venue;
            if (venue == Venue.Kerasoulia)
            {
                _fenceHeight = 3.4f;
                _goalFrameColor = new Color(30, 60, 200);
            }
            else if (venue == Venue.Street || venue == Venue.Prosfygika)
            {
                _fenceHeight = venue == Venue.Street ? 4.2f : 3.2f;
                _goalFrameColor = new Color(225, 225, 225);
            }

            _colorEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };

            _shadowEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };

            _grassTexture = CachedTexture($"grass:{venue}", () => CreateGrassTexture(device));
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
                _fenceTexture = CachedTexture("chainlink", () => CreateChainLinkTexture(device));
                _fenceEffect = new BasicEffect(device)
                {
                    VertexColorEnabled = false,
                    TextureEnabled = true,
                    Texture = _fenceTexture,
                    LightingEnabled = false
                };
                
                string signText = _venue switch
                {
                    Venue.Sperchogeia => "ΓΗΠΕΔΟ ΣΠΕΡΧΟΓΕΙΑΣ",
                    Venue.Sfageia => "ΓΗΠΕΔΟ ΣΦΑΓΕΙΩΝ",
                    Venue.Kerasoulia => "ΣΤΑΔΙΟ ΚΕΡΑΣΟΥΛΙΑΣ",
                    Venue.Soulinari => "ΓΗΠΕΔΟ ΣΟΥΛΗΝΑΡΙΟΥ",
                    Venue.Prosfygika => "ΣΩΣΤΕ ΤΑ ΠΡΟΣΦΥΓΙΚΑ",
                    _ => "ΠΑΝΑΓΙΩΤΗΣ ΜΠΑΧΡΑΜΗΣ",
                };
                _scoreboardTexture = CachedTexture($"scoreboard:{signText}", () => CreateScoreboardTexture(device, content, signText));
                _scoreboardEffect = new BasicEffect(device)
                {
                    VertexColorEnabled = false,
                    TextureEnabled = true,
                    Texture = _scoreboardTexture,
                    LightingEnabled = false
                };
                
                if (_venue == Venue.Sperchogeia || _venue == Venue.Sfageia ||
                    _venue == Venue.Kerasoulia || _venue == Venue.Soulinari)
                {
                    _bannerTexture = CachedTexture($"sponsor:{venue}", () => CreateBannerTexture(device, content));
                    _bannerEffect = new BasicEffect(device)
                    {
                        VertexColorEnabled = false,
                        TextureEnabled = true,
                        Texture = _bannerTexture,
                        LightingEnabled = false
                    };
                }
                
                // Fan-made banners (all grounds with a fence)
                _fanBannerTexture = CachedTexture($"fanbanners:{venue}", () => CreateFanBannerTexture(device, content));
                _fanBannerEffect = new BasicEffect(device)
                {
                    VertexColorEnabled = false,
                    TextureEnabled = true,
                    Texture = _fanBannerTexture,
                    LightingEnabled = false
                };

                if (_venue == Venue.Prosfygika)
                {
                    _facadeTexture = CachedTexture("prosfygika_facade", () => CreateProsfygikaFacadeAtlas(device));
                    _facadeEffect = new BasicEffect(device)
                    {
                        VertexColorEnabled = false,
                        TextureEnabled = true,
                        Texture = _facadeTexture,
                        LightingEnabled = false
                    };
                }
            }
            
            BuildPitch();
            BuildOpaqueGeometry();

            _shadowVertices = _shadowVertList.ToArray();
            _shadowIndices = _shadowIndexList.ToArray();
            _facadeVertices = _facadeVertList.ToArray();
            _facadeIndices = _facadeIndexList.ToArray();
            
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
                if (_fanBannerVertList.Count > 0)
                {
                    _fanBannerVertices = _fanBannerVertList.ToArray();
                    _fanBannerIndices = _fanBannerIndexList.ToArray();
                }
            }
        }
        
        /// <summary>Applies the environment tint to all world effects.</summary>
        public void ApplyEnvironment(MatchEnvironment environment)
        {
            environment.ApplyTo(_colorEffect, false);
            environment.ApplyTo(_pitchEffect, false);
            environment.ApplyTo(_shadowEffect, false);
            if (_facadeEffect != null) environment.ApplyTo(_facadeEffect, false);
            if (_crowdEffect != null) environment.ApplyTo(_crowdEffect, false);
            if (_fenceEffect != null) environment.ApplyTo(_fenceEffect, false);
            if (_scoreboardEffect != null) environment.ApplyTo(_scoreboardEffect, false);
            if (_bannerEffect != null) environment.ApplyTo(_bannerEffect, false);
            if (_fanBannerEffect != null) environment.ApplyTo(_fanBannerEffect, false);
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

            // Aged plaster facades on the Prosfygika blocks (behind windows/balconies)
            if (_facadeVertices != null && _facadeVertices.Length > 0)
            {
                _facadeEffect.View = view;
                _facadeEffect.Projection = projection;
                _facadeEffect.World = Matrix.Identity;
                device.BlendState = BlendState.Opaque;
                device.DepthStencilState = DepthStencilState.Default;
                device.RasterizerState = RasterizerState.CullNone;
                device.SamplerStates[0] = SamplerState.LinearClamp;
                foreach (var pass in _facadeEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                        _facadeVertices, 0, _facadeVertices.Length,
                        _facadeIndices, 0, _facadeIndices.Length / 3);
                }
            }

            // Soft tree shadows on the ground/apron. Drawn after the opaque pass
            // with depth-read/no-write: the shadow is slightly above the ground,
            // so it blends over it, while trunks/stands in front occlude it.
            if (_shadowVertices != null && _shadowVertices.Length > 0)
            {
                _shadowEffect.View = view;
                _shadowEffect.Projection = projection;
                _shadowEffect.World = Matrix.Identity;
                device.BlendState = BlendState.AlphaBlend;
                device.DepthStencilState = DepthStencilState.DepthRead;
                device.RasterizerState = RasterizerState.CullNone;
                foreach (var pass in _shadowEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                        _shadowVertices, 0, _shadowVertices.Length,
                        _shadowIndices, 0, _shadowIndices.Length / 3);
                }
                device.BlendState = BlendState.Opaque;
                device.DepthStencilState = DepthStencilState.Default;
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
                // Scoreboard panel with the rendered name texture (street has none)
                if (_scoreboardVertices != null)
                {
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
                
                // Fan-made banners on the fence (all fence venues)
                if (_fanBannerVertices != null)
                {
                    _fanBannerEffect.View = view;
                    _fanBannerEffect.Projection = projection;
                    _fanBannerEffect.World = Matrix.Identity;
                    foreach (var pass in _fanBannerEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                            _fanBannerVertices, 0, _fanBannerVertices.Length,
                            _fanBannerIndices, 0, _fanBannerIndices.Length / 3);
                    }
                }
                
                // See-through chain-link fence (alpha-blended wires, no depth
                // write: transparent texels must not hide players drawn later).
                // Prosfygika is an open court and has no fence.
                if (_fenceEffect != null && _fenceVertices != null && _fenceVertices.Length > 0)
                {
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
        }
        
        #region Pitch + grass texture
        
        private Texture2D CreateGrassTexture(GraphicsDevice device)
        {
            if (_venue == Venue.Prosfygika)
                return CreateDirtCourtTexture(device);
            if (IsStreetCourt)
                return CreateStreetCourtTexture(device);

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
                // (layers spaced >= 5mm apart in Y to avoid z-fighting lines)
                AddGroundQuad(verts, indices, -180f, -160f, 180f, 130f, -0.014f, new Color(96, 100, 58));
                AddGroundQuad(verts, indices, -100f, -85f, 100f, 85f, -0.009f, new Color(122, 118, 70));
                AddGroundQuad(verts, indices, -78f, -90f, -70f, 90f, -0.005f, new Color(110, 95, 75)); // West dirt road
            }
            else if (_venue == Venue.Kerasoulia)
            {
                // Mountain forest floor (dark green-brown), red running track
                // strip along the far touchline, then the curved asphalt road
                // (layers spaced >= 2mm apart in Y to avoid z-fighting lines)
                AddGroundQuad(verts, indices, -180f, -140f, 180f, 140f, -0.014f, new Color(70, 80, 55));
                AddGroundQuad(verts, indices, -100f, -85f, 100f, 85f, -0.009f, new Color(95, 100, 62));
                AddGroundQuad(verts, indices, -70f, -44f, 70f, -39f, -0.005f, new Color(160, 60, 45)); // red track
                Color kerasouliaAsphalt = new Color(55, 55, 60);
                AddGroundQuad(verts, indices, -75f, -52f, 75f, -44f, -0.003f, kerasouliaAsphalt);    // road
                AddGroundQuad(verts, indices, -75f, -44.6f, 75f, -44.2f, -0.001f, new Color(200, 200, 200)); // road edge line
            }
            else if (_venue == Venue.Soulinari)
            {
                // Dry village ground with a dirt road ring against the fence
                // (layers spaced >= 4mm apart in Y to avoid z-fighting lines)
                AddGroundQuad(verts, indices, -120f, -100f, 120f, 100f, -0.014f, new Color(120, 115, 70));
                AddGroundQuad(verts, indices, -100f, -85f, 100f, 85f, -0.009f, new Color(150, 140, 80));
                Color dirt = new Color(115, 100, 80);
                AddGroundQuad(verts, indices, -65f, -46f, 65f, -41f, -0.005f, dirt);  // North (-Z)
                AddGroundQuad(verts, indices, -65f, 41f, 65f, 46f, -0.005f, dirt);    // South (+Z)
                AddGroundQuad(verts, indices, -64f, -46f, -59.5f, 46f, -0.005f, dirt); // West (-X)
                AddGroundQuad(verts, indices, 59.5f, -46f, 64f, 46f, -0.005f, dirt);   // East (+X)
            }
            else if (_venue == Venue.Prosfygika)
            {
                // Packed dirt/gravel neighborhood lot and road
                AddGroundQuad(verts, indices, -70f, -50f, 70f, 50f, -0.014f, new Color(120, 104, 82));
                AddGroundQuad(verts, indices, -50f, -36f, 50f, 36f, -0.010f, new Color(136, 118, 94));
                AddGroundQuad(verts, indices, -100f, -70f, 100f, 70f, -0.018f, new Color(92, 82, 68));
            }
            else if (_venue == Venue.Street)
            {
                // Asphalt lot around the cage, a sidewalk band and a darker road beyond
                AddGroundQuad(verts, indices, -70f, -50f, 70f, 50f, -0.014f, new Color(58, 58, 64));
                AddGroundQuad(verts, indices, -50f, -36f, 50f, 36f, -0.010f, new Color(70, 70, 76));
                AddGroundQuad(verts, indices, -100f, -70f, 100f, 70f, -0.018f, new Color(45, 45, 50));
            }
            else if (_venue == Venue.Sfageia)
            {
                // Inland side: dry grass/dirt like the other grounds
                AddGroundQuad(verts, indices, -100f, -85f, 100f, 85f, -0.015f, new Color(150, 140, 80));
                // Road ring against the fence on the inland sides
                Color asphalt = new Color(50, 50, 55);
                AddGroundQuad(verts, indices, -65f, 39f, 65f, 44f, -0.01f, asphalt);    // South (+Z)
                AddGroundQuad(verts, indices, -64f, -46f, -57.5f, 44f, -0.01f, asphalt); // West (-X)
                AddGroundQuad(verts, indices, 57.5f, -46f, 62f, 44f, -0.01f, asphalt);   // East (+X)
                // Seaside promenade behind the far (-Z) fence, then the pebble beach
                AddGroundQuad(verts, indices, -120f, -46f, 120f, -41f, -0.008f, new Color(120, 115, 110));
                // Beach shelves down to the water (pebble gray-tan)
                AddGroundQuad(verts, indices, -300f, -70f, 300f, -46f, -0.35f, new Color(168, 158, 140));
                AddGroundQuad(verts, indices, -300f, -84f, 300f, -70f, -0.9f, new Color(160, 150, 132));
                // The sea (deep blue-green) with two foam lines near the shore
                AddGroundQuad(verts, indices, -400f, -400f, 400f, -84f, -1.4f, new Color(18, 78, 105));
                AddGroundQuad(verts, indices, -300f, -88f, 300f, -84.6f, -1.1f, new Color(120, 175, 185));
                AddGroundQuad(verts, indices, -300f, -95f, 300f, -91f, -1.3f, new Color(90, 150, 165));
            }
            
            // Apron around the pitch (street courts are asphalt, not grass)
            float apron = IsStreetCourt ? 3f : 6f;
            Color apronColor = _venue == Venue.Prosfygika
                ? new Color(128, 112, 88)
                : _venue == Venue.Street
                    ? new Color(64, 64, 70)
                    : new Color(24, 100, 24);
            AddGroundQuad(verts, indices,
                -_halfLength - apron, -_halfWidth - apron,
                _halfLength + apron, _halfWidth + apron,
                0f, apronColor);
            
            BuildFieldMarkings(verts, indices);
            if (_venue == Venue.Prosfygika)
                BuildProsfygikaGoals(verts, indices);
            else
                BuildGoals(verts, indices);
            if (!IsStreetCourt)
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
            else if (_venue == Venue.Sperchogeia)
            {
                BuildSperchogeiaVenue(verts, indices);
            }
            else if (_venue == Venue.Sfageia)
            {
                BuildSfageiaVenue(verts, indices);
            }
            else if (_venue == Venue.Kerasoulia)
            {
                BuildKerasouliaVenue(verts, indices);
            }
            else if (_venue == Venue.Street)
            {
                BuildStreetVenue(verts, indices);
            }
            else if (_venue == Venue.Prosfygika)
            {
                BuildProsfygikaVenue(verts, indices);
            }
            else
            {
                BuildSoulinariVenue(verts, indices);
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
            // Chalk wears into the dirt at Prosfygika.
            Color lineColor = _venue == Venue.Prosfygika
                ? new Color(196, 188, 170)
                : Color.White;
            
            // Outer boundary (touchlines + goal lines)
            AddRectOutline(verts, indices, -_halfLength, -_halfWidth, _halfLength, _halfWidth, LineWidth, lineColor);
            
            // Halfway line
            AddLineQuad(verts, indices, new Vector2(0, -_halfWidth), new Vector2(0, _halfWidth), LineWidth, lineColor);
            
            if (IsStreetCourt)
            {
                // Street court: small centre circle and compact goal boxes, no
                // penalty areas or corner arcs.
                AddRing(verts, indices, Vector2.Zero, 3.5f, LineWidth, 28, lineColor);
                AddGroundQuad(verts, indices, -0.15f, -0.15f, 0.15f, 0.15f, LineY, lineColor);

                float streetGoalHalfWidth = 3f;
                float streetGoalDepth = 2.5f;
                AddRectOutline(verts, indices, -_halfLength, -streetGoalHalfWidth,
                    -_halfLength + streetGoalDepth, streetGoalHalfWidth, LineWidth, lineColor);
                AddRectOutline(verts, indices, _halfLength - streetGoalDepth, -streetGoalHalfWidth,
                    _halfLength, streetGoalHalfWidth, LineWidth, lineColor);
                return;
            }
            
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
            Color postColor = _goalFrameColor;
            
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
        private bool IsStreetCourt => _venue == Venue.Street || _venue == Venue.Prosfygika;
        private float FenceOffset => IsStreetCourt ? 1.2f : 5f;
        private float FenceX => _halfLength + FenceOffset;
        private float FenceZ => _halfWidth + FenceOffset;
        // Fence height and goal frame color are venue-aware (Kerasoulia has a
        // taller chain-link fence and blue goal frames; everything else keeps
        // the original 2.1m fence and white frames).
        private readonly float _fenceHeight = 2.1f;
        private readonly Color _goalFrameColor = Color.White;
        
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
            BuildFanBanners();
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
            BuildFanBanners();
            
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
        
        /// <summary>Low-poly olive: gnarled forked trunk + a silvery-green crown.</summary>
        private void AddOliveTree(List<VertexPositionColor> verts, List<int> indices, Random random, float x, float z)
        {
            float scale = 0.7f + 0.6f * (float)random.NextDouble();
            Color trunk = new Color(105, 85, 60);
            Color trunkDark = new Color(82, 64, 45);

            Vector3 fork = new Vector3(x - 0.08f * scale,
                (0.95f + 0.30f * (float)random.NextDouble()) * scale,
                z + 0.05f * scale);
            AddCylinder(verts, indices, new Vector3(x, 0f, z), fork,
                0.20f * scale, 0.13f * scale, trunk, 6);
            AddCylinder(verts, indices, fork,
                fork + new Vector3(0.42f, 0.65f, -0.22f) * scale,
                0.09f * scale, 0.045f * scale, trunkDark, 5);
            AddCylinder(verts, indices, fork,
                fork + new Vector3(-0.38f, 0.72f, 0.28f) * scale,
                0.085f * scale, 0.04f * scale, trunkDark, 5);

            Color foliage = new Color(100, 118, 72); // Silvery olive green
            int blobs = 4 + random.Next(3);
            for (int i = 0; i < blobs; i++)
            {
                float ox = (float)(random.NextDouble() * 2.0 - 1.0) * 1.0f * scale;
                float oz = (float)(random.NextDouble() * 2.0 - 1.0) * 1.0f * scale;
                float oy = (1.6f + (float)random.NextDouble() * 0.9f) * scale;
                float r = (0.80f + 0.55f * (float)random.NextDouble()) * scale;
                int jitter = random.Next(-12, 13);
                Color c = new Color(
                    Math.Clamp(foliage.R + jitter, 0, 255),
                    Math.Clamp(foliage.G + jitter, 0, 255),
                    Math.Clamp(foliage.B + jitter, 0, 255));
                AddSphere(verts, indices, new Vector3(x + ox, oy, z + oz), r, c, 7, 4);
            }

            AddTreeShadow(x, z, 1.05f * scale);
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
        
        #region Fan banners (bed-sheet style, all fence venues)
        
        /// <summary>
        /// Hand-painted fan banners hung on the far fence: three of the five
        /// designs (FREE PALESTINE / ΛΕΥΤΕΡΙΑ ΣΤΗ ΠΑΛΑΙΣΤΙΝΗ /
        /// ΤΟ ΕΓΚΛΗΜΑ ΤΗΣ ΠΥΛΟΥ ΔΕ ΘΑ ΞΕΧΑΣΤΕΙ / ΤΕΜΠΗ - ΠΥΛΟΣ - ΠΑΛΑΙΣΤΙΝΗ /
        /// ΔΙΚΑΙΩΣΗ ΓΙΑ ΤΟ ΘΟΔΩΡΗ), chosen at random for each match.
        /// </summary>
        private void BuildFanBanners()
        {
            float zFar = -FenceZ + 0.12f;  // pitch side of the far fence, in front of sponsor banners
            const float y0 = 0.55f, y1 = 1.75f;
            
            // Shuffle the 6 atlas segments, take the first 3
            const int segmentCount = 6;
            var segs = new List<int> { 0, 1, 2, 3, 4, 5 };
            var rng = new Random();
            for (int i = segs.Count - 1; i > 0; i--)
            {
                int j = rng.Next(i + 1);
                (segs[i], segs[j]) = (segs[j], segs[i]);
            }

            // Prosfygika: put the neighbourhood motto on the central banner.
            if (_venue == Venue.Prosfygika)
            {
                segs.Remove(5);
                segs.Insert(1, 5);
            }
            
            var positions = new[] { (-25f, -11f), (-8f, 6f), (9f, 23f) };
            for (int i = 0; i < positions.Length; i++)
            {
                float u0 = segs[i] / (float)segmentCount, u1 = (segs[i] + 1) / (float)segmentCount;
                float x0 = positions[i].Item1, x1 = positions[i].Item2;
                AddTexturedQuad(_fanBannerVertList, _fanBannerIndexList,
                    new Vector3(x0, y0, zFar), new Vector2(u0, 1f),
                    new Vector3(x1, y0, zFar), new Vector2(u1, 1f),
                    new Vector3(x1, y1, zFar), new Vector2(u1, 0f),
                    new Vector3(x0, y1, zFar), new Vector2(u0, 0f));
            }
        }
        
        /// <summary>Bakes the 6 fan banners (3072x256, 512px each): aged cloth, dark red painted text.</summary>
        private static Texture2D CreateFanBannerTexture(GraphicsDevice device, ContentManager content)
        {
            const int width = 3072, height = 256, segW = 512;
            var target = new RenderTarget2D(device, width, height);
            device.SetRenderTarget(target);
            device.Clear(new Color(232, 224, 205));
            
            var texts = new[]
            {
                "FREE PALESTINE",
                "ΛΕΥΤΕΡΙΑ ΣΤΗ ΠΑΛΑΙΣΤΙΝΗ",
                "ΤΟ ΕΓΚΛΗΜΑ ΤΗΣ ΠΥΛΟΥ ΔΕ ΘΑ ΞΕΧΑΣΤΕΙ",
                "ΤΕΜΠΗ - ΠΥΛΟΣ - ΠΑΛΑΙΣΤΙΝΗ",
                "ΔΙΚΑΙΩΣΗ ΓΙΑ ΤΟ ΘΟΔΩΡΗ",
                "ΣΩΣΤΕ ΤΑ ΠΡΟΣΦΥΓΙΚΑ",
            };
            
            var spriteBatch = new SpriteBatch(device);
            spriteBatch.Begin();
            try
            {
                SpriteFont font = null;
                if (content != null)
                {
                    try { font = content.Load<SpriteFont>("Font"); }
                    catch (Exception) { font = null; }
                }
                if (font != null)
                {
                    Color paint = new Color(140, 20, 25);
                    for (int i = 0; i < texts.Length; i++)
                    {
                        // Vertically stretched (2x) hand-painted lettering: fills
                        // the cloth height so it reads from the stands
                        Vector2 size = font.MeasureString(texts[i]);
                        float scaleX = segW * 0.9f / size.X;
                        float scaleY = Math.Min(scaleX * 2.5f, height * 0.92f / size.Y);
                        var scale = new Vector2(scaleX, scaleY);
                        Vector2 position = new Vector2(
                            i * segW + segW / 2f - size.X * scaleX / 2f,
                            height / 2f - size.Y * scaleY / 2f);
                        spriteBatch.DrawString(font, texts[i], position, paint,
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
        
        #endregion
        
        #region Sfageia venue (seaside ground, Kalamata)
        
        /// <summary>
        /// ΓΗΠΕΔΟ ΣΦΑΓΕΙΩΝ: the seaside municipal ground of Kalamata. The far
        /// (-Z) side is the pebble beach and the Messenian Gulf with rock
        /// breakwaters; palms line the promenade; tennis courts on the east,
        /// the red-roofed clubhouse on the west, floodlights, houses uphill.
        /// </summary>
        private void BuildSfageiaVenue(List<VertexPositionColor> verts, List<int> indices)
        {
            var random = new Random(1821);
            
            BuildFence(verts, indices);
            BuildScoreboard(verts, indices);
            BuildSponsorBanners();
            BuildFloodlights(verts, indices);
            BuildFanBanners();
            BuildRockBreakwaters(verts, indices, random);
            BuildPalms(verts, indices, random);
            BuildTennisCourts(verts, indices);
            BuildClubhouse(verts, indices, random);
            
            // A few houses uphill on the inland side
            for (int i = 0; i < 6; i++)
                AddHouse(verts, indices, random,
                    -60f + 130f * (float)random.NextDouble(),
                    52f + 25f * (float)random.NextDouble());
        }
        
        /// <summary>Gray rock blobs along the waterline plus two short breakwaters reaching into the sea.</summary>
        private void BuildRockBreakwaters(List<VertexPositionColor> verts, List<int> indices, Random random)
        {
            Color rock = new Color(105, 102, 98);
            // Shore rocks scattered along the beach edge
            for (int i = 0; i < 60; i++)
            {
                float x = -110f + 220f * (float)random.NextDouble();
                float z = -80f - 6f * (float)random.NextDouble();
                float r = 0.7f + 0.9f * (float)random.NextDouble();
                int jitter = random.Next(-10, 11);
                Color c = new Color(
                    Math.Clamp(rock.R + jitter, 0, 255),
                    Math.Clamp(rock.G + jitter, 0, 255),
                    Math.Clamp(rock.B + jitter, 0, 255));
                AddSphere(verts, indices, new Vector3(x, -1.0f, z), r, c, 5, 3);
            }
            // Two breakwater arms reaching into the water
            foreach (float armX in new[] { -55f, 45f })
            {
                for (int i = 0; i < 14; i++)
                {
                    float z = -86f - i * 3.2f;
                    float x = armX + (float)(random.NextDouble() * 2.0 - 1.0) * 1.5f;
                    float r = 1.0f + 0.8f * (float)random.NextDouble();
                    AddSphere(verts, indices, new Vector3(x, -1.2f, z), r, rock, 5, 3);
                }
            }
        }
        
        /// <summary>Palm trees along the seaside promenade behind the far fence.</summary>
        private void BuildPalms(List<VertexPositionColor> verts, List<int> indices, Random random)
        {
            for (float x = -90f; x <= 90f; x += 12f)
            {
                float jx = x + (float)(random.NextDouble() * 2.0 - 1.0) * 3f;
                float jz = -48.5f + (float)(random.NextDouble() * 2.0 - 1.0) * 1.5f;
                AddPalmTree(verts, indices, random, jx, jz);
            }
            // A couple more near the clubhouse
            AddPalmTree(verts, indices, random, -62f, -30f);
            AddPalmTree(verts, indices, random, -66f, -18f);
        }
        
        /// <summary>
        /// Low-poly palm: curved tapered trunk, a crown of broad drooping fronds
        /// and a few coconuts. Much fuller than the old stick-and-beams version.
        /// </summary>
        private void AddPalmTree(List<VertexPositionColor> verts, List<int> indices, Random random, float x, float z)
        {
            float h = 4.8f + 1.8f * (float)random.NextDouble();
            Color trunk = new Color(125, 100, 70);
            Color trunkDark = new Color(105, 82, 56);
            Color frond = new Color(48, 115, 50);

            float lean = (float)(random.NextDouble() * 2.0 - 1.0) * 0.9f;
            float leanZ = (float)(random.NextDouble() * 2.0 - 1.0) * 0.7f;

            Vector3 prev = new Vector3(x, 0f, z);
            const int segments = 5;
            for (int i = 0; i < segments; i++)
            {
                float t0 = i / (float)segments;
                float t1 = (i + 1) / (float)segments;
                Vector3 p0 = new Vector3(x + lean * t0 * t0, h * t0, z + leanZ * t0 * t0);
                Vector3 p1 = new Vector3(x + lean * t1 * t1, h * t1, z + leanZ * t1 * t1);
                float r0 = 0.20f - i * 0.015f;
                float r1 = r0 - 0.015f;
                AddCylinder(verts, indices, p0, p1, r0, r1,
                    (i & 1) == 0 ? trunk : trunkDark, 6);
                prev = p1;
            }

            Vector3 top = prev;
            AddSphere(verts, indices, top + new Vector3(0f, 0.05f, 0f), 0.26f,
                new Color(75, 86, 48), 6, 3);

            // 9-11 fronds, each a shallow V-ribbon that rises then droops
            int fronds = 9 + random.Next(3);
            for (int i = 0; i < fronds; i++)
            {
                float angle = MathHelper.TwoPi * i / fronds + (float)random.NextDouble() * 0.25f;
                Vector3 dir = new Vector3((float)Math.Cos(angle), 0f, (float)Math.Sin(angle));
                Vector3 p0 = top + dir * 0.12f;
                Vector3 p1 = top + dir * 0.95f + new Vector3(0f, 0.35f, 0f);
                Vector3 p2 = top + dir * 1.90f + new Vector3(0f, 0.12f, 0f);
                Vector3 p3 = top + dir * 2.80f + new Vector3(0f, -0.55f, 0f);
                int jitter = random.Next(-8, 9);
                Color c = new Color(
                    Math.Clamp(frond.R + jitter, 0, 255),
                    Math.Clamp(frond.G + jitter, 0, 255),
                    Math.Clamp(frond.B + jitter, 0, 255));
                AddFrond(verts, indices, p0, p1, p2, p3, dir, c);
            }

            // Coconut cluster under the crown
            for (int i = 0; i < 3; i++)
            {
                float a = MathHelper.TwoPi * i / 3f;
                Vector3 offset = new Vector3((float)Math.Cos(a) * 0.18f, -0.12f, (float)Math.Sin(a) * 0.18f);
                AddSphere(verts, indices, top + offset, 0.12f, new Color(92, 68, 40), 5, 3);
            }

            AddTreeShadow(x, z, 1.30f);
        }
        
        /// <summary>The east-side tennis courts: two red-framed green courts with nets.</summary>
        private void BuildTennisCourts(List<VertexPositionColor> verts, List<int> indices)
        {
            Color surround = new Color(165, 60, 55);
            Color surface = new Color(60, 110, 70);
            Color lineColor = new Color(230, 230, 225);
            Color net = new Color(40, 40, 45);
            
            for (int court = 0; court < 2; court++)
            {
                float cz = -18f + court * 26f; // court centers along z
                float cx = 78f;
                // Surround + playing surface
                AddGroundQuad(verts, indices, cx - 8f, cz - 13f, cx + 8f, cz + 13f, -0.005f, surround);
                AddGroundQuad(verts, indices, cx - 5.5f, cz - 11f, cx + 5.5f, cz + 11f, 0f, surface);
                // Baselines + center service lines (simplified markings)
                AddLineQuad(verts, indices, new Vector2(cx - 5.5f, cz - 11f), new Vector2(cx + 5.5f, cz - 11f), 0.1f, lineColor);
                AddLineQuad(verts, indices, new Vector2(cx - 5.5f, cz + 11f), new Vector2(cx + 5.5f, cz + 11f), 0.1f, lineColor);
                AddLineQuad(verts, indices, new Vector2(cx, cz - 6.4f), new Vector2(cx, cz + 6.4f), 0.1f, lineColor);
                // Net across the middle
                AddBox(verts, indices,
                    new Vector3(cx - 5.5f, 0f, cz - 0.03f), new Vector3(cx + 5.5f, 0.9f, cz + 0.03f), net);
            }
        }
        
        /// <summary>The red-roofed clubhouse on the west corner with a small canopy.</summary>
        private void BuildClubhouse(List<VertexPositionColor> verts, List<int> indices, Random random)
        {
            AddHouse(verts, indices, random, -68f, -34f);
            // Canopy porch facing the pitch
            Color canopy = new Color(200, 60, 50);
            Color post = new Color(70, 70, 75);
            AddBox(verts, indices,
                new Vector3(-64.5f, 2.2f, -37.5f), new Vector3(-59f, 2.35f, -31.5f), canopy);
            AddBox(verts, indices,
                new Vector3(-59.4f, 0f, -37f), new Vector3(-59.2f, 2.2f, -36.8f), post);
            AddBox(verts, indices,
                new Vector3(-59.4f, 0f, -32.2f), new Vector3(-59.2f, 2.2f, -32f), post);
        }
        
        /// <summary>A box beam between two points (palm fronds).</summary>
        private static void AddBeam(List<VertexPositionColor> verts, List<int> indices,
            Vector3 a, Vector3 b, float thickness, Color color)
        {
            Vector3 dir = b - a;
            if (dir.LengthSquared() < 0.0001f) return;
            dir.Normalize();
            Vector3 upRef = Math.Abs(dir.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 side = Vector3.Normalize(Vector3.Cross(dir, upRef)) * (thickness / 2f);
            Vector3 up = Vector3.Normalize(Vector3.Cross(side, dir)) * (thickness / 2f);
            
            AddQuad(verts, indices, a - side - up, b - side - up, b + side - up, a + side - up, color);
            AddQuad(verts, indices, a - side + up, b - side + up, b + side + up, a + side + up, color);
            AddQuad(verts, indices, a - side - up, a - side + up, b - side + up, b - side - up, color);
            AddQuad(verts, indices, a + side - up, a + side + up, b + side + up, b + side - up, color);
        }
        
        #endregion
        
        #region Kerasoulia venue (mountain forest ground, Taygetos)
        
        /// <summary>
        /// ΣΤΑΔΙΟ ΚΕΡΑΣΟΥΛΙΑΣ: a small mountain ground on Taygetos near
        /// Alagonia. Dense conifer forest rings the whole venue, the Taygetos
        /// ridge towers behind, a red running-track strip and a curved asphalt
        /// road hug the far touchline (ground quads, built with the apron).
        /// Tall chain-link fence (set in the constructor), blue goal frames,
        /// floodlights, a basketball hoop behind the east goal, wooden benches
        /// and a small covered stone grandstand with blue pillars on the near side.
        /// </summary>
        private void BuildKerasouliaVenue(List<VertexPositionColor> verts, List<int> indices)
        {
            var random = new Random(1962);
            
            BuildFence(verts, indices);
            BuildScoreboard(verts, indices);
            BuildSponsorBanners();
            BuildFloodlights(verts, indices);
            BuildMountains(verts, indices, random);
            BuildForestRing(verts, indices, random);
            BuildCoveredStoneStand(verts, indices);
            BuildStandingTerrace(verts, indices);
            
            // Basketball hoop behind the east goal (multi-use court), rim toward the pitch
            AddBasketballHoop(verts, indices, FenceX + 3.5f, 0f, -1f);
            
            // Simple wooden benches along the near touchline, flanking the stand
            Color wood = new Color(120, 88, 55);
            foreach (float bx in new[] { -20f, 20f })
            {
                AddBox(verts, indices,
                    new Vector3(bx - 1.0f, 0.42f, 37.3f), new Vector3(bx + 1.0f, 0.5f, 37.7f), wood);
                AddBox(verts, indices,
                    new Vector3(bx - 0.8f, 0f, 37.4f), new Vector3(bx - 0.6f, 0.42f, 37.6f), wood);
                AddBox(verts, indices,
                    new Vector3(bx + 0.6f, 0f, 37.4f), new Vector3(bx + 0.8f, 0.42f, 37.6f), wood);
            }
            
            BuildFanBanners();
        }

        // Kerasoulia standing-terrace metrics - shared with FanSection (fans on the steps).
        // Tall enough that heads and waving flags clear the 3.4m fence + banners.
        public const float KerasouliaTerraceFrontZ = -40.5f;  // just behind the far fence
        public const float KerasouliaTerraceFirstRowY = 1.6f;
        public const float KerasouliaTerraceRowRise = 0.9f;
        public const float KerasouliaTerraceRowDepth = 0.8f;
        public const float KerasouliaTerraceCenterX = -8f;
        public const float KerasouliaTerraceHalfWidth = 12f;
        public const int KerasouliaTerraceRows = 2;

        /// <summary>
        /// A small raised standing terrace behind the far fence (the fence here is
        /// 3.4m tall and covered in banners - ground-level fans were invisible).
        /// Two concrete steps the supporters stand on.
        /// </summary>
        private void BuildStandingTerrace(List<VertexPositionColor> verts, List<int> indices)
        {
            Color concrete = new Color(110, 110, 112);
            Color concreteDark = new Color(85, 85, 88);
            float cx = KerasouliaTerraceCenterX, hw = KerasouliaTerraceHalfWidth;

            for (int row = 0; row < KerasouliaTerraceRows; row++)
            {
                float topY = KerasouliaTerraceFirstRowY + row * KerasouliaTerraceRowRise;
                float zFront = KerasouliaTerraceFrontZ - row * KerasouliaTerraceRowDepth;
                // Step block (riser + tread)
                AddBox(verts, indices,
                    new Vector3(cx - hw, row * KerasouliaTerraceRowRise * 0.5f, zFront - KerasouliaTerraceRowDepth),
                    new Vector3(cx + hw, topY, zFront), row % 2 == 0 ? concrete : concreteDark);
            }
            // Side walls
            foreach (float sx in new[] { cx - hw, cx + hw })
            {
                AddBox(verts, indices,
                    new Vector3(sx - 0.15f, 0f, KerasouliaTerraceFrontZ - KerasouliaTerraceRows * KerasouliaTerraceRowDepth),
                    new Vector3(sx + 0.15f, KerasouliaTerraceFirstRowY + KerasouliaTerraceRowRise, KerasouliaTerraceFrontZ),
                    concreteDark);
            }
        }

        /// <summary>
        /// Dense conifer forest ring all around the ground (the venue sits in a
        /// forest clearing on the mountain). Several depth rows on every side;
        /// the north rows start beyond the road that hugs the far touchline.
        /// </summary>
        private void BuildForestRing(List<VertexPositionColor> verts, List<int> indices, Random random)
        {
            for (int row = 0; row < 4; row++)
            {
                float offset = 7f + row * 6.5f;
                for (float t = -110f; t <= 110f; t += 5.5f)
                {
                    float jx = t + (float)(random.NextDouble() * 2.0 - 1.0) * 2.0f;
                    float jz = offset + (float)(random.NextDouble() * 2.0 - 1.0) * 2.0f;
                    
                    // North rows start past track + road (road at z -52..-44)
                    TryAddConifer(verts, indices, random, jx, -(FenceZ + 14f + jz));
                    // South rows
                    TryAddConifer(verts, indices, random, jx, FenceZ + jz);
                    // East and West rows (within the pitch's z span)
                    if (Math.Abs(t) <= 55f)
                    {
                        TryAddConifer(verts, indices, random, FenceX + jz, jx);
                        TryAddConifer(verts, indices, random, -(FenceX + jz), jx);
                    }
                }
            }
        }
        
        private void TryAddConifer(List<VertexPositionColor> verts, List<int> indices, Random random, float x, float z)
        {
            // Keep the scoreboard surroundings clear
            if (x < -FenceX - 1f && x > -FenceX - 14f && Math.Abs(z) < 10f) return;
            // Keep the floodlight footprints clear
            if (Math.Abs(Math.Abs(x) - (FenceX + 3f)) < 2.5f && Math.Abs(Math.Abs(z) - (FenceZ + 3f)) < 2.5f) return;
            // Keep the covered stand clear
            if (Math.Abs(x) < 19f && z > FenceZ - 1f && z < FenceZ + 13f) return;
            // Keep the basketball hoop surroundings clear
            if (x > FenceX + 0.5f && x < FenceX + 8f && Math.Abs(z) < 5f) return;
            AddConiferTree(verts, indices, random, x, z);
        }
        
        /// <summary>Tall conifer for the mountain forest: tapered trunk + layered tiers.</summary>
        private void AddConiferTree(List<VertexPositionColor> verts, List<int> indices, Random random, float x, float z)
        {
            float scale = 0.9f + 0.7f * (float)random.NextDouble();
            float h = (5.8f + 2.4f * (float)random.NextDouble()) * scale;
            Color trunk = new Color(80, 60, 45);
            AddCylinder(verts, indices, new Vector3(x, 0f, z), new Vector3(x, h * 0.26f, z),
                0.20f * scale, 0.11f * scale, trunk, 6);

            float y = h * 0.16f;
            float r = 1.85f * scale;
            for (int i = 0; i < 5; i++)
            {
                float tierH = h * (0.34f - i * 0.035f);
                int jitter = random.Next(-8, 9);
                Color c = new Color(
                    Math.Clamp(28 + jitter, 0, 255),
                    Math.Clamp(62 + jitter + i * 4, 0, 255),
                    Math.Clamp(38 + jitter, 0, 255));
                AddCylinder(verts, indices,
                    new Vector3(x, y, z), new Vector3(x, y + tierH, z),
                    r, r * 0.45f, c, 8);
                y += tierH * 0.52f;
                r *= 0.72f;
            }

            AddCone(verts, indices, new Vector3(x, y, z), r * 0.95f, h * 0.20f,
                new Color(36, 78, 44), 6);
            AddTreeShadow(x, z, 1.70f * scale);
        }
        
        /// <summary>
        /// The small covered grandstand on the near (+Z) side: 4 stone-gray
        /// stepped rows under a flat roof slab carried on six blue pillars
        /// (the same blue as the goal frames).
        /// </summary>
        private void BuildCoveredStoneStand(List<VertexPositionColor> verts, List<int> indices)
        {
            Color stone = new Color(150, 145, 135);
            Color pillar = new Color(30, 60, 200);
            Color roof = new Color(90, 90, 95);
            
            const float hw = 13f;             // 26m wide, centered on x=0
            const float frontZ = 40f;         // 1m outside the near fence
            const int steps = 4;
            const float stepDepth = 0.8f;
            const float stepHeight = 0.4f;
            
            // Stone steps rising away from the pitch
            for (int step = 0; step < steps; step++)
            {
                float zFront = frontZ + step * stepDepth;
                float zBack = zFront + stepDepth;
                float top = (step + 1) * stepHeight;
                AddBox(verts, indices,
                    new Vector3(-hw, 0f, zFront), new Vector3(hw, top, zBack), stone);
            }
            
            // Flat roof slab over the steps (cantilevered from the back pillars)
            float backZ = frontZ + steps * stepDepth;
            float roofY = steps * stepHeight + 2.4f;
            AddBox(verts, indices,
                new Vector3(-hw - 0.5f, roofY, frontZ - 0.8f),
                new Vector3(hw + 0.5f, roofY + 0.25f, backZ + 0.6f), roof);
            
            // Blue pillars along the back edge of the steps
            const int pillarCount = 6;
            for (int i = 0; i < pillarCount; i++)
            {
                float px = -hw + 2f * hw * i / (pillarCount - 1);
                AddBox(verts, indices,
                    new Vector3(px - 0.12f, 0f, backZ - 0.35f),
                    new Vector3(px + 0.12f, roofY, backZ - 0.11f), pillar);
            }
        }
        
        /// <summary>
        /// A basketball hoop: gray post, white backboard and an orange square
        /// rim. `facing` is the X direction the backboard/rim extend toward
        /// (+1 = toward +X, -1 = toward -X).
        /// </summary>
        private static void AddBasketballHoop(List<VertexPositionColor> verts, List<int> indices,
            float x, float z, float facing)
        {
            Color post = new Color(70, 70, 78);
            Color board = new Color(235, 235, 230);
            Color rim = new Color(220, 90, 30);
            
            const float rimHeight = 3.05f;
            
            // Post behind the backboard
            AddBox(verts, indices,
                new Vector3(x - 0.09f, 0f, z - 0.09f), new Vector3(x + 0.09f, 3.9f, z + 0.09f), post);
            // Backboard (1.8m wide, 1.05m tall) on the `facing` side of the post
            float boardX = x + facing * 0.25f;
            AddBox(verts, indices,
                new Vector3(boardX - 0.04f, rimHeight + 0.15f, z - 0.9f),
                new Vector3(boardX + 0.04f, rimHeight + 1.2f, z + 0.9f), board);
            // Rim: four thin orange boxes forming a square ring in front of the board
            float rimFar = boardX + facing * 0.45f;
            float rx0 = Math.Min(boardX + facing * 0.05f, rimFar);
            float rx1 = Math.Max(boardX + facing * 0.05f, rimFar);
            const float rw = 0.23f, rt = 0.04f;
            AddBox(verts, indices,
                new Vector3(rx0, rimHeight - rt, z - rw - rt), new Vector3(rx1, rimHeight + rt, z - rw + rt), rim);
            AddBox(verts, indices,
                new Vector3(rx0, rimHeight - rt, z + rw - rt), new Vector3(rx1, rimHeight + rt, z + rw + rt), rim);
            AddBox(verts, indices,
                new Vector3(rx0, rimHeight - rt, z - rw - rt), new Vector3(rx0 + rt * 2f, rimHeight + rt, z + rw + rt), rim);
            AddBox(verts, indices,
                new Vector3(rx1 - rt * 2f, rimHeight - rt, z - rw - rt), new Vector3(rx1, rimHeight + rt, z + rw + rt), rim);
        }
        
        #endregion

        #region Street venue (Antifa League cage)

        /// <summary>
        /// ANTIFA LEAGUE street court: asphalt cage surrounded by a graffiti
        /// wall, city blocks, benches, a container and a lamppost.
        /// </summary>
        private void BuildStreetVenue(List<VertexPositionColor> verts, List<int> indices)
        {
            BuildFence(verts, indices);
            BuildFloodlights(verts, indices);
            BuildFanBanners();
            BuildStreetSurroundings(verts, indices);
        }

        private void BuildStreetSurroundings(List<VertexPositionColor> verts, List<int> indices)
        {
            // Graffiti wall behind the far cage
            float wallZ = -FenceZ - 3.5f;
            float wallH = 4.5f;
            AddBox(verts, indices,
                new Vector3(-38f, 0f, wallZ - 0.25f),
                new Vector3(38f, wallH, wallZ + 0.25f), new Color(70, 70, 76));

            Color[] graffiti =
            {
                new Color(210, 35, 45), new Color(30, 90, 190), new Color(240, 190, 30),
                new Color(40, 150, 80), new Color(150, 60, 170), new Color(230, 110, 35),
                new Color(25, 25, 30), new Color(220, 220, 215),
            };
            int panel = 0;
            for (float x = -34f; x < 33f; x += 5.6f)
            {
                Color c = graffiti[panel++ % graffiti.Length];
                AddQuad(verts, indices,
                    new Vector3(x, 0.6f, wallZ + 0.26f),
                    new Vector3(x + 4.8f, 0.6f, wallZ + 0.26f),
                    new Vector3(x + 4.8f, 3.4f, wallZ + 0.26f),
                    new Vector3(x, 3.4f, wallZ + 0.26f), c);
            }
            AddBox(verts, indices,
                new Vector3(-38.3f, wallH, wallZ - 0.35f),
                new Vector3(38.3f, wallH + 0.25f, wallZ + 0.35f), new Color(50, 50, 56));

            // City blocks beyond the wall and to the sides
            AddUniversityBlock(verts, indices, 42f, -30f, 20f, 12f, 15f);
            AddUniversityBlock(verts, indices, -42f, -30f, 18f, 12f, 12f);
            AddUniversityBlock(verts, indices, 48f, 18f, 14f, 10f, 10f);

            // Container, benches and a lamppost outside the cage
            AddBox(verts, indices,
                new Vector3(24f, 0f, -FenceZ - 2.5f),
                new Vector3(30f, 2.6f, -FenceZ - 0.5f), new Color(35, 110, 60));

            for (int i = 0; i < 3; i++)
            {
                float bx = -8f + i * 8f;
                AddBox(verts, indices,
                    new Vector3(bx, 0f, FenceZ + 2.0f),
                    new Vector3(bx + 4f, 0.45f, FenceZ + 2.6f), new Color(90, 70, 50));
                AddBox(verts, indices,
                    new Vector3(bx, 0.45f, FenceZ + 2.5f),
                    new Vector3(bx + 4f, 0.9f, FenceZ + 2.7f), new Color(90, 70, 50));
            }

            AddBox(verts, indices,
                new Vector3(-FenceX - 2f, 0f, FenceZ + 3f),
                new Vector3(-FenceX - 1.8f, 6f, FenceZ + 3.2f), new Color(60, 60, 65));
            AddBox(verts, indices,
                new Vector3(-FenceX - 2.2f, 6f, FenceZ + 2.8f),
                new Vector3(-FenceX - 1.6f, 6.4f, FenceZ + 3.4f), new Color(255, 240, 180));

            // A few trees outside the lot
            AddTree(verts, indices, new Random(99), FenceX + 10f, FenceZ + 8f);
            AddTree(verts, indices, new Random(100), -FenceX - 10f, FenceZ + 6f);
            AddTree(verts, indices, new Random(101), FenceX + 8f, -FenceZ - 8f);
        }

        #endregion

        #region Prosfygika venue (Athens refugee housing blocks)

        /// <summary>
        /// ΠΡΟΣΦΥΓΙΚΑ: a neighborhood street court between the old refugee
        /// apartment blocks on Alexandras Avenue. No real goal frames — two
        /// flower pots mark each goal — plus parked cars, dense trees and the
        /// "ΣΩΣΤΕ ΤΑ ΠΡΟΣΦΥΓΙΚΑ" sign.
        /// </summary>
        private void BuildProsfygikaVenue(List<VertexPositionColor> verts, List<int> indices)
        {
            // Open neighborhood court: no cage/fence, banners hang on the blocks.
            BuildFloodlights(verts, indices);
            BuildProsfygikaBanners();
            BuildProsfygikaSurroundings(verts, indices);
        }

        /// <summary>Bed-sheet banners pinned to the far refugee block (no fence).</summary>
        private void BuildProsfygikaBanners()
        {
            float z = -17.42f;
            // Motto segment (5 of 6)
            AddTexturedQuad(_fanBannerVertList, _fanBannerIndexList,
                new Vector3(-9f, 1.2f, z), new Vector2(5f / 6f, 1f),
                new Vector3(9f, 1.2f, z), new Vector2(1f, 1f),
                new Vector3(9f, 3.2f, z), new Vector2(1f, 0f),
                new Vector3(-9f, 3.2f, z), new Vector2(5f / 6f, 0f));
            // A second banner on the side block
            AddTexturedQuad(_fanBannerVertList, _fanBannerIndexList,
                new Vector3(-20f, 1.2f, z), new Vector2(0f, 1f),
                new Vector3(-12f, 1.2f, z), new Vector2(1f / 6f, 1f),
                new Vector3(-12f, 2.8f, z), new Vector2(1f / 6f, 0f),
                new Vector3(-20f, 2.8f, z), new Vector2(0f, 0f));
        }

        private void BuildProsfygikaGoals(List<VertexPositionColor> verts, List<int> indices)
        {
            float goalHalfWidth = WorldUnits.PxToM(MatchEngine.GoalWidth) / 2f;
            foreach (float side in new[] { -1f, 1f })
            {
                float x = side * _halfLength;
                foreach (float zSign in new[] { -1f, 1f })
                {
                    AddFlowerPot(verts, indices,
                        new Vector3(x, 0f, zSign * goalHalfWidth), 0.28f);
                }
            }
        }

        private void BuildProsfygikaSurroundings(List<VertexPositionColor> verts, List<int> indices)
        {
            var random = new Random(1972);

            // Old refugee housing blocks: long, low, weathered, balconied.
            AddProsfygikaBlock(verts, indices, -18f, -22f, 30f, 9f, 9f,
                new Color(224, 216, 192), 3, 0);
            AddProsfygikaBlock(verts, indices, 16f, -22f, 26f, 9f, 10f,
                new Color(205, 196, 172), 3, 1);
            AddProsfygikaBlock(verts, indices, -34f, 2f, 10f, 26f, 9f,
                new Color(214, 205, 185), 3, 2);
            AddProsfygikaBlock(verts, indices, 34f, 2f, 10f, 26f, 8f,
                new Color(198, 188, 168), 3, 3);

            // Dense street trees between the blocks and the cage.
            AddTree(verts, indices, new Random(11), -24f, -15.5f);
            AddTree(verts, indices, new Random(12), -12f, -15.5f);
            AddTree(verts, indices, new Random(13), 0f, -16.0f);
            AddTree(verts, indices, new Random(14), 13f, -15.5f);
            AddTree(verts, indices, new Random(15), 24f, -15.5f);
            AddTree(verts, indices, new Random(16), -25f, 15.0f);
            AddTree(verts, indices, new Random(17), 25f, 15.0f);

            // Parked cars along both sides of the street.
            Color[] carColors =
            {
                new Color(38, 48, 72), new Color(170, 174, 178), new Color(92, 96, 102),
                new Color(210, 210, 205), new Color(30, 30, 34), new Color(70, 95, 140),
            };
            float[] farXs = { -27f, -19f, -9f, 2f, 12f, 21f, 28f };
            for (int i = 0; i < farXs.Length; i++)
                AddParkedCar(verts, indices, farXs[i], -FenceZ - 2.6f, carColors[i % carColors.Length]);
            float[] nearXs = { -24f, -14f, -2f, 10f, 22f };
            for (int i = 0; i < nearXs.Length; i++)
                AddParkedCar(verts, indices, nearXs[i], FenceZ + 2.6f, carColors[(i + 3) % carColors.Length]);

            // Street lamps
            foreach (float x in new[] { -FenceX - 2f, FenceX + 2f })
            {
                AddBox(verts, indices,
                    new Vector3(x - 0.1f, 0f, FenceZ + 3f),
                    new Vector3(x + 0.1f, 6f, FenceZ + 3.2f), new Color(60, 60, 65));
                AddBox(verts, indices,
                    new Vector3(x - 0.3f, 6f, FenceZ + 2.8f),
                    new Vector3(x + 0.3f, 6.4f, FenceZ + 3.4f), new Color(255, 240, 180));
            }

            // Anti-eviction graffiti on the ground floors.
            Color[] graffiti =
            {
                new Color(210, 35, 45), new Color(30, 90, 190), new Color(240, 190, 30),
                new Color(40, 150, 80), new Color(150, 60, 170), new Color(230, 110, 35),
            };
            int panel = 0;
            foreach (float zFace in new[] { -17.46f, 15.1f })
            {
                for (float x = -28f; x < 28f; x += 8f)
                {
                    AddQuad(verts, indices,
                        new Vector3(x, 0.35f, zFace),
                        new Vector3(x + 6.5f, 0.35f, zFace),
                        new Vector3(x + 6.5f, 1.45f, zFace),
                        new Vector3(x, 1.45f, zFace), graffiti[panel++ % graffiti.Length]);
                }
            }
        }

        /// <summary>
        /// Three-story refugee block with an aged plaster facade, windows,
        /// shutters, AC units, balconies with laundry, and rooftop water tanks.
        /// </summary>
        private void AddProsfygikaBlock(List<VertexPositionColor> verts, List<int> indices,
            float x, float z, float w, float d, float h, Color body, int floors, int facadeVariant)
        {
            AddBox(verts, indices,
                new Vector3(x - w / 2f, 0f, z - d / 2f),
                new Vector3(x + w / 2f, h, z + d / 2f), body);

            // Textured aged plaster front (behind windows/balconies)
            float wallZ = z + d / 2f + 0.005f;
            float u0 = facadeVariant / 4f, u1 = (facadeVariant + 1) / 4f;
            AddTexturedQuad(_facadeVertList, _facadeIndexList,
                new Vector3(x - w / 2f, 0.02f, wallZ), new Vector2(u0, 1f),
                new Vector3(x + w / 2f, 0.02f, wallZ), new Vector2(u1, 1f),
                new Vector3(x + w / 2f, h, wallZ), new Vector2(u1, 0f),
                new Vector3(x - w / 2f, h, wallZ), new Vector2(u0, 0f));

            // Flat roof with a small parapet
            Color roof = new Color(125, 120, 112);
            AddBox(verts, indices,
                new Vector3(x - w / 2f - 0.2f, h, z - d / 2f - 0.2f),
                new Vector3(x + w / 2f + 0.2f, h + 0.2f, z + d / 2f + 0.2f), roof);
            AddBox(verts, indices,
                new Vector3(x - w / 2f, h + 0.2f, z - d / 2f),
                new Vector3(x + w / 2f, h + 0.45f, z - d / 2f + 0.25f), roof);
            AddBox(verts, indices,
                new Vector3(x - w / 2f, h + 0.2f, z + d / 2f - 0.25f),
                new Vector3(x + w / 2f, h + 0.45f, z + d / 2f), roof);

            // Rooftop laundry/stair room, water heater, solar panel and antenna
            Color outbuilding = new Color(195, 188, 172);
            AddBox(verts, indices,
                new Vector3(x - w / 2f + 1.2f, h + 0.45f, z - d / 2f + 1.2f),
                new Vector3(x - w / 2f + 3.4f, h + 2.5f, z - d / 2f + 3.4f), outbuilding);
            AddQuad(verts, indices,
                new Vector3(x - w / 2f + 1.8f, h + 0.45f, z - d / 2f + 1.15f),
                new Vector3(x - w / 2f + 2.8f, h + 0.45f, z - d / 2f + 1.15f),
                new Vector3(x - w / 2f + 2.8f, h + 2.1f, z - d / 2f + 1.15f),
                new Vector3(x - w / 2f + 1.8f, h + 2.1f, z - d / 2f + 1.15f), new Color(60, 50, 42));

            AddCylinder(verts, indices,
                new Vector3(x + w / 2f - 2f, h + 0.45f, z + d / 2f - 2f),
                new Vector3(x + w / 2f - 2f, h + 1.85f, z + d / 2f - 2f),
                0.35f, 0.35f, new Color(145, 145, 150), 7);
            AddBox(verts, indices,
                new Vector3(x + w / 2f - 4.6f, h + 0.5f, z + d / 2f - 3.2f),
                new Vector3(x + w / 2f - 2.6f, h + 0.62f, z + d / 2f - 1.4f),
                new Color(28, 42, 68));

            Color antenna = new Color(55, 55, 60);
            AddBox(verts, indices,
                new Vector3(x - w / 2f + 0.8f, h + 0.45f, z + d / 2f - 1.2f),
                new Vector3(x - w / 2f + 0.9f, h + 4.5f, z + d / 2f - 1.1f), antenna);
            AddBox(verts, indices,
                new Vector3(x - w / 2f + 0.3f, h + 3.8f, z + d / 2f - 1.15f),
                new Vector3(x - w / 2f + 1.4f, h + 3.9f, z + d / 2f - 1.05f), antenna);

            Color window = new Color(45, 55, 68);
            Color boarded = new Color(72, 58, 44);
            Color balcony = new Color(150, 145, 135);
            Color sill = new Color(168, 162, 150);
            Color acUnit = new Color(175, 175, 172);
            float faceZ = z + d / 2f + 0.02f;
            int cols = Math.Max(3, (int)(w / 4.5f));
            Color[] shutterColors =
            {
                new Color(60, 90, 65), new Color(95, 70, 48), new Color(120, 118, 110),
            };
            Color[] laundryColors =
            {
                new Color(210, 60, 55), new Color(55, 95, 175), new Color(240, 240, 235),
                new Color(235, 195, 55), new Color(90, 160, 100),
            };

            for (int f = 0; f < floors; f++)
            {
                float y = 1.4f + f * 2.5f;
                if (y + 1.35f > h - 0.35f) break;
                for (int c = 0; c < cols; c++)
                {
                    float step = cols > 1 ? (w - 3f) / (cols - 1) : 0f;
                    float wx = x - w / 2f + 1.5f + c * step - 0.65f;

                    Color winColor = (c + f) % 7 == 0 ? boarded : window;
                    AddQuad(verts, indices,
                        new Vector3(wx, y, faceZ),
                        new Vector3(wx + 1.3f, y, faceZ),
                        new Vector3(wx + 1.3f, y + 1.25f, faceZ),
                        new Vector3(wx, y + 1.25f, faceZ), winColor);

                    // Window sill
                    AddBox(verts, indices,
                        new Vector3(wx - 0.08f, y - 0.08f, faceZ),
                        new Vector3(wx + 1.38f, y, faceZ + 0.16f), sill);

                    // Some shutters (often weathered green/brown)
                    if (c % 3 == 1)
                    {
                        Color shutter = shutterColors[(c + f) % shutterColors.Length];
                        AddQuad(verts, indices,
                            new Vector3(wx - 0.38f, y, faceZ + 0.006f),
                            new Vector3(wx - 0.04f, y, faceZ + 0.006f),
                            new Vector3(wx - 0.04f, y + 1.25f, faceZ + 0.006f),
                            new Vector3(wx - 0.38f, y + 1.25f, faceZ + 0.006f), shutter);
                    }

                    // Occasional wall-mounted AC unit
                    if (f > 0 && c % 4 == 2)
                    {
                        AddBox(verts, indices,
                            new Vector3(wx + 1.5f, y + 0.15f, faceZ + 0.02f),
                            new Vector3(wx + 2.1f, y + 0.55f, faceZ + 0.32f), acUnit);
                    }

                    if (f > 0 && c % 2 == 0)
                    {
                        // Concrete balcony slab + simple railing
                        AddBox(verts, indices,
                            new Vector3(wx - 0.35f, y - 0.15f, faceZ),
                            new Vector3(wx + 1.65f, y, faceZ + 0.7f), balcony);
                        AddBox(verts, indices,
                            new Vector3(wx - 0.35f, y + 0.75f, faceZ + 0.65f),
                            new Vector3(wx + 1.65f, y + 0.85f, faceZ + 0.72f), balcony);
                        for (int p = 0; p < 4; p++)
                        {
                            float px = wx - 0.3f + p * 0.6f;
                            AddBox(verts, indices,
                                new Vector3(px - 0.04f, y, faceZ + 0.62f),
                                new Vector3(px + 0.04f, y + 0.8f, faceZ + 0.7f), balcony);
                        }

                        // Colourful laundry hanging over the balcony rail
                        for (int l = 0; l < 2; l++)
                        {
                            float lx = wx + 0.15f + l * 0.75f;
                            Color cloth = laundryColors[(c + f + l) % laundryColors.Length];
                            AddQuad(verts, indices,
                                new Vector3(lx, y - 0.55f, faceZ + 0.74f),
                                new Vector3(lx + 0.5f, y - 0.55f, faceZ + 0.74f),
                                new Vector3(lx + 0.5f, y - 0.05f, faceZ + 0.74f),
                                new Vector3(lx, y - 0.05f, faceZ + 0.74f), cloth);
                        }
                    }
                }
            }
        }

        /// <summary>Low-poly parked car (visual only, outside the cage).</summary>        /// <summary>Low-poly parked car (visual only, outside the cage).</summary>
        private static void AddParkedCar(List<VertexPositionColor> verts, List<int> indices,
            float x, float z, Color body)
        {
            const float halfL = 2f, halfW = 0.9f;
            Color glass = new Color(38, 48, 58);
            Color tyre = new Color(25, 25, 28);
            Color light = new Color(235, 225, 190);

            AddBox(verts, indices,
                new Vector3(x - halfL, 0.18f, z - halfW),
                new Vector3(x + halfL, 0.78f, z + halfW), body);
            AddBox(verts, indices,
                new Vector3(x - 1.15f, 0.78f, z - halfW * 0.86f),
                new Vector3(x + 0.95f, 1.32f, z + halfW * 0.86f), body);

            // Windows on the cabin sides
            AddQuad(verts, indices,
                new Vector3(x - 1.0f, 0.88f, z + halfW * 0.88f),
                new Vector3(x + 0.8f, 0.88f, z + halfW * 0.88f),
                new Vector3(x + 0.8f, 1.24f, z + halfW * 0.88f),
                new Vector3(x - 1.0f, 1.24f, z + halfW * 0.88f), glass);
            AddQuad(verts, indices,
                new Vector3(x - 1.0f, 0.88f, z - halfW * 0.88f),
                new Vector3(x + 0.8f, 0.88f, z - halfW * 0.88f),
                new Vector3(x + 0.8f, 1.24f, z - halfW * 0.88f),
                new Vector3(x - 1.0f, 1.24f, z - halfW * 0.88f), glass);

            // Wheels
            foreach (float wx in new[] { x - 1.25f, x + 1.25f })
            {
                foreach (float wz in new[] { z - halfW, z + halfW })
                {
                    AddBox(verts, indices,
                        new Vector3(wx - 0.3f, 0f, wz - 0.12f),
                        new Vector3(wx + 0.3f, 0.55f, wz + 0.12f), tyre);
                }
            }

            // Head/tail lights
            AddBox(verts, indices,
                new Vector3(x + halfL - 0.05f, 0.45f, z - halfW + 0.15f),
                new Vector3(x + halfL + 0.02f, 0.62f, z - halfW + 0.55f), light);
            AddBox(verts, indices,
                new Vector3(x + halfL - 0.05f, 0.45f, z + halfW - 0.55f),
                new Vector3(x + halfL + 0.02f, 0.62f, z + halfW - 0.15f), light);
        }

        /// <summary>Terracotta flower pot with foliage and flowers (street goalpost).</summary>
        private void AddFlowerPot(List<VertexPositionColor> verts, List<int> indices,
            Vector3 basePos, float radius)
        {
            Color pot = new Color(178, 88, 55);
            Color potDark = new Color(145, 70, 44);
            Color soil = new Color(72, 47, 30);
            Color leaf = new Color(48, 128, 58);
            Color[] flowers =
            {
                new Color(225, 55, 60), new Color(240, 200, 55), new Color(230, 120, 190),
            };

            AddCylinder(verts, indices, basePos, basePos + new Vector3(0f, 0.45f, 0f),
                radius * 0.78f, radius, pot, 7);
            AddCylinder(verts, indices, basePos + new Vector3(0f, 0.45f, 0f),
                basePos + new Vector3(0f, 0.48f, 0f), radius * 0.9f, radius * 0.9f, soil, 7);

            AddSphere(verts, indices, basePos + new Vector3(0f, 0.68f, 0f),
                radius * 1.25f, leaf, 7, 4);
            AddSphere(verts, indices, basePos + new Vector3(radius * 0.5f, 0.62f, 0f),
                radius * 0.7f, new Color(58, 145, 66), 6, 3);
            AddSphere(verts, indices, basePos + new Vector3(-radius * 0.45f, 0.6f, radius * 0.2f),
                radius * 0.65f, new Color(42, 115, 52), 6, 3);

            for (int i = 0; i < 4; i++)
            {
                float a = MathHelper.TwoPi * i / 4f;
                Vector3 offset = new Vector3((float)Math.Cos(a) * radius * 0.55f,
                    0.78f + (i % 2) * 0.08f,
                    (float)Math.Sin(a) * radius * 0.55f);
                AddSphere(verts, indices, basePos + offset, radius * 0.18f,
                    flowers[i % flowers.Length], 5, 2);
            }

            // A little shadow under the pot
            AddTreeShadow(basePos.X, basePos.Z, radius * 1.1f);
        }

        #endregion
        
        #region Soulinari venue (village ground near Pylos)
        
        /// <summary>
        /// ΓΗΠΕΔΟ ΣΟΥΛΗΝΑΡΙΟΥ: a village ground near Pylos. Small uncovered
        /// metal bleacher with side stairs on the far touchline, a green metal
        /// container next to it, a flat-roof changing-rooms building at the SE
        /// corner, an olive grove on the west side, red-roofed village houses
        /// on the east side, a basketball court at the NE corner and a dirt
        /// road ring (ground quads, built with the apron).
        /// </summary>
        private void BuildSoulinariVenue(List<VertexPositionColor> verts, List<int> indices)
        {
            var random = new Random(1975);
            
            BuildFence(verts, indices);
            BuildScoreboard(verts, indices);
            BuildSponsorBanners();
            BuildMetalBleacher(verts, indices);
            BuildContainer(verts, indices);
            BuildChangingRooms(verts, indices);
            
            // Olive grove on the west side only
            for (int row = 0; row < 6; row++)
            {
                float offset = 8f + row * 7.5f;
                for (float t = -38f; t <= 38f; t += 8.5f)
                {
                    float jz = t + (float)(random.NextDouble() * 2.0 - 1.0) * 2.5f;
                    float jx = offset + (float)(random.NextDouble() * 2.0 - 1.0) * 2.5f;
                    TryAddOlive(verts, indices, random, -(FenceX + jx), jz);
                }
            }
            
            // Village houses with red roofs on the east side
            for (int i = 0; i < 6; i++)
                AddHouse(verts, indices, random,
                    68f + 22f * (float)random.NextDouble(),
                    -35f + 70f * (float)random.NextDouble());
            
            BuildBasketballCourt(verts, indices);
            
            // A few scattered trees on the east and south sides
            AddTree(verts, indices, random, 66f, 26f);
            AddTree(verts, indices, random, 67f, -18f);
            AddTree(verts, indices, random, 20f, 50f);
            AddTree(verts, indices, random, -28f, 49f);
            
            BuildFanBanners();
        }
        
        /// <summary>
        /// The small uncovered metal bleacher on the far (-Z) touchline: four
        /// light-gray seat planks on a darker metal frame, a thin railing along
        /// the back and stepped stairs at the east end.
        /// </summary>
        // Soulinari bleacher metrics - shared with FanSection (fans stand on the planks)
        public const float SoulinariBleacherFrontZ = -40.3f;  // just outside the far fence
        public const float SoulinariBleacherFirstRowY = 1.2f; // elevated frame, rises over the fence
        public const float SoulinariBleacherRowRise = 0.7f;
        public const float SoulinariBleacherRowDepth = 0.75f;
        public const float SoulinariBleacherCenterX = 8f;     // offset east of center
        public const float SoulinariBleacherHalfWidth = 11f;  // 22m wide
        public const int SoulinariBleacherRows = 4;

        private void BuildMetalBleacher(List<VertexPositionColor> verts, List<int> indices)
        {
            Color frame = new Color(120, 124, 130);
            Color plank = new Color(170, 174, 180);
            Color rail = new Color(90, 94, 100);

            const float hw = SoulinariBleacherHalfWidth;
            const float cx = SoulinariBleacherCenterX;
            const int rows = SoulinariBleacherRows;
            const float rowDepth = SoulinariBleacherRowDepth;
            const float frontZ = SoulinariBleacherFrontZ;
            const float firstRowY = SoulinariBleacherFirstRowY;
            const float rowRise = SoulinariBleacherRowRise;
            
            // Seat planks on support legs
            for (int row = 0; row < rows; row++)
            {
                float y = firstRowY + row * rowRise;
                float z = frontZ - row * rowDepth;
                AddBox(verts, indices,
                    new Vector3(cx - hw, y, z - rowDepth * 0.8f),
                    new Vector3(cx + hw, y + 0.07f, z), plank);
                for (float lx = cx - hw; lx <= cx + hw + 0.01f; lx += 3.7f)
                {
                    float px = Math.Min(lx, cx + hw);
                    AddBox(verts, indices,
                        new Vector3(px - 0.05f, 0f, z - 0.35f),
                        new Vector3(px + 0.05f, y, z - 0.25f), frame);
                }
            }
            
            // Thin railing along the back of the top row
            float topY = firstRowY + (rows - 1) * rowRise;
            float railZ = frontZ - (rows - 1) * rowDepth - 0.8f;
            for (float lx = cx - hw; lx <= cx + hw + 0.01f; lx += 2.2f)
            {
                float px = Math.Min(lx, cx + hw);
                AddBox(verts, indices,
                    new Vector3(px - 0.03f, topY, railZ - 0.03f),
                    new Vector3(px + 0.03f, topY + 1.1f, railZ + 0.03f), rail);
            }
            foreach (float y0 in new[] { topY + 0.55f, topY + 1.05f })
            {
                AddBox(verts, indices,
                    new Vector3(cx - hw, y0, railZ - 0.03f),
                    new Vector3(cx + hw, y0 + 0.05f, railZ + 0.03f), rail);
            }
            
            // Stepped side stairs at the east end
            for (int s = 0; s < rows; s++)
            {
                float sy = firstRowY - 0.3f + s * rowRise;
                AddBox(verts, indices,
                    new Vector3(cx + hw + 0.15f, 0f, frontZ - s * rowDepth - rowDepth),
                    new Vector3(cx + hw + 1.0f, sy, frontZ - s * rowDepth), frame);
            }
        }
        
        /// <summary>The dark green metal container/shed next to the bleacher.</summary>
        private void BuildContainer(List<VertexPositionColor> verts, List<int> indices)
        {
            Color green = new Color(35, 80, 50);
            Color dark = new Color(25, 60, 38);
            const float cx = -17f, cz = -42.5f, w = 6f, d = 2.5f, h = 2.6f;
            
            AddBox(verts, indices,
                new Vector3(cx - w / 2f, 0f, cz - d / 2f),
                new Vector3(cx + w / 2f, h, cz + d / 2f), green);
            // Darker top rim, suggesting the corrugated edge
            AddBox(verts, indices,
                new Vector3(cx - w / 2f - 0.03f, h - 0.15f, cz - d / 2f - 0.03f),
                new Vector3(cx + w / 2f + 0.03f, h + 0.06f, cz + d / 2f + 0.03f), dark);
        }
        
        /// <summary>
        /// The flat-roof changing-rooms building at the SE corner: beige walls,
        /// a gray roof slab with a small overhang, door and windows facing the pitch.
        /// </summary>
        private void BuildChangingRooms(List<VertexPositionColor> verts, List<int> indices)
        {
            Color body = new Color(225, 218, 200);
            Color roof = new Color(105, 105, 110);
            Color dark = new Color(70, 80, 90);
            const float cx = 42f, cz = 49f, w = 12f, d = 6f, h = 3.2f;
            
            AddBox(verts, indices,
                new Vector3(cx - w / 2f, 0f, cz - d / 2f),
                new Vector3(cx + w / 2f, h, cz + d / 2f), body);
            // Flat roof slab with a small overhang
            AddBox(verts, indices,
                new Vector3(cx - w / 2f - 0.3f, h, cz - d / 2f - 0.3f),
                new Vector3(cx + w / 2f + 0.3f, h + 0.25f, cz + d / 2f + 0.3f), roof);
            // Door + two windows on the pitch-facing (-Z) wall
            float faceZ = cz - d / 2f - 0.02f;
            AddQuad(verts, indices,
                new Vector3(cx - 1.6f, 0f, faceZ), new Vector3(cx - 0.6f, 0f, faceZ),
                new Vector3(cx - 0.6f, 2.1f, faceZ), new Vector3(cx - 1.6f, 2.1f, faceZ), dark);
            foreach (float wx in new[] { cx + 1.2f, cx + 3.6f })
            {
                AddQuad(verts, indices,
                    new Vector3(wx, 1.3f, faceZ), new Vector3(wx + 1.2f, 1.3f, faceZ),
                    new Vector3(wx + 1.2f, 2.3f, faceZ), new Vector3(wx, 2.3f, faceZ), dark);
            }
        }
        
        /// <summary>
        /// The basketball court at the NE corner (adapted from the tennis-court
        /// pattern): green surface with white markings on a dark surround and
        /// a hoop at each end.
        /// </summary>
        private void BuildBasketballCourt(List<VertexPositionColor> verts, List<int> indices)
        {
            Color surround = new Color(70, 70, 72);
            Color surface = new Color(55, 120, 75);
            Color lineColor = new Color(230, 230, 225);
            const float cx = 76f, cz = -51f;
            const float hw = 12f, hd = 7f;   // 24m x 14m playing surface
            
            AddGroundQuad(verts, indices, cx - hw - 2f, cz - hd - 2f, cx + hw + 2f, cz + hd + 2f, -0.005f, surround);
            AddGroundQuad(verts, indices, cx - hw, cz - hd, cx + hw, cz + hd, 0f, surface);
            // Boundary, halfway line and center circle
            AddRectOutline(verts, indices, cx - hw, cz - hd, cx + hw, cz + hd, 0.1f, lineColor);
            AddLineQuad(verts, indices, new Vector2(cx, cz - hd), new Vector2(cx, cz + hd), 0.1f, lineColor);
            AddRing(verts, indices, new Vector2(cx, cz), 1.8f, 0.1f, 16, lineColor);
            // Hoops at the two ends, rims facing into the court
            AddBasketballHoop(verts, indices, cx - hw - 0.9f, cz, 1f);
            AddBasketballHoop(verts, indices, cx + hw + 0.9f, cz, -1f);
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
            foreach (float y0 in new[] { _fenceHeight - railSize, 0.95f })
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
            const float y0p = 0.02f;
            float y1p = _fenceHeight - 0.1f;
            
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
                new Vector3(basePos.X + h, _fenceHeight, basePos.Z + h), color);
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

        /// <summary>Dark asphalt with subtle grain and worn patches for the street court.</summary>
        private static Texture2D CreateStreetCourtTexture(GraphicsDevice device)
        {
            const int size = 512;
            var texture = new Texture2D(device, size, size);
            var data = new Color[size * size];
            var random = new Random(7331);
            Color baseColor = new Color(52, 52, 58);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int grain = random.Next(-7, 8);
                    float patch = (float)(Math.Sin(x * 0.025f) * Math.Cos(y * 0.031f)) * 5f;
                    int v = grain + (int)patch;
                    data[y * size + x] = new Color(
                        Math.Clamp(baseColor.R + v, 0, 255),
                        Math.Clamp(baseColor.G + v, 0, 255),
                        Math.Clamp(baseColor.B + v, 0, 255));
                }
            }

            // A few lighter worn patches / repairs
            for (int i = 0; i < 14; i++)
            {
                int cx = random.Next(size), cy = random.Next(size);
                int rx = random.Next(10, 45), ry = random.Next(6, 26);
                int tint = random.Next(6, 16);
                for (int y = Math.Max(0, cy - ry); y < Math.Min(size, cy + ry); y++)
                {
                    for (int x = Math.Max(0, cx - rx); x < Math.Min(size, cx + rx); x++)
                    {
                        float dx = (x - cx) / (float)rx;
                        float dy = (y - cy) / (float)ry;
                        if (dx * dx + dy * dy > 1f) continue;
                        int idx = y * size + x;
                        Color p = data[idx];
                        data[idx] = new Color(
                            Math.Clamp(p.R + tint, 0, 255),
                            Math.Clamp(p.G + tint, 0, 255),
                            Math.Clamp(p.B + tint, 0, 255));
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>Packed dirt/gravel court with worn patches for Prosfygika.</summary>
        private static Texture2D CreateDirtCourtTexture(GraphicsDevice device)
        {
            const int size = 512;
            var texture = new Texture2D(device, size, size);
            var data = new Color[size * size];
            var random = new Random(1972);
            Color baseColor = new Color(124, 107, 84);

            for (int y = 0; y < size; y++)
            {
                for (int x = 0; x < size; x++)
                {
                    int grain = random.Next(-10, 11);
                    float patch = (float)(Math.Sin(x * 0.021f) * Math.Cos(y * 0.027f)) * 8f;
                    int v = grain + (int)patch;
                    data[y * size + x] = new Color(
                        Math.Clamp(baseColor.R + v, 0, 255),
                        Math.Clamp(baseColor.G + v, 0, 255),
                        Math.Clamp(baseColor.B + v, 0, 255));
                }
            }

            // Worn patches and damp spots
            for (int i = 0; i < 18; i++)
            {
                int cx = random.Next(size), cy = random.Next(size);
                int rx = random.Next(8, 50), ry = random.Next(5, 30);
                int tint = random.Next(-18, 19);
                for (int y = Math.Max(0, cy - ry); y < Math.Min(size, cy + ry); y++)
                {
                    for (int x = Math.Max(0, cx - rx); x < Math.Min(size, cx + rx); x++)
                    {
                        float dx = (x - cx) / (float)rx;
                        float dy = (y - cy) / (float)ry;
                        if (dx * dx + dy * dy > 1f) continue;
                        int idx = y * size + x;
                        Color p = data[idx];
                        data[idx] = new Color(
                            Math.Clamp(p.R + tint, 0, 255),
                            Math.Clamp(p.G + tint, 0, 255),
                            Math.Clamp(p.B + tint, 0, 255));
                    }
                }
            }

            texture.SetData(data);
            return texture;
        }

        /// <summary>
        /// Four aged plaster panels (512x256 each) for the Prosfygika blocks:
        /// water stains, grime, peeling plaster, cracks, bullet holes and
        /// ground-floor graffiti. Windows/balconies are 3D geometry on top.
        /// </summary>
        private static Texture2D CreateProsfygikaFacadeAtlas(GraphicsDevice device)
        {
            const int panelW = 512, panelH = 256, variants = 4;
            int width = panelW * variants;
            var data = new Color[width * panelH];
            var random = new Random(1944);

            Color[] bases =
            {
                new Color(205, 190, 165), // beige
                new Color(222, 211, 190), // cream
                new Color(190, 174, 148), // ochre
                new Color(176, 172, 164), // gray
            };

            void Blend(int ox, int lx, int ly, Color c, float a)
            {
                if (lx < 0 || lx >= panelW || ly < 0 || ly >= panelH) return;
                int i = ly * width + ox + lx;
                data[i] = Color.Lerp(data[i], c, a);
            }

            void Ellipse(int ox, int cx, int cy, int rx, int ry, Color c, float a)
            {
                for (int y = cy - ry; y <= cy + ry; y++)
                {
                    for (int x = cx - rx; x <= cx + rx; x++)
                    {
                        float dx = (x - cx) / (float)Math.Max(1, rx);
                        float dy = (y - cy) / (float)Math.Max(1, ry);
                        if (dx * dx + dy * dy > 1f) continue;
                        Blend(ox, x, y, c, a);
                    }
                }
            }

            for (int v = 0; v < variants; v++)
            {
                int ox = v * panelW;
                Color baseColor = bases[v];

                // Plaster grain + broad discoloration
                for (int y = 0; y < panelH; y++)
                {
                    for (int x = 0; x < panelW; x++)
                    {
                        int grain = random.Next(-9, 10);
                        float broad = (float)(Math.Sin(x * 0.02f + v) * Math.Cos(y * 0.035f)) * 6f;
                        int g = grain + (int)broad;
                        data[y * width + ox + x] = new Color(
                            Math.Clamp(baseColor.R + g, 0, 255),
                            Math.Clamp(baseColor.G + g, 0, 255),
                            Math.Clamp(baseColor.B + g, 0, 255));
                    }
                }

                // Large damp patches
                for (int i = 0; i < 8; i++)
                {
                    int cx = random.Next(panelW), cy = random.Next(panelH);
                    int rx = random.Next(20, 90), ry = random.Next(15, 60);
                    Color tint = random.Next(2) == 0 ? new Color(70, 64, 54) : new Color(235, 230, 215);
                    Ellipse(ox, cx, cy, rx, ry, tint, 0.10f);
                }

                // Vertical water streaks from the roof/balconies
                for (int i = 0; i < 9; i++)
                {
                    int sx = random.Next(panelW);
                    int len = random.Next(panelH / 2, panelH);
                    int w = random.Next(3, 10);
                    float a = 0.12f + (float)random.NextDouble() * 0.16f;
                    for (int y = 0; y < len; y++)
                    {
                        float fade = 1f - y / (float)Math.Max(1, len);
                        for (int x = sx; x < sx + w; x++)
                            Blend(ox, x, y, new Color(72, 65, 55), a * fade);
                    }
                }

                // Grime rising from the street
                for (int y = (int)(panelH * 0.66f); y < panelH; y++)
                {
                    float f = (y - panelH * 0.66f) / (panelH * 0.34f);
                    for (int x = 0; x < panelW; x++)
                        Blend(ox, x, y, new Color(62, 56, 47), 0.35f * f);
                }

                // Peeling plaster / exposed concrete patches
                for (int i = 0; i < 7; i++)
                {
                    int cx = random.Next(panelW), cy = random.Next(panelH);
                    int rx = random.Next(8, 30), ry = random.Next(5, 18);
                    Ellipse(ox, cx, cy, rx + 2, ry + 2, new Color(190, 184, 172), 0.30f);
                    Ellipse(ox, cx, cy, rx, ry,
                        random.Next(3) == 0 ? new Color(152, 98, 72) : new Color(150, 145, 135), 0.85f);
                }

                // Cracks
                for (int i = 0; i < 12; i++)
                {
                    int cx = random.Next(panelW);
                    int cy = random.Next(panelH / 3);
                    int steps = random.Next(18, 60);
                    for (int step = 0; step < steps; step++)
                    {
                        Blend(ox, cx, cy, new Color(68, 62, 55), 0.55f);
                        cx += random.Next(-1, 2);
                        cy += random.Next(0, 2);
                        if (cx < 0 || cx >= panelW || cy >= panelH) break;
                    }
                }

                // Bullet holes from the December 1944 fighting
                for (int i = 0; i < 26; i++)
                {
                    int cx = random.Next(panelW);
                    int cy = random.Next((int)(panelH * 0.72f));
                    int r = random.Next(1, 3);
                    for (int y = cy - r - 1; y <= cy + r + 1; y++)
                        for (int x = cx - r - 1; x <= cx + r + 1; x++)
                        {
                            float dx = x - cx, dy = y - cy;
                            float d = (float)Math.Sqrt(dx * dx + dy * dy);
                            if (d <= r + 0.9f) Blend(ox, x, y, new Color(195, 190, 180), 0.35f);
                            if (d <= r) Blend(ox, x, y, new Color(45, 42, 38), 0.85f);
                        }
                }

                // Faded ground-floor graffiti tags
                Color[] tags =
                {
                    new Color(190, 35, 45), new Color(30, 80, 180), new Color(40, 145, 80),
                    new Color(235, 180, 30), new Color(145, 60, 165),
                };
                for (int t = 0; t < 5; t++)
                {
                    Color tag = tags[random.Next(tags.Length)];
                    int ty = random.Next((int)(panelH * 0.78f), panelH - 10);
                    int tx = random.Next(panelW - 60);
                    int strokes = random.Next(2, 5);
                    for (int k = 0; k < strokes; k++)
                    {
                        int x0 = tx + random.Next(0, 45);
                        int y0 = ty + random.Next(-6, 6);
                        int len = random.Next(10, 35);
                        int th = random.Next(2, 6);
                        for (int y = y0; y < y0 + th; y++)
                            for (int x = x0; x < x0 + len; x++)
                                Blend(ox, x, y, tag, 0.45f);
                    }
                }
            }

            var texture = new Texture2D(device, width, panelH);
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
            Color trunk = new Color(92, 66, 45);
            Color trunkDark = new Color(74, 52, 36);

            if (random.Next(2) == 0)
            {
                // Cypress: tapered trunk + five organic, slightly offset cone tiers
                float h = (5.6f + 2.2f * (float)random.NextDouble()) * scale;
                AddCylinder(verts, indices,
                    new Vector3(x, 0f, z), new Vector3(x, h * 0.38f, z),
                    0.18f * scale, 0.10f * scale, trunkDark, 6);

                float y = h * 0.30f;
                float r = 0.95f * scale;
                for (int i = 0; i < 5; i++)
                {
                    float tierH = h * (0.40f - i * 0.040f);
                    float ox = ((i & 1) == 0 ? 0.05f : -0.05f) * scale;
                    float oz = ((i % 3) == 0 ? 0.04f : -0.04f) * scale;
                    int jitter = random.Next(-7, 8);
                    Color c = new Color(
                        Math.Clamp(30 + jitter + i * 4, 0, 255),
                        Math.Clamp(70 + jitter + i * 3, 0, 255),
                        Math.Clamp(36 + jitter, 0, 255));
                    AddCone(verts, indices, new Vector3(x + ox, y, z + oz), r, tierH, c, 7);
                    y += tierH * 0.48f;
                    r *= 0.70f;
                }
            }
            else
            {
                // Olive: gnarled forked trunk + a silvery-green crown of blobs
                float h = (2.2f + 1.0f * (float)random.NextDouble()) * scale;
                Vector3 fork = new Vector3(x + 0.10f * scale, h * 0.55f, z - 0.06f * scale);
                AddCylinder(verts, indices, new Vector3(x, 0f, z), fork,
                    0.22f * scale, 0.14f * scale, trunk, 6);
                AddCylinder(verts, indices, fork,
                    fork + new Vector3(-0.55f, 0.75f, 0.18f) * scale,
                    0.11f * scale, 0.06f * scale, trunkDark, 5);
                AddCylinder(verts, indices, fork,
                    fork + new Vector3(0.42f, 0.82f, -0.30f) * scale,
                    0.10f * scale, 0.05f * scale, trunkDark, 5);

                Color foliage = new Color(95, 112, 66);
                int blobs = 4 + random.Next(3);
                for (int i = 0; i < blobs; i++)
                {
                    float ox = (float)(random.NextDouble() * 2.0 - 1.0) * 1.0f * scale;
                    float oz = (float)(random.NextDouble() * 2.0 - 1.0) * 1.0f * scale;
                    float oy = (1.7f + (float)random.NextDouble() * 0.9f) * scale;
                    float r = (0.85f + 0.55f * (float)random.NextDouble()) * scale;
                    int jitter = random.Next(-12, 13);
                    Color c = new Color(
                        Math.Clamp(foliage.R + jitter, 0, 255),
                        Math.Clamp(foliage.G + jitter, 0, 255),
                        Math.Clamp(foliage.B + jitter, 0, 255));
                    AddSphere(verts, indices, new Vector3(x + ox, oy, z + oz), r, c, 7, 4);
                }
            }

            AddTreeShadow(x, z, 1.15f * scale);
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
        
        /// <summary>
        /// Bakes a cheap key-light + height ambient occlusion into a vertex color.
        /// The world uses flat unlit BasicEffect draws, so this is what gives the
        /// low-poly geometry form and depth without a normal-enabled vertex format.
        /// Uses |N·L| so it works for both sides of one-sided quads (CullNone).
        /// </summary>
        private static Color ShadeColor(Color color, Vector3 normal, float height)
        {
            if (normal.LengthSquared() > 1e-8f) normal.Normalize();
            float ndl = Math.Abs(Vector3.Dot(normal, BakeSunDir));
            float light = 0.72f + 0.33f * ndl;
            float ao = 0.90f + 0.10f * MathHelper.Clamp(height / 2.5f, 0f, 1f);
            float f = light * ao;
            return new Color(
                Math.Clamp((int)(color.R * f), 0, 255),
                Math.Clamp((int)(color.G * f), 0, 255),
                Math.Clamp((int)(color.B * f), 0, 255),
                color.A);
        }

        private static void AddQuad(List<VertexPositionColor> verts, List<int> indices,
            Vector3 a, Vector3 b, Vector3 c, Vector3 d, Color color, bool shade = true)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Color ca = shade ? ShadeColor(color, normal, a.Y) : color;
            Color cb = shade ? ShadeColor(color, normal, b.Y) : color;
            Color cc = shade ? ShadeColor(color, normal, c.Y) : color;
            Color cd = shade ? ShadeColor(color, normal, d.Y) : color;

            int baseIndex = verts.Count;
            verts.Add(new VertexPositionColor(a, ca));
            verts.Add(new VertexPositionColor(b, cb));
            verts.Add(new VertexPositionColor(c, cc));
            verts.Add(new VertexPositionColor(d, cd));
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
                new Vector3(x0, y, z1), color, shade: false);
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
                new Vector3(from.X + perp.X, LineY, from.Y + perp.Y), color,
                shade: false);
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
            Vector3 a, Vector3 b, Vector3 c, Color color, bool shade = true)
        {
            Vector3 normal = Vector3.Cross(b - a, c - a);
            Color ca = shade ? ShadeColor(color, normal, a.Y) : color;
            Color cb = shade ? ShadeColor(color, normal, b.Y) : color;
            Color cc = shade ? ShadeColor(color, normal, c.Y) : color;

            int baseIndex = verts.Count;
            verts.Add(new VertexPositionColor(a, ca));
            verts.Add(new VertexPositionColor(b, cb));
            verts.Add(new VertexPositionColor(c, cc));
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
                    Vector3 p = center + new Vector3(
                        (float)Math.Cos(phi) * ringRadius, y, (float)Math.Sin(phi) * ringRadius);
                    // Per-vertex normal => rounded, lit foliage instead of flat blobs
                    verts.Add(new VertexPositionColor(p, ShadeColor(color, p - center, p.Y)));
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
        
        /// <summary>Low-poly cone (used for conifer/cypress foliage tiers).</summary>
        private static void AddCone(List<VertexPositionColor> verts, List<int> indices,
            Vector3 baseCenter, float radius, float height, Color color, int segments = 7, bool cap = true)
        {
            Vector3 apex = baseCenter + new Vector3(0f, height, 0f);
            for (int i = 0; i < segments; i++)
            {
                float a0 = MathHelper.TwoPi * i / segments;
                float a1 = MathHelper.TwoPi * (i + 1) / segments;
                Vector3 p0 = baseCenter + new Vector3((float)Math.Cos(a0) * radius, 0f, (float)Math.Sin(a0) * radius);
                Vector3 p1 = baseCenter + new Vector3((float)Math.Cos(a1) * radius, 0f, (float)Math.Sin(a1) * radius);
                AddTriangle(verts, indices, p0, p1, apex, color);
                if (cap)
                    AddTriangle(verts, indices, baseCenter, p1, p0, color);
            }
        }

        /// <summary>
        /// Low-poly tapered cylinder between two points (trunks, branches, tiers).
        /// Radii can differ, so it doubles as a frustum.
        /// </summary>
        private static void AddCylinder(List<VertexPositionColor> verts, List<int> indices,
            Vector3 p0, Vector3 p1, float r0, float r1, Color color, int segments = 6, bool caps = true)
        {
            Vector3 axis = p1 - p0;
            if (axis.LengthSquared() < 1e-6f) return;
            axis.Normalize();
            Vector3 upRef = Math.Abs(axis.Y) > 0.9f ? Vector3.UnitX : Vector3.UnitY;
            Vector3 right = Vector3.Normalize(Vector3.Cross(axis, upRef));
            Vector3 fwd = Vector3.Normalize(Vector3.Cross(right, axis));

            for (int i = 0; i < segments; i++)
            {
                float a0 = MathHelper.TwoPi * i / segments;
                float a1 = MathHelper.TwoPi * (i + 1) / segments;
                Vector3 dir0 = right * (float)Math.Cos(a0) + fwd * (float)Math.Sin(a0);
                Vector3 dir1 = right * (float)Math.Cos(a1) + fwd * (float)Math.Sin(a1);
                Vector3 b0 = p0 + dir0 * r0;
                Vector3 b1 = p0 + dir1 * r0;
                Vector3 t0 = p1 + dir0 * r1;
                Vector3 t1 = p1 + dir1 * r1;
                AddQuad(verts, indices, b0, b1, t1, t0, color);
                if (caps && r1 > 0.002f)
                    AddTriangle(verts, indices, p1, t0, t1, color);
                if (caps && r0 > 0.002f)
                    AddTriangle(verts, indices, p0, b1, b0, color);
            }
        }

        /// <summary>
        /// A drooping palm frond: a shallow V-shaped ribbon along a 4-point curve,
        /// widest in the middle and tapering to the tip.
        /// </summary>
        private static void AddFrond(List<VertexPositionColor> verts, List<int> indices,
            Vector3 p0, Vector3 p1, Vector3 p2, Vector3 p3, Vector3 dir, Color color)
        {
            Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.Up, dir));
            Vector3 up = Vector3.Up * 0.06f;
            Vector3[] pts = { p0, p1, p2, p3 };
            float[] widths = { 0.09f, 0.30f, 0.23f, 0.02f };

            for (int i = 0; i < 3; i++)
            {
                Vector3 l0 = pts[i] - side * widths[i];
                Vector3 m0 = pts[i] + up;
                Vector3 r0 = pts[i] + side * widths[i];
                Vector3 l1 = pts[i + 1] - side * widths[i + 1];
                Vector3 m1 = pts[i + 1] + up;
                Vector3 r1 = pts[i + 1] + side * widths[i + 1];
                AddQuad(verts, indices, l0, m0, m1, l1, color);
                AddQuad(verts, indices, m0, r0, r1, m1, color);
            }
        }

        /// <summary>Two overlapping soft ellipses give a tree a grounded shadow.</summary>
        private void AddTreeShadow(float x, float z, float radius)
        {
            AddShadowEllipse(x - 0.12f, z + 0.10f, radius * 1.15f, radius * 0.95f, 42);
            AddShadowEllipse(x - 0.07f, z + 0.06f, radius * 0.85f, radius * 0.70f, 70);
        }

        private void AddShadowEllipse(float x, float z, float rx, float rz, int alpha, int segments = 10)
        {
            int baseIndex = _shadowVertList.Count;
            Color color = new Color(0, 0, 0, alpha);
            _shadowVertList.Add(new VertexPositionColor(new Vector3(x, ShadowY, z), color));
            for (int i = 0; i < segments; i++)
            {
                float a = MathHelper.TwoPi * i / segments;
                _shadowVertList.Add(new VertexPositionColor(
                    new Vector3(x + (float)Math.Cos(a) * rx, ShadowY, z + (float)Math.Sin(a) * rz),
                    color));
            }
            for (int i = 0; i < segments; i++)
            {
                _shadowIndexList.Add(baseIndex);
                _shadowIndexList.Add(baseIndex + 1 + i);
                _shadowIndexList.Add(baseIndex + 1 + (i + 1) % segments);
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
