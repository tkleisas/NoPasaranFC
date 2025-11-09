using Microsoft.Xna.Framework;

namespace NoPasaranFC.Gameplay
{
    public class Camera
    {
        public Vector2 Position { get; private set; }
        public float Zoom { get; set; }
        
        private Vector2 _targetPosition;
        private float _smoothSpeed;
        
        public int ViewportWidth { get; set; }
        public int ViewportHeight { get; set; }
        
        public Camera(int viewportWidth, int viewportHeight, float zoom = 1.0f)
        {
            ViewportWidth = viewportWidth;
            ViewportHeight = viewportHeight;
            Zoom = zoom;
            Position = Vector2.Zero;
            _smoothSpeed = 5f;
        }
        
        public void Follow(Vector2 targetPosition, float deltaTime)
        {
            _targetPosition = targetPosition;
            
            // Smooth camera movement
            Position = Vector2.Lerp(Position, _targetPosition, _smoothSpeed * deltaTime);
        }
        
        public Matrix GetTransformMatrix()
        {
            return Matrix.CreateTranslation(-Position.X, -Position.Y, 0) *
                   Matrix.CreateScale(Zoom, Zoom, 1) *
                   Matrix.CreateTranslation(ViewportWidth / 2f, ViewportHeight / 2f, 0);
        }
        
        public Vector2 ScreenToWorld(Vector2 screenPosition)
        {
            Matrix inverseTransform = Matrix.Invert(GetTransformMatrix());
            return Vector2.Transform(screenPosition, inverseTransform);
        }
        
        public Vector2 WorldToScreen(Vector2 worldPosition)
        {
            return Vector2.Transform(worldPosition, GetTransformMatrix());
        }
    }
}
