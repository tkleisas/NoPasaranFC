using System.Linq;
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

            // Defending stat scales threat awareness range
            float defendingRatio = player.Defending / AIConstants.MaxStatValue;
            float chaseBonus = defendingRatio * AIConstants.DefendingThreatRangeBonus;
            float diffMult = AIBehaviorManager.GetPositioningMultiplier();

            float effectiveDefensiveChase = AIConstants.DefenderChaseDefensiveDistance + chaseBonus * 0.5f;
            float effectiveEmergencyChase = (AIConstants.DefenderEmergencyChaseDistance + chaseBonus) * diffMult;
            float effectiveThreatZone = AIConstants.DefenderThreatZone + chaseBonus;

            bool ballDangerous = context.IsDefensiveHalf && context.DistanceToBall < effectiveDefensiveChase;
            bool emergencyDefense = opponentHasBall && distanceBallToOwnGoal < effectiveThreatZone
                                 && context.DistanceToBall < effectiveEmergencyChase;

            // Press toward ball carrier when nearby (Defending stat increases press range)
            bool shouldPress = opponentHasBall && context.DistanceToBall < (300f + defendingRatio * 150f) * diffMult
                            && context.IsDefensiveHalf;

            if (IsKickoffChase(context) || (context.ShouldChaseBall && ballDangerous) || emergencyDefense || shouldPress)
                return AIStateType.ChasingBall;

            return null;
        }

        protected override Vector2 CalculateTargetPosition(Player player, AIContext context)
        {
            float distanceToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
            float defendingRatio = player.Defending / AIConstants.MaxStatValue;
            float diffMult = AIBehaviorManager.GetPositioningMultiplier();

            bool opponentHasBall = context.ClosestToBall != null && context.ClosestToBall.TeamId != player.TeamId;
            bool teamHasBall = context.ClosestToBall != null && context.ClosestToBall.TeamId == player.TeamId;
            float distanceBallToOwnGoal = Vector2.Distance(context.BallPosition, context.OwnGoalCenter);

            // Threat-based lerp factor — reduced to prevent swarming ball
            float lerpBonus = defendingRatio * AIConstants.DefendingLerpBonus * diffMult;
            float lerpFactor;
            if (opponentHasBall && distanceBallToOwnGoal < 500f)
                lerpFactor = 0.50f + lerpBonus;
            else if (opponentHasBall && distanceBallToOwnGoal < AIConstants.DefenderThreatZone)
                lerpFactor = 0.35f + lerpBonus;
            else
                lerpFactor = GetDistanceBasedLerpFactor(distanceToBall, 0.25f, 0.18f, 0.12f, 0.08f) + lerpBonus * 0.5f;

            // Base position: push forward when team has ball in attacking half
            Vector2 basePos = player.HomePosition;
            if (teamHasBall && context.IsAttackingHalf)
            {
                float pushX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.45f :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.55f;
                basePos.X = MathHelper.Lerp(player.HomePosition.X, pushX, 0.5f);
            }
            else if (teamHasBall)
            {
                float pushX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.35f :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.65f;
                basePos.X = MathHelper.Lerp(player.HomePosition.X, pushX, 0.3f);
            }

            Vector2 target = Vector2.Lerp(basePos, context.BallPosition, MathHelper.Clamp(lerpFactor, 0f, 0.65f));

            // Man-marking: track nearest opponent when they're in our defensive zone
            if (opponentHasBall && context.Opponents != null)
            {
                Player nearestAttacker = FindNearestAttackerInZone(player, context);
                if (nearestAttacker != null)
                {
                    Vector2 markPosition = Vector2.Lerp(nearestAttacker.FieldPosition, context.OwnGoalCenter, 0.15f);
                    float markWeight = MathHelper.Clamp(defendingRatio * 0.5f * diffMult, 0.1f, 0.4f);
                    target = Vector2.Lerp(target, markPosition, markWeight);
                }
            }

            // Defensive line: conform to average X when opponent has ball (maintain shape under pressure)
            if (context.Teammates != null && opponentHasBall)
            {
                float avgDefX = GetDefensiveLineX(player, context);
                if (avgDefX > 0)
                    target.X = MathHelper.Lerp(target.X, avgDefX, AIConstants.DefensiveLineWeight * diffMult);
            }

            // Role-based lane adjustment — stronger to maintain formation shape
            if (player.Role == PlayerRole.LeftBack)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.25f, 0.5f);
            else if (player.Role == PlayerRole.RightBack)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.75f, 0.5f);
            else if (player.Role == PlayerRole.CenterBack || player.Role == PlayerRole.Sweeper)
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2, 0.3f);

            return target;
        }

        private Player FindNearestAttackerInZone(Player player, AIContext context)
        {
            if (context.Opponents == null) return null;

            Player nearest = null;
            float nearestDist = AIConstants.MarkingActivationDistance;

            foreach (var opp in context.Opponents.Where(o => o.IsStarting && !o.IsKnockedDown && o.Position != PlayerPosition.Goalkeeper))
            {
                float distToGoal = Vector2.Distance(opp.FieldPosition, context.OwnGoalCenter);
                if (distToGoal < nearestDist)
                {
                    float distToMe = Vector2.Distance(opp.FieldPosition, player.FieldPosition);
                    if (distToMe < nearestDist)
                    {
                        nearestDist = distToMe;
                        nearest = opp;
                    }
                }
            }
            return nearest;
        }

        private float GetDefensiveLineX(Player player, AIContext context)
        {
            if (context.Teammates == null) return 0;

            float totalX = 0;
            int count = 0;
            foreach (var tm in context.Teammates.Where(t => t.IsStarting && t.Position == PlayerPosition.Defender && t.Id != player.Id))
            {
                totalX += tm.FieldPosition.X;
                count++;
            }
            return count > 0 ? totalX / count : 0;
        }
    }
}
