using System;
using System.Collections;
using System.Reflection;
using System.IO;
using System.Security.Cryptography;
using GridEditor;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    // Read fields directly: even Blacksmith State query methods can allocate actor records.
    static object BlacksmithRead(object target, string name)
    {
        return RuntimeFieldRead.Read(target, name);
    }
    static JObject BlacksmithFields(object target, params string[] names)
    {
        JObject result = new JObject();
        if (target != null) foreach (string name in names)
        {
            object value = BlacksmithRead(target, name);
            result[name] = value == null ? new JValue((object)null) : JToken.FromObject(value);
        }
        return result;
    }
    static JArray BlacksmithReceipts(object state, string name)
    {
        JArray result = new JArray();
        foreach (object value in (IEnumerable)BlacksmithRead(state, name)) result.Add(value.ToString());
        return result;
    }
    static JObject BlacksmithBinary(Assembly assembly)
    {
        string hash;
        using (SHA256 sha = SHA256.Create())
        using (Stream stream = File.OpenRead(assembly.Location))
            hash = BitConverter.ToString(sha.ComputeHash(stream)).Replace("-", "").ToLowerInvariant();
        return new JObject {{"name", assembly.FullName}, {"mvid", assembly.ManifestModule.ModuleVersionId.ToString()},
            {"path", assembly.Location}, {"sha256", hash}};
    }
    static JObject BlacksmithGrant(IDictionary equipment, IDictionary actions, int item)
    {
        if (!equipment.Contains(item)) return null;
        JObject result = BlacksmithFields(equipment[item], "SetHammerArmor", "OverhandArmorPenalty", "TemperArmor");
        result["itemId"] = item;
        result["proficiencyId"] = actions.Contains(item) ? Convert.ToInt32(actions[item]) : -1;
        return result;
    }
    JObject BlacksmithCombatObservation(JObject command)
    {
        foreach (JProperty property in command.Properties())
            if (property.Name != "id" && property.Name != "session" && property.Name != "op")
                throw new ArgumentException("blacksmith-combat-state accepts only the command envelope.");
        RequireSinglePlayer();
        EncounterSession encounter = EncounterSession.Instance;
        if (encounter == null || FTKHub.Instance == null) throw new InvalidOperationException("Active native party and encounter session required.");
        Assembly framework = CatalogAssembly("FTKModFramework");
        Type runtime = framework.GetType("FTKModFramework.Core.BlacksmithRuntime", true);
        object state = BlacksmithRead(runtime, "State");
        IDictionary actors = (IDictionary)BlacksmithRead(state, "actors");
        IDictionary turns = (IDictionary)BlacksmithRead(state, "currentTurns");
        IDictionary equipment = (IDictionary)BlacksmithRead(runtime, "equipment");
        IDictionary actions = (IDictionary)BlacksmithRead(runtime, "itemActions");
        JArray heroes = new JArray(), timeline = new JArray(), storedActors = new JArray();
        foreach (DictionaryEntry entry in actors)
        {
            JObject record = BlacksmithFields(entry.Value, "SetHammer", "SetSource", "Penalty", "Temper", "TemperTurns", "TemperAppliedDuring", "TemperUsed");
            record["identity"] = entry.Key.ToString(); storedActors.Add(record);
        }
        foreach (CharacterOverworld hero in FTKHub.Instance.m_CharacterOverworlds)
        {
            if (hero == null || hero.m_CharacterStats == null) continue;
            CharacterStats stats = hero.m_CharacterStats;
            CharacterDummy dummy = hero.m_CurrentDummy;
            string identity = GuardianObservedFid(hero.m_FTKPlayerID);
            object actor = actors.Contains(identity) ? actors[identity] : null;
            bool used = actor != null && (bool)BlacksmithRead(actor, "TemperUsed");
            int set = actor == null ? 0 : (int)BlacksmithRead(actor, "SetHammer");
            int temper = actor == null ? 0 : (int)BlacksmithRead(actor, "Temper");
            int penalty = actor == null ? 0 : (int)BlacksmithRead(actor, "Penalty");
            JArray trinkets = new JArray(); int temperAmount = 0;
            if (hero.m_PlayerInventory != null)
                foreach (System.Collections.Generic.KeyValuePair<FTK_itembase.ID, int> item in hero.m_PlayerInventory.Get(PlayerInventory.ContainerID.Trinket).m_CountDictionary)
                {
                    JObject grant = BlacksmithGrant(equipment, actions, (int)item.Key);
                    trinkets.Add(new JObject {{"itemId", (int)item.Key}, {"count", item.Value}, {"grant", grant}});
                    if (item.Value > 0 && grant != null) temperAmount = Math.Max(temperAmount, (int)grant["TemperArmor"]);
                }
            int nativeMod = dummy == null ? 0 : dummy.m_TauntArmor;
            CharacterDummy.ProficiencyRecord armorRecord;
            if (dummy != null && dummy.m_SufferingProficiencies.TryGetValue(ProficiencyBase.Category.Armor, out armorRecord))
                nativeMod += (int)armorRecord.m_Proficiency.m_CustomValue;
            JObject defense = BlacksmithFields(stats, "m_BaseDefensePhysical", "m_ModDefensePhysical", "m_AugmentedDefensePhysical");
            int raw = (int)defense["m_BaseDefensePhysical"] + (int)defense["m_ModDefensePhysical"] + (int)defense["m_AugmentedDefensePhysical"] + encounter.m_TotalPartyArmor;
            int contribution = (bool)BlacksmithRead(runtime, "encounterActive") ? Math.Max(set, temper) - penalty : 0;
            defense["partyArmor"] = encounter.m_TotalPartyArmor; defense["nativeCombatArmorMod"] = nativeMod;
            defense["storedBlacksmithContribution"] = contribution;
            defense["preDiseaseArmorFromStoredFields"] = Math.Max(0, raw + (stats.m_IsInCombat ? nativeMod + contribution : 0));
            defense["nativeTotalArmor"] = new JValue((object)null);
            defense["limitation"] = "Native TotalArmor invokes mutating Blacksmith observation; subtotal excludes disease and other ArmorMod patches.";
            bool nativeClass = stats.m_CharacterClass == FTK_playerGameStart.ID.blacksmith;
            heroes.Add(new JObject {{"heroInstanceId", hero.GetInstanceID()}, {"identity", identity},
                {"classId", (int)stats.m_CharacterClass}, {"nativeBlacksmith", nativeClass},
                {"dummyInstanceId", dummy == null ? 0 : dummy.GetInstanceID()},
                {"dummyMatchesParty", dummy != null && ReferenceEquals(dummy.m_CharacterOverworld, hero)},
                {"hp", stats.m_HealthCurrent}, {"focus", stats.m_FocusPoints}, {"spentFocus", stats.SpentFocus},
                {"inCombat", stats.m_IsInCombat}, {"isMyTurn", stats.m_IsMyTurn}, {"isCombatTurn", stats.m_IsCombatTurn},
                {"owner", hero.IsOwner}, {"defense", defense}, {"nativeTotalResistance", stats.TotalResist},
                {"dummy", dummy == null ? null : new JObject {{"alive", dummy.m_IsAlive}, {"fled", dummy.m_DidFlee},
                    {"combatFinished", dummy.m_CombatFinished}, {"stunned", dummy.Stunned}, {"petrified", dummy.Petrified}}},
                {"currentStoredTurn", turns.Contains(identity) ? turns[identity].ToString() : null},
                {"weaponId", (int)hero.m_WeaponID}, {"shieldId", (int)hero.m_ShieldID},
                {"weaponGrant", BlacksmithGrant(equipment, actions, (int)hero.m_WeaponID)}, {"trinkets", trinkets},
                {"temperUsed", used}, {"temperBudgetAvailable", !used}, {"temperEquippedAmount", temperAmount},
                {"temperClassEquipmentBudgetEligible", nativeClass && temperAmount > 0 && !used}});
        }
        if (encounter.m_FightOrderVisual != null) foreach (EncounterSessionMC.FightOrderEntry entry in encounter.m_FightOrderVisual)
            timeline.Add(new JObject {{"entryId", entry.m_EntryID}, {"identity", GuardianObservedFid(entry.m_Pid)}, {"tta", entry.m_TTA}});
        return new JObject {{"ok", true}, {"operation", "blacksmith-combat-state"}, {"readOnly", true}, {"session", sessionId},
            {"contentRegistrationRun", CurrentContentRegistrationRun()}, {"encounterInstanceId", encounter.GetInstanceID()},
            {"encounterIndex", encounter.m_EncounterIndex}, {"currentPlayer", GuardianObservedFid(encounter.m_CurrentPlayer)},
            {"runtime", BlacksmithFields(runtime, "encounterActive", "encounterIndex")},
            {"runtimeSessionMatches", ReferenceEquals(BlacksmithRead(runtime, "encounterSession"), encounter)},
            {"binaries", new JObject {{"framework", BlacksmithBinary(framework)}, {"helper", BlacksmithBinary(typeof(RuntimeModelTest).Assembly)},
                {"game", BlacksmithBinary(typeof(CharacterStats).Assembly)}}},
            {"heroes", heroes}, {"storedActors", storedActors}, {"timeline", timeline},
            {"commits", BlacksmithReceipts(state, "commits")}, {"turnStarts", BlacksmithReceipts(state, "starts")}, {"turnEnds", BlacksmithReceipts(state, "ends")}};
    }
}
