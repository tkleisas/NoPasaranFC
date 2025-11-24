using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Represents a planned movement path as a sequence of waypoints.
    /// Used by AI to follow smooth trajectories instead of single target points.
    /// </summary>
    public class TrajectoryPath
    {
        public List<Vector2> Waypoints { get; set; }
        public float CreationTime { get; set; }
        public float Lifetime { get; set; } // How long before invalidation (seconds)
        public int CurrentWaypointIndex { get; set; }
        public Color DebugColor { get; set; } // For visualization
        
        public TrajectoryPath()
        {
            Waypoints = new List<Vector2>();
            CurrentWaypointIndex = 0;
            Lifetime = 1.0f; // Default: 1 second lifetime
            DebugColor = Color.Yellow;
        }
        
        /// <summary>
        /// Check if trajectory is still valid based on time
        /// </summary>
        public bool IsValid(float currentTime)
        {
            return (currentTime - CreationTime) < Lifetime;
        }
        
        /// <summary>
        /// Get the current target waypoint for the player to move towards
        /// </summary>
        public Vector2 GetCurrentTarget(Vector2 playerPosition)
        {
            if (Waypoints == null || Waypoints.Count == 0)
                return playerPosition;
                
            // Clamp index to valid range
            if (CurrentWaypointIndex >= Waypoints.Count)
                CurrentWaypointIndex = Waypoints.Count - 1;
                
            return Waypoints[CurrentWaypointIndex];
        }
        
        /// <summary>
        /// Check if the trajectory has been completed (reached last waypoint)
        /// </summary>
        public bool IsComplete(Vector2 playerPosition, float threshold = 30f)
        {
            if (Waypoints == null || Waypoints.Count == 0)
                return true;
                
            // Check if we've reached the last waypoint
            if (CurrentWaypointIndex >= Waypoints.Count - 1)
            {
                float distToLast = Vector2.Distance(playerPosition, Waypoints[Waypoints.Count - 1]);
                return distToLast < threshold;
            }
            
            return false;
        }
        
        /// <summary>
        /// Try to advance to next waypoint if close enough to current one
        /// </summary>
        public void UpdateProgress(Vector2 playerPosition, float waypointRadius = 20f)
        {
            if (Waypoints == null || Waypoints.Count == 0)
                return;
                
            if (CurrentWaypointIndex >= Waypoints.Count)
                return;
                
            Vector2 currentTarget = Waypoints[CurrentWaypointIndex];
            float distToTarget = Vector2.Distance(playerPosition, currentTarget);
            
            // Advance to next waypoint if close enough
            if (distToTarget < waypointRadius && CurrentWaypointIndex < Waypoints.Count - 1)
            {
                CurrentWaypointIndex++;
            }
        }
    }
}
