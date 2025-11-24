using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay
{
    public enum AIStateType
    {
        Idle,
        Positioning,
        ChasingBall,
        Dribbling,
        Passing,
        Shooting,
        Defending,
        AvoidingSideline
    }

    public abstract class AIState
    {
        public AIStateType Type { get; protected set; }
        
        public abstract void Enter(Player player, AIContext context);
        public abstract AIStateType Update(Player player, AIContext context, float deltaTime);
        public abstract void Exit(Player player, AIContext context);
        
        protected Vector2 GetSafeDirection(Vector2 position, Vector2 targetDirection, AIContext context)
        {
            // Check if too close to sidelines
            float leftMargin = MatchEngine.StadiumMargin + 100f;
            float rightMargin = MatchEngine.TotalWidth - MatchEngine.StadiumMargin - 100f;
            float topMargin = MatchEngine.StadiumMargin + 100f;
            float bottomMargin = MatchEngine.TotalHeight - MatchEngine.StadiumMargin - 100f;
            
            bool tooCloseToLeft = position.X < leftMargin;
            bool tooCloseToRight = position.X > rightMargin;
            bool tooCloseToTop = position.Y < topMargin;
            bool tooCloseToBottom = position.Y > bottomMargin;
            
            Vector2 avoidDirection = Vector2.Zero;
            
            if (tooCloseToLeft) avoidDirection.X = 1f;
            if (tooCloseToRight) avoidDirection.X = -1f;
            if (tooCloseToTop) avoidDirection.Y = 1f;
            if (tooCloseToBottom) avoidDirection.Y = -1f;
            
            if (avoidDirection.LengthSquared() > 0)
            {
                avoidDirection.Normalize();
                // Blend target direction with avoidance (favor avoidance near boundaries)
                float avoidanceWeight = 0.7f;
                Vector2 blended = targetDirection * (1f - avoidanceWeight) + avoidDirection * avoidanceWeight;
                if (blended.LengthSquared() > 0)
                    blended.Normalize();
                return blended;
            }
            
            return targetDirection;
        }
    }

    public class AIContext
    {
        public Vector2 BallPosition { get; set; }
        public Vector2 BallVelocity { get; set; }
        public Player NearestOpponent { get; set; }
        public Player NearestTeammate { get; set; }
        public float DistanceToBall { get; set; }
        public bool HasBallPossession { get; set; }
        public Vector2 OpponentGoalCenter { get; set; }
        public Vector2 OwnGoalCenter { get; set; }
        public bool IsPlayerTeam { get; set; }
        public Random Random { get; set; }
        public Player ClosestToBall { get; set; }
        public bool ShouldChaseBall { get; set; }
        
        // Additional context for role-based AI
        public float BallHeight { get; set; }
        public float MatchTime { get; set; }
        public float TimeSinceKickoff { get; set; } // Time since last kickoff
        public bool IsDefensiveHalf { get; set; }
        public bool IsAttackingHalf { get; set; }
        public List<Player> Teammates { get; set; }
        public List<Player> Opponents { get; set; }
        public Player BestPassTarget { get; set; }
        public bool IsHomeTeam { get; set; } // True if defending left goal
        public Random PlayerRandom { get; set; } // Unique random instance per player (breaks synchronization)
        
        // Ball steering helper
        public bool IsPlayerBehindBall(Player player, Vector2 desiredDirection)
        {
            Vector2 playerToBall = BallPosition - player.FieldPosition;
            if (playerToBall.LengthSquared() < 0.01f) return true;
            
            playerToBall.Normalize();
            desiredDirection.Normalize();
            
            // Player is behind ball if they're moving in the same direction as desired kick
            float alignment = Vector2.Dot(playerToBall, desiredDirection);
            return alignment > 0.7f; // ~45 degree tolerance
        }
        
        // Get ideal position to kick ball in desired direction
        public Vector2 GetIdealKickPosition(Vector2 desiredDirection, float distanceBehindBall = 70f)
        {
            if (desiredDirection.LengthSquared() < 0.01f) return BallPosition;
            desiredDirection.Normalize();
            return BallPosition - desiredDirection * distanceBehindBall;
        }
    }
}
