using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Debugging;
using NoPasaranFC.Graphics3D;
using NoPasaranFC.Graphics3D.Skinning;
using NoPasaranFC.Models;

namespace NoPasaranFC.Screens
{
    /// <summary>
    /// 3D championship award celebration, pushed on top of the post-match
    /// stats screen when the season ends: a slowly rotating procedural gold
    /// cup on a podium, the champion team's starting 11 cheering in an arc
    /// around it, falling confetti, stadium-night backdrop, orbiting camera.
    /// Enter/Space/click dismisses (re-armed so the key that dismissed the
    /// screen above can't skip it). Also opened via debug console ("champion").
    /// </summary>
    public class ChampionScreen : Screen
    {
        private const int PlayerCount = 11;
        private const int LatheSegments = 24;
        private const int ConfettiCount = 150;
        private const float PlayerScale = 0.72f;   // same scale as ConstructScreen
        private const float PlayerArcRadius = 3.8f;
        private const float PodiumTopY = 0.32f;

        private readonly Championship _championship;
        private readonly Team _champion;
        private readonly bool _playerIsChampion;

        private SpriteFont _font;
        private BasicEffect _effect;
        private SkinnedModel _playerModel;
        private SkinnedModel _playerModelF;
        private readonly List<SkinnedModelInstance> _instances = new List<SkinnedModelInstance>();
        private readonly List<float> _instanceAngles = new List<float>();

        private VertexPositionColor[] _trophyVerts;
        private VertexPositionColor[] _podiumVerts;
        private VertexPositionColor[] _groundVerts;
        private readonly Confetti[] _confetti = new Confetti[ConfettiCount];
        private readonly Random _random = new Random();

        private float _time;
        private KeyboardState _prevKeys;
        private MouseState _prevMouse;
        private Gameplay.InputHelper _input = new Gameplay.InputHelper();

        private struct Confetti
        {
            public Vector3 Pos;
            public float FallSpeed;
            public float SwayPhase;
            public float SwaySpeed;
            public float Rot;
            public float RotSpeed;
            public Color Color;
        }

        private static readonly Color[] ConfettiPalette =
        {
            Color.Gold, Color.OrangeRed, Color.DeepSkyBlue,
            Color.LimeGreen, Color.Magenta, Color.White, Color.Orange,
        };

        public ChampionScreen(Championship championship, ContentManager content,
            GraphicsDevice graphicsDevice)
            : base(content, graphicsDevice)
        {
            _championship = championship;
            _champion = championship.GetChampionTeam();
            var playerTeam = championship.Teams?.Find(t => t.IsPlayerControlled);
            _playerIsChampion = playerTeam != null && _champion != null &&
                playerTeam.Id == _champion.Id;

            _effect = new BasicEffect(graphicsDevice)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false // unlit: the gold pops against the night
            };

            BuildGround();
            BuildPodium();
            BuildTrophy();
            SpawnPlayers();
            SpawnConfetti();
        }

        // ---- scene construction ----

        private void SpawnPlayers()
        {
            _playerModel = ModelCache.TryGet(GraphicsDevice, "Player.glb");
            _playerModelF = ModelCache.TryGet(GraphicsDevice, "PlayerF.glb");
            if (_playerModel == null || _champion?.Players == null) return;

            var starters = _champion.Players
                .Where(p => p.IsStarting)
                .OrderBy(p => p.Position)
                .ThenBy(p => p.ShirtNumber)
                .Take(PlayerCount)
                .ToList();
            if (starters.Count == 0)
                starters = _champion.Players.Take(PlayerCount).ToList();

            for (int i = 0; i < starters.Count; i++)
            {
                var player = starters[i];
                var model = _playerModelF != null && FaceComposer.IsFemalePlayer(player)
                    ? _playerModelF : _playerModel;
                var instance = new SkinnedModelInstance(model);
                var appearance = FaceComposer.AppearanceFor(player);
                Texture2D composed = FaceComposer.Compose(GraphicsDevice,
                    model.Parts[0].Texture, appearance);
                KitBake.ApplyKitTextures(GraphicsDevice, instance, model, composed,
                    player.Team ?? _champion, player, _champion.Id);

                if (!instance.Play("Cheer", loop: true))
                    instance.Play("Idle", loop: true);
                instance.PlaybackSpeed = 0.9f + (i % 5) * 0.06f;
                instance.Update(i * 0.33f); // stagger phases: no synchronized robots

                _instances.Add(instance);
                // Arc around the cup, facing it
                _instanceAngles.Add(MathHelper.ToRadians(-120f + i * (240f / Math.Max(1, starters.Count - 1))));
            }
        }

