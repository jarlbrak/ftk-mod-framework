using System;
using System.Collections.Generic;
using System.Text;
using FTKModFramework.Core.Marketplace;
using FTKModFramework.Core.Data;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core.UI
{
    internal sealed partial class ModsPanel
    {
        // Match the native adventure selector's proportions. The shared canvas scales the
        // whole composition, keeping its banner and parchment cells aligned at any resolution.
        private void BuildNativeBrowse()
        {
            GameObject left = BrowseBox("Mod selection", _rootContent, 0, 0, 500, 900, false);
            GameObject right = BrowseBox("Mod page", _rootContent, 524, 0, 1056, 900, false);
            StyleNativePanel(right, false, true);
            BrowsePlaque(left.transform, "Mods", 250);

            _container = BrowseStack(left.transform, "Sources", 16, 38, 468, 46);
            Transform tabs = HorizontalRow("Sources", 44);
            _container = tabs;
            BrowseTab("Browse", "discover", 144);
            BrowseTab("Installed", "installed", 156);
            BrowseTab("Updates", "updates", 144);

            _container = BrowseStack(left.transform, "Choose a mod", 16, 104, 468, 378);
            if (_view == "discover") Discover(); else Installed(_view == "components");

            GameObject options = BrowseBox("Mod options", left.transform, 12, 510, 476, 274, false);
            StyleNativePanel(options, false, true);
            BrowsePlaque(options.transform, _view == "discover" ? "Browse Options" : "Mod Options", 250);
            _container = BrowseStack(options.transform, "Actions", 16, 32, 444, 240);
            if (_view == "discover")
            {
                Transform actions = _container;
                _container = HorizontalRow("Filters", 40);
                Button search = ActionButton(_search.Length == 0 ? "Search mods" : "Search: " + Short(_search, 12), delegate { Navigate("search"); }, true, 40);
                SetWidth(search.gameObject, 216);
                Button category = ActionButton("Category: " + Categories[_category], delegate {
                    _category = (_category + 1) % Categories.Length;
                    _page = 0; _entry = null; _package = null; Refresh();
                }, true, 40);
                SetWidth(category.gameObject, 216);
                _container = actions;
                ActionButton("Refresh catalog", LoadCatalog, !PanelBusy, 40);
            }
            else if (_package == null && _entry != null)
            {
                TextLine(_entry.PendingEnabled.HasValue ? "After restart: " + OnOff(_entry.PendingEnabled.Value) : "This launch: " + OnOff(_entry.Enabled), 22, 34);
                bool pending = _entry.PendingEnabled.HasValue;
                PrimaryButton(pending ? "Undo change" : _entry.Enabled ? "Turn off after restart" : "Turn on after restart", delegate {
                    ModRegistry.SetEnabled(_entry.Key, pending ? _entry.Enabled : !_entry.Enabled);
                    Refresh();
                }, _entry.FrameworkCompatible);
            }
            if (_package != null) PackageActions(_package);

            _container = BrowseStack(left.transform, "Menu controls", 16, 799, 468, 89);
            Transform utility = HorizontalRow("Utilities", 37);
            _container = utility;
            const float utilityWidth = 148;
            Button settings = ActionButton("Settings", delegate { Navigate("settings"); }, true, 37);
            SetWidth(settings.gameObject, utilityWidth);
            Button extra = ActionButton("Components", delegate { Navigate("components"); }, true, 37);
            SetWidth(extra.gameObject, utilityWidth);
            Button status = ActionButton(PanelBusy ? "Cancel" : PendingCount() > 0 ? "Changes" : "Next launch", delegate {
                if (PanelBusy)
                {
                    if (FrameworkUpdateRuntime.Busy) FrameworkUpdateRuntime.Cancel(); else MarketplaceRuntime.CancelRunning();
                    Refresh();
                }
                else Navigate("maintenance");
            }, true, 37);
            SetWidth(status.gameObject, utilityWidth);
            status.interactable = !HotReload.HotReloadCoordinator.Busy;
            foreach (Text caption in utility.GetComponentsInChildren<Text>()) caption.fontSize = 20;
            _container = utility.parent;
            _container = HorizontalRow("Back and management", 37);
            m_ButtonOnCancel = ActionButton(_navigation.CanGoBack ? "Back" : "Back to title", GoBack, true, 37);
            bool managed = _package != null && MarketplaceRuntime.FindManaged(_package.ModGuid) != null;
            SetWidth(m_ButtonOnCancel.gameObject, managed ? 228 : 468);
            if (managed)
            {
                Button remove = ActionButton("Remove mod...", delegate { ReviewSelection(_package, true, false); }, !PanelBusy, 37);
                SetWidth(remove.gameObject, 228);
            }

            if (_package != null || _entry != null) BrowseModPage(right.transform);
            else
            {
                BrowsePlaque(right.transform, "Choose a Mod", 340);
                _container = BrowseStack(right.transform, "Empty selection", 40, 200, 976, 400);
                EmptyDetails();
            }
            if (PanelBusy || !string.IsNullOrEmpty(_message))
            {
                _container = BrowseStack(_rootContent, "Operation status", 524, 910, 1056, 46);
                TextLine(PanelBusy ? "Working..." : Short(_message, 150), 20, 44);
            }
            _container = _rootContent;
        }

        private void BrowseTab(string title, string view, float width)
        {
            Button button = ActionButton(title, delegate {
                Navigate(view);
                if (view == "discover" && MarketplaceRuntime.Catalog == null && !PanelBusy) LoadCatalog();
                if (view == "updates" && FrameworkUpdateRuntime.State == null && !PanelBusy) LoadUpdates("refresh");
            }, true, 42);
            SetWidth(button.gameObject, width);
            StyleNativeButton(button, false, _view == view);
        }

        private void BrowseModPage(Transform parent)
        {
            if (_package != null) _package = CurrentListing(_package);
            string title = _package == null ? _entry.DisplayName : _package.Name;
            List<string> previews = PreviewPaths(_package);
            GameObject banner = NewChild("Mod banner", parent);
            BrowseRect(banner, 12, 12, 1032, 580);
            if (previews.Count > 0)
            {
                _imagePage = Math.Min(_imagePage, previews.Count - 1);
                AddPreviewImage(banner.transform, previews[_imagePage]);
            }
            else
            {
                Text placeholder = NewChild("No artwork", banner.transform).AddComponent<Text>();
                StyleText(placeholder, title, 48);
                placeholder.alignment = TextAnchor.MiddleCenter;
                Stretch(placeholder.rectTransform);
            }
            BrowsePlaque(parent, title, Mathf.Min(850, Math.Max(260, title.Length * 23)));

            string description = _package == null ? _entry.Description : _package.Description;
            StringBuilder about = new StringBuilder(description ?? "");
            if (_package != null && _package.ContentChanges != null && _package.ContentChanges.Length > 0) about.Append("\n");
            if (_package != null && _package.ContentChanges != null)
                foreach (string change in _package.ContentChanges)
                    if (!string.IsNullOrEmpty(change)) about.Append("\n• ").Append(change);
            BrowseParchment(parent, "About", about.ToString(), 12, 606, 510, 282);

            StringBuilder metadata = new StringBuilder();
            metadata.Append("By ").Append(Declared(_package == null ? _entry.Author : _package.Author));
            metadata.Append("\nVersion: ").Append(Declared(_package == null ? _entry.Version : _package.Version));
            metadata.Append("\nFramework: ").Append(Declared(_package == null ? _entry.FrameworkVersion : _package.FrameworkVersion));
            if (_package != null)
            {
                if (!string.IsNullOrEmpty(_package.License)) metadata.Append("\nLicense: ").Append(_package.License);
                if (_package.Platforms != null && _package.Platforms.Length > 0) metadata.Append("\nPlatforms: ").Append(string.Join(", ", _package.Platforms));
                string reason = CompatibilityText(_package);
                if (!string.IsNullOrEmpty(reason)) metadata.Append("\n\n").Append(reason);
                if (_package.Requirements != null)
                    foreach (string requirement in _package.Requirements) if (!string.IsNullOrEmpty(requirement)) metadata.Append("\n\n").Append(requirement);
                if (_package.Dependencies != null && _package.Dependencies.Length > 0)
                {
                    metadata.Append("\n\nRequires:");
                    foreach (PackageSelection dependency in _package.Dependencies) metadata.Append("\n").Append(DependencyName(dependency)).Append(" v").Append(dependency.Version);
                }
                if (!string.IsNullOrEmpty(_package.Changelog)) metadata.Append("\n\nLatest change: ").Append(_package.Changelog);
            }
            else if (!_entry.FrameworkCompatible) metadata.Append("\n\n").Append(_entry.CompatibilityReason);
            BrowseParchment(parent, "Mod Details", metadata.ToString(), 534, 606, 510, 282);

            // These actions are intentionally separate from selecting a mod. A selection only
            // changes the page; installation and toggles still use the existing reviewed plan.
            _container = BrowseStack(parent, "Preview controls", 16, 544, 1024, 42);
            Transform controls = HorizontalRow("Preview controls", 38);
            _container = controls;
            if (previews.Count > 1)
            {
                Button next = ActionButton("Next image (" + (_imagePage + 1) + "/" + previews.Count + ")", delegate { _imagePage = (_imagePage + 1) % previews.Count; Refresh(); }, true, 36);
                SetWidth(next.gameObject, 265);
            }
        }

        private void BrowseParchment(Transform parent, string title, string value, float x, float y, float width, float height)
        {
            GameObject panel = BrowseBox(title, parent, x, y, width, height, true);
            GameObject viewport = NewChild("Text viewport", panel.transform);
            BrowseRect(viewport, 15, 28, width - 30, height - 44);
            viewport.AddComponent<RectMask2D>();
            ScrollRect scroll = viewport.AddComponent<ScrollRect>();
            scroll.horizontal = false;
            scroll.movementType = ScrollRect.MovementType.Clamped;
            scroll.scrollSensitivity = 32;
            GameObject content = NewChild("Metadata", viewport.transform);
            RectTransform rect = content.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.sizeDelta = new Vector2(width - 30, height - 44);
            Text text = content.AddComponent<Text>();
            StyleText(text, value, 22);
            StyleNativeText(text, false, false);
            text.color = new Color(0.13f, 0.12f, 0.10f, 1);
            text.raycastTarget = true;
            float preferred = text.preferredHeight;
            rect.sizeDelta = new Vector2(width - 30, Math.Max(height - 44, preferred));
            scroll.content = rect;
            scroll.viewport = viewport.GetComponent<RectTransform>();
            if (preferred > height - 44)
            {
                // Mouse-wheel and explicit controls both reach the full author-supplied text.
                rect.sizeDelta = new Vector2(width - 30, preferred + 32);
                _container = BrowseStack(panel.transform, "Scroll controls", width - 142, height - 38, 126, 30);
                Transform row = HorizontalRow("Scroll", 30);
                _container = row;
                float pageStep = (height - 76) / Math.Max(1, preferred + 32 - (height - 44));
                Button up = ActionButton("Up", delegate { scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition + pageStep); }, true, 28);
                SetWidth(up.gameObject, 57);
                Button down = ActionButton("Down", delegate { scroll.verticalNormalizedPosition = Mathf.Clamp01(scroll.verticalNormalizedPosition - pageStep); }, true, 28);
                SetWidth(down.gameObject, 57);
            }
            BrowsePlaque(panel.transform, title, 220, 31);
        }

        private static GameObject BrowseBox(string name, Transform parent, float x, float y, float width, float height, bool parchment)
        {
            GameObject box = NewChild(name, parent);
            BrowseRect(box, x, y, width, height);
            StyleNativePanel(box, parchment);
            return box;
        }

        private static Transform BrowseStack(Transform parent, string name, float x, float y, float width, float height)
        {
            GameObject box = NewChild(name, parent);
            BrowseRect(box, x, y, width, height);
            VerticalLayoutGroup layout = box.AddComponent<VerticalLayoutGroup>();
            layout.spacing = 6;
            layout.childControlHeight = layout.childControlWidth = true;
            layout.childForceExpandHeight = false;
            layout.childForceExpandWidth = true;
            return box.transform;
        }

        private static void BrowseRect(GameObject go, float x, float y, float width, float height)
        {
            RectTransform rect = go.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0, 1);
            rect.anchoredPosition = new Vector2(x, -y);
            rect.sizeDelta = new Vector2(width, height);
        }

        private static void BrowsePlaque(Transform parent, string title, float width, float height = 44)
        {
            GameObject plaque = NewChild("Title plaque", parent);
            RectTransform rect = plaque.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(0.5f, 1);
            rect.pivot = new Vector2(0.5f, 0.5f);
            rect.sizeDelta = new Vector2(width, height);
            StyleNativePlaque(plaque, height <= 35);
            Text text = NewChild("Title", plaque.transform).AddComponent<Text>();
            StyleText(text, title, height > 35 ? 34 : 26);
            StyleNativeText(text, true);
            text.alignment = TextAnchor.MiddleCenter;
            text.resizeTextForBestFit = true;
            text.resizeTextMinSize = 20;
            text.resizeTextMaxSize = text.fontSize;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(18, 0);
            text.rectTransform.offsetMax = new Vector2(-18, 0);
        }
    }
}
