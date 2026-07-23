using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.UtilityAI
{
    public enum UtilityActionType { Idle, ChaseBall, HoldPosition, Dribble, Pass, Shoot, Clear }
    
    /// <summary>The chosen action for a decision period.</summary>
    public class UtilityAction
    {
        public UtilityActionType Type;
        public Vector2 Point;          // movement target (steering)
        public Player TargetPlayer;    // pass target etc.
        public float Score;
        
        public UtilityAction(UtilityActionType type, Vector2 point, float score, Player target = null)
        {
            Type = type;
            Point = point;
            Score = score;
            TargetPlayer = target;
        }
    }
    
    /// <summary>
    /// Utility-based AI brain: replaces the per-player FSM. Every decision tick
    /// candidate actions are scored from context; the current action gets a
    /// commitment bonus so boundary flapping dies by design. Movement executes
    /// through steering behaviors (smooth by construction).
    ///
    /// One instance per player (holds the current action + eval timer).
    /// </summary>
    public class UtilityBrain
    {
        private UtilityAction _current;
        private float _evalTimer;
        private float _carrierStallTimer;
        private readonly Random _random;
        
        // Decision tuning
        private const float EvalInterval = 0.25f;
        private const float CommitmentBonus = 15f;
        private const float PlayerMaxSpeed = 137f; // matches typical AI velocity (stat 55 * 2.5)
        
        // Ball callbacks (same delegates the old states used)
        private readonly Action<Player, Vector2, float> _passBall;
        private readonly Action<Player, Vector2, float> _shootBall;
        
        public UtilityBrain(Random random, Action<Player, Vector2, float> passBall,
            Action<Player, Vector2, float> shootBall)
        {
            _random = random;
            _passBall = passBall;
            _shootBall = shootBall;
            _current = new UtilityAction(UtilityActionType.Idle, Vector2.Zero, 0f);
        }
        
        public string CurrentActionName => _current.Type.ToString();
        
        public void Update(Player player, AIContext context, float deltaTime)
        {
            _evalTimer -= deltaTime;
            
            // Track the "AFK carrier" case: the ball sits with a controlled player
            // who isn't moving (harness without human input, or a real player who
            // walked away). After 3s of stall, teammates treat the ball as loose.
            if (context.BallCarrier != null && context.BallCarrier.IsControlled
                && context.BallCarrier.Velocity.LengthSquared() < 1f
                && context.BallVelocity.LengthSquared() < 900f)
            {
                _carrierStallTimer += deltaTime;
            }
            else
            {
                _carrierStallTimer = 0f;
            }
            
            // Re-evaluate on tick OR when the current action became impossible
            if (_evalTimer <= 0f || !IsActionViable(player, context, _current))
            {
                _current = Decide(player, context);
                _evalTimer = EvalInterval;
            }
            
            Execute(player, context, _current, deltaTime);
        }
        
        /// <summary>Ball stuck with an idle controlled carrier for 3s+ = loose ball.</summary>
        private bool IsBallEffectivelyLoose(AIContext ctx)
        {
            return ctx.BallCarrier == null || _carrierStallTimer > 3f;
        }
        
        // ------------------------------------------------------------------
        // Decision
        // ------------------------------------------------------------------
        
        private UtilityAction Decide(Player player, AIContext ctx)
        {
            // Goalkeeper has a specialized, narrow action set
            if (player.Position == PlayerPosition.Goalkeeper)
                return DecideGoalkeeper(player, ctx);
            
            UtilityAction best;
            
            // Carrier actions (Pass/Shoot/Dribble/Clear) only available when the
            // ball is actually at the player's feet; a "carrier" outside kick
            // range must first close the distance (no pass-blocked freeze)
            bool hasBallAtFeet = (ctx.HasBallPossession || ctx.BallCarrier == player)
                && ctx.DistanceToBall < 120f;
            
            if (hasBallAtFeet)
            {
                // --- I have the ball: Shoot / Pass / Dribble / Clear ---
                best = ScoreCarrierActions(player, ctx);
            }
            else
            {
                // --- I don't: chase or hold tactical position ---
                // A stalled controlled carrier means the ball is effectively loose
                // (harness without human input, or an AFK player) - chase it.
                bool ballLoose = IsBallEffectivelyLoose(ctx);
                bool designatedCarrier = ctx.BallCarrier == player;
                
                float chaseScore = 0f;
                if (ctx.ShouldChaseBall || designatedCarrier ||
                    (ballLoose && ctx.BallCarrier != null && ctx.DistanceToBall < 800f))
                {
                    // Closer = more attractive; must beat holdScore even for the
                    // designated chaser when the ball is far (kickoff distances)
                    chaseScore = 85f - ctx.DistanceToBall / 40f;
                    if (ctx.DistanceToBall < 200f) chaseScore += 20f;
                    if (designatedCarrier) chaseScore += 20f; // it's YOUR ball, go get it
                    else if (!ctx.ShouldChaseBall) chaseScore -= 10f; // rescue, not primary duty
                }
                
                Vector2 holdPoint = GetTacticalPoint(player, ctx);
                float holdScore = 50f;
                // Holding is more attractive when far from the ball or a teammate has it
                if (ctx.TeammateHasBall(player)) holdScore += 10f;
                
                best = chaseScore > holdScore
                    ? new UtilityAction(UtilityActionType.ChaseBall, GetBallInterceptPoint(ctx), chaseScore)
                    : new UtilityAction(UtilityActionType.HoldPosition, holdPoint, holdScore);
            }
            
            // Commitment bonus: staying with the current action beats switching
            // to something only marginally better (kills boundary oscillation)
            if (_current.Type == best.Type && best.Type != UtilityActionType.Idle)
                best.Score += CommitmentBonus;
            
            return best;
        }
        
        private UtilityAction ScoreCarrierActions(Player player, AIContext ctx)
        {
            float distToGoal = Vector2.Distance(player.FieldPosition, ctx.OpponentGoalCenter);
            float pressure = ctx.NearestOpponent != null
                ? Vector2.Distance(player.FieldPosition, ctx.NearestOpponent.FieldPosition)
                : float.MaxValue;
            
            // SHOOT: close range = high score; pressure reduces it (get rid of it instead)
            float shootScore = 0f;
            if (distToGoal < 700f) shootScore = 90f;
            else if (distToGoal < 1200f) shootScore = 45f;
            if (pressure < 250f) shootScore -= 25f;
            
            // CLEAR: own third + pressure = boot it
            float clearScore = 0f;
            if (ctx.IsInOwnThird(player) && pressure < 300f)
                clearScore = 65f;
            
            // PASS: from the context's pass-target scoring, plus pressure urgency
            float passScore = float.MinValue;
            if (ctx.BestPassTarget != null && ctx.BestPassScore > 0)
            {
                passScore = 30f + ctx.BestPassScore / 40f;
                if (pressure < 300f) passScore += 25f; // under pressure: release the ball
                if (distToGoal > 1200f) passScore += 10f; // too far to shoot: move it on
            }
            
            // DRIBBLE: default carry option; better with open space ahead
            float dribbleScore = 40f;
            if (pressure > 400f) dribbleScore += 15f; // no one near: carry it
            if (player.Position == PlayerPosition.Forward) dribbleScore += 5f;
            
            // Pick the best
            float bestScore = shootScore;
            var type = UtilityActionType.Dribble;
            Vector2 point = ctx.OpponentGoalCenter;
            Player target = null;
            
            if (clearScore > bestScore) { bestScore = clearScore; type = UtilityActionType.Clear; }
            if (passScore > bestScore) { bestScore = passScore; type = UtilityActionType.Pass; target = ctx.BestPassTarget; }
            if (type == UtilityActionType.Dribble) bestScore = dribbleScore;
            
            return new UtilityAction(type, point, bestScore, target);
        }
        
        private UtilityAction DecideGoalkeeper(Player player, AIContext ctx)
        {
            // GK with ball: clear it long
            if (ctx.HasBallPossession || ctx.BallCarrier == player)
            {
                return new UtilityAction(UtilityActionType.Clear, ctx.OpponentGoalCenter, 80f);
            }
            
            // Ball close to own goal: come out and get it
            float distBallToGoal = Vector2.Distance(ctx.BallPosition, ctx.OwnGoalCenter);
            if (distBallToGoal < 600f && ctx.DistanceToBall < 500f)
            {
                float chaseScore = 60f + (600f - distBallToGoal) / 30f;
                return new UtilityAction(UtilityActionType.ChaseBall, GetBallInterceptPoint(ctx), chaseScore);
            }
            
            // Otherwise hold on the line, tracking ball Y slightly
            Vector2 linePoint = new Vector2(
                ctx.OwnGoalCenter.X + (ctx.IsHomeTeam ? 60f : -60f),
                MathHelper.Lerp(ctx.OwnGoalCenter.Y, ctx.BallPosition.Y, 0.25f));
            return new UtilityAction(UtilityActionType.HoldPosition, linePoint, 50f);
        }
        
        private bool IsActionViable(Player player, AIContext ctx, UtilityAction action)
        {
            switch (action.Type)
            {
                case UtilityActionType.Shoot:
                case UtilityActionType.Pass:
                case UtilityActionType.Dribble:
                case UtilityActionType.Clear:
                    // Carrier actions die the moment the ball leaves the feet
                    return (ctx.HasBallPossession || ctx.BallCarrier == player)
                        && ctx.DistanceToBall < 120f;
                case UtilityActionType.ChaseBall:
                    // Stop chasing if a teammate now controls it
                    return !ctx.TeammateHasBall(player) || ctx.DistanceToBall < 150f;
                default:
                    return true;
            }
        }
        
        // ------------------------------------------------------------------
        // Execution (steering)
        // ------------------------------------------------------------------
        
        private void Execute(Player player, AIContext ctx, UtilityAction action, float deltaTime)
        {
            switch (action.Type)
            {
                case UtilityActionType.ChaseBall:
                    player.AITargetPosition = action.Point;
                    player.AITargetPositionSet = true;
                    player.Velocity = Steer(player, action.Point, PlayerMaxSpeed);
                    break;
                
                case UtilityActionType.HoldPosition:
                    player.AITargetPosition = action.Point;
                    player.AITargetPositionSet = true;
                    player.Velocity = Steer(player, action.Point, PlayerMaxSpeed * 0.8f);
                    break;
                
                case UtilityActionType.Dribble:
                    // Carry toward goal; engine ball-contact physics moves the ball
                    player.AITargetPosition = action.Point;
                    player.AITargetPositionSet = true;
                    player.Velocity = Steer(player, action.Point, PlayerMaxSpeed * 0.9f);
                    break;
                
                case UtilityActionType.Pass:
                    // Near-ball gate: only kick if we still actually have the ball
                    // (possession can change between decision ticks - no far kicks)
                    if (action.TargetPlayer != null &&
                        Vector2.Distance(player.FieldPosition, ctx.BallPosition) < 120f)
                    {
                        _passBall(player, action.TargetPlayer.FieldPosition + action.TargetPlayer.Velocity * 0.3f, 0.7f);
                    }
                    player.Velocity = Vector2.Zero;
                    // Pass is instant: fall back to re-evaluating next tick
                    _current = new UtilityAction(UtilityActionType.Idle, player.FieldPosition, 0f);
                    break;
                
                case UtilityActionType.Shoot:
                    if (Vector2.Distance(player.FieldPosition, ctx.BallPosition) < 120f)
                    {
                        _shootBall(player, ctx.OpponentGoalCenter, 0.85f);
                    }
                    player.Velocity = Vector2.Zero;
                    _current = new UtilityAction(UtilityActionType.Idle, player.FieldPosition, 0f);
                    break;
                
                case UtilityActionType.Clear:
                    if (Vector2.Distance(player.FieldPosition, ctx.BallPosition) < 120f)
                    {
                        Vector2 clearTarget = new Vector2(
                            MathHelper.Lerp(player.FieldPosition.X, ctx.OpponentGoalCenter.X, 0.6f),
                            ctx.BallPosition.Y + (float)(_random.NextDouble() - 0.5) * 800f);
                        _shootBall(player, clearTarget, 1.0f);
                    }
                    player.Velocity = Vector2.Zero;
                    _current = new UtilityAction(UtilityActionType.Idle, player.FieldPosition, 0f);
                    break;
                
                default:
                    player.Velocity = Vector2.Zero;
                    break;
            }
        }
        
        private Vector2 Steer(Player player, Vector2 target, float maxSpeed)
        {
            Vector2 velocity = Steering.Arrive(player.FieldPosition, target, maxSpeed);
            velocity = Steering.ApplySeparation(player, velocity);
            velocity = Steering.ApplyBoundaryAvoidance(player.FieldPosition, velocity);
            return velocity;
        }
        
        // ------------------------------------------------------------------
        // Targets
        // ------------------------------------------------------------------
        
        /// <summary>Where the ball is heading, not where it is.</summary>
        private static Vector2 GetBallInterceptPoint(AIContext ctx)
        {
            Vector2 predicted = ctx.BallPosition + ctx.BallVelocity * 0.25f;
            predicted.X = MathHelper.Clamp(predicted.X,
                MatchEngine.StadiumMargin, MatchEngine.StadiumMargin + MatchEngine.FieldWidth);
            predicted.Y = MathHelper.Clamp(predicted.Y,
                MatchEngine.StadiumMargin, MatchEngine.StadiumMargin + MatchEngine.FieldHeight);
            return predicted;
        }
        
        /// <summary>
        /// Role-based tactical point: home position shifted by ball progress and
        /// team possession. Continuous inputs only — no flapping by construction.
        /// </summary>
        private static Vector2 GetTacticalPoint(Player player, AIContext ctx)
        {
            float attackSign = ctx.IsHomeTeam ? 1f : -1f;
            
            // How far the role pushes up with ball position (fraction of ball's progress)
            float roleDepth = player.Position switch
            {
                PlayerPosition.Defender => 0.25f,
                PlayerPosition.Midfielder => 0.45f,
                PlayerPosition.Forward => 0.60f,
                _ => 0.35f,
            };
            
            // Ball progress from own goal to opponent goal (0..1)
            float fieldSpan = MatchEngine.FieldWidth;
            float ballProgress = ctx.IsHomeTeam
                ? (ctx.BallPosition.X - MatchEngine.StadiumMargin) / fieldSpan
                : (MatchEngine.StadiumMargin + fieldSpan - ctx.BallPosition.X) / fieldSpan;
            ballProgress = MathHelper.Clamp(ballProgress, 0f, 1f);
            
            // Team in possession pushes the whole line up
            float possessionPush = ctx.TeammateHasBall(player) ? 0.15f : 0f;
            
            float x = player.HomePosition.X
                + attackSign * (ballProgress - 0.5f + possessionPush) * fieldSpan * roleDepth * 0.5f;
            
            // Slight pull toward the ball's lane, keep formation shape mostly
            float y = MathHelper.Lerp(player.HomePosition.Y, ctx.BallPosition.Y, 0.15f);
            
            x = MathHelper.Clamp(x, MatchEngine.StadiumMargin + 100f,
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth - 100f);
            y = MathHelper.Clamp(y, MatchEngine.StadiumMargin + 100f,
                MatchEngine.StadiumMargin + MatchEngine.FieldHeight - 100f);
            return new Vector2(x, y);
        }
    }
}
