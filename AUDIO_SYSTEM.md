# Audio System Documentation

## Overview
The game includes a complete audio system with music and sound effects managed by the `AudioManager` singleton class.

## Architecture

### AudioManager Class
Location: `Gameplay/AudioManager.cs`

**Features**:
- Singleton pattern for global access
- Separate volume controls for music and SFX
- Graceful handling of missing audio files
- Dictionary-based asset management

### Audio Settings
Stored in `GameSettings`:
- `MusicVolume` (0.0-1.0, default 0.7)
- `SfxVolume` (0.0-1.0, default 0.8)
- `MusicEnabled` (bool, default true)
- `SfxEnabled` (bool, default true)

## Audio Assets

### Required Directory Structure
```
Content/Audio/
├── Music/
│   ├── menu.xnb
│   ├── match.xnb
│   └── victory.xnb
└── SFX/
    ├── menu_select.xnb
    ├── menu_move.xnb
    ├── menu_back.xnb
    ├── whistle_start.xnb
    ├── whistle_end.xnb
    ├── kick_ball.xnb
    ├── tackle.xnb
    ├── goal.xnb
    ├── crowd_cheer.xnb
    └── crowd_aww.xnb
```

### Music Tracks

**menu.wav/ogg** - Menu Background Music
- Plays in: Main menu, standings screen, options
- Loop: Yes
- Suggested: Upbeat, energetic menu music (30-120 seconds loop)

**match.wav/ogg** - Match Background Music
- Plays in: During matches
- Loop: Yes
- Suggested: Dynamic sports music, crowd ambience (60-180 seconds loop)

**victory.wav/ogg** - Victory Music
- Plays in: After winning championship (future feature)
- Loop: No
- Suggested: Triumphant fanfare (15-30 seconds)

### Sound Effects

**Menu SFX**:
- `menu_select.wav` - Confirm selection (Enter key)
- `menu_move.wav` - Navigate menu (Arrow keys)
- `menu_back.wav` - Go back (Escape key)

**Match SFX**:
- `whistle_start.wav` - Match/half starts
- `whistle_end.wav` - Match/half ends
- `kick_ball.wav` - Player kicks ball
- `tackle.wav` - Player collision/tackle
- `goal.wav` - Goal scored
- `crowd_cheer.wav` - Crowd celebrates goal
- `crowd_aww.wav` - Crowd disappointed (future)

## Implementation

### Initialization
```csharp
// In Game1.LoadContent()
AudioManager.Instance.Initialize(Content);
AudioManager.Instance.MusicVolume = GameSettings.Instance.MusicVolume;
AudioManager.Instance.SfxVolume = GameSettings.Instance.SfxVolume;
AudioManager.Instance.PlayMusic("menu_music");
```

### Playing Sound Effects
```csharp
// Simple playback
AudioManager.Instance.PlaySoundEffect("kick_ball");

// With volume multiplier
AudioManager.Instance.PlaySoundEffect("goal", 1.2f); // 20% louder
AudioManager.Instance.PlaySoundEffect("kick_ball", 0.6f); // 40% quieter
```

### Playing Music
```csharp
// Start looping music
AudioManager.Instance.PlayMusic("match_music");

// Non-looping music
AudioManager.Instance.PlayMusic("victory_music", loop: false);

// Stop music
AudioManager.Instance.StopMusic();

// Pause/Resume
AudioManager.Instance.PauseMusic();
AudioManager.Instance.ResumeMusic();
```

### Volume Control
```csharp
// Change volumes
AudioManager.Instance.MusicVolume = 0.5f;
AudioManager.Instance.SfxVolume = 0.8f;

// Update music volume immediately
AudioManager.Instance.UpdateMusicVolume();

// Enable/disable
AudioManager.Instance.MusicEnabled = false;
AudioManager.Instance.SfxEnabled = false;
```

## Audio Triggers

### Menu System
- **Arrow keys**: `menu_move` sound
- **Enter**: `menu_select` sound
- **Escape**: `menu_back` sound
- **Screen load**: Switch to `menu_music`

### Match System
- **Match starts**: `whistle_start` + `match_music`
- **Match ends**: `whistle_end` + back to `menu_music`
- **Ball kicked**: `kick_ball` (volume varies with power)
- **Player tackles**: `tackle`
- **Goal scored**: `goal` + `crowd_cheer` (louder)

## Creating Audio Assets

