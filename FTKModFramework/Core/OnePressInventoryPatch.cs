using System;
using System.Collections;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>One-press inventory tweak (convenience.one-press-inventory). The private
    /// CharacterOverworld.CheckInput moves input focus to the HUD belt on an Inventory press, and only
    /// a second press, read by uiPlayerMainHud.Update, starts InventoryToggleSequence(true). The prefix
    /// notes whether the character's own focus held input before CheckInput; the postfix treats a move
    /// to the belt during the call as the accepted press, because that SetFocus is the only one in
    /// CheckInput. For a keyboard and mouse press it then starts the same coroutine on the same HUD.
    ///
    /// The open waits two frames. Unity does not order Update calls between the character, the belt and
    /// the HUD, so vanilla could read the same press in the same frame and start its own open, which runs
    /// in the next frame. Waiting past that frame and rechecking every vanilla gate means the tweak opens
    /// only when vanilla did not. Controller presses, and any doubt about the device, stay vanilla.</summary>
    [HarmonyPatch(typeof(CharacterOverworld), "CheckInput")]
    internal static class OnePressInventoryPatch
    {
        private const string InventoryAction = "Inventory";

        private static void Prefix(CharacterOverworld __instance, out bool __state)
        {
            __state = false;
            int handle = FrameworkTweaks.OnePressInventory;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                uiPlayerMainHud hud = __instance.m_UIPlayMainHud;
                __state = hud != null && __instance.m_InputFocus != null && __instance.m_InputFocus.m_HasInputFocus
                    && !BeltHasFocus(hud);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                __state = false;
            }
        }

        private static void Postfix(CharacterOverworld __instance, bool __state)
        {
            if (!__state) return;
            int handle = FrameworkTweaks.OnePressInventory;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                uiPlayerMainHud hud = __instance.m_UIPlayMainHud;
                // SetFocus declines while a popup is pending; then the belt never took focus and a
                // second vanilla press could not open the inventory either.
                if (hud == null || !BeltHasFocus(hud)) return;
                InventoryPressDevice device = OnePressInventoryDecision.ClassifyPress(
                    __instance.m_IsUseMouse, __instance.m_IsUseController, BoundKeyOrMouseButtonDown());
                if (!ShouldOpen(handle, __instance, hud, device)) return;
                hud.StartCoroutine(OpenAfterVanillaFrame(__instance, hud, handle, device));
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }

        private static IEnumerator OpenAfterVanillaFrame(CharacterOverworld cow, uiPlayerMainHud hud, int handle, InventoryPressDevice device)
        {
            // Frame N+1 runs any open vanilla started in frame N; the check below runs in frame N+2.
            yield return null;
            yield return null;
            OpenIfStillAllowed(cow, hud, handle, device);
        }

        private static void OpenIfStillAllowed(CharacterOverworld cow, uiPlayerMainHud hud, int handle, InventoryPressDevice device)
        {
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                if (cow == null || hud == null || !ShouldOpen(handle, cow, hud, device)) return;
                hud.StartCoroutine(cow.InventoryToggleSequence(true));
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }

        /// <summary>Reads the vanilla gates. Vanilla's branch runs only in an active HUD's Update, and a
        /// missing singleton means that branch would throw rather than open, so neither opens here.</summary>
        private static bool ShouldOpen(int handle, CharacterOverworld cow, uiPlayerMainHud hud, InventoryPressDevice device)
        {
            GameLogic logic = GameLogic.Instance;
            FTKUI ui = FTKUI.Instance;
            if (!hud.isActiveAndEnabled || logic == null || ui == null || ui.m_BattleStanceButtons == null
                || ui.m_PlayerInventory == null || hud.m_OpenInventory == null) return false;
            uiPlayerInventory inventory = ui.m_PlayerInventory;
            bool showingCharacter = inventory.gameObject.activeSelf && inventory.m_InventoryOwner == cow;
            return OnePressInventoryDecision.ShouldOpen(Tweaks.Registry, handle, device,
                BeltHasFocus(hud), logic.m_GameAborted, ui.m_BattleStanceButtons.m_Initialized,
                hud.m_OpenInventory.interactable, showingCharacter);
        }

        private static bool BeltHasFocus(uiPlayerMainHud hud)
        {
            FTKInput input = FTKInput.Instance;
            uiPlayerBeltContainer belt = hud.m_QuickUseInput;
            return input != null && belt != null && input.m_CurrentInputFocus == belt && belt.m_HasInputFocus;
        }

        /// <summary>True when a keyboard key or mouse button bound to Inventory went down this frame. The
        /// bindings are FTKInput's remappable keys, which FTKInput.GetButtonDown reads from the shared
        /// keyboard and mouse; it tests keys through Unity's Input as FTKInput does for modifiers.</summary>
        private static bool BoundKeyOrMouseButtonDown()
        {
            FTKInput input = FTKInput.Instance;
            FTKInput.RemapKeyInfo info = input != null ? input.GetRemapKeyInfo(InventoryAction) : null;
            if (info == null || info.m_PosKeys == null) return false;
            foreach (KeyCode key in info.m_PosKeys)
            {
                if (FTKInput.IsKeyCodeMouse(key))
                {
                    if (FTKInput.GetMouseButtonDown(key)) return true;
                }
                else if (FTKInput.IsKeyCodeKey(key) && UnityEngine.Input.GetKeyDown(key))
                {
                    return true;
                }
            }
            return false;
        }
    }
}
