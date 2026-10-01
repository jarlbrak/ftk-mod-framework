using System;
using System.Collections;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;

namespace FTKModFramework.Core
{
    internal static class TownExchangeAcquisition
    {
        internal static void Scrub(MiniHexInfo poi)
        {
            if (poi == null || !TownExchangeService.HasRegistrations) return;
            TownExchangeStockPolicy.RemoveExclusive(poi.m_ShopItemStock, TownExchangeService.IsShopBlockedItem);
            if (poi.m_ShopItemStockCurrent != null)
            {
                int removed = TownExchangeStockPolicy.RemoveExclusive(poi.m_ShopItemStockCurrent.m_CountDictionary, TownExchangeService.IsShopBlockedItem);
                if (removed > 0) poi.m_ShopItemStockCurrent.Changed();
            }
        }
    }

    [HarmonyPatch(typeof(TownManager), "GetFilteredMarketAllItems")]
    internal static class ExchangeMarketCandidatePatch
    {
        private static void Postfix(ref List<FTK_itembase> __result)
        {
            if (__result == null || !TownExchangeService.HasRegistrations) return;
            __result.RemoveAll(item => item != null && TownExchangeService.IsShopBlockedItem(FTK_itembase.GetEnum(item.m_ID)));
        }
    }

    [HarmonyPatch(typeof(TownManager), "_registerShopItem")]
    internal static class ExchangeExplicitStockPatch
    {
        private static bool Prefix(FTK_itembase.ID _item)
        {
            return !TownExchangeService.IsShopBlockedItem(_item);
        }
    }

    [HarmonyPatch(typeof(MiniHexInfo), "StateDataDeserializeDone")]
    internal static class ExchangeSavedStockPatch
    {
        private static void Postfix(MiniHexInfo __instance)
        {
            TownExchangeAcquisition.Scrub(__instance);
        }
    }

    [HarmonyPatch(typeof(MiniHexInfo), "BuyItemRPC")]
    internal static class ExchangeExistingStockPurchasePatch
    {
        private static bool Prefix(FTK_itembase.ID _item)
        {
            return !TownExchangeService.IsShopBlockedItem(_item);
        }
    }

    [HarmonyPatch(typeof(MiniHexInfo), "BuyDoneRPC")]
    internal static class ExchangeExistingStockDeliveryPatch
    {
        private static bool Prefix(FTK_itembase.ID _item)
        {
            return !TownExchangeService.IsShopBlockedItem(_item);
        }
    }

    [HarmonyPatch(typeof(MiniHexInfo), "SyncShopStockRPC")]
    internal static class ExchangeSyncedStockPatch
    {
        private static void Postfix(MiniHexInfo __instance)
        {
            TownExchangeAcquisition.Scrub(__instance);
        }
    }

    [HarmonyPatch(typeof(uiBuyMenuHud), "EnableMenu", new Type[] { typeof(string), typeof(ContinueFSM), typeof(MiniHexInfo), typeof(FTKPlayerID) })]
    internal static class ExchangeShopDisplayPatch
    {
        private static void Prefix(MiniHexInfo _hex)
        {
            TownExchangeAcquisition.Scrub(_hex);
        }
    }

    [HarmonyPatch(typeof(GameLogic), "FillLootDropList")]
    internal static class ExchangeLootFilterPatch
    {
        private static void Postfix(ArrayList _arrayList)
        {
            if (_arrayList == null || !TownExchangeService.HasRegistrations) return;
            for (int i = _arrayList.Count - 1; i >= 0; i--)
            {
                string value = _arrayList[i] as string;
                int raw;
                if (value != null && Int32.TryParse(value, out raw) &&
                    TownExchangeService.IsExclusiveItem((FTK_itembase.ID)raw))
                    _arrayList.RemoveAt(i);
            }
        }
    }

    [HarmonyPatch(typeof(uiPopupMenu), "CanSell")]
    internal static class ExchangeCurrencySellAvailabilityPatch
    {
        private static bool Prefix(uiItemIcon itemIcon, ref bool __result)
        {
            if (itemIcon == null || !TownExchangeService.IsCurrencyItem(itemIcon.m_ItemName)) return true;
            __result = false;
            return false;
        }
    }

    [HarmonyPatch(typeof(uiPopupMenu), "ActionSell")]
    internal static class ExchangeCurrencySellActionPatch
    {
        private static bool Prefix(uiPopupMenu __instance)
        {
            return __instance.m_LastItemIcon == null || __instance.m_LastItemIcon.ItemIcon == null ||
                !TownExchangeService.IsCurrencyItem(__instance.m_LastItemIcon.ItemIcon.m_ItemName);
        }
    }
}
