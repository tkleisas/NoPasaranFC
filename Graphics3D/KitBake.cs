using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Graphics3D.Skinning;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// The shared kit-bake pipeline: composed atlas -> quadrant recolors
    /// (shirt gets pattern + paint, except for goalkeepers) -> shirt number.
    /// Used by the match renderer, benches, lineup portraits and the editor,
    /// so every surface shows the same kit.
    /// </summary>
    public static class KitBake
    {
        /// <summary>
        /// Bakes the part textures for a player (numbered shirt / shorts / socks /
        /// composed face+hair). `composed` is the FaceComposer output; `team` is
        /// the fallback when player.Team isn't linked (benches, catalog players).
        /// Only meaningful for the soccer-style atlas layout (Player/PlayerF).
        /// </summary>
        public static Dictionary<string, Texture2D> BakePartTextures(GraphicsDevice device,
            SkinnedModel model, Texture2D composed, Team team, Player player, int homeTeamId)
        {
            MatchRenderer3D.GetKitColors(player.Team ?? team, player, homeTeamId,
                out Color shirt, out Color shorts, out Color socks);

            // Patterns/paint are outfield-only; GK kits stay solid
            bool outfield = player.Position != PlayerPosition.Goalkeeper;
            Color patternColor = team != null && team.PatternColor != 0
                ? new Color((team.PatternColor >> 16) & 0xFF, (team.PatternColor >> 8) & 0xFF, team.PatternColor & 0xFF)
                : shirt;
            int pattern = outfield ? team?.ShirtPattern ?? 0 : 0;
            string paint = outfield ? team?.ShirtPaint : null;

            int q = composed.Width / 2;
            Texture2D shirtTexture = KitTextureFactory.GetKitTexture(device, composed, shirt,
                new Rectangle(0, 0, q, q), pattern, patternColor, paint);
            Texture2D shortsTexture = KitTextureFactory.GetKitTexture(device, composed, shorts,
                new Rectangle(q, 0, q, q));
            Texture2D socksTexture = KitTextureFactory.GetKitTexture(device, composed, socks,
                new Rectangle(0, q, q, q));
            Texture2D numberedShirt = KitTextureFactory.GetNumberedShirtTexture(device, shirtTexture,
                player.ShirtNumber, KitTextureFactory.ContrastFor(shirt));

            var overrides = new Dictionary<string, Texture2D>();
            foreach (var part in model.Parts)
            {
                string name = part.Name ?? "";
                if (name == "Soccer_Shirt")
                    overrides[part.Name] = numberedShirt;
                else if (name == "Soccer_Shorts")
                    overrides[part.Name] = shortsTexture;
                else if (name.StartsWith("Soccer_Sock"))
                    overrides[part.Name] = socksTexture;
                else if (name == "Soccer_Skin" || name == "Soccer_Hair")
                    overrides[part.Name] = composed;
            }
            return overrides;
        }

        /// <summary>Applies BakePartTextures to a skinned instance.</summary>
        public static void ApplyKitTextures(GraphicsDevice device, SkinnedModelInstance instance,
            SkinnedModel model, Texture2D composed, Team team, Player player, int homeTeamId)
        {
            foreach (var kv in BakePartTextures(device, model, composed, team, player, homeTeamId))
                instance.SetPartTexture(kv.Key, kv.Value);
        }
    }
}
