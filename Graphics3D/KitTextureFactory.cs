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


        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();

        /// <summary>
        /// Returns a texture where the given region is recolored to kitColor
        /// (luminance-preserving). Cached per (source texture, region, color).
        /// </summary>
        public static Texture2D GetKitTexture(GraphicsDevice device, Texture2D baseTexture, Color kitColor,
            Rectangle? region = null)
        {
            Rectangle r = region ?? new Rectangle(RegionX, RegionY, RegionW, RegionH);
            string key = $"{baseTexture.GetHashCode()}:{kitColor.PackedValue:X8}:{r.X},{r.Y},{r.Width},{r.Height}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var pixels = new Color[baseTexture.Width * baseTexture.Height];
            baseTexture.GetData(pixels);

            // Normalize by the region's max luminance so the brightest authored
            // shade maps exactly onto the kit color (white kits stay white even
            // in regions authored dark, and vice versa).
            float maxLuminance = 0.01f;
            for (int y = r.Y; y < r.Y + r.Height; y++)
            {
                for (int x = r.X; x < r.X + r.Width; x++)
                {
                    Color p = pixels[y * baseTexture.Width + x];
                    float l = (p.R * 0.299f + p.G * 0.587f + p.B * 0.114f) / 255f;
                    if (l > maxLuminance) maxLuminance = l;
                }
            }
            
            for (int y = r.Y; y < r.Y + r.Height; y++)
            {
                for (int x = r.X; x < r.X + r.Width; x++)
                {
                    int i = y * baseTexture.Width + x;
                    Color p = pixels[i];
                    float luminance = (p.R * 0.299f + p.G * 0.587f + p.B * 0.114f) / 255f;
                    float k = Math.Min(1f, luminance / maxLuminance);
                    pixels[i] = new Color(
                        (int)(kitColor.R * k),
                        (int)(kitColor.G * k),
                        (int)(kitColor.B * k),
                        p.A);
                }
            }

            var texture = new Texture2D(device, baseTexture.Width, baseTexture.Height);
            texture.SetData(pixels);
            var mipmapped = TextureTools.MakeMipmapped(device, texture);
            texture.Dispose();
            _cache[key] = mipmapped;
            return mipmapped;
        }

        /// <summary>Darker variant of a kit color, used for shorts/socks.</summary>
        public static Color Darken(Color color, float factor = 0.55f)
        {
            return new Color(
                (int)(color.R * factor),
                (int)(color.G * factor),
                (int)(color.B * factor));
        }
        
        #region Shirt numbers
        
        // Number stamp zone on the Player.glb shirt back (image px in the 512x512
        // atlas, measured from the mesh UVs): mid-torso back, reads correctly
        // (not mirrored) when viewed from behind.
        private static readonly Point ShirtBackCenter = new Point(184, 128);
        private const int DigitBlock = 7;   // px per font block
        private const int DigitGap = 6;     // px between digits
        
        // 3x5 block font
        private static readonly string[] DigitGlyphs =
        {
            "111101101101111", // 0
            "010110010010111", // 1
            "111001111100111", // 2
            "111001111001111", // 3
            "101101111001001", // 4
            "111100111001111", // 5
            "111100111101111", // 6
            "111001001010010", // 7
            "111101111101111", // 8
            "111101111001111", // 9
        };
        
        /// <summary>
        /// Returns a copy of the (already team-colored) shirt texture with the
        /// player's shirt number stamped on the back. Cached per texture/number/color.
        /// Only meaningful for the SoccerPlayer atlas layout.
        /// </summary>
        public static Texture2D GetNumberedShirtTexture(GraphicsDevice device, Texture2D shirtTexture,
            int shirtNumber, Color digitColor)
        {
            string key = $"num:{shirtTexture.GetHashCode()}:{shirtNumber}:{digitColor.PackedValue:X8}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;
            
            var pixels = new Color[shirtTexture.Width * shirtTexture.Height];
            shirtTexture.GetData(pixels);
            
            string digits = Math.Clamp(shirtNumber, 1, 99).ToString();
            int digitWidth = 3 * DigitBlock;
            int digitHeight = 5 * DigitBlock;
            int totalWidth = digits.Length * digitWidth + (digits.Length - 1) * DigitGap;
            int startX = ShirtBackCenter.X - totalWidth / 2;
            int startY = ShirtBackCenter.Y - digitHeight / 2;
            
            for (int d = 0; d < digits.Length; d++)
            {
                string glyph = DigitGlyphs[digits[d] - '0'];
                int baseX = startX + d * (digitWidth + DigitGap);
                for (int row = 0; row < 5; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        if (glyph[row * 3 + col] != '1') continue;
                        for (int by = 0; by < DigitBlock; by++)
                        {
                            for (int bx = 0; bx < DigitBlock; bx++)
                            {
                                int x = baseX + col * DigitBlock + bx;
                                int y = startY + row * DigitBlock + by;
                                if (x >= 0 && x < shirtTexture.Width && y >= 0 && y < shirtTexture.Height)
                                    pixels[y * shirtTexture.Width + x] = digitColor;
                            }
                        }
                    }
                }
            }
            
            var texture = new Texture2D(device, shirtTexture.Width, shirtTexture.Height);
            texture.SetData(pixels);
            var mipmapped = TextureTools.MakeMipmapped(device, texture);
            texture.Dispose();
            _cache[key] = mipmapped;
            return mipmapped;
        }
        
        /// <summary>Readable digit color for a kit: black on light shirts, white on dark.</summary>
        public static Color ContrastFor(Color kitColor)
        {
            float luminance = (kitColor.R * 0.299f + kitColor.G * 0.587f + kitColor.B * 0.114f) / 255f;
            return luminance > 0.55f ? new Color(20, 20, 20) : new Color(245, 245, 245);
        }
        
        #endregion
    }
}
