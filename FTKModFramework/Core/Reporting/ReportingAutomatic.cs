using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FTKModFramework.Core.Reporting
{
    // Automatic reports use the same durable, idempotent delivery path as the manual editor.
    internal static class ReportingAutomatic
    {
        private const int HistoryLimit = 32;
        private static readonly TimeSpan HistoryAge = TimeSpan.FromDays(30);
        private static string attemptedPayload, attemptedErrorId, attemptedSessionId;
        private static DateTime nextAttemptUtc;

        internal static void Tick(bool enabled)
        {
            if (DateTime.UtcNow < nextAttemptUtc) return;
            try { TickCore(enabled); }
            catch { nextAttemptUtc = DateTime.UtcNow.AddSeconds(30); }
        }
        private static void TickCore(bool enabled)
        {
            if (!enabled || !ReportingSubmission.Ready || ReportingSubmission.Busy) return;
            // The manual editor owns its queue and takes priority over background work.
            if (ReportingSubmission.PendingPayload != null) return;
            string pending = ReportingSubmission.PendingAutomaticPayload;
            if (pending != null)
            {
                if (pending != attemptedPayload && Automatic(pending))
                    Start(pending, null, PreviousSessionId(pending), Fingerprint(pending));
                return;
            }

            ReportingIncident prior = ReportingRuntime.Pending;
            if (prior != null && prior.SessionId != attemptedSessionId)
            {
                ReportingReport report = ReportingRuntime.CreateReport(prior);
                string payload = ReportingSubmissionPayload.Create(report, "The previous game session ended unexpectedly.",
                    "unexpected_exit", ReportingDiagnostics.CaptureCurrent(), ReportingDiagnostics.CapturePrevious(prior.SessionId), true);
                if (Start(payload, null, prior.SessionId, null)) attemptedSessionId = prior.SessionId;
                return;
            }

            ReportingDiagnosticsError error = ReportingDiagnostics.PendingError;
            if (error == null || error.Id == attemptedErrorId) return;
            string fingerprint = Digest(error.Signature);
            if (RecentlyReported(fingerprint, DateTime.UtcNow))
            { ReportingDiagnostics.Acknowledge(error.Id); return; }
            ReportingReport current = ReportingRuntime.CreateReport(false);
            JObject request = JObject.Parse(ReportingSubmissionPayload.Create(current, error.Summary, "error",
                ReportingDiagnostics.CaptureCurrent(), "", true));
            request["fingerprint"] = fingerprint;
            string json = request.ToString(Formatting.None);
            if (Start(json, error.Id, null, fingerprint))
            {
                attemptedErrorId = error.Id;
                ReportingDiagnostics.Acknowledge(error.Id);
            }
        }

        private static bool Start(string payload, string errorId, string sessionId, string fingerprint)
        {
            bool started = ReportingSubmission.Start(payload, delegate(ReportingSubmissionResult result) {
                if (!result.Success) return;
                try
                {
                    if (fingerprint != null) Remember(fingerprint, DateTime.UtcNow);
                    if (errorId != null) ReportingDiagnostics.Acknowledge(errorId);
                    ReportingIncident pending = ReportingRuntime.Pending;
                    if (sessionId != null && pending != null && pending.SessionId == sessionId)
                        ReportingRuntime.DismissPending(null);
                }
                catch { /* A delivered report stays delivered if local bookkeeping fails. */ }
            });
            if (started) attemptedPayload = payload;
            return started;
        }

        private static bool Automatic(string payload)
        { try { return (string)JObject.Parse(payload)["submissionMode"] == "automatic"; } catch { return false; } }
        private static string Fingerprint(string payload)
        { try { return (string)JObject.Parse(payload)["fingerprint"]; } catch { return null; } }
        private static string PreviousSessionId(string payload)
        { try { return (string)JObject.Parse(payload)["diagnostics"]?["previousSession"]?["sessionId"]; } catch { return null; } }

        internal static string Digest(string signature)
        {
            using (SHA256 hash = SHA256.Create())
                return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(signature ?? ""))).Replace("-", "").ToLowerInvariant();
        }
        internal static bool RecentlyReported(string fingerprint, DateTime now)
        {
            foreach (string item in (Plugin.AutomaticBugReportHistory.Value ?? "").Split(';'))
            {
                int split = item.IndexOf(':'); long ticks;
                if (split != 64 || item.Substring(0, split) != fingerprint ||
                    !Int64.TryParse(item.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks)) continue;
                if (ticks > 0 && ticks <= now.Ticks && now.Ticks - ticks < HistoryAge.Ticks) return true;
            }
            return false;
        }
        internal static void Remember(string fingerprint, DateTime now)
        {
            if (fingerprint == null || fingerprint.Length != 64) return;
            StringBuilder value = new StringBuilder(fingerprint).Append(':').Append(now.Ticks.ToString(CultureInfo.InvariantCulture));
            int count = 1;
            foreach (string item in (Plugin.AutomaticBugReportHistory.Value ?? "").Split(';'))
            {
                int split = item.IndexOf(':'); long ticks;
                if (split != 64 || item.Substring(0, split) == fingerprint ||
                    !Int64.TryParse(item.Substring(split + 1), NumberStyles.Integer, CultureInfo.InvariantCulture, out ticks) ||
                    ticks <= 0 || ticks > now.Ticks || now.Ticks - ticks >= HistoryAge.Ticks) continue;
                value.Append(';').Append(item); if (++count >= HistoryLimit) break;
            }
            Plugin.AutomaticBugReportHistory.Value = value.ToString();
            Plugin.Instance.Config.Save();
        }
    }
}
