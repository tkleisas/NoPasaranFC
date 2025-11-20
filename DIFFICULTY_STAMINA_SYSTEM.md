# Difficulty and Stamina System Documentation

## Overview
This document describes the implementation of the Difficulty and Stamina systems in NO PASARAN! FC.

---

## 🎮 Difficulty System

### Difficulty Levels

| Level | Value | Description | AI Modifier | Reaction Time |
|-------|-------|-------------|-------------|---------------|
| **Easy** | 0 | AI plays 30% worse | 0.7x | 1.5x slower |
| **Normal** | 1 | Balanced gameplay | 1.0x | Normal |
| **Hard** | 2 | AI plays 30% better | 1.3x | 0.7x faster (30% faster) |

### How Difficulty Affects Gameplay

#### AI Speed Multiplier
- **Easy**: AI moves at 70% effectiveness
- **Normal**: AI moves at 100% effectiveness  
- **Hard**: AI moves at 130% effectiveness

#### AI Reaction Time
- **Easy**: AI reacts 50% slower (more time for player)
- **Normal**: AI reacts at normal speed
- **Hard**: AI reacts 30% faster (less time for player)

#### Affected AI Actions
✅ **Movement speed** - How fast AI players run
✅ **Shooting power** - How hard AI players kick
✅ **Positioning** - How quickly AI reaches targets
✅ **Ball control** - Indirect effect through stats

### Code Implementation

```csharp
// In MatchEngine.cs

private float GetAIDifficultyModifier()
{
    int difficulty = GameSettings.Instance.Difficulty;
    return difficulty switch
    {
        0 => 0.7f,  // Easy: AI 30% worse
        1 => 1.0f,  // Normal: No change
        2 => 1.3f,  // Hard: AI 30% better
        _ => 1.0f
    };
}

private float GetAIReactionTimeMultiplier()
{
    int difficulty = GameSettings.Instance.Difficulty;
    return difficulty switch
    {
        0 => 1.5f,  // Easy: AI reacts 50% slower
        1 => 1.0f,  // Normal: Normal reactions
        2 => 0.7f,  // Hard: AI reacts 30% faster
        _ => 1.0f
    };
}
```

### Usage in Gameplay

**AI Movement:**
```csharp
float moveSpeed = player.Speed * 2.5f * urgency * 
                  GameSettings.Instance.PlayerSpeedMultiplier * 
                  staminaMultiplier * difficultyMultiplier;
```

**AI Shooting:**
```csharp
float kickPower = (player.Shooting / 15f + 3f) * 
                  staminaStatMultiplier * aiDifficultyModifier;
```

---

## ⚡ Stamina System

### Stamina Mechanics

**Starting Value**: 100 (full stamina)
**Range**: 0 to 100

### Stamina Changes

| Action | Stamina Change | Rate |
|--------|----------------|------|
| **Running** | -3 per second | Continuous drain while moving |
| **Shooting** | -5 per shot | Instant drain on shot |
| **Tackling** | -5 per tackle | Instant drain on tackle attempt |
| **Idle/Standing** | +2 per second | Continuous recovery when not moving |

### Constants
```csharp
private const float StaminaDecreasePerSecondRunning = 3f;
private const float StaminaDecreasePerShot = 5f;
private const float StaminaRecoveryPerSecond = 2f;
private const float LowStaminaThreshold = 30f;
```

### Stamina Effects

#### Speed Multiplier
When stamina drops below 30:
```csharp
float staminaRatio = stamina / 30f;
speedMultiplier = 0.7f + (staminaRatio * 0.3f); // Range: 0.7 to 1.0
```

| Stamina | Speed % | Effect |
|---------|---------|--------|
| 100-30 | 100% | Normal speed |
| 30 | 100% | Threshold (no penalty yet) |
| 20 | 87% | Slightly slower |
| 10 | 77% | Noticeably slower |
| 0 | 70% | 30% speed reduction |

#### Stats Multiplier
When stamina drops below 30:
```csharp
float staminaRatio = stamina / 30f;
statsMultiplier = 0.6f + (staminaRatio * 0.4f); // Range: 0.6 to 1.0
```

| Stamina | Stats % | Affected Actions |
|---------|---------|------------------|
| 100-30 | 100% | No penalty |
| 20 | 87% | Weaker shots/tackles |
| 10 | 73% | Much weaker performance |
| 0 | 60% | 40% stat reduction |

#### Affected Stats
✅ **Shooting** - Shot power reduced
✅ **Defending** - Tackle success reduced
✅ **Agility** - Player responsiveness reduced
✅ **Speed** - Movement speed reduced

### Code Implementation

```csharp
// Speed multiplier
private float GetStaminaSpeedMultiplier(Player player)
{
    if (player.Stamina < LowStaminaThreshold)
    {
        float staminaRatio = player.Stamina / LowStaminaThreshold;
        return 0.7f + (staminaRatio * 0.3f);
    }
    return 1.0f;
}

// Stats multiplier
private float GetStaminaStatMultiplier(Player player)
{
    if (player.Stamina < LowStaminaThreshold)
    {
        float staminaRatio = player.Stamina / LowStaminaThreshold;
        return 0.6f + (staminaRatio * 0.4f);
    }
    return 1.0f;
}
```

### Stamina Updates

**While Running:**
```csharp
player.Stamina = Math.Max(0, player.Stamina - StaminaDecreasePerSecondRunning * deltaTime);
```

**While Idle:**
```csharp
player.Stamina = Math.Min(100, player.Stamina + StaminaRecoveryPerSecond * deltaTime);
```

