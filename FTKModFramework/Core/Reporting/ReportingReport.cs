using System;
using System.Text;

namespace FTKModFramework.Core.Reporting
{
    // Creation collects metadata once. Narrative typing and browser focus cannot replace these strings.
    // External handoff still belongs to the reviewed draft/UI layer; this object has no transport.
    internal sealed class ReportingReport
    {
        internal readonly string ReportId, CaptureId, CurrentMetadata;
        internal readonly DateTime CreatedAtUtc;
        internal readonly string PreviousSessionId, PreviousCheckpointId, PreviousMetadata;
        internal readonly DateTime? PreviousObservedAtUtc;
        internal bool IncludeMetadata = true;
        internal bool IncludeLogs { get { return false; } }

        private ReportingReport(Func<string> collect, ReportingIncident previous, DateTime utcNow)
        {
            ReportId = Guid.NewGuid().ToString("N");
            CaptureId = Guid.NewGuid().ToString("N");
            CreatedAtUtc = utcNow;
            try
            {
                CurrentMetadata = collect();
                if (String.IsNullOrEmpty(CurrentMetadata) || Encoding.UTF8.GetByteCount(CurrentMetadata) > ReportingMetadata.MaximumBytes)
                    throw new InvalidOperationException();
            }
            catch
            {
                // Collection failure must leave a usable editor, without serializing exception text.
                CurrentMetadata = "{\"schemaVersion\":1,\"status\":\"failed\",\"reason\":\"source_error\",\"sections\":{}}";
            }
            if (previous != null)
            {
                PreviousSessionId = previous.SessionId;
                PreviousCheckpointId = previous.CheckpointId;
                PreviousMetadata = previous.Metadata;
                PreviousObservedAtUtc = previous.ObservedAtUtc;
            }
        }

        private ReportingReport(string reportId, string captureId, DateTime createdAtUtc, string currentMetadata, ReportingIncident previous)
        {
            ReportId = reportId; CaptureId = captureId; CreatedAtUtc = createdAtUtc; CurrentMetadata = currentMetadata;
            if (previous != null)
            {
                PreviousSessionId = previous.SessionId; PreviousCheckpointId = previous.CheckpointId;
                PreviousMetadata = previous.Metadata; PreviousObservedAtUtc = previous.ObservedAtUtc;
            }
            IncludeMetadata = false;
        }
        internal static ReportingReport Restore(string reportId, string captureId, DateTime createdAtUtc, string currentMetadata, ReportingIncident previous)
        { return new ReportingReport(reportId, captureId, createdAtUtc, currentMetadata, previous); }
        internal ReportingReport CopyForRecovery()
        {
            ReportingIncident previous = PreviousSessionId == null ? null : new ReportingIncident {
                SessionId = PreviousSessionId, CheckpointId = PreviousCheckpointId, Metadata = PreviousMetadata,
                ObservedAtUtc = PreviousObservedAtUtc.Value };
            return Restore(ReportId, CaptureId, CreatedAtUtc, CurrentMetadata, previous);
        }

        internal static ReportingReport Create(Func<string> collect, ReportingIncident previous, DateTime utcNow)
        {
            if (collect == null) throw new ArgumentNullException("collect");
            if (utcNow.Kind != DateTimeKind.Utc) throw new ArgumentException("UTC observation required.");
            return new ReportingReport(collect, previous, utcNow);
        }
    }
}
