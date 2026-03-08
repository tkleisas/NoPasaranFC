using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class DribblingState : AIState
    {
        private float _decisionTimer = 0f;
        private float _effectiveDecisionInterval;
        private bool _isOrbiting = false;
        private float _orbitTimer = 0f;
        private float _orbitDurationVariation = 0f;
        private Vector2 _trajectoryBallPosition = Vector2.Zero;
        private TrajectoryPath _orbitTrajectory;

        public DribblingState()
        {
            Type = AIStateType.Dribbling;
            _effectiveDecisionInterval = AIConstants.DecisionInterval;
        }

        public override void Enter(Player player, AIContext context)
        {
            _decisionTimer = 0f;

            // Decision interval: Agility makes decisions faster, difficulty also affects
            float agilityRatio = player.Agility / AIConstants.MaxStatValue;
            float agilitySpeedup = 1f - agilityRatio * 0.3f; // Up to 30% faster at max Agility
            _effectiveDecisionInterval = AIConstants.DecisionInterval * agilitySpeedup * AIBehaviorManager.GetDecisionMultiplier();

            float distToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
            if (distToBall > 200f)
            {
                _isOrbiting = false;
                _orbitTimer = 0f;
                if (context.PlayerRandom != null)
                    _orbitDurationVariation = (float)(context.PlayerRandom.NextDouble() - 0.5) * AIConstants.OrbitVariationRange;
            }
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            _decisionTimer += deltaTime;
            if (_orbitTimer > 0) _orbitTimer -= deltaTime;

            if (!context.HasBallPossession)
                return AIStateType.ChasingBall;

            // Boundary repulsion while dribbling
            AIStateType? boundaryResult = HandleBoundaryRepulsion(player);
            if (boundaryResult.HasValue)
                return boundaryResult.Value;

            // Decision making on timer (scaled by Agility and difficulty)
            if (_decisionTimer >= _effectiveDecisionInterval)
            {
                _decisionTimer = 0f;

                AIStateType? passResult = EvaluatePassOpportunity(player, context);
                if (passResult.HasValue)
                    return passResult.Value;

                AIStateType? shootResult = EvaluateShootOpportunity(player, context);
                if (shootResult.HasValue)
                    return shootResult.Value;
            }

            // Movement toward goal
            Vector2 desiredKickDirection = CalculateKickDirection(player, context);
            if (desiredKickDirection.LengthSquared() > 0)
            {
                desiredKickDirection.Normalize();

                player.AITargetPosition = context.OpponentGoalCenter;
                player.AITargetPositionSet = true;

                float distToBall = Vector2.Distance(player.FieldPosition, context.BallPosition);
                Vector2 playerToBall = context.BallPosition - player.FieldPosition;
                float dotProduct = Vector2.Dot(desiredKickDirection, playerToBall);
                bool playerAheadOfBall = dotProduct < 0;

                // Orbit logic when ahead of ball
                AIStateType? orbitResult = HandleOrbitMovement(player, context, desiredKickDirection, playerAheadOfBall, dotProduct);
                if (orbitResult.HasValue)
                    return orbitResult.Value;

                // Normal dribble/chase movement
                ExecuteDribbleMovement(player, context, desiredKickDirection, distToBall);
            }

            return AIStateType.Dribbling;
        }

        public override void Exit(Player player, AIContext context)
        {
            _decisionTimer = 0f;
        }

        private AIStateType? HandleBoundaryRepulsion(Player player)
        {
            Vector2 repulsion = Vector2.Zero;
            float margin = AIConstants.DribbleBoundaryMargin;

            if (player.FieldPosition.X < MatchEngine.StadiumMargin + margin) repulsion.X = 1f;
            if (player.FieldPosition.X > MatchEngine.TotalWidth - MatchEngine.StadiumMargin - margin) repulsion.X = -1f;
            if (player.FieldPosition.Y < MatchEngine.StadiumMargin + margin) repulsion.Y = 1f;
            if (player.FieldPosition.Y > MatchEngine.TotalHeight - MatchEngine.StadiumMargin - margin) repulsion.Y = -1f;

            if (repulsion.LengthSquared() > 0)
            {
                repulsion.Normalize();
                player.Velocity = repulsion * (player.Speed * AIConstants.BaseSpeedMultiplier);
                return AIStateType.Dribbling;
            }
            return null;
        }

        private AIStateType? EvaluatePassOpportunity(Player player, AIContext context)
        {
            Player passTarget = context.BestPassTarget ?? context.NearestTeammate;
            if (passTarget == null)
                return null;

            float distanceToGoal = Vector2.Distance(player.FieldPosition, context.OpponentGoalCenter);
            float distToTeammate = Vector2.Distance(player.FieldPosition, passTarget.FieldPosition);
            float teammateDistToGoal = Vector2.Distance(passTarget.FieldPosition, context.OpponentGoalCenter);
            float myDistToGoal = distanceToGoal;

            bool teammateAheadOfMe = teammateDistToGoal < myDistToGoal - 30f;
            bool validPassRange = distToTeammate > AIConstants.MinPassDistance && distToTeammate < AIConstants.MaxPassDistance;

            // Difficulty scales decision quality
            float probMult = AIBehaviorManager.GetProbabilityMultiplier();

            bool underPressure = false;
            if (context.NearestOpponent != null)
            {
                float distToOpponent = Vector2.Distance(player.FieldPosition, context.NearestOpponent.FieldPosition);
                underPressure = distToOpponent < AIConstants.PressureDistance;
            }

            // Always pass under pressure (unless in shooting range)
            if (underPressure && validPassRange && distanceToGoal > AIConstants.ShootCloseDistance)
                return AIStateType.Passing;

            bool isDefender = player.Position == PlayerPosition.Defender;
            bool isMidfielder = player.Position == PlayerPosition.Midfielder;
            bool isForward = player.Position == PlayerPosition.Forward;
            bool isTeammateForward = passTarget.Position == PlayerPosition.Forward;

            if (isDefender && validPassRange)
            {
                if (teammateAheadOfMe && context.Random.NextDouble() < AIConstants.DefenderForwardPassChance * probMult)
                    return AIStateType.Passing;
                if (context.Random.NextDouble() < AIConstants.DefenderLateralPassChance * probMult)
                    return AIStateType.Passing;
            }

            if (isMidfielder && validPassRange)
            {
                if (isTeammateForward && teammateAheadOfMe && distToTeammate > 400f)
                    return AIStateType.Passing;
                if (teammateAheadOfMe && context.Random.NextDouble() < AIConstants.MidfielderForwardPassChance * probMult)
                    return AIStateType.Passing;
                if (context.Random.NextDouble() < AIConstants.MidfielderLateralPassChance * probMult)
                    return AIStateType.Passing;
            }

            if (isForward && validPassRange)
            {
                if (distanceToGoal < 350f && context.Random.NextDouble() < 0.85 * probMult)
                    return AIStateType.Shooting;
                if (teammateAheadOfMe && (myDistToGoal - teammateDistToGoal) > 150f
                    && context.Random.NextDouble() < AIConstants.ForwardPassWhenTeammateCloserChance * probMult)
                    return AIStateType.Passing;
                if (context.Random.NextDouble() < AIConstants.ForwardDefaultPassChance * probMult)
                    return AIStateType.Passing;
            }

            return null;
        }

        private AIStateType? EvaluateShootOpportunity(Player player, AIContext context)
        {
            float distanceToGoal = Vector2.Distance(player.FieldPosition, context.OpponentGoalCenter);
            float probMult = AIBehaviorManager.GetProbabilityMultiplier();

            if (distanceToGoal < AIConstants.ShootAlwaysDistance)
                return AIStateType.Shooting;
            if (distanceToGoal < AIConstants.ShootCloseDistance && context.Random.NextDouble() < AIConstants.ShootCloseChance * probMult)
                return AIStateType.Shooting;
            if (distanceToGoal < AIConstants.ShootMediumDistance && context.Random.NextDouble() < AIConstants.ShootMediumChance * probMult)
                return AIStateType.Shooting;
            if (distanceToGoal < AIConstants.ShootLongDistance && context.Random.NextDouble() < AIConstants.ShootLongChance * probMult)
                return AIStateType.Shooting;
            if (distanceToGoal < AIConstants.ShootVeryLongDistance && context.Random.NextDouble() < AIConstants.ShootVeryLongChance * probMult)
                return AIStateType.Shooting;

            return null;
        }

        private Vector2 CalculateKickDirection(Player player, AIContext context)
        {
            Vector2 direction = context.OpponentGoalCenter - player.FieldPosition;

            // Goal entry: aim deep when close and aligned
            float goalTop = MatchEngine.StadiumMargin + (MatchEngine.FieldHeight - MatchEngine.GoalWidth) / 2;
            float goalBottom = goalTop + MatchEngine.GoalWidth;
            bool inGoalWidth = player.FieldPosition.Y >= goalTop && player.FieldPosition.Y <= goalBottom;

            if (inGoalWidth && Vector2.Distance(player.FieldPosition, context.OpponentGoalCenter) < 300f)
            {
                Vector2 deepTarget = context.OpponentGoalCenter
                    + Vector2.Normalize(context.OpponentGoalCenter - context.OwnGoalCenter) * 100f;
                direction = deepTarget - player.FieldPosition;
            }

            // Boundary escape in defensive half
            float escapeMargin = AIConstants.DribbleEscapeMargin;
            bool nearBoundary = context.BallPosition.X < MatchEngine.StadiumMargin + escapeMargin
                || context.BallPosition.X > MatchEngine.TotalWidth - MatchEngine.StadiumMargin - escapeMargin
                || context.BallPosition.Y < MatchEngine.StadiumMargin + escapeMargin
                || context.BallPosition.Y > MatchEngine.TotalHeight - MatchEngine.StadiumMargin - escapeMargin;

            if (nearBoundary && context.IsDefensiveHalf)
            {
                Vector2 escapeDir = Vector2.Zero;
                if (context.BallPosition.X < MatchEngine.StadiumMargin + escapeMargin) escapeDir.X = 1f;
                if (context.BallPosition.X > MatchEngine.TotalWidth - MatchEngine.StadiumMargin - escapeMargin) escapeDir.X = -1f;
                if (context.BallPosition.Y < MatchEngine.StadiumMargin + escapeMargin) escapeDir.Y = 1f;
                if (context.BallPosition.Y > MatchEngine.TotalHeight - MatchEngine.StadiumMargin - escapeMargin) escapeDir.Y = -1f;

                if (escapeDir.LengthSquared() > 0)
                {
                    escapeDir.Normalize();
                    if (direction.LengthSquared() > 0)
                    {
                        direction = Vector2.Normalize(direction) + escapeDir;
                        if (direction.LengthSquared() > 0)
                            direction.Normalize();
                    }
                }
            }

            return direction;
        }

        private AIStateType? HandleOrbitMovement(Player player, AIContext context,
            Vector2 desiredDirection, bool playerAheadOfBall, float dotProduct)
        {
            if (!playerAheadOfBall && !_isOrbiting)
                return null;

            // Invalidate trajectory if ball moved significantly
            if (_isOrbiting && _orbitTrajectory != null)
            {
                float ballMovement = Vector2.Distance(context.BallPosition, _trajectoryBallPosition);
                if (ballMovement > AIConstants.OrbitBallMovementInvalidation)
                {
                    _orbitTrajectory = null;
                    _isOrbiting = false;
                }
            }

            // Start orbiting
            if (playerAheadOfBall && !_isOrbiting)
            {
                _isOrbiting = true;
                _orbitTimer = AIConstants.OrbitBaseTime + _orbitDurationVariation;
                _trajectoryBallPosition = context.BallPosition;

                _orbitTrajectory = TrajectoryGenerator.GenerateOrbitArc(
                    context.BallPosition, player.FieldPosition,
                    AIConstants.OrbitArcAngle, AIConstants.OrbitWaypoints,
                    (float)context.MatchTime);
            }

            // Exit orbit conditions — use OR logic so any condition allows early exit
            bool trajectoryComplete = _orbitTrajectory == null || _orbitTrajectory.IsComplete(player.FieldPosition);
            if ((dotProduct > AIConstants.OrbitExitDotProduct) || (_orbitTimer <= 0f) || trajectoryComplete)
            {
                _isOrbiting = false;
                _orbitTrajectory = null;
                return null;
            }

            if (_isOrbiting && _orbitTrajectory != null)
            {
                Vector2 velocity = FollowOrbitTrajectory(player, (float)context.MatchTime);
                if (velocity.LengthSquared() > 0)
                {
                    player.Velocity = velocity;
                    return AIStateType.Dribbling;
                }

                // Trajectory ended - seek behind ball
                Vector2 toGoal = context.OpponentGoalCenter - context.BallPosition;
                if (toGoal.LengthSquared() > 0)
                {
                    toGoal.Normalize();
                    Vector2 targetPos = context.BallPosition - (toGoal * 150f);
                    Vector2 seekDir = targetPos - player.FieldPosition;
                    if (seekDir.LengthSquared() > 0)
                    {
                        seekDir.Normalize();
                        player.Velocity = seekDir * (player.Speed * AIConstants.OrbitSpeedMultiplier);
                    }
                }
                return AIStateType.Dribbling;
            }

            return null;
        }

        private void ExecuteDribbleMovement(Player player, AIContext context, Vector2 desiredDirection, float distToBall)
        {
            if (distToBall < AIConstants.DribbleCloseDistance)
            {
                // Close to ball: steer with opponent avoidance
                Vector2 goalForce = desiredDirection;
                Vector2 avoidanceForce = Vector2.Zero;

                if (context.NearestOpponent != null)
                {
                    float distToOpponent = Vector2.Distance(player.FieldPosition, context.NearestOpponent.FieldPosition);
                    if (distToOpponent < AIConstants.OpponentAvoidanceDistance)
                    {
                        Vector2 fromOpponent = player.FieldPosition - context.NearestOpponent.FieldPosition;
                        if (fromOpponent.LengthSquared() > 0)
                        {
                            fromOpponent.Normalize();
                            float strength = 1.0f - (distToOpponent / AIConstants.OpponentAvoidanceDistance);
                            avoidanceForce = fromOpponent * strength * 1.5f;
                        }
                    }
                }

                Vector2 finalDirection = goalForce + avoidanceForce;
                if (finalDirection.LengthSquared() > 0)
                    finalDirection.Normalize();
                else
                    finalDirection = goalForce;

                player.Velocity = finalDirection * (player.Speed * AIConstants.BaseSpeedMultiplier);
            }
            else
            {
                // Far from ball: chase it
                Vector2 toBall = context.BallPosition - player.FieldPosition;
                if (toBall.LengthSquared() > 0)
                    toBall.Normalize();
                player.Velocity = toBall * (player.Speed * AIConstants.BaseSpeedMultiplier);
            }
        }

        /// <summary>
        /// Follows the orbit trajectory, returning velocity vector.
        /// Self-contained trajectory following for the dribbling orbit.
        /// </summary>
        private Vector2 FollowOrbitTrajectory(Player player, float currentTime)
        {
            if (_orbitTrajectory == null || !_orbitTrajectory.IsValid(currentTime))
                return Vector2.Zero;

            _orbitTrajectory.UpdateProgress(player.FieldPosition, 20f);
            Vector2 target = _orbitTrajectory.GetCurrentTarget(player.FieldPosition);
            Vector2 direction = target - player.FieldPosition;

            if (direction.LengthSquared() > 0)
            {
                direction.Normalize();
                return direction * (player.Speed * AIConstants.OrbitSpeedMultiplier);
            }

            return Vector2.Zero;
        }
    }
}
