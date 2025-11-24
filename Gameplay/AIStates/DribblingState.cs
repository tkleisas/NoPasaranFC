using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class DribblingState : AIState
    {
        private float _decisionTimer = 0f;
        private const float DecisionInterval = 0.3f; // Make decision every 0.3 seconds
        private bool _isOrbiting = false;
        private float _orbitTimer = 0f; // Minimum time to stay in orbit state
        private float _orbitDurationVariation = 0f; // Per-player random variation (set on first orbit)
        private Vector2 _trajectoryBallPosition = Vector2.Zero; // Ball position when trajectory was created

        public DribblingState()
        {
            Type = AIStateType.Dribbling;
        }

        public override void Enter(Player player, AIContext context)
        {
            _decisionTimer = 0f;
            
            // Only reset if we've lost the ball completely (distance > 200)
            // This preserves the orbit state during micro-interruptions (e.g. 1-frame possession loss)
            float distToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
            if (distToBall > 200f)
            {
                _isOrbiting = false;
                _orbitTimer = 0f;
                
                // Set unique orbit duration for this player (±0.1s variation)
                // This breaks synchronization between players
                if (context.PlayerRandom != null)
                {
                    _orbitDurationVariation = (float)(context.PlayerRandom.NextDouble() - 0.5) * 0.2f;
                }
            }
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            _decisionTimer += deltaTime;
            if (_orbitTimer > 0) _orbitTimer -= deltaTime;
            
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
                    // Medium range - CHARGED SHOT opportunity
                    // Shoot 80% of the time if we have a clear line
                    if (context.Random.NextDouble() < 0.80)
                    {
                        // TODO: In future, we could pass a "charge" parameter to ShootingState
                        // For now, ShootingState will handle the shot, but we trigger it from further out
                        return AIStateType.Shooting;
                    }
                }
                else if (distanceToGoal < 800f)
                {
                    // Long range - shoot 50% (attempting long shots)
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
            
            // GOAL ENTRY LOGIC:
            // If we are close to the goal (within 300px) and within the goal width,
            // aim DEEP into the net to force the player to run into it.
            float goalTop = MatchEngine.StadiumMargin + (MatchEngine.FieldHeight - MatchEngine.GoalWidth) / 2;
            float goalBottom = goalTop + MatchEngine.GoalWidth;
            bool inGoalWidth = player.FieldPosition.Y >= goalTop && player.FieldPosition.Y <= goalBottom;
            
            if (inGoalWidth && Vector2.Distance(player.FieldPosition, context.OpponentGoalCenter) < 300f)
            {
                // Aim 100px BEHIND the goal line to force entry
                Vector2 deepGoalTarget = context.OpponentGoalCenter + (Vector2.Normalize(context.OpponentGoalCenter - context.OwnGoalCenter) * 100f);
                desiredKickDirection = deepGoalTarget - player.FieldPosition;
            }
            
            // BOUNDARY ESCAPE LOGIC (DEFENSIVE ONLY)
            // Only escape boundaries when in defensive half to avoid pushing ball to sidelines when attacking
            float escapeMargin = 150f;
            bool nearLeft = context.BallPosition.X < MatchEngine.StadiumMargin + escapeMargin;
            bool nearRight = context.BallPosition.X > MatchEngine.TotalWidth - MatchEngine.StadiumMargin - escapeMargin;
            bool nearTop = context.BallPosition.Y < MatchEngine.StadiumMargin + escapeMargin;
            bool nearBottom = context.BallPosition.Y > MatchEngine.TotalHeight - MatchEngine.StadiumMargin - escapeMargin;
            
            // Only apply boundary escape if in defensive half
            bool inDefensiveHalf = context.IsDefensiveHalf;
            
            if ((nearLeft || nearRight || nearTop || nearBottom) && inDefensiveHalf)
            {
                Vector2 escapeDir = Vector2.Zero;
                if (nearLeft) escapeDir.X = 1f;
                if (nearRight) escapeDir.X = -1f;
                if (nearTop) escapeDir.Y = 1f;
                if (nearBottom) escapeDir.Y = -1f;
                
                if (escapeDir.LengthSquared() > 0)
                {
                    escapeDir.Normalize();
                    // Blend escape with goal direction (50/50) instead of full override
                    desiredKickDirection = Vector2.Normalize(desiredKickDirection) + escapeDir;
                    if (desiredKickDirection.LengthSquared() > 0)
                        desiredKickDirection.Normalize();
                }
            }
            
            if (desiredKickDirection.LengthSquared() > 0)
            {
                desiredKickDirection.Normalize();
                
                // ALWAYS set the main target as the goal - this prevents target flipping
                Vector2 finalTarget = context.OpponentGoalCenter;
                player.AITargetPosition = finalTarget;
                player.AITargetPositionSet = true;
                
                float distToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);

                // Check if player is ahead of the ball relative to the desired kick direction
                Vector2 playerToBall = context.BallPosition - player.FieldPosition;
                float dotProduct = Vector2.Dot(desiredKickDirection, playerToBall);

                bool playerAheadOfBall = dotProduct < 0;

                // ORBIT LOGIC: If we are ahead of the ball, circle around it
                // Use trajectory-based circular arc for smooth, committed movement
                if (playerAheadOfBall || _isOrbiting)
                {
                    // Check if trajectory needs to be invalidated due to ball movement
                    if (_isOrbiting && _currentTrajectory != null)
                    {
                        float ballMovement = Vector2.Distance(context.BallPosition, _trajectoryBallPosition);
                        if (ballMovement > 100f) // Ball moved more than 100px
                        {
                            // Invalidate trajectory and regenerate
                            _currentTrajectory = null;
                            _isOrbiting = false;
                        }
                    }
                    
                    // Start orbiting if ahead
                    if (playerAheadOfBall && !_isOrbiting) 
                    {
                        _isOrbiting = true;
                        // Apply per-player variation: base 0.2s + variation (±0.1s)
                        _orbitTimer = 0.2f + _orbitDurationVariation;
                        
                        // Store ball position for trajectory invalidation tracking
                        _trajectoryBallPosition = context.BallPosition;
                        
                        // Generate circular arc trajectory around the ball
                        // Arc angle: 180 degrees (half circle to get behind ball)
                        _currentTrajectory = TrajectoryGenerator.GenerateOrbitArc(
                            context.BallPosition,  // Center of orbit
                            player.FieldPosition,   // Start position
                            180f,                   // Arc angle in degrees
                            16,                     // 16 waypoints for very smooth curve
                            (float)context.MatchTime  // Current time
                        );
                    }
                    
                    // Stop orbiting only if:
                    // 1. We are CLEARLY behind the ball (dot product > 0.5) AND
                    // 2. Timer has expired AND
                    // 3. Trajectory is complete
                    bool trajectoryComplete = _currentTrajectory == null || 
                                             _currentTrajectory.IsComplete(player.FieldPosition);
                    
                    if (dotProduct > 0.5f && _orbitTimer <= 0f && trajectoryComplete)
                    {
                        _isOrbiting = false;
                        _currentTrajectory = null;
                    }

                    if (_isOrbiting && _currentTrajectory != null)
                    {
                        // Follow the arc trajectory
                        Vector2 trajectoryVelocity = FollowTrajectory(
                            player, 
                            player.Speed * 3.0f,  // Fast orbiting speed
                            (float)context.MatchTime
                        );
                        
                        if (trajectoryVelocity.LengthSquared() > 0)
                        {
                            player.Velocity = trajectoryVelocity;
                            return AIStateType.Dribbling;
                        }
                        else
                        {
                            // Trajectory ended, fallback to seeking behind ball
                            Vector2 toGoal = context.OpponentGoalCenter - context.BallPosition;
                            toGoal.Normalize();
                            Vector2 targetPos = context.BallPosition - (toGoal * 150f);
                            Vector2 seekDir = targetPos - player.FieldPosition;
                            
                            if (seekDir.LengthSquared() > 0)
                            {
                                seekDir.Normalize();
                                player.Velocity = seekDir * (player.Speed * 3.0f);
                            }
                            
                            return AIStateType.Dribbling;
                        }
                    }
                }
                
                // Player is behind the ball, proceed with dribbling
                if (distToBall < 80f)
                {
                    // Close to ball - Dribble with STEERING behavior
                    
                    // 1. Goal Force (Primary)
                    Vector2 goalForce = desiredKickDirection;
                    
                    // 2. Avoidance Force (Secondary)
                    Vector2 avoidanceForce = Vector2.Zero;
                    
                    if (context.NearestOpponent != null)
                    {
                        float distToOpponent = Vector2.Distance(player.FieldPosition, context.NearestOpponent.FieldPosition);
                        if (distToOpponent < 200f) // Avoid if closer than 200px
                        {
                            Vector2 fromOpponent = player.FieldPosition - context.NearestOpponent.FieldPosition;
                            if (fromOpponent.LengthSquared() > 0)
                            {
                                fromOpponent.Normalize();
                                // Stronger avoidance when closer
                                float strength = 1.0f - (distToOpponent / 200f); 
                                avoidanceForce = fromOpponent * strength * 1.5f; // 1.5x multiplier for urgency
                            }
                        }
                    }
                    
                    // Blend forces: 70% Goal, 30% Avoidance (plus urgency multiplier)
                    Vector2 finalDirection = goalForce + avoidanceForce;
                    
                    if (finalDirection.LengthSquared() > 0)
                    {
                        finalDirection.Normalize();
                    }
                    else
                    {
                        finalDirection = goalForce; // Fallback
                    }
                    
                    float dribbleSpeed = player.Speed * 2.5f;
                    player.Velocity = finalDirection * dribbleSpeed;
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
