using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.Celebrations
{
    /// <summary>
    /// Manages all available goal celebrations and handles selection/execution
    /// </summary>
    public class CelebrationManager
    {
        private Dictionary<string, CelebrationBase> _availableCelebrations;
        private CelebrationBase _currentCelebration;
        private Player _scorer;
        private List<Player> _teammates;
        private List<Player> _opponents;
        private float _celebrationTime;
        private Random _random;

        public bool IsActive => _currentCelebration != null;
        public float CelebrationTime => _celebrationTime;
        public CelebrationBase CurrentCelebration => _currentCelebration;

        public CelebrationManager()
        {
            _availableCelebrations = new Dictionary<string, CelebrationBase>();
            _random = new Random();

            // Register all available celebrations
            RegisterCelebration(new RunAroundPitchCelebration());
            RegisterCelebration(new SlideCelebration());
            // Future celebrations can be added here:
            // RegisterCelebration(new GroupHuddleCelebration());
            // RegisterCelebration(new DanceCelebration());
        }

        /// <summary>
        /// Register a new celebration type
        /// </summary>
        public void RegisterCelebration(CelebrationBase celebration)
        {
            if (celebration == null || string.IsNullOrEmpty(celebration.CelebrationId))
                return;

            _availableCelebrations[celebration.CelebrationId] = celebration;
        }

        /// <summary>
        /// Start a celebration with automatic selection using hierarchy: Player -> Team -> Generic
        /// </summary>
        public void StartCelebration(Player scorer, List<Player> teammates, List<Player> opponents, bool isOwnGoal = false)
        {
            string celebrationId = SelectCelebrationId(scorer, isOwnGoal);
            StartCelebration(celebrationId, scorer, teammates, opponents);
        }

        /// <summary>
        /// Select celebration ID using hierarchy: Player -> Team -> Generic
        /// Own goals always use "run_around_pitch" (all players celebrate together)
        /// </summary>
        private string SelectCelebrationId(Player scorer, bool isOwnGoal = false)
        {
            // 0. Own goals always use the same celebration (all players running together)
            if (isOwnGoal)
            {
                return "run_around_pitch";
            }

            // 1. Check for player-specific celebrations
            if (scorer != null && scorer.CelebrationIds != null && scorer.CelebrationIds.Count > 0)
            {
                // Filter to only valid celebrations
                var validCelebrations = scorer.CelebrationIds.Where(id => _availableCelebrations.ContainsKey(id)).ToList();
                if (validCelebrations.Count > 0)
                {
                    // Randomly select from player's celebrations
                    int index = _random.Next(validCelebrations.Count);
                    return validCelebrations[index];
                }
            }

            // 2. Check for team-specific celebrations
            if (scorer?.Team != null && scorer.Team.CelebrationIds != null && scorer.Team.CelebrationIds.Count > 0)
            {
                // Filter to only valid celebrations
                var validCelebrations = scorer.Team.CelebrationIds.Where(id => _availableCelebrations.ContainsKey(id)).ToList();
                if (validCelebrations.Count > 0)
                {
                    // Randomly select from team's celebrations
                    int index = _random.Next(validCelebrations.Count);
                    return validCelebrations[index];
                }
            }

            // 3. Fall back to generic celebration (random selection)
            var celebration = SelectCelebration();
            return celebration?.CelebrationId;
        }

        /// <summary>
        /// Start a specific celebration by ID
        /// </summary>
        public void StartCelebration(string celebrationId, Player scorer, List<Player> teammates, List<Player> opponents)
        {
            if (string.IsNullOrEmpty(celebrationId) || !_availableCelebrations.ContainsKey(celebrationId))
            {
                // Fallback to first available celebration
                celebrationId = _availableCelebrations.Keys.FirstOrDefault();
            }

            if (celebrationId == null)
                return;

            // Stop current celebration if any
            StopCelebration();

            _currentCelebration = _availableCelebrations[celebrationId];
            _scorer = scorer;
            _teammates = teammates?.OrderBy(p => Vector2.Distance(p.FieldPosition, scorer.FieldPosition)).ToList() ?? new List<Player>();
            _opponents = opponents ?? new List<Player>();
            _celebrationTime = 0f;

            // Initialize the celebration
            _currentCelebration.Initialize(_scorer, _teammates, _opponents);
        }

        /// <summary>
        /// Update the active celebration
        /// </summary>
        public void Update(float deltaTime)
        {
            if (!IsActive)
                return;

            _celebrationTime += deltaTime;

            // Update scorer
            if (_scorer != null)
            {
                _currentCelebration.UpdateScorer(_scorer, deltaTime, _celebrationTime);
            }

            // Update teammates
            for (int i = 0; i < _teammates.Count; i++)
            {
                _currentCelebration.UpdateTeammate(_teammates[i], _scorer, _teammates, i, deltaTime, _celebrationTime);
            }

            // Update opponents
            foreach (var opponent in _opponents)
            {
                _currentCelebration.UpdateOpponent(opponent, deltaTime, _celebrationTime);
            }

            // Play celebration-specific audio
            _currentCelebration.PlayAudio(_celebrationTime);
        }

        /// <summary>
        /// Stop the current celebration
        /// </summary>
        public void StopCelebration()
        {
            if (_currentCelebration != null)
            {
                _currentCelebration.Cleanup();
                _currentCelebration = null;
            }

            // Stop all celebration audio
            Gameplay.AudioManager.Instance.StopAllSoundEffects();

            // Reset all player animations to default
            if (_scorer != null)
            {
                _scorer.CurrentAnimationState = "walk";
            }
            if (_teammates != null)
            {
                foreach (var teammate in _teammates)
                {
                    teammate.CurrentAnimationState = "walk";
                }
            }
            if (_opponents != null)
            {
                foreach (var opponent in _opponents)
                {
                    opponent.CurrentAnimationState = "walk";
                }
            }

            _scorer = null;
            _teammates?.Clear();
            _opponents?.Clear();
            _celebrationTime = 0f;
        }

        /// <summary>
        /// Get the camera target for the current celebration
        /// </summary>
        public Vector2 GetCameraTarget()
        {
            if (!IsActive || _scorer == null)
                return Vector2.Zero;

            Vector2? customTarget = _currentCelebration.GetCameraTarget(_scorer, _teammates);
            return customTarget ?? _scorer.FieldPosition;
        }

        /// <summary>
        /// Select a generic celebration (used when no player/team-specific celebration is set)
        /// Can be overridden for custom logic based on game context
        /// </summary>
        protected virtual CelebrationBase SelectCelebration()
        {
            // Randomly select from available celebrations
            if (_availableCelebrations.Count == 0)
                return null;

            int index = _random.Next(_availableCelebrations.Count);
            return _availableCelebrations.Values.ElementAt(index);

            // Future: Add weighted selection based on context
            // Example:
            // if (isWinningGoal) return GetCelebration("dramatic_celebration");
            // if (scoreDifference > 3) return GetCelebration("team_huddle");
            // return GetCelebration("run_around_pitch");
        }

        /// <summary>
        /// Get a specific celebration by ID
        /// </summary>
        public CelebrationBase GetCelebration(string celebrationId)
        {
            return _availableCelebrations.TryGetValue(celebrationId, out var celebration) ? celebration : null;
        }

        /// <summary>
        /// Get all available celebration IDs
        /// </summary>
        public IEnumerable<string> GetAvailableCelebrationIds()
        {
            return _availableCelebrations.Keys;
        }

        /// <summary>
        /// Get all available celebrations
        /// </summary>
        public IEnumerable<CelebrationBase> GetAvailableCelebrations()
        {
            return _availableCelebrations.Values;
        }
    }
}
