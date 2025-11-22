using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class GoalkeeperState : AIState
    {
        private const float PenaltyAreaDepth = 1205f;
        private const float PenaltyAreaWidth = 2942f;
        private const float GoalWidth = 534f;
        
        public GoalkeeperState()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context)
        {
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            // If goalkeeper has ball, transition to passing (clear it!)
            if (context.HasBallPossession)
            {
                return AIStateType.Passing;
            }
            
            // Determine which goal we're defending
            bool isHomeGK = context.IsHomeTeam;
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;
            
            // Define penalty area and goal
            float penaltyTop = centerY - PenaltyAreaWidth / 2;
            float penaltyBottom = centerY + PenaltyAreaWidth / 2;
            float penaltyLeft, penaltyRight, goalLineX;
            
            if (isHomeGK)
            {
                penaltyLeft = MatchEngine.StadiumMargin;
                penaltyRight = MatchEngine.StadiumMargin + PenaltyAreaDepth;
                goalLineX = MatchEngine.StadiumMargin + 100f; // Position in goal
            }
            else
            {
                penaltyLeft = MatchEngine.StadiumMargin + MatchEngine.FieldWidth - PenaltyAreaDepth;
                penaltyRight = MatchEngine.StadiumMargin + MatchEngine.FieldWidth;
                goalLineX = MatchEngine.StadiumMargin + MatchEngine.FieldWidth - 100f; // Position in goal
            }
            
            // Check for dangerous situations in penalty area
            bool ballInPenaltyArea = context.BallPosition.X >= penaltyLeft && 
                                    context.BallPosition.X <= penaltyRight &&
                                    context.BallPosition.Y >= penaltyTop && 
                                    context.BallPosition.Y <= penaltyBottom;
            
            bool opponentInPenaltyArea = context.NearestOpponent != null &&
                                         context.NearestOpponent.FieldPosition.X >= penaltyLeft &&
                                         context.NearestOpponent.FieldPosition.X <= penaltyRight &&
                                         context.NearestOpponent.FieldPosition.Y >= penaltyTop &&
                                         context.NearestOpponent.FieldPosition.Y <= penaltyBottom;
            
            Vector2 targetPosition;
            float speed;
            
            // Priority 1: Chase ball if very close and in penalty area
            if (ballInPenaltyArea && context.DistanceToBall < 250f && context.BallHeight < 100f)
            {
                // Move toward ball but stay in penalty area
                targetPosition = context.BallPosition;
                speed = player.Speed * 2.5f;
            }
            // Priority 2: Intercept opponent with ball in penalty area
            else if (opponentInPenaltyArea && context.NearestOpponent != null && 
                     Vector2.Distance(context.NearestOpponent.FieldPosition, context.BallPosition) < 150f)
            {
                // Position between opponent and goal
                Vector2 goalCenter = new Vector2(goalLineX, centerY);
                targetPosition = Vector2.Lerp(goalCenter, context.NearestOpponent.FieldPosition, 0.3f);
                speed = player.Speed * 2.5f;
            }
            // Priority 3: Position in goal based on ball position
            else
            {
                // Calculate goal boundaries
                float goalTop = MatchEngine.StadiumMargin + (MatchEngine.FieldHeight - GoalWidth) / 2;
                float goalBottom = goalTop + GoalWidth;
                
                // Position goalkeeper in center of goal, but shift slightly toward ball
                float ballYInfluence = 0.3f;
                float targetY = MathHelper.Lerp(centerY, context.BallPosition.Y, ballYInfluence);
                targetY = MathHelper.Clamp(targetY, goalTop + 60f, goalBottom - 60f);
                
                targetPosition = new Vector2(goalLineX, targetY);
                speed = player.Speed * 2f;
            }
            
            // CRITICAL: Clamp target position to penalty area bounds
            targetPosition.X = MathHelper.Clamp(targetPosition.X, penaltyLeft + 50f, penaltyRight - 50f);
            targetPosition.Y = MathHelper.Clamp(targetPosition.Y, penaltyTop + 50f, penaltyBottom - 50f);
            
            // Move toward target - but only if far enough away
            Vector2 direction = targetPosition - player.FieldPosition;
            float distance = direction.Length();
            
            // Dead zone: if very close to target, don't move (prevents oscillation)
            const float DEAD_ZONE = 15f;
            if (distance < DEAD_ZONE)
            {
                player.Velocity = Vector2.Zero;
                return AIStateType.Positioning;
            }
            
            if (distance > 10f)
            {
                direction.Normalize();
                player.Velocity = direction * speed;
            }
            else
            {
                player.Velocity = Vector2.Zero;
            }
            
            // CRITICAL: Enforce penalty area boundary - clamp actual position
            player.FieldPosition = new Vector2(
                MathHelper.Clamp(player.FieldPosition.X, penaltyLeft + 50f, penaltyRight - 50f),
                MathHelper.Clamp(player.FieldPosition.Y, penaltyTop + 50f, penaltyBottom - 50f)
            );
            
            return AIStateType.Positioning;
        }

        public override void Exit(Player player, AIContext context)
        {
        }
    }
}