        private void SpawnConfetti()
        {
            for (int i = 0; i < ConfettiCount; i++)
                _confetti[i] = NewConfetti(2f + (float)_random.NextDouble() * 8f);
        }

        private Confetti NewConfetti(float y)
        {
            double a = _random.NextDouble() * Math.PI * 2;
            float r = 1f + (float)_random.NextDouble() * 5.5f;
            return new Confetti
            {
                Pos = new Vector3((float)Math.Cos(a) * r, y, (float)Math.Sin(a) * r),
                FallSpeed = 0.8f + (float)_random.NextDouble() * 1.2f,
                SwayPhase = (float)_random.NextDouble() * MathHelper.TwoPi,
                SwaySpeed = 1f + (float)_random.NextDouble() * 2f,
                Rot = (float)_random.NextDouble() * MathHelper.TwoPi,
                RotSpeed = 2f + (float)_random.NextDouble() * 5f,
                Color = ConfettiPalette[_random.Next(ConfettiPalette.Length)],
            };
        }

        /// <summary>Revolve a (radius, height) profile around the Y axis.</summary>
        private static List<VertexPositionColor> Lathe(
            IReadOnlyList<Vector2> profile, Func<Vector2, Color> shade)
        {
            var verts = new List<VertexPositionColor>((profile.Count - 1) * LatheSegments * 6);
            for (int i = 0; i < profile.Count - 1; i++)
            {
                for (int s = 0; s < LatheSegments; s++)
                {
                    float a0 = s * MathHelper.TwoPi / LatheSegments;
                    float a1 = (s + 1) * MathHelper.TwoPi / LatheSegments;
                    Vector3 p00 = Ring(profile[i], a0), p01 = Ring(profile[i], a1);
                    Vector3 p10 = Ring(profile[i + 1], a0), p11 = Ring(profile[i + 1], a1);
                    Color c0 = shade(profile[i]);
                    Color c1 = shade(profile[i + 1]);
                    verts.Add(new VertexPositionColor(p00, c0));
                    verts.Add(new VertexPositionColor(p10, c1));
                    verts.Add(new VertexPositionColor(p11, c1));
                    verts.Add(new VertexPositionColor(p00, c0));
                    verts.Add(new VertexPositionColor(p11, c1));
                    verts.Add(new VertexPositionColor(p01, c0));
                }
            }
            return verts;
        }

        private static Vector3 Ring(Vector2 profilePoint, float angle) =>
            new Vector3(MathF.Cos(angle) * profilePoint.X, profilePoint.Y, MathF.Sin(angle) * profilePoint.X);

