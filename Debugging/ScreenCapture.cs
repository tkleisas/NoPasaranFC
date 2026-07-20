using System;
using System.IO;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;

namespace NoPasaranFC.Debugging
{
    /// <summary>
    /// Captures the fully composited frame (3D scene + SpriteBatch UI) by having
    /// Game1 render into a render target for one frame, then saving it as PNG
    /// and blitting it to the back buffer so presentation is unaffected.
    /// </summary>
    public class ScreenCapture
    {
        private RenderTarget2D _renderTarget;
        private SpriteBatch _blitBatch;
        private string _pendingPath;
        private int _framesRemaining;
        private Action<bool, string> _onComplete;

        public bool CaptureInProgress => _pendingPath != null;

        /// <summary>Queue a capture. delayFrames lets transitions/animations settle first.</summary>
        public void Request(string path, int delayFrames, Action<bool, string> onComplete)
        {
            _pendingPath = path;
            _framesRemaining = Math.Max(0, delayFrames);
            _onComplete = onComplete;
        }

        /// <summary>
        /// Called at the start of Game1.Draw. If this frame should be captured,
        /// redirects rendering to the internal render target and returns it;
        /// otherwise returns null and rendering proceeds to the back buffer.
        /// </summary>
        public RenderTarget2D BeginFrame(GraphicsDevice device)
        {
            if (_pendingPath == null)
                return null;
            if (_framesRemaining-- > 0)
                return null;

            int w = device.PresentationParameters.BackBufferWidth;
            int h = device.PresentationParameters.BackBufferHeight;
            if (_renderTarget == null || _renderTarget.Width != w || _renderTarget.Height != h)
            {
                _renderTarget?.Dispose();
                _renderTarget = new RenderTarget2D(device, w, h, false, SurfaceFormat.Color, DepthFormat.Depth24);
            }
            device.SetRenderTarget(_renderTarget);
            return _renderTarget;
        }

        /// <summary>Called at the end of Game1.Draw when BeginFrame returned a target.</summary>
        public void EndFrame(GraphicsDevice device)
        {
            device.SetRenderTarget(null);

            // Blit so the user still sees the frame.
            _blitBatch ??= new SpriteBatch(device);
            _blitBatch.Begin(SpriteSortMode.Immediate, BlendState.Opaque);
            _blitBatch.Draw(_renderTarget, device.Viewport.Bounds, Color.White);
            _blitBatch.End();

            bool ok = false;
            string path = _pendingPath;
            try
            {
                string fullPath = Path.GetFullPath(path);
                Directory.CreateDirectory(Path.GetDirectoryName(fullPath));
                using var stream = File.Create(fullPath);
                _renderTarget.SaveAsPng(stream, _renderTarget.Width, _renderTarget.Height);
                path = fullPath;
                ok = true;
            }
            catch
            {
                // reported via callback
            }

            var callback = _onComplete;
            _pendingPath = null;
            _onComplete = null;
            callback?.Invoke(ok, path);
        }
    }
}
