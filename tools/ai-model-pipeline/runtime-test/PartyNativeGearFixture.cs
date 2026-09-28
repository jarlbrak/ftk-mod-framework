using System;
using System.Collections.Generic;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    // Explicit native equipment setup for companion balance trials in the isolated copy.
    JObject PartyNativeGearStage(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "heroInstanceId", "classKey", "items");
        string action = Str(command, "action");
        if (action != "inspect" && action != "stage") throw new ArgumentException("action must be inspect or stage.");
        RequireSinglePlayer();
        RequireOutsideCombat();
        CatalogNoLinks(root);
        string classKey = Str(command, "classKey");
        if (classKey != "hunter" && classKey != "scholar" && classKey != "blacksmith")
            throw new ArgumentException("Explicit native hunter, scholar or blacksmith classKey required.");
        int heroId = LeaseObservationPin.ExactId(command, "heroInstanceId", false);
        CharacterOverworld hero = null;
        if (FTKHub.Instance == null || FTKHub.Instance.m_CharacterOverworlds == null)
            throw new InvalidOperationException("Native current party required.");
        foreach (CharacterOverworld candidate in FTKHub.Instance.m_CharacterOverworlds)
            if (candidate != null && candidate.GetInstanceID() == heroId)
            {
                if (hero != null) throw new InvalidOperationException("Ambiguous hero identity.");
                hero = candidate;
            }
        if (hero == null || hero.m_CharacterStats == null || hero.m_CharacterStats.m_HealthCurrent <= 0 ||
            !ReferenceEquals(hero.m_CharacterStats.m_CharacterOverworld, hero) || hero.m_PlayerInventory == null ||
            hero.GetDBEntry() == null || hero.GetDBEntry().m_ID != classKey)
            throw new InvalidOperationException("Exact living current-party hero and matching class required.");
        JArray requested = command["items"] as JArray;
        if (requested == null || requested.Count < 6 || requested.Count > 7)
            throw new ArgumentException("Six native equipment rows, plus an optional shield, required.");
        ItemContainer backpack = hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack);
        if (backpack == null) throw new InvalidOperationException("Native Backpack required.");
        var entries = new List<BlacksmithGearEntry>();
        var types = new HashSet<FTK_itembase.ObjectType>();
        foreach (JToken token in requested)
        {
            if (token.Type != JTokenType.String) throw new ArgumentException("Exact native row string IDs required.");
            string key = (string)token;
            if (string.IsNullOrEmpty(key) || key.Length > 128 || !Enum.IsDefined(typeof(FTK_itembase.ID), key))
                throw new ArgumentException("Item must be an assembly-defined native item ID: " + key);
            var item = (FTK_itembase.ID)Enum.Parse(typeof(FTK_itembase.ID), key, false);
            FTK_itembase row = FTK_itembase.GetItemBase(item);
            if (row == null || row.m_ID != key || !row.m_Equippable || !types.Add(row.m_ObjectType))
                throw new ArgumentException("Exact equippable native row with unique equipment type required: " + key);
            PlayerInventory.ContainerID slot;
            switch (row.m_ObjectType)
            {
                case FTK_itembase.ObjectType.armor: slot = PlayerInventory.ContainerID.Body; break;
                case FTK_itembase.ObjectType.boots: slot = PlayerInventory.ContainerID.Foot; break;
                case FTK_itembase.ObjectType.helmet: slot = PlayerInventory.ContainerID.Head; break;
                case FTK_itembase.ObjectType.necklace: slot = PlayerInventory.ContainerID.Neck; break;
                case FTK_itembase.ObjectType.trinket: slot = PlayerInventory.ContainerID.Trinket; break;
                case FTK_itembase.ObjectType.weapon: slot = PlayerInventory.ContainerID.RightHand; break;
                case FTK_itembase.ObjectType.shield: slot = PlayerInventory.ContainerID.LeftHand; break;
                default: throw new ArgumentException("Unsupported equipment type.");
            }
            ItemContainer target = hero.m_PlayerInventory.Get(slot);
            if (target == null) throw new InvalidOperationException("Native equipment container unavailable.");
            int total = OwnedAcrossEquipment(hero, item);
            if (total < 0 || total > 1 || (total == 1 && ExactCount(backpack, item) + ExactCount(target, item) != 1) ||
                (total == 0 && !backpack.CanAdd(item)))
                throw new InvalidOperationException("Ambiguous ownership or native Backpack capacity for " + key);
            entries.Add(new BlacksmithGearEntry { stringId = key, numericId = (int)item, itemId = item, row = row, container = slot });
        }
        foreach (FTK_itembase.ObjectType required in new[] { FTK_itembase.ObjectType.armor, FTK_itembase.ObjectType.boots,
            FTK_itembase.ObjectType.helmet, FTK_itembase.ObjectType.necklace, FTK_itembase.ObjectType.trinket, FTK_itembase.ObjectType.weapon })
            if (!types.Contains(required)) throw new ArgumentException("Missing native equipment type: " + required);
        BlacksmithGearEntry weapon = FindType(entries, FTK_itembase.ObjectType.weapon);
        BlacksmithGearEntry shield = FindType(entries, FTK_itembase.ObjectType.shield);
        if (weapon.row.m_ObjectSlot != FTK_itembase.ObjectSlot.oneHand && weapon.row.m_ObjectSlot != FTK_itembase.ObjectSlot.twoHands)
            throw new ArgumentException("Native one-hand or two-hand weapon required.");
        if (shield != null && weapon.row.m_ObjectSlot != FTK_itembase.ObjectSlot.oneHand)
            throw new ArgumentException("A shield cannot accompany a two-hand weapon.");
        JArray rows = new JArray();
        foreach (BlacksmithGearEntry entry in entries)
        {
            FTK_weaponStats2 nativeWeapon = entry.row as FTK_weaponStats2;
            rows.Add(new JObject { { "key", entry.stringId }, { "itemId", entry.numericId },
                { "type", entry.row.m_ObjectType.ToString() }, { "slot", entry.row.m_ObjectSlot.ToString() },
                { "minLevel", entry.row.m_MinLevel }, { "maxLevel", entry.row.m_MaxLevel },
                { "weaponSkill", nativeWeapon == null ? null : nativeWeapon._skilltest.ToString() },
                { "weaponDamage", nativeWeapon == null ? new JValue((object)null) : new JValue(nativeWeapon._maxdmg) },
                { "weaponDamageGainPerLevel", nativeWeapon == null ? new JValue((object)null) : new JValue(nativeWeapon._dmggain) } });
        }
        JObject before = EquipmentView(hero);
        if (action == "inspect") return new JObject { { "ok", true }, { "readOnly", true },
            { "heroInstanceId", heroId }, { "classKey", classKey }, { "rows", rows }, { "equipment", before } };
        JArray added = new JArray();
        foreach (BlacksmithGearEntry entry in entries)
            if (OwnedAcrossEquipment(hero, entry.itemId) == 0)
            {
                if (!backpack.Add(entry.itemId) || ExactCount(backpack, entry.itemId) != 1)
                    throw new InvalidOperationException("Native Backpack Add failed: " + entry.stringId);
                added.Add(entry.stringId);
            }
        // Native weapon equip runs before shield, independent of request order.
        foreach (BlacksmithGearEntry entry in entries)
            if (entry != shield && ExactCount(hero.m_PlayerInventory.Get(entry.container), entry.itemId) != 1)
                hero.ForceEquip(entry.itemId, false);
        if (shield != null && ExactCount(hero.m_PlayerInventory.Get(shield.container), shield.itemId) != 1)
            hero.ForceEquip(shield.itemId, false);
        foreach (BlacksmithGearEntry entry in entries)
            if (OwnedAcrossEquipment(hero, entry.itemId) != 1 || ExactCount(hero.m_PlayerInventory.Get(entry.container), entry.itemId) != 1)
                throw new InvalidOperationException("Native ForceEquip postcondition failed: " + entry.stringId);
        return new JObject { { "ok", true }, { "heroInstanceId", heroId }, { "classKey", classKey },
            { "rows", rows }, { "added", added }, { "before", before }, { "after", EquipmentView(hero) },
            { "scope", "Isolated native equipment setup, not natural acquisition or balanced-outcome evidence. No stats, level, combat, saves or shared database rows edited. Reobserve avatar weapon presence after the native rebuild settles." } };
    }
}
