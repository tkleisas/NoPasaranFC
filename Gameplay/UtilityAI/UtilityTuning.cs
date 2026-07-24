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
        public static float ShootRangeNear = 1400f;     // inside this: strong shoot score (~19m)
        public static float ShootRangeFar = 1640f;      // inside this: weaker shoot score (~22m)
        public static float ShootScoreNear = 91f;
        public static float ShootScoreFar = 58.6f;
        public static float ShootPressurePenalty = 9.4f; // subtracted when pressured (<250px)
        public static float RoleAttackForward = 1.8f;
        public static float RoleAttackMidfielder = 0.64f;
        public static float RoleAttackDefender = 1.0f;
        
        // Passing
        public static float PassBaseScore = 39f;        // + BestPassScore * PassScoreScale
        public static float PassScoreScale = 0.022f;    // maps BestPassScore (~0-2500) onto the action scale
        public static float PassPressureBonus = 25f;    // under pressure: release it
        public static float PassFarBonus = 12.5f;       // too far to shoot: move it on
        public static float CrossBonus = 26f;           // wide in attacking third: feed the box
        
        // Dribbling
        public static float DribbleBaseScore = 31f;
        public static float DribbleLaneBonus = 17.3f;   // per missing lane blocker (0-3)
        public static float DribbleFreeSpaceBonus = 11.7f; // no pressure within 400px
        
        // Clearing
        public static float ClearScore = 72.5f;         // own third + pressure
        
        // Chase vs hold
        public static float ChaseBaseScore = 80f;       // - distance/40
        public static float ChaseCloseBonus = 20f;      // ball within 200px
        public static float PounceBonus = 25f;          // loose ball in the attacking third
        public static float HoldBaseScore = 47.4f;
        public static float CommitmentBonus = 21.8f;    // anti-flapping stickiness
        
        // Attacking shape (GetTacticalPoint)
        public static float AttackDepthDefender = 0.45f;   // fraction of the way to the opponent goal
        public static float AttackDepthMidfielder = 0.90f;
        public static float AttackDepthForward = 0.78f;
        public static float HomePositionLerp = 0.52f;      // formation shape pull when attacking
        public static float DeepRunDepth = 0.96f;          // forward timed-run depth
        
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
