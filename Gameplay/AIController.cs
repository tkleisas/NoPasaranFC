using System;
using System.Collections.Generic;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;
using NoPasaranFC.Gameplay.AIStates;

namespace NoPasaranFC.Gameplay
{
    public class AIController
    {
        private Player _player;
        private readonly MatchEngine _engine;
        private AIState _currentState;
        private Dictionary<AIStateType, AIState> _states;
        private AIContext _context;
        private Random _playerRandom; // Unique random instance per player
        
        // Utility AI (rewrite): per-player utility brain replacing the FSM when enabled
        private UtilityAI.UtilityBrain _utilityBrain;
        private PassingState.PassBallHandler _passCallback;
        private ShootingState.ShootBallHandler _shootCallback;
        
        /// <summary>
        /// When true, all AIControllers run the utility brain instead of the
        /// legacy state machine. Static so the harness can A/B it (--legacy).
        /// </summary>
        public static bool UseUtilityBrain = true;

        /// <summary>
        /// When set (headless harness), per-player Randoms are seeded deterministically
        /// from this base + shirt number instead of the wall clock. Must be assigned
        /// before the MatchEngine constructor creates the controllers.
        /// </summary>
        public static int? DeterministicSeedBase = null;

        public AIController(Player player, MatchEngine engine)
        {
            _player = player;
            _engine = engine;
            _context = new AIContext();

            // Create unique random instance based on player ID (ensures different behavior per player)
            _playerRandom = DeterministicSeedBase.HasValue
                ? new Random(DeterministicSeedBase.Value + player.ShirtNumber * 12345
                    + Database.TeamSeeder.StableNameHash(player.Team?.Name ?? string.Empty))
                : new Random(player.Id * 12345 + Environment.TickCount);
            
            // Utility brain: create eagerly so its lifetime kick counters can be
            // carried over when kickoff resets recreate the controller (the
            // callbacks are late-bound fields, so pre-registration is safe).
            // The legacy FSM is only materialized when it is actually used, so
            // the live utility path allocates no FSM states per player.
            if (UseUtilityBrain)
            {
                _utilityBrain = new UtilityAI.UtilityBrain(_playerRandom,
                    (p, target, power) => _passCallback?.Invoke(target, power),
                    (p, target, power) => _shootCallback?.Invoke(target, power));
            }
            else
            {
                EnsureLegacyStates();
            }
        }

        /// <summary>
        /// Lazily builds the legacy state machine. Called by the constructor when
        /// <see cref="UseUtilityBrain"/> is false, or on the first legacy update if
        /// the flag was toggled at runtime (harness A/B runs).
        /// </summary>
        private void EnsureLegacyStates()
        {
            if (_states != null) return;

            _states = new Dictionary<AIStateType, AIState>
            {
                { AIStateType.Idle, new IdleState() },
                { AIStateType.Positioning, CreatePositioningStateForPlayer(_player) },
                { AIStateType.ChasingBall, new ChasingBallState() },
                { AIStateType.Dribbling, new DribblingState() },
                { AIStateType.AvoidingSideline, new AvoidingSidelineState() },
                { AIStateType.Passing, new PassingState(_engine) },
                { AIStateType.Shooting, new ShootingState(_engine) },
                { AIStateType.Celebration, new CelebrationRunState() },
                { AIStateType.CelebrationChase, new CelebrationChaseState() }
            };

            _currentState = _states[AIStateType.Idle];
            _currentState.Enter(_player, _context);

            // Callbacks may have been registered before the FSM was materialized
            if (_passCallback != null && _states[AIStateType.Passing] is PassingState passing)
                passing.OnPassBall += _passCallback;
            if (_shootCallback != null && _states[AIStateType.Shooting] is ShootingState shooting)
                shooting.OnShootBall += _shootCallback;
        }
        
        private AIState CreatePositioningStateForPlayer(Player player)
        {
            // Create role-specific positioning behavior
            switch (player.Position)
            {
                case PlayerPosition.Goalkeeper:
                    return new GoalkeeperState();
                case PlayerPosition.Defender:
                    return new DefenderState();
                case PlayerPosition.Midfielder:
                    return new MidfielderState();
                case PlayerPosition.Forward:
                    return new ForwardState();
                default:
                    return new PositioningState();
            }
        }
        
        public void Update(AIContext context, float deltaTime)
        {
            // Update context
            _context = context;
            
            // Override the shared Random with this player's unique Random instance
            _context.PlayerRandom = _playerRandom;
            
            // Utility brain path (the rewrite): utility scoring + steering movement
            if (UseUtilityBrain)
            {
                _utilityBrain ??= new UtilityAI.UtilityBrain(_playerRandom,
                    (p, target, power) => _passCallback?.Invoke(target, power),
                    (p, target, power) => _shootCallback?.Invoke(target, power));
                _utilityBrain.Update(_player, _context, deltaTime);
                return;
            }
            
            // Legacy state machine path (materialized on demand)
            EnsureLegacyStates();
            AIStateType nextState = _currentState.Update(_player, _context, deltaTime);
            
            // Check for state transition
            if (nextState != _currentState.Type)
            {
                TransitionTo(nextState);
            }
        }
        
        private void TransitionTo(AIStateType newState)
        {
            _currentState.Exit(_player, _context);
            _currentState = _states[newState];
            _currentState.Enter(_player, _context);
        }
        
        public void RegisterPassCallback(PassingState.PassBallHandler callback)
        {
            _passCallback = callback;
            if (_states != null && _states[AIStateType.Passing] is PassingState passingState)
            {
                passingState.OnPassBall += callback;
            }
        }
        
        public void RegisterShootCallback(ShootingState.ShootBallHandler callback)
        {
            _shootCallback = callback;
            if (_states != null && _states[AIStateType.Shooting] is ShootingState shootingState)
            {
                shootingState.OnShootBall += callback;
            }
        }
        
        public string GetCurrentStateName()
        {
            if (UseUtilityBrain)
                return _utilityBrain?.CurrentActionName ?? "Idle";
            return _currentState?.Type.ToString() ?? "Idle";
        }
        
        /// <summary>Lifetime kick counters from the utility brain (0 for FSM mode).</summary>
        public int ShotsAttempted => _utilityBrain?.ShotsAttempted ?? 0;
        public int PassesAttempted => _utilityBrain?.PassesAttempted ?? 0;
        
        /// <summary>The utility brain (null until its first update / in legacy FSM
        /// mode). The match recorder reads LastDecision for the verbose log.</summary>
        public UtilityAI.UtilityBrain Brain => _utilityBrain;

        public void ForceTransitionTo(AIStateType newState)
        {
            if (_states != null && _states.ContainsKey(newState))
            {
                TransitionTo(newState);
            }
        }

        public AIState GetState(AIStateType stateType)
        {
            if (_states != null && _states.ContainsKey(stateType))
            {
                return _states[stateType];
            }
            return null;
        }
    }
}
