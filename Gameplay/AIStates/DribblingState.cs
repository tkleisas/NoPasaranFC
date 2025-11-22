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
            
            // More aggressive boundary avoidance
            float boundaryMargin = 200f; // Increased margin
            Vector2 repulsion = Vector2.Zero;
            if (player.FieldPosition.X < MatchEngine.StadiumMargin + boundaryMargin)
                repulsion.X = 1f;
            if (player.FieldPosition.X > MatchEngine.TotalWidth - MatchEngine.StadiumMargin - boundaryMargin)
                repulsion.X = -1f;
            if (player.FieldPosition.Y < MatchEngine.StadiumMargin + boundaryMargin)
                repulsion.Y = 1f;
            if (player.FieldPosition.Y > MatchEngine.TotalHeight - MatchEngine.StadiumMargin - boundaryMargin)
                repulsion.Y = -1f;

            if (repulsion.LengthSquared() > 0)
            {
                repulsion.Normalize();
                // When avoiding boundary, move decisively
                player.Velocity = repulsion * (player.Speed * 2.5f);
                return AIStateType.Dribbling; // Stay in dribbling state
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
            Vector2 desiredKickDirection = context.OpponentGoalCenter - player.FieldPosition;
            if (desiredKickDirection.LengthSquared() > 0)
            {
                desiredKickDirection.Normalize();
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

                // Check if player is ahead of the ball relative to the desired kick direction
                Vector2 playerToBall = context.BallPosition - player.FieldPosition;
                float dotProduct = Vector2.Dot(desiredKickDirection, playerToBall);

                bool playerAheadOfBall = dotProduct < 0;

                if (playerAheadOfBall)
                {
                    // Player is ahead of the ball, so reposition behind it
                    Vector2 positionBehindBall = context.BallPosition - (desiredKickDirection * 80f); // 80px behind ball
                    Vector2 directionToGetBehind = positionBehindBall - player.FieldPosition;
                    if (directionToGetBehind.LengthSquared() > 0)
                    {
                        directionToGetBehind.Normalize();
                    }
                    player.Velocity = directionToGetBehind * (player.Speed * 2.5f);
                }
                else
                {
                    // Player is behind the ball, proceed with dribbling
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
            }
            
            return AIStateType.Dribbling;
        }

        public override void Exit(Player player, AIContext context)
        {
            _decisionTimer = 0f;
        }
    }
}
