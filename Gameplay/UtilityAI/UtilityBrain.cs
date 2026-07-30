using System;
using Microsoft.Xna.Framework;
using NoPasaranFC.Models;

namespace NoPasaranFC.Gameplay.UtilityAI
{
    public enum UtilityActionType { Idle, ChaseBall, HoldPosition, Dribble, Pass, Shoot, Clear, RunAfterPass }
    
    /// <summary>
    /// Last scored decision, written by the brain each evaluation and read by the
    /// match recorder (verbose log): the chosen action plus up to two rejected
    /// alternatives. Scores are pre-commitment-bonus (the bonus never flips the
    /// winner). Allocation-light mutable struct - no strings until read.
    /// </summary>
    public struct DecisionSnapshot
    {
        public string Action;
        public float Score;
        public string Alt1Action;
        public float Alt1Score;
        public string Alt2Action;
        public float Alt2Score;
    }
    
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
        
        // Give-and-go: after passing, the passer makes a run into space so the
        // receiver has a return option (the wall pass)
        private float _runAfterPassUntil = -1f;
        private Vector2 _runAfterPassTarget;
        
        // Post-kick commitment: for a beat after a deliberate kick (pass, shot
        // or clearance), the kicker may not kick the ball he just kicked AGAIN
        // (Pass/Shoot/Clear suppressed in carrier scoring; Dribble still
        // collects it) - kills the Idle<->RunAfterPass flap when an under-hit
        // pass dies next to him, and blocked-shot machine-gun loops
        private float _postPassCommitUntil = -1f;
        
        // Timed-run hysteresis: enter the deep run when the ball is clearly in
        // through-pass position, stay until play clearly breaks down
        private bool _inDeepRun;
        
        // Dribble collect/guide hysteresis: collect the ball when far (>120),
        // guide it forward when close (<70) — no flapping at the boundary
        private bool _collectingBall;
        
        // Decision tuning
        private static float EvalInterval => GameSettings.Instance.AIDecisionInterval;
        
        /// <summary>Per-player top speed (respects the Speed stat like the old code).</summary>
        private static float MaxSpeedFor(Player player) =>
            player.Speed * AIConstants.BaseSpeedMultiplier;
        
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
            
