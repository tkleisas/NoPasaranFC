using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using NoPasaranFC.Graphics3D.Skinning;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Renders head-and-shoulders portraits of the skinned player models into
    /// off-screen render targets (used by the lineup screen). The KayKit models
    /// face +Z; the camera sits in front of the face, ~35° above eye level,
    /// looking at the upper torso. The background stays transparent so portraits
    /// composite cleanly over any UI backdrop.
    /// </summary>
    public static class PortraitRenderer
    {
        // Framing for the chibi player models (~2.3 units tall, head center ~1.7).
        private static readonly Vector3 LookAt = new Vector3(0f, 1.35f, 0f);
        private const float CameraDistance = 2.1f;
        private const float CameraElevationDegrees = 35f;
        private const float CameraFovDegrees = 32f;

        /// <summary>
        /// Renders the model (first clip at t=0.1s, bind pose if there are no clips,
        /// with optional per-part texture overrides) to a new size x size render
        /// target and returns it. The caller owns the returned texture. Device
        /// states are restored for the SpriteBatch drawing that screens do.
        /// </summary>
        public static Texture2D RenderPlayerPortrait(GraphicsDevice device, SkinnedModel model,
            Dictionary<string, Texture2D> partTextureOverrides, int size)
        {
            var instance = new SkinnedModelInstance(model);
            if (partTextureOverrides != null)
            {
                foreach (var kvp in partTextureOverrides)
                    instance.SetPartTexture(kvp.Key, kvp.Value);
            }
            instance.Update(0.1f); // settle one step into the first clip

            var target = new RenderTarget2D(device, size, size, false,
                SurfaceFormat.Color, DepthFormat.Depth24);

            float elevation = MathHelper.ToRadians(CameraElevationDegrees);
            Vector3 cameraPosition = LookAt + new Vector3(
                0f,
                (float)Math.Sin(elevation) * CameraDistance,
                (float)Math.Cos(elevation) * CameraDistance);
            Matrix view = Matrix.CreateLookAt(cameraPosition, LookAt, Vector3.Up);
            Matrix projection = Matrix.CreatePerspectiveFieldOfView(
                MathHelper.ToRadians(CameraFovDegrees), 1f, 0.05f, 20f);

            var previousTargets = device.GetRenderTargets();
            device.SetRenderTarget(target);
            device.Clear(Color.Transparent);
            instance.Draw(device, Matrix.Identity, view, projection);
            device.SetRenderTargets(previousTargets);

            // Restore the states SpriteBatch-based screens expect
            device.DepthStencilState = DepthStencilState.None;
            device.BlendState = BlendState.AlphaBlend;
            device.RasterizerState = RasterizerState.CullCounterClockwise;
            device.SamplerStates[0] = SamplerState.LinearClamp;

            return target;
        }
    }
}
