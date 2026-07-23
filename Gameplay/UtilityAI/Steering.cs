using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.UtilityAI
{
    /// <summary>
    /// Steering behaviors: smooth, continuous movement primitives that replace
    /// per-state velocity hacks. Arrive has built-in deceleration (physical inertia).
    /// </summary>
    public static class Steering
    {
        /// <summary>
        /// Seek-with-deceleration: full speed when far, smoothly decelerating
        /// inside slowRadius, arriving at zero speed exactly at the target.
        /// </summary>
        public static Vector2 Arrive(Vector2 position, Vector2 target, float maxSpeed, float slowRadius = 120f)
        {
            Vector2 toTarget = target - position;
            float distance = toTarget.Length();
            if (distance < 1f) return Vector2.Zero;
            
            float speed = maxSpeed * Math.Clamp(distance / slowRadius, 0.15f, 1f);
            return toTarget / distance * speed;
        }
        
        /// <summary>
        /// Separation from nearby teammates (personal space), blended into the
        /// desired velocity without killing forward progress.
        /// </summary>
        public static Vector2 ApplySeparation(Player player, Vector2 velocity, float blend = 0.25f)
        {
            if (player.Team == null) return velocity;
            
            Vector2 separation = Vector2.Zero;
            int count = 0;
            foreach (var mate in player.Team.Players)
            {
                if (!mate.IsStarting || mate == player || mate.IsKnockedDown) continue;
                float dist = Vector2.Distance(player.FieldPosition, mate.FieldPosition);
                if (dist < AIConstants.PlayerPersonalSpace && dist > 0.1f)
                {
                    Vector2 away = (player.FieldPosition - mate.FieldPosition) / dist;
                    separation += away * (1f - dist / AIConstants.PlayerPersonalSpace);
                    count++;
                }
            }
            
            if (count == 0) return velocity;
            separation /= count;
            
            Vector2 blended = velocity * (1f - blend) + separation * (velocity.Length() * blend);
            return blended;
        }
        
        /// <summary>
        /// Soft boundary avoidance: bends velocity away from pitch edges as the
        /// player approaches them (no hard clamps, no oscillation).
        /// </summary>
        public static Vector2 ApplyBoundaryAvoidance(Vector2 position, Vector2 velocity)
        {
            float trigger = AIConstants.BoundaryRepulsionTrigger;
            float left = position.X - MatchEngine.StadiumMargin;
            float right = MatchEngine.StadiumMargin + MatchEngine.FieldWidth - position.X;
            float top = position.Y - MatchEngine.StadiumMargin;
            float bottom = MatchEngine.StadiumMargin + MatchEngine.FieldHeight - position.Y;
            
            Vector2 bend = Vector2.Zero;
            if (left < trigger) bend.X += 1f - left / trigger;
            if (right < trigger) bend.X -= 1f - right / trigger;
            if (top < trigger) bend.Y += 1f - top / trigger;
            if (bottom < trigger) bend.Y -= 1f - bottom / trigger;
            
            if (bend.LengthSquared() < 0.001f) return velocity;
            bend.Normalize();
            return Vector2.Lerp(velocity, bend * velocity.Length(), AIConstants.BoundaryBlendWeight);
        }
    }
}
