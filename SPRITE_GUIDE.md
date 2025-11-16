# Sprite Assets Guide

## Current Implementation

The game currently uses **placeholder colored rectangles** for players and ball. The infrastructure is in place to support proper sprite sheets with animations.

## Sprite Specifications

### Player Sprites

To add player sprites, create sprite sheets with the following specifications:

**File format:** PNG with transparency  
**Sprite size:** 64x64 pixels per frame  
**Layout:** 4 rows (directions) x 4 columns (animation frames) = 256x256 total
- Row 0: Walking down (frames 0-3)
- Row 1: Walking up (frames 0-3)
- Row 2: Walking left (frames 0-3)
- Row 3: Walking right (frames 0-3)

**Total sheet size:** 256 x 256 pixels (4 frames × 64px wide, 4 directions × 64px tall)

**Current files:**
- `player_blue.png` - Home team (256x256 with 64x64 frames)
- `player_red.png` - Away team (256x256 with 64x64 frames)

**Required files:**
- `player_blue.png` - Home team sprites
- `player_red.png` - Away team sprites  
- `player_yellow.png` - Controlled player highlight (or use color tinting)

### Ball Sprite

**File format:** PNG with transparency  
**Size:** 32x32 pixels
**Animation:** Optional - can be a single frame or rotating animation

### Current Sizes

The game uses larger sprites for better detail:
- **Players:** 64x64 pixels (controlled player renders at 68x68)
- **Ball:** 32x32 pixels
- **Shadows:** Automatically rendered underneath sprites

### Adding Sprites to Project

1. Place sprite files in `Content/Sprites/` folder
2. Add to `Content.mgcb` using MGCB Editor:
   ```
   dotnet mgcb-editor Content/Content.mgcb
   ```
3. Set Build Action to "Build"

4. Load sprites in `MatchScreen.SetGraphicsDevice()`:
   ```csharp
   _playerSprite = content.Load<Texture2D>("Sprites/player_blue");
   _ballSprite = content.Load<Texture2D>("Sprites/ball");
   ```

5. Update `DrawPlayer()` method to use sprite sheet frames:
   ```csharp
   int frameX = (int)player.AnimationFrame * 64; // 64px per frame
   int frameY = player.SpriteDirection * 64; // 64px per row
   Rectangle sourceRect = new Rectangle(frameX, frameY, 64, 64);
   spriteBatch.Draw(_playerSprite, destRect, sourceRect, playerColor);
   ```

## Visual Scale Reference

With 64x64 player sprites:
- Player appears as a clear, detailed character
- Easy to distinguish team colors and animations
- Good balance between detail and performance
- Ball at 32x32 is proportionally sized to players
- Shadows add depth perception

## Example Sprite Layout

```
Player Sprite Sheet (256x256):
+-------+-------+-------+-------+
| Down0 | Down1 | Down2 | Down3 |  Row 0 (Walking Down)
+-------+-------+-------+-------+
|  Up0  |  Up1  |  Up2  |  Up3  |  Row 1 (Walking Up)
+-------+-------+-------+-------+
| Left0 | Left1 | Left2 | Left3 |  Row 2 (Walking Left)
+-------+-------+-------+-------+
|Right0 |Right1 |Right2 |Right3 |  Row 3 (Walking Right)
+-------+-------+-------+-------+
Each cell: 64x64 pixels
```

## Animation System

The animation system is already implemented:
- `Player.AnimationFrame` - Current frame (0-3)
- `Player.SpriteDirection` - Direction (0=down, 1=up, 2=left, 3=right)
- `Player.AnimationSpeed` - Speed of animation (0.15 default)

Animation automatically updates based on player movement in `UpdatePlayerAnimations()`.

## Creating Sprites

You can create sprites using:
- **Aseprite** - Pixel art editor with animation support
- **Piskel** - Free online pixel art tool
- **GIMP** - Free image editor
- **GraphicsGale** - Pixel animation tool

### Example Simple Sprite

For quick testing, you can create a simple 64x64 colored circle or character in any image editor and save as PNG.

**Quick Test Sprite:**
1. Create new image: 64x64 pixels
2. Draw a simple character (stick figure, circle with features, etc.)
3. Add transparency around the character
4. Save as PNG

**For Ball:**
1. Create new image: 32x32 pixels  
2. Draw a soccer ball pattern (or simple circle)
3. Add transparency
4. Save as PNG

## Placeholder System

Until proper sprites are added, the game uses:
- Colored rectangles (Blue=Home, Red=Away, Yellow=Controlled)
- Size: 64x64 for players, 32x32 for ball
- Simple shadows underneath
- Size variation for controlled player (68x68 vs 64x64)

The sprite rendering code is ready - just add the PNG files!
