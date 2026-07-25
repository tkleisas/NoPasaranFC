using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input;
using NoPasaranFC.Debugging;
using NoPasaranFC.Gameplay;
using NoPasaranFC.Graphics3D;
using NoPasaranFC.Graphics3D.Skinning;
using NoPasaranFC.Models;

namespace NoPasaranFC.Screens
{
    /// <summary>
    /// The Construct: a Matrix-style inspection chamber for player models.
    /// A row of players stands on a green grid in the void, slowly turning so
    /// faces can be inspected. Left/Right selects, E cycles expression,
    /// F cycles facial feature, H hair color, S skin tone, Up/Down zoom,
    /// PageUp/Down pages the roster. Opened via the debug console ("construct").
    /// </summary>
    public class ConstructScreen : Screen
    {
        private const int PageSize = 7;
        
        private readonly List<Player> _players;
        private SkinnedModel _playerModel;
        private SkinnedModel _playerModelF;
        private readonly List<SkinnedModelInstance> _instances = new List<SkinnedModelInstance>();
        private readonly List<FaceComposer.Appearance> _appearances = new List<FaceComposer.Appearance>();
        private BasicEffect _gridEffect;
        private Texture2D _pixel;
        
        private int _page;
        private int _selected;
        private float _cameraDistance = 5.5f;
        private float _time;
        private KeyboardState _prevKeys;
        
        private static readonly FaceComposer.Expression[] Expressions =
            (FaceComposer.Expression[])Enum.GetValues(typeof(FaceComposer.Expression));
        private static readonly FaceComposer.Feature[] Features =
            (FaceComposer.Feature[])Enum.GetValues(typeof(FaceComposer.Feature));
        
        public ConstructScreen(Team team, ContentManager content, GraphicsDevice graphicsDevice)
            : base(content, graphicsDevice)
        {
            _players = team.Players
                .OrderBy(p => p.Position)
                .ThenBy(p => p.ShirtNumber)
                .ToList();
            
            TryLoadModels();
            _gridEffect = new BasicEffect(graphicsDevice) { VertexColorEnabled = true };
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            
            RebuildPage();
        }
        
        private void TryLoadModels()
        {
            try
            {
                string path = PlatformHelper.GetAssetPath(Path.Combine("Content", "Models3D", "Player.glb"));
                if (File.Exists(path))
                    _playerModel = SkinnedModel.Load(GraphicsDevice, path);
            }
            catch (Exception) { _playerModel = null; }
            try
            {
                string pathF = PlatformHelper.GetAssetPath(Path.Combine("Content", "Models3D", "PlayerF.glb"));
                if (File.Exists(pathF))
                    _playerModelF = SkinnedModel.Load(GraphicsDevice, pathF);
            }
            catch (Exception) { _playerModelF = null; }
        }
        
        private SkinnedModel ModelFor(Player p) =>
            _playerModelF != null && FaceComposer.IsFemalePlayer(p) ? _playerModelF : _playerModel;
        
        /// <summary>Instantiate the current page of players with their appearances.</summary>
        private void RebuildPage()
        {
            _instances.Clear();
            _appearances.Clear();
            if (_playerModel == null) return;
            
            for (int i = 0; i < PageSize; i++)
            {
                int idx = _page * PageSize + i;
                if (idx >= _players.Count) break;
                var player = _players[idx];
                var model = ModelFor(player);
                var instance = new SkinnedModelInstance(model);
                var appearance = FaceComposer.AppearanceFor(player);
                ApplyAppearance(instance, model, player, appearance);
                instance.Play("Idle");
                instance.Update((float)(i * 0.37));
                _instances.Add(instance);
                _appearances.Add(appearance);
            }
        }
        
