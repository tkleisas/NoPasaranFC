using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class DefenderState : PositioningStateBase
    {
        protected override float GetStaminaDrainRate() => AIConstants.DefenderStaminaDrain;
        protected override bool UseBoundaryRepulsion => false;

        protected override AIStateType? CheckChaseBall(Player player, AIContext context)
        {
            bool opponentHasBall = context.ClosestToBall != null && context.ClosestToBall.TeamId != player.TeamId;
            float distanceBallToOwnGoal = Vector2.Distance(context.BallPosition, context.OwnGoalCenter);

            bool ballDangerous = context.IsDefensiveHalf && context.DistanceToBall < AIConstants.DefenderChaseDefensiveDistance;
            bool emergencyDefense = opponentHasBall && distanceBallToOwnGoal < AIConstants.DefenderThreatZone
                                 && context.DistanceToBall < AIConstants.DefenderEmergencyChaseDistance;

            if (IsKickoffChase(context) || (context.ShouldChaseBall && ballDangerous) || emergencyDefense)
                return AIStateType.ChasingBall;

            return null;
        }

        protected override Vector2 CalculateTargetPosition(Player player, AIContext context)
        {
            float distanceToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);

            bool opponentHasBall = context.ClosestToBall != null && context.ClosestToBall.TeamId != player.TeamId;
            float distanceBallToOwnGoal = Vector2.Distance(context.BallPosition, context.OwnGoalCenter);

            // Threat-based lerp factor
            float lerpFactor;
            if (opponentHasBall && distanceBallToOwnGoal < 500f)
                lerpFactor = 0.70f;
            else if (opponentHasBall && distanceBallToOwnGoal < AIConstants.DefenderThreatZone)
                lerpFactor = 0.50f;
            else
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.45f, 0.30f, 0.20f, 0.12f);

            Vector2 target = Vector2.Lerp(player.HomePosition, context.BallPosition, lerpFactor);

            // Role-based lane adjustment
            if (player.Role == PlayerRole.LeftBack)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.25f, 0.3f);
            else if (player.Role == PlayerRole.RightBack)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.75f, 0.3f);
            else if (player.Role == PlayerRole.CenterBack || player.Role == PlayerRole.Sweeper)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2, 0.2f);

            return target;
        }
    }
}
