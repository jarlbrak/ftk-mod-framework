namespace FTKModFramework.Core
{
    /// <summary>
    /// Registers built-in quest verbs before authored content is loaded. Mod-provided behaviors are
    /// registered under their own GUID by the external behavior loader.
    /// </summary>
    internal static class FrameworkBehaviors
    {
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
        /// Register built-in quest verbs before the data loader resolves behavior references.
        /// </summary>
        public static void Register()
        {
            // The built-in collect-N quest verb (#40). Registered as kind=QuestLogic so the resolver
            // instantiates it via Activator.CreateInstance (NOT BehaviorHost): CollectNQuestLogic is a plain
            // QuestLogicBase, not a MonoBehaviour. BehaviorRegistry enforces the QuestLogicBase base for this kind.
            BehaviorRegistry.Register(CollectNVerbKey, typeof(CollectNQuestLogic), BehaviorKind.QuestLogic);
            Plugin.Log.LogInfo("FrameworkBehaviors: registered built-in quest verb '" +
                CollectNVerbKey + "' -> CollectNQuestLogic.");
        }
    }
}
