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
        private static bool _keyWarned;

        private uiPlayerMainHud _hud;
        private int _shown = -1;
        private int _focus = -1;
        private int _pipCount = -1;
        private readonly List<int> _boundKeys = new List<int>();

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
                if (shown > 0 && KeyPressed()) Refund("the refund key");
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

        /// <summary>The configured key went down this frame, on the keyboard of a keyboard and mouse
        /// character, outside the chat box, and no vanilla action uses it. GetKeyDown is the only
        /// per-frame read; the rest runs on a press.</summary>
        private bool KeyPressed()
        {
            ConfigEntry<KeyCode> key = Plugin.RefundMovementFocusKey;
            if (key == null) return false;
            KeyCode code = key.Value;
            if (code == KeyCode.None || !Input.GetKeyDown(code)) return false;
            uiChatBox chat = uiChatBox.Instance;
            if (chat != null && chat.IsTextInputInFocus()) return false;
            CollectBoundKeys();
            if (RefundFocusInput.Usable((int)code, _boundKeys)) return true;
            if (!_keyWarned)
            {
                _keyWarned = true;
                Plugin.Log.LogWarning("Tweaks: the refund movement focus key " + code + " is not a free keyboard key (a game control uses it, "
                    + "or it is a mouse or controller button), so it is ignored. Change [TweakKeys] RefundMovementFocus in the framework config.");
            }
            return false;
        }

        private void CollectBoundKeys()
        {
            _boundKeys.Clear();
            FTKInput input = FTKInput.Instance;
            if (input == null || input.m_RemappableKeys == null) return;
            foreach (FTKInput.RemapKeyInfo info in input.m_RemappableKeys)
            {
                if (info == null) continue;
                AddKeys(info.m_PosKeys);
                AddKeys(info.m_NegKeys);
            }
        }

        private void AddKeys(KeyCode[] keys)
        {
            if (keys == null) return;
            for (int i = 0; i < keys.Length; i++)
                if (keys[i] != KeyCode.None) _boundKeys.Add((int)keys[i]);
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
    internal sealed class RefundFocusPip : MonoBehaviour, IPointerClickHandler
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
                    KeyCode key = Plugin.RefundMovementFocusKey != null ? Plugin.RefundMovementFocusKey.Value : KeyCode.None;
                    _tooltip.SetToolTipInfo(RefundFocusText.Title, RefundFocusText.Detail(key == KeyCode.None ? null : key.ToString()), true);
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
            enabled = false;
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (!_marked || _owner == null || eventData.button != PointerEventData.InputButton.Left) return;
            _owner.Refund("a focus pip");
        }
    }
}
