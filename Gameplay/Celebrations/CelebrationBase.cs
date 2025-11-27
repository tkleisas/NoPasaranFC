using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.Celebrations
{
    /// <summary>
    /// Base class for all goal celebration types.
    /// Each celebration defines how the scorer and teammates behave after a goal.
    /// </summary>
    public abstract class CelebrationBase
    {
        /// <summary>
        /// Unique identifier for this celebration type
        /// </summary>
        public abstract string CelebrationId { get; }

        /// <summary>
        /// Display name for this celebration
        /// </summary>
        public abstract string Name { get; }

        /// <summary>
        /// Description of what this celebration looks like
        /// </summary>
        public abstract string Description { get; }

        /// <summary>
        /// Initialize the celebration for a specific goal scorer and their team
        /// </summary>
        /// <param name="scorer">The player who scored</param>
        /// <param name="teammates">All teammates (excluding the scorer)</param>
        /// <param name="opponents">All opponent team players</param>
        public abstract void Initialize(Player scorer, List<Player> teammates, List<Player> opponents);

        /// <summary>
        /// Optional: Play celebration-specific audio during the celebration
        /// Called every frame during the celebration
        /// </summary>
        /// <param name="celebrationTime">Total time this celebration has been running</param>
        public virtual void PlayAudio(float celebrationTime)
        {
            // Default: no additional audio
        }

        /// <summary>
        /// Update the scorer's behavior during celebration
        /// </summary>
        /// <param name="scorer">The player who scored</param>
        /// <param name="deltaTime">Time since last frame</param>
        /// <param name="celebrationTime">Total time this celebration has been running</param>
        public abstract void UpdateScorer(Player scorer, float deltaTime, float celebrationTime);

        /// <summary>
        /// Update a teammate's behavior during celebration
        /// </summary>
        /// <param name="teammate">The teammate</param>
        /// <param name="scorer">The player who scored</param>
        /// <param name="allTeammates">All teammates in the celebration</param>
        /// <param name="teammateIndex">This teammate's index in the ordered list</param>
        /// <param name="deltaTime">Time since last frame</param>
        /// <param name="celebrationTime">Total time this celebration has been running</param>
        public abstract void UpdateTeammate(Player teammate, Player scorer, List<Player> allTeammates,
            int teammateIndex, float deltaTime, float celebrationTime);

        /// <summary>
        /// Update an opponent's behavior during celebration
        /// </summary>
        /// <param name="opponent">The opponent player</param>
        /// <param name="deltaTime">Time since last frame</param>
        /// <param name="celebrationTime">Total time this celebration has been running</param>
        public abstract void UpdateOpponent(Player opponent, float deltaTime, float celebrationTime);

        /// <summary>
        /// Clean up when celebration ends
        /// </summary>
        public abstract void Cleanup();

        /// <summary>
        /// Optional: Get camera target position for this celebration
        /// Return null to use default (follow scorer)
        /// </summary>
        public virtual Vector2? GetCameraTarget(Player scorer, List<Player> teammates)
        {
            return scorer?.FieldPosition; // Default: follow scorer
        }

        /// <summary>
        /// Optional: Get camera zoom multiplier for this celebration
        /// Return 1.0 for default zoom out (0.65), >1.0 to zoom in more, <1.0 to zoom out more
        /// Return null to use default celebration zoom behavior
        /// </summary>
        public virtual float? GetCameraZoomMultiplier()
        {
            return null; // Default: use standard celebration zoom
        }

        /// <summary>
        /// Optional: Get the duration of this celebration in seconds
        /// Return null to use default duration (60 seconds)
        /// </summary>
        public virtual float? GetCelebrationDuration()
        {
            return null; // Default: 60 seconds
        }

        /// <summary>
        /// Helper method to set celebration animation on a player
        /// </summary>
        protected void SetCelebrationAnimation(Player player, string animationState = "celebrate")
        {
            if (player != null)
            {
                player.CurrentAnimationState = animationState;
            }
        }

        /// <summary>
        /// Helper method to reset player animation to default
        /// </summary>
        protected void ResetPlayerAnimation(Player player)
        {
            if (player != null)
            {
                player.CurrentAnimationState = "walk";
            }
        }
    }
}
