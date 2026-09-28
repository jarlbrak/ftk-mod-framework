using System;
using System.Collections.Generic;
using BepInEx.Configuration;
using HarmonyLib;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    /// <summary>Refund movement focus HUD (Spec #264 FR-5). The private uiPlayerMainHud.SetFocusMeter
    /// keeps one Image per MaxFocus point in m_FocusPoints, cloning m_MasterFocusPoint as needed, and
    /// shows each pip's fill, its child 0, when the index is below m_FocusPoints. The postfix attaches
    /// one RefundFocusHud to a HUD whose character this client owns, only while the tweak is on, so an
    /// off tweak adds nothing to any HUD. SetFocusMeter runs only when the HUD is flagged for update,
    /// and UpdatePlayerAction never flags it, so the component also watches the gates each frame and
    /// redraws only when the shown count or the focus changes.</summary>
    [HarmonyPatch(typeof(uiPlayerMainHud), "SetFocusMeter")]
    internal static class RefundFocusMeterPatch
    {
        private static void Postfix(uiPlayerMainHud __instance)
        {
            int handle = FrameworkTweaks.RefundMovementFocus;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                CharacterOverworld cow = __instance.m_Cow;
                if (cow == null || !cow.IsOwner) return;
                RefundFocusHud hud = __instance.GetComponent<RefundFocusHud>();
                if (hud == null) hud = __instance.gameObject.AddComponent<RefundFocusHud>();
                hud.Bind(__instance);
                hud.enabled = true;
                hud.Redraw();
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }
    }

    /// <summary>One per owned character's HUD. Marks the refundable pips, polls the refund key while a
    /// refund is possible, and restores every pip it changed when the tweak goes off.</summary>
    internal sealed class RefundFocusHud : MonoBehaviour
    {
        /// <summary>The last key value warned about, so each unusable value warns once.</summary>
        private static int _warnedKey = RefundFocusInput.None;
        private static readonly List<int> BoundKeys = new List<int>();

        private uiPlayerMainHud _hud;
        private int _shown = -1;
        private int _focus = -1;
        private int _pipCount = -1;

        internal void Bind(uiPlayerMainHud hud)
        {
            _hud = hud;
        }

        /// <summary>Redraws from the current state. The postfix calls it after vanilla has reset every
        /// fill, so it always reapplies.</summary>
        internal void Redraw()
        {
            _shown = -1;
            Refresh();
            // Checked here too, not only on a press, so a conflicting key warns once the HUD is up
            // even when the game acts on the press first (End Turn ends the turn and hides the pips).
            KeyCode code;
            ConfiguredKey(out code);
        }

        private void Update()
        {
            int handle = FrameworkTweaks.RefundMovementFocus;
            if (!Tweaks.IsOn(handle))
            {
                RestoreAll();
                enabled = false;
                return;
            }
            try
            {
                int shown = Refresh();
                if (KeyPressed() && shown > 0) Refund("the refund key");
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                RestoreAll();
                enabled = false;
            }
        }

        /// <summary>Recomputes the shown count and applies it only when it or the pips changed.</summary>
        private int Refresh()
        {
            if (_hud == null || _hud.m_Cow == null || _hud.m_FocusPoints == null) return 0;
            CharacterOverworld cow = _hud.m_Cow;
            RefundFocusState state = RefundMovementFocus.ReadState(cow);
            // Controllers are unsupported, so a character without the keyboard and mouse sees no pips.
            int shown = cow.m_IsUseMouse ? FrameworkTweaks.RefundFocusPips(Tweaks.Registry, FrameworkTweaks.RefundMovementFocus, state) : 0;
            List<Image> pips = _hud.m_FocusPoints;
            if (shown == _shown && state.FocusPoints == _focus && pips.Count == _pipCount) return shown;
            _shown = shown;
            _focus = state.FocusPoints;
            _pipCount = pips.Count;
            for (int i = 0; i < pips.Count; i++)
            {
                Image pip = pips[i];
                if (pip == null || pip == _hud.m_MasterFocusPoint) continue;
                bool marked = RefundFocus.PipMarked(i, state.FocusPoints, shown);
                RefundFocusPip handler = pip.GetComponent<RefundFocusPip>();
                if (handler == null)
                {
                    if (!marked) continue;
                    handler = pip.gameObject.AddComponent<RefundFocusPip>();
                    handler.Attach(this, pip);
                }
                handler.SetMarked(marked, i < state.FocusPoints);
            }
            return shown;
        }

        /// <summary>Called by a pip click or the key. Both are keyboard and mouse input, so in local
        /// multiplayer they act only for a character that uses the keyboard and mouse, as vanilla's own
        /// mouse input does. Redraws at once so a second press sees the new state.</summary>
        internal void Refund(string via)
        {
            if (_hud == null || _hud.m_Cow == null || !_hud.m_Cow.m_IsUseMouse) return;
            if (RefundMovementFocus.TryRefund(_hud.m_Cow, via)) Redraw();
        }

        /// <summary>The configured key went down this frame, outside the chat box, and no game control
        /// uses it. GetKeyDown is the only per-frame read. The conflict check runs on every press,
        /// whether or not a refund is possible, so a conflicting press is never acted on and is always
        /// reported.</summary>
        private static bool KeyPressed()
        {
            ConfigEntry<KeyCode> key = Plugin.RefundMovementFocusKey;
            if (key == null) return false;
            KeyCode pressed = key.Value;
            if (pressed == KeyCode.None || !Input.GetKeyDown(pressed)) return false;
            KeyCode code;
            if (ConfiguredKey(out code) != RefundKeyStatus.Usable) return false;
            uiChatBox chat = uiChatBox.Instance;
            return chat == null || !chat.IsTextInputInFocus();
        }

        /// <summary>The configured key and whether it is usable now, against the fixed game bindings and
        /// the live remap table, warning once for each unusable value.</summary>
        internal static RefundKeyStatus ConfiguredKey(out KeyCode code)
        {
            ConfigEntry<KeyCode> key = Plugin.RefundMovementFocusKey;
            code = key != null ? key.Value : KeyCode.None;
            RefundKeyStatus status = RefundFocusInput.Status((int)code, CollectBoundKeys());
            string warning = RefundFocusInput.Warning(status, code.ToString());
            if (warning != null && _warnedKey != (int)code)
            {
                _warnedKey = (int)code;
                Plugin.Log.LogWarning(warning);
            }
            return status;
        }

        /// <summary>Every key and modifier key of FTKInput.m_RemappableKeys, the player's current
        /// bindings, or null while the table is not loaded, which means the defaults.</summary>
        private static List<int> CollectBoundKeys()
        {
            FTKInput input = FTKInput.Instance;
            if (input == null || input.m_RemappableKeys == null) return null;
            BoundKeys.Clear();
            foreach (FTKInput.RemapKeyInfo info in input.m_RemappableKeys)
            {
                if (info == null) continue;
                AddKeys(info.m_PosKeys, info.m_PosMods);
                AddKeys(info.m_NegKeys, info.m_NegMods);
            }
            return BoundKeys;
        }

        private static void AddKeys(KeyCode[] keys, FTKInput.FTKModifierKeyFlags[] mods)
        {
            if (keys != null)
                for (int i = 0; i < keys.Length; i++)
                    if (keys[i] != KeyCode.None) BoundKeys.Add((int)keys[i]);
            if (mods == null) return;
            for (int i = 0; i < mods.Length; i++)
            {
                if (mods[i] == 0) continue;
                foreach (KeyCode modifier in FTKInput.GetKeyFromModifier(mods[i])) BoundKeys.Add((int)modifier);
            }
        }

        private void RestoreAll()
        {
            _shown = -1;
            if (_hud == null || _hud.m_FocusPoints == null || _hud.m_Cow == null || _hud.m_Cow.m_CharacterStats == null) return;
            int focus = _hud.m_Cow.m_CharacterStats.m_FocusPoints;
            List<Image> pips = _hud.m_FocusPoints;
            for (int i = 0; i < pips.Count; i++)
            {
                Image pip = pips[i];
                if (pip == null) continue;
                RefundFocusPip handler = pip.GetComponent<RefundFocusPip>();
                if (handler != null) handler.SetMarked(false, i < focus);
            }
        }

        private void OnDisable()
        {
            _shown = -1;
        }
    }

    /// <summary>Added to a pip the first time it is refundable and kept, disabled, afterwards, so a
    /// redraw never allocates. While disabled Unity sends it no pointer events, so an unmarked pip
    /// behaves as vanilla's. Marking fades the pip's fill through a CanvasGroup, makes the pip a raycast
    /// target and shows the tooltip; unmarking restores each value and vanilla's fill state.</summary>
    internal sealed class RefundFocusPip : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        private const float FadedAlpha = 0.4f;

        private RefundFocusHud _owner;
        private Image _pip;
        private GameObject _fill;
        private CanvasGroup _fade;
        private uiToolTipGeneral _tooltip;
        private bool _ownTooltip;
        private string _info;
        private string _detail;
        private bool _raw;
        private bool _raycast;
        private bool _marked;
        private PointerEventData _entered;

        internal void Attach(RefundFocusHud owner, Image pip)
        {
            _owner = owner;
            _pip = pip;
            _fill = pip.transform.childCount > 0 ? pip.transform.GetChild(0).gameObject : null;
            _tooltip = pip.GetComponent<uiToolTipGeneral>();
            _ownTooltip = _tooltip == null;
            if (_ownTooltip)
            {
                _tooltip = pip.gameObject.AddComponent<uiToolTipGeneral>();
                _tooltip.enabled = false;
            }
            enabled = false;
        }

        /// <summary>Vanilla's SetFocusMeter hides every fill at or above m_FocusPoints each time it
        /// runs, so a marked pip's fill and raycast state are reapplied on every call; the originals
        /// are saved and the tooltip set only when the pip becomes marked.</summary>
        internal void SetMarked(bool marked, bool vanillaFilled)
        {
            if (marked)
            {
                if (!_marked)
                {
                    _marked = true;
                    _raycast = _pip.raycastTarget;
                    if (_ownTooltip) _tooltip.enabled = true;
                    else
                    {
                        _info = _tooltip.m_Info;
                        _detail = _tooltip.m_DetailInfo;
                        _raw = _tooltip.m_ReturnRawInfo;
                    }
                    KeyCode key;
                    bool usable = RefundFocusHud.ConfiguredKey(out key) == RefundKeyStatus.Usable;
                    _tooltip.SetToolTipInfo(RefundFocusText.Title, RefundFocusText.Detail(usable ? key.ToString() : null), true);
                    enabled = true;
                }
                if (_fill != null)
                {
                    if (_fade == null) _fade = _fill.GetComponent<CanvasGroup>();
                    if (_fade == null) _fade = _fill.AddComponent<CanvasGroup>();
                    _fade.alpha = FadedAlpha;
                    _fill.SetActive(true);
                }
                _pip.raycastTarget = true;
                return;
            }
            if (!_marked) return;
            _marked = false;
            if (_fade != null) _fade.alpha = 1f;
            if (_fill != null) _fill.SetActive(vanillaFilled);
            _pip.raycastTarget = _raycast;
            if (_ownTooltip)
            {
                _tooltip.SetToolTipInfo(null, null, true);
                _tooltip.enabled = false;
            }
            else _tooltip.SetToolTipInfo(_info, _detail, _raw);
            _entered = null;
            enabled = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_marked || _owner == null || eventData.button != PointerEventData.InputButton.Left) return;
            _owner.Refund("a focus pip");
        }

        /// <summary>BaseInputModule.HandlePointerExitAndEnter sends pointer enter to the new target and
        /// then to each ancestor up to the common root with the previous target. From the map that
        /// includes focusBar, the pips' parent, whose own uiToolTipGeneral ("Focus Points") enters after
        /// the pip's and replaces it; from a neighbouring pip focusBar is the common root and is skipped.
        /// So the pip notes the enter and, after this frame's events, takes the tooltip back once.</summary>
        public void OnPointerEnter(PointerEventData eventData)
        {
            if (_marked) _entered = eventData;
        }

        public void OnPointerExit(PointerEventData eventData)
        {
            _entered = null;
        }

        private void LateUpdate()
        {
            if (_entered == null) return;
            PointerEventData entered = _entered;
            _entered = null;
            int handle = FrameworkTweaks.RefundMovementFocus;
            if (!Tweaks.IsOn(handle)) return;
            try
            {
                uiToolTipManager manager = uiToolTipManager.Instance;
                if (!_marked || manager == null || _tooltip == null) return;
                IToolTipInfo current = manager.m_CurrentToolTip;
                if (current == null || ReferenceEquals(current, _tooltip)) return;
                manager.ClientOnPointerEnter(_tooltip, entered);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }
    }
}
