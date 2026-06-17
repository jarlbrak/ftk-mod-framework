using System;
using UnityEngine;
using UnityEngine.UI;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core.UI
{
    /// <summary>
    /// Runtime Mods toggle panel opened by the title-screen "Mods" button (see
    /// <see cref="MainScreen_OnSetFocus_Patch"/>). Renders one stock UnityEngine.UI.Toggle row per
    /// <see cref="ModRegistry.Entries"/> item; flipping a row persists immediately via
    /// <see cref="ModRegistry.SetEnabled"/> (PlayerPrefs for data mods, EnableSampleContent for the demo).
    /// A change takes effect on the next load, hence the fixed "Changes apply on restart." banner.
    ///
    /// WHY A uiScreen SUBCLASS: deriving from uiScreen (one level: uiScreen : FTKInputFocus : MonoBehaviour,
    /// verified against the real Assembly-CSharp) lets the panel ride the game's own focus pipeline:
    /// FTKInput.SetFocus(this) activates the subtree and arms keyboard/controller nav, and the inherited
    /// cancel path (m_Cancel = OnButton when m_ButtonOnCancel is set) gives the B-button / Back behaviour
    /// for free.
    ///
    /// RUNTIME-CONSTRUCTION SAFETY (verified): FTKInputFocus.Awake() only dereferences serialized selectable
    /// lists, which are non-null empty (new List&lt;&gt;()) on a fresh AddComponent, plus an
    /// (m_SelectableParent != null) branch that calls m_SelectableParent.GetComponentsInChildren&lt;FTKSelectable&gt;()
    /// (an empty array when our container has no rows, never null). We AddComponent onto an INACTIVE root, so
    /// Awake runs only when SetFocus activates it, by which point m_SelectableParent and the rows are already
    /// in place. No serialized-field NPE path exists. We never instantiate a serialized uiScreen prefab; we
    /// build the whole hierarchy in code.
    ///
    /// DEFENSE: the entire build is wrapped. If anything throws mid-build the partial root is destroyed and
    /// Open() rethrows to its caller, which logs and leaves the title screen untouched (no panel, no
    /// soft-lock). If the build succeeds, a Back button (wired to m_ButtonOnCancel and to FTKInput.Close) is
    /// always present, so the player can always back out to the title.
    /// </summary>
    internal sealed class ModsPanel : uiScreen
    {
        // The fixed banner text. Exact literal per the spec (do not localize; a non-"STR_" literal is left
        // untouched by FTKLocalizationUI).
        private const string BannerText = "Changes apply on restart.";
        private const string EmptyText = "No mods installed.";

        // Built lazily on first Open() and cached for the process lifetime. ModRegistry.Entries is fixed after
        // load, so the row set never changes; we build once and just re-show on subsequent clicks.
        private static ModsPanel _instance;

        /// <summary>
        /// Entry point for the title button. Builds the panel on first call (cached thereafter), then shows it
        /// through the game's focus pipeline. Any build failure propagates to the caller (ModsButtonPatch),
        /// which logs it; on failure the title screen is left exactly as it was.
        /// </summary>
        public static void Open()
        {
            if (_instance == null)
                _instance = Build();

            // _saveState: true restores the prior focus (the title MainScreen) when this panel closes, exactly
            // like uiOptionsMain.OnGameOptions opens uiOptionsMenu.m_GameOptions.
            FTKInput.Instance.SetFocus(_instance, null, true, null, false);
        }

        /// <summary>
        /// Construct the full panel hierarchy in code and return the live component. Throws on any failure
        /// after destroying whatever partial root was created, so the caller can treat a throw as "no panel".
        /// The returned root starts INACTIVE: SetFocus (called by Open) activates it.
        /// </summary>
        private static ModsPanel Build()
        {
            GameObject root = null;
            try
            {
                // Root canvas: a screen-space overlay drawn above the title with a high sorting order, plus a
                // raycaster so the toggles receive clicks and a scaler so it sizes sensibly. Created inactive
                // so FTKInputFocus.Awake is deferred until SetFocus activates the (by then fully built) object.
                root = new GameObject("FtkmfModsPanel");
                root.SetActive(false);
                UnityEngine.Object.DontDestroyOnLoad(root);

                Canvas canvas = root.AddComponent<Canvas>();
                canvas.renderMode = RenderMode.ScreenSpaceOverlay;
                canvas.sortingOrder = 30000; // above the title-screen canvases.

                CanvasScaler scaler = root.AddComponent<CanvasScaler>();
                scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
                scaler.referenceResolution = new Vector2(1920f, 1080f);

                root.AddComponent<GraphicRaycaster>();

                // Full-rect semi-opaque background: reads as a modal screen and blocks clicks to the title
                // behind it.
                Image bg = root.AddComponent<Image>();
                bg.color = new Color(0f, 0f, 0f, 0.85f);
                StretchFull(bg.rectTransform);

                ModsPanel panel = root.AddComponent<ModsPanel>();

                // Vertical container that holds the banner, the rows, and the Back button. This is also the
                // panel's m_SelectableParent, so the inherited nav auto-scan walks these children in order.
                GameObject containerGo = NewUIChild("Container", root.transform);
                RectTransform containerRt = containerGo.GetComponent<RectTransform>();
                CenterBox(containerRt, 640f, 720f);

                VerticalLayoutGroup layout = containerGo.AddComponent<VerticalLayoutGroup>();
                layout.spacing = 12f;
                layout.childAlignment = TextAnchor.UpperCenter;
                layout.childForceExpandWidth = true;
                layout.childForceExpandHeight = false;
                layout.childControlWidth = true;
                layout.childControlHeight = true;
                layout.padding = new RectOffset(24, 24, 24, 24);

                AddBanner(containerGo.transform, BannerText);

                // One toggle row per registered mod. If the registry is empty (should not happen: the demo is
                // always registered), show a "No mods installed." label instead of rows. Either way the Back
                // button below is always added.
                System.Collections.ObjectModel.ReadOnlyCollection<ModEntry> entries = ModRegistry.Entries;
                if (entries == null || entries.Count == 0)
                {
                    AddBanner(containerGo.transform, EmptyText);
                }
                else
                {
                    Toggle source = ResolveToggleSource();
                    for (int i = 0; i < entries.Count; i++)
                        AddModRow(containerGo.transform, source, entries[i]);
                }

                // Back button: explicit click-to-close AND the wiring that makes the inherited cancel/B-button
                // path work. uiScreen.SetFocus sets m_Cancel = OnButton only when m_ButtonOnCancel is non-null,
                // and OnButton invokes m_ButtonOnCancel.onClick, so one onClick listener serves both paths.
                Button back = AddBackButton(containerGo.transform);
                panel.m_ButtonOnCancel = back;

                // Nav: assign the container as the selectable parent and ask for vertical auto-scan. The
                // inherited FTKInputFocus.SetupNavigation() (deferred in Update, gated by m_IsUpdateSelected
                // which base SetFocus sets) then wires up/down by sibling order across every FTKSelectable row.
                // FALLBACK (documented, not used): wire each adjacent pair manually via
                // uiItemIcon.SetNavigatePairUpDown(upRow.transform, downRow.transform).
                panel.m_SelectableParent = containerGo.transform;
                panel.m_NavigationSetup = FTKInputFocus.NavigationSetup.Vertical;

                Plugin.Log.LogInfo("[ftkmf] Mods panel built (" +
                    ((entries == null) ? 0 : entries.Count) + " row(s)).");
                return panel;
            }
            catch (Exception)
            {
                // Leave nothing half-built behind, then rethrow so Open's caller logs and the title is intact.
                if (root != null)
                    UnityEngine.Object.Destroy(root);
                throw;
            }
        }

        /// <summary>
        /// Build one mod row by cloning the live autosave Toggle, relabelling it, reflecting the entry's current
        /// Enabled state, and wiring onValueChanged to persist via ModRegistry.SetEnabled. CRITICAL ORDER:
        /// isOn is set BEFORE the listener is attached so building the panel does not fire a no-op persist; and
        /// the entry Key is copied into a per-row local so the closure binds THIS row's key (no loop-variable
        /// capture bug).
        /// </summary>
        private static void AddModRow(Transform parent, Toggle source, ModEntry entry)
        {
            if (source == null)
            {
                // No clonable Toggle (Options not reachable yet): fall back to a plain label so the row still
                // appears and the panel stays usable. Persistence is unavailable without a real toggle, which is
                // logged once at ResolveToggleSource.
                AddBanner(parent, RowLabel(entry) + "  (toggle unavailable)");
                return;
            }

            // Per-row local: the closure below must capture THIS key, not the shared loop entry.
            string key = entry.Key;
            bool initial = entry.Enabled;

            GameObject row = UnityEngine.Object.Instantiate(source.gameObject, parent);
            row.name = "ModRow_" + key;
            row.SetActive(true); // the source may be inactive at rest; the clone must be visible.

            // The cloned options row carries the source's sizing, which does not stack in our
            // VerticalLayoutGroup: rows overlapped with no height (the second row's toggle hid behind the
            // first). Give each row an explicit height so the layout puts every mod on its own line.
            LayoutElement rowLayout = row.GetComponent<LayoutElement>();
            if (rowLayout == null) rowLayout = row.AddComponent<LayoutElement>();
            rowLayout.minHeight = 48f;
            rowLayout.preferredHeight = 48f;
            rowLayout.flexibleHeight = 0f;

            Toggle toggle = row.GetComponent<Toggle>();
            if (toggle == null)
            {
                // Defensive: the clone source is a Toggle, so this should never happen. If it does, do not leave
                // a dead row: drop it and show a label instead.
                UnityEngine.Object.Destroy(row);
                AddBanner(parent, RowLabel(entry));
                return;
            }

            // Label child: scene-authored Text on the cloned subtree. Resolve including inactive children; add
            // one if the clone lacks it. A non-"STR_" literal is left untouched by FTKLocalizationUI.
            Text label = row.GetComponentInChildren<Text>(true);
            if (label != null)
            {
                label.text = RowLabel(entry);
                label.color = Color.white;             // the source label can render faint; normalize it
                label.alignment = TextAnchor.MiddleLeft;
            }
            else
            {
                AddLabelChild(row.transform, RowLabel(entry));
            }

            // Reflect current state, THEN wire the listener. Order matters: setting isOn can invoke
            // onValueChanged, so we set it first (while no listener is attached) to avoid a spurious persist.
            toggle.onValueChanged.RemoveAllListeners();
            toggle.isOn = initial;
            toggle.onValueChanged.AddListener(delegate(bool value) { ModRegistry.SetEnabled(key, value); });
        }

        /// <summary>
        /// Resolve the live autosave Toggle to clone, guarding every hop. Returns null (with one warning) if
        /// any hop is missing, e.g. before the title scene has built uiOptionsMenu. Path verified against the
        /// decompile: uiOptionsMenu.Instance (static) -&gt; m_GameOptions (uiOptionsGame) -&gt; m_AutoSave (Toggle).
        /// </summary>
        private static Toggle ResolveToggleSource()
        {
            uiOptionsMenu menu = uiOptionsMenu.Instance;
            if (menu == null)
            {
                Plugin.Log.LogWarning("[ftkmf] Mods panel: uiOptionsMenu.Instance is null; rows fall back to " +
                    "plain labels (toggles unavailable this session).");
                return null;
            }

            uiOptionsGame game = menu.m_GameOptions;
            if (game == null)
            {
                Plugin.Log.LogWarning("[ftkmf] Mods panel: uiOptionsMenu.m_GameOptions is null; rows fall back " +
                    "to plain labels.");
                return null;
            }

            Toggle autosave = game.m_AutoSave;
            if (autosave == null)
            {
                Plugin.Log.LogWarning("[ftkmf] Mods panel: m_GameOptions.m_AutoSave is null; rows fall back to " +
                    "plain labels.");
                return null;
            }

            return autosave;
        }

        /// <summary>Row caption: DisplayName, plus " (v&lt;Version&gt;)" when Version is present and non-blank.</summary>
        private static string RowLabel(ModEntry entry)
        {
            string name = entry.DisplayName;
            if (name == null || name.Length == 0)
                name = entry.Key;

            string version = entry.Version;
            if (version != null && version.Trim().Length != 0)
                name = name + " (v" + version + ")";

            return name;
        }

        /// <summary>
        /// Back button: closes the panel via the game's focus pipeline (restoring the title focus saved by
        /// SetFocus's _saveState). Returned so the caller can also assign it to m_ButtonOnCancel for the
        /// cancel/B-button path.
        /// </summary>
        private static Button AddBackButton(Transform parent)
        {
            GameObject go = NewUIChild("BackButton", parent);
            Image img = go.AddComponent<Image>();
            img.color = new Color(0.20f, 0.20f, 0.24f, 1f);

            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 44f;
            le.preferredHeight = 44f;

            Button button = go.AddComponent<Button>();
            button.targetGraphic = img;
            button.onClick.RemoveAllListeners();
            button.onClick.AddListener(ClosePanel);

            AddLabelChild(go.transform, "Back");
            return button;
        }

        /// <summary>Close through FTKInput so the saved title focus is restored. Used by the Back button.
        /// Named ClosePanel (not Close) to avoid hiding the inherited instance FTKInputFocus.Close(). The
        /// cancel/B-button path is served separately by m_ButtonOnCancel -&gt; OnButton -&gt; Back.onClick, which
        /// also lands here.</summary>
        private static void ClosePanel()
        {
            if (_instance != null)
                FTKInput.Instance.Close(_instance);
        }

        // ----- small UI builders (each trivially understandable) -----

        private static void AddBanner(Transform parent, string text)
        {
            GameObject go = NewUIChild("Banner", parent);
            LayoutElement le = go.AddComponent<LayoutElement>();
            le.minHeight = 30f;
            le.preferredHeight = 30f;

            Text t = go.AddComponent<Text>();
            ApplyText(t, text);
        }

        private static void AddLabelChild(Transform parent, string text)
        {
            GameObject go = NewUIChild("Label", parent);
            RectTransform rt = go.GetComponent<RectTransform>();
            StretchFull(rt);

            Text t = go.AddComponent<Text>();
            ApplyText(t, text);
        }

        private static void ApplyText(Text t, string text)
        {
            t.text = text; // non-"STR_" literal: FTKLocalizationUI leaves it alone.
            t.alignment = TextAnchor.MiddleCenter;
            t.color = Color.white;
            t.fontSize = 22;
            t.font = Resources.GetBuiltinResource<Font>("Arial.ttf");
            t.horizontalOverflow = HorizontalWrapMode.Overflow;
            t.verticalOverflow = VerticalWrapMode.Overflow;
        }

        private static GameObject NewUIChild(string name, Transform parent)
        {
            GameObject go = new GameObject(name, typeof(RectTransform));
            go.transform.SetParent(parent, false);
            return go;
        }

        private static void StretchFull(RectTransform rt)
        {
            rt.anchorMin = Vector2.zero;
            rt.anchorMax = Vector2.one;
            rt.offsetMin = Vector2.zero;
            rt.offsetMax = Vector2.zero;
        }

        private static void CenterBox(RectTransform rt, float width, float height)
        {
            rt.anchorMin = new Vector2(0.5f, 0.5f);
            rt.anchorMax = new Vector2(0.5f, 0.5f);
            rt.pivot = new Vector2(0.5f, 0.5f);
            rt.anchoredPosition = Vector2.zero;
            rt.sizeDelta = new Vector2(width, height);
        }
    }
}
