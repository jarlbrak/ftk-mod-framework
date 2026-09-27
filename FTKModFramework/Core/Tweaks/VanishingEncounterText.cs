namespace FTKModFramework.Core
{
    /// <summary>The marker for information.vanishing-encounters, kept free of game types so
    /// Tests/Tweaks can cover it. MiniEncounterMenuBase.UseLeaveOrEndTurnButton sends DecayHexRPC
    /// for an encounter whose FTK_miniEncounter.m_DestroyOnLeave is set, so leaving or ending the
    /// turn there removes it.</summary>
    internal static class VanishingEncounterText
    {
        internal const string Line = "Gone once you leave or end your turn here.";

        /// <summary>True only for an encounter the player already knows (MiniEncounter.m_Known) whose
        /// entry has m_DestroyOnLeave. An unknown encounter's card says "could be anything", and the
        /// marker would reveal part of what it is.</summary>
        internal static bool Shown(bool known, bool destroyOnLeave)
        {
            return known && destroyOnLeave;
        }

        /// <summary>The card's effect text with the marker on its own line, the "\n" join
        /// MiniHexHaunt.GetPOIProfile uses for a two-line effect, or the marker alone when the
        /// effect is empty.</summary>
        internal static string Append(string effect)
        {
            if (string.IsNullOrEmpty(effect)) return Line;
            return effect + "\n" + Line;
        }
    }
}
