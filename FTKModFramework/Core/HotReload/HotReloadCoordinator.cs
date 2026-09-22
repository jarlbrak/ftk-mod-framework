using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using GridEditor;
using FTKModFramework.Core.Data;
using FTKModFramework.Core.Marketplace;
using FTKModFramework.Core.UI;
using UnityEngine;

namespace FTKModFramework.Core.HotReload
{
    // A closed activation transaction. No call to the startup loader is a claim of isolation:
    // all Unity mutation happens after the boundary is locked, with exact rollback snapshots.
    internal static class HotReloadCoordinator
    {
        internal static bool Busy { get; private set; }
        internal static bool Faulted { get; private set; }
        internal static string Notice = "Prepare a supported mod selection, then apply it here.";
        internal static int Epoch { get; private set; }
        private static ManagedSnapshot target, previous;
        private static DefinitionState definitions;
        private static HotReloadNativeCaches caches;
        private static PaladinResourceState.Snapshot resources;
        private static ClassPreferences preferences;
        private static string failurePoint;
        private static bool draining;
        private static bool awaitingCommit;
        private static System.Collections.IEnumerator assetPreflight;
        private static Stopwatch clock;
        private static Stopwatch totalClock;
        private static long phaseStart;
        private static readonly Dictionary<string, object> timings = new Dictionary<string, object>();
        private static void Mark(string phase)
        {
            if (totalClock == null) return;
            long elapsed = totalClock.ElapsedMilliseconds;
            timings[phase] = elapsed - phaseStart;
            phaseStart = elapsed;
        }
        private static Action completed;
        private static string oldIdentity;
        private static string finalNotice;
        private static bool pinnedAssembly;
        private static bool startupValidated;


        internal static void CaptureBaseline(TableManager manager)
        {
            if (!HotReloadBoundary.Enabled) return;
            try
            {
                if (Plugin.EnableSampleContent.Value || Plugin.SelfTestsEnabled || Plugin.DiagnosticsEnableGate.Value ||
                    Plugin.ForceCustomEnemy.Value || Plugin.ForceCustomEncounter.Value || Plugin.SyntheticContentCount.Value != 0 || !Plugin.EnableDataContent.Value)
                    throw new InvalidOperationException("Injected or diagnostic content is outside title-screen activation.");
                using (SHA256 sha = SHA256.Create())
                using (FileStream stream = File.OpenRead(typeof(TableManager).Assembly.Location))
                    pinnedAssembly = Hex(sha.ComputeHash(stream)) == "94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8";
                if (!pinnedAssembly) throw new InvalidOperationException("Native reload adapters require the audited game assembly.");
                DefinitionState.CaptureBaseline(manager);
                MarketplaceRuntime.MutationsBlocked = delegate { return Busy || Faulted; };
            }
            catch (Exception e) { Fault(e); }
        }

        internal static LoadResult LoadInitial()
        {
            try
            {
                if (Faulted || !MarketplaceRuntime.CanDiscover) throw new InvalidOperationException("Initial activation is unavailable.");
                ValidateSelection(MarketplaceRuntime.Active);
                LoadResult result = ContentLoader.LoadCandidate(Plugin.DataContentRootPath, MarketplaceRuntime.Active);
                SaveNamespace.Initialize(MarketplaceRuntime.Active);
                startupValidated = true;
                return result;
            }
            catch (Exception e) { Fault(e); throw; }
        }

        internal static string UnavailableReason()
        {
            if (Faulted) return "Activation is faulted: " + Notice;
            if (Busy || MarketplaceRuntime.Busy || FrameworkUpdateRuntime.Busy) return "Another operation is running.";
            if (!pinnedAssembly) return "No audited assembly baseline.";
            if (!startupValidated) return "Initial content has not passed strict admission.";
            if (MarketplaceRuntime.RegistrationNotice != null) return "Startup registration reported errors.";
            return HotReloadBoundary.Check();
        }

