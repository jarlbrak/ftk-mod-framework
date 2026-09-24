using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using BepInEx;
using HarmonyLib;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.Events;

namespace FTKReportingProof
{
    [BepInPlugin("com.ftkmf.reporting.proof", "Reporting menu proof", "0.0.1")]
    public sealed class Plugin : BaseUnityPlugin
    {
        internal static Plugin Live;
        internal static string Root;
        private string session;
        private bool nativeReporting;
        private string last;
        private float next;
        private Harmony harmony;
        private static int steamInitIntercepts;
        private static int steamRestartIntercepts;
        private static int offlineStatReads;
        private static int offlineAchievementReads;
        private Button entry;
        private Button disabledEntry;
        private bool disabledEntryWasInteractable;
        private uiOptionsMenu owner;
        private ProofPanel panel;
        private string injectionError;
        private static int legacyHandlerSuppressed;
        private static int legacyCaptureSuppressed;
        private static int legacyFormSuppressed;
        private string openFault;
        private bool commandBusy;
        private bool applicationFocused;
        private int applicationFocusLosses;
        private int applicationFocusGains;
        private sealed class ExpectedOpenFault : Exception
        {
            internal ExpectedOpenFault(string point) : base("Expected open fault: " + point) { }
        }
        private void FaultAt(string point)
        {
            if (openFault == point) throw new ExpectedOpenFault(point);
        }

