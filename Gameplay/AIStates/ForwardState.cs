using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class ForwardState : AIState
    {
        public ForwardState()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context)
        {
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            // If forward has ball, dribble
            if (context.HasBallPossession)
            {
                return AIStateType.Dribbling;
            }
            
            // At match start (first 5 seconds), only closest players rush to ball
            bool matchJustStarted = context.MatchTime < 5f;
            
            // Forwards are aggressive - chase ball when reasonably close
            bool isCloseToBall = context.DistanceToBall < 350f; // Slightly reduced from 400f
            
            if ((matchJustStarted && context.DistanceToBall < 500f) || (context.ShouldChaseBall && isCloseToBall))
            {
                return AIStateType.ChasingBall;
            }
            
            // Forwards stay high and wide
            // CRITICAL: Use context.IsHomeTeam (set once at match start) instead of calculating from position
            float attackingX = context.IsHomeTeam ? 
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.85f :
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.15f;
            
            bool teamHasBall = context.ClosestToBall != null && context.ClosestToBall.TeamId == player.TeamId;
            
            // Lerp between attacking position and ball position
            float lerpFactor;
            if (teamHasBall)
            {
                // Team has ball - be ready to receive
                lerpFactor = 0.3f; // Less ball influence - maintain spacing
            }
            else
            {
                // Opponent has ball - track it more
                lerpFactor = 0.5f;
            }
            
            Vector2 attackingPosition = new Vector2(attackingX, player.HomePosition.Y);
            Vector2 newTargetPosition = Vector2.Lerp(attackingPosition, context.BallPosition, lerpFactor);
            
            // Spread strikers vertically
            if (player.Role == PlayerRole.Striker || player.Role == PlayerRole.CenterForward)
            {
                float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;
                float spreadOffset = (player.ShirtNumber % 2 == 0) ? 150f : -150f;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, centerY + spreadOffset, 0.4f);
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
                float speed = player.Speed * 2.5f * 1.2f; // Forwards run faster (20% boost)
                player.Velocity = direction * speed; // Set velocity - MatchEngine will apply multipliers and update position
                player.Stamina = System.Math.Max(0, player.Stamina - 3f * deltaTime);
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
