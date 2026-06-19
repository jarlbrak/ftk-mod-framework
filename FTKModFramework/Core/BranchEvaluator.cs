using System;

namespace FTKModFramework.Core
{
    /// <summary>
    /// The CLOSED-operator branch evaluator (#42). Two DISJOINT closed vocabularies, each interpreted by a plain
    /// <c>switch</c> (NO expression trees, NO <c>dynamic</c>, NO AND/OR/nesting/arithmetic, net35/Mono-safe):
    /// <list type="bullet">
    /// <item><b>COMPARISON</b> (<see cref="BranchCondition.Op"/>): <c>eq</c> (==), <c>ne</c> (!=), <c>ge</c> (&gt;=),
    /// <c>le</c> (&lt;=). <c>gt</c>/<c>lt</c> are intentionally omitted.</item>
    /// <item><b>MUTATION</b> (<see cref="FlagOp.Op"/>): <c>set</c> (assign), <c>add</c> (current + value).</item>
    /// </list>
    /// An unknown op in EITHER vocabulary is rejected at LOAD/authoring time (<see cref="ValidateCompareOp"/> /
    /// <see cref="ValidateMutateOp"/> throw with a precise diagnostic, called from the builder methods). At
    /// RUNTIME the router only ever feeds ops that already passed validation, but <see cref="Compare"/> still
    /// defends with a benign <c>false</c> on an unrecognised op so the router can NEVER throw.
    /// </summary>
    internal static class BranchEvaluator
    {
        /// <summary>The closed comparison vocabulary (used in diagnostics).</summary>
        internal const string CompareOps = "eq, ne, ge, le";

        /// <summary>The closed mutation vocabulary (used in diagnostics).</summary>
        internal const string MutateOps = "set, add";

        /// <summary>True iff <paramref name="op"/> is a known comparison op.</summary>
        internal static bool IsCompareOp(string op)
        {
            return op == "eq" || op == "ne" || op == "ge" || op == "le";
        }

        /// <summary>True iff <paramref name="op"/> is a known mutation op.</summary>
        internal static bool IsMutateOp(string op)
        {
            return op == "set" || op == "add";
        }

        /// <summary>
        /// Evaluate one comparison: is <paramref name="flagValue"/> <c>op</c> <paramref name="value"/>? An
        /// unrecognised op returns <c>false</c> (defensive; validated ops are guaranteed at authoring time).
        /// </summary>
        internal static bool Compare(int flagValue, string op, int value)
        {
            switch (op)
            {
                case "eq": return flagValue == value;
                case "ne": return flagValue != value;
                case "ge": return flagValue >= value;
                case "le": return flagValue <= value;
                default: return false; // never reached for validated ops; router stays throw-free
            }
        }

        /// <summary>
        /// Apply a mutation op to a current flag value, returning the new value. An unrecognised op returns
        /// <paramref name="current"/> unchanged (defensive; validated ops are guaranteed at authoring time).
        /// </summary>
        internal static int Apply(int current, string op, int value)
        {
            switch (op)
            {
                case "set": return value;
                case "add": return current + value;
                default: return current; // never reached for validated ops
            }
        }

        /// <summary>Reject an unknown comparison op at authoring time with a precise diagnostic.</summary>
        /// <exception cref="ArgumentException">If <paramref name="op"/> is not in the closed comparison set.</exception>
        internal static void ValidateCompareOp(string op)
        {
            if (!IsCompareOp(op))
                throw new ArgumentException(
                    "BranchCondition op '" + (op ?? "<null>") + "' is not a valid comparison op (expected one of: " +
                    CompareOps + ").", "op");
        }

        /// <summary>Reject an unknown mutation op at authoring time with a precise diagnostic.</summary>
        /// <exception cref="ArgumentException">If <paramref name="op"/> is not in the closed mutation set.</exception>
        internal static void ValidateMutateOp(string op)
        {
            if (!IsMutateOp(op))
                throw new ArgumentException(
                    "FlagOp op '" + (op ?? "<null>") + "' is not a valid mutation op (expected one of: " +
                    MutateOps + ").", "op");
        }
    }
}
