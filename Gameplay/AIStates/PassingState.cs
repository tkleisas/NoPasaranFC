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
                // PREDICT target's future position based on their velocity
                Vector2 targetCurrentPos = passTarget.FieldPosition;
                Vector2 targetVelocity = passTarget.Velocity;
                
                // Calculate approximate travel time for the pass
                float distanceToTarget = Vector2.Distance(context.BallPosition, targetCurrentPos);
                float estimatedPassSpeed = 400f; // Rough estimate of pass speed
                float travelTime = distanceToTarget / estimatedPassSpeed;
                
                // Predict where target will be
                Vector2 predictedPosition = targetCurrentPos + (targetVelocity * travelTime);
                
                // Calculate desired pass direction to PREDICTED position
                Vector2 passDirection = predictedPosition - context.BallPosition;
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
                        
                        // Check if player is behind ball relative to pass direction
                        // Dot product: 1 = player directly behind ball, -1 = player ahead
                        float dotProduct = Vector2.Dot(playerToBall, passDirection);
                        
                        // Good position: player behind ball (dot > 0.3) and close enough
                        if (dotProduct > 0.3f && distToBall < 80f)
                        {
                            // DRAMATICALLY INCREASED power scaling: passes should travel far
                            // Field is 3200 wide, so 1/8 = 400, 1/4 = 800
                            float power;
                            if (distance < 400f) // Short passes (< 1/8 field)
                            {
                                power = 0.7f + (distance / 400f) * 0.2f; // 0.7 to 0.9
                            }
                            else if (distance < 800f) // Medium passes (1/8 to 1/4 field)
                            {
                                power = 0.9f + ((distance - 400f) / 400f) * 0.2f; // 0.9 to 1.1
                            }
                            else // Long passes (> 1/4 field)
                            {
                                // Allow overcharge (up to 1.3f) for very long passes
                                power = MathHelper.Clamp(1.1f + ((distance - 800f) / 800f) * 0.2f, 1.1f, 1.3f); 
                            }
                            
                            // Pass to PREDICTED position, not current position
                            OnPassBall?.Invoke(predictedPosition, power);
                            _hasExecutedPass = true;
                            return AIStateType.Positioning;
                        }
                        else
                        {
                            // Need to reposition - move to position behind ball relative to pass direction
                            Vector2 idealPosition = context.BallPosition - (passDirection * 60f);
                            Vector2 toIdealPos = idealPosition - player.FieldPosition;
                            
                            if (toIdealPos.LengthSquared() > 50f) // More than ~7 units away
                            {
                                toIdealPos.Normalize();
                                float repositionSpeed = player.Speed * 2.5f;
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
