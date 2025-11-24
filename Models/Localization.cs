using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace NoPasaranFC.Models
{
    public class Localization
    {
        private static Localization _instance;
        private Dictionary<string, string> _strings;
        private string _currentLanguage;
        
        public static Localization Instance
        {
            get
            {
                if (_instance == null)
                {
                    _instance = new Localization();
                    _instance.LoadLanguage(GameSettings.Instance.Language);
                }
                return _instance;
            }
        }
        
        public void LoadLanguage(string languageCode)
        {
            _currentLanguage = languageCode;
            _strings = GetDefaultStrings(languageCode);
            
            // Try to load from JSON file (optional)
            string filePath = $"Content/Localization/{languageCode}.json";
            if (File.Exists(filePath))
            {
                try
                {
                    string json = File.ReadAllText(filePath);
                    var loaded = JsonSerializer.Deserialize<Dictionary<string, string>>(json);
                    if (loaded != null)
                    {
                        _strings = loaded;
                    }
                }
                catch (Exception ex)
                {
                    System.Diagnostics.Debug.WriteLine($"Failed to load localization file: {ex.Message}");
                }
            }
        }
        
        public string Get(string key)
        {
            if (_strings != null && _strings.ContainsKey(key))
            {
                return _strings[key];
            }
            return key; // Return key if translation not found
        }
        
        private Dictionary<string, string> GetDefaultStrings(string languageCode)
        {
            if (languageCode == "el")
            {
                return GetGreekStrings();
            }
            else
            {
                return GetEnglishStrings();
            }
        }
        
        private Dictionary<string, string> GetEnglishStrings()
        {
            return new Dictionary<string, string>
            {
                // Menu
                ["menu.standings"] = "VIEW STANDINGS",
                ["menu.nextMatch"] = "NEXT MATCH",
                ["menu.newSeason"] = "NEW SEASON",
                ["menu.settings"] = "SETTINGS",
                ["menu.exit"] = "EXIT",
                ["menu.seasonComplete"] = "Season Complete!",
                
                // Standings
                ["standings.title"] = "STANDINGS",
                ["standings.rank"] = "Rank",
                ["standings.team"] = "Team",
                ["standings.wins"] = "W",
                ["standings.draws"] = "D",
                ["standings.losses"] = "L",
                ["standings.goalsFor"] = "GF",
                ["standings.goalsAgainst"] = "GA",
                ["standings.points"] = "Pts",
                
                // Match
                ["match.goal"] = "GOAL!",
                ["match.finalScore"] = "FINAL SCORE",
                ["match.halfTime"] = "HALF TIME",
                
                // Round Results
                ["round_results_title"] = "MATCHWEEK {0} RESULTS",
                ["round_results_continue"] = "Press ENTER or SPACE to continue",
                ["round_results_champion"] = "Team {0} is the Champion!",
                
                // Lineup
                ["lineup.title"] = "SELECT LINEUP",
                ["lineup.starting"] = "STARTING",
                ["lineup.bench"] = "BENCH",
                ["lineup.confirm"] = "Press ENTER to confirm",
                ["lineup.need11"] = "Need 11 starting players!",
                ["lineup.position.gk"] = "GK",
                ["lineup.position.def"] = "DEF",
                ["lineup.position.mid"] = "MID",
                ["lineup.position.fwd"] = "FWD",
                
                // Settings
                ["settings.title"] = "SETTINGS",
                ["settings.video"] = "=== VIDEO ===",
                ["settings.audio"] = "=== AUDIO ===",
                ["settings.gameplay"] = "=== GAMEPLAY ===",
                ["settings.display"] = "=== DISPLAY ===",
                ["settings.camera"] = "=== CAMERA ===",
                ["settings.language"] = "=== LANGUAGE ===",
                
                ["settings.resolution"] = "Resolution",
                ["settings.fullscreen"] = "Fullscreen",
                ["settings.vsync"] = "VSync",
                ["settings.masterVolume"] = "Master Volume",
                ["settings.musicVolume"] = "Music Volume",
                ["settings.sfxVolume"] = "SFX Volume",
                ["settings.muteAll"] = "Mute All",
                ["settings.difficulty"] = "Difficulty",
                ["settings.matchDuration"] = "Match Duration",
                ["settings.playerSpeed"] = "Player Speed",
                ["settings.showMinimap"] = "Show Minimap",
                ["settings.showNames"] = "Show Player Names",
                ["settings.showStamina"] = "Show Stamina",
                ["settings.cameraZoom"] = "Camera Zoom",
                ["settings.cameraSpeed"] = "Camera Speed",
                ["settings.languageSelect"] = "Language",
                
                ["settings.difficulty.easy"] = "Easy",
                ["settings.difficulty.normal"] = "Normal",
                ["settings.difficulty.hard"] = "Hard",
                
                ["settings.on"] = "ON",
                ["settings.off"] = "OFF",
            };
        }
        
        private Dictionary<string, string> GetGreekStrings()
        {
            return new Dictionary<string, string>
            {
                // Menu
                ["menu.standings"] = "ΠΡΟΒΟΛΗ ΑΠΟΤΕΛΕΣΜΑΤΩΝ",
                ["menu.nextMatch"] = "ΕΠΟΜΕΝΟΣ ΑΓΩΝΑΣ",
                ["menu.newSeason"] = "ΝΕΟ ΠΡΩΤΑΘΛΗΜΑ",
                ["menu.settings"] = "ΕΠΙΛΟΓΕΣ",
                ["menu.exit"] = "ΕΞΟΔΟΣ",
                ["menu.seasonComplete"] = "Τέλος Σεζόν!",
                
                // Standings
                ["standings.title"] = "ΒΑΘΜΟΛΟΓΙΑ",
                ["standings.rank"] = "Θέση",
                ["standings.team"] = "Ομάδα",
                ["standings.wins"] = "Ν",
                ["standings.draws"] = "Ι",
                ["standings.losses"] = "Η",
                ["standings.goalsFor"] = "ΓΥ",
                ["standings.goalsAgainst"] = "ΓΚ",
                ["standings.points"] = "Βαθ",
                
                // Match
                ["match.goal"] = "ΓΚΟΛ!",
                ["match.finalScore"] = "ΤΕΛΙΚΟ ΣΚΟΡ",
                ["match.halfTime"] = "ΗΜΙΧΡΟΝΟ",
                
                // Lineup
                ["lineup.title"] = "ΕΠΙΛΟΓΗ ΣΥΝΘΕΣΗΣ",
                ["lineup.starting"] = "ΒΑΣΙΚΟΙ",
                ["lineup.bench"] = "ΠΗΓΑΔΙ",
                ["lineup.confirm"] = "Πατήστε ENTER για επιβεβαίωση",
                ["lineup.need11"] = "Χρειάζεστε 11 βασικούς παίκτες!",
                ["lineup.position.gk"] = "ΤΕ",
                ["lineup.position.def"] = "ΑΜ",
                ["lineup.position.mid"] = "ΜΕ",
                ["lineup.position.fwd"] = "ΕΠ",
                
                // Round Results
                ["round_results_title"] = "ΑΠΟΤΕΛΕΣΜΑΤΑ {0}ΗΣ ΑΓΩΝΙΣΤΙΚΗΣ",
                ["round_results_continue"] = "Πατήστε ENTER ή SPACE για συνέχεια",
                ["round_results_champion"] = "Η ομάδα {0} είναι Πρωταθλήτρια!",
                // Settings
                ["settings.title"] = "ΕΠΙΛΟΓΕΣ",
                ["settings.video"] = "=== ΒΙΝΤΕΟ ===",
                ["settings.audio"] = "=== ΗΧΟΣ ===",
                ["settings.gameplay"] = "=== GAMEPLAY ===",
                ["settings.display"] = "=== ΕΜΦΑΝΙΣΗ ===",
                ["settings.camera"] = "=== ΚΑΜΕΡΑ ===",
                ["settings.language"] = "=== ΓΛΩΣΣΑ ===",
                
                ["settings.resolution"] = "Ανάλυση",
                ["settings.fullscreen"] = "Πλήρης Οθόνη",
                ["settings.vsync"] = "VSync",
                ["settings.masterVolume"] = "Γενική Ένταση",
                ["settings.musicVolume"] = "Ένταση Μουσικής",
                ["settings.sfxVolume"] = "Ένταση Εφέ",
                ["settings.muteAll"] = "Σίγαση Όλων",
                ["settings.difficulty"] = "Δυσκολία",
                ["settings.matchDuration"] = "Διάρκεια Αγώνα",
                ["settings.playerSpeed"] = "Ταχύτητα Παικτών",
                ["settings.showMinimap"] = "Εμφάνιση Μίνι Χάρτη",
                ["settings.showNames"] = "Εμφάνιση Ονομάτων",
                ["settings.showStamina"] = "Εμφάνιση Κόπωσης",
                ["settings.cameraZoom"] = "Ζουμ Κάμερας",
                ["settings.cameraSpeed"] = "Ταχύτητα Κάμερας",
                ["settings.languageSelect"] = "Γλώσσα",
                
                ["settings.difficulty.easy"] = "Εύκολο",
                ["settings.difficulty.normal"] = "Κανονικό",
                ["settings.difficulty.hard"] = "Δύσκολο",
                
                ["settings.on"] = "ΝΑΙ",
                ["settings.off"] = "ΟΧΙ",
            };
        }
        
        public static void ReloadLanguage()
        {
            if (_instance != null)
            {
                _instance.LoadLanguage(GameSettings.Instance.Language);
            }
        }
    }
}