        private void Awake()
        {
            if (Environment.GetEnvironmentVariable("FTK_REPORTING_PROOF") != "1") { enabled = false; return; }
            try
            {
                Root = Environment.GetEnvironmentVariable("FTK_REPORTING_ROOT");
                string bundle = Environment.GetEnvironmentVariable("FTK_REPORTING_BUNDLE");
                string product = String.IsNullOrEmpty(bundle) ? "" : bundle.Substring(bundle.LastIndexOf('.') + 1);
                session = Environment.GetEnvironmentVariable("FTK_REPORTING_SESSION");
                if (String.IsNullOrEmpty(Root) || Path.GetFullPath(Root) != Path.GetFullPath(Paths.GameRootPath)
                    || (new DirectoryInfo(Root).Parent == null || new DirectoryInfo(Root).Parent.Name != "scratch")
                    || (File.GetAttributes(Root) & FileAttributes.ReparsePoint) != 0
                    || String.IsNullOrEmpty(bundle) || !bundle.StartsWith("com.ftkmf.reporting.proof.", StringComparison.Ordinal)
                    || Application.identifier != bundle || Application.companyName != "FTKReportingProof"
                    || Application.productName != product || String.IsNullOrEmpty(session)
                    || !(Application.persistentDataPath.EndsWith("/FTKReportingProof/" + product, StringComparison.Ordinal)
                         || Application.persistentDataPath.EndsWith("/" + bundle, StringComparison.Ordinal)))
                    throw new InvalidOperationException("Disposable identity/root guard rejected launch");
                string receipt = Path.Combine(Root, "proof-steam-guard.receipt");
                if (!File.Exists(receipt) || File.ReadAllText(receipt) != session)
                    throw new InvalidOperationException("Missing preloader receipt for this session");
                // Inspect backing fields directly; the public property lazily creates SteamManager.
                System.Reflection.FieldInfo steamInstanceField = AccessTools.Field(typeof(SteamManager), "s_instance");
                System.Reflection.FieldInfo steamInitializedField = AccessTools.Field(typeof(SteamManager), "m_bInitialized");
                System.Reflection.FieldInfo steamEverField = AccessTools.Field(typeof(SteamManager), "s_EverInialized");
                if (steamInstanceField == null || steamInitializedField == null || steamEverField == null)
                    throw new InvalidOperationException("Cannot verify preexisting Steam state");
                object existingSteam = steamInstanceField.GetValue(null);
                if ((bool)steamEverField.GetValue(null)
                    || (existingSteam != null && (bool)steamInitializedField.GetValue(existingSteam)))
                    throw new InvalidOperationException("Steam was initialized before proof isolation");
                harmony = new Harmony("com.ftkmf.reporting.proof");
                // Installed wrappers call native Steam immediately. Block before any game Awake.
                harmony.Patch(AccessTools.Method(typeof(Steamworks.SteamAPI), "Init"), new HarmonyMethod(typeof(Plugin), "NoSteam"));
                harmony.Patch(AccessTools.Method(typeof(Steamworks.SteamAPI), "RestartAppIfNecessary"), new HarmonyMethod(typeof(Plugin), "NoSteam"));
                // The installed stats adapter guards writes but its two reads bypass Initialized.
                // Return unavailable, not fabricated Steam success, in this offline fixture only.
                harmony.Patch(AccessTools.Method(typeof(StatsAchievements.SteamStatsAndAchievements), "GetStatValue",
                    new[] { typeof(string), typeof(int).MakeByRefType() }), new HarmonyMethod(typeof(Plugin), "OfflineStat"));
                harmony.Patch(AccessTools.Method(typeof(StatsAchievements.SteamStatsAndAchievements), "GetAchievementValue",
                    new[] { typeof(string), typeof(bool).MakeByRefType() }), new HarmonyMethod(typeof(Plugin), "OfflineAchievement"));
                harmony.Patch(AccessTools.Method(typeof(uiStartGame), "GetSavePath"), new HarmonyMethod(typeof(Plugin), "SavePath"));
                harmony.Patch(AccessTools.Method(typeof(uiStartGame), "GetSavePathSlash"), new HarmonyMethod(typeof(Plugin), "SavePathSlash"));
                harmony.Patch(AccessTools.Method(typeof(uiOptionsMenu), "Show"), null, new HarmonyMethod(typeof(Plugin), "Shown"));
                nativeReporting = Environment.GetEnvironmentVariable("FTK_REPORTING_NATIVE") == "1";
                if (!nativeReporting)
                {
                harmony.Patch(AccessTools.Method(typeof(uiOptionsMain), "OnReportBugs"), new HarmonyMethod(typeof(Plugin), "ReportBugs"));
                harmony.Patch(AccessTools.Method(typeof(SerializeGO), "ShowBugForm", new[] { typeof(Action) }),
                    new HarmonyMethod(typeof(Plugin), "SuppressLegacyCapture"));
                harmony.Patch(AccessTools.Method(typeof(uiSystemDialog), "ShowBugForm", new[] { typeof(Action) }),
                    new HarmonyMethod(typeof(Plugin), "SuppressLegacyForm"));
                }
                Directory.CreateDirectory(Path.Combine(Root, "proof-saves"));
                Directory.CreateDirectory(Path.Combine(Root, "proof-output"));
                Application.runInBackground = true;
                applicationFocused = Application.isFocused;
                Live = this;
                Write("ready", new { session, root = Root, bundle = Application.identifier, persistent = Application.persistentDataPath, steamSuppressed = true });
            }
            catch (Exception e)
            {
                Logger.LogFatal("Reporting proof isolation failed: " + e);
                // A guarded disposable process must fail closed before game initialization.
                Environment.Exit(71);
            }
        }
        private static bool NoSteam(System.Reflection.MethodBase __originalMethod, ref bool __result)
        {
            if (__originalMethod.Name == "Init") steamInitIntercepts++;
            else if (__originalMethod.Name == "RestartAppIfNecessary") steamRestartIntercepts++;
            __result = false;
            return false;
        }
        private static bool OfflineStat(ref int __1, ref bool __result)
        {
            offlineStatReads++;
            __1 = 0; __result = false; return false;
        }
        private static bool OfflineAchievement(ref bool __1, ref bool __result)
        {
            offlineAchievementReads++;
            __1 = false; __result = false; return false;
        }
        private static bool SavePath(ref string __result) { __result = Path.Combine(Root, "proof-saves"); return false; }
        private static bool SavePathSlash(ref string __result) { __result = Path.Combine(Root, "proof-saves") + Path.DirectorySeparatorChar; return false; }
        private static void Shown(uiOptionsMenu __instance) { if (Live != null) Live.Inject(__instance); }
        private static bool SuppressLegacyCapture() { legacyCaptureSuppressed++; return false; }
        private static bool SuppressLegacyForm() { legacyFormSuppressed++; return false; }
        private static bool ReportBugs(uiOptionsMain __instance)
        {
            legacyHandlerSuppressed++;
            if (Live != null)
            {
                try
                {
                    if (!Live.owner || Live.owner.m_MainOptions != __instance || !Live.entry || Live.injectionError != null)
                        throw new InvalidOperationException("Reporting takeover is unavailable for this Options owner");
                    Live.Open();
                }
                catch (Exception e)
                {
                    Live.injectionError = e.ToString();
                    if (Live.owner && Live.owner.m_MainOptions == __instance) Live.DisableEntry(Live.entry);
                    Live.Logger.LogError("Reporting takeover unavailable: " + e);
                }
            }
            // The armed proof never falls through to the legacy close-and-submit form.
            return false;
        }
        internal static void Rewire(Button b, UnityAction action)
        {
            for (int i = 0; i < b.onClick.GetPersistentEventCount(); i++) b.onClick.SetPersistentListenerState(i, UnityEventCallState.Off);
            b.onClick.RemoveAllListeners(); b.onClick.AddListener(action);
        }
        private void DisableEntry(Button button)
        {
            if (!button) return;
            if (disabledEntry != button)
            {
                disabledEntry = button;
                disabledEntryWasInteractable = button.interactable;
            }
            button.interactable = false;
        }
        private void Inject(uiOptionsMenu menu)
        {
            try
            {
                Button source = menu.m_ReportBugs;
                if (!source || !source.gameObject.scene.IsValid() || !source.GetComponent<FTKSelectable>()
                    || !source.GetComponentInChildren<Text>(true) || !menu.m_MainOptions || !menu.m_MainOptions.m_SubBlocker
                    || !menu.m_AudioOptions)
                    throw new InvalidOperationException("Missing native reporting source");
                if (owner != menu)
                {
                    if (panel) Destroy(panel.gameObject);
                    panel = null;
                }
                owner = menu;
                entry = source;
                if (disabledEntry == source)
                {
                    source.interactable = disabledEntryWasInteractable;
                    disabledEntry = null;
                }
                injectionError = null;
            }
            catch (Exception e)
            {
                injectionError = e.ToString();
                if (menu) DisableEntry(menu.m_ReportBugs);
                owner = null;
                entry = null;
            }
        }
        private void Open()
        {
            if (!owner || FTKInput.Instance.InputFocus != owner.m_MainOptions || FTKInput.Instance.m_WaitingForPopup) return;
            Type boundary = AccessTools.TypeByName("FTKModFramework.Core.HotReload.HotReloadBoundary");
            if (boundary != null)
            {
                System.Reflection.PropertyInfo locked = AccessTools.Property(boundary, "NavigationLocked");
                if (locked == null || (bool)locked.GetValue(null, null)) return;
            }
            if (!panel) panel = ProofPanel.Create(owner, entry);
            bool blocker = owner.m_MainOptions.m_SubBlocker.gameObject.activeSelf;
            FTKInputState rollback = new FTKInputState();
            FTKInputState parentSaved = owner.m_MainOptions.m_InputState;
            FTKInputFocus parentSavedFocus = parentSaved != null ? parentSaved.m_InputFocus : null;
            try
            {
                owner.m_MainOptions.m_SubBlocker.gameObject.SetActive(true);
                FaultAt("after-blocker");
                FTKInput.Instance.SetFocus(panel, null, true);
                if (FTKInput.Instance.InputFocus != panel || !panel.m_HasInputFocus) throw new InvalidOperationException("Native focus rejected proof: current=" + Node(FTKInput.Instance.InputFocus)
                    + ", owned=" + panel.m_HasInputFocus + ", childModal=" + panel.m_IsModal + ", parentModal=" + owner.m_MainOptions.m_IsModal);
                FaultAt("after-focus");
            }
            catch
            {
                if (parentSaved != null) parentSaved.m_InputFocus = parentSavedFocus;
                owner.m_MainOptions.m_InputState = parentSaved;
                if (FTKInput.Instance.InputFocus != owner.m_MainOptions) rollback.Restore();
                owner.m_MainOptions.m_InputState = parentSaved;
                panel.gameObject.SetActive(false);
                panel.m_InputState = null;
                owner.m_MainOptions.m_SubBlocker.gameObject.SetActive(blocker);
                throw;
            }
        }
        private void OnApplicationFocus(bool focused)
        {
            if (Live != this) return;
            applicationFocused = focused;
            if (focused) applicationFocusGains++;
            else applicationFocusLosses++;
        }
        private object BrowserFocusProbe()
        {
            if (Live != this || openFault != null || !owner || !owner.m_Showing
                || !owner.m_MainOptions || !owner.m_MainOptions.m_SubBlocker
                || !owner.m_MainOptions.m_SubBlocker.gameObject.activeSelf
                || !panel || !panel.gameObject.activeInHierarchy || !panel.m_HasInputFocus
                || !FTKInput.Instance || FTKInput.Instance.InputFocus != panel || FTKInput.Instance.m_WaitingForPopup
                || File.ReadAllText(Path.Combine(Root, "proof-steam-guard.receipt")) != session)
                throw new InvalidOperationException("Browser focus probe requires guarded owned reporting child focus");
            // Read-only fixed destination: no report data, form, query, or configurable URL.
            Application.OpenURL("https://github.com/jarlbrak/ftk-mod-framework/issues");
            return new { session, browserRequestStatus = "requested", state = State() };
        }
        private IEnumerator RunFaultProbe(string id, string point)
        {
            commandBusy = true;
            IEnumerator trial = FaultProbe(id, point);
            try
            {
                while (true)
                {
                    bool more;
                    try { more = trial.MoveNext(); }
                    catch (Exception e) { Write(id, new { session, error = e.ToString() }); yield break; }
                    if (!more) yield break;
                    yield return trial.Current;
                }
            }
            finally
            {
                openFault = null;
                commandBusy = false;
                IDisposable disposable = trial as IDisposable;
                if (disposable != null) disposable.Dispose();
            }
        }
        private IEnumerator FaultProbe(string id, string point)
        {
            if (point != "after-blocker" && point != "after-focus") throw new InvalidOperationException("Unknown fault point");
            if (Live != this || openFault != null || !owner || !entry || !owner.m_Showing
                || !owner.m_MainOptions || !owner.m_MainOptions.m_HasInputFocus
                || FTKInput.Instance.InputFocus != owner.m_MainOptions || FTKInput.Instance.m_WaitingForPopup
                || !owner.m_MainOptions.m_SubBlocker || owner.m_MainOptions.m_SubBlocker.gameObject.activeSelf
                || (panel && panel.gameObject.activeInHierarchy)
                || File.ReadAllText(Path.Combine(Root, "proof-steam-guard.receipt")) != session)
                throw new InvalidOperationException("Fault probe requires guarded, unblocked owned Options focus");
            uiOptionsMenu trialOwner = owner;
            FTKInputState saved = owner.m_MainOptions.m_InputState;
            FTKInputFocus savedFocus = saved != null ? saved.m_InputFocus : null;
            FTKInputFocus firstModal = FTKInput.Instance.m_FirstModal;
            FTKSelectable selected = owner.m_MainOptions.m_CurrentSelected;
            bool paused = owner.IsPaused;
            float timeScale = Time.timeScale;
            object before = State();
            bool expected = false;
            string fault = null;
            try
            {
                openFault = point;
                try { Open(); }
                catch (ExpectedOpenFault e) { expected = true; fault = e.Message; }
            }
            finally { openFault = null; }
            object immediate = State();
            int observationFrames = 0;
            float started = Time.realtimeSinceStartup;
            bool restored = false;
            while (true)
            {
                if (!trialOwner || owner != trialOwner || !trialOwner.m_MainOptions
                    || !trialOwner.m_MainOptions.m_SubBlocker || !FTKInput.Instance) break;
                restored = expected && FTKInput.Instance.InputFocus == trialOwner.m_MainOptions
                    && trialOwner.m_MainOptions.m_HasInputFocus && trialOwner.m_MainOptions.m_InputState == saved
                    && (saved == null || saved.m_InputFocus == savedFocus)
                    && FTKInput.Instance.m_FirstModal == firstModal && trialOwner.m_MainOptions.m_CurrentSelected == selected
                    && !trialOwner.m_MainOptions.m_SubBlocker.gameObject.activeSelf
                    && panel && !panel.gameObject.activeInHierarchy && !panel.m_HasInputFocus && panel.m_InputState == null
                    && trialOwner.m_Showing && trialOwner.IsPaused == paused && Time.timeScale == timeScale;
                if (restored || !expected || Time.realtimeSinceStartup - started >= 1f || observationFrames >= 120) break;
                // Native SetFocus queues selection; its Update consumes two setup frames first.
                yield return null;
                observationFrames++;
            }
            Write(id, new { session, point, expectedFaultObserved = expected, fault, rollbackRestored = restored,
                injectionCleared = openFault == null, observationFrames,
                observationSeconds = Time.realtimeSinceStartup - started, before, immediate, after = State() });
        }

