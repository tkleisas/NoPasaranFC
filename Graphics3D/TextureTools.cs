using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Graphics3D
{
    public static class TextureTools
    {
        /// <summary>
        /// Recreates a texture WITH mipmaps, generated in software (box filter).
        /// MonoGame cannot auto-generate mip levels at runtime, and un-mipmapped
        /// atlas textures lose small features (faces!) when sampled at distance.
        /// Returns a new texture; the source is left untouched.
        /// </summary>
        public static Texture2D MakeMipmapped(GraphicsDevice device, Texture2D source)
        {
            int w = source.Width;
            int h = source.Height;
            var level0 = new Color[w * h];
            source.GetData(level0);
            
            var mipmapped = new Texture2D(device, w, h, true, SurfaceFormat.Color);
            mipmapped.SetData(0, 0, null, level0, 0, level0.Length);
            
            Color[] previous = level0;
            int level = 1;
            while (w > 1 || h > 1)
            {
                int pw = w, ph = h;
                w = System.Math.Max(1, pw / 2);
                h = System.Math.Max(1, ph / 2);
                var data = new Color[w * h];
                for (int y = 0; y < h; y++)
                {
                    for (int x = 0; x < w; x++)
                    {
                        // Box filter over the 2x2 block (edge-safe)
                        int sx = x * 2, sy = y * 2;
                        int r = 0, g = 0, b = 0, a = 0, count = 0;
                        for (int dy = 0; dy < 2 && sy + dy < ph; dy++)
                        {
                            for (int dx = 0; dx < 2 && sx + dx < pw; dx++)
                            {
                                Color c = previous[(sy + dy) * pw + (sx + dx)];
                                r += c.R; g += c.G; b += c.B; a += c.A;
                                count++;
                            }
                        }
                        data[y * w + x] = new Color(r / count, g / count, b / count, a / count);
                    }
                }
                mipmapped.SetData(level, 0, null, data, 0, data.Length);
                previous = data;
                level++;
            }
            
            return mipmapped;
        }
    }
}
