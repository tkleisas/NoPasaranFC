using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class ShootingState : AIState
    {
        public delegate void ShootBallHandler(Vector2 targetPosition, float power);
        public event ShootBallHandler OnShootBall;

        public ShootingState()
        {
            Type = AIStateType.Shooting;
        }

        private bool _hasExecutedShot = false;
        private float _repositionTimer = 0f;
        private const float MaxRepositionTime = 0.8f;

        public override void Enter(Player player, AIContext context)
        {
            _hasExecutedShot = false;
            _repositionTimer = 0f;
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            if (_hasExecutedShot)
            {
                return AIStateType.Positioning;
            }

            _repositionTimer += deltaTime;

            // Timeout: if we can't get into position, just shoot from where we are
            if (_repositionTimer > MaxRepositionTime)
            {
                Vector2 forceTarget = context.OpponentGoalCenter;
                float shootStatRatio = player.Shooting / AIConstants.MaxStatValue;
                float maxOffset = AIConstants.ShotBaseOffset * (1f - shootStatRatio * AIConstants.ShotStatInfluence) * AIBehaviorManager.GetAccuracyMultiplier();
                forceTarget.Y += (float)(context.Random.NextDouble() - 0.5) * maxOffset * 1.3f; // Wider spread for forced shot

                float distToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
                if (distToBall < 100f)
                {
                    float power = MathHelper.Clamp(0.5f + shootStatRatio * 0.3f, 0.5f, 0.8f);
                    OnShootBall?.Invoke(forceTarget, power);
                    _hasExecutedShot = true;
                    return AIStateType.Positioning;
                }
                // Too far from ball, give up
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
                
                if (distToBall > 0.01f)
                {
                    playerToBall.Normalize();
                    float dotProduct = Vector2.Dot(playerToBall, shotDirection);
                    
                    // Relaxed shooting position: lower dot threshold and wider distance
                    if (dotProduct > 0.15f && distToBall < 80f)
                    {
                        float basePower = MathHelper.Clamp(0.6f + (1000f - distance) / 1000f * 0.4f, 0.6f, 1f);
                        float statBonus = statRatio * 0.15f;
                        float power = MathHelper.Clamp(basePower + statBonus, 0.6f, 1.15f);
                        OnShootBall?.Invoke(target, power);
                        _hasExecutedShot = true;
                        return AIStateType.Positioning;
                    }
                    else
                    {
                        // Reposition behind ball — move to side-offset position for quicker approach
                        Vector2 idealPosition = context.BallPosition - (shotDirection * 50f);
                        Vector2 toIdealPos = idealPosition - player.FieldPosition;
                        
                        if (toIdealPos.LengthSquared() > 25f)
                        {
                            toIdealPos.Normalize();
                            float repositionSpeed = player.Speed * 3.0f;
                            player.Velocity = toIdealPos * repositionSpeed;
                            return AIStateType.Shooting;
                        }
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
