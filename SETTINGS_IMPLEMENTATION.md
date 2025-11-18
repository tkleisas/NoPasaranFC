# Settings System Implementation

## Overview
Comprehensive settings persistence system with scrollable UI for all game configuration options.

## Features Implemented

### 1. Enhanced GameSettings Model
Located in: `Models/GameSettings.cs`

All settings categories:
- **Video**: Resolution, Fullscreen, VSync
- **Audio**: Master Volume, Music Volume, SFX Volume, Mute All
- **Gameplay**: Difficulty, Match Duration, Player Speed Multiplier
- **Display**: Show Minimap, Show Player Names, Show Stamina
- **Camera**: Zoom, Speed
- **Localization**: Language (en/el)

### 2. Database Persistence
Located in: `Database/DatabaseManager.cs`

- **Settings Table**: Stores all 17 settings with defaults
- **SaveSettings()**: Automatically saves any setting change
- **LoadSettings()**: Loads settings on game startup
- **Single Row**: Uses ID=1 with INSERT OR REPLACE pattern

### 3. Settings Screen with Scrolling
Located in: `Screens/SettingsScreen.cs`

#### Features:
- **17 Total Settings** displayed in scrollable list
- **11 Visible at Once** - remaining options accessible via scrolling
- **Visual Scroll Indicators**: "▲ MORE" and "▼ MORE" arrows
- **Auto-scroll**: Selection automatically scrolls viewport
- **Real-time Updates**: Changes applied and saved immediately

#### Controls:
- **↑/↓ Arrow Keys**: Navigate through options
- **←/→ Arrow Keys**: Adjust numeric values and cycle options
- **Enter**: Toggle boolean settings
- **Page Up/Down**: Quick scroll by full page
- **Escape**: (inherited) Return to menu

#### Display Format:
```
SETTINGS

▲ MORE                    (if scrolled down)

Resolution              1280x720
Fullscreen              OFF
VSync                   ON
Master Volume           100%
Music Volume            70%
SFX Volume              80%
Mute All                OFF
Difficulty              Normal
Match Duration          3.0 min
Player Speed            1.0x
Show Minimap            ON

▼ MORE                    (if more options below)

Arrow Keys: navigate | Enter: toggle | Left/Right: adjust | PgUp/PgDn: scroll
```

### 4. Settings Integration

#### Game1.cs
- Loads settings on initialization
- Applies video settings (resolution, fullscreen, VSync)
- Settings available globally via `GameSettings.Instance`

#### MenuScreen.cs
- "ΕΠΙΛΟΓΕΣ" (Options) menu item opens SettingsScreen
- Settings accessible from main menu

#### Screen Base Class
- Added `IsFinished` property for screen lifecycle
- ScreenManager automatically pops finished screens

## Setting Details

| Setting | Type | Range/Values | Default | Description |
|---------|------|--------------|---------|-------------|
| Resolution | Choice | 800x600 to 1920x1080 | 1280x720 | Screen resolution |
| Fullscreen | Toggle | ON/OFF | OFF | Fullscreen mode |
| VSync | Toggle | ON/OFF | ON | Vertical sync |
| Master Volume | Float | 0%-100% | 100% | Overall audio level |
| Music Volume | Float | 0%-100% | 70% | Background music |
| SFX Volume | Float | 0%-100% | 80% | Sound effects |
| Mute All | Toggle | ON/OFF | OFF | Mute all audio |
| Difficulty | Choice | Easy/Normal/Hard | Normal | AI difficulty |
| Match Duration | Float | 1.0-10.0 min | 3.0 min | Real-time match length |
| Player Speed | Float | 0.5x-2.0x | 1.0x | Player movement speed |
| Show Minimap | Toggle | ON/OFF | ON | Display tactical minimap |
| Show Player Names | Toggle | ON/OFF | ON | Display player names |
| Show Stamina | Toggle | ON/OFF | ON | Display stamina bars |
| Camera Zoom | Float | 0.5x-2.0x | 0.8x | Camera zoom level |
| Camera Speed | Float | 0.05-0.5 | 0.1 | Camera follow speed |
| Language | Choice | en/el | en | User interface language |

## Technical Notes

### Scrolling Implementation
- Maintains `_scrollOffset` to track top visible item
- `MaxVisibleOptions = 11` defines viewport size
- Auto-adjusts scroll when navigating beyond visible range
- Page Up/Down jumps by full viewport height

### Data Persistence
- All changes saved immediately to database
- No "Apply" or "Save" button needed
- Settings survive game restarts
- Single-row design (ID=1) for simplicity

### UTF-8 Support
- Database uses UTF-8 encoding for multilingual support
- Greek language option fully supported
- Future languages easily added to `_languages` array

## Future Enhancements
- Add audio preview when adjusting volumes
- Add graphics quality presets (Low/Medium/High/Ultra)
- Add control remapping settings
- Add profile/save slot management
- Add factory reset option
- Localize all menu text based on Language setting
