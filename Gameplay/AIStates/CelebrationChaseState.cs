using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    public class CelebrationChaseState : AIState
    {
        private Player _targetPlayer = null;
        private int _positionInLine = 0; // 0 = closest to scorer, 1 = second, etc.
        private const float DesiredSpacing = 200f; // Distance between players in the line
        private List<Player> _allFollowers = new List<Player>();

        public CelebrationChaseState()
        {
            Type = AIStateType.CelebrationChase;
        }

        public void SetTarget(Player target)
        {
            _targetPlayer = target;
        }

        public void SetPositionInLine(int position, List<Player> allFollowers)
        {
            _positionInLine = position;
            _allFollowers = allFollowers;
        }

        public override void Enter(Player player, AIContext context)
        {
            // Target will be set externally by MatchEngine
        }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            if (_targetPlayer == null)
            {
                // No target, just stay idle
                player.Velocity = Vector2.Zero;
                return AIStateType.CelebrationChase;
            }

            // Determine who to follow
            Player followTarget = _targetPlayer;
            float desiredDistance = DesiredSpacing;

            // If not the first in line, follow the player ahead
            if (_positionInLine > 0 && _positionInLine - 1 < _allFollowers.Count)
            {
                followTarget = _allFollowers[_positionInLine - 1];
            }

            // Calculate desired position (behind the follow target)
            Vector2 toTarget = followTarget.FieldPosition - player.FieldPosition;
            float distanceToTarget = toTarget.Length();

            Vector2 desiredVelocity = Vector2.Zero;

            // Maintain desired spacing
            if (distanceToTarget > desiredDistance + 50f)
            {
                // Too far behind - speed up
                if (toTarget.LengthSquared() > 0)
                {
                    toTarget.Normalize();
                    desiredVelocity = toTarget * (player.Speed * 3.5f);
                }
            }
            else if (distanceToTarget < desiredDistance - 50f)
            {
                // Too close - slow down or back off slightly
                if (toTarget.LengthSquared() > 0)
                {
                    toTarget.Normalize();
                    desiredVelocity = toTarget * (player.Speed * 1.5f);
                }
            }
            else
            {
                // Maintain current distance - match target's velocity
                if (toTarget.LengthSquared() > 0)
                {
                    toTarget.Normalize();
                    desiredVelocity = toTarget * (player.Speed * 3.0f);
                }
            }

            player.Velocity = desiredVelocity;

            return AIStateType.CelebrationChase;
        }

        public override void Exit(Player player, AIContext context)
        {
            _targetPlayer = null;
            _allFollowers.Clear();
        }
    }
}
