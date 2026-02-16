using Microsoft.Xna.Framework.Audio;
using Microsoft.Xna.Framework.Content;
using Microsoft.Xna.Framework.Media;
using System;
using System.Collections.Generic;

namespace NoPasaranFC.Gameplay
{
    public class AudioManager
    {
        private static AudioManager _instance;
        public static AudioManager Instance => _instance ??= new AudioManager();

        private ContentManager _content;
        private Dictionary<string, SoundEffect> _soundEffects;
        private Dictionary<string, Song> _songs;
        private Dictionary<string, SoundEffectInstance> _activeSoundInstances;
        private Song _currentSong;
        private string _currentSongName;
        
        // Volume settings (0.0 to 1.0)
        public float MusicVolume { get; set; } = 0.7f;
        public float SfxVolume { get; set; } = 0.8f;
        public bool MusicEnabled { get; set; } = true;
        public bool SfxEnabled { get; set; } = true;

        private AudioManager()
        {
            _soundEffects = new Dictionary<string, SoundEffect>();
            _songs = new Dictionary<string, Song>();
            _activeSoundInstances = new Dictionary<string, SoundEffectInstance>();
        }

        public void Initialize(ContentManager content)
        {
            _content = content;
            LoadAudio();
        }

        private void LoadAudio()
        {
            // Try to load sound effects (gracefully handle missing files)
            TryLoadSoundEffect("menu_select", "Audio/SFX/menu_select");
            TryLoadSoundEffect("menu_move", "Audio/SFX/menu_move");
            TryLoadSoundEffect("menu_back", "Audio/SFX/menu_back");
            TryLoadSoundEffect("whistle_start", "Audio/SFX/whistle_start");
            TryLoadSoundEffect("whistle_end", "Audio/SFX/whistle_end");
            TryLoadSoundEffect("kick_ball", "Audio/SFX/kick_ball");
            TryLoadSoundEffect("tackle", "Audio/SFX/tackle");
            TryLoadSoundEffect("goal", "Audio/SFX/goal");
            TryLoadSoundEffect("crowd_cheer", "Audio/SFX/crowd_cheer");
            TryLoadSoundEffect("crowd_aww", "Audio/SFX/crowd_aww");
            
            // Try to load music (gracefully handle missing files)
            TryLoadSong("menu_music", "Audio/Music/empros_no_pasaran");
            TryLoadSong("match_music", "Audio/Music/match");
            TryLoadSong("victory_music", "Audio/Music/victory");
        }

        private void TryLoadSoundEffect(string name, string assetPath)
        {
            try
            {
                var sfx = _content.Load<SoundEffect>(assetPath);
                _soundEffects[name] = sfx;
            }
            catch (Exception)
            {
                // Audio file not found - continue without it
                System.Diagnostics.Debug.WriteLine($"Audio file not found: {assetPath}");
            }
        }

        private void TryLoadSong(string name, string assetPath)
        {
            try
            {
                var song = _content.Load<Song>(assetPath);
                _songs[name] = song;
            }
            catch (Exception)
            {
                // Audio file not found - continue without it
                System.Diagnostics.Debug.WriteLine($"Music file not found: {assetPath}");
            }
        }

        public void PlaySoundEffect(string name, float volumeMultiplier = 1.0f, bool allowRetrigger = true)
        {
            if (!SfxEnabled) return;
            
            if (_soundEffects.TryGetValue(name, out var sfx))
            {
                // Check if this sound is already playing and shouldn't be retriggered
                if (!allowRetrigger && _activeSoundInstances.TryGetValue(name, out var existingInstance))
                {
                    if (existingInstance.State == SoundState.Playing)
                    {
                        return; // Don't retrigger while still playing
                    }
                    else
                    {
                        // Clean up stopped instance
                        existingInstance.Dispose();
                        _activeSoundInstances.Remove(name);
                    }
                }
                
                // For non-retriggerable sounds, create an instance we can track
                if (!allowRetrigger)
                {
                    var instance = sfx.CreateInstance();
                    instance.Volume = SfxVolume * volumeMultiplier;
                    instance.Play();
                    _activeSoundInstances[name] = instance;
                }
                else
                {
                    // For retriggerable sounds, just play normally (allows overlapping)
                    sfx.Play(SfxVolume * volumeMultiplier, 0f, 0f);
                }
            }
        }

        public void PlayMusic(string name, bool loop = true)
        {
            if (!MusicEnabled) return;
            
            if (_songs.TryGetValue(name, out var song))
            {
                // Don't restart if already playing
                if (_currentSongName == name && MediaPlayer.State == MediaState.Playing)
                    return;
                
                try
                {
                    _currentSong = song;
                    _currentSongName = name;
                    MediaPlayer.IsRepeating = loop;
                    MediaPlayer.Volume = MusicVolume;
                    MediaPlayer.Play(song);
                }
                catch (Exception ex)
                {
                    // On Android, MediaPlayer.Play can throw if the file is missing
                    // even though Content.Load<Song> succeeded (lazy file resolution)
                    System.Diagnostics.Debug.WriteLine($"Failed to play music '{name}': {ex.Message}");
                    _songs.Remove(name);
                    _currentSong = null;
                    _currentSongName = null;
                }
            }
        }

        public void StopMusic()
        {
            MediaPlayer.Stop();
            _currentSong = null;
            _currentSongName = null;
        }
        
        public void Update()
        {
            // Clean up finished sound instances
            var finishedSounds = new List<string>();
            foreach (var kvp in _activeSoundInstances)
            {
                if (kvp.Value.State == SoundState.Stopped)
                {
                    kvp.Value.Dispose();
                    finishedSounds.Add(kvp.Key);
                }
            }
            
            foreach (var soundName in finishedSounds)
            {
                _activeSoundInstances.Remove(soundName);
            }
            
            // Update volume in case it changed
            if (MediaPlayer.State == MediaState.Playing)
            {
                MediaPlayer.Volume = MusicVolume;
            }
        }

        public void PauseMusic()
        {
            if (MediaPlayer.State == MediaState.Playing)
            {
                MediaPlayer.Pause();
            }
        }

        public void ResumeMusic()
        {
            if (MediaPlayer.State == MediaState.Paused)
            {
                MediaPlayer.Resume();
            }
        }

        public void UpdateMusicVolume()
        {
            MediaPlayer.Volume = MusicVolume;
        }

        public void StopAllSoundEffects()
        {
            // Stop and dispose all active sound instances
            foreach (var instance in _activeSoundInstances.Values)
            {
                if (instance.State == SoundState.Playing)
                {
                    instance.Stop();
                }
                instance.Dispose();
            }
            _activeSoundInstances.Clear();
        }
    }
}