        internal static bool ApplyPending(Action onComplete)
        {
            string reason = UnavailableReason();
            if (reason != null) { Notice = reason; return false; }
            try
            {
                MarketplaceRuntime.RefreshPendingForHotReload();
                target = MarketplaceRuntime.Pending;
                previous = MarketplaceRuntime.Active;
                ValidateSelection(target);
                ValidateSelection(previous);
                if (target == null) throw new InvalidOperationException("No prepared generation.");
                MarketplaceProtocol.ValidateSnapshot(MarketplaceRuntime.StateRoot, target);
                oldIdentity = Identity();
                Busy = true;
                completed = onComplete;
                clock = Stopwatch.StartNew();
                totalClock = Stopwatch.StartNew(); phaseStart = 0; timings.Clear();
                Notice = "Validating prepared content...";
                ModsPanel.InvalidateForHotReload();
                if (!MarketplaceRuntime.StartHotReload("hot-validate", Id(previous), Id(target), Validated))
                    throw new IOException(MarketplaceRuntime.Notice);
                return true;
            }
            catch (Exception e) { Rollback(e); return false; }
        }

        private static void ValidateSelection(ManagedSnapshot selection)
        {
            if (selection == null) return;
            if (selection.Packages == null || selection.Packages.Count > 1)
                throw new InvalidOperationException("Title-screen activation supports only empty or Paladin selections.");
            foreach (PackageDescriptor package in selection.Packages)
                if (package.ModGuid != "com.ftkmf.paladin" || (package.Dependencies != null && package.Dependencies.Length != 0))
                    throw new InvalidOperationException("Unsupported package or dependency closure.");
        }

        private static void Validated(MarketplaceResult result)
        {
            try
            {
                if (!result.Ok) throw new IOException(result.Message);
                if (Id(result.Active) != Id(previous) || Id(result.Pending) != Id(target))
                    throw new IOException("Validated selection differs from captured selection.");
                previous = MarketplaceProtocol.ReadVerifiedGeneration(MarketplaceRuntime.StateRoot, result.Active);
                target = MarketplaceProtocol.ReadVerifiedGeneration(MarketplaceRuntime.StateRoot, result.Pending);
                string reason = HotReloadBoundary.Check();
                if (reason != null) throw new InvalidOperationException(reason);
                Mark("helperValidateMs");
                definitions = DefinitionState.Capture(TableManager.Instance);
                caches = HotReloadNativeCaches.Capture();
                preferences = new ClassPreferences();
                resources = PaladinResourceState.Suspend();
                DefinitionState.RestoreBaseline(TableManager.Instance);
                Mark("snapshotResetMs");
                Inject("after-reset");
                ContentLoader.LoadCandidate(Plugin.DataContentRootPath, target);
                DefinitionState.ValidateLookups(TableManager.Instance);
                Inject("after-load");
                Mark("registrationMs");
                assetPreflight = PaladinResourceState.PreflightAssetsSteps();
                Notice = "Validating model and texture resources...";
                clock = Stopwatch.StartNew();
            }
            catch (Exception e) { Rollback(e); }
        }

        private static void AssetsValidated()
        {
            try
            {
                Mark("assetPreflightMs");
                caches.Rebuild();
                caches.Validate();
                preferences.Plan();
                Mark("cachesPreferencesMs");
                Inject("after-cache");
                VerifyCandidate();
                Inject("before-commit");
                Notice = "Runtime verified; draining preflight resources...";
                clock = Stopwatch.StartNew();
                awaitingCommit = true;
            }
            catch (Exception e) { Rollback(e); }
        }

