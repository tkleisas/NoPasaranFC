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
            
            // Predict ball's future position based on velocity and player Agility
            // High Agility = better prediction = more efficient pursuit
            float agilityRatio = player.Agility / AIConstants.MaxStatValue;
            float predictionTime = AIConstants.ChasePredictionTime * (0.3f + 0.7f * agilityRatio);
            predictionTime *= (1f / AIBehaviorManager.GetDecisionMultiplier()); // Better prediction on hard
            
            Vector2 predictedBallPos = context.BallPosition + context.BallVelocity * predictionTime;
            
            // Clamp prediction to field bounds
            predictedBallPos.X = MathHelper.Clamp(predictedBallPos.X,
                MatchEngine.StadiumMargin, MatchEngine.StadiumMargin + MatchEngine.FieldWidth);
            predictedBallPos.Y = MathHelper.Clamp(predictedBallPos.Y,
                MatchEngine.StadiumMargin, MatchEngine.StadiumMargin + MatchEngine.FieldHeight);

            Vector2 toTarget = predictedBallPos - player.FieldPosition;
            if (toTarget.LengthSquared() > 0)
            {
                toTarget.Normalize();
                float speed = player.Speed * AIConstants.BaseSpeedMultiplier;
                player.Velocity = toTarget * speed;
                
                player.AITargetPosition = predictedBallPos;
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
