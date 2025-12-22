using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;
using NoPasaranFC.Gameplay.Celebrations;

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
        public enum MatchState { CameraInit, Countdown, Playing, HalfTime, Ended, GoalCelebration, FinalScore, ThrowIn, CornerKick, GoalKick }
        public MatchState CurrentState { get; private set; }
        public float CountdownTimer { get; private set; }
        public int CountdownNumber { get; private set; }
        public GoalCelebration GoalCelebration { get; private set; }
        public CelebrationManager CelebrationManager { get; private set; }
        public float FinalScoreTimer { get; private set; }
        
        // Set piece state
        public float RestartTimer { get; private set; }
        public Player RestartPlayer { get; private set; }
        public Vector2 RestartDirection { get; private set; } = Vector2.Zero;
        public float ThrowInPowerCharge { get; private set; } = 0f; // 0.0 to 1.0
        private bool _throwInAnimationStarted = false; // Track if throw animation is playing
        private float _timeSinceSetPiece = 0f; // Tracks time since set piece was executed
        private bool _cornerKickRunUp = false; // Track if player is running up to kick corner
        public bool IsCornerKickRunUp => _cornerKickRunUp; // Public accessor for UI
        private Vector2 _cornerKickStartPosition; // Where player starts run-up from
        
        private float _timeSinceKickoff; // Tracks time since last kickoff
        
        public Vector2 BallPosition { get; set; }
        public Vector2 BallVelocity { get; set; }
        public float BallHeight { get; set; } // Z-axis height
        public float BallVerticalVelocity { get; set; } // Vertical velocity (up/down)
        private float _shootButtonHoldTime = 0f;
        private bool _wasShootButtonDown = false;
        private bool _goalScored = false;
        private bool _isOwnGoal = false;
        private float _goalCelebrationDelay = 0f;
        private const float GoalCelebrationDelayTime = 0.5f; // Half second delay to see ball in goal
        private float _normalZoom;
        private const float CelebrationZoomOut = 0.65f; // Zoom out to 65% during celebration
        private const float AutoKickCooldown = 0.3f; // Cooldown between automatic kicks (300ms)
        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }
        public float MatchTime { get; private set; }
        public bool IsMatchOver => MatchTime >= 90f;
        public Camera Camera { get; private set; }
        
        // Track ball half for hysteresis (prevent oscillation at center line)
        private bool _ballInHomeHalfCache = true; // Home team's half
        
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
        private const float BallPossessionDistance = 20f; // Small - actual sprites smaller than boundary
        private const float BallKickDistance = 5f; // Very tight control, accounts for transparent pixels
        private const float BallShootDistance = 70f; // Distance for charged shots (must be > collision distance ~56px)
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
            
            // Set team references for all players
            foreach (var player in homeTeam.Players)
            {
                player.Team = homeTeam;
            }
            foreach (var player in awayTeam.Players)
            {
                player.Team = awayTeam;
            }
            
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
            _normalZoom = zoom; // Store normal zoom for restoration after celebration

            // Initialize referee position (center of field)
            RefereePosition = new Vector2(StadiumMargin + FieldWidth / 2, StadiumMargin + FieldHeight / 2);
            _refereeVelocity = Vector2.Zero;
            
            // Initialize goal celebration
            GoalCelebration = new GoalCelebration();
            CelebrationManager = new CelebrationManager();

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
            startingPlayers[0].Role = PlayerRole.Goalkeeper;
            
            // Defenders (4) - spread vertically with specific roles
            float[] defenderY = { 0.2f, 0.4f, 0.6f, 0.8f };
            PlayerRole[] defenderRoles = { PlayerRole.LeftBack, PlayerRole.CenterBack, PlayerRole.CenterBack, PlayerRole.RightBack };
            for (int i = 0; i < 4; i++)
            {
                startingPlayers[1 + i].FieldPosition = new Vector2(defenseX, yOffset + FieldHeight * defenderY[i]);
                startingPlayers[1 + i].HomePosition = new Vector2(defenseX, yOffset + FieldHeight * defenderY[i]);
                startingPlayers[1 + i].Role = defenderRoles[i];
            }
            
            // Midfielders (4) with specific roles
            float[] midfielderY = { 0.2f, 0.4f, 0.6f, 0.8f };
            PlayerRole[] midfielderRoles = { PlayerRole.LeftMidfielder, PlayerRole.CentralMidfielder, PlayerRole.CentralMidfielder, PlayerRole.RightMidfielder };
            for (int i = 0; i < 4; i++)
            {
                startingPlayers[5 + i].FieldPosition = new Vector2(midfieldX, yOffset + FieldHeight * midfielderY[i]);
                startingPlayers[5 + i].HomePosition = new Vector2(midfieldX, yOffset + FieldHeight * midfielderY[i]);
                startingPlayers[5 + i].Role = midfielderRoles[i];
            }
            
            // Forwards (2)
            startingPlayers[9].FieldPosition = new Vector2(attackX, yOffset + FieldHeight * 0.33f);
            startingPlayers[9].HomePosition = new Vector2(attackX, yOffset + FieldHeight * 0.33f);
            startingPlayers[9].Role = PlayerRole.Striker;
            
            startingPlayers[10].FieldPosition = new Vector2(attackX, yOffset + FieldHeight * 0.67f);
            startingPlayers[10].HomePosition = new Vector2(attackX, yOffset + FieldHeight * 0.67f);
            startingPlayers[10].Role = PlayerRole.Striker;
            
            // Initialize AI controllers for all starting players
            foreach (var player in startingPlayers)
            {
                player.AIController = new AIController(player);
                
                // Register callbacks for AI actions
                var aiController = player.AIController as AIController;
                if (aiController != null)
                {
                    // Capture the current player for the callback
                    var capturedPlayer = player;
                    aiController.RegisterPassCallback((targetPosition, power) => AIPassBall(capturedPlayer, targetPosition, power));
                    aiController.RegisterShootCallback((targetPosition, power) => AIShootBall(capturedPlayer, targetPosition, power));
                }
            }
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
                    _timeSinceKickoff = 0f; // Reset kickoff timer when play starts
                }
                else
                {
                    // Update countdown number (3, 2, 1)
                    CountdownNumber = (int)Math.Ceiling(CountdownTimer);
                }
                
                // Allow AI players to move into position during countdown
                // Player can't kick the ball (moveDirection = Zero, isShootKeyDown = false)
                UpdatePlayers(deltaTime, Vector2.Zero, false);
                
                // Keep ball stationary during countdown
                BallVelocity = Vector2.Zero;
                BallVerticalVelocity = 0f;
                
                // Keep camera following ball
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
                    _isOwnGoal = false; // Reset own goal flag
                }
                // Let ball physics continue during delay
            }
            
            // Handle goal celebration
            if (CurrentState == MatchState.GoalCelebration)
            {
                // Smoothly zoom based on celebration preference
                float zoomMultiplier = CelebrationManager.CurrentCelebration?.GetCameraZoomMultiplier() ?? CelebrationZoomOut;
                float targetZoom = _normalZoom * zoomMultiplier;
                Camera.Zoom = MathHelper.Lerp(Camera.Zoom, targetZoom, deltaTime * 2f);

                // Check if user wants to skip celebration (any key press) - only after 5 seconds
                const float MinCelebrationTime = 5.0f;
                if (GoalCelebration.Timer >= MinCelebrationTime && (moveDirection.LengthSquared() > 0 || isShootKeyDown))
                {
                    GoalCelebration.Stop();
                    CelebrationManager.StopCelebration();
                }

                GoalCelebration.Update(deltaTime);
                CelebrationManager.Update(deltaTime);

                if (!GoalCelebration.IsActive)
                {
                    // Celebration ended, reset for kickoff with countdown
                    CelebrationManager.StopCelebration();

                    // Smoothly zoom back in to normal
                    Camera.Zoom = MathHelper.Lerp(Camera.Zoom, _normalZoom, deltaTime * 2f);

                    ResetAfterGoal();
                    CurrentState = MatchState.Countdown;
                    CountdownTimer = 3.5f; // 3 seconds countdown + 0.5 for "GO!"
                    CountdownNumber = 3;
                }

                // Update players during celebration (only AI updates, handled by CelebrationManager)
                UpdatePlayersPhysics(deltaTime);

                // Get camera target from celebration manager
                Vector2 cameraTarget = CelebrationManager.GetCameraTarget();
                Camera.Follow(cameraTarget, deltaTime);
                return;
            }
            else
            {
                // Not in celebration - ensure zoom is at normal level
                Camera.Zoom = MathHelper.Lerp(Camera.Zoom, _normalZoom, deltaTime * 3f);
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
            _timeSinceKickoff += deltaTime; // Update kickoff timer
            _timeSinceSetPiece += deltaTime; // Update set piece timer
            
            // Check if match should end
            if (MatchTime >= 90f)
            {
                CurrentState = MatchState.FinalScore;
                FinalScoreTimer = 5f;
                AudioManager.Instance.PlaySoundEffect("whistle_end");
                return;
            }
            
            // Handle set pieces (ThrowIn, CornerKick, GoalKick)
            if (CurrentState == MatchState.ThrowIn || CurrentState == MatchState.CornerKick || CurrentState == MatchState.GoalKick)
            {
                RestartTimer -= deltaTime;
                
                // Keep camera on restart player
                if (RestartPlayer != null)
                {
                    Camera.Follow(RestartPlayer.FieldPosition, deltaTime);
                    
                    // Update restart player animation (idle/aiming)
                    if (CurrentState == MatchState.ThrowIn)
                    {
                        RestartPlayer.IsThrowingIn = true;
                        RestartPlayer.Velocity = Vector2.Zero; // Ensure stationary
                    }
                    else
                    {
                        // For corners/goal kicks, also keep stationary
                        RestartPlayer.Velocity = Vector2.Zero;
                    }
                    
                    // Handle input for controlled player
                    if (RestartPlayer.IsControlled)
                    {
                        // Handle throw-in specific controls
                        if (CurrentState == MatchState.ThrowIn)
                        {
                            // Use left/right to rotate direction
                            if (moveDirection.X != 0 || moveDirection.Y != 0)
                            {
                                // Rotate the current direction based on left/right input
                                float rotationSpeed = 2.5f * deltaTime; // Radians per second
                                float currentAngle = (float)Math.Atan2(RestartDirection.Y, RestartDirection.X);
                                
                                if (moveDirection.X < 0) // Left key
                                {
                                    currentAngle -= rotationSpeed;
                                }
                                else if (moveDirection.X > 0) // Right key
                                {
                                    currentAngle += rotationSpeed;
                                }
                                
                                RestartDirection = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle));
                            }
                            
                            // Handle power charging
                            if (isShootKeyDown)
                            {
                                // Charge power (0.0 to 1.0 over 1 second)
                                ThrowInPowerCharge = Math.Min(1.0f, ThrowInPowerCharge + deltaTime);
                            }
                            else if (_wasShootButtonDown)
                            {
                                // Released - start throw animation (ball will be released when animation finishes)
                                RestartPlayer.CurrentAnimationState = "throw_in_throw";
                                _throwInAnimationStarted = true;
                                _wasShootButtonDown = false;
                            }
                            
                            // Track button state
                            if (isShootKeyDown && !_wasShootButtonDown)
                            {
                                _wasShootButtonDown = true;
                            }
                        }
                        else if (CurrentState == MatchState.CornerKick)
                        {
                            if (_cornerKickRunUp)
                            {
                                // Player is running toward ball - handled in UpdatePlayers
                                // Check if player reached the ball
                                float distToBall = Vector2.Distance(RestartPlayer.FieldPosition, BallPosition);
                                if (distToBall < 30f)
                                {
                                    // Player reached ball - execute kick
                                    ExecuteSetPieceAction();
                                    _cornerKickRunUp = false;
                                }
                            }
                            else
                            {
                                // Corner kick: rotate direction with arrow keys, charge power with X
                                if (moveDirection.X != 0 || moveDirection.Y != 0)
                                {
                                    // Rotate the current direction based on input
                                    float rotationSpeed = 2.5f * deltaTime;
                                    float currentAngle = (float)Math.Atan2(RestartDirection.Y, RestartDirection.X);
                                    
                                    if (moveDirection.X < 0)
                                        currentAngle -= rotationSpeed;
                                    else if (moveDirection.X > 0)
                                        currentAngle += rotationSpeed;
                                    
                                    RestartDirection = new Vector2((float)Math.Cos(currentAngle), (float)Math.Sin(currentAngle));
                                }
                                
                                // Handle power charging
                                if (isShootKeyDown)
                                {
                                    ThrowInPowerCharge = Math.Min(1.0f, ThrowInPowerCharge + deltaTime);
                                }
                                else if (_wasShootButtonDown)
                                {
                                    // Released - start run-up toward ball
                                    _cornerKickRunUp = true;
                                    _wasShootButtonDown = false;
                                }
                                
                                if (isShootKeyDown && !_wasShootButtonDown)
                                {
                                    _wasShootButtonDown = true;
                                }
                            }
                        }
                        else
                        {
                            // For goal kicks, use arrow keys for direction and execute on press
                            if (moveDirection.Length() > 0.1f)
                            {
                                RestartDirection = Vector2.Normalize(moveDirection);
                            }
                            
                            // Execute immediately on press
                            if (isShootKeyDown && !_wasShootButtonDown)
                            {
                                ExecuteSetPieceAction();
                                _wasShootButtonDown = true;
                            }
                            else if (!isShootKeyDown)
                            {
                                _wasShootButtonDown = false;
                            }
                        }
                    }
                    else
                    {
                        // AI Logic for set pieces
                        // For throw-ins and corners, wait longer to give teammates time to position
                        float executeThreshold = (CurrentState == MatchState.ThrowIn || CurrentState == MatchState.CornerKick) ? 2.0f : 0.5f;
                        if (RestartTimer < executeThreshold)
                        {
                            // Calculate best direction
                            Vector2 target = Vector2.Zero;
                            if (CurrentState == MatchState.GoalKick)
                            {
                                // Aim far forward
                                float forwardX = RestartPlayer.Team == _homeTeam ? RestartPlayer.FieldPosition.X + 500 : RestartPlayer.FieldPosition.X - 500;
                                target = new Vector2(forwardX, RestartPlayer.FieldPosition.Y);
                            }
                            else if (CurrentState == MatchState.CornerKick)
                            {
                                // Aim at goal area
                                float goalX = RestartPlayer.Team == _homeTeam ? StadiumMargin + FieldWidth : StadiumMargin;
                                target = new Vector2(goalX, StadiumMargin + FieldHeight / 2);
                                // AI uses high power (80-100%) for effective corners
                                ThrowInPowerCharge = 0.80f + (float)_random.NextDouble() * 0.20f;
                            }
                            else // ThrowIn - improved AI
                            {
                                target = FindBestThrowInTarget(RestartPlayer);
                                // AI uses high power (85-100%) for effective throws
                                ThrowInPowerCharge = 0.85f + (float)_random.NextDouble() * 0.15f;
                            }
                            
                            RestartDirection = Vector2.Normalize(target - RestartPlayer.FieldPosition);
                            
                            // Play throw animation for AI throw-ins, start run-up for corners
                            if (CurrentState == MatchState.ThrowIn)
                            {
                                RestartPlayer.CurrentAnimationState = "throw_in_throw";
                                _throwInAnimationStarted = true;
                            }
                            else if (CurrentState == MatchState.CornerKick)
                            {
                                // Start run-up for AI corner kick
                                _cornerKickRunUp = true;
                            }
                            else
                            {
                                // Execute goal kick immediately
                                ExecuteSetPieceAction();
                            }
                        }
                    }
                }
                
                // Auto-execute if timer expires
                if (RestartTimer <= 0)
                {
                    // Default direction if none set
                    if (RestartDirection == Vector2.Zero)
                    {
                        float forwardX = (RestartPlayer != null && RestartPlayer.Team == _homeTeam) ? 1 : -1;
                        RestartDirection = new Vector2(forwardX, 0);
                    }
                    ExecuteSetPieceAction();
                }
                
                // Don't return here! We want UpdatePlayers to run so teammates can move.
                // But we need to make sure the RestartPlayer stays pinned in UpdatePlayers.
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
        
        private void UpdatePlayersPhysics(float deltaTime)
        {
            // Update player positions based on their velocities (set by CelebrationManager)
            foreach (var player in _homeTeam.Players.Where(p => p.IsStarting).Concat(_awayTeam.Players.Where(p => p.IsStarting)))
            {
                // Apply velocity to position
                player.FieldPosition += player.Velocity * deltaTime;

                // Clamp to field
                Vector2 pos = player.FieldPosition;
                ClampToField(ref pos);
                player.FieldPosition = pos;
            }
        }

        private void UpdatePlayers(float deltaTime, Vector2 moveDirection, bool isShootKeyDown)
        {
            // During celebration, all players use AI (including controlled player)
            bool celebrationActive = CurrentState == MatchState.GoalCelebration;

            // Update controlled player
            if (_controlledPlayer != null && !celebrationActive)
            {
                float distToBall = Vector2.Distance(_controlledPlayer.FieldPosition, BallPosition);
                
                // Handle shoot button for charging shot or tackle
                if (isShootKeyDown && distToBall < BallShootDistance && BallHeight < 100f)
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
                    if (distToBall < BallShootDistance && BallHeight < 100f)
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
                    // Check if player is in corner kick run-up mode
                    bool inCornerRunUp = CurrentState == MatchState.CornerKick && 
                                         _controlledPlayer == RestartPlayer && 
                                         _cornerKickRunUp;
                    
                    if (inCornerRunUp)
                    {
                        // During corner kick run-up, move automatically toward ball
                        Vector2 toBall = BallPosition - _controlledPlayer.FieldPosition;
                        if (toBall.Length() > 1f)
                        {
                            toBall.Normalize();
                            _controlledPlayer.Velocity = toBall * _controlledPlayer.Speed * 3.5f;
                            _controlledPlayer.FieldPosition += _controlledPlayer.Velocity * deltaTime;
                        }
                    }
                    else
                    {
                        // Check if player should be frozen during set piece (not during run-up)
                        bool frozenForSetPiece = (CurrentState == MatchState.ThrowIn || 
                                                  CurrentState == MatchState.CornerKick || 
                                                  CurrentState == MatchState.GoalKick) &&
                                                 _controlledPlayer == RestartPlayer;
                        
                        // Normal movement (only if not knocked down and not frozen for set piece)
                        if (moveDirection.Length() > 0 && !frozenForSetPiece)
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
                            // Don't kick during countdown
                            // Don't auto-kick if player is charging a shot
                            if (CurrentState == MatchState.Playing && moveDirection.Length() > 0.1f && BallHeight < 100f && !isShootKeyDown && CanPlayerKickBall(_controlledPlayer, moveDirection, BallKickDistance))
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
            }
            
            // Update AI players (only starting players)
            foreach (var player in _homeTeam.Players.Where(p => p.IsStarting).Concat(_awayTeam.Players.Where(p => p.IsStarting)))
            {
                // During celebration, update ALL players including controlled player
                if (player.IsControlled && !celebrationActive) continue;

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
            
            // Handle set piece behavior (ThrowIn, CornerKick, GoalKick)
            if (CurrentState == MatchState.ThrowIn || CurrentState == MatchState.CornerKick || CurrentState == MatchState.GoalKick)
            {
                // If this is the restart player, handle special movement
                if (player == RestartPlayer)
                {
                    // During corner kick run-up, move toward ball
                    if (CurrentState == MatchState.CornerKick && _cornerKickRunUp)
                    {
                        Vector2 toBall = BallPosition - player.FieldPosition;
                        if (toBall.Length() > 1f)
                        {
                            toBall.Normalize();
                            player.Velocity = toBall * player.Speed * 3.5f; // Run fast toward ball
                            player.FieldPosition += player.Velocity * deltaTime;
                        }
                    }
                    else
                    {
                        // Stay put during aiming phase
                        player.Velocity = Vector2.Zero;
                    }
                    return;
                }
                
                // If teammate of restart player, move closer to offer support
                if (RestartPlayer != null && player.Team == RestartPlayer.Team)
                {
                    Vector2 targetPosition;
                    float targetDist;
                    
                    if (CurrentState == MatchState.ThrowIn)
                    {
                        // For throw-ins, nearest player moves toward the ball
                        targetPosition = BallPosition;
                        targetDist = 100f; // Get close to receive the throw
                    }
                    else
                    {
                        // For corners/goal kicks, spread out around restarter
                        targetPosition = RestartPlayer.FieldPosition;
                        targetDist = 200f; // Ideal support distance
                    }
                    
                    float distToTarget = Vector2.Distance(player.FieldPosition, targetPosition);
                    
                    // Move closer if too far, but keep distance (don't crowd)
                    if (distToTarget > targetDist + 50f)
                    {
                        // Move towards target
                        Vector2 dir = Vector2.Normalize(targetPosition - player.FieldPosition);
                        player.Velocity = dir * player.Speed * 2.5f; // Run towards ball/restarter
                    }
                    else if (distToTarget < targetDist - 50f)
                    {
                        // Too close, back off slightly
                        Vector2 dir = Vector2.Normalize(player.FieldPosition - targetPosition);
                        player.Velocity = dir * player.Speed * 1.0f;
                    }
                    else
                    {
                        // Good distance, stop and wait
                        player.Velocity = Vector2.Zero;
                    }
                    
                    // Update position
                    player.FieldPosition += player.Velocity * deltaTime;
                    Vector2 pos = player.FieldPosition;
                    ClampToField(ref pos);
                    player.FieldPosition = pos;
                    
                    // Face the ball/restarter
                    Vector2 lookDir = targetPosition - player.FieldPosition;
                    if (lookDir.Length() > 0.1f)
                    {
                        // Update sprite direction to face target
                        if (Math.Abs(lookDir.X) > Math.Abs(lookDir.Y))
                            player.SpriteDirection = lookDir.X > 0 ? 3 : 2;
                        else
                            player.SpriteDirection = lookDir.Y > 0 ? 0 : 1;
                    }
                    
                    return; // Skip normal AI logic
                }
                
                // If opponent, mark/defend (simplified: stay put or move towards ball if close)
                if (RestartPlayer != null && player.Team != RestartPlayer.Team)
                {
                    // Opponents stay put or mark nearby players (simplified: stop)
                    player.Velocity = Vector2.Zero;
                    return;
                }
            }
            
            if (player.AIController is AIController aiController)
            {
                // Build context for AI
                var context = BuildAIContext(player);
                
                // Update AI controller (states set player.Velocity with base speed)
                aiController.Update(context, deltaTime);
                
                // Store base velocity for next frame (states need this for direction smoothing)
                Vector2 baseVelocity = player.Velocity;
                
                // Apply game settings and difficulty multipliers to AI velocity
                float staminaMultiplier = GetStaminaSpeedMultiplier(player);
                float difficultyMultiplier = GetAIDifficultyModifier();
                float settingsMultiplier = GameSettings.Instance.PlayerSpeedMultiplier;
                
                // Apply all multipliers to velocity and update position
                Vector2 adjustedVelocity = baseVelocity * staminaMultiplier * difficultyMultiplier * settingsMultiplier;
                player.FieldPosition += adjustedVelocity * deltaTime;
                
                // Keep BASE velocity in player.Velocity for states to use (NOT adjusted)
                // Animation uses velocity direction, not magnitude, so this is fine
                player.Velocity = baseVelocity;
                
                // Clamp player to field
                Vector2 pos = player.FieldPosition;
                ClampToField(ref pos);
                player.FieldPosition = pos;
                
                // AI dribbling: Kick ball automatically when close and moving (similar to player control)
                // Don't kick during countdown
                if (CurrentState == MatchState.Playing && baseVelocity.LengthSquared() > 0.01f && BallHeight < 100f)
                {
                    float distToBall = Vector2.Distance(player.FieldPosition, BallPosition);
                    if (distToBall < BallShootDistance * 0.6f)
                    {
                        Vector2 moveDirection = Vector2.Normalize(baseVelocity);
                        
                        // SIMPLIFIED: Just kick the ball in movement direction when close
                        // No position checks - allows backheel kicks, side kicks, etc.
                        // This eliminates oscillation caused by positioning requirements
                        float timeSinceLastKick = (float)MatchTime - player.LastKickTime;
                        if (timeSinceLastKick >= AutoKickCooldown)
                        {
                            // Kick ball in movement direction
                            float staminaStatMultiplier = GetStaminaStatMultiplier(player);
                            float kickPower = (player.Shooting / 8f + 6f) * staminaStatMultiplier * GetAIDifficultyModifier();
                            BallVelocity = moveDirection * kickPower * player.Speed * 1.0f;
                            BallVerticalVelocity = 15f; // Very low kick for dribbling
                            _lastPlayerTouchedBall = player;
                            player.LastKickTime = (float)MatchTime;
                        }
                    }
                }
            }
            else
            {
                // Fallback to old positioning behavior if no AI controller
                // (This should not happen in normal gameplay)
                player.Velocity = Vector2.Zero;
                player.Stamina = Math.Min(100, player.Stamina + StaminaRecoveryPerSecond * deltaTime);
            }
        }
        
        private AIContext BuildAIContext(Player player)
        {
            // Use Name comparison for robustness as requested
            bool isHomeTeam = player.Team != null && player.Team.Name == _homeTeam.Name;
            var myTeam = isHomeTeam ? _homeTeam : _awayTeam; 
            var opponentTeam = isHomeTeam ? _awayTeam : _homeTeam;
            
            // Find nearest opponent and teammate
            Player nearestOpponent = null;
            float nearestOpponentDist = float.MaxValue;
            foreach (var opponent in opponentTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown))
            {
                float dist = Vector2.Distance(player.FieldPosition, opponent.FieldPosition);
                if (dist < nearestOpponentDist)
                {
                    nearestOpponentDist = dist;
                    nearestOpponent = opponent;
                }
            }
            
            Player nearestTeammate = null;
            float nearestTeammateDist = float.MaxValue;
            Player bestPassTarget = null;
            float bestPassScore = float.MinValue;
            
            // Get list of active opponents for blocking checks
            var activeOpponents = opponentTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown).ToList();

            foreach (var teammate in myTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown && p != player))
            {
                float dist = Vector2.Distance(player.FieldPosition, teammate.FieldPosition);
                if (dist < nearestTeammateDist)
                {
                    nearestTeammateDist = dist;
                    nearestTeammate = teammate;
                }
                
                // Calculate pass score (closer to opponent goal = better)
                Vector2 opponentGoalCenter = isHomeTeam ? 
                    new Vector2(StadiumMargin + FieldWidth, StadiumMargin + FieldHeight / 2) :
                    new Vector2(StadiumMargin, StadiumMargin + FieldHeight / 2);
                float distToGoal = Vector2.Distance(teammate.FieldPosition, opponentGoalCenter);
                float passScore = 1000f - distToGoal; // Lower distance to goal = higher score
                
                // Check if pass path is blocked by opponent
                if (IsPathBlocked(player.FieldPosition, teammate.FieldPosition, activeOpponents, 60f))
                {
                    passScore -= 5000f; // Heavy penalty for blocked path
                }
                
                if (passScore > bestPassScore)
                {
                    bestPassScore = passScore;
                    bestPassTarget = teammate;
                }
            }
            
            float distanceToBall = Vector2.Distance(player.FieldPosition, BallPosition);
            bool hasControl = _lastPlayerTouchedBall == player && distanceToBall < 80f;
            bool shouldChaseBall = ShouldPlayerChaseBall(player);
            
            Vector2 ownGoalCenter = isHomeTeam ? 
                new Vector2(StadiumMargin, StadiumMargin + FieldHeight / 2) :
                new Vector2(StadiumMargin + FieldWidth, StadiumMargin + FieldHeight / 2);
                
            Vector2 opponentGoalCenterFinal = isHomeTeam ? 
                new Vector2(StadiumMargin + FieldWidth, StadiumMargin + FieldHeight / 2) :
                new Vector2(StadiumMargin, StadiumMargin + FieldHeight / 2);
            
            bool ballInDefensiveHalf = IsBallInHalf(player.Team.Name);
            bool ballInAttackingHalf = !ballInDefensiveHalf;
            
            return new AIContext
            {
                BallPosition = BallPosition,
                BallVelocity = BallVelocity,
                BallHeight = BallHeight,
                NearestOpponent = nearestOpponent,
                NearestTeammate = nearestTeammate,
                BestPassTarget = bestPassTarget,
                DistanceToBall = distanceToBall,
                HasBallPossession = hasControl,
                OpponentGoalCenter = opponentGoalCenterFinal,
                OwnGoalCenter = ownGoalCenter,
                IsPlayerTeam = isHomeTeam,
                IsHomeTeam = isHomeTeam,
                Random = _random,
                ClosestToBall = GetPlayerClosestToBall(),
                ShouldChaseBall = shouldChaseBall,
                MatchTime = MatchTime,
                TimeSinceKickoff = _timeSinceKickoff,
                IsDefensiveHalf = ballInDefensiveHalf,
                IsAttackingHalf = ballInAttackingHalf,
                Teammates = myTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown && p != player).ToList(),
                Opponents = opponentTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown).ToList()
            };
        }

        private bool IsPathBlocked(Vector2 start, Vector2 end, List<Player> obstacles, float threshold = 50f)
        {
            Vector2 direction = end - start;
            float distance = direction.Length();
            
            if (distance < 0.01f) return false;
            
            direction.Normalize();
            
            foreach (var obstacle in obstacles)
            {
                // Project obstacle position onto the line segment
                Vector2 toObstacle = obstacle.FieldPosition - start;
                float projection = Vector2.Dot(toObstacle, direction);
                
                // Check if projection is within the segment
                if (projection > 0 && projection < distance)
                {
                    // Calculate perpendicular distance
                    Vector2 closestPoint = start + direction * projection;
                    float distToLine = Vector2.Distance(obstacle.FieldPosition, closestPoint);
                    
                    if (distToLine < threshold)
                    {
                        return true;
                    }
                }
            }
            
            return false;
        }

        private void AIPassBall(Player passer, Vector2 targetPosition, float power)
        {
            // Callback for passing state
            if (BallHeight < 100f)
            {
                Vector2 passDirection = targetPosition - BallPosition;
                float passDistance = passDirection.Length();
                
                if (passDistance > 0)
                {
                    passDirection.Normalize();
                    
                    // Determine which team the passer belongs to
                    bool passerIsHomeTeam = _homeTeam.Players.Any(p => p.Id == passer.Id);
                    Team opposingTeam = passerIsHomeTeam ? _awayTeam : _homeTeam;
                    
                    // Check if there are defenders in the path (lofted pass detection)
                    bool needsLoftedPass = false;
                    int defendersInPath = 0;
                    
                    foreach (var opponent in opposingTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown))
                    {
                        // Check if opponent is in the pass corridor
                        Vector2 toOpponent = opponent.FieldPosition - BallPosition;
                        float dotProduct = Vector2.Dot(toOpponent, passDirection);
                        
                        // Opponent is ahead in pass direction
                        if (dotProduct > 0 && dotProduct < passDistance)
                        {
                            // Check perpendicular distance from pass line
                            Vector2 projectedPoint = BallPosition + passDirection * dotProduct;
                            float perpDistance = Vector2.Distance(opponent.FieldPosition, projectedPoint);
                            
                            // If opponent is close to pass line (within 150 pixels)
                            if (perpDistance < 150f)
                            {
                                defendersInPath++;
                            }
                        }
                    }
                    
                    // Use lofted pass if 2+ defenders in path or pass is very long
                    needsLoftedPass = defendersInPath >= 2 || passDistance > 800f;
                    
                    float passPower = (passer.Passing / 10f + power * 5f) * GetStaminaStatMultiplier(passer);
                    BallVelocity = passDirection * passPower * passer.Speed;
                    
                    // Lofted pass: higher arc, goes over defenders
                    if (needsLoftedPass)
                    {
                        // Higher vertical velocity based on distance
                        BallVerticalVelocity = 200f + (passDistance / 10f);
                        // Reduce horizontal speed slightly for the arc
                        BallVelocity *= 0.85f;
                    }
                    else
                    {
                        // Ground pass: low trajectory
                        BallVerticalVelocity = 30f;
                    }
                    
                    _lastPlayerTouchedBall = passer;
                    passer.LastKickTime = (float)MatchTime;
                    AudioManager.Instance.PlaySoundEffect("kick_ball", needsLoftedPass ? 0.6f : 0.4f, allowRetrigger: false);
                }
            }
        }
        
        private void AIShootBall(Player shooter, Vector2 targetPosition, float power)
        {
            // Callback for shooting state
            if (BallHeight < 100f)
            {
                Vector2 shootDirection = targetPosition - BallPosition;
                if (shootDirection.LengthSquared() > 0)
                {
                    shootDirection.Normalize();
                    float shootPower = (shooter.Shooting / 8f + power * 10f) * GetStaminaStatMultiplier(shooter);
                    BallVelocity = shootDirection * shootPower * shooter.Speed;
                    BallVerticalVelocity = 100f + (float)_random.NextDouble() * 200f;
                    _lastPlayerTouchedBall = shooter;
                    shooter.LastKickTime = (float)MatchTime;
                    AudioManager.Instance.PlaySoundEffect("kick_ball", 0.7f, allowRetrigger: false);
                }
            }
        }
        
        private void UpdateBall(float deltaTime)
        {
            // During throw-in, keep ball at player's hands position
            if (CurrentState == MatchState.ThrowIn && RestartPlayer != null && _throwInAnimationStarted)
            {
                // Ball follows player's position (held overhead)
                BallPosition = RestartPlayer.FieldPosition;
                BallVelocity = Vector2.Zero;
                BallHeight = 180f; // Ball held above player's head (player is ~128px tall)
                BallVerticalVelocity = 0f;
                return; // Don't apply physics while holding ball
            }
            
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
                    bool sameTeam = p1.Team == p2.Team;
                    
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
            // Don't affect ball immediately after set piece execution
            if (_timeSinceSetPiece < 0.3f) return;
            
            // Check if any player is colliding with the ball
            var allPlayers = GetAllPlayers();
            const float ballRadius = 16f; // Ball is 32x32, so radius is 16
            const float playerRadius = 40f; // Player collision radius (smaller than sprite size)
            const float collisionDistance = ballRadius + playerRadius;
            
            foreach (var player in allPlayers)
            {
                // Skip if player is knocked down or ball is too high in the air
                // Allow players to intercept low balls but not high aerial passes
                if (player.IsKnockedDown || BallHeight > 100f) continue;
                
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
        
        private bool IsBallInHalf(string teamName)
        {
            float centerX = StadiumMargin + FieldWidth / 2;
            bool isHomeTeam = teamName == _homeTeam.Name;
            
            // Hysteresis zone around center line to prevent oscillation
            // Ball must move 100 pixels past center to change half
            const float hysteresisZone = 100f;
            
            // Check current ball position
            bool ballActuallyInHomeHalf = BallPosition.X < centerX;
            
            // Update cache only if ball has moved significantly past center
            if (ballActuallyInHomeHalf)
            {
                // Ball in left side
                if (BallPosition.X < centerX - hysteresisZone)
                {
                    // Clearly in home half
                    _ballInHomeHalfCache = true;
                }
                // else: in hysteresis zone, keep previous state
            }
            else
            {
                // Ball in right side
                if (BallPosition.X > centerX + hysteresisZone)
                {
                    // Clearly in away half
                    _ballInHomeHalfCache = false;
                }
                // else: in hysteresis zone, keep previous state
            }
            
            // Return appropriate value based on team
            if (isHomeTeam)
                return _ballInHomeHalfCache; // Home team: is ball in their (left) half?
            else
                return !_ballInHomeHalfCache; // Away team: is ball in their (right) half?
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
            
            // GOAL ENTRY LOGIC:
            // If within the vertical range of the goal, allow X to go deeper (into the net)
            float goalTop = StadiumMargin + (FieldHeight - GoalWidth) / 2;
            float goalBottom = goalTop + GoalWidth;
            bool inGoalWidth = position.Y >= goalTop && position.Y <= goalBottom;
            
            float minX = StadiumMargin - OutOfBoundsMargin;
            float maxX = StadiumMargin + FieldWidth + OutOfBoundsMargin;
            
            if (inGoalWidth)
            {
                // Allow entering the goal net (extra depth)
                minX -= GoalDepth; 
                maxX += GoalDepth;
            }
            
            position.X = MathHelper.Clamp(position.X, minX, maxX);
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
            // Don't check for goals/out-of-bounds during set pieces
            if (CurrentState == MatchState.ThrowIn || 
                CurrentState == MatchState.CornerKick || 
                CurrentState == MatchState.GoalKick)
            {
                return;
            }
            
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

                // Check if it's an own goal (home team scored on themselves)
                _isOwnGoal = _lastPlayerTouchedBall != null && _lastPlayerTouchedBall.Team == _homeTeam;

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

                // Check if it's an own goal (away team scored on themselves)
                _isOwnGoal = _lastPlayerTouchedBall != null && _lastPlayerTouchedBall.Team == _awayTeam;

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
            
            PlaceBallForRestart(new Vector2(xPos, yPos), null, MatchState.GoalKick);
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
                    giveToHomeTeam = isCornerKick ? false : true; // Corner: give to away, Goal kick: give to home
                }
                else // right side
                {
                    // Right goal (defended by away team)
                    isCornerKick = !lastTouchWasHomeTeam; // Away defender touched = corner for home
                    giveToHomeTeam = isCornerKick ? true : false; // Corner: give to home, Goal kick: give to away
                }
            }
            
            float xPos, yPos;
            
            if (isCornerKick)
            {
                // Corner kick - place near corner
                xPos = leftSide ? StadiumMargin + 50 : StadiumMargin + FieldWidth - 50;
                yPos = yPosition < StadiumMargin + FieldHeight / 2 ? 
                    StadiumMargin + 50 : StadiumMargin + FieldHeight - 50;
            }
            else
            {
                // Goal kick - place in LARGE penalty area (not small), centered horizontally
                xPos = leftSide ? StadiumMargin + 500 : StadiumMargin + FieldWidth - 500;
                yPos = StadiumMargin + FieldHeight / 2;
            }
            
            PlaceBallForRestart(new Vector2(xPos, yPos), giveToHomeTeam, isCornerKick ? MatchState.CornerKick : MatchState.GoalKick);
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
            
            PlaceBallForRestart(new Vector2(xPos, yPos), giveToHomeTeam, MatchState.ThrowIn);
        }
        
        private void PlaceBallForRestart(Vector2 position, bool? preferHomeTeam = null, MatchState restartState = MatchState.Playing)
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
            if (nearestPlayer != null)
            {
                // For throw-ins, place player slightly behind the line
                // For corners/goal kicks, place near ball
                Vector2 placementOffset = Vector2.Zero;
                
                if (restartState == MatchState.ThrowIn)
                {
                    // Determine sideline direction (up or down)
                    bool isTopSideline = position.Y < StadiumMargin + FieldHeight / 2;
                    // Place player on the sideline, properly positioned for throw
                    float yOffset = isTopSideline ? -50f : 50f;
                    nearestPlayer.FieldPosition = new Vector2(position.X, position.Y + yOffset);
                }
                else if (restartState == MatchState.CornerKick)
                {
                    // Corner kick - place player further behind ball for run-up (80px back)
                    Vector2 directionToCenter = new Vector2(StadiumMargin + FieldWidth/2, StadiumMargin + FieldHeight/2) - position;
                    if (directionToCenter != Vector2.Zero) directionToCenter.Normalize();
                    nearestPlayer.FieldPosition = position - directionToCenter * 80f;
                    _cornerKickStartPosition = nearestPlayer.FieldPosition;
                    _cornerKickRunUp = false;
                }
                else
                {
                    // Goal kick - place slightly behind ball relative to center
                    Vector2 directionToCenter = new Vector2(StadiumMargin + FieldWidth/2, StadiumMargin + FieldHeight/2) - position;
                    if (directionToCenter != Vector2.Zero) directionToCenter.Normalize();
                    nearestPlayer.FieldPosition = position - directionToCenter * 40f;
                }
                
                // Set restart state
                RestartPlayer = nearestPlayer;
                RestartTimer = 5.0f;
                CurrentState = restartState;
                
                // Default direction towards center
                Vector2 toCenter = new Vector2(StadiumMargin + FieldWidth/2, StadiumMargin + FieldHeight/2) - nearestPlayer.FieldPosition;
                if (toCenter != Vector2.Zero)
                    RestartDirection = Vector2.Normalize(toCenter);
                else
                    RestartDirection = new Vector2(1, 0);
            }
        }
        
        public void ExecuteThrowIn()
        {
            if (RestartPlayer == null || !_throwInAnimationStarted) return;
            
            // Execute the actual throw-in action
            ExecuteSetPieceAction();
            _throwInAnimationStarted = false;
        }
        
        private void ExecuteSetPieceAction()
        {
            if (RestartPlayer == null) return;
            
            // Reset player state
            RestartPlayer.IsThrowingIn = false;
            RestartPlayer.CurrentAnimationState = "idle"; // Reset animation state
            
            // Apply velocity to ball
            float power = 0f;
            float height = 0f;
            
            if (CurrentState == MatchState.ThrowIn)
            {
                // Power ranges from 200f (minimum) to 500f (maximum) based on charge
                float minPower = 200f;
                float maxPower = 500f;
                float chargeAmount = Math.Max(0.1f, ThrowInPowerCharge); // Minimum 10% charge
                power = minPower + (maxPower - minPower) * chargeAmount;
                
                // Height scales with power (short throws are flatter, long throws arc higher)
                height = 40f + 120f * chargeAmount;
                
                float volume = 0.4f + 0.3f * chargeAmount;
                AudioManager.Instance.PlaySoundEffect("kick_ball", volume);
            }
            else if (CurrentState == MatchState.CornerKick)
            {
                // Power ranges from 500f (minimum) to 2400f (maximum) based on charge
                float minPower = 500f;
                float maxPower = 2400f;
                float chargeAmount = Math.Max(0.1f, ThrowInPowerCharge); // Minimum 10% charge
                power = minPower + (maxPower - minPower) * chargeAmount;
                
                // Height scales with power (short corners are flatter, long corners arc higher)
                height = 200f + 400f * chargeAmount;
                
                float volume = 0.5f + 0.5f * chargeAmount;
                AudioManager.Instance.PlaySoundEffect("kick_ball", volume);
            }
            else if (CurrentState == MatchState.GoalKick)
            {
                power = 25f; // Goal kick power
                height = 200f; // Very high arc
                AudioManager.Instance.PlaySoundEffect("kick_ball", 1.0f);
            }
            
            // Apply velocity based on set piece type
            if (CurrentState == MatchState.ThrowIn || CurrentState == MatchState.CornerKick)
            {
                // Throw-in and Corner: direct power application
                BallVelocity = RestartDirection * power;
            }
            else
            {
                // Goal kicks: scale by player speed
                BallVelocity = RestartDirection * power * RestartPlayer.Speed * 0.05f;
            }
            BallVerticalVelocity = height;
            _lastPlayerTouchedBall = RestartPlayer;
            RestartPlayer.LastKickTime = (float)MatchTime;
            
            // Reset throw-in power
            ThrowInPowerCharge = 0f;
            
            // Reset corner kick run-up flag
            _cornerKickRunUp = false;
            
            // Reset set piece timer to protect ball from immediate collisions
            _timeSinceSetPiece = 0f;
            
            // Reset state
            CurrentState = MatchState.Playing;
            RestartPlayer = null;
        }
        
        private Player FindNearestTeammate(Player player)
        {
            Player nearest = null;
            float minDist = float.MaxValue;
            foreach (var teammate in player.Team.Players.Where(p => p.IsStarting && p != player && !p.IsKnockedDown))
            {
                float dist = Vector2.Distance(player.FieldPosition, teammate.FieldPosition);
                if (dist < minDist)
                {
                    minDist = dist;
                    nearest = teammate;
                }
            }
            return nearest;
        }
        
        private Vector2 FindBestThrowInTarget(Player thrower)
        {
            if (thrower == null) return thrower.FieldPosition + new Vector2(100, 0);
            
            bool throwingHomeTeam = thrower.Team == _homeTeam;
            float attackingDirection = throwingHomeTeam ? 1f : -1f;
            
            // Evaluate all teammates for throw-in target
            Player bestTarget = null;
            float bestScore = float.MinValue;
            
            foreach (var teammate in thrower.Team.Players.Where(p => p.IsStarting && p != thrower && !p.IsKnockedDown))
            {
                float score = 0f;
                float distance = Vector2.Distance(thrower.FieldPosition, teammate.FieldPosition);
                
                // Skip if too far (max realistic throw range ~600px)
                if (distance > 600f) continue;
                
                // Prefer forward passes (attacking direction)
                float forwardProgress = (teammate.FieldPosition.X - thrower.FieldPosition.X) * attackingDirection;
                if (forwardProgress > 0)
                    score += forwardProgress * 0.5f; // Bonus for forward throws
                else
                    score += forwardProgress * 0.1f; // Penalty for backward throws
                
                // Prefer teammates in open space (fewer opponents nearby)
                int nearbyOpponents = GetOpponentsWithinRadius(teammate.FieldPosition, 150f, thrower.Team).Count();
                score -= nearbyOpponents * 100f; // Penalty for crowded areas
                
                // Prefer midfielders and forwards over defenders for throw-ins
                if (teammate.Position == PlayerPosition.Midfielder || 
                    teammate.Position == PlayerPosition.Forward)
                {
                    score += 50f;
                }
                
                // Slight preference for closer teammates (easier throws)
                score += (600f - distance) * 0.1f;
                
                // Avoid throwing to teammates too close to sideline
                float distanceFromTopEdge = teammate.FieldPosition.Y - StadiumMargin;
                float distanceFromBottomEdge = (StadiumMargin + FieldHeight) - teammate.FieldPosition.Y;
                float minDistanceFromEdge = Math.Min(distanceFromTopEdge, distanceFromBottomEdge);
                if (minDistanceFromEdge < 100f)
                    score -= 150f; // Penalty for sideline proximity
                
                if (score > bestScore)
                {
                    bestScore = score;
                    bestTarget = teammate;
                }
            }
            
            // If no good target found, throw towards attacking direction in open space
            if (bestTarget == null)
            {
                float targetX = thrower.FieldPosition.X + attackingDirection * 300f;
                float targetY = StadiumMargin + FieldHeight / 2; // Center of field
                return new Vector2(targetX, targetY);
            }
            
            return bestTarget.FieldPosition;
        }
        
        private IEnumerable<Player> GetOpponentsWithinRadius(Vector2 position, float radius, Team friendlyTeam)
        {
            Team opponentTeam = friendlyTeam == _homeTeam ? _awayTeam : _homeTeam;
            return opponentTeam.Players.Where(p => 
                p.IsStarting && 
                !p.IsKnockedDown && 
                Vector2.Distance(p.FieldPosition, position) <= radius);
        }
        
        private void TriggerGoalCelebration()
        {
            CurrentState = MatchState.GoalCelebration;

            // Goal sound already played when goal was detected, just play crowd cheer
            AudioManager.Instance.PlaySoundEffect("crowd_cheer", 1.2f, allowRetrigger: false);

            // Stop ball movement
            BallVelocity = Vector2.Zero;
            BallVerticalVelocity = 0f;

            // Start player celebration using the new CelebrationManager
            if (_lastPlayerTouchedBall != null)
            {
                List<Player> teammates;
                List<Player> opponents;

                if (_isOwnGoal)
                {
                    // Own goal - BOTH teams celebrate together!
                    // The "scorer" is the player who made the own goal
                    teammates = _homeTeam.Players
                        .Where(p => p.IsStarting && p != _lastPlayerTouchedBall && !p.IsKnockedDown)
                        .Concat(_awayTeam.Players.Where(p => p.IsStarting && p != _lastPlayerTouchedBall && !p.IsKnockedDown))
                        .ToList();

                    // No opponents - everyone celebrates
                    opponents = new List<Player>();
                }
                else
                {
                    // Normal goal - only scoring team celebrates
                    teammates = _lastPlayerTouchedBall.Team.Players
                        .Where(p => p.IsStarting && p != _lastPlayerTouchedBall && !p.IsKnockedDown)
                        .ToList();

                    var opponentTeam = _lastPlayerTouchedBall.Team == _homeTeam ? _awayTeam : _homeTeam;
                    opponents = opponentTeam.Players
                        .Where(p => p.IsStarting && !p.IsKnockedDown)
                        .ToList();
                }

                // Start celebration (own goals always use "run_around_pitch")
                CelebrationManager.StartCelebration(_lastPlayerTouchedBall, teammates, opponents, _isOwnGoal);
            }

            // Start visual celebration (ball animation and text) - must be after CelebrationManager.StartCelebration
            // so we can get the custom duration from the active celebration
            float? customDuration = CelebrationManager.CurrentCelebration?.GetCelebrationDuration();
            if (_font != null && _graphicsDevice != null)
            {
                string goalText = Models.Localization.Instance.Get("match.goal");
                GoalCelebration.Start(goalText, _font, _graphicsDevice, customDuration);
            }
            else
            {
                GoalCelebration.Start(); // Fallback to empty
            }
        }
        
        private bool ShouldPlayerChaseBall(Player player)
        {
            // Prevent clustering: only allow closest player per team to actively chase the ball
            // The 2nd closest should support but not rush directly to ball
            
            var team = player.Team;
            
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
        
        private Player GetPlayerClosestToBall()
        {
            Player closest = null;
            float closestDist = float.MaxValue;
            
            foreach (var player in _homeTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown)
                .Concat(_awayTeam.Players.Where(p => p.IsStarting && !p.IsKnockedDown)))
            {
                float dist = Vector2.Distance(player.FieldPosition, BallPosition);
                if (dist < closestDist)
                {
                    closestDist = dist;
                    closest = player;
                }
            }
            
            return closest;
        }
        
        private Vector2 ApplyTeammateAvoidance(Player player, Vector2 desiredDirection)
        {
            // Apply separation force to avoid clustering with teammates
            var team = player.Team;
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
            if (BallHeight > 100f)
                return false;
            
            // Check if ball is in front of player (within 120 degree cone)
            // This prevents kicking ball that's behind or too far to the side
            if (playerDirection.Length() > 0.01f)
            {
                Vector2 toBall = BallPosition - player.FieldPosition;
                {
                    toBall.Normalize();
                    Vector2 normalizedDirection = Vector2.Normalize(playerDirection);
                    
                    // Dot product gives us the angle (1 = same direction, -1 = opposite)
                    float dotProduct = Vector2.Dot(normalizedDirection, toBall);
                    
                    // RELAXED CONSTRAINT: Allow kicking from almost any angle (360 degrees)
                    // This enables "heel kicks" (kicking backward) and side kicks
                    // Only prevent kicking if ball is EXTREMELY far behind (optional, but safer)
                    // -0.95f allows almost everything except directly behind at distance
                    if (dotProduct < -0.95f && distanceToBall > 20f) 
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
            var opposingTeam = _controlledPlayer.Team == _homeTeam ? _awayTeam : _homeTeam;
            
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
        
        // Debug methods for triggering set pieces
        public void DebugTriggerThrowIn()
        {
            if (CurrentState != MatchState.Playing) return;
            
            // Place ball at center of the field on top sideline
            float xPos = StadiumMargin + FieldWidth / 2;
            float yPos = StadiumMargin + 20; // Top sideline
            
            // Give throw-in to player's team
            bool giveToHomeTeam = _homeTeam.IsPlayerControlled;
            PlaceBallForRestart(new Vector2(xPos, yPos), giveToHomeTeam, MatchState.ThrowIn);
        }
        
        public void DebugTriggerCornerKick()
        {
            if (CurrentState != MatchState.Playing) return;
            
            // Place corner for player's team (attacking corner)
            bool giveToHomeTeam = _homeTeam.IsPlayerControlled;
            float cornerX = giveToHomeTeam ? StadiumMargin + FieldWidth - 20 : StadiumMargin + 20;
            float cornerY = StadiumMargin + 20; // Top corner
            
            PlaceBallForRestart(new Vector2(cornerX, cornerY), giveToHomeTeam, MatchState.CornerKick);
        }
        
        public void DebugTriggerGoalKick()
        {
            if (CurrentState != MatchState.Playing) return;
            
            // Place goal kick for player's team
            bool giveToHomeTeam = _homeTeam.IsPlayerControlled;
            float goalKickX = giveToHomeTeam ? StadiumMargin + 100 : StadiumMargin + FieldWidth - 100;
            float goalKickY = StadiumMargin + FieldHeight / 2;
            
            PlaceBallForRestart(new Vector2(goalKickX, goalKickY), giveToHomeTeam, MatchState.GoalKick);
        }
        
        public void DebugTriggerGoal()
        {
            if (CurrentState != MatchState.Playing) return;
            
            // Score a goal for the player's team
            if (_homeTeam.IsPlayerControlled)
            {
                HomeScore++;
                _lastPlayerTouchedBall = _controlledPlayer;
            }
            else
            {
                AwayScore++;
                _lastPlayerTouchedBall = _controlledPlayer;
            }
            
            _goalScored = true;
            _isOwnGoal = false;
            _goalCelebrationDelay = 0f;
            AudioManager.Instance.PlaySoundEffect("goal", allowRetrigger: false);
        }
    }
}
