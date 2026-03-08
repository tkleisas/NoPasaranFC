using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class IdleState : AIState
    {
        public IdleState()
        {
            Type = AIStateType.Idle;
        }

        public override void Enter(Player player, AIContext context)
        {
            // Nothing to do
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            // Stop moving and recover stamina
            player.Velocity = Vector2.Zero;
            player.Stamina = System.Math.Min(100, player.Stamina + 2f * deltaTime);
            
            // At kickoff (first 5 seconds after kickoff), all players rush to ball
            bool justAfterKickoff = context.TimeSinceKickoff < AIConstants.KickoffDuration;
            
            // Transition logic
            if (justAfterKickoff || (context.ShouldChaseBall && context.DistanceToBall < 800f))
            {
                return AIStateType.ChasingBall;
            }
            
            // Return to position if too far — use larger threshold (150px) to prevent oscillation
            // PositioningState returns to Idle at StopDistance (30px), so 150px gap prevents flip-flopping
            float distanceToHome = Vector2.Distance(player.FieldPosition, player.HomePosition);
            if (distanceToHome > 150f)
            {
                return AIStateType.Positioning;
            }
            
            return AIStateType.Idle;
        }

        public override void Exit(Player player, AIContext context)
        {
            // Nothing to do
        }
    }
}
