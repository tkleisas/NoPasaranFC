using System;

namespace NoPasaranFC.Graphics3D
{
    /// <summary>
    /// Easter egg (night only): the floodlights flicker and die for a few
    /// seconds, flicker again, then come back. Pure lighting - drives
    /// MatchEnvironment.SetBlackout each frame, no geometry of its own.
    /// Timeline: ~1s dying flicker, 5-8s dark, ~1s flicker back, restore.
    /// </summary>
    public class BlackoutFx
    {
        private const float FlickerInTime = 1f;
        private const float FlickerOutTime = 1f;
        private const float DarkLevel = 0.05f; // faint ambient, not pitch black

        private readonly MatchEnvironment _environment;
        private readonly float _darkTime;
        private float _time;

        public bool IsDone => _time > FlickerInTime + _darkTime + FlickerOutTime;

        public BlackoutFx(MatchEnvironment environment, Random random = null)
        {
            _environment = environment;
            random ??= new Random();
            _darkTime = 5f + (float)random.NextDouble() * 3f;
        }

        public void Update(float dt)
        {
            _time += dt;
            float factor;
            if (_time < FlickerInTime)
                factor = Flicker(_time); // the lights are dying
            else if (_time < FlickerInTime + _darkTime)
                factor = DarkLevel;
            else if (_time < FlickerInTime + _darkTime + FlickerOutTime)
                factor = Flicker(_time * 1.7f); // sputtering back to life
            else
                factor = 1f; // fully restored
            _environment?.SetBlackout(factor);
        }

        /// <summary>Irregular fluorescent-tube flicker: mostly dark, sharp bright pops.</summary>
        private static float Flicker(float t)
        {
            float wave = MathF.Sin(t * 43f) * MathF.Sin(t * 17.3f + 1.7f) * MathF.Sin(t * 7.9f + 0.4f);
            return wave > 0.45f ? 1f : DarkLevel;
        }
    }
}
