using System.Text;
using GridEditor;
using HarmonyLib;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// Goal #5 (Adventures) — Slice B: inject a brand-new overworld ENCOUNTER into the live draw pool
    /// and prove it appears in a normal run, WITHOUT touching world generation.
    ///
    /// The "Smuggler's Cache" is cloned from TreasureChest, made eligible in every realm at Common
    /// rarity. The selector (GameLogic.GetMiniEncounter) walks the whole FTK_miniEncounterDB by index
    /// and weighted-rolls the eligible rows, so our freshly registered row is automatically a candidate
    /// — see Content.AddEncounter. To make verification deterministic (rather than waiting for a ~1/170
    /// weighted roll), a debug toggle swaps our encounter in wherever the game already chose to spawn one.
    /// </summary>
    internal static class AdventureContent
    {
        public const string EncounterId = "ftkmf_smugglers_cache";

        /// <summary>The synthetic int the encounter resolved to (read by the debug force-spawn patch).</summary>
        internal static int EncounterIntId = -1;

        public static void Register()
        {
            Content.AddEncounter(
                Plugin.Guid, EncounterId, FTK_miniEncounter.ID.TreasureChest, "Smuggler's Cache",
                e =>
                {
                    e.m_Rarity = "Common";                   // reuse an existing draw-chance bucket
                    e.m_RealmInclude = new FTK_realm.ID[0];  // empty => eligible in EVERY realm
                    e.m_RealmExclude = new FTK_realm.ID[0];
                    e.m_MinTier = FTK_progressionTier.ID.None;
                    e.m_MaxTier = FTK_progressionTier.ID.None;
                    e.m_Spawn = FTK_miniEncounter.SpawnOption.Any;
                    e.m_RequiresMode = FTK_miniEncounter.GameMode.Any;
                    e.m_Requires1 = FTK_miniEncounter.MiniEncounterRequirements.None;
                    e.m_Requires2 = FTK_miniEncounter.MiniEncounterRequirements.None;
                    e.m_Requires3 = FTK_miniEncounter.MiniEncounterRequirements.Land; // land only (safe placement)
                    e.m_OrRequires1 = FTK_miniEncounter.MiniEncounterRequirements.None;
                    e.m_OrRequires2 = FTK_miniEncounter.MiniEncounterRequirements.None;
                    e.m_OrRequires3 = FTK_miniEncounter.MiniEncounterRequirements.None;
                    e.m_OncePerSession = false;
                    e.m_DontSpawnIfLoreReveal = false;
                    e.m_LoreItemUnlock = "";   // no lore-unlock gate
                    e.m_AchievementID = "";
                    e.m_DLC = FTK_dlc.ID.None;
                    e.m_DisplayTop = "You stumble onto a cache hidden by smugglers.";
                    e.m_DisplayBottom = "Fortune favours the curious.";
                });

            EncounterIntId = Content.Db<FTK_miniEncounterDB>().GetIntFromID(EncounterId);

            SelfTest();
            DumpDrawChances();

            RegisterAdventure();
        }

        /// <summary>
        /// Slice C: a new SELECTABLE adventure, "Smuggler's Run", cloned at runtime from the installed
        /// DungeonCrawl adventure and retuned (richer gold/lore, surfaced near the top of the list). It
        /// plays exactly like DungeonCrawl but themed around the smugglers whose caches now litter the
        /// overworld (Slice B), tying the two slices together.
        /// </summary>
        private static void RegisterAdventure()
        {
            Adventures.AddFromTemplate(
                Plugin.Guid, "SmugglersRun", "DungeonCrawl",
                "Smuggler's Run",
                "A treasure-hunter's romp across Fahrul: looser purse-strings, richer lore, and " +
                "smugglers' caches hidden down every road. Same dangers as the Dungeon Crawl — deeper pockets.",
                jo =>
                {
                    jo["m_GoldMultiplier"] = 1.5;        // richer pickings
                    jo["m_LoreMultiplier"] = 1.5;        // more lore
                    jo["m_EncounterChanceMultiplier"] = 1.25; // more overworld encounters (more caches)
                    jo["m_SelectionPriority"] = 250;     // surface it near the top of the adventure list
                });
        }

        /// <summary>Confirm the encounter resolves through every lookup path the game's selector uses.</summary>
        private static void SelfTest()
        {
            FTK_miniEncounterDB db = Content.Db<FTK_miniEncounterDB>();

            int intId = db.GetIntFromID(EncounterId);                       // DbLookupPatcher path
            FTK_miniEncounter.ID enumId = FTK_miniEncounter.GetEnum(EncounterId); // GetEnum patch path
            FTK_miniEncounter byInt = db.GetEntryByInt(intId);             // the spawn/sync resolution path
            FTK_miniEncounter byEnum = db.GetEntry((FTK_miniEncounter.ID)intId);

            bool ok = intId >= 0 && (int)enumId == intId && byInt != null && byEnum != null;
            string name = byInt != null ? byInt.GetDisplayName() : "(null)";

            // Empty m_RealmInclude => valid in every realm (replicates GameLogic.IsValidInRealm).
            bool everyRealm = byInt != null && (byInt.m_RealmInclude == null || byInt.m_RealmInclude.Length == 0);

            if (ok && name == "Smuggler's Cache")
                Plugin.Log.LogInfo("SELF-TEST PASS [encounter]: '" + EncounterId + "' resolves (int=" + intId +
                    ", enum==int=" + ((int)enumId == intId) + ") as \"" + name + "\", rarity=" + byInt.m_Rarity +
                    ", eligibleInEveryRealm=" + everyRealm + ".");
            else
                Plugin.Log.LogError("SELF-TEST FAIL [encounter]: int=" + intId + " enum=" + (int)enumId +
                    " byInt=" + (byInt == null ? "null" : "ok") + " name=\"" + name + "\".");
        }

        /// <summary>
        /// Runtime dump of FTK_encounterDrawChanceDB (the rarity -> weight table). Confirms the real
        /// probability values behind our chosen "Common" bucket — one of the open questions from recon.
        /// </summary>
        private static void DumpDrawChances()
        {
            FTK_encounterDrawChanceDB db = Content.Db<FTK_encounterDrawChanceDB>();
            StringBuilder sb = new StringBuilder("FTK_encounterDrawChanceDB rarity weights:");
            foreach (FTK_encounterDrawChance row in db.m_Array)
                sb.Append(" ").Append(row.m_ID).Append("=").Append(row.m_Probability).Append(";");
            Plugin.Log.LogInfo(sb.ToString());
        }
    }

    /// <summary>
    /// DEBUG verification aid (config: Adventures/ForceCustomEncounter). When enabled, every overworld
    /// encounter the game decides to spawn is replaced by the custom "Smuggler's Cache" — giving an
    /// immediate, unambiguous in-game confirmation that injection worked. We only swap when the game
    /// already chose to spawn SOMETHING (__result != None), so the target hex is guaranteed valid.
    /// Turn this off for normal play.
    /// </summary>
    [HarmonyPatch(typeof(GameLogic), "GetMiniEncounter")]
    internal static class ForceCustomEncounter_Patch
    {
        private static void Postfix(ref FTK_miniEncounter.ID __result)
        {
            if (Plugin.ForceCustomEncounter == null || !Plugin.ForceCustomEncounter.Value) return;
            if (AdventureContent.EncounterIntId < 0) return;
            if (__result == FTK_miniEncounter.ID.None) return; // nothing was going to spawn here anyway
            __result = (FTK_miniEncounter.ID)AdventureContent.EncounterIntId;
        }
    }
}
