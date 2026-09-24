using System;
using System.Globalization;
using System.Text;

namespace FTKModFramework.Core.Reporting
{
    // The in-game form records exactly the required GitHub narrative fields for review and recovery.
    internal sealed class ReportingIssueNarrative
    {
        internal const int MaximumTextBytes = 8192;
        private const string Prefix = "FTKREPORT1|";
        internal readonly string Summary, Reproduction, ExpectedActual;
        internal readonly bool CannotReproduce;

        internal ReportingIssueNarrative(string summary, string reproduction, string expectedActual, bool cannotReproduce)
        {
            Summary = summary ?? ""; Reproduction = reproduction ?? ""; ExpectedActual = expectedActual ?? "";
            CannotReproduce = cannotReproduce;
            if (Encoding.UTF8.GetByteCount(Summary) + Encoding.UTF8.GetByteCount(Reproduction) + Encoding.UTF8.GetByteCount(ExpectedActual) > MaximumTextBytes)
                throw new ArgumentException("Narrative exceeds the report limit.");
        }

        internal bool Complete
        { get { return Summary.Trim().Length != 0 && (CannotReproduce || Reproduction.Trim().Length != 0) && ExpectedActual.Trim().Length != 0; } }

        internal string Serialize()
        {
            StringBuilder value = new StringBuilder(Prefix).Append(CannotReproduce ? '1' : '0').Append('|');
            Append(value, Summary); Append(value, Reproduction); Append(value, ExpectedActual);
            return value.ToString();
        }

        internal static ReportingIssueNarrative Restore(string value)
        {
            if (value == null) throw new ArgumentNullException("value");
            if (!value.StartsWith(Prefix, StringComparison.Ordinal))
                return new ReportingIssueNarrative("", "", value, false); // Legacy single-field draft, retained for editing.
            int cursor = Prefix.Length;
            if (cursor + 2 > value.Length || (value[cursor] != '0' && value[cursor] != '1') || value[cursor + 1] != '|')
                throw new FormatException("Invalid saved report fields.");
            bool cannotReproduce = value[cursor] == '1'; cursor += 2;
            string summary = Read(value, ref cursor), reproduction = Read(value, ref cursor), actual = Read(value, ref cursor);
            if (cursor != value.Length) throw new FormatException("Invalid saved report fields.");
            return new ReportingIssueNarrative(summary, reproduction, actual, cannotReproduce);
        }

        private static void Append(StringBuilder output, string field)
        { output.Append(field.Length.ToString(CultureInfo.InvariantCulture)).Append(':').Append(field); }
        private static string Read(string value, ref int cursor)
        {
            int colon = value.IndexOf(':', cursor);
            if (colon < cursor || colon - cursor > 6) throw new FormatException("Invalid saved report field length.");
            int length;
            if (!Int32.TryParse(value.Substring(cursor, colon - cursor), NumberStyles.None, CultureInfo.InvariantCulture, out length) || length < 0 || length > MaximumTextBytes)
                throw new FormatException("Invalid saved report field length.");
            cursor = colon + 1;
            if (length > value.Length - cursor) throw new FormatException("Truncated saved report field.");
            string result = value.Substring(cursor, length); cursor += length; return result;
        }
    }
}
