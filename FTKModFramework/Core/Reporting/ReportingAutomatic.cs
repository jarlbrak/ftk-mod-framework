using System;
using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FTKModFramework.Core.Reporting
{
    // Automatic reports use the same durable, idempotent delivery path as the manual editor, but
    // are best effort: a payload that failed this launch never blocks a newer report.
    internal static class ReportingAutomatic
    {
        private const int HistoryLimit = 32;
        private static readonly TimeSpan HistoryAge = TimeSpan.FromDays(30);
        private static string attemptedPayload, attemptedErrorId, attemptedSessionId, failure;
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
            // A payload kept from an earlier launch gets one attempt per launch with its original
            // ID and bytes, so the service can reconcile an uncertain earlier send.
            string retained = ReportingSubmission.PendingAutomaticPayload;
            if (retained != null && retained != attemptedPayload && Automatic(retained))
            {
                Start(retained, null, PreviousSessionId(retained), Fingerprint(retained));
                return;
            }

            // An unexpected exit is chosen before an error. It stays pending in the session store
            // until a report settles it, so dropping its payload after a failure that sent nothing
            // lets the next launch report it again.
            ReportingIncident prior = ReportingRuntime.Pending;
            bool incident = prior != null && prior.SessionId != attemptedSessionId &&
                (retained == null || prior.SessionId != PreviousSessionId(retained));
            ReportingDiagnosticsError error = incident ? null : ReportingDiagnostics.PendingError;
            if (error != null && error.Id == attemptedErrorId) error = null;
            string fingerprint = error == null ? null : Digest(error.Signature);
            if (error != null && RecentlyReported(fingerprint, DateTime.UtcNow))
            { ReportingDiagnostics.Acknowledge(error.Id); return; }
            if (!incident && error == null) return;
            // Any retained payload here already failed this launch. Replace it with the newer report.
            if (retained != null) { Replace(retained); return; }

            if (incident)
            {
                ReportingReport report = ReportingRuntime.CreateReport(prior);
                string payload = ReportingSubmissionPayload.Create(report, "The previous game session ended unexpectedly.",
                    "unexpected_exit", ReportingDiagnostics.CaptureCurrent(), ReportingDiagnostics.CapturePrevious(prior.SessionId), true);
                if (Start(payload, null, prior.SessionId, null)) attemptedSessionId = prior.SessionId;
                return;
            }

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
                if (!result.Success)
                {
                    failure = result.Error;
                    // The submitter already deleted a permanently refused payload. Settle its
                    // subject too, or every launch would rebuild and resend it under a new ID.
                    if (!ReportingSubmissionPayload.PermanentFailure(result.Error)) return;
                }
                Settle(fingerprint, errorId, sessionId);
            });
            if (started) { attemptedPayload = payload; failure = null; }
            return started;
        }

        private static void Replace(string retained)
        {
            // Only a failure that proves nothing was created leaves the subject reportable. After
            // an uncertain one (timeout, transport error), the report may already be an issue, so
            // the error is remembered and the incident dismissed rather than resent under a new ID.
            bool settle = retained != attemptedPayload || !ReportingSubmissionPayload.NotDelivered(failure);
            string fingerprint = Fingerprint(retained), sessionId = PreviousSessionId(retained);
            ReportingSubmission.DiscardAutomatic(retained, delegate(bool discarded) {
                if (!discarded) { nextAttemptUtc = DateTime.UtcNow.AddSeconds(30); return; }
                if (settle) Settle(fingerprint, null, sessionId);
            });
        }

        private static void Settle(string fingerprint, string errorId, string sessionId)
        {
            try
            {
                if (fingerprint != null) Remember(fingerprint, DateTime.UtcNow);
                if (errorId != null) ReportingDiagnostics.Acknowledge(errorId);
                ReportingIncident pending = ReportingRuntime.Pending;
                if (sessionId != null && pending != null && pending.SessionId == sessionId)
                    ReportingRuntime.DismissPending(null);
            }
            catch { /* The queue outcome stands if local bookkeeping fails. */ }
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
