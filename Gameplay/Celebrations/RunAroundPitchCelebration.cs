using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Gameplay.Celebrations
{
    /// <summary>
    /// Celebration where the scorer runs around the pitch boundaries
    /// with teammates following in a line formation
    /// </summary>
    public class RunAroundPitchCelebration : CelebrationBase
    {
        // Scorer's path data
        private List<Vector2> _pathPoints;
        private int _currentTargetIndex = 0;
        private const float CelebrationSpeed = 350f; // Fixed speed for all players (pixels per second)

        // Teammate formation data
        private const float DesiredSpacing = 200f;
        private const float CatchUpSpeed = 420f; // Faster fixed speed to catch up to formation

        // Audio tracking
        private bool _halfwayCheerPlayed = false;
        private bool _completionCheerPlayed = false;

        public override string CelebrationId => "run_around_pitch";
        public override string Name => "Victory Lap";
        public override string Description => "Scorer runs around the pitch with teammates following in a line";

        public override void Initialize(Player scorer, List<Player> teammates, List<Player> opponents)
        {
            _currentTargetIndex = 0;

            // Create a path that follows the pitch boundaries (counter-clockwise)
            _pathPoints = new List<Vector2>();

            float margin = MatchEngine.StadiumMargin + 200f; // Run just inside the boundaries
            float fieldWidth = MatchEngine.FieldWidth;
            float fieldHeight = MatchEngine.FieldHeight;

            // Define corners
            Vector2 topLeft = new Vector2(margin, margin);
            Vector2 topRight = new Vector2(margin + fieldWidth, margin);
            Vector2 bottomRight = new Vector2(margin + fieldWidth, margin + fieldHeight);
            Vector2 bottomLeft = new Vector2(margin, margin + fieldHeight);

            // Find closest corner to scorer to start from
            List<Vector2> corners = new List<Vector2> { topLeft, topRight, bottomRight, bottomLeft };
            float minDist = float.MaxValue;
            int startCornerIndex = 0;

            for (int i = 0; i < corners.Count; i++)
            {
                float dist = Vector2.Distance(scorer.FieldPosition, corners[i]);
                if (dist < minDist)
                {
                    minDist = dist;
                    startCornerIndex = i;
                }
            }

            // Build path starting from closest corner (counter-clockwise)
            for (int i = 0; i < 4; i++)
            {
                int cornerIndex = (startCornerIndex + i) % 4;
                _pathPoints.Add(corners[cornerIndex]);
            }

            // Add intermediate points between corners for smoother path
            List<Vector2> smoothPath = new List<Vector2>();
            for (int i = 0; i < _pathPoints.Count; i++)
            {
                Vector2 current = _pathPoints[i];
                Vector2 next = _pathPoints[(i + 1) % _pathPoints.Count];

                smoothPath.Add(current);

                // Add 3 points between corners
                for (int j = 1; j < 4; j++)
                {
                    float t = j / 4f;
                    smoothPath.Add(Vector2.Lerp(current, next, t));
                }
            }

            _pathPoints = smoothPath;

            // Reset audio flags
            _halfwayCheerPlayed = false;
            _completionCheerPlayed = false;
        }

        public override void UpdateScorer(Player scorer, float deltaTime, float celebrationTime)
        {
            // Set celebration animation (arms extended)
            scorer.CurrentAnimationState = "celebrate";

            // Get current target point
            Vector2 targetPoint = _pathPoints[_currentTargetIndex];
            Vector2 toTarget = targetPoint - scorer.FieldPosition;
            float distToTarget = toTarget.Length();

            // Move to next waypoint if close enough
            if (distToTarget < 150f)
            {
                _currentTargetIndex = (_currentTargetIndex + 1) % _pathPoints.Count;
                targetPoint = _pathPoints[_currentTargetIndex];
                toTarget = targetPoint - scorer.FieldPosition;
            }

            // Move toward target
            Vector2 direction = Vector2.Zero;
            if (toTarget.LengthSquared() > 0)
            {
                direction = Vector2.Normalize(toTarget);
            }

            // Set velocity to fixed speed (same for all players)
            scorer.Velocity = direction * CelebrationSpeed;
        }

        public override void UpdateTeammate(Player teammate, Player scorer, List<Player> allTeammates,
            int teammateIndex, float deltaTime, float celebrationTime)
        {
            // Set celebration animation (arms extended)
            teammate.CurrentAnimationState = "celebrate";

            // Determine who to follow
            Player followTarget = scorer;

            // If not the first in line, follow the player ahead
            if (teammateIndex > 0 && teammateIndex - 1 < allTeammates.Count)
            {
                followTarget = allTeammates[teammateIndex - 1];
            }

            // Calculate desired position (behind the follow target)
            Vector2 toTarget = followTarget.FieldPosition - teammate.FieldPosition;
            float distanceToTarget = toTarget.Length();

            Vector2 desiredVelocity = Vector2.Zero;

            // Maintain desired spacing using fixed speeds
            if (distanceToTarget > DesiredSpacing + 50f)
            {
                // Too far behind - speed up to catch up
                if (toTarget.LengthSquared() > 0)
                {
                    toTarget.Normalize();
                    desiredVelocity = toTarget * CatchUpSpeed;
                }
            }
            else if (distanceToTarget < DesiredSpacing - 50f)
            {
                // Too close - slow down to avoid overtaking
                if (toTarget.LengthSquared() > 0)
                {
                    toTarget.Normalize();
                    desiredVelocity = toTarget * (CelebrationSpeed * 0.6f);
                }
            }
            else
            {
                // Perfect spacing - maintain same fixed speed as formation
                if (toTarget.LengthSquared() > 0)
                {
                    toTarget.Normalize();
                    desiredVelocity = toTarget * CelebrationSpeed;
                }
            }

            teammate.Velocity = desiredVelocity;
        }

        public override void UpdateOpponent(Player opponent, float deltaTime, float celebrationTime)
        {
            // Opponents stay idle
            opponent.Velocity = Vector2.Zero;
        }

        public override void PlayAudio(float celebrationTime)
        {
            // Play cheer halfway through the lap (around 10-15 seconds)
            if (!_halfwayCheerPlayed && celebrationTime >= 10f && celebrationTime < 10.1f)
            {
                AudioManager.Instance.PlaySoundEffect("crowd_cheer", 0.9f, allowRetrigger: false);
                _halfwayCheerPlayed = true;
            }

            // Play another cheer when completing the lap (around 25-30 seconds)
            if (!_completionCheerPlayed && celebrationTime >= 25f && celebrationTime < 25.1f)
            {
                AudioManager.Instance.PlaySoundEffect("crowd_cheer", 1.0f, allowRetrigger: false);
                _completionCheerPlayed = true;
            }
        }

        public override void Cleanup()
        {
            _pathPoints?.Clear();
            _currentTargetIndex = 0;
        }
    }
}
