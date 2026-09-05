using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using UnityEngine;
using UnityEngine.UI;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Marketplace;

namespace FTKModFramework.Core.UI
{
    /// <summary>Title-screen marketplace. Native helper work is polled without blocking Unity;
    /// every action is a stock Button with the game's FTKSelectable controller adapter.</summary>
    internal sealed class ModsPanel : uiScreen
    {
        private static ModsPanel _instance;
        private Transform _container;
        private Transform _rootContent;
        private string _view = "installed";
        private string _search = "";
        private string _message = "";
        private int _page;
        private int _detailPage;
        private int _category;
        private int _letter;
        private int _focusIndex = 1;
        private readonly List<FTKSelectable> _controls = new List<FTKSelectable>();
        private PackageDescriptor _package;
        private ModEntry _entry;
        private List<PackageSelection> _planSelection;
        private MarketplaceResult _plan;
        private string _confirmOperation;
        private string _planIntent;
        private bool _wasBusy;
        private bool _refreshPending;
        private int _renderCount;
        private readonly List<Texture2D> _previewTextures = new List<Texture2D>();
        private bool _showGallery;
        private int _imagePage;
        private bool _showAdvanced;
        private static Font _serif;
        private static readonly Color Paper = new Color(0.94f, 0.925f, 0.88f, 1f);
        private static readonly Color CardPaper = new Color(0.985f, 0.975f, 0.945f, 1f);
        private static readonly Color Ink = new Color(0.17f, 0.18f, 0.16f, 1f);
        private static readonly Color MutedInk = new Color(0.34f, 0.38f, 0.36f, 1f);
        private static readonly Color Gold = new Color(0.48f, 0.32f, 0.07f, 1f);
        private static readonly Color WarmBorder = new Color(0.78f, 0.75f, 0.68f, 1f);
        private const string Alphabet = " abcdefghijklmnopqrstuvwxyz0123456789-";
        private static readonly string[] Categories = { "All", "items", "weapons", "proficiencies", "classes", "enemies", "encounters" };

        // Explicit, environment-gated smoke seam. Only visible framework-owned buttons are invokable.
        internal static object TestBridge(IDictionary<string, object> args)
        {
            if (Environment.GetEnvironmentVariable("FTK_AGENT_BRIDGE") != "1") throw new InvalidOperationException("Marketplace test bridge is disabled.");
            object raw;
            string operation = args != null && args.TryGetValue("operation", out raw) ? Convert.ToString(raw) : "inspect";
            if (operation == "open")
            {
                StartGameFE.MainScreen title = UnityEngine.Object.FindObjectOfType<StartGameFE.MainScreen>();
                if (title == null || !title.gameObject.activeInHierarchy) throw new InvalidOperationException("Marketplace test bridge requires the title screen.");
                Open();
            }
            if (_instance == null || !_instance.gameObject.activeInHierarchy) throw new InvalidOperationException("Mods panel is not visible.");
            Button[] buttons = _instance.GetComponentsInChildren<Button>(false);
            if (operation == "click")
            {
                Button target = null;
                if (args.TryGetValue("index", out raw))
                {
                    int index = Convert.ToInt32(raw);
                    if (index >= 0 && index < buttons.Length) target = buttons[index];
                }
                else if (args.TryGetValue("label", out raw))
                {
                    string label = Convert.ToString(raw);
                    foreach (Button button in buttons)
                    {
                        Text text = button.GetComponentInChildren<Text>();
                        if (text != null && text.text == label) { target = button; break; }
                    }
                }
                if (target == null || !target.interactable || !target.gameObject.activeInHierarchy) throw new InvalidOperationException("No matching visible enabled marketplace button.");
                target.onClick.Invoke();
            }
            else if (operation != "inspect" && operation != "open") throw new InvalidOperationException("Unsupported marketplace test operation.");
            List<object> visibleButtons = new List<object>();
            buttons = _instance.GetComponentsInChildren<Button>(false);
            for (int n = 0; n < buttons.Length; n++)
            {
                Text caption = buttons[n].GetComponentInChildren<Text>();
                visibleButtons.Add(new Dictionary<string, object> { { "index", n }, { "label", caption == null ? "" : caption.text }, { "enabled", buttons[n].interactable }, { "bounds", ScreenBounds(buttons[n].GetComponent<RectTransform>()) } });
            }
            List<string> texts = new List<string>();
            List<object> metrics = new List<object>();
            foreach (Text text in _instance.GetComponentsInChildren<Text>(false))
            {
                texts.Add(text.text);
                Dictionary<string, object> bounds = ScreenBounds(text.rectTransform);
                bounds["text"] = text.text;
                bounds["font"] = text.font == null ? "" : text.font.name;
                bounds["fontNames"] = text.font == null ? new string[0] : text.font.fontNames;
                // Unity owns the cached generator. Do not allocate/dispose native generators
                // here: this game's old Unity build can dispose them again on its finalizer thread.
                int renderedSize = text.resizeTextForBestFit ? text.cachedTextGenerator.fontSizeUsedForBestFit : text.fontSize;
                bounds["renderedFontSize"] = renderedSize;
                bounds["preferredHeight"] = text.preferredHeight;
                bounds["rectHeight"] = text.rectTransform.rect.height;
                bounds["clippingMeasured"] = !text.resizeTextForBestFit;
                bounds["clipped"] = !text.resizeTextForBestFit && text.preferredHeight > text.rectTransform.rect.height + 1f;
                metrics.Add(bounds);
            }
            List<string> loadedFonts = new List<string>();
            foreach (Font font in Resources.FindObjectsOfTypeAll<Font>()) loadedFonts.Add(font.name + ": " + string.Join(", ", font.fontNames));
            return new Dictionary<string, object> { { "loadedFonts", loadedFonts }, { "osFonts", Font.GetOSInstalledFontNames() }, { "renderCount", _instance._renderCount }, { "view", _instance._view }, { "texts", texts }, { "buttons", visibleButtons }, { "busy", MarketplaceRuntime.Busy },
                { "screenWidth", Screen.width }, { "screenHeight", Screen.height }, { "textMetrics", metrics } };
        }

        private static Dictionary<string, object> ScreenBounds(RectTransform rect)
        {
            Vector3[] corners = new Vector3[4];
            rect.GetWorldCorners(corners);
            Vector2 lower = RectTransformUtility.WorldToScreenPoint(null, corners[0]);
            Vector2 upper = RectTransformUtility.WorldToScreenPoint(null, corners[2]);
            return new Dictionary<string, object> { { "x", lower.x }, { "y", Screen.height - upper.y }, { "width", upper.x - lower.x }, { "height", upper.y - lower.y } };
        }

        public static void Open()
        {
            if (_instance == null) _instance = Build();
            else _instance.Render();
            FTKInput.Instance.SetFocus(_instance, null, true, null, false);
            _instance.UpdateSelectables();
            _instance.SetupNavigation();
            if (_instance._controls.Count > 0) FTKInput.SetSelected(_instance._controls[Math.Min(_instance._focusIndex, _instance._controls.Count - 1)]);
        }

