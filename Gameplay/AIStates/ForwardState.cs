using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class ForwardState : PositioningStateBase
    {
        protected override float GetStaminaDrainRate() => AIConstants.ForwardStaminaDrain;
        protected override float GetSpeedMultiplier() => AIConstants.ForwardSpeedBoost;

        protected override AIStateType? CheckChaseBall(Player player, AIContext context)
        {
            bool teamHasBall = context.ClosestToBall != null && context.ClosestToBall.Team == player.Team;
            bool ballInOpponentHalf = context.IsHomeTeam ?
                (context.BallPosition.X > MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f) :
                (context.BallPosition.X < MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f);

            if (teamHasBall && ballInOpponentHalf && context.DistanceToBall < AIConstants.ForwardAggressiveChaseDistance)
                return AIStateType.ChasingBall;

            if (IsKickoffChase(context) || (context.ShouldChaseBall && context.DistanceToBall < AIConstants.ForwardCloseChaseDistance))
                return AIStateType.ChasingBall;

            return null;
        }

        protected override Vector2 CalculateTargetPosition(Player player, AIContext context)
        {
            bool teamHasBall = context.ClosestToBall != null && context.ClosestToBall.Team == player.Team;
            bool ballInOpponentHalf = context.IsHomeTeam ?
                (context.BallPosition.X > MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f) :
                (context.BallPosition.X < MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f);

            float attackingX;
            if (teamHasBall)
            {
                attackingX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.85f :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.15f;
            }
            else
            {
                attackingX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.75f :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.25f;
            }

            float lerpFactor;
            if (teamHasBall && ballInOpponentHalf)
                lerpFactor = 0.6f;
            else if (teamHasBall)
                lerpFactor = 0.3f;
            else
                lerpFactor = 0.5f;

            Vector2 attackingPosition = new Vector2(attackingX, player.HomePosition.Y);
            Vector2 target = Vector2.Lerp(attackingPosition, context.BallPosition, lerpFactor);

            // Prevent clustering - spread forwards
            if (player.Role == PlayerRole.Striker || player.Role == PlayerRole.CenterForward)
            {
                float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;
                float spreadOffset = (player.ShirtNumber % 2 == 0) ? 250f : -250f;
                target.Y = MathHelper.Lerp(target.Y, centerY + spreadOffset, 0.5f);
            }
            else if (player.Role == PlayerRole.LeftWinger)
            {
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.2f, 0.6f);
            }
            else if (player.Role == PlayerRole.RightWinger)
            {
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.8f, 0.6f);
            }

            return target;
        }
    }
}