        private static void Committed(MarketplaceResult result)
        {
            // Resolve even a lost acknowledgement by reading the durable pointer. Do not
            // restore old runtime state if the helper already committed the candidate.
            try
            {
                Mark("helperCommitMs");
                MarketplaceResult disk = MarketplaceProtocol.ReadState(MarketplaceRuntime.StateRoot);
                if (Id(disk.Active) == Id(target))
                {
                    string boundary = HotReloadBoundary.Check();
                    if (boundary != null) throw new InvalidOperationException(boundary);
                    definitions.ValidateComponents(TableManager.Instance);
                    caches.Validate();
                    VerifyCandidate();
                    preferences.Commit();
                    SaveNamespace.Publish(target);
                    MarketplaceRuntime.PublishHotReload(target);
                    Epoch++;
                    ModsPanel.InvalidateForHotReload();
                    caches.Retire();
                    caches = null;
                    PaladinResourceState.Retire(resources);
                    resources = null;
                    definitions = null;
                    preferences = null;
                    BeginDrain("Activated in this process. Start a new local adventure. Epoch " + Epoch + ".");
                }
                else if (Id(disk.Active) == Id(previous)) Rollback(new IOException(result.Message ?? "Commit rejected."));
                else Fault(new IOException("Durable selection is neither captured generation; runtime remains locked."));
            }
            catch (Exception e) { Fault(e); }
        }

        private static void VerifyCandidate()
        {
            DefinitionState.ValidateLookups(TableManager.Instance);
            bool paladin = target.Packages.Count == 1 && target.Packages[0].Enabled;
            int count = 0;
            foreach (Dictionary<string, int> table in ContentRegistry.CustomIds.Values) count += table.Count;
            if (count != (paladin ? 64 : 0)) throw new InvalidOperationException("Unexpected registered row count: " + count);
            FTK_playerGameStartDB classes = TableManager.Instance.Get<FTK_playerGameStartDB>();
            for (int i = 0; i < classes.m_Array.Length; i++)
                if ((int)FTK_playerGameStart.GetEnum(classes.m_Array[i].m_ID) != i)
                    throw new InvalidOperationException("Class identity differs from its array position.");
            if (paladin && !GuardianRuntime.Enabled) throw new InvalidOperationException("Guardian capability is missing.");
            if (!paladin && GuardianRuntime.Enabled) throw new InvalidOperationException("Guardian capability survived removal.");
            foreach (KeyValuePair<Type, Dictionary<string, int>> table in ContentRegistry.CustomIds)
            {
                GEDataArrayBase db = TableManager.Instance.Get(table.Key);
                foreach (KeyValuePair<string, int> row in table.Value)
                    if ((int)Reflect.Invoke(db, "GetIntFromID", row.Key) != row.Value)
                        throw new InvalidOperationException("Registered identity lookup mismatch.");
            }
        }

        private static void Rollback(Exception error)
        {
            try
            {
                if (definitions != null) definitions.Restore(TableManager.Instance);
                if (caches != null) caches.Restore();

                ModsPanel.InvalidateForHotReload();
                if (resources != null) PaladinResourceState.Rollback(resources);
                if (preferences != null) preferences.Rollback();
                definitions = null; caches = null; preferences = null; resources = null;
                if (oldIdentity != null && Identity() != oldIdentity) throw new InvalidOperationException("Rollback identity verification failed.");
                BeginDrain("Activation rejected; previous content retained. " + error.Message);
            }
            catch (Exception rollbackError) { Fault(new InvalidOperationException("Rollback failed after " + error.Message, rollbackError)); }
        }

        private static void BeginDrain(string message)
        {
            finalNotice = message;
            awaitingCommit = false;
            Busy = true;
            draining = true;
            Notice = "Waiting for owned resources to retire...";
            clock = Stopwatch.StartNew();
        }

