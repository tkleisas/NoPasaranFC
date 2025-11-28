# Goal Celebration System

This directory contains the goal celebration system for the football game. The system is designed to be extensible, allowing you to easily add new celebration types.

## Architecture Overview

The celebration system consists of three main components:

1. **CelebrationBase** - Abstract base class that defines the interface for all celebrations
2. **Specific Celebrations** (e.g., `RunAroundPitchCelebration`) - Concrete implementations of different celebration types
3. **CelebrationManager** - Manages all available celebrations and handles selection/execution

## Special Features

### Celebration Selection Hierarchy

The system uses a **hierarchical selection system** to determine which celebration to use:

**0. Own Goal Override** ⚠️ (Highest Priority - Overrides Everything!)
   - **Own goals ALWAYS use "run_around_pitch"**
   - All players from both teams celebrate together
   - Ignores player/team celebration preferences
   - Non-negotiable for hilarious effect!

**1. Player-Specific Celebrations** (High Priority)
   - Each player can have multiple unique celebrations
   - Set via `player.CelebrationIds = new List<string> { "celebration1", "celebration2" }`
   - Randomly picks from player's celebration list
   - Example: Star players with multiple signature celebrations

**2. Team-Specific Celebrations** (Medium Priority)
   - Each team can have multiple celebration styles
   - Set via `team.CelebrationIds = new List<string> { "celebration1", "celebration2" }`
   - Randomly picks from team's celebration list
   - Used when player has no specific celebrations
   - Example: Teams with traditional celebration repertoire

**3. Generic Celebrations** (Fallback)
   - Randomly selected from all available celebrations
   - Used when neither player nor team has specific celebrations
   - Default behavior

**How it works:**
```csharp
// Set player-specific celebrations (can have multiple!)
player.CelebrationIds = new List<string>
{
    "backflip_celebration",
    "slide_celebration",
    "run_around_pitch"
};

// Set team-specific celebrations
team.CelebrationIds = new List<string>
{
    "team_huddle",
    "run_around_pitch"
};

// Normal goal by this player: randomly picks from their 3 celebrations
// Normal goal by other team members: randomly picks from team's 2 celebrations
// Normal goal with no celebrations set: picks from all generic celebrations
// OWN GOAL: ALWAYS uses "run_around_pitch" regardless of settings!
```

### Own Goal Detection
The system automatically detects own goals! When a player scores on their own team's goal:
- **ALWAYS uses "run_around_pitch" celebration** - Overrides all player/team preferences
- **Both teams celebrate together** - All players join the celebration
- The player who made the own goal becomes the "scorer" and leads the celebration
- No opponents - everyone runs together in the line formation
- Makes for hilarious moments when someone accidentally scores on themselves!
- **Cannot be customized** - own goals are special and always treated the same way

## Current Celebrations