        /// <summary>Compose the face atlas + bake kit part textures for one player.</summary>
        private void ApplyAppearance(SkinnedModelInstance instance, SkinnedModel model,
            Player player, FaceComposer.Appearance appearance)
        {
            Texture2D baseTexture = model.Parts[0].Texture;
            Texture2D composed = FaceComposer.Compose(GraphicsDevice, baseTexture, appearance);
            
            Color shirt = new Color(200, 30, 30), shorts = new Color(30, 30, 35), socks = new Color(200, 30, 30);
            int q = 256 * FaceComposer.AtlasScale;
            var shirtTex = KitTextureFactory.GetKitTexture(GraphicsDevice, composed, shirt, new Rectangle(0, 0, q, q));
            var shortsTex = KitTextureFactory.GetKitTexture(GraphicsDevice, composed, shorts, new Rectangle(q, 0, q, q));
            var socksTex = KitTextureFactory.GetKitTexture(GraphicsDevice, composed, socks, new Rectangle(0, q, q, q));
            var numbered = KitTextureFactory.GetNumberedShirtTexture(GraphicsDevice, shirtTex,
                player.ShirtNumber, KitTextureFactory.ContrastFor(shirt));
            
            foreach (var part in model.Parts)
            {
                string name = part.Name ?? "";
                if (name == "Soccer_Shirt") instance.SetPartTexture(part.Name, numbered);
                else if (name == "Soccer_Shorts") instance.SetPartTexture(part.Name, shortsTex);
                else if (name.StartsWith("Soccer_Sock")) instance.SetPartTexture(part.Name, socksTex);
                else if (name == "Soccer_Skin" || name == "Soccer_Hair")
                    instance.SetPartTexture(part.Name, composed);
            }
        }
        
        /// <summary>Cycle one aspect of the selected player's appearance and re-apply it.</summary>
        private void CycleAppearance(int exprDelta, int featDelta, int hairDelta, int skinDelta)
        {
            int idx = _page * PageSize + _selected;
            if (idx >= _players.Count || _selected >= _instances.Count) return;
            
            var a = _appearances[_selected];
            int e = ((int)a.Expr + exprDelta + Expressions.Length) % Expressions.Length;
            int f = ((int)a.Feat + featDelta + Features.Length) % Features.Length;
            int h = (a.HairColor + hairDelta + 6) % 6;
            int s = (a.SkinTone + skinDelta + 5) % 5;
            var updated = new FaceComposer.Appearance(s, h, Expressions[e], Features[f]);
            _appearances[_selected] = updated;
            
            var player = _players[idx];
            ApplyAppearance(_instances[_selected], ModelFor(player), player, updated);
        }
        
        public override void Update(GameTime gameTime)
        {
            float dt = (float)gameTime.ElapsedGameTime.TotalSeconds;
            _time += dt;
            
            var keys = DebugInput.GetState();
            bool JustPressed(Keys k) => keys.IsKeyDown(k) && !_prevKeys.IsKeyDown(k);
            
            int pageCount = (_players.Count + PageSize - 1) / PageSize;
            
            if (JustPressed(Keys.Right)) _selected = Math.Min(_selected + 1, _instances.Count - 1);
            if (JustPressed(Keys.Left)) _selected = Math.Max(_selected - 1, 0);
            if (JustPressed(Keys.Down)) _cameraDistance = Math.Min(_cameraDistance + 0.5f, 12f);
            if (JustPressed(Keys.Up)) _cameraDistance = Math.Max(_cameraDistance - 0.5f, 2.5f);
            if (JustPressed(Keys.PageDown) && _page < pageCount - 1) { _page++; _selected = 0; RebuildPage(); }
            if (JustPressed(Keys.PageUp) && _page > 0) { _page--; _selected = 0; RebuildPage(); }
            
            if (JustPressed(Keys.E)) CycleAppearance(1, 0, 0, 0);
            if (JustPressed(Keys.F)) CycleAppearance(0, 1, 0, 0);
            if (JustPressed(Keys.H)) CycleAppearance(0, 0, 1, 0);
            if (JustPressed(Keys.S)) CycleAppearance(0, 0, 0, 1);
            
            if (JustPressed(Keys.Escape))
                IsFinished = true;
            
            foreach (var instance in _instances)
                instance.Update(dt);
            
            _prevKeys = keys;
        }
        
        public override void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            var device = GraphicsDevice;
            int w = Game1.ScreenWidth, h = Game1.ScreenHeight;
            
            // The void: near-black with a faint green tint
            device.Clear(new Color(4, 10, 6));
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            device.BlendState = BlendState.Opaque;
            
