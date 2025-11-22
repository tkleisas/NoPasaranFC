# AI Aerial Passing System

## Overview
The AI now uses intelligent aerial/lofted passes to bypass defenders when appropriate. The system analyzes the pass corridor and automatically decides whether to use a ground pass or a lofted pass that arcs over opponents.

## How It Works

### Pass Type Detection
When an AI player attempts a pass, the system:
1. **Analyzes the pass corridor**: Checks for defenders between the passer and target
2. **Counts defenders in path**: Any opponent within 150 pixels of the pass line is counted
3. **Evaluates pass distance**: Long passes (>800 pixels / ~1/4 field) are more likely to be lofted
4. **Decides pass type**:
   - **Lofted Pass**: Used when 2+ defenders block the path OR pass distance > 800 pixels
   - **Ground Pass**: Used for clear passing lanes with 0-1 defenders

### Physics

#### Ground Pass (Normal)
- **Horizontal Velocity**: Based on passer's Passing stat and power
- **Vertical Velocity**: 30f (minimal bounce)
- **Sound Volume**: 0.4 (quieter)

#### Lofted Pass (Aerial)
- **Horizontal Velocity**: Reduced to 85% of normal (for realistic arc)
- **Vertical Velocity**: 200f + (distance / 10f) - scales with pass distance
- **Sound Volume**: 0.6 (louder)
- **Effect**: Ball arcs high over defenders' heads

### Ball Height Threshold
- Players can only interact with balls below **100 pixels** height
- Aerial passes go well above this threshold, making them uncatchable mid-flight
- Ball must land before players can intercept it

### Visual Feedback
The game already has visual cues for ball height:
- **Shadow**: Grows larger and fainter as ball goes higher
- **Ball Size**: Appears larger when higher (perspective effect)
- **Y Position**: Ball sprite moves up on screen when airborne

## Advantages

### Strategic Benefits
1. **Bypass Defensive Lines**: Long balls over packed defenses reach forwards
2. **Counter-Attack**: Quick lofted passes from defense to forwards
3. **Pressure Relief**: Defenders can clear danger with aerial passes
4. **Space Creation**: Forces defenders to track aerial balls, opening gaps

### Gameplay Benefits
1. **More Dynamic Play**: Breaks up static ball contests in midfield
2. **Realistic Football**: Mimics real soccer tactics (long balls, over-the-top passes)
3. **AI Variety**: Adds another dimension to AI decision-making
4. **Defensive Challenge**: Human players must track both ground and aerial balls

## Implementation Details

### Pass Corridor Analysis
```
For each opposing player:
  1. Calculate vector from ball to opponent
  2. Project opponent position onto pass direction line
  3. Check if projection is within pass distance
  4. Measure perpendicular distance from pass line
  5. If < 150 pixels, count as "in path"
```

### Decision Logic
```
needsLoftedPass = (defendersInPath >= 2) OR (passDistance > 800f)

if needsLoftedPass:
  BallVerticalVelocity = 200f + (passDistance / 10f)
  BallVelocity *= 0.85f  // Reduce horizontal for arc
else:
  BallVerticalVelocity = 30f
```

## AI Passing Strategy

### Position-Based Behavior

#### Defenders
- **98% pass rate** when teammate is ahead
- Prefer long balls to midfielders/forwards
- Use aerial passes to bypass pressing opponents

#### Midfielders  
- **ALWAYS pass** to forwards ahead of them (>500px distance)
- **95% pass rate** for any forward pass
- **60% pass rate** even for lateral/backward passes
- Very aggressive passing style

#### Forwards
- **90% pass rate** when teammate is much closer to goal (>200px difference)
- **40% pass rate** otherwise
- Still prefer shooting when in range (<400px from goal)

### Pressure Response
- **ALWAYS pass** when opponent is within 300px (unless in shooting range)
- Prioritizes safety over dribbling when pressured
- Uses aerial passes to escape tight situations

## Testing Tips

1. **Watch for arc**: Lofted passes should visibly arc higher
2. **Listen for sound**: Louder kick sound indicates aerial pass
3. **Check shadow**: Ball shadow should grow and fade during flight
4. **Observe defense**: Defenders shouldn't intercept mid-flight balls
5. **Long distance**: Passes over 800px should usually be lofted
6. **Crowded midfield**: Passes through 2+ defenders should arc over them

## Future Enhancements

Potential improvements:
- Add headers (players jump to intercept aerial balls)
- Goalkeeper rushing to claim high balls
- Different pass types (chip, driven, floated)
- Weather effects on aerial passes (wind)
- Stamina impact on pass height/distance
- Advanced AI: Through balls, diagonal passes
- Visual trail/arc indicator for aerial passes