### RunAroundPitchCelebration
- **ID**: `run_around_pitch`
- **Description**: Scorer runs a lap around the pitch boundaries while teammates follow in a line formation
- **Behavior**:
  - Scorer: Follows a counter-clockwise path around the pitch perimeter at 350 px/s (fixed speed)
  - Teammates: Form a spaced line (200px apart) following the scorer at same 350 px/s (fixed speed)
  - Catch-up: Players run faster (420 px/s) when too far behind, then match formation speed
  - Animation: All celebrating players use "celebrate" animation (arms extended)
  - Camera: Zooms out to 40% to show the entire celebration
  - Opponents: Stay idle (unless it's an own goal - see below)
  - **Own Goals**: If it's an own goal, ALL players from BOTH teams join the celebration!

## How to Add a New Celebration

### Step 1: Create a New Celebration Class

Create a new file in this directory (e.g., `GroupHuddleCelebration.cs`):

```csharp
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.Celebrations
{
    public class GroupHuddleCelebration : CelebrationBase
    {
        // Unique identifier
        public override string CelebrationId => "group_huddle";

        // Display information
        public override string Name => "Team Huddle";
        public override string Description => "Players rush together in a celebratory huddle";

        // Store celebration-specific data
        private Vector2 _huddleCenter;
        private const float HuddleRadius = 150f;

        public override void Initialize(Player scorer, List<Player> teammates, List<Player> opponents)
        {
            // Initialize celebration-specific data
            // For example: set huddle center to scorer's position
            _huddleCenter = scorer.FieldPosition;
        }

        public override void UpdateScorer(Player scorer, float deltaTime, float celebrationTime)
        {
            // Define scorer behavior
            // For example: jump up and down at huddle center
            scorer.Velocity = Vector2.Zero; // Stay at huddle center
        }

        public override void UpdateTeammate(Player teammate, Player scorer,
            List<Player> allTeammates, int teammateIndex, float deltaTime, float celebrationTime)
        {
            // Define teammate behavior
            // For example: run toward huddle center
            Vector2 toHuddle = _huddleCenter - teammate.FieldPosition;
            float distance = toHuddle.Length();

            if (distance > HuddleRadius)
            {
                // Run toward huddle
                toHuddle.Normalize();
                teammate.Velocity = toHuddle * teammate.Speed * 3.0f;
            }
            else
            {
                // At huddle, slow down
                teammate.Velocity = Vector2.Zero;
            }
        }

        public override void UpdateOpponent(Player opponent, float deltaTime, float celebrationTime)
        {
            // Opponents stay idle or show disappointment
            opponent.Velocity = Vector2.Zero;
        }

        public override void Cleanup()
        {
            // Clean up any resources
            // Reset celebration-specific data
        }

        public override Vector2? GetCameraTarget(Player scorer, List<Player> teammates)
        {
            // Return huddle center for camera to follow
            return _huddleCenter;
        }
    }
}
```

### Step 2: Register the New Celebration

Open `CelebrationManager.cs` and add your celebration in the constructor:

```csharp
public CelebrationManager()
{
    _availableCelebrations = new Dictionary<string, CelebrationBase>();
    _random = new Random();

    // Register all available celebrations
    RegisterCelebration(new RunAroundPitchCelebration());
    RegisterCelebration(new GroupHuddleCelebration()); // ← Add your new celebration here
    // Add more celebrations below...
}
```

### Step 3: Test Your Celebration

That's it! Your celebration will now be randomly selected when a goal is scored.

## Advanced: Custom Selection Logic

To implement custom celebration selection logic (e.g., based on goal importance), override the `SelectCelebration()` method in `CelebrationManager`:

```csharp
protected override CelebrationBase SelectCelebration()
{
    // Example: Use different celebrations based on score difference
    int scoreDifference = Math.Abs(HomeScore - AwayScore);

    if (scoreDifference >= 3)
    {
        // Blowout - use subdued celebration
        return GetCelebration("group_huddle");
    }
    else if (scoreDifference == 0)
    {
        // Equalizer - use dramatic celebration
        return GetCelebration("run_around_pitch");
    }

    // Default random selection
    return base.SelectCelebration();
}
```

## How to Assign Celebrations

### For a Specific Player (Star Player Signature)
```csharp
// Give Ronaldo multiple signature celebrations
Player ronaldo = team.Players.Find(p => p.Name == "Ronaldo");
ronaldo.CelebrationIds = new List<string>
{
    "backflip_celebration",
    "run_around_pitch",
    "airplane_run"
};

// Now when Ronaldo scores, he'll randomly do one of his 3 signature moves
```

### For an Entire Team
```csharp
// Give a team their traditional celebrations
Team barcelona = GetTeam("FC Barcelona");
barcelona.CelebrationIds = new List<string>
{
    "team_huddle",
    "run_around_pitch"
};

// All Barcelona players without personal celebrations will randomly pick from these 2
```

### Single Celebration (Player or Team)
```csharp
// If you only want one celebration, just add one to the list
player.CelebrationIds = new List<string> { "backflip_celebration" };
team.CelebrationIds = new List<string> { "team_huddle" };
```

### Leave as Random (Default)
```csharp
// Don't set anything - celebrations will be randomly selected from all available
player.CelebrationIds = null; // or just don't set it
team.CelebrationIds = null;   // or just don't set it

// Or use an empty list
player.CelebrationIds = new List<string>();
team.CelebrationIds = new List<string>();
```

## Celebration Ideas

Here are some ideas for future celebrations:

1. **Slide Celebration** - Scorer slides on knees, teammates run and pile on top
2. **Corner Flag Run** - Scorer runs to corner flag, teammates follow
3. **Dance Celebration** - Players perform synchronized dance moves
4. **Acrobatic Celebration** - Scorer does flips/cartwheels (backflip animation)
5. **Bench Rush** - Teammates run from their positions toward scorer
6. **Heart Shape** - Players form a heart shape with their positions
7. **Wave Celebration** - Players do "the wave" in sequence
8. **Individual Pose** - Scorer strikes a pose while teammates circle around
9. **Airplane Run** - Scorer runs with arms out like airplane wings
10. **Robot Dance** - Stiff, robotic movements

## Key Methods

### CelebrationBase Methods

- **Initialize()** - Set up celebration when it starts (called once)
- **UpdateScorer()** - Called every frame to update scorer's behavior
- **UpdateTeammate()** - Called every frame for each teammate
- **UpdateOpponent()** - Called every frame for each opponent
- **Cleanup()** - Called when celebration ends
- **GetCameraTarget()** - Optional: Return custom camera position

### CelebrationManager Methods

- **StartCelebration()** - Start a celebration (auto-select or specify ID)
- **Update()** - Update active celebration (called every frame)
- **StopCelebration()** - End current celebration
- **RegisterCelebration()** - Add a new celebration type
- **GetCelebration(id)** - Get a specific celebration by ID

## Player Animations

Players have different animation states that can be set during celebrations:

- **"celebrate"** - Arms extended to sides (celebration pose)
- **"walk"** - Normal walking animation
- **"idle"** - Standing still
- **"shoot"** - Kicking animation
- **"tackle"** - Sliding tackle
- **"fall"** - Falling/knocked down

To set an animation during celebration:

```csharp
public override void UpdateScorer(Player scorer, float deltaTime, float celebrationTime)
{
    // Set celebration animation (arms extended)
    scorer.CurrentAnimationState = "celebrate";

    // ... rest of your celebration logic
}
```

The `CelebrationManager` automatically resets all animations to "walk" when the celebration ends, so you don't need to manually clean up.

## Tips for Creating Celebrations

1. **Keep it Simple** - Players are represented by velocity vectors; complex physics may not work well
2. **Use Timers** - `celebrationTime` parameter helps you create multi-stage celebrations
3. **Test Boundaries** - Make sure players don't run off the field
4. **Consider Camera** - Think about what the camera should focus on
5. **Variety is Key** - Different celebrations for different situations keeps it interesting
6. **Performance** - Keep calculations lightweight (runs every frame)
7. **Use Animations** - Set `CurrentAnimationState` to make celebrations more expressive

## File Structure

```
Gameplay/Celebrations/
├── README.md                        (this file)
├── CelebrationBase.cs              (abstract base class)
├── CelebrationManager.cs           (manager and selector)
├── RunAroundPitchCelebration.cs   (implementation)
└── [YourNewCelebration].cs        (add new files here)
```
