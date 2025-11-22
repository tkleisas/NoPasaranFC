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
            if (context.DistanceToBall > 1000f)
            {
                return AIStateType.Positioning;
            }
            
            // CRITICAL: If opponent has clear possession and is very close to ball, back off to defensive position
            if (context.NearestOpponent != null)
            {
                float opponentDistToBall = Vector2.Distance(context.NearestOpponent.FieldPosition, context.BallPosition);
                float myDistToBall = context.DistanceToBall;
                
                // If opponent is significantly closer (50+ units) and very close to ball (< 120), stop contesting
                if (opponentDistToBall < 120f && myDistToBall > opponentDistToBall + 50f)
                {
                    return AIStateType.Positioning;
                }
            }
            
            // Simple, direct ball chasing - just go to the ball
            Vector2 toBall = context.BallPosition - player.FieldPosition;
            if (toBall.LengthSquared() > 0)
            {
                toBall.Normalize();
                float speed = player.Speed * 2.5f;
                player.Velocity = toBall * speed; // Set velocity - MatchEngine will apply multipliers and update position
            }
            
            return AIStateType.ChasingBall;
        }

        public override void Exit(Player player, AIContext context)
        {
            // Nothing to do
        }
    }
}
