using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class ForwardState : AIState
    {
        public ForwardState()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context)
        {
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            // If forward has ball, dribble
            if (context.HasBallPossession)
            {
                return AIStateType.Dribbling;
            }
            
            // At kickoff (first 5 seconds after kickoff), only closest players rush to ball
            bool justAfterKickoff = context.TimeSinceKickoff < 5f;
            
            bool teamHasBall = context.ClosestToBall != null && context.ClosestToBall.TeamId == player.TeamId;
            bool ballInOpponentHalf = context.IsHomeTeam ? 
                (context.BallPosition.X > MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f) :
                (context.BallPosition.X < MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f);
            
            // FORWARDS ARE VERY AGGRESSIVE when team has ball in opponent half
            if (teamHasBall && ballInOpponentHalf && context.DistanceToBall < 800f)
            {
                return AIStateType.ChasingBall;
            }
            
            // Also chase when very close to ball
            if ((justAfterKickoff && context.DistanceToBall < 500f) || (context.ShouldChaseBall && context.DistanceToBall < 400f))
            {
                return AIStateType.ChasingBall;
            }
            
            // Calculate attacking position based on game situation
            // REDUCED max position to prevent going out of bounds (90% -> 85%)
            float attackingX;
            if (teamHasBall)
            {
                // Push high when team has ball, but not too close to sideline
                attackingX = context.IsHomeTeam ? 
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.85f :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.15f;
            }
            else
            {
                // Still high but not as extreme
                attackingX = context.IsHomeTeam ? 
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.75f :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.25f;
            }
            
            // Lerp between attacking position and ball position
            float lerpFactor;
            if (teamHasBall && ballInOpponentHalf)
            {
                // Team attacking - move toward ball to support/receive
                lerpFactor = 0.6f; // High ball influence
            }
            else if (teamHasBall)
            {
                // Team has ball but not in opponent half - maintain position
                lerpFactor = 0.3f;
            }
            else
            {
                // Opponent has ball - track it
                lerpFactor = 0.5f;
            }
            
            Vector2 attackingPosition = new Vector2(attackingX, player.HomePosition.Y);
            Vector2 newTargetPosition = Vector2.Lerp(attackingPosition, context.BallPosition, lerpFactor);
            
            // Prevent clustering - spread forwards horizontally and vertically
            if (player.Role == PlayerRole.Striker || player.Role == PlayerRole.CenterForward)
            {
                float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;
                float spreadOffset = (player.ShirtNumber % 2 == 0) ? 250f : -250f; // Wider spread
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, centerY + spreadOffset, 0.5f);
            }
            else if (player.Role == PlayerRole.LeftWinger)
            {
                float topY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.2f;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, topY, 0.6f);
            }
            else if (player.Role == PlayerRole.RightWinger)
            {
                float bottomY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.8f;
                newTargetPosition.Y = MathHelper.Lerp(newTargetPosition.Y, bottomY, 0.6f);
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
                
                float speed = player.Speed * 2.5f * 1.2f; // Forwards run faster (20% boost)
                player.Velocity = direction * speed; // Set velocity - MatchEngine will apply multipliers and update position
                player.Stamina = System.Math.Max(0, player.Stamina - 3f * deltaTime);
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