        internal static void Tick()
        {
            HotReloadBoundary.ObserveTitle();
            if (!HotReloadBoundary.Enabled || Faulted) return;
            if (Busy && !draining) MarketplaceRuntime.Poll();
            if (assetPreflight != null)
            {
                // A timeout locks the process rather than restoring while background readers
                // still own candidate inputs. Normal failures surface only after their fence.
                if (clock.ElapsedMilliseconds > 30000)
                {
                    Fault(new InvalidOperationException("Asset validation timed out; quit to recover."));
                    return;
                }
                try
                {
                    if (assetPreflight.MoveNext()) return;
                    assetPreflight = null;
                    AssetsValidated();
                }
                catch (Exception error) { assetPreflight = null; Rollback(error); }
            }
            if (awaitingCommit)
            {
                if (PaladinResourceState.PendingDestroyCount != 0)
                {
                    if (clock.ElapsedMilliseconds > 30000) Rollback(new InvalidOperationException("Preflight resource destruction did not complete."));
                    return;
                }
                awaitingCommit = false;
                try
                {
                    string boundary = HotReloadBoundary.Check();
                    if (boundary != null) throw new InvalidOperationException(boundary);
                    definitions.ValidateComponents(TableManager.Instance);
                    caches.Validate();
                    SaveNamespace.Prepare(target);
                    preferences.Prepare(MarketplaceRuntime.StateRoot, Id(previous), Id(target));
                    Mark("verifyDrainMs");
                    Notice = "Runtime verified; committing selection...";
                    if (!MarketplaceRuntime.StartHotReload("hot-commit", Id(previous), Id(target), Committed))
                        throw new IOException(MarketplaceRuntime.Notice);
                }
                catch (Exception e) { Rollback(e); }
            }
            if (!draining || Faulted) return;
            if (PaladinResourceState.PendingDestroyCount != 0)
            {
                if (clock.ElapsedMilliseconds > 30000) Fault(new InvalidOperationException("Resource destruction did not complete."));
                return;
            }
            Mark("publishRetireMs");
            if (totalClock != null) timings["totalMs"] = totalClock.ElapsedMilliseconds;
            draining = false; Busy = false;
            Notice = finalNotice;
            Plugin.Log.LogInfo("[hot-reload] " + Notice);
            Action callback = completed; completed = null;
            target = null; previous = null; oldIdentity = null;
            if (callback != null) callback();
        }

        private static void Fault(Exception error)
        {
            Faulted = true; Busy = true;
            Notice = "Activation could not finish safely. Quit and restart. " + error.Message;
            Plugin.Log.LogError("[hot-reload] FAULTED: " + error);
            ModsPanel.InvalidateForHotReload();
        }
        private static void Inject(string point)
        {
            if (failurePoint == point) { failurePoint = null; throw new InvalidOperationException("Injected failure: " + point); }
        }
        private static string Id(ManagedSnapshot snapshot) { return snapshot == null ? "" : snapshot.GenerationId; }
        private static string Hex(byte[] bytes) { return BitConverter.ToString(bytes).Replace("-", "").ToLowerInvariant(); }
        internal static string Identity()
        {
            List<string> rows = new List<string>();
            foreach (KeyValuePair<Type, Dictionary<string, int>> table in ContentRegistry.CustomIds)
                foreach (KeyValuePair<string, int> row in table.Value) rows.Add(table.Key.FullName + ":" + row.Key + "=" + row.Value);
            rows.Sort(StringComparer.Ordinal);
            using (SHA256 sha = SHA256.Create()) return Hex(sha.ComputeHash(Encoding.UTF8.GetBytes(string.Join("\n", rows.ToArray()))));
        }

