# Localization System

The game supports multiple languages through a centralized localization system.

## Supported Languages

- **English** (`en`)
- **Greek** (`el`)

## How It Works

### 1. Localization Class (`Models/Localization.cs`)

Singleton class that loads and provides translated strings:

```csharp
// Get translated text
string text = Models.Localization.Instance.Get("menu.settings");

// Reload language after settings change
Models.Localization.ReloadLanguage();
```

### 2. String Keys

All UI text uses keys organized by category:

#### Menu Keys
- `menu.standings` - "VIEW STANDINGS" / "ΠΡΟΒΟΛΗ ΑΠΟΤΕΛΕΣΜΑΤΩΝ"
- `menu.nextMatch` - "NEXT MATCH" / "ΕΠΟΜΕΝΟΣ ΑΓΩΝΑΣ"
- `menu.newSeason` - "NEW SEASON" / "ΝΕΟ ΠΡΩΤΑΘΛΗΜΑ"
- `menu.settings` - "SETTINGS" / "ΕΠΙΛΟΓΕΣ"
- `menu.exit` - "EXIT" / "ΕΞΟΔΟΣ"
- `menu.seasonComplete` - "Season Complete!" / "Τέλος Σεζόν!"

#### Standings Keys
- `standings.title` - "STANDINGS" / "ΒΑΘΜΟΛΟΓΙΑ"
- `standings.rank` - "Rank" / "Θέση"
- `standings.team` - "Team" / "Ομάδα"
- `standings.wins` - "W" / "Ν"
- `standings.draws` - "D" / "Ι"
- `standings.losses` - "L" / "Η"
- `standings.goalsFor` - "GF" / "ΓΥ"
- `standings.goalsAgainst` - "GA" / "ΓΚ"
- `standings.points` - "Pts" / "Βαθ"

#### Match Keys
- `match.goal` - "GOAL!" / "ΓΚΟΛ!"
- `match.finalScore` - "FINAL SCORE" / "ΤΕΛΙΚΟ ΣΚΟΡ"
- `match.halfTime` - "HALF TIME" / "ΗΜΙΧΡΟΝΟ"

#### Lineup Keys
- `lineup.title` - "SELECT LINEUP" / "ΕΠΙΛΟΓΗ ΣΥΝΘΕΣΗΣ"
- `lineup.starting` - "STARTING" / "ΒΑΣΙΚΟΙ"
- `lineup.bench` - "BENCH" / "ΠΗΓΑΔΙ"
- `lineup.confirm` - "Press ENTER to confirm" / "Πατήστε ENTER για επιβεβαίωση"
- `lineup.need11` - "Need 11 starting players!" / "Χρειάζεστε 11 βασικούς παίκτες!"
- `lineup.position.gk` - "GK" / "ΤΕ" (Goalkeeper / Τερματοφύλακας)
- `lineup.position.def` - "DEF" / "ΑΜ" (Defender / Αμυντικός)
- `lineup.position.mid` - "MID" / "ΜΕ" (Midfielder / Μέσος)
- `lineup.position.fwd` - "FWD" / "ΕΠ" (Forward / Επιθετικός)

