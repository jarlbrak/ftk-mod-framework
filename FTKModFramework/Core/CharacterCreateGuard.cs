using System;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Robustness guard for the in-game character-create screen.
    ///
    /// uiQuickPlayerCreate.CanUseClass(int) does FTK_playerGameStartDB.GetDB().GetEntryByIndex(i),
    /// i.e. a raw m_Array[i]. The lobby hands it the class id the player picked. If that id is out of
    /// the array's bounds at create-time, the raw index THROWS IndexOutOfRangeException inside Awake,
    /// which half-builds the party UI and then cascades into RemoveCharacter's List.RemoveAt overflow —
    /// the "character creation screen is bugged" symptom.
    ///
    /// A custom class (e.g. the Thief at index 14) makes this reachable: if the create-screen's
    /// TableManager rebuilt its DBs to vanilla (14 classes) while the lobby still selected id 14, the
    /// raw index overflows. The game's own intent when a class isn't usable is to fall back to
    /// Default_Classes — but it can only do that if CanUseClass returns false instead of throwing.
    /// So: bounds-check first, and log the numbers (array length vs requested id) to pinpoint whether
    /// custom content is being dropped on a TableManager rebuild.
    /// </summary>
    [HarmonyPatch(typeof(uiQuickPlayerCreate), "CanUseClass", new Type[] { typeof(int) })]
    internal static class CanUseClassGuard_Patch
    {
        private static bool _loggedOnce;

        private static bool Prefix(int _classIndex, ref bool __result)
        {
            int count = -1;
            try { count = FTK_playerGameStartDB.GetDB().GetCount(); } catch { /* fall through to guard */ }

            if (count < 0 || _classIndex < 0 || _classIndex >= count)
            {
                if (!_loggedOnce)
                {
                    _loggedOnce = true;
                    Plugin.Log.LogWarning("[ftkmf] CanUseClass guard: out-of-range class id " + _classIndex +
                        " vs roster count " + count + " — returning false so the game falls back to a default " +
                        "class (prevents the character-create crash). If count is the VANILLA class count, " +
                        "custom classes were dropped by a TableManager rebuild.");
                }
                __result = false;
                return false; // skip the original m_Array[_classIndex] (which would throw)
            }
            return true; // in range; run the original
        }
    }
}
