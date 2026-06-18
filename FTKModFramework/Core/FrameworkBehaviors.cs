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

        /// <summary>
        /// Register the framework's bundled demo behaviour(s). Idempotent: <see cref="BehaviorRegistry"/> is
        /// first-wins and warns on a duplicate, so a second call is a safe no-op. MUST run BEFORE the data
        /// loader resolves behaviour references, so the key is present when the demo fixture asks for it.
        /// </summary>
        public static void Register()
        {
            // ThiefStealProficiency lives in the modder-facing Content/ namespace (FTKModFramework); Core
            // references it by full namespace, as it does elsewhere. Registering typeof(...) keys the demo
            // fixture's behavior:"Steal" to the framework's compiled-in Steal behaviour.
            BehaviorRegistry.Register(SampleDataModGuid, "Steal", typeof(FTKModFramework.ThiefStealProficiency), BehaviorKind.Proficiency);
            Plugin.Log.LogInfo("FrameworkBehaviors: registered bundled-demo behaviour '" +
                SampleDataModGuid + ":Steal' -> ThiefStealProficiency.");
        }
    }
}
