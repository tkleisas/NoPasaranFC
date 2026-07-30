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
        public static float ShootRangeNear = 1600f;     // inside this: strong shoot score (~22m)
        public static float ShootRangeFar = 1835f;      // inside this: weaker shoot score (~25m)
        public static float ShootScoreNear = 136f;
        public static float ShootScoreFar = 52.5f;
        public static float ShootPressurePenalty = 5.7f; // subtracted when pressured (<250px)
        public static float RoleAttackForward = 1.38f;
        public static float RoleAttackMidfielder = 0.6f;
        public static float RoleAttackDefender = 0.97f;
        
        // Passing
        public static float PassBaseScore = 29f;        // + BestPassScore * PassScoreScale
        public static float PassScoreScale = 0.021f;    // maps BestPassScore (~0-2500) onto the action scale
        public static float PassPressureBonus = 15.6f;  // under pressure: release it
        public static float PassFarBonus = 17.8f;       // too far to shoot: move it on
        public static float CrossBonus = 26f;           // wide in attacking third: feed the box
        
        // Dribbling
        public static float DribbleBaseScore = 33.5f;
        public static float DribbleLaneBonus = 27f;     // per missing lane blocker (0-3)
        public static float DribbleFreeSpaceBonus = 10.6f; // no pressure within 400px
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
        
        // Clearing
        public static float ClearScore = 47f;           // own third + pressure
        
        // Chase vs hold
        public static float ChaseBaseScore = 69f;       // - distance/40
        public static float ChaseCloseBonus = 20f;      // ball within 200px
        public static float PounceBonus = 25f;          // loose ball in the attacking third
        public static float HoldBaseScore = 47.3f;
        public static float CommitmentBonus = 30f;      // anti-flapping stickiness
        public static float ChaseEnterMargin = 6f;      // switching INTO ChaseBall must beat hold by this
        public static float ChaseExitMargin = 4f;       // a chaser only drops out below hold - this
        public static float PostPassCommitSeconds = 1.5f; // after a deliberate kick: his own kick can't be re-kicked (dribble-collect only)
        
        // Attacking shape (GetTacticalPoint)
        public static float AttackDepthDefender = 0.46f;   // fraction of the way to the opponent goal
        public static float AttackDepthMidfielder = 0.90f;
        public static float AttackDepthForward = 0.83f;
        public static float HomePositionLerp = 0.58f;      // formation shape pull when attacking
        public static float DeepRunDepth = 0.98f;          // forward timed-run depth
        
        // Defensive shape (GetTacticalPoint, not-attacking branch)
        public static float DefendDepthDefender = 0.23f;   // shift toward the ball by role
        public static float DefendDepthMidfielder = 0.34f;
        public static float DefendDepthForward = 0.80f;
        public static float DefendBallPull = 0.08f;        // y drift toward the ball when defending
        
        // Goalkeeper
        public static float GKChaseGoalDistance = 510f;    // ball this close to goal: come out
        public static float GKChaseBallDistance = 720f;    // ...and this close to the GK
        public static float GKLineOffset = 62f;            // hold position this far off the line
        public static float GKTrackLerp = 0.48f;           // how much the GK tracks ball Y
        public static float GKShotDetectSpeed = 400f;      // ball faster than this toward goal = shot
        public static float GKDiveBurst = 2.5f;            // dive speed multiplier (save reaction)
        public static float GKAdvanceMax = 200f;           // max advance off the line to narrow the angle
        public static float GKCloseDownLerp = 0.4f;        // step toward an opponent carrying in the box
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
