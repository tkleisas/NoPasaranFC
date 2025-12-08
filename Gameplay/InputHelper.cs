using System;
using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;
using Microsoft.Xna.Framework.Input.Touch;

namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Unified input helper that supports keyboard, gamepad, and touch
    /// </summary>
    public class InputHelper
    {
        private KeyboardState _currentKeyState;
        private KeyboardState _previousKeyState;
        private GamePadState _currentPadState;
        private GamePadState _previousPadState;
        private TouchCollection _currentTouchState;
        private TouchCollection _previousTouchState;
        
        private const float DeadZone = 0.2f;
        
        // Virtual joystick settings for touch
        private Vector2 _joystickCenter;
        private Vector2 _joystickPosition;
        private bool _joystickActive;
        private int _joystickTouchId = -1;
        
        // Touch button areas (will be set based on screen size)
        private Rectangle _shootButtonArea;
        private Rectangle _switchButtonArea;
        private Rectangle _backButtonArea;
        private bool _shootButtonPressed;
        private bool _switchButtonPressed;
        private bool _previousSwitchButtonPressed;
        private bool _backButtonPressed;
        private bool _previousBackButtonPressed;
        
        // Screen dimensions for touch calculations
        public int ScreenWidth { get; set; } = 1280;
        public int ScreenHeight { get; set; } = 720;
        
        // Touch control visibility
        public bool TouchControlsEnabled { get; set; }
        
        // Virtual joystick properties for rendering
        public Vector2 JoystickCenter => _joystickCenter;
        public Vector2 JoystickPosition => _joystickPosition;
        public bool JoystickActive => _joystickActive;
        public Rectangle ShootButtonArea => _shootButtonArea;
        public Rectangle SwitchButtonArea => _switchButtonArea;
        public bool ShootButtonPressed => _shootButtonPressed;
        public bool SwitchButtonVisualPressed => _switchButtonPressed;
        
        public InputHelper()
        {
            _currentKeyState = Keyboard.GetState();
            _previousKeyState = _currentKeyState;
            _currentPadState = GamePad.GetState(PlayerIndex.One);
            _previousPadState = _currentPadState;
            
#if ANDROID
            TouchControlsEnabled = true;
            TouchPanel.EnabledGestures = GestureType.None;
#else
            TouchControlsEnabled = false;
#endif
            
            UpdateTouchAreas();
        }
        
        /// <summary>
        /// Update touch button areas based on screen size
        /// </summary>
        public void UpdateTouchAreas()
        {
            int buttonSize = Math.Min(ScreenWidth, ScreenHeight) / 6;
            int padding = 20;
            
            // Shoot button - bottom right
            _shootButtonArea = new Rectangle(
                ScreenWidth - buttonSize - padding,
                ScreenHeight - buttonSize - padding,
                buttonSize,
                buttonSize
            );
            
            // Switch button - above shoot button
            _switchButtonArea = new Rectangle(
                ScreenWidth - buttonSize - padding,
                ScreenHeight - buttonSize * 2 - padding * 2,
                buttonSize,
                buttonSize
            );
            
            // Back button - top right corner (smaller)
            int smallButton = buttonSize / 2;
            _backButtonArea = new Rectangle(
                ScreenWidth - smallButton - padding,
                padding,
                smallButton,
                smallButton
            );
            
            // Default joystick center (bottom left area)
            _joystickCenter = new Vector2(padding + buttonSize, ScreenHeight - padding - buttonSize);
        }
        
        public void Update()
        {
            _previousKeyState = _currentKeyState;
            _previousPadState = _currentPadState;
            _previousTouchState = _currentTouchState;
            _previousSwitchButtonPressed = _switchButtonPressed;
            _previousBackButtonPressed = _backButtonPressed;
            
            _currentKeyState = Keyboard.GetState();
            _currentPadState = GamePad.GetState(PlayerIndex.One);
            
            // Touch input
            if (TouchControlsEnabled)
            {
                _currentTouchState = TouchPanel.GetState();
                ProcessTouchInput();
            }
        }
        
        private void ProcessTouchInput()
        {
            _shootButtonPressed = false;
            _switchButtonPressed = false;
            _backButtonPressed = false;
            
            // Check if joystick touch is still active
            bool joystickTouchFound = false;
            
            foreach (TouchLocation touch in _currentTouchState)
            {
                Point touchPoint = new Point((int)touch.Position.X, (int)touch.Position.Y);
                
                // Check action buttons
                if (_shootButtonArea.Contains(touchPoint))
                {
                    _shootButtonPressed = true;
                }
                else if (_switchButtonArea.Contains(touchPoint))
                {
                    _switchButtonPressed = true;
                }
                else if (_backButtonArea.Contains(touchPoint))
                {
                    _backButtonPressed = true;
                }
                // Virtual joystick - left side of screen
                else if (touchPoint.X < ScreenWidth / 2)
                {
                    if (touch.State == TouchLocationState.Pressed && !_joystickActive)
                    {
                        // Start new joystick touch
                        _joystickActive = true;
                        _joystickTouchId = touch.Id;
                        _joystickCenter = touch.Position;
                        _joystickPosition = touch.Position;
                        joystickTouchFound = true;
                    }
                    else if (touch.Id == _joystickTouchId && _joystickActive)
                    {
                        // Update existing joystick touch
                        _joystickPosition = touch.Position;
                        joystickTouchFound = true;
                    }
                }
                
                // Track joystick touch even if it moves to right side
                if (touch.Id == _joystickTouchId && _joystickActive)
                {
                    _joystickPosition = touch.Position;
                    joystickTouchFound = true;
                }
            }
            
            // Release joystick if touch ended
            if (!joystickTouchFound)
            {
                _joystickActive = false;
                _joystickTouchId = -1;
            }
        }
        
        /// <summary>
        /// Get movement direction from keyboard (arrows/WASD), gamepad (left stick/D-pad), or touch
        /// </summary>
        public Vector2 GetMovementDirection()
        {
            Vector2 direction = Vector2.Zero;
            
            // Keyboard input
            if (_currentKeyState.IsKeyDown(Keys.Up) || _currentKeyState.IsKeyDown(Keys.W))
                direction.Y -= 1;
            if (_currentKeyState.IsKeyDown(Keys.Down) || _currentKeyState.IsKeyDown(Keys.S))
                direction.Y += 1;
            if (_currentKeyState.IsKeyDown(Keys.Left) || _currentKeyState.IsKeyDown(Keys.A))
                direction.X -= 1;
            if (_currentKeyState.IsKeyDown(Keys.Right) || _currentKeyState.IsKeyDown(Keys.D))
                direction.X += 1;
            
            // GamePad input (left stick)
            if (_currentPadState.IsConnected)
            {
                Vector2 leftStick = _currentPadState.ThumbSticks.Left;
                leftStick.Y = -leftStick.Y; // Invert Y axis (thumbstick Y is opposite)
                
                if (leftStick.Length() > DeadZone)
                {
                    direction = leftStick;
                }
                
                // D-Pad override
                if (_currentPadState.DPad.Up == ButtonState.Pressed)
                    direction.Y -= 1;
                if (_currentPadState.DPad.Down == ButtonState.Pressed)
                    direction.Y += 1;
                if (_currentPadState.DPad.Left == ButtonState.Pressed)
                    direction.X -= 1;
                if (_currentPadState.DPad.Right == ButtonState.Pressed)
                    direction.X += 1;
            }
            
            // Touch virtual joystick
            if (TouchControlsEnabled && _joystickActive)
            {
                Vector2 touchDirection = _joystickPosition - _joystickCenter;
                float maxDistance = Math.Min(ScreenWidth, ScreenHeight) / 8f;
                
                if (touchDirection.Length() > DeadZone * maxDistance)
                {
                    // Clamp to max distance and normalize
                    if (touchDirection.Length() > maxDistance)
                    {
                        touchDirection = Vector2.Normalize(touchDirection) * maxDistance;
                    }
                    direction = touchDirection / maxDistance;
                }
            }
            
            if (direction != Vector2.Zero && direction.Length() > 1f)
                direction.Normalize();
            
            return direction;
        }
        
        /// <summary>
        /// Check if shoot/action button is held down
        /// Keyboard: X | GamePad: A button | Touch: Shoot button
        /// </summary>
        public bool IsShootButtonDown()
        {
            bool keyDown = _currentKeyState.IsKeyDown(Keys.X);
            bool padDown = _currentPadState.IsConnected && _currentPadState.Buttons.A == ButtonState.Pressed;
            bool touchDown = TouchControlsEnabled && _shootButtonPressed;
            
            return keyDown || padDown || touchDown;
        }
        
        /// <summary>
        /// Check if switch player button was just pressed
        /// Keyboard: Space | GamePad: X button | Touch: Switch button
        /// </summary>
        public bool IsSwitchPlayerPressed()
        {
            bool keyPressed = _currentKeyState.IsKeyDown(Keys.Space) && !_previousKeyState.IsKeyDown(Keys.Space);
            bool padPressed = _currentPadState.IsConnected && 
                             _currentPadState.Buttons.X == ButtonState.Pressed && 
                             _previousPadState.Buttons.X == ButtonState.Released;
            bool touchPressed = TouchControlsEnabled && _switchButtonPressed && !_previousSwitchButtonPressed;
            
            return keyPressed || padPressed || touchPressed;
        }
        
        /// <summary>
        /// Check if back/cancel button was pressed
        /// Keyboard: Escape | GamePad: B button or Back button | Touch: Back button
        /// </summary>
        public bool IsBackPressed()
        {
            bool keyPressed = _currentKeyState.IsKeyDown(Keys.Escape) && !_previousKeyState.IsKeyDown(Keys.Escape);
            bool padPressed = _currentPadState.IsConnected && 
                             ((_currentPadState.Buttons.B == ButtonState.Pressed && _previousPadState.Buttons.B == ButtonState.Released) ||
                              (_currentPadState.Buttons.Back == ButtonState.Pressed && _previousPadState.Buttons.Back == ButtonState.Released));
            bool touchPressed = TouchControlsEnabled && _backButtonPressed && !_previousBackButtonPressed;
            
            return keyPressed || padPressed || touchPressed;
        }
        
        /// <summary>
        /// Check if menu up was pressed (for menus)
        /// Keyboard: Up | GamePad: D-Pad Up or Left Stick Up
        /// </summary>
        public bool IsMenuUpPressed()
        {
            bool keyPressed = _currentKeyState.IsKeyDown(Keys.Up) && !_previousKeyState.IsKeyDown(Keys.Up);
            bool padPressed = _currentPadState.IsConnected &&
                             ((_currentPadState.DPad.Up == ButtonState.Pressed && _previousPadState.DPad.Up == ButtonState.Released) ||
                              (_currentPadState.ThumbSticks.Left.Y > DeadZone && _previousPadState.ThumbSticks.Left.Y <= DeadZone));
            
            return keyPressed || padPressed;
        }
        
        /// <summary>
        /// Check if menu down was pressed (for menus)
        /// Keyboard: Down | GamePad: D-Pad Down or Left Stick Down
        /// </summary>
        public bool IsMenuDownPressed()
        {
            bool keyPressed = _currentKeyState.IsKeyDown(Keys.Down) && !_previousKeyState.IsKeyDown(Keys.Down);
            bool padPressed = _currentPadState.IsConnected &&
                             ((_currentPadState.DPad.Down == ButtonState.Pressed && _previousPadState.DPad.Down == ButtonState.Released) ||
                              (_currentPadState.ThumbSticks.Left.Y < -DeadZone && _previousPadState.ThumbSticks.Left.Y >= -DeadZone));
            
            return keyPressed || padPressed;
        }
        
        /// <summary>
        /// Check if confirm/select was pressed (for menus)
        /// Keyboard: Enter | GamePad: A button or Start button
        /// </summary>
        public bool IsConfirmPressed()
        {
            bool keyPressed = _currentKeyState.IsKeyDown(Keys.Enter) && !_previousKeyState.IsKeyDown(Keys.Enter);
            bool padPressed = _currentPadState.IsConnected &&
                             ((_currentPadState.Buttons.A == ButtonState.Pressed && _previousPadState.Buttons.A == ButtonState.Released) ||
                              (_currentPadState.Buttons.Start == ButtonState.Pressed && _previousPadState.Buttons.Start == ButtonState.Released));
            
            return keyPressed || padPressed;
        }
        
        /// <summary>
        /// Check if gamepad is connected
        /// </summary>
        public bool IsGamePadConnected()
        {
            return _currentPadState.IsConnected;
        }
        
        /// <summary>
        /// Get touch tap position if a tap occurred this frame, otherwise null
        /// </summary>
        public Vector2? GetTouchTapPosition()
        {
            if (!TouchControlsEnabled) return null;
            
            foreach (TouchLocation touch in _currentTouchState)
            {
                if (touch.State == TouchLocationState.Pressed)
                {
                    // Check if this is a new touch (not tracked in previous state)
                    TouchLocation prevTouch;
                    if (!touch.TryGetPreviousLocation(out prevTouch) || prevTouch.State == TouchLocationState.Invalid)
                    {
                        return touch.Position;
                    }
                }
            }
            
            return null;
        }
        
        /// <summary>
        /// Check if a tap occurred in the given rectangle
        /// </summary>
        public bool IsTapInRect(Rectangle rect)
        {
            var tap = GetTouchTapPosition();
            if (tap.HasValue)
            {
                return rect.Contains(new Point((int)tap.Value.X, (int)tap.Value.Y));
            }
            return false;
        }
    }
}
