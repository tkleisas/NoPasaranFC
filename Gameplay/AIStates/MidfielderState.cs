using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class MidfielderState : AIState
    {
        public MidfielderState()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context)
        {
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            // If midfielder has ball, dribble
            if (context.HasBallPossession)
            {
                return AIStateType.Dribbling;
            }
            
            // At kickoff (first 5 seconds after kickoff), only closest players rush to ball
            bool justAfterKickoff = context.TimeSinceKickoff < 5f;
            
            bool teamHasBall = context.ClosestToBall != null && context.ClosestToBall.Team == player.Team;
            bool ballInOpponentHalf = context.IsHomeTeam ? 
                (context.BallPosition.X > MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f) :
                (context.BallPosition.X < MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f);
            
            bool isAttackingMidfielder = player.Role == PlayerRole.AttackingMidfielder ||
                                        player.Role == PlayerRole.LeftWinger ||
                                        player.Role == PlayerRole.RightWinger;
            
            // Attacking midfielders are aggressive when team has ball in opponent half
            if (isAttackingMidfielder && teamHasBall && ballInOpponentHalf && context.DistanceToBall < 600f)
            {
                return AIStateType.ChasingBall;
            }
            
            // Midfielders are box-to-box players - moderate aggression
            bool ballInDefensiveHalf = context.IsDefensiveHalf && context.DistanceToBall < 350f; // Increased from 250f
            bool ballVeryClose = context.DistanceToBall < 300f; // Increased from 200f
            
            if ((justAfterKickoff && context.DistanceToBall < 500f) || (context.ShouldChaseBall && (ballInDefensiveHalf || ballVeryClose)))
            {
                return AIStateType.ChasingBall;
            }
            
            // Calculate distance to ball
            float distanceToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
            
            // Midfielders balance between home position and supporting ball
            Vector2 basePosition;
            float lerpFactor;
            
            if (teamHasBall && ballInOpponentHalf)
            {
                // Team attacking in opponent half - push forward aggressively
                float forwardX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.80f : 0.70f) :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.20f : 0.30f);
                basePosition = new Vector2(forwardX, player.HomePosition.Y);
                
                // Higher ball influence when attacking
                if (distanceToBall > 800f) lerpFactor = 0.3f;
                else if (distanceToBall > 500f) lerpFactor = 0.4f;
                else if (distanceToBall > 300f) lerpFactor = 0.5f;
                else lerpFactor = 0.6f;
            }
            else if (teamHasBall)
            {
                // Team has ball in own half - push forward moderately
                float forwardX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.70f : 0.60f) :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.30f : 0.40f);
                basePosition = new Vector2(forwardX, player.HomePosition.Y);
                
                if (distanceToBall > 800f) lerpFactor = 0.2f;
                else if (distanceToBall > 500f) lerpFactor = 0.3f;
                else if (distanceToBall > 300f) lerpFactor = 0.4f;
                else lerpFactor = 0.5f;
            }
            else
            {
                // Opponent has ball - hold defensive position
                basePosition = player.HomePosition;
                
                if (distanceToBall > 800f) lerpFactor = 0.15f;
                else if (distanceToBall > 500f) lerpFactor = 0.25f;
                else if (distanceToBall > 300f) lerpFactor = 0.35f;
                else lerpFactor = 0.50f;
            }
            
            Vector2 newTargetPosition = Vector2.Lerp(basePosition, context.BallPosition, lerpFactor);
            
            // IMPROVEMENT: Add "Support Offset" to avoid being in a straight line (blocked by markers)
            // Create a triangle formation by offsetting perpendicular to the line between home and ball
            if (distanceToBall > 200f)
            {
                Vector2 directionToBall = context.BallPosition - basePosition;
                if (directionToBall.LengthSquared() > 0)
                {
                    directionToBall.Normalize();
                    // Perpendicular vector (-y, x)
                    Vector2 perpendicular = new Vector2(-directionToBall.Y, directionToBall.X);
                    
                    // Determine which side to offset based on field position
                    // If we are on the left side of field, offset left (up/negative Y)
                    // If we are on the right side, offset right (down/positive Y)
                    float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;
                    float offsetDirection = (player.HomePosition.Y < centerY) ? -1f : 1f;
                    
                    // Vary offset based on role
                    if (player.Role == PlayerRole.CentralMidfielder)
                    {
                        // Central mids alternate or stay central
                        offsetDirection = (player.Id % 2 == 0) ? 1f : -1f;
                    }
                    
                    float supportOffset = 150f; // 150px offset
                    newTargetPosition += perpendicular * offsetDirection * supportOffset;
                }
            }
            
            // Wing midfielders stay wider
            if (player.Role == PlayerRole.LeftMidfielder || player.Role == PlayerRole.LeftWinger)
            {
                float leftY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.15f;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, leftY, 0.4f);
            }
            else if (player.Role == PlayerRole.RightMidfielder || player.Role == PlayerRole.RightWinger)
            {
                float rightY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.85f;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, rightY, 0.4f);
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
            
            // Clamp target to field boundaries (with margin to prevent going out)
            float fieldMargin = 150f; // Stay 150px away from boundaries
            newTargetPosition.X = MathHelper.Clamp(newTargetPosition.X, 
                MatchEngine.StadiumMargin + fieldMargin, 
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth - fieldMargin);
            newTargetPosition.Y = MathHelper.Clamp(newTargetPosition.Y, 
                MatchEngine.StadiumMargin + fieldMargin, 
                MatchEngine.StadiumMargin + MatchEngine.FieldHeight - fieldMargin);

            // Update target
            player.AITargetPosition = newTargetPosition;
            player.AITargetPositionSet = true;
            
            // Move toward target
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

                // Check if approaching field boundaries and adjust direction
                float leftDist = player.FieldPosition.X - MatchEngine.StadiumMargin;
                float rightDist = (MatchEngine.StadiumMargin + MatchEngine.FieldWidth) - player.FieldPosition.X;
                float topDist = player.FieldPosition.Y - MatchEngine.StadiumMargin;
                float bottomDist = (MatchEngine.StadiumMargin + MatchEngine.FieldHeight) - player.FieldPosition.Y;
                
                // If too close to boundary (< 200px), add repulsion force
                Vector2 repulsion = Vector2.Zero;
                if (leftDist < 200f) repulsion.X += 0.5f;
                if (rightDist < 200f) repulsion.X -= 0.5f;
                if (topDist < 200f) repulsion.Y += 0.5f;
                if (bottomDist < 200f) repulsion.Y -= 0.5f;
                
                // Blend movement direction with boundary repulsion
                if (repulsion.LengthSquared() > 0)
                {
                    repulsion.Normalize();
                    direction = Vector2.Lerp(direction, repulsion, 0.4f);
                    direction.Normalize();
                }

                float speed = player.Speed * 2.5f; // Base speed
                player.Velocity = direction * speed; // Set velocity - MatchEngine will apply multipliers and update position
                player.Stamina = System.Math.Max(0, player.Stamina - 2.5f * deltaTime);
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
