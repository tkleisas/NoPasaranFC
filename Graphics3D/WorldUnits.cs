using Microsoft.Xna.Framework;
using NoPasaranFC.Gameplay;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Single conversion point between MatchEngine pixel coordinates and 3D world units (meters).
    /// Engine X is field length (goals at X ends), engine Y is field width.
    /// The 3D pitch lies on the XZ plane (Y-up) with the origin at pitch center.
    /// </summary>
    public static class WorldUnits
    {
        public const float PixelsPerMeter = 73f;
        
        // Field dimensions in meters (7665/73 = 105, 4964/73 = 68)
        public static readonly float PitchLengthMeters = MatchEngine.FieldWidth / PixelsPerMeter;
        public static readonly float PitchWidthMeters = MatchEngine.FieldHeight / PixelsPerMeter;
        public static readonly float StadiumMarginMeters = MatchEngine.StadiumMargin / PixelsPerMeter;
        
        // Pitch center in engine pixel coordinates (field starts at StadiumMargin)
        private static readonly Vector2 PitchCenterPx = new Vector2(
            MatchEngine.StadiumMargin + MatchEngine.FieldWidth / 2f,
            MatchEngine.StadiumMargin + MatchEngine.FieldHeight / 2f);
        
        public static float PxToM(float px)
        {
            return px / PixelsPerMeter;
        }
        
        /// <summary>
        /// Convert an engine pixel position (+ optional pixel height) to 3D world coordinates.
        /// World X = field length direction, Y = up, Z = field width direction.
        /// </summary>
        public static Vector3 ToWorld(Vector2 px, float heightPx = 0f)
        {
            return new Vector3(
                (px.X - PitchCenterPx.X) / PixelsPerMeter,
                heightPx / PixelsPerMeter,
                (px.Y - PitchCenterPx.Y) / PixelsPerMeter);
        }
    }
}
