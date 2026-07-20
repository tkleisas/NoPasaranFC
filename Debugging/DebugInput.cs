using System.Collections.Generic;
using System.Linq;
using Microsoft.Xna.Framework.Input;

namespace NoPasaranFC.Debugging
{
    /// <summary>
    /// Keyboard input seam for remote debugging. All keyboard reads in the game
    /// go through GetState(), which merges the real keyboard with keys injected
    /// over the debug TCP console. Tap lifecycle is frame-based: keys queued via
    /// InjectTap become part of the state for exactly the next frame
    /// (BeginFrame is called once per Update from Game1).
    /// </summary>
    public static class DebugInput
    {
        private static readonly HashSet<Keys> _held = new HashSet<Keys>();
        private static readonly Queue<Keys> _tapQueue = new Queue<Keys>();
        private static readonly List<Keys> _activeTaps = new List<Keys>();

        /// <summary>Called once per frame from Game1.Update before screens update.</summary>
        public static void BeginFrame()
        {
            _activeTaps.Clear();
            while (_tapQueue.Count > 0)
                _activeTaps.Add(_tapQueue.Dequeue());
        }

        /// <summary>Key is seen as pressed for exactly one frame.</summary>
        public static void InjectTap(Keys key) => _tapQueue.Enqueue(key);

        /// <summary>Key is seen as held until InjectUp.</summary>
        public static void InjectDown(Keys key) => _held.Add(key);

        public static void InjectUp(Keys key) => _held.Remove(key);

        /// <summary>Real keyboard state merged with injected keys.</summary>
        public static KeyboardState GetState()
        {
            var real = Keyboard.GetState();
            if (_held.Count == 0 && _activeTaps.Count == 0)
                return real;

            var keys = new List<Keys>(real.GetPressedKeys());
            foreach (var k in _held)
                if (!keys.Contains(k)) keys.Add(k);
            foreach (var k in _activeTaps)
                if (!keys.Contains(k)) keys.Add(k);
            return new KeyboardState(keys.ToArray());
        }
    }
}
