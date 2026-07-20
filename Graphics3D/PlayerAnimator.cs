using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Graphics3D.Skinning;
using NoPasaranFC.Models;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Owns one SkinnedModelInstance for a match player and maps the engine's
    /// 2D animation state (Player.CurrentAnimationState) to KayKit GLB clips:
    ///
    ///   state / condition      clip                          loop
    ///   ---------------------  ---------------------------   --------
    ///   idle / standing        Idle                          loop
    ///   slow movement          Walking_A                     loop
    ///   fast movement          Running_A                     loop
    ///   shoot                  Unarmed_Melee_Attack_Kick     one-shot
    ///   tackle                 Dodge_Forward                 one-shot
    ///   fall / knocked down    Death_A -> Lie_Idle           one-shot -> loop
    ///   celebrate              Cheer                         loop
    ///   throw_in_static        Idle                          loop
    ///   throw_in_throw         Throw                         one-shot
    ///
    /// One-shots freeze on their last frame and auto-cross-fade back to the
    /// last looping clip (SkinnedModelInstance handles that). Missing clips
    /// fall back to Idle.
    /// </summary>
    public class PlayerAnimator
    {
        private readonly SkinnedModelInstance _instance;
        private bool _wasKnockedDown;
        private bool _isLyingDown;
        private float _yaw;

        // Locomotion thresholds in engine pixels/second (73 px = 1 m).
        // Mirrors MatchScreen.UpdatePlayerAnimations: LengthSquared > 0.1 means "moving".
        private const float IdleThresholdSquared = 0.1f;
        private const float RunThresholdPx = 120f; // ~1.6 m/s; above this use Running_A
        // KayKit clips are authored slower than the game's movement cadence
        private const float AnimationSpeed = 1.4f;

        // The KayKit bind pose faces +Z; add MathHelper.Pi here if visual
        // verification shows the model running backwards.
        public static float ModelYawOffset = 0f;

        public SkinnedModelInstance Instance => _instance;
        public float Yaw => _yaw;

        public PlayerAnimator(SkinnedModel model)
        {
            _instance = new SkinnedModelInstance(model);
            _instance.PlaybackSpeed = AnimationSpeed;
            _instance.Play("Idle");
        }

        public void Update(Player player, float deltaTime)
        {
            string state = player.CurrentAnimationState ?? "idle";
            Vector2 velocity = player.Velocity;

            // Facing from velocity (engine Y maps to world Z); keep last direction when nearly stopped
            if (velocity.LengthSquared() > 1f)
            {
                _yaw = (float)Math.Atan2(velocity.X, velocity.Y) + ModelYawOffset;
            }

            if (player.IsKnockedDown || state == "fall")
            {
                if (!_wasKnockedDown)
                {
                    PlayClip("Death_A", loop: false);
                    _isLyingDown = false;
                }
                else if (!_isLyingDown && _instance.CurrentClipFinished)
                {
                    PlayClip("Lie_Idle", loop: true);
                    _isLyingDown = true;
                }
                _wasKnockedDown = true;
            }
            else
            {
                _wasKnockedDown = false;
                _isLyingDown = false;

                switch (state)
                {
                    case "shoot":
                        PlayClip("Unarmed_Melee_Attack_Kick", loop: false);
                        break;
                    case "tackle":
                        PlayClip("Dodge_Forward", loop: false);
                        break;
                    case "celebrate":
                        PlayClip("Cheer", loop: true);
                        break;
                    case "throw_in_static":
                        PlayClip("Idle", loop: true);
                        break;
                    case "throw_in_throw":
                        PlayClip("Throw", loop: false);
                        break;
                    default: // "idle", "walk", anything unknown -> locomotion by speed
                        float speedSquared = velocity.LengthSquared();
                        if (speedSquared <= IdleThresholdSquared)
                            PlayClip("Idle", loop: true);
                        else if (speedSquared < RunThresholdPx * RunThresholdPx)
                            PlayClip("Walking_A", loop: true);
                        else
                            PlayClip("Running_A", loop: true);
                        break;
                }
            }

            _instance.Update(deltaTime);
        }

        private void PlayClip(string clipName, bool loop)
        {
            if (!_instance.Play(clipName, loop) && clipName != "Idle")
            {
                _instance.Play("Idle", loop: true); // graceful fallback for missing clips
            }
        }
    }
}
