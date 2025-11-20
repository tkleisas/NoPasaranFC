# Lineup Selection Screen Documentation

## Overview
Before each match, players can now select their starting XI from their full roster (11+ players). The lineup screen provides an interactive interface for managing the team composition.

## Features

### Player List (Left Side)
- **All squad players displayed** sorted by shirt number (scrollable)
- **Scrollable list** with PageUp/PageDown support
- Shows for each player:
  - Shirt number (#1-22)
  - Player name (truncated to 15 chars)
  - Position (GK/DEF/MID/FWD)
  - Status ([STARTER] or [BENCH])
- **Visual selection**: Yellow highlight for selected player
- **Background colors**: Green tint for starting XI, gray for bench

### Formation Preview (Right Side)
- **Visual formation display** showing player positions
- **Interactive preview**:
  - Blue circles represent players
  - Shirt numbers displayed on each position
  - Formation notation (e.g., "4-4-2")
  - Standard formation layout (GK-DEF-MID-FWD)
- **Real-time updates** as you toggle players

### Status Indicators
- **Starter Count**: "ΒΑΣΙΚΟΙ: X/11" (Greek for "STARTERS: X/11")
  - Green when exactly 11 selected
  - Yellow when less than 11
  - Red when more than 11
- **Scroll indicators**: ▲/▼ arrows when more players available

## Controls

| Key | Action |
|-----|--------|
| ↑ / ↓ | Navigate through player list |
| Page Up / Page Down | Quick scroll (15 players) |
| Space | Toggle player between starter/bench |
| Enter | Confirm lineup and start match (requires exactly 11 starters) |
| Escape | Cancel and return to menu |

## Validation Rules

1. **Must have exactly 11 starters** to proceed to match
2. **Cannot remove starter if already at 11** (must add another first)
3. **Cannot add starter if already at 11** (must remove one first)
4. **Changes are saved** to database when confirmed

## Workflow

1. **Select "ΕΠΟΜΕΝΟΣ ΑΓΩΝΑΣ" (Next Match)** from main menu
2. **Lineup screen appears** automatically for player's team
3. **Navigate and adjust** starting lineup as desired
4. **Press Enter** when satisfied (must have 11 starters)
5. **Match begins** with selected lineup

## Technical Details

### Position Ordering
Players are sorted for formation preview:
1. Goalkeeper (1)
2. Defenders (4)
3. Midfielders (4)
4. Forwards (2)

### Formation Positions
Default 4-4-2 formation with fixed positions:
- 1 GK at back
- 4 DEF across defensive line
- 4 MID across midfield
- 2 FWD up front

### Database Integration
- **Reads**: All players from team roster
- **Writes**: IsStarting status for each player
- **Persists**: Lineup choices across game sessions

## UI Layout

```
┌─────────────────────────────────────────────────────────┐
│         ΕΠΙΛΟΓΗ ΣΥΝΘΕΣΗΣ - NO PASARAN!                  │
│                ΒΑΣΙΚΟΙ: 11/11                            │
├──────────────────────────┬──────────────────────────────┤
│  PLAYER LIST             │  FORMATION PREVIEW           │
│                          │                              │
│  #  NAME      POS STATUS │      FORMATION PREVIEW       │
│  1  Kostas    GK [START] │                              │
│  2  Giorgos   DEF[START] │         ●10  ●11            │
│  3  Dimitris  DEF[START] │      ●6  ●7  ●8  ●9         │
│  4  Nikos     DEF[START] │      ●2  ●3  ●4  ●5         │
│  5  Michalis  DEF[START] │            ●1                │
│  ...                     │                              │
│  25 Fotis     DEF[BENCH] │         4-4-2                │
│                          │                              │
└──────────────────────────┴──────────────────────────────┘
     ↑↓: Navigate | SPACE: Toggle | ENTER: Confirm
```

## Future Enhancements

Potential improvements:
- [ ] Formation templates (4-3-3, 3-5-2, etc.)
- [ ] Player stats display on selection
- [ ] Automatic formation suggestions
- [ ] Player fitness/stamina indicators
- [ ] Recent form indicators
- [ ] Tactical instructions
- [ ] Save multiple lineups as presets
- [ ] Drag-and-drop positioning
- [ ] Opposition team preview

## Notes

- **Greek text support**: UI uses Greek characters for labels
- **Font compatibility**: All text uses standard Font.spritefont
- **Sound effects**: Menu sounds play for navigation and selection
- **Screen stack**: Lineup screen is pushed onto screen manager stack
- **Auto-save**: Lineup is saved when Enter is pressed, not on toggle
