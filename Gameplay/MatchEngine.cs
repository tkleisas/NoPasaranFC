using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay
{
    public class MatchEngine
    {
        private Team _homeTeam;
        private Team _awayTeam;
        private Player _controlledPlayer;
        private Player _lastPlayerTouchedBall;
        private Random _random;
        
        // Referee
        public Vector2 RefereePosition { get; set; }
        private Vector2 _refereeVelocity;
        
        // Match state
        public enum MatchState { CameraInit, Countdown, Playing, HalfTime, Ended, GoalCelebration, FinalScore }
        public MatchState CurrentState { get; private set; }
        public float CountdownTimer { get; private set; }
        public int CountdownNumber { get; private set; }
        public GoalCelebration GoalCelebration { get; private set; }
        public float FinalScoreTimer { get; private set; }
        
        public Vector2 BallPosition { get; set; }
        public Vector2 BallVelocity { get; set; }
        public float BallHeight { get; set; } // Z-axis height
        public float BallVerticalVelocity { get; set; } // Vertical velocity (up/down)
        private float _shootButtonHoldTime = 0f;
        private bool _wasShootButtonDown = false;
        private bool _goalScored = false;
        private float _goalCelebrationDelay = 0f;
        private const float GoalCelebrationDelayTime = 0.5f; // Half second delay to see ball in goal
        private const float AutoKickCooldown = 0.3f; // Cooldown between automatic kicks (300ms)
        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }
        public float MatchTime { get; private set; }
        public bool IsMatchOver => MatchTime >= 90f;
        public Camera Camera { get; private set; }
        
        // Field dimensions scaled to player size
        // Player sprite (128px rendered) ≈ 1.75m tall → 73 pixels per meter
        // FIFA standard pitch: 105m × 68m
        public const float FieldWidth = 7665f;  // 105m × 73px/m
        public const float FieldHeight = 4964f; // 68m × 73px/m
        public const float StadiumMargin = 400f; // Space for stadium stands
        public const float TotalWidth = FieldWidth + (StadiumMargin * 2);
        public const float TotalHeight = FieldHeight + (StadiumMargin * 2);
        
        // Goal dimensions (FIFA standard: 7.32m wide × 2.44m high × 2m deep)
        public const float GoalWidth = 534f; // 7.32m × 73px/m
        public const float GoalDepth = 146f;  // 2m × 73px/m (actual goal depth)
        public const float GoalPostHeight = 178f; // 2.44m × 73px/m (crossbar height)
        
        // Ball physics
        private const float BallFriction = 0.95f;
        private const float BallPossessionDistance = 80f; // Scaled for larger sprites
        private const float BallKickDistance = 35f; // Reduced from 50f for tighter control
        private const float TackleDistance = 70f; // Scaled for larger sprites
        private const float TackleSuccessBase = 40f; // Base tackle success %
        private const float Gravity = 1200f; // Gravity for ball vertical movement
        private const float PlayerPersonalSpace = 80f; // Minimum distance between AI players
        private const float MaxShootHoldTime = 0.8f; // Maximum time to hold shoot button (faster charging)
        
        // Stamina system
        private const float StaminaDecreasePerSecondRunning = 3f; // Stamina lost per second while running
        private const float StaminaDecreasePerShot = 5f; // Stamina lost per shot/tackle
        private const float StaminaRecoveryPerSecond = 2f; // Stamina recovered per second when idle
        private const float LowStaminaThreshold = 30f; // Below this, player is affected
        
        // Viewport zoom (adjust to show desired portion of field)
        public const float ZoomLevel = 0.8f; // Higher value = more zoomed in, shows smaller area
        
        private Microsoft.Xna.Framework.Graphics.SpriteFont _font;
        private Microsoft.Xna.Framework.Graphics.GraphicsDevice _graphicsDevice;
        
        public MatchEngine(Team homeTeam, Team awayTeam, int viewportWidth, int viewportHeight)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _random = new Random();
            
            // Filter to only use starting players (if they exist, otherwise use first 11)
            EnsureStartingLineup(homeTeam);
            EnsureStartingLineup(awayTeam);
            HomeScore = 0;
            AwayScore = 0;
            MatchTime = 0f;
            BallVelocity = Vector2.Zero;
            BallHeight = 0f;
            BallVerticalVelocity = 0f;
            
            // Initialize camera with settings
            float zoom = GameSettings.Instance.CameraZoom * ZoomLevel; // Apply both base zoom and user zoom
            Camera = new Camera(viewportWidth, viewportHeight, zoom);
            
            // Initialize referee position (center of field)
            RefereePosition = new Vector2(StadiumMargin + FieldWidth / 2, StadiumMargin + FieldHeight / 2);
            _refereeVelocity = Vector2.Zero;
            
            // Initialize goal celebration
            GoalCelebration = new GoalCelebration();
            
            // Start with camera initialization (center on ball first)
            CurrentState = MatchState.CameraInit;
            CountdownTimer = 0.5f; // Half second to center camera
            CountdownNumber = 3;
            
            InitializePositions();
        }
        
        public void SetCelebrationResources(Microsoft.Xna.Framework.Graphics.SpriteFont font, 
            Microsoft.Xna.Framework.Graphics.GraphicsDevice graphicsDevice)
        {
            _font = font;
            _graphicsDevice = graphicsDevice;
        }
        
        private void InitializePositions()
        {
            // Formation 4-4-2 for both teams
            SetupTeamPositions(_homeTeam, true);
            SetupTeamPositions(_awayTeam, false);
            
            // Ball at center
            BallPosition = new Vector2(StadiumMargin + FieldWidth / 2, StadiumMargin + FieldHeight / 2);
            
            // Clear all IsControlled flags first (ensure only one player is controlled)
            foreach (var player in _homeTeam.Players)
                player.IsControlled = false;
            foreach (var player in _awayTeam.Players)
                player.IsControlled = false;
            
            // Find the first starting player from controlled team
            var controlledTeamPlayers = _homeTeam.IsPlayerControlled ? 
                _homeTeam.Players.Where(p => p.IsStarting).ToList() : 
                _awayTeam.Players.Where(p => p.IsStarting).ToList();
            if (controlledTeamPlayers.Any())
            {
                _controlledPlayer = controlledTeamPlayers[0];
                _controlledPlayer.IsControlled = true;
            }
            
            // Initialize camera to follow ball
            Camera.Follow(BallPosition, 1f);
        }
        
        private void SetupTeamPositions(Team team, bool isHome)
        {
            // Get only starting players
            var startingPlayers = team.Players.Where(p => p.IsStarting).ToList();
            if (startingPlayers.Count < 11) return;
            
            float xOffset = StadiumMargin;
            float yOffset = StadiumMargin;
            float centerX = xOffset + FieldWidth / 2;
            float centerY = yOffset + FieldHeight / 2;
            
            // Positions relative to team's goal (left for home, right for away)
            float defenseX = isHome ? xOffset + FieldWidth * 0.2f : xOffset + FieldWidth * 0.8f;
            float midfieldX = isHome ? xOffset + FieldWidth * 0.4f : xOffset + FieldWidth * 0.6f;
            float attackX = isHome ? xOffset + FieldWidth * 0.6f : xOffset + FieldWidth * 0.4f;
            float goalX = isHome ? xOffset + 50f : xOffset + FieldWidth - 50f;
            
            // Goalkeeper
            startingPlayers[0].FieldPosition = new Vector2(goalX, centerY);
            startingPlayers[0].HomePosition = new Vector2(goalX, centerY);
            
            // Defenders (4) - spread vertically
            float[] defenderY = { 0.2f, 0.4f, 0.6f, 0.8f };
            for (int i = 0; i < 4; i++)
            {
                startingPlayers[1 + i].FieldPosition = new Vector2(defenseX, yOffset + FieldHeight * defenderY[i]);
                startingPlayers[1 + i].HomePosition = new Vector2(defenseX, yOffset + FieldHeight * defenderY[i]);
            }
            
            // Midfielders (4)
            float[] midfielderY = { 0.2f, 0.4f, 0.6f, 0.8f };
            for (int i = 0; i < 4; i++)
            {
                startingPlayers[5 + i].FieldPosition = new Vector2(midfieldX, yOffset + FieldHeight * midfielderY[i]);
                startingPlayers[5 + i].HomePosition = new Vector2(midfieldX, yOffset + FieldHeight * midfielderY[i]);
            }
            
            // Forwards (2)
            startingPlayers[9].FieldPosition = new Vector2(attackX, yOffset + FieldHeight * 0.33f);
            startingPlayers[9].HomePosition = new Vector2(attackX, yOffset + FieldHeight * 0.33f);
            startingPlayers[10].FieldPosition = new Vector2(attackX, yOffset + FieldHeight * 0.67f);
            startingPlayers[10].HomePosition = new Vector2(attackX, yOffset + FieldHeight * 0.67f);
        }
        
        public void Update(GameTime gameTime, Vector2 moveDirection, bool isShootKeyDown)
        {
            float deltaTime = (float)gameTime.ElapsedGameTime.TotalSeconds;
            
            // Handle camera initialization
            if (CurrentState == MatchState.CameraInit)
            {
                CountdownTimer -= deltaTime;
                
                // Center camera on ball
                Camera.Follow(BallPosition, deltaTime * 5f); // Fast centering
                
                if (CountdownTimer <= 0)
                {
                    // Switch to countdown
                    CurrentState = MatchState.Countdown;
                    CountdownTimer = 3.5f; // 3 seconds countdown + 0.5 for "GO!"
                    CountdownNumber = 3;
                }
                
                return; // Don't update gameplay during camera init
            }
            
            // Handle countdown
            if (CurrentState == MatchState.Countdown)
            {
                CountdownTimer -= deltaTime;
                
                if (CountdownTimer <= 0 )
                {
                    AudioManager.Instance.PlaySoundEffect("whistle_start");
                    CurrentState = MatchState.Playing;
                    
                }
                else
                {
                    // Update countdown number (3, 2, 1)
                    CountdownNumber = (int)Math.Ceiling(CountdownTimer);
                }
                
                // Don't update gameplay during countdown, but keep camera following ball
                Camera.Follow(BallPosition, deltaTime);
                return;
            }
            
            // Handle goal scored delay (ball continues moving before celebration)
            if (_goalScored && CurrentState == MatchState.Playing)
            {
                _goalCelebrationDelay += deltaTime;
                if (_goalCelebrationDelay >= GoalCelebrationDelayTime)
                {
                    TriggerGoalCelebration();
                    _goalScored = false;
                }
                // Let ball physics continue during delay
            }
            
            // Handle goal celebration
            if (CurrentState == MatchState.GoalCelebration)
            {
                GoalCelebration.Update(deltaTime);
                
                if (!GoalCelebration.IsActive)
                {
                    // Celebration ended, reset for kickoff
                    CurrentState = MatchState.Playing;
                    ResetAfterGoal();
                }
                
                // Keep camera on ball during celebration
                Camera.Follow(BallPosition, deltaTime);
                return;
            }
            
            // Handle final score overlay
            if (CurrentState == MatchState.FinalScore)
            {
                FinalScoreTimer -= deltaTime;
                
                if (FinalScoreTimer <= 0)
                {
                    CurrentState = MatchState.Ended;
                }
                
                // Keep camera on ball during overlay
                Camera.Follow(BallPosition, deltaTime);
                return;
            }
            
            // Update match time - map real time to game time (90 minutes)
            float realTimeDuration = GameSettings.Instance.GetMatchDurationSeconds();
            float gameTimeIncrement = (90f / realTimeDuration) * deltaTime;
            MatchTime += gameTimeIncrement;
            
            // Check if match should end
            if (MatchTime >= 90f)
            {
                CurrentState = MatchState.FinalScore;
                FinalScoreTimer = 5f;
                AudioManager.Instance.PlaySoundEffect("whistle_end");
                return;
            }
            
            // Update all players
            UpdatePlayers(deltaTime, moveDirection, isShootKeyDown);
            
            // Ensure only the controlled player has IsControlled = true (safeguard)
            foreach (var player in _homeTeam.Players.Concat(_awayTeam.Players))
            {
                player.IsControlled = (player == _controlledPlayer);
            }
            
            // Check for collisions between players
            CheckPlayerCollisions(deltaTime);
            
            // Update ball physics
            UpdateBall(deltaTime);
            
            // Update referee position (follows ball at a distance)
            UpdateReferee(deltaTime);
            
            // Update camera to follow ball smoothly
            // Also update zoom in case settings changed
            Camera.Zoom = GameSettings.Instance.CameraZoom * ZoomLevel;
            Camera.Follow(BallPosition, deltaTime);
            
            // Check for goals
            CheckGoal();
        }
        
        private void UpdatePlayers(float deltaTime, Vector2 moveDirection, bool isShootKeyDown)
        {
            // Update controlled player
            if (_controlledPlayer != null)
            {
                float distToBall = Vector2.Distance(_controlledPlayer.FieldPosition, BallPosition);
                
                // Handle shoot button for charging shot or tackle
                if (isShootKeyDown && distToBall < BallKickDistance * 2f && BallHeight < 50f)
                {
                    // Near ball - charge shot (no angle check here, allow from any angle when charging)
                    if (!_wasShootButtonDown)
                    {
                        // Just pressed
                        _shootButtonHoldTime = 0f;
                    }
                    _shootButtonHoldTime += deltaTime;
                    _shootButtonHoldTime = Math.Min(_shootButtonHoldTime, MaxShootHoldTime);
                    _wasShootButtonDown = true;
                }
                else if (_wasShootButtonDown && !isShootKeyDown)
                {
                    // Button released
                    if (distToBall < BallKickDistance * 2f && BallHeight < 50f)
                    {
                        // Near ball - shoot! (no angle check for charged shots)
                        PerformShoot(moveDirection);
                    }
                    else if (_shootButtonHoldTime < 0.1f && _lastPlayerTouchedBall != _controlledPlayer)
                    {
                        // Not near ball, quick tap, and controlled player doesn't have possession - try to tackle
                        // Only tackle if button was pressed very briefly (not a shot attempt)
                        // and the controlled player is NOT the one who last touched the ball
                        Tackle();
                    }
                    _wasShootButtonDown = false;
                    _shootButtonHoldTime = 0f;
                }
                else if (!_wasShootButtonDown && !isShootKeyDown)
                {
                    // Reset state when not pressing
                    _shootButtonHoldTime = 0f;
                }
                
                // Handle knockdown for controlled player
                if (_controlledPlayer.IsKnockedDown)
                {
                    _controlledPlayer.KnockdownTimer -= deltaTime;
                    if (_controlledPlayer.KnockdownTimer <= 0)
                    {
                        _controlledPlayer.IsKnockedDown = false;
                        _controlledPlayer.Velocity = Vector2.Zero;
                    }
                    else
                    {
                        // Player is knocked down, can't control but slides
                        _controlledPlayer.Velocity *= 0.9f; // Slide to stop
                        _controlledPlayer.FieldPosition += _controlledPlayer.Velocity * deltaTime;
                        Vector2 pos = _controlledPlayer.FieldPosition;
                        ClampToField(ref pos);
                        _controlledPlayer.FieldPosition = pos;
                    }
                }
                else
                {
                    // Normal movement (only if not knocked down)
                    if (moveDirection.Length() > 0)
                    {
                        // Apply stamina speed multiplier
                        float staminaMultiplier = GetStaminaSpeedMultiplier(_controlledPlayer);
                        float moveSpeed = _controlledPlayer.Speed * 3f * GameSettings.Instance.PlayerSpeedMultiplier * staminaMultiplier;
                        var newPosition = _controlledPlayer.FieldPosition + moveDirection * moveSpeed * deltaTime;
                        _controlledPlayer.Velocity = moveDirection * moveSpeed;
                        ClampToField(ref newPosition);
                        _controlledPlayer.FieldPosition = newPosition;
                        
                        // Decrease stamina while running
                        _controlledPlayer.Stamina = Math.Max(0, _controlledPlayer.Stamina - StaminaDecreasePerSecondRunning * deltaTime);
                        
                        // If controlled player is near ball and moving, kick it (with angle check and cooldown)
                        // Don't kick if ball is in the air (prevents headbutting glitch)
                        if (moveDirection.Length() > 0.1f && BallHeight < 50f && CanPlayerKickBall(_controlledPlayer, moveDirection, BallKickDistance))
                        {
                            // Check cooldown to prevent continuous juggling
                            float timeSinceLastKick = (float)MatchTime - _controlledPlayer.LastKickTime;
                            if (timeSinceLastKick >= AutoKickCooldown)
                            {
                                // Trigger shoot animation
                                _controlledPlayer.CurrentAnimationState = "shoot";
                                
                                // Kick ball in movement direction with stamina effect
                                float staminaStatMultiplier = GetStaminaStatMultiplier(_controlledPlayer);
                                float kickPower = (_controlledPlayer.Shooting / 8f + 6f) * staminaStatMultiplier;
                                BallVelocity = moveDirection * kickPower * _controlledPlayer.Speed * 1.2f;
                                AudioManager.Instance.PlaySoundEffect("kick_ball", 0.6f, allowRetrigger: false);
                                _controlledPlayer.LastKickTime = (float)MatchTime;
                            }
                        }
                    }
                    else
                    {
                        _controlledPlayer.Velocity = Vector2.Zero;
                        // Recover stamina when idle
                        _controlledPlayer.Stamina = Math.Min(100, _controlledPlayer.Stamina + StaminaRecoveryPerSecond * deltaTime);
                    }
                }
            }
            
            // Update AI players (only starting players)
            foreach (var player in _homeTeam.Players.Where(p => p.IsStarting).Concat(_awayTeam.Players.Where(p => p.IsStarting)))
            {
                if (player.IsControlled) continue;
                
                UpdateAIPlayer(player, deltaTime);
            }
        }
        
        private void UpdateAIPlayer(Player player, float deltaTime)
        {
            // Skip if player is knocked down
            if (player.IsKnockedDown)
            {
                player.KnockdownTimer -= deltaTime;
                if (player.KnockdownTimer <= 0)
                {
                    player.IsKnockedDown = false;
                }
                else
                {
                    // Player is knocked down, can't move
                    player.Velocity *= 0.9f; // Slide to stop
                    player.FieldPosition += player.Velocity * deltaTime;
                    Vector2 pos = player.FieldPosition;
                    ClampToField(ref pos);
                    player.FieldPosition = pos;
                    return;
                }
            }
            
            float distanceToBall = Vector2.Distance(player.FieldPosition, BallPosition);
            
            // Determine if this player should chase the ball (anti-clustering logic)
            bool shouldChaseBall = ShouldPlayerChaseBall(player);
            
            Vector2 targetPosition;
            float urgency;
            
            // At match start (first 5 seconds), all players rush to ball
            bool matchJustStarted = CurrentState == MatchState.Playing && MatchTime < 5f;
            
            if (matchJustStarted)
            {
                targetPosition = BallPosition;
                urgency = 1.0f;
            }
            // Determine target based on position and ball proximity
            else switch (player.Position)
            {
                case PlayerPosition.Goalkeeper:
                    // Smart goalkeeper positioning
                    bool isHomeGK = player.TeamId == _homeTeam.Id;
                    float penaltyDepth = 1205f; // 16.5m × 73px/m
                    float penaltyWidth = 2942f; // 40.3m × 73px/m
                    float centerY = StadiumMargin + FieldHeight / 2;
                    float penaltyTop = centerY - penaltyWidth / 2;
                    float penaltyBottom = centerY + penaltyWidth / 2;
                    
                    // Define penalty area boundaries for this goalkeeper
                    float penaltyLeft, penaltyRight;
                    if (isHomeGK)
                    {
                        penaltyLeft = StadiumMargin;
                        penaltyRight = StadiumMargin + penaltyDepth;
                    }
                    else
                    {
                        penaltyLeft = StadiumMargin + FieldWidth - penaltyDepth;
                        penaltyRight = StadiumMargin + FieldWidth;
                    }
                    
                    // Check if any opponent has the ball in penalty area
                    var opponentTeam = isHomeGK ? _awayTeam : _homeTeam;
                    Player closestOpponentInBox = null;
                    float closestOpponentDist = float.MaxValue;
                    
                    foreach (var opponent in opponentTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown))
                    {
                        float distToBallFromOpponent = Vector2.Distance(opponent.FieldPosition, BallPosition);
                        
                        // Check if opponent is close to ball and in penalty area
                        if (distToBallFromOpponent < 100f && 
                            opponent.FieldPosition.X >= penaltyLeft && 
                            opponent.FieldPosition.X <= penaltyRight &&
                            opponent.FieldPosition.Y >= penaltyTop && 
                            opponent.FieldPosition.Y <= penaltyBottom)
                        {
                            float distToGK = Vector2.Distance(player.FieldPosition, opponent.FieldPosition);
                            if (distToGK < closestOpponentDist)
                            {
                                closestOpponentDist = distToGK;
                                closestOpponentInBox = opponent;
                            }
                        }
                    }
                    
                    if (closestOpponentInBox != null)
                    {
                        // Opponent with ball in penalty area - move vertically to intercept
                        // Stay on goal line but adjust Y position to match opponent
                        float goalLineX = isHomeGK ? StadiumMargin + 50f : StadiumMargin + FieldWidth - 50f;
                        targetPosition = new Vector2(goalLineX, closestOpponentInBox.FieldPosition.Y);
                        
                        // Clamp to goal width (don't go outside the posts)
                        float goalTop = StadiumMargin + (FieldHeight - GoalWidth) / 2;
                        float goalBottom = goalTop + GoalWidth;
                        targetPosition.Y = Math.Clamp(targetPosition.Y, goalTop + 64f, goalBottom - 64f);
                        
                        urgency = 1.0f;
                    }
                    else if (distanceToBall < 150f)
                    {
                        // Ball is close but no opponent in box - chase it
                        targetPosition = BallPosition;
                        urgency = 1.0f;
                    }
                    else
                    {
                        // Default: stay at home position
                        targetPosition = player.HomePosition;
                        urgency = 0.3f;
                    }
                    break;
                
                case PlayerPosition.Defender:
                    bool ballInDefHalf = IsBallInHalf(player.TeamId);
                    if (shouldChaseBall && (ballInDefHalf || distanceToBall < 200f))
                    {
                        targetPosition = BallPosition;
                        urgency = 0.8f;
                    }
                    else
                    {
                        targetPosition = Vector2.Lerp(player.HomePosition, BallPosition, 0.3f);
                        urgency = 0.4f;
                    }
                    break;
                
                case PlayerPosition.Midfielder:
                    bool isHomeTeamMID = player.TeamId == _homeTeam.Id;
                    bool ballInAttackingHalfMID = isHomeTeamMID ? 
                        (BallPosition.X > StadiumMargin + FieldWidth / 2) : 
                        (BallPosition.X < StadiumMargin + FieldWidth / 2);
                    
                    // Midfielders more aggressive when ball is in attacking half
                    if (shouldChaseBall && (distanceToBall < 300f || (ballInAttackingHalfMID && distanceToBall < 500f)))
                    {
                        targetPosition = BallPosition;
                        urgency = 0.9f;
                    }
                    else
                    {
                        // Support attack or defense based on ball position
                        float lerpFactor = ballInAttackingHalfMID ? 0.6f : 0.4f;
                        targetPosition = Vector2.Lerp(player.HomePosition, BallPosition, lerpFactor);
                        urgency = ballInAttackingHalfMID ? 0.7f : 0.5f;
                    }
                    break;
                
                case PlayerPosition.Forward:
                    bool isHomeTeamFWD = player.TeamId == _homeTeam.Id;
                    bool ballInAttackingHalf = isHomeTeamFWD ? 
                        (BallPosition.X > StadiumMargin + FieldWidth / 2) : 
                        (BallPosition.X < StadiumMargin + FieldWidth / 2);
                    
                    // Forwards should be more aggressive in chasing the ball in attacking half
                    if (shouldChaseBall && (distanceToBall < 400f || ballInAttackingHalf))
                    {
                        targetPosition = BallPosition;
                        urgency = 1.0f;
                    }
                    else
                    {
                        // Move toward opponent goal area (stay in attacking position)
                        // Position between ball and opponent goal for better positioning
                        float targetGoalX = isHomeTeamFWD ? 
                            StadiumMargin + FieldWidth - 400f : 
                            StadiumMargin + 400f;
                        
                        // Move toward center Y when in attacking position (avoid going to edges)
                        float fieldCenterY = StadiumMargin + FieldHeight / 2;
                        float targetY = fieldCenterY + (player.HomePosition.Y - fieldCenterY) * 0.5f; // 50% toward center
                        
                        targetPosition = new Vector2(targetGoalX, targetY);
                        urgency = 0.7f; // Keep forwards more active
                    }
                    break;
                
                default:
                    targetPosition = BallPosition;
                    urgency = 0.5f;
                    break;
            }
            
            // Move player toward target
            Vector2 direction = targetPosition - player.FieldPosition;
            float distance = direction.Length();
            
            // Check if AI player has ball control
            bool hasControl = _lastPlayerTouchedBall == player && Vector2.Distance(player.FieldPosition, BallPosition) < 80f;
            
            // AI repositioning logic: If player has the ball and needs to change direction significantly,
            // move to a position behind the ball first
            if (hasControl && urgency > 0.7f)
            {
                bool isHomeTeam = player.TeamId == _homeTeam.Id;
                float opponentGoalX = isHomeTeam ? StadiumMargin + FieldWidth : StadiumMargin;
                Vector2 idealKickDirection = new Vector2(opponentGoalX - BallPosition.X, 0);
                idealKickDirection.Normalize();
                
                // Check sideline proximity - need to redirect?
                float topBoundary = StadiumMargin + 150f;
                float bottomBoundary = StadiumMargin + FieldHeight - 150f;
                bool nearTopSideline = BallPosition.Y < topBoundary;
                bool nearBottomSideline = BallPosition.Y > bottomBoundary;
                
                if (nearTopSideline || nearBottomSideline)
                {
                    // Need to redirect ball toward center
                    float centerY = StadiumMargin + FieldHeight / 2;
                    idealKickDirection = new Vector2(opponentGoalX - BallPosition.X, centerY - BallPosition.Y);
                    if (idealKickDirection.Length() > 0)
                    {
                        idealKickDirection.Normalize();
                    }
                }
                
                // Calculate where player should be to kick in ideal direction (behind the ball)
                Vector2 idealPlayerPosition = BallPosition - idealKickDirection * 70f; // 70 pixels behind ball
                
                // Check if player is roughly in position to kick in ideal direction
                Vector2 playerToBall = BallPosition - player.FieldPosition;
                if (playerToBall.Length() > 0)
                {
                    playerToBall.Normalize();
                    float alignment = Vector2.Dot(playerToBall, idealKickDirection);
                    
                    // If alignment is poor (player not behind ball relative to kick direction), reposition
                    if (alignment < 0.7f) // Less than ~45 degree alignment
                    {
                        // Override target to ideal position behind ball
                        targetPosition = idealPlayerPosition;
                        direction = targetPosition - player.FieldPosition;
                        distance = direction.Length();
                        if (distance > 0)
                        {
                            direction.Normalize();
                        }
                    }
                }
            }
            
            // Stop closer to target for tighter control (reduced from 15f to 10f)
            if (distance > 10f)
            {
                direction.Normalize();
                
                // Apply teammate avoidance to prevent overlapping players chasing same target
                direction = ApplyTeammateAvoidance(player, direction);
                
                // Apply stamina and difficulty modifiers
                float staminaMultiplier = GetStaminaSpeedMultiplier(player);
                float difficultyMultiplier = GetAIDifficultyModifier();
                float reactionMultiplier = GetAIReactionTimeMultiplier();
                
                // AI is affected by difficulty and stamina
                float moveSpeed = player.Speed * 2.5f * urgency * GameSettings.Instance.PlayerSpeedMultiplier * 
                                staminaMultiplier * difficultyMultiplier;
                
                var newPosition = player.FieldPosition + direction * moveSpeed * deltaTime;
                player.Velocity = direction * moveSpeed; // Store velocity for collision
                ClampToField(ref newPosition);
                player.FieldPosition = newPosition;
                
                // Decrease stamina while running (AI also gets tired)
                player.Stamina = Math.Max(0, player.Stamina - StaminaDecreasePerSecondRunning * deltaTime);
                
                // If AI player is near ball and moving toward it, kick it (with angle check and cooldown)
                // Don't kick if ball is in the air (prevents headbutting glitch)
                if (urgency > 0.7f && BallHeight < 50f && CanPlayerKickBall(player, direction, BallKickDistance))
                {
                    // Check cooldown to prevent continuous juggling
                    float timeSinceLastKick = (float)MatchTime - player.LastKickTime;
                    if (timeSinceLastKick >= AutoKickCooldown)
                    {
                        // Trigger shoot animation for AI
                        player.CurrentAnimationState = "shoot";
                        
                        bool isHomeTeam = player.TeamId == _homeTeam.Id;
                        Vector2 kickTarget;
                        float kickPowerMultiplier = 1.0f;
                        
                        // Strategic passing based on position
                        switch (player.Position)
                        {
                            case PlayerPosition.Defender:
                                // Defenders pass to midfielders or center (90% of the time)
                                if (_random.NextDouble() < 0.9)
                                {
                                    // Find best midfielder to pass to
                                    var myTeam = isHomeTeam ? _homeTeam : _awayTeam;
                                    var midfielders = myTeam.Players.Where(p => 
                                        p.IsStarting && 
                                        !p.IsKnockedDown && 
                                        p.Position == PlayerPosition.Midfielder).ToList();
                                    
                                    if (midfielders.Any())
                                    {
                                        // Find midfielder closest to opponent goal (most advanced position)
                                        float opponentGoalX = isHomeTeam ? StadiumMargin + FieldWidth : StadiumMargin;
                                        var bestMidfielder = midfielders.OrderBy(m => 
                                            Math.Abs(m.FieldPosition.X - opponentGoalX)).First();
                                        kickTarget = bestMidfielder.FieldPosition;
                                        kickPowerMultiplier = 0.7f; // Moderate pass
                                    }
                                    else
                                    {
                                        // No midfielders available, pass toward attacking half center
                                        float centerX = isHomeTeam ? 
                                            StadiumMargin + FieldWidth * 0.65f : 
                                            StadiumMargin + FieldWidth * 0.35f;
                                        float centerY = StadiumMargin + FieldHeight / 2;
                                        kickTarget = new Vector2(centerX, centerY);
                                        kickPowerMultiplier = 0.8f;
                                    }
                                }
                                else
                                {
                                    // 10% chance: Clear ball toward opponent half
                                    float targetX = isHomeTeam ? 
                                        StadiumMargin + FieldWidth * 0.75f : 
                                        StadiumMargin + FieldWidth * 0.25f;
                                    kickTarget = new Vector2(targetX, StadiumMargin + FieldHeight / 2);
                                    kickPowerMultiplier = 1.2f; // Strong clearance
                                }
                                break;
                                
                            case PlayerPosition.Midfielder:
                                // Midfielders pass to forwards (70%) or try to attack (30%)
                                if (_random.NextDouble() < 0.7)
                                {
                                    // Find best forward to pass to
                                    var myTeam = isHomeTeam ? _homeTeam : _awayTeam;
                                    var forwards = myTeam.Players.Where(p => 
                                        p.IsStarting && 
                                        !p.IsKnockedDown && 
                                        p.Position == PlayerPosition.Forward).ToList();
                                    
                                    if (forwards.Any())
                                    {
                                        // Find forward closest to opponent goal (most advanced position)
                                        float opponentGoalX2 = isHomeTeam ? StadiumMargin + FieldWidth : StadiumMargin;
                                        var bestForward = forwards.OrderBy(f => 
                                            Math.Abs(f.FieldPosition.X - opponentGoalX2)).First();
                                        kickTarget = bestForward.FieldPosition;
                                        kickPowerMultiplier = 0.9f;
                                    }
                                    else
                                    {
                                        // No forwards, shoot at goal
                                        goto case PlayerPosition.Forward;
                                    }
                                }
                                else
                                {
                                    // Attack - shoot at goal
                                    goto case PlayerPosition.Forward;
                                }
                                break;
                                
                            case PlayerPosition.Forward:
                            default:
                                // Forwards and others shoot at goal
                                float goalTop = StadiumMargin + (FieldHeight - GoalWidth) / 2;
                                float targetGoalY = goalTop + (float)_random.NextDouble() * GoalWidth;
                                // Home team attacks right goal (away goal), away team attacks left goal (home goal)
                                float targetGoalX = isHomeTeam ? 
                                    StadiumMargin + FieldWidth : // Right goal (away goal)
                                    StadiumMargin;               // Left goal (home goal)
                                kickTarget = new Vector2(targetGoalX, targetGoalY);
                                kickPowerMultiplier = 1.0f;
                                break;
                        }
                        
                        Vector2 kickDirection = kickTarget - BallPosition;
                        if (kickDirection.Length() > 0)
                        {
                            kickDirection.Normalize();
                            
                            // Apply stamina and difficulty to AI kicking
                            float staminaStatMultiplier = GetStaminaStatMultiplier(player);
                            float aiDifficultyModifier = GetAIDifficultyModifier();
                            float kickPower = (player.Shooting / 8f + 6f) * staminaStatMultiplier * aiDifficultyModifier * kickPowerMultiplier;
                            BallVelocity = kickDirection * kickPower * player.Speed;
                            _lastPlayerTouchedBall = player;
                            
                            // Decrease stamina for kicking
                            player.Stamina = Math.Max(0, player.Stamina - StaminaDecreasePerShot);
                            
                            // Calculate shot height - passes are lower, shots can be higher
                            float shootingSkill = player.Shooting / 100f;
                            float distToTarget = Vector2.Distance(BallPosition, kickTarget);
                            
                            if (player.Position == PlayerPosition.Forward || kickPowerMultiplier >= 1.0f)
                            {
                                // Shots can have varied heights
                                float baseHeight = 50f;
                                float skillBonus = shootingSkill * 300f;
                                float distanceBonus = Math.Min(distToTarget / 20f, 200f);
                                float randomFactor = (float)_random.NextDouble() * 150f;
                                
                                if (_random.NextDouble() < shootingSkill * 0.2f)
                                {
                                    BallVerticalVelocity = baseHeight + skillBonus + distanceBonus + randomFactor;
                                }
                                else
                                {
                                    BallVerticalVelocity = baseHeight + randomFactor * 0.5f;
                                }
                            }
                            else
                            {
                                // Passes stay low
                                BallVerticalVelocity = 20f + (float)_random.NextDouble() * 30f;
                            }
                            
                            // Play kick sound
                            AudioManager.Instance.PlaySoundEffect("kick_ball", 0.5f, allowRetrigger: false);
                            
                            // Update last kick time
                            player.LastKickTime = (float)MatchTime;
                        }
                    }
                }
            }
            else
            {
                // Player reached target, stop moving
                player.Velocity = Vector2.Zero;
                // Recover stamina when idle
                player.Stamina = Math.Min(100, player.Stamina + StaminaRecoveryPerSecond * deltaTime);
            }
        }
        
        private void UpdateBall(float deltaTime)
        {
            // Apply horizontal velocity to ball position
            BallPosition += BallVelocity * deltaTime;
            
            // Check collision with back of goal net
            CheckGoalNetCollision();
            
            // Check collision with players (players push the ball)
            CheckPlayerBallCollisions();
            
            // Apply vertical physics (gravity)
            BallVerticalVelocity -= Gravity * deltaTime;
            BallHeight += BallVerticalVelocity * deltaTime;
            
            // Ball bounces when it hits the ground
            if (BallHeight <= 0f)
            {
                BallHeight = 0f;
                if (Math.Abs(BallVerticalVelocity) > 50f) // Bounce threshold
                {
                    BallVerticalVelocity = -BallVerticalVelocity * 0.6f; // Bounce with energy loss
                }
                else
                {
                    BallVerticalVelocity = 0f; // Stop bouncing
                }
            }
            
            // Apply friction only when ball is on ground
            if (BallHeight <= 0f)
            {
                BallVelocity *= BallFriction;
                
                // Stop ball if moving very slowly
                if (BallVelocity.Length() < 1f)
                {
                    BallVelocity = Vector2.Zero;
                }
            }
            else
            {
                // Air resistance when ball is in the air
                BallVelocity *= 0.99f;
            }
            
            // Clamp ball to field
            ClampBallToField();
        }
        
        private void UpdateReferee(float deltaTime)
        {
            // Referee follows ball but keeps some distance
            Vector2 targetPosition = BallPosition;
            Vector2 toBall = targetPosition - RefereePosition;
            float distance = toBall.Length();
            
            // Stay about 150-300 units away from the ball
            if (distance > 300f)
            {
                toBall.Normalize();
                _refereeVelocity = toBall * 180f; // Referee speed
            }
            else if (distance < 150f)
            {
                toBall.Normalize();
                _refereeVelocity = -toBall * 100f; // Move away
            }
            else
            {
                _refereeVelocity *= 0.9f; // Slow down when in good position
            }
            
            RefereePosition += _refereeVelocity * deltaTime;
            
            // Clamp referee to field
            Vector2 refPos = RefereePosition;
            ClampToField(ref refPos);
            RefereePosition = refPos;
        }
        
        private void CheckPlayerCollisions(float deltaTime)
        {
            var allPlayers = GetAllPlayers();
            
            for (int i = 0; i < allPlayers.Count; i++)
            {
                for (int j = i + 1; j < allPlayers.Count; j++)
                {
                    Player p1 = allPlayers[i];
                    Player p2 = allPlayers[j];
                    
                    // Skip if either player is already knocked down
                    if (p1.IsKnockedDown || p2.IsKnockedDown) continue;
                    
                    // Check if players are on the same team
                    bool sameTeam = p1.TeamId == p2.TeamId;
                    
                    float distance = Vector2.Distance(p1.FieldPosition, p2.FieldPosition);
                    float collisionDistance = 70f; // Collision radius for 64x64 sprites
                    
                    if (distance < collisionDistance)
                    {
                        // Check if collision is near the ball (more intense)
                        float distanceToBall = Vector2.Distance(BallPosition, p1.FieldPosition);
                        bool nearBall = distanceToBall < 150f;
                        
                        // Calculate relative speeds
                        float speed1 = p1.Velocity.Length();
                        float speed2 = p2.Velocity.Length();
                        
                        // Only check knockdown if at least one player is moving fast enough
                        if (speed1 > 50f || speed2 > 50f)
                        {
                            // Teammates rarely knock each other down (only 1-2% chance)
                            if (sameTeam && _random.NextDouble() > 0.02f)
                            {
                                // Skip knockdown for teammates (98% of the time)
                                // Just do separation below
                            }
                            else
                            {
                                // Calculate knockdown probability based on:
                                // - Speed difference
                                // - Strength (defending stat)
                                // - Agility (helps avoid knockdown)
                                // - Randomness
                                
                                float p1Force = speed1 * (p1.Defending / 100f) * (1.0f - (p1.Agility / 100f) * 0.3f);
                                float p2Force = speed2 * (p2.Defending / 100f) * (1.0f - (p2.Agility / 100f) * 0.3f);
                                
                                // Reduce force for friendly collisions (if they do happen)
                                if (sameTeam)
                                {
                                    p1Force *= 0.3f;
                                    p2Force *= 0.3f;
                                }
                                
                                // Boost collision intensity if near ball (only for opposing teams)
                                if (nearBall && !sameTeam)
                                {
                                    p1Force *= 1.5f;
                                    p2Force *= 1.5f;
                                }
                                
                                // Random factor
                                float randomFactor = (float)_random.NextDouble();
                                
                                // Determine if anyone gets knocked down
                                float knockdownThreshold = 40f; // Base threshold
                                
                                if (p1Force > knockdownThreshold && randomFactor > 0.6f)
                                {
                                    // P2 gets knocked down by P1
                                    KnockDownPlayer(p2, p1.Velocity);
                                }
                                else if (p2Force > knockdownThreshold && randomFactor < 0.4f)
                                {
                                    // P1 gets knocked down by P2
                                    KnockDownPlayer(p1, p2.Velocity);
                                }
                                else if (nearBall && !sameTeam && (p1Force + p2Force) > 60f && randomFactor > 0.7f)
                                {
                                    // Both get knocked down in intense collision near ball (only opposing teams)
                                    KnockDownPlayer(p1, p2.Velocity * 0.5f);
                                    KnockDownPlayer(p2, p1.Velocity * 0.5f);
                                }
                            }
                        }
                        
                        // Simple separation to prevent sticking
                        Vector2 separation = p1.FieldPosition - p2.FieldPosition;
                        if (separation.Length() > 0)
                        {
                            separation.Normalize();
                            float pushDistance = (collisionDistance - distance) / 2;
                            Vector2 p1Pos = p1.FieldPosition + separation * pushDistance;
                            Vector2 p2Pos = p2.FieldPosition - separation * pushDistance;
                            ClampToField(ref p1Pos);
                            ClampToField(ref p2Pos);
                            p1.FieldPosition = p1Pos;
                            p2.FieldPosition = p2Pos;
                        }
                    }
                }
            }
        }
        
        private void CheckPlayerBallCollisions()
        {
            // Check if any player is colliding with the ball
            var allPlayers = GetAllPlayers();
            const float ballRadius = 16f; // Ball is 32x32, so radius is 16
            const float playerRadius = 40f; // Player collision radius (smaller than sprite size)
            const float collisionDistance = ballRadius + playerRadius;
            
            foreach (var player in allPlayers)
            {
                // Skip if player is knocked down or ball is too high in the air
                if (player.IsKnockedDown || BallHeight > 50f) continue;
                
                float distance = Vector2.Distance(player.FieldPosition, BallPosition);
                
                if (distance < collisionDistance)
                {
                    // Player is colliding with the ball - push it
                    Vector2 pushDirection = BallPosition - player.FieldPosition;
                    
                    // If player has zero velocity, don't push the ball
                    if (player.Velocity.Length() < 0.1f) continue;
                    
                    pushDirection.Normalize();
                    
                    // Push ball in the direction the player is moving
                    // The push strength depends on player velocity and ball's current state
                    float playerSpeed = player.Velocity.Length();
                    float pushStrength = Math.Min(playerSpeed * 0.3f, 200f); // Cap the push strength
                    
                    // If ball is already moving away from player, don't add too much velocity
                    float currentBallSpeed = BallVelocity.Length();
                    if (currentBallSpeed < 150f) // Only push if ball isn't moving too fast already
                    {
                        Vector2 playerDirection = player.Velocity;
                        playerDirection.Normalize();
                        
                        // Push ball in player's movement direction
                        BallVelocity += playerDirection * pushStrength;
                        
                        // Update last player touched
                        _lastPlayerTouchedBall = player;
                    }
                    
                    // Separate ball from player to prevent phasing
                    float overlap = collisionDistance - distance;
                    BallPosition += pushDirection * overlap;
                }
            }
        }
        
        private void KnockDownPlayer(Player player, Vector2 impactVelocity)
        {
            player.IsKnockedDown = true;
            player.KnockdownTimer = 0.5f + (float)_random.NextDouble() * 1.0f; // 0.5 to 1.5 seconds
            
            // Apply impact velocity (player slides in direction of impact)
            player.Velocity = impactVelocity * 0.5f;
            
            // Play tackle sound
            AudioManager.Instance.PlaySoundEffect("tackle", 0.7f);
            
            // Note: Don't auto-switch here - let the player choose when to switch with Space
            // This makes the gameplay more realistic and gives player control
        }
        
        private bool IsBallInHalf(int teamId)
        {
            float centerX = StadiumMargin + FieldWidth / 2;
            bool isHomeTeam = teamId == _homeTeam.Id;
            
            if (isHomeTeam)
                return BallPosition.X < centerX; // Ball in left half
            else
                return BallPosition.X > centerX; // Ball in right half
        }
        
        private void PerformShoot(Vector2 moveDirection)
        {
            // Calculate power based on hold time (0 to 1)
            float power = _shootButtonHoldTime / MaxShootHoldTime;
            
            // Determine shoot direction
            Vector2 shootDirection = moveDirection.Length() > 0.1f ? moveDirection : _controlledPlayer.Velocity;
            if (shootDirection.Length() < 0.1f)
            {
                // Default to shooting toward nearest goal
                float leftGoalDist = BallPosition.X - StadiumMargin;
                float rightGoalDist = (StadiumMargin + FieldWidth) - BallPosition.X;
                shootDirection = leftGoalDist < rightGoalDist ? Vector2.UnitX * -1 : Vector2.UnitX;
            }
            else
            {
                shootDirection.Normalize();
            }
            
            // Trigger shoot animation
            _controlledPlayer.CurrentAnimationState = "shoot";
            
            // Calculate horizontal and vertical velocity with stamina effect
            float staminaMultiplier = GetStaminaStatMultiplier(_controlledPlayer);
            float basePower = (_controlledPlayer.Shooting / 10f + 5f) * staminaMultiplier;
            float horizontalPower = basePower * (1f + power * 2f); // More power = faster
            BallVelocity = shootDirection * horizontalPower * _controlledPlayer.Speed;
            _lastPlayerTouchedBall = _controlledPlayer;
            
            // Calculate vertical velocity (height)
            // More hold time = higher shot
            BallVerticalVelocity = power * 800f * staminaMultiplier; // Max height with max hold
            
            // Decrease stamina for shooting
            _controlledPlayer.Stamina = Math.Max(0, _controlledPlayer.Stamina - StaminaDecreasePerShot);
            
            // Play kick sound (louder for shooting)
            AudioManager.Instance.PlaySoundEffect("kick_ball", 0.8f + power * 0.4f, allowRetrigger: false);
        }
        
        private void ClampToField(ref Vector2 position)
        {
            // Allow players to go 100px outside the field boundaries
            const float OutOfBoundsMargin = 100f;
            position.X = MathHelper.Clamp(position.X, StadiumMargin - OutOfBoundsMargin, StadiumMargin + FieldWidth + OutOfBoundsMargin);
            position.Y = MathHelper.Clamp(position.Y, StadiumMargin - OutOfBoundsMargin, StadiumMargin + FieldHeight + OutOfBoundsMargin);
        }
        
        private void ClampBallToField()
        {
            // Don't clamp - let the ball go out of bounds!
            // CheckGoal() will handle out-of-bounds detection and restarts
            // This allows proper throw-ins, corners, and goal kicks
            
            // Note: The ball can now travel beyond the field boundaries
            // which is correct behavior for football
        }
        
        private void CheckGoalNetCollision()
        {
            // Calculate goal area boundaries
            float leftGoalLine = StadiumMargin;
            float rightGoalLine = StadiumMargin + FieldWidth;
            float leftNetBack = leftGoalLine - GoalDepth; // Back of left net
            float rightNetBack = rightGoalLine + GoalDepth; // Back of right net
            float goalTop = StadiumMargin + (FieldHeight - GoalWidth) / 2;
            float goalBottom = goalTop + GoalWidth;
            
            // Check if ball is inside goal area (within goal width)
            bool inGoalWidth = BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom;
            
            if (inGoalWidth && BallHeight <= GoalPostHeight)
            {
                // Check left goal net back collision
                if (BallPosition.X < leftNetBack && BallVelocity.X < 0)
                {
                    // Ball hit back of left net - bounce back lightly
                    BallPosition = new Vector2(leftNetBack, BallPosition.Y);
                    BallVelocity = new Vector2(-BallVelocity.X * 0.3f, BallVelocity.Y * 0.8f); // Soft bounce
                    AudioManager.Instance.PlaySoundEffect("kick_ball", 0.3f, allowRetrigger: false);
                }
                // Check right goal net back collision
                else if (BallPosition.X > rightNetBack && BallVelocity.X > 0)
                {
                    // Ball hit back of right net - bounce back lightly
                    BallPosition = new Vector2(rightNetBack, BallPosition.Y);
                    BallVelocity = new Vector2(-BallVelocity.X * 0.3f, BallVelocity.Y * 0.8f); // Soft bounce
                    AudioManager.Instance.PlaySoundEffect("kick_ball", 0.3f, allowRetrigger: false);
                }
            }
        }
        
        private void CheckGoal()
        {
            float goalTop = StadiumMargin + (FieldHeight - GoalWidth) / 2;
            float goalBottom = goalTop + GoalWidth;
            float leftGoalLine = StadiumMargin;
            float rightGoalLine = StadiumMargin + FieldWidth;
            
            // Define out-of-bounds areas (tighter bounds to prevent ball getting stuck)
            float leftOutBound = StadiumMargin - GoalDepth - 50;
            float rightOutBound = StadiumMargin + FieldWidth + GoalDepth + 50;
            float topOutBound = StadiumMargin - 50;
            float bottomOutBound = StadiumMargin + FieldHeight + 50;
            
            // Additional check: if ball is behind goal and outside goal width, it's out
            bool behindLeftGoal = BallPosition.X < leftGoalLine - 30;
            bool behindRightGoal = BallPosition.X > rightGoalLine + 30;
            bool outsideGoalWidth = BallPosition.Y < goalTop || BallPosition.Y > goalBottom;
            
            // Check if ball is near crossbar height (with tolerance for ricochet)
            bool ballAtCrossbarHeight = BallHeight >= GoalPostHeight - 20 && BallHeight <= GoalPostHeight + 20;
            bool ballBelowGoalHeight = BallHeight <= GoalPostHeight;
            
            // === CROSSBAR RICOCHET ===
            // Check if ball hits the crossbar (within goal area, at crossbar height)
            if (((BallPosition.X >= leftGoalLine - 30 && BallPosition.X <= leftGoalLine + 30) ||
                 (BallPosition.X >= rightGoalLine - 30 && BallPosition.X <= rightGoalLine + 30)) &&
                BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom &&
                ballAtCrossbarHeight && BallVerticalVelocity < 0)
            {
                // Ball hit crossbar - bounce back down and reverse horizontal direction
                BallVerticalVelocity = -BallVerticalVelocity * 0.6f; // Bounce down with energy loss
                BallVelocity = new Vector2(-BallVelocity.X * 0.7f, BallVelocity.Y * 0.7f); // Bounce back
                AudioManager.Instance.PlaySoundEffect("kick_ball", 0.4f, allowRetrigger: false);
                return;
            }
            
            // === SIDE POST RICOCHET ===
            // Check if ball hits the left or right goalpost (vertical posts)
            float postTolerance = 30f; // Collision detection tolerance
            bool nearLeftGoalLine = BallPosition.X >= leftGoalLine - 30 && BallPosition.X <= leftGoalLine + 30;
            bool nearRightGoalLine = BallPosition.X >= rightGoalLine - 30 && BallPosition.X <= rightGoalLine + 30;
            bool nearTopPost = Math.Abs(BallPosition.Y - goalTop) < postTolerance;
            bool nearBottomPost = Math.Abs(BallPosition.Y - goalBottom) < postTolerance;
            
            if ((nearLeftGoalLine || nearRightGoalLine) && (nearTopPost || nearBottomPost) && ballBelowGoalHeight)
            {
                // Ball hit side post - bounce sideways and reverse horizontal direction
                BallVelocity = new Vector2(-BallVelocity.X * 0.7f, -BallVelocity.Y * 0.7f); // Bounce both directions
                BallVerticalVelocity *= 0.6f; // Reduce vertical velocity
                AudioManager.Instance.PlaySoundEffect("kick_ball", 0.4f, allowRetrigger: false);
                return;
            }
            
            // === GOAL DETECTION ===
            // Left goal (home team defends) - Ball must cross the goal line
            if (BallPosition.X < leftGoalLine && 
                BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom &&
                ballBelowGoalHeight && !_goalScored)
            {
                AwayScore++;
                _goalScored = true;
                _goalCelebrationDelay = 0f;
                AudioManager.Instance.PlaySoundEffect("goal");
                // Don't trigger celebration yet - let ball continue for visual effect
                return;
            }
            // Right goal (away team defends) - Ball must cross the goal line
            else if (BallPosition.X > rightGoalLine && 
                     BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom &&
                     ballBelowGoalHeight && !_goalScored)
            {
                HomeScore++;
                _goalScored = true;
                _goalCelebrationDelay = 0f;
                AudioManager.Instance.PlaySoundEffect("goal");
                // Don't trigger celebration yet - let ball continue for visual effect
                return;
            }
            
            // === BALL OUT OF BOUNDS DETECTION ===
            // Ball went over the crossbar (over goal posts)
            if ((BallPosition.X < leftGoalLine || BallPosition.X > rightGoalLine) &&
                BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom &&
                !ballBelowGoalHeight)
            {
                // Ball went over crossbar - corner kick or goal kick
                bool leftSide = BallPosition.X < leftGoalLine;
                HandleBallOverCrossbar(leftSide);
                return;
            }
            
            // Ball went out past goal line (not in goal area)
            // ALSO check if ball is stuck behind goal but outside goal width
            if (BallPosition.X < leftOutBound || (behindLeftGoal && outsideGoalWidth))
            {
                // Ball went out on left side - corner or goal kick
                HandleBallOutGoalLine(true, BallPosition.Y);
                return;
            }
            else if (BallPosition.X > rightOutBound || (behindRightGoal && outsideGoalWidth))
            {
                // Ball went out on right side - corner or goal kick
                HandleBallOutGoalLine(false, BallPosition.Y);
                return;
            }
            
            // Ball went out on top or bottom (throw-in)
            if (BallPosition.Y < topOutBound)
            {
                HandleBallOutSideline(BallPosition.X, true); // Top sideline
                return;
            }
            else if (BallPosition.Y > bottomOutBound)
            {
                HandleBallOutSideline(BallPosition.X, false); // Bottom sideline
                return;
            }
        }
        
        private void HandleBallOverCrossbar(bool leftSide)
        {
            // Determine which team last touched based on ball direction
            // For now, simplified: corner kick from opposite side
            float xPos = leftSide ? StadiumMargin - 20 : StadiumMargin + FieldWidth + 20;
            float yPos = BallPosition.Y < StadiumMargin + FieldHeight / 2 ? 
                StadiumMargin + 50 : StadiumMargin + FieldHeight - 50;
            
            PlaceBallForRestart(new Vector2(xPos, yPos));
        }
        
        private void HandleBallOutGoalLine(bool leftSide, float yPosition)
        {
            // Determine if corner kick or goal kick based on who touched the ball last
            bool isCornerKick = false;
            bool giveToHomeTeam = true;
            
            if (_lastPlayerTouchedBall != null)
            {
                // Check if last touch was from defending team
                bool lastTouchWasHomeTeam = _homeTeam.Players.Contains(_lastPlayerTouchedBall);
                
                // Home team defends left goal, away team defends right goal
                // If defending team touched it last -> corner kick for attacking team
                // If attacking team touched it last -> goal kick for defending team
                if (leftSide)
                {
                    // Left goal (defended by home team)
                    isCornerKick = lastTouchWasHomeTeam; // Home defender touched = corner for away
                    giveToHomeTeam = !lastTouchWasHomeTeam; // Give to opposite of who touched
                }
                else // right side
                {
                    // Right goal (defended by away team)
                    isCornerKick = !lastTouchWasHomeTeam; // Away defender touched = corner for home
                    giveToHomeTeam = lastTouchWasHomeTeam; // Give to opposite of who touched
                }
            }
            
            float xPos, yPos;
            
            if (isCornerKick)
            {
                // Corner kick - place near corner
                xPos = leftSide ? StadiumMargin + 30 : StadiumMargin + FieldWidth - 30;
                yPos = yPosition < StadiumMargin + FieldHeight / 2 ? 
                    StadiumMargin + 30 : StadiumMargin + FieldHeight - 30;
            }
            else
            {
                // Goal kick - place in goal area, centered
                xPos = leftSide ? StadiumMargin + 200 : StadiumMargin + FieldWidth - 200;
                yPos = StadiumMargin + FieldHeight / 2;
            }
            
            PlaceBallForRestart(new Vector2(xPos, yPos), giveToHomeTeam);
        }
        
        private void HandleBallOutSideline(float xPosition, bool topSide)
        {
            // Place ball for throw-in
            float xPos = Math.Clamp(xPosition, StadiumMargin + 50, StadiumMargin + FieldWidth - 50);
            float yPos = topSide ? StadiumMargin + 20 : StadiumMargin + FieldHeight - 20;
            
            // Determine which team gets the throw-in (opposite of who touched it last)
            bool giveToHomeTeam = true; // default
            
            if (_lastPlayerTouchedBall != null)
            {
                bool lastTouchWasHomeTeam = _homeTeam.Players.Contains(_lastPlayerTouchedBall);
                // Give to opposite team
                giveToHomeTeam = !lastTouchWasHomeTeam;
            }
            
            PlaceBallForRestart(new Vector2(xPos, yPos), giveToHomeTeam);
        }
        
        private void PlaceBallForRestart(Vector2 position, bool? preferHomeTeam = null)
        {
            BallPosition = position;
            BallVelocity = Vector2.Zero;
            BallHeight = 0f;
            BallVerticalVelocity = 0f;
            
            // Find nearest player from the team that gets possession
            IEnumerable<Player> playersToConsider;
            
            if (preferHomeTeam.HasValue)
            {
                // Use specified team
                playersToConsider = preferHomeTeam.Value ? 
                    _homeTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown) : 
                    _awayTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown);
            }
            else
            {
                // Use any nearby player
                playersToConsider = GetAllPlayers();
            }
            
            Player nearestPlayer = null;
            float minDistance = float.MaxValue;
            
            foreach (var player in playersToConsider)
            {
                float dist = Vector2.Distance(player.FieldPosition, position);
                if (dist < minDistance)
                {
                    minDistance = dist;
                    nearestPlayer = player;
                }
            }
            
            // Move nearest player close to ball
            if (nearestPlayer != null && minDistance > 100f)
            {
                Vector2 directionToBall = position - nearestPlayer.FieldPosition;
                directionToBall.Normalize();
                nearestPlayer.FieldPosition = position - directionToBall * 80f;
            }
        }
        
        private void TriggerGoalCelebration()
        {
            CurrentState = MatchState.GoalCelebration;
            
            // Goal sound already played when goal was detected, just play crowd cheer
            AudioManager.Instance.PlaySoundEffect("crowd_cheer", 1.2f);
            
            // Start celebration with font rendering
            if (_font != null && _graphicsDevice != null)
            {
                string goalText = Models.Localization.Instance.Get("match.goal");
                GoalCelebration.Start(goalText, _font, _graphicsDevice);
            }
            else
            {
                GoalCelebration.Start(); // Fallback to empty
            }
            
            // Stop ball movement
            BallVelocity = Vector2.Zero;
            BallVerticalVelocity = 0f;
        }
        
        private bool ShouldPlayerChaseBall(Player player)
        {
            // Prevent clustering: only allow closest player per team to actively chase the ball
            // The 2nd closest should support but not rush directly to ball
            
            var team = player.TeamId == _homeTeam.Id ? _homeTeam : _awayTeam;
            
            // Get all non-knocked-down, non-goalkeeper teammates
            var activeTeammates = team.Players
                .Where(p => p.IsStarting && !p.IsKnockedDown && p.Position != PlayerPosition.Goalkeeper)
                .ToList();
            
            // Get distances to ball for all teammates
            var teamDistances = activeTeammates
                .Select(p => new { Player = p, Distance = Vector2.Distance(p.FieldPosition, BallPosition) })
                .OrderBy(x => x.Distance)
                .ToList();
            
            // Find current player's rank
            int playerRank = teamDistances.FindIndex(x => x.Player == player);
            
            // Goalkeeper can always chase if ball is close
            if (player.Position == PlayerPosition.Goalkeeper)
            {
                return true; // Goalkeeper logic already has its own distance check
            }
            
            // Only the closest non-goalkeeper player should actively chase
            // The 2nd closest will support but with different positioning (handled elsewhere)
            return playerRank == 0;
        }
        
        private Vector2 ApplyTeammateAvoidance(Player player, Vector2 desiredDirection)
        {
            // Apply separation force to avoid clustering with teammates
            var team = player.TeamId == _homeTeam.Id ? _homeTeam : _awayTeam;
            Vector2 separationForce = Vector2.Zero;
            int nearbyCount = 0;
            
            foreach (var teammate in team.Players.Where(p => p.IsStarting && p != player && !p.IsKnockedDown))
            {
                float distanceToTeammate = Vector2.Distance(player.FieldPosition, teammate.FieldPosition);
                
                if (distanceToTeammate < PlayerPersonalSpace && distanceToTeammate > 0.1f)
                {
                    // Push away from nearby teammates
                    Vector2 awayFromTeammate = player.FieldPosition - teammate.FieldPosition;
                    awayFromTeammate.Normalize();
                    
                    // Stronger force when closer
                    float strength = 1f - (distanceToTeammate / PlayerPersonalSpace);
                    separationForce += awayFromTeammate * strength;
                    nearbyCount++;
                }
            }
            
            if (nearbyCount > 0)
            {
                separationForce /= nearbyCount; // Average the force
                
                // Blend desired direction with separation force (70% desired, 30% separation)
                Vector2 blendedDirection = desiredDirection * 0.7f + separationForce * 0.3f;
                if (blendedDirection.Length() > 0.01f)
                {
                    blendedDirection.Normalize();
                    return blendedDirection;
                }
            }
            
            return desiredDirection;
        }
        
        private bool CanPlayerKickBall(Player player, Vector2 playerDirection, float maxDistance)
        {
            // Check if ball is within kick distance
            float distanceToBall = Vector2.Distance(player.FieldPosition, BallPosition);
            if (distanceToBall > maxDistance)
                return false;
            
            // Check if ball is on ground
            if (BallHeight > 50f)
                return false;
            
            // Check if ball is in front of player (within 120 degree cone)
            // This prevents kicking ball that's behind or too far to the side
            if (playerDirection.Length() > 0.01f)
            {
                Vector2 toBall = BallPosition - player.FieldPosition;
                if (toBall.Length() > 0.01f)
                {
                    toBall.Normalize();
                    Vector2 normalizedDirection = Vector2.Normalize(playerDirection);
                    
                    // Dot product gives us the angle (1 = same direction, -1 = opposite)
                    float dotProduct = Vector2.Dot(normalizedDirection, toBall);
                    
                    // Allow kick if ball is within ~120 degree cone in front (dot > -0.5)
                    // 0.5 = 60 degrees each side, -0.5 = 120 degrees each side
                    if (dotProduct < -0.2f) // ~102 degree cone (51 degrees each side)
                        return false;
                }
            }
            
            return true;
        }
        
        private void ResetAfterOut()
        {
            // Simple reset for now - could be improved with throw-ins, corners, etc.
            BallPosition = new Vector2(StadiumMargin + FieldWidth / 2, StadiumMargin + FieldHeight / 2);
            BallVelocity = Vector2.Zero;
            BallHeight = 0f;
            BallVerticalVelocity = 0f;
        }
        
        private void ResetAfterGoal()
        {
            BallPosition = new Vector2(StadiumMargin + FieldWidth / 2, StadiumMargin + FieldHeight / 2);
            BallVelocity = Vector2.Zero;
            BallHeight = 0f;
            BallVerticalVelocity = 0f;
            InitializePositions();
        }
        
        public void SwitchControlledPlayer()
        {
            if (_controlledPlayer == null) return;

            _controlledPlayer.IsControlled = false;

            var controlledTeam = _homeTeam.IsPlayerControlled ? _homeTeam : _awayTeam;

            // Get viewport bounds to find visible players
            float viewportWidth = 800f / Camera.Zoom; // Actual world units visible
            float viewportHeight = 600f / Camera.Zoom;

            Vector2 cameraPos = Camera.Position;
            float minX = cameraPos.X - viewportWidth / 2;
            float maxX = cameraPos.X + viewportWidth / 2;
            float minY = cameraPos.Y - viewportHeight / 2;
            float maxY = cameraPos.Y + viewportHeight / 2;

            // Find visible players that are NOT knocked down
            var visiblePlayers = controlledTeam.Players
                .Where(p => !p.IsKnockedDown && 
                           p.FieldPosition.X >= minX && p.FieldPosition.X <= maxX &&
                           p.FieldPosition.Y >= minY && p.FieldPosition.Y <= maxY)
                .OrderBy(p => Vector2.Distance(p.FieldPosition, BallPosition))
                .ToList();

            if (visiblePlayers.Any())
            {
                // Find current player in visible list
                int currentIndex = visiblePlayers.IndexOf(_controlledPlayer);
                int nextIndex = (currentIndex + 1) % visiblePlayers.Count;

                _controlledPlayer = visiblePlayers[nextIndex];
                _controlledPlayer.IsControlled = true;
            }
            else
            {
                // Fallback to nearest non-knocked player overall if no visible players
                var nearestPlayer = controlledTeam.Players
                    .Where(p => !p.IsKnockedDown)
                    .OrderBy(p => Vector2.Distance(p.FieldPosition, BallPosition))
                    .FirstOrDefault();

                if (nearestPlayer != null)
                {
                    _controlledPlayer = nearestPlayer;
                    _controlledPlayer.IsControlled = true;
                }
            }
        }
        
        public void Tackle()
        {
            if (_controlledPlayer == null) return;
            
            // Don't tackle if the controlled player has the ball
            if (_lastPlayerTouchedBall == _controlledPlayer)
            {
                return;
            }
            
            // Trigger tackle animation
            _controlledPlayer.CurrentAnimationState = "tackle";
            
            // Find nearest opponent with the ball
            var opposingTeam = _controlledPlayer.TeamId == _homeTeam.Id ? _awayTeam : _homeTeam;
            
            Player nearestOpponent = null;
            float minDistance = float.MaxValue;
            
            foreach (var opponent in opposingTeam.Players)
            {
                float dist = Vector2.Distance(_controlledPlayer.FieldPosition, opponent.FieldPosition);
                if (dist < minDistance && dist < TackleDistance)
                {
                    // Check if opponent is near the ball (possessing it)
                    if (Vector2.Distance(opponent.FieldPosition, BallPosition) < BallPossessionDistance)
                    {
                        nearestOpponent = opponent;
                        minDistance = dist;
                    }
                }
            }
            
            if (nearestOpponent != null)
            {
                // Calculate tackle success based on attributes with stamina effect
                float staminaMultiplier = GetStaminaStatMultiplier(_controlledPlayer);
                float tacklerDefending = _controlledPlayer.Defending * staminaMultiplier;
                float tacklerAgility = _controlledPlayer.Agility * staminaMultiplier;
                float opponentTechnique = nearestOpponent.Technique;
                float opponentAgility = nearestOpponent.Agility;
                
                // Success probability formula
                float tackleSuccess = TackleSuccessBase + 
                    (tacklerDefending * 0.3f) + 
                    (tacklerAgility * 0.2f) - 
                    (opponentTechnique * 0.2f) - 
                    (opponentAgility * 0.1f);
                
                // Decrease stamina for tackle attempt
                _controlledPlayer.Stamina = Math.Max(0, _controlledPlayer.Stamina - StaminaDecreasePerShot);
                
                // Random element
                float roll = (float)_random.NextDouble() * 100f;
                
                if (roll < tackleSuccess)
                {
                    // Successful tackle - ball bounces away from opponent
                    Vector2 tackleDirection = (nearestOpponent.FieldPosition - _controlledPlayer.FieldPosition);
                    if (tackleDirection.Length() > 0)
                    {
                        tackleDirection.Normalize();
                        // Ball goes to the side, not straight back
                        tackleDirection = Vector2.Transform(tackleDirection, 
                            Matrix.CreateRotationZ((_random.Next(2) == 0 ? 1 : -1) * MathHelper.PiOver4));
                        BallVelocity = tackleDirection * 150f;
                        _lastPlayerTouchedBall = _controlledPlayer;
                    }
                }
                else
                {
                    // Failed tackle - opponent keeps ball and gets a boost
                    Vector2 escapeDirection = (nearestOpponent.FieldPosition - _controlledPlayer.FieldPosition);
                    if (escapeDirection.Length() > 0)
                    {
                        escapeDirection.Normalize();
                        BallVelocity = escapeDirection * 100f;
                        _lastPlayerTouchedBall = nearestOpponent;
                    }
                }
            }
        }
        
        
        public bool IsChargingShot()
        {
            return _wasShootButtonDown && _shootButtonHoldTime > 0f;
        }
        
        public float GetShotPower()
        {
            return Math.Min(_shootButtonHoldTime / MaxShootHoldTime, 1f);
        }
        
        public List<Player> GetAllPlayers()
        {
            return _homeTeam.Players.Where(p => p.IsStarting).Concat(_awayTeam.Players.Where(p => p.IsStarting)).ToList();
        }
        
        private void EnsureStartingLineup(Team team)
        {
            var startingPlayers = team.Players.Where(p => p.IsStarting).ToList();
            
            // If no starting players marked, mark the first 11 as starting
            if (startingPlayers.Count == 0 && team.Players.Count >= 11)
            {
                for (int i = 0; i < 11 && i < team.Players.Count; i++)
                {
                    team.Players[i].IsStarting = true;
                }
            }
        }
        
        // Difficulty modifiers for AI
        private float GetAIDifficultyModifier()
        {
            int difficulty = GameSettings.Instance.Difficulty;
            return difficulty switch
            {
                0 => 0.7f,  // Easy: AI 30% worse
                1 => 1.0f,  // Normal: No change
                2 => 1.3f,  // Hard: AI 30% better
                _ => 1.0f
            };
        }
        
        private float GetAIReactionTimeMultiplier()
        {
            int difficulty = GameSettings.Instance.Difficulty;
            return difficulty switch
            {
                0 => 1.5f,  // Easy: AI reacts 50% slower
                1 => 1.0f,  // Normal: Normal reactions
                2 => 0.7f,  // Hard: AI reacts 30% faster
                _ => 1.0f
            };
        }
        
        // Stamina effects
        private float GetStaminaSpeedMultiplier(Player player)
        {
            if (player.Stamina < LowStaminaThreshold)
            {
                // Speed reduced by up to 30% when stamina is very low
                float staminaRatio = player.Stamina / LowStaminaThreshold;
                return 0.7f + (staminaRatio * 0.3f); // Range: 0.7 to 1.0
            }
            return 1.0f;
        }
        
        private float GetStaminaStatMultiplier(Player player)
        {
            if (player.Stamina < LowStaminaThreshold)
            {
                // All stats reduced by up to 40% when stamina is very low
                float staminaRatio = player.Stamina / LowStaminaThreshold;
                return 0.6f + (staminaRatio * 0.4f); // Range: 0.6 to 1.0
            }
            return 1.0f;
        }
    }
}
