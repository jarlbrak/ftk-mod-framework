using System.Collections.Generic;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// Two plain string lists (spec #6): there is NO schema DSL and NO JSON-Schema validator. The
    /// loader appends a human-readable line for every problem it tolerates.
    ///
    /// An ERROR means a unit (a mod folder, a file, or an entry) was skipped. A WARNING means a unit
    /// loaded with something dropped (e.g. one unknown or un-coercible field). Both lists are logged
    /// at the end of the load as a one-line summary plus per-item detail.
    /// </summary>
    internal sealed class ValidationReport
    {
        public readonly List<string> Errors = new List<string>();
        public readonly List<string> Warnings = new List<string>();

        public void Error(string message) { Errors.Add(message); }
        public void Warning(string message) { Warnings.Add(message); }
    }
}
