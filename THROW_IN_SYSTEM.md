# Throw-In System Improvements

## Overview
Enhanced throw-in mechanism with power charging, intelligent AI targeting, and visual feedback.

## Features

### 1. Power Charging System
**Human Players:**
- Hold X button to charge throw power (0-100%)
- Power charges over 1 second
- Release button to execute throw
- Visual power bar shows current charge level
- Arrow length grows with power (100px → 250px)

**Power Ranges:**
- Minimum power (10%+): 12f velocity, 40f height - Short, flat throw
- Medium power (50%): 20f velocity, 100f height - Standard throw
- Maximum power (100%): 28f velocity, 160f height - Long, high-arc throw

**AI Players:**
- Automatically use 70-90% power for realistic variety
- Executes throw after 2-second positioning delay

### 2. Intelligent AI Throw-In Logic
The AI now evaluates multiple factors to find the best throw-in target:

**Scoring System:**
- **Forward Progress** (+): Throws toward attacking goal (0.5x multiplier)
- **Backward Throws** (-): Penalty for throwing back (0.1x multiplier)
- **Open Space** (+): Prefer teammates with fewer opponents nearby (150px radius)
  - Each nearby opponent: -100 points
- **Position Preference** (+50): Prefers midfielders and forwards over defenders
- **Distance** (+): Slight preference for closer teammates (easier throws)
- **Sideline Avoidance** (-150): Penalty if target too close to sideline (<100px)

**Fallback Behavior:**
- If no good target found, throws 300px forward toward center of field
- Maximum realistic throw range: 600px

### 3. Visual Improvements

**Throw-In Animations:**
- **throw_in_static**: Player holds ball overhead (static pose)
- **throw_in_throw**: 2-frame animation of releasing ball
- Player sprite rotates 360° to face throw direction
- Smooth left/right rotation control
- Animation automatically triggered on ball release

**No Visual Indicators for Throw-Ins:**
- No power bar displayed
- No direction arrow shown
- No countdown timer
- Player rotation is the only visual feedback
- Minimalist approach for realistic feel

**Other Set Pieces (Corners/Goal Kicks):**
- Localized labels displayed:
  - English: "CORNER KICK", "GOAL KICK"
  - Greek: "ΚΟΡΝΕΡ", "ΑΠΟΒΟΛΗ ΤΕΡΜΑΤΟΦΥΛΑΚΑ"
- Cyan color for visibility
- Timer countdown and direction arrow shown
- Standard set piece indicators

### 4. Player Positioning
**Improved Placement:**
- Thrower placed 50px outside field boundary (was 30px)
- Properly faces inward toward field center
- More realistic throw-in stance

**Throw Mechanics:**
- Player must be stationary during throw
- Uses two animations from PlayerAnimationSystem:
  * **throw_in_static** (non-looping): Frame 36 (row 9, column 0) - Holding ball overhead
  * **throw_in_throw** (non-looping): Frames 36-37 (0.15s per frame) - Releasing ball
- Player rotates to face throw direction (8-directional)
- **Animation-driven throw**: Ball is released when throw_in_throw animation completes
  * Button release triggers animation start
  * Ball velocity applied at end of animation (frame 2)
  * Total animation time: ~0.3 seconds
- Ball trajectory scales with power charge
- Sound volume increases with power (0.4-0.7)

## Technical Implementation

### New Properties
```csharp
public float ThrowInPowerCharge { get; private set; } // 0.0 to 1.0
```

### Key Methods
- `FindBestThrowInTarget(Player thrower)` - AI target evaluation
- `GetOpponentsWithinRadius(Vector2, float, Team)` - Opponent proximity check
- `DrawPowerBar(SpriteBatch, Vector2, float)` - Visual power indicator
- `DrawRectangleBorder(...)` - Helper for power bar border

### Physics
```csharp
// Power calculation
float minPower = 12f;
float maxPower = 28f;
float power = minPower + (maxPower - minPower) * chargeAmount;

// Height scales with power
float height = 40f + 120f * chargeAmount;
```

## Controls

**Human Player (during throw-in):**
- **Left/Right Arrow Keys**: Rotate throw direction continuously
- **X (Hold)**: Charge throw power (0-100%)
- **X (Release)**: Execute throw
- **No timer countdown**: Take as long as needed to aim
- **No visual indicators**: Direction shown by player rotation only

**AI Player:**
- Automatic targeting and execution after 2 seconds
- Evaluates best teammate using scoring algorithm
- Uses 70-90% power for realistic variety

## Comparison to Previous System

### Before:
- Fixed 15f power (very short throws)
- AI always threw to nearest teammate
- No visual indication of throw power
- Basic 30px player offset
- No strategic targeting

### After:
- Variable 12-28f power (adjustable range)
- AI evaluates multiple factors (position, space, direction)
- Visual power bar and growing arrow
- Improved 50px offset with proper facing
- Strategic forward-thinking AI throws
- Better throw physics (power affects height and distance)

## Future Enhancements
- Add foul throws (stepping over line, incorrect technique)
- Quick throw-in option (tap for quick short throw)
- Different throw animations for short vs long throws
- Teammate movement AI to get open for throw-ins
- Throw-in training mini-game
