using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class PassingState : AIState
    {
        public delegate void PassBallHandler(Vector2 targetPosition, float power);
        public event PassBallHandler OnPassBall;

        private readonly MatchEngine _engine;

        public PassingState(MatchEngine engine)
        {
            Type = AIStateType.Passing;
            _engine = engine;
        }

        private bool _hasExecutedPass = false;
        private float _repositionTimer = 0f;
        private const float MaxRepositionTime = 1.2f;

        public override void Enter(Player player, AIContext context)
        {
            _hasExecutedPass = false;
            _repositionTimer = 0f;
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            if (_hasExecutedPass)
                return AIStateType.Positioning;

            // Validate ball possession — abort if we lost the ball
            float distCheck = Vector2.Distance(player.FieldPosition, context.BallPosition);
            if (_engine.LastPlayerTouchedBall != player || distCheck > 120f)
                return AIStateType.ChasingBall;

            _repositionTimer += deltaTime;

            Player passTarget = context.BestPassTarget ?? context.NearestTeammate;
            
            if (passTarget == null)
                return AIStateType.Positioning;

            // Predict target's future position using stat-based pass speed estimate
            Vector2 targetCurrentPos = passTarget.FieldPosition;
            Vector2 targetVelocity = passTarget.Velocity;
            float distanceToTarget = Vector2.Distance(context.BallPosition, targetCurrentPos);
            float estimatedPassSpeed = (player.Passing / 10f + 4f) * player.Speed;
            float travelTime = estimatedPassSpeed > 0 ? distanceToTarget / estimatedPassSpeed : 1f;
            travelTime = MathHelper.Clamp(travelTime, 0f, 1.5f);
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
                if (distToBall < 120f)
                {
                    float power = CalculatePassPower(distance);
                    OnPassBall?.Invoke(predictedPosition, power * 0.85f);
                    _hasExecutedPass = true;
                    return AIStateType.Positioning;
                }
                return AIStateType.Positioning;
            }

            Vector2 playerToBall = context.BallPosition - player.FieldPosition;
            float playerDistToBall = playerToBall.Length();
            
            if (playerDistToBall < 0.01f)
            {
                // Standing on the ball — just pass
                float power = CalculatePassPower(distance);
                OnPassBall?.Invoke(predictedPosition, power);
                _hasExecutedPass = true;
                return AIStateType.Positioning;
            }

            playerToBall.Normalize();
            float dotProduct = Vector2.Dot(playerToBall, passDirection);
            
            // Execute pass when roughly aligned and close enough
            if (dotProduct > 0.0f && playerDistToBall < 100f)
            {
                float power = CalculatePassPower(distance);
                OnPassBall?.Invoke(predictedPosition, power);
                _hasExecutedPass = true;
                return AIStateType.Positioning;
            }
            else
            {
                // Reposition behind ball with controlled speed
                Vector2 idealPosition = context.BallPosition - (passDirection * 50f);
                Vector2 toIdealPos = idealPosition - player.FieldPosition;
                
                if (toIdealPos.LengthSquared() > 25f)
                {
                    toIdealPos.Normalize();
                    float repositionSpeed = player.Speed * 1.8f;
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