        private static ModsPanel Build()
        {
            GameObject root = null;
            try
            {
                root = new GameObject("FtkmfModsPanel");
                root.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(root);
                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000;
                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);
                root.AddComponent<GraphicRaycaster>();
                Image background = root.AddComponent<Image>();
                background.color = new Color(0.035f, 0.03f, 0.02f, 0.72f);
                Stretch(background.rectTransform);
                ModsPanel panel = root.AddComponent<ModsPanel>();
                GameObject container = NewChild("Content", root.transform);
                RectTransform rect = container.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(1580f, 1010f);
                container.AddComponent<Image>().color = Paper;
                Border(container, WarmBorder, 1);
                VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 10f;
                layout.padding = new RectOffset(36, 36, 24, 20);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                panel._container = panel._rootContent = container.transform;
                panel.m_SelectableParent = container.transform;
                panel.m_NavigationSetup = FTKInputFocus.NavigationSetup.Vertical;
                // Polling lives on a separate component so uiScreen's own Update/focus logic is preserved.
                root.AddComponent<MarketplacePanelTick>().Panel = panel;
                panel.Render();
                return panel;
            }
            catch
            {
                if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
                throw;
            }
        }

        internal void ReleasePreviews()
        {
            foreach (Texture2D texture in _previewTextures) UnityEngine.Object.Destroy(texture);
            _previewTextures.Clear();
        }

        internal void Tick()
        {
            MarketplaceRuntime.Poll();
            bool busy = MarketplaceRuntime.Busy;
            if (_wasBusy != busy) { _wasBusy = busy; Refresh(); }
            if (_view == "search" && Input.inputString.Length > 0)
            {
                foreach (char c in Input.inputString)
                {
                    if (c == '\b' && _search.Length > 0) _search = _search.Substring(0, _search.Length - 1);
                    else if (c >= ' ' && c <= '~' && _search.Length < 40) _search += c;
                }
                Refresh();
            }
        }

        private void Navigate(string view)
        {
            _view = view;
            _page = 0;
            _detailPage = 0;
            _showAdvanced = false; _showGallery = false; _imagePage = 0;
            _focusIndex = view == "installed" ? 1 : 0;
            _message = "";
            if (view == "installed" || view == "discover" || view == "components") { _entry = null; _package = null; }
            Refresh();
        }

        // Click and helper callbacks only request a render. FTKInputFocus continues using its
        // selected controls after OnControllerClick returns, so rebuilding inside that callback
        // can invalidate the controls still in use. Coalesce all requests at the end of the frame.
        private void Refresh() { _refreshPending = true; }

        internal void RenderPending()
        {
            if (!_refreshPending || !gameObject.activeInHierarchy) return;
            try { Render(); }
            catch (Exception e)
            {
                Plugin.Log.LogError("Mods panel render failed in " + _view + ": " + e);
                _refreshPending = false;
                // Release the modal on a UI failure so the player can reopen it from the title.
                try { FTKInput.Instance.Close(this); }
                catch (Exception closeError) { Plugin.Log.LogWarning("Mods panel focus cleanup: " + closeError.Message); }
                finally { gameObject.SetActive(false); }
            }
        }

        private void Render()
        {
            _refreshPending = false;
            try { RenderContent(); }
            finally { _container = _rootContent; }
        }

