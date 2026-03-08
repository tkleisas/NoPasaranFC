namespace NoPasaranFC.Gameplay
{
    /// <summary>
    /// Shared constants for the AI system. Centralizes magic numbers
    /// used across multiple AI states for easier tuning.
    /// </summary>
    public static class AIConstants
    {
        // Movement thresholds
        public const float DeadZone = 15f;
        public const float StopDistance = 30f;
        public const float BaseSpeedMultiplier = 2.5f;
        public const float ForwardSpeedBoost = 1.2f;
        public const float OrbitSpeedMultiplier = 3.0f;
        public const float PlayerPersonalSpace = 80f;

        // Kickoff behavior
        public const float KickoffDuration = 5f;
        public const float KickoffChaseDistance = 500f;

        // Target update (anti-oscillation)
        public const float TargetUpdateThreshold = 50f;

        // Field boundary margins
        public const float FieldMargin = 150f;
        public const float BoundaryRepulsionTrigger = 200f;
        public const float BoundaryRepulsionStrength = 0.5f;
        public const float BoundaryBlendWeight = 0.4f;
        public const float SidelineAvoidanceMargin = 250f;

        // Dribbling
        public const float DribbleBoundaryMargin = 200f;
        public const float DribbleEscapeMargin = 150f;
        public const float DecisionInterval = 0.3f;
        public const float DribbleCloseDistance = 80f;
        public const float DribbleChaseDistance = 150f;
        public const float OpponentAvoidanceDistance = 200f;

        // Passing thresholds
        public const float MinPassDistance = 80f;
        public const float MaxPassDistance = 2500f;
        public const float PressureDistance = 300f;

        // Shooting distance thresholds
        public const float ShootAlwaysDistance = 200f;
        public const float ShootCloseDistance = 400f;
        public const float ShootMediumDistance = 600f;
        public const float ShootLongDistance = 800f;
        public const float ShootVeryLongDistance = 1000f;

        // Shooting probabilities
        public const double ShootCloseChance = 0.95;
        public const double ShootMediumChance = 0.80;
        public const double ShootLongChance = 0.50;
        public const double ShootVeryLongChance = 0.20;

        // Passing probabilities by role
        public const double DefenderForwardPassChance = 0.98;
        public const double DefenderLateralPassChance = 0.70;
        public const double MidfielderForwardPassChance = 0.95;
        public const double MidfielderLateralPassChance = 0.70;
        public const double ForwardPassWhenTeammateCloserChance = 0.90;
        public const double ForwardDefaultPassChance = 0.50;

        // Ball chase distances
        public const float ChaseBallGiveUpDistance = 1000f;
        public const float DefenderChaseDefensiveDistance = 400f;
        public const float DefenderEmergencyChaseDistance = 600f;
        public const float DefenderThreatZone = 800f;
        public const float MidfielderDefensiveChaseDistance = 350f;
        public const float MidfielderCloseChaseDistance = 300f;
        public const float ForwardAggressiveChaseDistance = 800f;
        public const float ForwardCloseChaseDistance = 400f;

        // Stamina drain rates
        public const float DefenderStaminaDrain = 1.5f;
        public const float MidfielderStaminaDrain = 2.5f;
        public const float ForwardStaminaDrain = 3.0f;

        // Orbit (DribblingState)
        public const float OrbitBaseTime = 0.2f;
        public const float OrbitVariationRange = 0.2f;
        public const float OrbitBallMovementInvalidation = 100f;
        public const float OrbitArcAngle = 180f;
        public const int OrbitWaypoints = 16;
        public const float OrbitExitDotProduct = 0.5f;

        // Positioning alignment
        public const float KickAlignmentDotProduct = 0.3f;
        public const float KickRepositionDistance = 60f;

        // Goalkeeper
        public const float GKPenaltyAreaDepth = 1205f;
        public const float GKPenaltyAreaWidth = 2942f;
        public const float GKGoalWidth = 534f;
        public const float GKPenaltyPadding = 50f;
        public const float GKBallChaseDistance = 250f;
    }
}
