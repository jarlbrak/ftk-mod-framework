using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// DEBUG verification aid (config: Adventures/ForceCustomEncounter). When enabled, every overworld
    /// encounter the game decides to spawn is replaced by the custom "Smuggler's Cache" — giving an
    /// immediate, unambiguous in-game confirmation that injection worked. We only swap when the game
    /// already chose to spawn SOMETHING (__result != None), so the target hex is guaranteed valid.
    /// Turn this off for normal play.
    /// </summary>
    [HarmonyPatch(typeof(GameLogic), "GetMiniEncounter")]
    internal static class ForceCustomEncounter_Patch
    {
        private static void Postfix(ref FTK_miniEncounter.ID __result)
        {
            if (Plugin.ForceCustomEncounter == null || !Plugin.ForceCustomEncounter.Value) return;
            if (__result == FTK_miniEncounter.ID.None) return; // nothing was going to spawn here anyway

            // The string id is the source of truth; resolve the synthetic int on demand (same lookup
            // AdventureContent.Register uses) rather than caching it in a cross-class mutable static.
            int intId = Content.Db<FTK_miniEncounterDB>().GetIntFromID(AdventureContent.EncounterId);
            if (intId < 0) return;
            __result = (FTK_miniEncounter.ID)intId;
        }
    }
}
