using System;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    // The native class panel has no wearer. Show class eligibility there, then report
    // the current visible armor role on the owned inventory's class labels.
    [HarmonyPatch(typeof(uiSelectCharacterInfo), "ShowCharacterInfo")]
    internal static class ThiefClassSelectionPatch
    {
        [HarmonyPriority(Priority.Last)]
        private static void Postfix(uiSelectCharacterInfo __instance, FTK_playerGameStart.ID _characterType)
        {
            try
            {
                if (__instance == null || __instance.m_ClassAbility == null) return;
                Text label = __instance.m_ClassAbility;
                if (!ThiefRuntime.IsThiefClass((int)_characterType))
                { ThiefRoleTooltip.Clear(label); ThiefClassRolePanel.Clear(__instance); return; }
                string native = label.text ?? string.Empty;
                string marker = "Skill: Opportunist";
                string proposed = native.TrimEnd() + "\n" + marker;
                label.text = proposed;
                Canvas.ForceUpdateCanvases();
                if (label.preferredHeight > label.rectTransform.rect.height + 0.5f ||
                    label.preferredWidth > label.rectTransform.rect.width + 0.5f)
                {
                    label.text = native.TrimEnd() + "\nSkill: Opportunist";
                    Canvas.ForceUpdateCanvases();
                    if (label.preferredHeight > label.rectTransform.rect.height + 0.5f ||
                        label.preferredWidth > label.rectTransform.rect.width + 0.5f)
                        label.text = native;
                }
                // Party Select has no uiToolTipManager. Keep the class rules visible
                // in its existing text panel rather than attaching an inert hover.
                ThiefClassRolePanel.Show(__instance);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[thief-class-ui] " + e.Message); }
        }
    }

    [HarmonyPatch(typeof(uiSelectCharacterInfo), "HideCharacterInfo")]
    internal static class ThiefClassSelectionHidePatch
    {
        private static void Prefix(uiSelectCharacterInfo __instance)
        {
            if (__instance == null) return;
            ThiefRoleTooltip.Clear(__instance.m_ClassAbility);
            ThiefClassRolePanel.Clear(__instance);
        }
    }

    internal sealed class ThiefClassRolePanel : MonoBehaviour
    {
        private Text startingItems;
        private VerticalWrapMode originalOverflow;

        internal static void Clear(uiSelectCharacterInfo owner)
        {
            if (owner == null) return;
            ThiefClassRolePanel panel = owner.GetComponent<ThiefClassRolePanel>();
            if (panel == null || panel.startingItems == null) return;
            panel.startingItems.verticalOverflow = panel.originalOverflow;
            panel.startingItems = null;
        }

        internal static void Show(uiSelectCharacterInfo owner)
        {
            if (owner == null || owner.m_StartingItems == null) return;
            ThiefClassRolePanel panel = owner.GetComponent<ThiefClassRolePanel>();
            if (panel == null) panel = owner.gameObject.AddComponent<ThiefClassRolePanel>();
            Text items = owner.m_StartingItems;
            if (panel.startingItems != items)
            {
                panel.startingItems = items;
                panel.originalOverflow = items.verticalOverflow;
            }
            string native = items.text ?? string.Empty;
            string summary = "Opportunist: 1 Sneak hit/open foe/turn.\n" +
                "Match head/body/feet: 2 minor, 3 full.\n" +
                "Locksmith prepares; Nightblade opens.\n" +
                "Wayfarer evades; all cost Sneak power.";
            items.verticalOverflow = VerticalWrapMode.Overflow;
            items.text = native.TrimEnd() + "\n" + summary;
            Canvas.ForceUpdateCanvases();
            RectTransform frame = owner.m_DisplayRoot == null ? null : owner.m_DisplayRoot.transform as RectTransform;
            if (frame == null) return;
            float scale = Mathf.Abs(items.rectTransform.lossyScale.y);
            float bottom = items.rectTransform.TransformPoint(
                new Vector3(0f, items.rectTransform.rect.yMax - items.preferredHeight, 0f)).y;
            float frameBottom = frame.TransformPoint(new Vector3(0f, frame.rect.yMin, 0f)).y;
            if (scale < 0.01f || bottom < frameBottom + 12f * scale)
                items.text = native.TrimEnd() + "\nOpportunist: 1 Sneak hit/open foe/turn.\n" +
                    "Match head/body/feet: 2 minor, 3 full.";
        }
    }

    [HarmonyPatch(typeof(uiPlayerInventory), "UpdateStatsText")]
    internal static class ThiefOwnedClassInfoPatch
    {
        private static void Postfix(uiPlayerInventory __instance)
        {
            try
            {
                if (__instance == null) return;
                CharacterOverworld owner = __instance.m_InventoryOwner;
                string detail = ThiefRuntime.OwnedEquipmentDescription(owner);
                string label = ThiefRuntime.OwnedEquipmentLabel(owner);
                Update(__instance.m_playerClassDisplay, detail, label);
                Update(__instance.m_playerStatsClassDisplay, detail, label);
                ThiefOwnedClassRefreshState state = __instance.GetComponent<ThiefOwnedClassRefreshState>();
                if (state == null) state = __instance.gameObject.AddComponent<ThiefOwnedClassRefreshState>();
                state.Owner = owner;
                state.Role = label;
                state.Detail = detail;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[thief-owned-ui] " + e.Message); }
        }

        private static void Update(Text text, string detail, string role)
        {
            if (text == null) return;
            if (string.IsNullOrEmpty(detail)) { ThiefRoleTooltip.Clear(text); return; }
            string native = text.text ?? string.Empty;
            string proposed = native + " | " + role;
            text.text = proposed;
            Canvas.ForceUpdateCanvases();
            if (text.preferredWidth > text.rectTransform.rect.width + 0.5f) text.text = native;
            ThiefRoleTooltip.Show(text, "Owned Thief armor role", detail);
        }
    }

    [HarmonyPatch(typeof(uiPlayerInventory), "Update")]
    internal static class ThiefOwnedClassRefreshPatch
    {
        private static bool loggedFailure;
        private static void Postfix(uiPlayerInventory __instance)
        {
            try
            {
                if (__instance == null || !__instance.m_IsShowing || __instance.m_InventoryOwner == null) return;
                ThiefOwnedClassRefreshState state = __instance.GetComponent<ThiefOwnedClassRefreshState>();
                if (state == null || state.Owner != __instance.m_InventoryOwner ||
                    state.Role != ThiefRuntime.OwnedEquipmentLabel(__instance.m_InventoryOwner) ||
                    state.Detail != ThiefRuntime.OwnedEquipmentDescription(__instance.m_InventoryOwner))
                    __instance.UpdateStatsText();
            }
            catch (Exception e)
            {
                if (loggedFailure) return;
                loggedFailure = true;
                Plugin.Log.LogWarning("[thief-owned-ui] refresh: " + e.Message);
            }
        }
    }

    internal sealed class ThiefOwnedClassRefreshState : MonoBehaviour
    {
        internal CharacterOverworld Owner;
        internal string Role;
        internal string Detail;
    }

    [HarmonyPatch(typeof(uiPlayerInventory), "OnClose")]
    internal static class ThiefOwnedClassClosePatch
    {
        private static void Prefix(uiPlayerInventory __instance)
        {
            if (__instance == null) return;
            ThiefRoleTooltip.Clear(__instance.m_playerClassDisplay);
            ThiefRoleTooltip.Clear(__instance.m_playerStatsClassDisplay);
        }
    }

    internal sealed class ThiefRoleTooltip : uiToolTipGeneral
    {
        private bool originalRaycast;
        private bool changedRaycast;

        internal static void Show(Text text, string title, string detail)
        {
            if (text == null) return;
            uiToolTipGeneral existing = text.GetComponent<uiToolTipGeneral>();
            if (existing != null && !(existing is ThiefRoleTooltip))
            {
                ThiefRoleTooltipOverrideState previous = text.GetComponent<ThiefRoleTooltipOverrideState>();
                if (previous == null) previous = text.gameObject.AddComponent<ThiefRoleTooltipOverrideState>();
                if (!previous.Active)
                {
                    previous.Target = existing;
                    previous.Info = existing.m_Info;
                    previous.Detail = existing.m_DetailInfo;
                    previous.Raw = existing.m_ReturnRawInfo;
                    previous.Enabled = existing.enabled;
                    previous.Raycast = text.raycastTarget;
                    previous.Active = true;
                }
                text.raycastTarget = true;
                existing.SetToolTipInfo(title, detail, true);
                existing.enabled = true;
                return;
            }
            ThiefRoleTooltip tooltip = text.GetComponent<ThiefRoleTooltip>();
            if (tooltip == null) tooltip = text.gameObject.AddComponent<ThiefRoleTooltip>();
            if (!tooltip.changedRaycast)
            {
                tooltip.originalRaycast = text.raycastTarget;
                tooltip.changedRaycast = true;
            }
            text.raycastTarget = true;
            tooltip.SetToolTipInfo(title, detail, true);
            tooltip.enabled = true;
        }

        internal static void Clear(Text text)
        {
            if (text == null) return;
            ThiefRoleTooltipOverrideState previous = text.GetComponent<ThiefRoleTooltipOverrideState>();
            if (previous != null && previous.Active && previous.Target != null)
            {
                previous.Target.SetToolTipInfo(previous.Info, previous.Detail, previous.Raw);
                previous.Target.enabled = previous.Enabled;
                text.raycastTarget = previous.Raycast;
                previous.Active = false;
            }
            ThiefRoleTooltip tooltip = text.GetComponent<ThiefRoleTooltip>();
            if (tooltip == null) return;
            tooltip.SetToolTipInfo(null, null, true);
            tooltip.enabled = false;
            if (tooltip.changedRaycast) text.raycastTarget = tooltip.originalRaycast;
            tooltip.changedRaycast = false;
        }
    }

    internal sealed class ThiefRoleTooltipOverrideState : MonoBehaviour
    {
        internal uiToolTipGeneral Target;
        internal string Info, Detail;
        internal bool Raw, Raycast, Active, Enabled;
    }
}
