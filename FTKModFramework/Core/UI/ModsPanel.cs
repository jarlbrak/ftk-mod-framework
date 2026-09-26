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
    internal sealed partial class ModsPanel : uiScreen
    {
        private static ModsPanel _instance;
        private StartGameFE.MainScreen _openingTitle;
        internal static bool HasTitleOwner(StartGameFE.MainScreen title)
        {
            return _instance != null && _instance._openingTitle == title &&
                _instance.gameObject.activeInHierarchy && _instance.m_HasInputFocus &&
                uiScreen.gCurrent == _instance && FTKInput.Instance != null &&
                FTKInput.Instance.m_CurrentInputFocus == _instance;
        }
        internal static bool IsCurrentFocus(FTKInputFocus focus)
        {
            return _instance != null && focus == _instance && _instance.gameObject.activeInHierarchy;
        }
        private Transform _container;
        private Transform _rootContent;
        private string _view = "installed";
        private readonly ModsPanelNavigation _navigation = new ModsPanelNavigation();
        // Enabled-control count captured with a restored frame. Zero means "no restore in flight".
        private int _restoreFocusCount;
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
        private string _lastHotNotice;
        private bool _refreshPending;
        private readonly List<Texture2D> _previewTextures = new List<Texture2D>();
        private bool _showGallery;
        private int _imagePage;
        private bool _showAdvanced;
        private static Font _serif;
        private static readonly Color Paper = new Color(0.075f, 0.07f, 0.065f, 1f);
        private static readonly Color CardPaper = new Color(0.20f, 0.19f, 0.18f, 1f);
        private static readonly Color Ink = new Color(0.94f, 0.91f, 0.83f, 1f);
        private static readonly Color MutedInk = new Color(0.84f, 0.82f, 0.76f, 1f);
        private static readonly Color Gold = new Color(0.91f, 0.83f, 0.65f, 1f);
        private static readonly Color WarmBorder = new Color(0.78f, 0.75f, 0.68f, 1f);
        private static bool PanelBusy { get { return MarketplaceRuntime.Busy || FrameworkUpdateRuntime.Busy || HotReload.HotReloadCoordinator.Busy; } }
        private const string Alphabet = " abcdefghijklmnopqrstuvwxyz0123456789-";
        private static readonly string[] Categories = { "All", "items", "weapons", "proficiencies", "classes", "enemies", "encounters" };

        public static void Open()
        {
            if (HotReload.HotReloadBoundary.NavigationLocked) return;
            if (_instance != null && uiScreen.gCurrent == _instance && _instance.gameObject.activeInHierarchy) return;
            StartGameFE.MainScreen openingTitle = uiScreen.gCurrent as StartGameFE.MainScreen;
            CaptureNativeSkin();
            if (_instance == null) _instance = Build();
            else _instance.Render();
            _instance._openingTitle = openingTitle;
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
                scaler.matchWidthOrHeight = 1f;
                scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
                root.AddComponent<GraphicRaycaster>();
                Image background = root.AddComponent<Image>();
                background.color = Color.clear;
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

        internal static void InvalidateForHotReload()
        {
            if (_instance == null) return;
            _instance._entry = null; _instance._package = null;
            _instance.ClearPlan();
            ModsPanelNavigation.Frame discarded;
            _instance._navigation.Enter("installed", null, out discarded);
            _instance._view = "maintenance";
            _instance.ResetViewState("maintenance");
            foreach (Button button in _instance.GetComponentsInChildren<Button>(true)) button.interactable = false;
            _instance.ReleasePreviews();
            _instance.Refresh();
        }

        internal void ReleasePreviews()
        {
            foreach (Texture2D texture in _previewTextures) UnityEngine.Object.Destroy(texture);
            _previewTextures.Clear();
        }

        internal void Tick()
        {
            MarketplaceRuntime.Poll();
            FrameworkUpdateRuntime.Poll();
            string hotNotice = HotReload.HotReloadCoordinator.Notice;
            if (_lastHotNotice != hotNotice) { _lastHotNotice = hotNotice; Refresh(); }
            bool busy = PanelBusy;
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
            if (HotReload.HotReloadBoundary.NavigationLocked) return;
            ModsPanelNavigation.Frame restored;
            if (!_navigation.Enter(view, Capture(), out restored))
            {
                // Already here. Re-selecting a tab is a request to start the view over, not history.
                ResetViewState(view);
                Refresh();
                return;
            }
            // Every exit from a confirmation drops its plan, including a return to an ancestor.
            if (_view == "confirm") ClearPlan();
            if (restored != null) { Apply(restored); Refresh(); return; }
            _view = view;
            ResetViewState(view);
            _focusIndex = ModsPanelNavigation.DefaultFocus(view);
            Refresh();
        }

        /// <summary>Back is contextual: it leaves an in-view mode before it leaves the view.
        /// The footer label and the controller cancel button both read this, so they cannot diverge.</summary>
        private int BackKind()
        {
            // Both branches guard on the state that makes the mode visible, so Back never spends a
            // press on a mode the view is no longer rendering.
            if (_view == "updates" && _frameworkRelease != null && (_updateReview || _expandedNotes)) return 0;
            // Guard on a live selection: browsing filters clear the selection without clearing the
            // mode, and an unguarded check would spend a Back press with nothing on screen to leave.
            if ((_showAdvanced || _showGallery) && (_entry != null || _package != null)) return 1;
            return 2;
        }

        private void GoBack()
        {
            if (HotReload.HotReloadBoundary.NavigationLocked) return;
            int kind = BackKind();
            if (kind == 0)
            {
                if (_updateReview) _updateReview = false; else _expandedNotes = false;
                Refresh();
                return;
            }
            if (kind == 1)
            {
                _showAdvanced = false; _showGallery = false; _imagePage = 0;
                Refresh();
                return;
            }
            if (_view == "confirm") ClearPlan();
            ModsPanelNavigation.Frame parent = _navigation.Back();
            if (parent == null) { FTKInput.Instance.Close(this); return; }
            Apply(parent);
            Refresh();
        }

        private ModsPanelNavigation.Frame Capture()
        {
            return new ModsPanelNavigation.Frame { View = _view, Page = _page, DetailPage = _detailPage,
                Focus = _focusIndex, FocusCount = _controls.Count, ImagePage = _imagePage,
                Entry = _entry, Package = _package, Advanced = _showAdvanced, Gallery = _showGallery };
        }

        private void Apply(ModsPanelNavigation.Frame frame)
        {
            _view = frame.View;
            _page = frame.Page;
            _detailPage = frame.DetailPage;
            _entry = frame.Entry;
            _package = frame.Package;
            _showAdvanced = frame.Advanced;
            _showGallery = frame.Gallery;
            _imagePage = frame.ImagePage;
            _expandedNotes = false; _updateReview = false;
            _focusIndex = frame.Focus;
            _restoreFocusCount = frame.FocusCount;
            _message = "";
        }

        private void ResetViewState(string view)
        {
            _page = 0;
            _detailPage = 0;
            _showAdvanced = false; _showGallery = false; _imagePage = 0;
            _expandedNotes = false; _updateReview = false;
            _message = "";
            _restoreFocusCount = 0;
            if (ModsPanelNavigation.IsBrowse(view)) { _entry = null; _package = null; }
        }

        /// <summary>A prepared plan belongs to the operation the player was reviewing. Drop it the
        /// moment they leave, so no later confirmation can submit another operation's revision.</summary>
        private void ClearPlan()
        {
            _plan = null;
            _planSelection = null;
            _planIntent = null;
            _confirmOperation = null;
        }

        // Click and helper callbacks only request a render. FTKInputFocus continues using its
        // selected controls after OnControllerClick returns, so rebuilding inside that callback
        // can invalidate the controls still in use. Coalesce all requests at the end of the frame.
        private void Refresh() { _refreshPending = true; }

        internal void RenderPending()
        {
            if (!_refreshPending || !gameObject.activeInHierarchy) return;
            // Activation replaces resources used by the panel. Keep its controls inert until the
            // transaction finishes, then rebuild from the committed or rolled-back generation.
            if (HotReload.HotReloadCoordinator.Busy) return;
            try { Render(); }
            catch (Exception e)
            {
                Plugin.Log.LogError("Mods panel render failed in " + _view + ": " + e);
                _refreshPending = false;
                if (HotReload.HotReloadBoundary.NavigationLocked) return;
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
            _container = _rootContent;
            for (int i = _rootContent.childCount - 1; i >= 0; i--)
            {
                GameObject child = _rootContent.GetChild(i).gameObject;
                child.SetActive(false);
                UnityEngine.Object.Destroy(child);
            }
            _controls.Clear();
            ReleasePreviews();
            bool browse = ModsPanelNavigation.IsBrowse(_view);
            _rootContent.GetComponent<VerticalLayoutGroup>().enabled = !browse;
            _rootContent.GetComponent<Image>().enabled = !browse;
            _rootContent.GetComponent<Outline>().enabled = !browse;
            _rootContent.GetComponent<RectTransform>().sizeDelta = new Vector2(1580, browse ? 900 : 1010);
            if (browse) BuildNativeBrowse();
            else
            {
                StyleNativePanel(_rootContent.gameObject, false);
                TextLine("Mods", 38, 58);
                Rule();
                AddTabs();
                if (_view == "updates") BuildUpdates();
                else if (_view == "details") Details();
                else if (_view == "confirm") Confirmation();
                else if (_view == "search") Search();
                else if (_view == "maintenance") Maintenance();
                else if (_view == "settings") SettingsAndHelp();
                else if (_view == "saved-sets") SavedSets();
                else if (_view == "saved-set-review") SavedSetReview();
                AddFooter();
            }
            if (gameObject.activeInHierarchy)
            {
                UpdateSelectables();
                SetupNavigation();
                if (_restoreFocusCount > 0)
                {
                    _focusIndex = ModsPanelNavigation.ResolveFocus(_focusIndex, _restoreFocusCount, _controls.Count, ModsPanelNavigation.DefaultFocus(_view));
                    _restoreFocusCount = 0;
                }
                if (_controls.Count > 0) FTKInput.SetSelected(_controls[Math.Min(_focusIndex, _controls.Count - 1)]);
            }
        }

        private void Installed(bool components)
        {
            if (MarketplaceRuntime.RegistrationNotice != null)
                TextLine("Some content did not load correctly. Open Settings & Help for the error and recovery options.", 22, 76).color = Gold;
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
                ModEntry queued = new ModEntry(package.ModGuid, package.Name, package.Version, false, package.Description, package.Author, package.FrameworkVersion, true);
                queued.MarkManaged(package.PackageId);
                entries.Add(queued);
            }
            if (entries.Count == 0) TextLine(components ? "No extra components are needed." : "No mods installed yet.", 30, 90);
            if ((_entry == null && _package == null) && entries.Count > 0) SelectEntry(entries[0], false);
            int perPage = MarketplaceRuntime.RegistrationNotice == null ? 6 : 4;
            int pages = Math.Max(1, (entries.Count + perPage - 1) / perPage);
            _page = Math.Min(_page, pages - 1);
            for (int i = _page * perPage; i < Math.Min(entries.Count, (_page + 1) * perPage); i++)
            {
                ModEntry entry = entries[i];
                string origin = entry.IsManaged ? "COMMUNITY" : "INSTALLED MANUALLY";
                string summary = Short(entry.Description, 110);
                Card(entry.DisplayName, summary, origin, EntryState(entry),
                    _entry != null && _entry.Key == entry.Key, delegate { SelectEntry(entry, true); }, PreviewPaths(entry.IsManaged ? CurrentListing(MarketplaceRuntime.FindManaged(entry.Key) ?? DesiredPackage(entry.PackageId)) : null));
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
            if (!entry.FrameworkCompatible) return "BLOCKED / FRAMEWORK REQUIREMENT";
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
            MarketplaceResult catalog = MarketplaceRuntime.Catalog;
            if (MarketplaceRuntime.CatalogUnsupported && !PanelBusy)
            {
                Spacer(24);
                TextLine("The catalog needs a matching framework and helper", 30, 86);
                TextLine("Update or repair the framework and helper together from the launcher, then try Refresh. You can keep playing with everything in Installed.", 24, 104);
                if (!string.IsNullOrEmpty(MarketplaceRuntime.Notice))
                    TextLine(Short(MarketplaceRuntime.Notice, 240), 22, 110).color = Gold;
                return;
            }
            if (catalog == null || catalog.Status == "unavailable")
            {
                Spacer(24);
                TextLine(PanelBusy ? "Opening the community catalog..." : "The catalog is unavailable right now", 30, 86);
                TextLine(PanelBusy ? "Your installed mods stay available while we check." : "Try Refresh when you are online. You can keep playing with everything in Installed.", 24, 104);
                if (!PanelBusy && !string.IsNullOrEmpty(MarketplaceRuntime.Notice))
                    TextLine(Short(MarketplaceRuntime.Notice, 240), 22, 110).color = Gold;
                return;
            }
            if (catalog.Status == "offline") TextLine("Offline / saved " + FriendlyAge(catalog.CatalogAgeSeconds) + " ago", 20, 36);
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
                TextLine(filtered ? "Try a different search or category." : "Community packages will appear here once published. Manage your existing mods in Installed.", 24, 114);
                if (filtered) ActionButton("Clear filters", delegate { _search = ""; _category = 0; _page = 0; _entry = null; _package = null; Refresh(); });
            }
            if (_package == null && results.Count > 0) _package = results[0];
            int perPage = catalog.Status == "offline" ? 5 : 6;
            int pages = Math.Max(1, (results.Count + perPage - 1) / perPage);
            _page = Math.Min(_page, pages - 1);
            for (int i = _page * perPage; i < Math.Min(results.Count, (_page + 1) * perPage); i++)
            {
                PackageDescriptor package = results[i];
                PackageDescriptor active = MarketplaceRuntime.FindManaged(package.ModGuid);
                PackageDescriptor desired = DesiredPackage(package.PackageId);
                string state = active == null && desired != null ? "READY FOR NEXT LAUNCH" : active != null ? "INSTALLED" : package.Compatible ? "AVAILABLE" : "CHECK REQUIREMENTS";
                Card(package.Name, Short(package.Description, 100), Declared(package.Category).ToUpperInvariant(), state,
                    _package != null && _package.PackageId == package.PackageId,
                    delegate { _package = package; _entry = null; _detailPage = 0; _showAdvanced = false; _showGallery = false; _imagePage = 0; Refresh(); }, PreviewPaths(package));
            }
            PageButtons(pages);
        }

        private void LoadCatalog()
        {
            // MarketplaceRuntime.Start refuses a second operation and never calls back, so returning
            // here keeps a repeated Browse or Refresh from clearing the player's selection for nothing.
            // PanelBusy is deliberately stricter than MarketplaceRuntime.Busy: it also covers updates.
            if (PanelBusy) return;
            _package = null; _entry = null; _detailPage = 0; _showAdvanced = false; _showGallery = false; _imagePage = 0;
            MarketplaceRuntime.Start("catalog", null, false, delegate(MarketplaceResult result) { _message = result.Message ?? result.Status; Refresh(); });
            Refresh();
        }

        private void Details()
        {
            if (_package != null) _package = CurrentListing(_package);
            string name = _package != null ? _package.Name : _entry.DisplayName;
            List<string> previews = PreviewPaths(_package);
            if (previews.Count == 0 || _showAdvanced || _showGallery) TextLine(_package != null ? "COMMUNITY MOD" : "INSTALLED MANUALLY", 18, 24).color = Gold;
            Text title = TextLine(Short(name, 50), 36, 72);
            title.resizeTextForBestFit = true;
            title.resizeTextMinSize = 24;
            title.resizeTextMaxSize = 36;
            if (_entry != null && !_entry.FrameworkCompatible && !_showAdvanced)
            {
                TextLine("Not loaded this launch", 28, 40).color = Gold;
                TextLine("Declared framework: " + Declared(_entry.FrameworkVersion) + "\nRunning framework: " + Plugin.Version, 24, 70);
                TextLine(_entry.CompatibilityReason, 24, 120);
                TextLine("Your saved On / Off preference is unchanged. Ask the mod author for an updated manifest or use a compatible framework version.", 22, 100);
                LinkButton("Details and requirements", delegate { _showAdvanced = true; _detailPage = 0; Refresh(); });
                return;
            }
            if (_showGallery && previews.Count > 0)
            {
                _imagePage = Math.Min(_imagePage, previews.Count - 1);
                AddScreenshot(previews[_imagePage]);
                TextLine("Preview supplied by the mod author", 22, 48);
                if (previews.Count > 1) ActionButton("Next preview (" + (_imagePage + 1) + " of " + previews.Count + ")", delegate { _imagePage = (_imagePage + 1) % previews.Count; Refresh(); });
                return;
            }
            if (_showAdvanced)
            {
                AdvancedDetails();
                return;
            }
            TextLine(Short(OverviewCredit(), 64), 20, 28);
            if (previews.Count > 0)
            {
                GameObject hero = NewChild("Mod preview", _container);
                Height(hero, 175);
                AddPreviewImage(hero.transform, previews[0]);
                Button gallery = LinkButton("View previews (" + previews.Count + ")", delegate { _showGallery = true; _imagePage = 0; Refresh(); });
                gallery.GetComponent<LayoutElement>().minHeight = gallery.GetComponent<LayoutElement>().preferredHeight = 32;
                TextLine(Short(_package != null ? _package.Description : _entry.Description, 135), 22, 78);
            }
            else
            {
                string description = _package != null ? _package.Description : _entry.Description;
                TextLine(Short(description, 135), 24, 84);
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
                blocks.Add(Declared(_entry.Description) + "\nAuthor: " + Declared(_entry.Author) + "\nVersion: " + Declared(_entry.Version) + "\nLicense: Not declared.\nInstalled manually; marketplace actions cannot remove these files.");
            }
            else
            {
                PackageDescriptor p = _package;
                // The descriptor owns the listing. Show each field once, keeping full text
                // available here only when its overview preview needed shortening.
                StringBuilder details = new StringBuilder();
                if (p.Description != null && p.Description.Length > 135) details.Append(p.Description).Append("\n");
                string credit = OverviewCredit();
                if (credit.Length > 64) details.Append(credit).Append("\n");
                AppendDetailSection(details, "What it adds", p.ContentChanges);
                string compatibility = CompatibilityText(p);
                if (!string.IsNullOrEmpty(compatibility)) details.Append(compatibility).Append("\n");
                details.Append("Framework ").Append(Declared(p.FrameworkVersion));
                if (p.Platforms != null && p.Platforms.Length > 0) details.Append(" / ").Append(string.Join(", ", p.Platforms));
                details.Append("\n");
                if (p.Requirements != null && p.Requirements.Length > 0)
                    AppendDetailSection(details, "Requirements", p.Requirements);
                if (p.Dependencies != null && p.Dependencies.Length > 0)
                {
                    details.Append("Required components:\n");
                    foreach (PackageSelection dependency in p.Dependencies)
                        details.Append(DependencyName(dependency)).Append(" / v").Append(dependency.Version).Append("\n");
                }
                if (!string.IsNullOrEmpty(p.Changelog)) details.Append("Latest change\n").Append(p.Changelog).Append("\n");
                blocks.Add(details.ToString().TrimEnd());
            }
            if (_package == null && _entry != null)
                blocks.Add("Declared framework: " + Declared(_entry.FrameworkVersion) + "\n" +
                    (_entry.CompatibilityReason ?? "Meets the declared minimum within the same framework major. This is not a save or co-op guarantee."));
            List<string> pages = TextPages(blocks, 10);
            _detailPage = Math.Min(_detailPage, pages.Count - 1);
            TextLine(pages[_detailPage], 22, 280);
            if (pages.Count > 1) ActionButton("Next detail (" + (_detailPage + 1) + " of " + pages.Count + ")", delegate { _detailPage = (_detailPage + 1) % pages.Count; Refresh(); });
            if (_package != null && PreviewPaths(_package).Count > 0)
                LinkButton("View previews", delegate { _showAdvanced = false; _showGallery = true; _imagePage = 0; Refresh(); });
            if (_package != null && MarketplaceRuntime.FindManaged(_package.ModGuid) != null)
                LinkButton("Remove this community mod...", delegate { ReviewSelection(_package, true, false); }, !PanelBusy);
        }

        private string OverviewCredit()
        {
            if (_package == null) return "By " + Declared(_entry.Author) + " / v" + Declared(_entry.Version);
            return "By " + Declared(_package.Author) + " / v" + Declared(_package.Version) +
                (string.IsNullOrEmpty(_package.License) ? "" : " / " + _package.License);
        }

        private static void AppendDetailSection(StringBuilder text, string heading, string[] values)
        {
            if (values == null) return;
            bool addedHeading = false;
            foreach (string value in values)
            {
                if (string.IsNullOrEmpty(value)) continue;
                if (!addedHeading) { text.Append(heading).Append("\n"); addedHeading = true; }
                text.Append("• ").Append(value).Append("\n");
            }
        }

        /// <summary>Names a required component from a descriptor that declares the exact version.
        /// Falls back to the package id rather than inventing a name for an unknown dependency.</summary>
        private static string DependencyName(PackageSelection dependency)
        {
            PackageDescriptor selected = DesiredPackage(dependency.PackageId);
            if (selected != null && selected.Version == dependency.Version && !string.IsNullOrEmpty(selected.Name)) return selected.Name;
            if (MarketplaceRuntime.Catalog != null && MarketplaceRuntime.Catalog.Packages != null)
                foreach (PackageDescriptor candidate in MarketplaceRuntime.Catalog.Packages)
                    if (candidate.PackageId == dependency.PackageId && candidate.Version == dependency.Version && !string.IsNullOrEmpty(candidate.Name)) return candidate.Name;
            return dependency.PackageId;
        }

        private void PackageActions(PackageDescriptor package)
        {
            bool titleActivation = HotReload.HotReloadBoundary.Enabled && HotReload.HotReloadBoundary.SealReason == null && !HotReload.HotReloadCoordinator.Faulted && package.ModGuid == "com.ftkmf.paladin";
            string reason = ModFrameworkCompatibility.Reason(package.FrameworkVersion, Plugin.Version);
            string installedReason = ModRegistry.CompatibilityReasonFor(package.ModGuid, package.Version);
            if (installedReason != null) reason = installedReason;
            if (reason != null)
            {
                TextLine(Short(reason, 170), 22, 96).color = Gold;
                return;
            }
            PackageDescriptor active = MarketplaceRuntime.FindManaged(package.ModGuid);
            PackageDescriptor desired = DesiredPackage(package.PackageId);
            bool queued = MarketplaceRuntime.Pending != null && (active == null ? desired != null : desired == null || desired.Version != active.Version || desired.Enabled != active.Enabled);
            if (queued)
            {
                string state = titleActivation
                    ? (active == null ? "Install prepared; review and apply from the initial title" : desired == null ? "Removal prepared; review and apply from the initial title" : desired.Version != active.Version ? "Update prepared; review and apply from the initial title" : "Now: " + OnOff(active.Enabled) + " / Prepared: " + OnOff(desired.Enabled))
                    : (active == null ? "Ready to install when you restart" : desired == null ? "Will be removed after restart" : desired.Version != active.Version ? "Update ready for next launch" : "Now: " + OnOff(active.Enabled) + " / After restart: " + OnOff(desired.Enabled));
                TextLine(state, 22, 44).color = Gold;
                PrimaryButton(titleActivation ? "Review prepared changes" : "Review next-launch changes", delegate { Navigate("maintenance"); });
                LinkButton(active == null ? "Cancel this install" : "Undo this mod's change", delegate {
                    if (active == null) ReviewSelection(package, true, false);
                    else ReviewSelection(active, false, active.Enabled);
                }, !PanelBusy);
                return;
            }
            if (package.Revoked) TextLine("This mod is no longer offered in the catalog. An installed copy stays until you choose to remove it.", 22, 76).color = Gold;
            else if (active == null && !package.Compatible) TextLine(Short(package.CompatibilityReason, 125), 22, 76).color = Gold;
            else TextLine(active != null ? (active.Enabled ? "On for this adventure" : "Currently turned off") : titleActivation ? "Free / Prepare, then apply from the initial title" : "Free / Changes apply after restart", 22, 38);
            if (active == null || active.Version != package.Version)
                PrimaryButton(active == null ? "Install..." : "Update to " + package.Version + "...", delegate { ReviewSelection(package, false, true); }, package.Compatible && !package.Revoked && !PanelBusy);
            else PrimaryButton(titleActivation
                ? (active.Enabled ? "Turn off..." : "Turn on...") : (active.Enabled ? "Turn off after restart..." : "Turn on after restart..."), delegate { ReviewSelection(active, false, !active.Enabled); }, !PanelBusy);
        }

        private static string Bullets(string[] values)
        {
            StringBuilder text = new StringBuilder();
            for (int i = 0; i < Math.Min(3, values.Length); i++) text.Append("• ").Append(Short(values[i], 85)).Append("\n");
            return text.ToString();
        }

        private static string CompatibilityText(PackageDescriptor package)
        {
            string requirement = ModFrameworkCompatibility.Reason(package.FrameworkVersion, Plugin.Version);
            if (requirement != null) return requirement;
            if (package.Compatible) return null;
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

        /// <summary>Pages a set of text blocks. linesPerPage must match the height the caller gives
        /// the page, because Text truncates rather than scrolls.</summary>
        private static List<string> TextPages(List<string> blocks, int linesPerPage = 8)
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
                        if (++lines == linesPerPage) { pages.Add(page.ToString()); page.Length = 0; lines = 0; }
                    } while (rest.Length > 0);
                }
                if (page.Length > 0) pages.Add(page.ToString());
            }
            return pages;
        }

        private static List<string> PreviewPaths(PackageDescriptor package)
        {
            List<string> paths = new List<string>();
            if (package != null && package.ScreenshotPaths != null)
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
                if (!MarketplaceProtocol.IsScreenshotPath(MarketplaceRuntime.StateRoot, path)) throw new IOException("Preview path is not approved.");
                bytes = File.ReadAllBytes(path);
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
            bool titleActivation = HotReload.HotReloadBoundary.Enabled && HotReload.HotReloadBoundary.SealReason == null && !HotReload.HotReloadCoordinator.Faulted && package.ModGuid == "com.ftkmf.paladin";
            _planIntent = titleActivation
                ? (remove ? "Prepare removal of " + package.Name + "." : "Prepare " + package.Name + " set to " + OnOff(enabled) + ".")
                : (remove ? "Remove " + package.Name + " from your next-launch selection." : "Set " + package.Name + " to " + OnOff(enabled) + " for your next launch.");
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
            if (string.IsNullOrEmpty(_confirmOperation) || (_confirmOperation == "prepare" && (_plan == null || _planSelection == null)))
            {
                // Defensive: a review without its plan must never offer to submit a stale revision.
                TextLine("This review is no longer current. Open the mod again and choose the change you want to make.", 25, 120);
                return;
            }
            bool titleActivation = HotReload.HotReloadBoundary.Enabled && HotReload.HotReloadBoundary.SealReason == null && !HotReload.HotReloadCoordinator.Faulted && _confirmOperation == "prepare";
            if (_plan != null && _plan.Packages != null)
                foreach (PackageDescriptor package in _plan.Packages) if (package.ModGuid != "com.ftkmf.paladin") titleActivation = false;
            TextLine(titleActivation ? "Prepare these changes first, then explicitly apply them from the initial title. Preparing alone does not change your running mods." : "Nothing changes in your current adventure. These choices apply when you next launch the game.", 25, 68);
            if (_confirmOperation == "prepare")
            {
                List<MarketplacePlanEntry> entries = _plan == null || _plan.Plan == null ? new List<MarketplacePlanEntry>() : _plan.Plan;
                List<string> review = new List<string>();
                review.Add(_planIntent ?? "Review your next-launch selection.");
                StringBuilder desired = new StringBuilder(titleActivation ? "Prepared community mods:\n" : "Community mods next launch:\n");
                if (_plan == null || _plan.Packages == null || _plan.Packages.Count == 0) desired.Append("None. Included and manually installed mods keep their saved settings.");
                else foreach (PackageDescriptor item in _plan.Packages)
                {
                    desired.Append(item.Name ?? item.PackageId).Append(" ").Append(item.Version).Append(" / ").Append(OnOff(item.Enabled)).Append("\n");
                }
                review.Add(desired.ToString());
                StringBuilder delta = new StringBuilder("Changes from this launch:\n");
                foreach (MarketplacePlanEntry entry in entries)
                {
                    delta.Append(entry.Action).Append(": ").Append(entry.Name ?? entry.PackageId).Append(" ").Append(entry.FromVersion).Append(" -> ").Append(entry.ToVersion).Append(entry.Dependency ? " (required component)" : "").Append("\n");
                    if (!string.IsNullOrEmpty(entry.Notice)) delta.Append("  Note: ").Append(entry.Notice).Append("\n");
                }
                if (entries.Count == 0) delta.Append("Current community selection unchanged; queued choices cleared.");
                review.Add(delta.ToString());
                List<string> pages = TextPages(new List<string> { string.Join("\n", review.ToArray()) });
                _page = Math.Min(_page, pages.Count - 1);
                TextLine(pages[_page], 25, 250);
                PageButtons(pages.Count);
            }
            else TextLine(_confirmOperation == "rollback" ? "Restore the previous community mod set on your next launch. Saves are not rolled back, and current content stays loaded." : "Discard prepared community downloads and keep the current community selection. Included and manual mod toggles are unchanged.", 25, 170);
            TextLine("Existing saves may require their original mod set. Start a new run after changing class mods. No saves are modified, migrated, deleted or automatically backed up.", 24, 106);
            PrimaryButton(_confirmOperation == "prepare" ? (titleActivation ? "Prepare changes" : "Save for next launch") : _confirmOperation == "rollback" ? "Restore on next launch" : "Discard community changes", delegate {
                MarketplaceRuntime.Start(_confirmOperation, _confirmOperation == "prepare" ? _planSelection : null, false, delegate(MarketplaceResult result) { Navigate("maintenance"); _message = result.Ok && PendingCount() == 0 ? "Your selection is saved. No gameplay changes will apply." : result.Message ?? result.Status; Refresh(); }, _confirmOperation == "prepare" && _plan != null ? _plan.PlanRevision : null);
                Refresh();
            }, !PanelBusy);
        }

        private void Maintenance()
        {
            if (HotReload.HotReloadBoundary.Enabled)
            {
                TextLine("Title-screen activation", 30, 44);
                TextLine(HotReload.HotReloadCoordinator.Notice, 22, 75);
                if (HotReload.HotReloadCoordinator.Faulted)
                    PrimaryButton("Quit game to recover", delegate { Application.Quit(); }, true, true);
                string blocked = HotReload.HotReloadCoordinator.UnavailableReason();
                if (blocked != null) TextLine(blocked, 20, 60);
                PrimaryButton("Apply prepared mods now", delegate {
                    HotReload.HotReloadCoordinator.ApplyPending(delegate { Refresh(); }); Refresh();
                }, blocked == null && MarketplaceRuntime.Pending != null);
            }
            bool titleActivation = HotReload.HotReloadBoundary.Enabled && HotReload.HotReloadBoundary.SealReason == null && !HotReload.HotReloadCoordinator.Faulted;
            if (MarketplaceRuntime.Pending != null)
                foreach (PackageDescriptor package in MarketplaceRuntime.Pending.Packages) if (package.ModGuid != "com.ftkmf.paladin") titleActivation = false;
            TextLine(titleActivation ? "Prepared changes" : "Your next launch", 36, 54);
            int pendingCount = PendingCount();
            TextLine(pendingCount == 0 ? (MarketplaceRuntime.Pending == null ? "There are no changes waiting to apply." : "Your selection is saved. No gameplay changes will apply.") : pendingCount + " change(s) are saved. Your current adventure has not changed.", 25, 48);
            List<string> lines = ModsPanelNextLaunch.Lines(MarketplaceRuntime.Active, MarketplaceRuntime.Pending, ModRegistry.Entries);
            if (titleActivation && lines.Count > 0) lines[0] = "Community mods after you apply:";
            List<string> pages = TextPages(new List<string> { string.Join("\n", lines.ToArray()) });
            if (pages.Count > 0)
            {
                _page = Math.Min(_page, pages.Count - 1);
                // 210 was short of a full eight-line page and truncated it. Size to the content.
                TextLine(pages[_page], 22, PageHeight(pages, 22));
                PageButtons(pages.Count);
            }
            if (pendingCount > 0)
            {
                TextLine("Start a new run after changing class mods. Existing saves may need their original mod set.", 22, 40);
                PrimaryButton("Quit and apply on next launch", delegate { Application.Quit(); }, !PanelBusy);
                TextLine("Then start the game again from Steam or the launcher to load these changes.", 22, 32);
            }
            if (MarketplaceRuntime.Pending != null)
                LinkButton("Discard community download changes...", delegate { _confirmOperation = "cancel"; Navigate("confirm"); }, !PanelBusy);
            bool preferences = false;
            foreach (ModEntry entry in ModRegistry.Entries) if (!entry.IsManaged && entry.PendingEnabled.HasValue) preferences = true;
            if (preferences) TextLine("Undo included or manual mod changes in Installed.", 22, 32);
        }

        private void SettingsAndHelp()
        {
            TextLine("Settings & Help", 36, 56);
            List<string> blocks = new List<string>();
            // The newest result first, so an export path or a failed operation is the page you land on.
            if (_message.Length > 0) blocks.Add(_message);
            blocks.Add(MarketplaceRuntime.RegistrationNotice != null
                ? MarketplaceRuntime.RegistrationNotice + "\nBepInEx/LogOutput.log names the content that failed. Turn that mod off in Installed, or restore your previous community mods below."
                : "Installed content is selected for this launch. Changes never unload it mid-game.");
            blocks.Add(MarketplaceRuntime.Notice ?? "No marketplace status to report.");
            if (HotReload.ClassPreferences.RecoveryFaulted) blocks.Add(HotReload.ClassPreferences.RecoveryNotice);
            if (MarketplaceRuntime.LeaseFailure != null) blocks.Add(MarketplaceRuntime.LeaseFailure);
            blocks.Add(HotReload.HotReloadBoundary.Enabled
                ? "Title-screen activation is on. Apply supported packages before starting an adventure. Each exact mod set has its own save library. Multiplayer is unavailable in this mode."
                : "Title-screen activation is off for this session. " + (HotReload.HotReloadBoundary.EligibilityNotice ?? "Enable it below for your next launch."));
            // Cap a status page at six lines; the rest goes on the next page, and PageHeight sizes
            // the box to whichever page is tallest.
            List<string> pages = TextPages(blocks, 6);
            _detailPage = Math.Min(_detailPage, pages.Count - 1);
            TextLine(pages[_detailPage], 23, PageHeight(pages, 23));
            if (pages.Count > 1) ActionButton("Next status detail (" + (_detailPage + 1) + " of " + pages.Count + ")", delegate { _detailPage = (_detailPage + 1) % pages.Count; Refresh(); });
            ActionButton("Title-screen activation next launch: " + (Plugin.EnableTitleScreenActivation.Value ? "On" : "Off"), delegate {
                Plugin.EnableTitleScreenActivation.Value = !Plugin.EnableTitleScreenActivation.Value;
                Plugin.Instance.Config.Save(); Refresh();
            }, !PanelBusy);
            ActionButton("Automatic bug reports: " + (Plugin.AutomaticBugReports.Value ? "On" : "Off"), delegate {
                Plugin.AutomaticBugReports.Value = !Plugin.AutomaticBugReports.Value;
                Plugin.Instance.Config.Save(); Refresh();
            }, !PanelBusy);
            TextLine("When on, detected errors and unexpected exits send filtered diagnostics to public GitHub issues without asking. Turn off to stop new automatic sends. Report Bugs remains available.", 23, 76);
            ActionButton("Review prepared changes", delegate { Navigate("maintenance"); });
            ActionButton("Saved mod sets", delegate { Navigate("saved-sets"); }, !PanelBusy);
            ActionButton("Restore previous mods...", delegate { _confirmOperation = "rollback"; Navigate("confirm"); }, MarketplaceRuntime.PreviousAvailable && !PanelBusy);
            TextLine(MarketplaceRuntime.PreviousAvailable
                ? "Restores your previous community mod selection on the next launch. Included mods, manual mods and saves are unchanged. You review the change first."
                : "No previous community mod selection is saved yet. Restore becomes available once you replace an active selection.", 23, 62);
            ActionButton("Export mod list", delegate {
                MarketplaceRuntime.Start("export", null, false, delegate(MarketplaceResult result) {
                    _message = result.Ok ? "Mod list saved to: " + result.ExportPath : "Could not export the mod list: " + Declared(result.Message ?? result.Status);
                    _detailPage = 0;
                    Refresh();
                });
                Refresh();
            }, !PanelBusy);
            TextLine("Saves a list of your community mods to share when you ask for help. Manual mods are not fully fingerprinted, so it does not prove save or co-op compatibility.", 23, 62);
            TextLine("Required components used by community mods are listed under Components in Installed. To repair marketplace access, use Install / Repair in the launcher; your manually installed files are not removed.", 23, 76);
        }

        /// <summary>Height for a paged text block, sized to the tallest page so the controls below
        /// it do not move as pages turn and no page reserves space it does not use. TextPages ends
        /// every line with a newline, so a page renders one more line than it has separators.</summary>
        private static float PageHeight(List<string> pages, int size)
        {
            int longest = 0;
            foreach (string page in pages)
            {
                int lines = 0;
                foreach (char c in page) if (c == '\n') lines++;
                if (lines > longest) longest = lines;
            }
            return (longest + 1) * (size + 5);
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
            Button browse = ActionButton("Browse", delegate { Navigate("discover"); if (MarketplaceRuntime.Catalog == null && !PanelBusy) LoadCatalog(); }, true, 48);
            SetWidth(browse.gameObject, 160);
            if (_view == "discover") Border(browse.gameObject, Gold, 2);
            Button installed = ActionButton("Installed", delegate { Navigate("installed"); }, true, 48);
            SetWidth(installed.gameObject, 170);
            if (_view == "installed") Border(installed.gameObject, Gold, 2);
            Button updates = ActionButton("Updates", delegate { Navigate("updates"); if (FrameworkUpdateRuntime.State == null && !PanelBusy) LoadUpdates("refresh"); }, true, 48);
            SetWidth(updates.gameObject, 170);
            if (_view == "updates") Border(updates.gameObject, Gold, 2);
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
            // Browse and Updates fill the body with a fixed-height column pair. The stacked views
            // size to their content, so absorb the remainder here and keep the footer at the
            // bottom edge instead of floating wherever the content happens to end.
            if (!ModsPanelNavigation.IsBrowse(_view) && _view != "updates")
            {
                GameObject fill = NewChild("Footer fill", _container);
                LayoutElement grow = fill.AddComponent<LayoutElement>();
                grow.minHeight = grow.preferredHeight = 0;
                grow.flexibleHeight = 1;
            }
            Rule();
            Transform row = HorizontalRow("Footer", 60);
            _container = row;
            bool titleActivation = HotReload.HotReloadBoundary.Enabled && HotReload.HotReloadBoundary.SealReason == null && !HotReload.HotReloadCoordinator.Faulted;
            if (MarketplaceRuntime.Pending != null)
                foreach (PackageDescriptor package in MarketplaceRuntime.Pending.Packages) if (package.ModGuid != "com.ftkmf.paladin") titleActivation = false;
            if (_view == "details" && _entry != null && !_entry.IsManaged) titleActivation = false;
            if (MarketplaceRuntime.Pending == null)
                foreach (ModEntry entry in ModRegistry.Entries) if (!entry.IsManaged && entry.PendingEnabled.HasValue) titleActivation = false;
            string notice = PanelBusy ? "Working... Your current mods stay unchanged."
                : titleActivation ? (MarketplaceRuntime.Pending != null ? "Community changes are prepared. Review, then apply from the initial title." : "Prepare supported community changes, then apply from the initial title.")
                : PendingCount() > 0 ? "Changes are saved for next launch." : MarketplaceRuntime.Pending != null ? "Your selection is saved. No gameplay changes will apply." : "Your installed mods stay unchanged until you restart.";
            if (_view == "updates") notice = Short(FrameworkUpdateRuntime.Notice, 120);
            else if (_message.Length > 0) notice = Short(_message, 120);
            Text status = TextLine(notice, 21, 58);
            status.GetComponent<LayoutElement>().flexibleWidth = 1;
            if (PanelBusy)
            {
                Button cancel = LinkButton("Cancel operation", delegate { if (FrameworkUpdateRuntime.Busy) FrameworkUpdateRuntime.Cancel(); else MarketplaceRuntime.CancelRunning(); Refresh(); });
                cancel.interactable = !HotReload.HotReloadCoordinator.Busy;
                SetWidth(cancel.gameObject, 200);
            }
            else if (_view == "installed")
            {
                Button components = LinkButton("Components", delegate { Navigate("components"); });
                SetWidth(components.gameObject, 200);
            }
            // Reserve the slot so starting or finishing an operation never slides Settings or Back
            // under the pointer, and never adds a controller stop that does nothing.
            else FooterSlot(200);
            if (_view == "settings") FooterSlot(205);
            else
            {
                Button settings = LinkButton("Settings & Help", delegate { Navigate("settings"); });
                SetWidth(settings.gameObject, 205);
            }
            int kind = BackKind();
            Button back = LinkButton(kind == 0 ? "Back to versions" : kind == 1 ? "Back to overview" : _navigation.CanGoBack ? "Back" : "Back to title", GoBack);
            back.interactable = !HotReload.HotReloadBoundary.NavigationLocked;
            SetWidth(back.gameObject, 200);
            m_ButtonOnCancel = back;
            _container = _rootContent;
        }

        private void FooterSlot(float width)
        {
            GameObject slot = NewChild("Footer slot", _container);
            Height(slot, 38);
            SetWidth(slot, width);
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
                TextLine("Required components", 36, 104);
                TextLine("Some community mods need supporting components. They appear here when needed, separate from the mods you choose to play.", 25, 170);
                return;
            }
            Spacer(56);
            TextLine("Select a mod on the left to see its artwork, features and requirements.\n\nBrowse community content or manage your existing mods in Installed.", 25, 170);
        }

        private void Card(string title, string summary, string category, string state, bool selected, Action action, List<string> previews)
        {
            if (ModsPanelNavigation.IsBrowse(_view))
            {
                Button row = ActionButton(title, action, true, 50);
                StyleNativeButton(row, true, selected);
                Text label = row.GetComponentInChildren<Text>();
                StyleNativeText(label, true);
                label.alignment = TextAnchor.MiddleCenter;
                label.fontSize = 31;
                label.resizeTextForBestFit = true;
                label.resizeTextMinSize = 22;
                label.resizeTextMaxSize = 31;
                return;
            }
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
        private Button PrimaryButton(string title, Action action, bool enabled = true, bool allowDuringActivation = false)
        {
            Button button = ActionButton(title, action, enabled, 54, allowDuringActivation);
            button.GetComponent<Image>().color = new Color(0.95f, 0.87f, 0.67f, 1f);
            button.GetComponentInChildren<Text>().alignment = TextAnchor.MiddleCenter;
            Border(button.gameObject, Gold, 1);
            StyleNativeButton(button, false);
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
            StyleNativeButton(button, false);
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

        private Button ActionButton(string caption, Action action, bool enabled = true, float height = 45f, bool allowDuringActivation = false)
        {
            GameObject go = NewChild("Action", _container);
            Image image = go.AddComponent<Image>();
            image.color = CardPaper;
            Border(go, WarmBorder, 1);
            Button button = go.AddComponent<Button>();
            button.targetGraphic = image;
            enabled = enabled && (allowDuringActivation || !HotReload.HotReloadBoundary.NavigationLocked);
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
                if (!enabled || (!allowDuringActivation && HotReload.HotReloadBoundary.NavigationLocked)) return;
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
            StyleNativeButton(button, false);
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
            StyleNativeText(text, size >= 27);
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
