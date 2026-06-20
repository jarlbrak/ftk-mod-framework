using System;
using System.Collections.Generic;
using GridEditor;
using Newtonsoft.Json.Linq;
using FTKModFramework.Core;

namespace FTKModFramework
{
    /// <summary>
    /// Spec #57 / Slice D1: the bespoke CUSTOM REALM + BOSS demo, the consumer-side deliverable that proves a
    /// modder can ship a self-contained adventure with its OWN realm, its OWN boss, and a true-victory questline,
    /// built ENTIRELY on the public/Core seams already in place this session:
    ///   - <see cref="Content.AddEnemy"/> + <see cref="Content.AttachEnemyProficiencies"/> (the boss enemy, #60)
    ///   - <see cref="RealmBossRegistration.RegisterEnemySet"/> (the boss set that serves both the set-piece and
    ///     the final quest, #60) and <see cref="RealmBossRegistration.RegisterRealm"/> (the realm, #61/#62)
    ///   - <see cref="Adventures.AddCampaignFromTemplate"/> + the <see cref="CampaignBuilder"/>/<see cref="StageBuilder"/>
    ///     (the playable adventure, its final-quest boss bounty, and its last-quest victory, #61/#62)
    /// The FR-1 dictionary-key spike PASSED in-game this session (a synthetic realm int round-trips as the
    /// m_RealmStages decimal-string KEY: "SELF-TEST PASS [realm-spike]"), so the BESPOKE-realm path is in use; no
    /// fallback realm is needed.
    ///
    /// THEME (game-designer + game-decompile-analyst brief, this session): the realm "The Hollow Mire" is a
    /// drowned-worksite poison bog cloned from PoisonBog; the boss "Mudwretch Foreman" is the foreman who kept
    /// the work going after the bog took the crew. Names/flavor display verbatim (literal strings; the game's
    /// Localized&lt;T&gt; passes an unknown key straight through, and the framework's Localization postfixes
    /// substitute our registered name/description).
    ///
    /// AUTHORING SHAPE (decision, grounded in CampaignBuilder.cs):
    ///   The CampaignBuilder ctor SNAPSHOTS m_Stages[0] as its per-stage clone template, then CLEARS the live
    ///   m_Stages array; every AddStage() deep-clones that snapshot. So the builder NEVER appends to the template
    ///   stage: it rebuilds the campaign from a clone of the (configureJson-modified) stage 0. We exploit that:
    ///   configureJson REPLACES m_Stages with ONE bespoke stage whose m_RealmStages holds ONLY the custom realm
    ///   (keyed by its decimal-int, the spike-proven shape) flagged m_GameStartRealm=true with the custom set as
    ///   m_BossEnemy and a realistic POI mix copied from DungeonCrawl's PoisonBog RealmProperties; then the builder
    ///   authors the questline over a clone of THAT stage, so every authored quest inherits the custom realm. This
    ///   keeps configureCampaign != null so the load-time QuestValidator pre-pass runs (and reports 0 errors). The
    ///   whole run happens in The Hollow Mire (single game-start realm), so the boss set-piece and the final boss
    ///   bounty are both reachable.
    ///
    /// SET-PIECE vs FINAL-QUEST (one set serves both, decompile-verified):
    ///   - Set-piece: FTKHex (1337) reads realmProperties.m_BossEnemy, FTK_enemySetDB.Get(setInt).GetEnemySet(),
    ///     and spawns the (single-enemy) set at the realm's FURTHEST hex via a decimal-string RPC.
    ///   - Final quest: a BountyQuestDef with m_EnemySet=setInt + m_SpecifiedRealm=realmInt, the LAST quest of the
    ///     LAST stage, so its completion fires victory (with m_EndGameAfterLastQuest=true).
    ///
    /// Idempotent (guarded like the other demos) and gated by EnableSampleContent (it registers a real, selectable
    /// adventure). Emits exactly two self-test lines, each wrapped so a failure never throws out of registration:
    ///   SELF-TEST PASS [realm-boss-set] (covers #60) and SELF-TEST PASS [realm-boss] (covers #61 + #62 at load).
    /// </summary>
    internal static class RealmBossAdventure
    {
        // ---- ids (all string keys; the helpers/IdAllocator mint the synthetic ints) -----------------------
        private const string BossId = "ftkmf_mudwretch_foreman";
        private const string SetId = "ftkmf_mudwretch_set";
        private const string RealmId = "ftkmf_hollow_mire";

        private const string SaveFileName = "HollowMire";
        private const string DisplayName = "The Hollow Mire";
        private const string RealmDisplay = "The Hollow Mire";
        private const string Template = "DungeonCrawl";

