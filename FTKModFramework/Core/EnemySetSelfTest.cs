using System;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Startup self-test for the internal <see cref="RealmBossRegistration.RegisterEnemySet"/> helper (#59).
    /// It clones a throwaway enemy set from the vanilla <c>bounty1A</c>, fills all three party arrays with one
    /// VALID vanilla enemy (<c>banditA</c>), and proves the set resolves through the same int/dictionary lookup
    /// paths the boss/enemy-set code uses, that it is non-empty for a solo party, and that it is NOT a
    /// GenericBoss (the only set path that would route through the unpatched <c>FTK_enemySet.GetEnum</c>).
    ///
    /// SOLO NON-EMPTY CHECK (intentional): <c>FTK_enemySet.GetEnemySet()</c> reads LIVE state
    /// (<c>FTKHub.Instance.TotalPlayers</c> and <c>GameFlow.Instance.m_GameDifficultyType</c>) that does not
    /// exist at plugin-load / on the menu, so calling it here would NRE. The solo party path is exactly
    /// <c>m_HalfParty</c> (returned when fewer than 3 players, per the decompile), so we assert
    /// <c>m_HalfParty.Length &gt; 0</c> DIRECTLY: that is the array a 1-3 player game would receive.
    ///
    /// Emits exactly one "SELF-TEST PASS [enemyset]" line on success (or a matching FAIL line). Gated by
    /// EnableSampleContent (wired from <see cref="AdventureContent"/>), like the rest of the demo self-tests.
    /// </summary>
    internal static class EnemySetSelfTest
    {
        private const string SetId = "ftkmf_enemyset_selftest";
        private const FTK_enemySet.ID Template = FTK_enemySet.ID.bounty1A;     // a Bounty set (m_Type == Bounty)
        private const FTK_enemyCombat.ID Member = FTK_enemyCombat.ID.banditA;  // a valid vanilla enemy id

        public static void Run()
        {
            try
            {
                FTK_enemySet set = RealmBossRegistration.RegisterEnemySet(
                    Plugin.Guid, SetId, Template,
                    s =>
                    {
                        // One valid vanilla enemy in every party array. m_HalfParty is the solo (1-3 player)
                        // path; fill the full-party arrays too so a 3+ player game also resolves a non-empty set.
                        s.m_HalfParty = new FTK_enemyCombat.ID[] { Member };
                        s.m_FullPartyNormal = new FTK_enemyCombat.ID[] { Member };
                        s.m_FullPartyEasy = new FTK_enemyCombat.ID[] { Member };
                        // Keep the type off GenericBoss (cloned from a Bounty set, so it already is; assert below).
                        s.m_Type = EnemySetType.Bounty;
                    });

                Validate(set);
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [enemyset]: " + e);
            }
        }

        private static void Validate(FTK_enemySet set)
        {
            FTK_enemySetDB db = Content.Db<FTK_enemySetDB>();

            int intId = db.GetIntFromID(SetId);             // DbLookupPatcher prefix path (no GetEnum needed)
            FTK_enemySet byInt = db.GetEntryByInt(intId);   // the boss/enemy-set int resolution path
            FTK_enemySet byEnum = db.GetEntry((FTK_enemySet.ID)intId);

            bool idResolves = intId >= 0 && IdAllocator.IsCustom(intId);
            bool byIntOk = byInt != null;
            bool byEnumOk = byEnum != null;
            bool sameRow = byInt != null && set != null && ReferenceEquals(byInt, set);

            // Solo (1-3 player) party path == m_HalfParty; assert it directly (GetEnemySet reads live player
            // count + difficulty that do not exist at load, so we do not call it here).
            bool soloNonEmpty = set != null && set.m_HalfParty != null && set.m_HalfParty.Length > 0;
            bool notGenericBoss = set != null && set.m_Type != EnemySetType.GenericBoss;

            bool ok = idResolves && byIntOk && byEnumOk && sameRow && soloNonEmpty && notGenericBoss;

            if (ok)
                Plugin.Log.LogInfo("SELF-TEST PASS [enemyset]: '" + SetId + "' resolves (int=" + intId +
                    ", byInt=ok, byEnum=ok, sameRow=true), soloHalfParty=" + set.m_HalfParty.Length +
                    " (direct m_HalfParty check; GetEnemySet reads live player count, unavailable at load), " +
                    "m_Type=" + set.m_Type + " (!= GenericBoss).");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [enemyset]: int=" + intId + " idResolves=" + idResolves +
                    " byInt=" + (byInt == null ? "null" : "ok") + " byEnum=" + (byEnum == null ? "null" : "ok") +
                    " sameRow=" + sameRow + " soloHalfParty=" +
                    (set != null && set.m_HalfParty != null ? set.m_HalfParty.Length.ToString() : "null") +
                    " m_Type=" + (set != null ? set.m_Type.ToString() : "null") + ".");
        }
    }
}
