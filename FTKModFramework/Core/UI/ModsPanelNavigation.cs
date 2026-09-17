using System.Collections.Generic;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Marketplace;

namespace FTKModFramework.Core.UI
{
    /// <summary>Back-stack policy for the mods panel: which view Back returns to and what it
    /// re-selects. Keep this file free of UnityEngine and Plugin so the game-free PlayerMods
    /// harness can compile and exercise the policy without the game.</summary>
    internal sealed class ModsPanelNavigation
    {
        /// <summary>What Back restores. Sub-mode flags are recorded because a forward navigation
        /// can start from one, such as removing a mod from its requirements page.</summary>
        internal sealed class Frame
        {
            internal string View;
            internal int Page;
            internal int DetailPage;
            internal int Focus;
            internal int FocusCount;
            internal int ImagePage;
            internal ModEntry Entry;
            internal PackageDescriptor Package;
            internal bool Advanced;
            internal bool Gallery;
        }

        /// <summary>Tab roots. Entering one starts a new journey, so it discards the stack.</summary>
        internal static bool IsRoot(string view)
        {
            return view == "installed" || view == "discover" || view == "updates";
        }

        /// <summary>Views rendered as a browse list beside a details column. The required-components
        /// list is one of these but is not a root: it is reached from Installed and Back returns there.</summary>
        internal static bool IsBrowse(string view)
        {
            return view == "installed" || view == "discover" || view == "components";
        }

        /// <summary>A confirmation is never restorable. Back must not re-enter a review whose plan
        /// belongs to an operation the player already left.</summary>
        internal static bool IsRestorable(string view) { return view != "confirm"; }

        internal static int DefaultFocus(string view)
        {
            return view == "updates" ? 2 : view == "installed" ? 1 : 0;
        }

        private readonly List<Frame> _stack = new List<Frame>();

        internal int Depth { get { return _stack.Count; } }
        internal bool CanGoBack { get { return _stack.Count > 0; } }

        /// <summary>Records <paramref name="current"/> as the parent of <paramref name="view"/>.
        /// Returns false when the panel is already on that view, which the caller treats as a
        /// re-entry that resets in-view state without changing history. When the target is already
        /// an ancestor, <paramref name="restored"/> receives its frame and the stack unwinds to it
        /// instead of growing, so no view can appear twice and Back is never a dead press.</summary>
        internal bool Enter(string view, Frame current, out Frame restored)
        {
            restored = null;
            if (current != null && current.View == view) return false;
            if (IsRoot(view)) { _stack.Clear(); return true; }
            int existing = IndexOf(view);
            if (existing >= 0)
            {
                restored = _stack[existing];
                _stack.RemoveRange(existing, _stack.Count - existing);
                return true;
            }
            if (current != null && IsRestorable(current.View)) _stack.Add(current);
            return true;
        }

        /// <summary>Pops the parent frame, or null when the panel should close.</summary>
        internal Frame Back()
        {
            if (_stack.Count == 0) return null;
            Frame frame = _stack[_stack.Count - 1];
            _stack.RemoveAt(_stack.Count - 1);
            return frame;
        }

        internal bool Contains(string view) { return IndexOf(view) >= 0; }

        /// <summary>Controller focus is an index into the enabled controls of a rendered view.
        /// Restore it only when the rebuilt view offers exactly as many, because a pending-change
        /// tab or a registration notice appearing or leaving shifts every index.</summary>
        internal static int ResolveFocus(int saved, int savedCount, int actualCount, int fallback)
        {
            if (actualCount <= 0) return 0;
            if (savedCount != actualCount || saved < 0 || saved >= actualCount) return fallback < actualCount ? fallback : 0;
            return saved;
        }

        private int IndexOf(string view)
        {
            for (int i = 0; i < _stack.Count; i++) if (_stack[i].View == view) return i;
            return -1;
        }
    }
}
