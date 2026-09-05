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
        private int _focusIndex;
        private readonly List<FTKSelectable> _controls = new List<FTKSelectable>();
        private PackageDescriptor _package;
        private ModEntry _entry;
        private List<PackageSelection> _planSelection;
        private MarketplaceResult _plan;
        private string _confirmOperation;
        private bool _wasBusy;
        private Texture2D _previewTexture;
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
                visibleButtons.Add(new Dictionary<string, object> { { "index", n }, { "label", caption == null ? "" : caption.text }, { "enabled", buttons[n].interactable } });
            }
            List<string> texts = new List<string>();
            foreach (Text text in _instance.GetComponentsInChildren<Text>(false)) texts.Add(text.text);
            return new Dictionary<string, object> { { "view", _instance._view }, { "texts", texts }, { "buttons", visibleButtons }, { "busy", MarketplaceRuntime.Busy } };
        }

        public static void Open()
        {
            if (_instance == null) _instance = Build();
            _instance.Refresh();
            FTKInput.Instance.SetFocus(_instance, null, true, null, false);
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
                background.color = new Color(0.035f, 0.045f, 0.06f, 0.98f);
                Stretch(background.rectTransform);
                ModsPanel panel = root.AddComponent<ModsPanel>();
                GameObject container = NewChild("Content", root.transform);
                RectTransform rect = container.GetComponent<RectTransform>();
                rect.anchorMin = rect.anchorMax = rect.pivot = new Vector2(0.5f, 0.5f);
                rect.sizeDelta = new Vector2(1460f, 1020f);
                VerticalLayoutGroup layout = container.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 8f;
                layout.padding = new RectOffset(24, 24, 16, 16);
                layout.childControlWidth = layout.childControlHeight = true;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                panel._container = panel._rootContent = container.transform;
                panel.m_SelectableParent = container.transform;
                panel.m_NavigationSetup = FTKInputFocus.NavigationSetup.Vertical;
                // Polling lives on a separate component so uiScreen's own Update/focus logic is preserved.
                root.AddComponent<MarketplacePanelTick>().Panel = panel;
                panel.Refresh();
                return panel;
            }
            catch
            {
                if (root != null) { root.SetActive(false); UnityEngine.Object.Destroy(root); }
                throw;
            }
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
            _focusIndex = 0;
            _message = "";
            if (view == "installed" || view == "discover" || view == "components") { _entry = null; _package = null; }
            Refresh();
        }

        private void Refresh()
        {
            if (_rootContent == null) return;
            _container = _rootContent;
            for (int i = _rootContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = _rootContent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            _controls.Clear();
            if (_previewTexture != null) { UnityEngine.Object.Destroy(_previewTexture); _previewTexture = null; }
            TextLine("FOR THE KING   /   COMMUNITY MODS", 34, 50);
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
                    TextLine("Your next adventure", 32, 56);
                    TextLine("Select a mod to see its content, requirements, screenshots and changes.\n\nInstall plans are reviewed here and apply after restart. Your current session stays unchanged.", 25, 220);
                }
                _container = _rootContent;
            }
            else if (_view == "details") Details();
            else if (_view == "confirm") Confirmation();
            else if (_view == "search") Search();
            else if (_view == "maintenance") Maintenance();
            if (MarketplaceRuntime.Busy) ActionButton("Cancel running operation", delegate { MarketplaceRuntime.CancelRunning(); Refresh(); });
            string notice = _message.Length > 0 ? _message : MarketplaceRuntime.Notice;
            TextLine(notice, 20, 66);
            Button back = ActionButton(_view == "installed" || _view == "discover" ? "Back to title" : "Back to Installed", delegate {
                if (_view == "installed" || _view == "discover") FTKInput.Instance.Close(this);
                else Navigate("installed");
            });
            m_ButtonOnCancel = back;
            if (gameObject.activeInHierarchy)
            {
                UpdateSelectables();
                SetupNavigation();
                if (_controls.Count > 0) FTKInput.SetSelected(_controls[Math.Min(_focusIndex, _controls.Count - 1)]);
            }
        }

        private void Installed(bool components)
        {
            TextLine(components ? "REQUIRED COMPONENTS" : "INSTALLED GAMEPLAY", 25, 38);
            TextLine("Changes apply after restart. Active content stays loaded.", 21, 58);
            if (MarketplaceRuntime.RegistrationNotice != null) TextLine(MarketplaceRuntime.RegistrationNotice, 21, 70);
            List<ModEntry> entries = new List<ModEntry>();
            foreach (ModEntry candidate in ModRegistry.Entries)
            {
                PackageDescriptor managed = candidate.IsManaged ? MarketplaceRuntime.FindManaged(candidate.Key) : null;
                bool component = managed != null && (managed.Classification == "dependency" || managed.Classification == "component");
                if (component == components) entries.Add(candidate);
            }
            if (!components) ActionButton("Required components", delegate { Navigate("components"); });
            if (entries.Count == 0) TextLine(components ? "No managed dependencies installed." : "No gameplay mods installed.", 25, 80);
            if (_entry == null && _package == null && entries.Count > 0)
            {
                _entry = entries[0];
                _package = _entry.IsManaged ? CurrentListing(MarketplaceRuntime.FindManaged(_entry.Key)) : null;
            }
            int pages = Math.Max(1, (entries.Count + 2) / 3);
            _page = Math.Min(_page, pages - 1);
            for (int i = _page * 3; i < Math.Min(entries.Count, (_page + 1) * 3); i++)
            {
                ModEntry entry = entries[i];
                string origin = entry.IsBundledDemo ? "Bundled" : entry.IsManaged ? "Managed" : "Manual / unmanaged";
                string pending = PendingLabel(entry);
                ActionButton(Short(entry.DisplayName, 38) + "\n" + origin + (entry.IsBundledDemo ? "" : "  |  " + Declared(entry.Version)) + "\n" +
                    (entry.Enabled ? "Active: enabled" : "Active: disabled") + pending,
                    delegate { _entry = entry; _package = entry.IsManaged ? CurrentListing(MarketplaceRuntime.FindManaged(entry.Key)) : null; _detailPage = 0; Refresh(); }, true, 104);
            }
            PageButtons(pages);
        }

        private string PendingLabel(ModEntry entry)
        {
            if (!entry.IsManaged) return entry.PendingEnabled.HasValue ? "  |  Pending: " + (entry.PendingEnabled.Value ? "enable" : "disable") : "";
            if (MarketplaceRuntime.Pending == null) return "";
            PackageDescriptor next = null;
            foreach (PackageDescriptor p in MarketplaceRuntime.Pending.Packages) if (p.PackageId == entry.PackageId) next = p;
            if (next == null) return "  |  Pending: remove";
            if (next.Version != entry.Version) return "  |  Pending: version " + next.Version;
            return next.Enabled == entry.Enabled ? "" : "  |  Pending: " + (next.Enabled ? "enable" : "disable");
        }

        private void Discover()
        {
            TextLine("DISCOVER GAMEPLAY", 25, 30);
            ActionButton("Search: " + (_search.Length == 0 ? "all names and descriptions" : _search), delegate { Navigate("search"); });
            ActionButton("Category: " + Categories[_category] + " (change)", delegate { _category = (_category + 1) % Categories.Length; _page = 0; Refresh(); });
            ActionButton("Refresh curated catalog", LoadCatalog, !MarketplaceRuntime.Busy);
            MarketplaceResult catalog = MarketplaceRuntime.Catalog;
            if (catalog == null) { TextLine(MarketplaceRuntime.Busy ? "Loading the curated catalog..." : "No verified catalog is available. Installed mods remain usable. Refresh to retry.", 25, 110); return; }
            if (catalog.Status == "unavailable") { TextLine(catalog.Message ?? "Catalog unavailable; no verified cached packages. Installed mods remain available.", 24, 110); return; }
            TextLine(catalog.Status == "offline" ? "Offline catalog: " + catalog.CatalogAgeSeconds + " seconds old." : "Free curated gameplay packages.", 21, 36);
            List<PackageDescriptor> results = new List<PackageDescriptor>();
            if (catalog.Packages != null) foreach (PackageDescriptor package in LatestListings(catalog.Packages))
            {
                if (package.Revoked || package.Classification == "dependency" || package.Classification == "component" || package.Classification == "development" || package.ModGuid == Plugin.Guid) continue;
                string text = (package.Name ?? "") + " " + (package.Description ?? "") + " " + (package.Author ?? "");
                if (_search.Length > 0 && text.IndexOf(_search, StringComparison.OrdinalIgnoreCase) < 0) continue;
                if (_category != 0 && !string.Equals(package.Category, Categories[_category], StringComparison.OrdinalIgnoreCase)) continue;
                results.Add(package);
            }
            if (results.Count == 0) TextLine("No published gameplay packages match this view. The bundled Adventure Pack is already in Installed.", 25, 90);
            int pages = Math.Max(1, (results.Count + 1) / 2);
            _page = Math.Min(_page, pages - 1);
            for (int i = _page * 2; i < Math.Min(results.Count, (_page + 1) * 2); i++)
            {
                PackageDescriptor package = results[i];
                ActionButton(Short(package.Name, 38) + "  " + Declared(package.Version) + "\nBy " + Short(package.Author, 28) + "\n" + Declared(package.Category) + " | " + Declared(package.License) + "\n" + (package.Compatible ? "Requirements match" : "View compatibility requirements") + "\n" + Short(package.Description, 65),
                    delegate { _package = package; _entry = null; _detailPage = 0; Refresh(); }, true, 146);
            }
            PageButtons(pages);
        }

        private void LoadCatalog()
        {
            MarketplaceRuntime.Start("catalog", null, false, delegate(MarketplaceResult result) { _message = result.Message ?? result.Status; Refresh(); });
            Refresh();
        }

        private void Details()
        {
            string name = _package != null ? _package.Name : _entry.DisplayName;
            TextLine(name, 28, 72);
            if (_package == null)
            {
                List<string> manualPages = TextPages(new List<string> { Declared(_entry.Description) + "\nAuthor: " + Declared(_entry.Author) + "\n" + (_entry.IsBundledDemo ? "Included with FTK Mod Framework " + Plugin.Version + "\nLicense: MIT. Included with your framework build." : "Version: " + Declared(_entry.Version) + "\nLicense: Not declared. Compatibility: Unknown.") + "\n" + (_entry.IsBundledDemo ? "Bundled gameplay, enabled with the framework config. It is not a separate marketplace download." : "Manually installed content. Marketplace operations cannot update or remove its files.") });
                _detailPage = Math.Min(_detailPage, manualPages.Count - 1);
                TextLine(manualPages[_detailPage], 24, 250);
                if (manualPages.Count > 1) ActionButton("More details (" + (_detailPage + 1) + "/" + manualPages.Count + ")", delegate { _detailPage = (_detailPage + 1) % manualPages.Count; Refresh(); });
                ActionButton((_entry.PendingEnabled ?? _entry.Enabled) ? "Disable on next launch" : "Enable on next launch", delegate {
                    ModRegistry.SetEnabled(_entry.Key, !(_entry.PendingEnabled ?? _entry.Enabled));
                    _message = "Preference saved for next launch. Current content remains loaded.";
                    Refresh();
                });
                TextLine("Existing saves may require their original mod set. Start a new run after changing class mods. Saves are not modified or backed up here.", 23, 100);
                return;
            }
            PackageDescriptor p = _package;
            List<string> blocks = new List<string>();
            blocks.Add(Declared(p.Description) + "\nAuthor: " + Declared(p.Author) + " | Version: " + Declared(p.Version) + " | License: " + Declared(p.License) + "\nCategory: " + Declared(p.Category) + "\n" + CompatibilityText(p));
            blocks.Add("What changes\n" + Join(p.ContentChanges) + "\nRequirements\n" + Join(p.Requirements) + "\nFramework: " + Declared(p.FrameworkRange) + "\nPlatforms: " + Join(p.Platforms));
            StringBuilder dependencies = new StringBuilder("Required components (exact versions)\n");
            if (p.Dependencies == null || p.Dependencies.Length == 0) dependencies.Append("None declared.");
            else foreach (PackageSelection dependency in p.Dependencies) dependencies.Append(dependency.PackageId).Append(" ").Append(dependency.Version).Append("\n");
            blocks.Add(dependencies.ToString() + "\nChangelog\n" + Declared(p.Changelog));
            blocks.Add("Source: " + Declared(p.SourceUrl) + "\nSupport: " + Declared(p.SupportUrl) + "\nScreenshots: " + Join(p.Screenshots) + "\nArtifact SHA-256: " + Declared(p.Sha256) + "\nA checksum verifies bytes, not author trust or runtime safety.");
            List<string> pages = TextPages(blocks);
            List<string> screenshots = new List<string>();
            if (p.ScreenshotPaths != null) foreach (string path in p.ScreenshotPaths)
                if (MarketplaceProtocol.IsScreenshotPath(MarketplaceRuntime.StateRoot, path)) screenshots.Add(path);
            int detailPages = pages.Count + screenshots.Count;
            _detailPage = Math.Min(_detailPage, detailPages - 1);
            if (_detailPage < pages.Count) TextLine(pages[_detailPage], 24, 280);
            else AddScreenshot(screenshots[_detailPage - pages.Count]);
            ActionButton("More details / screenshots (" + (_detailPage + 1) + "/" + detailPages + ")", delegate { _detailPage = (_detailPage + 1) % detailPages; Refresh(); });
            PackageDescriptor active = MarketplaceRuntime.FindManaged(p.ModGuid);
            if (active == null || active.Version != p.Version)
                ActionButton(active == null ? "Review install plan" : "Review update to " + p.Version, delegate { ReviewSelection(p, false, true); }, p.Compatible && !p.Revoked && !MarketplaceRuntime.Busy);
            if (active != null)
            {
                ActionButton(active.Enabled ? "Review disable plan" : "Review enable plan", delegate { ReviewSelection(active, false, !active.Enabled); }, !MarketplaceRuntime.Busy);
                ActionButton("Review removal plan", delegate { ReviewSelection(active, true, false); }, !MarketplaceRuntime.Busy);
            }
            if (p.Revoked) TextLine("This package has been revoked. It remains installed until you choose a change; new installs are blocked.", 23, 66);
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

        private void AddScreenshot(string path)
        {
            try
            {
                _previewTexture = new Texture2D(2, 2, TextureFormat.ARGB32, false);
                if (!ImageConversion.LoadImage(_previewTexture, File.ReadAllBytes(path))) throw new IOException("Screenshot image could not be decoded.");
                GameObject host = NewChild("Screenshot", _container);
                Height(host, 280);
                GameObject go = NewChild("Image", host.transform);
                RawImage image = go.AddComponent<RawImage>();
                image.texture = _previewTexture;
                image.raycastTarget = false;
                Stretch(image.rectTransform);
                AspectRatioFitter aspect = go.AddComponent<AspectRatioFitter>();
                aspect.aspectMode = AspectRatioFitter.AspectMode.FitInParent;
                aspect.aspectRatio = (float)_previewTexture.width / _previewTexture.height;
            }
            catch (Exception e) { TextLine("Screenshot unavailable: " + e.Message, 23, 280); }
        }

        private void ReviewSelection(PackageDescriptor package, bool remove, bool enabled)
        {
            List<PackageSelection> selection = MarketplaceRuntime.DesiredSelection();
            selection.RemoveAll(delegate(PackageSelection item) { return item.PackageId == package.PackageId; });
            if (!remove) selection.Add(new PackageSelection { PackageId = package.PackageId, Version = package.Version, Enabled = enabled });
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
            TextLine("Review the complete change before preparation. Current-session content will not change.", 24, 62);
            if (_confirmOperation == "prepare")
            {
                List<MarketplacePlanEntry> entries = _plan == null || _plan.Plan == null ? new List<MarketplacePlanEntry>() : _plan.Plan;
                int pages = Math.Max(1, (entries.Count + 5) / 6);
                _page = Math.Min(_page, pages - 1);
                StringBuilder plan = new StringBuilder();
                for (int i = _page * 6; i < Math.Min(entries.Count, (_page + 1) * 6); i++)
                {
                    MarketplacePlanEntry entry = entries[i];
                    plan.Append(entry.Action).Append(": ").Append(entry.Name ?? entry.PackageId).Append(" ").Append(entry.FromVersion).Append(" -> ").Append(entry.ToVersion).Append(entry.Dependency ? " (required component)" : "").Append("\n");
                }
                TextLine(entries.Count == 0 ? "No package changes are required." : plan.ToString(), 25, 220);
                PageButtons(pages);
            }
            else TextLine(_confirmOperation == "rollback" ? "Prepare the previous managed generation and enabled selection for next launch. This does not roll back saves or unload current content." : "Cancel prepared changes and keep the current managed selection.", 25, 170);
            TextLine("Existing saves may require their original mod set. Start a new run after changing class mods. No saves are modified, migrated, deleted or automatically backed up.", 24, 106);
            ActionButton("Confirm " + (_confirmOperation == "prepare" ? "preparation" : _confirmOperation), delegate {
                MarketplaceRuntime.Start(_confirmOperation, _confirmOperation == "prepare" ? _planSelection : null, false, delegate(MarketplaceResult result) { Navigate("maintenance"); _message = result.Message ?? result.Status; Refresh(); }, _confirmOperation == "prepare" && _plan != null ? _plan.PlanRevision : null);
                Refresh();
            }, !MarketplaceRuntime.Busy);
        }

        private void Maintenance()
        {
            ManagedSnapshot pending = MarketplaceRuntime.Pending;
            TextLine("Managed packages active this launch: " + (MarketplaceRuntime.Active == null ? 0 : MarketplaceRuntime.Active.Packages.Count) + "\n" + (pending == null ? "No prepared changes for next launch." : "Prepared next-launch selection: " + pending.Packages.Count + " packages."), 24, 70);
            if (pending != null)
            {
                StringBuilder list = new StringBuilder("Next-launch selection\n");
                int selectionPages = Math.Max(1, (pending.Packages.Count + 3) / 4);
                _page = Math.Min(_page, selectionPages - 1);
                for (int i = _page * 4; i < Math.Min(pending.Packages.Count, (_page + 1) * 4); i++)
                {
                    PackageDescriptor p = pending.Packages[i];
                    list.Append(Declared(p.Name)).Append(" ").Append(p.Version).Append(p.Enabled ? " enabled" : " disabled").Append("\n");
                }
                TextLine(list.ToString(), 23, 150);
                PageButtons(selectionPages);
                ActionButton("Cancel pending changes", delegate { _confirmOperation = "cancel"; Navigate("confirm"); }, !MarketplaceRuntime.Busy);
            }
            ActionButton("Prepare rollback to previous generation", delegate { _confirmOperation = "rollback"; Navigate("confirm"); }, MarketplaceRuntime.PreviousAvailable && !MarketplaceRuntime.Busy);
            ActionButton("Export exact managed set (partial co-op information)", delegate {
                MarketplaceRuntime.Start("export", null, false, delegate(MarketplaceResult result) { _message = result.Ok ? "Exported: " + result.ExportPath + ". Manual mods are not fully fingerprinted; this is not a co-op compatibility guarantee." : result.Message; Refresh(); });
                Refresh();
            }, !MarketplaceRuntime.Busy);
            TextLine("Helper missing or incompatible? Use Install / Repair in the branded launcher. Existing manually installed mods remain available.", 23, 66);
            ActionButton("Quit game, then launch from Steam to apply changes", delegate { Application.Quit(); }, !MarketplaceRuntime.Busy);
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
            ActionButton("Previous page (" + (_page + 1) + "/" + pages + ")", delegate { _page = (_page + pages - 1) % pages; Refresh(); });
            ActionButton("Next page", delegate { _page = (_page + 1) % pages; Refresh(); });
        }

        private void AddTabs()
        {
            GameObject row = NewChild("Tabs", _rootContent);
            Height(row, 46);
            HorizontalLayoutGroup layout = row.AddComponent<HorizontalLayoutGroup>();
            layout.spacing = 12;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            _container = row.transform;
            ActionButton("Installed", delegate { Navigate("installed"); });
            ActionButton("Discover", delegate { Navigate("discover"); if (MarketplaceRuntime.Catalog == null) LoadCatalog(); });
            ActionButton("Manage changes", delegate { Navigate("maintenance"); });
            _container = _rootContent;
        }

        private void CreateColumns(out Transform left, out Transform right)
        {
            GameObject body = NewChild("Marketplace", _rootContent);
            Height(body, 700);
            HorizontalLayoutGroup split = body.AddComponent<HorizontalLayoutGroup>();
            split.spacing = 18;
            split.childControlWidth = split.childControlHeight = true;
            split.childForceExpandWidth = false;
            split.childForceExpandHeight = true;
            left = Column(body.transform, "Browse", 570, new Color(0.075f, 0.09f, 0.12f, 1f));
            right = Column(body.transform, "Details", 824, new Color(0.105f, 0.12f, 0.15f, 1f));
        }

        private static Transform Column(Transform parent, string name, float width, Color color)
        {
            GameObject go = NewChild(name, parent);
            go.AddComponent<Image>().color = color;
            LayoutElement size = go.AddComponent<LayoutElement>();
            size.minWidth = size.preferredWidth = width;
            VerticalLayoutGroup layout = go.AddComponent<VerticalLayoutGroup>();
            layout.padding = new RectOffset(20, 20, 18, 18);
            layout.spacing = 8;
            layout.childControlWidth = layout.childControlHeight = true;
            layout.childForceExpandWidth = true;
            layout.childForceExpandHeight = false;
            return go.transform;
        }

        private Button ActionButton(string caption, Action action, bool enabled = true, float height = 45f)
        {
            GameObject go = NewChild("Action", _container);
            Image image = go.AddComponent<Image>();
            image.color = new Color(0.19f, 0.20f, 0.22f, 1f);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            button.interactable = enabled;
            ColorBlock colors = button.colors;
            colors.highlightedColor = new Color(0.85f, 0.68f, 0.35f, 1f);
            colors.pressedColor = new Color(0.68f, 0.48f, 0.18f, 1f);
            colors.disabledColor = new Color(0.35f, 0.35f, 0.35f, 0.55f);
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
            Height(go, height);
            return button;
        }

        private void TextLine(string value, int size, float height)
        {
            GameObject go = NewChild("Text", _container);
            Text text = go.AddComponent<Text>();
            StyleText(text, value, size);
            Height(go, height);
        }
        private static void StyleText(Text text, string value, int size)
        {
            text.text = value ?? "";
            text.supportRichText = false;
            text.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            text.fontSize = size;
            text.color = size >= 25 ? new Color(0.91f, 0.77f, 0.49f, 1f) : new Color(0.91f, 0.89f, 0.84f, 1f);
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
    }
}