        // The narrative speaker: a custom UserNPC shipped with this adventure (data-only, no Harmony patch). Her
        // folder name IS the key the game and the WithStartStory/WithCompleteStory npcKey reference. The portrait is
        // embedded in this assembly and extracted to npcs/<NpcKey>/portrait.png at Register() time; the framework
        // ships a single DLL. NpcPortraitResource must match the EmbeddedResource name (RootNamespace + asset path).
        private const string NpcKey = "reeve_maddow";
        private const string NpcName = "Reeve Maddow";
        private const string NpcTitle = "Warden of the Hollow Mire";
        private const string NpcPortraitResource = "FTKModFramework.assets.npcs.reeve_maddow.portrait.png";

        // The game intro (m_HasTextIntro gate + title/body), shown on a new game. Body renders verbatim.
        private const string IntroTitle = "The Hollow Mire";
        private const string IntroBody =
            "A generation ago, men came to the Mire to dig, and the Mire dug back. The works flooded, the whistle " +
            "drowned, and the crew was never counted out. Now the water has grown a taste for the living, and the " +
            "village upstream is running short of neighbors.";

        // The boss display name + a wry, lightly-grim FTK-tone description (shown verbatim).
        private const string BossDisplay = "Mudwretch Foreman";
        private const string BossDescription =
            "He kept the dig going long after the bog took the crew. Still clocking everyone in, still expecting " +
            "a full shift. The whistle never stopped; neither did he.";

        // Chassis: PRIMARY swampmonsterA (on-theme bog beast); FALLBACK banditA (the proven Cutpurse chassis).
        // One-line switchable: the demo picks swampmonsterA unless its cloned m_EnemyAsset is unusable at
        // registration, in which case it re-clones from banditA (see PickAndBuildBoss).
        private const FTK_enemyCombat.ID PrimaryChassis = FTK_enemyCombat.ID.swampmonsterA;
        private const FTK_enemyCombat.ID FallbackChassis = FTK_enemyCombat.ID.banditA;

        // Signature procs: a DoT ("the mire poisons you") + an armor shred ("drags your guard down"). Confirmed
        // FTK_proficiencyTable.ID members (FTK_proficiencyTable.cs lines 26, 257).
        private static readonly string[] BossProficiencies = { "enPoison2", "enArmorDestroy" };

        // Stage + quest ids (m_StageLookup / m_QuestLookup keys; unique across the campaign).
        private const string Stage1 = "FtkmfHollowMire_Stage1";
        private const string Q1Visit = "ftkmf_hollowmire_arrive";   // arrive at the drowned worksite
        private const string Q2Dungeon = "ftkmf_hollowmire_crypt";  // clear the Flooded Crypt (the sunken works)
        private const string Q3Boss = "ftkmf_hollowmire_foreman";    // the boss bounty (VICTORY)

        // The realm's main dungeon (a valid FTK_dungeonEncounter.ID, placed by map-gen since m_MainDungeon is set
        // and m_LimitMainDungeons is off). A Clear-Dungeon quest is the legible middle beat: DungeonQuestLogic
        // finds this placed dungeon by id, gives the quest a marked destination, and completes when it is cleared.
        // (Replaces the earlier MiniEncounter/TreasureChest quest, which tracked one designated chest hex and so
        // did not complete when the player opened other overworld chests.)
        private const string MidDungeon = "FloodedCrypt";            // FTK_dungeonEncounter.ID (the realm's m_MainDungeon)

        // Captured synthetic ints (filled during Register, read by the self-tests).
        private static int _bossInt = -1;
        private static int _setInt = -1;
        private static int _realmInt = -1;
        private static string _chassisUsedName;   // "swampmonsterA" or "banditA", for the self-test log line
        private static bool _done;

