using System.Linq;
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

            float diffMult = AIBehaviorManager.GetPositioningMultiplier();

            if (isAttackingMidfielder && teamHasBall && ballInOpponentHalf && context.DistanceToBall < 600f * diffMult)
                return AIStateType.ChasingBall;

            // Pressing: defensive midfielder actively presses when opponent has ball nearby
            bool opponentHasBall = !teamHasBall && context.ClosestToBall != null;
            if (opponentHasBall && player.Role == PlayerRole.DefensiveMidfielder 
                && context.DistanceToBall < (400f * diffMult) && context.IsDefensiveHalf)
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
            float diffMult = AIBehaviorManager.GetPositioningMultiplier();

            Vector2 basePosition;
            float lerpFactor;

            if (teamHasBall && ballInOpponentHalf)
            {
                float forwardX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.82f : 0.72f) :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.18f : 0.28f);
                basePosition = new Vector2(forwardX, player.HomePosition.Y);
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.20f, 0.15f, 0.10f, 0.05f) * diffMult;
            }
            else if (teamHasBall)
            {
                float forwardX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.72f : 0.62f) :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.28f : 0.38f);
                basePosition = new Vector2(forwardX, player.HomePosition.Y);
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.18f, 0.12f, 0.08f, 0.05f) * diffMult;
            }
            else
            {
                basePosition = player.HomePosition;
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.22f, 0.15f, 0.10f, 0.05f) * diffMult;
            }

            Vector2 target = Vector2.Lerp(basePosition, context.BallPosition, MathHelper.Clamp(lerpFactor, 0f, 0.50f));

            // Support offset: wider spread to create passing lanes
            if (distanceToBall > 200f && teamHasBall)
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

                    target += perpendicular * offsetDir * 280f;
                }
            }

            // Pressing behavior when opponent has ball
            if (!teamHasBall && context.NearestOpponent != null && context.IsDefensiveHalf)
            {
                float distToOpponent = Vector2.Distance(player.FieldPosition, context.NearestOpponent.FieldPosition);
                float pressRange = 400f * diffMult;
                if (distToOpponent < pressRange)
                {
                    Vector2 pressTarget = Vector2.Lerp(context.NearestOpponent.FieldPosition, context.BallPosition, 0.4f);
                    float pressWeight = 0.25f * diffMult;
                    target = Vector2.Lerp(target, pressTarget, pressWeight);
                }
            }

            // Wing midfielders stay wider — stronger lane anchoring
            if (player.Role == PlayerRole.LeftMidfielder || player.Role == PlayerRole.LeftWinger)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.20f, 0.55f);
            else if (player.Role == PlayerRole.RightMidfielder || player.Role == PlayerRole.RightWinger)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.80f, 0.55f);

            return target;
        }
    }
}
