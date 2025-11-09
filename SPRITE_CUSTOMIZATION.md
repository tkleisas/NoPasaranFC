# Player Sprite Customization Guide

## Overview
Each player can now have their own unique sprite, allowing for better visual differentiation and game aesthetics.

## Features Added

### 1. **Per-Player Sprites**
- Each `Player` object has a `SpriteFileName` property
- Each `Player` object has a `SpriteColor` property for tinting
- Players with the same position (Goalkeeper, Defender, etc.) get different base colors by default

### 2. **Automatic Position-Based Sprites**
By default, players get different colored sprites based on their position:
- **Goalkeepers**: Gold/Yellow (255, 200, 0)
- **Defenders**: Steel Blue (70, 130, 180)
- **Midfielders**: Medium Sea Green (60, 179, 113)
- **Forwards**: Crimson (220, 20, 60)

### 3. **Team Color Tinting**
- Home team players have a Blue tint
- Away team players have a Red tint
- The final sprite color is a blend of:
  - Player's custom sprite color
  - Team color (Blue/Red)
  - Special highlight (Yellow) if controlled

### 4. **Visual Indicators**
- **Controlled Player**: Yellow tint + yellow circle outline
- **Knocked Down**: Semi-transparent gray
- **Shadow**: All players cast a small shadow

## How to Add Custom Sprites

### Option 1: Using Custom Images (Future Enhancement)
1. Create 64x64 PNG images for each player
2. Place them in `Content/Sprites/Players/` folder
3. Set player's `SpriteFileName` to match: `player.SpriteFileName = "myplayer.png"`
4. The game will load the image automatically

### Option 2: Using Code (Current Method)
The game currently generates sprites programmatically. Each player gets a unique sprite based on their position.

## Example: Customizing a Player

```csharp
// Create a player
var player = new Player("STRIKER_01", PlayerPosition.Forward);

// Customize sprite
player.SpriteFileName = "forward_special.png"; // Custom sprite file
player.SpriteColor = new Color(255, 100, 100); // Custom tint (pinkish red)

// The player will now use this custom sprite and color
```

## Sprite Format

### Current Implementation
- Size: **64x64 pixels**
- Format: Programmatically generated texture
- Features:
  - Circle-based body
  - Smaller head (skin tone)
  - Black outline
  - Position-based coloring

### Future Implementation (Image Files)
- Size: **64x64 pixels** recommended
- Format: **PNG** with transparency
- Naming: `player_<name>.png` or `goalkeeper_<team>.png`
- Location: `Content/Sprites/Players/`

## Visual Hierarchy

```
Player Rendering:
1. Shadow (beneath player)
2. Sprite (with team color tint)
3. Selection indicator (yellow circle if controlled)
4. Name tag (above player)
```

## Advantages

✅ **Better Visual Differentiation**: Easy to identify player positions  
✅ **Team Recognition**: Blue vs Red tints for home/away  
✅ **Position Recognition**: Different colors for GK, DEF, MID, FWD  
✅ **Scalable**: Can add custom images per player later  
✅ **Flexibility**: Each player can have unique appearance  
✅ **Performance**: Sprite cache prevents duplicate loading  

## Technical Details

### Sprite Cache
- Sprites are loaded once and cached in `_playerSpriteCache`
- Shared sprites between players (e.g., all forwards) use the same texture
- Reduces memory usage and improves performance

### Color Blending
```csharp
finalColor = (teamColor + playerSpriteColor) / 2
```

This creates a balanced mix of team identity and individual player characteristics.

## Future Enhancements

1. **Animation Support**: Different frames for running, kicking, tackling
2. **Direction Sprites**: Different sprites for facing up/down/left/right
3. **Custom Jersey Numbers**: Render numbers on sprites
4. **Facial Features**: Add variety to player heads
5. **Equipment**: Different boots, gloves for goalkeepers
6. **Injury Indicators**: Visual feedback for tired/injured players

## Notes

- Default sprite is used if custom sprite fails to load
- All sprites must be 64x64 for consistent rendering
- Transparency is supported for non-rectangular sprites
- Knocked down players are rendered as flattened (64x20) rectangles
