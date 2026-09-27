namespace FTKModFramework.Core
{
    /// <summary>The display decision and text for information.sell-price, kept free of game types so
    /// Tests/Tweaks can cover it. The inputs mirror the gate uiItemMenu.ShowPlayerInventory applies
    /// before it adds "Sell (N)": the hex POI exists and CanSellItems(), and the item is neither
    /// quest rarity nor listed in GameLogic.m_CantSellOrDiscardItems.</summary>
    internal static class SellPriceText
    {
        /// <summary>True when the card should show a sell price. The price itself is computed by the
        /// caller only after this returns true, because FTK_itembase.GetCost dereferences the POI.</summary>
        /// <param name="inventoryView">The card is the viewing character's own inventory card
        /// (uiItemDetail.Mode.Inventory), not a shop, reward, vote or lore card.</param>
        /// <param name="shopCanSell">The character's hex POI is non-null and CanSellItems().</param>
        /// <param name="sellableItem">Not quest rarity and not in m_CantSellOrDiscardItems.</param>
        /// <param name="hasPricePoi">CharacterOverworld.GetPOI(), the POI the Sell button prices
        /// with, is non-null.</param>
        internal static bool Shown(bool inventoryView, bool shopCanSell, bool sellableItem, bool hasPricePoi)
        {
            return inventoryView && shopCanSell && sellableItem && hasPricePoi;
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
