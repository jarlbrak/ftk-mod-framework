using System;
using System.Collections.Generic;
using System.Text;
using System.Text.RegularExpressions;

namespace FTKModFramework.Core.Reporting
{
    internal sealed class ReportingDiagnosticsError
    {
        internal string Id { get; private set; }
        internal string Summary { get; private set; }
        internal string Signature { get; private set; }
        internal DateTime CapturedAtUtc { get; private set; }
        internal ReportingDiagnosticsError(string summary, string signature, DateTime utcNow)
        { Id = Guid.NewGuid().ToString("N"); Summary = summary; Signature = signature; CapturedAtUtc = utcNow; }
    }

    // A process-local transcript includes ordinary activity before a freeze, even when nothing
    // throws. Redaction is defensive, not a promise that arbitrary third-party log text can
    // never contain personal information. The UI must disclose this.
    internal sealed class ReportingDiagnosticsBuffer
    {
        internal const int ByteLimit = 128 * 1024;
        private const int EventLimit = 2048;
        private const int ErrorSignatureLimit = 64;
        private const int HeaderReserve = 128;
        private readonly object gate = new object();
        private readonly Queue<string> entries = new Queue<string>();
        private readonly List<string> signatures = new List<string>();
        private int bytes, errorsThisSecond, ordinaryThisSecond, offers;
        private long omitted;
        private long second;
        private DateTime nextOffer;
        private ReportingDiagnosticsError pending;
        private long version;
        internal long Version { get { lock (gate) return version; } }
        internal ReportingDiagnosticsError Pending { get { lock (gate) return pending; } }
        internal void Acknowledge(string id) { lock (gate) { if (pending != null && pending.Id == id) pending = null; } }

        internal void Add(string source, string message, string stack, DateTime utcNow)
        { Add(source, message, stack, utcNow, true); }

        internal void Add(string source, string message, string stack, DateTime utcNow, bool isError)
        {
            lock (gate)
            {
                // Bound callback cost before regex processing. Keep a separate error allowance
                // so a busy normal log cannot suppress an exception or its reporting offer.
                long currentSecond = utcNow.Ticks / TimeSpan.TicksPerSecond;
                if (currentSecond != second) { second = currentSecond; errorsThisSecond = ordinaryThisSecond = 0; }
                if ((isError ? ++errorsThisSecond > 32 : ++ordinaryThisSecond > 128)) { omitted++; return; }
                string body = Sanitize(message) + (String.IsNullOrEmpty(stack) ? "" : "\n" + Sanitize(stack));
                body = LimitUtf8(body, 8192);
                if (body.Trim().Length == 0) return;
                // Retain repeated log messages in order: a repeated combat transition can be
                // the useful clue. Deduplication applies only to automatic error offers.
                bool newError = false;
                string signature = null;
                if (isError)
                {
                    signature = body.Length > 512 ? body.Substring(0, 512) : body;
                    newError = !signatures.Contains(signature);
                    if (newError)
                    {
                        signatures.Add(signature);
                        if (signatures.Count > ErrorSignatureLimit) signatures.RemoveAt(0);
                    }
                }
                string entry = utcNow.ToString("u", System.Globalization.CultureInfo.InvariantCulture) + " [" +
                    LimitUtf8(Sanitize(source), 80).Replace('\n', ' ') + "] " + body + "\n";
                int count = Encoding.UTF8.GetByteCount(entry);
                while (entries.Count != 0 && (entries.Count >= EventLimit || bytes + count > ByteLimit - HeaderReserve))
                { bytes -= Encoding.UTF8.GetByteCount(entries.Dequeue()); omitted++; }
                entries.Enqueue(entry); bytes += count; version++;
                if (newError && pending == null && offers < 3 && utcNow >= nextOffer)
                {
                    string summary = body.Split('\n')[0];
                    pending = new ReportingDiagnosticsError(LimitUtf8(summary, 240), signature, utcNow);
                    offers++; nextOffer = utcNow.AddMinutes(5);
                }
            }
        }

        internal string Capture()
        {
            lock (gate)
            {
                StringBuilder result = new StringBuilder();
                if (omitted != 0) result.Append("[Recent log dump: ").Append(omitted).Append(" older or rate-limited entries omitted]\n");
                foreach (string entry in entries) result.Append(entry);
                return result.ToString();
            }
        }

        internal static string Sanitize(string value)
        {
            if (String.IsNullOrEmpty(value)) return "";
            if (value.Length > 8192) value = value.Substring(0, 8192);
            // Omit an entire message containing sensitive fields, including multiline values.
            if (Regex.IsMatch(value, @"(?i)\b(password|passwd|pwd|credentials?|secret|token|authorization|cookie|api[_ -]?key|access[_ -]?key|connectionstring|private[_ -]?key|username|user[_ -]?name|user[_ -]?id|display[_ -]?name|player[_ -]?name|account[_ -]?id|steam[_ -]?id)\b\s*[""']?\s*[:=]|-----BEGIN .*PRIVATE KEY-----|\bBearer\s+"))
                return "[sensitive log entry omitted]";
            value = Regex.Replace(value, @"(?i)\b(?:https?|ftp|file)://[^\s<>""']+", "[url]");
            value = Regex.Replace(value, @"(?i)\b(?:gh[pousr]_[a-z0-9_]+|github_pat_[a-z0-9_]+|sk-[a-z0-9_-]{12,}|AKIA[A-Z0-9]{16})\b", "[credential]");
            value = Regex.Replace(value, @"\beyJ[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\.[a-zA-Z0-9_-]+\b", "[credential]");
            value = Regex.Replace(value, @"[a-zA-Z0-9_.+%-]+@[a-zA-Z0-9.-]+\.[a-zA-Z]{2,}", "[email]");
            value = Regex.Replace(value, @"(?<![\w])(?:\d{1,3}\.){3}\d{1,3}(?::\d+)?", "[ip]");
            value = Regex.Replace(value, @"(?i)(?<![a-z0-9])(?:[a-f0-9]{0,4}:){2,}[a-f0-9:]{0,39}(?:%[a-z0-9]+)?", "[ip]");
            value = Regex.Replace(value, @"\b\d{15,20}\b|\bSTEAM_[0-5]:[01]:\d+\b|\[U:1:\d+\]", "[account]");
            value = Regex.Replace(value, @"(?i)[a-z]:[\\/][^\r\n""<>]*|\\\\[^\r\n""<>]*", "[path]");
            value = Regex.Replace(value, @"(?<![\w])/(?:Users|home|private|var|tmp|Volumes|mnt|opt|usr|Applications|Library|app|workspace|root)(?:/[^\r\n""<>]*)?", "[path]");
            return Regex.Replace(value, @"[\x00-\x08\x0b\x0c\x0e-\x1f\x7f]", "");
        }

        internal static string LimitUtf8(string value, int limit)
        {
            if (Encoding.UTF8.GetByteCount(value) <= limit) return value;
            int low = 0, high = Math.Min(value.Length, limit);
            while (low < high)
            {
                int middle = (low + high + 1) / 2;
                if (Encoding.UTF8.GetByteCount(value.Substring(0, middle)) <= limit - 16) low = middle;
                else high = middle - 1;
            }
            int end = low;
            if (end > 0 && Char.IsHighSurrogate(value[end - 1])) end--;
            return value.Substring(0, end) + "\n[truncated]";
        }
    }
}
