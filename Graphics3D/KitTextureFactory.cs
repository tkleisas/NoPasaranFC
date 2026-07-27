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
        /// (luminance-preserving), with optional shirt pattern + paint grid
        /// stamped on top. Cached per (source texture, region, color, pattern, paint).
        /// </summary>
        public static Texture2D GetKitTexture(GraphicsDevice device, Texture2D baseTexture, Color kitColor,
            Rectangle? region = null, int pattern = 0, Color? patternColor = null, string paintHex = null)
        {
            Rectangle r = region ?? new Rectangle(RegionX, RegionY, RegionW, RegionH);
            string key = $"{baseTexture.GetHashCode()}:{kitColor.PackedValue:X8}:{r.X},{r.Y},{r.Width},{r.Height}" +
                $":{pattern}:{patternColor?.PackedValue ?? 0:X8}:{paintHex?.GetHashCode() ?? 0}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;

            var pixels = new Color[baseTexture.Width * baseTexture.Height];
            baseTexture.GetData(pixels);
            RecolorPixels(pixels, baseTexture.Width, r, kitColor);
            if (pattern != 0 && patternColor.HasValue)
                StampPatternPixels(pixels, baseTexture.Width, r, pattern, patternColor.Value);
            if (!string.IsNullOrEmpty(paintHex))
                ApplyPaintPixels(pixels, baseTexture.Width, r, paintHex);

            var texture = new Texture2D(device, baseTexture.Width, baseTexture.Height);
            texture.SetData(pixels);
            var mipmapped = TextureTools.MakeMipmapped(device, texture);
            texture.Dispose();
            _cache[key] = mipmapped;
            return mipmapped;
        }
        
        /// <summary>
        /// Pure recolor pipeline (no GraphicsDevice): remaps the region onto
        /// kitColor while preserving the authored luminance gradient (brightest
        /// shade maps exactly onto the kit color). Separated for headless tests.
        /// </summary>
        public static void RecolorPixels(Color[] pixels, int width, Rectangle r, Color kitColor)
        {
            // Normalize by the region's max luminance so the brightest authored
            // shade maps exactly onto the kit color (white kits stay white even
            // in regions authored dark, and vice versa).
            float maxLuminance = 0.01f;
            for (int y = r.Y; y < r.Y + r.Height; y++)
            {
                for (int x = r.X; x < r.X + r.Width; x++)
                {
                    Color p = pixels[y * width + x];
                    float l = (p.R * 0.299f + p.G * 0.587f + p.B * 0.114f) / 255f;
                    if (l > maxLuminance) maxLuminance = l;
                }
            }
            
            for (int y = r.Y; y < r.Y + r.Height; y++)
            {
                for (int x = r.X; x < r.X + r.Width; x++)
                {
                    int i = y * width + x;
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
        }

        #region Shirt patterns & freehand paint

        /// <summary>Shirt patterns (Team.ShirtPattern).</summary>
        public enum ShirtPattern { Solid = 0, StripesV = 1, Hoops = 2, Halves = 3, Sash = 4 }

        /// <summary>The 16-color palette for kit colors and shirt painting.</summary>
        public static readonly Color[] PaintPalette =
        {
            new Color(245, 245, 245), // 1 white
            new Color(25, 25, 30),    // 2 black
            new Color(224, 0, 0),     // 3 red
            new Color(0, 64, 160),    // 4 blue
            new Color(0, 140, 60),    // 5 green
            new Color(240, 200, 30),  // 6 yellow
            new Color(240, 130, 20),  // 7 orange
            new Color(128, 60, 160),  // 8 purple
            new Color(140, 140, 150), // 9 gray
            new Color(0, 30, 80),     // 10 navy
            new Color(90, 180, 240),  // 11 sky
            new Color(240, 120, 170), // 12 pink
            new Color(120, 70, 30),   // 13 brown
            new Color(120, 200, 80),  // 14 lime
            new Color(220, 170, 60),  // 15 gold
            new Color(0, 110, 110),   // 16 teal
        };

        /// <summary>
        /// Stamps a shirt pattern over `region` with the secondary color.
        /// Pure pixel pipeline (headless-testable).
        /// </summary>
        public static void StampPatternPixels(Color[] pixels, int width, Rectangle r, int pattern, Color patternColor)
        {
            if (pattern == (int)ShirtPattern.Solid) return;
            int stripeW = System.Math.Max(2, r.Width / 8);
            int stripeH = System.Math.Max(2, r.Height / 8);
            int sashBand = System.Math.Max(2, r.Width / 6);

            for (int y = r.Y; y < r.Y + r.Height; y++)
            {
                for (int x = r.X; x < r.X + r.Width; x++)
                {
                    int i = y * width + x;
                    if (pixels[i].A < 10) continue; // keep transparent background

                    int lx = x - r.X, ly = y - r.Y;
                    bool paint = pattern switch
                    {
                        (int)ShirtPattern.StripesV => (lx / stripeW) % 2 == 1,
                        (int)ShirtPattern.Hoops => (ly / stripeH) % 2 == 1,
                        (int)ShirtPattern.Halves => lx >= r.Width / 2,
                        (int)ShirtPattern.Sash =>
                            System.Math.Abs(lx - (r.Width - 1 - ly * r.Width / System.Math.Max(1, r.Height))) < sashBand / 2,
                        _ => false,
                    };
                    if (paint)
                        pixels[i] = new Color(patternColor.R, patternColor.G, patternColor.B, pixels[i].A);
                }
            }
        }

        /// <summary>
        /// Applies the freehand paint grid over `region`. The grid is 32x32
        /// cells of 4-bit palette indices (1024 hex chars; '0' = empty).
        /// Pure pixel pipeline (headless-testable).
        /// </summary>
        public static void ApplyPaintPixels(Color[] pixels, int width, Rectangle r, string paintHex)
        {
            if (string.IsNullOrEmpty(paintHex) || paintHex.Length < 1024) return;
            float cellW = r.Width / 32f, cellH = r.Height / 32f;

            for (int cy = 0; cy < 32; cy++)
            {
                for (int cx = 0; cx < 32; cx++)
                {
                    int v = HexValue(paintHex[cy * 32 + cx]);
                    if (v <= 0 || v > PaintPalette.Length) continue;
                    var color = PaintPalette[v - 1];

                    int x0 = r.X + (int)(cx * cellW), x1 = r.X + (int)((cx + 1) * cellW);
                    int y0 = r.Y + (int)(cy * cellH), y1 = r.Y + (int)((cy + 1) * cellH);
                    for (int y = y0; y < y1; y++)
                        for (int x = x0; x < x1; x++)
                        {
                            int i = y * width + x;
                            if (pixels[i].A >= 10)
                                pixels[i] = new Color(color.R, color.G, color.B, pixels[i].A);
                        }
                }
            }
        }

        private static int HexValue(char c) =>
            c >= '0' && c <= '9' ? c - '0' :
            c >= 'a' && c <= 'f' ? c - 'a' + 10 :
            c >= 'A' && c <= 'F' ? c - 'A' + 10 : 0;

        #endregion

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
        /// Only meaningful for the SoccerPlayer atlas layout. Scale-aware: works
        /// with 512x512 and higher-resolution composed atlases.
        /// </summary>
        public static Texture2D GetNumberedShirtTexture(GraphicsDevice device, Texture2D shirtTexture,
            int shirtNumber, Color digitColor)
        {
            string key = $"num:{shirtTexture.GetHashCode()}:{shirtNumber}:{digitColor.PackedValue:X8}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;
            
            var pixels = new Color[shirtTexture.Width * shirtTexture.Height];
            shirtTexture.GetData(pixels);
            StampNumberPixels(pixels, shirtTexture.Width, shirtTexture.Height, shirtNumber, digitColor);
            
            var texture = new Texture2D(device, shirtTexture.Width, shirtTexture.Height);
            texture.SetData(pixels);
            var mipmapped = TextureTools.MakeMipmapped(device, texture);
            texture.Dispose();
            _cache[key] = mipmapped;
            return mipmapped;
        }
        
        /// <summary>Pure number-stamp pipeline (no GraphicsDevice): draws the
        /// shirt number in the 3x5 block font centered on the shirt back.
        /// Separated for headless tests.</summary>
        public static void StampNumberPixels(Color[] pixels, int width, int height, int shirtNumber, Color digitColor)
        {
            int scale = Math.Max(1, width / 512);
            int centerX = ShirtBackCenter.X * scale;
            int centerY = ShirtBackCenter.Y * scale;
            int block = DigitBlock * scale;
            int gap = DigitGap * scale;
            
            string digits = Math.Clamp(shirtNumber, 1, 99).ToString();
            int digitWidth = 3 * block;
            int digitHeight = 5 * block;
            int totalWidth = digits.Length * digitWidth + (digits.Length - 1) * gap;
            int startX = centerX - totalWidth / 2;
            int startY = centerY - digitHeight / 2;
            
            for (int d = 0; d < digits.Length; d++)
            {
                string glyph = DigitGlyphs[digits[d] - '0'];
                int baseX = startX + d * (digitWidth + gap);
                for (int row = 0; row < 5; row++)
                {
                    for (int col = 0; col < 3; col++)
                    {
                        if (glyph[row * 3 + col] != '1') continue;
                        for (int by = 0; by < block; by++)
                        {
                            for (int bx = 0; bx < block; bx++)
                            {
                                int x = baseX + col * block + bx;
                                int y = startY + row * block + by;
                                if (x >= 0 && x < width && y >= 0 && y < height)
                                    pixels[y * width + x] = digitColor;
                            }
                        }
                    }
                }
            }
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
