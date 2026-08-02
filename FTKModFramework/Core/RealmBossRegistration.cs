using System;
using GridEditor;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Core registration helpers for the bespoke-realm + boss slice (spec #57). Both clone a vanilla
    /// <c>FTK_*DB</c> row and register it through <see cref="ContentRegistry"/>, so the new row gets a
    /// deterministic high-band <see cref="IdAllocator"/> int and the broad <see cref="DbLookupPatcher"/>
    /// <c>GetIntFromID</c> prefix (auto-installed by Register) resolves its string id. No <c>Content.*</c>
    /// public surface is added: these are Core-internal seams the slice's self-tests and (later) the
    /// realm/boss content author consume.
    ///
    /// Why this is mechanically safe for both tables (verified against the decompile):
    ///   - <c>FTK_realm.ID</c> and <c>FTK_enemySet.ID</c> are dictionary/string-keyed (no id == array index;
    ///     every lookup is GetEntryByInt/GetEntry over the int->row dictionary MakeIndex builds), so a
    ///     synthetic high-band id slots in cleanly with no positional constraint.
    ///   - <c>FTK_realmDB.GetIntFromID</c> and <c>FTK_enemySetDB.GetIntFromID</c> are the tolerant
    ///     <c>Enum.Parse</c> pattern the DbLookupPatcher prefix covers; no per-DB <c>GetEnum</c> Harmony
    ///     patch is needed (the boss/enemy-set resolution paths are int/dictionary only).
    /// </summary>
    internal static class RealmBossRegistration
    {
        /// <summary>
        /// Clone a vanilla <see cref="FTK_realm"/> row and register it via <see cref="ContentRegistry.Register"/>
        /// over <c>FTK_realmDB</c>. The clone inherits the template realm's art/audio/tile/flags
        /// (<c>m_LandHexes</c>/<c>m_TownHexes</c>/<c>m_WaterHexes</c>, <c>m_DioramaCode</c>, <c>m_MusicEvent</c>,
        /// <c>m_GroundMaterial</c>, <c>m_IsWater</c>, ...) so the realm is renderable immediately; override only
        /// what you need in <paramref name="configure"/>. Returns the registered row.
        /// </summary>
        internal static FTK_realm RegisterRealm(
            string modGuid, string id, FTK_realm.ID template, Action<FTK_realm> configure)
        {
            FTK_realmDB db = Content.Db<FTK_realmDB>();
            FTK_realm tmpl = db.GetEntry(template);
            FTK_realm row = (FTK_realm)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_realm)o); });
            return row;
        }

        /// <summary>
        /// Clone a vanilla <see cref="FTK_enemySet"/> row and register it via <see cref="ContentRegistry.Register"/>
        /// over <c>FTK_enemySetDB</c>. The clone inherits the template's party arrays / type / tier; fill the
        /// party arrays (<c>m_HalfParty</c> for solo correctness, <c>m_FullPartyNormal</c>/<c>m_FullPartyEasy</c>)
        /// in <paramref name="configure"/>. Keep <c>m_Type</c> OFF <see cref="EnemySetType.GenericBoss"/> (that is
        /// the only set path that calls the unpatched <c>FTK_enemySet.GetEnum</c>). Returns the registered row.
        /// </summary>
        internal static FTK_enemySet RegisterEnemySet(
            string modGuid, string id, FTK_enemySet.ID template, Action<FTK_enemySet> configure)
        {
            FTK_enemySetDB db = Content.Db<FTK_enemySetDB>();
            FTK_enemySet tmpl = db.GetEntry(template);
            FTK_enemySet row = (FTK_enemySet)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_enemySet)o); });
            return row;
        }
    }
}
