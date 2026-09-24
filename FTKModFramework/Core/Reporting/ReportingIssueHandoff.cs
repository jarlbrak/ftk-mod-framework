using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text;
using Newtonsoft.Json.Linq;

namespace FTKModFramework.Core.Reporting
{
    internal sealed class ReportingIssuePlan
    {
        internal readonly string Url, Title, Summary, Reproduction, ExpectedActual, Affected, Frequency, Environment, Diagnostics;
        internal readonly string ReportText, DiagnosticsText;
        internal readonly bool Prefilled, TitleShortened;
        internal ReportingIssuePlan(string url, string title, Dictionary<string, string> fields, string reportText,
            string diagnosticsText, bool prefilled, bool titleShortened)
        {
            Url = url; Title = title; Summary = fields["summary"]; Reproduction = fields["repro"];
            ExpectedActual = fields["expected-actual"]; Affected = fields["affected"]; Frequency = fields["frequency"];
            Environment = fields["environment"]; Diagnostics = fields["diagnostics"];
            ReportText = reportText; DiagnosticsText = diagnosticsText;
            Prefilled = prefilled; TitleShortened = titleShortened;
        }
    }

    // The reviewed narrative is prefilled into the public form. Full metadata is saved only as
    // a separately reviewed local attachment; it never enters the URL or browser history.
    internal static class ReportingIssueHandoff
    {
        internal const int MaximumUrlBytes = 6000;
        private const string BaseUrl = "https://github.com/jarlbrak/ftk-mod-framework/issues/new";
        private const string TemplateUrl = BaseUrl + "?template=bug_report.yml";
        private static readonly string[] FieldOrder = { "summary", "repro", "expected-actual", "affected", "frequency", "environment", "diagnostics" };

        internal static ReportingIssuePlan Create(ReportingReport report, ReportingIssueNarrative narrative)
        {
            if (report == null || narrative == null) throw new ArgumentNullException();
            if (!narrative.Complete) throw new ArgumentException("All required report fields must be complete.");
            string summary = narrative.Summary.Trim();
            string titleSource = FirstLine(summary);
            if (titleSource.Length == 0) titleSource = "Bug reported through the FTK in-game reporter";
            string title = "Bug: " + LimitScalars(titleSource, 115, out bool shortened);
            string reproduction = narrative.CannotReproduce ? "I could not reproduce this reliably." : narrative.Reproduction.Trim();
            string expectedActual = narrative.ExpectedActual.Trim();
            string diagnosticsText = report.IncludeMetadata ? Diagnostics(report) : null;
            string environment = report.IncludeMetadata
                ? "See the optional diagnostics.txt attachment for observed framework, game, runtime, and mod state. Unknown values are identified there."
                : "Metadata was excluded by the player. Framework, game, runtime, and mod state were not shared.";
            string diagnostics = report.IncludeMetadata
                ? "Optional diagnostics attachment (select the saved local file in GitHub to upload it). Report " + report.ReportId + "; capture " + report.CaptureId + "."
                : "No diagnostics attachment. Metadata was excluded by the player. Report " + report.ReportId + ".";
            Dictionary<string, string> fields = new Dictionary<string, string>(StringComparer.Ordinal) {
                { "summary", summary }, { "repro", reproduction }, { "expected-actual", expectedActual },
                { "affected", "Not sure" }, { "frequency", "Not sure" }, { "environment", environment }, { "diagnostics", diagnostics }
            };
            string reportText = BuildReportText(report, title, fields);
            long encodedLength = BaseUrl.Length + 1 + EncodedLength("template") + 1 + EncodedLength("bug_report.yml") +
                1 + EncodedLength("title") + 1 + EncodedLength(title);
            foreach (string key in FieldOrder) encodedLength += 1L + EncodedLength(key) + 1 + EncodedLength(fields[key]);
            bool prefilled = encodedLength <= MaximumUrlBytes;
            string url = prefilled ? BuildUrl(title, fields) : TemplateUrl;
            return new ReportingIssuePlan(url, title, fields, reportText, diagnosticsText, prefilled, shortened);
        }