        public static void Register()
        {
            if (_done) return;
            _done = true;

            // 1) The boss enemy + its signature procs (#60). Built first so the set can target it by int.
            FTK_enemyCombat boss = PickAndBuildBoss();

            // 2) The boss enemy SET (one set serves both the overworld set-piece and the final quest). Cloned from
            //    bounty1A (m_Type stays Bounty, NOT GenericBoss), all party arrays filled with the single boss so
            //    GetEnemySet() returns the boss for every player-count/difficulty path at map gen.
            _bossInt = Content.Db<FTK_enemyCombatDB>().GetIntFromID(BossId);
            FTK_enemyCombat.ID bossEnumId = (FTK_enemyCombat.ID)_bossInt;
            RealmBossRegistration.RegisterEnemySet(
                Plugin.Guid, SetId, FTK_enemySet.ID.bounty1A,
                s =>
                {
                    s.m_HalfParty = new FTK_enemyCombat.ID[] { bossEnumId };       // solo (1-3 player) path
                    s.m_FullPartyNormal = new FTK_enemyCombat.ID[] { bossEnumId };
                    s.m_FullPartyEasy = new FTK_enemyCombat.ID[] { bossEnumId };
                    // Keep m_Type as the cloned Bounty (NOT GenericBoss: that is the only set path that would route
                    // through the unpatched FTK_enemySet.GetEnum, which cannot see a synthetic id).
                });
            _setInt = Content.Db<FTK_enemySetDB>().GetIntFromID(SetId);

            // 3) The custom REALM, cloned from PoisonBog (inherits bog art/audio/tiles + m_DLC=None so the caster
            //    table's DLC gate passes). It is its own realm, not part of PoisonBog's group.
            RealmBossRegistration.RegisterRealm(
                Plugin.Guid, RealmId, FTK_realm.ID.PoisonBog,
                r => { r.m_PartOf = new FTK_realm.ID[0]; });
            _realmInt = Content.Db<FTK_realmDB>().GetIntFromID(RealmId);

            // Give the synthetic realm a real in-world name. With no enum name, the game renders the realm from
            // the raw key "STR_<int>Display"; the framework's concrete-caller postfixes (HexLand.GetRealmDisplayValue
            // + QuestLogicBase.GetMessageParams in Core/Localization) substitute this name. We deliberately do NOT
            // patch the generic FTKHub.Localized<T>: a generic-method patch corrupts Mono's shared generic code body
            // and blanks every text table. This call touches only the public Localization API, no engine internals.
            Localization.SetRealmName(_realmInt, RealmDisplay);

            // 4) The playable ADVENTURE: clone DungeonCrawl, replace its stage with one bespoke custom-realm stage
            //    (configureJson), then author the questline over it (builder). configureCampaign != null keeps the
            //    QuestValidator load pre-pass engaged.
            GameDefinitionPreview preview = Adventures.AddCampaignFromTemplate(
                Plugin.Guid, SaveFileName, Template,
                DisplayName,
                "Drowned timber and a whistle that won't quit. The Hollow Mire swallowed a whole worksite, crew " +
                "and all, but someone is still keeping the shift. Wade in, find the Foreman, and clock him out.",
                configure: Author,
                configureJson: BuildHollowMireStage);

            // 5) The narrative SPEAKER: ship a custom UserNPC ("Reeve Maddow") scoped to THIS adventure. This gives
            //    the adventure its OWN writable mod folder (next to the DLL), writes the bare UserNPC JSON +
            //    extracts the embedded portrait into npcs/<NpcKey>/, and repoints preview.m_ModFolderPath there so
            //    the game scans her at game start. Data-only (no Harmony patch); the attract art is already an
            //    in-memory Sprite, so repointing the folder does not lose the preview image. Guarded internally.
            Adventures.RegisterUserNpc(preview, "HollowMire", NpcKey, NpcName, NpcTitle, NpcPortraitResource);

            // Self-tests (each fully wrapped; a FAIL is logged, never thrown).
            SelfTestRealmBossSet(boss);
            SelfTestRealmBoss(preview);
        }

        // ---- #60: the boss enemy + procs --------------------------------------------------------------------

        /// <summary>
        /// Choose the chassis, then build the boss EXACTLY ONCE (registering twice under the same id would append a
        /// duplicate DB row). PRIMARY is swampmonsterA (on-theme bog beast); we inspect its TEMPLATE row first and
        /// fall back to banditA (the proven Cutpurse body) only if the primary template's render/fight assets
        /// (m_EnemyAsset / m_WeaponAsset) are missing, since the boss renders from m_EnemyAsset and we attach its
        /// signature procs to m_WeaponAsset. One-line switchable via <see cref="PrimaryChassis"/>.
        /// </summary>
        private static FTK_enemyCombat PickAndBuildBoss()
        {
            FTK_enemyCombatDB db = Content.Db<FTK_enemyCombatDB>();
            FTK_enemyCombat primaryTmpl = db.GetEntry(PrimaryChassis);
            bool primaryUsable = primaryTmpl != null && primaryTmpl.m_EnemyAsset != null
                                 && primaryTmpl.m_WeaponAsset != null;

            FTK_enemyCombat.ID chassis = primaryUsable ? PrimaryChassis : FallbackChassis;
            if (!primaryUsable)
                Plugin.Log.LogWarning("[realm-boss] primary chassis " + PrimaryChassis +
                    " is unusable (template m_EnemyAsset/m_WeaponAsset null); falling back to " + FallbackChassis + ".");

            return BuildBoss(chassis);
        }

