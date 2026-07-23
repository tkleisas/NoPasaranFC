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
            _context = new AIContext();

            // Create unique random instance based on player ID (ensures different behavior per player)
            _playerRandom = DeterministicSeedBase.HasValue
                ? new Random(DeterministicSeedBase.Value + player.ShirtNumber * 12345
                    + Database.TeamSeeder.StableNameHash(player.Team?.Name ?? string.Empty))
                : new Random(player.Id * 12345 + Environment.TickCount);
            
            // Initialize all states - use role-specific positioning state
            _states = new Dictionary<AIStateType, AIState>
            {
                { AIStateType.Idle, new IdleState() },
                { AIStateType.Positioning, CreatePositioningStateForPlayer(player) },
                { AIStateType.ChasingBall, new ChasingBallState() },
                { AIStateType.Dribbling, new DribblingState() },
                { AIStateType.AvoidingSideline, new AvoidingSidelineState() },
                { AIStateType.Passing, new PassingState(engine) },
                { AIStateType.Shooting, new ShootingState(engine) },
                { AIStateType.Celebration, new CelebrationRunState() },
                { AIStateType.CelebrationChase, new CelebrationChaseState() }
            };
            
            // Set initial state
            _currentState = _states[AIStateType.Idle];
            _currentState.Enter(_player, _context);
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
            
            // Legacy state machine path
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
            if (_states[AIStateType.Passing] is PassingState passingState)
            {
                passingState.OnPassBall += callback;
            }
        }
        
        public void RegisterShootCallback(ShootingState.ShootBallHandler callback)
        {
            _shootCallback = callback;
            if (_states[AIStateType.Shooting] is ShootingState shootingState)
            {
                shootingState.OnShootBall += callback;
            }
        }
        
        public string GetCurrentStateName()
        {
            if (UseUtilityBrain)
                return _utilityBrain?.CurrentActionName ?? "Idle";
            return _currentState.Type.ToString();
        }

        public void ForceTransitionTo(AIStateType newState)
        {
            if (_states.ContainsKey(newState))
            {
                TransitionTo(newState);
            }
        }

        public AIState GetState(AIStateType stateType)
        {
            if (_states.ContainsKey(stateType))
            {
                return _states[stateType];
            }
            return null;
        }
    }
}