            var view = Matrix.CreateLookAt(
                new Vector3(0f, 2.6f, _cameraDistance), new Vector3(0f, 1.2f, 0f), Vector3.Up);
            var projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(45f), w / (float)h, 0.05f, 100f);
            
            DrawGrid(device, view, projection);
            
            // The row: players slowly oscillate their facing so faces swing by
            for (int i = 0; i < _instances.Count; i++)
            {
                float x = (i - (_instances.Count - 1) / 2f) * 1.1f;
                float yaw = (float)Math.Sin(_time * 0.6 + i * 1.3) * 0.7f;
                var world = Matrix.CreateScale(0.72f)
                    * Matrix.CreateRotationY(yaw)
                    * Matrix.CreateTranslation(x, 0f, 0f);
                
                // Selected player gets a brighter environment (simple highlight)
                _instances[i].Draw(device, world, view, projection);
            }
            
            // Grid-line under the selected player
            if (_selected < _instances.Count)
            {
                float x = (_selected - (_instances.Count - 1) / 2f) * 1.1f;
                _gridEffect.View = view;
                _gridEffect.Projection = projection;
                _gridEffect.DiffuseColor = new Vector3(0.9f, 0.9f, 0.2f);
                foreach (var pass in _gridEffect.CurrentTechnique.Passes)
                {
                    pass.Apply();
                    var verts = new[]
                    {
                        new VertexPositionColor(new Vector3(x - 0.4f, 0.01f, -0.4f), Color.Yellow),
                        new VertexPositionColor(new Vector3(x + 0.4f, 0.01f, -0.4f), Color.Yellow),
                        new VertexPositionColor(new Vector3(x + 0.4f, 0.01f, 0.4f), Color.Yellow),
                        new VertexPositionColor(new Vector3(x - 0.4f, 0.01f, 0.4f), Color.Yellow),
                        new VertexPositionColor(new Vector3(x - 0.4f, 0.01f, -0.4f), Color.Yellow),
                    };
                    device.DrawUserPrimitives(PrimitiveType.LineStrip, verts, 0, 4);
                }
            }
            
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.AlphaBlend;
            
            // HUD
            spriteBatch.Draw(_pixel, new Rectangle(0, 0, w, 60), new Color(0, 0, 0, 150));
            spriteBatch.DrawString(font, "THE CONSTRUCT", new Vector2(20, 18), new Color(80, 255, 120));
            
            if (_instances.Count > 0 && _selected < _instances.Count)
            {
                var player = _players[_page * PageSize + _selected];
                var a = _appearances[_selected];
                string info = $"#{player.ShirtNumber} {player.Name}  |  expr:{a.Expr} feat:{a.Feat} hair:{a.HairColor} skin:{a.SkinTone}";
                spriteBatch.DrawString(font, info, new Vector2(20, 42), Color.LightGray,
                    0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
            }
            
            string hints = "L/R: select  Up/Down: zoom  E: expression  F: feature  H: hair  S: skin  PgUp/PgDn: page  ESC: exit";
            var hintSize = font.MeasureString(hints) * 0.7f;
            spriteBatch.DrawString(font, hints, new Vector2((w - hintSize.X) / 2, h - 30), Color.Gray,
                0f, Vector2.Zero, 0.7f, SpriteEffects.None, 0f);
        }
        
        /// <summary>Green wireframe grid stretching into the void.</summary>
        private void DrawGrid(GraphicsDevice device, Matrix view, Matrix projection)
        {
            var verts = new List<VertexPositionColor>();
            Color gridColor = new Color(20, 90, 40);
            const int half = 10;
            for (int i = -half; i <= half; i++)
            {
                verts.Add(new VertexPositionColor(new Vector3(i, 0f, -half), gridColor));
                verts.Add(new VertexPositionColor(new Vector3(i, 0f, half), gridColor));
                verts.Add(new VertexPositionColor(new Vector3(-half, 0f, i), gridColor));
                verts.Add(new VertexPositionColor(new Vector3(half, 0f, i), gridColor));
            }
            
            _gridEffect.View = view;
            _gridEffect.Projection = projection;
            _gridEffect.DiffuseColor = Vector3.One;
            foreach (var pass in _gridEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserPrimitives(PrimitiveType.LineList, verts.ToArray(), 0, verts.Count / 2);
            }
        }
    }
}
