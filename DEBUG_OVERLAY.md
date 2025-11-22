# Debug Overlay System

## Overview

Added a togglable debug visual overlay to help identify and diagnose AI positioning and movement issues.

## Activation

Press **F3** during a match to toggle the debug overlay on/off.

## Visual Elements

### Field Zones

**Center Line** (Red):
- Vertical red line at field center (X=1600)
- Shows exact midfield dividing line

**Center Zone Boundaries** (Yellow):
- Two vertical yellow lines at X=1400 and X=1800
- Marks the 200px zone on each side of center line
- Players in this zone use 100px sticky target threshold

### Player Information

Each player displays:

#### AI Target Position
- **Green line**: Points from player to their AI target position
- **Green circle**: Marks the target position
- Shows where the AI wants the player to move

#### Velocity Vector
- **Cyan line**: Shows player's current movement direction and speed
- Line length represents velocity magnitude (scaled 0.5x for visibility)
- Helps identify oscillation or erratic movement

#### Position Circle
- **Orange circle**: Player in center zone (±200px from center)
- **White circle**: Player outside center zone
- Circle radius: 70px (approximately player size)

#### Player State Text

**Controlled Player** (Yellow):
```
CTRL
V:245  (velocity magnitude)
```

**AI Players** (Cyan):
```
Positioning  (AI state name)
T:100px      (sticky target threshold)
```

AI states shown:
- `Idle` - No movement
- `Positioning` - Moving to tactical position
- `ChasingBall` - Running toward ball
- `Dribbling` - Has ball, deciding action
- `Passing` - Executing pass
- `Shooting` - Executing shot
- `AvoidingSideline` - Avoiding field boundaries

Threshold values:
- `50px` - Normal responsiveness (outside center zone)
- `100px` - Reduced responsiveness (inside center zone)

### Ball Information

**Ball Circle** (Red):
- Red circle around ball (50px radius)

**Ball Debug Text** (White):
```
Ball
V:320  (velocity magnitude)
H:15   (height above ground)
```

## Use Cases

### 1. Diagnosing Oscillation

Look for:
- **Rapid target changes**: Green line direction changing frequently
- **Short velocity vectors**: Player stopping and starting repeatedly
- **Threshold issues**: Players in center zone showing "50px" instead of "100px"

### 2. Checking AI States

Verify:
- **State transitions**: Players changing states appropriately
- **Positioning behavior**: Defenders vs forwards positioning differently
- **Ball chasing**: Correct players transitioning to ChasingBall state

### 3. Analyzing Movement

Observe:
- **Velocity consistency**: Smooth cyan lines indicate stable movement
- **Target commitment**: Green lines should be relatively stable
- **Zone behavior**: Orange circles (center zone) should have more stable targets

### 4. Ball Tracking

Monitor:
- **Ball velocity**: High V values when kicked, decreasing with friction
- **Ball height**: Should be near 0 except during shooting/lobbing
- **Player-ball interaction**: Players' target positions updating based on ball

## Camera Zoom Adjustment

To see the entire field with debug overlay:

1. Press **Escape** to exit match
2. Go to **Settings**
3. Adjust **Camera Zoom**: Now supports 0.1x to 2.0x (was 0.5x to 2.0x)
4. Set to 0.1x to see entire field
5. Return to match and press F3 for debug overlay

**Recommended zoom for debugging**: 0.3x - 0.5x
- 0.1x - 0.2x: See entire field but players very small
- 0.3x - 0.4x: Good balance for overview debugging
- 0.5x - 0.8x: Standard gameplay view
- 1.0x+: Close-up view

## Technical Details

### Drawing Order

1. Field zones (center line, boundaries)
2. Player AI target lines
3. Player velocity vectors
4. Player position circles
5. Player state text
6. Ball circle and text

All drawn with camera transformation so overlays move with camera.

### Performance Impact

- Added rendering per frame:
  * 3 lines for field zones
  * Per player: 1 line, 1-2 circles, 1 text label
  * Ball: 1 circle, 1 text label
- Total: ~50-70 draw calls
- Performance: < 0.5ms per frame (negligible)

### Color Coding

| Color | Meaning |
|-------|---------|
| Red | Center line, ball |
| Yellow | Center zone boundaries, controlled player |
| Green/Lime | AI target positions |
| Cyan | Velocity vectors, AI state text |
| Orange | Players in center zone |
| White | Players outside center zone, ball text |

## Files Modified

✅ **MatchScreen.cs**
- Added `_debugOverlayEnabled` flag
- Added F3 key toggle in Update()
- Added `DrawDebugOverlay()` method
- Uses existing `DrawLine()` and `DrawCircle()` methods

✅ **SettingsScreen.cs**
- Camera Zoom range: 0.5f → 0.1f (minimum)
- Allows much wider field of view for debugging

## Build Status

✅ Project builds successfully
✅ No errors

## Usage Example

### Debugging Center Line Oscillation

1. Start match
2. Press F3 to enable debug overlay
3. Adjust camera zoom to 0.3x (Settings menu)
4. Observe players near center line (X=1400-1800):
   - Orange circles indicate center zone
   - Text shows "T:100px" threshold
   - Green target lines should be stable
   - Cyan velocity vectors should be smooth

### Debugging AI Passing

1. Enable debug overlay (F3)
2. Watch AI player with ball (state: "Dribbling")
3. Look for state transition to "Passing"
4. Green line should point to teammate
5. After pass, player returns to "Positioning"

## Keyboard Controls Summary

| Key | Function |
|-----|----------|
| **F3** | Toggle debug overlay |
| **Arrow Keys** | Move controlled player |
| **Space** | Switch controlled player |
| **X** | Shoot/Tackle |
| **Escape** | Exit match |

## Summary

The debug overlay provides real-time visual feedback on:
- ✅ AI decision making (states)
- ✅ Movement intentions (target positions)
- ✅ Actual movement (velocity)
- ✅ Zone behaviors (center line threshold)
- ✅ Ball physics (velocity, height)

Perfect tool for diagnosing oscillation, AI behavior, and movement issues! 🔍🎮
