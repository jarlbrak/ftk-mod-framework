using System;
using System.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace FTKModFramework.Core.Reporting
{
    internal static class ReportingSubmissionPayload
    {
        internal const int MaximumBytes = 2 * 1024 * 1024;
        internal const int MaximumLogBytes = 128 * 1024;
        internal const string Disclosure = "Send publishes your description and selected diagnostics to the public FTK framework GitHub tracker through our Railway service. Diagnostics include filtered recent game/framework logs with informational messages, warnings and errors, plus versions, mods and session context. No saves or screenshots are collected. Filtering may miss personal information. Opening this editor does not send this report. Automatic reports may send separately when enabled in Mods > Settings & Help. Downloadable diagnostics expire after 30 days; text published on GitHub remains public.";
        internal static string Create(ReportingReport report, string description, string kind, string currentLogs, string previousLogs)
        { return Create(report, description, kind, currentLogs, previousLogs, false); }
        internal static string Create(ReportingReport report, string description, string kind, string currentLogs, string previousLogs, bool automatic)
        {
            if (report == null || !ReportingDraft.ValidId(report.ReportId) || !ReportingDraft.ValidId(report.CaptureId)) throw new ArgumentException("Invalid report.");
            if (kind != "manual" && kind != "error" && kind != "unexpected_exit") throw new ArgumentException("Invalid report kind.");
            if (description == null || description.Length > 4000) throw new ArgumentException("Keep the description within 4,000 characters.");
            JObject payload = new JObject { ["schemaVersion"] = 1, ["reportId"] = report.ReportId, ["captureId"] = report.CaptureId,
                ["kind"] = kind, ["description"] = description, ["includeDiagnostics"] = report.IncludeMetadata };
            if (automatic) payload["submissionMode"] = "automatic";
            if (report.IncludeMetadata)
            {
                JObject diagnostics = new JObject { ["metadata"] = Metadata(report.CurrentMetadata),
                    ["logs"] = Tail(currentLogs, MaximumLogBytes), ["logCoverage"] = "Recent filtered game/framework process logs observed since reporting initialized, including informational messages, warnings and errors; at most 128 KiB of UTF-8 text, not a complete game log." };
                if (report.PreviousSessionId != null)
                    diagnostics["previousSession"] = new JObject { ["sessionId"] = report.PreviousSessionId,
                        ["metadata"] = Metadata(report.PreviousMetadata), ["logs"] = Tail(previousLogs, MaximumLogBytes),
                        ["logCoverage"] = "Last persisted filtered process log dump matching this session, up to 128 KiB of UTF-8 text; abrupt exit may lose the most recent entries. Logs missing from older captures cannot be recovered." };
                payload["diagnostics"] = diagnostics;
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
            if (String.IsNullOrEmpty(value)) return "No matching log dump was captured.";
            byte[] bytes = Encoding.UTF8.GetBytes(value);
            if (bytes.Length <= maximum) return value;
            const string prefix = "[Earlier entries omitted]\n";
            int start = bytes.Length - maximum + Encoding.UTF8.GetByteCount(prefix);
            while (start < bytes.Length && (bytes[start] & 0xc0) == 0x80) start++;
            return prefix + Encoding.UTF8.GetString(bytes, start, bytes.Length - start);
        }
    }
}
