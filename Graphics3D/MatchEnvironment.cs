using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Resolved day/night lighting and weather preset for a 3D match.
    /// "Random" settings are resolved once at construction (i.e. once per match).
    /// Exposes the sky clear color, a directional "sun" (fake floodlight at night),
    /// ambient light, and helpers to apply the preset to BasicEffect / SkinnedEffect.
    /// At night it also owns the 4 corner floodlight pylons (unlit bright heads).
    /// </summary>
    public class MatchEnvironment
    {
        public string TimeOfDay { get; }
        public string Weather { get; }
        public Color SkyColor { get; private set; }
        public Vector3 SunDirection { get; private set; }
        public Vector3 SunColor { get; private set; }
        public Vector3 AmbientColor { get; private set; }
        
        /// <summary>Multiplier for unlit (flat colored/textured) geometry.</summary>
        public Vector3 UnlitTint { get; private set; }
        
        public bool IsNight => TimeOfDay == "Night";
        public bool IsRaining => Weather == "Rain";
        
        // Floodlight pylons (night only)
        private BasicEffect _floodlightEffect;
        private VertexPositionColor[] _floodlightVertices;
        private int[] _floodlightIndices;
        
        public MatchEnvironment(GraphicsDevice device, string timeOfDaySetting, string weatherSetting)
        {
            var random = new Random();
            
            TimeOfDay = timeOfDaySetting == "Random"
                ? new[] { "Day", "Sunset", "Night" }[random.Next(3)]
                : (timeOfDaySetting ?? "Day");
            Weather = weatherSetting == "Random"
                ? (random.Next(2) == 0 ? "Clear" : "Rain")
                : (weatherSetting ?? "Clear");
            
            switch (TimeOfDay)
            {
                case "Sunset":
                    // Warm low sun from a goal end, orange/pink sky
                    SkyColor = new Color(233, 140, 90);
                    SunDirection = Vector3.Normalize(new Vector3(0.85f, -0.3f, 0.15f));
                    SunColor = new Vector3(0.9f, 0.55f, 0.32f);
                    AmbientColor = new Vector3(0.5f, 0.35f, 0.3f);
                    UnlitTint = new Color(242, 204, 179).ToVector3();
                    break;
                case "Night":
                    // Dark navy sky, cool dim ambient, bluish "floodlight" from above
                    SkyColor = new Color(8, 12, 35);
                    SunDirection = Vector3.Normalize(new Vector3(-0.2f, -1f, 0.3f));
                    SunColor = new Vector3(0.55f, 0.6f, 0.75f);
                    AmbientColor = new Vector3(0.22f, 0.25f, 0.38f);
                    UnlitTint = new Color(140, 153, 191).ToVector3();
                    break;
                default: // "Day" - the current neutral look
                    SkyColor = new Color(100, 149, 237); // Cornflower blue
                    SunDirection = Vector3.Normalize(new Vector3(-0.35f, -0.8f, 0.25f));
                    SunColor = Vector3.One;
                    AmbientColor = new Vector3(0.45f);
                    UnlitTint = Vector3.One;
                    break;
            }
            
            // Overcast: rain grays out the sky and dims everything (night stays dark)
            if (IsRaining && !IsNight)
            {
                SkyColor = new Color(
                    (SkyColor.R + 110) / 2,
                    (SkyColor.G + 115) / 2,
                    (SkyColor.B + 125) / 2);
                SunColor *= 0.5f;
                AmbientColor *= 0.8f;
                UnlitTint *= 0.8f;
            }
            
            if (IsNight)
                BuildFloodlights(device);
        }
        
        /// <summary>
        /// Apply the preset to a BasicEffect. Lit effects (geometry with normals)
        /// get the directional sun + ambient; unlit effects get the flat tint.
        /// </summary>
        public void ApplyTo(BasicEffect effect, bool lit)
        {
            if (lit)
            {
                effect.LightingEnabled = true;
                effect.AmbientLightColor = AmbientColor;
                effect.DirectionalLight0.Enabled = true;
                effect.DirectionalLight0.Direction = SunDirection;
                effect.DirectionalLight0.DiffuseColor = SunColor;
                effect.DirectionalLight0.SpecularColor = Vector3.Zero;
                effect.DirectionalLight1.Enabled = false;
                effect.DirectionalLight2.Enabled = false;
            }
            else
            {
                effect.LightingEnabled = false;
                effect.DiffuseColor = UnlitTint;
            }
        }
        
        /// <summary>Apply the preset to a SkinnedEffect (3D players).</summary>
        public void ApplyTo(SkinnedEffect effect)
        {
            effect.AmbientLightColor = AmbientColor;
            effect.DirectionalLight0.Enabled = true;
            effect.DirectionalLight0.Direction = SunDirection;
            effect.DirectionalLight0.DiffuseColor = SunColor;
            effect.DirectionalLight0.SpecularColor = Vector3.Zero;
            effect.DirectionalLight1.Enabled = false;
            effect.DirectionalLight2.Enabled = false;
        }
        
        /// <summary>Tint a color for the current preset (for effects driven per-frame).</summary>
        public Vector3 ApplyTint(Vector3 color)
        {
            return color * UnlitTint;
        }
        
        /// <summary>Draws the floodlight pylons (night matches only).</summary>
        public void Draw(GraphicsDevice device, Matrix view, Matrix projection)
        {
            if (_floodlightEffect == null) return;
            
            _floodlightEffect.View = view;
            _floodlightEffect.Projection = projection;
            _floodlightEffect.World = Matrix.Identity;
            
            device.BlendState = BlendState.Opaque;
            device.DepthStencilState = DepthStencilState.Default;
            device.RasterizerState = RasterizerState.CullNone;
            
            foreach (var pass in _floodlightEffect.CurrentTechnique.Passes)
            {
                pass.Apply();
                device.DrawUserIndexedPrimitives(PrimitiveType.TriangleList,
                    _floodlightVertices, 0, _floodlightVertices.Length,
                    _floodlightIndices, 0, _floodlightIndices.Length / 3);
            }
        }
        
        #region Floodlight pylons
        
        private void BuildFloodlights(GraphicsDevice device)
        {
            _floodlightEffect = new BasicEffect(device)
            {
                VertexColorEnabled = true,
                TextureEnabled = false,
                LightingEnabled = false
            };
            
            var verts = new List<VertexPositionColor>();
            var indices = new List<int>();
            
            float gap = WorldUnits.StadiumMarginMeters;
            float pylonX = WorldUnits.PitchLengthMeters / 2f + gap + 16f;
            float pylonZ = WorldUnits.PitchWidthMeters / 2f + gap + 16f;
            const float pylonHeight = 20f;
            Color pylonColor = new Color(30, 30, 35);
            Color headColor = new Color(255, 255, 230); // Emissive look (unlit, bright)
            
            foreach (float sx in new[] { -1f, 1f })
            {
                foreach (float sz in new[] { -1f, 1f })
                {
                    float cx = sx * pylonX;
                    float cz = sz * pylonZ;
                    
                    // Pylon mast
                    AddBox(verts, indices,
                        new Vector3(cx - 0.4f, 0f, cz - 0.4f),
                        new Vector3(cx + 0.4f, pylonHeight, cz + 0.4f), pylonColor);
                    
                    // Bright head panel facing the pitch
                    AddBox(verts, indices,
                        new Vector3(cx - 1.5f, pylonHeight - 1.2f, cz - 0.3f),
                        new Vector3(cx + 1.5f, pylonHeight + 0.3f, cz + 0.3f), headColor);
                }
            }
            
            _floodlightVertices = verts.ToArray();
            _floodlightIndices = indices.ToArray();
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
