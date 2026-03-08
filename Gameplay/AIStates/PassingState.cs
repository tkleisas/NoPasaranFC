using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class PassingState : AIState
    {
        public delegate void PassBallHandler(Vector2 targetPosition, float power);
        public event PassBallHandler OnPassBall;

        public PassingState()
        {
            Type = AIStateType.Passing;
        }

        private bool _hasExecutedPass = false;
        private float _repositionTimer = 0f;
        private const float MaxRepositionTime = 0.6f;

        public override void Enter(Player player, AIContext context)
        {
            _hasExecutedPass = false;
            _repositionTimer = 0f;
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            if (_hasExecutedPass)
            {
                return AIStateType.Positioning;
            }

            _repositionTimer += deltaTime;

            Player passTarget = context.BestPassTarget ?? context.NearestTeammate;
            
            if (passTarget == null)
                return AIStateType.Positioning;

            // Predict target's future position
            Vector2 targetCurrentPos = passTarget.FieldPosition;
            Vector2 targetVelocity = passTarget.Velocity;
            float distanceToTarget = Vector2.Distance(context.BallPosition, targetCurrentPos);
            float estimatedPassSpeed = 400f;
            float travelTime = distanceToTarget / estimatedPassSpeed;
            Vector2 predictedPosition = targetCurrentPos + (targetVelocity * travelTime);

            Vector2 passDirection = predictedPosition - context.BallPosition;
            float distance = passDirection.Length();
            
            if (distance < 0.01f)
                return AIStateType.Positioning;

            passDirection.Normalize();

            // Timeout: force pass from current position
            if (_repositionTimer > MaxRepositionTime)
            {
                float distToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
                if (distToBall < 100f)
                {
                    float power = CalculatePassPower(distance);
                    OnPassBall?.Invoke(predictedPosition, power * 0.85f); // Slightly weaker forced pass
                    _hasExecutedPass = true;
                    return AIStateType.Positioning;
                }
                return AIStateType.Positioning;
            }

            Vector2 playerToBall = context.BallPosition - player.FieldPosition;
            float playerDistToBall = playerToBall.Length();
            
            if (playerDistToBall < 0.01f)
                return AIStateType.Positioning;

            playerToBall.Normalize();
            float dotProduct = Vector2.Dot(playerToBall, passDirection);
            
            // Relaxed passing position: lower dot threshold and wider distance
            if (dotProduct > 0.1f && playerDistToBall < 90f)
            {
                float power = CalculatePassPower(distance);
                OnPassBall?.Invoke(predictedPosition, power);
                _hasExecutedPass = true;
                return AIStateType.Positioning;
            }
            else
            {
                // Reposition behind ball
                Vector2 idealPosition = context.BallPosition - (passDirection * 50f);
                Vector2 toIdealPos = idealPosition - player.FieldPosition;
                
                if (toIdealPos.LengthSquared() > 25f)
                {
                    toIdealPos.Normalize();
                    float passingRatio = player.Passing / AIConstants.MaxStatValue;
                    float repositionSpeed = player.Speed * (2.5f + passingRatio * 0.8f);
                    player.Velocity = toIdealPos * repositionSpeed;
                    return AIStateType.Passing;
                }
            }
            
            return AIStateType.Positioning;
        }

        public override void Exit(Player player, AIContext context)
        {
            _repositionTimer = 0f;
        }

        private float CalculatePassPower(float distance)
        {
            if (distance < 400f)
                return 0.7f + (distance / 400f) * 0.2f;
            if (distance < 800f)
                return 0.9f + ((distance - 400f) / 400f) * 0.2f;
            return MathHelper.Clamp(1.1f + ((distance - 800f) / 800f) * 0.2f, 1.1f, 1.3f);
        }
    }
}
