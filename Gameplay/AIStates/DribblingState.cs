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
            
            // Check if VERY close to sideline (reduced margin to allow repositioning)
            // Only trigger avoidance when truly near the boundary (150px instead of 300px)
            float leftMarginCheck = MatchEngine.StadiumMargin + 150f;
            float rightMarginCheck = MatchEngine.TotalWidth - MatchEngine.StadiumMargin - 150f;
            float topMarginCheck = MatchEngine.StadiumMargin + 150f;
            float bottomMarginCheck = MatchEngine.TotalHeight - MatchEngine.StadiumMargin - 150f;
            
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
                
                // CRITICAL: Check for pass opportunities FIRST (prioritize teamwork)
                if (context.BestPassTarget != null)
                {
                    float distToTeammate = Vector2.Distance(player.FieldPosition, context.BestPassTarget.FieldPosition);
                    float teammateDistToGoal = Vector2.Distance(context.BestPassTarget.FieldPosition, context.OpponentGoalCenter);
                    float myDistToGoal = Vector2.Distance(player.FieldPosition, context.OpponentGoalCenter);
                    
                    // Is teammate ahead of me (closer to goal)?
                    bool teammateAheadOfMe = teammateDistToGoal < myDistToGoal - 30f;
                    
                    // Valid pass range: 80 to 2500 pixels (lowered minimum for close passes)
                    bool validPassRange = distToTeammate > 80f && distToTeammate < 2500f;
                    
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
                    if (isDefender && validPassRange)
                    {
                        if (teammateAheadOfMe)
                        {
                            // 98% pass chance when teammate ahead
                            if (context.Random.NextDouble() < 0.98)
                            {
                                return AIStateType.Passing;
                            }
                        }
                        else if (context.Random.NextDouble() < 0.70)
                        {
                            // 70% pass chance even if not ahead (lateral/back pass)
                            return AIStateType.Passing;
                        }
                    }
                    
                    // MIDFIELDERS: Very aggressive passing, especially to forwards
                    if (isMidfielder && validPassRange)
                    {
                        // Long ball to forward ahead of us: ALWAYS pass
                        if (isTeammateForward && teammateAheadOfMe && distToTeammate > 400f)
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
                        // Even lateral/backward passes: 70% chance (increased from 60%)
                        else if (context.Random.NextDouble() < 0.70)
                        {
                            return AIStateType.Passing;
                        }
                    }
                    
                    // FORWARDS: Pass when teammate is in better position, but prioritize shooting at close range
                    if (isForward && validPassRange)
                    {
                        // Very close to goal: shoot instead of pass
                        if (distanceToGoal < 350f)
                        {
                            if (context.Random.NextDouble() < 0.85)
                            {
                                return AIStateType.Shooting;
                            }
                        }
                        // If teammate is much closer to goal, pass 90% of the time
                        else if (teammateAheadOfMe && (myDistToGoal - teammateDistToGoal) > 150f)
                        {
                            if (context.Random.NextDouble() < 0.90)
                            {
                                return AIStateType.Passing;
                            }
                        }
                        // Otherwise, pass 50% of the time (increased from 40%)
                        else if (context.Random.NextDouble() < 0.50)
                        {
                            return AIStateType.Passing;
                        }
                    }
                }
                
                // Check if in shooting range - SECOND PRIORITY (after passing)
                // MUCH MORE AGGRESSIVE shooting to prevent dribbling into goal
                if (distanceToGoal < 200f)
                {
                    // Very close - ALWAYS shoot (inside penalty box)
                    return AIStateType.Shooting;
                }
                else if (distanceToGoal < 400f)
                {
                    // Close range - shoot 95%
                    if (context.Random.NextDouble() < 0.95)
                    {
                        return AIStateType.Shooting;
                    }
                }
                else if (distanceToGoal < 600f)
                {
                    // Medium range - shoot 80%
                    if (context.Random.NextDouble() < 0.80)
                    {
                        return AIStateType.Shooting;
                    }
                }
                else if (distanceToGoal < 800f)
                {
                    // Long range - shoot 50%
                    if (context.Random.NextDouble() < 0.50)
                    {
                        return AIStateType.Shooting;
                    }
                }
                else if (distanceToGoal < 1000f)
                {
                    // Very long range - shoot 20%
                    if (context.Random.NextDouble() < 0.20)
                    {
                        return AIStateType.Shooting;
                    }
                }
            }
            
            // CRITICAL: Dribble toward the opponent's goal
            // This creates attacking behavior - always advance the ball forward
            Vector2 desiredKickDirection = context.OpponentGoalCenter - context.BallPosition;
            if (desiredKickDirection.LengthSquared() > 0)
            {
                desiredKickDirection.Normalize();
            }
            

            // Check if VERY near boundaries - adjust direction toward center
            // Reduced margins to allow better positioning near goallines and sidelines
            float leftMargin = MatchEngine.StadiumMargin + 100f;  // Reduced from 200f
            float rightMargin = MatchEngine.TotalWidth - MatchEngine.StadiumMargin - 100f;
            float topMargin = MatchEngine.StadiumMargin + 100f;  // Reduced from 200f
            float bottomMargin = MatchEngine.TotalHeight - MatchEngine.StadiumMargin - 100f;
            
            bool nearLeftSideline = context.BallPosition.X < leftMargin;
            bool nearRightSideline = context.BallPosition.X > rightMargin;
            bool nearTopSideline = context.BallPosition.Y < topMargin;
            bool nearBottomSideline = context.BallPosition.Y > bottomMargin;
            
            if (nearLeftSideline || nearRightSideline || nearTopSideline || nearBottomSideline)
            {
                // Slightly redirect toward field center (reduced strength for better repositioning)
                Vector2 fieldCenter = new Vector2(
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2,
                    MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2
                );
                Vector2 toCenter = fieldCenter - context.BallPosition;
                // Blend target direction with center direction (reduced center influence)
                desiredKickDirection = desiredKickDirection * 0.7f + toCenter * 0.3f;  // Was 0.4/0.6
            }
            
            if (desiredKickDirection.LengthSquared() > 0)
            {
                desiredKickDirection.Normalize();
                
                // ALWAYS set the main target as the goal - this prevents target flipping
                Vector2 finalTarget = context.OpponentGoalCenter;
                player.AITargetPosition = finalTarget;
                player.AITargetPositionSet = true;
                
                // SIMPLIFIED DRIBBLING: Just move toward goal!
                // When we have possession, the ball moves with us due to auto-kick
                // Trying to get "behind" the ball causes infinite loops since ball follows us
                // The auto-kick system will handle kicking the ball in the right direction
                
                float distToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
                
                if (distToBall < 80f)
                {
                    // Close to ball - just move toward goal
                    // Auto-kick will fire when appropriate
                    float dribbleSpeed = player.Speed * 2.5f;
                    player.Velocity = desiredKickDirection * dribbleSpeed;
                }
                else
                {
                    // Far from ball - chase it
                    Vector2 toBall = context.BallPosition - player.FieldPosition;
                    toBall.Normalize();
                    float chaseSpeed = player.Speed * 2.5f;
                    player.Velocity = toBall * chaseSpeed;
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
