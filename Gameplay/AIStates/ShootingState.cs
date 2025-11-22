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
            
            if (distance > 0.01f)
            {
                shotDirection.Normalize();
                
                // Check if player is in a good position to shoot (within 60-degree cone)
                Vector2 playerToBall = context.BallPosition - player.FieldPosition;
                float distToBall = playerToBall.Length();
                
                if (distToBall > 0.01f)
                {
                    playerToBall.Normalize();
                    
                    // Calculate angle between player-to-ball and shot direction
                    float dotProduct = Vector2.Dot(playerToBall, shotDirection);
                    float angle = MathHelper.ToDegrees((float)System.Math.Acos(MathHelper.Clamp(dotProduct, -1f, 1f)));
                    
                    // If within 60-degree cone and close enough, execute shot
                    if (angle < 30f && distToBall < 80f)
                    {
                        float power = MathHelper.Clamp(0.5f + (800f - distance) / 800f * 0.5f, 0.5f, 1f);
                        OnShootBall?.Invoke(target, power);
                        _hasExecutedShot = true;
                        return AIStateType.Positioning;
                    }
                    else
                    {
                        // Need to reposition - move to position behind ball
                        Vector2 idealPosition = context.BallPosition - (shotDirection * 70f);
                        Vector2 toIdealPos = idealPosition - player.FieldPosition;
                        
                        if (toIdealPos.LengthSquared() > 100f) // More than 10 units away
                        {
                            toIdealPos.Normalize();
                            float repositionSpeed = player.Speed * 2.0f;
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
