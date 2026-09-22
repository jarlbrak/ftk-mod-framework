using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Threading;
using BepInEx;
using Newtonsoft.Json;
using FTKModFramework.Core.Data;

namespace FTKModFramework.Core.Marketplace
{
    internal sealed class MarketplaceOperation
    {
        internal Process Process;
        internal string Id;
        internal string ResultPath;
        internal Stopwatch Clock;
        internal int TimeoutMs;
        internal bool CannotStop;
        internal bool DeferCompletion;
        internal Action<MarketplaceResult> Complete;
    }

    internal static class MarketplaceRuntime
    {
        internal static ManagedSnapshot Active { get; private set; }
        internal static ManagedSnapshot Pending { get; private set; }
        internal static MarketplaceResult Catalog { get; private set; }
        // True after the helper reported an unsupported catalog schema or returned a result this
        // framework cannot read. Cleared by the next catalog result that is stored.
        internal static bool CatalogUnsupported { get; private set; }
        internal static bool PreviousAvailable { get; private set; }
        internal static string Notice = "Marketplace has not been opened.";
        internal static string RegistrationNotice;
        internal static bool Busy { get { return _operation != null; } }
        private static MarketplaceOperation _operation;
        // Hot-reload completion rebuilds game tables and native caches. Do not return from that
        // work through Process polling on the same Mono update stack: the shipped runtime has
        // faulted there after the callback completed. The coordinator dispatches it at the start
        // of the following Unity update, after Poll has fully unwound.
        private static Action<MarketplaceResult> _deferredHotReloadComplete;
        private static MarketplaceResult _deferredHotReloadResult;
        internal static Func<bool> MutationsBlocked;
        private static bool _initialized;
        internal static bool CanDiscover = true;
        internal static string LeaseFailure;
        internal static bool BootstrapVerified;
        internal static bool EnsureEmptyGeneration;
        private static Dictionary<string, object> _activeSettings;
        private sealed class StartupJob
        {
            internal readonly object Sync = new object();
            internal bool Cancelled;
            internal MarketplaceOperation Operation;
            internal MarketplaceResult Prior;
            internal MarketplaceResult Result;
            internal Exception Error;
            internal Exception CaptureError;
        }
        internal static string StateRoot { get { return Path.Combine(Path.Combine(Paths.GameRootPath, "BepInEx"), "ftkmf/marketplace"); } }
        private static string HelperPath
        {
            get { return Path.Combine(Path.Combine(Paths.GameRootPath, "BepInEx/ftkmf"),
                Environment.OSVersion.Platform == PlatformID.Win32NT ? "ftkmf-launcher-helper.exe" : "ftkmf-launcher-helper"); }
        }

