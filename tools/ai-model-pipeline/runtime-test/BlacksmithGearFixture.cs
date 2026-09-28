using System;
using System.Collections.Generic;
using System.Reflection;
using GridEditor;
using Newtonsoft.Json.Linq;

// Isolated game-test operations for the unpublished Blacksmith gear package.
// RuntimeModelTest itself refuses to run outside its task-owned scratch game copy.
public sealed partial class RuntimeModelTest
{
    sealed class BlacksmithGearEntry
    {
        internal string stringId;
        internal int numericId;
        internal FTK_itembase.ID itemId;
        internal FTK_itembase row;
        internal PlayerInventory.ContainerID container;
        internal int ownedCount;
    }

    static BlacksmithGearEntry ResolveBlacksmithGear(string stringId)
    {
        if (string.IsNullOrEmpty(stringId) || stringId.Length > 128 ||
            !stringId.StartsWith("blacksmith_", StringComparison.Ordinal))
            throw new ArgumentException("Exact Blacksmith package item string ID required.");
        Type registry = CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.ContentRegistry", true);
        MethodInfo lookupMethod = registry.GetMethod("TryGetSyntheticId", Statics);
        if (lookupMethod == null) throw new MissingMethodException("ContentRegistry.TryGetSyntheticId unavailable.");
        object[] lookup = { stringId, -1, new Type[] { typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB) } };
        if (!(bool)lookupMethod.Invoke(null, lookup))
            throw new InvalidOperationException("Blacksmith item is not registered in an item database: " + stringId);
        int numericId = (int)lookup[1];
        FTK_itembase.ID itemId = (FTK_itembase.ID)numericId;
        FTK_itembase row = FTK_itembase.GetItemBase(itemId);
        if (row == null || !string.Equals(row.m_ID, stringId, StringComparison.Ordinal) || !row.m_Equippable)
            throw new InvalidOperationException("Synthetic item ID did not resolve to its exact equippable row: " + stringId);