        private void RenderContent()
        {
            if (_rootContent == null) return;
            _renderCount++;
            _container = _rootContent;
            for (int i = _rootContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = _rootContent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            _controls.Clear();
            ReleasePreviews();
            TextLine("For The King / Mods", 38, 58).color = Gold;
            Rule();
            AddTabs();
            if (_view == "installed" || _view == "components" || _view == "discover")
            {
                Transform left;
                Transform right;
                CreateColumns(out left, out right);
                _container = left;
                if (_view == "discover") Discover(); else Installed(_view == "components");
                _container = right;
                if (_package != null || _entry != null) Details();
                else
                {
                    EmptyDetails();
                }
                _container = _rootContent;
            }
            else if (_view == "details") Details();
            else if (_view == "confirm") Confirmation();
            else if (_view == "search") Search();
            else if (_view == "maintenance") Maintenance();
            else if (_view == "tools") Tools();
            AddFooter();
            if (gameObject.activeInHierarchy)
            {
                UpdateSelectables();
                SetupNavigation();
                if (_controls.Count > 0) FTKInput.SetSelected(_controls[Math.Min(_focusIndex, _controls.Count - 1)]);
            }
        }

        private void Installed(bool components)
        {
            TextLine(components ? "Required components" : "Your installed mods", 25, 42);
            TextLine(components ? "Supporting content used by your community mods." : "Choose a mod to see what it adds.", 23, 42);
            if (MarketplaceRuntime.RegistrationNotice != null)
                TextLine("Some content did not load correctly. Open Tools for the error and recovery options.", 22, 76).color = Gold;
            List<ModEntry> entries = new List<ModEntry>();
            foreach (ModEntry candidate in ModRegistry.Entries)
            {
                PackageDescriptor managed = candidate.IsManaged ? MarketplaceRuntime.FindManaged(candidate.Key) : null;
                bool component = managed != null && (managed.Classification == "dependency" || managed.Classification == "component");
                if (component == components) entries.Add(candidate);
            }
            ManagedSnapshot pending = MarketplaceRuntime.Pending;
            if (pending != null) foreach (PackageDescriptor package in pending.Packages)
            {
                if (MarketplaceRuntime.FindManaged(package.ModGuid) != null) continue;
                bool component = package.Classification == "dependency" || package.Classification == "component";
                if (component != components) continue;
                ModEntry queued = new ModEntry(package.ModGuid, package.Name, false, package.Version, false, package.Description, package.Author);
                queued.MarkManaged(package.PackageId);
                entries.Add(queued);
            }
            if (entries.Count == 0) TextLine(components ? "No extra components are needed." : "No mods installed yet.", 30, 90);
            if ((_entry == null && _package == null) && entries.Count > 0) SelectEntry(entries[0], false);
            int perPage = MarketplaceRuntime.RegistrationNotice == null ? 3 : 2;
            int pages = Math.Max(1, (entries.Count + perPage - 1) / perPage);
            _page = Math.Min(_page, pages - 1);
            for (int i = _page * perPage; i < Math.Min(entries.Count, (_page + 1) * perPage); i++)
            {
                ModEntry entry = entries[i];
                string origin = entry.IsBundledDemo ? "INCLUDED" : entry.IsManaged ? "COMMUNITY" : "INSTALLED MANUALLY";
                string summary = entry.IsBundledDemo ? "New classes, enemies, equipment and two adventures." : Short(entry.Description, 110);
                Card(entry.DisplayName, summary, origin, EntryState(entry),
                    _entry != null && _entry.Key == entry.Key, delegate { SelectEntry(entry, true); }, PreviewPaths(entry.IsManaged ? CurrentListing(MarketplaceRuntime.FindManaged(entry.Key) ?? DesiredPackage(entry.PackageId)) : null, entry.IsBundledDemo));
            }
            PageButtons(pages);
        }

        private void SelectEntry(ModEntry entry, bool refresh)
        {
            _entry = entry;
            PackageDescriptor active = entry.IsManaged ? MarketplaceRuntime.FindManaged(entry.Key) : null;
            _package = entry.IsManaged ? CurrentListing(active ?? DesiredPackage(entry.PackageId)) : null;
            _detailPage = 0;
            _showAdvanced = false; _showGallery = false; _imagePage = 0;
            if (refresh) Refresh();
        }

        private static PackageDescriptor DesiredPackage(string id)
        {
            ManagedSnapshot selection = MarketplaceRuntime.Pending ?? MarketplaceRuntime.Active;
            if (selection != null) foreach (PackageDescriptor package in selection.Packages)
                if (package.PackageId == id) return package;
            return null;
        }

        private static string EntryState(ModEntry entry)
        {
            if (!entry.IsManaged)
                return entry.PendingEnabled.HasValue ? "NOW: " + OnOff(entry.Enabled) + " / NEXT LAUNCH: " + OnOff(entry.PendingEnabled.Value) : OnOff(entry.Enabled);
            PackageDescriptor active = MarketplaceRuntime.FindManaged(entry.Key);
            PackageDescriptor desired = DesiredPackage(entry.PackageId);
            if (active == null) return "READY FOR NEXT LAUNCH";
            if (MarketplaceRuntime.Pending == null) return OnOff(active.Enabled);
            if (desired == null) return "REMOVES AFTER RESTART";
            if (desired.Version != active.Version) return "UPDATE AFTER RESTART";
            return desired.Enabled != active.Enabled ? "NOW: " + OnOff(active.Enabled) + " / NEXT: " + OnOff(desired.Enabled) : OnOff(active.Enabled);
        }

        private static string OnOff(bool enabled) { return enabled ? "ON" : "OFF"; }

        private void Discover()
        {
            TextLine("Find your next adventure", 25, 42);
            ActionButton(_search.Length == 0 ? "Search mods" : "Search: " + _search, delegate { Navigate("search"); }, true, 52);
            Transform browse = _container;
            string[] categoryLabels = { "All", "Items", "Weapons", "Abilities", "Classes", "Enemies", "Encounters" };
            for (int row = 0; row < 2; row++)
            {
                _container = browse;
                _container = HorizontalRow("Categories", 38);
                for (int n = row * 4; n < Math.Min(categoryLabels.Length, (row + 1) * 4); n++)
                {
                    int choice = n;
                    Button chip = ActionButton(categoryLabels[n], delegate { _category = choice; _page = 0; _entry = null; _package = null; Refresh(); }, true, 36);
                    SetWidth(chip.gameObject, n == 6 ? 190 : 170);
                    chip.GetComponentInChildren<Text>().fontSize = 20;
                    if (_category == n) Border(chip.gameObject, Gold, 2);
                }
                if (row == 1)
                {
                    Button refresh = ActionButton("Refresh", LoadCatalog, !MarketplaceRuntime.Busy, 36);
                    SetWidth(refresh.gameObject, 170);
                    refresh.GetComponentInChildren<Text>().fontSize = 20;
                }
            }
            _container = browse;
            MarketplaceResult catalog = MarketplaceRuntime.Catalog;
            if (catalog == null || catalog.Status == "unavailable")
            {
                Spacer(24);
                TextLine(MarketplaceRuntime.Busy ? "Opening the community catalog..." : "The catalog is unavailable right now", 30, 86);
                TextLine(MarketplaceRuntime.Busy ? "Your installed mods stay available while we check." : "Try Refresh when you are online. You can keep playing with everything in Installed.", 24, 104);
                return;
            }
            TextLine(catalog.Status == "offline" ? "Offline / saved catalog from " + FriendlyAge(catalog.CatalogAgeSeconds) + " ago" : "Free mods, reviewed before publication", 20, 36);
            List<PackageDescriptor> results = new List<PackageDescriptor>();
            if (catalog.Packages != null) foreach (PackageDescriptor package in LatestListings(catalog.Packages))
            {
                if (package.Revoked || package.Classification == "dependency" || package.Classification == "component" || package.Classification == "development" || package.ModGuid == Plugin.Guid) continue;
                string text = (package.Name ?? "") + " " + (package.Description ?? "") + " " + (package.Author ?? "");
                if (_search.Length > 0 && text.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (_category != 0 && !string.Equals(package.Category, Categories[_category], StringComparison.OrdinalIgnoreCase)) continue;
                results.Add(package);
            }
            if (results.Count == 0)
            {
                Spacer(28);
                bool filtered = _search.Length > 0 || _category != 0;
                TextLine(filtered ? "No matching mods" : "More adventures are on the way", 32, 84);
                TextLine(filtered ? "Try a different search or category." : "Community packages will appear here once published. Your included Adventure Pack is ready in Installed.", 24, 114);
                if (filtered) ActionButton("Clear filters", delegate { _search = ""; _category = 0; _page = 0; _entry = null; _package = null; Refresh(); });
            }
            int pages = Math.Max(1, (results.Count + 1) / 2);
            _page = Math.Min(_page, pages - 1);
            for (int i = _page * 2; i < Math.Min(results.Count, (_page + 1) * 2); i++)
            {
                PackageDescriptor package = results[i];
                PackageDescriptor active = MarketplaceRuntime.FindManaged(package.ModGuid);
                PackageDescriptor desired = DesiredPackage(package.PackageId);
                string state = active == null && desired != null ? "READY FOR NEXT LAUNCH" : active != null ? "INSTALLED" : package.Compatible ? "AVAILABLE" : "CHECK REQUIREMENTS";
                Card(package.Name, Short(package.Description, 100), Declared(package.Category).ToUpperInvariant(), state,
                    _package != null && _package.PackageId == package.PackageId,
                    delegate { _package = package; _entry = null; _detailPage = 0; _showAdvanced = false; _showGallery = false; _imagePage = 0; Refresh(); }, PreviewPaths(package, false));
            }
            PageButtons(pages);
        }

        private void LoadCatalog()
        {
            _package = null; _entry = null; _detailPage = 0; _showAdvanced = false; _showGallery = false; _imagePage = 0;
            MarketplaceRuntime.Start("catalog", null, false, delegate(MarketplaceResult result) { _message = result.Message ?? result.Status; Refresh(); });
            Refresh();
        }

        private void Details()
        {
            if (_package != null) _package = CurrentListing(_package);
            bool bundled = _entry != null && _entry.IsBundledDemo;
            string name = _package != null ? _package.Name : _entry.DisplayName;
            List<string> previews = PreviewPaths(_package, bundled);
            if (previews.Count == 0 || _showAdvanced || _showGallery) TextLine(bundled ? "INCLUDED CONTENT" : _package != null ? "COMMUNITY MOD" : "INSTALLED MANUALLY", 18, 24).color = Gold;
            Text title = TextLine(Short(name, 50), 36, bundled ? 50 : 72);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 24;
            title.resizeTextMaxSize = 36;
            if (_showGallery && previews.Count > 0)
            {
                _imagePage = Math.Min(_imagePage, previews.Count - 1);
                AddScreenshot(previews[_imagePage]);
                TextLine(bundled ? (_imagePage == 0 ? "The Hollow Mire / adventure artwork" : "Reeve Maddow / character portrait") : "Preview supplied by the mod author", 22, 48);
                if (previews.Count > 1) ActionButton("Next preview (" + (_imagePage + 1) + " of " + previews.Count + ")", delegate { _imagePage = (_imagePage + 1) % previews.Count; Refresh(); });
                LinkButton("Back to overview", delegate { _showGallery = false; Refresh(); });
                return;
            }
            if (_showAdvanced)
            {
                AdvancedDetails();
                return;
            }
            if (previews.Count > 0)
            {
                GameObject hero = NewChild("Mod preview", _container);
                Height(hero, bundled ? 220 : 175);
                AddPreviewImage(hero.transform, previews[0]);
                Button gallery = LinkButton("View " + (bundled ? "artwork" : "previews") + " (" + previews.Count + ")", delegate { _showGallery = true; _imagePage = 0; Refresh(); });
                gallery.GetComponent<LayoutElement>().minHeight = gallery.GetComponent<LayoutElement>().preferredHeight = 32;
                TextLine(bundled ? "• Play as the Thief or Innkeeper.\n• Encounter the Cutpurse and new equipment.\n• Explore Smuggler's Run and The Hollow Mire." : Short(_package != null ? _package.Description : _entry.Description, 90), 22, bundled ? 78 : 52);
            }
            else if (bundled)
            {
                TextLine("FTK Mod Framework / Included", 22, 28);
                TextLine("The playable content included with the framework, together in one optional pack.", 24, 60);
                Spacer(4);
                TextLine("What it adds", 28, 36).color = Gold;
                TextLine("• Play as the Thief or Innkeeper.\n• Encounter the Cutpurse and new equipment.\n• Explore Smuggler's Run and The Hollow Mire.", 24, 115);
            }
            else
            {
                string credit = _package != null ? "By " + Declared(_package.Author) + " / v" + Declared(_package.Version) : "By " + Declared(_entry.Author);
                TextLine(Short(credit, 60), 22, 28);
                string description = _package != null ? _package.Description : _entry.Description;
                TextLine(Short(description, 135), 24, 84);
                if (_package != null && _package.ContentChanges != null && _package.ContentChanges.Length > 0)
                {
                    TextLine("What it adds", 28, 36).color = Gold;
                    TextLine("• " + Short(_package.ContentChanges[0], 85), 22, 52);
                }
                else Spacer(24);
            }
            Rule();
            if (_package == null)
            {
                TextLine(_entry.PendingEnabled.HasValue ? "Now: " + OnOff(_entry.Enabled) + " / After restart: " + OnOff(_entry.PendingEnabled.Value) : (_entry.Enabled ? "On for this adventure" : "Currently turned off"), 22, 28);
                bool hasPending = _entry.PendingEnabled.HasValue;
                PrimaryButton(hasPending ? "Undo this change" : _entry.Enabled ? "Turn off after restart" : "Turn on after restart", delegate {
                    ModRegistry.SetEnabled(_entry.Key, hasPending ? _entry.Enabled : !_entry.Enabled);
                    _message = hasPending ? "Change undone. Your current selection will stay on next launch." : "Saved for next launch. Your current adventure stays unchanged.";
                    Refresh();
                });
            }
            else PackageActions(_package);
            LinkButton("Details and requirements", delegate { _showAdvanced = true; _detailPage = 0; Refresh(); });
        }

        private void AdvancedDetails()
        {
            List<string> blocks = new List<string>();
            string fullName = _package != null ? _package.Name : _entry.DisplayName;
            if (fullName != null && fullName.Length > 50) blocks.Add("Full mod name\n" + fullName);
            if (_package == null)
            {
                blocks.Add(Declared(_entry.Description) + "\nAuthor: " + Declared(_entry.Author) + "\n" + (_entry.IsBundledDemo ? "Included with FTK Mod Framework " + Plugin.Version + "\nLicense: MIT." : "Version: " + Declared(_entry.Version) + "\nLicense: Not declared. Compatibility: Unknown.\nInstalled manually; marketplace actions cannot remove these files."));
            }
            else
            {
                PackageDescriptor p = _package;
                blocks.Add(Declared(p.Description) + "\nAuthor: " + Declared(p.Author) + "\nVersion: " + Declared(p.Version) + " / License: " + Declared(p.License) + "\nCategory: " + Declared(p.Category));
                blocks.Add("Compatibility\n" + CompatibilityText(p) + "\nRequirements\n" + Join(p.Requirements) + "\nFramework: " + Declared(p.FrameworkRange) + "\nPlatforms: " + Join(p.Platforms));
                blocks.Add("What changes\n" + Join(p.ContentChanges) + "\nChangelog\n" + Declared(p.Changelog));
                StringBuilder dependencies = new StringBuilder("Required components\n");
                if (p.Dependencies == null || p.Dependencies.Length == 0) dependencies.Append("None declared.");
                else foreach (PackageSelection dependency in p.Dependencies) dependencies.Append(dependency.PackageId).Append(" ").Append(dependency.Version).Append("\n");
                blocks.Add(dependencies.ToString());
                blocks.Add("Source: " + Declared(p.SourceUrl) + "\nSupport: " + Declared(p.SupportUrl) + "\nScreenshots: " + Join(p.Screenshots) + "\nArtifact SHA-256: " + Declared(p.Sha256) + "\nA checksum verifies bytes, not author trust or runtime safety.");
            }
            List<string> pages = TextPages(blocks);
            List<string> screenshots = new List<string>();
            if (_package != null && _package.ScreenshotPaths != null) foreach (string path in _package.ScreenshotPaths)
                if (MarketplaceProtocol.IsScreenshotPath(MarketplaceRuntime.StateRoot, path)) screenshots.Add(path);
            int total = pages.Count + screenshots.Count;
            _detailPage = Math.Min(_detailPage, total - 1);
            if (_detailPage < pages.Count) TextLine(pages[_detailPage], 24, 280); else AddScreenshot(screenshots[_detailPage - pages.Count]);
            if (total > 1) ActionButton("Next detail / screenshot (" + (_detailPage + 1) + " of " + total + ")", delegate { _detailPage = (_detailPage + 1) % total; Refresh(); });
            if (_package != null && MarketplaceRuntime.FindManaged(_package.ModGuid) != null)
                LinkButton("Remove this community mod...", delegate { ReviewSelection(_package, true, false); }, !MarketplaceRuntime.Busy);
            LinkButton("Back to overview", delegate { _showAdvanced = false; _showGallery = false; _imagePage = 0; Refresh(); });
        }

        private void PackageActions(PackageDescriptor package)
        {
            PackageDescriptor active = MarketplaceRuntime.FindManaged(package.ModGuid);
            PackageDescriptor desired = DesiredPackage(package.PackageId);
            bool queued = MarketplaceRuntime.Pending != null && (active == null ? desired != null : desired == null || desired.Version != active.Version || desired.Enabled != active.Enabled);
            if (queued)
            {
                string state = active == null ? "Ready to install when you restart" : desired == null ? "Will be removed after restart" : desired.Version != active.Version ? "Update ready for next launch" : "Now: " + OnOff(active.Enabled) + " / After restart: " + OnOff(desired.Enabled);
                TextLine(state, 22, 44).color = Gold;
                PrimaryButton("Review next-launch changes", delegate { Navigate("maintenance"); });
                LinkButton(active == null ? "Cancel this install" : "Undo this mod's change", delegate {
                    if (active == null) ReviewSelection(package, true, false);
                    else ReviewSelection(active, false, active.Enabled);
                }, !MarketplaceRuntime.Busy);
                return;
            }
            if (package.Revoked) TextLine("This mod is no longer offered in the catalog. An installed copy stays until you choose to remove it.", 22, 76).color = Gold;
            else if (active == null && !package.Compatible) TextLine(Short(package.CompatibilityReason, 125), 22, 76).color = Gold;
            else TextLine(active != null ? (active.Enabled ? "On for this adventure" : "Currently turned off") : "Free / Changes apply after restart", 22, 38);
            if (active == null || active.Version != package.Version)
                PrimaryButton(active == null ? "Install..." : "Update to " + package.Version + "...", delegate { ReviewSelection(package, false, true); }, package.Compatible && !package.Revoked && !MarketplaceRuntime.Busy);
            else PrimaryButton(active.Enabled ? "Turn off after restart..." : "Turn on after restart...", delegate { ReviewSelection(active, false, !active.Enabled); }, !MarketplaceRuntime.Busy);
        }

        private static string Bullets(string[] values)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < Math.Min(3, values.Length); i++) text.Append("• ").Append(Short(values[i], 85)).Append("\n");
            return text.ToString();
        }

