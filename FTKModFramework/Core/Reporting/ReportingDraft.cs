using System;
using System.Text;

namespace FTKModFramework.Core.Reporting
{
    internal sealed class ReportingDraft
    {
        internal const int MaximumDescriptionBytes = 8256;
        internal readonly ReportingReport Report;
        internal readonly string Description;
        internal readonly DateTime SavedAtUtc;
        private ReportingDraft(ReportingReport report, string description, DateTime savedAtUtc)
        { Report = report.CopyForRecovery(); Description = description; SavedAtUtc = savedAtUtc; }
        internal ReportingDraft Copy() { return new ReportingDraft(Report, Description, SavedAtUtc); }
        internal static ReportingDraft Create(ReportingReport report, string description, DateTime savedAtUtc)
        {
            if (report == null || description == null || savedAtUtc.Kind != DateTimeKind.Utc || savedAtUtc.Ticks == 0 ||
                report.CreatedAtUtc.Kind != DateTimeKind.Utc || report.CreatedAtUtc.Ticks == 0 ||
                !ValidId(report.ReportId) || !ValidId(report.CaptureId) || description.Length > MaximumDescriptionBytes ||
                Encoding.UTF8.GetByteCount(description) > MaximumDescriptionBytes || !Metadata(report.CurrentMetadata, false) ||
                !Metadata(report.PreviousMetadata, true)) throw new ArgumentException("Invalid draft.");
            if (report.PreviousSessionId == null)
            {
                if (report.PreviousCheckpointId != null || report.PreviousMetadata != null || report.PreviousObservedAtUtc.HasValue)
                    throw new ArgumentException("Invalid prior provenance.");
            }
            else if (!ValidId(report.PreviousSessionId) || (report.PreviousCheckpointId != null && !ValidId(report.PreviousCheckpointId)) ||
                !report.PreviousObservedAtUtc.HasValue || report.PreviousObservedAtUtc.Value.Kind != DateTimeKind.Utc ||
                report.PreviousObservedAtUtc.Value.Ticks == 0 || (report.PreviousCheckpointId == null && !String.IsNullOrEmpty(report.PreviousMetadata)))
                throw new ArgumentException("Invalid prior provenance.");
            return new ReportingDraft(report, description, savedAtUtc);
        }
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
