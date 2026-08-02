using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// DEBUG verification aid (config: Adventures/ForceCustomEncounter). When enabled, every overworld
    /// encounter the game decides to spawn is replaced by the custom encounter whose string id was
    /// registered in <see cref="TargetEncounterId"/>, giving an immediate, unambiguous in-game
    /// confirmation that injection worked. The target is registered by whoever owns the content (the
    /// bundled sample points it at the "Smuggler's Cache" when EnableSampleContent is on), so the
    /// framework never names a specific content pack. Nothing registered => the override is inert.
    /// We only swap when the game already chose to spawn SOMETHING (__result != None), so the target
    /// hex is guaranteed valid. Turn this off for normal play.
    /// </summary>
    [HarmonyPatch(typeof(GameLogic), "GetMiniEncounter")]
    internal static class ForceCustomEncounter_Patch
    {
        /// <summary>String id of the encounter to force in. Set at registration time; null => inert.</summary>
        internal static string TargetEncounterId;

        private static void Postfix(ref FTK_miniEncounter.ID __result)
        {
            if (Plugin.ForceCustomEncounter == null || !Plugin.ForceCustomEncounter.Value) return;
            if (__result == FTK_miniEncounter.ID.None) return; // nothing was going to spawn here anyway
            if (string.IsNullOrEmpty(TargetEncounterId)) return; // nobody registered a target

            // The string id is the source of truth; resolve the synthetic int on demand (the same lookup
            // the owning registration uses) rather than caching it in a cross-class mutable static.
            int intId = Content.Db<FTK_miniEncounterDB>().GetIntFromID(TargetEncounterId);
            if (intId < 0) return;
            __result = (FTK_miniEncounter.ID)intId;
        }
    }
}
