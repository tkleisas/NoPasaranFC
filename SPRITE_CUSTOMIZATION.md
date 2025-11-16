# Player Sprite Sheet System

## Overview
The game now uses sprite sheets for animated player graphics, with separate sheets for home (blue) and away (red) teams.

## Sprite Sheet Format

### Current Implementation
- **Files**: `player_blue.png` and `player_red.png` in `Content/Sprites/`
- **Size**: 256x256 pixels total (4x4 grid)
- **Frame Size**: 64x64 pixels per frame
- **Layout**: 4 rows × 4 columns
  - Row 0: Walking Down (frames 0-3)
  - Row 1: Walking Up (frames 0-3)
  - Row 2: Walking Left (frames 0-3)
  - Row 3: Walking Right (frames 0-3)

### Sprite Sheet Grid

```
+-------+-------+-------+-------+
| Down0 | Down1 | Down2 | Down3 |  Row 0 (SpriteDirection = 0)
+-------+-------+-------+-------+
|  Up0  |  Up1  |  Up2  |  Up3  |  Row 1 (SpriteDirection = 1)
+-------+-------+-------+-------+
| Left0 | Left1 | Left2 | Left3 |  Row 2 (SpriteDirection = 2)
+-------+-------+-------+-------+
|Right0 |Right1 |Right2 |Right3 |  Row 3 (SpriteDirection = 3)
+-------+-------+-------+-------+
Each cell: 64x64 pixels
```

## Animation System

### Player Properties
- `AnimationFrame` (float): Current frame (0-3.99), loops back to 0
- `SpriteDirection` (int): Direction index (0=down, 1=up, 2=left, 3=right)
- `AnimationSpeed` (float): Speed of animation (default: 0.15)

### Animation Logic
1. **When Moving**: 
   - Frame advances: `AnimationFrame += 8 * deltaTime` (8 fps)
   - Direction updates based on velocity:
     - Horizontal dominant: Left (vel.X < 0) or Right (vel.X > 0)
     - Vertical dominant: Down (vel.Y > 0) or Up (vel.Y < 0)
   - Frames cycle through 0-3 continuously

2. **When Idle**:
   - Frame resets to 0 (standing pose)
   - Direction maintains last movement direction

### Visual Tinting System
- **Home Team**: Uses `player_blue.png` sprite sheet
- **Away Team**: Uses `player_red.png` sprite sheet
- **Controlled Player**: Light yellow tint (255, 255, 150) + yellow circle outline
- **Knocked Down**: Semi-transparent gray (180, 180, 180, 180)
- **Normal**: White tint (shows sprite colors as-is)

### Rendering
- **Render Size**: 128x128 pixels (2x scale from 64x64 sprite frame)
- **Controlled Player**: 136x136 pixels (slightly larger)
- **Knocked Down**: 128x40 pixels (flattened horizontal)
- **Shadow**: 12-pixel tall oval beneath player
- **Rotation**: Automatic rotation when moving diagonally
  - Uses left/right facing sprites
  - Rotation angle calculated from velocity vector

## How to Create Custom Sprite Sheets

### Requirements
- **Image Editor**: Aseprite, Piskel, GIMP, or similar
- **Format**: PNG with transparency
- **Size**: 256x256 pixels (4x4 grid of 64x64 frames)
- **Color**: Full color support

### Steps
1. Create a new 256x256 pixel image
2. Divide into 4×4 grid (64×64 frames)
3. Draw character in 4 directions:
   - Row 0: Facing down (4 walking frames)
   - Row 1: Facing up (4 walking frames)
   - Row 2: Facing left (4 walking frames)
   - Row 3: Facing right (4 walking frames)
4. Save as PNG with transparency
5. Place in `Content/Sprites/` folder
6. Add to `Content.mgcb` using MGCB Editor
7. Load in code: `_content.Load<Texture2D>("Sprites/player_yourname")`

## Example: Using Custom Sprite Sheets