        private static FTK_enemyCombat BuildBoss(FTK_enemyCombat.ID chassis)
        {
            _chassisUsedName = chassis.ToString();   // record the chassis we actually clone from, for the log
            FTK_enemyCombat boss = Content.AddEnemy(
                Plugin.Guid, BossId, chassis, BossDisplay,
                e =>
                {
                    // A PLACED boss (not cache-drawn): the ambient spawn-pool filter (which rejects m_IsBoss) does
                    // not apply to a set-piece/bounty spawn, so we keep it a real boss.
                    e.m_IsBoss = true;
                    e.m_IsScourge = false;

                    // Durability-and-attrition posture: ~2.5-3.5x a normal Tier2-4 enemy (~24-32 HP).
                    e.m_HealthTotal = 80;

                    // ASYMMETRIC defense (the counterplay lever): the Foreman is a waterlogged bruiser, soaking
                    // physical blows but brittle to magic. Lean on spells / elemental hits to crack him.
                    e.m_BaseDefPhys = 6;
                    e.m_BaseDefMag = 1;

                    // Fire its signature procs ~40% of its turns (so the poison DoT + armor shred actually land),
                    // picking uniformly across its actions rather than always its first.
                    e.m_ChanceToProf = 0.4f;
                    e.m_UseFirstProfAsReg = false;

                    // Modestly upgraded reward for a boss kill (AddEnemy already deep-copied m_ItemDrops, so these
                    // mutate a private copy; the chassis's vanilla loot table is untouched).
                    if (e.m_ItemDrops != null)
                    {
                        e.m_ItemDrops._golddrop = 120;
                        e.m_ItemDrops._itemdropcount = 2;
                        e.m_ItemDrops._itemdropchance = 0.75f;
                    }
                });

            if (boss != null)
            {
                Localization.SetEnemyDescription(BossId, BossDescription);
                // Attach the signature procs to a PRIVATE copy of the chassis weapon (the chassis's vanilla
                // weapon is untouched). If an id fails to resolve at runtime the helper logs and proceeds with
                // whichever attaches; both are confirmed-present FTK_proficiencyTable.ID keys.
                Content.AttachEnemyProficiencies(boss, BossProficiencies);
            }
            return boss;
        }

        // ---- the questline (over the bespoke custom-realm stage) --------------------------------------------

        /// <summary>
        /// Author a short interest-curve questline (designer brief: a handful of quests ending on the boss) over a
        /// clone of the bespoke custom-realm stage. Every destination is the custom realm (its decimal-int string);
        /// the LAST quest is the boss bounty (m_EnemySet=setInt), so completing it fires victory.
        /// </summary>
        private static void Author(CampaignBuilder campaign)
        {
            string realm = _realmInt.ToString(); // the synthetic realm as a decimal-int string (no enum name exists)
            string set = _setInt.ToString();     // the synthetic boss set as a decimal-int string

            StageBuilder stage = campaign.AddStage(Stage1);

            // Q1 (reach): wade into the drowned worksite. A plain Visit resolves to the realm capital. Maddow sets
            // the hook on quest start (3 popup pages, her portrait/name/title verbatim).
            stage.AddVisitQuest(Q1Visit, realm)
                .WithStartStory(NpcKey,
                    "Visitors. Good. The Mire's been eating my census, and I am tired of crossing out names.",
                    "Four gone in a fortnight. No bodies, no struggle, just empty beds and wet footprints leading " +
                    "the wrong way: toward the old works, not away from them.",
                    "Folk say it is a shade out of the drowned crypts, come to collect. I say a thing that takes " +
                    "people can be made to give them back. Follow the wet prints down.");

            // Q2 (clear): descend into the Flooded Crypt, the sunken works. A Clear-Dungeon objective resolves to
            // the realm's placed main dungeon (a marked, obvious POI) and completes when it is cleared. Maddow
            // narrates the descent on quest start (3 pages).
            stage.AddClearDungeonQuest(Q2Dungeon, MidDungeon, realm)
                .WithStartStory(NpcKey,
                    "This is as far as my authority reaches. Below here it is the works' jurisdiction, and the " +
                    "works do not recognize me.",
                    "Listen for water that moves on its own. The drowned do not drift down here, they march. " +
                    "Whatever is taking my people, it has them doing something.",
                    "They are not dead down there. They are working, clocked in by lantern-rot, waiting on a " +
                    "whistle. Find who is blowing it.");

            // Q3 (kill): the Mudwretch Foreman bounty, targeting the custom set in the custom realm. LAST quest of
            // the LAST stage -> victory on completion (with m_EndGameAfterLastQuest=true, set in configureJson).
            // Maddow names the boss on start (2 pages) and gives the victory beat on complete (2 pages).
            stage.AddKillQuest(Q3Boss, set, realm)
                .WithStartStory(NpcKey,
                    "At the bottom of the works there is a foreman who never clocked out. He kept the dig going " +
                    "long after the bog took his crew, and now he fills the empty slots with the living.",
                    "He keeps a ledger, they say, and your names are already on it. Go and strike his out first.")
                .WithCompleteStory(NpcKey,
                    "Four out, all breathing. First time in a fortnight I am adding names instead of striking them.",
                    "The whistle is quiet now. Latch your doors upstream tonight, just the same.");
        }