        private void BuildTrophy()
        {
            // Cup profile: base, stem with knob, flared bowl, rim
            var profile = new[]
            {
                new Vector2(0.45f, 0.00f), new Vector2(0.45f, 0.10f),
                new Vector2(0.28f, 0.14f), new Vector2(0.10f, 0.18f),
                new Vector2(0.09f, 0.38f), new Vector2(0.16f, 0.44f),
                new Vector2(0.16f, 0.50f), new Vector2(0.10f, 0.56f),
                new Vector2(0.14f, 0.66f), new Vector2(0.26f, 0.80f),
                new Vector2(0.38f, 0.92f), new Vector2(0.42f, 1.00f),
                new Vector2(0.44f, 1.04f),
            };
            // Fake-lit gold: brighter toward the rim, subtle banding for shape
            var verts = Lathe(profile, p =>
            {
                float t = Math.Clamp(p.Y / 1.04f, 0f, 1f);
                float band = 0.92f + 0.08f * MathF.Sin(p.Y * 40f);
                return new Color(
                    (int)((180 + 75 * t) * band),
                    (int)((130 + 90 * t) * band),
                    (int)((30 + 50 * t) * band));
            });

            // Two handles: half-torus arcs in the vertical planes through ±X
            foreach (float side in new[] { -1f, 1f })
            {
                const int arcSegs = 10, tubeSegs = 6;
                const float rx = 0.56f, ry = 0.20f, cy = 0.80f, tube = 0.045f;
                var gold = new Color(235, 185, 60);
                for (int i = 0; i < arcSegs; i++)
                {
                    float u0 = MathHelper.ToRadians(-62f + i * (124f / arcSegs));
                    float u1 = MathHelper.ToRadians(-62f + (i + 1) * (124f / arcSegs));
                    for (int j = 0; j < tubeSegs; j++)
                    {
                        float v0 = j * MathHelper.TwoPi / tubeSegs;
                        float v1 = (j + 1) * MathHelper.TwoPi / tubeSegs;
                        Vector3 q00 = Handle(side, rx, ry, cy, tube, u0, v0);
                        Vector3 q01 = Handle(side, rx, ry, cy, tube, u0, v1);
                        Vector3 q10 = Handle(side, rx, ry, cy, tube, u1, v0);
                        Vector3 q11 = Handle(side, rx, ry, cy, tube, u1, v1);
                        verts.Add(new VertexPositionColor(q00, gold));
                        verts.Add(new VertexPositionColor(q10, gold));
                        verts.Add(new VertexPositionColor(q11, gold));
                        verts.Add(new VertexPositionColor(q00, gold));
                        verts.Add(new VertexPositionColor(q11, gold));
                        verts.Add(new VertexPositionColor(q01, gold));
                    }
                }
            }
            _trophyVerts = verts.ToArray();
        }

        /// <summary>Point on a handle arc: centerline ellipse in the XY plane,
        /// tube circle spanning (in-plane normal, Z).</summary>
        private static Vector3 Handle(float side, float rx, float ry, float cy,
            float tube, float u, float v)
        {
            var center = new Vector3(side * MathF.Cos(u) * rx, cy + MathF.Sin(u) * ry, 0f);
            var normal = new Vector3(MathF.Cos(u), MathF.Sin(u), 0f);
            return center + tube * (MathF.Cos(v) * normal + MathF.Sin(v) * Vector3.UnitZ);
        }

        private void BuildPodium()
        {
            var lower = Lathe(new[]
            {
                new Vector2(1.15f, 0.00f), new Vector2(1.15f, 0.12f),
            }, _ => new Color(45, 48, 60));
            var upper = Lathe(new[]
            {
                new Vector2(0.80f, 0.12f), new Vector2(0.80f, PodiumTopY),
            }, _ => new Color(70, 74, 90));
            var top = Lathe(new[]
            {
                new Vector2(0.00f, PodiumTopY + 0.001f), new Vector2(0.80f, PodiumTopY + 0.001f),
            }, _ => new Color(90, 95, 115));
            _podiumVerts = lower.Concat(upper).Concat(top).ToArray();
        }

        private void BuildGround()
        {
            var grass = new Color(8, 26, 12); // stadium-night pitch
            const float half = 40f;
            _groundVerts = new[]
            {
                new VertexPositionColor(new Vector3(-half, 0f, -half), grass),
                new VertexPositionColor(new Vector3(-half, 0f, half), grass),
                new VertexPositionColor(new Vector3(half, 0f, half), grass),
                new VertexPositionColor(new Vector3(-half, 0f, -half), grass),
                new VertexPositionColor(new Vector3(half, 0f, half), grass),
                new VertexPositionColor(new Vector3(half, 0f, -half), grass),
            };
        }

