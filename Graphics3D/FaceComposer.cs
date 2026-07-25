using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Per-player face/appearance composer for the player atlas (512x512,
    /// skin quadrant bottom-right). Rebuilds the face area from scratch:
    /// bold expression stamp (smile/neutral/sad/wow - the old ~4px features
    /// blurred away at broadcast distance), optional facial hair / eyelashes,
    /// skin tone recolor of the flesh pixels, and hair color on the hair texel.
    ///
    /// Appearance is seeded from the player (stable across matches).
    /// The composed atlas replaces the base for kit bakes AND is applied to
    /// the Soccer_Skin/Soccer_Hair parts directly.
    /// </summary>
    public static class FaceComposer
    {
        public enum Expression { Smile, Neutral, Sad, Wow }
        public enum Feature { None, Beard, Goatee, Sideburns, Eyelashes }
        
        /// <summary>Composed atlases are rendered at 512 * AtlasScale px (crisper faces).</summary>
        public const int AtlasScale = 2;
        private const int BaseSize = 512;
        private const int OutSize = BaseSize * AtlasScale;
        
        public readonly struct Appearance
        {
            public readonly int SkinTone;
            public readonly int HairColor;
            public readonly Expression Expr;
            public readonly Feature Feat;
            public Appearance(int skinTone, int hairColor, Expression expr, Feature feat)
            {
                SkinTone = skinTone; HairColor = hairColor; Expr = expr; Feat = feat;
            }
        }
        
        // Face region in the 512 atlas (skin quadrant, face front); the face mesh
        // samples y 307-343, so features live inside that band, clear of the hair
        private const int FaceX = 345, FaceY = 306, FaceW = 55, FaceH = 55;
        // Hair texel block (hair mesh samples a single point at ~(0.938, 0.562))
        private static readonly Rectangle HairBlock = new Rectangle(448, 256, 64, 64);
        
        private static readonly Color[] SkinTones =
        {
            new Color(255, 205, 160), // light (base)
            new Color(240, 184, 140),
            new Color(210, 150, 105),
            new Color(165, 110, 75),
            new Color(120, 75, 50),
        };
        
        private static readonly Color[] HairColors =
        {
            new Color(120, 75, 40),   // brown (base)
            new Color(35, 30, 28),    // black
            new Color(85, 55, 30),    // dark brown
            new Color(190, 160, 90),  // blond
            new Color(150, 70, 40),   // auburn
            new Color(160, 160, 165), // gray
        };
        
        // Reference flesh color of the base atlas (sampled from the skin quadrant)
        private static readonly Color BaseFlesh = new Color(255, 205, 160);
        
        private static readonly Dictionary<string, Texture2D> _cache = new Dictionary<string, Texture2D>();
        
        /// <summary>Stable process-independent hash (String.GetHashCode varies per run).</summary>
        public static int StableHash(string s)
        {
            int h = 0;
            foreach (char c in s ?? "")
                h = h * 31 + c;
            return h & 0x7FFFFFFF;
        }
        
        /// <summary>Same hash rule as MatchRenderer3D.GetModelForPlayer (~1 in 4 female).</summary>
        public static bool IsFemalePlayer(Player player) =>
            (StableHash(player.Name) & 3) == 0;
        
        /// <summary>Stable per-player appearance (hash of name + shirt number).</summary>
        public static Appearance AppearanceFor(Player player)
        {
            int h = StableHash((player.Name ?? "x") + "#" + player.ShirtNumber);
            
            var expr = (Expression)(h % 4);
            bool isFemale = IsFemalePlayer(player);
            Feature feat;
            int f = (h >> 4) % 10;
            if (isFemale)
                feat = f < 4 ? Feature.Eyelashes : Feature.None;
            else
                feat = f < 2 ? Feature.Beard : f < 4 ? Feature.Goatee : f < 6 ? Feature.Sideburns : Feature.None;
            
            return new Appearance(
                (h >> 8) % SkinTones.Length,
                (h >> 12) % HairColors.Length,
                expr, feat);
        }
        
        /// <summary>Composed atlas for the given appearance (cached). Output is
        /// OutSize x OutSize (base upscaled with nearest neighbor, then stamped).</summary>
        public static Texture2D Compose(GraphicsDevice device, Texture2D baseAtlas, Appearance a)
        {
            string key = $"{baseAtlas.GetHashCode():X}:{a.SkinTone}:{a.HairColor}:{a.Expr}:{a.Feat}";
            if (_cache.TryGetValue(key, out var cached))
                return cached;
            
            var src = new Color[baseAtlas.Width * baseAtlas.Height];
            baseAtlas.GetData(src);
            
            // Upscale to OutSize (nearest neighbor keeps the flat palette colors)
            var pixels = new Color[OutSize * OutSize];
            for (int y = 0; y < OutSize; y++)
            {
                int sy = Math.Min(y / AtlasScale, baseAtlas.Height - 1);
                for (int x = 0; x < OutSize; x++)
                {
                    int sx = Math.Min(x / AtlasScale, baseAtlas.Width - 1);
                    pixels[y * OutSize + x] = src[sy * baseAtlas.Width + sx];
                }
            }
            
            // Skin tone: shift flesh-colored pixels (anywhere on the atlas)
            Color tone = SkinTones[a.SkinTone];
            if (a.SkinTone != 0)
            {
                float rRatio = tone.R / (float)BaseFlesh.R;
                float gRatio = tone.G / (float)BaseFlesh.G;
                float bRatio = tone.B / (float)BaseFlesh.B;
                for (int i = 0; i < pixels.Length; i++)
                {
                    if (IsFlesh(pixels[i]))
                    {
                        pixels[i] = new Color(
                            Math.Clamp((int)(pixels[i].R * rRatio), 0, 255),
                            Math.Clamp((int)(pixels[i].G * gRatio), 0, 255),
                            Math.Clamp((int)(pixels[i].B * bRatio), 0, 255),
                            pixels[i].A);
                    }
                }
            }
            
            // Hair color on the hair texel block
            Color hair = HairColors[a.HairColor];
            FillRect(pixels, ScaleRect(HairBlock), hair);
            
            // Clear the face region to the flesh tone, then stamp the expression
            FillRect(pixels, ScaleRect(new Rectangle(FaceX, FaceY, FaceW, FaceH)), tone);
            StampExpression(pixels, a.Expr, tone, hair);
            
            // Facial feature on top
            StampFeature(pixels, a.Feat, hair);
            
            var texture = new Texture2D(device, OutSize, OutSize);
            texture.SetData(pixels);
            var mipmapped = TextureTools.MakeMipmapped(device, texture);
            texture.Dispose();
            _cache[key] = mipmapped;
            return mipmapped;
        }
        
        private static Rectangle ScaleRect(Rectangle r) =>
            new Rectangle(r.X * AtlasScale, r.Y * AtlasScale, r.Width * AtlasScale, r.Height * AtlasScale);
        
        private static int SX(int v) => v * AtlasScale;
        
        /// <summary>Flesh test: close to the base atlas flesh color (tolerant).</summary>
        private static bool IsFlesh(Color p)
        {
            return Math.Abs(p.R - BaseFlesh.R) < 28 &&
                   Math.Abs(p.G - BaseFlesh.G) < 28 &&
                   Math.Abs(p.B - BaseFlesh.B) < 28 &&
                   p.A > 200;
        }
        
        // ---------- expression + feature stamps ----------
        
        private static void StampExpression(Color[] px, Expression expr, Color tone, Color hair)
        {
            Color eyeDark = new Color(25, 22, 20);
            Color glint = new Color(245, 245, 245);
            Color mouthC = new Color(150, 45, 50);
            
            int cx = SX(FaceX + FaceW / 2);
            int eyeY = SX(FaceY + 24);
            int eyeDX = SX(11);
            
            bool wow = expr == Expression.Wow;
            // Eyes: bold ovals with a glint (big enough to survive mipmapping)
            foreach (int sx in new[] { -eyeDX, eyeDX })
            {
                FillEllipse(px, cx + sx, eyeY, SX(wow ? 5 : 4), SX(wow ? 6 : 5), eyeDark);
                FillEllipse(px, cx + sx + SX(1), eyeY - SX(1), SX(1), SX(1), glint);
            }
            
            // Mouth
            int mouthY = SX(FaceY + 36);
            switch (expr)
            {
                case Expression.Smile:
                    DrawArc(px, cx, mouthY - SX(4), SX(8), SX(5), 0.15, 0.85, SX(2), mouthC);
                    break;
                case Expression.Neutral:
                    FillRect(px, new Rectangle(cx - SX(6), mouthY, SX(12), SX(3)), mouthC);
                    break;
                case Expression.Sad:
                    DrawArc(px, cx, mouthY + SX(4), SX(8), SX(5), 1.15, 1.85, SX(2), mouthC);
                    break;
                case Expression.Wow:
                    FillEllipse(px, cx, mouthY, SX(4), SX(5), mouthC);
                    FillEllipse(px, cx, mouthY + SX(1), SX(2), SX(3), new Color(60, 20, 25));
                    break;
            }
        }
        
        private static void StampFeature(Color[] px, Feature feat, Color hair)
        {
            int cx = SX(FaceX + FaceW / 2);
            switch (feat)
            {
                case Feature.Beard:
                    // Jaw + chin coverage below the mouth, faded edges
                    FillEllipse(px, cx, SX(FaceY + 46), SX(15), SX(8), WithAlpha(hair, 220));
                    FillRect(px, new Rectangle(SX(FaceX + 6), SX(FaceY + 36), SX(6), SX(12)), WithAlpha(hair, 200));
                    FillRect(px, new Rectangle(SX(FaceX + FaceW - 12), SX(FaceY + 36), SX(6), SX(12)), WithAlpha(hair, 200));
                    break;
                case Feature.Goatee:
                    FillEllipse(px, cx, SX(FaceY + 45), SX(7), SX(6), WithAlpha(hair, 220));
                    break;
                case Feature.Sideburns:
                    FillRect(px, new Rectangle(SX(FaceX + 4), SX(FaceY + 16), SX(5), SX(18)), WithAlpha(hair, 210));
                    FillRect(px, new Rectangle(SX(FaceX + FaceW - 9), SX(FaceY + 16), SX(5), SX(18)), WithAlpha(hair, 210));
                    break;
                case Feature.Eyelashes:
                    Color lash = new Color(20, 18, 16);
                    int eyeY = SX(FaceY + 24);
                    // small strokes at the outer eye corners
                    foreach (int sx in new[] { -1, 1 })
                    {
                        int ex = cx + SX(sx * 16);
                        FillRect(px, new Rectangle(ex - SX(1), eyeY - SX(5), SX(3), SX(2)), lash);
                        FillRect(px, new Rectangle(ex + (sx > 0 ? SX(1) : -SX(2)), eyeY - SX(7), SX(2), SX(2)), lash);
                    }
                    break;
            }
        }
        
        private static Color WithAlpha(Color c, byte alpha) => new Color(c.R, c.G, c.B, alpha);
        
        // ---------- tiny pixel helpers (all coordinates in OutSize space) ----------
        
        private static void FillRect(Color[] px, Rectangle r, Color c)
        {
            for (int y = r.Y; y < r.Y + r.Height; y++)
            {
                if (y < 0 || y >= OutSize) continue;
                for (int x = r.X; x < r.X + r.Width; x++)
                {
                    if (x < 0 || x >= OutSize) continue;
                    px[y * OutSize + x] = Blend(px[y * OutSize + x], c);
                }
            }
        }
        
        private static void FillEllipse(Color[] px, int cx, int cy, int rx, int ry, Color c)
        {
            for (int y = cy - ry; y <= cy + ry; y++)
            {
                if (y < 0 || y >= OutSize) continue;
                for (int x = cx - rx; x <= cx + rx; x++)
                {
                    if (x < 0 || x >= OutSize) continue;
                    float dx = (x - cx) / (float)rx, dy = (y - cy) / (float)ry;
                    if (dx * dx + dy * dy <= 1f)
                        px[y * OutSize + x] = Blend(px[y * OutSize + x], c);
                }
            }
        }
        
        /// <summary>Thick arc: fraction 0..1 maps left-to-right along an ellipse.</summary>
        private static void DrawArc(Color[] px, int cx, int cy, int rx, int ry,
            double from, double to, int thickness, Color c)
        {
            int steps = 64;
            for (int i = 0; i <= steps; i++)
            {
                double t = from + (to - from) * i / steps;
                double a = Math.PI * t;
                int x = cx + (int)(Math.Cos(a) * rx);
                int y = cy + (int)(Math.Sin(a) * ry);
                FillEllipse(px, x, y, thickness, thickness, c);
            }
        }
        
        private static Color Blend(Color dst, Color src)
        {
            if (src.A >= 250) return src;
            if (src.A == 0) return dst;
            float a = src.A / 255f;
            return new Color(
                (int)(dst.R * (1 - a) + src.R * a),
                (int)(dst.G * (1 - a) + src.G * a),
                (int)(dst.B * (1 - a) + src.B * a),
                Math.Max(dst.A, src.A));
        }
    }
}