        // ---- the bespoke custom-realm stage (configureJson) ------------------------------------------------

        /// <summary>
        /// Replace the cloned GameDefinition's m_Stages with ONE bespoke stage whose m_RealmStages holds ONLY the
        /// custom realm (keyed by its decimal-int, the spike-proven dictionary-key shape) flagged m_GameStartRealm
        /// with the custom set as m_BossEnemy and a realistic POI mix copied from DungeonCrawl's PoisonBog
        /// RealmProperties. Also stamps m_EndGameAfterLastQuest=true (true victory) and leaves m_OceanRealmID alone
        /// ("Ocean" in the template; never collides with the synthetic realm int). The single template stage's
        /// generic (None) map-layout casters carry over and cover the one custom realm via the caster table's
        /// second-pass assignment (GameDefinition._createRealmCasterTable), so the realm generates hexes.
        ///
        /// Runs BEFORE the CampaignBuilder snapshots stage 0, so the builder's per-stage clone template IS this
        /// bespoke stage and every authored quest inherits the custom realm key.
        /// </summary>
        private static void BuildHollowMireStage(JObject jo)
        {
            // True victory on the last quest.
            jo["m_EndGameAfterLastQuest"] = true;

            // The new-game text intro (m_HasTextIntro gate + title/body). Body/title render VERBATIM on a new game
            // (GameDefinition.GetIntroTitle/Body resolve via Localized<TextStory> -> GetUserModText, pass-through on
            // a miss). This is the opener before the first quest.
            jo["m_HasTextIntro"] = true;
            jo["m_IntroTitle"] = IntroTitle;
            jo["m_IntroBody"] = IntroBody;

            // CRITICAL (single-realm map-gen): DungeonCrawl ships m_LimitMainDungeons=true with
            // m_LimitMainDungeonsAmount=5 (it has 9 realms). GenerateHexGrid._buildHexCoroutine then runs
            // `while (allowedMainDungeonsInRealmStages.Count < 5)` picking DISTINCT realm-stages that have a main
            // dungeon; a one-realm stage saturates that dedup list at 1, so 1 < 5 loops FOREVER (a silent 99% CPU
            // hang right after "GenerateMap: Coroutine Start", with no yield/log). Turning the limit off skips the
            // while-block (the else-branch just takes all realm-stages). This is what the vanilla single-realm
            // adventures GraveRobber and HildebrantsCellar do.
            jo["m_LimitMainDungeons"] = false;

            // No ocean realm: the Mire is one land realm that fills the map. Leaving the inherited
            // m_OceanRealmID=Ocean would invite the ocean/port POI passes to resolve an "Ocean" realm that is not a
            // key in m_RealmStages. None disables those passes cleanly (GraveRobber does the same).
            jo["m_OceanRealmID"] = "None";

            string realmKey = _realmInt.ToString(); // decimal-string dictionary KEY for the synthetic realm

            // The bespoke RealmProperties: a realistic POI mix copied from PoisonBog, marked as the game-start
            // realm, with the custom set as the overworld set-piece boss (written as the decimal int, the spike's
            // m_BossEnemy shape). m_UseTypicalOverworldProperties=true reuses the realm row's own overworld props
            // (inherited from PoisonBog), so no override object is needed.
            JObject realmProps = new JObject();
            realmProps["$type"] = "RealmProperties, Assembly-CSharp";
            realmProps["m_GameStartRealm"] = true;                 // the start realm: the whole run is in the Mire
            realmProps["m_RealmSize"] = 6;                          // a touch larger than PoisonBog's 5 for the arc
            realmProps["m_TownsToSpawn"] = 2;
            realmProps["m_IsolateFromOtherRealms"] = false;
            realmProps["m_GenerateIslands"] = false;               // no water features (no ocean realm)
            realmProps["m_FillMap"] = true;                        // single realm: fill the map with it
            realmProps["m_BaseEnemyAmount"] = 7;
            realmProps["m_UseTypicalOverworldProperties"] = true;  // reuse the realm row's overworld props
            realmProps["m_BossEnemy"] = _setInt;                   // the custom set, as the decimal int (set-piece)
            realmProps["m_MainDungeon"] = "FloodedCrypt";          // PoisonBog's main dungeon (valid, on-theme)
            realmProps["m_MiniDungeons"] = new JArray("Crypt");
            realmProps["m_Haunts"] = 1;
            realmProps["m_Sanctums"] = 1;
            realmProps["m_StoneHeroes"] = 1;
            realmProps["m_ReduceChaosPOIs"] = 1;
            realmProps["m_FairyFountains"] = 1;
            realmProps["m_DarkCarnivals"] = 1;
            realmProps["m_NightMarkets"] = 1;
            realmProps["m_GamblingDens"] = 0;
            realmProps["m_StoneTables"] = 1;
            realmProps["m_LocalArenas"] = 1;
            realmProps["m_HasPorts"] = false;             // no ocean realm => no ports
            realmProps["m_HasAlluringPools"] = false;     // no water features
            realmProps["m_EnemyCampChance"] = 0.2;
            realmProps["m_MimicChance"] = 0.25;
            realmProps["m_TownCostMultiplier"] = 1;
            realmProps["m_TownOverride"] = null;
            // The bog's poison-hazard def, copied from PoisonBog (it is what makes the Mire a mire).
            JObject hazard = new JObject();
            hazard["$type"] = "RealmPoisonDefinition, Assembly-CSharp";
            hazard["m_StartHexCount"] = 6;
            hazard["m_FinalHexCount"] = 15;
            hazard["m_LifeTimeMin"] = 2;
            hazard["m_LifeTimeMax"] = 5;
            realmProps["m_HazardHexDefs"] = new JArray(hazard);

            // The custom realm keyed by its decimal-int string (the spike-proven dictionary-key conversion).
            JObject realmStages = new JObject();
            realmStages[realmKey] = realmProps;

            // The bespoke stage. We clone the template stage 0's scalar scaffolding (progression fields, allocated
            // rounds, etc.) so the stage stays valid, then replace m_RealmStages + the (cosmetic, unused) realm
            // start filter and clear its quests (the builder fills them). m_RealmStartFilter is declared on
            // GameStage but referenced NOWHERE in the assembly, so its value is inert; we set it to the custom
            // realm for hygiene.
            JArray stages = jo["m_Stages"] as JArray;
            if (stages == null || stages.Count == 0)
            {
                Plugin.Log.LogError("[realm-boss] template '" + Template + "' has no m_Stages to build the bespoke stage from.");
                return;
            }
            JObject stage = (JObject)stages[0].DeepClone();
            stage["m_ThisStageID"] = Stage1;
            stage["m_RealmStages"] = realmStages;
            stage["m_RealmStartFilter"] = new JArray(_realmInt); // inert but coherent
            stage["m_Quests"] = new JArray();                     // the builder authors these

            // Drop DungeonCrawl's STAGE-LEVEL narrative (the cloned stage carries them): m_StageStartEvents is the
            // Queen "Rosomon" / chaos-generator intro (STR_dungeonCrawlQueenIntro), m_StageCompleteEvents its
            // stage-end beat. Without clearing these, the Queen's stock chaos-generator message plays alongside our
            // Reeve Maddow narrative. Our own story lives on the quests (m_StartEvents via WithStartStory) + the game
            // intro (m_IntroBody), so the stage-level lists must be emptied for The Hollow Mire to read as its own.
            stage["m_StageStartEvents"] = new JArray();
            stage["m_StageCompleteEvents"] = new JArray();

            JArray newStages = new JArray();
            newStages.Add(stage);
            jo["m_Stages"] = newStages;
        }

