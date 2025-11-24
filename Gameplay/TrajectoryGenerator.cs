using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;

namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Static helper class for generating common trajectory types
    /// </summary>
    public static class TrajectoryGenerator
    {
        /// <summary>
        /// Generate a circular arc trajectory around a center point
        /// Used for orbiting around the ball
        /// </summary>
        /// <param name="center">Center of the arc (e.g., ball position)</param>
        /// <param name="startPos">Starting position of the player</param>
        /// <param name="arcAngleDegrees">Total angle to travel (e.g., 180 for half circle)</param>
        /// <param name="segments">Number of waypoints to generate (more = smoother)</param>
        /// <param name="currentTime">Current game time for trajectory creation timestamp</param>
        public static TrajectoryPath GenerateOrbitArc(
            Vector2 center, 
            Vector2 startPos, 
            float arcAngleDegrees, 
            int segments,
            float currentTime)
        {
            var trajectory = new TrajectoryPath
            {
                CreationTime = currentTime,
                Lifetime = 0.8f, // Short lifetime for responsive orbiting
                DebugColor = Color.Cyan
            };
            
            // Calculate radius from start position to center
            Vector2 toStart = startPos - center;
            float radius = toStart.Length();
            
            if (radius < 1f) // Too close to center, can't orbit
            {
                trajectory.Waypoints.Add(startPos);
                return trajectory;
            }
            
            // Calculate starting angle
            float startAngle = MathF.Atan2(toStart.Y, toStart.X);
            
            // Convert arc angle to radians
            float arcAngleRadians = MathHelper.ToRadians(arcAngleDegrees);
            
            // Generate waypoints along the arc
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                float angle = startAngle + (arcAngleRadians * t);
                
                Vector2 waypoint = center + new Vector2(
                    MathF.Cos(angle) * radius,
                    MathF.Sin(angle) * radius
                );
                
                trajectory.Waypoints.Add(waypoint);
            }
            
            return trajectory;
        }
        
        /// <summary>
        /// Generate a curved path that avoids an obstacle
        /// Used for dribbling around opponents
        /// </summary>
        /// <param name="start">Starting position</param>
        /// <param name="end">Desired end position</param>
        /// <param name="obstaclePos">Position of obstacle to avoid</param>
        /// <param name="curvature">How much to curve (0.0-1.0, higher = more curve)</param>
        /// <param name="currentTime">Current game time</param>
        public static TrajectoryPath GenerateCurvedAvoidance(
            Vector2 start,
            Vector2 end,
            Vector2 obstaclePos,
            float curvature,
            float currentTime)
        {
            var trajectory = new TrajectoryPath
            {
                CreationTime = currentTime,
                Lifetime = 0.6f,
                DebugColor = Color.Orange
            };
            
            // Calculate perpendicular offset direction
            Vector2 toEnd = end - start;
            float distance = toEnd.Length();
            
            if (distance < 1f)
            {
                trajectory.Waypoints.Add(start);
                trajectory.Waypoints.Add(end);
                return trajectory;
            }
            
            toEnd.Normalize();
            
            // Perpendicular vector (rotate 90 degrees)
            Vector2 perpendicular = new Vector2(-toEnd.Y, toEnd.X);
            
            // Determine which side of the line the obstacle is on
            Vector2 toObstacle = obstaclePos - start;
            float side = Vector2.Dot(toObstacle, perpendicular);
            
            // Curve away from the obstacle
            Vector2 curveDirection = side > 0 ? -perpendicular : perpendicular;
            
            // Generate curved path with 5 waypoints
            int segments = 5;
            float maxOffset = distance * curvature;
            
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                
                // Quadratic curve offset (peaks in the middle)
                float curveAmount = 4f * t * (1f - t) * maxOffset;
                
                Vector2 waypoint = Vector2.Lerp(start, end, t) + curveDirection * curveAmount;
                trajectory.Waypoints.Add(waypoint);
            }
            
            return trajectory;
        }
        
        /// <summary>
        /// Generate a simple arc between two points
        /// Used for smooth movement to positioning targets
        /// </summary>
        public static TrajectoryPath GenerateSmoothArc(
            Vector2 start,
            Vector2 end,
            float arcHeight,
            float currentTime)
        {
            var trajectory = new TrajectoryPath
            {
                CreationTime = currentTime,
                Lifetime = 1.0f,
                DebugColor = Color.LightGreen
            };
            
            Vector2 toEnd = end - start;
            float distance = toEnd.Length();
            
            if (distance < 1f)
            {
                trajectory.Waypoints.Add(end);
                return trajectory;
            }
            
            toEnd.Normalize();
            Vector2 perpendicular = new Vector2(-toEnd.Y, toEnd.X);
            
            // Generate arc with 6 waypoints
            int segments = 6;
            for (int i = 0; i <= segments; i++)
            {
                float t = (float)i / segments;
                
                // Parabolic arc
                float heightOffset = 4f * t * (1f - t) * arcHeight;
                
                Vector2 waypoint = Vector2.Lerp(start, end, t) + perpendicular * heightOffset;
                trajectory.Waypoints.Add(waypoint);
            }
            
            return trajectory;
        }
    }
}
