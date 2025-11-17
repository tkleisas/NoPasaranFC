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
        public enum MatchState { CameraInit, Countdown, Playing, HalfTime, Ended }
        public MatchState CurrentState { get; private set; }
        public float CountdownTimer { get; private set; }
        public int CountdownNumber { get; private set; }
        
        public Vector2 BallPosition { get; set; }
        public Vector2 BallVelocity { get; set; }
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
        
        // Viewport zoom (adjust to show desired portion of field)
        public const float ZoomLevel = 0.8f; // Higher value = more zoomed in, shows smaller area
        
        public MatchEngine(Team homeTeam, Team awayTeam, int viewportWidth, int viewportHeight)
        {
            _homeTeam = homeTeam;
            _awayTeam = awayTeam;
            _random = new Random();
            HomeScore = 0;
            AwayScore = 0;
            MatchTime = 0f;
            BallVelocity = Vector2.Zero;
            BallVelocity = Vector2.Zero;
            
            // Initialize camera
            Camera = new Camera(viewportWidth, viewportHeight, ZoomLevel);
            
            // Initialize referee position (center of field)
            RefereePosition = new Vector2(StadiumMargin + FieldWidth / 2, StadiumMargin + FieldHeight / 2);
            _refereeVelocity = Vector2.Zero;
            
            // Start with camera initialization (center on ball first)
            CurrentState = MatchState.CameraInit;
            CountdownTimer = 0.5f; // Half second to center camera
            CountdownNumber = 3;
            
            InitializePositions();
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
        
        public void Update(GameTime gameTime, Vector2 moveDirection)
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
            
            // Update match time - map real time to game time (90 minutes)
            float realTimeDuration = GameSettings.Instance.GetMatchDurationSeconds();
            float gameTimeIncrement = (90f / realTimeDuration) * deltaTime;
            MatchTime += gameTimeIncrement;
            
            // Update all players
            UpdatePlayers(deltaTime, moveDirection);
            
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
        
        private void UpdatePlayers(float deltaTime, Vector2 moveDirection)
        {
            // Update controlled player
            if (_controlledPlayer != null)
            {
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
                        float distToBall = Vector2.Distance(_controlledPlayer.FieldPosition, BallPosition);
                        if (distToBall < BallKickDistance && moveDirection.Length() > 0.1f)
                        {
                            // Kick ball in movement direction
                            float kickPower = _controlledPlayer.Shooting / 10f + 5f;
                            BallVelocity = moveDirection * kickPower * _controlledPlayer.Speed;
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
                        bool isHomeTeam = player.TeamId == _homeTeam.Id;
                        float targetGoalX = isHomeTeam ? 
                            StadiumMargin + FieldWidth - 100f : 
                            StadiumMargin + 100f;
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
                
                // If AI player is near ball and moving toward it, kick it
                if (distanceToBall < BallKickDistance && urgency > 0.7f)
                {
                    // Kick ball toward opponent goal
                    bool isHomeTeam = player.TeamId == _homeTeam.Id;
                    float targetGoalX = isHomeTeam ? 
                        StadiumMargin + FieldWidth - 100f : 
                        StadiumMargin + 100f;
                    float targetGoalY = StadiumMargin + FieldHeight / 2;
                    
                    Vector2 goalDirection = new Vector2(targetGoalX, targetGoalY) - BallPosition;
                    if (goalDirection.Length() > 0)
                    {
                        goalDirection.Normalize();
                        float kickPower = player.Shooting / 15f + 3f;
                        BallVelocity = goalDirection * kickPower * player.Speed * 0.7f;
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
            // Apply velocity to ball position
            BallPosition += BallVelocity * deltaTime;
            
            // Apply friction
            BallVelocity *= BallFriction;
            
            // Stop ball if moving very slowly
            if (BallVelocity.Length() < 1f)
            {
                BallVelocity = Vector2.Zero;
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
            player.KnockdownTimer = 1.5f + (float)_random.NextDouble() * 1.0f; // 1.5 to 2.5 seconds
            
            // Apply impact velocity (player slides in direction of impact)
            player.Velocity = impactVelocity * 0.5f;
            
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
        
        private void ClampToField(ref Vector2 position)
        {
            position.X = MathHelper.Clamp(position.X, StadiumMargin, StadiumMargin + FieldWidth);
            position.Y = MathHelper.Clamp(position.Y, StadiumMargin, StadiumMargin + FieldHeight);
        }
        
        private void ClampBallToField()
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
            
            // Left goal (home team defends)
            if (BallPosition.X <= StadiumMargin + GoalDepth && 
                BallPosition.Y > goalTop && BallPosition.Y < goalBottom)
            {
                AwayScore++;
                ResetAfterGoal();
            }
            // Right goal (away team defends)
            else if (BallPosition.X >= StadiumMargin + FieldWidth - GoalDepth && 
                     BallPosition.Y > goalTop && BallPosition.Y < goalBottom)
            {
                HomeScore++;
                ResetAfterGoal();
            }
        }
        
        private void ResetAfterGoal()
        {
            BallPosition = new Vector2(StadiumMargin + FieldWidth / 2, StadiumMargin + FieldHeight / 2);
            BallVelocity = Vector2.Zero;
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
        
        public void Shoot()
        {
            if (_controlledPlayer == null) return;
            
            float distToBall = Vector2.Distance(_controlledPlayer.FieldPosition, BallPosition);
            
            // Check if player is near the ball - if so, shoot
            if (distToBall < BallKickDistance * 1.5f)
            {
                // Shoot toward opponent goal
                bool isHomeTeam = _controlledPlayer.TeamId == _homeTeam.Id;
                float targetGoalX = isHomeTeam ? 
                    StadiumMargin + FieldWidth - 50f : 
                    StadiumMargin + 50f;
                float targetGoalY = StadiumMargin + FieldHeight / 2;
                
                Vector2 shootDirection = new Vector2(targetGoalX, targetGoalY) - BallPosition;
                if (shootDirection.Length() > 0)
                {
                    shootDirection.Normalize();
                    float shootPower = _controlledPlayer.Shooting / 5f + 10f;
                    BallVelocity = shootDirection * shootPower * _controlledPlayer.Speed * 1.5f;
                }
            }
            else if (distToBall < TackleDistance * 2f)
            {
                // Not close enough to shoot, try to tackle instead
                Tackle();
            }
        }
        
        public List<Player> GetAllPlayers()
        {
            return _homeTeam.Players.Concat(_awayTeam.Players).ToList();
        }
    }
}
