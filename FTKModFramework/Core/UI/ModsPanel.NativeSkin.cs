using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core.UI
{
    internal sealed partial class ModsPanel
    {
        private static StartGameFE.GameConfig _nativeMenu;
        private static Text _nativeHeadingText;
        private static Text _nativeBodyText;
        private static Image _nativePanelImage;
        private static Image _nativePreviewImage;
        private static Image _nativePaperImage;
        private static Image _nativePaperTexture;
        private static Image _nativePlaqueImage;
        private static Image _nativeSmallPlaqueImage;
        private static Button _nativeActionButton;
        private static Button _nativeAdventureButton;
        private static Image _nativeAdventureSelector;

        // Read the inactive native screen too: opening Mods must not depend on visiting New Game.
        // Only fresh framework objects receive these shared, read-only asset references.
        internal static void CaptureNativeSkin()
        {
            _nativeMenu = uiStartGame.Instance == null ? null : uiStartGame.Instance.m_GameConfig;
            if (_nativeMenu == null)
            {
                StartGameFE.GameConfig[] menus = Resources.FindObjectsOfTypeAll<StartGameFE.GameConfig>();
                if (menus.Length > 0) _nativeMenu = menus[0];
            }
            if (_nativeMenu == null) return;
            _nativeHeadingText = _nativeMenu.m_CurrentGameDefText;
            _nativeBodyText = _nativeMenu.m_GameDiffDynamicText;
            _nativeActionButton = _nativeMenu.m_BackButton;
            if (_nativeMenu.m_GameDefButtonPrefab != null)
            {
                _nativeAdventureButton = _nativeMenu.m_GameDefButtonPrefab.GetComponent<Button>();
                GameObject selector = _nativeMenu.m_GameDefButtonPrefab.m_Selector;
                if (selector != null) _nativeAdventureSelector = selector.GetComponent<Image>();
            }
            CaptureNativePanels();
        }

        internal static void StyleNativePanel(GameObject target, bool parchment, bool previewFrame = false)
        {
            Image image = target.GetComponent<Image>();
            if (image == null) image = target.AddComponent<Image>();
            Image donor = parchment ? _nativePaperImage : previewFrame ? _nativePreviewImage : _nativePanelImage;
            if (donor != null) CopyNativeImage(donor, image);
            else image.color = parchment ? new Color(0.72f, 0.67f, 0.57f, 1f) : new Color(0.07f, 0.065f, 0.06f, 0.98f);
            if (parchment && _nativePaperTexture != null)
            {
                Transform existing = target.transform.Find("NativePaperTexture");
                GameObject texture = existing == null ? NewChild("NativePaperTexture", target.transform) : existing.gameObject;
                texture.transform.SetAsFirstSibling();
                Image textureImage = texture.GetComponent<Image>();
                if (textureImage == null) textureImage = texture.AddComponent<Image>();
                CopyNativeImage(_nativePaperTexture, textureImage);
                Stretch(textureImage.rectTransform);
                textureImage.rectTransform.offsetMin = _nativePaperTexture.rectTransform.offsetMin;
                textureImage.rectTransform.offsetMax = _nativePaperTexture.rectTransform.offsetMax;
                LayoutElement layout = texture.GetComponent<LayoutElement>();
                if (layout == null) layout = texture.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
            }
            image.raycastTarget = false;
            DisableOldBorder(target);
        }

        internal static void StyleNativePlaque(GameObject target, bool compact = false)
        {
            Image image = target.GetComponent<Image>();
            if (image == null) image = target.AddComponent<Image>();
            Image donor = compact ? _nativeSmallPlaqueImage : _nativePlaqueImage;
            if (donor != null) CopyNativeImage(donor, image);
            else image.color = new Color(0.19f, 0.18f, 0.16f, 1f);
            image.raycastTarget = false;
            DisableOldBorder(target);
        }

        internal static void StyleNativeButton(Button target, bool adventureRow, bool selected = false)
        {
            Button donor = adventureRow ? _nativeAdventureButton : _nativeActionButton;
            Image image = target.GetComponent<Image>();
            if (image == null) image = target.gameObject.AddComponent<Image>();
            Image donorImage = donor == null ? null : donor.targetGraphic as Image;
            if (donorImage != null) CopyNativeImage(donorImage, image);
            else image.color = new Color(0.27f, 0.25f, 0.22f, 1f);
            image.raycastTarget = true;
            target.targetGraphic = image;
            if (donor != null)
            {
                // The original animation controller and click handlers remain on the native menu.
                target.colors = donor.colors;
                target.spriteState = donor.spriteState;
                target.transition = donor.transition == Selectable.Transition.Animation
                    ? Selectable.Transition.ColorTint : donor.transition;
                if (!adventureRow && selected && donor.spriteState.highlightedSprite != null)
                    image.sprite = donor.spriteState.highlightedSprite;
            }
            DisableOldBorder(target.gameObject);
            Transform previousMarker = target.transform.Find("NativeSelection");
            if (previousMarker != null) previousMarker.gameObject.SetActive(adventureRow && selected);
            if (adventureRow && selected && _nativeAdventureSelector != null)
            {
                GameObject marker = previousMarker == null ? NewChild("NativeSelection", target.transform) : previousMarker.gameObject;
                marker.transform.SetAsFirstSibling();
                Image markerImage = marker.GetComponent<Image>();
                if (markerImage == null) markerImage = marker.AddComponent<Image>();
                CopyNativeImage(_nativeAdventureSelector, markerImage);
                Stretch(marker.GetComponent<RectTransform>());
            }
            Text caption = target.GetComponentInChildren<Text>();
            if (caption != null)
            {
                Text nativeCaption = adventureRow && _nativeMenu != null && _nativeMenu.m_GameDefButtonPrefab != null
                    ? _nativeMenu.m_GameDefButtonPrefab.m_GameDefNameText
                    : donor == null ? null : donor.GetComponentInChildren<Text>(true);
                ApplyNativeText(caption, nativeCaption, true);
                caption.alignment = TextAnchor.MiddleCenter;
            }
        }

        internal static void StyleNativeText(Text target, bool heading, bool onDark = true)
        {
            ApplyNativeText(target, heading ? _nativeHeadingText : _nativeBodyText, onDark);
        }

        private static void ApplyNativeText(Text target, Text donor, bool onDark)
        {
            if (donor != null)
            {
                target.font = donor.font;
                target.fontStyle = donor.fontStyle;
                target.lineSpacing = donor.lineSpacing;
                target.color = donor.color;
            }
            target.color = onDark
                ? donor != null && donor.color.grayscale > 0.5f ? donor.color : new Color(0.97f, 0.957f, 0.942f, 1f)
                : _nativeBodyText != null ? _nativeBodyText.color : Color.black;
            foreach (Shadow old in target.GetComponents<Shadow>()) old.enabled = false;
            if (onDark && donor != null)
            {
                foreach (Shadow source in donor.GetComponents<Shadow>())
                {
                    if (!source.enabled) continue;
                    Shadow effect = null;
                    foreach (Shadow candidate in target.GetComponents<Shadow>())
                        if (candidate.GetType() == source.GetType() && !candidate.enabled) { effect = candidate; break; }
                    if (effect == null)
                        effect = source is Outline ? (Shadow)target.gameObject.AddComponent<Outline>() : target.gameObject.AddComponent<Shadow>();
                    effect.enabled = true;
                    effect.effectColor = source.effectColor;
                    effect.effectDistance = source.effectDistance;
                    effect.useGraphicAlpha = source.useGraphicAlpha;
                }
            }
            target.raycastTarget = false;
        }

        private static void CopyNativeImage(Image source, Image target)
        {
            target.sprite = source.sprite;
            target.type = source.type;
            target.color = source.color;
            target.material = source.material;
            target.preserveAspect = source.preserveAspect;
            target.fillCenter = source.fillCenter;
            target.fillMethod = source.fillMethod;
            target.fillAmount = source.fillAmount;
            target.fillClockwise = source.fillClockwise;
            target.fillOrigin = source.fillOrigin;
            target.raycastTarget = false;
        }

        private static void DisableOldBorder(GameObject target)
        {
            foreach (Outline outline in target.GetComponents<Outline>()) outline.enabled = false;
        }

        private static void CaptureNativePanels()
        {
            // Paths verified against the installed title-scene serialized hierarchy.
            _nativePanelImage = NativeImageAt("Background");
            _nativePreviewImage = NativeImageAt("imageBackground");
            _nativePlaqueImage = NativeImageAt("Background/menuHeader");
            _nativeSmallPlaqueImage = NativeImageAt("Background/gameDefHeader");
            _nativePaperImage = NativeImageAt("imageBackground/adventureImage/GameDetails");
            _nativePaperTexture = NativeImageAt("imageBackground/adventureImage/GameDetails/Image");
        }

        private static Image NativeImageAt(string path)
        {
            Transform node = _nativeMenu.transform.Find(path);
            return node == null ? null : node.GetComponent<Image>();
        }
    }
}
