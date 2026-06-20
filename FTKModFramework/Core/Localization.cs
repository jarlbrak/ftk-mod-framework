using System;
using System.Collections.Generic;
using System.Reflection;
using GridEditor;
using Google2u;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>
    /// Gives custom content real display names.
    ///
    /// The game resolves names from Google2u text tables: FTK_itembase.GetLocalizedName() returns
    /// Localized&lt;TextItems&gt;("STR_" + m_ID), and FTK_proficiencyTable.GetLocalizedDisplayName()
    /// returns Localized&lt;TextMisc&gt;(m_DisplayName). Our custom string IDs aren't in those tables,
    /// so we postfix the lookups and substitute the name registered for that row's m_ID.
    /// </summary>
    public static class Localization
    {
        // keyed by the row's string m_ID
        private static readonly Dictionary<string, string> Names = new Dictionary<string, string>();

        public static void SetName(string contentId, string displayName)
        {
            Names[contentId] = displayName;
        }

        public static bool TryGetName(string contentId, out string displayName)
        {
            if (contentId == null) { displayName = null; return false; }
            return Names.TryGetValue(contentId, out displayName);
        }

        // Synthetic-realm display names. A custom realm has no enum name, so the game renders its in-world
        // name from the raw text key "STR_<int>Display" (HexLand.GetRealmDisplayValue builds exactly that,
        // and QuestLogicBase wraps it in rich text for quest UI). Those keys are not in any Google2u table,
        // so the generic Localized<TextLore> passes them through verbatim. We CANNOT patch that generic
        // method (instantiating a generic patch corrupts Mono's shared generic code body and blanks every
        // text table), so instead we substitute the rendered key at the CONCRETE callers (the two patches
        // below) by full-key string replacement, which also handles the rich-text-wrapped quest-param form
        // "<color=#xxxxxx>STR_<int>Display</color>".
        private static readonly Dictionary<string, string> RealmDisplayKeys = new Dictionary<string, string>();

        public static void SetRealmName(int realmInt, string displayName)
        {
            RealmDisplayKeys["STR_" + realmInt + "Display"] = displayName;
        }

        /// <summary>
        /// Substitute any registered "STR_&lt;int&gt;Display" realm key found inside <paramref name="s"/> with its
        /// display name. Uses substring Replace (NOT equality), because quest params arrive rich-text-wrapped.
        /// Null-safe and a no-op when no realm names are registered; logs once per actual substitution.
        /// </summary>
        public static string ApplyRealmKeyReplacements(string s)
        {
            if (s == null || RealmDisplayKeys.Count == 0) return s;
            foreach (KeyValuePair<string, string> kvp in RealmDisplayKeys)
            {
                if (s.Contains(kvp.Key))
                {
                    s = s.Replace(kvp.Key, kvp.Value);
                    Plugin.Log.LogInfo("[realm-name] realm key -> " + kvp.Value);
                }
            }
            return s;
        }

        // Class flavor/description text (the character-select panel reads it inline, not via a method).
        private static readonly Dictionary<string, string> ClassFlavors = new Dictionary<string, string>();

        public static void SetClassFlavor(string classId, string flavor)
        {
            ClassFlavors[classId] = flavor;
        }

        public static bool TryGetClassFlavor(string classId, out string flavor)
        {
            if (classId == null) { flavor = null; return false; }
            return ClassFlavors.TryGetValue(classId, out flavor);
        }

        // Proficiency description (the combat tooltip's effect line, normally derived from the category).
        private static readonly Dictionary<string, string> ProficiencyDescriptions = new Dictionary<string, string>();

        public static void SetProficiencyDescription(string proficiencyId, string description)
        {
            ProficiencyDescriptions[proficiencyId] = description;
        }

        public static bool TryGetProficiencyDescription(string proficiencyId, out string description)
        {
            if (proficiencyId == null) { description = null; return false; }
            return ProficiencyDescriptions.TryGetValue(proficiencyId, out description);
        }

        // Enemy description (the bestiary / inspect text). The display NAME reuses the shared Names map.
        private static readonly Dictionary<string, string> EnemyDescriptions = new Dictionary<string, string>();

        public static void SetEnemyDescription(string enemyId, string description)
        {
            EnemyDescriptions[enemyId] = description;
        }

        public static bool TryGetEnemyDescription(string enemyId, out string description)
        {
            if (enemyId == null) { description = null; return false; }
            return EnemyDescriptions.TryGetValue(enemyId, out description);
        }
    }

    [HarmonyPatch(typeof(FTK_itembase), "GetLocalizedName")]
    internal static class ItemName_Patch
    {
        private static void Postfix(FTK_itembase __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    [HarmonyPatch(typeof(FTK_proficiencyTable), "GetLocalizedDisplayName")]
    internal static class ProficiencyName_Patch
    {
        private static void Postfix(FTK_proficiencyTable __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    // The combat ability tooltip header uses DisplayTitle (not DisplayName); show our name there too.
    [HarmonyPatch(typeof(FTK_proficiencyTable), "GetLocalizedDisplayTitle")]
    internal static class ProficiencyTitle_Patch
    {
        private static void Postfix(FTK_proficiencyTable __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    // Override the tooltip's effect-description line for proficiencies whose category has no
    // built-in description (e.g. StealGold falls through to a "GetCategoryDescription #...#" placeholder).
    [HarmonyPatch(typeof(FTK_proficiencyTable), "GetCategoryDescription")]
    internal static class ProficiencyCategoryDesc_Patch
    {
        private static void Postfix(FTK_proficiencyTable __instance, ref string __result)
        {
            // 1) explicit per-proficiency override wins. Our custom steal abilities (Cutpurse, Thief Steal)
            //    each register an explicit description, so tier-1 always handles OUR content.
            string desc;
            if (Localization.TryGetProficiencyDescription(__instance.m_ID, out desc)) { __result = desc; return; }

            // 2) Fallback for VANILLA steal abilities only. The game's own GetCategoryDescription switch has
            //    no case for StealGold / StealItem, so those categories fall through to a
            //    "GetCategoryDescription #StealGold#" placeholder. We fill them in with the game's own
            //    "Robbed" string. Kept (not deleted) because removing it would regress vanilla steal
            //    tooltips that hit the same placeholder; custom steal abilities never reach here (tier-1).
            if (__instance.m_ProficiencyPrefab != null &&
                (__instance.m_ProficiencyPrefab.m_Category == ProficiencyBase.Category.StealGold ||
                 __instance.m_ProficiencyPrefab.m_Category == ProficiencyBase.Category.StealItem))
            {
                __result = FTKHub.Localized<TextMisc>("STR_profRobbed");
            }
        }
    }

    [HarmonyPatch(typeof(FTK_playerGameStart), "GetDisplayName")]
    internal static class ClassName_Patch
    {
        private static void Postfix(FTK_playerGameStart __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    // The class-select panel sets the flavor text inline (FTKHub.Localized<TextCharacters>(m_Flavor)),
    // so we override it after the fact for custom classes.
    [HarmonyPatch(typeof(uiSelectCharacterInfo), "ShowCharacterInfo")]
    internal static class ClassFlavor_Patch
    {
        private static void Postfix(uiSelectCharacterInfo __instance, FTK_playerGameStart.ID _characterType)
        {
            FTK_playerGameStart entry = FTK_playerGameStartDB.GetDB().GetEntry(_characterType);
            string flavor;
            if (entry != null && Localization.TryGetClassFlavor(entry.m_ID, out flavor) && __instance.m_ClassFlavor != null)
                __instance.m_ClassFlavor.text = flavor;
        }
    }

    // Enemy name shown in combat / the bestiary. GetEnemyDisplay() is wrapped in try/catch and otherwise
    // falls back to Localized<TextEnemy>("STR_"+m_ID) (a placeholder for our custom ids), so a postfix
    // cleanly substitutes the name we registered.
    [HarmonyPatch(typeof(FTK_enemyCombat), "GetEnemyDisplay")]
    internal static class EnemyName_Patch
    {
        private static void Postfix(FTK_enemyCombat __instance, ref string __result)
        {
            string name;
            if (Localization.TryGetName(__instance.m_ID, out name)) __result = name;
        }
    }

    [HarmonyPatch(typeof(FTK_enemyCombat), "GetEnemyDescription")]
    internal static class EnemyDescription_Patch
    {
        private static void Postfix(FTK_enemyCombat __instance, ref string __result)
        {
            string desc;
            if (Localization.TryGetEnemyDescription(__instance.m_ID, out desc)) __result = desc;
        }
    }

    // --- Slice D1: custom-realm name + boss banner, via CONCRETE callers only (never the generic Localized<T>) ---

    // Patch 1: the realm banner + quest-param [4]. HexLand.GetRealmDisplayValue() is a concrete
    //   `public string GetRealmDisplayValue()` whose body is `Localized<TextLore>("STR_" + ... + "Display")`.
    //   For a synthetic realm that key is passed through verbatim, so we substitute it on the result.
    [HarmonyPatch(typeof(HexLand), "GetRealmDisplayValue")]
    internal static class RealmDisplayValue_Patch
    {
        private static void Postfix(ref string __result)
        {
            __result = Localization.ApplyRealmKeyReplacements(__result);
        }
    }

    // Patch 2: the quest objective realm everywhere (param [5]). QuestLogicBase.GetMessageParams(bool _refresh)
    //   is a concrete `public string[]` returning the live _messageParams array (each entry rich-text-wrapped),
    //   so a single substitution over every element covers every quest UI surface (tracker HUD, quest-confirm
    //   popup, get-quest menu, portrait message, one-liner + story-body paths).
    [HarmonyPatch(typeof(QuestLogicBase), "GetMessageParams", new[] { typeof(bool) })]
    internal static class QuestMessageParams_Patch
    {
        private static void Postfix(ref string[] __result)
        {
            if (__result == null) return;
            for (int i = 0; i < __result.Length; i++)
                __result[i] = Localization.ApplyRealmKeyReplacements(__result[i]);
        }
    }

    // Patch 3: the boss engage banner. The ONLY write to FTKUI.m_BossEnemyName in the assembly is
    //   MessagePresenter.WaitForEngageCamera (a coroutine):
    //     FTKUI.Instance.m_BossEnemyName.text = FTKHub.Localized<TextEnemy>("STR_" + ...m_EnemyCombat.m_ID);
    //   For a custom enemy that key is passed through verbatim. We cannot patch the generic Localized<T>, so we
    //   postfix the iterator's concrete MoveNext and re-set the banner to our registered name AFTER the game wrote
    //   the raw key. Maximally defensive: this runs on every coroutine step (including non-boss engages) and must
    //   NEVER throw out of MoveNext. If the iterator/MoveNext cannot be resolved, TargetMethod returns null and
    //   PatchAll skips it (the banner falls back to the raw key, but nothing breaks).
    [HarmonyPatch]
    internal static class BossEngageBanner_Patch
    {
        private static MethodBase TargetMethod()
        {
            try
            {
                // Primary path: HarmonyX/MonoMod resolves the iterator's MoveNext for us.
                MethodInfo coroutine = AccessTools.Method(
                    typeof(MessagePresenter), "WaitForEngageCamera", new[] { typeof(ContinueFSM) });
                if (coroutine != null)
                {
                    MethodInfo mn = AccessTools.EnumeratorMoveNext(coroutine);
                    if (mn != null) return mn;
                }

                // Fallback: reflect the nested iterator type and grab its MoveNext directly.
                Type[] nested = typeof(MessagePresenter).GetNestedTypes(BindingFlags.NonPublic | BindingFlags.Public);
                if (nested != null)
                {
                    for (int i = 0; i < nested.Length; i++)
                    {
                        if (nested[i].Name.Contains("WaitForEngageCamera"))
                        {
                            MethodInfo mn = nested[i].GetMethod("MoveNext",
                                BindingFlags.Instance | BindingFlags.NonPublic | BindingFlags.Public);
                            if (mn != null) return mn;
                        }
                    }
                }
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("[realm-boss] boss-banner MoveNext target unresolved: " + e.Message);
                return null;
            }

            Plugin.Log.LogWarning("[realm-boss] boss-banner MoveNext target not found; banner stays a raw key.");
            return null;
        }

        private static void Postfix()
        {
            try
            {
                if (FTKUI.Instance == null || FTKUI.Instance.m_BossEnemyName == null) return;
                EncounterSession es = EncounterSession.Instance;
                if (es == null) return;
                EnemyDummy cur = es.GetCurrentEnemy();
                if (cur == null || cur.m_EnemyCombat == null) return;
                string name;
                if (Localization.TryGetName(cur.m_EnemyCombat.m_ID, out name))
                    FTKUI.Instance.m_BossEnemyName.text = name;
            }
            catch
            {
                // Never throw out of a coroutine step.
            }
        }
    }
}
