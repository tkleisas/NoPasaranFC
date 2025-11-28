using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Gameplay.Celebrations
{
    /// <summary>
    /// Scorer slides on knees while teammates run and pile on top in a huddle
    /// </summary>
    public class SlideCelebration : CelebrationBase
    {
        public override string CelebrationId => "slide_celebration";
        public override string Name => "Knee Slide";
        public override string Description => "Scorer slides on knees while teammates pile on in celebration";

        private Vector2 _slideStartPosition;
        private Vector2 _slideEndPosition;
        private Vector2 _runStartPosition;
        private const float InitialDelay = 2.5f; // Wait for goal animation to finish
        private const float RunDuration = 0.5f; // Run before sliding
        private const float RunDistance = 150f; // Distance to run before slide
        private const float SlideDistance = 250f; // Distance to slide
        private const float SlideDuration = 1.5f; // Slide duration
        private const float HuddleRadius = 100f;
        private const float PlayerRunSpeed = 450f; // Faster run
        private const float TotalDuration = 15f; // Total celebration length

        // Audio tracking
        private bool _slideAudioPlayed = false;
        private bool _huddleAudioPlayed = false;

        public override void Initialize(Player scorer, List<Player> teammates, List<Player> opponents)
        {
            // Store the run start position (current position)
            _runStartPosition = scorer.FieldPosition;

            // Calculate direction (toward center of field)
            Vector2 fieldCenter = new Vector2(1920f / 2f, 1080f / 2f); // Pitch center
            Vector2 direction = fieldCenter - scorer.FieldPosition;

            // If too close to center, go toward bottom of screen
            if (direction.Length() < (RunDistance + SlideDistance))
            {
                direction = new Vector2(0, 1);
            }
            else
            {
                direction.Normalize();
            }

            // Calculate positions: run start -> slide start -> slide end
            _slideStartPosition = _runStartPosition + (direction * RunDistance);
            _slideEndPosition = _slideStartPosition + (direction * SlideDistance);

            // Set celebrate animation for all players
            scorer.CurrentAnimationState = "celebrate";
            foreach (var teammate in teammates)
            {
                teammate.CurrentAnimationState = "celebrate";
            }

            // Reset audio flags
            _slideAudioPlayed = false;
            _huddleAudioPlayed = false;
        }

        public override void UpdateScorer(Player scorer, float deltaTime, float celebrationTime)
        {
            // Phase 1: Initial delay - stay in place
            if (celebrationTime < InitialDelay)
            {
                scorer.Velocity = Vector2.Zero;
                scorer.CurrentAnimationState = "celebrate";
                return;
            }

            float adjustedTime = celebrationTime - InitialDelay;

            // Phase 2: Running phase
            if (adjustedTime < RunDuration)
            {
                scorer.CurrentAnimationState = "walk";

                float progress = adjustedTime / RunDuration;
                float smoothProgress = SmoothStep(progress);

                Vector2 currentPos = Vector2.Lerp(_runStartPosition, _slideStartPosition, smoothProgress);
                Vector2 movement = currentPos - scorer.FieldPosition;
                scorer.Velocity = movement / deltaTime;
            }
            // Phase 3: Sliding phase
            else if (adjustedTime < RunDuration + SlideDuration)
            {
                // Use tackle animation (sliding on ground)
                scorer.CurrentAnimationState = "tackle";

                float slideTime = adjustedTime - RunDuration;
                float progress = slideTime / SlideDuration;
                float smoothProgress = SmoothStep(progress);

                Vector2 currentPos = Vector2.Lerp(_slideStartPosition, _slideEndPosition, smoothProgress);
                Vector2 movement = currentPos - scorer.FieldPosition;
                scorer.Velocity = movement / deltaTime;

                // Slow down at the end of the slide
                if (progress > 0.8f)
                {
                    scorer.Velocity *= (1f - progress) * 5f;
                }
            }
            // Phase 4: After slide - STAY in tackle animation (lying on ground)
            else
            {
                scorer.CurrentAnimationState = "tackle";
                scorer.Velocity = Vector2.Zero;
                scorer.FieldPosition = _slideEndPosition;
            }
        }

        public override void UpdateTeammate(Player teammate, Player scorer, List<Player> allTeammates,
            int teammateIndex, float deltaTime, float celebrationTime)
        {
            teammate.CurrentAnimationState = "celebrate";

            // Initial delay - stay in place
            if (celebrationTime < InitialDelay)
            {
                teammate.Velocity = Vector2.Zero;
                return;
            }

            float adjustedTime = celebrationTime - InitialDelay;
            float totalScorerMovementTime = RunDuration + SlideDuration;

            if (adjustedTime < totalScorerMovementTime)
            {
                // Wait for scorer to finish running and sliding, but start moving toward them
                Vector2 toScorer = scorer.FieldPosition - teammate.FieldPosition;
                float distance = toScorer.Length();

                if (distance > HuddleRadius * 2f)
                {
                    // Run toward scorer
                    toScorer.Normalize();
                    teammate.Velocity = toScorer * PlayerRunSpeed;
                }
                else
                {
                    // Slow down as approaching
                    teammate.Velocity *= 0.95f;
                }
            }
            else
            {
                // Pile on! Run to huddle center
                Vector2 huddleCenter = _slideEndPosition;
                Vector2 toHuddle = huddleCenter - teammate.FieldPosition;
                float distance = toHuddle.Length();

                if (distance > HuddleRadius)
                {
                    // Still running to huddle
                    toHuddle.Normalize();
                    teammate.Velocity = toHuddle * PlayerRunSpeed;
                }
                else
                {
                    // At huddle - find a spot around the scorer
                    float angle = (float)(teammateIndex * Math.PI * 2.0 / allTeammates.Count);
                    float radius = HuddleRadius * 0.5f;
                    Vector2 targetPosition = huddleCenter + new Vector2(
                        (float)Math.Cos(angle) * radius,
                        (float)Math.Sin(angle) * radius
                    );

                    Vector2 toTarget = targetPosition - teammate.FieldPosition;
                    if (toTarget.Length() > 10f)
                    {
                        toTarget.Normalize();
                        teammate.Velocity = toTarget * PlayerRunSpeed * 0.5f;
                    }
                    else
                    {
                        // At target position - jump around with small random movements
                        teammate.Velocity = new Vector2(
                            (float)(Math.Sin(celebrationTime * 5f + teammateIndex) * 50f),
                            (float)(Math.Cos(celebrationTime * 5f + teammateIndex) * 50f)
                        );
                    }
                }
            }
        }

        public override void UpdateOpponent(Player opponent, float deltaTime, float celebrationTime)
        {
            // Opponents stay idle and look disappointed
            opponent.Velocity = Vector2.Zero;
            opponent.CurrentAnimationState = "walk";
        }

        public override void PlayAudio(float celebrationTime)
        {
            float adjustedTime = celebrationTime - InitialDelay;

            // Play cheer when the slide starts
            if (!_slideAudioPlayed && adjustedTime >= RunDuration && adjustedTime < RunDuration + 0.1f)
            {
                AudioManager.Instance.PlaySoundEffect("crowd_cheer", 1.0f, allowRetrigger: false);
                _slideAudioPlayed = true;
            }

            // Play another cheer when teammates start piling on
            float huddleStartTime = RunDuration + SlideDuration;
            if (!_huddleAudioPlayed && adjustedTime >= huddleStartTime && adjustedTime < huddleStartTime + 0.1f)
            {
                AudioManager.Instance.PlaySoundEffect("crowd_cheer", 1.1f, allowRetrigger: false);
                _huddleAudioPlayed = true;
            }
        }

        public override void Cleanup()
        {
            // Nothing to clean up
        }

        public override Vector2? GetCameraTarget(Player scorer, List<Player> teammates)
        {
            // Camera follows the slide end position (huddle center)
            return _slideEndPosition;
        }

        public override float? GetCameraZoomMultiplier()
        {
            // Zoom in more to really capture the slide (1.4 = 140% of normal zoom)
            return 1.4f;
        }

        public override float? GetCelebrationDuration()
        {
            // Shorter celebration - just 15 seconds
            return TotalDuration;
        }

        /// <summary>
        /// Smooth step interpolation for natural movement
        /// </summary>
        private float SmoothStep(float t)
        {
            return t * t * (3f - 2f * t);
        }
    }
}