### Music Format
**Recommended**:
- Format: OGG Vorbis or MP3
- Sample Rate: 44100 Hz
- Bitrate: 128-192 kbps
- Channels: Stereo

**Using MGCB Editor**:
1. Open `Content/Content.mgcb`
2. Right-click → Add → Existing Item
3. Select your music file
4. Set Processor: "Song - MonoGame"
5. Build

### Sound Effects Format
**Recommended**:
- Format: WAV or OGG
- Sample Rate: 44100 Hz
- Bitrate: 16-bit
- Channels: Mono or Stereo
- Length: 0.1-3 seconds

**Using MGCB Editor**:
1. Open `Content/Content.mgcb`
2. Right-click → Add → Existing Item
3. Select your SFX file
4. Set Processor: "Sound Effect - MonoGame"
5. Build

## Free Audio Resources

### Music Sources
- **OpenGameArt.org** - Public domain game music
- **Incompetech.com** - Royalty-free music (Kevin MacLeod)
- **FreePD.com** - Public domain music
- **ccMixter.org** - Creative Commons music

### SFX Sources
- **Freesound.org** - User-uploaded sound effects
- **Zapsplat.com** - Free SFX for games
- **SoundBible.com** - Public domain sounds
- **99Sounds.org** - Free sample packs

### Recommended Search Terms
- Sports crowd ambience
- Referee whistle
- Ball kick/hit
- Menu beep/click
- Victory fanfare
- Soccer/football stadium

## Placeholder Audio

If you don't have audio files yet, the system gracefully handles missing files:
- Missing SFX: Game continues silently
- Missing music: Game continues without music
- No crashes or errors

### Creating Simple Placeholder Sounds

**Using Audacity (Free)**:
1. Generate → Tone (for beeps)
2. Generate → Noise (for crowd)
3. Effects → Echo (for stadium ambience)
4. Export as WAV or OGG

**Quick Placeholder Examples**:
- Menu beep: 440Hz sine wave, 0.1s
- Kick: Pink noise burst, 0.2s with fade
- Whistle: 2000Hz sine, 0.5s with vibrato
- Crowd: Brown noise + reverb, 10s loop

## Performance Notes

### Memory Usage
- Music: Streamed from disk (low memory)
- SFX: Loaded into memory (small files)
- Typical total: 5-20 MB for all assets

### CPU Usage
- Audio mixing: < 1% CPU
- Playback: Handled by XNA/MonoGame
- No performance impact on gameplay

## Troubleshooting

### No Sound Playing
1. Check `GameSettings.Instance.MusicEnabled` / `SfxEnabled`
2. Check volume levels (not 0.0)
3. Verify audio files exist in Content folder
4. Check MGCB build output for errors

### Crackling/Distortion
- Reduce SFX volume multipliers
- Check source audio quality
- Ensure 44100Hz sample rate

### Music Not Looping
- Verify `MediaPlayer.IsRepeating = true`
- Check track has proper loop points
- Some formats don't loop seamlessly

## Future Enhancements

### Planned Features
- Dynamic crowd volume (based on score)
- Commentary system
- Team-specific chants
- Halftime music
- Injury sounds
- Weather effects (rain, wind)
- National anthems

### Audio Settings Screen
Add to `SettingsScreen.cs`:
- Master volume slider
- Music volume slider
- SFX volume slider
- Mute all toggle
- Test sound buttons

## Code Examples

### Adding New Sound Effect
```csharp
// 1. Add to AudioManager.LoadAudio()
TryLoadSoundEffect("new_sound", "Audio/SFX/new_sound");

// 2. Play it anywhere
AudioManager.Instance.PlaySoundEffect("new_sound");
```

### Adding New Music
```csharp
// 1. Add to AudioManager.LoadAudio()
TryLoadSong("new_music", "Audio/Music/new_music");

// 2. Play it
AudioManager.Instance.PlayMusic("new_music");
```

### Conditional Audio
```csharp
// Play different music based on game state
if (championship.IsComplete())
{
    AudioManager.Instance.PlayMusic("victory_music", loop: false);
}
else
{
    AudioManager.Instance.PlayMusic("menu_music");
}
```

## Summary

The audio system is fully implemented and integrated:
- ✅ AudioManager singleton
- ✅ Music playback (menu, match)
- ✅ Sound effects (menu, gameplay)
- ✅ Volume controls
- ✅ Graceful missing file handling
- ✅ Settings persistence
- ⏳ Audio assets (need to be added)

**Next Step**: Create or download audio files and add them to the Content project!
