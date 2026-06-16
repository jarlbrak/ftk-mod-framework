using GridEditor;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// Proof-of-pipeline smoke test. Registers a new item by cloning the vanilla Shortsword,
    /// then immediately reads it back through the game's own lookup path to prove the full
    /// inject + synthetic-ID + reindex chain works in a live game.
    ///
    /// We deliberately do NOT surface it in shops/loot yet: a freshly-injected item has no
    /// localized name (the localization table is a Phase-2 feature), and some FTK UI paths can
    /// NPE on a missing name. The log line below is the unambiguous success signal for v0.1.
    /// Delete this file once real content packs exist.
    /// </summary>
    internal static class SampleContent
    {
        private const string Id = "ftkmf_testblade";

        public static void Register()
        {
            FTK_itemsDB itemsDb = TableManager.Instance.Get<FTK_itemsDB>();

            // Clone an existing item so the new one inherits a valid icon/prefab/mesh.
            FTK_items template = itemsDb.GetEntry(FTK_itembase.ID.bladeShortsword);

            FTK_items blade = (FTK_items)ContentRegistry.Register(
                itemsDb,
                Plugin.Guid,
                Id,
                template,
                row =>
                {
                    FTK_items item = (FTK_items)row;
                    item.m_ItemRarity = FTK_itemRarityLevel.ID.uncommon;
                    item._goldValue = 999;
                    item.m_TownMarket = false; // keep it out of the shop UI until Phase 2 localization
                    item.m_Dropable = false;
                });

            // Read it back through the game's own lookup (exercises the patched GetIntFromID + reindex).
            FTK_items roundTrip = itemsDb.GetEntryByStringID(Id);
            bool ok = roundTrip != null && roundTrip.m_ID == Id && roundTrip._goldValue == 999;

            if (ok)
                Plugin.Log.LogInfo("SELF-TEST PASS: '" + Id + "' registered and resolves via the game's lookup (goldValue=" + roundTrip._goldValue + ").");
            else
                Plugin.Log.LogError("SELF-TEST FAIL: '" + Id + "' did not round-trip (roundTrip=" + (roundTrip == null ? "null" : roundTrip.m_ID) + ").");
        }
    }
}
