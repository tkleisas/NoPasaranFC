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

        public override void Enter(Player player, AIContext context)
        {
            _hasExecutedPass = false;
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            if (_hasExecutedPass)
            {
                return AIStateType.Positioning;
            }

            // Execute pass - prefer BestPassTarget (forwards/attackers)
            Player passTarget = context.BestPassTarget ?? context.NearestTeammate;
            
            if (passTarget != null)
            {
                // Calculate desired pass direction
                Vector2 passDirection = passTarget.FieldPosition - context.BallPosition;
                float distance = passDirection.Length();
                
                if (distance > 0.01f)
                {
                    passDirection.Normalize();
                    
                    // Check if player is in a good position to pass (within 60-degree cone)
                    Vector2 playerToBall = context.BallPosition - player.FieldPosition;
                    float distToBall = playerToBall.Length();
                    
                    if (distToBall > 0.01f)
                    {
                        playerToBall.Normalize();
                        
                        // Calculate angle between player-to-ball and pass direction
                        float dotProduct = Vector2.Dot(playerToBall, passDirection);
                        float angle = MathHelper.ToDegrees((float)System.Math.Acos(MathHelper.Clamp(dotProduct, -1f, 1f)));
                        
                        // VERY lenient passing: 120-degree cone (60 degrees each side) and much further distance
                        if (angle < 60f && distToBall < 150f)
                        {
                            // DRAMATICALLY INCREASED power scaling: passes should travel far
                            // Field is 3200 wide, so 1/8 = 400, 1/4 = 800
                            float power;
                            if (distance < 400f) // Short passes (< 1/8 field)
                            {
                                power = 0.6f + (distance / 400f) * 0.2f; // 0.6 to 0.8
                            }
                            else if (distance < 800f) // Medium passes (1/8 to 1/4 field)
                            {
                                power = 0.8f + ((distance - 400f) / 400f) * 0.15f; // 0.8 to 0.95
                            }
                            else // Long passes (> 1/4 field)
                            {
                                power = MathHelper.Clamp(0.95f + ((distance - 800f) / 800f) * 0.05f, 0.95f, 1.0f); // 0.95 to 1.0
                            }
                            
                            OnPassBall?.Invoke(passTarget.FieldPosition, power);
                            _hasExecutedPass = true;
                            return AIStateType.Positioning;
                        }
                        else
                        {
                            // Need to reposition - move to position behind ball
                            Vector2 idealPosition = context.BallPosition - (passDirection * 70f);
                            Vector2 toIdealPos = idealPosition - player.FieldPosition;
                            
                            if (toIdealPos.LengthSquared() > 100f) // More than 10 units away
                            {
                                toIdealPos.Normalize();
                                float repositionSpeed = player.Speed * 2.0f;
                                player.Velocity = toIdealPos * repositionSpeed;
                                return AIStateType.Passing; // Stay in passing state while repositioning
                            }
                        }
                    }
                }
            }
            
            // Can't pass, go back to positioning
            return AIStateType.Positioning;
        }

        public override void Exit(Player player, AIContext context)
        {
            // Nothing to do
        }
    }
}
