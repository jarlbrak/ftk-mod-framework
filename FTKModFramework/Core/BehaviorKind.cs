namespace FTKModFramework.Core
{
    /// <summary>
    /// The CLOSED set of behaviour kinds the framework knows how to register and instantiate. Each kind
    /// pins an explicit game base type AND an explicit instantiation path; the two are deliberately NOT
    /// interchangeable:
    /// <list type="bullet">
    /// <item><see cref="Proficiency"/> -&gt; base <c>ProficiencyBase</c> (a MonoBehaviour, per the decompile),
    /// hosted on a live GameObject via <see cref="BehaviorHost"/>.Create.</item>
    /// <item><see cref="QuestLogic"/> -&gt; base <c>QuestLogicBase</c> (a plain serializable class deriving from
    /// System.Object, NOT a MonoBehaviour; confirmed in the decompile to have a public parameterless ctor that
    /// Newtonsoft uses), instantiated via <c>Activator.CreateInstance</c>. It MUST NOT be routed through
    /// BehaviorHost.Create / AddComponent (that path returns null for a non-MonoBehaviour).</item>
    /// </list>
    ///
    /// This enum is intentionally a closed two-value set: there is NO open kind-keyed strategy registry and NO
    /// IBehaviorKindStrategy. Adding a third kind is a deliberate future spec, not an open extension point. Do
    /// NOT reorder these members: the ordinal is not persisted in saves today, but keeping a stable order avoids
    /// surprising any future diagnostics that print the ordinal.
    /// </summary>
    internal enum BehaviorKind
    {
        Proficiency,
        QuestLogic
    }
}
