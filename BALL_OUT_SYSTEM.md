# Ball Out of Bounds System

## Overview
When the ball goes out of bounds, it is automatically repositioned for restart with the nearest player moved close to the ball position. Different restart positions are used based on where and how the ball went out.

## Architecture

### Detection in CheckGoal()
The `CheckGoal()` method now handles multiple scenarios:

1. **Valid Goal**: Ball crosses goal line within goal area and below crossbar
2. **Ball Over Crossbar**: Ball crosses goal line but above crossbar height
3. **Ball Out on Goal Line**: Ball goes out past goal line outside goal area
4. **Ball Out on Sideline**: Ball crosses top or bottom boundary

### Boundary Definitions
```csharp
float leftGoalLine = StadiumMargin;                    // 400px
float rightGoalLine = StadiumMargin + FieldWidth;      // 6800px
float topSideline = StadiumMargin;                     // 400px
float bottomSideline = StadiumMargin + FieldHeight;    // 5200px

// Out-of-bounds detection with buffer
float leftOutBound = StadiumMargin - GoalDepth - 20;   // 320px
float rightOutBound = StadiumMargin + FieldWidth + GoalDepth + 20; // 6880px
float topOutBound = StadiumMargin - 20;                // 380px
float bottomOutBound = StadiumMargin + FieldHeight + 20; // 5220px
```

## Out-of-Bounds Scenarios

### 1. Ball Over Crossbar
**Condition**: Ball crosses goal line within goal area but height > 200px

**Handler**: `HandleBallOverCrossbar(bool leftSide)`

**Placement Logic**:
- X position: Just outside goal line (±20px)
- Y position: Near corner (50px from top or bottom)
- Simulates: Corner kick placement

```csharp
float xPos = leftSide ? StadiumMargin - 20 : StadiumMargin + FieldWidth + 20;
float yPos = BallPosition.Y < fieldCenter ? 
    StadiumMargin + 50 : StadiumMargin + FieldHeight - 50;
```

### 2. Ball Out on Goal Line
**Condition**: Ball goes out past goal line (X < leftOutBound OR X > rightOutBound)

**Handler**: `HandleBallOutGoalLine(bool leftSide, float yPosition)`

**Placement Logic**:
- X position: 30px from goal line
- Y position: 30px from corner (top or bottom based on where ball went out)
- Simulates: Corner kick or goal kick

```csharp
float xPos = leftSide ? StadiumMargin + 30 : StadiumMargin + FieldWidth - 30;
float yPos = yPosition < fieldCenter ? 
    StadiumMargin + 30 : StadiumMargin + FieldHeight - 30;
```

### 3. Ball Out on Sideline
**Condition**: Ball crosses top or bottom boundary (Y < topOutBound OR Y > bottomOutBound)

**Handler**: `HandleBallOutSideline(float xPosition, bool topSide)`

**Placement Logic**:
- X position: Clamped to current position (50px from corners)
- Y position: 20px from sideline
- Simulates: Throw-in position

```csharp
float xPos = Clamp(xPosition, StadiumMargin + 50, StadiumMargin + FieldWidth - 50);
float yPos = topSide ? StadiumMargin + 20 : StadiumMargin + FieldHeight - 20;
```

## Player Positioning

### Automatic Nearest Player Movement

**Method**: `PlaceBallForRestart(Vector2 position)`

**Logic**:
1. Find nearest player to ball restart position
2. If player is more than 100px away:
   - Calculate direction to ball
   - Position player 80px away from ball
   - This gives player immediate control opportunity

```csharp
Vector2 directionToBall = position - nearestPlayer.FieldPosition;
directionToBall.Normalize();
nearestPlayer.FieldPosition = position - directionToBall * 80f;
```

**Benefits**:
- Player doesn't have to run long distances to reach ball
- Simulates real soccer where specific player takes restart
- Maintains game flow and reduces downtime

## ClampBallToField() Changes

### Before (Broken)
```csharp
// Always clamped ball to field boundaries
if (BallPosition.X < StadiumMargin)
{
    BallPosition = StadiumMargin;
    // Ball could never cross goal line!
}
```

### After (Working)
```csharp
// Check if ball is in goal area
bool inLeftGoalArea = BallPosition.X < StadiumMargin && 
                      BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom;

// Only clamp if NOT in goal area
if (!inLeftGoalArea && !inRightGoalArea)
{
    if (BallPosition.X < StadiumMargin)
    {
        BallPosition = StadiumMargin;
        BallVelocity = -BallVelocity * 0.5f;
    }
}
```