            // Per-player threshold jitter: boundaries are up to 5% larger for
            // some players, so the AI doesn't obey the same invisible lines
            // everywhere (organic, non-robotic feel)
            _thresholdJitter = 1f + (float)random.NextDouble() * 0.05f;
        }
        
        private readonly float _thresholdJitter;
        
        /// <summary>Ball nobody is controlling: unowned, or its "owner" has
        /// abandoned it (more than 200px away and not collecting).</summary>
        private static bool IsBallLooseForReal(AIContext ctx)
        {
            return ctx.BallCarrier == null ||
                Vector2.Distance(ctx.BallCarrier.FieldPosition, ctx.BallPosition) > 200f;
        }
        
        public string CurrentActionName => _current.Type.ToString();
        
        // Recorder decision log (verbose setting): the most recent evaluation,
        // kept until the next one (evals run on EvalInterval, reads may be faster)
        private DecisionSnapshot _lastDecision;
        
        /// <summary>Most recent decision (chosen action + up to 2 runners-up).</summary>
        public DecisionSnapshot LastDecision => _lastDecision;
        
        private void SetDecision(string action, float score,
            string alt1 = null, float alt1Score = 0f, string alt2 = null, float alt2Score = 0f)
        {
            _lastDecision.Action = action;
            _lastDecision.Score = score;
            _lastDecision.Alt1Action = alt1;
            _lastDecision.Alt1Score = alt1Score;
            _lastDecision.Alt2Action = alt2;
            _lastDecision.Alt2Score = alt2Score;
        }
        
        /// <summary>Snapshots an unscored (heuristic) decision, then returns it.</summary>
        private UtilityAction Snap(UtilityAction action)
        {
            SetDecision(action.Type.ToString(), action.Score);
            return action;
        }
        
        // Lifetime action counters (harness metrics: kicks are instant actions
        // that never persist for a full frame, so state-name census misses them)
        public int ShotsAttempted { get; private set; }
        public int PassesAttempted { get; private set; }
        public int ClearsAttempted { get; private set; }
        
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

            // Dribble failure tracking: lost the ball to an opponent while
            // dribbling -> dribble less for a while (pass first next time)
            if (_wasCarrier && _current.Type == UtilityActionType.Dribble &&
                context.BallCarrier != player && context.BallCarrier != null &&
                context.BallCarrier.TeamId != player.TeamId)
            {
                _dribbleFailures = Math.Min(3, _dribbleFailures + 1);
                _dribbleFailUntil = context.MatchTime + 10f;
            }
            _wasCarrier = context.BallCarrier == player;

            // Post-pass commitment ends for good once anyone else controls the
            // ball (the pass was received, intercepted, or genuinely came back)
            if (_postPassCommitUntil > 0f && context.BallCarrier != null && context.BallCarrier != player)
                _postPassCommitUntil = -1f;

            // Re-evaluate on tick OR when the current action became impossible
            if (_evalTimer <= 0f || !IsActionViable(player, context, _current))
            {
                _current = Decide(player, context);
                _evalTimer = EvalInterval;
            }

            Execute(player, context, _current, deltaTime);
        }

        private bool _wasCarrier;
        private int _dribbleFailures;
        private float _dribbleFailUntil = -1f;
        
        /// <summary>Ball stuck with an idle controlled carrier for 3s+ = loose ball.</summary>
        private bool IsBallEffectivelyLoose(AIContext ctx)
        {
            return ctx.BallCarrier == null || _carrierStallTimer > 3f;
        }
        
        /// <summary>True during the post-pass commitment window while nobody
        /// else has controlled the ball since the kick (latched off in Update).</summary>
        private bool InPostPassCommit(Player player, AIContext ctx)
        {
            return _postPassCommitUntil > ctx.MatchTime &&
                (ctx.BallCarrier == null || ctx.BallCarrier == player);
        }
        
        // ------------------------------------------------------------------
        // Decision
        // ------------------------------------------------------------------
        
        private UtilityAction Decide(Player player, AIContext ctx)
        {
            // Goalkeeper has a specialized, narrow action set
            if (player.Position == PlayerPosition.Goalkeeper)
                return Snap(DecideGoalkeeper(player, ctx));

            // Ball-stall watchdog: a stalled loose ball overrides everything -
            // this player is the nearest and MUST engage (kills idle dead zones)
            if (ctx.ForcedPounce && ctx.BallCarrier != player)
                return Snap(new UtilityAction(UtilityActionType.ChaseBall, GetBallInterceptPoint(ctx), 999f));

            UtilityAction best;
            
            // Carrier mode: anyone who owns the ball stays in carrier actions
            // (Dribble collects the ball itself) across a huge radius — no
            // Dribble<->Chase boundary to flip on
            bool isCarrier = (ctx.HasBallPossession || ctx.BallCarrier == player)
                && ctx.DistanceToBall < 800f;
            
            if (isCarrier)
            {
                // --- I have the ball: Shoot / Pass / Dribble / Clear ---
                best = ScoreCarrierActions(player, ctx);
            }
            else
            {
                // --- I don't: chase or hold tactical position ---
                // Frozen-kickoff guard: our kick and I'm a designated chaser - go
                // play the ball regardless of the hold/chase score balance
                if (!ctx.KickoffTaken && player.TeamId == ctx.KickoffTeamId && ctx.ShouldChaseBall)
                {
                    return Snap(new UtilityAction(UtilityActionType.ChaseBall, GetBallInterceptPoint(ctx), 120f));
                }
                
                // Kickoff encroachment rule: the non-kickoff team must not move
                // toward the ball until the kickoff has been played
                if (!ctx.KickoffTaken && player.TeamId != ctx.KickoffTeamId)
                {
                    return Snap(new UtilityAction(UtilityActionType.HoldPosition,
                        GetTacticalPoint(player, ctx), 100f));
                }
                
                // A stalled controlled carrier means the ball is effectively loose
                // (harness without human input, or an AFK player) - chase it.
                bool ballLoose = IsBallEffectivelyLoose(ctx);
                
                // Give-and-go: just passed -> sprint into space for the return ball.
                // Beats holding position; cancelled if we get the ball back sooner
                if (_runAfterPassUntil > ctx.MatchTime && ctx.BallCarrier != player)
                {
                    return Snap(new UtilityAction(UtilityActionType.RunAfterPass, _runAfterPassTarget, 90f));
                }
                if (_runAfterPassUntil <= ctx.MatchTime)
                {
                    _runAfterPassUntil = -1f;
                }
                
                float chaseScore = 0f;
                float ballProgress = Math.Abs(ctx.BallPosition.X - ctx.OwnGoalCenter.X)
                    / Math.Abs(ctx.OpponentGoalCenter.X - ctx.OwnGoalCenter.X);
                
                // Pounce: ball in the attacking third, loose (or abandoned), and
                // I'm close — attack it regardless of chase rank (rebounds,
                // defensive mistakes; this is what forwards exist for)
                bool pounce = ballProgress > 0.6f / _thresholdJitter &&
                    ctx.DistanceToBall < 400f * _thresholdJitter &&
                    (IsBallLooseForReal(ctx) || ctx.BallCarrier.TeamId != player.TeamId);
                
                if (ctx.ShouldChaseBall || pounce ||
                    (ballLoose && ctx.BallCarrier != null && ctx.DistanceToBall < 800f))
                {
                    // Closer = more attractive; must beat holdScore even for the
                    // designated chaser when the ball is far (kickoff distances)
                    chaseScore = UtilityTuning.ChaseBaseScore - ctx.DistanceToBall / 40f;
                    if (ctx.DistanceToBall < 200f) chaseScore += UtilityTuning.ChaseCloseBonus;
                    if (pounce) chaseScore += UtilityTuning.PounceBonus; // box pounce: highest priority
                    else if (!ctx.ShouldChaseBall) chaseScore -= 10f; // rescue, not primary duty
                    
                    // Press hard when the ball is loose in the attacking third
                    if (ballProgress > 0.6f) chaseScore += 15f;
                    // And when it's loose right next to us, rank be damned
                    if (ctx.BallCarrier == null && ctx.DistanceToBall < 350f) chaseScore += 25f;
                }
                
                Vector2 holdPoint = GetTacticalPoint(player, ctx);
                float holdScore = UtilityTuning.HoldBaseScore;
                // Holding is more attractive when far from the ball or a teammate has it
                if (ctx.TeammateHasBall(player)) holdScore += 10f;
                
                // Boundary hysteresis on a LOOSE ball: switching INTO ChaseBall
                // must beat hold by a margin; a current chaser only drops back
                // below hold - margin. Score noise at the knife-edge can't flap
                // the boundary (the post-hoc CommitmentBonus below never gates
                // the switch itself). Against a clean carrier the comparison
                // stays raw - pressing must stay eager.
                bool chaseWins;
                if (ctx.BallCarrier == null && _current.Type == UtilityActionType.ChaseBall)
                    chaseWins = chaseScore > holdScore - UtilityTuning.ChaseExitMargin;
                else if (ctx.BallCarrier == null && _current.Type == UtilityActionType.HoldPosition)
                    chaseWins = chaseScore > holdScore + UtilityTuning.ChaseEnterMargin;
                else
                    chaseWins = chaseScore > holdScore;
                
                best = chaseWins
                    ? new UtilityAction(UtilityActionType.ChaseBall, GetBallInterceptPoint(ctx), chaseScore)
                    : new UtilityAction(UtilityActionType.HoldPosition, holdPoint, holdScore);
                
                // Recorder snapshot: the loser is the only alternative in this branch
                SetDecision(best.Type.ToString(), best.Score,
                    best.Type == UtilityActionType.ChaseBall ? "HoldPosition" : "ChaseBall",
                    best.Type == UtilityActionType.ChaseBall ? holdScore : chaseScore);
            }
            
            // Commitment bonus: staying with the current action beats switching
            // to something only marginally better (kills boundary oscillation).
            // (Never flips the winner, so the snapshot above stays accurate.)
            if (_current.Type == best.Type && best.Type != UtilityActionType.Idle)
                best.Score += UtilityTuning.CommitmentBonus;
            
            return best;
        }
        
        private UtilityAction ScoreCarrierActions(Player player, AIContext ctx)
        {
            float distToGoal = Vector2.Distance(player.FieldPosition, ctx.OpponentGoalCenter);
            float pressure = ctx.NearestOpponent != null
                ? Vector2.Distance(player.FieldPosition, ctx.NearestOpponent.FieldPosition)
                : float.MaxValue;
            
            // SHOOT: dominant option in and near the box; role-weighted so
            // forwards shoot most, but anyone can have a go
            float roleAttack = player.Position switch
            {
                PlayerPosition.Forward => UtilityTuning.RoleAttackForward,
                PlayerPosition.Midfielder => UtilityTuning.RoleAttackMidfielder,
                PlayerPosition.Defender => UtilityTuning.RoleAttackDefender,
                _ => 0.85f,
            };
            float shootScore = 0f;
            float shootRangeNear = UtilityTuning.ShootRangeNear * _thresholdJitter;
            float shootRangeFar = UtilityTuning.ShootRangeFar * _thresholdJitter;
            if (distToGoal < shootRangeNear) shootScore = UtilityTuning.ShootScoreNear * roleAttack;
            else if (distToGoal < shootRangeFar) shootScore = UtilityTuning.ShootScoreFar * roleAttack;
            if (pressure < 250f) shootScore -= UtilityTuning.ShootPressurePenalty;
            
            // CLEAR: own third + pressure = boot it
            float clearScore = 0f;
            if (ctx.IsInOwnThird(player) && pressure < 300f)
                clearScore = UtilityTuning.ClearScore;
            
            // PASS: from the context's pass-target scoring, plus pressure urgency
            float passScore = float.MinValue;
            if (ctx.BestPassTarget != null && ctx.BestPassScore > 0)
            {
                passScore = UtilityTuning.PassBaseScore + ctx.BestPassScore * UtilityTuning.PassScoreScale;
                if (pressure < 300f) passScore += UtilityTuning.PassPressureBonus; // under pressure: release the ball
                if (distToGoal > 1200f) passScore += UtilityTuning.PassFarBonus; // too far to shoot: move it on
                
                // Cross opportunity: carrier wide in the attacking third -> feed the box
                float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
                if (distToGoal < 2200f &&
                    Math.Abs(player.FieldPosition.Y - centerY) > MatchEngine.FieldHeight * 0.2f)
                {
                    passScore += UtilityTuning.CrossBonus;
                }
            }
            
            // DRIBBLE: carrying forward is how lines break - dominant when the
            // lane toward goal is open; passing is for pressure or better options
            int laneBlockers = 0;
            Vector2 toGoal = ctx.OpponentGoalCenter - player.FieldPosition;
            if (toGoal.LengthSquared() > 1f)
            {
                Vector2 goalDir = Vector2.Normalize(toGoal);
                foreach (var opp in ctx.Opponents)
                {
                    Vector2 rel = opp.FieldPosition - player.FieldPosition;
                    float ahead = Vector2.Dot(rel, goalDir);
                    if (ahead > 0f && ahead < 800f)
                    {
                        float lateral = Math.Abs(rel.X * goalDir.Y - rel.Y * goalDir.X);
                        if (lateral < 350f) laneBlockers++;
                    }
                }
            }
            float dribbleScore = UtilityTuning.DribbleBaseScore
                + (3 - Math.Min(3, laneBlockers)) * UtilityTuning.DribbleLaneBonus; // open lane vs packed
            dribbleScore *= roleAttack;
            if (pressure > 400f) dribbleScore += UtilityTuning.DribbleFreeSpaceBonus;

            // Dead-end detection: converging defenders (2+ closing in) means the
            // dribble dies here - release the ball instead of hogging into a trap
            int converging = 0;
            foreach (var opp in ctx.Opponents)
            {
                float d = Vector2.Distance(opp.FieldPosition, player.FieldPosition);
                if (d < 350f) converging++;
            }
            if (converging >= 2)
            {
                dribbleScore *= 0.3f;
                if (passScore > float.MinValue) passScore += 20f; // get rid of it NOW
            }

            // Recent dribble failures: this player keeps losing it -> pass first
            if (ctx.MatchTime < _dribbleFailUntil)
                dribbleScore *= 1f - 0.25f * _dribbleFailures; // x0.75 / x0.5 / x0.25
            
            // Post-pass commitment: the ball I just kicked is not mine to kick
            // AGAIN for a beat (kills the Pass<->Idle flap when an under-hit
            // pass dies next to the passer) - but Dribble still collects it,
            // so the ball never goes dead. ForcedPounce still overrides (it is
            // checked before carrier scoring), and the window latches off in
            // Update the moment anyone else controls the ball.
            if (InPostPassCommit(player, ctx))
            {
                shootScore = 0f;
                clearScore = 0f;
                passScore = float.MinValue;
            }
            
            // Pick the best (shoot can actually win now)
            float bestScore = shootScore;
            var type = UtilityActionType.Shoot;
            Vector2 point = GetDribblePoint(player, ctx);
            Player target = null;
            
            if (dribbleScore > bestScore) { bestScore = dribbleScore; type = UtilityActionType.Dribble; }
            if (clearScore > bestScore) { bestScore = clearScore; type = UtilityActionType.Clear; }
            if (passScore > bestScore) { bestScore = passScore; type = UtilityActionType.Pass; target = ctx.BestPassTarget; }
            
            // Recorder snapshot: the winner plus the two best rejected options
            string n1 = null, n2 = null;
            float s1 = 0f, s2 = 0f;
            void Consider(UtilityActionType t, float s)
            {
                if (t == type) return;
                if (n1 == null || s > s1) { n2 = n1; s2 = s1; n1 = t.ToString(); s1 = s; }
                else if (n2 == null || s > s2) { n2 = t.ToString(); s2 = s; }
            }
            Consider(UtilityActionType.Shoot, shootScore);
            Consider(UtilityActionType.Dribble, dribbleScore);
            Consider(UtilityActionType.Clear, clearScore);
            Consider(UtilityActionType.Pass, passScore);
            SetDecision(type.ToString(), bestScore, n1, s1, n2, s2);
            
            return new UtilityAction(type, point, bestScore, target);
        }
        
        // GK dive state (shot reaction): short burst to the predicted intercept
        private float _gkDiveUntil = -1f;

        /// <summary>
        /// Forward distribution: the GK starts the attack. Quick roll/throw to an
        /// open teammate (fullbacks and open midfielders first); a long punt to
        /// the emptier flank only when every short option is covered.
        /// </summary>
        private UtilityAction DecideGkDistribution(Player player, AIContext ctx)
        {
            // Find the best open teammate: openness dominates, forward progress
            // breaks ties. "Open" = no opponent within 250px.
            Player bestTarget = null;
            float bestScore = float.MinValue;
            foreach (var mate in ctx.Teammates)
            {
                if (mate.Position == PlayerPosition.Goalkeeper) continue;
                float dist = Vector2.Distance(player.FieldPosition, mate.FieldPosition);
                if (dist < 300f || dist > 3500f) continue;

                float openness = float.MaxValue;
                foreach (var opp in ctx.Opponents)
                {
                    float d = Vector2.Distance(opp.FieldPosition, mate.FieldPosition);
                    if (d < openness) openness = d;
                }
                if (openness < 250f) continue; // marked

                float progress = (mate.FieldPosition.X - ctx.OwnGoalCenter.X) * ctx.AttackSign;
                float score = openness / 100f + progress / 400f;
                if (mate.Position == PlayerPosition.Defender) score += 5f; // fullbacks are the safe out-ball
                if (score > bestScore) { bestScore = score; bestTarget = mate; }
            }

            if (bestTarget != null && bestScore > 4f)
            {
                // Open teammate found: roll/throw to them (execution leads the run)
                return new UtilityAction(UtilityActionType.Pass, Vector2.Zero, bestScore, bestTarget);
            }

            // Nobody open: punt to the emptier flank, deep into the opponent half
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
            int lowPressure = 0, highPressure = 0;
            foreach (var opp in ctx.Opponents)
            {
                if (opp.FieldPosition.Y > centerY) highPressure++; else lowPressure++;
            }
            float flankY = lowPressure <= highPressure
                ? centerY + MatchEngine.FieldHeight * 0.35f
                : centerY - MatchEngine.FieldHeight * 0.35f;
            var puntTarget = new Vector2(
                MathHelper.Lerp(ctx.OwnGoalCenter.X, ctx.OpponentGoalCenter.X, 0.65f), flankY);
            return new UtilityAction(UtilityActionType.Clear, puntTarget, 80f);
        }

        private UtilityAction DecideGoalkeeper(Player player, AIContext ctx)
        {
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
            float goalLineX = ctx.OwnGoalCenter.X;
            float goalTop = centerY - MatchEngine.GoalWidth / 2f;
            float goalBottom = centerY + MatchEngine.GoalWidth / 2f;
            bool defendsLeft = ctx.AttackSign > 0f; // attacking right -> own goal on the left
            float defendingRatio = player.Defending / 100f;
            
            // GK with ball: FORWARD distribution - start the attack with an open
            // teammate (roll/throw short when possible), punt long only when
            // every short option is covered
            if (ctx.HasBallPossession || ctx.BallCarrier == player)
                return DecideGkDistribution(player, ctx);
            
            // Shot detection: ball flying toward our goal -> dive to the predicted
            // crossing point on the line (the save mechanic)
            float ballSpeed = ctx.BallVelocity.Length();
            if (ballSpeed > UtilityTuning.GKShotDetectSpeed)
            {
                float vx = ctx.BallVelocity.X;
                bool towardGoal = defendsLeft ? vx < -100f : vx > 100f;
                if (towardGoal && Math.Abs(vx) > 1f)
                {
                    float t = (goalLineX - ctx.BallPosition.X) / vx;
                    if (t > 0f && t < 2f)
                    {
                        float crossY = ctx.BallPosition.Y + ctx.BallVelocity.Y * t;
                        if (crossY > goalTop - 50f && crossY < goalBottom + 50f)
                        {
                            _gkDiveUntil = ctx.MatchTime + 0.5f;
                            var diveTarget = new Vector2(goalLineX,
                                Math.Clamp(crossY, goalTop + 30f, goalBottom - 30f));
                            return new UtilityAction(UtilityActionType.ChaseBall, diveTarget, 200f);
                        }
                    }
                }
            }
            
            // Continue an active dive (re-detected shots refresh it above)
            if (ctx.MatchTime < _gkDiveUntil)
            {
                float crossY2 = ctx.BallPosition.Y;
                var target = new Vector2(goalLineX,
                    Math.Clamp(crossY2, goalTop + 30f, goalBottom - 30f));
                return new UtilityAction(UtilityActionType.ChaseBall, target, 190f);
            }
            
            // Sweeper-keeper: a through-ball inside the box that the GK reaches
            // first (or no attacker near it) -> rush it, don't stay home
            float distBallToGoal = Vector2.Distance(ctx.BallPosition, ctx.OwnGoalCenter);
            bool ballInBox = ctx.IsInOwnThird(player) && distBallToGoal < 1400f &&
                Math.Abs(ctx.BallPosition.Y - centerY) < AIConstants.GKPenaltyAreaWidth / 2f;
            if (ballInBox && ctx.BallCarrier == null && ctx.BallHeight < 80f)
            {
                float oppDist = ctx.NearestOpponent != null
                    ? Vector2.Distance(ctx.NearestOpponent.FieldPosition, ctx.BallPosition)
                    : float.MaxValue;
                if (oppDist > 250f || ctx.DistanceToBall < oppDist * 0.9f)
                    return new UtilityAction(UtilityActionType.ChaseBall, GetBallInterceptPoint(ctx), 120f);
            }

            // Cross claiming: an aerial ball dropping inside the box with no
            // attacker at the landing point -> step out and take it
            if (ctx.BallHeight > 60f && ctx.BallVerticalVelocity < 0f && distBallToGoal < 1400f &&
                Math.Abs(ctx.BallPosition.Y - centerY) < MatchEngine.GoalWidth / 2f + 150f)
            {
                float oppAtDrop = ctx.NearestOpponent != null
                    ? Vector2.Distance(ctx.NearestOpponent.FieldPosition, ctx.BallPosition)
                    : float.MaxValue;
                if (oppAtDrop > 300f)
                {
                    var claimPoint = new Vector2(
                        MathHelper.Lerp(goalLineX, ctx.BallPosition.X, 0.35f),
                        Math.Clamp(ctx.BallPosition.Y, goalTop + 40f, goalBottom - 40f));
                    return new UtilityAction(UtilityActionType.HoldPosition, claimPoint, 110f);
                }
            }

            // Ball close to own goal: come out and get it
            if (distBallToGoal < UtilityTuning.GKChaseGoalDistance && ctx.DistanceToBall < UtilityTuning.GKChaseBallDistance)
            {
                float chaseScore = 60f + (UtilityTuning.GKChaseGoalDistance - distBallToGoal) / 30f;
                return new UtilityAction(UtilityActionType.ChaseBall, GetBallInterceptPoint(ctx), chaseScore);
            }
            
            // Close down: an opponent carrying inside the box - step out to cut
            // the shooting angle (without leaving the line completely)
            bool oppInBox = ctx.NearestOpponent != null &&
                Math.Abs(ctx.NearestOpponent.FieldPosition.X - goalLineX) < 1205f &&
                ctx.BallCarrier == ctx.NearestOpponent;
            if (oppInBox)
            {
                Vector2 closeDown = Vector2.Lerp(ctx.OwnGoalCenter,
                    ctx.NearestOpponent.FieldPosition,
                    UtilityTuning.GKCloseDownLerp * (0.6f + 0.4f * defendingRatio));
                return new UtilityAction(UtilityActionType.HoldPosition, closeDown, 60f);
            }
            
            // Default positioning: NEAR-POST DISCIPLINE first. When the ball is
            // on a flank, the GK seals the ball-side post (nothing squeezes in at
            // the near post), covering the far side only as the ball centralizes.
            float trackY = MathHelper.Lerp(centerY, ctx.BallPosition.Y, UtilityTuning.GKTrackLerp);
            float flankness = Math.Abs(ctx.BallPosition.Y - centerY) / (MatchEngine.FieldHeight * 0.5f);
            float nearPostBias = Math.Clamp((flankness - 0.2f) * 1.8f, 0f, 0.8f);
            if (nearPostBias > 0f)
            {
                float nearPostY = ctx.BallPosition.Y > centerY ? goalBottom - 70f : goalTop + 70f;
                trackY = MathHelper.Lerp(trackY, nearPostY, nearPostBias);
            }
            float targetY = Math.Clamp(trackY, goalTop + 60f, goalBottom - 60f);

            float ballDistToGoal = Math.Abs(ctx.BallPosition.X - goalLineX);
            float advanceAmount = Math.Clamp(1f - ballDistToGoal / (MatchEngine.FieldWidth * 0.5f), 0f, 1f);
            float advance = advanceAmount * UtilityTuning.GKAdvanceMax * (0.5f + 0.5f * defendingRatio);
            float holdX = goalLineX + (defendsLeft ? UtilityTuning.GKLineOffset + advance
                                                   : -UtilityTuning.GKLineOffset - advance);
            return new UtilityAction(UtilityActionType.HoldPosition, new Vector2(holdX, targetY), 50f);
        }
        
        private bool IsActionViable(Player player, AIContext ctx, UtilityAction action)
        {
            switch (action.Type)
            {
                case UtilityActionType.Shoot:
                case UtilityActionType.Pass:
                case UtilityActionType.Clear:
                    // Kick actions die when the ball leaves kick range
                    return (ctx.HasBallPossession || ctx.BallCarrier == player)
                        && ctx.DistanceToBall < 140f;
                case UtilityActionType.Dribble:
                    // Dribble stays alive while we own the ball (it collects)
                    return (ctx.HasBallPossession || ctx.BallCarrier == player)
                        && ctx.DistanceToBall < 800f;
                case UtilityActionType.ChaseBall:
                    // Stop chasing if a teammate now controls it
                    return !ctx.TeammateHasBall(player) || ctx.DistanceToBall < 150f;
                case UtilityActionType.RunAfterPass:
                    // Run dies when the window closes or we get the return ball
                    return _runAfterPassUntil > ctx.MatchTime && ctx.BallCarrier != player;
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
                {
                    // Close in: seek the ball itself at full speed so contact
                    // actually happens (Arrive's deceleration left chasers
                    // creeping behind the ball forever without touching it).
                    // Own ball: gentle approach — full seek would sprint past it
                    bool ownBall = ctx.BallCarrier == player;
                    Vector2 chasePoint = ctx.DistanceToBall < 400f ? ctx.BallPosition : action.Point;
                    player.AITargetPosition = chasePoint;
                    player.AITargetPositionSet = true;
                    // GK dive burst: shot reaction gets a big speed multiplier
                    float chaseMax = MaxSpeedFor(player);
                    if (player.Position == PlayerPosition.Goalkeeper && ctx.MatchTime < _gkDiveUntil)
                        chaseMax *= UtilityTuning.GKDiveBurst;
                    Vector2 chaseVelocity;
                    if (!ownBall && ctx.DistanceToBall < 200f)
                        chaseVelocity = Steering.Seek(player.FieldPosition, chasePoint, chaseMax);
                    else
                        chaseVelocity = Steering.Arrive(player.FieldPosition, chasePoint, chaseMax);
                    chaseVelocity = Steering.ApplySeparation(player, chaseVelocity);
                    chaseVelocity = Steering.ApplyBoundaryAvoidance(player.FieldPosition, chaseVelocity);
                    player.Velocity = chaseVelocity;
                    break;
                }
                
                case UtilityActionType.HoldPosition:
                    player.AITargetPosition = action.Point;
                    player.AITargetPositionSet = true;
                    player.Velocity = Steer(player, action.Point, MaxSpeedFor(player) * 0.85f);
                    break;
                
                case UtilityActionType.Dribble:
                {
                    // Collect/guide with hysteresis: when far, go TO the ball;
                    // when close, guide it forward. Both point at the ball until
                    // contact, so there is no direction-reversing flip.
                    if (ctx.DistanceToBall > 120f) _collectingBall = true;
                    else if (ctx.DistanceToBall < 70f) _collectingBall = false;
                    
                    Vector2 target;
                    if (_collectingBall)
                    {
                        target = ctx.BallPosition;
                    }
                    else
                    {
                        // Guide: stay INSIDE contact range (56px) so the ball
                        // keeps being nudged forward - a big lead just parks
                        // the carrier ahead of a stationary ball
                        Vector2 toGoal = action.Point - ctx.BallPosition;
                        if (toGoal.LengthSquared() > 1f) toGoal.Normalize();
                        target = ctx.BallPosition + toGoal * 40f;
                    }
                    
                    player.AITargetPosition = target;
                    player.AITargetPositionSet = true;
                    player.Velocity = Steer(player, target, MaxSpeedFor(player) * 0.7f);
                    break;
                }
                
                case UtilityActionType.RunAfterPass:
                    // Sprint into space for the return ball (give-and-go)
                    player.AITargetPosition = action.Point;
                    player.AITargetPositionSet = true;
                    player.Velocity = Steer(player, action.Point, MaxSpeedFor(player));
                    break;
                
                case UtilityActionType.Pass:
                    // Near-ball gate: only kick if we still actually have the ball
                    // (possession can change between decision ticks - no far kicks)
                    if (action.TargetPlayer != null &&
                        Vector2.Distance(player.FieldPosition, ctx.BallPosition) < 120f)
                    {
                        // Lead the receiver by ball travel time, not a flat 0.3s
                        float passDist = Vector2.Distance(ctx.BallPosition, action.TargetPlayer.FieldPosition);
                        float lead = Math.Clamp(passDist / 1200f, 0.2f, 0.8f);
                        _passBall(player, action.TargetPlayer.FieldPosition + action.TargetPlayer.Velocity * lead, 0.85f);
                        PassesAttempted++;
                        
                        // Give-and-go: run into space after releasing, offering
                        // the return pass. Target: deep, offset from the pass lane
                        _runAfterPassUntil = ctx.MatchTime + 5f;
                        _postPassCommitUntil = ctx.MatchTime + UtilityTuning.PostPassCommitSeconds;
                        float side = player.FieldPosition.Y < action.TargetPlayer.FieldPosition.Y ? -1f : 1f;
                        _runAfterPassTarget = new Vector2(
                            MathHelper.Lerp(player.FieldPosition.X, ctx.OpponentGoalCenter.X, 0.55f),
                            player.FieldPosition.Y + side * 500f);
                        
                        // The kick is instant: decide the follow-up NOW instead of
                        // parking in Idle for a tick (the Idle gap read as state
                        // flapping). The commit above bans an instant re-kick.
                        player.Velocity = Vector2.Zero;
                        _current = Decide(player, ctx);
                        _evalTimer = EvalInterval;
                    }
                    else
                    {
                        player.Velocity = Vector2.Zero;
                        // Kick became impossible: fall back to re-evaluating next tick
                        _current = new UtilityAction(UtilityActionType.Idle, player.FieldPosition, 0f);
                    }
                    break;
                
                case UtilityActionType.Shoot:
                    if (Vector2.Distance(player.FieldPosition, ctx.BallPosition) < 120f)
                    {
                        var (aim, power) = GetShotAim(player, ctx);
                        _shootBall(player, aim, power);
                        ShotsAttempted++;
                        // Same commitment as after a pass: no instant re-blast of
                        // the rebound (kills blocked-shot machine-gun loops), then
                        // decide the follow-up now instead of parking in Idle
                        _postPassCommitUntil = ctx.MatchTime + UtilityTuning.PostPassCommitSeconds;
                        player.Velocity = Vector2.Zero;
                        _current = Decide(player, ctx);
                        _evalTimer = EvalInterval;
                    }
                    else
                    {
                        player.Velocity = Vector2.Zero;
                        _current = new UtilityAction(UtilityActionType.Idle, player.FieldPosition, 0f);
                    }
                    break;
                
                case UtilityActionType.Clear:
                    if (Vector2.Distance(player.FieldPosition, ctx.BallPosition) < 120f)
                    {
                        Vector2 clearTarget = new Vector2(
                            MathHelper.Lerp(player.FieldPosition.X, ctx.OpponentGoalCenter.X, 0.6f),
                            ctx.BallPosition.Y + (float)(_random.NextDouble() - 0.5) * 800f);
                        _shootBall(player, clearTarget, 1.0f);
                        ClearsAttempted++;
                        _postPassCommitUntil = ctx.MatchTime + UtilityTuning.PostPassCommitSeconds;
                        player.Velocity = Vector2.Zero;
                        _current = Decide(player, ctx);
                        _evalTimer = EvalInterval;
                    }
                    else
                    {
                        player.Velocity = Vector2.Zero;
                        _current = new UtilityAction(UtilityActionType.Idle, player.FieldPosition, 0f);
                    }
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
        /// Role-based tactical point. Defending: compact shape near HomePosition.
        /// Attacking (teammate has the ball): the line pushes up by role and wide
        /// roles take the flanks — creating width and forward pass options.
        /// </summary>
        private Vector2 GetTacticalPoint(Player player, AIContext ctx)
        {
            bool attacking = ctx.TeammateHasBall(player);
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
            
            // Timed-run hysteresis (per player): run deep when the ball is clearly
            // in through-pass position (>0.40), keep running until play clearly
            // breaks down (<0.25) — kills the depth flip-flop at the trigger line
            float carrierProgress = Math.Abs(ctx.BallPosition.X - ctx.OwnGoalCenter.X)
                / Math.Abs(ctx.OpponentGoalCenter.X - ctx.OwnGoalCenter.X);
            if (carrierProgress > 0.40f) _inDeepRun = true;
            else if (carrierProgress < 0.25f) _inDeepRun = false;
            
            // Stable per-player variance so lines don't move in lockstep
            float variance = ((player.Id * 2654435761u) % 1000) / 1000f; // 0..1 stable
            float depthVariance = 0.85f + variance * 0.3f; // 0.85..1.15 per player
            float laneJitter = (variance - 0.5f) * 200f; // ±100px lane offset
            
            float x, y;
            if (attacking)
            {
                // Depth: whole line pushes up toward the opponent goal by role,
                // with per-player depth variance (no chorus-line movement)
                float roleDepth = player.Position switch
                {
                    PlayerPosition.Defender => UtilityTuning.AttackDepthDefender,
                    PlayerPosition.Midfielder => UtilityTuning.AttackDepthMidfielder,
                    PlayerPosition.Forward => UtilityTuning.AttackDepthForward,
                    _ => 0.5f,
                };
                roleDepth *= depthVariance;
                x = ctx.OwnGoalCenter.X + (ctx.OpponentGoalCenter.X - ctx.OwnGoalCenter.X) * roleDepth;
                // Formation shape dominates (was 0.25) - keeps individual positions
                x = MathHelper.Lerp(x, player.HomePosition.X, UtilityTuning.HomePositionLerp);
                
                // Width: wide roles attack the flanks (stretch the defense)
                float laneOffset = player.Role switch
                {
                    PlayerRole.LeftMidfielder or PlayerRole.LeftWinger => -0.38f,
                    PlayerRole.RightMidfielder or PlayerRole.RightWinger => 0.38f,
                    _ => 0f,
                };
                if (laneOffset != 0f)
                {
                    y = centerY + laneOffset * MatchEngine.FieldHeight + laneJitter;
                }
                else
                {
                    // Own lane mostly, slight ball pull
                    y = MathHelper.Lerp(player.HomePosition.Y + laneJitter, ctx.BallPosition.Y, 0.2f);
                }
                
                // Forwards: make runs BEHIND the defensive line when the ball is
                // genuinely in position for the through pass (timed runs, not
                // permanent camping at the offside line)
                if (player.Position == PlayerPosition.Forward)
                {
                    // Deep run while the hysteresis flag is set (entered >0.40,
                    // exits <0.25 - no flip-flopping at the trigger line)
                    if (_inDeepRun)
                    {
                        x = ctx.OwnGoalCenter.X + (ctx.OpponentGoalCenter.X - ctx.OwnGoalCenter.X) * UtilityTuning.DeepRunDepth;
                    }
                    y = MathHelper.Lerp(y, centerY, 0.4f);

                    // Box occupation: a teammate has the ball WIDE in the attacking
                    // third -> get to the posts for the cross. Even shirt numbers
                    // take the far post, odd take the near post (no double-ups)
                    if (ctx.TeammateHasBall(player) && carrierProgress > 0.6f &&
                        Math.Abs(ctx.BallPosition.Y - centerY) > MatchEngine.FieldHeight * 0.2f)
                    {
                        bool ballLow = ctx.BallPosition.Y > centerY; // ball on the bottom flank
                        bool farPost = (player.ShirtNumber % 2) == 0;
                        float postY = centerY + (farPost ? (ballLow ? -1f : 1f) : (ballLow ? 1f : -1f))
                            * MatchEngine.GoalWidth * 0.35f;
                        x = ctx.OpponentGoalCenter.X - ctx.AttackSign * 500f;
                        y = postY;
                    }
                }

                // Offside awareness: hold the line instead of camping offside.
                // The run target is clamped to the onside side of the offside
                // line (second-last defender) when it matters (ahead of the ball)
                if (GameSettings.Instance.OffsidesEnabled && ctx.Opponents != null && ctx.Opponents.Count > 1)
                {
                    // Second-last defender by distance to the goal line (loop idiom)
                    float goalLineX = ctx.OpponentGoalCenter.X;
                    float d1 = float.MaxValue, d2 = float.MaxValue;
                    Player closest = null, secondLast = null;
                    foreach (var opp in ctx.Opponents)
                    {
                        float d = Math.Abs(opp.FieldPosition.X - goalLineX);
                        if (d < d1) { d2 = d1; secondLast = closest; d1 = d; closest = opp; }
                        else if (d < d2) { d2 = d; secondLast = opp; }
                    }
                    if (secondLast != null)
                    {
                        float lineX = ctx.AttackSign > 0f
                            ? secondLast.FieldPosition.X - 60f
                            : secondLast.FieldPosition.X + 60f;
                        if (ctx.AttackSign > 0f)
                        {
                            if (x > lineX && x > ctx.BallPosition.X) x = lineX;
                        }
                        else
                        {
                            if (x < lineX && x < ctx.BallPosition.X) x = lineX;
                        }
                    }
                }
            }
            else
            {
                // Defending/neutral: home position shifted by ball progress
                float attackSign = ctx.AttackSign;
                float roleDepth = player.Position switch
                {
                    PlayerPosition.Defender => UtilityTuning.DefendDepthDefender,
                    PlayerPosition.Midfielder => UtilityTuning.DefendDepthMidfielder,
                    PlayerPosition.Forward => UtilityTuning.DefendDepthForward,
                    _ => 0.35f,
                };
                
                float fieldSpan = MatchEngine.FieldWidth;
                float ballProgress = ctx.AttackSign > 0f
                    ? (ctx.BallPosition.X - MatchEngine.StadiumMargin) / fieldSpan
                    : (MatchEngine.StadiumMargin + fieldSpan - ctx.BallPosition.X) / fieldSpan;
                ballProgress = MathHelper.Clamp(ballProgress, 0f, 1f);
                
                x = player.HomePosition.X
                    + attackSign * (ballProgress - 0.5f) * fieldSpan * roleDepth * 0.5f;
                y = MathHelper.Lerp(player.HomePosition.Y, ctx.BallPosition.Y, UtilityTuning.DefendBallPull);
            }
            
            x = MathHelper.Clamp(x, MatchEngine.StadiumMargin + 100f,
                MatchEngine.StadiumMargin + MatchEngine.FieldWidth - 100f);
            y = MathHelper.Clamp(y, MatchEngine.StadiumMargin + 100f,
                MatchEngine.StadiumMargin + MatchEngine.FieldHeight - 100f);
            
            // Organic drift: slow per-player wander around the tactical point so
            // the shape breathes instead of holding a rigid grid
            float phase = variance * 100f + ctx.MatchTime * 0.5f;
            x += (float)Math.Sin(phase + player.Id * 1.7f) * 60f;
            y += (float)Math.Cos(phase * 1.3f + player.Id * 2.3f) * 50f;
            
            return new Vector2(x, y);
        }
        
        /// <summary>
        /// Shot aim: far post relative to the shooter (harder for the GK to
        /// reach), with power scaling by distance. Central shooters aim at the
        /// post away from their dominant side with a dash of randomness.
        /// </summary>
        private (Vector2 aim, float power) GetShotAim(Player player, AIContext ctx)
        {
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
            float halfGoal = MatchEngine.GoalWidth / 2f;
            float goalX = ctx.OpponentGoalCenter.X;

            // Far post: shooter left of center -> aim right post, and vice versa
            float inset = 60f + (float)_random.NextDouble() * 60f; // inside the frame
            float aimY = player.FieldPosition.Y < centerY
                ? centerY + halfGoal - inset
                : centerY - halfGoal + inset;

            // Power scales with distance (no floaters from range, no blasting 1-on-1)
            float dist = Vector2.Distance(player.FieldPosition, ctx.OpponentGoalCenter);
            float power = Math.Clamp(0.55f + dist / 3000f, 0.65f, 1.0f);

            return (new Vector2(goalX, aimY), power);
        }

        /// <summary>
        /// Dribble target: goal center by default, but if the central lane ahead
        /// is crowded, shift to the more open flank (wing attack).
        /// </summary>
        private static Vector2 GetDribblePoint(Player player, AIContext ctx)
        {
            float centerY = MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f;
            
            // Count opponents ahead-center vs ahead on each flank
            int centerBlock = 0, leftOpen = 0, rightOpen = 0;
            Vector2 toGoal = ctx.OpponentGoalCenter - player.FieldPosition;
            foreach (var opp in ctx.Opponents)
            {
                float d = Vector2.Distance(opp.FieldPosition, player.FieldPosition);
                if (d > 600f) continue;
                float yDiff = opp.FieldPosition.Y - player.FieldPosition.Y;
                if (Math.Abs(yDiff) < 400f) centerBlock++;
                else if (yDiff < 0) leftOpen++;
                else rightOpen++;
            }
            
            if (centerBlock >= 2)
            {
                // Central lane blocked: attack the emptier flank
                float flankY = leftOpen <= rightOpen
                    ? MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.15f
                    : MatchEngine.StadiumMargin + MatchEngine.FieldHeight * 0.85f;
                return new Vector2(ctx.OpponentGoalCenter.X, flankY);
            }
            
            return ctx.OpponentGoalCenter;
        }
    }
}
