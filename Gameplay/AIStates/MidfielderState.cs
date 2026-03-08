using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class MidfielderState : PositioningStateBase
    {
        protected override float GetStaminaDrainRate() => AIConstants.MidfielderStaminaDrain;

        protected override AIStateType? CheckChaseBall(Player player, AIContext context)
        {
            bool teamHasBall = context.ClosestToBall != null && context.ClosestToBall.Team == player.Team;
            bool ballInOpponentHalf = context.IsHomeTeam ?
                (context.BallPosition.X > MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f) :
                (context.BallPosition.X < MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f);

            bool isAttackingMidfielder = player.Role == PlayerRole.AttackingMidfielder ||
                                        player.Role == PlayerRole.LeftWinger ||
                                        player.Role == PlayerRole.RightWinger;

            if (isAttackingMidfielder && teamHasBall && ballInOpponentHalf && context.DistanceToBall < 600f)
                return AIStateType.ChasingBall;

            bool ballInDefensiveHalf = context.IsDefensiveHalf && context.DistanceToBall < AIConstants.MidfielderDefensiveChaseDistance;
            bool ballVeryClose = context.DistanceToBall < AIConstants.MidfielderCloseChaseDistance;

            if (IsKickoffChase(context) || (context.ShouldChaseBall && (ballInDefensiveHalf || ballVeryClose)))
                return AIStateType.ChasingBall;

            return null;
        }

        protected override Vector2 CalculateTargetPosition(Player player, AIContext context)
        {
            bool teamHasBall = context.ClosestToBall != null && context.ClosestToBall.Team == player.Team;
            bool ballInOpponentHalf = context.IsHomeTeam ?
                (context.BallPosition.X > MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f) :
                (context.BallPosition.X < MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.5f);

            bool isAttackingMidfielder = player.Role == PlayerRole.AttackingMidfielder ||
                                        player.Role == PlayerRole.LeftWinger ||
                                        player.Role == PlayerRole.RightWinger;

            float distanceToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);

            Vector2 basePosition;
            float lerpFactor;

            if (teamHasBall && ballInOpponentHalf)
            {
                float forwardX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.80f : 0.70f) :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.20f : 0.30f);
                basePosition = new Vector2(forwardX, player.HomePosition.Y);
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.6f, 0.5f, 0.4f, 0.3f);
            }
            else if (teamHasBall)
            {
                float forwardX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.70f : 0.60f) :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.30f : 0.40f);
                basePosition = new Vector2(forwardX, player.HomePosition.Y);
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.5f, 0.4f, 0.3f, 0.2f);
            }
            else
            {
                basePosition = player.HomePosition;
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.50f, 0.35f, 0.25f, 0.15f);
            }

            Vector2 target = Vector2.Lerp(basePosition, context.BallPosition, lerpFactor);

            // Support offset: triangle formation to avoid being blocked
            if (distanceToBall > 200f)
            {
                Vector2 directionToBall = context.BallPosition - basePosition;
                if (directionToBall.LengthSquared() > 0)
                {
                    directionToBall.Normalize();
                    Vector2 perpendicular = new Vector2(-directionToBall.Y, directionToBall.X);

                    float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;
                    float offsetDir = (player.HomePosition.Y < centerY) ? -1f : 1f;

                    if (player.Role == PlayerRole.CentralMidfielder)
                        offsetDir = (player.Id % 2 == 0) ? 1f : -1f;

                    target += perpendicular * offsetDir * 150f;
                }
            }

            // Wing midfielders stay wider
            if (player.Role == PlayerRole.LeftMidfielder || player.Role == PlayerRole.LeftWinger)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.15f, 0.4f);
            else if (player.Role == PlayerRole.RightMidfielder || player.Role == PlayerRole.RightWinger)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.85f, 0.4f);

            return target;
        }
    }
}
