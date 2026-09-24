using System;
using System.IO;
using System.Threading;
using BepInEx;
using FTKModFramework.Core.HotReload;

namespace FTKModFramework.Core.Reporting
{
    // Unity reads stay on the caller thread. One background owner serializes all storage work.
    internal static class ReportingRuntime
    {
        private static readonly object Gate = new object();
        private static readonly AutoResetEvent Wake = new AutoResetEvent(false);
        private static Thread worker;
        private static bool sourcesReady, captureRequested, quitting, dismissRequested, dismissBusy, trackingAvailable, stopped;
        private static int observedEpoch = -1;
        private static string notice = "Restart tracking is starting.";
        internal static bool TrackingAvailable { get { lock (Gate) return trackingAvailable; } }
        internal static string Notice { get { lock (Gate) return notice; } }
        private static string phase = "startup", queuedMetadata, queuedPhase;
        private static DateTime queuedAt;
        private static ReportingIncident pending;
        private static Action<bool> dismissCompletion;
        private static Action delivery;
        private static ReportingDraft savedDraft, queuedDraft;
        private static bool draftsReady, saveBusy;
        private static Action<bool> saveCompletion;
        private static string queuedDeleteId;
        private static ReportingExportRequest queuedExport;
        private static Action<ReportingExportArtifact> exportCompletion;
        private static bool exportBusy;
        internal static void ExportReviewed(string reportId, long revision, string captureId, string reportText,
            string diagnosticsText, Action<ReportingExportArtifact> completed)
        {
            ReportingExportRequest snapshot;
            try { snapshot = ReportingExportRequest.Create(reportId, revision, captureId, reportText, diagnosticsText); }
            catch { lock (Gate) { delivery += delegate { if (completed != null) completed(null); }; } return; }
            lock (Gate)
            {
                if (quitting || worker == null || stopped || !draftsReady || exportBusy)
                { delivery += delegate { if (completed != null) completed(null); }; return; }
                exportBusy = true; queuedExport = snapshot; exportCompletion = completed;
            }
            Wake.Set();
        }
        internal static bool DraftsReady { get { lock (Gate) return draftsReady; } }
        internal static ReportingDraft SavedDraft
        { get { lock (Gate) return savedDraft == null || DateTime.UtcNow - savedDraft.SavedAtUtc >= TimeSpan.FromDays(7) ? null : savedDraft.Copy(); } }
        internal static void SaveDraft(ReportingReport report, string description, Action<bool> completed)
        {
            ReportingDraft snapshot;
            try { snapshot = ReportingDraft.Create(report, description, DateTime.UtcNow); }
            catch { lock (Gate) { delivery += delegate { if (completed != null) completed(false); }; } return; }
            lock (Gate)
            {
                if (quitting || worker == null || stopped || !draftsReady || saveBusy)
                { delivery += delegate { if (completed != null) completed(false); }; return; }
                saveBusy = true; queuedDraft = snapshot; saveCompletion = completed;
            }
            Wake.Set();
        }
        internal static void DeleteDraft(string expectedReportId, Action<bool> completed)
        {
            lock (Gate)
            {
                if (quitting || worker == null || stopped || !draftsReady || saveBusy ||
                    !ReportingDraft.ValidId(expectedReportId) || savedDraft == null || savedDraft.Report.ReportId != expectedReportId)
                { delivery += delegate { if (completed != null) completed(false); }; return; }
                saveBusy = true; queuedDeleteId = expectedReportId; saveCompletion = completed;
            }
            Wake.Set();
        }
        internal static ReportingIncident Pending
        {
            get
            {
                lock (Gate)
                    return dismissBusy || pending == null || DateTime.UtcNow - pending.StartedAtUtc >= TimeSpan.FromDays(7) ? null : pending.Copy();
            }
        }

