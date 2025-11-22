using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class AvoidingSidelineState : AIState
    {
        private float _stateTimer = 0f;
        private const float MaxStateTime = 1.5f;

        public AvoidingSidelineState()
        {
            Type = AIStateType.AvoidingSideline;
        }

        public override void Enter(Player player, AIContext context)
        {
            _stateTimer = 0f;
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            _stateTimer += deltaTime;
            
            // Lost possession
            if (!context.HasBallPossession)
            {
                return AIStateType.ChasingBall;
            }
            
            // Calculate center of field
            Vector2 fieldCenter = new Vector2(
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f,
                MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f
            );
            
            // Determine which boundary is closest and move away from it strongly
            float leftDist = player.FieldPosition.X - MatchEngine.StadiumMargin;
            float rightDist = (MatchEngine.TotalWidth - MatchEngine.StadiumMargin) - player.FieldPosition.X;
            float topDist = player.FieldPosition.Y - MatchEngine.StadiumMargin;
            float bottomDist = (MatchEngine.TotalHeight - MatchEngine.StadiumMargin) - player.FieldPosition.Y;
            
            // Move STRONGLY away from nearest boundary
            Vector2 avoidDirection = Vector2.Zero;
            
            if (leftDist < 400f) avoidDirection.X += 1f; // Move right
            if (rightDist < 400f) avoidDirection.X -= 1f; // Move left
            if (topDist < 400f) avoidDirection.Y += 1f; // Move down
            if (bottomDist < 400f) avoidDirection.Y -= 1f; // Move up
            
            // Also add direction towards center
            Vector2 toCenter = fieldCenter - player.FieldPosition;
            if (toCenter.LengthSquared() > 0)
            {
                toCenter.Normalize();
            }
            
            // Combine avoidance (priority) with center direction
            Vector2 targetDirection = avoidDirection * 0.8f + toCenter * 0.2f;
            
            if (targetDirection.LengthSquared() > 0)
            {
                targetDirection.Normalize();
                float speed = player.Speed * 0.9f;
                player.Velocity = targetDirection * speed; // Set velocity - MatchEngine will apply multipliers and update position
            }
            else
            {
                player.Velocity = Vector2.Zero;
            }
            
            // Check if safe now (need bigger safety margin)
            float leftMargin = MatchEngine.StadiumMargin + 400f;
            float rightMargin = MatchEngine.TotalWidth - MatchEngine.StadiumMargin - 400f;
            float topMargin = MatchEngine.StadiumMargin + 400f;
            float bottomMargin = MatchEngine.TotalHeight - MatchEngine.StadiumMargin - 400f;
            
            bool isSafe = player.FieldPosition.X > leftMargin && player.FieldPosition.X < rightMargin &&
                         player.FieldPosition.Y > topMargin && player.FieldPosition.Y < bottomMargin;
            
            if (isSafe || _stateTimer > MaxStateTime)
            {
                return AIStateType.Dribbling;
            }
            
            return AIStateType.AvoidingSideline;
        }

        public override void Exit(Player player, AIContext context)
        {
            _stateTimer = 0f;
        }
    }
}
