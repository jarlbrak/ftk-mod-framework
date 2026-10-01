using System;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    [HarmonyPatch(typeof(uiPlayerMainHudStatus), "SetStatusIcons")]
    internal static class GuardianStatusIconPatch
    {
        private static void Postfix(uiPlayerMainHudStatus __instance, CharacterOverworld _cow)
        {
            if (__instance == null || __instance.m_Protected == null) return;
            try
            {
                GuardianStatusIcon icon = __instance.GetComponent<GuardianStatusIcon>();
                if (icon == null)
                {
                    if (!GuardianRuntime.Enabled) return;
                    icon = __instance.gameObject.AddComponent<GuardianStatusIcon>();
                }
                icon.Bind(__instance.m_Protected.gameObject, _cow);
            }
            catch (Exception e)
            {
                Plugin.Log.LogWarning("Guard status icon unavailable: " + e.Message);
            }
        }
    }

    // Owned by one HUD. uiPlayerMainHud.Update calls SetStatusIcons each active frame, so the
    // same refresh cadence catches guardian incapacity and card reuse.
    internal sealed class GuardianStatusIcon : MonoBehaviour
    {
        private GameObject icon;
        private CharacterOverworld character;

        internal void Bind(GameObject nativeProtected, CharacterOverworld cow)
        {
            character = cow;
            if (icon == null && nativeProtected != null)
            {
                icon = (GameObject)Instantiate(nativeProtected);
                icon.SetActive(false);
                icon.name = "FTK Guarded";
                icon.transform.SetParent(nativeProtected.transform.parent, false);
                icon.transform.SetSiblingIndex(nativeProtected.transform.GetSiblingIndex() + 1);
                uiToolTipGeneral tooltip = icon.GetComponent<uiToolTipGeneral>();
                if (tooltip == null) tooltip = icon.AddComponent<uiToolTipGeneral>();
                tooltip.m_ReturnRawInfo = true;
                tooltip.m_Info = "Guarded";
                tooltip.m_DetailInfo = "Guard reduces direct attack damage until the guarding ally's next turn. Protection ends if the guarding ally is incapacitated.";
            }
            if (icon == null) return;
            try { Refresh(); }
            catch (Exception)
            {
                if (icon != null && icon.activeSelf) icon.SetActive(false);
            }
        }

        private void Refresh()
        {
            bool guarded = false;
            if (GuardianRuntime.Enabled && character != null && character.m_CharacterStats != null &&
                character.m_CharacterStats.m_IsInCombat && character.m_CurrentDummy != null &&
                GuardianRuntime.LivingAlly(character.m_CurrentDummy) && EncounterSession.Instance != null)
            {
                string target = GuardianRuntime.Identity(character.m_CurrentDummy);
                foreach (string guardianId in GuardianRuntime.State.ActiveGuardians(target))
                {
                    if (GuardianRuntime.CanAct(GuardianRuntime.Find(guardianId)))
                    {
                        guarded = true;
                        break;
                    }
                }
            }
            if (icon.activeSelf != guarded) icon.SetActive(guarded);
        }

        private void OnDestroy()
        {
            if (icon != null) Destroy(icon);
        }
    }
}
