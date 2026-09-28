using System;
using System.Collections.Generic;
using System.Reflection;
using GridEditor;
using HarmonyLib;
using Newtonsoft.Json.Linq;

// Test-only accelerated XP setup for the task-owned disposable game copy.
// Every level is reached through the installed game's native XP and Update path.
public sealed partial class RuntimeModelTest
{
    static bool endgameLevelStatisticGuardInstalled;
    static int endgameLevelToSuppress = -1;
    static int endgameLevelStatisticSuppressions;

    static void InstallEndgameLevelStatisticGuard()
    {
        Type owner = typeof(CharacterStats).Assembly.GetType("StatsAchievements.StatsAchievements", true);
        MethodInfo target = owner.GetMethod("TryPlayerStatisticSetValue", Statics, null,
            new Type[] { typeof(FTK_statistic.ID), typeof(int) }, null);
        if (target == null) throw new MissingMethodException(owner.FullName, "TryPlayerStatisticSetValue(FTK_statistic.ID, int)");
        Harmony harmony = new Harmony("com.ftkmf.runtime-model-test.endgame-level-stat-guard");
        harmony.Patch(target, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("EndgameLevelStatisticPrefix", Statics)));
        endgameLevelStatisticGuardInstalled = true;
    }

    static bool EndgameLevelStatisticPrefix(FTK_statistic.ID pId, int pValue)
    {
        if (endgameLevelToSuppress >= 0 && endgameLevelToSuppress == pValue && pId == FTK_statistic.ID.STAT_CHARACTER_LEVEL)
        {
            endgameLevelStatisticSuppressions++;
            return false;
        }
        return true;
    }

    static int LevelForNativeXp(int xp, int[] thresholds)
    {
        for (int level = 0; level < thresholds.Length; level++)
            if (xp < thresholds[level]) return level;
        return 0;
    }

    static JObject EndgameHeroState(CharacterOverworld hero)
    {
        CharacterStats stats = hero.m_CharacterStats;
        FTK_weaponStats2.SkillType vitality = FTK_weaponStats2.SkillType.vitality;
        JObject result = new JObject {
            { "heroInstanceId", hero.GetInstanceID() },
            { "classId", hero.GetDBEntry() == null ? null : hero.GetDBEntry().m_ID },
            { "playerLevel", stats.m_PlayerLevel },
            { "playerXp", stats.m_PlayerXP },
            { "health", stats.m_HealthCurrent },
            { "maxHealth", stats.MaxHealth },
            { "focusPoints", stats.m_FocusPoints },
            { "spentFocus", stats.SpentFocus },
            { "vitalityRaw", stats.GetRawSkillValue(vitality) },
            { "vitalityEffectiveNoFocus", stats.GetSkillValue(vitality, false) }
        };
        try { result["totalArmor"] = stats.TotalArmor; result["totalResistance"] = stats.TotalResist; }
        catch (Exception error) { result["defenseTotalsUnavailable"] = error.GetType().Name + ": " + error.Message; }
        try { result["weaponMaxDamage"] = stats.GetWeaponMaxDamage(); }
        catch (Exception error) { result["weaponDamageUnavailable"] = error.GetType().Name + ": " + error.Message; }
        return result;
    }

    JObject BlacksmithEndgameLevelUp(JObject command)
    {
        JArray heroes = new JArray();
        JArray milestones = new JArray();
        List<CharacterOverworld> party = new List<CharacterOverworld>();
        string failure = null;
        int maxLevel = 0;
        int targetLevel = 0;
        int[] thresholds = null;
        bool preflightComplete = false;
        int suppressionsBefore = endgameLevelStatisticSuppressions;
        try
        {
            CatalogKeys(command, "id", "session", "op", "targetLevel");
            CatalogNoLinks(root);
            RequireSinglePlayer();
            RequireOutsideCombat();
            if (!endgameLevelStatisticGuardInstalled)
                throw new InvalidOperationException("Exact native player-level statistic guard did not install; refusing XP setup.");
            if (FTKHub.Instance == null || FTKHub.Instance.m_CharacterOverworlds == null || GameFlow.Instance == null)
                throw new InvalidOperationException("Active overworld party and native GameFlow are required.");

            maxLevel = GameFlow.Instance.m_MaxCharacterLevels;
            thresholds = GameFlow.Instance.m_LevelXpValues;
            if (maxLevel < 1 || maxLevel >= thresholds.Length || maxLevel > 20)
                throw new InvalidOperationException("Native character-level cap or XP thresholds are unavailable or outside the fixture limit.");
            JToken requestedLevel = command["targetLevel"];
            if (requestedLevel != null && requestedLevel.Type != JTokenType.Integer)
                throw new ArgumentException("targetLevel must be a native integer level.");
            targetLevel = requestedLevel == null ? maxLevel : (int)requestedLevel;
            if (targetLevel < 0 || targetLevel > maxLevel)
                throw new ArgumentException("targetLevel must be between zero and the native level cap.");
            for (int index = 1; index < thresholds.Length; index++)
                if (thresholds[index] <= thresholds[index - 1])
                    throw new InvalidOperationException("Native character XP thresholds are not strictly increasing.");

            MethodInfo nativeUpdate = typeof(CharacterStats).GetMethod("Update", Members, null, Type.EmptyTypes, null);
            FieldInfo xpChanged = typeof(CharacterStats).GetField("m_XPChanged", Members);
            FieldInfo goldChanged = typeof(CharacterStats).GetField("m_GoldChanged", Members);
            if (nativeUpdate == null || nativeUpdate.ReturnType != typeof(void) || xpChanged == null || xpChanged.FieldType != typeof(bool)
                || goldChanged == null || goldChanged.FieldType != typeof(bool))
                throw new MissingMethodException("Exact installed CharacterStats.Update and change flags are required.");
            HashSet<int> instanceIds = new HashSet<int>();
            int blacksmithCount = 0;
            foreach (CharacterOverworld hero in FTKHub.Instance.m_CharacterOverworlds)
            {
                if (hero == null) continue;
                if (!instanceIds.Add(hero.GetInstanceID())) throw new InvalidOperationException("Duplicate current-party hero instance.");
                if (hero.m_CharacterStats == null || hero.m_CharacterStats.m_HealthCurrent <= 0 || hero.GetDBEntry() == null)
                    throw new InvalidOperationException("All current-party heroes must be alive and have native stats and class rows.");
                CharacterStats stats = hero.m_CharacterStats;
                if (stats.m_PlayerLevel < 0 || stats.m_PlayerLevel > maxLevel || stats.m_PlayerXP < 0 || stats.m_PlayerXP >= thresholds[maxLevel])
                    throw new InvalidOperationException("Current hero level or XP is outside native thresholds.");
                if (stats.m_PlayerLevel > targetLevel)
                    throw new InvalidOperationException("Level setup cannot lower an existing hero; use a fresh isolated run.");
                if (stats.m_PlayerLevel != LevelForNativeXp(stats.m_PlayerXP, thresholds))
                    throw new InvalidOperationException("Hero has pending or inconsistent native XP level progression; retry after the game updates.");
                if ((bool)xpChanged.GetValue(stats) || (bool)goldChanged.GetValue(stats))
                    throw new InvalidOperationException("Hero has another native stats update pending; refusing accelerated progression.");
                if (hero.GetDBEntry().m_ID == "blacksmith") blacksmithCount++;
                party.Add(hero);
                heroes.Add(new JObject { { "before", EndgameHeroState(hero) }, { "after", null } });
            }
            if (party.Count != 3 || blacksmithCount != 1)
                throw new InvalidOperationException("Exactly three living party members, including one Blacksmith, are required.");
            preflightComplete = true;

            for (int heroIndex = 0; heroIndex < party.Count; heroIndex++)
            {
                CharacterOverworld hero = party[heroIndex];
                CharacterStats stats = hero.m_CharacterStats;
                JArray heroMilestones = new JArray();
                for (int nextLevel = stats.m_PlayerLevel + 1; nextLevel <= targetLevel; nextLevel++)
                {
                    int targetXp = nextLevel == maxLevel ? thresholds[maxLevel] - 1 : thresholds[nextLevel - 1];
                    int xpDelta = checked(targetXp - stats.m_PlayerXP);
                    if (xpDelta <= 0) throw new InvalidOperationException("Native level-up target XP must exceed the current XP.");
                    int previousLevel = stats.m_PlayerLevel;
                    int levelSuppressionsBefore = endgameLevelStatisticSuppressions;
                    stats.UpdateXP(xpDelta, true);
                    if (!(bool)xpChanged.GetValue(stats))
                        throw new InvalidOperationException("Native UpdateXP did not mark XP for the game level-up path.");
                    endgameLevelToSuppress = nextLevel;
                    try { nativeUpdate.Invoke(stats, null); }
                    catch (TargetInvocationException error)
                    {
                        throw new InvalidOperationException("Installed CharacterStats.Update failed at level " + nextLevel + ".",
                            error.InnerException == null ? error : error.InnerException);
                    }
                    finally { endgameLevelToSuppress = -1; }
                    if (stats.m_PlayerLevel != nextLevel || stats.m_PlayerXP != targetXp)
                        throw new InvalidOperationException("Native CharacterStats.Update did not process the expected exact XP threshold for hero " + hero.GetInstanceID() + ".");
                    if ((bool)xpChanged.GetValue(stats) || endgameLevelStatisticSuppressions != levelSuppressionsBefore + 1)
                        throw new InvalidOperationException("Expected the exact native character-level statistic write to be suppressed once per level.");
                    JObject milestone = new JObject {
                        { "heroInstanceId", hero.GetInstanceID() },
                        { "fromLevel", previousLevel }, { "toLevel", stats.m_PlayerLevel },
                        { "xp", stats.m_PlayerXP }, { "nativeThreshold", targetXp }
                    };
                    heroMilestones.Add(milestone);
                    milestones.Add((JObject)milestone.DeepClone());
                }
                ((JObject)heroes[heroIndex])["after"] = EndgameHeroState(hero);
                ((JObject)heroes[heroIndex])["nativeLevelUps"] = heroMilestones;
            }
        }
        catch (Exception error) { failure = error.ToString(); }
        finally
        {
            endgameLevelToSuppress = -1;
            for (int index = 0; index < party.Count && index < heroes.Count; index++)
            {
                JObject record = heroes[index] as JObject;
                if (record != null) record["after"] = EndgameHeroState(party[index]);
            }
        }
        bool completed = failure == null && preflightComplete && party.Count == 3
            && endgameLevelStatisticSuppressions - suppressionsBefore == milestones.Count;
        JObject result = new JObject {
            { "ok", completed },
            { "status", completed ? "native-level-test-setup-complete" : "native-level-test-setup-failed-or-partial" },
            { "provenance", "Disposable single-player test setup. XP was accelerated through installed CharacterStats.UpdateXP; each intervening level then ran through the exact installed CharacterStats.Update method on Unity's main thread. Native health, defense, focus and level calculations ran. This is not natural campaign progression." },
            { "nativeMaximumCharacterLevel", maxLevel },
            { "requestedTargetLevel", targetLevel },
            { "levelXpThresholds", thresholds == null ? null : new JArray(thresholds) },
            { "heroes", heroes },
            { "nativeLevelUpMilestones", milestones },
            { "suppressedPlayerLevelStatisticWrites", endgameLevelStatisticSuppressions - suppressionsBefore },
            { "completed", completed }
        };
        if (failure != null) result["error"] = failure;
        else if (!completed) result["error"] = "Preflight or exact native level-up count did not match the expected three-hero target-level setup.";
        return result;
    }
}