        private static string CompatibilityText(PackageDescriptor package)
        {
            if (package.Compatible) return "Requirements match the reported build. This is not a save or co-op guarantee.";
            if (string.IsNullOrEmpty(package.CompatibilityReason) && MarketplaceRuntime.FindManaged(package.ModGuid) != null)
                return "Active this launch. Compatibility has not been refreshed from the catalog.";
            return "Unavailable for this build: " + Declared(package.CompatibilityReason);
        }

        private static PackageDescriptor CurrentListing(PackageDescriptor active)
        {
            if (active == null || MarketplaceRuntime.Catalog == null || MarketplaceRuntime.Catalog.Packages == null) return active;
            foreach (PackageDescriptor listing in MarketplaceRuntime.Catalog.Packages)
                if (listing.PackageId == active.PackageId && listing.Version == active.Version) return listing;
            return active;
        }

        private static List<PackageDescriptor> LatestListings(List<PackageDescriptor> catalog)
        {
            Dictionary<string, PackageDescriptor> latest = new Dictionary<string, PackageDescriptor>(StringComparer.Ordinal);
            foreach (PackageDescriptor p in catalog)
            {
                if (p == null || p.Revoked || string.IsNullOrEmpty(p.PackageId)) continue;
                PackageDescriptor existing;
                if (!latest.TryGetValue(p.PackageId, out existing) ||
                    (p.Compatible && !existing.Compatible) || (p.Compatible == existing.Compatible && new Version(p.Version).CompareTo(new Version(existing.Version)) > 0)) latest[p.PackageId] = p;
            }
            List<PackageDescriptor> values = new List<PackageDescriptor>(latest.Values);
            values.Sort(delegate(PackageDescriptor a, PackageDescriptor b) { return string.CompareOrdinal(a.Name, b.Name); });
            return values;
        }

