using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class GoalkeeperState : AIState
    {
        public GoalkeeperState()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context) { }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            if (context.HasBallPossession)
                return AIStateType.Passing;

            bool isHomeGK = context.IsHomeTeam;
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;

            float penaltyTop = centerY - AIConstants.GKPenaltyAreaWidth / 2;
            float penaltyBottom = centerY + AIConstants.GKPenaltyAreaWidth / 2;
            float penaltyLeft, penaltyRight, goalLineX;

            if (isHomeGK)
            {
                penaltyLeft = MatchEngine.StadiumMargin;
                penaltyRight = MatchEngine.StadiumMargin + AIConstants.GKPenaltyAreaDepth;
                goalLineX = MatchEngine.StadiumMargin + 100f;
            }
            else
            {
                penaltyLeft = MatchEngine.StadiumMargin + MatchEngine.FieldWidth - AIConstants.GKPenaltyAreaDepth;
                penaltyRight = MatchEngine.StadiumMargin + MatchEngine.FieldWidth;
                goalLineX = MatchEngine.StadiumMargin + MatchEngine.FieldWidth - 100f;
            }

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

            if (ballInPenaltyArea && context.DistanceToBall < AIConstants.GKBallChaseDistance && context.BallHeight < 100f)
            {
                targetPosition = context.BallPosition;
                speed = player.Speed * AIConstants.BaseSpeedMultiplier;
            }
            else if (opponentInPenaltyArea && context.NearestOpponent != null &&
                     Vector2.Distance(context.NearestOpponent.FieldPosition, context.BallPosition) < 150f)
            {
                Vector2 goalCenter = new Vector2(goalLineX, centerY);
                targetPosition = Vector2.Lerp(goalCenter, context.NearestOpponent.FieldPosition, 0.3f);
                speed = player.Speed * AIConstants.BaseSpeedMultiplier;
            }
            else
            {
                float goalTop = MatchEngine.StadiumMargin + (MatchEngine.FieldHeight - AIConstants.GKGoalWidth) / 2;
                float goalBottom = goalTop + AIConstants.GKGoalWidth;

                float targetY = MathHelper.Lerp(centerY, context.BallPosition.Y, 0.3f);
                targetY = MathHelper.Clamp(targetY, goalTop + 60f, goalBottom - 60f);

                targetPosition = new Vector2(goalLineX, targetY);
                speed = player.Speed * 2f;
            }

            float pad = AIConstants.GKPenaltyPadding;
            targetPosition.X = MathHelper.Clamp(targetPosition.X, penaltyLeft + pad, penaltyRight - pad);
            targetPosition.Y = MathHelper.Clamp(targetPosition.Y, penaltyTop + pad, penaltyBottom - pad);

            Vector2 direction = targetPosition - player.FieldPosition;
            float distance = direction.Length();

            if (distance < AIConstants.DeadZone)
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

            player.FieldPosition = new Vector2(
                MathHelper.Clamp(player.FieldPosition.X, penaltyLeft + pad, penaltyRight - pad),
                MathHelper.Clamp(player.FieldPosition.Y, penaltyTop + pad, penaltyBottom - pad)
            );

            return AIStateType.Positioning;
        }

        public override void Exit(Player player, AIContext context) { }
    }
}
