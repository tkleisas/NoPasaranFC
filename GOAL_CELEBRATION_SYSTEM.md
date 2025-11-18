# Goal Celebration System

## Overview
When a goal is scored, an animated celebration displays with balls flying in from all directions to form **any text** in the center of the screen. The system renders the text as a bitmap and places balls at each visible pixel for an accurate, dynamic representation.

## Architecture

### GoalCelebration Class
Located in: `Gameplay/GoalCelebration.cs`

#### Key Features
- **Dynamic Particle System**: Variable number of ball particles based on text complexity
- **Bitmap-Based Rendering**: Converts any text to monochrome bitmap
- **Pixel-Perfect Formation**: Balls placed at exact pixel positions from rendered text
- **Font-Accurate**: Uses actual SpriteFont glyphs - works with any character
- **Ease-Out Animation**: Smooth, dynamic movement with position-based staggered delays
- **Auto-Duration**: 2.5 seconds, then automatically ends
- **Universal Support**: Works with Greek, symbols, emojis - any Unicode character

### Integration with MatchEngine

#### New Match State
```csharp
public enum MatchState { 
    CameraInit, 
    Countdown, 
    Playing, 
    GoalCelebration,  // NEW
    HalfTime, 
    Ended 
}
```

#### Triggering Celebration
```csharp
private void TriggerGoalCelebration()
{
    CurrentState = MatchState.GoalCelebration;
    
    // Start celebration with custom text
    if (_font != null && _graphicsDevice != null)
    {
        GoalCelebration.Start("GOAL!", _font, _graphicsDevice);
        // Can use ANY text: "ΓΚΟΛ!", "⚽ GOAL! ⚽", etc.
    }
    
    BallVelocity = Vector2.Zero;
    BallVerticalVelocity = 0f;
}
```

#### Update Loop Handling
```csharp
if (CurrentState == MatchState.GoalCelebration)
{
    GoalCelebration.Update(deltaTime);
    
    if (!GoalCelebration.IsActive)
    {
        CurrentState = MatchState.Playing;
        ResetAfterGoal();
    }
    
    Camera.Follow(BallPosition, deltaTime);
    return; // Don't update gameplay during celebration
}
```

## Bitmap Rendering System

### How It Works

1. **Text Measurement**: Measures text size using `SpriteFont.MeasureString()`
2. **Off-Screen Rendering**: Creates temporary `RenderTarget2D` with text dimensions
3. **Font Rendering**: Draws text to render target using `SpriteBatch.DrawString()`
4. **Pixel Extraction**: Reads pixel data back from render target
5. **Sampling**: Samples pixels at 8px intervals (ball spacing)
6. **Visibility Check**: Only uses pixels where `alpha > 128`
7. **Position Calculation**: Converts pixel coordinates to centered positions

### Algorithm

```csharp
private List<Vector2> RenderTextToBitmap(string text, SpriteFont font, GraphicsDevice graphicsDevice)
{
    // 1. Measure text
    Vector2 textSize = font.MeasureString(text);
    int width = (int)Math.Ceiling(textSize.X);
    int height = (int)Math.Ceiling(textSize.Y);
    
    // 2. Create render target
    using (RenderTarget2D renderTarget = new RenderTarget2D(
        graphicsDevice, width, height, false, SurfaceFormat.Color, DepthFormat.None))
    {
        // 3. Render text
        graphicsDevice.SetRenderTarget(renderTarget);
        graphicsDevice.Clear(Color.Transparent);
        SpriteBatch spriteBatch = new SpriteBatch(graphicsDevice);
        spriteBatch.Begin();
        spriteBatch.DrawString(font, text, Vector2.Zero, Color.White);
        spriteBatch.End();
        graphicsDevice.SetRenderTarget(null);
        
        // 4. Extract pixels
        Color[] pixels = new Color[width * height];
        renderTarget.GetData(pixels);
        
        // 5. Sample and create positions
        int ballSpacing = 16; // Increased for better visibility
        float displayScale = 3.0f; // Scale up for screen display
        
        for (int y = 0; y < height; y += ballSpacing)
        {
            for (int x = 0; x < width; x += ballSpacing)
            {
                if (pixels[y * width + x].A > 128) // Visible pixel
                {
                    // Scale up positions for screen visibility
                    float posX = (x - width/2f) * displayScale;
                    float posY = (y - height/2f) * displayScale;
                    positions.Add(new Vector2(posX, posY));
                }
            }
        }
    }
    return positions;
}
```

