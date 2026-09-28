using System;
using System.Collections.Generic;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>Player Taunt and Petrified icons (fix.player-status-icons, Spec #269). The HUD prefab's
    /// aliments grid already holds taunt and petrified icons with their sprites and grid slots, but
    /// uiPlayerMainHudStatus has no field for them, so vanilla never shows them. This postfix finds both
    /// by path once per HUD, then sets them under SetStatusIcons' own combat predicate. It also names
    /// Dazed on the stunned icon's tooltip. It clones and adds nothing, and changes only this machine's
    /// display.
    ///
    /// It is a sibling of StaleCombatIconPatch on the same method rather than part of it: it needs a
    /// per-HUD cache and an undo when switched off, and it keeps its own fault boundary, so a fault
    /// here cannot switch off the stale icon fixes. The two postfixes touch different icons, so their
    /// order does not matter.</summary>
    [HarmonyPatch(typeof(uiPlayerMainHudStatus), "SetStatusIcons")]
    internal static class PlayerStatusIconsPatch
    {
        /// <summary>The children one HUD resolved. Found is false when either icon or the taunt tooltip
        /// is missing; such a HUD is remembered so the search runs once.</summary>
        private sealed class Slots
        {
            internal uiPlayerMainHudStatus Hud;
            internal bool Found;
            internal GameObject Taunt;
            internal GameObject Petrified;
            internal uiToolTipGeneral TauntTooltip;
            internal uiToolTipGeneral StunTooltip;
            internal string StunInfo;
            internal string StunDetail;
        }

        // Keyed by instance ID: HUDs are few and live for a scene. Dead entries are pruned when a new
        // HUD is added, so a steady call only does one lookup and allocates nothing.
        private static readonly Dictionary<int, Slots> Cache = new Dictionary<int, Slots>();
        private static readonly List<int> Dead = new List<int>();
        private static bool _missingLogged;

        private static void Postfix(uiPlayerMainHudStatus __instance, CharacterOverworld _cow)
        {
            int handle = FrameworkTweaks.PlayerStatusIconsFix;
            bool on = Tweaks.IsOn(handle);
            // Off with no HUD ever touched: nothing to undo, so vanilla's call stands untouched.
            if (!on && Cache.Count == 0) return;
            if (__instance == null) return;
            try
            {
                Slots slots;
                if (!Cache.TryGetValue(__instance.GetInstanceID(), out slots))
                {
                    if (!on) return;
                    slots = Resolve(__instance);
                }
                if (!slots.Found) return;

                PlayerStatusIconChange change = FrameworkTweaks.PlayerStatusIconChanges(Tweaks.Registry, handle,
                    Observe(slots, _cow, on));
                if (change != PlayerStatusIconChange.None) Apply(slots, change);
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
            }
        }

        private static Slots Resolve(uiPlayerMainHudStatus hud)
        {
            PruneDead();
            var slots = new Slots { Hud = hud };
            Transform root = hud.transform;
            Transform taunt = root.Find(PlayerStatusIcons.TauntPath);
            Transform petrified = root.Find(PlayerStatusIcons.PetrifiedPath);
            uiToolTipGeneral tauntTooltip = taunt != null ? taunt.GetComponent<uiToolTipGeneral>() : null;
            if (taunt != null && petrified != null && tauntTooltip != null)
            {
                slots.Found = true;
                slots.Taunt = taunt.gameObject;
                slots.Petrified = petrified.gameObject;
                slots.TauntTooltip = tauntTooltip;
                slots.StunTooltip = hud.m_Stunned != null ? hud.m_Stunned.GetComponent<uiToolTipGeneral>() : null;
                if (slots.StunTooltip != null)
                {
                    slots.StunInfo = slots.StunTooltip.m_Info;
                    slots.StunDetail = slots.StunTooltip.m_DetailInfo;
                }
            }
            else if (!_missingLogged)
            {
                _missingLogged = true;
                Plugin.Log.LogWarning("Tweaks: fix.player-status-icons found no " + PlayerStatusIcons.TauntPath + " or "
                    + PlayerStatusIcons.PetrifiedPath + " icon with its tooltip under the player HUD, so that HUD keeps vanilla's icons.");
            }
            Cache[hud.GetInstanceID()] = slots;
            return slots;
        }

        private static void PruneDead()
        {
            Dead.Clear();
            foreach (KeyValuePair<int, Slots> entry in Cache)
                if (entry.Value.Hud == null) Dead.Add(entry.Key);
            for (int i = 0; i < Dead.Count; i++) Cache.Remove(Dead[i]);
        }

        /// <summary>Reads the state after vanilla ran. Statuses are read only on vanilla's combat branch.
        /// The CharacterDummy getters are ContainsKey on m_SufferingProficiencies; one pass over its
        /// entries gives the same answers, and unlike ContainsKey with an enum key it cannot box under
        /// the game's older Mono, so the postfix itself allocates nothing.</summary>
        private static PlayerStatusIconState Observe(Slots slots, CharacterOverworld cow, bool on)
        {
            var state = new PlayerStatusIconState
            {
                ChildrenFound = true,
                TauntShown = slots.Taunt.activeSelf,
                PetrifiedShown = slots.Petrified.activeSelf,
                TauntTooltipRewritten = slots.TauntTooltip.m_Info == PlayerStatusIcons.TauntInfo
                    && slots.TauntTooltip.m_DetailInfo == PlayerStatusIcons.TauntDetail,
                StunTooltipFound = slots.StunTooltip != null,
                StunTooltipShowsDazed = slots.StunTooltip != null && slots.StunTooltip.m_Info == PlayerStatusIcons.DazedInfo,
            };
            if (!on || cow == null || cow.m_CharacterStats == null) return state;
            CharacterDummy dummy = cow.m_CurrentDummy;
            state.HasCurrentDummy = (bool)dummy;
            state.InCombat = cow.m_CharacterStats.m_IsInCombat;
            if (!state.HasCurrentDummy || !state.InCombat || dummy.m_SufferingProficiencies == null) return state;
            foreach (KeyValuePair<ProficiencyBase.Category, CharacterDummy.ProficiencyRecord> entry in dummy.m_SufferingProficiencies)
            {
                switch (entry.Key)
                {
                    case ProficiencyBase.Category.Taunt: state.Taunting = true; break;
                    case ProficiencyBase.Category.Petrify: state.Petrified = true; break;
                    case ProficiencyBase.Category.Stunned: state.StunnedStatus = true; break;
                    case ProficiencyBase.Category.Dazed: state.DazedStatus = true; break;
                }
            }
            return state;
        }

        private static void Apply(Slots slots, PlayerStatusIconChange change)
        {
            if ((change & PlayerStatusIconChange.RewriteTauntTooltip) != 0)
            {
                slots.TauntTooltip.m_Info = PlayerStatusIcons.TauntInfo;
                slots.TauntTooltip.m_DetailInfo = PlayerStatusIcons.TauntDetail;
            }
            if ((change & PlayerStatusIconChange.StunTooltipDazed) != 0)
            {
                slots.StunTooltip.m_Info = PlayerStatusIcons.DazedInfo;
                slots.StunTooltip.m_DetailInfo = PlayerStatusIcons.DazedDetail;
            }
            else if ((change & PlayerStatusIconChange.StunTooltipStunned) != 0)
            {
                slots.StunTooltip.m_Info = slots.StunInfo;
                slots.StunTooltip.m_DetailInfo = slots.StunDetail;
            }
            if ((change & PlayerStatusIconChange.ShowTaunt) != 0) slots.Taunt.SetActive(true);
            else if ((change & PlayerStatusIconChange.HideTaunt) != 0) slots.Taunt.SetActive(false);
            if ((change & PlayerStatusIconChange.ShowPetrified) != 0) slots.Petrified.SetActive(true);
            else if ((change & PlayerStatusIconChange.HidePetrified) != 0) slots.Petrified.SetActive(false);
        }
    }
}
