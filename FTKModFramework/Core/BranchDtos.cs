namespace FTKModFramework.Core
{
    /// <summary>
    /// A single branch GUARD: one comparison of a campaign flag against a literal. PURE DATA (fields only, no
    /// methods, no Evaluate). The router's internal closed evaluator (<see cref="BranchEvaluator"/>) interprets
    /// it; the DTO carries no behaviour so no engine type leaks through the public builder surface.
    ///
    /// <see cref="Op"/> is drawn from the closed COMPARISON vocabulary <c>eq</c>/<c>ne</c>/<c>ge</c>/<c>le</c>
    /// (a DISJOINT vocabulary from <see cref="FlagOp"/>'s mutation ops; <c>gt</c>/<c>lt</c> are intentionally
    /// omitted). An unknown op is rejected at AUTHORING time (<see cref="QuestBuilder.BranchTo"/>), never at
    /// runtime: the router must never throw.
    /// </summary>
    public sealed class BranchCondition
    {
        /// <summary>Campaign flag key compared via <see cref="Campaign.GetFlag"/> (absent flag reads as 0).</summary>
        public string Flag;

        /// <summary>Comparison op, one of the closed set <c>eq</c>/<c>ne</c>/<c>ge</c>/<c>le</c>.</summary>
        public string Op;

        /// <summary>The literal the flag value is compared against.</summary>
        public int Value;
    }

    /// <summary>
    /// A single on-complete flag MUTATION applied when a quest finishes. PURE DATA (fields only). Recorded by
    /// <see cref="QuestBuilder.OnCompleteSetFlag"/> and applied by the router BEFORE its branch conditions are
    /// evaluated (so a quest can set a flag on completion and branch on that same flag).
    ///
    /// <see cref="Op"/> is drawn from the closed MUTATION vocabulary <c>set</c>/<c>add</c> (a DISJOINT vocabulary
    /// from <see cref="BranchCondition"/>'s comparison ops). An unknown op is rejected at AUTHORING time
    /// (<see cref="QuestBuilder.OnCompleteSetFlag"/>).
    /// </summary>
    public sealed class FlagOp
    {
        /// <summary>Campaign flag key written via <see cref="Campaign.SetFlag"/>.</summary>
        public string Flag;

        /// <summary>Mutation op, one of the closed set <c>set</c> (assign) / <c>add</c> (current + value).</summary>
        public string Op;

        /// <summary>For <c>set</c>: the assigned value. For <c>add</c>: the delta added to the current value.</summary>
        public int Value;
    }
}
