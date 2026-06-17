using System.Collections.Generic;

namespace FTKModFramework.Core.Data
{
    /// <summary>
    /// A curated, per-kind map of FRIENDLY names to the real serialized field on the cloned game row
    /// (spec #6, FR-5). It is a convenience layer ONLY: an alias is rewritten to its real field BEFORE
    /// <see cref="OverrideEngine"/> looks the member up, so a friendly alias and the raw field name take
    /// the byte-for-byte identical resolution path. The raw <c>fields</c> name stays the escape hatch
    /// for anything not aliased here.
    ///
    /// Aliases are curated against the FULL inheritance chain (R7): a weapon (FTK_weaponStats2 : FTK_itembase)
    /// exposes both its own fields (_maxdmg, _skilltest, ...) and inherited base fields (m_ItemRarity,
    /// _goldValue, ...); the class stat block lives on FTK_playerGameStart. Every real field below was
    /// confirmed against the decompiled Assembly-CSharp (FTK_itembase / FTK_weaponStats2 / FTK_playerGameStart).
    /// Only HIGH-VALUE fields are aliased on purpose; the table is small and obvious.
    /// </summary>
    internal static class AliasTable
    {
        // kind (lower-case) -> (friendly alias -> real field name).
        private static readonly Dictionary<string, Dictionary<string, string>> Map = Build();

        /// <summary>
        /// Resolve a member name for a kind: if it is a known alias for that kind, return the real field;
        /// otherwise return <paramref name="name"/> unchanged (it is already a raw field name, or unknown,
        /// in which case OverrideEngine warns on the lookup miss). Case-insensitive on the alias key.
        /// </summary>
        public static string Resolve(string kind, string name)
        {
            if (kind == null || name == null) return name;

            Dictionary<string, string> aliases;
            if (!Map.TryGetValue(kind.ToLowerInvariant(), out aliases)) return name;

            string real;
            if (aliases.TryGetValue(name.ToLowerInvariant(), out real)) return real;
            return name;
        }

        private static Dictionary<string, Dictionary<string, string>> Build()
        {
            Dictionary<string, Dictionary<string, string>> m =
                new Dictionary<string, Dictionary<string, string>>();

            // --- shared item-base aliases (FTK_itembase) ---
            // Reused by weapon and item kinds so the same friendly name means the same field everywhere.
            Dictionary<string, string> itemBase = new Dictionary<string, string>
            {
                { "rarity", "m_ItemRarity" },     // FTK_itembase.m_ItemRarity  (FTK_itemRarityLevel.ID)
                { "goldvalue", "_goldValue" },     // FTK_itembase._goldValue    (int)
                { "minlevel", "m_MinLevel" },      // FTK_itembase.m_MinLevel    (int)
                { "maxlevel", "m_MaxLevel" },      // FTK_itembase.m_MaxLevel    (int)
                { "dropable", "m_Dropable" },      // FTK_itembase.m_Dropable    (bool)
                { "townmarket", "m_TownMarket" },  // FTK_itembase.m_TownMarket  (bool)
                { "dlc", "m_DLC" },                // FTK_itembase.m_DLC         (FTK_dlc.ID)
            };

            // --- weapon (FTK_weaponStats2 : FTK_itembase) ---
            Dictionary<string, string> weapon = CopyOf(itemBase);
            weapon["damage"] = "_maxdmg";          // FTK_weaponStats2._maxdmg     (float)
            weapon["damagetype"] = "_dmgtype";     // FTK_weaponStats2._dmgtype    (FTK_weaponStats2.DamageType)
            weapon["skill"] = "_skilltest";        // FTK_weaponStats2._skilltest  (FTK_weaponStats2.SkillType)
            weapon["slots"] = "_slots";            // FTK_weaponStats2._slots      (int)
            weapon["damagegain"] = "_dmggain";     // FTK_weaponStats2._dmggain    (float)
            m["weapon"] = weapon;

            // --- item (FTK_items : FTK_itembase): the shared item-base aliases only ---
            m["item"] = CopyOf(itemBase);

            // --- proficiency (FTK_proficiencyTable): high-value combat-action fields ---
            m["proficiency"] = new Dictionary<string, string>
            {
                { "damage", "m_DmgMultiplier" },   // FTK_proficiencyTable.m_DmgMultiplier  (float)
                { "ignoresarmor", "m_IgnoresArmor" }, // FTK_proficiencyTable.m_IgnoresArmor   (bool)
                { "chancetoaffect", "m_ChanceToAffect" }, // FTK_proficiencyTable.m_ChanceToAffect (float)
                { "slots", "m_SlotOverride" },     // FTK_proficiencyTable.m_SlotOverride   (int)
            };

            // --- class stat block (FTK_playerGameStart). No luck field; rarity is item-only. ---
            m["class"] = new Dictionary<string, string>
            {
                { "strength", "_toughness" },      // STR  -> _toughness  (float)
                { "intelligence", "_fortitude" },  // INT/magic -> _fortitude (float)
                { "awareness", "_awareness" },     // _awareness (float)
                { "talent", "_talent" },           // _talent    (float)
                { "speed", "_quickness" },         // SPD  -> _quickness (float)
                { "vitality", "_vitality" },       // _vitality  (float)
                { "startinggold", "_startinggold" }, // _startinggold (int)
                { "focus", "_basefocus" },         // _basefocus  (int)
                { "primarystat", "m_PrimaryWeaponStat" }, // m_PrimaryWeaponStat (FTK_weaponStats2.SkillType)
                { "startweapon", "m_StartWeapon" }, // m_StartWeapon (FTK_itembase.ID)
                { "startitems", "m_StartItems" },  // m_StartItems  (FTK_itembase.ID[])
                { "skills", "m_CharacterSkills" }, // m_CharacterSkills (CharacterSkills)
                { "dlc", "m_DLC" },                // m_DLC (FTK_dlc.ID)
            };

            return m;
        }

        private static Dictionary<string, string> CopyOf(Dictionary<string, string> src)
        {
            Dictionary<string, string> dst = new Dictionary<string, string>();
            foreach (KeyValuePair<string, string> kv in src) dst[kv.Key] = kv.Value;
            return dst;
        }
    }
}
