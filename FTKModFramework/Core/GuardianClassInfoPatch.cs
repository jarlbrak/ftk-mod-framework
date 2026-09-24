using System;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiSelectCharacterInfo), "ShowCharacterInfo")]
    internal static class GuardianClassInfoPatch
    {
        private static void Prefix(uiSelectCharacterInfo __instance)
        {
            GuardianClassLayoutState state = __instance.GetComponent<GuardianClassLayoutState>();
            if (state != null) state.Restore();
        }

        private static void Postfix(uiSelectCharacterInfo __instance, FTK_playerGameStart.ID _characterType)
        {
            try
            {
                int classId = (int)_characterType;
                if (__instance.m_ClassAbility == null) return;
                bool guardian = GuardianRuntime.IsGuardianClass(classId);
                bool cleansing = OverworldAilmentImmunity.IsRegistered(classId);
                if (!guardian && !cleansing) return;
                if (guardian) __instance.m_ClassAbility.text = GuardianEquipmentDescription.Append(
                    __instance.m_ClassAbility.text, GuardianEquipmentDescription.ClassRules);
                if (cleansing) __instance.m_ClassAbility.text = GuardianEquipmentDescription.Append(
                    __instance.m_ClassAbility.text, OverworldAilmentImmunity.DisplayName(classId) +
                    ": immune to\nPoison/Curse while exploring.");
                GuardianClassLayoutState state = __instance.GetComponent<GuardianClassLayoutState>();
                if (state == null) state = __instance.gameObject.AddComponent<GuardianClassLayoutState>();
                state.Arrange(__instance);
            }
            catch (Exception e) { Plugin.Log.LogWarning("[guardian-class-ui] " + e.Message); }
        }
    }

    // Kept on the native UI instance, not a global dictionary. Restore before every Show so
    // Guardian refreshes cannot accumulate offsets and vanilla reuse gets its original layout.
    internal sealed class GuardianClassLayoutState : MonoBehaviour
    {
        private Text ability;
        private Transform heading, items;
        private Vector3 headingPosition, itemPosition;
        private TextAnchor alignment;
        private bool changed;

        internal void Restore()
        {
            if (!changed) return;
            if (ability != null) ability.alignment = alignment;
            if (heading != null) heading.localPosition = headingPosition;
            if (items != null) items.localPosition = itemPosition;
            changed = false;
        }

        internal void Arrange(uiSelectCharacterInfo info)
        {
            Restore();
            Text label = info.m_ClassAbility;
            Transform parent = label.transform.parent;
            // Both installed native variants have these exact sibling nodes. Do not move the
            // shared DisplayRoot: it also owns the class title and ordinary stat labels.
            Transform group = parent == null ? null : parent.Find("Image (1)");
            Transform header = group == null ? null : group.Find("charItemsHeader");
            Text headingText = header == null ? null : header.GetComponent<Text>();
            if (headingText == null || info.m_StartingItems == null ||
                info.m_StartingItems.transform.parent != parent)
                throw new InvalidOperationException("Native class equipment layout does not match the supported hierarchy");
            ability = label; heading = group; items = info.m_StartingItems.transform;
            alignment = label.alignment; headingPosition = heading.localPosition; itemPosition = items.localPosition;
            changed = true;
            try
            {
                // Native MiddleLeft plus vertical overflow lets long text extend above and below
                // its box. Top anchoring gives the expanded rules one measurable lower boundary.
                label.alignment = TextAnchor.UpperLeft;
                RectTransform textRect = label.rectTransform, headerRect = headingText.rectTransform;
                Vector3 bottom = parent.InverseTransformPoint(textRect.TransformPoint(
                    new Vector3(textRect.rect.xMin, textRect.rect.yMax - label.preferredHeight, 0)));
                Vector3 top = parent.InverseTransformPoint(headerRect.TransformPoint(
                    new Vector3(headerRect.rect.xMin, headerRect.rect.yMax, 0)));
                float shift = GuardianClassLayout.Shift(bottom.y, top.y, 6f);
                heading.localPosition = headingPosition + new Vector3(0, shift, 0);
                items.localPosition = itemPosition + new Vector3(0, shift, 0);
            }
            catch { Restore(); throw; }
        }
    }
}
