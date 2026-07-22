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
        // Latch for one-shot clips: the engine can hold a state (e.g. "shoot")
        // longer than the clip, and without this the one-shot would retrigger
        private string _lastOneShotState;

        // Locomotion thresholds in engine pixels/second (73 px = 1 m).
        // Mirrors MatchScreen.UpdatePlayerAnimations: LengthSquared > 0.1 means "moving".
        private const float IdleThresholdSquared = 0.1f;
        private const float RunThresholdPx = 120f; // ~1.6 m/s; above this use Running_A
        // Hysteresis margins: entering a state needs a clearly higher speed than
        // exiting it, so velocity noise can't flap idle/walk/run every frame
        private const float WalkEnterSpeedSquared = 25f;   // idle -> walk above 5 px/s
        private const float WalkExitSpeedSquared = 1f;     // walk -> idle below 1 px/s
        private const float RunEnterSpeedPx = 150f;        // walk -> run
        private const float RunExitSpeedPx = 85f;          // run -> walk
        private const float LocomotionMinHold = 0.18f;     // seconds between switches
        
        private string _locomotion = "Idle";
        private float _locomotionHold;
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
                        PlayOneShot("shoot", "Unarmed_Melee_Attack_Kick");
                        break;
                    case "tackle":
                        PlayOneShot("tackle", "Dodge_Forward");
                        break;
                    case "celebrate":
                        _lastOneShotState = null;
                        PlayClip("Cheer", loop: true);
                        break;
                    case "throw_in_static":
                        _lastOneShotState = null;
                        PlayClip("Idle", loop: true);
                        break;
                    case "throw_in_throw":
                        PlayOneShot("throw_in_throw", "Throw");
                        break;
                    default: // "idle", "walk", anything unknown -> locomotion by speed
                        _lastOneShotState = null;
                        UpdateLocomotion(velocity.Length(), deltaTime);
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
        
        /// <summary>Plays a one-shot only when the state is new (latch against retriggering).</summary>
        private void PlayOneShot(string state, string clipName)
        {
            if (_lastOneShotState == state) return;
            _lastOneShotState = state;
            PlayClip(clipName, loop: false);
        }
        
        /// <summary>
        /// Locomotion clip selection with hysteresis: entering idle/walk/run needs
        /// a clearly different speed than exiting it, plus a minimum hold time,
        /// so per-frame velocity noise can't flap the animation.
        /// </summary>
        private void UpdateLocomotion(float speed, float deltaTime)
        {
            _locomotionHold -= deltaTime;
            float speedSquared = speed * speed;
            
            string desired = _locomotion;
            switch (_locomotion)
            {
                case "Idle":
                    if (speedSquared > WalkEnterSpeedSquared)
                        desired = speed > RunEnterSpeedPx ? "Running_A" : "Walking_A";
                    break;
                case "Walking_A":
                    if (speedSquared <= WalkExitSpeedSquared)
                        desired = "Idle";
                    else if (speed > RunEnterSpeedPx)
                        desired = "Running_A";
                    break;
                default: // Running_A
                    if (speed < RunExitSpeedPx)
                        desired = speedSquared <= WalkExitSpeedSquared ? "Idle" : "Walking_A";
                    break;
            }
            
            if (desired != _locomotion && _locomotionHold <= 0f)
            {
                _locomotion = desired;
                _locomotionHold = LocomotionMinHold;
            }
            
            PlayClip(_locomotion, loop: true);
        }
    }
}
