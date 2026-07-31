using System;
using System.Collections.Generic;
using System.Reflection;

namespace NoPasaranFC.Gameplay.UtilityAI
{
    /// <summary>
    /// Tunable knobs for UtilityBrain decision-making. Centralized here (instead
    /// of inline literals) so offline parameter search can override them at
    /// startup (harness --params); at runtime they always hold the defaults
    /// below unless explicitly overridden.
    /// </summary>
    public static class UtilityTuning
    {
        // Shooting (carrier actions): two range bands with flat scores
        // (v2.27: adopted from param-search v5 best - longer range, stronger far band)
        public static float ShootRangeNear = 930.93f;   // inside this: strong shoot score (~12.8m)
        public static float ShootRangeFar = 2800f;      // inside this: weaker shoot score (~38m)
        public static float ShootScoreNear = 137.06f;
        public static float ShootScoreFar = 120f;
        public static float ShootPressurePenalty = 1.91f; // subtracted when pressured (<250px)
        public static float RoleAttackForward = 1.3f;
        public static float RoleAttackMidfielder = 0.95f;
        public static float RoleAttackDefender = 1.17f;
        
        // Passing (v2.27: adopted from param-search v5 best)
        public static float PassBaseScore = 18.72f;     // + BestPassScore * PassScoreScale
        public static float PassScoreScale = 0.0084f;   // maps BestPassScore (~0-2500) onto the action scale
        public static float PassPressureBonus = 11.61f; // under pressure: release it
        public static float PassFarBonus = 8.04f;       // too far to shoot: move it on
        public static float CrossBonus = 22.56f;        // wide in attacking third: feed the box
        
        // Pass-failure memory (boomerang loop): opponent touch / the ball coming
        // straight back within this window = failed pass
        public static float PassBoomerangSeconds = 2.5f;  // failure detection window after a pass
        public static float PassFailMemorySeconds = 8f;   // the failed target stays penalized this long
        public static float PassFailPenaltyFactor = 0.25f; // per-failure score decay (x0.75 / x0.5 / x0.25)
        
        // Dribbling (v2.27: adopted from param-search v5 best)
        public static float DribbleBaseScore = 62.59f;
        public static float DribbleLaneBonus = 12.55f;  // per missing lane blocker (0-3)
        public static float DribbleFreeSpaceBonus = 8.02f; // no pressure within 400px
        public static float DribbleEnterMargin = 6f;   // marginal touch: entering Dribble must clearly beat chase/hold
        public static float DribbleCommitSeconds = 1.0f; // once dribbling: glue/contest flicker tolerated before dropping out
        public static float DribbleEnterMaxBallSpeed = 900f; // ball faster than this: a touch is a deflection, not a reception
        
        // Scramble discipline (goal-mouth pinball): a loose ball with this many
        // players this close is a scramble - only the designated contestor per
        // team pounces, teammates hold anticipation positions
        public static float ScrambleRadius = 300f;      // players within this of the ball count as crowd
        public static int ScramblePlayers = 4;          // crowd size that makes a loose ball a scramble
        public static float ScrambleMinBallSpeed = 300f; // above this the ball is ricocheting, not controlled
        public static float ScramblePersistSeconds = 1.0f; // scramble window persistence (no per-bounce flicker)
        public static float ContestCommitSeconds = 0.75f; // contestor stays on the ball through ricochets
        
        // Clearing (v2.27: adopted from param-search v5 best)
        public static float ClearScore = 34.07f;        // own third + pressure
        
        // Chase vs hold
        public static float ChaseBaseScore = 69f;       // - distance/40
        public static float ChaseCloseBonus = 20f;      // ball within 200px
        public static float PounceBonus = 25f;          // loose ball in the attacking third
        public static float HoldBaseScore = 47.3f;
        public static float CommitmentBonus = 30f;      // SUPERSEDED by ActionCommitMargin (kept for --params compat)
        public static float ChaseEnterMargin = 6f;      // SUPERSEDED by ActionCommitMargin (kept for --params compat)
        public static float ChaseExitMargin = 4f;       // SUPERSEDED by ActionCommitMargin (kept for --params compat)
        public static float PostPassCommitSeconds = 1.5f; // after a deliberate kick: his own kick can't be re-kicked (dribble-collect only)
        