        // ---- #60 self-test: the boss enemy + the set -------------------------------------------------------

        /// <summary>
        /// SELF-TEST PASS [realm-boss-set]: the boss enemy + set resolve by int, the set's m_HalfParty is
        /// non-empty, m_Type != GenericBoss, and the boss carries its attached procs. Wrapped so it never throws.
        /// </summary>
        private static void SelfTestRealmBossSet(FTK_enemyCombat boss)
        {
            try
            {
                FTK_enemyCombatDB enemyDb = Content.Db<FTK_enemyCombatDB>();
                FTK_enemySetDB setDb = Content.Db<FTK_enemySetDB>();

                FTK_enemyCombat byInt = enemyDb.GetEntryByInt(_bossInt);
                FTK_enemySet set = setDb.GetEntryByInt(_setInt);

                bool bossResolves = _bossInt >= 0 && byInt != null;
                bool setResolves = _setInt >= 0 && set != null;
                bool halfPartyNonEmpty = set != null && set.m_HalfParty != null && set.m_HalfParty.Length > 0;
                bool notGenericBoss = set != null && set.m_Type != EnemySetType.GenericBoss;
                bool setTargetsBoss = halfPartyNonEmpty && (int)set.m_HalfParty[0] == _bossInt;

                // The procs live on the boss's private weapon copy; confirm via the same instantiate path the game
                // uses. Best-effort + guarded (the proficiency DB can be null at early load).
                int profCount = -1; bool hasProcs = false; bool procChecked = false;
                try
                {
                    FTK_proficiencyTableDB profDb = TableManager.Instance != null
                        ? TableManager.Instance.Get<FTK_proficiencyTableDB>() : null;
                    if (boss != null && boss.m_WeaponAsset != null && profDb != null)
                    {
                        Weapon w = UnityEngine.Object.Instantiate(boss.m_WeaponAsset);
                        List<FTK_proficiencyTable.ID> ids = w.GetProficiencyIDs();
                        profCount = ids.Count;
                        int attached = 0;
                        foreach (string pid in BossProficiencies)
                        {
                            int pi = profDb.GetIntFromID(pid);
                            if (pi >= 0 && ids.Contains((FTK_proficiencyTable.ID)pi)) attached++;
                        }
                        hasProcs = attached > 0; // helper proceeds with whichever attaches; >=1 is the gate
                        procChecked = true;
                        UnityEngine.Object.Destroy(w.gameObject);
                    }
                }
                catch (Exception pe)
                {
                    Plugin.Log.LogWarning("[realm-boss-set] proc check deferred (tables not ready at load): " + pe.Message);
                }

                bool contentOk = boss != null && bossResolves && setResolves && halfPartyNonEmpty
                                 && notGenericBoss && setTargetsBoss;
                bool procOk = !procChecked || hasProcs;

                if (contentOk && procOk)
                    Plugin.Log.LogInfo("SELF-TEST PASS [realm-boss-set]: boss '" + BossId + "' (int=" + _bossInt +
                        ", chassis=" + ChassisName() + ", HP=" + (boss != null ? boss.m_HealthTotal : -1) +
                        ", defPhys=" + (boss != null ? boss.m_BaseDefPhys : -1) + "/defMag=" +
                        (boss != null ? boss.m_BaseDefMag : -1) + ") + set '" + SetId + "' (int=" + _setInt +
                        ") resolve by int; set m_HalfParty=" + (set != null ? set.m_HalfParty.Length : -1) +
                        " targets the boss, m_Type=" + (set != null ? set.m_Type.ToString() : "null") +
                        " (!= GenericBoss); boss procs on weapon=" +
                        (procChecked ? hasProcs.ToString() + " (" + profCount + " actions)" : "deferred") + ".");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-boss-set]: bossInt=" + _bossInt +
                        " bossResolves=" + bossResolves + " setInt=" + _setInt + " setResolves=" + setResolves +
                        " halfPartyNonEmpty=" + halfPartyNonEmpty + " setTargetsBoss=" + setTargetsBoss +
                        " notGenericBoss=" + notGenericBoss + " procs=" +
                        (procChecked ? hasProcs.ToString() : "deferred") + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [realm-boss-set]: " + e);
            }
        }

