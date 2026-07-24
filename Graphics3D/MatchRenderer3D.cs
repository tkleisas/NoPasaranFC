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
        private SkinnedModel _playerModelF; // female variant (optional, same skeleton/atlas)
        private readonly Dictionary<Player, PlayerAnimator> _playerAnimators = new Dictionary<Player, PlayerAnimator>();
        private readonly Dictionary<Player, SkinnedModel> _playerModelChoice = new Dictionary<Player, SkinnedModel>();
        private readonly HashSet<Player> _kitsApplied = new HashSet<Player>();
        
        // Easter egg fox wandering the apron (null if Fox.glb missing)
        private FoxWalker _fox;
        
        // Animated supporters on the stand (null if no player model)
        private FanSection _fans;
        
        // Team dugouts with substitutes and coaches
        private TeamBench _homeBench;
        private TeamBench _awayBench;
        
        // Match officials (referee + linesmen)
        private MatchOfficials _officials;
        
        // Goal replay: live play is recorded into a ring buffer and replayed
        // from a goal-side camera over the post-goal countdown
        private readonly ReplayBuffer _replayBuffer = new ReplayBuffer();
        private ReplaySequence _replay;
        private float _replayClock;
        private Vector3 _replayCameraFocus;
        private int _replayGoalSide = 1; // +1 = goal at +X, -1 = goal at -X
        private ReplayBuffer.PlayerFrame[] _replayFrames; // scratch for GetInterpolated
        private float[] _replayYaws;                      // per-player facing during replay
        private Player[] _replayPlayers;                  // player order matching the recording
        private int _homeTeamId;

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
            _world = new World3D(device, content);
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
            TryLoadFemalePlayerModel(device);
            
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
            
            // Easter egg: the stadium fox (tolerant of a missing file)
            try
            {
#if ANDROID
                var context = global::Android.App.Application.Context;
                using (var stream = context.Assets.Open("Content/Models3D/Fox.glb"))
                    _fox = new FoxWalker(SkinnedModel.Load(device, stream));
#else
                string foxPath = PlatformHelper.GetAssetPath(Path.Combine("Content", "Models3D", "Fox.glb"));
                if (File.Exists(foxPath))
                    _fox = new FoxWalker(SkinnedModel.Load(device, foxPath));
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MatchRenderer3D: no fox ({ex.Message}).");
                _fox = null;
            }
            
            // Supporters on the stand (reuses the player models/atlases)
            if (_playerModel != null)
                _fans = new FanSection(device, _playerModel, _playerModelF);
        }
        
        /// <summary>Creates the team dugouts once the match engine exists.</summary>
        public void InitializeBenches(GraphicsDevice device, MatchEngine engine, int homeTeamId)
        {
            _homeTeamId = homeTeamId;
            if (_playerModel == null) return;
            
            Texture2D atlas = _playerModel.Parts[0].Texture;
            float benchZ = -WorldUnits.PitchWidthMeters / 2f - 3.5f; // far touchline, in front of the stand
            
            // Benches flank the main stand (stand is x ∈ [-15, +15])
            foreach (var (team, offset) in new[] { (engine.HomeTeam, -22f), (engine.AwayTeam, 22f) })
            {
                GetKitColors(team.Players[0], homeTeamId, out Color shirt, out Color shorts, out Color socks);
                var bench = new TeamBench(device, team, new Vector2(offset, benchZ),
                    _playerModel, _playerModelF, atlas, shirt, shorts, socks);
                if (team == engine.HomeTeam) _homeBench = bench;
                else _awayBench = bench;
            }
            
            _officials = new MatchOfficials(device, _playerModel, atlas);
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
        
        /// <summary>Loads the optional female player variant (PlayerF.glb).</summary>
        private void TryLoadFemalePlayerModel(GraphicsDevice device)
        {
            try
            {
#if ANDROID
                var context = global::Android.App.Application.Context;
                using (var stream = context.Assets.Open("Content/Models3D/PlayerF.glb"))
                    _playerModelF = SkinnedModel.Load(device, stream);
#else
                string glbPath = PlatformHelper.GetAssetPath(Path.Combine("Content", "Models3D", "PlayerF.glb"));
                if (File.Exists(glbPath))
                    _playerModelF = SkinnedModel.Load(device, glbPath);
#endif
            }
            catch (Exception ex)
            {
                Debug.WriteLine($"MatchRenderer3D: failed to load PlayerF.glb ({ex.Message}).");
                _playerModelF = null;
            }
        }
        
        /// <summary>Deterministic per-player model choice (~1 in 4 female when available).</summary>
        private SkinnedModel GetModelForPlayer(Player player)
        {
            if (_playerModelF != null && player.Name != null && (player.Name.GetHashCode() & 3) == 0)
                return _playerModelF;
            return _playerModel;
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
            
            // While a goal replay overlays the post-goal countdown, DrawReplay
            // drives the camera, ball and player poses from the recording instead
            bool replayActive = IsReplayActive && engine.CurrentState == MatchEngine.MatchState.Countdown;
            
            _camera.UpdateViewport(Game1.ScreenWidth, Game1.ScreenHeight);
            _camera.SetCelebrating(engine.CurrentState == MatchEngine.MatchState.GoalCelebration);
            
            // During goal celebrations the camera follows the celebrating players
            // (centroid of the scoring team), not the ball lying in the net
            Vector2 cameraFocus = engine.BallPosition;
            if (engine.CurrentState == MatchEngine.MatchState.GoalCelebration &&
                engine.LastPlayerTouchedBall != null)
            {
                int scorerTeamId = engine.LastPlayerTouchedBall.TeamId;
                Vector2 sum = Vector2.Zero;
                int count = 0;
                foreach (var p in engine.GetAllPlayers())
                {
                    if (p.TeamId == scorerTeamId)
                    {
                        sum += p.FieldPosition;
                        count++;
                    }
                }
                if (count > 0)
                    cameraFocus = sum / count;
            }
            if (!replayActive)
            {
                _camera.Follow(cameraFocus, dt);
                _ball.Update(engine.BallPosition, engine.BallVelocity, engine.BallHeight, dt);
            }
            
            _world.Update(dt);
            
            // Cloth nets react to the ball when it's inside the goal volume
            Vector3 ballWorld = WorldUnits.ToWorld(engine.BallPosition, engine.BallHeight)
                + new Vector3(0f, Ball3D.RadiusMeters, 0f);
            Vector3 ballVelWorld = new Vector3(engine.BallVelocity.X, 0f, engine.BallVelocity.Y)
                / WorldUnits.PixelsPerMeter;
            foreach (var net in _nets)
                net.Update(dt, ballWorld, ballVelWorld);
            
            _rain?.Update(dt, _camera.Target);
            _fox?.Update(dt, engine);
            _fans?.Update(dt, engine);
            float ballWorldX = WorldUnits.ToWorld(engine.BallPosition).X;
            _homeBench?.Update(dt, ballWorldX);
            _awayBench?.Update(dt, ballWorldX);
            _officials?.Update(dt, engine);
            
            if (!replayActive)
            {
                if (_playerModel != null)
                    UpdatePlayerAnimators(engine, dt);
                else
                    UpdateBillboardAnimations(engine, dt);
            }
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
            DrawSetPieceArrow(device, engine);
            _fox?.Draw(device, _camera.View, _camera.Projection, _environment);
            _fans?.Draw(device, _camera.View, _camera.Projection, _environment);
            _homeBench?.Draw(device, _camera.View, _camera.Projection, _environment);
            _awayBench?.Draw(device, _camera.View, _camera.Projection, _environment);
            _officials?.Draw(device, _camera.View, _camera.Projection, _environment);
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
                    var model = GetModelForPlayer(player);
                    animator = new PlayerAnimator(model);
                    _playerAnimators[player] = animator;
                    _playerModelChoice[player] = model;
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
            
            // Ring on the ground under controlled player(s): P1 yellow, P2 cyan
            // (matches the 2D indicators). CullNone: skinned instances leave
            // CullClockwise active and the ring's winding is not guaranteed.
            device.RasterizerState = RasterizerState.CullNone;
            foreach (var player in engine.GetAllPlayers())
            {
                if (player.IsControlled && !player.IsKnockedDown)
                {
                    Vector3 pos = WorldUnits.ToWorld(player.FieldPosition);
                    bool isP2 = engine.ControlledPlayer2 == player;
                    _ringEffect.DiffuseColor = (isP2 ? Color.Cyan : Color.Yellow).ToVector3();
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
        /// 3D aiming arrow for set pieces (throw-in, corner, goal kick): a flat
        /// ground arrow at the restart position, length and color driven by the
        /// power charge. The 2D arrow is skipped in 3D mode, so without this the
        /// player aims blind.
        /// </summary>
        private void DrawSetPieceArrow(GraphicsDevice device, MatchEngine engine)
        {
            bool isSetPiece = engine.CurrentState == MatchEngine.MatchState.ThrowIn ||
                              engine.CurrentState == MatchEngine.MatchState.CornerKick ||
                              engine.CurrentState == MatchEngine.MatchState.GoalKick;
            if (!isSetPiece || engine.RestartPlayer == null || engine.RestartDirection == Vector2.Zero)
                return;
            
            float power = engine.ThrowInPowerCharge;
            float length = 4f + 10f * power;      // meters
            float shaftWidth = 0.35f;
            float headLength = 1.2f;
            float headWidth = 1.0f;
            Color color = Color.Lerp(Color.White * 0.7f, Color.Orange, power);
            
            Vector3 start = WorldUnits.ToWorld(engine.RestartPlayer.FieldPosition) + new Vector3(0f, 0.06f, 0f);
            Vector3 dir = new Vector3(engine.RestartDirection.X, 0f, engine.RestartDirection.Y);
            dir.Normalize();
            Vector3 side = Vector3.Normalize(Vector3.Cross(Vector3.Up, dir));
            
            Vector3 shaftEnd = start + dir * (length - headLength);
            Vector3 tip = start + dir * length;
            
            var verts = new List<VertexPositionColor>
            {
                // Shaft quad
                new VertexPositionColor(start - side * shaftWidth / 2f, color),
                new VertexPositionColor(start + side * shaftWidth / 2f, color),
                new VertexPositionColor(shaftEnd + side * shaftWidth / 2f, color),
                new VertexPositionColor(shaftEnd - side * shaftWidth / 2f, color),
                // Head triangle
                new VertexPositionColor(shaftEnd - side * headWidth / 2f, color),
                new VertexPositionColor(shaftEnd + side * headWidth / 2f, color),
                new VertexPositionColor(tip, color),
            };
            int[] indices = { 0, 1, 2, 0, 2, 3, 4, 5, 6 };
            
            _ringEffect.View = _camera.View;
            _ringEffect.Projection = _camera.Projection;
            _ringEffect.World = Matrix.Identity;
            
            device.RasterizerState = RasterizerState.CullNone;
            device.DepthStencilState = DepthStencilState.Default;
            foreach (var pass in _ringEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    verts.ToArray(), 0, verts.Count, indices, 0, 3);
            }
        }
        
        /// <summary>
        /// Debug overlay (D key): world-space AI visualization drawn on top of
        /// everything - target lines + crosses (lime), velocity arrows (cyan).
        /// Screen-space state labels are drawn by MatchScreen.
        /// </summary>
        public void DrawDebug(GraphicsDevice device, MatchEngine engine)
        {
            device.DepthStencilState = DepthStencilState.None; // always on top
            device.RasterizerState = RasterizerState.CullNone;
            _ringEffect.View = _camera.View;
            _ringEffect.Projection = _camera.Projection;
            _ringEffect.World = Matrix.Identity;
            
            var lines = new List<VertexPositionColor>();
            Color targetColor = Color.Lime * 0.8f;
            Color velocityColor = Color.Cyan * 0.9f;
            
            foreach (var player in engine.GetAllPlayers())
            {
                Vector3 pos = WorldUnits.ToWorld(player.FieldPosition) + new Vector3(0f, 0.1f, 0f);
                
                // Line to AI target + cross at the target point
                if (player.AITargetPositionSet && player.AITargetPosition != Vector2.Zero)
                {
                    Vector3 target = WorldUnits.ToWorld(player.AITargetPosition) + new Vector3(0f, 0.1f, 0f);
                    lines.Add(new VertexPositionColor(pos, targetColor));
                    lines.Add(new VertexPositionColor(target, targetColor));
                    
                    const float cross = 0.5f;
                    lines.Add(new VertexPositionColor(target + new Vector3(-cross, 0, 0), targetColor));
                    lines.Add(new VertexPositionColor(target + new Vector3(cross, 0, 0), targetColor));
                    lines.Add(new VertexPositionColor(target + new Vector3(0, 0, -cross), targetColor));
                    lines.Add(new VertexPositionColor(target + new Vector3(0, 0, cross), targetColor));
                }
                
                // Velocity arrow (scaled for visibility)
                if (player.Velocity.LengthSquared() > 1f)
                {
                    Vector3 vel = new Vector3(player.Velocity.X, 0f, player.Velocity.Y)
                        / WorldUnits.PixelsPerMeter;
                    lines.Add(new VertexPositionColor(pos, velocityColor));
                    lines.Add(new VertexPositionColor(pos + vel, velocityColor));
                }
            }
            
            // Ball velocity vector
            Vector3 ballPos = WorldUnits.ToWorld(engine.BallPosition, engine.BallHeight)
                + new Vector3(0f, 0.15f, 0f);
            Vector3 ballVel = new Vector3(engine.BallVelocity.X, engine.BallVerticalVelocity,
                engine.BallVelocity.Y) / WorldUnits.PixelsPerMeter;
            lines.Add(new VertexPositionColor(ballPos, Color.Magenta));
            lines.Add(new VertexPositionColor(ballPos + ballVel, Color.Magenta));
            
            if (lines.Count == 0) return;
            
            foreach (var pass in _ringEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList,
                    lines.ToArray(), 0, lines.Count / 2);
            }
            
            // Restore sprite-batch-friendly states for HUD
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearClamp;
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
            
            var model = _playerModelChoice.TryGetValue(player, out var m) ? m : _playerModel;
            Texture2D baseTexture = model.Parts[0].Texture;
            bool soccerStyle = model == _playerModelF ||
                (model == _playerModel && _playerModelKind == PlayerModelKind.SoccerPlayer);
            
            if (soccerStyle)
            {
                // player_atlas.png layout (512x512): quadrants shirt / shorts / socks / skin+extras
                Texture2D shirtTexture = KitTextureFactory.GetKitTexture(device, baseTexture, shirt,
                    new Rectangle(0, 0, 256, 256));
                Texture2D shortsTexture = KitTextureFactory.GetKitTexture(device, baseTexture, shorts,
                    new Rectangle(256, 0, 256, 256));
                Texture2D socksTexture = KitTextureFactory.GetKitTexture(device, baseTexture, socks,
                    new Rectangle(0, 256, 256, 256));
                
                // Per-player: shirt number stamped on the back
                Texture2D numberedShirt = KitTextureFactory.GetNumberedShirtTexture(device, shirtTexture,
                    player.ShirtNumber, KitTextureFactory.ContrastFor(shirt));
                
                foreach (var part in model.Parts)
                {
                    string name = part.Name ?? "";
                    if (name == "Soccer_Shirt")
                        animator.Instance.SetPartTexture(part.Name, numberedShirt);
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
                
                foreach (var part in model.Parts)
                {
                    string name = part.Name ?? "";
                    if (name.Contains("Body") || name.Contains("Arm") || name.Contains("Helmet"))
                        animator.Instance.SetPartTexture(part.Name, shirtTexture);
                    else if (name.Contains("Leg"))
                        animator.Instance.SetPartTexture(part.Name, shortsTexture);
                }
            }
        }
        
        /// <summary>Shirt/shorts/socks colors per team, sampled from the 2D kit sprite sheets.
        /// Also used by the lineup screen for portraits and team-color accents.</summary>
        internal static void GetKitColors(Player player, int homeTeamId, out Color shirt, out Color shorts, out Color socks)
        {
            // Goalkeepers wear a distinct kit (yellow home / lime green away)
            if (player.Position == PlayerPosition.Goalkeeper)
            {
                if (player.TeamId == homeTeamId)
                {
                    shirt = new Color(240, 200, 30); shorts = new Color(35, 35, 40); socks = new Color(240, 200, 30);
                }
                else
                {
                    shirt = new Color(80, 200, 80); shorts = new Color(35, 35, 40); socks = new Color(80, 200, 80);
                }
                return;
            }
            
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
            Color ringColor = Color.White; // tinted per draw via DiffuseColor (P1 yellow, P2 cyan)
            
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
        
        #region Goal replay
        
        /// <summary>Records one frame of live play into the replay ring buffer (call every Playing update).</summary>
        public void RecordReplayFrame(MatchEngine engine)
        {
            _replayBuffer.Record(engine);
            _replayPlayers = engine.GetAllPlayers().ToArray();
        }
        
        /// <summary>
        /// Snapshots the recorded build-up when a goal is scored. Skipped when
        /// less than 2 seconds are on tape (e.g. a goal straight after kickoff).
        /// The buffer is reset so the next replay is built up from fresh play.
        /// </summary>
        public void CaptureReplay()
        {
            const float ReplaySeconds = 3.5f; // matches the post-goal countdown length
            const float MinSeconds = 2f;
            
            _replay = _replayBuffer.RecordedSeconds >= MinSeconds
                ? _replayBuffer.Snapshot(ReplaySeconds)
                : null;
            _replayBuffer.Reset();
            _replayClock = 0f;
            
            if (_replay != null)
            {
                _replayFrames = new ReplayBuffer.PlayerFrame[_replay.PlayerCount];
                _replayYaws = new float[_replay.PlayerCount];
                
                // The ball ends up inside the scored-on goal; its final side picks the camera end
                _replay.GetInterpolated(_replay.Duration, out ReplayBuffer.BallFrame lastBall, _replayFrames);
                _replayGoalSide = WorldUnits.ToWorld(lastBall.Position).X >= 0f ? 1 : -1;
                
                _replay.GetInterpolated(0f, out ReplayBuffer.BallFrame firstBall, _replayFrames);
                _replayCameraFocus = WorldUnits.ToWorld(firstBall.Position, firstBall.Height);
            }
        }
        
        /// <summary>True while a captured goal replay is still playing back.</summary>
        public bool IsReplayActive => _replay != null && _replayClock < _replay.Duration;
        
        /// <summary>Fast-forwards the replay to its end (player holds the shoot key).</summary>
        public void SkipReplay()
        {
            if (_replay != null)
                _replayClock = _replay.Duration;
        }
        
        /// <summary>Drops any captured replay (called when the post-goal countdown ends).</summary>
        public void ClearReplay() => _replay = null;
        
        /// <summary>
        /// Draws the captured goal build-up instead of the live scene (post-goal
        /// countdown window). Cinematic camera: low, just inside the scored-on
        /// goal's end, ~12m to the side of the goal mouth, panning to track the
        /// recorded ball. Players are the live skinned instances, re-posed from
        /// the recording (yaw from velocity, clip from speed).
        /// </summary>
        public void DrawReplay(GraphicsDevice device, MatchEngine engine, float dt)
        {
            // No replay on tape (or billboard fallback without skinned players): live scene
            if (!IsReplayActive || _playerModel == null || _replayPlayers == null)
            {
                Draw(device, engine, _homeTeamId);
                return;
            }
            
            _replayClock = Math.Min(_replayClock + dt, _replay.Duration);
            _replay.GetInterpolated(_replayClock, out ReplayBuffer.BallFrame ball, _replayFrames);
            
            // Cinematic camera: goal-side view of the scored-on goal, smoothly
            // tracking the recorded ball (frame-rate independent lerp)
            Vector3 ballWorld = WorldUnits.ToWorld(ball.Position, ball.Height);
            float follow = 1f - (float)Math.Pow(1f - 0.12f, dt * 60f);
            _replayCameraFocus = Vector3.Lerp(_replayCameraFocus, ballWorld, follow);
            float halfLength = WorldUnits.PitchLengthMeters / 2f;
            Vector3 cameraPos = new Vector3(_replayGoalSide * (halfLength - 2f), 2.5f, 12f);
            _camera.SetView(cameraPos, _replayCameraFocus + new Vector3(0f, 0.5f, 0f));
            
            // Full clear (color + depth) - 3D scene replaces the 2D world entirely
            device.Clear(ClearOptions.Target | ClearOptions.DepthBuffer,
                _environment.SkyColor, 1f, 0);
            
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            
            _world.Draw(device, _camera.View, _camera.Projection);
            _environment.Draw(device, _camera.View, _camera.Projection); // Floodlights (night only)
            foreach (var net in _nets)
                net.Draw(device, _camera.View, _camera.Projection);
            
            // Ball from the recording (rolls from its recorded velocity, keeps its shadow)
            _ball.Update(ball.Position, ball.Velocity, ball.Height, dt);
            _ball.Draw(device, _camera.View, _camera.Projection);
            
            // Players from the recording, reusing the live skinned instances
            int count = Math.Min(_replayPlayers.Length, _replay.PlayerCount);
            for (int i = 0; i < count; i++)
            {
                var player = _replayPlayers[i];
                if (!_playerAnimators.TryGetValue(player, out var animator))
                    continue;
                
                // Per-team kit textures, applied once per player (normally done by now)
                if (_kitsApplied.Add(player))
                    ApplyKit(device, player, _homeTeamId, animator);
                
                var frame = _replayFrames[i];
                
                // Facing from recorded velocity; keep the last direction when nearly stopped
                if (frame.Velocity.LengthSquared() > 1f)
                    _replayYaws[i] = (float)Math.Atan2(frame.Velocity.X, frame.Velocity.Y) + PlayerAnimator.ModelYawOffset;
                
                float speed = frame.Velocity.Length();
                string clip = frame.KnockedDown ? "Lie_Idle"
                    : speed > 120f ? "Running_A"
                    : speed > 5f ? "Walking_A"
                    : "Idle";
                if (!animator.Instance.Play(clip, loop: true) && clip != "Idle")
                    animator.Instance.Play("Idle", loop: true); // graceful fallback for missing clips
                animator.Instance.Update(dt);
                animator.Instance.Environment = _environment;
                
                Matrix world = Matrix.CreateScale(PlayerModelScale)
                    * Matrix.CreateRotationY(_replayYaws[i])
                    * Matrix.CreateTranslation(WorldUnits.ToWorld(frame.Position));
                animator.Instance.Draw(device, world, _camera.View, _camera.Projection);
            }
            
            _fox?.Draw(device, _camera.View, _camera.Projection, _environment);
            _fans?.Draw(device, _camera.View, _camera.Projection, _environment);
            _homeBench?.Draw(device, _camera.View, _camera.Projection, _environment);
            _awayBench?.Draw(device, _camera.View, _camera.Projection, _environment);
            _officials?.Draw(device, _camera.View, _camera.Projection, _environment);
            _rain?.Draw(device, _camera.View, _camera.Projection);
            
            // Restore GraphicsDevice states for SpriteBatch (HUD drawn after us)
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearClamp;
        }
        
        #endregion
    }
}