        PlayerInventory.ContainerID container;
        switch (row.m_ObjectType)
        {
            case FTK_itembase.ObjectType.armor: container = PlayerInventory.ContainerID.Body; break;
            case FTK_itembase.ObjectType.boots: container = PlayerInventory.ContainerID.Foot; break;
            case FTK_itembase.ObjectType.helmet: container = PlayerInventory.ContainerID.Head; break;
            case FTK_itembase.ObjectType.necklace: container = PlayerInventory.ContainerID.Neck; break;
            case FTK_itembase.ObjectType.trinket: container = PlayerInventory.ContainerID.Trinket; break;
            case FTK_itembase.ObjectType.weapon: container = PlayerInventory.ContainerID.RightHand; break;
            case FTK_itembase.ObjectType.shield: container = PlayerInventory.ContainerID.LeftHand; break;
            default: throw new InvalidOperationException("Unsupported Blacksmith equipment type: " + row.m_ObjectType);
        }
        return new BlacksmithGearEntry { stringId = stringId, numericId = numericId, itemId = itemId, row = row, container = container };
    }

    static CharacterOverworld ExactBlacksmithHero(int heroInstanceId)
    {
        return ExactBlacksmithGrantHero(heroInstanceId, false);
    }

    static bool BlacksmithGrantClassAllowed(string classId, bool allowNonBlacksmith)
    {
        return classId == "blacksmith" || (allowNonBlacksmith && (classId == "hunter" || classId == "scholar"));
    }

    static bool ApprovedBlacksmithGrantId(string id)
    {
        foreach (string tier in new[] { "coalmark", "bellowsworn", "rivetwatch", "kilnward" })
            foreach (string kind in new[] { "hammer_1h", "hammer_2h", "shield", "helmet", "trinket", "necklace", "armor", "boots" })
                if (id == "blacksmith_" + kind + "_" + tier) return true;
        return false;
    }

    static CharacterOverworld ExactBlacksmithGrantHero(int heroInstanceId, bool allowNonBlacksmith)
    {
        if (heroInstanceId == 0 || FTKHub.Instance == null || FTKHub.Instance.m_CharacterOverworlds == null)
            throw new ArgumentException("Exact living current-party Blacksmith hero instance required.");
        CharacterOverworld result = null;
        foreach (CharacterOverworld candidate in FTKHub.Instance.m_CharacterOverworlds)
            if (candidate != null && candidate.GetInstanceID() == heroInstanceId)
            {
                if (result != null) throw new InvalidOperationException("Ambiguous current-party hero instance.");
                result = candidate;
            }
        if (result == null || result.m_CharacterStats == null || result.m_PlayerInventory == null ||
            result.m_CharacterStats.m_HealthCurrent <= 0 || !ReferenceEquals(result.m_CharacterStats.m_CharacterOverworld, result))
            throw new InvalidOperationException("Exact living current-party hero with native inventory required.");
        FTK_playerGameStart classRow = result.GetDBEntry();
        if (classRow == null || !BlacksmithGrantClassAllowed(classRow.m_ID, allowNonBlacksmith))
            throw new InvalidOperationException("The selected hero must be the registered Blacksmith class.");
        if (allowNonBlacksmith && (!SceneOwner(result) || !result.gameObject.activeInHierarchy ||
            !result.IsOwner || result.m_CharacterStats.m_IsInCombat || result.m_WaitForRespawn))
            throw new InvalidOperationException("Owned living noncombat native party hero required for wearability grants.");
        return result;
    }

    static int OwnedAcrossEquipment(CharacterOverworld hero, FTK_itembase.ID item)
    {
        int total = 0;
        foreach (PlayerInventory.ContainerID slot in Enum.GetValues(typeof(PlayerInventory.ContainerID)))
        {
            ItemContainer container = hero.m_PlayerInventory.Get(slot);
            if (container != null) total += ExactCount(container, item);
        }
        return total;
    }

    static bool BlacksmithGrantPreservedEquals(JToken expected, JToken actual)
    {
        if (expected == null || actual == null) return ReferenceEquals(expected, actual);
        if (expected.Type != actual.Type) return false;
        JObject expectedObject = expected as JObject;
        if (expectedObject != null)
        {
            JObject actualObject = (JObject)actual;
            int expectedCount = 0, actualCount = 0;
            foreach (JProperty property in expectedObject.Properties())
            {
                expectedCount++;
                if (!BlacksmithGrantPreservedEquals(property.Value, actualObject[property.Name])) return false;
            }
            foreach (JProperty property in actualObject.Properties()) actualCount++;
            return expectedCount == actualCount;
        }
        JArray expectedArray = expected as JArray;
        if (expectedArray != null)
        {
            JArray actualArray = (JArray)actual;
            if (expectedArray.Count != actualArray.Count) return false;
            for (int index = 0; index < expectedArray.Count; index++)
                if (!BlacksmithGrantPreservedEquals(expectedArray[index], actualArray[index])) return false;
            return true;
        }
        // Native dictionaries reuse removed entries, so object enumeration order can
        // change after Add. Preserve every key/value and array order without relying
        // on shipped Newtonsoft's broken empty-object DeepEquals implementation.
        return string.Equals(expected.ToString(Newtonsoft.Json.Formatting.None),
            actual.ToString(Newtonsoft.Json.Formatting.None), StringComparison.Ordinal);
    }

    JObject BlacksmithGearGrant(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "heroInstanceId", "items", "allowNonBlacksmith");
        RequireSinglePlayer();
        RequireOutsideCombat();
        CatalogNoLinks(root);
        if (blacksmithAppearance != null) throw new InvalidOperationException("Restore appearance before changing gear.");
        int heroId = Int(command, "heroInstanceId", 0);
        JToken allowToken = command["allowNonBlacksmith"];
        if (allowToken != null && allowToken.Type != JTokenType.Boolean)
            throw new ArgumentException("allowNonBlacksmith must be boolean.");
        bool allowNonBlacksmith = allowToken != null && (bool)allowToken;
        CharacterOverworld hero = ExactBlacksmithGrantHero(heroId, allowNonBlacksmith);
        JArray requested = command["items"] as JArray;
        if (requested == null || requested.Count < 1 || requested.Count > 32)
            throw new ArgumentException("Provide 1 to 32 exact Blacksmith item string IDs.");

        List<BlacksmithGearEntry> entries = new List<BlacksmithGearEntry>();
        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        ItemContainer backpack = hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack);
        if (backpack == null) throw new InvalidOperationException("Native Backpack container required.");
        JArray before = new JArray();
        foreach (JToken token in requested)
        {
            if (token == null || token.Type != JTokenType.String) throw new ArgumentException("Each item must be a string ID.");
            string stringId = (string)token;
            if (!unique.Add(stringId)) throw new ArgumentException("Duplicate Blacksmith item ID: " + stringId);
            if (!ApprovedBlacksmithGrantId(stringId)) throw new ArgumentException("Approved 32-item Blacksmith gear ID required: " + stringId);
            BlacksmithGearEntry entry = ResolveBlacksmithGear(stringId);
            int owned = OwnedAcrossEquipment(hero, entry.itemId);
            if (owned == 0 && !backpack.CanAdd(entry.itemId))
                throw new InvalidOperationException("Native Backpack cannot add item: " + stringId);
            entries.Add(entry);
            before.Add(new JObject { { "item", stringId }, { "numericId", entry.numericId }, { "objectType", entry.row.m_ObjectType.ToString() }, { "ownedCount", owned } });
        }

        JObject preservedBefore = BlacksmithAppearancePreserved(hero);
        JObject expected = BlacksmithAppearancePreserved(hero);
        JObject statsBefore = BlacksmithAppearanceScalars(hero.m_CharacterStats);
        FTK_playerGameStart.SkinType skinBefore = hero.m_SkinType;
        CharacterStats statsOwner = hero.m_CharacterStats;
        PlayerInventory inventoryOwner = hero.m_PlayerInventory;
        JArray added = new JArray();
        foreach (BlacksmithGearEntry entry in entries)
        {
            if (OwnedAcrossEquipment(hero, entry.itemId) != 0) continue;
            if (!backpack.Add(entry.itemId) || ExactCount(backpack, entry.itemId) != 1)
                throw new InvalidOperationException("Native Backpack Add failed or did not reach exactly one item: " + entry.stringId);
            ((JObject)expected["inventory"][PlayerInventory.ContainerID.Backpack.ToString()])[
                entry.numericId.ToString(System.Globalization.CultureInfo.InvariantCulture)] = 1;
            added.Add(entry.stringId);
        }
        JObject statsAfter = BlacksmithAppearanceScalars(hero.m_CharacterStats);
        JObject preservedAfter = BlacksmithAppearancePreserved(hero);
        bool statsOwnerSame = ReferenceEquals(statsOwner, hero.m_CharacterStats);
        bool inventoryOwnerSame = ReferenceEquals(inventoryOwner, hero.m_PlayerInventory);
        bool skinSame = skinBefore == hero.m_SkinType;
        bool statsSame = BlacksmithGrantPreservedEquals(statsBefore, statsAfter);
        bool inventoryAndAppearanceSame = BlacksmithGrantPreservedEquals(expected, preservedAfter);
        if (!statsOwnerSame || !inventoryOwnerSame || !skinSame || !statsSame || !inventoryAndAppearanceSame)
            return new JObject {
                { "ok", false }, { "heroInstanceId", hero.GetInstanceID() }, { "added", added },
                { "error", "Grant preservation failed after native Add; inspect diagnostics before retrying. No automatic correction attempted." },
                { "diagnostics", new JObject {
                    { "statsOwnerSame", statsOwnerSame }, { "inventoryOwnerSame", inventoryOwnerSame },
                    { "skinSame", skinSame }, { "skinBefore", skinBefore.ToString() }, { "skinAfter", hero.m_SkinType.ToString() },
                    { "statsSame", statsSame }, { "inventoryAndAppearanceSame", inventoryAndAppearanceSame },
                    { "statsBefore", statsBefore }, { "statsAfter", statsAfter },
                    { "preservedBefore", preservedBefore }, { "expectedAfter", expected }, { "actualAfter", preservedAfter }
                } }
            };
        return new JObject {
            { "ok", true }, { "heroInstanceId", hero.GetInstanceID() }, { "classId", hero.GetDBEntry().m_ID },
            { "provenance", "Test-only direct native Backpack ownership setup in the disposable game copy; each item row and synthetic ID resolved from the loaded package. No live save, production game, or multiplayer state." },
            { "allowNonBlacksmith", allowNonBlacksmith }, { "preservationVerified", true },
            { "before", before }, { "added", added }, { "equipment", EquipmentView(hero) }
        };
    }

    JObject BlacksmithGearEquip(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "heroInstanceId", "items");
        RequireSinglePlayer();
        RequireOutsideCombat();
        CatalogNoLinks(root);
        if (blacksmithAppearance != null) throw new InvalidOperationException("Restore appearance before changing gear.");
        CharacterOverworld hero = ExactBlacksmithHero(Int(command, "heroInstanceId", 0));
        JArray requested = command["items"] as JArray;
        if (requested == null || requested.Count < 6 || requested.Count > 7)
            throw new ArgumentException("Provide one complete 1H-plus-shield set (7 items) or 2H set (6 items).");
        List<BlacksmithGearEntry> entries = new List<BlacksmithGearEntry>();
        HashSet<string> unique = new HashSet<string>(StringComparer.Ordinal);
        HashSet<FTK_itembase.ObjectType> types = new HashSet<FTK_itembase.ObjectType>();
        string tier = null;
        foreach (JToken token in requested)
        {
            if (token == null || token.Type != JTokenType.String) throw new ArgumentException("Each item must be a string ID.");
            string stringId = (string)token;
            if (!unique.Add(stringId)) throw new ArgumentException("Duplicate Blacksmith item ID: " + stringId);
            int separator = stringId.LastIndexOf('_');
            string itemTier = separator < 0 ? string.Empty : stringId.Substring(separator + 1);
            if (tier == null) tier = itemTier;
            else if (!string.Equals(tier, itemTier, StringComparison.Ordinal)) throw new ArgumentException("All items in a test set must use the same tier.");
            BlacksmithGearEntry entry = ResolveBlacksmithGear(stringId);
            if (!types.Add(entry.row.m_ObjectType)) throw new ArgumentException("A set may contain only one item per equipment type.");
            entries.Add(entry);
        }
        foreach (FTK_itembase.ObjectType required in new FTK_itembase.ObjectType[] {
            FTK_itembase.ObjectType.armor, FTK_itembase.ObjectType.boots, FTK_itembase.ObjectType.helmet,
            FTK_itembase.ObjectType.necklace, FTK_itembase.ObjectType.trinket, FTK_itembase.ObjectType.weapon })
            if (!types.Contains(required)) throw new ArgumentException("The test set is missing " + required + ".");
        bool hasShield = types.Contains(FTK_itembase.ObjectType.shield);
        if (types.Count != (hasShield ? 7 : 6)) throw new ArgumentException("Unsupported equipment type in the test set.");
        BlacksmithGearEntry weapon = FindType(entries, FTK_itembase.ObjectType.weapon);
        BlacksmithGearEntry shield = FindType(entries, FTK_itembase.ObjectType.shield);
        if (weapon == null) throw new ArgumentException("One native weapon required.");
        if (hasShield && weapon.row.m_ObjectSlot != FTK_itembase.ObjectSlot.oneHand)
            throw new ArgumentException("A shield can only accompany a native one-hand weapon.");
        if (!hasShield && weapon.row.m_ObjectSlot != FTK_itembase.ObjectSlot.twoHands)
            throw new ArgumentException("A shieldless test set must use a native two-hand weapon.");

        foreach (BlacksmithGearEntry entry in entries)
        {
            PreflightOwnedBlacksmithItem(hero, entry);
        }

        Dictionary<FTK_itembase.ID, int> inventoryBefore = BlacksmithInventoryTotals(hero);
        // Match the game's own ForceEquip route, which swaps occupied equipment and preserves it in Backpack.
        BlacksmithGearEntry[] ordered = new BlacksmithGearEntry[] {
            FindType(entries, FTK_itembase.ObjectType.armor), FindType(entries, FTK_itembase.ObjectType.boots),
            FindType(entries, FTK_itembase.ObjectType.helmet), FindType(entries, FTK_itembase.ObjectType.necklace),
            FindType(entries, FTK_itembase.ObjectType.trinket), weapon, shield
        };
        JArray equipped = new JArray();
        foreach (BlacksmithGearEntry entry in ordered)
        {
            if (entry == null) continue;
            EquipOwnedBlacksmithItem(hero, entry);
            equipped.Add(entry.stringId);
        }
        if (!hasShield)
        {
            ItemContainer leftHand = hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.LeftHand);
            if (leftHand != null && leftHand.m_ItemCounts != null)
                foreach (KeyValuePair<FTK_itembase.ID, int> handItem in leftHand.m_ItemCounts)
                    if (handItem.Value != 0) throw new InvalidOperationException("Native two-handed ForceEquip left a shield in the off hand.");
        }

        RequireBlacksmithInventoryPreserved(inventoryBefore, BlacksmithInventoryTotals(hero));
        CharacterStats stats = hero.m_CharacterStats;
        FTK_weaponStats2 weaponRow = weapon.row as FTK_weaponStats2;
        JObject resultStats = new JObject {
            { "playerLevel", stats.m_PlayerLevel }, { "playerXp", stats.m_PlayerXP },
            { "health", stats.m_HealthCurrent }, { "maxHealth", stats.MaxHealth },
            { "vitalityRaw", stats.GetRawSkillValue(FTK_weaponStats2.SkillType.vitality) },
            { "vitalityEffectiveNoFocus", stats.GetSkillValue(FTK_weaponStats2.SkillType.vitality, false) },
            { "vitalityAugmented", stats.m_AugmentedVitality },
            { "weaponSkill", weaponRow._skilltest.ToString() }, { "weaponDamage", weaponRow._maxdmg },
            { "weaponDamageGainPerLevel", weaponRow._dmggain }, { "weaponHands", weaponRow.m_ObjectSlot.ToString() }
        };
        try { resultStats["totalArmor"] = stats.TotalArmor; resultStats["totalResistance"] = stats.TotalResist; }
        catch (Exception error) { resultStats["defenseTotalsUnavailable"] = error.GetType().Name + ": " + error.Message; }
        MiniHexDungeon dungeon = GameFlow.Instance == null ? null : Field(GameFlow.Instance, "m_DungeonEntered") as MiniHexDungeon;
        JObject dungeonContext = dungeon == null ? null : new JObject {
            { "dungeon", dungeon.m_ID.ToString() }, { "level", dungeon.m_Level }, { "room", dungeon.m_RoomIndex },
            { "levelCount", dungeon.GetLevelCount() }, { "isAtLastLevel", dungeon.IsAtLastLevel() }
        };
        return new JObject {
            { "ok", true }, { "heroInstanceId", hero.GetInstanceID() }, { "classId", hero.GetDBEntry().m_ID },
            { "tier", tier }, { "loadout", hasShield ? "one-hand-and-shield" : "two-hand" },
            { "provenance", "Native CharacterOverworld.ForceEquip in the disposable game copy, with exact package rows and pre/post ownership checks." },
            { "equipped", equipped }, { "stats", resultStats }, { "dungeonContext", dungeonContext },
            { "equipment", EquipmentView(hero) }
        };
    }

    static Dictionary<FTK_itembase.ID, int> BlacksmithInventoryTotals(CharacterOverworld hero)
    {
        Dictionary<FTK_itembase.ID, int> totals = new Dictionary<FTK_itembase.ID, int>();
        foreach (PlayerInventory.ContainerID slot in Enum.GetValues(typeof(PlayerInventory.ContainerID)))
        {
            ItemContainer container = hero.m_PlayerInventory.Get(slot);
            if (container == null || container.m_ItemCounts == null) continue;
            foreach (KeyValuePair<FTK_itembase.ID, int> item in container.m_ItemCounts)
            {
                if (item.Value == 0) continue;
                if (item.Value < 0) throw new InvalidOperationException("Negative native inventory count.");
                int previous;
                totals.TryGetValue(item.Key, out previous);
                totals[item.Key] = checked(previous + item.Value);
            }
        }
        return totals;
    }

    static void RequireBlacksmithInventoryPreserved(Dictionary<FTK_itembase.ID, int> before, Dictionary<FTK_itembase.ID, int> after)
    {
        if (before.Count != after.Count) throw new InvalidOperationException("Native equipment swap changed total item ownership.");
        foreach (KeyValuePair<FTK_itembase.ID, int> item in before)
        {
            int count;
            if (!after.TryGetValue(item.Key, out count) || count != item.Value)
                throw new InvalidOperationException("Native equipment swap changed total item ownership: " + item.Key);
        }
    }

    static void PreflightOwnedBlacksmithItem(CharacterOverworld hero, BlacksmithGearEntry entry)
    {
        int total = OwnedAcrossEquipment(hero, entry.itemId);
        int backpackCount = ExactCount(hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack), entry.itemId);
        int equippedCount = ExactCount(hero.m_PlayerInventory.Get(entry.container), entry.itemId);
        if (total < 1 || equippedCount < 0 || equippedCount > 1 || backpackCount < 0 || backpackCount + equippedCount != total)
            throw new InvalidOperationException("Each exact test item must be owned in Backpack or its native equipment container, with at most one equipped copy: " + entry.stringId);
        entry.ownedCount = total;
    }

    static void EquipOwnedBlacksmithItem(CharacterOverworld hero, BlacksmithGearEntry entry, EquipRpcTrace trace = null)
    {
        // ForceEquip removes one Backpack copy; an already equipped copy needs no swap.
        if (ExactCount(hero.m_PlayerInventory.Get(entry.container), entry.itemId) != 1)
        {
            ObserveEquipRpc(trace, hero, entry, "before-force-equip");
            hero.ForceEquip(entry.itemId, false);
            ObserveEquipRpc(trace, hero, entry, "after-force-equip");
        }
        if (ExactCount(hero.m_PlayerInventory.Get(entry.container), entry.itemId) != 1 ||
            ExactCount(hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack), entry.itemId) != entry.ownedCount - 1 ||
            OwnedAcrossEquipment(hero, entry.itemId) != entry.ownedCount)
            throw new InvalidOperationException("Native ForceEquip did not preserve the expected item multiplicity: " + entry.stringId);
    }

    static BlacksmithGearEntry FindType(List<BlacksmithGearEntry> entries, FTK_itembase.ObjectType type)
    {
        foreach (BlacksmithGearEntry entry in entries) if (entry.row.m_ObjectType == type) return entry;
        return null;
    }
}
