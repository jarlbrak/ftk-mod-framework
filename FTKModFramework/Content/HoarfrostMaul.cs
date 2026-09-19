using UnityEngine;
using GridEditor;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// Spec #85 showcase: the Hoarfrost Maul, a two-handed hammer carrying a custom combat STATUS effect
    /// authored with ZERO new framework code. A status in this engine IS a proficiency row: cloning a
    /// vanilla FTK_proficiencyTable row through the shipped <see cref="Content.AddProficiency"/> inherits the
    /// whole vanilla status pipeline (the shared ProficiencyBase prefab, its Category, the per-Category
    /// HUD icon, immunity, the combat-log line, ticking via UpdateProficiency, and refresh-not-stack
    /// semantics keyed by Category). Duration is the row's own m_RepeatCount. Nothing here touches Core/,
    /// adds a Harmony patch, subclasses ProficiencyBase, or writes a vanilla global such as
    /// GameFlow.m_FrozenDmgPercent (the Frozen magnitude is inherited from vanilla, not configured).
    ///
    /// Phase 1 (this file): Rimefall Strike, a damaging hit that leaves the struck enemy Frozen (a clone of
    /// the player blunt Category.Ice row). While Frozen, DamageCalculator._calcDamage multiplies the
    /// enemy's incoming damage by the vanilla m_FrozenDmgPercent and the enemy HUD shows the vanilla
    /// frozen icon. A frozen ENEMY still acts: CharacterDummy.CanUseAbility is only consulted on the
    /// player side (ability trigger, Distract, Encourage), never by EnemyDummy.
    /// </summary>
    internal static class HoarfrostMaul
    {
        private const string WeaponKey = "ftkmf_hoarfrostmaul";
        private const string FrozenKey = "ftkmf_rimefallstrike";

        /// <summary>
        /// Frozen duration in ticks (ProficiencyRecord.m_Count). Each tick is 1 / m_Quickness seconds of
        /// combat time on the UpdateTime path, unless the SHARED Ice prefab flags m_IsEndOnTurn, in which case
        /// the record is decremented per turn instead. The prefab is vanilla and read-only to us, so the
        /// self-test logs which mode the clone inherited rather than asserting it.
        /// </summary>
        private const int FrozenTicks = 3;

        public static void Register()
        {
            // 1) The Frozen status: a clone of bluntIceReg, the player blunt Category.Ice row.
            //
            //    Template choice (spec #85 open question): bluntIceReg over bluntIceSplash. Both share the same
            //    vanilla ProficiencyIce prefab, so the STATUS they apply is identical (Category.Ice, the frozen
            //    icon, the freeze SFX loop, m_ImmuneIce handling). They differ only in the carrier attack: the
            //    "Splash" row exists to spread its hit across adjacent enemies (m_Target = Splash, which
            //    GetBattleButtonInfo renders as a multi-target action), which is one more inherited side effect
            //    than a single-target maul swing needs. "Reg" is the regular single-target ice swing, so it is
            //    the row with the fewest inherited side effects. Row VALUES live in the serialized DB asset,
            //    not in Assembly-CSharp, so the inherited m_Target / m_DamagePerAttack / m_Quickness are logged
            //    by VerifyHoarfrostMaul for the live pass rather than asserted here. Enemy en* ice rows are
            //    not valid templates: they carry enemy tendency weights and enemy-keyed animation triggers.
            Content.AddProficiency(Plugin.Guid, FrozenKey, FTK_proficiencyTable.ID.bluntIceReg, "Rimefall Strike",
                p =>
                {
                    p.m_RepeatCount = FrozenTicks; // DURATION. The one knob a cloning author owns for Frozen.

                    // m_FullSlots = false, DELIBERATELY (FR-3a). With true, m_ProfSuccess would require a
                    // PERFECT slot roll and a landed-but-imperfect swing would apply nothing. With false the gate
                    // is: the swing landed (not Dodge) AND the target is not ice-immune AND non-zero damage got
                    // through armor AND the m_ChanceToAffect roll. Live criterion: "any landed damaging hit
                    // applies Frozen; a complete miss or a fully-armored hit applies nothing."
                    p.m_FullSlots = false;
                    p.m_ChanceToAffect = 1f;    // no hidden second roll: a landed damaging hit always freezes
                    p.m_TargetFriendly = false; // hostile targeting; explicit because Warding Roar (Phase 2) flips it
                });
            // Explicit tooltip line (tier 1) so the battle button does not depend on the Category fallback text.
            Localization.SetProficiencyDescription(FrozenKey,
                "A crushing blow that leaves the target Frozen for a short time. Frozen enemies take extra damage from every hit.");

            // 2) The carrier weapon: a clone of the vanilla War Hammer (a plain physical blunt two-hander with
            //    no elemental action of its own, so the maul's status actions are the ones we attach, not
            //    something inherited from the template's prefab).
            FTK_weaponStats2 maul = Content.AddWeapon(Plugin.Guid, WeaponKey, FTK_itembase.ID.bluntWarHammer, "Hoarfrost Maul",
                w =>
                {
                    w._maxdmg += 2f;                                   // a step above the template, not a new tier
                    w.m_ItemRarity = FTK_itemRarityLevel.ID.rare;
                    w._goldValue = 180;
                    w.m_Dropable = true;
                });

            // 3) Attach the status action (one private prefab copy; the vanilla War Hammer is untouched).
            Content.AttachProficiencies(maul, FrozenKey);

            GiveToInnkeeper();
            VerifyHoarfrostMaul();
        }

        /// <summary>
        /// Verification aid (FR-3: "obtainable in a solo run"). The maul is appended to the INNKEEPER's start
        /// items, the same pattern that put a rum in that kit to verify Iron Belly. The Innkeeper is a custom
        /// row registered earlier in the fixed demo order, and its m_StartItems is already a private array,
        /// so this touches no vanilla row (NFR-4) and needs no new config entry. Start a new game as the
        /// Innkeeper and the maul is in the inventory.
        /// </summary>
        private static void GiveToInnkeeper()
        {
            int weaponId = Content.Db<FTK_weaponStats2DB>().GetIntFromID(WeaponKey);
            FTK_playerGameStart innkeeper = Content.Db<FTK_playerGameStartDB>().GetEntryByStringID("ftkmf_innkeeper");
            if (innkeeper == null || weaponId < 0)
            {
                Plugin.Log.LogWarning("HoarfrostMaul: could not resolve innkeeper/weapon (innkeeper=" +
                    (innkeeper == null ? "null" : "ok") + ", weaponId=" + weaponId + ").");
                return;
            }

            FTK_itembase.ID[] old = innkeeper.m_StartItems ?? new FTK_itembase.ID[0];
            FTK_itembase.ID[] next = new FTK_itembase.ID[old.Length + 1];
            System.Array.Copy(old, next, old.Length);
            next[old.Length] = (FTK_itembase.ID)weaponId;
            innkeeper.m_StartItems = next;
            Plugin.Log.LogInfo("Added the Hoarfrost Maul to Innkeeper start items (now " + next.Length +
                " items). Start a new game as the Innkeeper to see it.");
        }

        /// <summary>
        /// SELF-TEST idiom (mirrors ThiefClass.VerifyThief / InnkeeperClass.VerifyInnkeeper): one PASS/FAIL
        /// line per check. Combat behaviour is vanilla and cannot be asserted at TableManager.Initialize; these
        /// checks target the fields most likely to be silently wrong, all through public accessors.
        /// </summary>
        private static void VerifyHoarfrostMaul()
        {
            FTK_proficiencyTableDB profs = Content.Db<FTK_proficiencyTableDB>();

            // --- check 1: Frozen row registered, Category.Ice via the public GetCategory(), duration set,
            //     gate fields as authored. Inherited template fields are LOGGED for the live pass.
            int frozenId = profs.GetIntFromID(FrozenKey);
            FTK_proficiencyTable frozen = frozenId >= 0 ? profs.GetEntry((FTK_proficiencyTable.ID)frozenId) : null;
            bool frozenOk = frozen != null &&
                frozen.GetCategory() == ProficiencyBase.Category.Ice &&
                frozen.m_RepeatCount == FrozenTicks &&
                !frozen.m_FullSlots &&
                !frozen.m_TargetFriendly &&
                frozen.GetLocalizedDisplayName() == "Rimefall Strike";
            if (frozenOk)
                Plugin.Log.LogInfo("SELF-TEST PASS: Frozen status row (Rimefall Strike) [id=" + frozenId +
                    ", category=" + frozen.GetCategory() + ", repeatCount=" + frozen.m_RepeatCount +
                    ", fullSlots=" + frozen.m_FullSlots + ", chanceToAffect=" + frozen.m_ChanceToAffect +
                    " | inherited: target=" + frozen.m_Target + ", quickness=" + frozen.m_Quickness +
                    ", dmgPerAttack=" + frozen.m_DamagePerAttack + ", dmgMult=" + frozen.m_DmgMultiplier +
                    ", endOnTurn=" + (frozen.m_ProficiencyPrefab != null && frozen.m_ProficiencyPrefab.m_IsEndOnTurn) + "].");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: Frozen status row (Rimefall Strike); id=" + frozenId +
                    " row=" + (frozen == null ? "null" : "ok") +
                    " category=" + (frozen == null ? "n/a" : frozen.GetCategory().ToString()) +
                    " repeatCount=" + (frozen == null ? -1 : frozen.m_RepeatCount) +
                    " fullSlots=" + (frozen != null && frozen.m_FullSlots) +
                    " targetFriendly=" + (frozen != null && frozen.m_TargetFriendly) + ".");

            // --- check 2: GetEnum(id) round-trips to the synthetic id. A row that fails this is cached under
            //     ID.None by ProficiencyManager and silently never applies (regression guard for
            //     ProficiencyGetEnum_Patch).
            FTK_proficiencyTable.ID frozenEnum = FTK_proficiencyTable.GetEnum(FrozenKey);
            bool enumOk = frozenId >= 0 && frozenEnum == (FTK_proficiencyTable.ID)frozenId &&
                frozenEnum != FTK_proficiencyTable.ID.None;
            if (enumOk)
                Plugin.Log.LogInfo("SELF-TEST PASS: status GetEnum round-trip [" + FrozenKey + " -> " + (int)frozenEnum + "].");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: status GetEnum round-trip; " + FrozenKey + " -> " + (int)frozenEnum +
                    " (expected " + frozenId + ").");

            // --- check 3: the maul is registered and its prefab exposes the status action through the game's
            //     own instantiate path (uiWeaponDetail.GetWeaponProfIDs: Instantiate, GetComponentInChildren
            //     WITHOUT includeInactive).
            FTK_weaponStats2 maul = Content.Db<FTK_weaponStats2DB>().GetEntryByStringID(WeaponKey);
            int actionCount = -1;
            bool hasFrozen = false;
            if (maul != null && maul.m_Prefab != null)
            {
                GameObject inst = UnityEngine.Object.Instantiate(maul.m_Prefab);
                Weapon w = inst.GetComponentInChildren<Weapon>();
                if (w != null)
                {
                    System.Collections.Generic.List<FTK_proficiencyTable.ID> ids = w.GetProficiencyIDs();
                    actionCount = ids.Count;
                    hasFrozen = ids.Contains((FTK_proficiencyTable.ID)frozenId);
                }
                UnityEngine.Object.Destroy(inst);
            }
            bool maulOk = maul != null && maul.GetLocalizedName() == "Hoarfrost Maul" && hasFrozen;
            if (maulOk)
                Plugin.Log.LogInfo("SELF-TEST PASS: Hoarfrost Maul registered with Rimefall Strike attached [" +
                    actionCount + " combat actions on the prefab, maxdmg=" + maul._maxdmg + "].");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: Hoarfrost Maul; row=" + (maul == null ? "null" : "ok") +
                    " prefab=" + (maul != null && maul.m_Prefab != null ? "ok" : "null") +
                    " hasRimefallStrike=" + hasFrozen + " actions=" + actionCount + ".");
        }
    }
}
