# Settings Screen User Guide

## Accessing Settings
1. Launch the game
2. From the main menu, select "ΕΠΙΛΟΓΕΣ" (Options)
3. The Settings screen will open

## Screen Layout

```
┌─────────────────────────────────────────────────────────────┐
│                        SETTINGS                             │
├─────────────────────────────────────────────────────────────┤
│                         ▲ MORE                              │ ← Shows when scrolled down
│                                                             │
│  Resolution                           1280x720             │
│  Fullscreen                           OFF                  │
│  VSync                                ON                   │
│  Master Volume                        100%                 │
│  Music Volume                         70%                  │
│  SFX Volume                           80%                  │
│  Mute All                             OFF                  │
│  Difficulty                           Normal               │
│  Match Duration                       3.0 min              │
│  Player Speed                         1.0x                 │
│  Show Minimap                         ON                   │
│                                                             │
│                         ▼ MORE                              │ ← Shows when more below
├─────────────────────────────────────────────────────────────┤
│ Arrow Keys: navigate | Enter: toggle | Left/Right: adjust  │
│ PgUp/PgDn: scroll                                          │
└─────────────────────────────────────────────────────────────┘
```

## Controls

### Navigation
- **↑ (Up Arrow)**: Move selection up
- **↓ (Down Arrow)**: Move selection down
- **Page Up**: Scroll up one full page (11 items)
- **Page Down**: Scroll down one full page (11 items)

### Adjustment
- **← (Left Arrow)**: Decrease value / Previous option
- **→ (Right Arrow)**: Increase value / Next option
- **Enter**: Toggle ON/OFF settings

### Exit
- Navigate to "Back" and press Enter
- Or use Escape key (if available)

## Settings Reference

### Video Settings
1. **Resolution** (←/→ to change)
   - Available: 800x600, 1024x768, 1280x720, 1366x768, 1600x900, 1920x1080
   - Changes apply immediately

2. **Fullscreen** (Enter to toggle)
   - ON: Full screen mode
   - OFF: Windowed mode

3. **VSync** (Enter to toggle)
   - ON: Smooth frame rate, no tearing
   - OFF: Maximum frame rate

### Audio Settings
4. **Master Volume** (←/→ to adjust)
   - Range: 0% to 100% (steps of 10%)
   - Controls overall audio level

5. **Music Volume** (←/→ to adjust)
   - Range: 0% to 100% (steps of 10%)
   - Background music only

6. **SFX Volume** (←/→ to adjust)
   - Range: 0% to 100% (steps of 10%)
   - Sound effects only

7. **Mute All** (Enter to toggle)
   - ON: All audio muted
   - OFF: Audio plays normally

### Gameplay Settings
8. **Difficulty** (←/→ to change)
   - Easy: Slower AI, easier tackles
   - Normal: Balanced gameplay
   - Hard: Faster AI, harder to score

9. **Match Duration** (←/→ to adjust)
   - Range: 1.0 to 10.0 minutes (steps of 0.5)
   - Real-time duration of each match

10. **Player Speed** (←/→ to adjust)
    - Range: 0.5x to 2.0x (steps of 0.1)
    - Movement speed multiplier

### Display Settings
11. **Show Minimap** (Enter to toggle)
    - ON: Tactical minimap visible during match
    - OFF: Minimap hidden

12. **Show Player Names** (Enter to toggle)
    - ON: Player names displayed above sprites
    - OFF: Names hidden

13. **Show Stamina** (Enter to toggle)
    - ON: Stamina bars visible
    - OFF: Stamina hidden

### Camera Settings
14. **Camera Zoom** (←/→ to adjust)
    - Range: 0.5x to 2.0x (steps of 0.1)
    - 0.5x: Zoomed out (see more field)
    - 2.0x: Zoomed in (larger players)

15. **Camera Speed** (←/→ to adjust)
    - Range: 0.05 to 0.5 (steps of 0.05)
    - Higher = faster camera following

### Language
16. **Language** (←/→ to change)
    - EN: English
    - EL: Ελληνικά (Greek)

17. **Back**
    - Press Enter to return to main menu
    - All changes are automatically saved

## Tips
- **All changes are saved automatically** - no need to confirm
- **Settings persist** between game sessions
- **Use Page Up/Down** for quick navigation through long list
- **Scroll indicators** (▲ MORE / ▼ MORE) show when more options exist
- **Selected option** is highlighted in yellow
- **Video settings** apply immediately when changed

## Troubleshooting
- If game doesn't start after resolution change, delete `nopasaran.db` to reset
- Settings stored in `bin/Debug/net9.0/nopasaran.db`
- Default settings will be restored if database is deleted