### Ball Spacing & Scaling
- **Render Scale**: 4.0x (text rendered 4x larger before sampling)
- **Ball Spacing**: 24 pixels (doubled for clarity and readability)
- **Ball Size**: 32px base (grows from 0.5x to 1.5x = 16-48px)
- **No Anti-Aliasing**: Point sampling for crisp edges
- **Adjustable**: Change `ballSpacing` and `renderScale` constants
- **Trade-off**: Smaller spacing = more balls = more detail but denser appearance

### Examples

**Text: "GOAL!"**
- Font size: 24pt
- Rendered at: 96pt (4x scale)
- Bitmap dimensions: ~480×120 pixels
- Ball count: ~50-75 balls (24px spacing)
- Clear, readable letter formation

**Text: "ΓΚΟΛ!"** (Greek)
- Font size: 24pt
- Rendered at: 96pt (4x scale)
- Bitmap dimensions: ~400×120 pixels
- Ball count: ~40-60 balls (24px spacing)
- Clear, readable letter formation

**Text: "⚽"** (Single emoji)
- Font size: 48pt (if available)
- Rendered at: 192pt (4x scale)
- Bitmap dimensions: ~192×192 pixels
- Ball count: ~64 balls (24px spacing)
- Clear symbol shape

## Animation Details

### Ball Particle Structure
```csharp
struct BallParticle
{
    Vector2 Position;        // Current position
    Vector2 TargetPosition;  // Final position in letter
    float Progress;          // 0.0 to 1.0 animation progress
    float Delay;             // Stagger start time
}
```

### Letter Formations

#### G - Circle with Opening
- 8 balls arranged in 270° arc
- Opening on right side (270° / 8 = 33.75° per ball)
- Radius: 40px

#### O - Complete Circle
- 8 balls arranged in 360° circle
- Evenly spaced (45° per ball)
- Radius: 40px

#### A - Triangle with Crossbar
- 3 balls: Left leg (diagonal up-left)
- 3 balls: Right leg (diagonal up-right)
- 2 balls: Horizontal crossbar in middle

#### L - Vertical Line with Base
- 6 balls: Vertical line
- 2 balls: Horizontal base at bottom

#### ! - Exclamation Mark
- 3 balls: Vertical line
- 1 ball: Dot below line

### Animation Timing
```
Time 0.0s: Balls start off-screen (800-1200px away)
Time 0.0s-0.5s: Balls fly toward letter positions
Time 0.5s-2.2s: "GOAL!" text displayed
Time 2.2s-2.5s: Fade out
Time 2.5s: Celebration ends, return to kickoff
```

### Easing Function
Ease-out cubic for smooth deceleration:
```csharp
float easeT = 1f - MathF.Pow(1f - t, 3f);
Vector2 position = Vector2.Lerp(start, target, easeT);
```

## Rendering

### MatchScreen Integration
```csharp
if (_matchEngine.CurrentState == MatchEngine.MatchState.GoalCelebration)
{
    DrawGoalCelebration(spriteBatch, font);
}
```

### Rendering Order
1. **Ball Particles**: Drawn with scale and alpha based on progress
2. **"GOAL!" Text**: Large yellow text with black shadow
3. **Background Overlay**: Semi-transparent (70% opacity)

### Visual Effects
- **Scale**: Balls grow from 0.5× to 2.0× during animation
- **Alpha**: Fade in during first 33% of animation
- **Rotation**: None (balls use frame 0 of sprite sheet)
- **Position**: Lerp with ease-out from random edge to letter position

## Goal Net Improvements

### Mesh Pattern
```
┌─────────────────┐
│ ║ ║ ║ ║ ║ ║ ║ ║ │  Vertical lines (every 20px)
│═════════════════│  Horizontal lines (every 20px)
│ ╲ ╲ ╲ ╲ ╲ ╲ ╲ ╲│  Diagonal lines (every 40px)
│  ╲ ╲ ╲ ╲ ╲ ╲ ╲ │
│───────────────  │  Goal line
└─────────────────┘
```

