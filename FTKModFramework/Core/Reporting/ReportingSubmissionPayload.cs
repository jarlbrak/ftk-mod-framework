using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FTKModFramework.Core.Reporting
{
    internal static class ReportingSubmissionPayload
    {
        internal const int MaximumBytes = 128 * 1024;
        internal const string Disclosure = "Send publishes your description and selected diagnostics to the public FTK framework GitHub tracker through our Railway service. Diagnostics include recent errors, versions, mods and session context. No saves or screenshots are collected. Logs are filtered, but may contain personal information. Nothing is uploaded until you press Send. Downloadable diagnostics expire after 30 days; text published on GitHub remains public.";
        internal static string Create(ReportingReport report, string description, string kind, string currentLogs, string previousLogs)
        {
            if (report == null || !ReportingDraft.ValidId(report.ReportId) || !ReportingDraft.ValidId(report.CaptureId)) throw new ArgumentException("Invalid report.");
            if (kind != "manual" && kind != "error" && kind != "unexpected_exit") throw new ArgumentException("Invalid report kind.");
            if (description == null || description.Length > 4000) throw new ArgumentException("Keep the description within 4,000 characters.");
            JObject payload = new JObject { ["schemaVersion"] = 1, ["reportId"] = report.ReportId, ["captureId"] = report.CaptureId,
                ["kind"] = kind, ["description"] = description, ["includeDiagnostics"] = report.IncludeMetadata };
            if (report.IncludeMetadata)
            {
                JObject diagnostics = new JObject { ["metadata"] = Metadata(report.CurrentMetadata),
                    ["logs"] = Tail(currentLogs, 20000), ["logCoverage"] = "Errors, exceptions and asserts observed since reporting initialized; bounded and filtered, not a complete game log." };
                if (report.PreviousSessionId != null)
                    diagnostics["previousSession"] = new JObject { ["sessionId"] = report.PreviousSessionId,
                        ["metadata"] = Metadata(report.PreviousMetadata), ["logs"] = Tail(previousLogs, 20000),
                        ["logCoverage"] = "Last persisted errors matching this session; abrupt exit may lose the most recent entries." };
                payload["diagnostics"] = diagnostics;
                if (Encoding.UTF8.GetByteCount(payload.ToString(Formatting.None)) > MaximumBytes)
                {
                    diagnostics["logs"] = Tail(currentLogs, 8000);
                    if (diagnostics["previousSession"] != null) diagnostics["previousSession"]["logs"] = Tail(previousLogs, 8000);
                    diagnostics["truncated"] = true;
                }
            }
            string json = payload.ToString(Formatting.None);
            if (Encoding.UTF8.GetByteCount(json) > MaximumBytes) throw new ArgumentException("Diagnostic capture exceeds the report limit.");
            return json;
        }
        private static JToken Metadata(string json)
        {
            if (String.IsNullOrEmpty(json)) return new JObject { ["status"] = "unavailable" };
            if (Encoding.UTF8.GetByteCount(json) > 24000) return new JObject { ["status"] = "omitted", ["reason"] = "size_limit" };
            try
            {
                JToken value = JToken.Parse(json);
                // The metadata-only collector's legacy logs placeholder does not describe this bundle.
                JObject sections = value["sections"] as JObject;
                if (sections != null) sections.Remove("logs");
                return value;
            }
            catch { return new JObject { ["status"] = "unavailable", ["reason"] = "invalid_source" }; }
        }
        private static string Tail(string value, int maximum)
        {
            if (String.IsNullOrEmpty(value)) return "No matching error log was captured.";
            if (value.Length <= maximum) return value;
            int start = value.Length - maximum;
            if (Char.IsLowSurrogate(value[start])) start++;
            return "[Earlier entries omitted]\n" + value.Substring(start);
        }
    }
}