        // The protected test plugin invokes this on Unity's main thread. No arbitrary paths or code.
        internal static object TestBridge(IDictionary<string, object> args)
        {
            if (!HotReloadBoundary.Enabled) throw new InvalidOperationException("Active title-screen activation mode required.");
            object value;
            string action = args.TryGetValue("action", out value) ? Convert.ToString(value) : "status";
            bool accepted = true;
            if (action == "apply") accepted = ApplyPending(null);
            else if (action == "inject")
            {
                if (Busy || Faulted) throw new InvalidOperationException("Cannot change injection during activation.");
                failurePoint = args.TryGetValue("point", out value) ? Convert.ToString(value) : null;
                if (failurePoint != "after-reset" && failurePoint != "after-load" && failurePoint != "after-cache" && failurePoint != "before-commit")
                    throw new ArgumentException("Unknown failure point.");
            }
            else if (action != "status" && action != "audit") throw new ArgumentException("Unknown hot reload action.");
            Dictionary<string, object> result = PaladinResourceState.Diagnostics();
            result["ok"] = accepted; result["busy"] = Busy; result["faulted"] = Faulted;
            result["notice"] = Notice; result["epoch"] = Epoch; result["identity"] = Identity();
            result["blockedReason"] = UnavailableReason(); result["sealedReason"] = HotReloadBoundary.SealReason;
            result["active"] = Id(MarketplaceRuntime.Active); result["pending"] = Id(MarketplaceRuntime.Pending);
            result["pid"] = Process.GetCurrentProcess().Id;
            result["timings"] = new Dictionary<string, object>(timings);
            result["scene"] = UnityEngine.SceneManagement.SceneManager.GetActiveScene().name;
            uiStartGame title = UnityEngine.Object.FindObjectOfType<uiStartGame>();
            result["titleScene"] = title == null ? null : title.gameObject.scene.name;
            result["titleObserved"] = HotReloadBoundary.TitleObserved;
            result["managedBytes"] = GC.GetTotalMemory(false);
            result["nativeAllocatedBytes"] = UnityEngine.Profiling.Profiler.GetTotalAllocatedMemoryLong();
            if (action == "audit")
            {
                Dictionary<string, object> ids = new Dictionary<string, object>();
                foreach (KeyValuePair<Type, Dictionary<string, int>> table in ContentRegistry.CustomIds)
                    foreach (KeyValuePair<string, int> row in table.Value) ids[table.Key.Name + ":" + row.Key] = row.Value;
                result["ids"] = ids;
                Dictionary<string, object> nativeTables = new Dictionary<string, object>();
                foreach (Type tableType in new[] { typeof(FTK_playerGameStartDB), typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB), typeof(FTK_proficiencyTableDB), typeof(FTK_characterModifierDB) })
                {
                    object database = TableManager.Instance.Get(tableType);
                    System.Collections.IDictionary index = Reflect.GetField(database, "m_Dictionary") as System.Collections.IDictionary;
                    System.Collections.IList rows = Reflect.GetField(database, "m_Array") as System.Collections.IList;
                    int unresolved = 0;
                    foreach (object row in rows)
                    {
                        string key = Convert.ToString(Reflect.GetField(row, "m_ID"));
                        int id = Convert.ToInt32(Reflect.Invoke(database, "GetIntFromID", key));
                        if (!object.ReferenceEquals(row, Reflect.Invoke(database, "GetEntryByInt", id))) unresolved++;
                    }
                    nativeTables[tableType.Name] = new Dictionary<string, object> { { "rows", rows.Count }, { "indexNull", index == null }, { "indexCount", index == null ? 0 : index.Count }, { "unresolvedRows", unresolved } };
                }
                result["nativeTables"] = nativeTables;
                result["proficiencyInstances"] = ProficiencyManager.Instance.m_ProficiencyTable.Count;
                result["proficiencyChildren"] = ProficiencyManager.Instance.transform.childCount;
                Dictionary<string, object> counts = new Dictionary<string, object>();
                foreach (Type type in new[] { typeof(GameObject), typeof(Texture2D), typeof(Sprite), typeof(Mesh), typeof(Material) })
                    counts[type.Name] = Resources.FindObjectsOfTypeAll(type).Length;
                result["unityObjects"] = counts;
                foreach (FTK_weaponStats2 row in Content.Db<FTK_weaponStats2DB>().m_Array)
                    if (row.m_ID == "paladin_hammer_1h_novice") result["noviceHammerDamage"] = Reflect.GetField(row, "_maxdmg");
            }
            return result;
        }

    }
}
