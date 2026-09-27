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
        // uiPlayerInventory.m_CurrentItem is private; SelectItemIcon sets it to the icon just before
        // it shows the Mode.Inventory card, so it tells the postfix which container the card is for.
        private static AccessTools.FieldRef<uiPlayerInventory, uiItemIcon> _currentItem;

        private static void Postfix(uiInventoryItemDisplay __instance, FTK_itembase.ID _itemID, Transform _lastOwner,
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
                // The same gate uiPopupMenu applies before it offers an interactable Sell button: an
                // unequipped icon, m_CanControl, and CanSell's POI and item tests, with the POI
                // null-checked before CanSellItems. Shop, reward, vote and lore cards stop at the
                // mode, so they never call into the inventory, the POI or the item tables.
                bool inventoryView = _mode == uiItemDetail.Mode.Inventory;
                bool unequippedIcon = inventoryView && UnequippedIcon(_lastOwner, _itemID);
                bool canControl = unequippedIcon
                    && (!GameLogic.Instance.IsMultiplayer() || _cow.IsOwner || _cow.m_WaitForRespawn);
                MiniHexInfo shop = canControl ? _cow.m_HexLand.GetPOI() : null;
                bool shopCanSell = shop != null && shop.CanSellItems();
                MiniHexInfo pricePoi = _cow.GetPOI();
                bool shown = FrameworkTweaks.SellPriceShown(Tweaks.Registry, handle, inventoryView, unequippedIcon,
                    canControl, shopCanSell, shopCanSell && Sellable(_itemID), pricePoi != null);
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

        /// <summary>True when the card is for the owning uiPlayerInventory's selected icon and that icon
        /// is not in an equipped slot. uiPopupMenu.Show sends a uiInventory1ItemContainer icon (worn
        /// gear and the HUD weapon slot) to ShowPlayerEquiped, which offers Unequip but never Sell.
        /// Any other caller, such as a card with no inventory owner, has no Sell button to mirror.</summary>
        private static bool UnequippedIcon(Transform lastOwner, FTK_itembase.ID item)
        {
            if (lastOwner == null) return false;
            uiPlayerInventory inventory = lastOwner.GetComponent<uiPlayerInventory>();
            if (inventory == null) return false;
            if (_currentItem == null) _currentItem = AccessTools.FieldRefAccess<uiPlayerInventory, uiItemIcon>("m_CurrentItem");
            uiItemIcon icon = _currentItem(inventory);
            return icon != null && icon.m_ItemName == item && !(icon.GetUIContainer() is uiInventory1ItemContainer);
        }

        /// <summary>uiPopupMenu.CanSell's item test: quest rarity and the
        /// GameLogic.m_CantSellOrDiscardItems list never get an interactable Sell button.</summary>
        private static bool Sellable(FTK_itembase.ID item)
        {
            FTK_itemRarityLevel.ID rarity = FTK_itemRarityLevel.ID.None;
            if (FTK_itemsDB.GetDB().IsContain(item)) rarity = FTK_itemsDB.GetDB().GetEntry(item).m_ItemRarity;
            else if (FTK_weaponStats2DB.GetDB().IsContain(item)) rarity = FTK_weaponStats2DB.GetDB().GetEntry(item).m_ItemRarity;
            return rarity != FTK_itemRarityLevel.ID.quest && !GameLogic.Instance.m_CantSellOrDiscardItems.Contains(item);
        }

        /// <summary>The value the Sell button shows: uiPopupMenu.GetSellItemValue (and the unused
        /// uiItemMenu.GetSellItemValue) dispatch to GetSellValue the same way, with the character and
        /// the POI from CharacterOverworld.GetPOI. They are private instance methods bound to the
        /// menu's own character, so the dispatch is repeated here rather than the price reimplemented.</summary>
        private static int SellValue(FTK_itembase.ID item, CharacterOverworld cow, MiniHexInfo poi)
        {
            if (FTK_weaponStats2DB.GetDB().IsContain(item)) return FTK_weaponStats2DB.Get(item).GetSellValue(cow, poi);
            return FTK_itemsDB.Get(item).GetSellValue(cow, poi);
        }
    }
}
