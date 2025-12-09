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
        
        // Back button hold timer (0.3 second hold required)
        private float _backButtonHoldTime;
        private const float BackButtonHoldThreshold = 0.3f;
        private bool _backButtonHeld; // True when held long enough
        
        // Touch state
        private TouchCollection _currentTouchState;
        private TouchCollection _previousTouchState;
        
        // Public properties for input checking
        public bool IsActionPressed => _actionPressed;
        public bool IsActionJustPressed => _actionPressed && !_actionPreviousPressed;
        public bool IsBackPressed => _backButtonHeld; // Only true after held 0.3s
        public bool IsBackJustPressed => _backButtonHeld && !_backPreviousPressed;
        public bool IsSwitchPressed => _switchPressed;
        public bool IsSwitchJustPressed => _switchPressed && !_switchPreviousPressed;
        
        // Back button hold progress (0.0 to 1.0)
        public float BackButtonHoldProgress => _backPressed ? Math.Min(_backButtonHoldTime / BackButtonHoldThreshold, 1f) : 0f;
        
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
            int buttonSize = (int)(120 * UIScale);  // Increased from 100 to 120
            int padding = (int)(20 * UIScale);
            int smallButtonSize = (int)(85 * UIScale);  // Increased from 70 to 85
            int verticalOffset = (int)(40 * UIScale);  // Move buttons up by 40 pixels
            
            // Action button (A) - bottom right, largest
            _actionButtonArea = new Rectangle(
                ScreenWidth - buttonSize - padding,
                ScreenHeight - buttonSize - padding - verticalOffset,
                buttonSize,
                buttonSize
            );
            
            // Switch button (X) - above action button
            _switchButtonArea = new Rectangle(
                ScreenWidth - buttonSize - padding,
                ScreenHeight - buttonSize * 2 - padding * 2 - verticalOffset,
                buttonSize,
                buttonSize
            );
            
            // Back/Menu button (B) - TOP RIGHT corner (requires 0.3s hold)
            _backButtonArea = new Rectangle(
                ScreenWidth - smallButtonSize - padding,
                padding,
                smallButtonSize,
                smallButtonSize
            );
            
            // Default joystick center (bottom left)
            _joystickCenter = new Vector2(
                padding + buttonSize,
                ScreenHeight - padding - buttonSize - verticalOffset
            );
        }
        
        public void Update(GameTime gameTime = null)
        {
            if (!Enabled) return;
            
            float deltaTime = gameTime != null ? (float)gameTime.ElapsedGameTime.TotalSeconds : 0.016f;
            
            _previousTouchState = _currentTouchState;
            _actionPreviousPressed = _actionPressed;
            _backPreviousPressed = _backButtonHeld;
            _switchPreviousPressed = _switchPressed;
            
            _currentTouchState = TouchPanel.GetState();
            
            _actionPressed = false;
            bool backTouched = false;
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
                    backTouched = true;
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
            
            // Handle back button hold timer
            if (backTouched)
            {
                _backPressed = true;
                _backButtonHoldTime += deltaTime;
                if (_backButtonHoldTime >= BackButtonHoldThreshold)
                {
                    _backButtonHeld = true;
                }
            }
            else
            {
                _backPressed = false;
                _backButtonHoldTime = 0f;
                _backButtonHeld = false;
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
            Color textColor = new Color(40, 40, 40, 255); // Dark text for visibility
            
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
            
            // Draw back/menu button (B) - top right with hold progress indicator
            Color backColor = _backButtonHeld ? buttonPressedColor : (_backPressed ? new Color(200, 200, 100, 120) : buttonColor);
            Vector2 backCenter = new Vector2(_backButtonArea.Center.X, _backButtonArea.Center.Y);
            float backRadius = _backButtonArea.Width / 2;
            DrawFilledCircle(spriteBatch, backCenter, backRadius, backColor);
            
            // Draw hold progress ring
            if (_backPressed && !_backButtonHeld)
            {
                float progress = BackButtonHoldProgress;
                Color progressColor = new Color(255, 200, 0, 180);
                DrawProgressRing(spriteBatch, backCenter, backRadius + 5, backRadius + 12, progress, progressColor);
            }
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
            float scale = UIScale * 1.5f; // Larger text for visibility
            Vector2 textSize = font.MeasureString(text);
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
        
        private void DrawProgressRing(SpriteBatch spriteBatch, Vector2 center, float innerRadius, float outerRadius, float progress, Color color)
        {
            // Draw progress ring as arc from top (0 degrees) clockwise
            int segments = 36;
            int segmentsToDraw = (int)(segments * progress);
            
            for (int i = 0; i < segmentsToDraw; i++)
            {
                // Start from top (-90 degrees) and go clockwise
                float angle1 = (float)(-Math.PI / 2 + (i * 2 * Math.PI / segments));
                float angle2 = (float)(-Math.PI / 2 + ((i + 1) * 2 * Math.PI / segments));
                
                // Draw thick line segment
                for (float r = innerRadius; r <= outerRadius; r += 1)
                {
                    Vector2 p1 = center + new Vector2((float)Math.Cos(angle1) * r, (float)Math.Sin(angle1) * r);
                    Vector2 p2 = center + new Vector2((float)Math.Cos(angle2) * r, (float)Math.Sin(angle2) * r);
                    DrawLine(spriteBatch, p1, p2, color);
                }
            }
        }
        
        private void DrawLine(SpriteBatch spriteBatch, Vector2 start, Vector2 end, Color color)
        {
            Vector2 edge = end - start;
            float angle = (float)Math.Atan2(edge.Y, edge.X);
            float length = edge.Length();
            
            spriteBatch.Draw(_pixel,
                new Rectangle((int)start.X, (int)start.Y, (int)length, 2),
                null, color, angle, Vector2.Zero, SpriteEffects.None, 0);
        }
    }
}
