using System;
using GridEditor;

namespace FTKModFramework.Core
{
    public static partial class Content
    {
        /// <summary>Give a registered physical item an authoritative enemy reward rule.</summary>
        public static void SetEnemyDropRule(FTK_items rewardItem, EnemyDropRule rule)
        {
            if (rewardItem == null) throw new ArgumentNullException("rewardItem");
            EnemyDropRules.Validate(rule);
            FTK_itembase.ID itemId = FTK_itembase.GetEnum(rewardItem.m_ID);
            if (!ContentRegistry.IsRegisteredSyntheticId((int)itemId, typeof(FTK_itemsDB)) ||
                !object.ReferenceEquals(Db<FTK_itemsDB>().GetEntry(itemId), rewardItem))
                throw new ArgumentException("Enemy reward item must be registered content.");
            EnemyDropRuntime.Register((int)itemId, rule);
        }
    }
}
