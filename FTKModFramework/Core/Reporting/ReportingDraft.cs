using System;
using System.Text;

namespace FTKModFramework.Core.Reporting
{
    internal sealed class ReportingDraft
    {
        internal const int MaximumDescriptionBytes = 16000;
        internal const int MaximumDescriptionCharacters = 4000;
        internal const int MaximumLogBytes = 128 * 1024;
        internal readonly ReportingReport Report;
        internal readonly string Description;
        internal readonly string Kind, CurrentLogs, PreviousLogs, ErrorId;
        internal readonly DateTime SavedAtUtc;
        private ReportingDraft(ReportingReport report, string description, DateTime savedAtUtc, string kind, string currentLogs, string previousLogs, string errorId)
        { Report = report.CopyForRecovery(); Description = description; SavedAtUtc = savedAtUtc;
            Kind = kind; CurrentLogs = currentLogs; PreviousLogs = previousLogs; ErrorId = errorId; }
        internal ReportingDraft Copy() { return new ReportingDraft(Report, Description, SavedAtUtc, Kind, CurrentLogs, PreviousLogs, ErrorId); }
        internal static ReportingDraft Create(ReportingReport report, string description, DateTime savedAtUtc)
        { return Create(report, description, savedAtUtc, report != null && report.PreviousSessionId != null ? "unexpected_exit" : "manual", "", "", null); }
        internal static ReportingDraft Create(ReportingReport report, string description, DateTime savedAtUtc,
            string kind, string currentLogs, string previousLogs, string errorId)
        { return Validate(report, description, savedAtUtc, kind, currentLogs, previousLogs, errorId, false); }
        internal static ReportingDraft ImportLegacy(ReportingReport report, string description, DateTime savedAtUtc)
        { return Validate(report, description, savedAtUtc, report.PreviousSessionId == null ? "manual" : "unexpected_exit", "", "", null, true); }
        internal static ReportingDraft RestoreSaved(ReportingReport report, string description, DateTime savedAtUtc,
            string kind, string currentLogs, string previousLogs, string errorId)
        { return Validate(report, description, savedAtUtc, kind, currentLogs, previousLogs, errorId,
            description != null && description.Length > MaximumDescriptionCharacters); }
        private static ReportingDraft Validate(ReportingReport report, string description, DateTime savedAtUtc,
            string kind, string currentLogs, string previousLogs, string errorId, bool legacy)
        {
            if (report == null || description == null || savedAtUtc.Kind != DateTimeKind.Utc || savedAtUtc.Ticks == 0 ||
                report.CreatedAtUtc.Kind != DateTimeKind.Utc || report.CreatedAtUtc.Ticks == 0 ||
                !ValidId(report.ReportId) || !ValidId(report.CaptureId) || description.Length > (legacy ? 8256 : MaximumDescriptionCharacters) ||
                Encoding.UTF8.GetByteCount(description) > (legacy ? 8256 : MaximumDescriptionBytes) || !Metadata(report.CurrentMetadata, false) ||
                !Metadata(report.PreviousMetadata, true)) throw new ArgumentException("Invalid draft.");
            if ((kind != "manual" && kind != "error" && kind != "unexpected_exit") ||
                (errorId != null && (!ValidId(errorId) || kind != "error")) ||
                !Logs(currentLogs) || !Logs(previousLogs) || (report.PreviousSessionId == null && previousLogs.Length != 0))
                throw new ArgumentException("Invalid draft diagnostics.");
            if (report.PreviousSessionId == null)
            {
                if (report.PreviousCheckpointId != null || report.PreviousMetadata != null || report.PreviousObservedAtUtc.HasValue)
                    throw new ArgumentException("Invalid prior provenance.");
            }
            else if (!ValidId(report.PreviousSessionId) || (report.PreviousCheckpointId != null && !ValidId(report.PreviousCheckpointId)) ||
                !report.PreviousObservedAtUtc.HasValue || report.PreviousObservedAtUtc.Value.Kind != DateTimeKind.Utc ||
                report.PreviousObservedAtUtc.Value.Ticks == 0 || (report.PreviousCheckpointId == null && !String.IsNullOrEmpty(report.PreviousMetadata)))
                throw new ArgumentException("Invalid prior provenance.");
            return new ReportingDraft(report, description, savedAtUtc, kind, currentLogs, previousLogs, errorId);
        }
        private static bool Logs(string value) { return value != null && value.Length <= MaximumLogBytes && Encoding.UTF8.GetByteCount(value) <= MaximumLogBytes; }
        private static bool Metadata(string value, bool nullable)
        { return value == null ? nullable : value.Length <= ReportingMetadata.MaximumBytes && Encoding.UTF8.GetByteCount(value) <= ReportingMetadata.MaximumBytes && (nullable || value.Length > 0); }
        internal static bool ValidId(string value)
        {
            if (value == null || value.Length != 32) return false;
            foreach (char c in value) if (!(c >= 'a' && c <= 'f') && !(c >= '0' && c <= '9')) return false;
            return true;
        }
    }
}
