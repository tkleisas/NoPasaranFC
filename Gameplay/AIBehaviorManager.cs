using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Manages AI decision-making, context building, and ball actions (pass/shoot).
    /// Extracted from MatchEngine to keep AI logic centralized.
    /// </summary>
    public class AIBehaviorManager
    {
        private readonly MatchEngine _engine;

        public AIBehaviorManager(MatchEngine engine)
        {
            _engine = engine;
        }

        // --- Difficulty helpers (used by AI states via AIContext) ---

        /// <summary>Returns decision interval multiplier based on difficulty.</summary>
        public static float GetDecisionMultiplier()
        {
            int difficulty = GameSettings.Instance.Difficulty;
            return difficulty switch
            {
                0 => AIConstants.DifficultyEasyDecisionMult,
                2 => AIConstants.DifficultyHardDecisionMult,
                _ => AIConstants.DifficultyNormalDecisionMult
            };
        }

        /// <summary>Returns accuracy multiplier (lower = more accurate).</summary>
        public static float GetAccuracyMultiplier()
        {
            int difficulty = GameSettings.Instance.Difficulty;
            return difficulty switch
            {
                0 => AIConstants.DifficultyEasyAccuracyMult,
                2 => AIConstants.DifficultyHardAccuracyMult,
                _ => AIConstants.DifficultyNormalAccuracyMult
            };
        }

        /// <summary>Returns probability multiplier for pass/shoot decisions.</summary>
        public static float GetProbabilityMultiplier()
        {
            int difficulty = GameSettings.Instance.Difficulty;
            return difficulty switch
            {
                0 => AIConstants.DifficultyEasyProbMult,
                2 => AIConstants.DifficultyHardProbMult,
                _ => AIConstants.DifficultyNormalProbMult
            };
        }

        /// <summary>Returns positioning quality multiplier for lerp factors.</summary>
        public static float GetPositioningMultiplier()
        {
            int difficulty = GameSettings.Instance.Difficulty;
            return difficulty switch
            {
                0 => AIConstants.DifficultyEasyLerpMult,
                2 => AIConstants.DifficultyHardLerpMult,
                _ => AIConstants.DifficultyNormalLerpMult
            };
        }

        public AIContext BuildAIContext(Player player)
        {
            bool isHomeTeam = player.Team != null && player.Team == _engine.HomeTeam;
            var myTeam = isHomeTeam ? _engine.HomeTeam : _engine.AwayTeam;
            var opponentTeam = isHomeTeam ? _engine.AwayTeam : _engine.HomeTeam;

            Player nearestOpponent = null;
            float nearestOpponentDist = float.MaxValue;
            foreach (var opponent in opponentTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown))
            {
                float dist = Vector2.Distance(player.FieldPosition, opponent.FieldPosition);
                if (dist < nearestOpponentDist)
                {
                    nearestOpponentDist = dist;
                    nearestOpponent = opponent;
                }
            }

            Player nearestTeammate = null;
            float nearestTeammateDist = float.MaxValue;
            Player bestPassTarget = null;
            float bestPassScore = float.MinValue;

            var activeOpponents = opponentTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown).ToList();

            Vector2 opponentGoalCenter = isHomeTeam
                ? new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth, MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2)
                : new Vector2(MatchEngine.StadiumMargin, MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2);

            foreach (var teammate in myTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown && p != player))
            {
                float dist = Vector2.Distance(player.FieldPosition, teammate.FieldPosition);
                if (dist < nearestTeammateDist)
                {
                    nearestTeammateDist = dist;
                    nearestTeammate = teammate;
                }

                // Multi-factor pass target scoring
                float distToGoal = Vector2.Distance(teammate.FieldPosition, opponentGoalCenter);
                float passScore = 0f;

                // Goal progress (normalized by field dimensions to avoid dominating)
                float normalizedGoalDist = distToGoal / (MatchEngine.FieldWidth + 1f);
                passScore += (1f - normalizedGoalDist) * 600f;

                // Prefer teammates at medium distance (not too close, not too far)
                if (dist > AIConstants.MinPassDistance && dist < 1200f)
                    passScore += 300f;
                else if (dist >= 1200f && dist < AIConstants.MaxPassDistance)
                    passScore += 100f;
                else
                    passScore -= 200f;

                // Bonus for open/unmarked teammates
                float nearestDefDist = float.MaxValue;
                foreach (var opp in activeOpponents)
                {
                    float oppDist = Vector2.Distance(teammate.FieldPosition, opp.FieldPosition);
                    if (oppDist < nearestDefDist) nearestDefDist = oppDist;
                }
                if (nearestDefDist > 200f)
                    passScore += 400f;
                else if (nearestDefDist > 120f)
                    passScore += 200f;

                // Prefer forwards and attacking midfielders
                if (teammate.Position == PlayerPosition.Forward)
                    passScore += 250f;
                else if (teammate.Position == PlayerPosition.Midfielder &&
                    (teammate.Role == PlayerRole.AttackingMidfielder || teammate.Role == PlayerRole.LeftWinger || teammate.Role == PlayerRole.RightWinger))
                    passScore += 120f;

                // Penalty if pass path is blocked (reduced from extreme -3000)
                if (IsPathBlocked(player.FieldPosition, teammate.FieldPosition, activeOpponents, 40f))
                    passScore -= 1500f;

                if (passScore > bestPassScore)
                {
                    bestPassScore = passScore;
                    bestPassTarget = teammate;
                }
            }

            float distanceToBall = Vector2.Distance(player.FieldPosition, _engine.BallPosition);
            bool hasControl = _engine.LastPlayerTouchedBall == player && distanceToBall < 80f;
            bool shouldChaseBall = ShouldPlayerChaseBall(player);

            Vector2 ownGoalCenter = isHomeTeam
                ? new Vector2(MatchEngine.StadiumMargin, MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2)
                : new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth, MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2);

            Vector2 opponentGoalCenterFinal = isHomeTeam
                ? new Vector2(MatchEngine.StadiumMargin + MatchEngine.FieldWidth, MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2)
                : new Vector2(MatchEngine.StadiumMargin, MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2);

            bool ballInDefensiveHalf = _engine.IsBallInHalf(player.Team.Name);

            return new AIContext
            {
                BallPosition = _engine.BallPosition,
                BallVelocity = _engine.BallVelocity,
                BallHeight = _engine.BallHeight,
                NearestOpponent = nearestOpponent,
                NearestTeammate = nearestTeammate,
                BestPassTarget = bestPassTarget,
                DistanceToBall = distanceToBall,
                HasBallPossession = hasControl,
                OpponentGoalCenter = opponentGoalCenterFinal,
                OwnGoalCenter = ownGoalCenter,
                IsPlayerTeam = player.Team != null && player.Team.IsPlayerControlled,
                IsHomeTeam = isHomeTeam,
                Random = _engine.SharedRandom,
                ClosestToBall = GetPlayerClosestToBall(),
                ShouldChaseBall = shouldChaseBall,
                MatchTime = _engine.MatchTime,
                TimeSinceKickoff = _engine.TimeSinceKickoff,
                IsDefensiveHalf = ballInDefensiveHalf,
                IsAttackingHalf = !ballInDefensiveHalf,
                Teammates = myTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown && p != player).ToList(),
                Opponents = opponentTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown).ToList()
            };
        }

        public bool IsPathBlocked(Vector2 start, Vector2 end, List<Player> obstacles, float threshold = 50f)
        {
            Vector2 direction = end - start;
            float distance = direction.Length();
            if (distance < 0.01f) return false;

            direction.Normalize();

            foreach (var obstacle in obstacles)
            {
                Vector2 toObstacle = obstacle.FieldPosition - start;
                float projection = Vector2.Dot(toObstacle, direction);

                if (projection > 0 && projection < distance)
                {
                    Vector2 closestPoint = start + direction * projection;
                    float distToLine = Vector2.Distance(obstacle.FieldPosition, closestPoint);
                    if (distToLine < threshold)
                        return true;
                }
            }
            return false;
        }

        public void AIPassBall(Player passer, Vector2 targetPosition, float power)
        {
            if (_engine.BallHeight >= 100f) return;

            Vector2 passDirection = targetPosition - _engine.BallPosition;
            float passDistance = passDirection.Length();
            if (passDistance <= 0) return;

            passDirection.Normalize();

            bool passerIsHomeTeam = _engine.HomeTeam.Players.Any(p => p.Id == passer.Id);
            Team opposingTeam = passerIsHomeTeam ? _engine.AwayTeam : _engine.HomeTeam;

            bool needsLoftedPass = false;
            int defendersInPath = 0;

            foreach (var opponent in opposingTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown))
            {
                Vector2 toOpponent = opponent.FieldPosition - _engine.BallPosition;
                float dotProduct = Vector2.Dot(toOpponent, passDirection);

                if (dotProduct > 0 && dotProduct < passDistance)
                {
                    Vector2 projectedPoint = _engine.BallPosition + passDirection * dotProduct;
                    float perpDistance = Vector2.Distance(opponent.FieldPosition, projectedPoint);
                    if (perpDistance < 150f)
                        defendersInPath++;
                }
            }

            // Technique stat improves lofted pass decision (high Technique = smarter choice)
            float techniqueInfluence = passer.Technique / AIConstants.MaxStatValue;
            needsLoftedPass = defendersInPath >= 2 || passDistance > 800f;
            // Low Technique players sometimes loft unnecessarily or fail to loft when needed
            if (techniqueInfluence < 0.5f && _engine.SharedRandom.NextDouble() < 0.2)
                needsLoftedPass = !needsLoftedPass;

            // Passing stat affects accuracy: add direction error inversely proportional to stat
            float passStatRatio = passer.Passing / AIConstants.MaxStatValue;
            float maxError = AIConstants.PassMaxDirectionError * (1f - passStatRatio * AIConstants.PassStatInfluence);
            maxError *= GetAccuracyMultiplier();
            float directionError = (float)(_engine.SharedRandom.NextDouble() - 0.5) * 2f * maxError;

            // Rotate pass direction by error angle
            float cos = (float)Math.Cos(directionError);
            float sin = (float)Math.Sin(directionError);
            Vector2 adjustedDirection = new Vector2(
                passDirection.X * cos - passDirection.Y * sin,
                passDirection.X * sin + passDirection.Y * cos);

            float passPower = (passer.Passing / 10f + power * 5f) * _engine.GetStaminaStatMultiplier(passer);
            _engine.BallVelocity = adjustedDirection * passPower * passer.Speed;

            if (needsLoftedPass)
            {
                _engine.BallVerticalVelocity = 200f + (passDistance / 10f);
                _engine.BallVelocity *= 0.85f;
            }
            else
            {
                _engine.BallVerticalVelocity = 30f;
            }

            _engine.LastPlayerTouchedBall = passer;
            passer.LastKickTime = (float)_engine.MatchTime;
            AudioManager.Instance.PlaySoundEffect("kick_ball", needsLoftedPass ? 0.6f : 0.4f, allowRetrigger: false);
        }

        public void AIShootBall(Player shooter, Vector2 targetPosition, float power)
        {
            if (_engine.BallHeight >= 100f) return;

            Vector2 shootDirection = targetPosition - _engine.BallPosition;
            if (shootDirection.LengthSquared() <= 0) return;

            shootDirection.Normalize();

            // Shooting stat affects accuracy: better shooters have tighter spread
            float shootStatRatio = shooter.Shooting / AIConstants.MaxStatValue;
            float offsetReduction = shootStatRatio * AIConstants.ShotStatInfluence;
            float maxOffset = AIConstants.ShotBaseOffset * (1f - offsetReduction) * GetAccuracyMultiplier();

            // Apply Y offset (perpendicular to shot direction)
            float yOffset = (float)(_engine.SharedRandom.NextDouble() - 0.5) * maxOffset;
            Vector2 perpendicular = new Vector2(-shootDirection.Y, shootDirection.X);
            Vector2 adjustedTarget = targetPosition + perpendicular * yOffset;
            Vector2 adjustedDirection = adjustedTarget - _engine.BallPosition;
            if (adjustedDirection.LengthSquared() > 0)
                adjustedDirection.Normalize();
            else
                adjustedDirection = shootDirection;

            float shootPower = (shooter.Shooting / 8f + power * 10f) * _engine.GetStaminaStatMultiplier(shooter);
            _engine.BallVelocity = adjustedDirection * shootPower * shooter.Speed;
            _engine.BallVerticalVelocity = 100f + (float)_engine.SharedRandom.NextDouble() * 200f;
            _engine.LastPlayerTouchedBall = shooter;
            shooter.LastKickTime = (float)_engine.MatchTime;
            AudioManager.Instance.PlaySoundEffect("kick_ball", 0.7f, allowRetrigger: false);
        }

        public float GetAIDifficultyModifier()
        {
            int difficulty = GameSettings.Instance.Difficulty;
            return difficulty switch
            {
                0 => 0.7f,
                1 => 1.0f,
                2 => 1.3f,
                _ => 1.0f
            };
        }

        public float GetAIReactionTimeMultiplier()
        {
            int difficulty = GameSettings.Instance.Difficulty;
            return difficulty switch
            {
                0 => 1.5f,
                1 => 1.0f,
                2 => 0.7f,
                _ => 1.0f
            };
        }

        public bool ShouldPlayerChaseBall(Player player)
        {
            // GK never chases ball via this method — GoalkeeperState handles its own ball pursuit
            if (player.Position == PlayerPosition.Goalkeeper)
                return false;

            var team = player.Team;
            var activeTeammates = team.Players
                .Where(p => p.IsStarting && !p.IsKnockedDown && p.Position != PlayerPosition.Goalkeeper)
                .ToList();

            var teamDistances = activeTeammates
                .Select(p => new { Player = p, Distance = Vector2.Distance(p.FieldPosition, _engine.BallPosition) })
                .OrderBy(x => x.Distance)
                .ToList();

            int playerRank = teamDistances.FindIndex(x => x.Player == player);

            // Primary chaser: closest player always chases
            if (playerRank == 0)
                return true;

            // Support chaser: 2nd closest chases if they're close enough to the ball
            if (playerRank == 1 && teamDistances[1].Distance < 400f)
                return true;

            return false;
        }

        public Player GetPlayerClosestToBall()
        {
            Player closest = null;
            float closestDist = float.MaxValue;

            foreach (var player in _engine.HomeTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown)
                .Concat(_engine.AwayTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown)))
            {
                float dist = Vector2.Distance(player.FieldPosition, _engine.BallPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = player;
                }
            }
            return closest;
        }

        public Vector2 ApplyTeammateAvoidance(Player player, Vector2 desiredDirection)
        {
            var team = player.Team;
            Vector2 separationForce = Vector2.Zero;
            int nearbyCount = 0;

            foreach (var teammate in team.Players.Where(p => p.IsStarting && p != player && !p.IsKnockedDown))
            {
                float distanceToTeammate = Vector2.Distance(player.FieldPosition, teammate.FieldPosition);

                if (distanceToTeammate < AIConstants.PlayerPersonalSpace && distanceToTeammate > 0.1f)
                {
                    Vector2 awayFromTeammate = player.FieldPosition - teammate.FieldPosition;
                    awayFromTeammate.Normalize();
                    float strength = 1f - (distanceToTeammate / AIConstants.PlayerPersonalSpace);
                    separationForce += awayFromTeammate * strength;
                    nearbyCount++;
                }
            }

            if (nearbyCount > 0)
            {
                separationForce /= nearbyCount;
                Vector2 blendedDirection = desiredDirection * 0.7f + separationForce * 0.3f;
                if (blendedDirection.Length() > 0.01f)
                {
                    blendedDirection.Normalize();
                    return blendedDirection;
                }
            }
            return desiredDirection;
        }
    }
}
