using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class GoalkeeperState : AIState
    {
        private bool _isDiving = false;
        private Vector2 _diveTarget;
        private float _diveTimer = 0f;

        public GoalkeeperState()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context)
        {
            _isDiving = false;
            _diveTimer = 0f;
        }

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

            // Difficulty affects GK reaction and positioning quality
            float posMultiplier = AIBehaviorManager.GetPositioningMultiplier();
            float decisionMult = AIBehaviorManager.GetDecisionMultiplier();

            // Stat-based tracking: Agility affects reaction, Defending affects range
            float agilityRatio = player.Agility / AIConstants.MaxStatValue;
            float defendingRatio = player.Defending / AIConstants.MaxStatValue;
            float trackingLerp = AIConstants.GKBallTrackingLerp * (0.6f + 0.4f * agilityRatio) * posMultiplier;

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

            // Shot detection: predict ball trajectory and dive to intercept
            bool shotDetected = DetectShot(context, isHomeGK, goalLineX, centerY);
            if (_isDiving)
            {
                _diveTimer -= deltaTime;
                if (_diveTimer <= 0f)
                    _isDiving = false;

                targetPosition = _diveTarget;
                speed = player.Speed * AIConstants.GKDiveBurstMultiplier * (0.7f + 0.3f * agilityRatio);
            }
            else if (shotDetected)
            {
                // Start dive to intercept point
                Vector2 interceptPoint = PredictBallInterceptPoint(context, goalLineX, centerY);
                _isDiving = true;
                _diveTarget = interceptPoint;
                _diveTimer = 0.5f / decisionMult; // Faster reaction on hard difficulty
                targetPosition = interceptPoint;
                speed = player.Speed * AIConstants.GKDiveBurstMultiplier * (0.7f + 0.3f * agilityRatio);
            }
            else if (ballInPenaltyArea && context.DistanceToBall < AIConstants.GKBallChaseDistance + defendingRatio * 100f && context.BallHeight < 100f)
            {
                // Chase ball in penalty area - range scales with Defending stat
                targetPosition = context.BallPosition;
                speed = player.Speed * AIConstants.BaseSpeedMultiplier;
            }
            else if (opponentInPenaltyArea && context.NearestOpponent != null &&
                     Vector2.Distance(context.NearestOpponent.FieldPosition, context.BallPosition) < 150f)
            {
                // Close down opponent with ball in penalty area
                Vector2 goalCenter = new Vector2(goalLineX, centerY);
                targetPosition = Vector2.Lerp(goalCenter, context.NearestOpponent.FieldPosition, 0.3f + 0.2f * defendingRatio);
                speed = player.Speed * AIConstants.BaseSpeedMultiplier;
            }
            else
            {
                // Default positioning: track ball Y with stat-scaled lerp
                float goalTop = MatchEngine.StadiumMargin + (MatchEngine.FieldHeight - AIConstants.GKGoalWidth) / 2;
                float goalBottom = goalTop + AIConstants.GKGoalWidth;

                float targetY = MathHelper.Lerp(centerY, context.BallPosition.Y, trackingLerp);
                targetY = MathHelper.Clamp(targetY, goalTop + 60f, goalBottom - 60f);

                // Advance slightly off goal line when ball is far away
                float ballDistToGoal = Math.Abs(context.BallPosition.X - goalLineX);
                float advanceAmount = MathHelper.Clamp(ballDistToGoal / (MatchEngine.FieldWidth * 0.5f), 0f, 1f);
                float advanceX = isHomeGK ? goalLineX + advanceAmount * 200f * defendingRatio :
                                            goalLineX - advanceAmount * 200f * defendingRatio;

                targetPosition = new Vector2(advanceX, targetY);
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

        public override void Exit(Player player, AIContext context)
        {
            _isDiving = false;
        }

        /// <summary>
        /// Detects if the ball is being shot toward goal based on velocity and direction.
        /// </summary>
        private bool DetectShot(AIContext context, bool isHomeGK, float goalLineX, float centerY)
        {
            float ballSpeed = context.BallVelocity.Length();
            if (ballSpeed < AIConstants.GKShotDetectionSpeed)
                return false;

            // Ball must be moving toward our goal
            bool movingTowardGoal = isHomeGK ? context.BallVelocity.X < -100f : context.BallVelocity.X > 100f;
            if (!movingTowardGoal)
                return false;

            // Predict where ball will cross the goal line
            float timeToGoalLine = (goalLineX - context.BallPosition.X) / context.BallVelocity.X;
            if (timeToGoalLine < 0 || timeToGoalLine > 2f)
                return false;

            float predictedY = context.BallPosition.Y + context.BallVelocity.Y * timeToGoalLine;
            float goalTop = centerY - AIConstants.GKGoalWidth / 2 - 50f;
            float goalBottom = centerY + AIConstants.GKGoalWidth / 2 + 50f;

            return predictedY >= goalTop && predictedY <= goalBottom;
        }

        /// <summary>
        /// Predicts where the ball will cross the goal line for interception.
        /// </summary>
        private Vector2 PredictBallInterceptPoint(AIContext context, float goalLineX, float centerY)
        {
            if (Math.Abs(context.BallVelocity.X) < 1f)
                return new Vector2(goalLineX, centerY);

            float timeToGoalLine = (goalLineX - context.BallPosition.X) / context.BallVelocity.X;
            float predictedY = context.BallPosition.Y + context.BallVelocity.Y * timeToGoalLine;

            float goalTop = centerY - AIConstants.GKGoalWidth / 2 + 30f;
            float goalBottom = centerY + AIConstants.GKGoalWidth / 2 - 30f;
            predictedY = MathHelper.Clamp(predictedY, goalTop, goalBottom);

            return new Vector2(goalLineX, predictedY);
        }
    }
}