        // ---- update ----

        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;
            _input.Update();
            var touchUI = Gameplay.TouchUI.Instance;

            if (_font == null)
                _font = Content.Load<SpriteFont>("Font");

            var keys = DebugInput.GetState();
            var mouse = Mouse.GetState();

            // Dismiss: Enter/Space/click (re-armed first, so the press that
            // dismissed the stats screen above can't skip the celebration)
            if (DismissReArmed(keys, mouse) &&
                ((keys.IsKeyDown(Keys.Enter) && _prevKeys.IsKeyUp(Keys.Enter)) ||
                 (keys.IsKeyDown(Keys.Space) && _prevKeys.IsKeyUp(Keys.Space)) ||
                 (mouse.LeftButton == ButtonState.Pressed && _prevMouse.LeftButton == ButtonState.Released) ||
                 _input.IsConfirmPressed() || touchUI.IsActionJustPressed))
            {
                IsFinished = true;
            }

            foreach (var instance in _instances)
                instance.Update(dt);

            for (int i = 0; i < ConfettiCount; i++)
            {
                _confetti[i].Pos.Y -= _confetti[i].FallSpeed * dt;
                _confetti[i].Rot += _confetti[i].RotSpeed * dt;
                if (_confetti[i].Pos.Y < 0.02f)
                    _confetti[i] = NewConfetti(7f + (float)_random.NextDouble() * 3f);
            }

