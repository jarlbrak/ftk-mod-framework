using GridEditor;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// Phase 2 demo: registers a named WEAPON (clone of the Shortsword) and a named ABILITY
    /// (clone of the fire1 proficiency), then verifies both resolve through the game's own
    /// lookups WITH their custom names. The weapon also opts into the town market.
    /// Self-test log lines are the success signal. Delete once real content packs exist.
    /// </summary>
    internal static class SampleContent
    {
        public static void Register()
        {
            // --- a named weapon, in shops ---
            Content.AddWeapon(Plugin.Guid, "ftkmf_emberbrand", FTK_itembase.ID.bladeShortsword, "Emberbrand",
                w =>
                {
                    w._maxdmg += 3f;
                    w.m_ItemRarity = FTK_itemRarityLevel.ID.rare;
                    w._goldValue = 750;
                    w.m_MinLevel = 1;
                    w.m_TownMarket = true; // safe now: it has a localized name
                    w.m_Dropable = true;
                });

            // --- a named combat action (ability) ---
            Content.AddProficiency(Plugin.Guid, "ftkmf_emberlash", FTK_proficiencyTable.ID.fire1, "Ember Lash",
                p => { p.m_DmgMultiplier = 1.5f; });

            VerifyWeapon();
            VerifyProficiency();
            GiveToBlacksmith();
        }

        /// <summary>
        /// Guaranteed visual test: drop Emberbrand into the Blacksmith's starting inventory so a new
        /// game as the Blacksmith shows it immediately (the town shop selects randomly, so it's an
        /// unreliable way to confirm injection).
        /// </summary>
        private static void GiveToBlacksmith()
        {
            FTK_weaponStats2DB weapons = Content.Db<FTK_weaponStats2DB>();
            int weaponId = weapons.GetIntFromID("ftkmf_emberbrand");

            FTK_playerGameStartDB classes = Content.Db<FTK_playerGameStartDB>(); // ensures the index is built
            FTK_playerGameStart blacksmith = classes.GetEntry(FTK_playerGameStart.ID.blacksmith);

            if (blacksmith == null || weaponId < 0)
            {
                Plugin.Log.LogWarning("GiveToBlacksmith: could not resolve blacksmith/weapon (blacksmith=" +
                    (blacksmith == null ? "null" : "ok") + ", weaponId=" + weaponId + ").");
                return;
            }

            FTK_itembase.ID[] old = blacksmith.m_StartItems ?? new FTK_itembase.ID[0];
            FTK_itembase.ID[] next = new FTK_itembase.ID[old.Length + 1];
            System.Array.Copy(old, next, old.Length);
            next[old.Length] = (FTK_itembase.ID)weaponId;
            blacksmith.m_StartItems = next;

            Plugin.Log.LogInfo("Added Emberbrand to Blacksmith start items (now " + next.Length + " items). Start a new game as the Blacksmith to see it.");
        }

        private static void VerifyWeapon()
        {
            FTK_weaponStats2DB db = TableManager.Instance.Get<FTK_weaponStats2DB>();
            int id = db.GetIntFromID("ftkmf_emberbrand");
            FTK_itembase resolved = FTK_itembase.GetItemBase((FTK_itembase.ID)id); // exercises the routing patch
            string name = resolved != null ? resolved.GetLocalizedName() : "(null)"; // exercises the name patch

            float dmg = resolved is FTK_weaponStats2 ? ((FTK_weaponStats2)resolved)._maxdmg : -1f;
            if (resolved != null && name == "Emberbrand")
                Plugin.Log.LogInfo("SELF-TEST PASS [weapon]: 'ftkmf_emberbrand' resolves via GetItemBase as \"" + name + "\" (maxdmg=" + dmg + ", should be Shortsword+3 if the clone took).");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [weapon]: resolved=" + (resolved == null ? "null" : "ok") + " name=\"" + name + "\".");
        }

        private static void VerifyProficiency()
        {
            FTK_proficiencyTableDB db = TableManager.Instance.Get<FTK_proficiencyTableDB>();
            int id = db.GetIntFromID("ftkmf_emberlash");
            FTK_proficiencyTable prof = db.GetEntry((FTK_proficiencyTable.ID)id);
            string name = prof != null ? prof.GetLocalizedDisplayName() : "(null)";

            if (prof != null && name == "Ember Lash")
                Plugin.Log.LogInfo("SELF-TEST PASS [ability]: 'ftkmf_emberlash' resolves as \"" + name + "\" (dmgMult=" + prof.m_DmgMultiplier + ").");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [ability]: prof=" + (prof == null ? "null" : "ok") + " name=\"" + name + "\".");
        }
    }
}
