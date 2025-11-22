# Dynamic Goal Net System

## Overview
The goal nets in No Pasaran FC feature a realistic physics-based system that responds to wind and ball collisions, providing a more immersive match experience.

## Features

### 1. **Wind Animation**
- Subtle wave motion simulates wind effect
- Sinusoidal movement pattern for natural appearance
- Different frequencies for horizontal and vertical motion
- Only affects interior net points (edges remain fixed to goalposts)

### 2. **Ball Collision Deformation**
- Net deforms when the ball enters the goal
- Deformation strength based on:
  - Ball velocity (faster shots = more deformation)
  - Distance from ball to net points
  - Influence radius (30px around the ball)
- Creates realistic "net bulge" effect when scoring

### 3. **Spring Physics**
- Net points return to rest position using spring forces
- Damping prevents excessive oscillation
- Creates natural "settling" motion after disturbance

### 4. **Player Depth Rendering**
- Players behind the net (inside goal area) render under the net
- Players in front of the net render normally
- Creates proper depth perception

## Technical Details

### Grid System
- **Grid Size**: 8 columns × 12 rows
- **Dynamic Points**: Interior points respond to physics
- **Fixed Points**: Edges remain attached to goalpost frame

### Physics Constants
```csharp
Damping = 0.85f         // Energy loss per frame (85% retained)
Stiffness = 0.3f        // Spring force strength
WindStrength = 0.15f    // Wind displacement magnitude
WindFrequency = 1.5f    // Wind wave speed
```

### Rendering
- **Net Lines**: Semi-transparent gray (180, 180, 180, 200)
- **Back Panel**: Dark semi-transparent (100, 100, 100, 100)
- **Pattern**: Vertical, horizontal, and diagonal lines create mesh appearance
- **Goal Posts**: White circles and crossbar render on top of net

## Implementation

### GoalNet Class (Gameplay/GoalNet.cs)
Main class managing net physics and rendering:
- `Update()`: Physics simulation (wind, ball collision, spring forces)
- `Draw()`: Render net grid with lines
- `IsPlayerBehindNet()`: Depth check for player rendering

### Integration with MatchScreen
1. Two GoalNet instances (left and right goals)
2. Updated every frame with ball position and velocity
3. Rendered in specific order:
   - Background elements
   - Ball (if inside goal)
   - Goal nets
   - Players (not behind nets)
   - Goal posts
   - Players (behind nets)
   - Ball (if outside goal)

## Visual Effects
- **Scoring**: Net deforms dramatically as ball enters, then settles back
- **Wind**: Constant subtle movement adds life to static scene
- **Depth**: Players entering goals appear correctly behind the net
- **Realism**: Mesh pattern and deformation mimic real soccer nets

## Performance
- Efficient grid-based simulation (96 points per net)
- Minimal performance impact (< 1ms per frame for both nets)
- No complex collision detection required

## Future Enhancements
- Net sound effects (ball hitting net)
- Tearing effect for very powerful shots
- Weather-based wind variation
- Net color customization per team
