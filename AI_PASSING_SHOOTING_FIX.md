# AI Passing and Shooting Fix

## Problem
The AI players were not passing or scoring goals effectively due to conflicting AI systems.

## Root Causes

### 1. Dual AI Systems Conflict
The game had TWO different systems for AI ball control:
- **Modern State Machine**: PassingState and ShootingState with callbacks
- **Legacy PerformAIKick**: Automatic ball kicking that bypassed the state machine

The legacy `PerformAIKick()` was being called every frame when AI had possession (line 524-530 in MatchEngine.cs), which prevented the state machine from executing its pass/shoot decisions.

### 2. Overly Aggressive Shooting Logic
In DribblingState, shooting checks occurred BEFORE passing checks:
- <300px: 95% shoot
- <500px: 85% shoot
- <700px: 60% shoot
- <900px: 30% shoot

This meant AI players would shoot from medium distance instead of passing to better-positioned teammates.

### 3. Restrictive Passing Conditions
- Minimum pass distance was 150px (too far for close passes)
- Forwards only passed when >400px from goal (prevented close teamwork)
- Several position-specific conditions were too strict

## Solution

### 1. Removed Conflicting System
**File**: `Gameplay/MatchEngine.cs` (lines 522-531)

Removed the automatic `PerformAIKick()` call that was overriding state machine decisions:

```csharp
// OLD CODE (REMOVED):
if (context.HasBallPossession && BallHeight < 100f && CanPlayerKickBall(...))
{
    float timeSinceLastKick = (float)MatchTime - player.LastKickTime;
    if (timeSinceLastKick >= AutoKickCooldown)
    {
        PerformAIKick(player);
    }
}

// NEW CODE:
// AI controller handles ALL ball kicking through state machine callbacks
// PassingState and ShootingState will invoke AIPassBall and AIShootBall
```

Now the state machine has full control through its callback system.

### 2. Prioritized Passing Over Shooting
**File**: `Gameplay/AIStates/DribblingState.cs`

Moved passing checks BEFORE shooting checks so AI considers teamwork first.

### 3. Made Passing More Liberal

**Lowered minimum pass distance**: 150px → 80px (allows close passes)

**Reduced teammate-ahead buffer**: 50px → 30px (more flexible positioning)

**Increased pass probabilities**:
- Defenders: Now 70% chance to pass even lateral/backward
- Midfielders: 70% pass chance (up from 60%) for lateral passes
- Forwards: 50% pass chance (up from 40%) when not in ideal shooting position

### 4. Reduced Shooting Aggression

**New shooting probabilities**:
- <300px: 90% shoot (down from 95%)
- <500px: 70% shoot (down from 85%)
- <700px: 40% shoot (down from 60%)
- <900px: 20% shoot (down from 30%)

**Special forward logic**: Only prioritize shooting when <350px from goal, otherwise consider passing more.

## Expected Behavior After Fix

### Passing
- AI defenders will pass the ball forward to midfielders/forwards frequently
- AI midfielders will look for forwards and make long passes
- AI forwards will pass to each other when not in direct shooting position
- All positions will pass when under pressure

### Shooting
- AI forwards will shoot when close to goal (<350px)
- AI will still take shots from medium range but less frequently
- Better balance between individual play and teamwork

### Goal Scoring
- More build-up play leading to better scoring opportunities
- Forwards receiving passes in dangerous positions
- More coordinated attacks instead of solo dribbling runs

## Technical Details

### State Machine Flow
1. Player gets ball possession
2. Enters `DribblingState`
3. Every 0.3s, evaluates:
   - **First**: Can I pass to a better-positioned teammate?
   - **Second**: Am I in shooting range?
   - **Third**: Keep dribbling toward goal
4. Transitions to `PassingState` or `ShootingState`
5. State executes action via callback (`AIPassBall` or `AIShootBall`)
6. Returns to `PositioningState`

### Key Methods
- `AIPassBall()`: Handles aerial/ground pass physics, lofted passes for long distances
- `AIShootBall()`: Executes shots with power calculation
- `BuildAIContext()`: Provides decision-making data (nearest opponent, best pass target, etc.)

## Testing Recommendations

1. Watch AI defenders - should pass forward frequently
2. Watch AI midfielders - should make long passes to forwards
3. Watch AI forwards - should shoot when close, pass otherwise
4. Check goal scoring - should see more goals from coordinated play
5. Enable debug overlay (D key) to visualize AI states and targets
