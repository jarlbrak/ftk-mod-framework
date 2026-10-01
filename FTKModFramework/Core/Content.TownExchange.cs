using System;
using GridEditor;

namespace FTKModFramework.Core
{
    public sealed class TownExchangeOffer
    {
        public readonly FTK_itembase.ID Item;
        public readonly string Name;
        public readonly string Family;
        public readonly string Slot;
        public readonly FTK_playerGameStart.ID OwnerClass;

        public TownExchangeOffer(FTK_playerGameStart.ID ownerClass, FTK_itembase.ID item, string name, string family, string slot)
        {
            if (ownerClass == FTK_playerGameStart.ID.None || item == FTK_itembase.ID.None || String.IsNullOrEmpty(name) || String.IsNullOrEmpty(family))
                throw new ArgumentException("Exchange offer requires an item, name, and family.");
            OwnerClass = ownerClass;
            Item = item;
            Name = name;
            Family = family;
            Slot = slot ?? String.Empty;
        }

        [Obsolete("Exchange offers require an explicit registered owner class.")]
        public TownExchangeOffer(FTK_itembase.ID item, string name, string family, string slot)
            : this(FTK_playerGameStart.ID.None, item, name, family, slot) { }
    }

    public static partial class Content
    {
        /// <summary>Add offers to the framework's universal Guild Token exchange.</summary>
        public static void RegisterTownExchange(params TownExchangeOffer[] offers)
        {
            BuiltinEquipmentExchange.RequirePresentation();
            TownExchangeService.Register(FTK_itembase.GetEnum(BuiltinEquipmentExchange.Token.m_ID), offers);
            BuiltinEquipmentExchange.EnableDrops();
        }

        /// <summary>Register a physical token and fixed, one-token-per-item city exchange catalog.</summary>
        public static void RegisterTownExchange(FTK_itembase.ID token, params TownExchangeOffer[] offers)
        {
            if (token != FTK_itembase.GetEnum(BuiltinEquipmentExchange.Token.m_ID))
                throw new ArgumentException("Town exchange uses the framework Guild Token.");
            RegisterTownExchange(offers);
        }

        /// <summary>Register an exchange using the already registered physical token row.</summary>
        public static void RegisterTownExchange(FTK_items token, params TownExchangeOffer[] offers)
        {
            if (token == null) throw new ArgumentNullException("token");
            FTK_itembase.ID id = FTK_itembase.GetEnum(token.m_ID);
            if (!Object.ReferenceEquals(token, BuiltinEquipmentExchange.Token) ||
                !Object.ReferenceEquals(Db<FTK_itemsDB>().GetEntry(id), token))
                throw new ArgumentException("Town exchange token must be the framework Guild Token row.");
            RegisterTownExchange(offers);
        }
    }
}
