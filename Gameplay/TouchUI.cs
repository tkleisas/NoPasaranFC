using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Graphics;
using Microsoft.Xna.Framework.Input.Touch;

namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Centralized touch UI system for virtual controls on all screens
    /// </summary>
    public class TouchUI
    {
        private static TouchUI _instance;
        public static TouchUI Instance => _instance ??= new TouchUI();
        
        private Texture2D _pixel;
        private bool _initialized;
        
        // Screen dimensions
        public int ScreenWidth { get; private set; } = 1280;
        public int ScreenHeight { get; private set; } = 720;
        
        // UI Scale factor for high DPI screens
        public float UIScale { get; private set; } = 1f;
        
        // Virtual joystick state
        private Vector2 _joystickCenter;
        private Vector2 _joystickPosition;
        private bool _joystickActive;
        private int _joystickTouchId = -1;
        private const float JoystickMaxDistance = 120f; // Increased from 80 to 120 (150%)
        
        // Button areas
        private Rectangle _actionButtonArea;  // A button (confirm/shoot)
        private Rectangle _backButtonArea;    // B button (back/cancel)
        private Rectangle _switchButtonArea;  // X button (switch player)
        
        // Button states
        private bool _actionPressed;
        private bool _actionPreviousPressed;
        private bool _backPressed;
        private bool _backPreviousPressed;
        private bool _switchPressed;
        private bool _switchPreviousPressed;
        
        // Touch state
        private TouchCollection _currentTouchState;
        private TouchCollection _previousTouchState;
        
        // Public properties for input checking
        public bool IsActionPressed => _actionPressed;
        public bool IsActionJustPressed => _actionPressed && !_actionPreviousPressed;
        public bool IsBackPressed => _backPressed;
        public bool IsBackJustPressed => _backPressed && !_backPreviousPressed;
        public bool IsSwitchPressed => _switchPressed;
        public bool IsSwitchJustPressed => _switchPressed && !_switchPreviousPressed;
        
        public Vector2 JoystickDirection
        {
            get
            {
                if (!_joystickActive) return Vector2.Zero;
                
                Vector2 dir = _joystickPosition - _joystickCenter;
                float length = dir.Length();
                if (length < 10f) return Vector2.Zero;
                
                // Normalize and scale
                dir /= JoystickMaxDistance;
                if (dir.Length() > 1f) dir.Normalize();
                return dir;
            }
        }
        
        public bool JoystickActive => _joystickActive;
        public Vector2 JoystickCenter => _joystickCenter;
        public Vector2 JoystickThumbPosition => _joystickPosition;
        
        // Button areas for rendering
        public Rectangle ActionButtonArea => _actionButtonArea;
        public Rectangle BackButtonArea => _backButtonArea;
        public Rectangle SwitchButtonArea => _switchButtonArea;
        
        public bool Enabled { get; set; }
        
        private TouchUI()
        {
#if ANDROID
            Enabled = true;
#else
            Enabled = false;
#endif
        }
        
        public void Initialize(GraphicsDevice graphicsDevice)
        {
            if (_initialized) return;
            
            _pixel = new Texture2D(graphicsDevice, 1, 1);
            _pixel.SetData(new[] { Color.White });
            _initialized = true;
        }
        
        public void UpdateScreenSize(int width, int height)
        {
            ScreenWidth = width;
            ScreenHeight = height;
            
            // Calculate UI scale based on screen density
            // Base resolution is 1280x720, scale up for higher resolutions
            float baseHeight = 720f;
            UIScale = Math.Max(1f, height / baseHeight);
            
            // Clamp scale to reasonable values
            UIScale = Math.Clamp(UIScale, 1f, 3f);
            
            UpdateButtonAreas();
        }
        
        private void UpdateButtonAreas()
        {
            int buttonSize = (int)(100 * UIScale);
            int padding = (int)(20 * UIScale);
            int smallButtonSize = (int)(70 * UIScale);
            
            // Action button (A) - bottom right, largest
            _actionButtonArea = new Rectangle(
                ScreenWidth - buttonSize - padding,
                ScreenHeight - buttonSize - padding,
                buttonSize,
                buttonSize
            );
            
            // Switch button (X) - above action button
            _switchButtonArea = new Rectangle(
                ScreenWidth - buttonSize - padding,
                ScreenHeight - buttonSize * 2 - padding * 2,
                buttonSize,
                buttonSize
            );
            
            // Back button (B) - to the left of action button
            _backButtonArea = new Rectangle(
                ScreenWidth - buttonSize * 2 - padding * 2,
                ScreenHeight - buttonSize - padding,
                smallButtonSize,
                smallButtonSize
            );
            
            // Default joystick center (bottom left)
            _joystickCenter = new Vector2(
                padding + buttonSize,
                ScreenHeight - padding - buttonSize
            );
        }
        
        public void Update()
        {
            if (!Enabled) return;
            
            _previousTouchState = _currentTouchState;
            _actionPreviousPressed = _actionPressed;
            _backPreviousPressed = _backPressed;
            _switchPreviousPressed = _switchPressed;
            
            _currentTouchState = TouchPanel.GetState();
            
            _actionPressed = false;
            _backPressed = false;
            _switchPressed = false;
            
            bool joystickTouchFound = false;
            
            foreach (TouchLocation touch in _currentTouchState)
            {
                Point touchPoint = new Point((int)touch.Position.X, (int)touch.Position.Y);
                
                // Check buttons
                if (_actionButtonArea.Contains(touchPoint))
                {
                    _actionPressed = true;
                }
                else if (_backButtonArea.Contains(touchPoint))
                {
                    _backPressed = true;
                }
                else if (_switchButtonArea.Contains(touchPoint))
                {
                    _switchPressed = true;
                }
                // Virtual joystick - left half of screen
                else if (touchPoint.X < ScreenWidth / 2)
                {
                    if (touch.State == TouchLocationState.Pressed && !_joystickActive)
                    {
                        _joystickActive = true;
                        _joystickTouchId = touch.Id;
                        _joystickCenter = touch.Position;
                        _joystickPosition = touch.Position;
                        joystickTouchFound = true;
                    }
                    else if (touch.Id == _joystickTouchId && _joystickActive)
                    {
                        _joystickPosition = touch.Position;
                        joystickTouchFound = true;
                    }
                }
                
                // Track joystick even if it moves to right side
                if (touch.Id == _joystickTouchId && _joystickActive)
                {
                    _joystickPosition = touch.Position;
                    joystickTouchFound = true;
                }
            }
            
            if (!joystickTouchFound)
            {
                _joystickActive = false;
                _joystickTouchId = -1;
            }
        }
        
        /// <summary>
        /// Check if a tap occurred at the given position this frame
        /// </summary>
        public Vector2? GetTapPosition()
        {
            if (!Enabled) return null;
            
            foreach (TouchLocation touch in _currentTouchState)
            {
                if (touch.State == TouchLocationState.Pressed)
                {
                    TouchLocation prevTouch;
                    if (!touch.TryGetPreviousLocation(out prevTouch) || prevTouch.State == TouchLocationState.Invalid)
                    {
                        // Don't count taps on control buttons
                        Point p = new Point((int)touch.Position.X, (int)touch.Position.Y);
                        if (!_actionButtonArea.Contains(p) && 
                            !_backButtonArea.Contains(p) && 
                            !_switchButtonArea.Contains(p) &&
                            touch.Position.X > ScreenWidth / 3) // Not on joystick area
                        {
                            return touch.Position;
                        }
                    }
                }
            }
            return null;
        }
        
        /// <summary>
        /// Draw the virtual controls overlay
        /// </summary>
        public void Draw(SpriteBatch spriteBatch, SpriteFont font)
        {
            if (!Enabled || !_initialized) return;
            
            Color buttonColor = new Color(255, 255, 255, 80);
            Color buttonPressedColor = new Color(255, 255, 255, 160);
            Color textColor = new Color(255, 255, 255, 200);
            
            // Draw joystick
            float joystickScale = 1.5f; // 150% size
            if (_joystickActive)
            {
                // Outer circle
                DrawFilledCircle(spriteBatch, _joystickCenter, JoystickMaxDistance * UIScale * joystickScale, new Color(80, 80, 80, 60));
                
                // Thumb
                Vector2 thumbPos = _joystickPosition;
                Vector2 dir = thumbPos - _joystickCenter;
                float maxDist = JoystickMaxDistance * UIScale * 0.8f;
                if (dir.Length() > maxDist)
                {
                    dir = Vector2.Normalize(dir) * maxDist;
                    thumbPos = _joystickCenter + dir;
                }
                DrawFilledCircle(spriteBatch, thumbPos, 60 * UIScale * joystickScale, new Color(200, 200, 200, 120));
            }
            else
            {
                // Draw joystick hint
                Vector2 hintPos = new Vector2(
                    20 * UIScale + 120 * UIScale * joystickScale,
                    ScreenHeight - 20 * UIScale - 120 * UIScale * joystickScale
                );
                DrawFilledCircle(spriteBatch, hintPos, 90 * UIScale * joystickScale, new Color(80, 80, 80, 40));
            }
            
            // Draw action button (A)
            Color actionColor = _actionPressed ? buttonPressedColor : buttonColor;
            DrawFilledCircle(spriteBatch, 
                new Vector2(_actionButtonArea.Center.X, _actionButtonArea.Center.Y), 
                _actionButtonArea.Width / 2, actionColor);
            DrawButtonLabel(spriteBatch, font, "A", _actionButtonArea, textColor);
            
            // Draw back button (B)
            Color backColor = _backPressed ? buttonPressedColor : buttonColor;
            DrawFilledCircle(spriteBatch, 
                new Vector2(_backButtonArea.Center.X, _backButtonArea.Center.Y), 
                _backButtonArea.Width / 2, backColor);
            DrawButtonLabel(spriteBatch, font, "B", _backButtonArea, textColor);
            
            // Draw switch button (X)
            Color switchColor = _switchPressed ? buttonPressedColor : buttonColor;
            DrawFilledCircle(spriteBatch, 
                new Vector2(_switchButtonArea.Center.X, _switchButtonArea.Center.Y), 
                _switchButtonArea.Width / 2, switchColor);
            DrawButtonLabel(spriteBatch, font, "X", _switchButtonArea, textColor);
        }
        
        private void DrawButtonLabel(SpriteBatch spriteBatch, SpriteFont font, string text, Rectangle area, Color color)
        {
            Vector2 textSize = font.MeasureString(text);
            float scale = UIScale;
            Vector2 pos = new Vector2(
                area.Center.X - (textSize.X * scale) / 2,
                area.Center.Y - (textSize.Y * scale) / 2
            );
            spriteBatch.DrawString(font, text, pos, color, 0f, Vector2.Zero, scale, SpriteEffects.None, 0f);
        }
        
        private void DrawFilledCircle(SpriteBatch spriteBatch, Vector2 center, float radius, Color color)
        {
            // Draw filled circle using horizontal lines
            for (int y = (int)-radius; y <= (int)radius; y++)
            {
                float halfWidth = (float)Math.Sqrt(radius * radius - y * y);
                Rectangle rect = new Rectangle(
                    (int)(center.X - halfWidth),
                    (int)(center.Y + y),
                    (int)(halfWidth * 2),
                    1
                );
                spriteBatch.Draw(_pixel, rect, color);
            }
        }
    }
}
