using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class DefenderState : AIState
    {
        public DefenderState()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context)
        {
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            // If defender has ball, dribble
            if (context.HasBallPossession)
            {
                return AIStateType.Dribbling;
            }
            
            // At kickoff (first 5 seconds after kickoff), only closest players rush to ball
            bool justAfterKickoff = context.TimeSinceKickoff < 5f;
            
            // AGGRESSIVE DEFENDING: Check if opponent has ball near goal
            bool opponentHasBall = context.ClosestToBall != null && context.ClosestToBall.TeamId != player.TeamId;
            float distanceBallToOwnGoal = Vector2.Distance(context.BallPosition, context.OwnGoalCenter);
            bool ballNearOurGoal = distanceBallToOwnGoal < 800f; // Threat zone
            
            // Defenders should ALWAYS chase when:
            // 1. Ball is in defensive half AND close
            // 2. Opponent has ball near our goal
            // 3. We're the designated chaser
            bool ballDangerous = context.IsDefensiveHalf && context.DistanceToBall < 300f;
            bool emergencyDefense = opponentHasBall && ballNearOurGoal && context.DistanceToBall < 500f;
            
            if ((justAfterKickoff && context.DistanceToBall < 500f) || 
                (context.ShouldChaseBall && ballDangerous) ||
                emergencyDefense)
            {
                return AIStateType.ChasingBall;
            }
            
            // Calculate distance to ball for update decisions
            float distanceToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
            
            // INCREASED ball tracking when opponent threatens our goal
            // Distance-based lerp: players far from ball use smaller lerp factor
            float lerpFactor;
            
            // If opponent has ball very close to our goal, ALL defenders track more aggressively
            if (opponentHasBall && distanceBallToOwnGoal < 500f)
            {
                lerpFactor = 0.70f; // VERY high ball influence when defending goal
            }
            else if (opponentHasBall && distanceBallToOwnGoal < 800f)
            {
                lerpFactor = 0.50f; // High ball influence in danger zone
            }
            else
            if (distanceToBall > 800f)
            {
                lerpFactor = 0.12f;  // Very far: minimal ball influence
            }
            else if (distanceToBall > 500f)
            {
                lerpFactor = 0.20f;  // Far: reduced influence
            }
            else if (distanceToBall > 300f)
            {
                lerpFactor = 0.30f;  // Medium: moderate influence
            }
            else
            {
                lerpFactor = 0.45f;   // Close: high ball influence
            }
            
            // Calculate new target position using SINGLE consistent formula
            Vector2 newTargetPosition = Vector2.Lerp(player.HomePosition, context.BallPosition, lerpFactor);
            
            // Adjust position based on role
            if (player.Role == PlayerRole.LeftBack)
            {
                // Left back covers left side
                float leftY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.25f;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, leftY, 0.3f);
            }
            else if (player.Role == PlayerRole.RightBack)
            {
                // Right back covers right side
                float rightY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.75f;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, rightY, 0.3f);
            }
            else if (player.Role == PlayerRole.CenterBack || player.Role == PlayerRole.Sweeper)
            {
                // Center backs stay central
                float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, centerY, 0.2f);
            }
            
            // Only update target if it changed significantly (prevents oscillation)
            if (player.AITargetPositionSet)
            {
                float targetDifference = Vector2.Distance(player.AITargetPosition, newTargetPosition);
                float updateThreshold = 50f; // Only update if new target is 50+ pixels different
                
                if (targetDifference < updateThreshold)
                {
                    // Target hasn't changed significantly - keep old target
                    newTargetPosition = player.AITargetPosition;
                }
            }
            
            // Update target position
            player.AITargetPosition = newTargetPosition;
            player.AITargetPositionSet = true;
            
            // Move toward target - but only if far enough away
            Vector2 direction = player.AITargetPosition - player.FieldPosition;
            float distance = direction.Length();
            
            // Dead zone: if very close to target, don't move (prevents oscillation)
            const float DEAD_ZONE = 15f;
            if (distance < DEAD_ZONE)
            {
                player.Velocity = Vector2.Zero;
                return AIStateType.Positioning;
            }
            
            if (distance < 30f) // Close enough, stop
            {
                player.Velocity = Vector2.Zero;
                return AIStateType.Idle;
            }
            
            if (distance > 0)
            {
                direction.Normalize();
                float speed = player.Speed * 2.5f; // Base speed
                player.Velocity = direction * speed; // Set velocity - MatchEngine will apply multipliers and update position
                player.Stamina = System.Math.Max(0, player.Stamina - 1.5f * deltaTime);
            }
            else
            {
                player.Velocity = Vector2.Zero;
            }
            
            return AIStateType.Positioning;
        }

        public override void Exit(Player player, AIContext context)
        {
        }
    }
}
