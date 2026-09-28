namespace FTKModFramework.Core
{
    /// <summary>The device behind one accepted Inventory press, as far as this client can tell.</summary>
    internal enum InventoryPressDevice
    {
        Unknown,
        KeyboardMouse,
        Controller,
    }

    /// <summary>Pure decisions for the One-press inventory tweak (convenience.one-press-inventory).
    /// The patch reads the game state into these inputs; everything here is Unity-free so the off
    /// path and every vanilla gate can be tested without the game.</summary>
    internal static class OnePressInventoryDecision
    {
        /// <summary>Which device pressed Inventory. FTKInput reads bound keyboard keys and mouse buttons
        /// from the shared devices whoever's turn it is, so a key press only counts for a character
        /// assigned keyboard and mouse. A character with both (solo with a gamepad plugged in) counts as
        /// keyboard and mouse only when a bound key or mouse button went down this frame.</summary>
        /// <param name="usesKeyboardMouse">CharacterOverworld.m_IsUseMouse.</param>
        /// <param name="usesController">CharacterOverworld.m_IsUseController.</param>
        /// <param name="boundKeyOrMouseButtonDown">A keyboard key or mouse button bound to Inventory went
        /// down this frame.</param>
        internal static InventoryPressDevice ClassifyPress(bool usesKeyboardMouse, bool usesController, bool boundKeyOrMouseButtonDown)
        {
            if (usesKeyboardMouse && boundKeyOrMouseButtonDown) return InventoryPressDevice.KeyboardMouse;
            return usesController ? InventoryPressDevice.Controller : InventoryPressDevice.Unknown;
        }

        /// <summary>Whether to start the inventory open that vanilla's second press would start. Off,
        /// faulted or uninitialized never opens. Otherwise it mirrors the uiPlayerMainHud.Update branch
        /// that handles the second press: the belt holds input focus, the game is not aborted, no battle
        /// stance is up, and the HUD's inventory button is interactable. It also never opens an
        /// inventory that is already showing this character, so the tweak cannot add a second open.</summary>
        internal static bool ShouldOpen(TweakRegistry registry, int handle, InventoryPressDevice device,
            bool beltHasFocus, bool gameAborted, bool battleStance, bool openInventoryInteractable,
            bool inventoryShowingCharacter)
        {
            if (!registry.IsOn(handle)) return false;
            if (device != InventoryPressDevice.KeyboardMouse) return false;
            if (!beltHasFocus || gameAborted || battleStance || !openInventoryInteractable) return false;
            return !inventoryShowingCharacter;
        }
    }
}