        internal static void InitializeBeforeDiscovery()
        {
            if (_initialized) return;
            _initialized = true;
            _activeSettings = new Dictionary<string, object>();
            _activeSettings["EnableSampleContent"] = Plugin.EnableSampleContent.Value;
            _activeSettings["EnableDataContent"] = Plugin.EnableDataContent.Value;
            _activeSettings["EnableBehaviorLoading"] = Plugin.EnableBehaviorLoading.Value;
            _activeSettings["EnableCampaignEngine"] = Plugin.EnableCampaignEngine.Value;
            _activeSettings["RunSelfTests"] = Plugin.SelfTestsEnabled;
            _activeSettings["ForceCustomEnemy"] = Plugin.ForceCustomEnemy.Value;
            _activeSettings["ForceCustomEncounter"] = Plugin.ForceCustomEncounter.Value;
            _activeSettings["DiagnosticsEnableGate"] = Plugin.DiagnosticsEnableGate.Value;
            _activeSettings["SyntheticContentCount"] = Plugin.SyntheticContentCount.Value;
            _activeSettings["SyntheticContentKind"] = Plugin.SyntheticContentKind.Value;
            _activeSettings["SyntheticContentTemplate"] = Plugin.SyntheticContentTemplate.Value;
            _activeSettings["SyntheticCampaignStages"] = Plugin.SyntheticCampaignStages.Value;
            _activeSettings["SyntheticCampaignQuestsPerStage"] = Plugin.SyntheticCampaignQuestsPerStage.Value;
            _activeSettings["manualModsFingerprintComplete"] = false;
            StartupJob job = new StartupJob();
            Thread worker = new Thread(delegate() { RunStartup(job); });
            worker.IsBackground = true;
            worker.Start();
            // Reserve one second of the five-second gate for termination and reaping.
            bool finished = worker.Join(4000);
            MarketplaceOperation operation;
            lock (job.Sync)
            {
                job.Cancelled = !finished;
                operation = job.Operation;
                if (job.Prior != null)
                {
                    Active = job.Prior.Active;
                    Pending = job.Prior.Pending;
                    PreviousAvailable = job.Prior.PreviousAvailable;
                }
                if (finished && job.Result != null && job.Result.Ok)
                {
                    BootstrapVerified = true;
                    Active = job.Result.Active;
                    Pending = job.Result.Pending;
                    PreviousAvailable = job.Result.PreviousAvailable;
                    Notice = job.Result.Message ?? "Managed content selected for this launch.";
                }
                else Notice = finished ? (job.Error == null ? "Managed activation failed." : job.Error.Message) :
                    (job.Prior != null && job.Prior.Active != null
                        ? "Activation exceeded its startup budget. This session keeps the captured previous managed set. Restart or repair before retrying."
                        : "Activation exceeded its startup budget before a trusted managed selection was available. No managed content is loaded this launch; restart or repair before retrying.");
            }
            if (!finished && operation != null)
            {
                try { Stop(operation); }
                catch (Exception e)
                {
                    CanDiscover = false;
                    operation.CannotStop = true;
                    _operation = operation;
                    Notice = "Activation helper could not be reaped. Data-mod discovery is disabled for this launch. Quit and repair before restarting. " + e.Message;
                }
            }
            if (operation != null && (finished || CanDiscover)) operation.Process.Dispose();
            if (!finished || job.Error != null) Plugin.Log.LogWarning("Marketplace startup: " + Notice);
        }

        private static void RunStartup(StartupJob job)
        {
            try
            {
                try { MarketplaceRuntimeLease.Acquire(StateRoot); }
                catch (Exception leaseError) { CanDiscover = false; LeaseFailure = leaseError.Message; throw; }
                // Capture before invoking activate. Worker never mutates the process's Active snapshot.
                try
                {
                    MarketplaceResult prior = MarketplaceProtocol.ReadState(StateRoot);
                    lock (job.Sync) { if (job.Cancelled) return; job.Prior = prior; }
                }
                catch (Exception captureError)
                {
                    // An unreadable old record is not a trusted fallback, but must not prevent the
                    // offline helper from validating and recovering a complete pending generation.
                    // The returned replacement still passes all protocol and snapshot checks below.
                    lock (job.Sync)
                    {
                        if (job.Cancelled) return;
                        job.CaptureError = captureError;
                    }
                }
                MarketplaceOperation operation = Launch("activate", null, false, 5000, null, job, null);
                operation.Process.WaitForExit();
                if (operation.Process.ExitCode != 0) throw new IOException("Activation helper failed; the captured previous set is retained. See the operation result in BepInEx/ftkmf/marketplace/operations.");
                MarketplaceResult result = MarketplaceProtocol.ReadResult(operation.ResultPath, operation.Id);
                if (!result.Ok) throw new IOException(result.Message ?? "Managed activation failed.");
                MarketplaceProtocol.ValidateSnapshot(StateRoot, result.Active);
                MarketplaceProtocol.ValidateSnapshot(StateRoot, result.Pending);
                result.Active = MarketplaceProtocol.ReadVerifiedGeneration(StateRoot, result.Active);
                result.Pending = MarketplaceProtocol.ReadVerifiedGeneration(StateRoot, result.Pending);
                if (job.Prior != null && job.Prior.Active != null && result.Active == null)
                    throw new IOException("Activation returned no active generation despite a previously selected generation.");
                lock (job.Sync) { if (!job.Cancelled) { job.Result = result; job.Error = null; } }
            }
            catch (Exception e)
            {
                lock (job.Sync)
                {
                    job.Error = job.CaptureError == null ? e : new IOException(
                        "Managed content is unavailable this launch: the prior selection could not be read and recovery failed. " + e.Message);
                }
            }
        }