```csharp
// In MatchScreen.SetGraphicsDevice()

// Load your custom sprite sheets
_playerSpriteBlue = _content.Load<Texture2D>("Sprites/player_custom_blue");
_playerSpriteRed = _content.Load<Texture2D>("Sprites/player_custom_red");

// The animation system automatically handles:
// - Frame selection based on AnimationFrame
// - Direction selection based on SpriteDirection
// - Proper source rectangle extraction
```

## Technical Implementation

### Source Rectangle Calculation
```csharp
int frameIndex = (int)player.AnimationFrame % 4;  // 0-3
int directionRow = player.SpriteDirection % 4;     // 0-3

Rectangle sourceRect = new Rectangle(
    frameIndex * 64,      // X: Column
    directionRow * 64,    // Y: Row
    64,                   // Width
    64                    // Height
);
```

### Rendering
```csharp
// Position and scale
Vector2 drawPos = new Vector2(pos.X, pos.Y);
Vector2 origin = new Vector2(32, 32); // Center of 64x64 sprite
float scale = 2.0f; // Double size: 64x64 -> 128x128

// Rotation for diagonal movement
float rotation = 0f;
if (isDiagonal)
{
    rotation = (float)Math.Atan2(velocity.Y, velocity.X);
}

spriteBatch.Draw(spriteSheet, drawPos, sourceRect, tintColor, 
    rotation, origin, scale, SpriteEffects.None, 0f);
```

## Current Sprite Sheets

### player_blue.png (Home Team)
- Blue jersey with orange/skin colored limbs
- 4-directional walking animation
- 4 frames per direction for smooth movement

### player_red.png (Away Team)
- Red jersey with orange/skin colored limbs
- Same animation layout as blue team
- Provides visual team differentiation

## Advantages

✅ **Smooth Animation**: 4 frames per direction creates fluid walking motion  
✅ **Directional Movement**: Character faces the direction they're moving  
✅ **Team Differentiation**: Separate sprite sheets for home/away teams  
✅ **Scalable**: Easy to add more sprite sheets for different teams  
✅ **Efficient**: Single sprite sheet per team (all players share)  
✅ **Good Detail**: 64x64 pixel frames provide clear graphics  

## Performance Notes

- **Sprite Sheets**: Only 2 textures loaded (home + away)
- **No Per-Player Sprites**: All team members share one sprite sheet
- **Efficient Drawing**: Source rectangle selection is very fast
- **Memory Usage**: Minimal (2 × 256×256 textures)

## Visual Rendering Order

```
Player Rendering:
1. Shadow (8-pixel oval beneath player)
2. Sprite (from sprite sheet with animated frame)
3. Selection indicator (yellow circle if controlled)
4. Name tag (above player with background)
```

## Future Enhancements

1. **More Teams**: Additional sprite sheets for all 8 teams with unique colors
2. **Goalkeeper Sprites**: Separate sprite sheet for goalkeepers
3. **Action Animations**: 
   - Kicking (shooting)
   - Tackling 
   - Jumping/Heading
   - Celebrating goals
4. **Jersey Numbers**: Render numbers on player jerseys
5. **Facial Variations**: Different faces/hairstyles
6. **Equipment**: Boots, shin guards, captain's armband
7. **Status Effects**: 
   - Tired/low stamina (bent over)
   - Injured (limping animation)
   - Yellow/red card indicators
8. **Ball Sprite Sheet**: Rotating soccer ball animation

## Animation Tips

### For Smooth Walking
- Keep frames consistent in size/position
- Use slight bobbing motion (up/down)
- Alternate leg positions
- Frame 0 & 2: Neutral stance
- Frame 1 & 3: Extended stride

### For Different Directions
- **Down**: Show front of character
- **Up**: Show back of character
- **Left/Right**: Use side profile (can mirror if symmetric)

## File Structure

```
Content/
└── Sprites/
    ├── player_blue.png   (Home team - 256×256, 4x4 grid)
    ├── player_red.png    (Away team - 256×256, 4x4 grid)
    └── ball.png          (Ball - 256×256, 8x8 grid, 64 frames)
```
