namespace FTKModFramework.Core
{
    /// <summary>The display decision and text for information.sell-price, kept free of game types so
    /// Tests/Tweaks can cover it. The inputs mirror the gate uiPopupMenu applies before it shows an
    /// interactable "Sell (N)" (the static uiItemMenu.Show forwards there): the icon is not in an
    /// equipped slot, since a uiInventory1ItemContainer icon takes ShowPlayerEquiped, which has no
    /// Sell button; and CanSell holds: the hex POI (HexLand.GetPOI, land or air) exists and
    /// CanSellItems(), the item is neither quest rarity nor listed in
    /// GameLogic.m_CantSellOrDiscardItems, and the menu's m_CanControl is set. Without CanSell the
    /// button is shown disabled and carries no price.</summary>
    internal static class SellPriceText
    {
        /// <summary>True when the card should show a sell price. The price itself is computed by the
        /// caller only after this returns true, because FTK_itembase.GetCost dereferences the POI.</summary>
        /// <param name="inventoryView">The card is the viewing character's own inventory card
        /// (uiItemDetail.Mode.Inventory), not a shop, reward, vote or lore card.</param>
        /// <param name="unequippedIcon">The card belongs to the inventory's selected icon and that
        /// icon is not in a uiInventory1ItemContainer, so uiPopupMenu would take ShowPlayerBackpack,
        /// the only player path with a Sell button. Equipped items must be unequipped first.</param>
        /// <param name="canControl">uiPopupMenu.m_CanControl: always outside multiplayer, otherwise
        /// the character IsOwner or m_WaitForRespawn.</param>
        /// <param name="shopCanSell">The character's hex POI is non-null and CanSellItems().</param>
        /// <param name="sellableItem">Not quest rarity and not in m_CantSellOrDiscardItems.</param>
        /// <param name="hasPricePoi">CharacterOverworld.GetPOI(), the POI the Sell button prices
        /// with, is non-null.</param>
        internal static bool Shown(bool inventoryView, bool unequippedIcon, bool canControl, bool shopCanSell,
            bool sellableItem, bool hasPricePoi)
        {
            return inventoryView && unequippedIcon && canControl && shopCanSell && sellableItem && hasPricePoi;
        }

        internal static string Line(int value)
        {
            return "Sells for " + value.ToString();
        }

        /// <summary>The card's rarity line with the price after it. The rarity line is used because
        /// every item type shows it, whereas the weapon, armor and item panels are exclusive.</summary>
        internal static string Append(string rarityText, int value)
        {
            if (string.IsNullOrEmpty(rarityText)) return Line(value);
            return rarityText + "  (" + Line(value) + ")";
        }
    }
}