**On Shot/Tackle:**
```csharp
player.Stamina = Math.Max(0, player.Stamina - StaminaDecreasePerShot);
```

---

## 📊 Visual Display

### Stamina Bar

**Location**: Below each player sprite
**Size**: 50px wide × 4px tall
**Visibility**: Controlled by `GameSettings.Instance.ShowStamina`

#### Color Coding

| Stamina Range | Color | Visual |
|---------------|-------|--------|
| 60-100% | Green | `Color(0, 255, 0)` |
| 30-60% | Yellow/Orange | `Color(255, 200, 0)` |
| 0-30% | Red | `Color(255, 0, 0)` |

#### Rendering
```csharp
if (GameSettings.Instance.ShowStamina && !player.IsKnockedDown)
{
    // Background (dark gray)
    spriteBatch.Draw(_pixel, backgroundRect, new Color(40, 40, 40, 200));
    
    // Stamina fill (color based on level)
    Color staminaColor = staminaPercent > 0.6f ? Color.Green :
                         staminaPercent > 0.3f ? Color.Yellow : Color.Red;
    spriteBatch.Draw(_pixel, fillRect, staminaColor);
    
    // Border (white outline)
    DrawRectangleOutline(spriteBatch, _pixel, barRect, Color.White, 1);
}
```

---

## 🎯 Strategy and Tactics

### For Players

**Managing Stamina:**
1. **Sprint strategically** - Don't run constantly
2. **Rest when possible** - Let stamina recover during defensive phases
3. **Conserve for key moments** - Save stamina for attacking runs
4. **Switch players** - Control fresh players when others are tired

**Difficulty Tactics:**

**Easy Mode:**
- AI slower and weaker
- More time to position and shoot
- Good for learning the game

**Normal Mode:**
- Balanced gameplay
- Requires skill and positioning
- Standard challenge

**Hard Mode:**
- AI faster and more aggressive
- Requires excellent stamina management
- Demands precise timing and positioning

### Gameplay Impact Examples

**Example 1: Full Stamina vs Low Stamina**
```
Full Stamina (100):
- Shot power: 100%
- Speed: 100%
- Tackle success: 100%

Low Stamina (10):
- Shot power: 73% (weaker shots)
- Speed: 77% (slower movement)
- Tackle success: 73% (less likely to win ball)
```

**Example 2: Difficulty Comparison (Same AI player)**
```
Easy (AI modifier 0.7):
- Movement: 70% of normal
- Shooting: 70% of normal
- Easier to beat

Normal (AI modifier 1.0):
- Movement: 100% (balanced)
- Shooting: 100% (balanced)

Hard (AI modifier 1.3):
- Movement: 130% (very fast)
- Shooting: 130% (powerful)
- Challenging to beat
```

---

## 🔧 Settings Integration

Both systems are controlled through the Settings menu:

**Difficulty** (Settings option 7):
- Adjustable: Left/Right arrow keys
- Values: Easy / Normal / Hard
- Saved to database
- Takes effect immediately in next match

**Show Stamina** (Settings option 12):
- Toggle: Left/Right arrow keys or Enter
- Values: ON / OFF
- Saved to database
- Takes effect immediately during match

---

## 🌍 Localization

Both features are fully localized in English and Greek:

### English
```
settings.difficulty = "Difficulty"
settings.difficulty.easy = "Easy"
settings.difficulty.normal = "Normal"  
settings.difficulty.hard = "Hard"
settings.showStamina = "Show Stamina"
```

### Greek
```
settings.difficulty = "Δυσκολία"
settings.difficulty.easy = "Εύκολο"
settings.difficulty.normal = "Κανονικό"
settings.difficulty.hard = "Δύσκολο"
settings.showStamina = "Εμφάνιση Κόπωσης"
```

---

## 💾 Database Storage

Both settings persist in the Settings table:

```sql
CREATE TABLE Settings (
    ...
    Difficulty INTEGER DEFAULT 1,
    ShowStamina INTEGER DEFAULT 1,
    ...
);
```

---

## 🎮 Complete Flow Example

**Match Start:**
1. All players at 100 stamina (green bars)
2. Difficulty modifier applied to AI

**During Match:**
1. Player runs → stamina decreases (-3/sec)
2. Stamina drops below 30 → speed/stats affected
3. Player stands still → stamina recovers (+2/sec)
4. Player shoots → stamina drops (-5)
5. Stamina bar color changes: Green → Yellow → Red

**AI Behavior:**
1. AI affected by difficulty (speed, shooting, reaction)
2. AI also gets tired (stamina decreases)
3. AI performance degrades with low stamina
4. Creates realistic fatigue simulation

---

## 📈 Performance Notes

- Stamina calculations are lightweight (simple math)
- No significant performance impact
- Updates happen once per frame per player
- Visual bars rendered only when ShowStamina is ON

---

## 🚀 Future Enhancements (Potential)

- [ ] Stamina-based substitutions (automatic when very low)
- [ ] Formation-specific stamina drain (attackers tire faster)
- [ ] Weather effects on stamina (hot weather = faster drain)
- [ ] Difficulty-specific stamina recovery rates
- [ ] Player-specific stamina stats (some players have better endurance)
- [ ] Halftime stamina recovery boost
- [ ] Training to improve stamina over season

---

**Version**: 1.0  
**Last Updated**: 2025-11-20  
**Systems Implemented**: Difficulty (Easy/Normal/Hard) + Stamina (0-100)  
**Status**: ✅ Fully Functional
