using System;
using HarmonyLib;

namespace FTKModFramework.Core
{
    /// <summary>Poison turns left tweak (information.poison-turns). The poison status icons carry a
    /// uiToolTipGeneral whose serialized m_DetailInfo is STR_statusPoisonInfo: three on the
    /// playerMainHUD prefab (under uiPlayerMainHud) and one icon plus the stats panel's poison entry
    /// (a uiPlayerStatEntry, which inherits GetMoreToolTip) under uiPlayerInventory. A postfix lets
    /// vanilla localize the detail first and only extends the returned string, so no game state
    /// changes. uiToolTipManager.Update calls GetMoreToolTip every frame while a tooltip is showing,
    /// so the off path returns before reading anything and the on path caches both the owner lookup
    /// per tooltip and the composed string per value.</summary>
    [HarmonyPatch(typeof(uiToolTipGeneral), "GetMoreToolTip")]
    internal static class PoisonTurnsPatch
    {
        private const string PoisonDetailKey = "STR_statusPoisonInfo";

        private static readonly PoisonTurnsText Text = new PoisonTurnsText();

        // CharacterStats.m_PoisonTimeCounter is private. The field ref is built on first use inside
        // the try block, so a renamed field faults the tweak instead of failing the type initializer.
        private static AccessTools.FieldRef<CharacterStats, int> _poisonCounter;

        // The last tooltip resolved and the owner component above it. Parent lookups walk the
        // hierarchy, so they run once per hovered tooltip rather than once per frame.
        private static uiToolTipGeneral _lastTooltip;
        private static uiPlayerMainHud _lastHud;
        private static uiPlayerInventory _lastInventory;

        private static void Postfix(uiToolTipGeneral __instance, ref string __result)
        {
            int handle = FrameworkTweaks.PoisonTurns;
            if (!Tweaks.IsOn(handle)) return;
            if (!string.Equals(__instance.m_DetailInfo, PoisonDetailKey)) return;
            string text;
            try
            {
                CharacterOverworld cow = Owner(__instance);
                if (cow == null) return;
                CharacterStats stats = cow.m_CharacterStats;
                if (stats == null) return;
                if (_poisonCounter == null)
                    _poisonCounter = AccessTools.FieldRefAccess<CharacterStats, int>("m_PoisonTimeCounter");
                // IsOwner, not m_PhotonView.isMine: isMine is also true on a master client that took
                // over a departed player's character, whose counter restarted there mid-countdown.
                text = FrameworkTweaks.PoisonTurnsDetail(Tweaks.Registry, handle, Text, __result,
                    cow.IsOwner, stats.m_HealthCurrent > 0, cow.m_WaitForRespawn,
                    stats.m_PoisonLvl, _poisonCounter(stats));
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                return;
            }
            __result = text;
        }

        /// <summary>The character a poison tooltip describes: the HUD's m_Cow or the open
        /// inventory's m_InventoryOwner, read fresh each call because the inventory changes owner.
        /// Any other placement yields null and vanilla text.</summary>
        private static CharacterOverworld Owner(uiToolTipGeneral tooltip)
        {
            if (!ReferenceEquals(tooltip, _lastTooltip))
            {
                _lastHud = tooltip.GetComponentInParent<uiPlayerMainHud>();
                _lastInventory = _lastHud == null ? tooltip.GetComponentInParent<uiPlayerInventory>() : null;
                _lastTooltip = tooltip;
            }
            if (_lastHud != null) return _lastHud.m_Cow;
            if (_lastInventory != null) return _lastInventory.m_InventoryOwner;
            return null;
        }
    }
}
