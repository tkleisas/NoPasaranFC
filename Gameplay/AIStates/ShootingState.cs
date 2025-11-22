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

        public override void Enter(Player player, AIContext context)
        {
            _hasExecutedShot = false;
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            if (_hasExecutedShot)
            {
                return AIStateType.Positioning;
            }

            // Calculate desired shot direction
            Vector2 target = context.OpponentGoalCenter;
            target.Y += (float)(context.Random.NextDouble() - 0.5) * 200f; // Random vertical offset
            
            Vector2 shotDirection = target - context.BallPosition;
            float distance = shotDirection.Length();
            
            // Set AI target position to goal for debug visualization
            player.AITargetPosition = target;
            player.AITargetPositionSet = true;
            
            if (distance > 0.01f)
            {
                shotDirection.Normalize();
                
                // Check if player is in a good position to shoot (within 60-degree cone)
                Vector2 playerToBall = context.BallPosition - player.FieldPosition;
                float distToBall = playerToBall.Length();
                
                if (distToBall > 0.01f)
                {
                    playerToBall.Normalize();
                    
                    // Check if player is behind ball relative to shot direction
                    // Dot product: 1 = player directly behind ball, -1 = player ahead
                    float dotProduct = Vector2.Dot(playerToBall, shotDirection);
                    
                    // Good shooting position: player behind ball (dot > 0.3) and close
                    if (dotProduct > 0.3f && distToBall < 70f)
                    {
                        // Distance-based power: closer = more power
                        float power = MathHelper.Clamp(0.6f + (1000f - distance) / 1000f * 0.4f, 0.6f, 1f);
                        OnShootBall?.Invoke(target, power);
                        _hasExecutedShot = true;
                        return AIStateType.Positioning;
                    }
                    else
                    {
                        // Need to reposition - move to position behind ball relative to shot direction
                        Vector2 idealPosition = context.BallPosition - (shotDirection * 60f);
                        Vector2 toIdealPos = idealPosition - player.FieldPosition;
                        
                        if (toIdealPos.LengthSquared() > 50f) // More than ~7 units away
                        {
                            toIdealPos.Normalize();
                            float repositionSpeed = player.Speed * 2.5f;
                            player.Velocity = toIdealPos * repositionSpeed;
                            return AIStateType.Shooting; // Stay in shooting state while repositioning
                        }
                    }
                }
            }
            
            // Can't shoot, go back to positioning
            return AIStateType.Positioning;
        }

        public override void Exit(Player player, AIContext context)
        {
            // Nothing to do
        }
    }
}
