using System;
using System.Collections.Generic;
using System.Reflection;

namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Shared constants for the AI system. Centralizes magic numbers
    /// used across multiple AI states for easier tuning.
    ///
    /// Values are static fields (not const) so offline parameter search can
    /// override them at startup (harness --params); at runtime they always
    /// hold the defaults below unless explicitly overridden.
    /// </summary>
    public static class AIConstants
    {
        // Movement thresholds
        public static float DeadZone = 15f;
        public static float StopDistance = 30f;
        public static float BaseSpeedMultiplier = 2.5f;
        public static float ForwardSpeedBoost = 1.2f;
        public static float OrbitSpeedMultiplier = 3.0f;
        public static float PlayerPersonalSpace = 120f;
        public static float PersonalSpaceBlend = 0.25f;

        // Kickoff behavior
        public static float KickoffDuration = 5f;
        public static float KickoffChaseDistance = 500f;

        // Target update (anti-oscillation)
        public static float TargetUpdateThreshold = 30f;
        // Target inertia: low-pass filter time constant for AI target positions.
        // The chased point glides toward new targets instead of jumping,
        // which kills rapid left-right oscillation near the ball/center line.
        public static float TargetInertiaTime = 0.25f;
        // Start/stop hysteresis: a stationary player only starts moving when the
        // target is this many times StopDistance away (prevents stop-start stutter)
        public static float StationaryStartMultiplier = 2.5f;

        // Field boundary margins
        public static float FieldMargin = 150f;
        public static float BoundaryRepulsionTrigger = 200f;
        public static float BoundaryRepulsionStrength = 0.8f;
        public static float BoundaryBlendWeight = 0.35f;
        public static float SidelineAvoidanceMargin = 250f;

        // Dribbling
        public static float DribbleBoundaryMargin = 200f;
        public static float DribbleEscapeMargin = 150f;
        public static float DecisionInterval = 0.5f;
        public static float DribbleCloseDistance = 80f;
        public static float DribbleChaseDistance = 150f;
        public static float OpponentAvoidanceDistance = 200f;

        // Passing thresholds
        public static float MinPassDistance = 50f;
        public static float MaxPassDistance = 3500f;
        public static float PressureDistance = 350f;

        // Shooting distance thresholds (field scale: 73px/m — box edge is ~1205px from goal line)
        public static float ShootAlwaysDistance = 450f;    // ~6m: six-yard box, always shoot
        public static float ShootCloseDistance = 850f;     // ~11m: penalty spot
        public static float ShootMediumDistance = 1300f;   // ~18m: edge of the box
        public static float ShootLongDistance = 1700f;     // ~23m
        public static float ShootVeryLongDistance = 2100f; // ~29m: speculative

        // Shooting probabilities (base - scaled by difficulty, role, and Shooting stat)
        public static double ShootCloseChance = 0.95;
        public static double ShootMediumChance = 0.80;
        public static double ShootLongChance = 0.35;
        public static double ShootVeryLongChance = 0.10;

        // Shooting willingness by role (multiplies shot probabilities)
        public static float ForwardShootWillingness = 1.0f;
        public static float MidfielderShootWillingness = 0.75f;
        public static float DefenderShootWillingness = 0.35f;

        // Passing probabilities by role (NOT scaled by difficulty — difficulty affects accuracy, not willingness)
        public static double DefenderForwardPassChance = 0.95;
        public static double DefenderLateralPassChance = 0.85;
        public static double MidfielderForwardPassChance = 0.90;
        public static double MidfielderLateralPassChance = 0.80;
        public static double ForwardPassWhenTeammateCloserChance = 0.90;
        public static double ForwardDefaultPassChance = 0.75;

        // Ball chase distances
        public static float ChaseBallGiveUpDistance = 1000f;
        public static float DefenderChaseDefensiveDistance = 400f;
        public static float DefenderEmergencyChaseDistance = 600f;
        public static float DefenderThreatZone = 800f;
        public static float MidfielderDefensiveChaseDistance = 350f;
        public static float MidfielderCloseChaseDistance = 300f;
        public static float ForwardAggressiveChaseDistance = 800f;
        public static float ForwardCloseChaseDistance = 400f;

        // Stamina drain rates (recovery is only 2/s while standing, so drains must stay
        // below it on average or players spend the whole half in the low-stamina crawl)
        public static float DefenderStaminaDrain = 0.8f;
        public static float MidfielderStaminaDrain = 1.2f;
        public static float ForwardStaminaDrain = 1.5f;
        public static float StaminaStationaryRecovery = 2.0f; // Per second while standing still

        // Orbit (DribblingState)
        public static float OrbitBaseTime = 0.2f;
        public static float OrbitVariationRange = 0.2f;
        public static float OrbitBallMovementInvalidation = 50f;
        public static float OrbitArcAngle = 180f;
        public static int OrbitWaypoints = 16;
        public static float OrbitExitDotProduct = 0.5f;

        // Positioning alignment
        public static float KickAlignmentDotProduct = 0.3f;
        public static float KickRepositionDistance = 60f;

        // Goalkeeper
        public static float GKPenaltyAreaDepth = 1205f;
        public static float GKPenaltyAreaWidth = 2942f;
        public static float GKGoalWidth = 534f;
        public static float GKPenaltyPadding = 50f;
        public static float GKBallChaseDistance = 250f;
        public static float GKShotDetectionSpeed = 400f;
        public static float GKDiveBurstMultiplier = 4.0f;
        public static float GKBallTrackingLerp = 0.5f;

        // --- Difficulty scaling ---
        // Decision interval multipliers (applied to DecisionInterval)
        public static float DifficultyEasyDecisionMult = 1.5f;    // Slower decisions
        public static float DifficultyNormalDecisionMult = 1.0f;
        public static float DifficultyHardDecisionMult = 0.7f;     // Faster decisions

        // Accuracy offset scaling (lower = more accurate)
        public static float DifficultyEasyAccuracyMult = 1.6f;     // Wide shots/passes
        public static float DifficultyNormalAccuracyMult = 1.0f;
        public static float DifficultyHardAccuracyMult = 0.5f;     // Precise shots/passes

        // Probability scaling (multiplied into pass/shoot chances)
        public static float DifficultyEasyProbMult = 0.7f;         // Worse decisions
        public static float DifficultyNormalProbMult = 1.0f;
        public static float DifficultyHardProbMult = 1.15f;        // Better decisions

        // Positioning quality (lerp factor multiplier)
        public static float DifficultyEasyLerpMult = 0.75f;        // Sluggish tracking
        public static float DifficultyNormalLerpMult = 1.0f;
        public static float DifficultyHardLerpMult = 1.25f;        // Tight tracking

        // Stat influence ranges for shot accuracy
        // Shot Y offset = BaseOffset * (1 - Shooting/MaxStat * StatInfluence) * DifficultyAccuracyMult
        public static float ShotBaseOffset = 440f;
        public static float ShotStatInfluence = 0.6f;  // At Shooting=100, offset reduced by 60%
        public static float MaxStatValue = 100f;

        // Stat influence for pass accuracy (direction error in radians)
        public static float PassMaxDirectionError = 0.15f;  // ~8.6 degrees max error
        public static float PassStatInfluence = 0.8f;       // At Passing=100, error reduced by 80%

        // Defending stat influence
        public static float DefendingThreatRangeBonus = 200f;  // Extra range at Defending=100
        public static float DefendingLerpBonus = 0.15f;        // Extra lerp at Defending=100

        // Marking behavior
        public static float MarkingActivationDistance = 1500f;  // Start marking when opponent this close to goal
        public static float MarkingOffsetDistance = 80f;        // Stand between opponent and own goal

        // Defensive line
        public static float DefensiveLineWeight = 0.3f;  // How much defenders conform to average X

        // Forward runs
        public static float ForwardRunTriggerDistance = 600f;  // Teammate must be this close with ball
        public static float ForwardRunDepth = 0.92f;           // How far forward to run (% of field)

        // Chasing prediction
        public static float ChasePredictionTime = 0.3f;  // Seconds ahead to predict ball position

        // Chaser selection (AIBehaviorManager.ShouldPlayerChaseBall)
        public static float ChaseSelectionLookahead = 0.35f;  // Seconds of ball travel considered when ranking chasers
        public static float ChaseStickinessFactor = 0.8f;     // Current chaser's distance discount (hysteresis)
        public static float SupportChaseDistance = 400f;      // 2nd-closest joins the chase within this range
        public static float BallControlRadius = 250f;         // Last toucher within this of the ball = ball carrier (covers dribble taps)

        // Pass quality gating
        public static float MinAcceptablePassScore = 150f;    // Below this, the best pass option is considered bad

        // Defensive clearances
        public static float ClearanceDistance = 1600f;        // How far upfield clearances aim
        public static float ClearancePower = 1.25f;

        // Shot placement
        public static float ShotPostInset = 90f;               // Aim this far inside the post, away from GK
        public static float ShotGKCenteredTolerance = 30f;     // GK within this of center = pick a random corner
        
        // ---- Runtime overrides (offline parameter search) ----
        
        /// <summary>Snapshot of every tunable field at its compiled-in default.</summary>
        public static Dictionary<string, float> SnapshotDefaults()
        {
            var result = new Dictionary<string, float>();
            foreach (var f in typeof(AIConstants).GetFields(BindingFlags.Public | BindingFlags.Static))
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
            var fields = typeof(AIConstants).GetFields(BindingFlags.Public | BindingFlags.Static);
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