#### Settings Keys
- `settings.title` - "SETTINGS" / "ΕΠΙΛΟΓΕΣ"
- `settings.video` - "=== VIDEO ===" / "=== ΒΙΝΤΕΟ ==="
- `settings.audio` - "=== AUDIO ===" / "=== ΗΧΟΣ ==="
- `settings.gameplay` - "=== GAMEPLAY ===" / "=== GAMEPLAY ==="
- `settings.display` - "=== DISPLAY ===" / "=== ΕΜΦΑΝΙΣΗ ==="
- `settings.camera` - "=== CAMERA ===" / "=== ΚΑΜΕΡΑ ==="
- `settings.language` - "=== LANGUAGE ===" / "=== ΓΛΩΣΣΑ ==="
- `settings.resolution` - "Resolution" / "Ανάλυση"
- `settings.fullscreen` - "Fullscreen" / "Πλήρης Οθόνη"
- `settings.vsync` - "VSync" / "VSync"
- `settings.masterVolume` - "Master Volume" / "Γενική Ένταση"
- `settings.musicVolume` - "Music Volume" / "Ένταση Μουσικής"
- `settings.sfxVolume` - "SFX Volume" / "Ένταση Εφέ"
- `settings.muteAll` - "Mute All" / "Σίγαση Όλων"
- `settings.difficulty` - "Difficulty" / "Δυσκολία"
- `settings.matchDuration` - "Match Duration" / "Διάρκεια Αγώνα"
- `settings.playerSpeed` - "Player Speed" / "Ταχύτητα Παικτών"
- `settings.showMinimap` - "Show Minimap" / "Εμφάνιση Μίνι Χάρτη"
- `settings.showNames` - "Show Player Names" / "Εμφάνιση Ονομάτων"
- `settings.showStamina` - "Show Stamina" / "Εμφάνιση Κόπωσης"
- `settings.cameraZoom` - "Camera Zoom" / "Ζουμ Κάμερας"
- `settings.cameraSpeed` - "Camera Speed" / "Ταχύτητα Κάμερας"
- `settings.languageSelect` - "Language" / "Γλώσσα"
- `settings.difficulty.easy` - "Easy" / "Εύκολο"
- `settings.difficulty.normal` - "Normal" / "Κανονικό"
- `settings.difficulty.hard` - "Hard" / "Δύσκολο"
- `settings.on` - "ON" / "ΝΑΙ"
- `settings.off` - "OFF" / "ΟΧΙ"

## Adding New Languages

### Method 1: Add to Code (Hardcoded)

Edit `Models/Localization.cs` and add a new method like `GetSpanishStrings()`:

```csharp
private Dictionary<string, string> GetSpanishStrings()
{
    return new Dictionary<string, string>
    {
        ["menu.settings"] = "CONFIGURACIÓN",
        ["match.goal"] = "¡GOL!",
        // ... add all keys
    };
}
```

Then update `GetDefaultStrings()`:

```csharp
private Dictionary<string, string> GetDefaultStrings(string languageCode)
{
    return languageCode switch
    {
        "el" => GetGreekStrings(),
        "es" => GetSpanishStrings(),
        _ => GetEnglishStrings()
    };
}
```

### Method 2: JSON Files (Optional)

Create a file `Content/Localization/es.json`:

```json
{
    "menu.settings": "CONFIGURACIÓN",
    "match.goal": "¡GOL!",
    "match.finalScore": "RESULTADO FINAL"
}
```

The system will automatically load JSON files if they exist.

## Changing Language

1. Go to Settings (`ΕΠΙΛΟΓΕΣ` / `SETTINGS`)
2. Scroll to "Language" (`Γλώσσα`)
3. Use Left/Right arrows to change
4. Language changes immediately
5. Setting is saved to database

## Implementation Notes

- Language preference is stored in `GameSettings.Language`
- Persisted in SQLite database
- Singleton pattern ensures consistency
- Falls back to key name if translation missing
- UTF-8 encoding for all text (Greek characters, etc.)
- Font must support all characters (see `FONT_CHARACTER_SUPPORT.md`)

## Usage in Code

```csharp
// Get localization instance
var loc = Models.Localization.Instance;

// Get translated string
string text = loc.Get("menu.settings");

// After changing language in settings
Models.Localization.ReloadLanguage();
```

## Currently Localized

✅ **Main menu** - All 5 options + season complete message
✅ **Match screen** - GOAL, Final Score texts
✅ **Settings menu** - Title, all 16 option labels, ON/OFF, difficulty levels
⚠️ **Standings screen** - Keys defined, not yet applied
⚠️ **Lineup screen** - Keys defined, not yet applied

## To Do

- [ ] Apply localization to Standings screen
- [ ] Apply localization to Lineup screen  
- [ ] Localize "Back" button in settings
- [ ] Add date/time formatting per locale
- [ ] Add number formatting (decimal separator)
- [ ] Consider adding more languages (Spanish, Italian, French, etc.)
