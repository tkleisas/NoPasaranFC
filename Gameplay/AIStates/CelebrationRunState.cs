using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class CelebrationRunState : AIState
    {
        private List<Vector2> _pathPoints;
        private int _currentTargetIndex = 0;
        private float _celebrationTime = 0f;

        // Store position history for followers
        private Queue<Vector2> _positionHistory = new Queue<Vector2>();
        private const int MaxHistorySize = 100; // Keep last 100 positions

        public CelebrationRunState()
        {
            Type = AIStateType.Celebration;
        }

        public override void Enter(Player player, AIContext context)
        {
            _celebrationTime = 0f;
            _currentTargetIndex = 0;
            _positionHistory.Clear();

            // Create a path that follows the pitch boundaries (counter-clockwise)
            _pathPoints = new List<Vector2>();

            float margin = MatchEngine.StadiumMargin + 200f; // Run just inside the boundaries
            float fieldWidth = MatchEngine.FieldWidth;
            float fieldHeight = MatchEngine.FieldHeight;

            // Find closest corner to start from
            Vector2 topLeft = new Vector2(margin, margin);
            Vector2 topRight = new Vector2(margin + fieldWidth, margin);
            Vector2 bottomRight = new Vector2(margin + fieldWidth, margin + fieldHeight);
            Vector2 bottomLeft = new Vector2(margin, margin + fieldHeight);

            // Determine which corner to start from (closest to player)
            List<Vector2> corners = new List<Vector2> { topLeft, topRight, bottomRight, bottomLeft };
            float minDist = float.MaxValue;
            int startCornerIndex = 0;

            for (int i = 0; i < corners.Count; i++)
            {
                float dist = Vector2.Distance(player.FieldPosition, corners[i]);
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
            _currentTargetIndex = 0;
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            _celebrationTime += deltaTime;

            // Record position for followers
            _positionHistory.Enqueue(player.FieldPosition);
            if (_positionHistory.Count > MaxHistorySize)
            {
                _positionHistory.Dequeue();
            }

            // Get current target point
            Vector2 targetPoint = _pathPoints[_currentTargetIndex];
            Vector2 toTarget = targetPoint - player.FieldPosition;
            float distToTarget = toTarget.Length();

            // Move to next waypoint if close enough
            if (distToTarget < 150f)
            {
                _currentTargetIndex = (_currentTargetIndex + 1) % _pathPoints.Count;
                targetPoint = _pathPoints[_currentTargetIndex];
                toTarget = targetPoint - player.FieldPosition;
            }

            // Move toward target
            Vector2 direction = Vector2.Zero;
            if (toTarget.LengthSquared() > 0)
            {
                direction = Vector2.Normalize(toTarget);
            }

            // Run fast during celebration!
            float celebrationSpeed = player.Speed * 3.5f;
            player.Velocity = direction * celebrationSpeed;

            return AIStateType.Celebration;
        }

        public override void Exit(Player player, AIContext context)
        {
            _celebrationTime = 0f;
            _positionHistory.Clear();
        }

        // Allow followers to query the path history
        public Queue<Vector2> GetPositionHistory()
        {
            return _positionHistory;
        }
    }
}
