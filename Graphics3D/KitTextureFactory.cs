using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Builds per-team kit texture variants from the KayKit palette texture.
    /// The armor-gray palette columns (used by the Body/Arms/Legs/Helmet meshes)
    /// are remapped onto a team color while keeping the authored vertical
    /// gradient (shading). Textures are cached so all players of a team share
    /// one variant.
    ///
    /// Palette layout (1024x1024, 16x16 grid analysis): armor grays live in
    /// columns 6-7 (x 384-512), rows 12-15 in UV space (image y 0-256).
    /// </summary>
    public static class KitTextureFactory
    {
        // Armor-gray palette region in image pixels (see class summary)
        private const int RegionX = 384;
        private const int RegionY = 0;
        private const int RegionW = 128;
        private const int RegionH = 256;

        // Luminance remap gain: mid-gray armor (~0.5) maps to the full kit color
        private const float Gain = 1.7f;

        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Returns a texture where the armor region is recolored to kitColor.
        /// Cached per (source texture, color).
        /// </summary>
        public static Texture2D GetKitTexture(GraphicsDevice device, Texture2D baseTexture, Color kitColor)
        {
            string key = $"{baseTexture.GetHashCode()}:{kitColor.PackedValue:X8}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var pixels = new Color[baseTexture.Width * baseTexture.Height];
            baseTexture.GetData(pixels);

            for (int y = RegionY; y < RegionY + RegionH; y++)
            {
                for (int x = RegionX; x < RegionX + RegionW; x++)
                {
                    int i = y * baseTexture.Width + x;
                    Color p = pixels[i];
                    float luminance = (p.R * 0.299f + p.G * 0.587f + p.B * 0.114f) / 255f;
                    float k = Math.Min(1f, luminance * Gain);
                    pixels[i] = new Color(
                        (int)(kitColor.R * k),
                        (int)(kitColor.G * k),
                        (int)(kitColor.B * k),
                        p.A);
                }
            }

            var texture = new Texture2D(device, baseTexture.Width, baseTexture.Height);
            texture.SetData(pixels);
            _cache[key] = texture;
            return texture;
        }

        /// <summary>Darker variant of a kit color, used for shorts/socks.</summary>
        public static Color Darken(Color color, float factor = 0.55f)
        {
            return new Color(
                (int)(color.R * factor),
                (int)(color.G * factor),
                (int)(color.B * factor));
        }
    }
}
