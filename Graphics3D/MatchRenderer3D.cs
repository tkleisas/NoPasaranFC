using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Graphics3D.Skinning;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Optional 3D match renderer. Draws the pitch world, ball and players.
    /// Players are rigged, GPU-skinned 3D models (KayKit Knight GLB) when the
    /// model file loads; otherwise it falls back to the original camera-facing
    /// billboards using the kit sprite sheets. Additive alternative to the
    /// 2D rendering in MatchScreen - the simulation is untouched.
    /// </summary>
    public class MatchRenderer3D
    {
        private readonly Camera3D _camera;
        private readonly World3D _world;
        private readonly Ball3D _ball;
        private readonly BasicEffect _billboardEffect;
        private readonly BasicEffect _ringEffect;
        private Dictionary<string, Texture2D> _spriteSheets;
        private readonly Dictionary<Player, BillboardAnimState> _animStates = new Dictionary<Player, BillboardAnimState>();
        
        // Match environment (day/night lighting + weather), cloth nets, rain
        private readonly MatchEnvironment _environment;
        private readonly GoalNet3D[] _nets;
        private readonly RainSystem _rain;

        // Skinned 3D players (null => billboard fallback)
        private SkinnedModel _playerModel;
        private readonly Dictionary<Player, PlayerAnimator> _playerAnimators = new Dictionary<Player, PlayerAnimator>();
        private readonly HashSet<Player> _kitsApplied = new HashSet<Player>();

        // KayKit chibi is ~2.3 units tall; scale to ~1.7m
        private const float PlayerModelScale = 0.75f;
        // Subtle red-ish multiply tint for the away team (SkinnedEffect.DiffuseColor)
        private static readonly Color AwayTeamTint = new Color(255, 140, 140);

        // Controlled player indicator ring (unit annulus in the XZ plane)
        private VertexPositionColor[] _ringVertices;
        private int[] _ringIndices;

        // Billboard dimensions (meters)
        private const float PlayerHeight = 1.8f;
        private const float PlayerHalfWidth = 0.45f;
        private const int SpriteSize = 64;     // Kit sheets are 64x64 frames (4x12 grid)
        private const int SpritesPerRow = 4;
        
        // Animation frame table - mirrors PlayerAnimationSystem.InitializeSharedAnimations()
        // (the 2D system keeps its frame state private, so the minimal selection
        //  logic is duplicated here without touching the 2D code)
        private class BillboardAnimation
        {
            public int[] SpriteIndices;
            public float FrameDuration;
            public bool Loop;
        }
        
        private static readonly Dictionary<string, BillboardAnimation> _animations =
            new Dictionary<string, BillboardAnimation>
            {
                ["idle"] = new BillboardAnimation { SpriteIndices = new[] { 0, 1, 2, 3 }, FrameDuration = 0.2f, Loop = true },
                ["walk"] = new BillboardAnimation { SpriteIndices = new[] { 8, 9, 10, 11 }, FrameDuration = 0.12f, Loop = true },
                ["fall"] = new BillboardAnimation { SpriteIndices = new[] { 16, 17, 18, 19 }, FrameDuration = 0.15f, Loop = false },
                ["shoot"] = new BillboardAnimation { SpriteIndices = new[] { 12, 13, 14, 15 }, FrameDuration = 0.1f, Loop = false },
                ["tackle"] = new BillboardAnimation { SpriteIndices = new[] { 28, 29, 30, 31 }, FrameDuration = 0.1f, Loop = false },
                ["celebrate"] = new BillboardAnimation { SpriteIndices = new[] { 32, 33, 34, 35 }, FrameDuration = 0.15f, Loop = true },
                ["throw_in_static"] = new BillboardAnimation { SpriteIndices = new[] { 36 }, FrameDuration = 0.2f, Loop = false },
                ["throw_in_throw"] = new BillboardAnimation { SpriteIndices = new[] { 36, 37 }, FrameDuration = 0.15f, Loop = false },
            };
        
        private class BillboardAnimState
        {
            public string AnimationName = "";
            public float Timer;
            public int FrameIndex;
        }
        
        public MatchRenderer3D(GraphicsDevice device, ContentManager content)
        {
            _camera = new Camera3D(Game1.ScreenWidth, Game1.ScreenHeight);
            _world = new World3D(device);
            _ball = new Ball3D(device);
            
            _billboardEffect = new BasicEffect(device)
            {
                TextureEnabled = true,
                VertexColorEnabled = false,
                LightingEnabled = false
            };
            
            _ringEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
            
            LoadSpriteSheets(content);
            BuildRing();
            TryLoadSkinnedPlayerModel(device);
            
            // Resolve TimeOfDay/Weather (Random resolved once per match) and
            // apply the lighting preset to all static world effects
            _environment = new MatchEnvironment(device,
                GameSettings.Instance.TimeOfDay, GameSettings.Instance.Weather);
            _world.ApplyEnvironment(_environment);
            _ball.ApplyEnvironment(_environment);
            
            // Dynamic cloth nets replace the old static net quads
            float halfLength = WorldUnits.PitchLengthMeters / 2f;
            _nets = new[]
            {
                new GoalNet3D(device, -halfLength, -1f),
                new GoalNet3D(device, halfLength, 1f),
            };
            foreach (var net in _nets)
                net.ApplyEnvironment(_environment);
            
            _rain = _environment.IsRaining ? new RainSystem(device) : null;
        }

        /// <summary>
        /// Loads the rigged player model (raw GLB file, not the XNB pipeline).
        /// Prefers the purpose-built soccer player (Player.glb, dedicated kit
        /// regions); falls back to the KayKit Knight, then to billboards.
        /// </summary>
        private void TryLoadSkinnedPlayerModel(GraphicsDevice device)
        {
            foreach (string fileName in new[] { "Player.glb", "Knight.glb" })
            {
                try
                {
#if ANDROID
                    var context = global::Android.App.Application.Context;
                    using (var stream = context.Assets.Open($"Content/Models3D/{fileName}"))
                        _playerModel = SkinnedModel.Load(device, stream);
#else
                    string glbPath = PlatformHelper.GetAssetPath(Path.Combine("Content", "Models3D", fileName));
                    if (!File.Exists(glbPath)) continue;
                    _playerModel = SkinnedModel.Load(device, glbPath);
#endif
                    _playerModelKind = fileName.StartsWith("Player") ? PlayerModelKind.SoccerPlayer : PlayerModelKind.Knight;
                    return;
                }
                catch (Exception ex)
                {
                    Debug.WriteLine($"MatchRenderer3D: failed to load {fileName} ({ex.Message}).");
                }
            }
            Debug.WriteLine("MatchRenderer3D: no player model found - using billboard players.");
            _playerModel = null;
        }
        
        private enum PlayerModelKind { Knight, SoccerPlayer }
        private PlayerModelKind _playerModelKind;
        
        private void LoadSpriteSheets(ContentManager content)
        {
            // Same kit sheets as PlayerAnimationSystem.LoadSharedResources
            _spriteSheets = new Dictionary<string, Texture2D>
            {
                ["player_red_multi"] = content.Load<Texture2D>("Sprites/player_red_multi"),
                ["player_blue_multi"] = content.Load<Texture2D>("Sprites/player_blue_multi"),
                ["no_pasaran_kit"] = content.Load<Texture2D>("Sprites/no_pasaran_kit"),
                ["asalagitos_kit"] = content.Load<Texture2D>("Sprites/asalagitos_kit"),
                ["tiganitis_kit"] = content.Load<Texture2D>("Sprites/tiganitis_kit"),
                ["asteras_exarchion_kit"] = content.Load<Texture2D>("Sprites/asteras_exarchion_kit"),
                ["chandrinaikos_kit"] = content.Load<Texture2D>("Sprites/chandrinaikos_kit"),
            };
        }
        
        public void Update(GameTime gameTime, MatchEngine engine)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            _camera.UpdateViewport(Game1.ScreenWidth, Game1.ScreenHeight);
            _camera.Follow(engine.BallPosition, dt);
            _ball.Update(engine.BallPosition, engine.BallVelocity, engine.BallHeight, dt);
            
            _world.Update(dt);
            
            // Cloth nets react to the ball when it's inside the goal volume
            Vector3 ballWorld = WorldUnits.ToWorld(engine.BallPosition, engine.BallHeight)
                + new Vector3(0f, Ball3D.RadiusMeters, 0f);
            Vector3 ballVelWorld = new Vector3(engine.BallVelocity.X, 0f, engine.BallVelocity.Y)
                / WorldUnits.PixelsPerMeter;
            foreach (var net in _nets)
                net.Update(dt, ballWorld, ballVelWorld);
            
            _rain?.Update(dt, _camera.Target);
            
            if (_playerModel != null)
                UpdatePlayerAnimators(engine, dt);
            else
                UpdateBillboardAnimations(engine, dt);
        }
        
        public void Draw(GraphicsDevice device, MatchEngine engine, int homeTeamId)
        {
            // Full clear (color + depth) - 3D scene replaces the 2D world entirely
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer,
                _environment.SkyColor, 1f, 0);
            
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            
            _world.Draw(device, _camera.View, _camera.Projection);
            _environment.Draw(device, _camera.View, _camera.Projection); // Floodlights (night only)
            foreach (var net in _nets)
                net.Draw(device, _camera.View, _camera.Projection);
            _ball.Draw(device, _camera.View, _camera.Projection);
            DrawPlayers(device, engine, homeTeamId);
            _rain?.Draw(device, _camera.View, _camera.Projection);
            
            // Restore GraphicsDevice states for SpriteBatch (HUD drawn after us)
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearClamp;
        }
        
        #region Players (skinned 3D / billboard fallback)
        
        private void UpdatePlayerAnimators(MatchEngine engine, float dt)
        {
            foreach (var player in engine.GetAllPlayers())
            {
                if (!_playerAnimators.TryGetValue(player, out var animator))
                {
                    animator = new PlayerAnimator(_playerModel);
                    _playerAnimators[player] = animator;
                }
                animator.Update(player, dt);
            }
        }
        
        private void DrawPlayers(GraphicsDevice device, MatchEngine engine, int homeTeamId)
        {
            _ringEffect.View = _camera.View;
            _ringEffect.Projection = _camera.Projection;
            
            if (_playerModel != null)
                DrawSkinnedPlayers(device, engine, homeTeamId);
            else
                DrawBillboardPlayers(device, engine, homeTeamId);
            
            // Yellow ring on the ground under the controlled player
            // (CullNone: the skinned instances leave CullClockwise active and the
            //  ring's winding is not guaranteed)
            device.RasterizerState = RasterizerState.CullNone;
            foreach (var player in engine.GetAllPlayers())
            {
                if (player.IsControlled && !player.IsKnockedDown)
                {
                    Vector3 pos = WorldUnits.ToWorld(player.FieldPosition);
                    // Gentle pulse so the indicator reads clearly at broadcast distance
                    float pulse = 1.1f + 0.08f * (float)Math.Sin(Environment.TickCount64 / 150.0);
                    _ringEffect.World = Matrix.CreateScale(pulse * 1.15f, 1f, pulse * 1.15f)
                        * Matrix.CreateTranslation(pos + new Vector3(0, 0.03f, 0));
                    foreach (var pass in _ringEffect.CurrentTechnique.Passes)
                    {
                        pass.Apply();
                        device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                            _ringVertices, 0, _ringVertices.Length,
                            _ringIndices, 0, _ringIndices.Length / 3);
                    }
                }
            }
        }
        
        /// <summary>
        /// Project an engine-space field position (px) to screen coordinates,
        /// for HUD indicators (stamina bars, shot power). heightPx lifts the point
        /// above the ground (e.g. above the player's head). Null when off-camera.
        /// </summary>
        public Vector2? WorldToScreen(Vector2 fieldPosPx, float heightPx = 0f)
        {
            return _camera.WorldToScreen(WorldUnits.ToWorld(fieldPosPx, heightPx));
        }
        
        private void DrawSkinnedPlayers(GraphicsDevice device, MatchEngine engine, int homeTeamId)
        {
            // Skinned players are opaque: default depth state, no alpha sorting needed.
            // (Instance.Draw sets its own rasterizer/depth/blend states.)
            foreach (var player in engine.GetAllPlayers())
            {
                if (!_playerAnimators.TryGetValue(player, out var animator))
                    continue;
                
                // Per-team kit textures (shirt/shorts/socks recolor), applied once per player
                if (_kitsApplied.Add(player))
                    ApplyKit(device, player, homeTeamId, animator);
                
                Vector3 pos = WorldUnits.ToWorld(player.FieldPosition);
                
                animator.Instance.Environment = _environment;
                
                Matrix world = Matrix.CreateScale(PlayerModelScale)
                    * Matrix.CreateRotationY(animator.Yaw)
                    * Matrix.CreateTranslation(pos);
                
                animator.Instance.Draw(device, world, _camera.View, _camera.Projection);
            }
        }
        
        /// <summary>
        /// Recolors the player's clothing parts to the team kit. SoccerPlayer model:
        /// dedicated texture regions per garment (shirt/shorts/socks). Knight model:
        /// armor palette columns (Body/Arms/Helmet = shirt, Legs = shorts).
        /// Skin, hair and boots are never touched. Kit colors mirror the 2D kit
        /// sprite sheets (falling back to home blue / away red without a named kit).
        /// </summary>
        private void ApplyKit(GraphicsDevice device, Player player, int homeTeamId, PlayerAnimator animator)
        {
            GetKitColors(player, homeTeamId, out Color shirt, out Color shorts, out Color socks);
            Texture2D baseTexture = _playerModel.Parts[0].Texture;
            
            if (_playerModelKind == PlayerModelKind.SoccerPlayer)
            {
                // player_atlas.png layout (512x512): quadrants shirt / shorts / socks / skin+extras
                Texture2D shirtTexture = KitTextureFactory.GetKitTexture(device, baseTexture, shirt,
                    new Rectangle(0, 0, 256, 256));
                Texture2D shortsTexture = KitTextureFactory.GetKitTexture(device, baseTexture, shorts,
                    new Rectangle(256, 0, 256, 256));
                Texture2D socksTexture = KitTextureFactory.GetKitTexture(device, baseTexture, socks,
                    new Rectangle(0, 256, 256, 256));
                
                foreach (var part in _playerModel.Parts)
                {
                    string name = part.Name ?? "";
                    if (name == "Soccer_Shirt")
                        animator.Instance.SetPartTexture(part.Name, shirtTexture);
                    else if (name == "Soccer_Shorts")
                        animator.Instance.SetPartTexture(part.Name, shortsTexture);
                    else if (name.StartsWith("Soccer_Sock"))
                        animator.Instance.SetPartTexture(part.Name, socksTexture);
                }
            }
            else
            {
                Texture2D shirtTexture = KitTextureFactory.GetKitTexture(device, baseTexture, shirt);
                Texture2D shortsTexture = KitTextureFactory.GetKitTexture(device, baseTexture, shorts);
                
                foreach (var part in _playerModel.Parts)
                {
                    string name = part.Name ?? "";
                    if (name.Contains("Body") || name.Contains("Arm") || name.Contains("Helmet"))
                        animator.Instance.SetPartTexture(part.Name, shirtTexture);
                    else if (name.Contains("Leg"))
                        animator.Instance.SetPartTexture(part.Name, shortsTexture);
                }
            }
        }
        
        /// <summary>Shirt/shorts/socks colors per team, sampled from the 2D kit sprite sheets.</summary>
        private static void GetKitColors(Player player, int homeTeamId, out Color shirt, out Color shorts, out Color socks)
        {
            switch (player.Team?.KitName)
            {
                case "no_pasaran_kit":
                    shirt = new Color(224, 0, 0); shorts = new Color(240, 240, 240); socks = new Color(224, 0, 0);
                    break;
                case "asalagitos_kit":
                    shirt = new Color(128, 96, 160); shorts = new Color(50, 40, 70); socks = new Color(128, 96, 160);
                    break;
                case "asteras_exarcheion_kit":
                    shirt = new Color(35, 35, 40); shorts = new Color(35, 35, 40); socks = new Color(35, 35, 40);
                    break;
                case "chandrinaikos_kit":
                    shirt = new Color(0, 64, 160); shorts = new Color(240, 240, 240); socks = new Color(0, 64, 160);
                    break;
                case "tiganitis_kit":
                    shirt = new Color(240, 240, 240); shorts = new Color(35, 35, 40); socks = new Color(224, 160, 0);
                    break;
                default:
                    // No named kit: home blue / away red (player_*_multi defaults)
                    if (player.TeamId == homeTeamId)
                    {
                        shirt = new Color(0, 64, 160); shorts = new Color(240, 240, 240); socks = new Color(0, 64, 160);
                    }
                    else
                    {
                        shirt = new Color(128, 0, 0); shorts = new Color(35, 35, 40); socks = new Color(128, 0, 0);
                    }
                    break;
            }
        }
        
        #endregion
        
        #region Players (billboards)
        
        private void UpdateBillboardAnimations(MatchEngine engine, float dt)
        {
            foreach (var player in engine.GetAllPlayers())
            {
                if (!_animStates.TryGetValue(player, out var state))
                {
                    state = new BillboardAnimState();
                    _animStates[player] = state;
                }
                
                string animName = player.CurrentAnimationState ?? "walk";
                if (!_animations.ContainsKey(animName))
                {
                    animName = "idle";
                }
                
                if (state.AnimationName != animName)
                {
                    state.AnimationName = animName;
                    state.Timer = 0f;
                    state.FrameIndex = 0;
                }
                
                var anim = _animations[animName];
                state.Timer += dt;
                while (state.Timer >= anim.FrameDuration)
                {
                    state.Timer -= anim.FrameDuration;
                    state.FrameIndex++;
                    if (state.FrameIndex >= anim.SpriteIndices.Length)
                    {
                        state.FrameIndex = anim.Loop ? 0 : anim.SpriteIndices.Length - 1;
                    }
                }
            }
        }
        
        private Texture2D GetSheetForPlayer(Player player, int homeTeamId)
        {
            bool isHomeTeam = player.TeamId == homeTeamId;
            string sheetName = !string.IsNullOrEmpty(player.Team?.KitName)
                ? player.Team.KitName
                : (isHomeTeam ? "player_blue_multi" : "player_red_multi");
            
            if (!_spriteSheets.ContainsKey(sheetName))
            {
                sheetName = isHomeTeam ? "player_blue_multi" : "player_red_multi";
            }
            return _spriteSheets[sheetName];
        }
        
        private int GetCurrentSpriteIndex(Player player)
        {
            if (_animStates.TryGetValue(player, out var state) &&
                _animations.TryGetValue(state.AnimationName, out var anim))
            {
                return anim.SpriteIndices[Math.Min(state.FrameIndex, anim.SpriteIndices.Length - 1)];
            }
            return 0;
        }
        
        private void DrawBillboardPlayers(GraphicsDevice device, MatchEngine engine, int homeTeamId)
        {
            // Sort far-to-near for correct alpha blending
            var players = engine.GetAllPlayers()
                .OrderByDescending(p => (WorldUnits.ToWorld(p.FieldPosition) - _camera.Position).LengthSquared())
                .ToList();
            
            device.BlendState = BlendState.AlphaBlend;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            
            _billboardEffect.View = _camera.View;
            _billboardEffect.Projection = _camera.Projection;
            _billboardEffect.World = Matrix.Identity;
            
            foreach (var player in players)
            {
                Vector3 pos = WorldUnits.ToWorld(player.FieldPosition);
                Texture2D sheet = GetSheetForPlayer(player, homeTeamId);
                
                // Source frame in the kit sheet (4x12 grid of 64x64 frames)
                int spriteIndex = GetCurrentSpriteIndex(player);
                int col = spriteIndex % SpritesPerRow;
                int row = spriteIndex / SpritesPerRow;
                float u0 = col * SpriteSize / (float)sheet.Width;
                float u1 = (col + 1) * SpriteSize / (float)sheet.Width;
                float v0 = row * SpriteSize / (float)sheet.Height;
                float v1 = (row + 1) * SpriteSize / (float)sheet.Height;
                
                // Cylindrical billboard: faces the camera around the Y axis only
                Vector3 forward = pos - _camera.Position;
                forward.Y = 0f;
                if (forward.LengthSquared() < 0.0001f) forward = new Vector3(0, 0, 1);
                forward.Normalize();
                Vector3 right = Vector3.Normalize(Vector3.Cross(Vector3.Up, forward));
                
                Vector3 bottomLeft = pos - right * PlayerHalfWidth;
                Vector3 bottomRight = pos + right * PlayerHalfWidth;
                Vector3 topLeft = bottomLeft + new Vector3(0, PlayerHeight, 0);
                Vector3 topRight = bottomRight + new Vector3(0, PlayerHeight, 0);
                
                var verts = new[]
                {
                    new VertexPositionTexture(bottomLeft, new Vector2(u0, v1)),
                    new VertexPositionTexture(bottomRight, new Vector2(u1, v1)),
                    new VertexPositionTexture(topRight, new Vector2(u1, v0)),
                    new VertexPositionTexture(bottomLeft, new Vector2(u0, v1)),
                    new VertexPositionTexture(topRight, new Vector2(u1, v0)),
                    new VertexPositionTexture(topLeft, new Vector2(u0, v0)),
                };
                
                // Tints match the 2D view: gray for knocked down, light yellow for
                // controlled; all multiplied by the environment (day/night) tint
                if (player.IsKnockedDown)
                {
                    _billboardEffect.DiffuseColor = _environment.ApplyTint(new Color(180, 180, 180).ToVector3());
                    _billboardEffect.Alpha = 0.7f;
                }
                else if (player.IsControlled)
                {
                    _billboardEffect.DiffuseColor = _environment.ApplyTint(new Color(255, 255, 150).ToVector3());
                    _billboardEffect.Alpha = 1f;
                }
                else
                {
                    _billboardEffect.DiffuseColor = _environment.ApplyTint(Color.White.ToVector3());
                    _billboardEffect.Alpha = 1f;
                }
                
                _billboardEffect.Texture = sheet;
                foreach (var pass in _billboardEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    device.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, 2);
                }
            }
        }
        
        private void BuildRing()
        {
            // Unit annulus in the XZ plane, drawn at ~player scale (unit = 1.15m)
            const int segments = 24;
            const float inner = 0.75f;
            const float outer = 1.0f;
            Color ringColor = Color.Yellow;
            
            var verts = new List<VertexPositionColor>();
            var indices = new List<int>();
            
            for (int i = 0; i < segments; i++)
            {
                float angle1 = MathHelper.TwoPi * i / segments;
                float angle2 = MathHelper.TwoPi * (i + 1) / segments;
                
                int baseIndex = verts.Count;
                verts.Add(new VertexPositionColor(
                    new Vector3((float)Math.Cos(angle1) * inner, 0f, (float)Math.Sin(angle1) * inner), ringColor));
                verts.Add(new VertexPositionColor(
                    new Vector3((float)Math.Cos(angle1) * outer, 0f, (float)Math.Sin(angle1) * outer), ringColor));
                verts.Add(new VertexPositionColor(
                    new Vector3((float)Math.Cos(angle2) * inner, 0f, (float)Math.Sin(angle2) * inner), ringColor));
                verts.Add(new VertexPositionColor(
                    new Vector3((float)Math.Cos(angle2) * outer, 0f, (float)Math.Sin(angle2) * outer), ringColor));
                
                indices.Add(baseIndex + 0);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 2);
                indices.Add(baseIndex + 1);
                indices.Add(baseIndex + 3);
            }
            
            _ringVertices = verts.ToArray();
            _ringIndices = indices.ToArray();
        }
        
        #endregion
    }
}
