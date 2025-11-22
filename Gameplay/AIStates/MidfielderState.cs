using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class MidfielderState : AIState
    {
        public MidfielderState()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context)
        {
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            // If midfielder has ball, dribble
            if (context.HasBallPossession)
            {
                return AIStateType.Dribbling;
            }
            
            // At match start (first 5 seconds), only closest players rush to ball
            bool matchJustStarted = context.MatchTime < 5f;
            
            // Midfielders are box-to-box players - moderate aggression
            bool ballInDefensiveHalf = context.IsDefensiveHalf && context.DistanceToBall < 250f; // Reduced from 300f
            bool ballVeryClose = context.DistanceToBall < 200f; // Reduced from 250f
            
            if ((matchJustStarted && context.DistanceToBall < 500f) || (context.ShouldChaseBall && (ballInDefensiveHalf || ballVeryClose)))
            {
                return AIStateType.ChasingBall;
            }
            
            // Calculate distance to ball
            float distanceToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
            
            // Calculate lerp factor based on distance
            float lerpFactor;
            if (distanceToBall > 800f) lerpFactor = 0.15f;
            else if (distanceToBall > 500f) lerpFactor = 0.25f;
            else if (distanceToBall > 300f) lerpFactor = 0.35f;
            else lerpFactor = 0.50f;
            
            // Midfielders balance between home position and supporting ball
            // Team with ball should push forward more
            bool teamHasBall = context.ClosestToBall != null && context.ClosestToBall.TeamId == player.TeamId;
            bool isAttackingMidfielder = player.Role == PlayerRole.AttackingMidfielder ||
                                        player.Role == PlayerRole.LeftWinger ||
                                        player.Role == PlayerRole.RightWinger;
            
            Vector2 basePosition;
            if (teamHasBall)
            {
                // Team has ball - push toward enemy goal
                // CRITICAL: Use context.IsHomeTeam (set once at match start) instead of calculating from position
                // This prevents oscillation when crossing centerline
                float forwardX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.75f : 0.65f) :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.25f : 0.35f);
                basePosition = new Vector2(forwardX, player.HomePosition.Y);
            }
            else
            {
                // Opponent has ball - hold position
                basePosition = player.HomePosition;
            }
            
            Vector2 newTargetPosition = Vector2.Lerp(basePosition, context.BallPosition, lerpFactor);
            
            // Wing midfielders stay wider
            if (player.Role == PlayerRole.LeftMidfielder || player.Role == PlayerRole.LeftWinger)
            {
                float leftY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.15f;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, leftY, 0.4f);
            }
            else if (player.Role == PlayerRole.RightMidfielder || player.Role == PlayerRole.RightWinger)
            {
                float rightY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.85f;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, rightY, 0.4f);
            }
            
            // Only update target if it changed significantly (prevents oscillation)
            if (player.AITargetPositionSet)
            {
                float targetDifference = Vector2.Distance(player.AITargetPosition, newTargetPosition);
                float updateThreshold = 50f; // Only update if new target is 50+ pixels different
                
                if (targetDifference < updateThreshold)
                {
                    // Target hasn't changed significantly - keep old target
                    newTargetPosition = player.AITargetPosition;
                }
            }
            
            // Update target
            player.AITargetPosition = newTargetPosition;
            player.AITargetPositionSet = true;
            
            // Move toward target
            Vector2 direction = player.AITargetPosition - player.FieldPosition;
            float distance = direction.Length();
            
            // Dead zone: if very close to target, don't move (prevents oscillation)
            const float DEAD_ZONE = 15f;
            if (distance < DEAD_ZONE)
            {
                player.Velocity = Vector2.Zero;
                return AIStateType.Positioning;
            }
            
            if (distance < 30f) // Close enough, stop
            {
                player.Velocity = Vector2.Zero;
                return AIStateType.Idle;
            }
            
            if (distance > 0)
            {
                direction.Normalize();
                float speed = player.Speed * 2.5f; // Base speed
                player.Velocity = direction * speed; // Set velocity - MatchEngine will apply multipliers and update position
                player.Stamina = System.Math.Max(0, player.Stamina - 2.5f * deltaTime);
            }
            else
            {
                player.Velocity = Vector2.Zero;
            }
            
            return AIStateType.Positioning;
        }

        public override void Exit(Player player, AIContext context)
        {
        }
    }
}
