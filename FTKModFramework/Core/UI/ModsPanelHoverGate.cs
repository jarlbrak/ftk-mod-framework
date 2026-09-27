namespace FTKModFramework.Core.UI
{
    /// <summary>Decides whether a pointer enter may claim hover and focus in the mods panel.
    /// The input module raycasts every frame, so a rebuild that places a new control under a
    /// cursor that has not moved still delivers pointer-enter to it, and the game lets that enter
    /// outrank the focus the panel just chose. The gate holds at every rebuild and opens once the
    /// pointer really moves. Keep this file free of UnityEngine and Plugin so the game-free
    /// PlayerMods harness can compile and exercise it.</summary>
    internal sealed class ModsPanelHoverGate
    {
        private bool _held;
        private float _x;
        private float _y;

        internal bool Held { get { return _held; } }

        /// <summary>Records where the pointer rested when the panel rebuilt.</summary>
        internal void Hold(float x, float y)
        {
            _held = true;
            _x = x;
            _y = y;
        }

        /// <summary>True when a pointer enter at (x, y) may take effect. Any movement opens the
        /// gate until the next rebuild, so returning to the recorded spot does not hold it again.
        /// Exact comparison matches the input module, which treats any nonzero delta as motion.</summary>
        internal bool Admits(float x, float y)
        {
            if (_held && (x != _x || y != _y)) _held = false;
            return !_held;
        }
    }
}