        internal static void RecordRegistrationErrors(ValidationReport report)
        {
            if (report.Errors.Count == 0) return;
            RegistrationNotice = "Content registration reported " + report.Errors.Count + " error(s). The selected generation is not proof of successful loading. See the BepInEx log; rollback applies on the next launch.";
        }

        internal static PackageDescriptor FindManaged(string guid)
        {
            if (Active != null && Active.Packages != null)
                foreach (PackageDescriptor package in Active.Packages) if (package.ModGuid == guid) return package;
            return null;
        }

        internal static List<PackageSelection> DesiredSelection()
        {
            List<PackageSelection> selection = new List<PackageSelection>();
            ManagedSnapshot snapshot = Pending ?? Active;
            if (snapshot != null && snapshot.Packages != null)
                foreach (PackageDescriptor package in snapshot.Packages)
                    selection.Add(new PackageSelection { PackageId = package.PackageId, Version = package.Version, Enabled = package.Enabled });
            return selection;
        }

        internal static bool Start(string operation, List<PackageSelection> selection, bool dryRun, Action<MarketplaceResult> complete, string expectedRevision = null)
        {
            if (LeaseFailure != null || (_initialized && !MarketplaceRuntimeLease.Acquired) || Busy || (MutationsBlocked != null && MutationsBlocked())) return false;
            try
            {
                _operation = Launch(operation, selection, dryRun, operation == "catalog" ? 17000 : 125000, complete, null, expectedRevision);
                Notice = operation == "catalog" ? "Loading curated catalog..." : "Preparing marketplace operation...";
                return true;
            }
            catch (Exception e)
            {
                Notice = e.Message;
                if (complete != null) complete(new MarketplaceResult { SchemaVersion = 1, Status = "error", Message = Notice });
                return false;
            }
        }

        internal static void Poll()
        {
            MarketplaceOperation operation = _operation;
            if (operation == null) return;
            MarketplaceResult result = null;
            try
            {
                if (!operation.Process.HasExited)
                {
                    if (operation.CannotStop || operation.Clock.ElapsedMilliseconds <= operation.TimeoutMs) return;
                    Stop(operation);
                    throw new IOException("Marketplace operation timed out. Active content is unchanged. Inspect pending changes before retrying.");
                }
                result = MarketplaceProtocol.ReadResult(operation.ResultPath, operation.Id);
                if (operation.Process.ExitCode != 0 && result.Ok) throw new IOException("Helper exited with an error despite its success result.");
                if (result.Ok)
                {
                    MarketplaceProtocol.ValidateSnapshot(StateRoot, result.Pending);
                    Pending = result.Pending;
                    PreviousAvailable = result.PreviousAvailable;
                    // Never replace Active after startup, even if a concurrent process activated another set.
                    if (result.Packages != null && (result.Status == "online" || result.Status == "offline" || result.Status == "empty" || result.Status == "unavailable"))
                    {
                        Catalog = result;
                        CatalogUnsupported = false;
                    }
                }
                else if (result.Status == "unsupported") CatalogUnsupported = true;
            }
            catch (Exception e)
            {
                if (!ConfirmedExit(operation))
                {
                    operation.CannotStop = true;
                    Notice = "The helper has not exited. New operations are blocked; retry cancellation or quit the game. " + e.Message;
                    return;
                }
                try { ReconcilePending(); } catch (Exception stateError) { Plugin.Log.LogWarning("Marketplace reconciliation: " + stateError.Message); }
                bool unsupported = e is MarketplaceProtocolException;
                if (unsupported) CatalogUnsupported = true;
                result = new MarketplaceResult { SchemaVersion = 1, Status = unsupported ? "unsupported" : "error", Message = e.Message };
            }
            finally
            {
                if (result != null)
                {
                    operation.Process.Dispose();
                    _operation = null;
                    Notice = result.Message ?? (result.Ok ? "Ready. Changes apply on next launch." : "Marketplace operation failed.");
                }
            }
            if (result != null && operation.Complete != null)
            {
                if (operation.DeferCompletion)
                {
                    if (_deferredHotReloadComplete != null)
                        throw new InvalidOperationException("A hot-reload completion is already awaiting dispatch.");
                    _deferredHotReloadComplete = operation.Complete;
                    _deferredHotReloadResult = result;
                }
                else operation.Complete(result);
            }
        }