        // ---- #61 + #62 self-test: the adventure / realm / set-piece / victory at LOAD ----------------------

        /// <summary>
        /// SELF-TEST PASS [realm-boss]: the HollowMire adventure registered + is whitelisted/selectable; its
        /// authored GameDefinition deserializes with the game's settings; GetRealmProperties((FTK_realm.ID)realmInt,
        /// 0) returns properties whose m_BossEnemy == (FTK_enemySet.ID)setInt (the overworld set-piece); and the
        /// last quest of the last stage is a BountyQuestDef whose m_EnemySet == (FTK_enemySet.ID)setInt; reports
        /// whether m_EndGameAfterLastQuest is true. The full playthrough-to-victory is a manual in-game gate.
        /// Wrapped so it never throws.
        /// </summary>
        private static void SelfTestRealmBoss(GameDefinitionPreview preview)
        {
            try
            {
                bool registered = preview != null && !string.IsNullOrEmpty(preview.m_FullFileData);
                bool whitelisted = Adventures.IsWhitelisted(SaveFileName);

                if (!registered)
                {
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-boss]: AddCampaignFromTemplate returned " +
                        (preview == null ? "null (template '" + Template + "' not installed?)" : "no m_FullFileData") +
                        " for adventure '" + DisplayName + "'.");
                    return;
                }

