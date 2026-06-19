namespace FTKModFramework.Core
{
    /// <summary>
    /// Registers the behaviours the FRAMEWORK itself ships, so the bundled demo data fixture can reference
    /// them by name the same way a third-party data mod would. Phase 1 (in-assembly only): this registers the
    /// framework's own <c>ThiefStealProficiency</c> under the bundled-demo guid so the shipped
    /// <c>com.ftkmf.sampledata</c> fixture can carry <c>behavior:"Steal"</c> on its dagger and drop the old
    /// "minus the custom MonoBehaviour" caveat.
    ///
    /// This is the in-assembly path ONLY. A real third-party mod supplies its OWN behaviour DLL and the
    /// framework reflects on <see cref="FTKModFramework.Behaviors.ContentBehaviorAttribute"/> to register it
    /// under that mod's own guid (#33/#34); this file does NOT do attribute scanning. The registry key scheme
    /// is (owningModGuid + ":" + behaviorName), so the demo behaviour MUST be registered under the demo
    /// fixture's guid (<c>com.ftkmf.sampledata</c>) for that fixture's <c>behavior:"Steal"</c> to resolve.
    /// </summary>
    internal static class FrameworkBehaviors
    {
        // The guid the bundled data fixture (SampleData/com.ftkmf.sampledata/manifest.json) declares. The
        // demo behaviour is keyed under THIS guid (not the framework's own guid) because behaviour resolution
        // is (owningModGuid + ":" + behaviorName) and the fixture is the owning mod of the reference.
        private const string SampleDataModGuid = "com.ftkmf.sampledata";

        // The verb name of the framework's built-in collect-N quest objective (#40). Keyed under the FRAMEWORK's
        // own guid (Plugin.Guid), so the registry key is "com.ftkmf.framework:CollectN".
        private const string CollectNVerbName = "CollectN";

        /// <summary>
        /// The framework key for the built-in collect-N custom objective verb, in the
        /// <c>modGuid + ":" + verbName</c> form <see cref="BehaviorRegistry"/> uses. The campaign builder
        /// (<see cref="StageBuilder.AddCollectQuest"/>) stamps this onto the <see cref="ModQuestDef"/> it emits,
        /// and <see cref="QuestVerbResolverPatch"/> resolves it to <see cref="CollectNQuestLogic"/>. Exposed so
        /// the builder and the self-test reference one source of truth instead of a literal.
        /// </summary>
        public static readonly string CollectNVerbKey = BehaviorRegistry.MakeKey(Plugin.Guid, CollectNVerbName);

        /// <summary>
        /// Register the framework's bundled demo behaviour(s) AND its built-in quest verbs. Idempotent:
        /// <see cref="BehaviorRegistry"/> is first-wins and warns on a duplicate, so a second call is a safe
        /// no-op. MUST run BEFORE the data loader resolves behaviour references, so the keys are present when a
        /// fixture (or an authored campaign) asks for them.
        /// </summary>
        public static void Register()
        {
            // ThiefStealProficiency lives in the modder-facing Content/ namespace (FTKModFramework); Core
            // references it by full namespace, as it does elsewhere. Registering typeof(...) keys the demo
            // fixture's behavior:"Steal" to the framework's compiled-in Steal behaviour.
            BehaviorRegistry.Register(SampleDataModGuid, "Steal", typeof(FTKModFramework.ThiefStealProficiency), BehaviorKind.Proficiency);
            Plugin.Log.LogInfo("FrameworkBehaviors: registered bundled-demo behaviour '" +
                SampleDataModGuid + ":Steal' -> ThiefStealProficiency.");

            // The built-in collect-N quest verb (#40). Registered as kind=QuestLogic so the resolver
            // instantiates it via Activator.CreateInstance (NOT BehaviorHost): CollectNQuestLogic is a plain
            // QuestLogicBase, not a MonoBehaviour. BehaviorRegistry enforces the QuestLogicBase base for this kind.
            BehaviorRegistry.Register(CollectNVerbKey, typeof(CollectNQuestLogic), BehaviorKind.QuestLogic);
            Plugin.Log.LogInfo("FrameworkBehaviors: registered built-in quest verb '" +
                CollectNVerbKey + "' -> CollectNQuestLogic.");
        }
    }
}
