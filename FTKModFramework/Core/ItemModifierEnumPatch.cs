using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    // Equipment resolves numeric modifier IDs, while native item detail cards resolve the
    // item's string ID through this separate enum helper before formatting its modifier row.
    [HarmonyPatch(typeof(FTK_characterModifier), "GetEnum")]
    internal static class ItemModifierEnumPatch
    {
        internal static bool Prefix(string _id, ref FTK_characterModifier.ID __result)
        {
            int id;
            if (_id == null || !ContentRegistry.TryGetSyntheticId(_id, out id, typeof(FTK_characterModifierDB)))
                return true;
            __result = (FTK_characterModifier.ID)id;
            return false;
        }
    }
}