        private static List<string> TextPages(List<string> blocks)
        {
            List<string> pages = new List<string>();
            foreach (string block in blocks)
            {
                StringBuilder page = new StringBuilder();
                int lines = 0;
                foreach (string line in block.Split('\n'))
                {
                    string rest = line;
                    do
                    {
                        int length = Math.Min(54, rest.Length);
                        if (length < rest.Length)
                        {
                            int space = rest.LastIndexOf(' ', length - 1, length);
                            if (space > 30) length = space;
                        }
                        page.Append(rest.Substring(0, length)).Append("\n");
                        rest = rest.Substring(length).TrimStart();
                        if (++lines == 8) { pages.Add(page.ToString()); page.Length = 0; lines = 0; }
                    } while (rest.Length > 0);
                }
                if (page.Length > 0) pages.Add(page.ToString());
            }
            return pages;
        }

        private static List<string> PreviewPaths(PackageDescriptor package, bool bundled)
        {
            List<string> paths = new List<string>();
            if (bundled)
            {
                paths.Add("embedded:FTKModFramework.assets.adventures.hollowmire.EndGameImage.png");
                paths.Add("embedded:FTKModFramework.assets.npcs.reeve_maddow.portrait.png");
            }
            else if (package != null && package.ScreenshotPaths != null)
                foreach (string path in package.ScreenshotPaths)
                    if (paths.Count < 3 && MarketplaceProtocol.IsScreenshotPath(MarketplaceRuntime.StateRoot, path)) paths.Add(path);
            return paths;
        }