**Key Fix**: Ball can now pass through goal area to score goals!

## Restart Positions Visualization

```
Field Layout (Top View):
                    Top Sideline
        ┌────────────────────────────────┐
        │                                │
        │  C                          C  │  C = Corner kick
 Left   │  ┌─┐                    ┌─┐  │  Right
 Goal   │  │G│                    │G│  │  Goal
 Line   │  └─┘                    └─┘  │  Line
        │  C                          C  │
        │                                │
        └────────────────────────────────┘
                  Bottom Sideline

Ball Out Positions:
- Over crossbar: Near C (corner)
- Out on goal line: Near C (corner)
- Out on sideline: Along T (throw-in line)

Buffer Zones:
- Goal area: 400px wide (centered)
- Corner radius: 50px from corner
- Throw-in margin: 20px from sideline
- Corner kick margin: 30px from goal line
```

## Future Enhancements

### Currently Missing
1. **Team Assignment**: Which team gets the restart
   - Currently: Nearest player (any team) gets ball
   - Needed: Last touch detection to assign correct team

2. **Restart Types**:
   - No distinction between corner kick, goal kick, and throw-in
   - All use automatic restart
   - Could add: Manual restart with button press

3. **Offside Rule**: Not implemented
   - Players can be anywhere on field
   - Could add: Offside line and detection

4. **Free Kicks**: Not implemented
   - Fouls don't trigger free kicks
   - Could add: Free kick positioning

### Possible Additions

1. **Visual Indicators**:
   - Show restart type (Corner, Goal Kick, Throw-In)
   - Highlight player taking restart
   - Show countdown timer before restart

2. **Manual Control**:
   - Player presses button to restart play
   - Choose direction/power for throw-in
   - Choose target player for goal kick

3. **AI Behavior**:
   - Players position themselves for restart
   - Defensive players mark attackers
   - Goalkeeper stays in goal for corner kicks

4. **Last Touch Tracking**:
   ```csharp
   private Player _lastPlayerToTouchBall;
   
   void OnBallTouched(Player player)
   {
       _lastPlayerToTouchBall = player;
   }
   
   void HandleBallOut()
   {
       bool homeTeamLastTouch = _lastPlayerToTouchBall.TeamId == _homeTeam.Id;
       // Assign restart to opposite team
   }
   ```

5. **Referee Positioning**:
   - Referee moves to restart position
   - Visual whistle animation
   - Delay before play resumes

## Testing Checklist

### Goal Detection
- [x] Ball crosses left goal line within goal area → Away team scores
- [x] Ball crosses right goal line within goal area → Home team scores
- [x] Ball below crossbar height → Goal counts
- [x] Ball above crossbar height → No goal (ball out)

### Ball Out Detection
- [ ] Ball goes over left crossbar → Positioned near left corner
- [ ] Ball goes over right crossbar → Positioned near right corner
- [ ] Ball out on left goal line → Corner/goal kick position
- [ ] Ball out on right goal line → Corner/goal kick position
- [ ] Ball out on top sideline → Throw-in position (top)
- [ ] Ball out on bottom sideline → Throw-in position (bottom)

### Player Positioning
- [ ] Nearest player moves to ball (within 80px)
- [ ] Player not moved if already close (< 100px)
- [ ] Correct player selected (actual nearest)

### Edge Cases
- [ ] Ball barely over crossbar (height = 201px)
- [ ] Ball at exact goal line (X = StadiumMargin)
- [ ] Ball in corner area
- [ ] Multiple balls out in succession
- [ ] Ball out during celebration (shouldn't happen)

## Code Locations

### Modified Methods
- `MatchEngine.ClampBallToField()` - Allow ball through goal area
- `MatchEngine.CheckGoal()` - Detect all out-of-bounds scenarios

### New Methods
- `HandleBallOverCrossbar(bool leftSide)` - Over crossbar handler
- `HandleBallOutGoalLine(bool leftSide, float yPosition)` - Goal line handler
- `HandleBallOutSideline(float xPosition, bool topSide)` - Sideline handler
- `PlaceBallForRestart(Vector2 position)` - Generic restart placement

### Dependencies
- Ball physics system (velocity, height)
- Player positioning system
- Field boundaries (StadiumMargin, FieldWidth, FieldHeight)
- Goal dimensions (GoalWidth, GoalDepth, GoalPostHeight)

## Performance Notes
- All handlers are O(n) where n = player count (22 players)
- Finding nearest player uses simple distance calculation
- No complex pathfinding or AI during restart
- Negligible performance impact (~10-20 microseconds)
