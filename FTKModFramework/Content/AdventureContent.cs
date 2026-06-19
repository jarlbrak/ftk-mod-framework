using GridEditor;
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

            SelfTest();

            RegisterAdventure();

            // Campaign builder (#38): author + register a 2-stage linear campaign and prove its $type
            // discriminators round-trip through the game's own serializer. Gated identically (this runs
            // only when EnableSampleContent is on, since it registers a real selectable demo adventure).
            CampaignSelfTest.Run();

            // Custom-verb resolver + collect-N (#40): author a collect-N quest, prove the framework-$type
            // ModQuestDef round-trips through the game serializer (the OQ2 in-engine check), the resolver
            // Prefix substitutes a CollectNQuestLogic, and the count<1 guard fires. Same gate (it registers a
            // real selectable demo adventure via AddCampaignFromTemplate, like the campaign self-test).
            CollectNSelfTest.Run();

            // Campaign-flag store (#41): prove a populated CampaignStateQuest round-trips through BOTH the disk
            // serializer (FullSerializer) and the co-op RPC serializer (Newtonsoft TypeNameHandling.Auto),
            // recovering identical flags AND the concrete subtype. Standalone (no live GameLogic/save needed).
            CampaignFlagSelfTest.Run();

            // Branch router (#42): author a 3-quest campaign with an on-complete flag + a flag-conditioned branch,
            // then drive the REAL QuestRouterPatch.Postfix and prove the match redirects (on-complete flag applied
            // first, then the m_Stages-walk target swap), a non-match leaves the vanilla successor, and an unknown
            // op (compare + mutate) is rejected at authoring. Same gate (registers a real selectable demo adventure).
            BranchRouterSelfTest.Run();

            // Campaign QuestValidator (#43): author a clean linear campaign + a broken one (unconditional cycle
            // that never reaches victory), and prove the load-time validator passes the valid one with 0 errors
            // and catches the broken one with the precise victory-reachability FAIL naming the offending quest.
            // Same gate (registers real selectable demo adventures via AddCampaignFromTemplate).
            QuestValidatorSelfTest.Run();

            // Bundled sample campaign (#44, spec #37 P5): the consumer-side deliverable. ONE cohesive campaign
            // authored SOLELY through the public Adventures.*/builder API, exercising all four objective types
            // (kill/visit/clear/encounter) + collect-N + one flag-conditioned (convergent) branch, terminating in
            // a last-quest victory. Emits SELF-TEST PASS [campaign]; same gate (registers a real selectable demo
            // adventure via AddCampaignFromTemplate; the load pre-pass reports 0 validation errors).
            CampaignContent.Register();
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
    }
}