        private static string BuildReportText(ReportingReport report, string title, IDictionary<string, string> fields)
        {
            StringBuilder text = new StringBuilder();
            text.Append(title).Append("\n\nFTK framework/mod report\nReport: ").Append(report.ReportId)
                .Append("\nCreated UTC: ").Append(report.CreatedAtUtc.ToString("o", CultureInfo.InvariantCulture))
                .Append("\n\nSummary\n").Append(fields["summary"])
                .Append("\n\nRepro steps (in-game)\n").Append(fields["repro"])
                .Append("\n\nExpected vs actual\n").Append(fields["expected-actual"])
                .Append("\n\nAffected content / game mode\n").Append(fields["affected"])
                .Append("\n\nHow often does this happen?\n").Append(fields["frequency"])
                .Append("\n\nEnvironment and mod state\n").Append(fields["environment"])
                .Append("\n\nOptional reviewed diagnostic metadata\n").Append(fields["diagnostics"])
                .Append("\n\nLogs: not included. GitHub file selection uploads a selected attachment publicly before issue submission.\n");
            return text.ToString();
        }

        private static string Diagnostics(ReportingReport report)
        {
            JObject value = new JObject {
                ["schemaVersion"] = 1, ["reportId"] = report.ReportId, ["captureId"] = report.CaptureId,
                ["metadataIncluded"] = true, ["logsIncluded"] = false,
                ["reportCreationMetadata"] = ParseMetadata(report.CurrentMetadata)
            };
            if (report.PreviousSessionId != null)
                value["previousSession"] = new JObject {
                    ["sessionId"] = report.PreviousSessionId, ["checkpointId"] = report.PreviousCheckpointId,
                    ["observedAtUtc"] = report.PreviousObservedAtUtc.HasValue ? report.PreviousObservedAtUtc.Value.ToString("o", CultureInfo.InvariantCulture) : null,
                    ["metadata"] = String.IsNullOrEmpty(report.PreviousMetadata) ? null : ParseMetadata(report.PreviousMetadata)
                };
            return value.ToString(Newtonsoft.Json.Formatting.Indented) + "\n";
        }

        private static JToken ParseMetadata(string value)
        {
            try { return JToken.Parse(value); }
            catch { return new JObject { ["status"] = "unavailable", ["reason"] = "invalid_source" }; }
        }

        private static string FirstLine(string value)
        {
            int end = value.IndexOfAny(new[] { '\r', '\n' });
            return (end < 0 ? value : value.Substring(0, end)).Trim();
        }

        private static string LimitScalars(string value, int maximum, out bool shortened)
        {
            int scalars = 0, index = 0;
            while (index < value.Length && scalars < maximum - 1)
            {
                if (Char.IsHighSurrogate(value[index]) && index + 1 < value.Length && Char.IsLowSurrogate(value[index + 1])) index += 2;
                else index++;
                scalars++;
            }
            shortened = index < value.Length;
            if (shortened) return value.Substring(0, index) + "…";
            return value.Substring(0, index);
        }

        private static long EncodedLength(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value); long length = 0;
            for (int i = 0; i < bytes.Length; i++) length += IsUnreserved(bytes[i]) ? 1 : 3;
            return length;
        }
        private static string BuildUrl(string title, IDictionary<string, string> fields)
        {
            StringBuilder url = new StringBuilder(BaseUrl);
            url.Append("?template=bug_report.yml&title=").Append(Encode(title));
            foreach (string key in FieldOrder) url.Append('&').Append(key).Append('=').Append(Encode(fields[key]));
            return url.ToString();
        }
        private static string Encode(string value)
        {
            byte[] bytes = Encoding.UTF8.GetBytes(value); StringBuilder result = new StringBuilder(bytes.Length);
            const string Hex = "0123456789ABCDEF";
            foreach (byte b in bytes)
            {
                if (IsUnreserved(b)) result.Append((char)b);
                else result.Append('%').Append(Hex[b >> 4]).Append(Hex[b & 15]);
            }
            return result.ToString();
        }
        private static bool IsUnreserved(byte value)
        { return (value >= (byte)'A' && value <= (byte)'Z') || (value >= (byte)'a' && value <= (byte)'z') ||
            (value >= (byte)'0' && value <= (byte)'9') || value == (byte)'-' || value == (byte)'_' || value == (byte)'.' || value == (byte)'~'; }
    }
}
