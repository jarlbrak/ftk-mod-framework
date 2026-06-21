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
    /// BOSS LOOK (visual identity): the Foreman uses a HULKING-HUMANOID chassis (an ordered fallback chain headed
    /// by trollCaveA/ogreA/yetiA, with swampmonsterA/banditA as on-theme/floor fallbacks), then RECOLORED mossy
    /// brown-green and UPSCALED (Content.SetEnemyVisual) so it reads as the victory-screen art: a big mossy bog
    /// brute. The recolor/rescale is visual-only and applied per-combat to the spawned clone (leak-safe), and
    /// determinism/save-safe (enemy visuals never network or persist; only the enum-int id + m_MarkerScale are
    /// shared state, read identically from the same mod DB on every machine).
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

        // The custom end-game (victory) plate, shipped embedded in this assembly and extracted at Register() time to
        // <adventureFolder>/EndGameImage.png, which the gamedef's GetEndGameImage() loads for the flat (non-StoneHero)
        // victory tableau. Must match the EmbeddedResource name (RootNamespace + asset path).
        private const string EndGameImageResource = "FTKModFramework.assets.adventures.hollowmire.EndGameImage.png";

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

        // Chassis: an ORDERED hulking-humanoid fallback CHAIN. PickAndBuildBoss walks it and uses the FIRST
        // chassis whose TEMPLATE row has BOTH a body (m_EnemyAsset) and a weapon (m_WeaponAsset), since the boss
        // renders from m_EnemyAsset and we attach its signature procs to m_WeaponAsset. The first three are big
        // broad-shouldered humanoids (the victory-art read); swampmonsterA is the on-theme bog regression; banditA
        // is the guaranteed-usable floor. The chosen body is then recolored mossy + upscaled (SetEnemyVisual) so
        // it matches the victory-screen brute. Enum members verified (FTK_enemyCombat.ID): trollCaveA(191/ord),
        // ogreA, yetiA, swampmonsterA, banditA.
        private static readonly FTK_enemyCombat.ID[] ChassisChain =
        {
            FTK_enemyCombat.ID.trollCaveA,      // PRIMARY: classic hunched broad-shouldered troll (closest hulk)
            FTK_enemyCombat.ID.ogreA,           // large hunched humanoid
            FTK_enemyCombat.ID.yetiA,           // large humanoid
            FTK_enemyCombat.ID.swampmonsterA,   // on-theme bog beast (safe regression)
            FTK_enemyCombat.ID.banditA,         // floor: guaranteed body + weapon (the proven Cutpurse chassis)
        };

        // ---- visual identity (tunable from the in-engine screenshot WITHOUT a re-brief) --------------------
        // The boss body is recolored mossy brown-green, given a wet-bog skin, hulked into a non-uniform broad-shouldered
        // silhouette, hunched forward, and handed a glowing lantern (the axe is hidden) to read as the victory-screen
        // bog brute. All of this is visual-only (per-combat clone edit, leak-safe, determinism/save-safe; see
        // Content.SetEnemyVisual). Iterate every value below from screenshots without a re-brief.
        //
        // Tuned from the in-crypt screenshot: greener + lower blue than a neutral olive, so it still reads mossy
        // under the Flooded Crypt's cool/dim lighting (a plain olive went muddy-purple in shadow). Multiplies the
        // troll body's Standard-shader albedo (_Color), verified in-engine on material 'matTrollCaveA'.
        private static readonly UnityEngine.Color BossTint = new UnityEngine.Color(0.46f, 0.66f, 0.30f); // mossy brown-green
        private const float BossBodyScale = 1.4f;   // hulking; the "tall" axis; reads well at ~1.4x in the crypt diorama
        private const float BossWidthBoost = 1.12f; // extra x/z multiplier on top of BossBodyScale (>1 = broader than tall)
        // m_MarkerScale (the target/click footprint) tracks the x/z, i.e. BossBodyScale * BossWidthBoost.
        private const float BossMarkerScale = BossBodyScale * BossWidthBoost;

        // Wet-bog skin (Standard shader): high smoothness + low metallic so the body reads waterlogged/slimy. Guarded
        // by HasProperty in the patch, so a non-Standard material is left alone.
        private const float BossSmoothness = 0.6f;  // _Glossiness (0..1); high = slimy
        private const float BossMetallic = 0.0f;    // _Metallic (0..1); low for skin

        // Spine hunch (best-effort): forward rotation (deg) of the first matching spine bone about its local right
        // axis, to exaggerate the hunch. Set to 0 to disable.
        private const float BossHunchDegrees = 14f;

        // Hide the held axe so the boss reads as the lantern-bearer (the art's lantern-not-axe read). Flip to false
        // to show the weapon again.
        private const bool BossHideWeapon = true;

        // Procedural lantern (best-effort): a small emissive cube + warm point light attached to a hand bone.
        // Iteration 1 read green-white / too hot (the Light was left white and the emission was *2). Warmed the
        // albedo + emission, dropped the emission to *1.2, and now drive the point Light with its OWN warm color.
        private static readonly UnityEngine.Color LanternColor =
            new UnityEngine.Color(1.0f, 0.62f, 0.28f);                 // warm amber albedo
        private static readonly UnityEngine.Color LanternEmission =
            new UnityEngine.Color(1.0f, 0.50f, 0.16f) * 1.2f;          // warm orange glow (intensity-scaled, NOT *2)
        private static readonly UnityEngine.Color LanternLightColor =
            new UnityEngine.Color(1.0f, 0.62f, 0.28f);                 // warm point-light color (was white = the blowout)
        private const float LanternSize = 0.16f;                       // desired WORLD size of the lantern body cube
        private const float LanternLightRange = 2.6f;                  // point-light range (units)
        private const float LanternLightIntensity = 0.9f;              // point-light intensity

        // Swamp aura (best-effort): a procedural ParticleSystem of bog flies / marsh gas drifting around the torso.
        // Off-network and never serialized (a per-combat clone child), so it stays determinism/save-safe like the
        // rest of the visual. Flip to false to drop the aura.
        private const bool BossSwampAura = true;

        // Procedural GOLEM body (best-effort): when true, HIDE the chassis' skinned mesh and assemble a runtime
        // low-poly mossy bog-golem from bone-segment + joint-blob meshes parented to the existing skeleton, so the new
        // body animates with the bones. Visual-only / per-combat clone / deterministic (the meshes are generated from
        // index hashes, no Random), so it stays determinism/save-safe like the rest of the visual. Flip to false to
        // show the recolored troll chassis again. Radii are in WORLD units at the (un-scaled) bone, then the cel-root
        // hulking scale (BossBodyScale*BossWidthBoost) scales the whole golem along with the bones.
        // EXPERIMENTAL, default OFF: the procedural golem renders + animates a genuinely custom code-built mesh
        // (proven in-engine), but it is rough "code-art" and at large radii it can occlude the boss's in-rig
        // EncounterCam (a black combat view). The shipped demo therefore defaults to the polished recolored chassis
        // (the better-looking, camera-safe result); flip this to true to show the procedural golem, and tune the
        // radii down first if the combat view goes black. A true high-fidelity model should come via the AssetBundle
        // loader (Content.SetEnemyBodyMesh / SetEnemyBodyFromBundle; see docs/CUSTOM-MODELS.md).
        private const bool BossProceduralBody = false;
        private const float GolemTorsoRadius = 0.32f;  // world radius of the spine/torso segments (thickest; hulking)
        private const float GolemLimbRadius = 0.15f;   // world radius of the arm/leg segments (chunky; tapers down)
        private const float GolemLumpiness = 0.22f;    // 0 = clean prisms; higher = chunkier mossy lumps (index-derived)
        // Sickly bog-green glowing eyes (emissive). Iterate from screenshots without a re-brief.
        private static readonly UnityEngine.Color GolemEyeGlow =
            new UnityEngine.Color(0.55f, 1.0f, 0.35f) * 1.4f;          // sickly green glow (intensity-scaled)

        // Signature procs: a DoT ("the mire poisons you") + an armor shred ("drags your guard down"). Confirmed
        // FTK_proficiencyTable.ID members (FTK_proficiencyTable.cs lines 26, 257).
        private static readonly string[] BossProficiencies = { "enPoison2", "enArmorDestroy" };

        // Stage + quest ids (m_StageLookup / m_QuestLookup keys; unique across the campaign).
        private const string Stage1 = "FtkmfHollowMire_Stage1";
        private const string Q1Visit = "ftkmf_hollowmire_arrive";   // arrive at the drowned worksite
        // Clear the Flooded Crypt: the LAST quest, and the VICTORY. The Foreman is the crypt's final room
        // (m_DungeonDefOverride), so clearing the crypt = beating him = win. The old separate boss bounty is gone.
        private const string Q2Dungeon = "ftkmf_hollowmire_crypt";

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
        private static string _chassisUsedName;   // the chassis actually used (see ChassisChain), for the log line
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

            // 6) The custom VICTORY image: ship a bespoke end-game plate into the SAME adventure folder
            //    (FTKModFramework_content/HollowMire). The flat (non-StoneHero) end-game tableau reads
            //    GetEndGameImage() -> m_ModFolderPath/EndGameImage.* (BuildHollowMireStage forces the flat branch via
            //    m_StoneHeroEndImage=false), so without this the victory screen is blank. Data-only, guarded.
            Adventures.RegisterEndGameImage(preview, "HollowMire", EndGameImageResource);

            // Self-tests (each fully wrapped; a FAIL is logged, never thrown).
            SelfTestRealmBossSet(boss);
            SelfTestRealmBoss(preview);
        }

        // ---- #60: the boss enemy + procs --------------------------------------------------------------------

        /// <summary>
        /// Choose the chassis, then build the boss EXACTLY ONCE (registering twice under the same id would append a
        /// duplicate DB row). Walks the ordered <see cref="ChassisChain"/> and uses the FIRST chassis whose TEMPLATE
        /// row has BOTH a body (m_EnemyAsset) and a weapon (m_WeaponAsset): the boss renders from m_EnemyAsset and
        /// we attach its signature procs to m_WeaponAsset, so a template missing either is unusable. The early
        /// entries are hulking-humanoid bodies (the victory-art read); the chosen body is then recolored + upscaled
        /// (SetEnemyVisual in BuildBoss). banditA at the chain's tail is the guaranteed-usable floor.
        /// </summary>
        private static FTK_enemyCombat PickAndBuildBoss()
        {
            FTK_enemyCombatDB db = Content.Db<FTK_enemyCombatDB>();

            FTK_enemyCombat.ID chassis = ChassisChain[ChassisChain.Length - 1]; // floor default
            bool picked = false;
            for (int i = 0; i < ChassisChain.Length; i++)
            {
                FTK_enemyCombat tmpl = db.GetEntry(ChassisChain[i]);
                bool usable = tmpl != null && tmpl.m_EnemyAsset != null && tmpl.m_WeaponAsset != null;
                if (usable)
                {
                    chassis = ChassisChain[i];
                    picked = true;
                    break;
                }
                Plugin.Log.LogWarning("[realm-boss] chassis " + ChassisChain[i] +
                    " is unusable (template m_EnemyAsset/m_WeaponAsset null); trying the next in the chain.");
            }
            if (!picked)
                Plugin.Log.LogWarning("[realm-boss] no chassis in the chain was usable; using the floor " + chassis + ".");

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

                    // Match the up-scaled body's click/target collider footprint (X/Z). Set on the FRAMEWORK'S
                    // OWN cloned row via the AddEnemy configure path, NOT on a vanilla row. m_MarkerScale is the
                    // only shared/persisted visual-adjacent field, read identically from the same mod DB on every
                    // machine (so it stays determinism/save-safe).
                    e.m_MarkerScale = BossMarkerScale;

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

                // Custom VISUAL identity: recolor the body mossy brown-green, give it a wet-bog skin, hulk it into a
                // non-uniform broad silhouette, hunch it forward, hide the axe, and hand it a glowing lantern so the
                // chosen hulking-humanoid chassis reads as the victory-screen bog brute. Applied per-combat to the
                // spawned clone (leak-safe; determinism/save-safe: enemy visuals never network or persist).
                EnemyVisualPatch.EnemyVisual visual = default(EnemyVisualPatch.EnemyVisual);
                visual.tint = BossTint;
                visual.scale = BossBodyScale;
                visual.widthBoost = BossWidthBoost;
                visual.applyWetSkin = true;
                visual.smoothness = BossSmoothness;
                visual.metallic = BossMetallic;
                visual.hunchDegrees = BossHunchDegrees;
                visual.hideWeapon = BossHideWeapon;
                visual.addLantern = true;
                visual.lanternColor = LanternColor;
                visual.lanternEmission = LanternEmission;
                visual.lanternLightColor = LanternLightColor;
                visual.lanternSize = LanternSize;
                visual.lanternLightRange = LanternLightRange;
                visual.lanternLightIntensity = LanternLightIntensity;
                visual.swampAura = BossSwampAura;
                // PROCEDURAL GOLEM BODY: hide the troll chassis mesh and build a runtime low-poly mossy bog-golem from
                // bone-segment + joint-blob meshes parented to the skeleton (animates with the bones). The recolor /
                // wet-skin above now harmlessly tints a hidden renderer; the lantern + aura + hunch still read on top.
                visual.proceduralBody = BossProceduralBody;
                visual.golemTorsoRadius = GolemTorsoRadius;
                visual.golemLimbRadius = GolemLimbRadius;
                visual.golemLumpiness = GolemLumpiness;
                visual.golemEyeGlow = GolemEyeGlow;
                Content.SetEnemyVisual(boss, visual);
            }
            return boss;
        }

        // ---- the questline (over the bespoke custom-realm stage) --------------------------------------------

        /// <summary>
        /// Author a short interest-curve questline (designer brief: arrive, then descend the crypt to face the boss)
        /// over a clone of the bespoke custom-realm stage. Every destination is the custom realm (its decimal-int
        /// string). The LAST quest is the crypt-clear: the Foreman now lives at the bottom of the Flooded Crypt
        /// (the m_DungeonDefOverride in configureJson), so clearing the crypt = beating the Foreman = victory.
        /// Victory fires when the last quest of the last stage completes, regardless of quest TYPE (decompile-verified:
        /// QuestLogicBase.m_IsLastQuest -> GameEventManager "complete"); there is NO separate boss bounty anymore.
        /// </summary>
        private static void Author(CampaignBuilder campaign)
        {
            string realm = _realmInt.ToString(); // the synthetic realm as a decimal-int string (no enum name exists)

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

            // Q2 (clear = VICTORY): descend the Flooded Crypt and face the Foreman in its final room. The boss is
            // the crypt's last fight (configureJson dungeon override), so a Clear-Dungeon objective bound to the
            // realm's placed main dungeon (a marked, obvious POI) completes when the crypt is cleared, which means
            // the Foreman is down. This is the LAST quest of the LAST stage, so its completion fires victory (with
            // m_EndGameAfterLastQuest=true, set in configureJson, controlling the post-credits final-click). Maddow
            // narrates the descent and NAMES the Foreman waiting below on start, and gives the victory beat on
            // complete (the existing victory text, kept verbatim).
            stage.AddClearDungeonQuest(Q2Dungeon, MidDungeon, realm)
                .WithStartStory(NpcKey,
                    "This is as far as my authority reaches. Below here it is the works' jurisdiction, and the " +
                    "works do not recognize me.",
                    "Listen for water that moves on its own. The drowned do not drift down here, they march. " +
                    "Whatever is taking my people, it has them doing something.",
                    "Find who is blowing it. He is at the bottom of the works, where the water never drained. " +
                    "Everything in this Mire that is wrong runs uphill from that one room.",
                    "You are standing in his works now. Down the last stair the whistle is loudest. Go and meet " +
                    "the man keeping the shift.")
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

            // Force the FLAT custom-image victory branch. GameEventManager.ShowStoneHero_CR branches on
            // m_StoneHeroEndImage: when FALSE it shows CreditScreen.m_EndGameImage.sprite = GetGameDef()
            // .GetEndGameImage(), which loads m_ModFolderPath/EndGameImage.* (our shipped victory plate, written by
            // Adventures.RegisterEndGameImage). It defaults false, but we set it explicitly so the intent is durable
            // against template drift. (m_EndMessageID is deliberately NOT set: it only gates an EndGame tutorial
            // popup, not free text.)
            jo["m_StoneHeroEndImage"] = false;

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
            // NO roaming overworld set-piece boss. FTKHex spawns the set-piece UNCONDITIONALLY at map-gen whenever
            // m_BossEnemy != None (no quest gate), which let the player walk up and kill the Foreman on the
            // overworld before ever entering the crypt, and that kill did not win (victory is the last quest). The
            // Foreman now lives ONLY at the bottom of the Flooded Crypt (the m_DungeonDefOverride below), so the
            // overworld set-piece is removed. "None" makes FTKHex skip the spawn block entirely.
            realmProps["m_BossEnemy"] = "None";                    // no overworld set-piece; the boss is in the crypt
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

            // Put the Foreman at the BOTTOM of the Flooded Crypt (a per-adventure dungeon override). This is the
            // clean, data-only lever: MiniHexDungeon.GetDungeonDefinition resolves m_DungeonDefOverride[<dungeon id>]
            // BEFORE the type-keyed m_DungeonDefType[Main], so this affects ONLY FloodedCrypt in ONLY this adventure
            // (vanilla FloodedCrypt elsewhere is untouched; it is a real FTK_dungeonEncounter.ID, not a clone). The
            // override is a deep clone of the template's Main def (the dungeon shape FloodedCrypt would otherwise use)
            // with its final BossEnemy room rewritten from the stock Generic boss to our SPECIFIC set, so the crypt's
            // last fight IS the Foreman. The Specific path reads FTK_enemySetDB.Get((ID)m_BossSet) (a dictionary
            // lookup, no GetEnum(string)), so the synthetic set int resolves, exactly as the bounty m_EnemySet does.
            AddFloodedCryptBossOverride(jo);
        }

        /// <summary>
        /// Deep-clone the template's Main dungeon def into m_DungeonDefOverride["FloodedCrypt"] and rewrite its final
        /// BossEnemy room to spawn OUR set (m_BossType="Specific", m_BossSet=&lt;setInt as the decimal-int string&gt;,
        /// the same shape m_EnemySet/m_BossEnemy use). Marks m_IsEndDungeon=true so the crypt drops end-dungeon loot
        /// on the boss kill. Vanilla proof of the Specific shape: DungeonCrawl's Arena def
        /// (m_BossType="Specific", m_BossSet="localArenaBoss"); m_DungeonDefOverride already ships a "MageDungeon" key.
        /// Affects ONLY FloodedCrypt in ONLY this adventure.
        /// </summary>
        private static void AddFloodedCryptBossOverride(JObject jo)
        {
            JObject defType = jo["m_DungeonDefType"] as JObject;
            JToken mainTok = defType != null ? defType["Main"] : null;
            if (mainTok == null)
            {
                Plugin.Log.LogError("[realm-boss] template '" + Template +
                    "' has no m_DungeonDefType[\"Main\"] to clone the FloodedCrypt boss override from.");
                return;
            }

            // Deep clone so editing the override never touches the shared type-keyed Main def.
            JObject cryptDef = (JObject)mainTok.DeepClone();

            // End-dungeon loot on the boss kill (loot-only flag; NOT a win trigger).
            cryptDef["m_IsEndDungeon"] = true;

            // Find the LAST level's BossEnemy room and make it our Specific set. The Main def's boss room is the
            // RoomDef.BossEnemy in its final m_DungeonLevels entry (verified: level[1].room[4] in the template).
            JArray levels = cryptDef["m_DungeonLevels"] as JArray;
            bool rewrote = false;
            if (levels != null && levels.Count > 0)
            {
                for (int li = levels.Count - 1; li >= 0 && !rewrote; li--)
                {
                    JObject lvl = levels[li] as JObject;
                    JArray rooms = lvl != null ? lvl["m_DungeonRooms"] as JArray : null;
                    if (rooms == null) continue;
                    foreach (JToken roomTok in rooms)
                    {
                        JObject room = roomTok as JObject;
                        string roomType = room != null ? (string)room["$type"] : null;
                        if (roomType != null && roomType.Contains("RoomDef.BossEnemy"))
                        {
                            room["m_BossType"] = "Specific";        // read FTK_enemySetDB.Get((ID)m_BossSet) directly
                            room["m_BossSet"] = _setInt.ToString();  // synthetic set as the decimal-int string
                            rewrote = true;
                            break;
                        }
                    }
                }
            }

            if (!rewrote)
            {
                Plugin.Log.LogError("[realm-boss] no RoomDef.BossEnemy room found in the cloned Main def; the " +
                    "FloodedCrypt boss override was NOT applied (the crypt would have no Foreman).");
                return;
            }

            // Stitch the override in under the FloodedCrypt dungeon id (per-id precedence over the type-keyed Main).
            JObject defOverride = jo["m_DungeonDefOverride"] as JObject;
            if (defOverride == null)
            {
                defOverride = new JObject();
                jo["m_DungeonDefOverride"] = defOverride;
            }
            defOverride[MidDungeon] = cryptDef;   // MidDungeon == "FloodedCrypt"
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

                // VISUAL: the boss's custom look (mossy tint + upscale) was registered into the Core visual
                // registry under its id, with the expected body scale, AND the row carries the matching collider
                // footprint (m_MarkerScale). Best-effort + guarded; visual is logged as part of the PASS line.
                bool visualRegistered = false; bool visualScaleOk = false; bool markerOk = false;
                EnemyVisualPatch.EnemyVisual reg = default(EnemyVisualPatch.EnemyVisual);
                if (boss != null && boss.m_ID != null)
                {
                    visualRegistered = EnemyVisualPatch.TryGet(boss.m_ID, out reg);
                    visualScaleOk = visualRegistered && Math.Abs(reg.scale - BossBodyScale) < 0.001f;
                    markerOk = Math.Abs(boss.m_MarkerScale - BossMarkerScale) < 0.001f;
                }
                bool visualOk = visualRegistered && visualScaleOk && markerOk;

                bool contentOk = boss != null && bossResolves && setResolves && halfPartyNonEmpty
                                 && notGenericBoss && setTargetsBoss;
                bool procOk = !procChecked || hasProcs;

                if (contentOk && procOk && visualOk)
                    Plugin.Log.LogInfo("SELF-TEST PASS [realm-boss-set]: boss '" + BossId + "' (int=" + _bossInt +
                        ", chassis=" + ChassisName() + ", HP=" + (boss != null ? boss.m_HealthTotal : -1) +
                        ", defPhys=" + (boss != null ? boss.m_BaseDefPhys : -1) + "/defMag=" +
                        (boss != null ? boss.m_BaseDefMag : -1) + ") + set '" + SetId + "' (int=" + _setInt +
                        ") resolve by int; set m_HalfParty=" + (set != null ? set.m_HalfParty.Length : -1) +
                        " targets the boss, m_Type=" + (set != null ? set.m_Type.ToString() : "null") +
                        " (!= GenericBoss); boss procs on weapon=" +
                        (procChecked ? hasProcs.ToString() + " (" + profCount + " actions)" : "deferred") +
                        "; visual: tint set, scale=" + reg.scale + ", marker=" +
                        (boss != null ? boss.m_MarkerScale : -1f) + ".");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-boss-set]: bossInt=" + _bossInt +
                        " bossResolves=" + bossResolves + " setInt=" + _setInt + " setResolves=" + setResolves +
                        " halfPartyNonEmpty=" + halfPartyNonEmpty + " setTargetsBoss=" + setTargetsBoss +
                        " notGenericBoss=" + notGenericBoss + " procs=" +
                        (procChecked ? hasProcs.ToString() : "deferred") +
                        " visualRegistered=" + visualRegistered + " visualScaleOk=" + visualScaleOk +
                        " markerOk=" + markerOk + " (scale=" + reg.scale + " expected " + BossBodyScale +
                        ", marker=" + (boss != null ? boss.m_MarkerScale : -1f) + " expected " + BossMarkerScale + ").");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [realm-boss-set]: " + e);
            }
        }

        // ---- #61 + #62 self-test: the adventure / realm / set-piece / victory at LOAD ----------------------

        /// <summary>
        /// SELF-TEST PASS [realm-boss]: the HollowMire adventure registered + is whitelisted/selectable; its
        /// authored GameDefinition deserializes with the game's settings; the realm resolves (m_GameStartRealm) and
        /// carries NO overworld set-piece boss (m_BossEnemy == None, the rework: the boss is in the crypt, not
        /// roaming); the FloodedCrypt DUNGEON OVERRIDE's final BossEnemy room is m_BossType==Specific with
        /// m_BossSet == (FTK_enemySet.ID)setInt (the boss IS the crypt's last fight); and the LAST quest of the LAST
        /// stage is the FloodedCrypt clear-dungeon quest (a DungeonQuestDef), so clearing the crypt fires victory.
        /// Reports whether m_EndGameAfterLastQuest is true (post-credits final-click; victory itself is last-quest
        /// completion, decompile-verified). The full playthrough-to-victory is a manual in-game gate. Wrapped so it
        /// never throws.
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
                FTK_dungeonEncounter.ID cryptKey = (FTK_dungeonEncounter.ID)Content.Db<FTK_dungeonEncounterDB>().GetIntFromID(MidDungeon);

                // (a) The realm resolves and is the game-start realm, and there is NO roaming overworld set-piece
                //     (the rework): GetRealmProperties (the game's own read path over m_RealmStages) returns props
                //     whose m_BossEnemy is None. FTKHex only spawns the set-piece when m_BossEnemy != None, so None
                //     means the boss does NOT roam the overworld.
                RealmProperties props = gd.GetRealmProperties(realmKey, 0);
                bool realmResolves = props != null;
                bool noSetPiece = props != null && props.m_BossEnemy == FTK_enemySet.ID.None;
                bool gameStartRealm = props != null && props.m_GameStartRealm;

                // (b) The boss IS the crypt's last fight: the FloodedCrypt per-id dungeon override's final
                //     RoomDef.BossEnemy room is m_BossType==Specific with m_BossSet==our custom set. The Specific
                //     path reads FTK_enemySetDB.Get((ID)m_BossSet) directly (dictionary lookup), so the synthetic
                //     set int resolves, exactly as the bounty m_EnemySet does.
                bool cryptOverrideOk = false; string bossRoomState = "(no override)";
                if (gd.m_DungeonDefOverride != null && gd.m_DungeonDefOverride.ContainsKey(cryptKey))
                {
                    DungeonDefinition cryptDef = gd.m_DungeonDefOverride[cryptKey];
                    RoomDef.BossEnemy bossRoom = FindBossRoom(cryptDef);
                    if (bossRoom != null)
                    {
                        bool specific = bossRoom.m_BossType == RoomDef.BossEnemy.BossType.Specific;
                        bool setMatch = bossRoom.m_BossSet == setKey;
                        cryptOverrideOk = specific && setMatch;
                        bossRoomState = "m_BossType=" + bossRoom.m_BossType + " m_BossSet=" + (int)bossRoom.m_BossSet;
                    }
                    else bossRoomState = "(override present but no RoomDef.BossEnemy room)";
                }

                // (c) Victory = the LAST quest of the LAST stage completing (decompile-verified: type-agnostic). With
                //     the rework that last quest is the FloodedCrypt clear-dungeon quest (a DungeonQuestDef whose
                //     m_DungeonID is FloodedCrypt), so clearing the crypt (which ends on the Foreman) wins.
                GameStage lastStage = gd.m_Stages[gd.m_Stages.Count - 1];
                bool lastQuestOk = false; bool lastQuestRealmOk = false; string lastQuestId = "(none)"; string lastQuestType = "(none)";
                if (lastStage != null && lastStage.m_Quests != null && lastStage.m_Quests.Count > 0)
                {
                    QuestDefBase last = lastStage.m_Quests[lastStage.m_Quests.Count - 1];
                    lastQuestType = last != null ? last.GetType().Name : "(null)";
                    DungeonQuestDef clear = last as DungeonQuestDef;
                    if (clear != null)
                    {
                        lastQuestId = clear.m_StoryQuestID;
                        lastQuestOk = clear.m_DungeonID == cryptKey;        // the crypt-clear is the victory
                        lastQuestRealmOk = clear.m_SpecifiedRealm == realmKey;
                    }
                }

                bool endGame = gd.m_EndGameAfterLastQuest;

                // (d) The custom victory plate was extracted to <pluginDir>/FTKModFramework_content/HollowMire/
                //     EndGameImage.png (what GetEndGameImage() loads for the flat, m_StoneHeroEndImage=false branch).
                //     Informational (logged, not gated): a missing file blanks the victory screen but does not fail
                //     the adventure. Fully qualified IO so no extra using is needed.
                bool endImageOk = false;
                try
                {
                    string pluginDir = System.IO.Path.GetDirectoryName(typeof(Plugin).Assembly.Location);
                    string endImagePath = System.IO.Path.Combine(
                        System.IO.Path.Combine(System.IO.Path.Combine(pluginDir, "FTKModFramework_content"), SaveFileName),
                        "EndGameImage.png");
                    endImageOk = System.IO.File.Exists(endImagePath);
                }
                catch { /* informational only; leave endImageOk=false on any IO issue */ }

                bool ok = whitelisted && realmResolves && gameStartRealm && noSetPiece
                          && cryptOverrideOk && lastQuestOk && lastQuestRealmOk;

                if (ok)
                    Plugin.Log.LogInfo("SELF-TEST PASS [realm-boss]: adventure '" + DisplayName + "' (" + SaveFileName +
                        ") registered + whitelisted=" + whitelisted + "; GameDefinition deserialized; realm int=" +
                        _realmInt + " resolves (m_GameStartRealm=" + gameStartRealm +
                        "), NO overworld set-piece (m_BossEnemy==None); FloodedCrypt dungeon override boss room is " +
                        "Specific with m_BossSet==set int=" + _setInt + " (the boss is the crypt's final fight); last " +
                        "quest of last stage '" + lastQuestId + "' is a " + lastQuestType + " clearing FloodedCrypt " +
                        "in realm int=" + _realmInt + " (clearing the crypt = beating the Foreman = victory); " +
                        "m_EndGameAfterLastQuest=" + endGame + " m_StoneHeroEndImage=false endImageOk=" + endImageOk +
                        ". Full playthrough-to-victory is a manual in-game gate.");
                else
                    Plugin.Log.LogError("SELF-TEST FAIL [realm-boss]: whitelisted=" + whitelisted +
                        " realmResolves=" + realmResolves + " gameStartRealm=" + gameStartRealm +
                        " noSetPiece=" + noSetPiece + " (m_BossEnemy=" +
                        (props != null ? props.m_BossEnemy.ToString() : "null") + " expected None) cryptOverrideOk=" +
                        cryptOverrideOk + " (bossRoom: " + bossRoomState + ", expected Specific + set int=" + _setInt +
                        ") lastQuestIsCryptClear=" + lastQuestOk + " lastQuestRealm=" + lastQuestRealmOk +
                        " (lastQuest='" + lastQuestId + "' type=" + lastQuestType + ") m_EndGameAfterLastQuest=" +
                        endGame + ".");
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("SELF-TEST FAIL [realm-boss]: " + e);
            }
        }

        /// <summary>
        /// Find the final <c>RoomDef.BossEnemy</c> room in a <see cref="DungeonDefinition"/> (the boss is in the
        /// last level), scanning levels last-to-first. Returns null if the def has no boss room. Mirrors the same
        /// last-level-first scan <see cref="AddFloodedCryptBossOverride"/> uses to write it.
        /// </summary>
        private static RoomDef.BossEnemy FindBossRoom(DungeonDefinition def)
        {
            if (def == null || def.m_DungeonLevels == null) return null;
            for (int li = def.m_DungeonLevels.Count - 1; li >= 0; li--)
            {
                DungeonDefinition.DungeonLevel lvl = def.m_DungeonLevels[li];
                if (lvl == null || lvl.m_DungeonRooms == null) continue;
                foreach (RoomDef.Base room in lvl.m_DungeonRooms)
                {
                    RoomDef.BossEnemy boss = room as RoomDef.BossEnemy;
                    if (boss != null) return boss;
                }
            }
            return null;
        }

        /// <summary>The chassis the boss was actually cloned from (the first usable <see cref="ChassisChain"/>
        /// entry, e.g. trollCaveA), for the log line.</summary>
        private static string ChassisName()
        {
            return _chassisUsedName ?? "(unknown)";
        }
    }
}
