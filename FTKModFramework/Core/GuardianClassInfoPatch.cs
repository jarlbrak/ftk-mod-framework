using System;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiSelectCharacterInfo), "ShowCharacterInfo")]
    internal static class GuardianClassInfoPatch
    {
        private static void Postfix(uiSelectCharacterInfo __instance, FTK_playerGameStart.ID _characterType)
        {
            try
            {
                if (__instance == null || __instance.m_ClassAbility == null) return;
                int classId = (int)_characterType;
                bool guardian = GuardianRuntime.IsGuardianClass(classId);
                bool cleansing = OverworldAilmentImmunity.IsRegistered(classId);
                if (!guardian && !cleansing) return;

                // Match native skill-name rows. Guard's full rules belong to its proficiency
                // description; compact names retain the native class-card layout.
                string text = __instance.m_ClassAbility.text;
                if (guardian) text = AppendSkill(text, "Skill: Guard");
                if (cleansing) text = AppendSkill(text,
                    "Passive Skill: " + OverworldAilmentImmunity.DisplayName(classId));
                __instance.m_ClassAbility.text = text;
            }
            catch (Exception e) { Plugin.Log.LogWarning("[guardian-class-ui] " + e.Message); }
        }

        private static string AppendSkill(string text, string skill)
        {
            if (text != null)
                foreach (string line in text.Split('\n'))
                    if (string.Equals(line.TrimEnd('\r'), skill, StringComparison.Ordinal)) return text;
            return GuardianEquipmentDescription.Append(text, skill);
        }
    }
}
