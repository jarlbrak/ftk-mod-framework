using System;
using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>Register a private modifier row sharing the item's allocated identity.</summary>
        public static FTK_characterModifier SetItemModifiers(string modGuid, FTK_itembase item,
            Action<FTK_characterModifier> configure)
        {
            int id;
            if (string.IsNullOrEmpty(modGuid) || item == null ||
                !ContentRegistry.TryGetSyntheticId(item.m_ID, out id, typeof(FTK_itemsDB), typeof(FTK_weaponStats2DB))) return null;
            FTK_itembase registered = item is FTK_weaponStats2
                ? (FTK_itembase)Db<FTK_weaponStats2DB>().GetEntry((FTK_itembase.ID)id)
                : (FTK_itembase)Db<FTK_itemsDB>().GetEntry((FTK_itembase.ID)id);
            if (!object.ReferenceEquals(item, registered)) return null;
            FTK_characterModifierDB db = Db<FTK_characterModifierDB>();
            int existing;
            if (ContentRegistry.TryGetSyntheticId(item.m_ID, out existing, typeof(FTK_characterModifierDB)))
                return db.GetEntry((FTK_characterModifier.ID)existing);
            // Native equip code converts the item enum to a decimal string, then resolves the
            // modifier with that same integer. An independently allocated modifier ID would fail.
            return (FTK_characterModifier)ContentRegistry.Register(db, modGuid, item.m_ID, null,
                delegate(object row)
                {
                    FTK_characterModifier modifier = (FTK_characterModifier)row;
                    modifier.m_CharacterSkills = new CharacterSkills();
                    if (configure != null) configure(modifier);
                }, id);
        }
    }
}