        internal static void Start()
        {
            try
            {
            lock (Gate)
            {
                if (worker != null) return;
                // No game singleton, plugin dictionary or registry is sampled during early Awake.
                DateTime now = DateTime.UtcNow;
                queuedMetadata = ReportingMetadata.Capture(new ReportingMetadataInput {
                    FrameworkVersion = Plugin.Version, Platform = Environment.OSVersion.Platform.ToString(),
                    RuntimeVersion = Environment.Version.ToString(), ProcessBits = IntPtr.Size * 8,
                    Phase = "startup_or_unknown", SourcesReady = false }, now);
                queuedPhase = "startup"; queuedAt = now;
                string root = Path.Combine(Paths.BepInExRootPath, "Reporting");
                worker = new Thread(delegate() { Run(root); });
                worker.IsBackground = true;
                worker.Name = "FTK reporting storage";
                worker.Start();
            }
            }
            catch { lock (Gate) { stopped = true; draftsReady = true; notice = "Restart tracking is unavailable for this launch."; } }
        }

        internal static void SourcesReady() { sourcesReady = true; captureRequested = true; }
        internal static void ObservePhase(string observedPhase)
        {
            if (observedPhase != "title" && observedPhase != "session_or_transition") return;
            if (phase != observedPhase) { phase = observedPhase; captureRequested = true; }
        }
        internal static void Tick()
        {
            Action completed;
            lock (Gate) { completed = delivery; delivery = null; }
            if (completed != null)
                foreach (Delegate callback in completed.GetInvocationList())
                    try { ((Action)callback)(); } catch { /* Each completion owns its cleanup even if another UI callback fails. */ }
            if (sourcesReady && !HotReloadCoordinator.Busy && !HotReloadCoordinator.Faulted && observedEpoch != HotReloadCoordinator.Epoch)
            { observedEpoch = HotReloadCoordinator.Epoch; captureRequested = true; }
            if (quitting || !captureRequested) return;
            captureRequested = false;
            try
            {
                string metadata = ReportingSources.Capture(sourcesReady);
                lock (Gate)
                {
                    if (quitting) return;
                    // Coalesce superseded phase observations; never grow an unbounded work queue.
                    queuedMetadata = metadata; queuedPhase = phase; queuedAt = DateTime.UtcNow;
                }
                Wake.Set();
            }
            catch { /* A missing observation leaves the prior committed checkpoint intact. */ }
        }
        internal static ReportingReport CreateReport(bool previous)
        { return ReportingReport.Create(delegate { return ReportingSources.Capture(sourcesReady); }, previous ? Pending : null, DateTime.UtcNow); }
        internal static ReportingReport CreateReport(ReportingIncident previous)
        { return ReportingReport.Create(delegate { return ReportingSources.Capture(sourcesReady); }, previous, DateTime.UtcNow); }
        internal static void DismissPending(Action<bool> completed)
        {
            lock (Gate)
            {
                if (quitting || worker == null || stopped || dismissBusy)
                {
                    if (completed != null) completed(false);
                    return;
                }
                dismissBusy = true; dismissRequested = true; dismissCompletion = completed;
                pending = null;
            }
            Wake.Set();
        }
        internal static void Quit()
        {
            Thread owned;
            lock (Gate) { quitting = true; owned = worker; }
            Wake.Set();
            // This records callback observation, not proof of process exit. A blocked filesystem
            // cannot hold up shutdown indefinitely; an unfinished write may offer again next launch.
            if (owned != null && owned.IsAlive) owned.Join(2000);
        }
        private static void Run(string root)
        {
            ReportingSessionStore store = null;
            ReportingDraftStore drafts = null;
            Action<bool> activeSave = null;
            Action<ReportingExportArtifact> activeExport = null;
            ReportingExportStore exports = null;
            try
            {
                ReportingSessionStore.TryOpen(root, DateTime.UtcNow, out store);
                if (store != null) ReportingDiagnostics.BindSession(store, root);
                ReportingDraftStore.TryOpen(store, root, DateTime.UtcNow, out drafts);
                if (store != null) { exports = new ReportingExportStore(store, root); exports.PruneExpired(DateTime.UtcNow); }
                lock (Gate)
                {
                    pending = store == null ? null : store.Pending;
                    savedDraft = drafts == null ? null : drafts.Saved; draftsReady = true;
                    trackingAvailable = store != null;
                    notice = trackingAvailable ? "Restart tracking is available." : "Restart tracking is unavailable for this launch.";
                }
                while (true)
                {
                    string metadata, observedPhase;
                    DateTime observedAt;
                    bool dismiss, quit;
                    Action<bool> callback;
                    ReportingDraft save;
                    string deleteId;
                    ReportingExportRequest export;
                    lock (Gate)
                    {
                        metadata = queuedMetadata; observedPhase = queuedPhase; observedAt = queuedAt; queuedMetadata = null;
                        dismiss = dismissRequested; dismissRequested = false;
                        callback = dismissCompletion; dismissCompletion = null;
                        quit = quitting;
                        save = queuedDraft; queuedDraft = null;
                        deleteId = queuedDeleteId; queuedDeleteId = null;
                        activeSave = saveCompletion; saveCompletion = null;
                        export = queuedExport; queuedExport = null;
                        activeExport = exportCompletion; exportCompletion = null;
                    }
                    if (metadata != null && store != null && !store.TryCheckpoint(metadata, observedPhase, observedAt))
                        lock (Gate) { trackingAvailable = false; notice = "Restart tracking is unavailable for this launch."; }
                    if (save != null || deleteId != null)
                    {
                        bool success = drafts != null && (deleteId != null ? drafts.TryDelete(deleteId, DateTime.UtcNow) : drafts.TrySave(save, DateTime.UtcNow));
                        Action<bool> completedSave = activeSave;
                        lock (Gate)
                        {
                            if (success) savedDraft = drafts.Saved;
                            delivery += delegate { lock (Gate) { saveBusy = false; } if (completedSave != null) completedSave(success); };
                        }
                        activeSave = null;
                    }
                    if (export != null)
                    {
                        ReportingExportArtifact artifact = exports == null ? null : exports.TryPublish(export, DateTime.UtcNow);
                        Action<ReportingExportArtifact> callbackExport = activeExport;
                        lock (Gate) delivery += delegate { lock (Gate) { exportBusy = false; } if (callbackExport != null) callbackExport(artifact); };
                        activeExport = null;
                    }
                    if (dismiss)
                    {
                        bool success = store != null && store.TryDismissPending(DateTime.UtcNow);
                        lock (Gate)
                        {
                            if (!success) { trackingAvailable = false; notice = "Dismissal could not be saved. The offer may return next launch."; }
                            delivery += delegate { lock (Gate) { dismissBusy = false; } if (callback != null) callback(success); };
                        }
                    }
                    lock (Gate) { pending = store == null ? null : store.Pending; }
                    ReportingDiagnostics.Flush();
                    if (quit)
                    {
                        if (store != null) store.TryRecordShutdown(DateTime.UtcNow);
                        return;
                    }
                    Wake.WaitOne(2000, false);
                }
            }
            catch
            {
                lock (Gate)
                {
                    pending = null; trackingAvailable = false; draftsReady = true;
                    Action<ReportingExportArtifact> failedExport = activeExport ?? exportCompletion;
                    exportCompletion = null; queuedExport = null;
                    if (exportBusy) delivery += delegate { lock (Gate) { exportBusy = false; } if (failedExport != null) failedExport(null); };
                    Action<bool> failedSave = activeSave ?? saveCompletion;
                    saveCompletion = null; queuedDraft = null; queuedDeleteId = null;
                    if (saveBusy) delivery += delegate { lock (Gate) { saveBusy = false; } if (failedSave != null) failedSave(false); };
                    notice = "Restart tracking is unavailable for this launch.";
                    Action<bool> callback = dismissCompletion;
                    dismissCompletion = null;
                    if (callback != null) delivery += delegate { lock (Gate) { dismissBusy = false; } callback(false); };
                }
            }
            finally { if (store != null) store.Dispose(); lock (Gate) { stopped = true; trackingAvailable = false; } }
        }
    }
}
