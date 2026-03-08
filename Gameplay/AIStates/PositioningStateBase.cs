using System.Linq;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.AIStates
{
    /// <summary>
    /// Base class for positioning states (Defender, Midfielder, Forward).
    /// Extracts shared logic: target update with oscillation prevention,
    /// boundary clamping, boundary repulsion, and movement execution.
    /// </summary>
    public abstract class PositioningStateBase : AIState
    {
        protected PositioningStateBase()
        {
            Type = AIStateType.Positioning;
        }

        public override void Enter(Player player, AIContext context) { }
        public override void Exit(Player player, AIContext context) { }

        public override AIStateType Update(Player player, AIContext context, float deltaTime)
        {
            if (context.HasBallPossession)
                return AIStateType.Dribbling;

            AIStateType? chaseResult = CheckChaseBall(player, context);
            if (chaseResult.HasValue)
                return chaseResult.Value;

            Vector2 newTarget = CalculateTargetPosition(player, context);
            newTarget = ApplyAntiOscillation(player, newTarget);
            // No hard clamp — boundary repulsion in MoveTowardTarget handles edge avoidance softly

            player.AITargetPosition = newTarget;
            player.AITargetPositionSet = true;

            return MoveTowardTarget(player, newTarget, deltaTime);
        }

        /// <summary>
        /// Subclasses implement this to decide whether to chase ball
        /// based on role-specific conditions. Return null to skip chasing.
        /// </summary>
        protected abstract AIStateType? CheckChaseBall(Player player, AIContext context);

        /// <summary>
        /// Subclasses implement this to calculate the ideal target position
        /// based on role-specific tactical logic.
        /// </summary>
        protected abstract Vector2 CalculateTargetPosition(Player player, AIContext context);

        /// <summary>
        /// Subclasses return stamina drain rate per second while moving.
        /// </summary>
        protected abstract float GetStaminaDrainRate();

        /// <summary>
        /// Subclasses return optional speed multiplier (e.g. forwards run 20% faster).
        /// </summary>
        protected virtual float GetSpeedMultiplier() => 1.0f;

        /// <summary>
        /// Subclasses can override to disable boundary repulsion (e.g. goalkeeper).
        /// </summary>
        protected virtual bool UseBoundaryRepulsion => true;

        /// <summary>
        /// Prevents target oscillation by keeping old target when new one is very close.
        /// </summary>
        protected Vector2 ApplyAntiOscillation(Player player, Vector2 newTarget)
        {
            if (player.AITargetPositionSet)
            {
                float diff = Vector2.Distance(player.AITargetPosition, newTarget);
                if (diff < AIConstants.TargetUpdateThreshold)
                    return player.AITargetPosition;
            }
            return newTarget;
        }

        /// <summary>
        /// Clamps position to stay within field boundaries with margin.
        /// </summary>
        protected Vector2 ClampToField(Vector2 position)
        {
            position.X = MathHelper.Clamp(position.X,
                MatchEngine.StadiumMargin + AIConstants.FieldMargin,
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth - AIConstants.FieldMargin);
            position.Y = MathHelper.Clamp(position.Y,
                MatchEngine.StadiumMargin + AIConstants.FieldMargin,
                MatchEngine.StadiumMargin + MatchEngine.FieldHeight - AIConstants.FieldMargin);
            return position;
        }

        /// <summary>
        /// <summary>
        /// Calculates gradient boundary repulsion — stronger when closer to edge.
        /// </summary>
        protected Vector2 GetBoundaryRepulsion(Vector2 position)
        {
            float leftDist = position.X - MatchEngine.StadiumMargin;
            float rightDist = (MatchEngine.StadiumMargin + MatchEngine.FieldWidth) - position.X;
            float topDist = position.Y - MatchEngine.StadiumMargin;
            float bottomDist = (MatchEngine.StadiumMargin + MatchEngine.FieldHeight) - position.Y;

            Vector2 repulsion = Vector2.Zero;
            float trigger = AIConstants.BoundaryRepulsionTrigger;
            float strength = AIConstants.BoundaryRepulsionStrength;

            // Gradient: repulsion scales from 0 at trigger distance to full strength at edge
            if (leftDist < trigger) repulsion.X += strength * (1f - leftDist / trigger);
            if (rightDist < trigger) repulsion.X -= strength * (1f - rightDist / trigger);
            if (topDist < trigger) repulsion.Y += strength * (1f - topDist / trigger);
            if (bottomDist < trigger) repulsion.Y -= strength * (1f - bottomDist / trigger);

            return repulsion;
        }

        /// <summary>
        /// Calculates ball-influenced lerp factor based on distance to ball.
        /// </summary>
        protected float GetDistanceBasedLerpFactor(float distanceToBall, float close, float medium, float far, float veryFar)
        {
            if (distanceToBall > 800f) return veryFar;
            if (distanceToBall > 500f) return far;
            if (distanceToBall > 300f) return medium;
            return close;
        }

        /// <summary>
        /// Shared movement logic: dead zone check, boundary repulsion, teammate avoidance, velocity set.
        /// </summary>
        protected AIStateType MoveTowardTarget(Player player, Vector2 target, float deltaTime)
        {
            Vector2 direction = target - player.FieldPosition;
            float distance = direction.Length();

            if (distance < AIConstants.DeadZone)
            {
                player.Velocity = Vector2.Zero;
                return AIStateType.Positioning;
            }

            if (distance < AIConstants.StopDistance)
            {
                player.Velocity = Vector2.Zero;
                return AIStateType.Idle;
            }

            if (distance > 0)
            {
                direction.Normalize();

                if (UseBoundaryRepulsion)
                {
                    Vector2 repulsion = GetBoundaryRepulsion(player.FieldPosition);
                    if (repulsion.LengthSquared() > 0)
                    {
                        repulsion.Normalize();
                        direction = Vector2.Lerp(direction, repulsion, AIConstants.BoundaryBlendWeight);
                        direction.Normalize();
                    }
                }

                // Teammate avoidance: prevent clustering
                direction = ApplyTeammateRepulsion(player, direction);

                // Decelerate when approaching target to avoid overshooting
                float speedScale = distance < 80f ? distance / 80f : 1f;
                float speed = player.Speed * AIConstants.BaseSpeedMultiplier * GetSpeedMultiplier() * MathHelper.Clamp(speedScale, 0.3f, 1f);
                player.Velocity = direction * speed;
                player.Stamina = System.Math.Max(0, player.Stamina - GetStaminaDrainRate() * deltaTime);
            }
            else
            {
                player.Velocity = Vector2.Zero;
            }

            return AIStateType.Positioning;
        }

        /// <summary>
        /// Applies repulsion from nearby teammates to prevent clustering.
        /// </summary>
        protected Vector2 ApplyTeammateRepulsion(Player player, Vector2 direction)
        {
            if (player.Team == null) return direction;

            Vector2 separationForce = Vector2.Zero;
            int nearbyCount = 0;
            float personalSpace = AIConstants.PlayerPersonalSpace;

            foreach (var teammate in player.Team.Players.Where(p => p.IsStarting && p != player && !p.IsKnockedDown))
            {
                float dist = Vector2.Distance(player.FieldPosition, teammate.FieldPosition);
                if (dist < personalSpace && dist > 0.1f)
                {
                    Vector2 away = player.FieldPosition - teammate.FieldPosition;
                    away.Normalize();
                    float strength = 1f - (dist / personalSpace);
                    separationForce += away * strength;
                    nearbyCount++;
                }
            }

            if (nearbyCount > 0)
            {
                separationForce /= nearbyCount;
                Vector2 blended = direction * (1f - AIConstants.PersonalSpaceBlend) + separationForce * AIConstants.PersonalSpaceBlend;
                if (blended.LengthSquared() > 0.01f)
                {
                    blended.Normalize();
                    return blended;
                }
            }

            return direction;
        }

        /// <summary>
        /// Helper to check standard kickoff chase conditions shared by all positioning states.
        /// </summary>
        protected bool IsKickoffChase(AIContext context)
        {
            return context.TimeSinceKickoff < AIConstants.KickoffDuration
                && context.DistanceToBall < AIConstants.KickoffChaseDistance;
        }

        /// <summary>
        /// Adjusts Y position toward a lane based on role (left/right/center).
        /// </summary>
        protected float AdjustYForLane(float currentY, float laneY, float blendFactor)
        {
            return MathHelper.Lerp(currentY, laneY, blendFactor);
        }
    }
}
