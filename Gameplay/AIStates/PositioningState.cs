using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class PositioningState : AIState
    {
        public PositioningState()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context)
        {
            // Nothing to do
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            // At match start (first 5 seconds), all players rush to ball
            bool matchJustStarted = context.MatchTime < 5f;
            
            // Check if should chase ball
            if (matchJustStarted || (context.ShouldChaseBall && context.DistanceToBall < 600f))
            {
                return AIStateType.ChasingBall;
            }
            
            // Move towards home position
            Vector2 toHome = player.HomePosition - player.FieldPosition;
            float distanceToHome = toHome.Length();
            
            if (distanceToHome < 30f) // Tighter threshold - keep moving until very close
            {
                return AIStateType.Idle;
            }
            
            if (distanceToHome > 0)
            {
                toHome.Normalize();
                float speed = player.Speed * 2.5f; // Base speed
                player.Velocity = toHome * speed; // Set velocity - MatchEngine will apply multipliers and update position
            }
            else
            {
                player.Velocity = Vector2.Zero;
            }
            
            return AIStateType.Positioning;
        }

        public override void Exit(Player player, AIContext context)
        {
            // Nothing to do
        }
    }
}
