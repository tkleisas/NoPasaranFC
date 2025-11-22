# AI Long Passes & Goal-Oriented Movement

## Changes Made

Implemented aggressive attacking play with long passes to forwards and goal-oriented positioning.

## 1. Long Pass System

### Increased Pass Frequency

**Decision Interval**: 0.5s → 0.3s (66% faster decision making)

More frequent evaluation of passing opportunities.

### Expanded Passing Ranges

| Pass Type | Old Range | New Range | Purpose |
|-----------|-----------|-----------|---------|
| Short | 200-700px | 150-600px | Quick passes |
| Long | N/A | 300-1200px | Feed forwards |

### Pass Target Priority

**Before**: Always pass to `NearestTeammate`

**After**: Pass to `BestPassTarget` (forwards/attackers closer to goal)

```csharp
// PassingState now prioritizes attackers
Player passTarget = context.BestPassTarget ?? context.NearestTeammate;
```

### Pass Power Scaling

**Old**: Max 0.9 power (capped too low for long passes)

**New**: Up to 1.0 power (full power for long passes)

```csharp
float power = MathHelper.Clamp(distance / 800f, 0.4f, 1.0f);
// 300px: 0.4 power
// 800px: 1.0 power (full power!)
```

### Pass Probability by Situation

| Situation | Condition | Probability | Notes |
|-----------|-----------|-------------|-------|
| Under pressure | Opponent < 250px | 85% | Very high (was 70%) |
| Long pass to forward | 300-1200px, forward closer to goal | 80% | NEW - long balls |
| Medium pass to forward | 150-600px, forward closer to goal | 70% | NEW - medium balls |
| Attacking player general | 150-600px, is Forward/Midfielder | 60% | NEW - attackers pass more |
| Near goal | < 600px from goal | 70% | Increased from 60% |

## 2. Attacking Player Movement

### Forwards (Already Good)

- Push to **85% of field** (very close to goal)
- Stay ahead of ball when in attacking half
- Don't drop back when ball in defensive half

### Attacking Midfielders (Improved)

**New aggressive positioning:**

```csharp
bool isAttackingMidfielder = player.Role == PlayerRole.AttackingMidfielder ||
                            player.Role == PlayerRole.LeftWinger ||
                            player.Role == PlayerRole.RightWinger;
```

**Positioning:**
- **Attacking midfielders**: Push to 75% of field (was 65%)
- **Other midfielders**: Stay at 65% of field
- **Urgency**: Attacking mids at 1.0 (full urgency)

**Ball following:**
- **Attacking midfielders**: 40% ball influence (focus on goal position)
- **Other midfielders**: 50% ball influence

### Position Comparison

| Role | Old Forward % | New Forward % | Improvement |
|------|---------------|---------------|-------------|
| Forwards | 85% | 85% | Same (already good) |
| Attacking Midfielders | 65% | **75%** | +10% closer to goal |
| Wingers | 65% | **75%** | +10% closer to goal |
| Central Midfielders | 65% | 65% | Same (balanced) |

## 3. Decision Making Improvements

### DribblingState Logic

**Passing priority hierarchy:**

1. **Under pressure** (opponent < 250px) → 85% pass
2. **Shooting range** (< 600px to goal) → 70% shoot
3. **Long pass available** (teammate 300-1200px, closer to goal) → 80% pass
4. **Medium pass available** (teammate 150-600px, closer to goal) → 70% pass
5. **General passing** (attacking player, 150-600px) → 60% pass
6. **Continue dribbling** → Move toward goal

### Why This Works

**More Pass Attempts**:
- Faster decision making (0.3s vs 0.5s)
- Higher pass probabilities (60-85% vs 50-70%)
- Longer pass range (up to 1200px vs 700px)

**Better Pass Selection**:
- Prioritizes forwards and attackers
- Long balls to players closer to goal
- Full power for distant targets

**Aggressive Positioning**:
- Attacking midfielders push to 75% field
- Forwards hold position at 85% field
- Creates passing lanes and goal threats

## Example Play Sequence

### Before

```
Defender gets ball at X=800 (own half)
  → Looks for pass (decisions every 0.5s)
  → NearestTeammate at X=1200, distance=400px
  → No pass (not in 200-700 range? or 50% dice roll failed?)
  → Dribbles forward slowly
  → Eventually shoots from distance
```

### After

```
Defender gets ball at X=800 (own half)
  → Looks for pass (decisions every 0.3s) ← FASTER
  → BestPassTarget is Forward at X=2800, distance=2000px... too far
  → Midfielder at X=1600, distance=800px
  → Long pass check: 300-1200px? YES! Closer to goal? YES!
  → 80% probability → PASS! ← LONG BALL
  → Midfielder gets ball at X=1600 (attacking half, now 75% forward)
  → Looks for pass (0.3s later)
  → Forward at X=2800, distance=1200px
  → Long pass check: 300-1200px? YES! Much closer to goal!
  → 80% probability → PASS! ← THROUGH BALL
  → Forward gets ball at X=2800 (near goal!)
  → Shooting range! 70% probability → SHOOT!
```

## Files Modified

✅ **DribblingState.cs**
- Increased decision frequency: 0.5s → 0.3s
- Added long pass logic (300-1200px range)
- Increased pass probabilities (60-85%)
- Prioritize BestPassTarget over NearestTeammate

✅ **PassingState.cs**
- Use BestPassTarget instead of just NearestTeammate
- Increased max power: 0.9 → 1.0
- Better power scaling for long passes

✅ **MidfielderState.cs**
- Attacking midfielders push to 75% (was 65%)
- Full urgency (1.0) for attacking mids
- Better goal-oriented positioning

## Build Status

✅ Project builds successfully
✅ No errors

## Testing Checklist

✅ AI attempts more passes (frequent decisions)
✅ Long passes to forwards (300-1200px range)
✅ Forwards hold advanced positions (85% field)
✅ Attacking midfielders push forward (75% field)
✅ Passes use full power for long distances
✅ BestPassTarget prioritized over nearest player
✅ Attacking play flows toward opponent goal

## Expected Behavior

### Passing
- Defenders/midfielders make long passes to forwards
- Forwards receive ball in advanced positions
- More dynamic, flowing play
- Less dribbling, more passing

### Movement
- Attacking midfielders push closer to goal
- Forwards stay near opponent's box
- Better offensive shape
- More goal-scoring opportunities

## Summary

**Problem**: 
- Conservative passing (short range, low probability)
- Midfielders not attacking enough
- Forwards isolated

**Solution**:
- Long pass system (300-1200px)
- Higher pass probabilities (60-85%)
- Attacking midfielders push to 75% field
- Faster decisions (0.3s vs 0.5s)

**Result**: Dynamic attacking play with long balls to forwards and aggressive goal-oriented positioning! ⚽🎯