            _prevKeys = keys;
            _prevMouse = mouse;
        }

        // ---- draw ----

        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            var device = GraphicsDevice;
            int w = Game1.ScreenWidth, h = Game1.ScreenHeight;

            device.Clear(new Color(4, 6, 14)); // night sky
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            device.BlendState = BlendState.Opaque;

            // Slow camera orbit around the cup with a gentle height drift
            // (high enough that the near side of the arc doesn't block the cup)
            float orbit = _time * 0.22f;
            var camPos = new Vector3(MathF.Sin(orbit) * 7.4f,
                4.1f + MathF.Sin(_time * 0.13f) * 0.4f, MathF.Cos(orbit) * 7.4f);
            var view = Matrix.CreateLookAt(camPos, new Vector3(0f, 0.7f, 0f), Vector3.Up);
            var projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45f), w / (float)h, 0.05f, 120f);

            _effect.View = view;
            _effect.Projection = projection;

            DrawPrimitives(device, _groundVerts, Matrix.Identity);
            DrawPrimitives(device, _podiumVerts, Matrix.Identity);
            // The cup slowly rotates on its podium
            DrawPrimitives(device, _trophyVerts,
                Matrix.CreateScale(1.25f) *
                Matrix.CreateRotationY(_time * 0.55f) *
                Matrix.CreateTranslation(0f, PodiumTopY, 0f));

            // The champions cheering in an arc around the cup
            for (int i = 0; i < _instances.Count; i++)
            {
                float a = _instanceAngles[i];
                var world = Matrix.CreateScale(PlayerScale)
                    * Matrix.CreateRotationY(a + MathHelper.Pi)
                    * Matrix.CreateTranslation(
                        MathF.Sin(a) * PlayerArcRadius, 0f, MathF.Cos(a) * PlayerArcRadius);
                _instances[i].Draw(device, world, view, projection);
            }

            DrawConfetti(device, view, projection);

            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.AlphaBlend;

            DrawHud(spriteBatch, w, h);
        }

        private void DrawPrimitives(GraphicsDevice device, VertexPositionColor[] verts, Matrix world)
        {
            if (verts == null || verts.Length == 0) return;
            _effect.World = world;
            foreach (var pass in _effect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.TriangleList, verts, 0, verts.Length / 3);
            }
        }

        private void DrawConfetti(GraphicsDevice device, Matrix view, Matrix projection)
        {
            // Camera-facing billboard quads, fluttering as they fall
            var right = new Vector3(view.M11, view.M21, view.M31);
            var up = new Vector3(view.M12, view.M22, view.M32);
            var verts = new List<VertexPositionColor>(ConfettiCount * 6);
            const float size = 0.055f;
            for (int i = 0; i < ConfettiCount; i++)
            {
                var c = _confetti[i];
                var pos = c.Pos;
                pos.X += MathF.Sin(_time * c.SwaySpeed + c.SwayPhase) * 0.4f;
                var r = right * MathF.Cos(c.Rot) + up * MathF.Sin(c.Rot);
                var u = up * MathF.Cos(c.Rot) - right * MathF.Sin(c.Rot);
                r *= size; u *= size * 0.6f;
                verts.Add(new VertexPositionColor(pos - r - u, c.Color));
                verts.Add(new VertexPositionColor(pos + r - u, c.Color));
                verts.Add(new VertexPositionColor(pos + r + u, c.Color));
                verts.Add(new VertexPositionColor(pos - r - u, c.Color));
                verts.Add(new VertexPositionColor(pos + r + u, c.Color));
                verts.Add(new VertexPositionColor(pos - r + u, c.Color));
            }
            DrawPrimitives(device, verts.ToArray(), Matrix.Identity);
        }

        private void DrawHud(SpriteBatch spriteBatch, int w, int h)
        {
            if (_font == null) return;
            float uiScale = Game1.UIScale;
            string championName = _champion?.Name ?? "";

            if (_playerIsChampion)
            {
                string title = Localization.Instance.Get("champion.title");
                float titleScale = 2.5f * uiScale;
                Vector2 titleSize = _font.MeasureString(title) * titleScale;
                spriteBatch.DrawString(_font, title,
                    new Vector2((w - titleSize.X) / 2, 40 * uiScale), Color.Gold,
                    0f, Vector2.Zero, titleScale, SpriteEffects.None, 0f);

                float nameScale = 1.8f * uiScale;
                Vector2 nameSize = _font.MeasureString(championName) * nameScale;
                spriteBatch.DrawString(_font, championName,
                    new Vector2((w - nameSize.X) / 2, 40 * uiScale + titleSize.Y + 10 * uiScale),
                    Color.White, 0f, Vector2.Zero, nameScale, SpriteEffects.None, 0f);
            }
            else
            {
                // Player's team didn't win: neutral announcement, champion name big
                string line = Localization.Instance.Get("champion.team_won")
                    .Replace("{0}", championName);
                float lineScale = 1.6f * uiScale;
                Vector2 lineSize = _font.MeasureString(line) * lineScale;
                spriteBatch.DrawString(_font, line,
                    new Vector2((w - lineSize.X) / 2, 50 * uiScale), Color.Gold,
                    0f, Vector2.Zero, lineScale, SpriteEffects.None, 0f);
            }

            if (!string.IsNullOrEmpty(_championship?.Name))
            {
                Vector2 subSize = _font.MeasureString(_championship.Name) * uiScale;
                spriteBatch.DrawString(_font, _championship.Name,
                    new Vector2((w - subSize.X) / 2, 130 * uiScale), Color.LightGray,
                    0f, Vector2.Zero, uiScale, SpriteEffects.None, 0f);
            }

            string hint = Localization.Instance.Get("round_results_continue");
            Vector2 hintSize = _font.MeasureString(hint) * uiScale;
            spriteBatch.DrawString(_font, hint,
                new Vector2((w - hintSize.X) / 2, h - 50 * uiScale), Color.LightGray,
                0f, Vector2.Zero, uiScale, SpriteEffects.None, 0f);
        }
    }
}