        internal static string Node(Component c)
        {
            if (!c) return null;
            string p = c.name; Transform t = c.transform.parent;
            while (t) { p = t.name + "/" + p; t = t.parent; }
            return p;
        }
        private object SetupGate()
        {
            uiStartGame start = uiStartGame.Instance;
            SelectScreenCamera camera = UnityEngine.Object.FindObjectOfType<SelectScreenCamera>();
            Animator animator = camera ? camera.GetComponent<Animator>() : null;
            bool hasState = animator && animator.runtimeAnimatorController && animator.layerCount > 0;
            AnimatorStateInfo state = hasState ? animator.GetCurrentAnimatorStateInfo(0) : new AnimatorStateInfo();
            GameFlowMC flow = UnityEngine.Object.FindObjectOfType<GameFlowMC>();
            return new
            {
                mapGenerated = flow && flow.m_IsMapReady,
                nativeMapReady = start && start.m_MapReady,
                createUiCount = start && start.m_CreateUIs != null ? start.m_CreateUIs.Count : -1,
                expectedCreateUiCount = start ? start.m_ActualMaxCharCount : -1,
                generatingMapActive = start && start.m_GeneratingMap && start.m_GeneratingMap.gameObject.activeInHierarchy,
                createUiRootActive = start && start.m_CreateCharacterRoot && start.m_CreateCharacterRoot.m_UIRoot
                    && start.m_CreateCharacterRoot.m_UIRoot.gameObject.activeInHierarchy,
                cameraFound = camera != null,
                animatorFound = animator != null,
                animatorEnabled = animator && animator.enabled,
                animatorActive = animator && animator.gameObject.activeInHierarchy,
                animatorSpeed = animator ? animator.speed : 0f,
                animatorHasState = hasState,
                animatorComplete = hasState && state.IsName("complete"),
                animatorFullPathHash = hasState ? state.fullPathHash : 0,
                animatorShortNameHash = hasState ? state.shortNameHash : 0,
                animatorNormalizedTime = hasState ? state.normalizedTime : 0f,
                animatorInTransition = hasState && animator.IsInTransition(0)
            };
        }
        private FTKInputFocus NativePanel()
        {
            if (!nativeReporting) return null;
            Type type = AccessTools.TypeByName("FTKModFramework.Core.UI.ReportingMenu");
            return type == null ? null : AccessTools.Property(type, "CurrentPanel").GetValue(null, null) as FTKInputFocus;
        }
        private object NativeReportState()
        {
            FTKInputFocus current = NativePanel();
            Type runtime = AccessTools.TypeByName("FTKModFramework.Core.Reporting.ReportingRuntime");
            if (!nativeReporting || runtime == null) return null;
            object report = current ? AccessTools.Property(current.GetType(), "Report").GetValue(current, null) : null;
            Dictionary<string, object> result = new Dictionary<string, object>();
            result["trackingAvailable"] = AccessTools.Property(runtime, "TrackingAvailable").GetValue(null, null);
            result["notice"] = AccessTools.Property(runtime, "Notice").GetValue(null, null);
            PropertyInfo draftsReady = AccessTools.Property(runtime, "DraftsReady");
            result["draftsReady"] = draftsReady != null && (bool)draftsReady.GetValue(null, null);
            PropertyInfo savedProperty = AccessTools.Property(runtime, "SavedDraft");
            object saved = savedProperty == null ? null : savedProperty.GetValue(null, null);
            object savedReport = saved == null ? null : AccessTools.Field(saved.GetType(), "Report").GetValue(saved);
            result["savedReportId"] = savedReport == null ? null : AccessTools.Field(savedReport.GetType(), "ReportId").GetValue(savedReport);
            if (report != null) result["IncludeMetadata"] = AccessTools.Field(report.GetType(), "IncludeMetadata").GetValue(report);
            object incident = AccessTools.Property(runtime, "Pending").GetValue(null, null);
            result["pendingSessionId"] = incident == null ? null : AccessTools.Field(incident.GetType(), "SessionId").GetValue(incident);
            if (report != null)
                foreach (string name in new[] { "ReportId", "CaptureId", "CurrentMetadata", "PreviousSessionId", "PreviousCheckpointId", "PreviousMetadata" })
                    result[name] = AccessTools.Field(report.GetType(), name).GetValue(report);
            if (current)
            {
                List<string> texts = new List<string>();
                foreach (Text text in current.GetComponentsInChildren<Text>(false)) texts.Add(text.text);
                result["texts"] = texts;
                InputField[] inputs = current.GetComponentsInChildren<InputField>(true);
                result["descriptionInputLength"] = inputs.Length == 1 ? inputs[0].text.Length : -1;
                List<object> canvases = new List<object>();
                foreach (Canvas canvas in current.transform.parent.GetComponentsInChildren<Canvas>(true))
                    canvases.Add(new { node = Node(canvas), canvas.overrideSorting, canvas.sortingOrder, canvas.sortingLayerID, enabled = canvas.enabled });
                result["canvases"] = canvases;
            }
            return result;
        }
        private object State()
        {
            FTKInputFocus focus = FTKInput.Instance ? FTKInput.Instance.InputFocus : null;
            uiOptionsMenu menu = uiOptionsMenu.Instance;
            List<object> buttons = new List<object>();
            foreach (Button b in Resources.FindObjectsOfTypeAll<Button>())
                if (b.gameObject.scene.IsValid() && buttons.Count < 600)
                    buttons.Add(new { path = Node(b), active = b.gameObject.activeInHierarchy, label = b.GetComponentInChildren<Text>(true) ? b.GetComponentInChildren<Text>(true).text : null,
                        up = Node(b.navigation.selectOnUp), down = Node(b.navigation.selectOnDown), interactable = b.interactable });
            FTKInputFocus reporting = nativeReporting ? NativePanel() : panel;
            return new { session, nativeReporting, nativeReport = NativeReportState(), steamInitIntercepts, steamRestartIntercepts, offlineStatReads, offlineAchievementReads, evidence = "Programmatic native calls; not physical input qualification", focus = Node(focus), selected = focus ? Node(focus.m_CurrentSelected) : null,
                focusOwned = focus && focus.m_HasInputFocus, focusModal = focus && focus.m_IsModal,
                firstModal = FTKInput.Instance ? Node(FTKInput.Instance.m_FirstModal) : null,
                parentSavedFocus = owner && owner.m_MainOptions && owner.m_MainOptions.m_InputState != null
                    ? Node(owner.m_MainOptions.m_InputState.m_InputFocus) : null,
                legacyHandlerSuppressed, legacyCaptureSuppressed, legacyFormSuppressed, reportingEntry = Node(entry), reportingEntryInteractable = entry && entry.interactable,
                openFaultArmed = openFault != null, applicationFocused, unityApplicationFocused = Application.isFocused, applicationFocusLosses, applicationFocusGains,
                nativeAudioModal = menu && menu.m_AudioOptions && menu.m_AudioOptions.m_IsModal, setupGate = SetupGate(), optionsShowing = menu && menu.m_Showing, paused = menu && menu.IsPaused, timeScale = Time.timeScale,
                phase = uiStartGame.Instance && uiStartGame.Instance.m_GameStarted ? "session" : "title-or-startup", reportActive = reporting && reporting.gameObject.activeInHierarchy, reportFocusOwned = reporting && reporting.m_HasInputFocus,
                blocker = menu && menu.m_MainOptions.m_SubBlocker.gameObject.activeSelf, injectionError, buttons };
        }
        private void Write(string id, object value)
        {
            File.WriteAllText(Path.Combine(Path.Combine(Root, "proof-output"), id + ".json"), JsonConvert.SerializeObject(value, Formatting.Indented));
        }
        private void Update()
        {
            if (Live != this || commandBusy || Time.realtimeSinceStartup < next) return;
            next = Time.realtimeSinceStartup + .2f;
            string path = Path.Combine(Root, "proof-command.json");
            if (!File.Exists(path) || new FileInfo(path).Length > 4096) return;
            string id = null;
            try
            {
                JObject command = JObject.Parse(File.ReadAllText(path));
                id = (string)command["id"];
                if ((string)command["session"] != session || String.IsNullOrEmpty(id) || id.Length > 64) return;
                foreach (char c in id) if (!(Char.IsLetterOrDigit(c) || c == '-')) return;
                if (last == id || File.Exists(Path.Combine(Path.Combine(Root, "proof-output"), id + ".json"))) return;
                last = id;
                string op = (string)command["op"];
                if (op == "state") Write(id, State());
                else if (op == "browser-focus") Write(id, BrowserFocusProbe());
                else if (op == "fault-open") StartCoroutine(RunFaultProbe(id, (string)command["point"]));
                else if (op == "screenshot") StartCoroutine(Screenshot(id));
                else if (op == "quit") { Write(id, State()); Application.Quit(); }
                else if (op == "text")
                {
                    FTKInputFocus target = NativePanel();
                    if (!target || FTKInput.Instance.InputFocus != target) throw new InvalidOperationException("Owned report focus required");
                    InputField[] fields = target.GetComponentsInChildren<InputField>(false);
                    if (fields.Length != 1) throw new InvalidOperationException("One owned report input required");
                    fields[0].text = (bool?)command["oversized"] == true ? new string('x', 8193) : (string)command["text"] ?? "";
                    Write(id, State());
                }
                else if (op == "click")
                {
                    Button found = null;
                    foreach (Button b in Resources.FindObjectsOfTypeAll<Button>())
                        if (b.gameObject.scene.IsValid() && b.gameObject.activeInHierarchy && b.interactable && Node(b) == (string)command["path"])
                        { if (found) throw new InvalidOperationException("Ambiguous path"); found = b; }
                    if (!found) throw new InvalidOperationException("No active button at exact path");
                    found.onClick.Invoke(); Write(id, State());
                }
                else if (op == "cancel")
                {
                    FTKInputFocus focus = FTKInput.Instance.InputFocus;
                    if (focus != panel && focus != NativePanel() && (!owner || focus != owner.m_MainOptions)) throw new InvalidOperationException("Cancel only owns reporting/options");
                    FTKInput.Instance.Close(focus); Write(id, State());
                }
                else throw new InvalidOperationException("Unknown operation");
            }
            catch (Exception e) { if (id != null && id == last) Write(id, new { session, error = e.ToString() }); }
        }
        private IEnumerator Screenshot(string id)
        {
            yield return new WaitForEndOfFrame();
            Texture2D texture = new Texture2D(Screen.width, Screen.height, TextureFormat.RGB24, false);
            try
            {
                texture.ReadPixels(new Rect(0, 0, Screen.width, Screen.height), 0, 0); texture.Apply();
                File.WriteAllBytes(Path.Combine(Path.Combine(Root, "proof-output"), id + ".png"), texture.EncodeToPNG());
                Write(id, State());
            }
            finally { Destroy(texture); }
        }
    }
    public sealed class ProofPanel : FTKInputFocus
    {
        private new void Awake() { m_IsOptionSubMenu = true; m_Cancel = Close; }
        public override void OnPreSetFocus() { gameObject.SetActive(true); base.OnPreSetFocus(); }
        public override void OnClose() { gameObject.SetActive(false); base.OnClose(); }
        internal static ProofPanel Create(uiOptionsMenu owner, Button source)
        {
            GameObject root = new GameObject("FrameworkReportingProofPanel", typeof(RectTransform), typeof(Image));
            root.SetActive(false);
            root.transform.SetParent(owner.m_AudioOptions.transform.parent, false);
            RectTransform rect = root.GetComponent<RectTransform>();
            rect.anchorMin = rect.anchorMax = new Vector2(.5f, .5f); rect.sizeDelta = new Vector2(720, 300);
            root.GetComponent<Image>().color = new Color(.06f, .06f, .09f, .98f);
            root.transform.SetAsLastSibling();
            ProofPanel panel = root.AddComponent<ProofPanel>();
            // Modal-to-nonmodal transfer invokes native first-modal restoration immediately.
            // Match the native option child ownership contract before requesting focus.
            panel.m_IsModal = owner.m_AudioOptions.m_IsModal;
            panel.m_IsOptionSubMenu = true;
            panel.m_IsCloseOnLoseFocus = owner.m_AudioOptions.m_IsCloseOnLoseFocus;
            panel.m_InputMode = owner.m_AudioOptions.m_InputMode;
            panel.m_NavigationSetup = owner.m_AudioOptions.m_NavigationSetup;
            panel.m_SelectableParent = root.transform;
            GameObject label = new GameObject("ProofText", typeof(RectTransform), typeof(Text));
            label.transform.SetParent(root.transform, false);
            RectTransform lr = label.GetComponent<RectTransform>(); lr.sizeDelta = new Vector2(650, 160); lr.anchoredPosition = new Vector2(0, 35);
            Text text = label.GetComponent<Text>(); text.font = source.GetComponentInChildren<Text>(true).font; text.fontSize = 24; text.alignment = TextAnchor.MiddleCenter;
            text.text = "Report framework/mod issue\nNative menu feasibility proof only\nNo report or diagnostics are collected.";
            Button back = Instantiate(source, root.transform); back.name = "ReportingProofBack";
            RectTransform backRect = back.GetComponent<RectTransform>();
            backRect.anchorMin = backRect.anchorMax = backRect.pivot = new Vector2(.5f, .5f);
            backRect.sizeDelta = new Vector2(240, 45);
            backRect.anchoredPosition = new Vector2(0, -100);
            back.GetComponentInChildren<Text>(true).text = "Back"; Plugin.Rewire(back, panel.Close);
            Navigation nav = new Navigation(); nav.mode = Navigation.Mode.None; back.navigation = nav;
            panel.m_FirstSelected = back.GetComponent<FTKSelectable>(); panel.m_FirstSelected.m_InputFocus = panel;
            return panel;
        }
    }
}