        // Called before HotReloadCoordinator.Tick. Clear first because a completion may begin
        // the next helper operation synchronously.
        internal static void DispatchHotReloadCompletion()
        {
            Action<MarketplaceResult> complete = _deferredHotReloadComplete;
            MarketplaceResult result = _deferredHotReloadResult;
            _deferredHotReloadComplete = null;
            _deferredHotReloadResult = null;
            if (complete != null) complete(result);
        }

        internal static void RefreshPendingForHotReload() { ReconcilePending(); }

        internal static bool StartHotReload(string operation, string current, string pending, Action<MarketplaceResult> complete)
        {
            if (LeaseFailure != null || !MarketplaceRuntimeLease.Acquired || Busy) return false;
            try
            {
                _operation = Launch(operation, null, false, 17000, complete, null, null, current, pending);
                return true;
            }
            catch (Exception e)
            {
                Notice = e.Message;
                return false;
            }
        }

        internal static bool RestoreSavedSet(string fingerprint, string expectedCurrent, string expectedPending, Action<MarketplaceResult> complete)
        {
            if (LeaseFailure != null || !MarketplaceRuntimeLease.Acquired || Busy || (MutationsBlocked != null && MutationsBlocked())) return false;
            try
            {
                _operation = Launch("restore-save-set", null, false, 17000, complete, null, null, expectedCurrent, expectedPending, fingerprint);
                Notice = "Preparing saved mod set...";
                return true;
            }
            catch (Exception error) { Notice = error.Message; return false; }
        }

        internal static void PublishHotReload(ManagedSnapshot snapshot)
        {
            Active = snapshot;
            ReconcilePending();
            RegistrationNotice = null;
        }

        internal static void CancelRunning()
        {
            // Hot activation must reconcile the durable commit even if a UI cancellation races it.
            if (MutationsBlocked != null && MutationsBlocked()) return;
            MarketplaceOperation operation = _operation;
            if (operation == null) return;
            try
            {
                File.WriteAllText(Path.Combine(Path.Combine(StateRoot, "operations"), operation.Id + ".cancel"), "cancel");
                Stop(operation);
                ReconcilePending();
                Notice = "Operation cancelled. Active content is unchanged; inspect pending changes before retrying.";
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Marketplace cancellation: " + e.Message);
                if (!ConfirmedExit(operation))
                {
                    operation.CannotStop = true;
                    Notice = "The helper has not exited. New operations remain blocked. Retry cancellation or quit the game.";
                    return;
                }
                Notice = "Helper stopped, but pending state could not be reconciled: " + e.Message;
            }
            finally
            {
                if (ConfirmedExit(operation)) { operation.Process.Dispose(); _operation = null; }
            }
        }

        private static bool ConfirmedExit(MarketplaceOperation operation)
        {
            try { return operation.Process.HasExited; }
            catch { return false; }
        }

        private static void ReconcilePending()
        {
            MarketplaceResult disk = MarketplaceProtocol.ReadState(StateRoot);
            Pending = disk.Pending;
            PreviousAvailable = disk.PreviousAvailable;
            // Disk activation never replaces the selection already loaded by this process.
        }

