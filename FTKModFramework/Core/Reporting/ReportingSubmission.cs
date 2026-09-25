using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using BepInEx;
using Newtonsoft.Json.Linq;
using FTKModFramework.Core.Marketplace;

namespace FTKModFramework.Core.Reporting
{
    internal sealed class ReportingSubmissionResult
    {
        internal bool Success;
        internal string IssueUrl, Error, ReportId;
        internal int IssueNumber;
    }

    // The native helper owns HTTPS. The shipped game's Mono TLS stack is not a transport authority.
    internal static class ReportingSubmission
    {
        internal const string Endpoint = "https://reporting-api-production-ff50.up.railway.app/v1/reports";
        private static readonly object Gate = new object();
        private static bool initialized, ready, busy;
        private static string pending;
        private static Action delivery;
        internal static bool Busy { get { lock (Gate) return busy; } }
        internal static bool Ready { get { lock (Gate) return ready; } }
        internal static string PendingPayload { get { lock (Gate) return pending; } }
        private static string Root { get { return Path.Combine(Paths.BepInExRootPath, "ReportingDelivery"); } }
        internal static void Initialize()
        {
            lock (Gate) { if (initialized) return; initialized = true; }
            Thread thread = new Thread(delegate() {
                try { using (ReportingSessionLease lease = new ReportingSessionLease()) { lease.Acquire(Root); ReadPending(); } }
                catch { }
                finally { lock (Gate) ready = true; }
            });
            thread.IsBackground = true; thread.Start();
        }
        internal static void Tick()
        {
            Action callback;
            lock (Gate) { callback = delivery; delivery = null; }
            if (callback != null) callback();
        }
        internal static void DiscardPending(string expectedPayload, Action<bool> completed)
        {
            lock (Gate) { if (busy || !ready) { delivery += delegate { completed(false); }; return; } busy = true; }
            Thread thread = new Thread(delegate() {
                bool success = false;
                try
                {
                    using (ReportingSessionLease lease = new ReportingSessionLease())
                    {
                        lease.Acquire(Root);
                        string path = Path.Combine(Root, "pending.json");
                        if (File.Exists(path))
                        {
                            SafeFile(path, ReportingSubmissionPayload.MaximumBytes);
                            if (File.ReadAllText(path) != expectedPayload) throw new IOException();
                            File.Delete(path);
                        }
                        lock (Gate) pending = null;
                        success = true;
                    }
                }
                catch { }
                lock (Gate) delivery += delegate { lock (Gate) busy = false; completed(success); };
            });
            thread.IsBackground = true;
            try { thread.Start(); } catch { lock (Gate) { busy = false; delivery += delegate { completed(false); }; } }
        }
        internal static bool Start(string json, Action<ReportingSubmissionResult> completed)
        {
            lock (Gate) { if (!ready || busy) return false; busy = true; }
            Thread thread = new Thread(delegate() {
                ReportingSubmissionResult result;
                try { result = Submit(json); }
                catch { result = new ReportingSubmissionResult { Error = "local_delivery_failed" }; }
                lock (Gate) delivery += delegate { lock (Gate) busy = false; if (completed != null) completed(result); };
            });
            thread.IsBackground = true;
            try { thread.Start(); return true; }
            catch { lock (Gate) busy = false; return false; }
        }
        private static void ReadPending()
        {
            string path = Path.Combine(Root, "pending.json");
            string json = null;
            if (File.Exists(path))
            {
                SafeFile(path, ReportingSubmissionPayload.MaximumBytes);
                // Automatic retention cleanup never initiates a network request.
                if (DateTime.UtcNow - File.GetLastWriteTimeUtc(path) > TimeSpan.FromDays(7)) File.Delete(path);
                else json = File.ReadAllText(path);
            }
            lock (Gate) pending = json;
        }
        private static ReportingSubmissionResult Submit(string json)
        {
            if (json == null || Encoding.UTF8.GetByteCount(json) > ReportingSubmissionPayload.MaximumBytes) throw new ArgumentException();
            JObject request = JObject.Parse(json);
            string id = (string)request["reportId"];
            if (!ReportingDraft.ValidId(id)) throw new ArgumentException();
            string requestHash;
            using (SHA256 hash = SHA256.Create())
                requestHash = BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(json))).Replace("-", "").ToLowerInvariant();
            using (ReportingSessionLease lease = new ReportingSessionLease())
            {
                lease.Acquire(Root); ReadPending();
                string original = PendingPayload;
                if (original != null && original != json) return new ReportingSubmissionResult { Error = "pending_report_exists" };
                string helper = Path.Combine(Path.Combine(Paths.BepInExRootPath, "ftkmf"),
                    Environment.OSVersion.Platform == PlatformID.Win32NT ? "ftkmf-launcher-helper.exe" : "ftkmf-launcher-helper");
                string requestPath = Path.Combine(Root, "pending.json");
                if (original == null)
                {
                    AtomicWrite(requestPath, json);
                    lock (Gate) pending = json;
                }
                string receiptPath = Path.Combine(Root, "submitted.json");
                if (File.Exists(receiptPath))
                {
                    SafeFile(receiptPath, 65536);
                    ReportingSubmissionResult prior = ReadReceipt(File.ReadAllText(receiptPath), id, requestHash);
                    if (prior != null && prior.Success) { File.Delete(requestPath); lock (Gate) pending = null; return prior; }
                }
                try { MarketplaceProtocol.VerifyHelper(helper); }
                catch { return new ReportingSubmissionResult { Error = "helper_unavailable", ReportId = id }; }
                string resultPath = Path.Combine(Root, "result.json");
                if (File.Exists(resultPath)) { SafeFile(resultPath, 65536); File.Delete(resultPath); }
                ProcessStartInfo info = new ProcessStartInfo(helper,
                    "report-submit --request " + Quote(requestPath) + " --result " + Quote(resultPath) + " --endpoint " + Quote(Endpoint));
                info.UseShellExecute = false; info.CreateNoWindow = true; info.WorkingDirectory = Root;
                PrepareHelperEnvironment(info);
                using (Process process = Process.Start(info))
                {
                    if (!process.WaitForExit(40000))
                    {
                        try { process.Kill(); process.WaitForExit(2000); } catch { }
                        return new ReportingSubmissionResult { Error = "submission_pending", ReportId = id };
                    }
                    if (!File.Exists(resultPath)) return new ReportingSubmissionResult { Error = "helper_unavailable", ReportId = id };
                }
                SafeFile(resultPath, 65536);
                string response = File.ReadAllText(resultPath);
                ReportingSubmissionResult result = ParseResult(response, id);
                if (result.Success)
                {
                    JObject receipt = JObject.Parse(response);
                    receipt["requestSha256"] = requestHash;
                    AtomicWrite(receiptPath, receipt.ToString(Newtonsoft.Json.Formatting.None));
                    File.Delete(requestPath);
                    lock (Gate) pending = null;
                }
                File.Delete(resultPath);
                return result;
            }
        }
        private static ReportingSubmissionResult ReadReceipt(string json, string expectedId, string requestHash)
        {
            try
            {
                // Identity alone is insufficient: edits can keep an ID while changing the
                // frozen request. Legacy receipts must go through server idempotency again.
                JToken storedHash = JObject.Parse(json)["requestSha256"];
                if (storedHash == null || storedHash.Type != JTokenType.String || (string)storedHash != requestHash) return null;
                return ParseResult(json, expectedId);
            }
            catch { return null; }
        }
        internal static void PrepareHelperEnvironment(ProcessStartInfo info)
        {
            // The Steam wrapper injects Doorstop into the game. Carrying that environment into
            // the standalone helper can crash its native loader before it writes a result.
            string[] names = new string[info.EnvironmentVariables.Count];
            info.EnvironmentVariables.Keys.CopyTo(names, 0);
            foreach (string name in names)
                if (String.Equals(name, "DYLD_INSERT_LIBRARIES", StringComparison.OrdinalIgnoreCase) ||
                    String.Equals(name, "LD_PRELOAD", StringComparison.OrdinalIgnoreCase) ||
                    name.StartsWith("DOORSTOP_", StringComparison.OrdinalIgnoreCase))
                    info.EnvironmentVariables.Remove(name);
        }
        internal static ReportingSubmissionResult ParseResult(string json, string expectedId)
        {
            try
            {
                JObject response = JObject.Parse(json);
                if (!ReportingDraft.ValidId(expectedId) || response["schemaVersion"] == null || response["schemaVersion"].Type != JTokenType.Integer ||
                    response["status"] == null || response["status"].Type != JTokenType.String) throw new FormatException();
                if ((int?)response["schemaVersion"] != 1) throw new FormatException();
                string state = (string)response["status"];
                if (state != "submitted") return new ReportingSubmissionResult { Error = (string)response["error"] ?? "submission_pending", ReportId = expectedId };
                if (response["issueNumber"] == null || response["issueNumber"].Type != JTokenType.Integer ||
                    response["reportId"] == null || response["reportId"].Type != JTokenType.String ||
                    response["issueUrl"] == null || response["issueUrl"].Type != JTokenType.String) throw new FormatException();
                int number = (int?)response["issueNumber"] ?? 0;
                string url = (string)response["issueUrl"];
                if ((string)response["reportId"] != expectedId || number <= 0 || url != "https://github.com/jarlbrak/ftk-mod-framework/issues/" + number.ToString(System.Globalization.CultureInfo.InvariantCulture)) throw new FormatException();
                return new ReportingSubmissionResult { Success = true, IssueNumber = number, IssueUrl = url, ReportId = expectedId };
            }
            catch { return new ReportingSubmissionResult { Error = "invalid_response", ReportId = expectedId }; }
        }
        private static void SafeFile(string path, int maximum)
        {
            if ((File.GetAttributes(path) & FileAttributes.ReparsePoint) != 0 || new FileInfo(path).Length > maximum) throw new IOException();
        }
        private static void AtomicWrite(string path, string text)
        {
            string temp = path + ".tmp";
            if (File.Exists(path)) SafeFile(path, ReportingSubmissionPayload.MaximumBytes);
            if (File.Exists(temp)) { SafeFile(temp, ReportingSubmissionPayload.MaximumBytes); File.Delete(temp); }
            using (FileStream stream = new FileStream(temp, FileMode.CreateNew, FileAccess.Write, FileShare.None))
            {
                byte[] bytes = Encoding.UTF8.GetBytes(text); stream.Write(bytes, 0, bytes.Length); stream.Flush();
            }
            if (File.Exists(path)) File.Delete(path);
            File.Move(temp, path);
        }
        private static string Quote(string value)
        {
            if (value.IndexOfAny(new[] { '\r', '\n', '"' }) >= 0) throw new ArgumentException();
            return "\"" + value + "\"";
        }
    }
}