### Drawing Layers (Back to Front)
1. **Back Panel**: Solid dark gray (100, 100, 100, 100)
2. **Vertical Mesh Lines**: Every 20px, gray (180, 180, 180, 200)
3. **Horizontal Mesh Lines**: Every 20px, gray (180, 180, 180, 200)
4. **Diagonal Mesh Lines**: Every 40px diagonal, gray (180, 180, 180, 200)
5. **Goal Posts**: White posts (8px thick) at corners and crossbar

## Goal Detection Logic

### Requirements for Valid Goal
```csharp
// Ball must:
1. Cross goal line (X < leftGoalLine OR X > rightGoalLine)
2. Be within goal width (Y >= goalTop AND Y <= goalBottom)
3. Be below crossbar height (BallHeight <= 200px)
```

### Goal Line Positions
- **Left Goal Line**: `StadiumMargin` (400px from left edge)
- **Right Goal Line**: `StadiumMargin + FieldWidth` (7200px)
- **Goal Width**: 400px centered on field midline
- **Goal Height**: 200px above ground

### Over Crossbar Detection
```csharp
if ((BallPosition.X < leftGoalLine || BallPosition.X > rightGoalLine) 
    && BallHeight > GoalPostHeight)
{
    // Ball went over crossbar - it's out
    ResetAfterOut();
}
```

## Performance Considerations

### Particle Count
- Total: 40 particles (4 letters × 8 balls + 1 symbol × 4 balls)
- Each particle: 2 vectors (64 bytes) + 2 floats (8 bytes) = 72 bytes
- Total memory: ~3KB

### Rendering Cost
- 40 sprite draws per frame during celebration
- 1 text draw (with shadow = 2 draws)
- Total: ~42 draw calls for 2.5 seconds
- Negligible impact on 60 FPS target

### Animation Updates
- Simple lerp calculation per particle
- No physics simulation
- O(n) complexity where n = particle count (40)
- ~1-2 microseconds per frame

## Future Enhancements

### Possible Additions
1. **Sound Effects**:
   - Crowd roar when goal is scored
   - Whistle sound
   - Stadium announcer voice

2. **Camera Shake**:
   - Slight shake when goal is scored
   - Zoom animation during celebration

3. **Player Celebrations**:
   - Scoring player runs and jumps
   - Teammates run to scorer
   - Different celebration animations per player

4. **Confetti Effect**:
   - Particle confetti falling from top of screen
   - Team colors

5. **Replay System**:
   - Show goal replay before celebration
   - Multiple camera angles
   - Slow motion

6. **Score Display**:
   - Large score update animation
   - Flash new score in corner

7. **Customization**:
   - Different celebration styles per difficulty
   - Team-specific celebrations
   - Special celebrations for important goals

## Testing

### Manual Testing Checklist
- [ ] Goal scored from left side triggers celebration
- [ ] Goal scored from right side triggers celebration
- [ ] Ball over crossbar does NOT trigger celebration
- [ ] Ball outside goal width does NOT trigger celebration
- [ ] Celebration lasts exactly 2.5 seconds
- [ ] Match resumes after celebration
- [ ] Score updates correctly
- [ ] Players reset to kickoff positions

### Edge Cases
- Ball barely inside/outside goal line
- Ball at exact crossbar height (200px)
- Multiple rapid goals (shouldn't double-trigger)
- Goal scored at match end (should still celebrate)

## Code Locations

### Modified Files
- `Gameplay/MatchEngine.cs`:
  - Added `GoalCelebration` property
  - Added `GoalCelebration` state to enum
  - Modified `CheckGoal()` method
  - Added `TriggerGoalCelebration()` method
  - Updated `Update()` to handle celebration state

- `Screens/MatchScreen.cs`:
  - Modified `DrawGoals()` for mesh net
  - Added `DrawGoalStructure()` method
  - Added `DrawGoalNet()` method
  - Added `DrawLine()` helper method
  - Added `DrawGoalCelebration()` method
  - Updated `Draw()` to render celebration

### New Files
- `Gameplay/GoalCelebration.cs`:
  - Complete celebration system
  - Particle animation
  - Letter formation logic
  - Rendering methods

## Dependencies
- MonoGame Framework
- Existing ball sprite sheet (`Sprites/ball.png`)
- Font for "GOAL!" text (`Font.spritefont`)
