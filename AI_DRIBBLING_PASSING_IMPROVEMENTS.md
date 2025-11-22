# AI Dribbling and Passing Improvements

## Problems Fixed

### 1. Player Not Moving With Ball While Dribbling
**Issue**: AI players would move toward their target but wouldn't actually control/push the ball. The ball would stay behind while the player ran ahead.

**Root Cause**: The DribblingState only set player velocity but didn't kick the ball. Unlike the human player who automatically kicks the ball when moving near it, AI players had no such mechanism.

### 2. Passing to Current Position Instead of Future Position
**Issue**: Passes were aimed at where teammates currently were, not where they would be when the ball arrived. This caused passes to go behind moving players.

**Root Cause**: PassingState used `passTarget.FieldPosition` (current position) instead of predicting where the player would move to.

---

## Solutions Implemented

### 1. AI Automatic Ball Kicking During Dribbling

**File**: `Gameplay/MatchEngine.cs` (lines ~522-543)

Added automatic ball kicking for AI players similar to human player control:

```csharp
// AI dribbling: Kick ball automatically when close and moving
if (baseVelocity.LengthSquared() > 0.01f && BallHeight < 100f)
{
    float distToBall = Vector2.Distance(player.FieldPosition, BallPosition);
    if (distToBall < BallKickDistance * 1.5f)
    {
        Vector2 moveDirection = Vector2.Normalize(baseVelocity);
        if (CanPlayerKickBall(player, moveDirection, BallKickDistance * 1.5f))
        {
            float timeSinceLastKick = (float)MatchTime - player.LastKickTime;
            if (timeSinceLastKick >= AutoKickCooldown)
            {
                // Kick ball in movement direction
                float staminaStatMultiplier = GetStaminaStatMultiplier(player);
                float kickPower = (player.Shooting / 8f + 6f) * staminaStatMultiplier * GetAIDifficultyModifier();
                BallVelocity = moveDirection * kickPower * player.Speed * 1.0f;
                BallVerticalVelocity = 15f; // Very low kick for dribbling
                _lastPlayerTouchedBall = player;
                player.LastKickTime = (float)MatchTime;
            }
        }
    }
}
```

**Key Features**:
- **Distance check**: Ball must be within 1.5x kick distance (~52px)
- **Direction check**: Uses `CanPlayerKickBall()` to ensure ball is in front of player
- **Cooldown**: Respects `AutoKickCooldown` to prevent continuous kicking
- **Low trajectory**: `BallVerticalVelocity = 15f` keeps ball on ground for dribbling
- **Stat-based power**: Uses player's Shooting stat and stamina
- **AI difficulty**: Modified by difficulty setting

### 2. Simplified Dribbling Movement Logic

**File**: `Gameplay/AIStates/DribblingState.cs` (lines ~238-268)

Replaced complex repositioning logic with distance-based movement:

```csharp
// If close to ball, move in the desired direction (ball will be kicked automatically)
if (distToBall < 60f)
{
    // Very close - just move in desired direction, player will kick ball
    Vector2 moveDirection = GetSafeDirection(player.FieldPosition, desiredKickDirection, context);
    float dribbleSpeed = player.Speed * 2.5f;
    player.Velocity = moveDirection * dribbleSpeed;
}
else if (distToBall < 150f)
{
    // Medium distance - move toward ball first
    Vector2 toBall = context.BallPosition - player.FieldPosition;
    if (toBall.LengthSquared() > 0)
    {
        toBall.Normalize();
        Vector2 moveDirection = GetSafeDirection(player.FieldPosition, toBall, context);
        float chaseSpeed = player.Speed * 2.5f;
        player.Velocity = moveDirection * chaseSpeed;
    }
}
else
{
    // Far from ball - move directly to ball
    Vector2 toBall = context.BallPosition - player.FieldPosition;
    if (toBall.LengthSquared() > 0)
    {
        toBall.Normalize();
        float chaseSpeed = player.Speed * 2.5f;
        player.Velocity = toBall * chaseSpeed;
    }
}
```

**Distance Thresholds**:
- **< 60px**: Move in desired direction (toward goal) - ball will be kicked automatically
- **60-150px**: Move toward ball to get close enough
- **> 150px**: Chase ball directly

### 3. Predictive Passing System

**File**: `Gameplay/AIStates/PassingState.cs` (lines ~30-50)

Added velocity-based position prediction:

```csharp
// PREDICT target's future position based on their velocity
Vector2 targetCurrentPos = passTarget.FieldPosition;
Vector2 targetVelocity = passTarget.Velocity;

// Calculate approximate travel time for the pass
float distanceToTarget = Vector2.Distance(context.BallPosition, targetCurrentPos);
float estimatedPassSpeed = 400f; // Rough estimate of pass speed
float travelTime = distanceToTarget / estimatedPassSpeed;

// Predict where target will be
Vector2 predictedPosition = targetCurrentPos + (targetVelocity * travelTime);

// Calculate desired pass direction to PREDICTED position
Vector2 passDirection = predictedPosition - context.BallPosition;
```

**How It Works**:
1. **Get current position and velocity** of pass target
2. **Estimate travel time**: `distance / passSpeed`
3. **Predict future position**: `currentPos + (velocity × travelTime)`
4. **Aim pass at predicted position** instead of current position

**Benefits**:
- Passes lead moving teammates correctly
- Forwards running toward goal receive ball in stride
- Reduces failed passes to moving players
- More realistic "through ball" behavior

---

## Technical Details

### Ball Kicking Mechanics

**Conditions Required**:
1. AI player has velocity (is moving)
2. Ball is on ground (height < 100f)
3. Ball is within 52.5px (BallKickDistance × 1.5)
4. Ball is in front of player (CanPlayerKickBall check)
5. Cooldown expired (prevents juggling)

**Kick Physics**:
- **Horizontal velocity**: `moveDirection × kickPower × playerSpeed`
- **Vertical velocity**: 15f (keeps ball low for dribbling)
- **Power calculation**: Based on Shooting stat, stamina, and difficulty

### Pass Prediction Formula

```
travelTime = distance / estimatedPassSpeed
predictedPosition = currentPosition + (velocity × travelTime)
```

**Variables**:
- `estimatedPassSpeed`: 400 pixels/second (approximate)
- `velocity`: Player's current movement velocity
- `travelTime`: How long ball takes to reach target

**Accuracy**:
- Works well for players running in straight lines
- Less accurate for players changing direction
- Good enough for most gameplay situations

---

## Expected Behavior

### Dribbling
✅ AI players now push the ball forward while running
✅ Ball stays close to dribbling player
✅ Natural-looking ball control similar to human player
✅ No more "player running without ball" issues

### Passing
✅ Passes lead moving teammates correctly
✅ Through balls work properly for forwards making runs
✅ Better pass accuracy for players in motion
✅ More successful pass completions

### Gameplay Impact
✅ Smoother AI dribbling animations
✅ More realistic attacking movements
✅ Better coordinated team play
✅ Fewer broken attacks due to poor ball control

---

## Testing Recommendations

1. **Watch AI dribbling**: Player should push ball forward smoothly
2. **Check pass accuracy**: Passes should lead moving teammates
3. **Test through balls**: Long passes to running forwards should work
4. **Observe ball control**: Ball shouldn't get left behind during runs
5. **Enable debug overlay** (D key): Visualize AI velocity and targets
