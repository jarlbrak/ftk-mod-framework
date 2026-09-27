using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FTKModFramework.Core.UI
{
    // Verified against the installed assemblies: PointerInputModule.ProcessMove raycasts every
    // frame and BaseInputModule.HandlePointerExitAndEnter sends pointer-enter whenever the object
    // under the cursor changes, including when a rebuild swaps it under a cursor that never moved.
    // FTKSelectable.OnPointerEnter then calls FTKInput.SetSelected and sets
    // FTKInputFocus.m_PointerSelected, which FTKInputFocus.Update prefers over the selection the
    // panel requested, and Selectable.OnPointerEnter paints the hover highlight. Both are held here
    // until the pointer moves, so the view keeps its intended focus without patching game input.
    internal sealed partial class ModsPanel
    {
        private readonly ModsPanelHoverGate _hoverGate = new ModsPanelHoverGate();
        private ModsPanelButton _heldHover;
        private PointerEventData _heldHoverEvent;

        private void HoldHover()
        {
            Vector3 pointer = Input.mousePosition;
            _hoverGate.Hold(pointer.x, pointer.y);
            _heldHover = null;
            _heldHoverEvent = null;
        }

        internal bool AdmitsHover()
        {
            Vector3 pointer = Input.mousePosition;
            return _hoverGate.Admits(pointer.x, pointer.y);
        }

        internal void HoldEnter(ModsPanelButton button, PointerEventData eventData)
        {
            _heldHover = button;
            _heldHoverEvent = eventData;
        }

        internal void DropEnter(ModsPanelButton button)
        {
            if (_heldHover != button) return;
            _heldHover = null;
            _heldHoverEvent = null;
        }

        /// <summary>A hover the game accepted as focus becomes the panel's intended focus, so a later
        /// rebuild under a still pointer lands on the control the pointer last chose rather than on
        /// an older click.</summary>
        internal void PointerFocused(FTKSelectable selectable)
        {
            int index = _controls.IndexOf(selectable);
            if (index >= 0) _focusIndex = index;
        }

        /// <summary>The input module sends enter only when the target changes, so moving within the
        /// control that was under the pointer at the rebuild would never highlight it. Replay the
        /// held enter once the pointer moves while it is still over that control.</summary>
        private void ReleaseHeldHover()
        {
            if (_heldHover == null || !AdmitsHover()) return;
            ModsPanelButton button = _heldHover;
            PointerEventData eventData = _heldHoverEvent;
            _heldHover = null;
            _heldHoverEvent = null;
            if (button == null || eventData == null || !button.gameObject.activeInHierarchy) return;
            if (!eventData.hovered.Contains(button.gameObject)) return;
            ExecuteEvents.Execute(button.gameObject, eventData, ExecuteEvents.pointerEnterHandler);
        }
    }

    /// <summary>Stock Button whose hover highlight waits for the panel's hover gate. Clicks, presses
    /// and keyboard or controller selection are untouched.</summary>
    internal sealed class ModsPanelButton : Button
    {
        internal ModsPanel Panel;

        public override void OnPointerEnter(PointerEventData eventData)
        {
            if (Panel != null && !Panel.AdmitsHover()) { Panel.HoldEnter(this, eventData); return; }
            base.OnPointerEnter(eventData);
        }

        public override void OnPointerExit(PointerEventData eventData)
        {
            if (Panel != null) Panel.DropEnter(this);
            base.OnPointerExit(eventData);
        }
    }

    /// <summary>The game's controller adapter, with its hover-to-focus step behind the same gate.</summary>
    internal sealed class ModsPanelSelectable : FTKSelectable
    {
        internal ModsPanel Panel;

        public override void OnPointerEnter(PointerEventData eventData)
        {
            if (Panel != null && !Panel.AdmitsHover()) return;
            base.OnPointerEnter(eventData);
            // The game marks an accepted hover as the focus's pointer selection; mirror only that.
            if (Panel != null && m_InputFocus != null && m_InputFocus.m_PointerSelected == this) Panel.PointerFocused(this);
        }
    }
}
