using System;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKModFramework.Core
{
    /// <summary>Sell price in item details tweak (information.sell-price). uiInventoryItemDisplay.Show
    /// is the item card: it knows the card's real mode and character, then calls uiItemDetail.Show
    /// with Mode.ItemDisplay, which rewrites the rarity line on every call. The postfix appends the
    /// price to that line after vanilla has built the card, so it never stacks and no game state
    /// changes. Show runs when an item is selected, not per frame. The shorter Show overload forwards
    /// to this one, so both paths pass through here.</summary>
    [HarmonyPatch(typeof(uiInventoryItemDisplay), "Show", new[]
    {
        typeof(FTK_itembase.ID), typeof(Transform), typeof(CharacterOverworld), typeof(uiItemDetail.Mode),
        typeof(string), typeof(Texture), typeof(bool), typeof(int), typeof(uiLoreCard), typeof(bool), typeof(bool),
    })]
    internal static class SellPricePatch
    {
        private static void Postfix(uiInventoryItemDisplay __instance, FTK_itembase.ID _itemID,
            CharacterOverworld _cow, uiItemDetail.Mode _mode)
        {
            int handle = FrameworkTweaks.SellPrice;
            if (!Tweaks.IsOn(handle)) return;
            Text rarity;
            string text;
            try
            {
                if (_cow == null || _cow.m_HexLand == null) return;
                rarity = __instance.m_ItemDetail != null ? __instance.m_ItemDetail.m_ItemRarityDisplay : null;
                if (rarity == null) return;
                // The same gate uiItemMenu.ShowPlayerInventory applies before it adds the Sell button,
                // with the POI null-checked before CanSellItems. Shop, reward, vote and lore cards
                // stop at the mode, so they never call into the POI or the item tables.
                bool inventoryView = _mode == uiItemDetail.Mode.Inventory;
                MiniHexInfo shop = _cow.m_HexLand.m_POI;
                bool shopCanSell = inventoryView && shop != null && shop.CanSellItems();
                MiniHexInfo pricePoi = _cow.GetPOI();
                bool shown = FrameworkTweaks.SellPriceShown(Tweaks.Registry, handle,
                    inventoryView, shopCanSell, shopCanSell && Sellable(_itemID), pricePoi != null);
                if (!shown) return;
                text = SellPriceText.Append(rarity.text, SellValue(_itemID, _cow, pricePoi));
            }
            catch (Exception e)
            {
                Tweaks.Fault(handle, e);
                return;
            }
            rarity.text = text;
        }

        /// <summary>uiItemMenu.ShowPlayerInventory's item test: quest rarity and the
        /// GameLogic.m_CantSellOrDiscardItems list never get a Sell button.</summary>
        private static bool Sellable(FTK_itembase.ID item)
        {
            FTK_itemRarityLevel.ID rarity = FTK_itemRarityLevel.ID.None;
            if (FTK_itemsDB.GetDB().IsContain(item)) rarity = FTK_itemsDB.GetDB().GetEntry(item).m_ItemRarity;
            else if (FTK_weaponStats2DB.GetDB().IsContain(item)) rarity = FTK_weaponStats2DB.GetDB().GetEntry(item).m_ItemRarity;
            return rarity != FTK_itemRarityLevel.ID.quest && !GameLogic.Instance.m_CantSellOrDiscardItems.Contains(item);
        }

        /// <summary>The value the Sell button shows: uiItemMenu.GetSellItemValue and
        /// uiPopupMenu.GetSellItemValue dispatch to GetSellValue the same way, with the character and
        /// the POI from CharacterOverworld.GetPOI. They are private instance methods bound to the
        /// menu's own character, so the dispatch is repeated here rather than the price reimplemented.</summary>
        private static int SellValue(FTK_itembase.ID item, CharacterOverworld cow, MiniHexInfo poi)
        {
            if (FTK_weaponStats2DB.GetDB().IsContain(item)) return FTK_weaponStats2DB.Get(item).GetSellValue(cow, poi);
            return FTK_itemsDB.Get(item).GetSellValue(cow, poi);
        }
    }
}
