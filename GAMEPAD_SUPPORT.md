# Game Controller Support

## Overview
The game now supports Xbox-compatible game controllers alongside keyboard controls. All major gameplay and menu functions can be controlled with a gamepad.

## Supported Controllers
- Xbox 360 Controller
- Xbox One Controller
- Xbox Series X|S Controller
- Any XInput-compatible controller

## Controls

### Menu Navigation
| Action | Keyboard | GamePad |
|--------|----------|---------|
| Navigate Up | ↑ Arrow | D-Pad Up / Left Stick Up |
| Navigate Down | ↓ Arrow | D-Pad Down / Left Stick Down |
| Confirm | Enter | A Button / Start |
| Back/Cancel | Escape | B Button / Back |

### Lineup Selection
| Action | Keyboard | GamePad |
|--------|----------|---------|
| Navigate Up | ↑ Arrow | D-Pad Up / Left Stick Up |
| Navigate Down | ↓ Arrow | D-Pad Down / Left Stick Down |
| Toggle Player | Space | X Button |
| Confirm Lineup | Enter | A Button |
| Cancel | Escape | B Button |

### Match Gameplay
| Action | Keyboard | GamePad |
|--------|----------|---------|
| Move Player | Arrow Keys / WASD | Left Stick / D-Pad |
| Shoot/Pass (tap) | X | A Button (tap) |
| Charge Shot (hold) | Hold X | Hold A Button |
| Tackle | X (near opponent) | A Button (near opponent) |
| Switch Player | Space | X Button |
| Pause/Exit | Escape | B Button |

## Features

### Analog Movement
- **Left Stick**: Provides smooth analog movement in all directions
- **Dead Zone**: 0.2 threshold to prevent drift
- **Normalization**: Diagonal movement speed matches horizontal/vertical

### Button Mapping Philosophy
- **A Button**: Primary action (confirm, shoot/pass)
- **B Button**: Back/cancel
- **X Button**: Secondary action (switch player)
- **Start Button**: Alternative confirm (menus)
- **Back Button**: Alternative cancel (menus)

### Hybrid Control
- **Keyboard + GamePad**: Both can be used simultaneously
- **Automatic Detection**: GamePad input automatically takes precedence when connected
- **Hot-Swap**: Connect/disconnect controllers without restarting

## Technical Details

### InputHelper Class
Location: `Gameplay/InputHelper.cs`

Provides unified input handling:
- `GetMovementDirection()` - Returns Vector2 from keyboard or gamepad
- `IsShootButtonDown()` - Checks X key or A button
- `IsSwitchPlayerPressed()` - Checks Space or X button
- `IsBackPressed()` - Checks Escape or B button
- `IsMenuUpPressed()` - For menu navigation
- `IsMenuDownPressed()` - For menu navigation
- `IsConfirmPressed()` - For menu selection
- `IsGamePadConnected()` - Check if controller is connected

### Dead Zone Handling
- Default: 0.2 (20% of maximum stick deflection)
- Prevents unintended movement from stick drift
- Applied to Left Stick input only

### D-Pad Override
- D-Pad input overrides Left Stick when both are active
- Provides precise 8-directional movement option
- Useful for players who prefer digital input

## Configuration

### Changing Button Mapping
Edit `Gameplay/InputHelper.cs` to customize button assignments:

```csharp
// Example: Change shoot button from A to X
public bool IsShootButtonDown()
{
    bool padDown = _currentPadState.Buttons.X == ButtonState.Pressed;
    return keyDown || padDown;
}
```

### Adjusting Dead Zone
Modify the `DeadZone` constant in `InputHelper.cs`:

```csharp
private const float DeadZone = 0.2f; // Increase for less sensitive sticks
```

### Supporting Multiple Controllers
Currently supports `PlayerIndex.One`. To add more:

```csharp
_currentPadState = GamePad.GetState(PlayerIndex.Two); // Player 2
```

## Troubleshooting

### Controller Not Detected
1. **Check connection**: Ensure controller is properly connected via USB or wireless
2. **Driver installation**: Update XInput drivers (Windows Update)
3. **Test in other games**: Verify controller works in other XInput games
4. **Check battery**: Wireless controllers need charged batteries

### Input Lag
1. **Wired connection**: Use USB cable for lowest latency
2. **Update drivers**: Ensure Xbox Accessories app is up to date (Windows)
3. **Close background apps**: Reduce CPU load

### Stick Drift
1. **Increase dead zone**: Edit `DeadZone` value in InputHelper.cs
2. **Controller calibration**: Use Windows Game Controllers settings
3. **Physical cleaning**: Clean around stick mechanism

### Buttons Not Responding
1. **Check mappings**: Verify button assignments in InputHelper.cs
2. **Test controller**: Use Windows "Set up USB game controllers" app
3. **XInput vs DirectInput**: Ensure controller is in XInput mode

## Future Enhancements

Potential improvements:
- [ ] Configurable button remapping in-game
- [ ] Adjustable dead zone in settings menu
- [ ] Multiple controller support (local multiplayer)
- [ ] Rumble/vibration feedback on shots and tackles
- [ ] Right stick camera control
- [ ] Trigger-based shot power
- [ ] Controller disconnection notification
- [ ] Button prompt display based on connected input method

## Compatibility

### Tested Controllers
- ✅ Xbox One Controller (wired)
- ✅ Xbox Series X Controller (wired)
- ⚠️ PlayStation controllers require third-party software (DS4Windows, etc.)
- ⚠️ Nintendo Switch Pro Controller requires third-party software

### Platform Support
- ✅ Windows: Full XInput support
- ✅ Linux: XInput via SDL2 (MonoGame)
- ✅ macOS: XInput via SDL2 (MonoGame)

### MonoGame GamePad Support
The game uses MonoGame's `GamePad` API which provides:
- Cross-platform controller support
- Automatic XInput detection
- Button and stick state polling
- Connection status monitoring

## Best Practices

### For Players
1. **Connect before starting**: Plug in controller before launching game
2. **Use D-Pad for menus**: More precise than analog stick
3. **Use Left Stick in-game**: Smoother movement during matches
4. **Wired for competitive**: USB connection has lowest latency

### For Developers
1. **Test with real hardware**: Emulators don't match real controller feel
2. **Implement dead zones**: Essential for stick drift compensation
3. **Normalize diagonals**: Ensure equal speed in all directions
4. **Support hybrid input**: Don't disable keyboard when gamepad connected
5. **Handle disconnections**: Game should continue if controller unplugged

## Credits

Gamepad support implemented using:
- **MonoGame GamePad API**: Cross-platform controller input
- **XInput Standard**: Microsoft's game controller protocol
- **SDL2**: Underlying input library (via MonoGame)
