using System.Linq;
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
                Vector2 forceTarget = GetShotAimPoint(context);
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

            Vector2 target = GetShotAimPoint(context);
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
                    // More power the farther out we are — long shots must beat ground friction
                    float basePower = MathHelper.Clamp(0.65f + (distance / 2000f) * 0.45f, 0.65f, 1.1f);
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
                    // More power the farther out we are — long shots must beat ground friction
                    float basePower = MathHelper.Clamp(0.65f + (distance / 2000f) * 0.45f, 0.65f, 1.1f);
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

        /// <summary>
        /// Picks the aim point inside the goal mouth away from the goalkeeper.
        /// Falls back to goal center when no keeper is found.
        /// </summary>
        private Vector2 GetShotAimPoint(AIContext context)
        {
            Vector2 aim = context.OpponentGoalCenter;

            Player keeper = context.Opponents?.FirstOrDefault(o => o.Position == PlayerPosition.Goalkeeper);
            if (keeper == null)
                return aim;

            float postOffset = MatchEngine.GoalWidth / 2f - AIConstants.ShotPostInset;
            float keeperOffset = keeper.FieldPosition.Y - aim.Y;

            if (System.Math.Abs(keeperOffset) < AIConstants.ShotGKCenteredTolerance)
            {
                // Keeper centered: pick a corner at random
                aim.Y += context.Random.NextDouble() < 0.5 ? -postOffset : postOffset;
            }
            else
            {
                // Aim at the post away from the keeper
                aim.Y -= System.Math.Sign(keeperOffset) * postOffset;
            }

            return aim;
        }
    }
}