        private static void Stop(MarketplaceOperation operation)
        {
            if (!operation.Process.HasExited) operation.Process.Kill();
            if (!operation.Process.WaitForExit(1000)) throw new IOException("Marketplace helper did not exit after termination. Active selection remains captured for this launch.");
        }

        private static MarketplaceOperation Launch(string operation, List<PackageSelection> selection, bool dryRun,
            int timeoutMs, Action<MarketplaceResult> complete, StartupJob startup, string expectedRevision, string expectedCurrent = null, string expectedPending = null, string saveFingerprint = null)
        {
            if (operation != "catalog" && operation != "prepare" && operation != "activate" && operation != "cancel" && operation != "rollback" && operation != "export" && operation != "hot-validate" && operation != "hot-commit" && operation != "collect" && operation != "restore-save-set")
                throw new ArgumentException("Unsupported marketplace operation.");
            Stopwatch clock = Stopwatch.StartNew();
            MarketplaceProtocol.VerifyHelper(HelperPath);
            MarketplaceRequest request = new MarketplaceRequest();
            request.OperationId = Guid.NewGuid().ToString("N");
            request.StateRoot = StateRoot;
            request.EnsureEmptyGeneration = EnsureEmptyGeneration;
            request.SaveFingerprint = saveFingerprint;
            // Tests preserve historical generations as evidence. Normal installs bound
            // preparation growth; the launcher collects unused generations while offline.
            request.MaxGenerationBytes = Environment.GetEnvironmentVariable("FTK_MODEL_TEST") == "1" ? 0 : 8L * 1024 * 1024 * 1024;
            request.FrameworkVersion = Plugin.Version;
            request.GameAssemblyPath = typeof(GridEditor.TableManager).Assembly.Location;
            request.Platform = Environment.OSVersion.Platform == PlatformID.Win32NT ? "windows" :
                (Directory.Exists("/System/Library/CoreServices") ? "macos" : "linux");
            request.ManualRoots = new string[] { Plugin.DataContentRootPath };
            request.ManualGuids = new string[0]; // helper scans manualRoots for named conflicts, without executing content.
            request.BundledGuids = new string[] { Plugin.Guid };
            request.Selection = selection;
            request.DryRun = dryRun;
            request.ExpectedRevision = expectedRevision;
            request.ExpectedCurrent = expectedCurrent;
            request.ExpectedPending = expectedPending;
            request.Settings = _activeSettings;
            string operations = Path.Combine(StateRoot, "operations");
            Directory.CreateDirectory(operations);
            string requestPath = Path.Combine(operations, request.OperationId + ".request.json");
            string resultPath = Path.Combine(operations, request.OperationId + ".result.json");
            File.WriteAllText(requestPath, JsonConvert.SerializeObject(request));
            ProcessStartInfo start = new ProcessStartInfo(HelperPath, "marketplace " + operation + " --request " + Quote(requestPath) + " --result " + Quote(resultPath));
            start.UseShellExecute = false;
            start.CreateNoWindow = true;
            MarketplaceOperation running = new MarketplaceOperation { Id = request.OperationId, ResultPath = resultPath, Clock = clock, TimeoutMs = timeoutMs, Complete = complete,
                DeferCompletion = operation == "hot-validate" || operation == "hot-commit" };
            if (startup == null) running.Process = Process.Start(start);
            else lock (startup.Sync)
            {
                if (startup.Cancelled) throw new IOException("Startup activation cancelled before helper launch.");
                running.Process = Process.Start(start);
                startup.Operation = running;
            }
            return running;
        }

        private static string Quote(string path)
        {
            if (path.IndexOf('"') >= 0 || path.IndexOf('\r') >= 0 || path.IndexOf('\n') >= 0) throw new IOException("Unsupported quote or newline in framework installation path.");
            return "\"" + path + "\"";
        }
    }
}
