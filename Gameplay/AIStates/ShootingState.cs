using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class ShootingState : AIState
    {
        public delegate void ShootBallHandler(Vector2 targetPosition, float power);
        public event ShootBallHandler OnShootBall;

        private readonly MatchEngine _engine;

        public ShootingState(MatchEngine engine)
        {
            Type = AIStateType.Shooting;
            _engine = engine;
        }

        private bool _hasExecutedShot = false;
        private float _repositionTimer = 0f;
        private const float MaxRepositionTime = 1.0f;

        public override void Enter(Player player, AIContext context)
        {
            _hasExecutedShot = false;
            _repositionTimer = 0f;
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            if (_hasExecutedShot)
                return AIStateType.Positioning;

            // Validate ball possession — abort if we lost the ball
            float distCheck = Vector2.Distance(player.FieldPosition, context.BallPosition);
            if (_engine.LastPlayerTouchedBall != player || distCheck > 120f)
                return AIStateType.ChasingBall;

            _repositionTimer += deltaTime;

            // Timeout: if we can't get into position, just shoot from where we are
            if (_repositionTimer > MaxRepositionTime)
            {
                Vector2 forceTarget = context.OpponentGoalCenter;
                float shootStatRatio = player.Shooting / AIConstants.MaxStatValue;
                float maxOffset = AIConstants.ShotBaseOffset * (1f - shootStatRatio * AIConstants.ShotStatInfluence) * AIBehaviorManager.GetAccuracyMultiplier();
                forceTarget.Y += (float)(context.Random.NextDouble() - 0.5) * maxOffset * 1.3f;

                float distToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
                if (distToBall < 120f)
                {
                    float power = MathHelper.Clamp(0.5f + shootStatRatio * 0.3f, 0.5f, 0.8f);
                    OnShootBall?.Invoke(forceTarget, power);
                    _hasExecutedShot = true;
                    return AIStateType.Positioning;
                }
                return AIStateType.Positioning;
            }

            // Shooting stat affects aim accuracy: high Shooting = tighter spread
            float statRatio = player.Shooting / AIConstants.MaxStatValue;
            float offsetReduction = statRatio * AIConstants.ShotStatInfluence;
            float offset = AIConstants.ShotBaseOffset * (1f - offsetReduction) * AIBehaviorManager.GetAccuracyMultiplier();

            Vector2 target = context.OpponentGoalCenter;
            target.Y += (float)(context.Random.NextDouble() - 0.5) * offset;
            
            Vector2 shotDirection = target - context.BallPosition;
            float distance = shotDirection.Length();
            
            player.AITargetPosition = target;
            player.AITargetPositionSet = true;
            
            if (distance > 0.01f)
            {
                shotDirection.Normalize();
                
                Vector2 playerToBall = context.BallPosition - player.FieldPosition;
                float distToBall = playerToBall.Length();
                
                if (distToBall < 0.01f)
                {
                    // Standing on the ball — just shoot
                    float basePower = MathHelper.Clamp(0.6f + (1500f - distance) / 1500f * 0.4f, 0.6f, 1f);
                    float statBonus = statRatio * 0.15f;
                    float power = MathHelper.Clamp(basePower + statBonus, 0.6f, 1.15f);
                    OnShootBall?.Invoke(target, power);
                    _hasExecutedShot = true;
                    return AIStateType.Positioning;
                }

                playerToBall.Normalize();
                float dotProduct = Vector2.Dot(playerToBall, shotDirection);
                
                // Execute shot when close enough — any approach angle works
                // Poor alignment reduces power (simulates awkward body position)
                if (distToBall < 100f)
                {
                    float alignmentFactor = MathHelper.Clamp((dotProduct + 1f) / 2f, 0.5f, 1f);
                    float basePower = MathHelper.Clamp(0.6f + (1500f - distance) / 1500f * 0.4f, 0.6f, 1f);
                    float statBonus = statRatio * 0.15f;
                    float power = MathHelper.Clamp((basePower + statBonus) * alignmentFactor, 0.5f, 1.15f);
                    OnShootBall?.Invoke(target, power);
                    _hasExecutedShot = true;
                    return AIStateType.Positioning;
                }
                else
                {
                    // Move toward ball (not behind it — just get close)
                    Vector2 towardBall = context.BallPosition - player.FieldPosition;
                    if (towardBall.LengthSquared() > 0)
                    {
                        towardBall.Normalize();
                        float repositionSpeed = player.Speed * 2.5f;
                        player.Velocity = towardBall * repositionSpeed;
                        return AIStateType.Shooting;
                    }
                }
            }
            
            return AIStateType.Positioning;
        }

        public override void Exit(Player player, AIContext context)
        {
            _repositionTimer = 0f;
        }
    }
}
