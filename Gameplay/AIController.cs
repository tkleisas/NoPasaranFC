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
        
        public AIController(Player player)
        {
            _player = player;
            _context = new AIContext();
            
            // Create unique random instance based on player ID (ensures different behavior per player)
            _playerRandom = new Random(player.Id * 12345 + Environment.TickCount);
            
            // Initialize all states - use role-specific positioning state
            _states = new Dictionary<AIStateType, AIState>
            {
                { AIStateType.Idle, new IdleState() },
                { AIStateType.Positioning, CreatePositioningStateForPlayer(player) },
                { AIStateType.ChasingBall, new ChasingBallState() },
                { AIStateType.Dribbling, new DribblingState() },
                { AIStateType.AvoidingSideline, new AvoidingSidelineState() },
                { AIStateType.Passing, new PassingState() },
                { AIStateType.Shooting, new ShootingState() },
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
            
            // Update current state
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
            if (_states[AIStateType.Passing] is PassingState passingState)
            {
                passingState.OnPassBall += callback;
            }
        }
        
        public void RegisterShootCallback(ShootingState.ShootBallHandler callback)
        {
            if (_states[AIStateType.Shooting] is ShootingState shootingState)
            {
                shootingState.OnShootBall += callback;
            }
        }
        
        public string GetCurrentStateName()
        {
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