        // Unified action commitment: once the brain commits to an action it
        // survives re-evaluation until the new best beats its current value by
        // this margin (or a hard interrupt fires: watchdog, lost/gained ball,
        // action impossible). Chaser designation feeds the score instead of
        // gating it: bonus for the designated, pile-on penalty for the rest
        public static float ActionCommitMargin = 8f;
        public static float ActionMinDwellSeconds = 0f; // minimum dwell before a switch is even considered (0 = margin only)
        public static float ChaseDesignationBonus = 25f;    // designated chaser's score bonus (replaces the hard gate)
        public static float ChaseNonDesignatedPenalty = 3f; // everyone else's pile-on damper
        
        // Team coordination: SECOND defender (cover) vs a controlled opponent
        // carrier - contains goal-side instead of diving in
        // (v2.27: offset/score adopted from param-search v5 best)
        public static float CoverOffsetDistance = 177.06f; // contain point this far goal-side of the carrier
        public static float CoverScore = 53.17f;        // cover hold score: beats plain hold (47.3), loses to designated chase
        public static float CoverReassignMargin = 200f; // cover role changes hands only when a rival is this much closer (px)
        
        // Team coordination: pass offers - 1-2 designated runners make timed
        // runs into the carrier's lane when a teammate has clean control
        // (v2.27: run geometry adopted from param-search v5 best)
        public static float PassOfferRunDepth = 856.99f; // how far ahead of the ball the run goes (px)
        public static float PassOfferRunWidth = 260.54f; // diagonal offset into the emptier lane (px)
        public static float PassOfferReassignMargin = 40f; // offer role stability (score points)
        
        // Attacking shape (GetTacticalPoint) (v2.27: adopted from param-search v5 best)
        public static float AttackDepthDefender = 0.56f;   // fraction of the way to the opponent goal
        public static float AttackDepthMidfielder = 0.82f;
        public static float AttackDepthForward = 1.0f;
        public static float HomePositionLerp = 0.28f;      // formation shape pull when attacking
        public static float DeepRunDepth = 0.7f;           // forward timed-run depth
        public static float AttackBallPull = 0.052f;       // central roles' y drift toward the ball (anti-convergence)
        public static float AttackMinSpacing = 476.93f;    // mutual spacing: anti-stack nudge distance (px)
        
        // Defensive shape (GetTacticalPoint, not-attacking branch) (v2.27: adopted from param-search v5 best)
        public static float DefendDepthDefender = 0.078f;  // shift toward the ball by role
        public static float DefendDepthMidfielder = 0.26f;
        public static float DefendDepthForward = 0.89f;
        public static float DefendBallPull = 0.19f;        // y drift toward the ball when defending
        
        // Goalkeeper (v2.27: adopted from param-search v5 best)
        public static float GKChaseGoalDistance = 433.58f; // ball this close to goal: come out
        public static float GKChaseBallDistance = 831.61f; // ...and this close to the GK
        public static float GKLineOffset = 52.83f;         // hold position this far off the line
        public static float GKTrackLerp = 0.535f;          // how much the GK tracks ball Y
        public static float GKShotDetectSpeed = 436.26f;   // ball faster than this toward goal = shot
        public static float GKDiveBurst = 1.61f;           // dive speed multiplier (save reaction)
        public static float GKAdvanceMax = 381f;           // max advance off the line to narrow the angle
        public static float GKCloseDownLerp = 0.55f;       // step toward an opponent carrying in the box
        public static float GKDistributionMinScore = 800f; // pass (not boot) when the best option beats this
        
        // ---- Runtime overrides (offline parameter search) ----
        
        /// <summary>Snapshot of every tunable field at its compiled-in default.</summary>
        public static Dictionary<string, float> SnapshotDefaults()
        {
            var result = new Dictionary<string, float>();
            foreach (var f in typeof(UtilityTuning).GetFields(BindingFlags.Public | BindingFlags.Static))
                result[f.Name] = Convert.ToSingle(f.GetValue(null));
            return result;
        }
        
        /// <summary>
        /// Overrides tunable fields by name (unknown names are ignored).
        /// Call before any MatchEngine is created (e.g. harness --params).
        /// </summary>
        public static void ApplyOverrides(Dictionary<string, float> overrides)
        {
            if (overrides == null) return;
            var fields = typeof(UtilityTuning).GetFields(BindingFlags.Public | BindingFlags.Static);
            foreach (var f in fields)
            {
                if (!overrides.TryGetValue(f.Name, out float value)) continue;
                if (f.FieldType == typeof(int)) f.SetValue(null, (int)value);
                else if (f.FieldType == typeof(double)) f.SetValue(null, (double)value);
                else f.SetValue(null, value);
            }
        }
    }
}
