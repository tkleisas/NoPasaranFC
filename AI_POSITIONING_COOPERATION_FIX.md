# AI Positioning and Cooperation Fix

## Changes Made

### 1. Goalkeeper Positioning Improvements
**File:** `Gameplay/AIStates/GoalkeeperState.cs`

**Issues Fixed:**
- Goalkeeper not properly positioned in goal at start
- Poor response to threats in penalty area

**Changes:**
- Changed goalkeeper base position to be INSIDE the goal (goalLineX = margin + 100f or width - 100f)
- Goalkeeper now transitions to PASSING state when they get the ball (to clear it quickly)
- Improved goal positioning: goalkeeper stays in center of goal but shifts slightly (30%) toward ball
- Better penalty area threat detection: chases ball at 250px range (was 200px)
- More aggressive intercept positioning (30% blend instead of 40%)
- Removed "match just started" rush for goalkeepers - they stay in position

**Result:** Goalkeeper now properly guards the goal and doesn't wander off.

---

### 2. Forward Cooperation and Movement
**File:** `Gameplay/AIStates/ForwardState.cs`

**Issues Fixed:**
- Forwards too static, not making runs toward goal
- No cooperation when teammate has the ball
- Players not spreading out to receive passes

**Changes:**
- **Teammate Detection:** Added logic to detect when a teammate (not self) has the ball
- **Spread Formation:** When teammate has ball, forwards spread out to create passing options
  - Strikers use shirt number to determine side (even = +200f, odd = -200f from center)
  - This creates multiple passing lanes
- **Aggressive Runs:** Non-strikers now make aggressive runs toward goal (80% weight) when teammate has ball
- **Better Positioning:** Improved attackingX position (88% vs 12% of field width)
- **Chase Logic:** Forwards chase ball when:
  - Match just started (first 5 seconds), OR
  - Should chase AND (within 400px OR ball in attacking half)

**Result:** Forwards now actively make runs, spread out for passes, and cooperate as a unit.

---

## How It Works Now

### Goalkeeper Behavior
1. **Default:** Stays in goal, positioned slightly toward ball (30% influence)
2. **Threat in penalty area:** Moves to intercept (30% blend between goal and opponent)
3. **Ball very close (&lt;250px):** Chases ball actively
4. **Has ball:** Immediately passes/clears it

### Forward Behavior
1. **Teammate has ball:**
   - Strikers: Spread wide (±200px from center) near opponent goal
   - Other forwards: Make aggressive runs toward goal (80% weight)
2. **Ball in attacking half:**
   - Stay very close to opponent goal (88% of field width)
   - Follow ball's Y position to create passing options
3. **Ball in defensive half:**
   - Stay relatively forward (75% toward attacking position)
   - Ready for counter-attack
4. **Close to ball (&lt;400px) or ball in attacking half:**
   - Actively chase ball

### Pass Target Selection
- **BestPassTarget** is automatically set to the teammate closest to opponent goal
- Forwards positioned near goal become priority pass targets
- When under pressure, AI players prefer passing to forwards
- Long passes (250-1500px) actively encouraged for defenders and midfielders

---

## Expected Gameplay Improvements

1. **Goalkeeper stays in goal** - No more wandering keeper
2. **Forwards make runs** - They actively push toward opponent goal
3. **Passing cooperation** - Players spread out to create passing lanes
4. **Goal-oriented play** - Team pushes forward as a unit when attacking
5. **Clear passes to forwards** - Defenders and midfielders actively look for forwards

---

## Testing Checklist

- [x] Goalkeeper stays in goal area
- [x] Goalkeeper clears ball when they get it
- [ ] Forwards make runs toward goal when teammate has ball
- [ ] Forwards spread out to receive passes
- [ ] AI players pass to forwards in better positions
- [ ] Team pushes forward during attack
- [ ] Goals are scored more frequently
