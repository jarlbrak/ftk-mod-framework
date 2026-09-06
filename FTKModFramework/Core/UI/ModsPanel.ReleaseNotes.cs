using System;
using System.Collections.Generic;
using FTKModFramework.Core.Marketplace;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;

namespace FTKModFramework.Core.UI
{
    internal sealed partial class ModsPanel
    {
        private bool _expandedNotes;
        private string _notesIdentity;
        private float _notesPosition = 1f;
        private ScrollRect _notesScroll;

        private void ReleaseNotes(FrameworkRelease release, float height)
        {
            string identity = release.ReleaseId + ":" + release.Tag;
            if (_notesIdentity != identity) { _notesIdentity = identity; _notesPosition = 1f; }
            Transform parent = _container;
            GameObject area = NewChild("Release notes", parent);
            Height(area, height);
            area.AddComponent<Image>().color = CardPaper;
            ScrollRect scroll = area.AddComponent<ScrollRect>();
            _notesScroll = scroll;
            scroll.horizontal = false;
            scroll.vertical = true;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 35f;
            GameObject viewport = NewChild("Notes viewport", area.transform);
            Stretch(viewport.GetComponent<RectTransform>());
            viewport.GetComponent<RectTransform>().offsetMax = new Vector2(-24, 0);
            viewport.AddComponent<Image>().color = CardPaper;
            viewport.AddComponent<Mask>().showMaskGraphic = false;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            GameObject content = NewChild("Notes content", viewport.transform);
            RectTransform rect = content.GetComponent<RectTransform>();
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1); rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = Vector2.zero;
            VerticalLayoutGroup layout = content.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 14;
            layout.padding = new RectOffset(4, 12, 8, 12);
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            content.AddComponent<ContentSizeFitter>().verticalFit = ContentSizeFitter.FitMode.PreferredSize;
            scroll.content = rect;
            _container = content.transform;
            foreach (ReleaseNoteBlock block in ReleaseNotesMarkdown.Parse(release.Notes))
            {
                GameObject go = NewChild("Markdown", content.transform);
                Text text = go.AddComponent<Text>();
                int size = block.Heading == 1 ? 28 : block.Heading > 0 ? 25 : 21;
                StyleText(text, block.Text, size);
                text.font = NotesFont(block.Heading > 0);
                text.supportRichText = true;
                text.lineSpacing = 1.12f;
                if (block.Heading > 0) text.color = Ink;
                if (block.Code) text.color = Gold;
                foreach (ReleaseNoteLink link in block.Links)
                {
                    string url = link.Url;
                    Button button = UpdateLink("Open: " + Short(link.Label, 60) + " (" + new Uri(url).Host + ")", delegate { if (ReleaseNotesMarkdown.SafeUrl(url)) Application.OpenURL(url); });
                    button.GetComponentInChildren<Text>().fontSize = 18;
                    button.gameObject.AddComponent<ReleaseNotesRevealSelection>().Scroll = scroll;
                }
            }
            _container = parent;
            GameObject rail = NewChild("Notes scrollbar", area.transform);
            RectTransform railRect = rail.GetComponent<RectTransform>();
            railRect.anchorMin = new Vector2(1, 0); railRect.anchorMax = new Vector2(1, 1);
            railRect.offsetMin = new Vector2(-14, 0); railRect.offsetMax = Vector2.zero;
            rail.AddComponent<Image>().color = WarmBorder;
            Scrollbar bar = rail.AddComponent<Scrollbar>();
            GameObject handle = NewChild("Scroll handle", rail.transform);
            Stretch(handle.GetComponent<RectTransform>());
            Image handleImage = handle.AddComponent<Image>();
            handleImage.color = Gold;
            bar.handleRect = handle.GetComponent<RectTransform>();
            bar.targetGraphic = handleImage;
            bar.direction = Scrollbar.Direction.BottomToTop;
            scroll.verticalScrollbar = bar;
            Canvas.ForceUpdateCanvases();
            LayoutRebuilder.ForceRebuildLayoutImmediate(rect);
            scroll.verticalNormalizedPosition = _notesPosition;
            scroll.onValueChanged.AddListener(delegate(Vector2 position) { _notesPosition = Mathf.Clamp01(position.y); });
            Transform row = HorizontalRow("Notes controls", 32);
            _container = row;
            SetWidth(UpdateLink("Scroll up", delegate { ScrollNotes(1); }).gameObject, 120);
            SetWidth(UpdateLink("Scroll down", delegate { ScrollNotes(-1); }).gameObject, 130);
            SetWidth(UpdateLink(_expandedNotes ? "Collapse notes" : "Expand notes", delegate { _expandedNotes = !_expandedNotes; Refresh(); }).gameObject, 180);
            _container = parent;
        }
        private static Font _notesBodyFont;
        private static Font _notesHeadingFont;
        private static Font NotesFont(bool heading)
        {
            Font cached = heading ? _notesHeadingFont : _notesBodyFont;
            if (cached != null) return cached;
            // Use an OS family with style faces for inline rich text. Both built-in Arial
            // and imported game Roboto produced compressed bold metrics on macOS.
            if (!heading)
            {
                try
                {
                    _notesBodyFont = Font.CreateDynamicFontFromOSFont(new string[] { "Helvetica", "Arial", "Liberation Sans", "DejaVu Sans", "Noto Sans" }, 21);
                    if (_notesBodyFont != null) return _notesBodyFont;
                }
                catch (Exception error) { Plugin.Log.LogWarning("Release notes font fallback: " + error.Message); }
            }
            string name = heading ? "Merriweather-Bold" : "Roboto-Regular";
            foreach (Font font in Resources.FindObjectsOfTypeAll<Font>())
                if (font.name == name)
                {
                    if (heading) _notesHeadingFont = font; else _notesBodyFont = font;
                    return font;
                }
            return heading ? HeadingFont() : Resources.GetBuiltinResource<Font>("Arial.ttf");
        }
        private object NotesScrollMetrics()
        {
            if (_notesScroll == null || !_notesScroll.gameObject.activeInHierarchy) return null;
            return new Dictionary<string, object> { { "position", _notesScroll.verticalNormalizedPosition },
                { "contentHeight", _notesScroll.content.rect.height }, { "viewportHeight", _notesScroll.viewport.rect.height },
                { "expanded", _expandedNotes } };
        }
        private void ScrollNotes(int direction)
        {
            if (_notesScroll == null) return;
            float overflow = _notesScroll.content.rect.height - _notesScroll.viewport.rect.height;
            if (overflow > 0) _notesScroll.verticalNormalizedPosition = Mathf.Clamp01(_notesScroll.verticalNormalizedPosition + direction * 140f / overflow);
        }
    }
    // Unity's selection event also fires through the game's existing FTKSelectable adapter.
    // Keep controller/keyboard focus visible when a note contains links below the fold.
    internal sealed class ReleaseNotesRevealSelection : MonoBehaviour, ISelectHandler
    {
        internal ScrollRect Scroll;
        public void OnSelect(BaseEventData eventData)
        {
            if (Scroll == null) return;
            Canvas.ForceUpdateCanvases();
            RectTransform target = GetComponent<RectTransform>();
            Vector3[] corners = new Vector3[4];
            target.GetWorldCorners(corners);
            float bottom = Scroll.viewport.InverseTransformPoint(corners[0]).y;
            float top = Scroll.viewport.InverseTransformPoint(corners[1]).y;
            Rect bounds = Scroll.viewport.rect;
            float delta = top > bounds.yMax ? top - bounds.yMax : bottom < bounds.yMin ? bottom - bounds.yMin : 0;
            float overflow = Scroll.content.rect.height - bounds.height;
            if (overflow > 0 && delta != 0) Scroll.verticalNormalizedPosition = Mathf.Clamp01(Scroll.verticalNormalizedPosition + delta / overflow);
        }
    }
}
