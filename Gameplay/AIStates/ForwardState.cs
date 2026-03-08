using System.Linq;
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

            float diffMult = AIBehaviorManager.GetPositioningMultiplier();

            if (teamHasBall && ballInOpponentHalf && context.DistanceToBall < AIConstants.ForwardAggressiveChaseDistance * diffMult)
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

            float diffMult = AIBehaviorManager.GetPositioningMultiplier();
            float speedRatio = player.Speed / AIConstants.MaxStatValue;

            float attackingX;
            if (teamHasBall)
            {
                float depth = 0.85f + speedRatio * 0.07f;
                attackingX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * depth :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (1f - depth);
            }
            else
            {
                // Stay higher even when not in possession
                attackingX = context.IsHomeTeam ?
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.70f :
                    MatchEngine.StadiumMargin + MatchEngine.FieldWidth * 0.30f;
            }

            // Forward ball lerp — more dynamic movement with play
            float lerpFactor;
            if (teamHasBall && ballInOpponentHalf)
                lerpFactor = 0.22f * diffMult;
            else if (teamHasBall)
                lerpFactor = 0.18f * diffMult;
            else
                lerpFactor = 0.25f * diffMult;

            Vector2 attackingPosition = new Vector2(attackingX, player.HomePosition.Y);
            Vector2 target = Vector2.Lerp(attackingPosition, context.BallPosition, MathHelper.Clamp(lerpFactor, 0f, 0.35f));

            // Forward runs: when any teammate has ball, make a run toward goal
            if (teamHasBall && context.ClosestToBall != null && context.ClosestToBall.Id != player.Id)
            {
                float teammateDistToBall = Vector2.Distance(context.ClosestToBall.FieldPosition, context.BallPosition);
                if (teammateDistToBall < AIConstants.ForwardRunTriggerDistance)
                {
                    float runDepth = AIConstants.ForwardRunDepth + speedRatio * 0.05f;
                    float runX = context.IsHomeTeam ?
                        MatchEngine.StadiumMargin + MatchEngine.FieldWidth * runDepth :
                        MatchEngine.StadiumMargin + MatchEngine.FieldWidth * (1f - runDepth);

                    float bestY = FindOpenLane(player, context, runX);
                    Vector2 runTarget = new Vector2(runX, bestY);
                    target = Vector2.Lerp(target, runTarget, 0.65f * diffMult);
                }
            }

            // Role differentiation — wider spread for strikers
            if (player.Role == PlayerRole.Striker || player.Role == PlayerRole.CenterForward)
            {
                float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2;
                float spreadOffset = (player.ShirtNumber % 2 == 0) ? 350f : -350f;
                target.Y = MathHelper.Lerp(target.Y, centerY + spreadOffset, 0.5f);
            }
            else if (player.Role == PlayerRole.LeftWinger)
            {
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.15f, 0.6f);
            }
            else if (player.Role == PlayerRole.RightWinger)
            {
                target.Y = AdjustYForLane(target.Y, MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.85f, 0.6f);
            }

            return target;
        }

        /// <summary>
        /// Finds the Y position with most open space at a given X depth.
        /// </summary>
        private float FindOpenLane(Player player, AIContext context, float atX)
        {
            if (context.Opponents == null || context.Opponents.Count == 0)
                return player.HomePosition.Y;

            float bestY = player.HomePosition.Y;
            float bestScore = float.MinValue;
            float fieldTop = MatchEngine.StadiumMargin + AIConstants.FieldMargin;
            float fieldBottom = MatchEngine.StadiumMargin + MatchEngine.FieldHeight - AIConstants.FieldMargin;

            // Test 9 candidate Y positions for better lane selection
            for (int i = 0; i < 9; i++)
            {
                float candidateY = MathHelper.Lerp(fieldTop, fieldBottom, (i + 0.5f) / 9f);
                Vector2 candidatePos = new Vector2(atX, candidateY);

                float minDefDist = float.MaxValue;
                foreach (var opp in context.Opponents.Where(o => o.IsStarting && !o.IsKnockedDown && o.Position == PlayerPosition.Defender))
                {
                    float dist = Vector2.Distance(opp.FieldPosition, candidatePos);
                    if (dist < minDefDist)
                        minDefDist = dist;
                }

                // Score: prefer positions far from defenders and closer to our current Y
                float proximityPenalty = System.Math.Abs(candidateY - player.HomePosition.Y) * 0.1f;
                float score = minDefDist - proximityPenalty;
                if (score > bestScore)
                {
                    bestScore = score;
                    bestY = candidateY;
                }
            }

            return bestY;
        }
    }
}
