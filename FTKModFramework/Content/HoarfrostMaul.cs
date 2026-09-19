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
    /// Phase 1: Rimefall Strike, a damaging hit that leaves the struck enemy Frozen (a clone of the player
    /// blunt Category.Ice row). While Frozen, DamageCalculator._calcDamage multiplies the enemy's incoming
    /// damage by the vanilla m_FrozenDmgPercent and the enemy HUD shows the vanilla frozen icon. A frozen
    /// ENEMY still acts: CharacterDummy.CanUseAbility is only consulted on the player side (ability
    /// trigger, Distract, Encourage), never by EnemyDummy.
    ///
    /// Phase 2: Warding Roar, a SELF-applied taunt (a clone of the Category.Taunt row) that makes enemies
    /// target the wielder. Two separate actions, never one combined action: they need opposite
    /// m_TargetFriendly values and so cannot coexist in one row.
    /// </summary>
    internal static class HoarfrostMaul
    {
        private const string WeaponKey = "ftkmf_hoarfrostmaul";
        private const string FrozenKey = "ftkmf_rimefallstrike";
        private const string RoarKey = "ftkmf_wardingroar";

        /// <summary>
        /// Frozen duration in ticks (ProficiencyRecord.m_Count). Each tick is 1 / m_Quickness seconds of
        /// combat time on the UpdateTime path, unless the SHARED Ice prefab flags m_IsEndOnTurn, in which case
        /// the record is decremented per turn instead. The prefab is vanilla and read-only to us, so the
        /// self-test logs which mode the clone inherited rather than asserting it.
        /// </summary>
        private const int FrozenTicks = 3;

        /// <summary>
        /// Warding Roar duration in ticks. Deliberately SHORT: while any hero is Taunting, vanilla does three
        /// things, not one. EncounterSessionMC.StartNextCombatRound2 forces taunting heroes into the enemy
        /// target list (the redirect we want), EnemyDummy zeroes every enemy's chance to use a proficiency
        /// (EncounterSession.AnyPlayersTaunting), and the same check blocks enemy fleeing. The last two are
        /// PARTY-WIDE and materially stronger than a bare redirect, so 2 ticks is a conservative opening
        /// number, open for a game-designer pass once the live observations are in.
        /// </summary>
        private const int RoarTicks = 2;

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

            // 2) The Warding Roar status: a clone of the vanilla `taunt` row (Category.Taunt), applied to the
            //    WIELDER. Vanilla applies taunt from the taunt button (CharacterOverworld.CanTaunt, gated by
            //    CharacterSkills.m_Taunt, which we never read or write); our application path is a weapon
            //    action, so every field that path reads is set EXPLICITLY rather than inherited:
            //    - m_TargetFriendly = true: DamageCalculator.StartEngageAttack reassigns the damaged dummy to
            //      the attacker. This is the mechanism that makes any self-buff possible. It also zeroes the
            //      evade rating, so a hero cannot dodge his own roar.
            //    - m_Target = TargetType.None: a single self target, not PickFriendly / OthersFriendly.
            //    - m_Harmless = true: zeroes the damage modifier (the wielder takes ZERO damage) and exempts the
            //      action from the "zero received damage cancels the proficiency" rule in DummyDamageInfo.
            //      NOT m_IgnoresArmor: that chip technique is for HOSTILE procs; on a friendly-targeted row it
            //      would force the hero's own armor to zero and deal him unmitigated self-damage.
            //    - m_FullSlots = false: with true, an imperfect roll clears m_ProfSuccess AND (because the row
            //      is friendly-targeted) collapses the target, so the roar would fizzle on anything but a
            //      perfect roll. The roar is a buff, so any landed roll applies it.
            //    Never apply a Category.Taunt row to an ENEMY: ProficiencyTaunt.AddToDummy and End dereference
            //    m_CharacterOverworld, which is null on an EnemyDummy. The self-target field set above is what
            //    keeps it on the hero.
            Content.AddProficiency(Plugin.Guid, RoarKey, FTK_proficiencyTable.ID.taunt, "Warding Roar",
                p =>
                {
                    p.m_RepeatCount = RoarTicks;                // DURATION (see RoarTicks for why it is short)
                    p.m_TargetFriendly = true;                  // self: the damaged dummy becomes the attacker
                    p.m_Target = CharacterDummy.TargetType.None; // one target (the wielder), no friendly pick
                    p.m_Harmless = true;                        // zero damage, and exempt from the zero-damage cancel
                    p.m_FullSlots = false;                      // any landed roll applies; no perfect-roll gate
                    p.m_ChanceToAffect = 1f;                    // no hidden second roll
                });
            // Category.Taunt has no entry in GetCategoryDescription, so without this the tooltip effect line
            // would be a placeholder. No status ICON exists for Taunt either; the feedback surfaces are the
            // vanilla STR_HudTaunt float text and the AddToDummy combat-log line.
            Localization.SetProficiencyDescription(RoarKey,
                "A bellowing challenge. Enemies turn their attacks on the wielder for a short time.");

            // 3) The carrier weapon: a clone of the vanilla War Hammer (a plain physical blunt two-hander with
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

            // 4) Attach both status actions (one private prefab copy; the vanilla War Hammer is untouched).
            Content.AttachProficiencies(maul, FrozenKey, RoarKey);

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

            // --- check 2: Warding Roar row registered, Category.Taunt via GetCategory(), duration set, and the
            //     three self-target fields. These are asserted explicitly because the template's vanilla
            //     application path (the taunt button) never reads them, so a wrong inherited value would only
            //     show up as self-damage or an auto-cancelled action in combat.
            int roarId = profs.GetIntFromID(RoarKey);
            FTK_proficiencyTable roar = roarId >= 0 ? profs.GetEntry((FTK_proficiencyTable.ID)roarId) : null;
            bool roarOk = roar != null &&
                roar.GetCategory() == ProficiencyBase.Category.Taunt &&
                roar.m_RepeatCount == RoarTicks &&
                roar.m_TargetFriendly &&
                roar.m_Target == CharacterDummy.TargetType.None &&
                roar.m_Harmless &&
                !roar.m_FullSlots &&
                roar.GetLocalizedDisplayName() == "Warding Roar";
            if (roarOk)
                Plugin.Log.LogInfo("SELF-TEST PASS: Warding Roar status row (self-target field set) [id=" + roarId +
                    ", category=" + roar.GetCategory() + ", repeatCount=" + roar.m_RepeatCount +
                    ", targetFriendly=" + roar.m_TargetFriendly + ", target=" + roar.m_Target +
                    ", harmless=" + roar.m_Harmless + ", fullSlots=" + roar.m_FullSlots +
                    ", ignoresArmor=" + roar.m_IgnoresArmor + " | inherited: quickness=" + roar.m_Quickness +
                    ", endOnTurn=" + (roar.m_ProficiencyPrefab != null && roar.m_ProficiencyPrefab.m_IsEndOnTurn) + "].");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: Warding Roar status row; id=" + roarId +
                    " row=" + (roar == null ? "null" : "ok") +
                    " category=" + (roar == null ? "n/a" : roar.GetCategory().ToString()) +
                    " repeatCount=" + (roar == null ? -1 : roar.m_RepeatCount) +
                    " targetFriendly=" + (roar != null && roar.m_TargetFriendly) +
                    " target=" + (roar == null ? "n/a" : roar.m_Target.ToString()) +
                    " harmless=" + (roar != null && roar.m_Harmless) +
                    " fullSlots=" + (roar != null && roar.m_FullSlots) + ".");

            // --- check 3: GetEnum(id) round-trips to the synthetic id for BOTH rows. A row that fails this is
            //     cached under ID.None by ProficiencyManager and silently never applies (regression guard for
            //     ProficiencyGetEnum_Patch).
            FTK_proficiencyTable.ID frozenEnum = FTK_proficiencyTable.GetEnum(FrozenKey);
            FTK_proficiencyTable.ID roarEnum = FTK_proficiencyTable.GetEnum(RoarKey);
            bool enumOk =
                frozenId >= 0 && frozenEnum == (FTK_proficiencyTable.ID)frozenId && frozenEnum != FTK_proficiencyTable.ID.None &&
                roarId >= 0 && roarEnum == (FTK_proficiencyTable.ID)roarId && roarEnum != FTK_proficiencyTable.ID.None;
            if (enumOk)
                Plugin.Log.LogInfo("SELF-TEST PASS: status GetEnum round-trip [" + FrozenKey + " -> " + (int)frozenEnum +
                    ", " + RoarKey + " -> " + (int)roarEnum + "].");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: status GetEnum round-trip; " + FrozenKey + " -> " + (int)frozenEnum +
                    " (expected " + frozenId + "), " + RoarKey + " -> " + (int)roarEnum + " (expected " + roarId + ").");

            // --- check 4: the maul is registered and its prefab exposes BOTH status actions through the game's
            //     own instantiate path (uiWeaponDetail.GetWeaponProfIDs: Instantiate, GetComponentInChildren
            //     WITHOUT includeInactive). The total action count is logged for the live "exactly two
            //     actions" check, since the template prefab's own action list is not readable offline.
            FTK_weaponStats2 maul = Content.Db<FTK_weaponStats2DB>().GetEntryByStringID(WeaponKey);
            int actionCount = -1;
            bool hasFrozen = false;
            bool hasRoar = false;
            if (maul != null && maul.m_Prefab != null)
            {
                GameObject inst = UnityEngine.Object.Instantiate(maul.m_Prefab);
                Weapon w = inst.GetComponentInChildren<Weapon>();
                if (w != null)
                {
                    System.Collections.Generic.List<FTK_proficiencyTable.ID> ids = w.GetProficiencyIDs();
                    actionCount = ids.Count;
                    hasFrozen = ids.Contains((FTK_proficiencyTable.ID)frozenId);
                    hasRoar = ids.Contains((FTK_proficiencyTable.ID)roarId);
                }
                UnityEngine.Object.Destroy(inst);
            }
            bool maulOk = maul != null && maul.GetLocalizedName() == "Hoarfrost Maul" && hasFrozen && hasRoar;
            if (maulOk)
                Plugin.Log.LogInfo("SELF-TEST PASS: Hoarfrost Maul registered with Rimefall Strike + Warding Roar attached [" +
                    actionCount + " combat actions on the prefab, maxdmg=" + maul._maxdmg + "].");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: Hoarfrost Maul; row=" + (maul == null ? "null" : "ok") +
                    " prefab=" + (maul != null && maul.m_Prefab != null ? "ok" : "null") +
                    " hasRimefallStrike=" + hasFrozen + " hasWardingRoar=" + hasRoar + " actions=" + actionCount + ".");
        }
    }
}
