using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class DribblingState : AIState
    {
        private float _decisionTimer = 0f;
        private const float DecisionInterval = 0.3f; // Make decision every 0.3 seconds (was 0.5 - more responsive)

        public DribblingState()
        {
            Type = AIStateType.Dribbling;
        }

        public override void Enter(Player player, AIContext context)
        {
            _decisionTimer = 0f;
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            _decisionTimer += deltaTime;
            
            // Lost possession
            if (!context.HasBallPossession)
            {
                return AIStateType.ChasingBall;
            }
            
            // Check if near sideline - CRITICAL CHECK (wider margin to prevent going out)
            float leftMarginCheck = MatchEngine.StadiumMargin + 300f;
            float rightMarginCheck = MatchEngine.TotalWidth - MatchEngine.StadiumMargin - 300f;
            float topMarginCheck = MatchEngine.StadiumMargin + 300f;
            float bottomMarginCheck = MatchEngine.TotalHeight - MatchEngine.StadiumMargin - 300f;
            
            bool nearSideline = player.FieldPosition.X < leftMarginCheck || player.FieldPosition.X > rightMarginCheck ||
                              player.FieldPosition.Y < topMarginCheck || player.FieldPosition.Y > bottomMarginCheck;
            
            if (nearSideline)
            {
                return AIStateType.AvoidingSideline;
            }
            
            // Decision making (more frequent for responsive play)
            if (_decisionTimer >= DecisionInterval)
            {
                _decisionTimer = 0f;
                
                float distanceToGoal = Vector2.Distance(player.FieldPosition, context.OpponentGoalCenter);
                
                // Check if in shooting range (very close to goal) - FIRST PRIORITY
                if (distanceToGoal < 400f)
                {
                    // 90% chance to shoot when very close to goal
                    if (context.Random.NextDouble() < 0.9)
                    {
                        return AIStateType.Shooting;
                    }
                }
                
                // CRITICAL: Check for pass opportunities FIRST (before pressure checks)
                if (context.BestPassTarget != null)
                {
                    float distToTeammate = Vector2.Distance(player.FieldPosition, context.BestPassTarget.FieldPosition);
                    float teammateDistToGoal = Vector2.Distance(context.BestPassTarget.FieldPosition, context.OpponentGoalCenter);
                    float myDistToGoal = Vector2.Distance(player.FieldPosition, context.OpponentGoalCenter);
                    
                    // Is teammate ahead of me (closer to goal)?
                    bool teammateAheadOfMe = teammateDistToGoal < myDistToGoal - 50f; // Reduced buffer
                    
                    // Valid pass range: 150 to 2500 pixels
                    bool validPassRange = distToTeammate > 150f && distToTeammate < 2500f;
                    
                    // Check if under pressure from opponent
                    bool underPressure = false;
                    if (context.NearestOpponent != null)
                    {
                        float distToOpponent = Vector2.Distance(player.FieldPosition, context.NearestOpponent.FieldPosition);
                        underPressure = distToOpponent < 300f;
                    }
                    
                    // Position checks
                    bool isDefender = player.Position == PlayerPosition.Defender;
                    bool isMidfielder = player.Position == PlayerPosition.Midfielder;
                    bool isForward = player.Position == PlayerPosition.Forward;
                    bool isTeammateForward = context.BestPassTarget.Position == PlayerPosition.Forward;
                    
                    // ALWAYS PASS when under pressure (unless in shooting range)
                    if (underPressure && validPassRange && distanceToGoal > 400f)
                    {
                        return AIStateType.Passing;
                    }
                    
                    // DEFENDERS: Almost always pass forward
                    if (isDefender && validPassRange && teammateAheadOfMe)
                    {
                        // 98% pass chance for defenders
                        if (context.Random.NextDouble() < 0.98)
                        {
                            return AIStateType.Passing;
                        }
                    }
                    
                    // MIDFIELDERS: Very aggressive passing, especially to forwards
                    if (isMidfielder && validPassRange)
                    {
                        // Long ball to forward ahead of us: ALWAYS pass
                        if (isTeammateForward && teammateAheadOfMe && distToTeammate > 500f)
                        {
                            return AIStateType.Passing;
                        }
                        // Any forward pass: 95% chance
                        else if (teammateAheadOfMe)
                        {
                            if (context.Random.NextDouble() < 0.95)
                            {
                                return AIStateType.Passing;
                            }
                        }
                        // Even lateral/backward passes: 60% chance
                        else if (context.Random.NextDouble() < 0.60)
                        {
                            return AIStateType.Passing;
                        }
                    }
                    
                    // FORWARDS: Pass when teammate is in significantly better position
                    if (isForward && validPassRange && distanceToGoal > 400f)
                    {
                        // If teammate is much closer to goal, pass 90% of the time
                        if (teammateAheadOfMe && (myDistToGoal - teammateDistToGoal) > 200f)
                        {
                            if (context.Random.NextDouble() < 0.90)
                            {
                                return AIStateType.Passing;
                            }
                        }
                        // Otherwise, still pass 40% of the time
                        else if (context.Random.NextDouble() < 0.40)
                        {
                            return AIStateType.Passing;
                        }
                    }
                }
            }
            
            // CRITICAL: Dribble toward the opponent's goal
            // This creates attacking behavior - always advance the ball forward
            Vector2 desiredKickDirection = context.OpponentGoalCenter - context.BallPosition;
            
            // Check if near sideline - adjust direction toward center
            float leftMargin = MatchEngine.StadiumMargin + 200f;
            float rightMargin = MatchEngine.TotalWidth - MatchEngine.StadiumMargin - 200f;
            float topMargin = MatchEngine.StadiumMargin + 200f;
            float bottomMargin = MatchEngine.TotalHeight - MatchEngine.StadiumMargin - 200f;
            
            bool nearLeftSideline = context.BallPosition.X < leftMargin;
            bool nearRightSideline = context.BallPosition.X > rightMargin;
            bool nearTopSideline = context.BallPosition.Y < topMargin;
            bool nearBottomSideline = context.BallPosition.Y > bottomMargin;
            
            if (nearLeftSideline || nearRightSideline || nearTopSideline || nearBottomSideline)
            {
                // Redirect toward field center
                Vector2 fieldCenter = new Vector2(
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2,
                    MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2
                );
                Vector2 toCenter = fieldCenter - context.BallPosition;
                // Blend target direction with center direction
                desiredKickDirection = desiredKickDirection * 0.4f + toCenter * 0.6f;
            }
            
            if (desiredKickDirection.LengthSquared() > 0)
            {
                desiredKickDirection.Normalize();
                
                // Check if player is in a good position to kick (within 60-degree cone behind ball)
                Vector2 playerToBall = context.BallPosition - player.FieldPosition;
                float distToBall = playerToBall.Length();
                
                if (distToBall > 0.01f)
                {
                    playerToBall.Normalize();
                    
                    // Calculate angle between player-to-ball and desired kick direction
                    float dotProduct = Vector2.Dot(playerToBall, desiredKickDirection);
                    float angle = MathHelper.ToDegrees((float)System.Math.Acos(MathHelper.Clamp(dotProduct, -1f, 1f)));
                    
                    // If within 60-degree cone (30 degrees on each side) and close enough, just push forward
                    if (angle < 30f && distToBall < 80f)
                    {
                        // Good position - just move toward goal
                        Vector2 moveDirection = GetSafeDirection(player.FieldPosition, desiredKickDirection, context);
                        float dribbleSpeed = player.Speed * 2.5f;
                        player.Velocity = moveDirection * dribbleSpeed;
                    }
                    else
                    {
                        // Need to reposition - move to a position behind the ball
                        // Place player 70 units behind ball in opposite direction of desired kick
                        Vector2 idealPosition = context.BallPosition - (desiredKickDirection * 70f);
                        
                        Vector2 toIdealPos = idealPosition - player.FieldPosition;
                        float distToIdeal = toIdealPos.Length();
                        
                        if (distToIdeal > 10f) // Only move if not close enough
                        {
                            toIdealPos.Normalize();
                            Vector2 moveDirection = GetSafeDirection(player.FieldPosition, toIdealPos, context);
                            float repositionSpeed = player.Speed * 2.0f; // Slightly slower when repositioning
                            player.Velocity = moveDirection * repositionSpeed;
                        }
                        else
                        {
                            // Close enough to ideal position, move toward goal
                            Vector2 moveDirection = GetSafeDirection(player.FieldPosition, desiredKickDirection, context);
                            float dribbleSpeed = player.Speed * 2.5f;
                            player.Velocity = moveDirection * dribbleSpeed;
                        }
                    }
                }
            }
            
            return AIStateType.Dribbling;
        }

        public override void Exit(Player player, AIContext context)
        {
            _decisionTimer = 0f;
        }
    }
}
