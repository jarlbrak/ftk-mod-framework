using System;
using System.IO;
using System.Collections;
using System.Collections.Generic;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    string packageGearConfigHash;

    void RequirePackageFitIsolation()
    {
        CatalogNoLinks(root);
        if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST") != "1" ||
            Environment.GetEnvironmentVariable("FTK_MODEL_TEST_PACKAGE_ONLY") != "1" ||
            Path.GetFullPath(Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT") ?? "").TrimEnd(Path.DirectorySeparatorChar) != root ||
            !string.Equals(uiStartGame.GetSavePath(), isolatedSavePath, StringComparison.Ordinal) ||
            !string.Equals(isolatedSavePath, Path.Combine(UnityEngine.Application.persistentDataPath, saveNamespace), StringComparison.Ordinal))
            throw new InvalidOperationException("Exact isolated package-only root and test save namespace required.");
        PreviewCoreIdentity();
        RequirePackageFitLoaded();
        RequireSinglePlayer(); RequireOutsideCombat();
    }

    static CharacterOverworld ExactPackageFitHero(int id)
    {
        RequireSinglePlayer(); RequireOutsideCombat();
        CharacterOverworld hero = null;
        if (id == 0 || FTKHub.Instance == null || FTKHub.Instance.m_CharacterOverworlds == null)
            throw new InvalidOperationException("Exact current-party hero required.");
        foreach (CharacterOverworld candidate in FTKHub.Instance.m_CharacterOverworlds)
            if (candidate != null && candidate.GetInstanceID() == id)
            {
                if (hero != null) throw new InvalidOperationException("Duplicate current-party hero.");
                hero = candidate;
            }
        if (hero == null || hero.m_CharacterStats == null || hero.m_PlayerInventory == null ||
            hero.m_CharacterStats.m_HealthCurrent <= 0 || !ReferenceEquals(hero.m_CharacterStats.m_CharacterOverworld, hero) ||
            !SceneOwner(hero) || !hero.gameObject.activeInHierarchy || !hero.IsOwner || hero.m_WaitForRespawn ||
            hero.m_CharacterStats.m_IsInCombat || hero.m_Avatar == null || hero.m_Avatar.m_CharacterOverworld != hero)
            throw new InvalidOperationException("Owned living current-party noncombat hero with reciprocal avatar required.");
        FTK_playerGameStart row = hero.GetDBEntry();
        if (row == null || !ReferenceEquals(row, ExactClassRow(row.m_ID, (int)hero.m_CharacterStats.m_CharacterClass)))
            throw new InvalidOperationException("Exact installed hero class required.");
        foreach (CharacterOverworld member in FTKHub.Instance.m_CharacterOverworlds)
        {
            if (member == null) continue;
            object queue = BlacksmithRead(member, "m_MoveRPCQueue");
            if (member.m_CharacterStats == null || member.m_IsMoving || member.m_CharacterStats.m_IsInCombat ||
                (bool)BlacksmithRead(queue, "m_MoveCoroutineRunning") || ((ICollection)BlacksmithRead(queue, "m_Queue")).Count != 0)
                throw new InvalidOperationException("Every party member must be idle with no queued movement or combat.");
        }
        return hero;
    }

    string PackageFitFile(string relative, string expectedHash)
    {
        if (string.IsNullOrEmpty(relative) || Path.IsPathRooted(relative) || string.IsNullOrEmpty(expectedHash) ||
            !System.Text.RegularExpressions.Regex.IsMatch(expectedHash, "^[a-f0-9]{64}$"))
            throw new ArgumentException("Relative installed package path and SHA256 required.");
        string path = Path.GetFullPath(Path.Combine(root, relative));
        if (!path.StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new ArgumentException("Candidate package must be installed under this exact isolated game root.");
        CatalogNoLinks(path);
        if (!File.Exists(path) || new FileInfo(path).Length > 4 * 1024 * 1024 || CatalogHash(path) != expectedHash)
            throw new InvalidOperationException("Installed candidate file hash differs: " + relative);
        return path;
    }

    Dictionary<string, string> PackageFitItems(string configHash)
    {
        string configPath = Path.Combine(root, "model-test-package-gear.json");
        CatalogNoLinks(configPath);
        if (!File.Exists(configPath) || new FileInfo(configPath).Length > 16384 ||
            string.IsNullOrEmpty(configHash) || CatalogHash(configPath) != configHash ||
            (packageGearConfigHash != null && packageGearConfigHash != configHash))
            throw new InvalidOperationException("Exact unchanged session candidate configuration SHA256 required.");
        JObject config = JObject.Parse(File.ReadAllText(configPath));
        CatalogKeys(config, "packages");
        JArray packages = config["packages"] as JArray;
        if (packages == null || packages.Count != 2) throw new ArgumentException("Exactly the Paladin and Thief candidate packages required.");
        var guids = new HashSet<string>(StringComparer.Ordinal);
        var items = new Dictionary<string, string>(StringComparer.Ordinal);
        foreach (JToken token in packages)
        {
            JObject package = token as JObject;
            if (package == null) throw new ArgumentException("Candidate package object required.");
            CatalogKeys(package, "modGuid", "version", "manifest", "manifestSha256", "content", "contentSha256");
            string guid = Str(package, "modGuid");
            if ((guid != "com.ftkmf.paladin" && guid != "com.ftkmf.thief") || !guids.Add(guid))
                throw new ArgumentException("Distinct approved Paladin and Thief package GUIDs required.");
            string manifestPath = PackageFitFile(Str(package, "manifest"), Str(package, "manifestSha256"));
            string contentPath = PackageFitFile(Str(package, "content"), Str(package, "contentSha256"));
            if (Path.GetFileName(manifestPath) != "manifest.json" || Path.GetFileName(contentPath) != "content.json" ||
                Path.GetDirectoryName(manifestPath) != Path.GetDirectoryName(contentPath))
                throw new ArgumentException("Candidate manifest.json and content.json must share their package directory.");
            JObject manifest = JObject.Parse(File.ReadAllText(manifestPath));
            if (Str(manifest, "modGuid") != guid || string.IsNullOrEmpty(Str(package, "version")) || Str(manifest, "version") != Str(package, "version"))
                throw new InvalidOperationException("Candidate manifest GUID/version differs.");
            JArray entries = JObject.Parse(File.ReadAllText(contentPath))["entries"] as JArray;
            if (entries == null || entries.Count > 2048) throw new ArgumentException("Bounded candidate content entries required.");
            var seen = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject entry in entries)
            {
                string key = Str(entry, "id"), kind = Str(entry, "kind");
                if (string.IsNullOrEmpty(key) || !seen.Add(key)) throw new ArgumentException("Duplicate or missing candidate entry ID.");
                if (kind != "item" && kind != "weapon") continue;
                if (!key.StartsWith(guid == "com.ftkmf.paladin" ? "paladin_" : "thief_", StringComparison.Ordinal) || items.ContainsKey(key))
                    throw new ArgumentException("Candidate item belongs to the wrong package.");
                items.Add(key, guid + ":" + (kind == "weapon" ? "FTK_weaponStats2DB/" : "FTK_itemsDB/") + key);
            }
        }
        packageGearConfigHash = configHash;
        return items;
    }

    static BlacksmithGearEntry ResolvePackageFitGear(string key, Dictionary<string, string> approved)
    {
        string allocationKey;
        if (string.IsNullOrEmpty(key) || !approved.TryGetValue(key, out allocationKey))
            throw new ArgumentException("Exact item ID from the pinned candidate allowlist required.");
        Type registry = CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.ContentRegistry", true);
        Type dbType = allocationKey.IndexOf(":FTK_weaponStats2DB/", StringComparison.Ordinal) >= 0 ? typeof(FTK_weaponStats2DB) : typeof(FTK_itemsDB);
        object[] args = { key, -1, new[] { dbType } };
        if (!(bool)registry.GetMethod("TryGetSyntheticId", Statics).Invoke(null, args)) throw new InvalidOperationException("Candidate item not registered.");
        int id = (int)args[1];
        Type allocator = registry.Assembly.GetType("FTKModFramework.Core.IdAllocator", true);
        IDictionary allocations = (IDictionary)allocator.GetField("KeyToInt", Statics).GetValue(null);
        IDictionary owners = (IDictionary)allocator.GetField("IntToKey", Statics).GetValue(null);
        if (!allocations.Contains(allocationKey) || (int)allocations[allocationKey] != id || !owners.Contains(id) || (string)owners[id] != allocationKey)
            throw new InvalidOperationException("Candidate synthetic item is not owned by the pinned package allocation.");
        FTK_itembase row = FTK_itembase.GetItemBase((FTK_itembase.ID)id);
        FTK_itembase exact = dbType == typeof(FTK_weaponStats2DB) ? (FTK_itembase)FTK_weaponStats2DB.GetDB().GetEntryByInt(id) : FTK_itemsDB.GetDB().GetEntryByInt(id);
        if (row == null || row.m_ID != key || !row.m_Equippable || !ReferenceEquals(row, exact))
            throw new InvalidOperationException("Candidate registered ID and equippable row identity differ.");
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
            default: throw new ArgumentException("Unsupported candidate equipment type.");
        }
        return new BlacksmithGearEntry { stringId = key, numericId = id, itemId = (FTK_itembase.ID)id, row = row, container = slot };
    }

    static JObject PackageFitStableStats(JObject scalars)
    {
        JObject result = new JObject();
        // These are the scalar outputs of native TallyCharacterMods/Defense/Health.
        // m_ModQuickness is a property whose serialized backing field is _ModQuickness.
        // Every other scalar, including permanent augments and progression, must survive.
        var derived = new HashSet<string>(new[] { "m_ReflectDamage", "m_EnemyTargetOverride", "m_MaxHealthOverride",
            "m_MaxActionsOverride", "m_ImmuneAmbush", "m_BaseMaxHealth", "m_HealthCurrent", "m_BaseDefensePhysical",
            "m_BaseDefenseMagic", "m_BaseEvadeRating", "m_PartyCombatArmor", "m_PartyCombatResist", "m_PartyCombatEvade", "_ModQuickness" });
        foreach (JProperty field in scalars.Properties())
            if (!field.Name.StartsWith("m_Mod", StringComparison.Ordinal) && !derived.Contains(field.Name))
                result[field.Name] = field.Value.DeepClone();
        return result;
    }

    JObject PackageGearFixture(JObject command, bool equip)
    {
        CatalogKeys(command, "id", "session", "op", "heroInstanceId", "classKey", "classId", "items", "configSha256");
        RequirePackageFitIsolation();
        if (blacksmithAppearance != null || previewRace != null) throw new InvalidOperationException("Restore and settle every appearance fixture before changing equipment.");
        CharacterOverworld hero = ExactPackageFitHero(LeaseObservationPin.ExactId(command, "heroInstanceId", false));
        if (!ReferenceEquals(hero.GetDBEntry(), ExactClassRow(Str(command, "classKey"), PreviewRaceIndex(command, "classId"))))
            throw new InvalidOperationException("Requested hero class differs.");
        if (uiPlayerInventory.Instance != null && uiPlayerInventory.Instance.m_IsShowing)
            throw new InvalidOperationException("Close native inventory before package gear setup.");
        Dictionary<string, string> approved = PackageFitItems(Str(command, "configSha256"));
        JArray requested = command["items"] as JArray;
        if (requested == null || requested.Count < 1 || requested.Count > (equip ? 7 : 64)) throw new ArgumentException("Bounded nonempty exact candidate item list required.");
        var unique = new HashSet<string>(StringComparer.Ordinal);
        var slots = new HashSet<PlayerInventory.ContainerID>();
        var entries = new List<BlacksmithGearEntry>();
        ItemContainer backpack = hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Backpack);
        if (backpack == null) throw new InvalidOperationException("Native Backpack required.");
        foreach (JToken token in requested)
        {
            if (token.Type != JTokenType.String || !unique.Add((string)token)) throw new ArgumentException("Distinct exact item strings required.");
            BlacksmithGearEntry entry = ResolvePackageFitGear((string)token, approved);
            ItemContainer target = hero.m_PlayerInventory.Get(entry.container);
            int owned = OwnedAcrossEquipment(hero, entry.itemId);
            if (target == null || owned < 0 || owned > 1 || (owned == 1 && ExactCount(backpack, entry.itemId) + ExactCount(target, entry.itemId) != 1))
                throw new InvalidOperationException("Each requested item must have zero or one unambiguous owned instance.");
            if (equip)
            {
                if (!slots.Add(entry.container)) throw new ArgumentException("Only one requested item per equipment slot.");
                PreflightOwnedBlacksmithItem(hero, entry);
            }
            else if (owned == 0 && !backpack.CanAdd(entry.itemId)) throw new InvalidOperationException("Backpack cannot add candidate item.");
            entries.Add(entry);
        }
        BlacksmithGearEntry weapon = FindType(entries, FTK_itembase.ObjectType.weapon);
        BlacksmithGearEntry shield = FindType(entries, FTK_itembase.ObjectType.shield);
        if (equip)
        {
            FTK_itembase hand = weapon == null ? FTK_itembase.GetItemBase(hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.RightHand).GetOne()) : weapon.row;
            if (weapon != null && weapon.row.m_ObjectSlot != FTK_itembase.ObjectSlot.oneHand && weapon.row.m_ObjectSlot != FTK_itembase.ObjectSlot.twoHands)
                throw new ArgumentException("Supported one-hand or two-hand weapon required.");
            if (shield != null && (hand == null || hand.m_ObjectSlot != FTK_itembase.ObjectSlot.oneHand))
                throw new ArgumentException("Shield requires an equipped or requested one-hand weapon.");
            if (weapon != null && weapon.row.m_ObjectSlot == FTK_itembase.ObjectSlot.twoHands) slots.Add(PlayerInventory.ContainerID.LeftHand);
            foreach (PlayerInventory.ContainerID slot in slots)
            {
                int occupied = 0;
                foreach (KeyValuePair<FTK_itembase.ID, int> old in hero.m_PlayerInventory.Get(slot).m_CountDictionary)
                {
                    if (old.Value == 0) continue;
                    if (old.Value != 1 || ++occupied > 1 || !backpack.CanAdd(old.Key))
                        throw new InvalidOperationException("Displaced equipment must fit natively in Backpack.");
                }
            }
        }
        JObject before = BlacksmithAppearancePreserved(hero);
        JObject statsBefore = BlacksmithAppearanceScalars(hero.m_CharacterStats);
        JObject expected = BlacksmithAppearancePreserved(hero);
        CharacterStats statsOwner = hero.m_CharacterStats;
        PlayerInventory inventoryOwner = hero.m_PlayerInventory;
        FTK_playerGameStart.SkinType skin = hero.m_SkinType;
        Dictionary<FTK_itembase.ID, int> totals = BlacksmithInventoryTotals(hero);
        JArray changed = new JArray();
        PackageFitBeginMutation(hero, equip ? "package-gear-equip" : "package-gear-grant");
        foreach (BlacksmithGearEntry entry in entries)
        {
            if (equip && entry == shield) continue;
            if (equip) EquipOwnedBlacksmithItem(hero, entry, CreateEquipRpcTrace(command, changed.Count));
            else if (OwnedAcrossEquipment(hero, entry.itemId) == 0)
            {
                if (!backpack.Add(entry.itemId) || ExactCount(backpack, entry.itemId) != 1) throw new InvalidOperationException("Native Add failed; reobserve before retrying.");
                ((JObject)expected["inventory"][PlayerInventory.ContainerID.Backpack.ToString()])[entry.numericId.ToString(System.Globalization.CultureInfo.InvariantCulture)] = 1;
            }
            changed.Add(entry.stringId);
        }
        if (equip && shield != null) { EquipOwnedBlacksmithItem(hero, shield, CreateEquipRpcTrace(command, changed.Count)); changed.Add(shield.stringId); }
        JObject after = BlacksmithAppearancePreserved(hero);
        JObject statsAfter = BlacksmithAppearanceScalars(hero.m_CharacterStats);
        if (equip)
        {
            RequireBlacksmithInventoryPreserved(totals, BlacksmithInventoryTotals(hero));
            // Keep every untouched slot identical, in addition to total ownership.
            foreach (JProperty slot in ((JObject)expected["inventory"]).Properties())
                if (slot.Name != PlayerInventory.ContainerID.Backpack.ToString() &&
                    !slots.Contains((PlayerInventory.ContainerID)Enum.Parse(typeof(PlayerInventory.ContainerID), slot.Name)))
                    if (!BlacksmithGrantPreservedEquals(slot.Value, after["inventory"][slot.Name]))
                        throw new InvalidOperationException("Native swap changed an unrelated inventory slot.");
            expected.Remove("inventory"); after.Remove("inventory");
        }
        bool statsOwnerSame = ReferenceEquals(statsOwner, hero.m_CharacterStats);
        bool inventoryOwnerSame = ReferenceEquals(inventoryOwner, hero.m_PlayerInventory);
        bool skinSame = skin == hero.m_SkinType;
        bool stateSame = BlacksmithGrantPreservedEquals(expected, after);
        JObject checkedStatsBefore = equip ? PackageFitStableStats(statsBefore) : statsBefore;
        JObject checkedStatsAfter = equip ? PackageFitStableStats(statsAfter) : statsAfter;
        bool statsSame = BlacksmithGrantPreservedEquals(checkedStatsBefore, checkedStatsAfter);
        packageFitMutationDiagnostics = new JObject {
            { "statsOwnerSame", statsOwnerSame }, { "inventoryOwnerSame", inventoryOwnerSame }, { "skinSame", skinSame },
            { "preservedStateSame", stateSame }, { "checkedStatsSame", statsSame },
            { "expectedState", expected }, { "actualState", after }, { "statsBefore", statsBefore }, { "statsAfter", statsAfter },
            { "checkedStatDifferences", PackageFitDifferences(checkedStatsBefore, checkedStatsAfter) },
            { "stateDifferences", PackageFitDifferences(expected, after) },
            { "allStatDifferences", PackageFitDifferences(statsBefore, statsAfter) }
        };
        bool preserved = statsOwnerSame && inventoryOwnerSame && skinSame && stateSame && statsSame;
        if (!preserved) throw new InvalidOperationException("Native setup changed preserved state; session remains uncertain.");
        packageFitMutationVerified = true;
        return new JObject { { "ok", preserved }, { "session", sessionId }, { "heroInstanceId", hero.GetInstanceID() },
            { "classKey", hero.GetDBEntry().m_ID }, { "classId", (int)hero.m_CharacterStats.m_CharacterClass },
            { "configSha256", packageGearConfigHash }, { "items", changed }, { "preservationVerified", preserved },
            { "diagnostics", packageFitMutationDiagnostics }, { "before", before }, { "after", after }, { "statsBefore", statsBefore }, { "statsAfter", statsAfter },
            { "equipment", EquipmentView(hero) }, { "celInstanceId", hero.m_Avatar.GetInstanceID() },
            { "error", preserved ? null : "Native setup changed preserved state. Reobserve; no automatic correction attempted." },
            { "scope", "Isolated direct native package gear setup only. Equip applies native equipment modifiers. Wait for avatar settling; no acquisition, motion, save or multiplayer evidence." } };
    }
}