        private void AddPreviewImage(Transform parent, string path)
        {
            Texture2D texture = null;
            try
            {
                byte[] bytes;
                if (path == "embedded:FTKModFramework.assets.adventures.hollowmire.EndGameImage.png" || path == "embedded:FTKModFramework.assets.npcs.reeve_maddow.portrait.png")
                {
                    using (Stream stream = typeof(Plugin).Assembly.GetManifestResourceStream(path.Substring(9)))
                    {
                        if (stream == null || stream.Length > 8 * 1024 * 1024) throw new IOException("Bundled preview unavailable.");
                        bytes = new byte[(int)stream.Length];
                        int read = 0;
                        while (read < bytes.Length) { int n = stream.Read(bytes, read, bytes.Length - read); if (n == 0) throw new EndOfStreamException(); read += n; }
                    }
                }
                else
                {
                    if (!MarketplaceProtocol.IsScreenshotPath(MarketplaceRuntime.StateRoot, path)) throw new IOException("Preview path is not approved.");
                    bytes = File.ReadAllBytes(path);
                }
                texture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(texture, bytes)) throw new IOException("Preview image could not be decoded.");
                _previewTextures.Add(texture);
                GameObject go = NewChild("Preview image", parent);
                RawImage image = go.AddComponent<RawImage>();
                image.texture = texture;
                image.raycastTarget = false;
                Stretch(image.rectTransform);
                AspectRatioFitter aspect = go.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = (float)texture.width / texture.height;
            }
            catch
            {
                if (texture != null && !_previewTextures.Contains(texture)) UnityEngine.Object.Destroy(texture);
                GameObject fallback = NewChild("Preview unavailable", parent);
                Text text = fallback.AddComponent<Text>();
                StyleText(text, "Preview unavailable", 20);
                text.alignment = TextAnchor.MiddleCenter;
                Stretch(text.rectTransform);
            }
        }

        private void AddScreenshot(string path)
        {
            GameObject host = NewChild("Screenshot", _container);
            Height(host, 280);
            AddPreviewImage(host.transform, path);
        }

        private void ReviewSelection(PackageDescriptor package, bool remove, bool enabled)
        {
            List<PackageSelection> selection = MarketplaceRuntime.DesiredSelection();
            selection.RemoveAll(delegate(PackageSelection item) { return item.PackageId == package.PackageId; });
            if (!remove) selection.Add(new PackageSelection { PackageId = package.PackageId, Version = package.Version, Enabled = enabled });
            _planIntent = remove ? "Remove " + package.Name + " from your next-launch selection." : "Set " + package.Name + " to " + OnOff(enabled) + " for your next launch.";
            if (remove && MarketplaceRuntime.FindManaged(package.ModGuid) == null) _planIntent = "Cancel the queued install of " + package.Name + ".";
            _planSelection = selection;
            _confirmOperation = "prepare";
            MarketplaceRuntime.Start("prepare", selection, true, delegate(MarketplaceResult result) {
                if (result.Ok) { _plan = result; Navigate("confirm"); }
                else { _message = result.Message ?? "Plan validation failed."; Refresh(); }
            });
            Refresh();
        }

        private void Confirmation()
        {
            TextLine("Review your changes", 36, 56);
            TextLine("Nothing changes in your current adventure. These choices apply when you next launch the game.", 25, 68);
            if (_confirmOperation == "prepare")
            {
                List<MarketplacePlanEntry> entries = _plan == null || _plan.Plan == null ? new List<MarketplacePlanEntry>() : _plan.Plan;
                List<string> review = new List<string>();
                review.Add(_planIntent ?? "Review your next-launch selection.");
                StringBuilder desired = new StringBuilder("Community mods next launch:\n");
                if (_plan == null || _plan.Packages == null || _plan.Packages.Count == 0) desired.Append("None. Included and manually installed mods keep their saved settings.");
                else foreach (PackageDescriptor item in _plan.Packages)
                {
                    desired.Append(item.Name ?? item.PackageId).Append(" ").Append(item.Version).Append(" / ").Append(OnOff(item.Enabled)).Append("\n");
                }
                review.Add(desired.ToString());
                StringBuilder delta = new StringBuilder("Changes from this launch:\n");
                foreach (MarketplacePlanEntry entry in entries)
                    delta.Append(entry.Action).Append(": ").Append(entry.Name ?? entry.PackageId).Append(" ").Append(entry.FromVersion).Append(" -> ").Append(entry.ToVersion).Append(entry.Dependency ? " (required component)" : "").Append("\n");
                if (entries.Count == 0) delta.Append("Current community selection unchanged; queued choices cleared.");
                review.Add(delta.ToString());
                List<string> pages = TextPages(new List<string> { string.Join("\n", review.ToArray()) });
                _page = Math.Min(_page, pages.Count - 1);
                TextLine(pages[_page], 25, 250);
                PageButtons(pages.Count);
            }
            else TextLine(_confirmOperation == "rollback" ? "Restore the previous community mod set on your next launch. Saves are not rolled back, and current content stays loaded." : "Discard prepared community downloads and keep the current community selection. Included and manual mod toggles are unchanged.", 25, 170);
            TextLine("Existing saves may require their original mod set. Start a new run after changing class mods. No saves are modified, migrated, deleted or automatically backed up.", 24, 106);
            PrimaryButton(_confirmOperation == "prepare" ? "Save for next launch" : _confirmOperation == "rollback" ? "Restore on next launch" : "Discard community changes", delegate {
                MarketplaceRuntime.Start(_confirmOperation, _confirmOperation == "prepare" ? _planSelection : null, false, delegate(MarketplaceResult result) { Navigate("maintenance"); _message = result.Ok && PendingCount() == 0 ? "Your selection is saved. No gameplay changes will apply." : result.Message ?? result.Status; Refresh(); }, _confirmOperation == "prepare" && _plan != null ? _plan.PlanRevision : null);
                Refresh();
            }, !MarketplaceRuntime.Busy);
        }

        private void Maintenance()
        {
            TextLine("Your next launch", 36, 54);
            int pendingCount = PendingCount();
            TextLine(pendingCount == 0 ? (MarketplaceRuntime.Pending == null ? "There are no changes waiting to apply." : "Your selection is saved. No gameplay changes will apply.") : pendingCount + " change(s) are saved. Your current adventure has not changed.", 25, 48);
            List<string> lines = new List<string>();
            foreach (ModEntry entry in ModRegistry.Entries)
                if (!entry.IsManaged && entry.PendingEnabled.HasValue) lines.Add(entry.DisplayName + ": " + (entry.PendingEnabled.Value ? "turn on" : "turn off"));
            if (MarketplaceRuntime.Pending != null)
            {
                lines.Add("Community selection after restart:");
                if (MarketplaceRuntime.Pending.Packages.Count == 0) lines.Add("No community mods selected.");
                foreach (PackageDescriptor package in MarketplaceRuntime.Pending.Packages)
                    lines.Add(package.Name + " " + package.Version + " / " + OnOff(package.Enabled));
            }
            List<string> pages = TextPages(new List<string> { string.Join("\n", lines.ToArray()) });
            if (pages.Count > 0)
            {
                _page = Math.Min(_page, pages.Count - 1);
                TextLine(pages[_page], 22, 210);
                PageButtons(pages.Count);
            }
            if (pendingCount > 0)
            {
                TextLine("Start a new run after changing class mods. Existing saves may need their original mod set.", 22, 40);
                PrimaryButton("Quit and apply on next launch", delegate { Application.Quit(); }, !MarketplaceRuntime.Busy);

            }
            if (MarketplaceRuntime.Pending != null)
                LinkButton("Discard community download changes...", delegate { _confirmOperation = "cancel"; Navigate("confirm"); }, !MarketplaceRuntime.Busy);
            bool preferences = false;
            foreach (ModEntry entry in ModRegistry.Entries) if (!entry.IsManaged && entry.PendingEnabled.HasValue) preferences = true;
            if (preferences) TextLine("Undo included or manual mod changes in Installed.", 22, 32);
            LinkButton("Tools and recovery", delegate { Navigate("tools"); });
        }

        private void Tools()
        {
            TextLine("Tools and recovery", 36, 56);
            List<string> pages = TextPages(new List<string> { MarketplaceRuntime.RegistrationNotice ?? "Installed content is selected for this launch. Changes never unload it mid-game.", MarketplaceRuntime.Notice ?? "No marketplace status to report.", _message });
            _detailPage = Math.Min(_detailPage, pages.Count - 1);
            TextLine(pages[_detailPage], 23, 225);
            if (pages.Count > 1) ActionButton("Next status detail (" + (_detailPage + 1) + " of " + pages.Count + ")", delegate { _detailPage = (_detailPage + 1) % pages.Count; Refresh(); });
            ActionButton("Review next-launch changes", delegate { Navigate("maintenance"); });
            ActionButton("Restore the previous community mod set...", delegate { _confirmOperation = "rollback"; Navigate("confirm"); }, MarketplaceRuntime.PreviousAvailable && !MarketplaceRuntime.Busy);
            ActionButton("Export community mod list", delegate {
                MarketplaceRuntime.Start("export", null, false, delegate(MarketplaceResult result) { _message = result.Ok ? "Exported: " + result.ExportPath + ". Manual mods are not fully fingerprinted; this is not a co-op compatibility guarantee." : result.Message; Refresh(); });
                Refresh();
            }, !MarketplaceRuntime.Busy);
            TextLine("Need to repair marketplace access? Use Install / Repair in the launcher. Your manually installed files are not removed.", 23, 76);
        }

        private void Search()
        {
            TextLine("Search: " + _search + "\nType on a keyboard, or choose and add characters with the controller.", 27, 88);
            TextLine("Selected character: [" + (Alphabet[_letter] == ' ' ? "space" : Alphabet[_letter].ToString()) + "]", 26, 46);
            ActionButton("Previous character", delegate { _letter = (_letter + Alphabet.Length - 1) % Alphabet.Length; Refresh(); });
            ActionButton("Next character", delegate { _letter = (_letter + 1) % Alphabet.Length; Refresh(); });
            ActionButton("Add selected character", delegate { if (_search.Length < 40) _search += Alphabet[_letter]; Refresh(); });
            ActionButton("Backspace", delegate { if (_search.Length > 0) _search = _search.Substring(0, _search.Length - 1); Refresh(); });
            ActionButton("Clear search", delegate { _search = ""; Refresh(); });
            ActionButton("Search catalog", delegate { Navigate("discover"); });
        }

        private void PageButtons(int pages)
        {
            if (pages <= 1) return;
            Transform parent = _container;
            _container = HorizontalRow("Pages", 42);
            Button previous = ActionButton("Previous (" + (_page + 1) + "/" + pages + ")", delegate { _page = (_page + pages - 1) % pages; Refresh(); }, true, 40);
            SetWidth(previous.gameObject, 240);
            Button next = ActionButton("Next", delegate { _page = (_page + 1) % pages; Refresh(); }, true, 40);
            SetWidth(next.gameObject, 120);
            _container = parent;
        }

        private void AddTabs()
        {
            Transform row = HorizontalRow("Tabs", 50);
            _container = row;
            Button browse = ActionButton("Browse", delegate { Navigate("discover"); if (MarketplaceRuntime.Catalog == null) LoadCatalog(); }, true, 48);
            SetWidth(browse.gameObject, 160);
            if (_view == "discover") Border(browse.gameObject, Gold, 2);
            Button installed = ActionButton("Installed", delegate { Navigate("installed"); }, true, 48);
            SetWidth(installed.gameObject, 170);
            if (_view == "installed") Border(installed.gameObject, Gold, 2);
            if (PendingCount() > 0)
            {
                Button changes = ActionButton(PendingCount() == 0 ? "Next-launch selection" : "Review changes (" + PendingCount() + ")", delegate { Navigate("maintenance"); }, true, 48);
                SetWidth(changes.gameObject, 290);
                changes.GetComponent<Image>().color = new Color(0.95f, 0.87f, 0.67f, 1f);
            }
            _container = _rootContent;
        }

        private void AddFooter()
        {
            _container = _rootContent;
            Rule();
            Transform row = HorizontalRow("Footer", 60);
            _container = row;
            string notice = MarketplaceRuntime.Busy ? "Working... Your current mods stay unchanged." : PendingCount() > 0 ? "Changes are saved for next launch." : MarketplaceRuntime.Pending != null ? "Your selection is saved. No gameplay changes will apply." : "Your installed mods stay unchanged until you restart.";
            if (_message.Length > 0) notice = Short(_message, 120);
            Text status = TextLine(notice, 21, 58);
            status.GetComponent<LayoutElement>().flexibleWidth = 1;
            if (MarketplaceRuntime.Busy)
            {
                Button cancel = LinkButton("Cancel operation", delegate { MarketplaceRuntime.CancelRunning(); Refresh(); });
                SetWidth(cancel.gameObject, 200);
            }
            else
            {
                if (_view != "tools")
                {
                    Button tools = LinkButton("Tools", delegate { Navigate("tools"); });
                    SetWidth(tools.gameObject, 90);
                }
                if (_view == "installed")
                {
                    Button components = LinkButton("Components", delegate { Navigate("components"); });
                    SetWidth(components.gameObject, 175);
                }
            }
            Button back = LinkButton(_view == "installed" || _view == "discover" ? "Back to title" : "Back", delegate {
                if (_view == "installed" || _view == "discover") FTKInput.Instance.Close(this); else Navigate("installed");
            });
            SetWidth(back.gameObject, 155);
            m_ButtonOnCancel = back;
            _container = _rootContent;
        }

        private static int PendingCount()
        {
            int count = 0;
            foreach (ModEntry entry in ModRegistry.Entries) if (!entry.IsManaged && entry.PendingEnabled.HasValue) count++;
            if (MarketplaceRuntime.Pending == null) return count;
            if (MarketplaceRuntime.Active != null) foreach (PackageDescriptor active in MarketplaceRuntime.Active.Packages)
            {
                PackageDescriptor desired = DesiredPackage(active.PackageId);
                if (desired == null || desired.Enabled != active.Enabled || desired.Version != active.Version) count++;
            }
            foreach (PackageDescriptor pending in MarketplaceRuntime.Pending.Packages)
                if (MarketplaceRuntime.FindManaged(pending.ModGuid) == null) count++;
            return count;
        }

        private void EmptyDetails()
        {
            if (_view == "components")
            {
                Spacer(56);
                TextLine("Behind the adventure", 36, 104);
                TextLine("Some community mods need supporting components. They appear here when needed, separate from the mods you choose to play.", 25, 170);
                return;
            }
            Spacer(56);
            TextLine("A new chapter awaits", 36, 104);
            TextLine("Pick a mod to learn what it adds.\n\nYour included Adventure Pack is ready to play in Installed.", 25, 170);
            Spacer(35);
            Rule();
            TextLine("Free community content.\nEvery change is yours to review.", 23, 90);
        }

        private void Card(string title, string summary, string category, string state, bool selected, Action action, List<string> previews)
        {
            Button button = ActionButton(title, action, true, 166);
            Border(button.gameObject, selected ? Gold : WarmBorder, selected ? 2 : 1);
            Text heading = button.GetComponentInChildren<Text>();
            StyleText(heading, title, 28);
            TopBox(heading.rectTransform, 14, 64, 24);
            heading.resizeTextForBestFit = true;
            heading.resizeTextMinSize = 20;
            heading.resizeTextMaxSize = 28;
            GameObject description = NewChild("Summary", button.transform);
            Text text = description.AddComponent<Text>();
            StyleText(text, summary, 23);
            TopBox(text.rectTransform, 66, 120, 24);
            if (previews.Count > 0)
            {
                heading.rectTransform.offsetMax = new Vector2(-200, heading.rectTransform.offsetMax.y);
                text.rectTransform.offsetMax = new Vector2(-200, text.rectTransform.offsetMax.y);
                text.text = Short(summary, 70);
                GameObject thumbnail = NewChild("Thumbnail", button.transform);
                RectTransform thumbnailRect = thumbnail.GetComponent<RectTransform>();
                thumbnailRect.anchorMin = thumbnailRect.anchorMax = thumbnailRect.pivot = new Vector2(1, 1);
                thumbnailRect.anchoredPosition = new Vector2(-20, -18);
                thumbnailRect.sizeDelta = new Vector2(160, 100);
                AddPreviewImage(thumbnail.transform, previews[0]);
            }
            GameObject badges = NewChild("Status", button.transform);
            TopBox(badges.GetComponent<RectTransform>(), 128, 153, 24);
            HorizontalLayoutGroup line = badges.AddComponent<HorizontalLayoutGroup>();
            line.spacing = 10;
            line.childControlWidth = line.childControlHeight = true;
            line.childForceExpandWidth = false;
            Pill(badges.transform, category, false);
            Pill(badges.transform, state, true);
        }

        private static void Pill(Transform parent, string label, bool filled)
        {
            GameObject go = NewChild("Badge", parent);
            if (filled) go.AddComponent<Image>().color = new Color(0.95f, 0.89f, 0.75f, 1f);
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.minWidth = layout.preferredWidth = Math.Min(440, label.Length * 10 + 20);
            GameObject labelGo = NewChild("Text", go.transform);
            Text text = labelGo.AddComponent<Text>();
            StyleText(text, label, 17);
            text.color = Gold;
            text.alignment = TextAnchor.MiddleCenter;
            Stretch(text.rectTransform);
        }

        private static void TopBox(RectTransform rect, float top, float bottom, float inset)
        {
            rect.anchorMin = new Vector2(0, 1); rect.anchorMax = new Vector2(1, 1);
            rect.offsetMin = new Vector2(inset, -bottom); rect.offsetMax = new Vector2(-inset, -top);
        }

        private Transform HorizontalRow(string name, float height)
        {
            GameObject go = NewChild(name, _container);
            Height(go, height);
            HorizontalLayoutGroup layout = go.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = false;
            return go.transform;
        }

        private void Rule()
        {
            GameObject go = NewChild("Divider", _container);
            go.AddComponent<Image>().color = WarmBorder;
            Height(go, 1);
        }
        private void Spacer(float height) { Height(NewChild("Space", _container), height); }
        private Button PrimaryButton(string title, Action action, bool enabled = true)
        {
            Button button = ActionButton(title, action, enabled, 54);
            button.GetComponent<Image>().color = new Color(0.95f, 0.87f, 0.67f, 1f);
            button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            Border(button.gameObject, Gold, 1);
            return button;
        }
        private Button LinkButton(string title, Action action, bool enabled = true)
        {
            Button button = ActionButton(title, action, enabled, 38);
            button.GetComponent<Image>().color = Paper;
            button.GetComponent<Outline>().enabled = false;
            Text text = button.GetComponentInChildren<Text>();
            text.fontSize = 20;
            text.color = Gold;
            return button;
        }
        private static void SetWidth(GameObject go, float width)
        {
            LayoutElement size = go.GetComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
        }
        private static void Border(GameObject go, Color color, float width)
        {
            Outline border = go.GetComponent<Outline>();
            if (border == null) border = go.AddComponent<Outline>();
            border.effectColor = color;
            border.effectDistance = new Vector2(width, -width);
        }
        private static Font HeadingFont()
        {
            if (_serif != null) return _serif;
            foreach (Font font in Resources.FindObjectsOfTypeAll<Font>())
                if (font.name == "Merriweather-Regular") { _serif = font; return _serif; }
            try { _serif = Font.CreateDynamicFontFromOSFont(new string[] { "Times", "Times New Roman", "Liberation Serif", "DejaVu Serif" }, 32); }
            catch { }
            if (_serif == null) _serif = Resources.GetBuiltinResource<Font>("Arial.ttf");
            return _serif;
        }
        private static string FriendlyAge(long seconds)
        {
            if (seconds < 60) return "less than a minute";
            if (seconds < 3600) return (seconds / 60) + " minutes";
            if (seconds < 86400) return (seconds / 3600) + " hours";
            return (seconds / 86400) + " days";
        }

        private void CreateColumns(out Transform left, out Transform right)
        {
            GameObject body = NewChild("Marketplace", _rootContent);
            Height(body, 700);
            HorizontalLayoutGroup split = body.AddComponent<HorizontalLayoutGroup>();
            split.spacing = 28;
            split.childControlWidth = split.childControlHeight = true;
            split.childForceExpandWidth = false;
            split.childForceExpandHeight = true;
            left = Column(body.transform, "Browse", 780, Paper);
            right = Column(body.transform, "Details", 700, CardPaper);
        }

        private static Transform Column(Transform parent, string name, float width, Color color)
        {
            GameObject go = NewChild(name, parent);
            go.AddComponent<Image>().color = color;
            if (name == "Details") Border(go, WarmBorder, 1);
            LayoutElement size = go.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = name == "Details" ? new RectOffset(30, 30, 24, 24) : new RectOffset(0, 0, 10, 10);
            layout.spacing = name == "Details" ? 6 : 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go.transform;
        }

        private Button ActionButton(string caption, Action action, bool enabled = true, float height = 45f)
        {
            GameObject go = NewChild("Action", _container);
            Image image = go.AddComponent<Image>();
            image.color = CardPaper;
            Border(go, WarmBorder, 1);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.97f, 0.88f, 0.66f, 1f);
            colors.pressedColor = new Color(0.88f, 0.74f, 0.45f, 1f);
            colors.disabledColor = new Color(0.88f, 0.86f, 0.81f, 1f);
            button.colors = colors;
            // Verified FTKSelectable.Awake requires the Unity Selectable to exist first.
            FTKSelectable selectable = go.AddComponent<FTKSelectable>();
            int index = _controls.Count;
            if (enabled) _controls.Add(selectable);
            button.onClick.AddListener(delegate {
                _focusIndex = index;
                try { action(); }
                catch (Exception e) { _message = e.Message; Plugin.Log.LogError("Mods panel: " + e); Refresh(); }
            });
            GameObject labelGo = NewChild("Label", go.transform);
            Text text = labelGo.AddComponent<Text>();
            StyleText(text, caption, height > 100 ? 21 : 23);
            text.alignment = TextAnchor.MiddleLeft;
            Stretch(text.rectTransform);
            text.rectTransform.offsetMin = new Vector2(14f, 3f);
            text.rectTransform.offsetMax = new Vector2(-14f, -3f);
            Height(go, _container.name == "Details" ? Math.Min(height, 46) : height);
            return button;
        }

        private Text TextLine(string value, int size, float height)
        {
            GameObject go = NewChild("Text", _container);
            Text text = go.AddComponent<Text>();
            StyleText(text, value, size);
            Height(go, height);
            return text;
        }
        private static void StyleText(Text text, string value, int size)
        {
            text.text = value ?? "";
            text.supportRichText = false;
            text.font = size >= 27 ? HeadingFont() : Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.color = size >= 27 ? Ink : MutedInk;
            text.alignment = TextAnchor.UpperLeft;
            text.horizontalOverflow = HorizontalWrapMode.Wrap;
            text.verticalOverflow = VerticalWrapMode.Truncate;
            text.raycastTarget = false;
        }
        private static void Height(GameObject go, float value)
        {
            LayoutElement layout = go.AddComponent<LayoutElement>();
            layout.minHeight = layout.preferredHeight = value;
            layout.flexibleHeight = 0;
        }
        private static string Declared(string value) { return string.IsNullOrEmpty(value) ? "Not declared" : value; }
        private static string Short(string value, int max) { return string.IsNullOrEmpty(value) ? "No description declared." : value.Length > max ? value.Substring(0, max - 3) + "..." : value; }
        private static string Join(string[] values) { return values == null || values.Length == 0 ? "Not declared" : string.Join("\n", values); }
        private static GameObject NewChild(string name, Transform parent)
        {
            GameObject child = new GameObject(name, typeof(RectTransform));
            child.transform.SetParent(parent, false);
            return child;
        }
        private static void Stretch(RectTransform rect)
        {
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
        }
    }
    internal sealed class MarketplacePanelTick : MonoBehaviour
    {
        internal ModsPanel Panel;
        private void Update() { if (Panel != null) Panel.Tick(); }
        private void LateUpdate() { if (Panel != null) Panel.RenderPending(); }
        private void OnDisable() { if (Panel != null) Panel.ReleasePreviews(); }
        private void OnDestroy() { if (Panel != null) Panel.ReleasePreviews(); }
    }
}
