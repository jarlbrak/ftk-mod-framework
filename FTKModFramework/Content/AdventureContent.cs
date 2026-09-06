using GridEditor;
using HarmonyLib;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// Goal #5 (Adventures), Slice B: inject a brand-new overworld ENCOUNTER into the live draw pool
    /// and prove it appears in a normal run, WITHOUT touching world generation.
    ///
    /// The "Smuggler's Cache" is cloned from TreasureChest, made eligible in every realm at Common
    /// rarity. The selector (GameLogic.GetMiniEncounter) walks the whole FTK_miniEncounterDB by index
    /// and weighted-rolls the eligible rows, so our freshly registered row is automatically a candidate
    /// (see Content.AddEncounter). To make verification deterministic (rather than waiting for a ~1/170
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

            // Engine self-tests that ride on the sample content (development gate: Diagnostics/RunSelfTests,
            // off by default). Every one of these registers PROBE content a player would otherwise see: throwaway
            // campaigns in the New Game list (several of them deliberately broken, logged as errors by design), a
            // probe enemy set, and a probe realm. They run only when EnableSampleContent is on (they need the
            // demo's rows) AND the self-tests are on. The real shipped content below is unaffected.
            if (Plugin.SelfTestsEnabled)
            {
                // Campaign builder (#38): author + register a 2-stage linear campaign and prove its $type
                // discriminators round-trip through the game's own serializer.
                CampaignSelfTest.Run();

                // Custom-verb resolver + collect-N (#40): author a collect-N quest, prove the framework-$type
                // ModQuestDef round-trips through the game serializer (the OQ2 in-engine check), the resolver
                // Prefix substitutes a CollectNQuestLogic, and the count<1 guard fires.
                CollectNSelfTest.Run();

                // Campaign-flag store (#41): prove a populated CampaignStateQuest round-trips through BOTH the disk
                // serializer (FullSerializer) and the co-op RPC serializer (Newtonsoft TypeNameHandling.Auto),
                // recovering identical flags AND the concrete subtype. Standalone (no live GameLogic/save needed).
                CampaignFlagSelfTest.Run();

                // Branch router (#42): author a 3-quest campaign with an on-complete flag + a flag-conditioned
                // branch, then drive the REAL QuestRouterPatch.Postfix and prove the match redirects (on-complete
                // flag applied first, then the m_Stages-walk target swap), a non-match leaves the vanilla
                // successor, and an unknown op (compare + mutate) is rejected at authoring.
                BranchRouterSelfTest.Run();

                // Campaign QuestValidator (#43): author a clean linear campaign + a broken one (unconditional cycle
                // that never reaches victory), and prove the load-time validator passes the valid one with 0
                // errors and catches the broken one with the precise victory-reachability FAIL naming the quest.
                QuestValidatorSelfTest.Run();
            }

            // Bundled sample campaign (#44, spec #37 P5): the consumer-side deliverable. ONE cohesive campaign
            // authored SOLELY through the public Adventures.*/builder API, exercising all four objective types
            // (kill/visit/clear/encounter) + collect-N + one flag-conditioned (convergent) branch, terminating in
            // a last-quest victory. Emits SELF-TEST PASS [campaign]; same gate (registers a real selectable demo
            // adventure via AddCampaignFromTemplate; the load pre-pass reports 0 validation errors).
            CampaignContent.Register();

            // Bespoke realm + boss slice (spec #57): the two Core registration helpers' load-time self-tests.
            // Same development gate as above (each registers a probe row a player never needs).
            if (Plugin.SelfTestsEnabled)
            {
                //   #59 RegisterEnemySet: clone bounty1A, fill m_HalfParty/m_FullParty*, prove the set resolves by
                //       int and is a non-empty, non-GenericBoss solo set. Emits SELF-TEST PASS [enemyset].
                EnemySetSelfTest.Run();

                //   #58 RegisterRealm + the gating dict-key spike: register a realm cloned from PoisonBog, then
                //       prove a SYNTHETIC realm int survives a full GameDefinition Newtonsoft round-trip (its
                //       decimal-string dictionary KEY in m_RealmStages converts to the enum-typed key) and resolves
                //       through the game's own GetRealmProperties. Emits SELF-TEST PASS [realm-spike] or FAIL with
                //       the exact failure (never throws out of registration).
                RealmSpikeSelfTest.Run();
            }

            // Slice D1 (#60/#61/#62): the bespoke custom-realm + boss DEMO, the consumer-side deliverable that
            // builds on the spike (which PASSED in-game, so the bespoke-realm path is in use). ONE cohesive
            // adventure: the realm "The Hollow Mire" (cloned from PoisonBog, m_GameStartRealm), the boss
            // "Mudwretch Foreman" (cloned enemy + signature procs), an enemy set serving BOTH the overworld
            // set-piece (RealmProperties.m_BossEnemy) AND the final boss bounty, and a true-victory questline
            // ending on that bounty (m_EndGameAfterLastQuest). Emits SELF-TEST PASS [realm-boss-set] (#60) and
            // SELF-TEST PASS [realm-boss] (#61/#62 at load). Same gate (registers a real selectable adventure).
            RealmBossAdventure.Register();
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
                "smugglers' caches hidden down every road. Same dangers as the Dungeon Crawl, deeper pockets.",
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

    /// <summary>
    /// DEBUG verification aid (config: Adventures/ForceCustomEncounter): replace every overworld encounter
    /// the game decides to spawn with this sample's "Smuggler's Cache", giving an immediate, unambiguous
    /// in-game confirmation that encounter injection worked. We only swap where the game already chose to
    /// spawn SOMETHING (__result != None), so the target hex is guaranteed valid. Off for normal play.
    ///
    /// It lives HERE, beside the encounter it forces (the same placement as ForceCutpurse_Patch in
    /// CutpurseEnemy.cs), so deleting Content/ removes the debug feature cleanly and Core/ never names a
    /// content pack. HarmonyX PatchAll discovers the attribute on this class wherever it sits in the
    /// assembly, so no registration wiring is needed.
    ///
    /// Samples-off behaviour: <see cref="AdventureContent.Register"/> only runs under
    /// ModRegistry.IsEnabled(Plugin.Guid), so with EnableSampleContent off the encounter row is never
    /// registered, GetIntFromID returns a negative value, and the intId &lt; 0 guard below makes this patch
    /// inert. That guard IS the samples-off gate.
    /// </summary>
    [HarmonyPatch(typeof(GameLogic), "GetMiniEncounter")]
    internal static class ForceCustomEncounter_Patch
    {
        private static void Postfix(ref FTK_miniEncounter.ID __result)
        {
            if (Plugin.ForceCustomEncounter == null || !Plugin.ForceCustomEncounter.Value) return;
            if (__result == FTK_miniEncounter.ID.None) return; // nothing was going to spawn here anyway

            // The string id is the source of truth; resolve the synthetic int on demand (the same lookup the
            // registration uses) rather than caching it, so a re-registration can never leave a stale int.
            int intId = Content.Db<FTK_miniEncounterDB>().GetIntFromID(AdventureContent.EncounterId);
            if (intId < 0) return; // not registered (samples off) => inert
            __result = (FTK_miniEncounter.ID)intId;
        }
    }
}
