using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Broadcast-style perspective camera for the 3D match view.
    /// Positioned to the side of the pitch (positive Z side, matching the 2D
    /// view orientation), elevated, smoothly following the ball.
    /// World units are meters, Y-up.
    /// </summary>
    public class Camera3D
    {
        public Matrix View { get; private set; }
        public Matrix Projection { get; private set; }
        public Vector3 Target { get; private set; }
        public Vector3 Position { get; private set; }
        public int ViewportWidth { get; private set; }
        public int ViewportHeight { get; private set; }
        
        // Base broadcast rig (at CameraZoom = 0.8); scaled by zoom at runtime
        // ~25° elevation, telephoto FOV for a TV-broadcast feel
        // "High" mode is a higher tactical view, further away with a wider FOV
        private const float BroadcastHeight = 11f;
        private const float BroadcastDistance = 24f;
        private const float HighHeight = 22f;
        private const float HighDistance = 36f;
        private const float BaseZoom = 0.8f;
        private static readonly float BroadcastFov = MathHelper.ToRadians(30f);
        private static readonly float HighFov = MathHelper.ToRadians(32f);
        
        private static bool HighMode => GameSettings.Instance.CameraMode == "High";
        private static float CurrentFov => HighMode ? HighFov : BroadcastFov;
        
        public Camera3D(int viewportWidth, int viewportHeight)
        {
            Target = Vector3.Zero;
            Position = new Vector3(0f, BroadcastHeight, BroadcastDistance);
            UpdateViewport(viewportWidth, viewportHeight);
            UpdateView();
        }
        
        public void UpdateViewport(int width, int height)
        {
            ViewportWidth = width;
            ViewportHeight = height;
            Projection = Matrix.CreatePerspectiveFieldOfView(
                CurrentFov,
                width / (float)Math.Max(1, height),
                0.1f,
                600f);
        }
        
        /// <summary>
        /// Project a world position to 2D screen coordinates (for HUD indicators).
        /// Returns null when the point is behind the camera.
        /// </summary>
        public Vector2? WorldToScreen(Vector3 world)
        {
            Vector4 clip = Vector4.Transform(world, View * Projection);
            if (clip.W <= 0.0001f) return null;
            
            float ndcX = clip.X / clip.W;
            float ndcY = clip.Y / clip.W;
            return new Vector2(
                (ndcX + 1f) * 0.5f * ViewportWidth,
                (1f - ndcY) * 0.5f * ViewportHeight);
        }
        
        /// <summary>
        /// Smoothly move the look-at target toward the ball position (engine pixels).
        /// Lerp speed comes from GameSettings.CameraSpeed, rig size from GameSettings.CameraZoom.
        /// </summary>
        public void Follow(Vector2 ballPos2D, float dt)
        {
            Vector3 ballWorld = WorldUnits.ToWorld(ballPos2D);
            
            // Frame-rate independent lerp using the same CameraSpeed setting as the 2D camera
            float speed = Math.Clamp(GameSettings.Instance.CameraSpeed, 0.01f, 1f);
            float t = 1f - (float)Math.Pow(1f - speed, dt * 60f);
            Target = Vector3.Lerp(Target, ballWorld, t);
            
            // Keep the target inside the pitch so the camera stays over the field
            float halfLength = WorldUnits.PitchLengthMeters / 2f;
            float halfWidth = WorldUnits.PitchWidthMeters / 2f;
            Target = new Vector3(
                Math.Clamp(Target.X, -halfLength, halfLength),
                0f,
                Math.Clamp(Target.Z, -halfWidth, halfWidth));
            
            UpdateView();
        }
        
        private void UpdateView()
        {
            // Zoom > base => closer/lower; zoom < base => further/higher
            float zoomScale = BaseZoom / Math.Clamp(GameSettings.Instance.CameraZoom, 0.1f, 2f);
            float height = (HighMode ? HighHeight : BroadcastHeight) * zoomScale;
            float distance = (HighMode ? HighDistance : BroadcastDistance) * zoomScale;
            
            // Camera sits on the POSITIVE Z side, looking toward -Z. This matches
            // the 2D top-down view (and minimap) orientation: engine +X is screen
            // right, engine -Y (up arrow) is into the screen. Watching from -Z
            // would mirror both axes and reverse the controls.
            Position = Target + new Vector3(0f, height, distance);
            
            // Look slightly above and ahead of the ball: lifts the horizon into
            // frame so the stands, crowd, floodlights and sky are visible at the
            // top (classic broadcast framing). Otherwise the top ray stays below
            // the horizon and everything above ~1m at the far side is cut off.
            Vector3 lookAt = Target + new Vector3(0f, 3.5f, -7f);
            View = Matrix.CreateLookAt(Position, lookAt, Vector3.Up);
        }
    }
}
