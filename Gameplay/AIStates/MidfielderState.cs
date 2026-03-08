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
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.80f : 0.70f) :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.20f : 0.30f);
                basePosition = new Vector2(forwardX, player.HomePosition.Y);
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.6f, 0.5f, 0.4f, 0.3f) * diffMult;
            }
            else if (teamHasBall)
            {
                float forwardX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.70f : 0.60f) :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (isAttackingMidfielder ? 0.30f : 0.40f);
                basePosition = new Vector2(forwardX, player.HomePosition.Y);
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.5f, 0.4f, 0.3f, 0.2f) * diffMult;
            }
            else
            {
                basePosition = player.HomePosition;
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.50f, 0.35f, 0.25f, 0.15f) * diffMult;
            }

            Vector2 target = Vector2.Lerp(basePosition, context.BallPosition, MathHelper.Clamp(lerpFactor, 0f, 0.75f));

            // Support offset: triangle formation to create passing lanes
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

                    target += perpendicular * offsetDir * 150f;
                }
            }

            // Pressing behavior when opponent has ball
            if (!teamHasBall && context.NearestOpponent != null && context.IsDefensiveHalf)
            {
                float distToOpponent = Vector2.Distance(player.FieldPosition, context.NearestOpponent.FieldPosition);
                float passingStatRatio = player.Passing / AIConstants.MaxStatValue;
                float pressRange = 400f * diffMult;
                if (distToOpponent < pressRange)
                {
                    // Move toward space between opponent and ball
                    Vector2 pressTarget = Vector2.Lerp(context.NearestOpponent.FieldPosition, context.BallPosition, 0.4f);
                    float pressWeight = 0.3f * diffMult;
                    target = Vector2.Lerp(target, pressTarget, pressWeight);
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
