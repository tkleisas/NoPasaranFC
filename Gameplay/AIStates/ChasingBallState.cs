using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class ChasingBallState : AIState
    {
        public ChasingBallState()
        {
            Type = AIStateType.ChasingBall;
        }

        public override void Enter(Player player, AIContext context)
        {
            // Nothing to do
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            // If gained possession
            if (context.HasBallPossession)
            {
                return AIStateType.Dribbling;
            }
            
            // If someone else is closer, stop chasing
            if (!context.ShouldChaseBall)
            {
                return AIStateType.Positioning;
            }
            
            // If ball too far, give up
            if (context.DistanceToBall > AIConstants.ChaseBallGiveUpDistance)
            {
                return AIStateType.Positioning;
            }
            
            // Simple, direct ball chasing - just go to the ball
            Vector2 toBall = context.BallPosition - player.FieldPosition;
            if (toBall.LengthSquared() > 0)
            {
                toBall.Normalize();
                float speed = player.Speed * AIConstants.BaseSpeedMultiplier;
                player.Velocity = toBall * speed; // Set velocity - MatchEngine will apply multipliers and update position
                
                // Set AI target position to ball for debug visualization
                player.AITargetPosition = context.BallPosition;
                player.AITargetPositionSet = true;
            }
            
            return AIStateType.ChasingBall;
        }

        public override void Exit(Player player, AIContext context)
        {
            // Nothing to do
        }
    }
}
