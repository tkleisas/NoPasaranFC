using Microsoft.Xna.Framework;
using Microsoft.Xna.Framework.Input;

namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Unified input helper that supports both keyboard and gamepad
    /// </summary>
    public class InputHelper
    {
        private KeyboardState _currentKeyState;
        private KeyboardState _previousKeyState;
        private GamePadState _currentPadState;
        private GamePadState _previousPadState;
        
        private const float DeadZone = 0.2f;
        
        public InputHelper()
        {
            _currentKeyState = Keyboard.GetState();
            _previousKeyState = _currentKeyState;
            _currentPadState = GamePad.GetState(PlayerIndex.One);
            _previousPadState = _currentPadState;
        }
        
        public void Update()
        {
            _previousKeyState = _currentKeyState;
            _previousPadState = _currentPadState;
            
            _currentKeyState = Keyboard.GetState();
            _currentPadState = GamePad.GetState(PlayerIndex.One);
        }
        
        /// <summary>
        /// Get movement direction from keyboard (arrows/WASD) or gamepad (left stick/D-pad)
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
            
            if (direction != Vector2.Zero)
                direction.Normalize();
            
            return direction;
        }
        
        /// <summary>
        /// Check if shoot/action button is held down
        /// Keyboard: X | GamePad: A button
        /// </summary>
        public bool IsShootButtonDown()
        {
            bool keyDown = _currentKeyState.IsKeyDown(Keys.X);
            bool padDown = _currentPadState.IsConnected && _currentPadState.Buttons.A == ButtonState.Pressed;
            
            return keyDown || padDown;
        }
        
        /// <summary>
        /// Check if switch player button was just pressed
        /// Keyboard: Space | GamePad: X button
        /// </summary>
        public bool IsSwitchPlayerPressed()
        {
            bool keyPressed = _currentKeyState.IsKeyDown(Keys.Space) && !_previousKeyState.IsKeyDown(Keys.Space);
            bool padPressed = _currentPadState.IsConnected && 
                             _currentPadState.Buttons.X == ButtonState.Pressed && 
                             _previousPadState.Buttons.X == ButtonState.Released;
            
            return keyPressed || padPressed;
        }
        
        /// <summary>
        /// Check if back/cancel button was pressed
        /// Keyboard: Escape | GamePad: B button or Back button
        /// </summary>
        public bool IsBackPressed()
        {
            bool keyPressed = _currentKeyState.IsKeyDown(Keys.Escape) && !_previousKeyState.IsKeyDown(Keys.Escape);
            bool padPressed = _currentPadState.IsConnected && 
                             ((_currentPadState.Buttons.B == ButtonState.Pressed && _previousPadState.Buttons.B == ButtonState.Released) ||
                              (_currentPadState.Buttons.Back == ButtonState.Pressed && _previousPadState.Buttons.Back == ButtonState.Released));
            
            return keyPressed || padPressed;
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
    }
}
