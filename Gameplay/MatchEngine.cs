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
        private Random _random;
        
        // Referee
        public Vector2 RefereePosition { get; set; }
        private Vector2 _refereeVelocity;
        
        // Match state
        public enum MatchState { CameraInit, Countdown, Playing, HalfTime, Ended, GoalCelebration }
        public MatchState CurrentState { get; private set; }
        public float CountdownTimer { get; private set; }
        public int CountdownNumber { get; private set; }
        public GoalCelebration GoalCelebration { get; private set; }
        
        public Vector2 BallPosition { get; set; }
        public Vector2 BallVelocity { get; set; }
        public float BallHeight { get; set; } // Z-axis height
        public float BallVerticalVelocity { get; set; } // Vertical velocity (up/down)
        private float _shootButtonHoldTime = 0f;
        private bool _wasShootButtonDown = false;
        public int HomeScore { get; private set; }
        public int AwayScore { get; private set; }
        public float MatchTime { get; private set; }
        public bool IsMatchOver => MatchTime >= 90f;
        public Camera Camera { get; private set; }
        
        // Larger field with stadium area (scaled up for 64x64 sprites)
        public const float FieldWidth = 6400f;  // 2x larger than before
        public const float FieldHeight = 4800f; // 2x larger than before
        public const float StadiumMargin = 400f; // Space for stadium stands (2x larger)
        public const float TotalWidth = FieldWidth + (StadiumMargin * 2);
        public const float TotalHeight = FieldHeight + (StadiumMargin * 2);
        
        // Goal dimensions
        private const float GoalWidth = 400f; // 2x larger
        private const float GoalDepth = 60f;  // 2x larger
        
        // Ball physics
        private const float BallFriction = 0.95f;
        private const float BallPossessionDistance = 80f; // Scaled for larger sprites
        private const float BallKickDistance = 50f; // Scaled for larger sprites
        private const float TackleDistance = 70f; // Scaled for larger sprites
        private const float TackleSuccessBase = 40f; // Base tackle success %
        private const float Gravity = 1200f; // Gravity for ball vertical movement
        private const float MaxShootHoldTime = 1.5f; // Maximum time to hold shoot button
        private const float GoalPostHeight = 200f; // Height of goal posts
        
        // Viewport zoom (adjust to show desired portion of field)
        public const float ZoomLevel = 0.8f; // Higher value = more zoomed in, shows smaller area
        
        private Microsoft.Xna.Framework.Graphics.SpriteFont _font;
        private Microsoft.Xna.Framework.Graphics.GraphicsDevice _graphicsDevice;
        
        public MatchEngine(Team homeTeam, Team awayTeam, int viewportWidth, int viewportHeight)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _random = new Random();
            HomeScore = 0;
            AwayScore = 0;
            MatchTime = 0f;
            BallVelocity = Vector2.Zero;
            BallHeight = 0f;
            BallVerticalVelocity = 0f;
            
            // Initialize camera
            Camera = new Camera(viewportWidth, viewportHeight, ZoomLevel);
            
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
            
            // Find the first player controlled player
            var controlledTeamPlayers = _homeTeam.IsPlayerControlled ? _homeTeam.Players : _awayTeam.Players;
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
            if (team.Players.Count < 11) return;
            
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
            team.Players[0].FieldPosition = new Vector2(goalX, centerY);
            team.Players[0].HomePosition = new Vector2(goalX, centerY);
            
            // Defenders (4) - spread vertically
            float[] defenderY = { 0.2f, 0.4f, 0.6f, 0.8f };
            for (int i = 0; i < 4; i++)
            {
                team.Players[1 + i].FieldPosition = new Vector2(defenseX, yOffset + FieldHeight * defenderY[i]);
                team.Players[1 + i].HomePosition = new Vector2(defenseX, yOffset + FieldHeight * defenderY[i]);
            }
            
            // Midfielders (4)
            float[] midfielderY = { 0.2f, 0.4f, 0.6f, 0.8f };
            for (int i = 0; i < 4; i++)
            {
                team.Players[5 + i].FieldPosition = new Vector2(midfieldX, yOffset + FieldHeight * midfielderY[i]);
                team.Players[5 + i].HomePosition = new Vector2(midfieldX, yOffset + FieldHeight * midfielderY[i]);
            }
            
            // Forwards (2)
            team.Players[9].FieldPosition = new Vector2(attackX, yOffset + FieldHeight * 0.33f);
            team.Players[9].HomePosition = new Vector2(attackX, yOffset + FieldHeight * 0.33f);
            team.Players[10].FieldPosition = new Vector2(attackX, yOffset + FieldHeight * 0.67f);
            team.Players[10].HomePosition = new Vector2(attackX, yOffset + FieldHeight * 0.67f);
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
                
                if (CountdownTimer <= 0)
                {
                    CurrentState = MatchState.Playing;
                    AudioManager.Instance.PlaySoundEffect("whistle_start");
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
            
            // Update match time - map real time to game time (90 minutes)
            float realTimeDuration = GameSettings.Instance.GetMatchDurationSeconds();
            float gameTimeIncrement = (90f / realTimeDuration) * deltaTime;
            MatchTime += gameTimeIncrement;
            
            // Update all players
            UpdatePlayers(deltaTime, moveDirection, isShootKeyDown);
            
            // Check for collisions between players
            CheckPlayerCollisions(deltaTime);
            
            // Update ball physics
            UpdateBall(deltaTime);
            
            // Update referee position (follows ball at a distance)
            UpdateReferee(deltaTime);
            
            // Update camera to follow ball smoothly
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
                if (isShootKeyDown && distToBall < BallKickDistance * 1.5f && BallHeight < 50f)
                {
                    // Near ball - charge shot
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
                    if (distToBall < BallKickDistance * 1.5f && BallHeight < 50f)
                    {
                        // Near ball - shoot!
                        PerformShoot(moveDirection);
                    }
                    else
                    {
                        // Not near ball - try to tackle
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
                        float moveSpeed = _controlledPlayer.Speed * 3f * GameSettings.Instance.PlayerSpeedMultiplier;
                        var newPosition = _controlledPlayer.FieldPosition + moveDirection * moveSpeed * deltaTime;
                        _controlledPlayer.Velocity = moveDirection * moveSpeed;
                        ClampToField(ref newPosition);
                        _controlledPlayer.FieldPosition = newPosition;
                        
                        // If controlled player is near ball and moving, kick it
                        float distToBallForKick = Vector2.Distance(_controlledPlayer.FieldPosition, BallPosition);
                        if (distToBallForKick < BallKickDistance && moveDirection.Length() > 0.1f)
                        {
                            // Kick ball in movement direction
                            float kickPower = _controlledPlayer.Shooting / 10f + 5f;
                            BallVelocity = moveDirection * kickPower * _controlledPlayer.Speed;
                            AudioManager.Instance.PlaySoundEffect("kick_ball", 0.6f);
                        }
                    }
                    else
                    {
                        _controlledPlayer.Velocity = Vector2.Zero;
                    }
                }
            }
            
            // Update AI players
            foreach (var player in _homeTeam.Players.Concat(_awayTeam.Players))
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
                    if (distanceToBall < 150f)
                    {
                        targetPosition = BallPosition;
                        urgency = 1.0f;
                    }
                    else
                    {
                        targetPosition = player.HomePosition;
                        urgency = 0.3f;
                    }
                    break;
                
                case PlayerPosition.Defender:
                    bool ballInDefHalf = IsBallInHalf(player.TeamId);
                    if (ballInDefHalf || distanceToBall < 200f)
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
                    if (distanceToBall < 300f)
                    {
                        targetPosition = BallPosition;
                        urgency = 0.9f;
                    }
                    else
                    {
                        targetPosition = Vector2.Lerp(player.HomePosition, BallPosition, 0.5f);
                        urgency = 0.6f;
                    }
                    break;
                
                case PlayerPosition.Forward:
                    if (distanceToBall < 250f)
                    {
                        targetPosition = BallPosition;
                        urgency = 1.0f;
                    }
                    else
                    {
                        // Move toward opponent goal when not chasing ball
                        // Stay further from goal line to avoid getting stuck
                        bool isHomeTeam = player.TeamId == _homeTeam.Id;
                        float targetGoalX = isHomeTeam ? 
                            StadiumMargin + FieldWidth - 300f : 
                            StadiumMargin + 300f;
                        targetPosition = new Vector2(targetGoalX, player.HomePosition.Y);
                        urgency = 0.5f;
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
            
            if (distance > 15f)
            {
                direction.Normalize();
                float moveSpeed = player.Speed * 2.5f * urgency * GameSettings.Instance.PlayerSpeedMultiplier;
                var newPosition = player.FieldPosition + direction * moveSpeed * deltaTime;
                player.Velocity = direction * moveSpeed; // Store velocity for collision
                ClampToField(ref newPosition);
                player.FieldPosition = newPosition;
                
                // If AI player is near ball and moving toward it, kick it (only if ball is on ground)
                if (distanceToBall < BallKickDistance && urgency > 0.7f && BallHeight < 50f)
                {
                    // Kick ball toward opponent goal (aim AT the goal, not before it)
                    bool isHomeTeam = player.TeamId == _homeTeam.Id;
                    
                    // Aim at the goal center, with slight variation for realism
                    float goalCenterY = StadiumMargin + FieldHeight / 2;
                    float goalTop = StadiumMargin + (FieldHeight - GoalWidth) / 2;
                    float goalBottom = goalTop + GoalWidth;
                    
                    // Random Y position within goal area
                    float targetGoalY = goalTop + (float)_random.NextDouble() * GoalWidth;
                    
                    // Aim past the goal line (not before it)
                    float targetGoalX = isHomeTeam ? 
                        StadiumMargin + FieldWidth + 50f : // Right goal (aim past the line)
                        StadiumMargin - 50f; // Left goal (aim past the line)
                    
                    Vector2 goalDirection = new Vector2(targetGoalX, targetGoalY) - BallPosition;
                    if (goalDirection.Length() > 0)
                    {
                        goalDirection.Normalize();
                        float kickPower = player.Shooting / 15f + 3f;
                        BallVelocity = goalDirection * kickPower * player.Speed * 0.7f;
                        
                        // AI can do varied shots based on shooting skill and distance to goal
                        float distToGoal = Vector2.Distance(BallPosition, new Vector2(targetGoalX, targetGoalY));
                        float shootingSkill = player.Shooting / 100f; // 0-1 range
                        
                        // Calculate shot height based on:
                        // - Shooting skill (better players can do higher shots)
                        // - Distance to goal (further away = higher shot)
                        // - Random variation for realism
                        float baseHeight = 50f; // Minimum height (ground shot)
                        float skillBonus = shootingSkill * 300f; // Up to 300 extra based on skill
                        float distanceBonus = Math.Min(distToGoal / 20f, 200f); // Up to 200 based on distance
                        float randomFactor = (float)_random.NextDouble() * 150f; // Random 0-150
                        
                        // Occasionally do a high shot (20% chance for skilled players)
                        if (_random.NextDouble() < shootingSkill * 0.2f)
                        {
                            BallVerticalVelocity = baseHeight + skillBonus + distanceBonus + randomFactor;
                        }
                        else
                        {
                            // Most shots are lower
                            BallVerticalVelocity = baseHeight + randomFactor * 0.5f;
                        }
                        
                        // Play kick sound
                        AudioManager.Instance.PlaySoundEffect("kick_ball", 0.5f);
                    }
                }
            }
            else
            {
                // Player reached target, stop moving
                player.Velocity = Vector2.Zero;
            }
        }
        
        private void UpdateBall(float deltaTime)
        {
            // Apply horizontal velocity to ball position
            BallPosition += BallVelocity * deltaTime;
            
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
                            // Calculate knockdown probability based on:
                            // - Speed difference
                            // - Strength (defending stat)
                            // - Agility (helps avoid knockdown)
                            // - Randomness
                            
                            float p1Force = speed1 * (p1.Defending / 100f) * (1.0f - (p1.Agility / 100f) * 0.3f);
                            float p2Force = speed2 * (p2.Defending / 100f) * (1.0f - (p2.Agility / 100f) * 0.3f);
                            
                            // Boost collision intensity if near ball
                            if (nearBall)
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
                            else if (nearBall && (p1Force + p2Force) > 60f && randomFactor > 0.7f)
                            {
                                // Both get knocked down in intense collision near ball
                                KnockDownPlayer(p1, p2.Velocity * 0.5f);
                                KnockDownPlayer(p2, p1.Velocity * 0.5f);
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
            
            // Calculate horizontal and vertical velocity
            float basePower = _controlledPlayer.Shooting / 10f + 5f;
            float horizontalPower = basePower * (1f + power * 2f); // More power = faster
            BallVelocity = shootDirection * horizontalPower * _controlledPlayer.Speed;
            
            // Calculate vertical velocity (height)
            // More hold time = higher shot
            BallVerticalVelocity = power * 800f; // Max height with max hold
            
            // Play kick sound (louder for shooting)
            AudioManager.Instance.PlaySoundEffect("kick_ball", 0.8f + power * 0.4f);
        }
        
        private void ClampToField(ref Vector2 position)
        {
            position.X = MathHelper.Clamp(position.X, StadiumMargin, StadiumMargin + FieldWidth);
            position.Y = MathHelper.Clamp(position.Y, StadiumMargin, StadiumMargin + FieldHeight);
        }
        
        private void ClampBallToField()
        {
            float goalTop = StadiumMargin + (FieldHeight - GoalWidth) / 2;
            float goalBottom = goalTop + GoalWidth;
            
            // Check if ball is in goal area (horizontally)
            bool inLeftGoalArea = BallPosition.X < StadiumMargin && 
                                  BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom;
            bool inRightGoalArea = BallPosition.X > StadiumMargin + FieldWidth && 
                                   BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom;
            
            // Only clamp X if NOT in goal area (allow goals to happen)
            if (!inLeftGoalArea && !inRightGoalArea)
            {
                if (BallPosition.X < StadiumMargin)
                {
                    BallPosition = new Vector2(StadiumMargin, BallPosition.Y);
                    BallVelocity = new Vector2(-BallVelocity.X * 0.5f, BallVelocity.Y);
                }
                else if (BallPosition.X > StadiumMargin + FieldWidth)
                {
                    BallPosition = new Vector2(StadiumMargin + FieldWidth, BallPosition.Y);
                    BallVelocity = new Vector2(-BallVelocity.X * 0.5f, BallVelocity.Y);
                }
            }
            
            // Always clamp Y (top/bottom boundaries)
            if (BallPosition.Y < StadiumMargin)
            {
                BallPosition = new Vector2(BallPosition.X, StadiumMargin);
                BallVelocity = new Vector2(BallVelocity.X, -BallVelocity.Y * 0.5f);
            }
            else if (BallPosition.Y > StadiumMargin + FieldHeight)
            {
                BallPosition = new Vector2(BallPosition.X, StadiumMargin + FieldHeight);
                BallVelocity = new Vector2(BallVelocity.X, -BallVelocity.Y * 0.5f);
            }
        }
        
        private void CheckGoal()
        {
            float goalTop = StadiumMargin + (FieldHeight - GoalWidth) / 2;
            float goalBottom = goalTop + GoalWidth;
            float leftGoalLine = StadiumMargin;
            float rightGoalLine = StadiumMargin + FieldWidth;
            
            // Define out-of-bounds areas (with some buffer for goal depth)
            float leftOutBound = StadiumMargin - GoalDepth - 20;
            float rightOutBound = StadiumMargin + FieldWidth + GoalDepth + 20;
            float topOutBound = StadiumMargin - 20;
            float bottomOutBound = StadiumMargin + FieldHeight + 20;
            
            // Check if ball is below goal post height (not too high)
            bool ballBelowGoalHeight = BallHeight <= GoalPostHeight;
            
            // === GOAL DETECTION ===
            // Left goal (home team defends) - Ball must cross the goal line
            if (BallPosition.X < leftGoalLine && 
                BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom &&
                ballBelowGoalHeight)
            {
                AwayScore++;
                TriggerGoalCelebration();
                return;
            }
            // Right goal (away team defends) - Ball must cross the goal line
            else if (BallPosition.X > rightGoalLine && 
                     BallPosition.Y >= goalTop && BallPosition.Y <= goalBottom &&
                     ballBelowGoalHeight)
            {
                HomeScore++;
                TriggerGoalCelebration();
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
            if (BallPosition.X < leftOutBound)
            {
                // Ball went out on left side - corner or goal kick
                HandleBallOutGoalLine(true, BallPosition.Y);
                return;
            }
            else if (BallPosition.X > rightOutBound)
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
            // Place ball for corner kick or goal kick
            float xPos = leftSide ? StadiumMargin + 30 : StadiumMargin + FieldWidth - 30;
            float yPos = yPosition < StadiumMargin + FieldHeight / 2 ? 
                StadiumMargin + 30 : StadiumMargin + FieldHeight - 30;
            
            PlaceBallForRestart(new Vector2(xPos, yPos));
        }
        
        private void HandleBallOutSideline(float xPosition, bool topSide)
        {
            // Place ball for throw-in
            float xPos = Math.Clamp(xPosition, StadiumMargin + 50, StadiumMargin + FieldWidth - 50);
            float yPos = topSide ? StadiumMargin + 20 : StadiumMargin + FieldHeight - 20;
            
            PlaceBallForRestart(new Vector2(xPos, yPos));
        }
        
        private void PlaceBallForRestart(Vector2 position)
        {
            BallPosition = position;
            BallVelocity = Vector2.Zero;
            BallHeight = 0f;
            BallVerticalVelocity = 0f;
            
            // Find nearest player from team that gets the ball
            // For now, just find any nearby player
            var allPlayers = GetAllPlayers();
            Player nearestPlayer = null;
            float minDistance = float.MaxValue;
            
            foreach (var player in allPlayers)
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
            
            // Play goal sound effects
            AudioManager.Instance.PlaySoundEffect("goal");
            AudioManager.Instance.PlaySoundEffect("crowd_cheer", 1.2f);
            
            // Start celebration with font rendering
            if (_font != null && _graphicsDevice != null)
            {
                GoalCelebration.Start("ΓΚΟΛ!", _font, _graphicsDevice);
            }
            else
            {
                GoalCelebration.Start(); // Fallback to empty
            }
            
            // Stop ball movement
            BallVelocity = Vector2.Zero;
            BallVerticalVelocity = 0f;
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
                // Calculate tackle success based on attributes
                float tacklerDefending = _controlledPlayer.Defending;
                float tacklerAgility = _controlledPlayer.Agility;
                float opponentTechnique = nearestOpponent.Technique;
                float opponentAgility = nearestOpponent.Agility;
                
                // Success probability formula
                float tackleSuccess = TackleSuccessBase + 
                    (tacklerDefending * 0.3f) + 
                    (tacklerAgility * 0.2f) - 
                    (opponentTechnique * 0.2f) - 
                    (opponentAgility * 0.1f);
                
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
            return _homeTeam.Players.Concat(_awayTeam.Players).ToList();
        }
    }
}
