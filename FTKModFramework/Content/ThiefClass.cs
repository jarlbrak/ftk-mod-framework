using UnityEngine;
using GridEditor;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// A WoW-style Thief: fast, evasive, crit/bleed dagger striker with a signature Steal that grants
    /// level-scaling gold. Demonstrates the full stack — a custom-behaviour ability + custom weapon +
    /// new playable class.
    /// </summary>
    internal static class ThiefClass
    {
        public static void Register()
        {
            // 1) Combat actions cloned from fitting vanilla proficiencies
            Content.AddProficiency(Plugin.Guid, "ftkmf_backstab", FTK_proficiencyTable.ID.bladeKnifeArmor, "Backstab",
                p => { p.m_DmgMultiplier = 1.4f; p.m_IgnoresArmor = true; });
            Content.AddProficiency(Plugin.Guid, "ftkmf_sinisterstrike", FTK_proficiencyTable.ID.bleed1, "Sinister Strike",
                p => { p.m_DmgMultiplier = 1.1f; });
            Content.AddProficiency(Plugin.Guid, "ftkmf_eviscerate", FTK_proficiencyTable.ID.heavyattack, "Eviscerate",
                p => { p.m_DmgMultiplier = 1.6f; });

            // Native Steal: light damage + level-scaling gold via a custom ProficiencyBase behaviour.
            ThiefStealProficiency stealBehaviour =
                (ThiefStealProficiency)BehaviorHost.Create(typeof(ThiefStealProficiency), "ftkmf_ThiefStealProf");
            stealBehaviour.m_Category = ProficiencyBase.Category.StealGold; // resting default; AddToDummy retargets per hit

            Content.AddProficiency(Plugin.Guid, "ftkmf_steal", FTK_proficiencyTable.ID.bladeDamage, "Steal",
                p =>
                {
                    p.m_DmgMultiplier = 0.15f;             // ~1 chip damage: a true 0-dmg hit is auto-blocked and can't be gated by the roll
                    p.m_IgnoresArmor = true;               // chip must bypass armor, else armor>=1 reduces it to 0 and re-blocks the steal
                    p.m_SlotOverride = 1;                  // ONE roll
                    p.m_PerSlotSkillRoll = -0.5f;          // drop slot accuracy to ~20% (the ROLL is the steal chance; Focus can guarantee it)
                    p.m_ChanceToAffect = 1f;               // a landed roll always steals (no hidden second roll)
                    p.m_ProficiencyPrefab = stealBehaviour; // custom: 50/50 item-or-gold on a successful roll
                });
            // Explicit (tier-1) tooltip so Steal doesn't depend on the mutable m_Category / tier-2 fallback.
            Localization.SetProficiencyDescription("ftkmf_steal",
                "Lands a quick strike that lifts either gold or an item from the enemy.");

            // 2) The Thief's dagger (cloned from the vanilla Dagger)
            FTK_weaponStats2 dagger = Content.AddWeapon(Plugin.Guid, "ftkmf_shadowfang", FTK_itembase.ID.bladeDagger, "Shadowfang",
                w =>
                {
                    w._skilltest = FTK_weaponStats2.SkillType.quickness; // matches the class primary (Speed)
                    w._slots = 3;
                    w._dmgtype = FTK_weaponStats2.DamageType.physical;
                    w._maxdmg = 8f;
                    w._dmggain = 0.6f;
                    w.m_ItemRarity = FTK_itemRarityLevel.ID.rare;
                    w._goldValue = 120;
                });

            // 3) Give the dagger its rogue actions + Steal (one private prefab copy)
            Content.AttachProficiencies(dagger, "ftkmf_backstab", "ftkmf_sinisterstrike", "ftkmf_eviscerate", "ftkmf_steal");

            // 4) The class (cloned from the Treasure Hunter: agile model + sane defaults)
            int daggerId = Content.Db<FTK_weaponStats2DB>().GetIntFromID("ftkmf_shadowfang");
            Content.AddClass(Plugin.Guid, "ftkmf_thief", FTK_playerGameStart.ID.treasureHunter, "Thief",
                c =>
                {
                    // Peer-budgeted (~3.72 total; classes range 3.64-4.20). Speed-primary, finesse-heavy, squishy.
                    c._quickness = 0.80f; // Speed: PRIMARY (fastest class). First turns + best evade + dagger slot rolls.
                    c._talent = 0.74f;    // strong secondary: dagger/scroll rolls, skill checks
                    c._awareness = 0.70f; // ambush avoidance / encounter initiative
                    c._toughness = 0.50f; // modest physical armor
                    c._vitality = 0.52f;  // low-ish HP (glass cannon)
                    c._fortitude = 0.46f; // low magic resist
                    c._basefocus = 3;
                    c._startinggold = 8;  // modest; the Thief earns gold via Steal
                    c.m_PrimaryWeaponStat = FTK_weaponStats2.SkillType.quickness; // Speed-primary
                    c.m_DLC = FTK_dlc.ID.None;
                    c.m_Release = true;
                    c.m_StartWeapon = (FTK_itembase.ID)daggerId;
                    c.m_StartItems = new FTK_itembase.ID[]
                    {
                        FTK_itembase.ID.armorMagicLeather,
                        FTK_itembase.ID.bootsQuick,
                        FTK_itembase.ID.trinketEvade1,
                        FTK_itembase.ID.conLockpicks,
                    };
                    c.m_CharacterSkills = new CharacterSkills
                    {
                        m_Sneak = true,
                        m_Ambush = true,
                        m_TrapDisarm = true,
                        m_CounterAttack = true,
                    };
                });

            Localization.SetClassFlavor("ftkmf_thief",
                "The thief is quick and elusive, striking from the shadows with a swift dagger and slipping away with a foe's coin before they even notice it's gone.");

            VerifyThief();
        }

        private static void VerifyThief()
        {
            FTK_playerGameStartDB db = Content.Db<FTK_playerGameStartDB>();
            int id = db.GetIntFromID("ftkmf_thief");
            int lastIndex = ((System.Array)Reflect.GetField(db, "m_Array")).Length - 1;
            FTK_playerGameStart thief = db.GetEntry((FTK_playerGameStart.ID)id);

            string name = thief != null ? thief.GetDisplayName() : "(null)";
            bool idIsIndex = id == lastIndex;
            FTK_itembase weapon = thief != null ? FTK_itembase.GetItemBase(thief.m_StartWeapon) : null;

            // Confirm Steal resolves as a proficiency on the dagger's prefab (the game's exact path).
            int stealId = TableManager.Instance.Get<FTK_proficiencyTableDB>().GetIntFromID("ftkmf_steal");
            FTK_weaponStats2 dagger = Content.Db<FTK_weaponStats2DB>().GetEntryByStringID("ftkmf_shadowfang");
            bool daggerHasSteal = false;
            if (dagger != null && dagger.m_Prefab != null)
            {
                GameObject inst = UnityEngine.Object.Instantiate(dagger.m_Prefab);
                Weapon w = inst.GetComponentInChildren<Weapon>();
                if (w != null) daggerHasSteal = w.GetProficiencyIDs().Contains((FTK_proficiencyTable.ID)stealId);
                UnityEngine.Object.Destroy(inst);
            }

            bool ok = thief != null && name == "Thief" && idIsIndex && weapon != null && daggerHasSteal;
            if (ok)
                Plugin.Log.LogInfo("SELF-TEST PASS [class]: Thief at id/index " + id + ", weapon=\"" +
                    weapon.GetLocalizedName() + "\", Steal on dagger=" + daggerHasSteal + ", quickness=" + thief._quickness + ".");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [class]: id=" + id + " lastIndex=" + lastIndex + " name=\"" + name +
                    "\" weapon=" + (weapon == null ? "null" : "ok") + " daggerHasSteal=" + daggerHasSteal + ".");
        }
    }
}