                // Deserialize the authored GameDefinition with the EXACT game settings (TypeNameHandling.Auto +
                // StringEnumConverter), as GameDefJSONMapper.Start does, so the synthetic realm/set ints resolve.
                GameDefinition gd;
                try
                {
                    Newtonsoft.Json.JsonSerializerSettings settings = new Newtonsoft.Json.JsonSerializerSettings();
                    settings.TypeNameHandling = Newtonsoft.Json.TypeNameHandling.Auto;
                    settings.Converters.Add(new Newtonsoft.Json.Converters.StringEnumConverter());
                    gd = Newtonsoft.Json.JsonConvert.DeserializeObject<GameDefinition>(preview.m_FullFileData, settings);
                }
                catch (Exception de)
                {
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-boss]: GameDefinition deserialize threw: " + de.Message);
                    return;
                }

                if (gd == null || gd.m_Stages == null || gd.m_Stages.Count == 0)
                {
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-boss]: deserialized GameDefinition/m_Stages is null/empty.");
                    return;
                }

                FTK_realm.ID realmKey = (FTK_realm.ID)_realmInt;
                FTK_enemySet.ID setKey = (FTK_enemySet.ID)_setInt;

                // (a) The overworld set-piece: GetRealmProperties (the game's own read path over the m_RealmStages
                //     dictionary) returns properties whose m_BossEnemy is the custom set.
                RealmProperties props = gd.GetRealmProperties(realmKey, 0);
                bool realmResolves = props != null;
                bool setPieceOk = props != null && props.m_BossEnemy == setKey;
                bool gameStartRealm = props != null && props.m_GameStartRealm;

                // (b) The final-quest boss + true victory: the LAST quest of the LAST stage is a BountyQuestDef
                //     whose m_EnemySet is the custom set, specifying the custom realm.
                GameStage lastStage = gd.m_Stages[gd.m_Stages.Count - 1];
                bool lastQuestOk = false; bool lastQuestRealmOk = false; string lastQuestId = "(none)";
                if (lastStage != null && lastStage.m_Quests != null && lastStage.m_Quests.Count > 0)
                {
                    QuestDefBase last = lastStage.m_Quests[lastStage.m_Quests.Count - 1];
                    BountyQuestDef bounty = last as BountyQuestDef;
                    if (bounty != null)
                    {
                        lastQuestId = bounty.m_StoryQuestID;
                        lastQuestOk = bounty.m_EnemySet == setKey;
                        lastQuestRealmOk = bounty.m_SpecifiedRealm == realmKey;
                    }
                }

                bool endGame = gd.m_EndGameAfterLastQuest;

                bool ok = whitelisted && realmResolves && setPieceOk && lastQuestOk && lastQuestRealmOk;

                if (ok)
                    Plugin.Log.LogInfo("SELF-TEST PASS [realm-boss]: adventure '" + DisplayName + "' (" + SaveFileName +
                        ") registered + whitelisted=" + whitelisted + "; GameDefinition deserialized; realm int=" +
                        _realmInt + " resolves (m_GameStartRealm=" + gameStartRealm +
                        "), GetRealmProperties.m_BossEnemy == set int=" + _setInt + " (overworld set-piece); last " +
                        "quest of last stage '" + lastQuestId + "' is a BountyQuestDef with m_EnemySet==set int (" +
                        _setInt + ") in realm int=" + _realmInt + "; m_EndGameAfterLastQuest=" + endGame +
                        " (true victory). Full playthrough-to-victory is a manual in-game gate.");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-boss]: whitelisted=" + whitelisted +
                        " realmResolves=" + realmResolves + " setPieceMatch=" + setPieceOk +
                        " (m_BossEnemy=" + (props != null ? props.m_BossEnemy.ToString() : "null") +
                        " expected set int=" + _setInt + ") lastQuestIsBountyWithSet=" + lastQuestOk +
                        " lastQuestRealm=" + lastQuestRealmOk + " (lastQuest='" + lastQuestId +
                        "') m_EndGameAfterLastQuest=" + endGame + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [realm-boss]: " + e);
            }
        }

        /// <summary>The chassis the boss was actually cloned from (swampmonsterA or banditA), for the log line.</summary>
        private static string ChassisName()
        {
            return _chassisUsedName ?? "(unknown)";
        }
    }
}
