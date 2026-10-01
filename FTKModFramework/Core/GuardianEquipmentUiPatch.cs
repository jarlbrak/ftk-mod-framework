using System;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiItemDetail), "Show")]
    internal static class GuardianEquipmentUiPatch
    {
        private static void Prefix(uiItemDetail __instance)
        {
            RestoreLayout(__instance);
        }

        internal static void RestoreLayout(uiItemDetail detail)
        {
            try
            {
                if (detail == null) return;
                GuardianCardLayoutState state = detail.GetComponent<GuardianCardLayoutState>();
                if (state != null) state.Restore();
            }
            catch (Exception e) { Plugin.Log.LogWarning("[guardian-item-ui] layout restore: " + e.Message); }
        }

        private static void Postfix(uiItemDetail __instance, FTK_itembase.ID _itemID,
            CharacterOverworld _cow)
        {
            try
            {
                string set = GuardianRuntime.SetEquipmentDescription((int)_itemID, _cow);
                Text nativeDetail = null;
                string section = string.Empty;
                if (__instance.m_ArmorPanel != null && __instance.m_ArmorPanel.gameObject.activeSelf &&
                    __instance.m_EquippableProperties != null)
                {
                    string perks = GuardianRuntime.EquipmentDescription((int)_itemID);
                    section = GuardianEquipmentDescription.Append(perks, set);
                    nativeDetail = __instance.m_EquippableProperties;
                }
                else if (__instance.m_WeaponDetail != null && __instance.m_WeaponDetail.gameObject.activeSelf &&
                    __instance.m_WeaponDetail.m_WeaponStatDisplay != null)
                {
                    string perks = GuardianRuntime.EquipmentDescription((int)_itemID);
                    section = GuardianEquipmentDescription.Append(perks, set);
                    nativeDetail = __instance.m_WeaponDetail.m_WeaponStatDisplay;
                }
                if (nativeDetail != null && section.Length != 0)
                {
                    GuardianCardLayoutState state = __instance.GetComponent<GuardianCardLayoutState>();
                    if (state == null) state = __instance.gameObject.AddComponent<GuardianCardLayoutState>();
                    Text style = __instance.m_EquippableProperties != null
                        ? __instance.m_EquippableProperties : nativeDetail;
                    state.ShowSection(__instance, nativeDetail, style, section);
                }
            }
            catch (Exception e)
            {
                RestoreLayout(__instance);
                Plugin.Log.LogWarning("[guardian-item-ui] " + e.Message);
            }
        }
    }

    [HarmonyPatch(typeof(uiItemDetail), "ShowOtherLoreItemDetail")]
    internal static class GuardianOtherLoreLayoutPatch
    {
        private static void Prefix(uiItemDetail __instance)
        {
            GuardianEquipmentUiPatch.RestoreLayout(__instance);
        }
    }

    [HarmonyPatch(typeof(uiItemDetail), "ShowLoreLockedItemDetail")]
    internal static class GuardianLockedLoreLayoutPatch
    {
        private static void Prefix(uiItemDetail __instance)
        {
            GuardianEquipmentUiPatch.RestoreLayout(__instance);
        }
    }

    [HarmonyPatch(typeof(uiInventoryItemDisplay), "Close2")]
    internal static class GuardianCardCloseLayoutPatch
    {
        private static void Prefix(uiInventoryItemDisplay __instance)
        {
            GuardianEquipmentUiPatch.RestoreLayout(__instance.m_ItemDetail);
        }
    }

    // The native title and portrait share stretch anchors with the card root. Keep their
    // geometry and the native stats untouched; the set text owns only its sibling and BG extent.
    internal sealed class GuardianCardLayoutState : MonoBehaviour
    {
        private RectTransform background;
        private RectTransform[] footers;
        private RectTransform cardRoot;
        private GameObject sectionObject;
        private Text nativeDetail;
        private uiItemDetail owner;
        private Vector2 backgroundSize, backgroundPosition;
        private Vector2[] footerPositions;
        private Vector3 cardPosition;
        private RectTransform[] backgroundChildren;
        private Vector2[] backgroundChildPositions;
        private Vector2 expandedBackgroundSize, expandedBackgroundPosition;
        private Vector2[] expandedFooterPositions, expandedChildPositions;
        private Vector3 expandedCardPosition;
        private bool frontSuspended;
        private bool applied;

        internal void Restore()
        {
            if (!applied) return;
            if (sectionObject != null) sectionObject.SetActive(false);
            if (cardRoot != null) cardRoot.position = cardPosition;
            if (background != null) { background.sizeDelta = backgroundSize; background.anchoredPosition = backgroundPosition; }
            if (backgroundChildren != null)
                for (int i = 0; i < backgroundChildren.Length; i++)
                    if (backgroundChildren[i] != null)
                        backgroundChildren[i].anchoredPosition = backgroundChildPositions[i];
            if (footers != null)
                for (int i = 0; i < footers.Length; i++)
                    if (footers[i] != null) footers[i].anchoredPosition = footerPositions[i];
            backgroundChildren = null;
            backgroundChildPositions = null;
            footers = null;
            footerPositions = null;
            nativeDetail = null;
            owner = null;
            frontSuspended = false;
            applied = false;
        }

        private void LateUpdate()
        {
            if (!applied || sectionObject == null || nativeDetail == null || owner == null) return;
            bool extraVisible = owner.m_WeaponExtraDetail != null &&
                owner.m_WeaponExtraDetail.gameObject.activeInHierarchy;
            if (extraVisible != frontSuspended)
            {
                frontSuspended = extraVisible;
                cardRoot.position = extraVisible ? cardPosition : expandedCardPosition;
                background.sizeDelta = extraVisible ? backgroundSize : expandedBackgroundSize;
                background.anchoredPosition = extraVisible ? backgroundPosition : expandedBackgroundPosition;
                if (backgroundChildren != null)
                    for (int i = 0; i < backgroundChildren.Length; i++)
                        if (backgroundChildren[i] != null)
                            backgroundChildren[i].anchoredPosition = extraVisible
                                ? backgroundChildPositions[i] : expandedChildPositions[i];
                if (footers != null)
                    for (int i = 0; i < footers.Length; i++)
                        if (footers[i] != null)
                            footers[i].anchoredPosition = extraVisible
                                ? footerPositions[i] : expandedFooterPositions[i];
            }
            bool show = !extraVisible && nativeDetail.gameObject.activeInHierarchy;
            if (sectionObject.activeSelf != show) sectionObject.SetActive(show);
        }

        internal void ShowSection(uiItemDetail itemDetail, Text nativeDetail, Text styleSource, string text)
        {
            RectTransform card = itemDetail == null ? null : itemDetail.transform as RectTransform;
            if (card == null || nativeDetail == null || string.IsNullOrEmpty(text)) return;
            RectTransform bg = card.Find("MainDisplay/BG") as RectTransform;
            if (bg == null) bg = card.Find("BG") as RectTransform;
            if (bg == null || bg.GetComponent<Image>() == null) return;
            Canvas canvas = card.GetComponentInParent<Canvas>();
            if (canvas == null) return;
            RectTransform body = nativeDetail.rectTransform;
            if (body == null) return;
            Restore();

            background = bg;
            footers = new RectTransform[] {
                card.Find("MainDisplay/bottomInstructions") as RectTransform,
                card.Find("bottomInstructionBG") as RectTransform,
                card.Find("ButtonPromptTag") as RectTransform
            };
            footerPositions = new Vector2[footers.Length];
            for (int i = 0; i < footers.Length; i++)
                if (footers[i] != null) footerPositions[i] = footers[i].anchoredPosition;
            cardRoot = card;
            this.nativeDetail = nativeDetail;
            owner = itemDetail;
            backgroundSize = bg.sizeDelta; backgroundPosition = bg.anchoredPosition;
            cardPosition = card.position;
            applied = true;

            if (sectionObject == null)
                sectionObject = new GameObject("GuardianSetInfo", typeof(RectTransform), typeof(Text));
            RectTransform sectionRect = sectionObject.GetComponent<RectTransform>();
            sectionRect.SetParent(bg.parent, false);
            sectionRect.SetAsLastSibling();
            sectionObject.SetActive(true);
            sectionRect.anchorMin = sectionRect.anchorMax = new Vector2(0f, 1f);
            sectionRect.pivot = new Vector2(0f, 1f);
            sectionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, body.rect.width);
            sectionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, 1f);
            Text section = sectionObject.GetComponent<Text>();
            section.font = nativeDetail.font;
            section.material = nativeDetail.material;
            section.fontSize = nativeDetail.fontSize;
            section.fontStyle = nativeDetail.fontStyle;
            section.lineSpacing = nativeDetail.lineSpacing;
            section.supportRichText = true;
            section.resizeTextForBestFit = false;
            section.horizontalOverflow = HorizontalWrapMode.Wrap;
            section.verticalOverflow = VerticalWrapMode.Truncate;
            section.alignment = TextAnchor.UpperLeft;
            section.raycastTarget = false;
            section.text = nativeDetail.text == null ? string.Empty : nativeDetail.text.TrimEnd();
            Canvas.ForceUpdateCanvases();
            float nativePreferredHeight = section.text.Length == 0 ? 0f : section.preferredHeight;
            section.font = styleSource.font;
            section.material = styleSource.material;
            section.fontSize = styleSource.fontSize;
            section.fontStyle = FontStyle.Normal;
            section.lineSpacing = styleSource.lineSpacing;
            section.color = new Color32(230, 225, 209, 255);
            section.text = text;

            Canvas.ForceUpdateCanvases();
            float bodyScale = Mathf.Abs(body.lossyScale.y);
            float bgScale = Mathf.Abs(bg.lossyScale.y);
            float sectionScale = Mathf.Abs(sectionRect.lossyScale.y);
            if (bodyScale < 0.01f || bgScale < 0.01f || sectionScale < 0.01f)
            { Restore(); return; }
            float sectionHeight = section.preferredHeight + 4f;
            sectionRect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, sectionHeight);
            Vector3 nativeTopLeft = body.TransformPoint(new Vector3(body.rect.xMin, body.rect.yMax, 0f));
            float nativeUsedHeight = Mathf.Min(nativePreferredHeight, body.rect.height) * bodyScale;
            float gapWorld = 9f * bodyScale;
            sectionRect.position = nativeTopLeft - Vector3.up * (nativeUsedHeight + gapWorld);
            Vector3 bgTop = Top(bg);
            float paddingWorld = 10f * bgScale;
            float neededWorld = Mathf.Max(0f, Bottom(bg).y - Bottom(sectionRect).y + paddingWorld);
            Camera camera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
            float screenBottom = RectTransformUtility.WorldToScreenPoint(camera, Bottom(bg)).y;
            float screenTop = RectTransformUtility.WorldToScreenPoint(camera, bgTop).y;
            float screenPerWorld = Mathf.Abs(screenTop - screenBottom) /
                Mathf.Max(0.01f, Mathf.Abs(bgTop.y - Bottom(bg).y));
            if (screenPerWorld < 0.01f) { Restore(); return; }
            float availableWorld = Mathf.Max(0f, (screenBottom - 8f) / screenPerWorld);
            if (neededWorld > availableWorld)
            {
                float headroomScreen = Screen.height - 8f - RectTransformUtility.WorldToScreenPoint(
                    camera, HighestActiveCorner(card)).y;
                float shiftWorld = Mathf.Min(neededWorld - availableWorld,
                    Mathf.Max(0f, headroomScreen / screenPerWorld));
                card.position += Vector3.up * shiftWorld;
                availableWorld += shiftWorld;
                bgTop = Top(bg);
            }
            if (neededWorld > availableWorld + 0.01f)
            {
                // Preserve a readable native card when the display cannot contain the set copy.
                Restore();
                Plugin.Log.LogWarning("[guardian-item-ui] set section exceeds screen height");
                return;
            }
            // EquippedDisplay's BG also contains its native detail panels. Hold those
            // panels in place while extending the sliced backing beneath them.
            Vector3[] childWorldPositions = null;
            if (bg.parent == card)
            {
                int childCount = bg.childCount;
                backgroundChildren = new RectTransform[childCount];
                backgroundChildPositions = new Vector2[childCount];
                childWorldPositions = new Vector3[childCount];
                for (int i = 0; i < childCount; i++)
                {
                    RectTransform child = bg.GetChild(i) as RectTransform;
                    if (child == null || child.anchorMin.y != child.anchorMax.y)
                    { Restore(); return; }
                    backgroundChildren[i] = child;
                    backgroundChildPositions[i] = child.anchoredPosition;
                    childWorldPositions[i] = child.position;
                }
            }
            GrowBelow(bg, bg.rect.height + neededWorld / bgScale, bgTop);
            if (backgroundChildren != null)
                for (int i = 0; i < backgroundChildren.Length; i++)
                    backgroundChildren[i].position = childWorldPositions[i];
            for (int i = 0; i < footers.Length; i++)
                if (footers[i] != null)
                {
                    float footerShiftWorld = neededWorld;
                    if (footers[i].gameObject.activeInHierarchy)
                        footerShiftWorld = Mathf.Max(footerShiftWorld,
                            Top(footers[i]).y - Bottom(sectionRect).y + 8f * sectionScale);
                    footers[i].anchoredPosition = footerPositions[i] +
                        Vector2.down * (footerShiftWorld /
                            Mathf.Max(0.01f, Mathf.Abs(footers[i].parent.lossyScale.y)));
                }
            expandedCardPosition = card.position;
            expandedBackgroundSize = bg.sizeDelta;
            expandedBackgroundPosition = bg.anchoredPosition;
            expandedFooterPositions = new Vector2[footers.Length];
            for (int i = 0; i < footers.Length; i++)
                if (footers[i] != null) expandedFooterPositions[i] = footers[i].anchoredPosition;
            if (backgroundChildren != null)
            {
                expandedChildPositions = new Vector2[backgroundChildren.Length];
                for (int i = 0; i < backgroundChildren.Length; i++)
                    expandedChildPositions[i] = backgroundChildren[i].anchoredPosition;
            }
        }

        private static Vector3 Top(RectTransform rect)
        {
            return rect.TransformPoint(new Vector3(0f, rect.rect.yMax, 0f));
        }

        private static Vector3 Bottom(RectTransform rect)
        {
            return rect.TransformPoint(new Vector3(0f, rect.rect.yMin, 0f));
        }

        private static void GrowBelow(RectTransform rect, float height, Vector3 top)
        {
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, height);
            rect.position += top - Top(rect);
        }

        private static Vector3 HighestActiveCorner(RectTransform root)
        {
            Vector3 highest = Top(root);
            RectTransform[] children = root.GetComponentsInChildren<RectTransform>(true);
            Vector3[] corners = new Vector3[4];
            foreach (RectTransform child in children)
            {
                if (!child.gameObject.activeInHierarchy) continue;
                child.GetWorldCorners(corners);
                foreach (Vector3 corner in corners)
                    if (corner.y > highest.y) highest = corner;
            }
            return highest;
        }
    }

}
