# Model skeleton and route validation register

The export tools are reusable across compatible skinned meshes. Compatibility
and visual quality must be established per chassis/profile. This register
tracks enemy, resource-prefab, and player routes; it is not a claim that every
native rig, controller, equipment combination, or authored model is universally supported.

Use `tools/ai-model-pipeline/plan_model_authoring_kit.py` with an exact topology
group and route kind to resolve the current native renderer IDs, bind and rig
fingerprints, local reference hashes, and fresh extraction and Blender-scaffold
commands. Its `--all-routes` mode generates the complete current authoring map.
Primary targets stay scoped to the named topology. Companion rig targets carry
the exact bind variants for other skinned paths required by the complete
profile; rigid `MeshRenderer` assignments remain structural profile data.
Conditional skinned equipment has separate apparel rig targets resolved from its
exact native mesh name and bind palette.
The map proves reproducible authoring inputs only; the route-specific live
evidence below remains the acceptance boundary.
Run `verify_model_authoring_references.py` on that map to compare every local
primary, companion, or apparel rig NPZ array and skeleton JSON with a fresh decode from
the pinned native asset.
Each stageable entry also carries a schema-valid route profile starter and exact
preflight and staging commands; adapter-bound entries remain explicitly separate.
`scaffold_model_package.py` converts one stageable entry into a new metadata-only
`art-experiments/<slug>/` workspace after validating every catalog pin and
checking its identity and generated asset names against the current isolated
catalogs. It lists all original assets and live acceptance work still required.

## Named-model live evidence

| Model/chassis | Binding | Captured motion and gameplay | Remaining limits |
|---|---|---|---|
| [Mirewarden / trollCaveA](../art-experiments/mirewarden-ftk/README.md) | Exact native `trollCaveA / enTroll01 / renderer 121153`; 37 matched joints, zero dropped slots; combat controller `trollController` (6006); native root scale 0.95 preserved at public factor 1.0 | The canonical [V3 archive](../art-experiments/mirewarden-ftk/live-validation-v3/README.md) preserves three complete 120-frame captures through `cidle_troll`, `attackProf_troll`, ordinary HP 58→48 plus `damage_troll` recovery, `attack_troll`, and explicit-fixture `deathHeavy_troll` with `m_DoRagdoll=true`; all 11 surviving bodies become nonkinematic from frame 28; one guarded Collect reaches strict Ready 0/2; 28 originals are reviewed | Exact direct-enemy source only; fixture death is not ordinary lethal evidence; this archive gives no credit to the separately validated Gloamcap resource route; intentional rock gaps, inherited `trollCave_e` emission, native UI/effect/hero occlusion, other clips and sources, culling, portraits, collision, resource lifetime and final art approval remain open |
| [Cairnfire Troll B / trollB](../art-experiments/cairnfire-troll/README.md) | Native `trollB` / `enTroll02`: `enTroll01`, renderer 121256; 37-bone bind profile shared with the Troll A/Cave family, but a distinct exact enemy row; combat controller `trollController` (6006) | The [corrected-palette V2 archive](../art-experiments/cairnfire-troll/live-validation-v2-palette-corrected/README.md) preserves complete 120-frame pass, ordinary HP 72→62, and `KillSingle` HP 62→0; sampled native `attack_troll` and `deathHeavy_troll`/ragdoll motion; one Collect reaches strict Ready 0/2 | V1's vertically inverted palette remains a recorded rejection. V2 is technically coherent in sampled live frames but art approval, ordinary lethal damage, all views/intervals, portraits, culling, collision, and resource lifetime remain open |
| [Hearthveil Blacksmith / blacksmith_Female](../art-experiments/hearthveil-blacksmith/README.md) | Original six-mesh custom class ID 113 on `playerBlacksmith`, `hairTop`, `hairBottom`, `bootsBlacksmith(Clone)`, and the default/Gambeson apparel branches; native combat controller `player_1H_Blunt_Combat` | The [V3 canonical route](../art-experiments/hearthveil-blacksmith/live-validation-v3-canonical/README.md) retains V1 native attack HP 72→62, hit HP 970→913, Body/Backpack item59 `1/0→0/1→1/0`, both owner rebuilds, watched lease 4/6 disposal, and strict Ready 0/3, then adds fresh same-session exact body and six-bone hair Party Select idle plus visible `death_overworld` native-state playback fixtures | Rigid accessories, UI, and effects remain game-owned. Death is a motion fixture, not ordinary lethal gameplay, corpse lifetime, or cleanup. Other skinsets/apparel/controllers, full culling, portraits, multiplayer, and final art approval remain outside this result |
| [Wildbloom Herbalist / herbalist_Female](../art-experiments/wildbloom-herbalist/README.md) | Original three-mesh custom class ID 114 on `player_Herbalist`, `hairTop`, and the player-only seven-bone `hairBottom`; exact Herbalist Female native route | The [V2 canonical archive](../art-experiments/wildbloom-herbalist/live-validation-v2-canonical/README.md) preserves exact directional Party Class selection, three-mesh preview idle and death-fixture captures, overworld, ordinary attack and hit, strict Ready, level 1 progression, and one combat-avatar rebuild | Native clothing remains game-owned. Death and the progression kill are explicit fixtures. Final owner teardown, corpse lifetime, portraits, multiplayer, every equipment/camera state, and final art approval remain outside the claim |
| [Tideglass Fishsmith / blacksmith_Fish](../art-experiments/tideglass-fishsmith/README.md) | Original three-mesh custom class ID 115 on exact-cased `playerFIsh`, `hairTop`, and the player-only seven-bone `hairBottom`; exact `Player_FishPerson` native pedestal route | The [V1 archive](../art-experiments/tideglass-fishsmith/live-validation-v1/README.md) preserves a strict Player 1 Party Select preview join and a settled 24-frame fixed-step `standardIdle_handsDown` capture. The body and lower mantle are visible; the crest is bound but inactive in the default native Fish preview | V1’s pale face treatment is art-rejected and V2 needs a fresh preview. Neither version establishes overworld/combat behavior, attack/hit/death, equipment branches, progression, lifetime, portraits, multiplayer, every camera/culling condition, or final art approval |
| [Ashfang / wolfA](../art-experiments/ashfang-wolf/README.md) | Exact original mesh at `wolf01`, renderer 121142; 33-joint `wolfA` palette; native root scale 1.0 preserved | Canonical [V2](../art-experiments/ashfang-wolf/live-validation-v2-canonical/README.md) preserves three complete 120-frame captures through `cidle_wolf`, native `attackProf_wolf`, ordinary HP 58→50 with `damaged_wolf`, recovery, two later `attack_wolf` sequences and explicit-fixture `deathHeavy_wolf`; at frame 27 the animator disables and all 14 bodies become dynamic, with zero measured velocity from frame 49 through 119; one guarded Collect reaches strict Ready 0/2; 24 originals are reviewed | Exact `wolfA` source only; fixture death is not ordinary lethal evidence; native `wolfA_e` emission, effects, hero/UI overlap and late depth blur limit review; collision, general corpse lifetime, cleanup, portraits, culling, sibling sources, resource lifetime, every animation and finished-art acceptance remain open |
| [Moonreed Sylph / fairyA](../art-experiments/moonreed-sylph/README.md) | Exact original mesh at `enFairy01`, renderer 121395; 66-joint fairyA palette; native root scale 1.0 preserved | Canonical [V2](../art-experiments/moonreed-sylph/live-validation-v2-canonical/README.md) preserves four complete 120-frame captures through `fairy_cIdle`, native `fairy_attackProf`, a first ordinary native `fairy_dodge`, a second ordinary HP 58→55 with `fairy_damaged`, recovery, later enemy attacks and explicit-fixture `fairy_die`; `m_DoRagdoll=false` and the animator stays enabled through death frame 119; one guarded Collect reaches strict Ready 0/2; 30 originals are reviewed | Exact `fairyA` source only; fixture death is not ordinary lethal evidence; inactive break props receive no ragdoll credit; small scale, native effects, hero/UI overlap and late depth blur limit review; collision, general corpse lifetime, cleanup, portraits, culling, sibling sources, resource lifetime, every animation and finished-art acceptance remain open |
| [Cinderwing / batA](../art-experiments/cinderwing-bat/README.md) | Exact custom mesh at `enBat01`, renderer 121104; 53-joint batA palette; native root scale 0.78 preserved at public factor 1.0 | The canonical [V3 archive](../art-experiments/cinderwing-bat/live-validation-v3/README.md) preserves three complete 120-frame captures, `batAttackProf`, `attackCrit_bat`, ordinary HP 58→48 with `batTakeHit`, explicit `batDeath`, one native Collect, strict Ready at level 0 room 2, and 22 reviewed originals | Fixture death is not ordinary lethal damage; `m_DoRagdoll=false` and zero rigid bodies establish animated death rather than ragdoll; small scale, hero/effect occlusion, sibling bat sources, other clips, later corpse lifetime, culling, portrait/resource lifetime and finished-art acceptance remain open |
| [Bramblecoil Viper / snakeJungleC](../art-experiments/bramblecoil-jungle-snake/README.md) | Exact original 44-bone mesh at `enJungleSnakeC`, renderer 121424; native root scale 1.0 and explicit leased `preserve-custom-body` policy | Canonical [V2](../art-experiments/bramblecoil-jungle-snake/live-validation-v2-canonical/README.md) preserves four complete 120-frame captures through `Snake_Idle`, native `Snake_BiteAttack`, an ordinary native Block at HP 86→86, a fixture-assisted ordinary HP 86→85 hit with `Snake_HitSmall` and recovery, and explicit-fixture `Snake_DeathBig`; at frame 28 the animator disables and all 13 active skeleton bodies become dynamic, with zero measured velocity from frames 59 through 119; one guarded Collect reaches strict Ready 0/2; 23 originals are reviewed | Exact `snakeJungleC / enJungleSnakeC / 121424` source only; the native skill-cap and damage fixtures make balance unrepresentative; fixture death is not ordinary lethal evidence; the coiled body remains visible through sampled loot frame 119 but later corpse lifetime and cleanup remain unproven; fixed-camera crop, hero/effect/UI occlusion, collision, portraits, culling, sibling sources, resource lifetime, every animation and finished-art acceptance remain open |
| [Duneshade Asp / snakeDesertA](../art-experiments/duneshade-desert-asp/README.md) | Exact original 46-bone mesh at `enDesertSnakeA`, renderer 121552; native root scale 0.65 preserved at public factor 1.0 | Canonical [V2](../art-experiments/duneshade-desert-asp/live-validation-v2-canonical/README.md) preserves three complete 120-frame captures through settled `Snake_Idle`, native `Snake_BiteAttack`, ordinary HP 58→48 with `Snake_HitSmall`, recovery, and explicit-fixture `Snake_DeathBig`; at frame 28 the animator disables and all 13 bodies become dynamic, settling by frame 50; one guarded Collect reaches strict Ready 0/2; 17 originals are reviewed | Exact `snakeDesertA` source only; fixture death is not ordinary lethal evidence; native effects/hero/loot UI limit fine review; collision, general corpse lifetime, cleanup, portraits, culling, sibling sources, resource lifetime, every animation and finished-art acceptance remain open |
| [Sargassum Lash V2 / krakenTentacle](../art-experiments/abyssal-kraken/README.md) | Exact original 20-bone mesh at `krakenTentacle`, renderer 121595; native root scale 1.0 preserved | Canonical [primary V2](../art-experiments/abyssal-kraken/live-validation-sargassum-primary-v2-canonical/README.md) preserves complete 120-frame Pass and ordinary Attack captures plus an accepted 91-frame death prefix through settled `Tentacle_Idle_M`, native `Tentacle_Attack2_M`, ordinary HP 162→154 with `Tentacle_Damaged_M`, later `Tentacle_AttackGrab_Mirrored` and `Tentacle_Attack1_M`, and explicit-fixture `Tentacle_Death`; strict Ready is observed at 0/2 without Collect; 17 originals are reviewed | Exact primary `krakenTentacle` source only; fixture death is not ordinary lethal evidence; `m_DoRagdoll=false` means animated death; the renderer-destroyed boundary leaves full death duration and cleanup unproven; the tall body, broad attacks, effects and hero exceed or obscure the fixed camera; mirror controller, portraits, collision, culling, resource lifetime, every interval and finished-art acceptance remain open |
| [Abyssal Crown V4 / krakenHead](../art-experiments/abyssal-kraken/README.md) | Exact original seven-bone skinned head at `kraken2`, renderer 121035, plus required same-owner rigid `Root_M/base/body/neck/eye/kraken2_eye`; native root scale 1.0 preserved | Canonical [head V5](../art-experiments/abyssal-kraken/live-validation-head-v5-canonical/README.md) preserves complete 120-frame Pass and ordinary Attack captures plus an accepted 91-frame death prefix through settled `krakenIdle`, native `krakenAttack`, ordinary HP 324→316 with `krakenDamage`, recovery and explicit-fixture `krakenDisappear`; strict Ready is observed at 0/2 without Collect; 31 originals are reviewed | Exact `krakenHead / kraken2 / 121035` motion topology only; the rigid eye is structural profile context without skinned motion credit; fixture death is not ordinary lethal evidence; the pass records a nonstandard zero-damage self-targeted action; `m_DoRagdoll=false` means animated disappearance; the renderer-destroyed boundary leaves cleanup causality and final disposal unproven; effects, hero overlap, tall-pose crop, portraits, collision, culling, resource lifetime, every interval and finished-art acceptance remain open |
| [Mournglass Wraith / chaosBeast](../art-experiments/mournglass-wraith/README.md) | Exact original two-material mesh at `enChaosBeast`, renderer 121008; 31-bone ghost palette; native CEL scale 1.5 preserved | Canonical [V2](../art-experiments/mournglass-wraith/live-validation-v2-canonical/README.md) preserves complete 120-frame Pass and ordinary Attack captures plus an accepted 94-frame death prefix through settled `cidle_ghost`, native `attackProf_ghost`, immediate ordinary HP 58→53 with `damageSmall_ghost`, recovery, later native attacks and the opening of explicit-fixture `death_ghost`; strict Ready is observed at 0/2 without Collect; 30 originals are reviewed | Exact `chaosBeast / enChaosBeast / 121008` source only; a later Ready poll sees HP 45 after uncaptured intervening events and is not attributed to the 5-damage hit; fixture death is not ordinary lethal evidence; `m_DoRagdoll=false` means animated disappearance; the renderer is visible only through death frame19 and the destruction boundary leaves full visible death, cleanup and final disposal unproven; effects, hero overlap, portraits, collision, culling, resource lifetime, every interval and finished-art acceptance remain open |
| [Bronzehollow / deathknightA](../art-experiments/bronzehollow-sentinel/README.md) | Exact original body at `deathKnight`, reference renderer 121217; 36 matched bones; native helmet, shield and weapon retained separately; one owner at visual scale 1.0 and captured native CEL scale 1.2 | Canonical V4 preserves ten complete 120-frame captures with exact identity in all 1,200 frames: settled idle, native `AttackProf`, native shield `Block`, ordinary `Damaged` recoil, later native attack, and explicit-fixture `deathHeavy_blunt1H` into a coherent ragdoll; seven zero-focus attacks return native Block at HP 45→45 before attempt 8 returns native Damaged for HP 45→44; two guarded Collects reach strict Ready 0/2; nineteen originals and ten videos are archived | The disposable party fixture caps only the equipped hammer's Toughness skill at native 0.95, so balance is unrepresentative; explicit `KillSingle` 44→0 is a fixture, not ordinary lethal evidence; helmet, shield and mace are native equipment and receive no original-art credit; detailed equipment clearance, other attacks, collision/culling, physics sleeping, portraits, resource lifetime, final disposal, sibling sources and finished-art approval remain open |
| [Bronzewake / bossGladiator](../art-experiments/bronzewake-champion/README.md) | Exact authored multipart body, hair, armor and boots at `enBossGladiator`/`hairBottomBossGladiator`/`armorBossGladiator`/`bootsBossGladiator` (121272/121500/121522/121661); one owner at visual scale 1.0; captured native CEL scale 1.1. Canonical V4 credits armor 121522, V5 credits body 121272, V6 credits boots 121661, and V7 credits hair 121500 | All four exact renderer topologies now have their own canonical source archive. V4 records complete pass and ordinary HP 11→1 hit captures for armor; V5 records HP 11→6 for body; V6 records HP 11→1 `DamagedHeavy` for boots; V7 records HP 11→1 `DamagedHeavy` for hair. Each archive retains 90/120 explicit `KillSingle` frames before the verified controller-unresolved boundary and reaches strict Ready 0/2. V7 preserves 20 reviewed originals and an actual `m_DoRagdoll=true` transition with all 11 surviving bodies nonkinematic from frame 28 | Fixture death is not ordinary lethal evidence, and each partial prefix leaves full duration, later corpse lifetime and cleanup causality unproven. Other proficiencies, `DeathLight`, native accessories, UI/effects, depth blur, culling, portraits, collision, resource lifetime, full campaign behavior and final art review remain open |
| [Rustpetal / plantD](../art-experiments/rustpetal-snapper/README.md) | Exact original mesh at `enJungleNibbler_C`, renderer 121537; 43-bone plantD palette and exact binding signature; one owner at visual scale 1.0 with native CEL scale 1.7 | Canonical [V2](../art-experiments/rustpetal-snapper/live-validation-v2/README.md) preserves complete idle/native `attack3` and ordinary HP 58→50 `hit1` captures, a later native `attackProf1`, and 94 exact explicit-fixture `death` frames through a coherent fall and source cleanup boundary; strict Ready succeeds at level 0 room 2 without Collect; 20 reviewed originals and three videos are archived | Exact plantD source only; fixture death is not ordinary lethal evidence; `m_DoRagdoll=false` and zero rigidbodies establish animated death rather than ragdoll; renderer destruction leaves full death duration, cleanup causality and later corpse lifetime unproven; effects/UI/hero/blur, culling, collision, portraits, resource lifetime, sibling sources and finished-art acceptance remain open |
| [Belladusk / plantE](../art-experiments/belladusk-pitcher/README.md) | Exact custom mesh at `enJungleNibbler_A`, renderer 121530; 37-bone plantE palette; one owner at visual scale 1.0 and captured native CEL scale 1.2 | The canonical [V3 archive](../art-experiments/belladusk-pitcher/live-validation-v3/README.md) preserves three complete 120-frame captures through settled `idle`, two `attack1` intervals, ordinary HP 58→50 with `hit1`, recovery and explicit-fixture `death`; one guarded Collect reaches strict Ready 0/2; six correctly classified original frames and three videos are archived | Exact plantE source only; fixture death is not ordinary lethal damage; `m_DoRagdoll=false` with zero rigidbodies establishes animated death rather than ragdoll; native effects/hero occlusion limit fine review; culling, collision/sleeping, every interval, portrait/resource lifetime and finished-art acceptance remain open |
| [Cinderbloom / plantA](../art-experiments/cinderbloom-plant/README.md) | Exact corrected multipart body and leaves at `enPlant01`/`enPlant01Leaves` (120953/121072); separate 45- and 16-joint palettes; one owner at visual scale 1.0; captured native CEL scale 1.6 | Fresh catalog-411 pass, ordinary attack HP 58→50, complete explicit 120-frame `KillSingle`, one native Collect and strict Ready at level 0 room 2; six selected facing/body/leaf/attack/loot views and three videos are archived | Fixture death is not ordinary lethal damage; native UI/effects and victory item surface limit fine teeth, jaw, leaf-intersection and material review; full deformation, culling, floor collision/sleeping, portrait/resource lifetime and finished-art acceptance remain open |
| [Emberjaw / skullA](../art-experiments/emberjaw-skull/README.md) | Exact original multipart jaw and cranium at `ChaosSkullBottom`/`ChaosSkullTop` (121483/121577); separate 20- and 22-joint palettes; one owner at visual scale 1.0; captured native CEL scale 0.75 | Fresh catalog-411 pass, ordinary attack HP 69→59, complete explicit 120-frame `KillSingle`, two native Collects and strict Ready at level 0 room 2; six selected skull/jaw/attack/loot views and three videos are archived | Fixture death is not ordinary lethal damage; inherited `chaosBeastBody` emission and purple effects change live color/readability; targeting UI/victory surface limit complete jaw/death deformation, culling, portrait/resource lifetime and finished-art acceptance |
| [Emberglass Bee / beeA](../art-experiments/emberglass-bee/README.md) | Exact custom mesh at `Monster Bee`, renderer 121062; 47-joint beeA palette; one owner at visual scale 1.0; captured native CEL scale 1.25 | The [canonical V4 archive](../art-experiments/emberglass-bee/live-validation-v4/README.md) preserves three complete 120-frame captures plus the accepted 104-frame death prefix: `beeStingerAttack`, a zero-loss native `dodge_bee` attempt, ordinary HP 58→50 with `BeeTakeDamage`, `attack2_bee`, explicit-fixture `BeeDie`, strict Ready 0/2, 24 reviewed originals, and four videos | Exact beeA only; fixture death is not ordinary lethal evidence; `m_DoRagdoll=true` switches 16 bodies dynamic, reaches a grounded corpse by frame 40, and retains it through frame 102 before renderer teardown at 103; effects/UI, later corpse lifetime, culling, portrait, collision, resource lifetime, final art approval, and two remaining exact-source representatives stay open |
| [Resinmaw Bogling / acidBlobA](../art-experiments/resinmaw-bogling/README.md) | Exact custom mesh at `enAcidMonster`, renderer 121344; 32-joint acidBlobA palette; one owner at visual scale 1.0; captured native CEL scale basis 0.75/0.8/0.8 | Fresh catalog-411 pass, ordinary attack HP 81→73, complete explicit 120-frame `KillSingle`, and strict Ready at level 0 room 2; six selected body/attack/kill views and three videos are archived | Fixture death is not ordinary lethal damage; direct Ready exposed no Collect action; native UI/effects and the victory overlay limit fine jaw/fang detail, full death deformation, culling, portrait/resource lifetime and finished-art acceptance |
| [Duskquill Raven / crowC](../art-experiments/duskquill-raven/README.md) | Exact custom mesh at `enCrow`, renderer 120964; 39-joint crowC palette and exact binding signature; native CEL scale 0.9 | The canonical [V3 archive](../art-experiments/duskquill-raven/live-validation-v3/README.md) preserves three complete 120-frame captures, `BirdFlySlow`, two native `birdAttack2` intervals, ordinary HP 58→50 with `BirdDamage`, explicit `BirdDeath`, two native Collects, strict Ready at level 0 room 2, and 20 reviewed originals | Fixture death is not ordinary lethal damage; `m_DoRagdoll=false` and zero rigid bodies establish animated death rather than ragdoll. A first stochastic escape is preserved as rejected boundary evidence. Sibling bird rows, other clips, later corpse lifetime, culling, portraits, resource lifetime and finished-art acceptance remain open |
| [Basilight Cockatrice / cockatriceC](../art-experiments/basilight-cockatrice/README.md) | Exact custom mesh at `enChicken`, renderer 121484; 50-joint cockatriceC palette; one owner at visual scale 1.0; captured native CEL scale 0.55 | The [canonical V3 archive](../art-experiments/basilight-cockatrice/live-validation-v3/README.md) preserves three complete 120-frame captures, native `AttackProf1` and `AttackProf`, ordinary HP 72→62 with `Cockatrice_HitSmall`, explicit `Cockatrice_DeathHuge`, one native Collect, strict Ready at level 0 room 2, and 18 reviewed originals | Fixture death is not ordinary lethal damage; `m_DoRagdoll=false` makes the sampled fall animator-driven, and native UI/effects plus the victory loot surface limit impact detail, later corpse lifetime, culling, portrait/resource lifetime and finished-art acceptance |
| [Lunacrest Clam / clamA](../art-experiments/lunacrest-clam/README.md) | Exact custom mesh at `enClam`, renderer 121306; seven-joint clamA palette; one owner at visual scale 1.0; captured native CEL scale 0.8 | Fresh catalog-411 pass, ordinary attack HP 58→50, complete explicit 120-frame `KillSingle`, one native Collect and strict Ready at level 0 room 2; six selected shell/attack/loot views and three videos are archived | Fixture death is not ordinary lethal damage; native hide path, UI/effects and victory loot surface limit full death deformation, corpse settling, culling, portrait/resource lifetime and finished-art acceptance |
| [Reefstrider Fish / fishA01](../art-experiments/reefstrider-fish/README.md) | Exact custom mesh at `enFishA`, renderer 121695; fishA01 palette; one owner at visual scale 1.0; captured native CEL scale 1.0 | Canonical V3: three complete 120-frame captures cover idle, native `AttackCrit`, ordinary `Damaged` and recovery, and native `Death` launch and ragdoll collapse; ordinary HP 58→48, two native Collects, strict Ready 0/2, 18 reviewed originals, and a verified immutable archive | Exact fishA01 only; fixture death is not ordinary lethal damage; impact occlusion, later corpse lifetime, culling, portrait, collision, resource lifetime, and final art approval remain outside scope; no fishA02/A03 or sibling coverage transfers |
| [Gloamcap Trickster / impA](../art-experiments/gloamcap-imp/README.md) | Exact resource route `impA / enbaseyimp / enBaseyImp / renderer 121117`; 37-bone Imp palette; native CEL scale 1.0 | The canonical [V3 archive](../art-experiments/gloamcap-imp/live-validation-v3/README.md) preserves five complete 120-frame captures through `cidle_impUnarmed`, repeated `attack_impUnarmed`, two native `dodge_imp` trials, ordinary HP 58→48 plus `damageHeavy_imp` recovery, and explicit-fixture `deathHeavy_imp`; all 11 bodies become nonkinematic from frame 26; one guarded Collect reaches strict Ready 0/2; 29 originals are reviewed | Exact resource-prefab source only; the first two ordinary trials are zero-loss dodge evidence, and fixture death is not ordinary lethal evidence; direct enemy rows, sibling Imp sources and the Mirewarden troll route receive no credit; UI/effects/hero occlusion, other clips, culling, portraits, collision, resource lifetime and final art approval remain open |
| [Thistlewick Hexer / scourgeG](../art-experiments/thistlewick-hexer/README.md) | Exact custom mesh at `enScourgeLeprechaun`, renderer 121222; 36-bone scourgeG palette and exact binding signature; one owner at visual scale 1.0; captured native CEL scale 1.0 | Canonical V3 combines a fresh complete idle/native robbery/flee capture, retained ordinary HP 58→48 plus `damageHeavy_imp`, the prior ordinary HP 58→0 result, explicit-fixture `deathHeavy_imp`, two native Collects and strict Ready 0/2; 14 originals and four videos are archived | Full-health robbery/flee removal is not death; fixture death is separate from ordinary lethal evidence; `m_DoRagdoll=false` establishes animated death rather than body ragdoll; native hat, hero/UI/effect occlusion, culling, portraits, resource lifetime, global Fergus behavior and finished-art acceptance remain open |
| [Saffronspine Puffer / pufferA](../art-experiments/saffronspine-puffer/README.md) | Exact custom mesh at `enBlowFishA`, renderer 121509; 30-bone pufferA palette; one owner at visual scale 1.0; captured native CEL scale 1.0 | Canonical V3 completes three 120-frame captures with settled idle, native `Attack` inflation and recovery, ordinary no-focus HP 58→48 plus `Damaged`, explicit-fixture native `Death`, one guarded Collect and strict Ready at level 0 room 2. Eighteen exact originals and three videos are archived | Fixture death is not ordinary lethal evidence. Native direct death hides the renderer between reviewed frames 23 and 27, so the archive accepts the effect handoff without a visible corpse claim. Impact/hero occlusion, indirect death, culling, portrait/resource lifetime and final art review remain open; pufferB is separate |
| [Saffronspine Puffer B / pufferB](../art-experiments/saffronspine-puffer-b/README.md) | Exact B mesh at `enBlowFishA`, renderer 121388; independent 30-bone pufferB palette and bind matrices; one owner at visual scale 1.0; captured native CEL scale 1.0 | Fresh catalog-411 process preserves native pass/attack behavior, ordinary attack HP 58→50 without a cheat, and a complete explicit 120-frame `KillSingle` with `BlowFish_DeathDirect`; two native Collects and strict Ready at level 0 room 2 are archived with seven selected views and three videos | Ordinary attack is nonlethal and separate from fixture death acceptance; B-specific material/bind/camera evidence is independent; native effects, foreground hero/UI occlusion and Victory depth blur limit fine surface, settled-body, culling, portrait/resource lifetime, ordinary lethal, indirect death and finished-art acceptance; no pufferA live evidence transfers |
| [Copperveil / spiderB](../art-experiments/copperveil-spider/README.md) | Exact custom mesh at `enSpiderB`, renderer 121386; 65-bone Spider B palette; one owner at visual scale 1.0; captured native CEL scale 1.0 | Canonical V3 completes three 120-frame captures with settled idle, native `AttackProf` and recovery, ordinary no-focus HP 63→59 plus `Damaged`, explicit-fixture native `Death`, one guarded Collect and strict Ready at level 0 room 2. Eighteen exact originals and three videos are archived | Fixture death is not ordinary lethal evidence. The native death path preserves the custom mesh through an airborne pose and coherent collapse that remains sampled behind the loot panel. Impact/hero occlusion, indirect death, other attacks, collision, extended culling, portrait/resource lifetime and final art review remain open; Spider A routes are separate |
| [Tideglass / crabB](../art-experiments/tideglass-crab/README.md) | Exact custom mesh at `enCrabWizard`, renderer 121411; 63-joint Crab B palette; one owner at visual scale 1.0; captured native CEL scale 0.9 | The canonical [V3 archive](../art-experiments/tideglass-crab/live-validation-v3/README.md) preserves three complete 120-frame captures, `Crab_Attack1`, `Crab_Attack2`, ordinary HP 58→56 with `Crab_Hit`, explicit `Crab_DeathDirect`, one native Collect, strict Ready at level 0 room 2, and 21 reviewed originals | Fixture death is not ordinary lethal damage; `m_DoRagdoll=false` establishes animated death rather than ragdoll, and native hat/UI/effects, foreground hero, loot overlay and depth blur limit fine inspection; indirect death, later corpse lifetime, culling, portrait/resource lifetime and finished-art acceptance remain open |
| [Mossglass / cubeA](../art-experiments/mossglass-reliquary/README.md) | Exact two-primitive mesh at `enJellyCube`, renderer 121012; three-joint Cube A palette; one owner at visual scale 1.0; authored material slots 0/1 with native slot-1 `_MainTex` scrolling | The [canonical V5 archive](../art-experiments/mossglass-reliquary/live-validation-v5/README.md) preserves three complete 120-frame captures, native `AOE_jelly`, ordinary HP 58→53 with `wobble_jelly`, explicit `deathHeavy_jelly`, two native Collects, strict Ready at level 0 room 2, 18 reviewed originals, and per-frame scroll readback | Fixture death is not ordinary lethal damage; `m_DoRagdoll=false` and the sampled geometry disappearance establish animator-driven removal without a visible corpse; effects/hero/UI occlusion, perceptual scroll continuity, clone independence, culling, portrait/resource lifetime and finished-art acceptance remain open |
| [Vesper Eye / beholderA](../art-experiments/vesper-eye/README.md) | Exact original multipart body and eye at `EyeBody`/`EyeBody/EyeEye` (121031/121210); separate two-joint palettes; one owner at visual scale 1.0; explicit per-renderer emission opt-out with live readback on both parts | Canonical V4 preserves three complete 120-frame captures with `eye_idle`, `eye_attack1`, `eye_attack2`, ordinary HP 135→125 plus `eye_damage`, explicit-fixture `Death`, one guarded Collect, strict Ready at level 0 room 2, 18 reviewed originals and a verified immutable archive | Fixture death is not ordinary lethal damage; both renderers become inactive before `eye_death` begins, so the archive establishes trigger, controller state and cleanup without visible custom death deformation, corpse, ragdoll, floor contact or lifetime; effects/hero/UI occlusion, culling, portrait/resource lifetime and final art review remain open |
| [Tamarind / monkeyC](../art-experiments/tamarind-trickster/README.md) | Exact custom mesh at `enMonkeyBasey`, renderer 121301; 42 captured bones; one owner at visual scale 1.0 | Canonical V2 preserves one complete 120-sample passive-arrival capture: settled idle at samples 40/60, native `enSuicideCurse` attack entry at game frame 29945, secondary damage at frame 29972, HP 58 to 0, renderer inactive from sample 108, and strict Ready 0/2 immediately before the single arm/Ready sequence; seven originals are reviewed | The exact native self-removal route has no received-hit phase and precedes any hero attack, so hit motion and ordinary damage are narrowly machine-classified not applicable; no sibling evidence transfers. Hidden terminal samples do not prove a visible full death or corpse; post-combat loot, portraits, continuous material behavior, full culling, collision/sleeping, final resource lifetime and finished-art acceptance remain open |
| [Verdigrin / mimicA](../art-experiments/verdigrin-mimic/README.md) | Exact custom mesh at `mimic01`, renderer 121192; nine-joint mimic palette and exact binding signature; one owner at visual scale 1.0 | Canonical V4 combines a retained complete idle/native `attack_mimic` capture, ordinary HP 58→52 plus `damageSmall_mimic`, later `chompAOE_mimic`, explicit-fixture `deathHeavy_mimic`, and the prior V3 two guarded Collects, strict Ready 0/2 and next Enemy room; seven reviewed originals and three videos are archived | Fixture death is not ordinary lethal damage; `m_DoRagdoll=false` establishes animated death rather than body ragdoll; the runner was reused only after exact semantic identity reconciliation; hero/UI/effect occlusion, every hinge/tongue interval, collision/sleeping, culling, portraits, resource lifetime and finished-art acceptance remain open |
| [Sunspire Roc / rocA](../art-experiments/sunspire-roc/README.md) | Exact custom mesh at `enRoc01`, renderer 121238; 36-joint Roc palette and exact binding signature; one owner at public visual scale 1.0 with native CEL scale 0.9 | Canonical [V4](../art-experiments/sunspire-roc/live-validation-v4/README.md) preserves three complete 120-frame captures through `cidle_roc`, native `attackProf_roc`, ordinary HP 81→71 plus `damageLight_roc`, later `attackCrit_roc`, explicit-fixture `deathHeavy_roc`, two native Collects, strict Ready 0/2, and one constructed native 328×280 row portrait; six reviewed originals and three videos are archived | Exact rocA/enRoc01 source only; fixture death is not ordinary lethal damage; `m_DoRagdoll=false` and zero rigid bodies establish animated death rather than a body ragdoll; the portrait is a constructed native row caller; UI/hero/effect occlusion, camera crop, later corpse lifetime, culling, collision, resource lifetime, sibling Roc sources and finished-art acceptance remain open |

| [Amberwake Dragon / dragonFrost](../art-experiments/amberwake-dragon/README.md) | Exact custom mesh at `enDragon`, renderer 121561; 70-bone palette shared with representative 121525; one native owner at deliberate visual scale 0.25 | Canonical V3 preserves complete 120-frame idle/native `AttackProf` and ordinary-hit captures; a zero-focus fixture-assisted strike produced native `Damaged` for 11 HP, from 675 to 664, and the exact hero baseline was restored at between-room Ready; an explicit `KillSingle` retains a 91-frame animated-death prefix; 23 selected originals and three videos are archived | Fixture-assisted combat is not representative balance or guaranteed hit behavior; fixture death is not ordinary lethal damage; `m_DoRagdoll=false` and zero rigidbodies establish animated death rather than ragdoll; the inactive frame 90 does not prove an active corpse; HUD, effects, blur and camera distance limit anatomy, culling, portraits, resource lifetime, natural-arena behavior and finished-art review |

Each linked experiment preserves its own live-validation record (historical
`live-validation.json` or an asset-local supplemental `validation.json`), media,
hashes, and limitations. [The runtime evidence index](model-runtime-validation.json)
collects these records and preserves the separate 106-assertion synthetic lease
test. Cinderwing's historical renderer disappearance is consistent with
source-backed native end-combat cleanup and observed room advancement; the exact
runtime cleanup branch was not traced. The newer native-scale V2 fixture capture
completes 120 frames, while the old 92-frame failure remains preserved.

The fixtures used a disposable single-player save, a health-boosted hero and
quiet tutorials. Fixed-step replays establish sampled gameplay observations,
not real-time performance. Mirewarden's normal material includes loader emission.
No completed combat exchange establishes every animation or final artistic quality.

### Multipart calibration evidence

`plantA` loaded both original calibration GLBs at `enPlant01` (reference renderer
120953) and `enPlant01Leaves` (121072) in one enemy owner. Separate body and leaf
captures recorded idle/attack states. The current sixfold-thicker probe recorded
an ordinary native hit from HP 18→10 and native KillSingle from HP 10→0, followed
by collapse, Dancing Nettle collection and strict Ready at level 0, room 2.
Capture IDs, hashes, exact observed intervals and renderer identities are in the
runtime evidence index. The earlier thin-probe run remains diagnostic history.

The thicker markers are visible in the reviewed frames; hero overlap and combat
effects still obscure parts of motion. Body and leaf telemetry comes from
separate captures, with current death telemetry targeting the leaves. This is
calibration geometry, not finished creature art or full motion/culling coverage.
The Blacksmith female player calibration also has initial binding, shared-lease
and combat evidence in [the player guide](MODEL-PLAYER-API.md); broader player
lifecycle and skinset coverage remain pending. These observations do not validate
all 162 enemy rig profiles or 222 rig/controller combinations.

### PlantD singular mesh and tint lifetime

The [PlantD diagnostic trial](evidence/plantd-legacy-tint-native-hud-lifetime-v1/README.md)
uses native `plantD`, exact `enJungleNibbler_C` renderer 121537 and 43 bones.
The legacy singular API applies body tint `(0.6, 0.8, 1, 1)` and original
calibration geometry. Three complete 120-frame recordings preserve a native
enemy turn, an ordinary hit from 58 to 50 HP, and explicit `KillSingle` death.
Four selected poses were visually reviewed; one native Collect reaches Ready
at level 0, room 3.

The passive observer independently verifies native HUD clone acquisition and
clone-first release, then final destruction of both owners/CELs and all six
owned resources, including intermediate tint and FX material copies. Native
assets survive. This is diagnostic binding, sampled motion and exact resource
lifetime evidence, not Rustpetal art acceptance, ordinary lethal damage,
quantified rendered tint fidelity, or other destruction orders.

## Reconciled coverage baseline

The repeatable inventory and database mapper now identify 230 exact bind/topology
character candidates. All 230 native-reference extraction/export roundtrips pass.
All 230 also pass the independent Blender scaffold, save, reopen, original
calibration export, and validator workflow. This second audit tests the modder
authoring bridge, not copied native geometry. The one initial uniform-scale hair
rig failure was corrected while preserving exact native binding matrices.
Neither offline audit establishes live animation or finished artistic quality.
The enemy database reconciles 372 rows: 363 nonnull enemy prefabs and 9 null-asset
entries, with no unresolved rows. These use 308 prefabs, 323 renderers, 162 exact
rig profiles, 222 rig/weapon-controller combinations, and 66 combat controllers.
Seven enemy entries are multipart. The earlier scratch mapper's count 214 was
incorrect because it shared mutable renderer records; the repeatable mapper
uses independent records and reports 222.

The generated [coverage baseline](model-coverage-baseline.json) accounts for every
candidate and marks original-asset/live checks pending. It is an offline audit
baseline, not the status of individually tested artwork. Keep attempt evidence
separate and keyed to source/model hashes. The named examples above carry their
own evidence and do not imply success for every variant.

Another 427 renderers are not referenced by a nonnull vanilla enemy row: 144 have
CharacterEventListener and 283 do not. A source-backed classifier now accounts
for all 467 CharacterEventListener renderers:

| Verified relationship | Renderers |
|---|---:|
| Enemy database prefab | 323 |
| Player skinset avatar | 94 |
| Embedded under a Diorama component | 10 |
| Empty renderer without a mesh | 2 |
| Unresolved character prefab | 38 |

The player relationship comes from 96 exact `FTK_skinsetDB.m_Avatar` references,
reconciled with the enum and serialized row count. Across the 230 rig profiles,
162 are enemy-used, 57 additional profiles have positive player skinset
references, and 11 have neither enemy nor skinset references. One of those 11
also appears in embedded diorama characters. Overlapping references do not
establish a single exclusive use for a rig.

The player profiles need avatar/equipment integration and their own live checks.
The remaining 11 need ownership and spawn research. Neither group is covered by
an enemy mesh-swap test. The 283 renderers outside CharacterEventListener remain
outside this character-rig audit; names suggesting equipment or boats are not
proof of an integration path.

Reproduce these relationships with `classify_rig_candidates.py` in the
[tools guide](../tools/ai-model-pipeline/README.md). No candidate is classified as
NPC, portrait, or unused from its name alone.

## Exact unresolved enemy probes

The [unresolved-enemy-probes-v1 archive](evidence/unresolved-enemy-probes-v1/README.md)
is the repeatable follow-up for rows that had an exact native renderer and rig
mapping but no indexed enemy-model run. Each row below is independent; a shared
bone count or controller does not transfer acceptance to a sibling row. The
archive keeps the case journals, raw captures, selected frames, videos, source
pins and the stopped prefixes so a later run can resume from the same boundary.

| Native row | Exact renderer / source ID | Live boundary | Current interpretation |
|---|---|---|---|
| [Tidecrown Sovereign / Sea King](../art-experiments/tidecrown-sea-king/README.md) | `enSeaKing` / 121357; original 60-bone body, one exact owner, native root scale 1.0 | Canonical V2 preserves two native Blocks, then a fixture-assisted ordinary no-focus 720→719 Damaged response; explicit `KillSingle` changes 12 active bodies to dynamic at frame 28 and retains 91 frames through the inactive boundary; strict Ready 0/2 | Exact seaKing/enSeaKing route is canonical. The native skill-cap fixture and party fortification make balance unrepresentative; fixture death is not ordinary lethal, and head/crown framing, portraits, full culling, cleanup, tentacles, props, and finished-art acceptance remain open |
| Jungle Snake C | `enJungleSnakeC` / 121424 | The historical probe stopped after HP 86→86; canonical V2 now preserves native Block, fixture-assisted ordinary HP 86→85 `Snake_HitSmall`, explicit `KillSingle` `Snake_DeathBig`/ragdoll, Collect and strict Ready 0/2 | Resolved by the exact canonical Bramblecoil V2 archive; fixture balance, ordinary lethal damage, later corpse lifetime, cleanup and finished-art acceptance remain open |
| Desert Snake A | `enDesertSnakeA` / 121552 | Pass, ordinary Attack 58→48, explicit `KillSingle`, native Ready 0/2 | Exact binding diagnostic complete; Snake siblings remain separate claims |
| plant G | `enJungleNibbler_B` / 121584 | Pass, ordinary Attack 58→50, explicit `KillSingle`, native Ready 0/2 | Exact binding diagnostic complete; other plant rows remain separate claims |
| skelly A | `skelly01naked` / 120979 | Pass, ordinary Attack 58→55, explicit `KillSingle`, native Ready 0/2 | Exact 37-joint binding diagnostic complete; other 37-joint rows remain separate claims |
| hag A | `hag01` / 121109 | Pass, ordinary Attack 58→50, explicit `KillSingle`, native Ready 0/3 | Exact 37-joint binding diagnostic complete; other hag/controller rows remain separate claims |
| bearman A | `bearman` / 121195 | Pass, ordinary Attack 58→50, explicit `KillSingle`, native Ready 0/2 | Exact 37-joint dual-weapon binding diagnostic complete; other bear/controller rows remain separate claims |
| bandit A | `bandit02` / 121005 | Pass, ordinary Attack 58→50, explicit `KillSingle`, two native Collects, strict Ready 0/2 | Exact 37-joint bow-controller binding diagnostic complete; other 37-joint rows remain separate claims |
| thief A | `enThief01` / 120997 | Pass capture; native controller reduced HP 58→0 before a hero attack | Exact 37-joint bladed-controller binding captured at a self-termination boundary; no ordinary damage, fixture death, loot or Ready acceptance |
| hag B | `hag02` / 120995 | Pass, ordinary Attack 58→48, explicit `KillSingle`, two native Collects, strict Ready 0/2 | Exact 37-joint hag-controller binding diagnostic complete; hag A and other 37-joint rows remain separate claims |
| skelly B | `skelly04archer` / 121169 | Pass, ordinary Attack 58→55, explicit `KillSingle`, two native Collects, strict Ready 0/2 | Exact 37-joint bow-controller binding diagnostic complete; skelly A and other 37-joint rows remain separate claims |
| witch A | `witch01` / 121039 | Pass, ordinary Attack 58→48, explicit `KillSingle`, two native Collects, strict Ready 0/2 | Exact 37-joint magic-controller binding diagnostic complete; other magic-controller and 37-joint rows remain separate claims |
| bandit D | `enBanditB02` / 121235 | Pass, ordinary Attack 58→56, explicit `KillSingle`, one native Collect, strict Ready 0/2 | Exact 37-joint spear-controller binding diagnostic complete; other bandit and 37-joint rows remain separate claims |
| bandit C | `enBanditB01` / 121156 | Pass, bounded native attacks, accepted ordinary HP 58→57 on attempt 6, explicit `KillSingle`, one native Collect, strict Ready 0/2 | Exact 37-joint bladed-controller binding diagnostic complete; earlier no-loss outcomes are preserved and no cause is inferred |
| assassin A | `bandit01` / 121075 | Pass, ordinary Attack 58→48, explicit `KillSingle`, one native Collect, strict Ready 0/2 | Exact 37-joint bow-controller binding diagnostic complete; other assassin and 37-joint rows remain separate claims |
| hag C | `hag03` / 121089 | Pass, ordinary Attack 58→50, explicit `KillSingle`, one native Collect, strict Ready 0/2 | Exact 37-joint hag-controller binding diagnostic complete; hag A/hag B remain separate claims |
| skelly C | `skelly03fighter` / 121228 | Pass, ordinary Attack 58→54, explicit `KillSingle`, two native Collects, strict Ready 0/2 | Exact 37-joint blunt-controller binding diagnostic complete; skelly A/skelly B remain separate claims |
| assassin B | `enAssassinB` / 121266 | Pass, ordinary Attack 68→58, explicit `KillSingle`, one native Collect, strict Ready 0/2 | Exact 37-joint bow-controller binding diagnostic complete; assassin A and other 37-joint rows remain separate claims |
| witch C | `enWitchC` / 121547 | Pass, ordinary Attack 63→55, explicit `KillSingle`, one native Collect, strict Ready 0/2 | Exact 37-joint magic-controller binding diagnostic complete; witch A and other 37-joint rows remain separate claims |
| demon A | `demon` / 121166 | Pass; eight bounded native attacks each observed 108→108 and stopped at `no_hp_loss_unclassified` | Exact 37-joint demon binding and repeatable no-loss boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| ghost A | `ghost01` / 121160 | Pass, ordinary Attack 58→48, explicit `KillSingle` renderer-destroyed prefix, native Ready at room 2 | Exact 37-joint ghost binding diagnostic complete; renderer-destroyed fixture prefix is retained and no finished-art acceptance is inferred |
| wildmage A | `wildMage02` / 121030 | Pass, ordinary Attack 58→53, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; other magic-controller rows remain separate claims |
| scourge A | `enDisciple` / 121535 | Pass, ordinary Attack 126→118, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint ghost-controller binding diagnostic complete; scourge B remains a separate claim |
| scourge B | `enScourgeBanditKing` / 120988 | Pass, ordinary Attack 81→79, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint blunt-controller binding diagnostic complete; scourge A and other blunt rows remain separate claims |
| beastman A | `beastman01` / 121227 | Pass, ordinary Attack 58→48, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; beastman B remains separate despite the shared renderer mesh |
| beastman B | `beastman01` / 121139 | Pass, ordinary Attack 58→50, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint blunt-controller binding diagnostic complete; beastman A remains separate despite the shared renderer mesh |
| cragHulk | `rockman01` / 121126 | Fresh bounded retry after an earlier 58→58 no-loss stop; accepted ordinary Attack 58→56, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint brute-controller binding diagnostic complete; earlier no-loss outcome is preserved and no combat cause is inferred |
| beastman C | `beastman01` / 121069 | Pass, ordinary Attack 58→53, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint blunt-controller binding diagnostic complete; beastman A and B remain separate source/controller claims |
| drycorpse A | `enDryCorpse01` / 120994 | Fresh bounded retry after an earlier 58→58 no-loss stop; accepted ordinary Attack 58→48, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint zombie-controller binding diagnostic complete; earlier no-loss outcome is preserved and no combat cause is inferred |
| lizardman A | `boggling` / 121028 | Pass, ordinary Attack 58→56, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; other spear and 37-joint rows remain separate claims |
| catmage A | `mysticCat` / 121022 | Seven bounded attacks at 58→58, accepted ordinary Attack 58→48 on attempt 8, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; earlier no-loss outcomes are preserved and no combat cause is inferred |
| wildmage B | `wildmage01` / 121112 | Pass, ordinary Attack 70→62, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; wildmage A remains a separate claim |
| bison A | `bisontaur` / 120958 | Pass, ordinary Attack 58→54, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint dual-weapon binding diagnostic complete; other dual-weapon and 37-joint rows remain separate claims |
| cyclops A | `triclops` / 121006 | Pass, ordinary Attack 90→82, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint giant-controller binding diagnostic complete; cyclops C remains a separate claim |
| cyclops C | `triclopsBaby` / 121102 | Pass, ordinary Attack 68→57, explicit `KillSingle`, native Ready at room 3 | Exact 37-joint giant-controller binding diagnostic complete; cyclops A remains a separate claim |
| mummy A | `mummy` / 121097 | Fresh bounded retry after an earlier 81→81 no-loss stop; accepted ordinary Attack 81→77 on attempt 2, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint zombie-controller binding diagnostic complete; earlier no-loss outcome is preserved and no combat cause is inferred |
| mummy B | `mummy02` / 120969 | Pass, ordinary Attack 90→87, explicit `KillSingle`, native Ready at room 3 | Exact 37-joint zombie-controller binding diagnostic complete; mummy A remains a separate claim |
| owlbear A | `enOwlBear` / 121209 | Pass, ordinary Attack 126→118, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint brute-controller binding diagnostic complete; other brute and 37-joint rows remain separate claims |
| cultist A | `cultist2` / 120950 | Pass, ordinary Attack 58→48, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; other cultist and 37-joint rows remain separate claims |
| cultist boss 1 | `cultist3` / 121091 | Pass, ordinary Attack 58→53, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; cultist A and cultist C remain separate claims |
| cultist C | `cultistC` / 121257 | Eight bounded native attacks at 58→58, stopped at `no_hp_loss_unclassified` | Exact 37-joint blunt-controller binding and repeatable no-loss boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| skellymage A | `skelly07mage` / 121146 | Pass, ordinary Attack 58→50, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; other magic-controller and 37-joint rows remain separate claims |
| skellymage D | `skellyMage02` / 121469 | Fresh bounded retry after an earlier 58→58 no-loss stop; accepted ordinary Attack 58→48 on attempt 1, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; earlier no-loss outcome is preserved and no combat cause is inferred |
| wraith A | `enWraith` / 121252 | Pass, ordinary Attack 58→50, explicit `KillSingle`, native Ready at room 3; no usable Collect button | Exact 37-joint wraith-controller binding diagnostic complete; native loot surface exposed no Collect action |
| druid A | `enDruid` / 121130 | Pass, ordinary Attack 58→48, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; other bow and 37-joint rows remain separate claims |
| shark brute A | `enSharkBruteA` / 121432 | Fresh bounded retry after an earlier 99→99 no-loss stop; accepted ordinary Attack 99→97 on attempt 2, explicit `KillSingle`, native Ready at room 2 | Exact 37-joint dual-weapon brute binding diagnostic complete; earlier no-loss outcome is preserved and no combat cause is inferred |
| mindflayer A | `mindMelter` / 121258 | Pass, ordinary Attack 63→53, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint mindflayer binding diagnostic complete; this source/controller assignment remains independent |
| ogre A | `enOgre` / 121076 | Pass, ordinary Attack 108→104, explicit `KillSingle`, one native Collect, strict Ready at room 3 | Exact 37-joint ogre binding diagnostic complete; armored Ogre C remains separate |
| ogre C | `enOgre_Armored` / 121350 | Pass; eight bounded native attacks each observed 158→158 and stopped at `no_hp_loss_unclassified` | Exact 37-joint armored-ogre binding and repeatable no-loss boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| ratthief A | `ratThief` / 121128 | Pass, ordinary Attack 58→48, explicit `KillSingle`; one native Collect accepted but loot did not progress to strict Ready | Exact 37-joint bladed binding diagnostic complete; once-only runner stopped at unchanged native loot state without a second Collect or forced Ready |
| scourge C | `scourgeJester` / 121203 | Pass, ordinary Attack 63→58, explicit `KillSingle`; two native Collects accepted but loot did not progress to strict Ready | Exact 37-joint lute-controller binding diagnostic complete; once-only runner stopped at unchanged native loot state |
| scourge D | `enScourgeFraybee` / 121034 | Pass, ordinary Attack 126→116, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; other scourge rows remain separate claims |
| scourge E | `scourgeTime` / 121138 | Pass, ordinary Attack 90→13, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint time-controller binding diagnostic complete; other scourge rows remain separate claims |
| scourge H | `enScourgeVolcanoWizard` / 121208 | Pass, ordinary Attack 81→73, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; other scourge rows remain separate claims |
| scourge I | `scourgeDemis` / 121445 | Fresh bounded retry after an earlier 108→108 no-loss stop; accepted ordinary Attack 108→105 on attempt 1, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint brute-controller binding diagnostic complete; earlier no-loss outcome is preserved and no combat cause is inferred |
| scourge J | `enScourgeWitchdoctor` / 121614 | Pass; eight bounded native attacks each observed 108→108 and stopped at `no_hp_loss_unclassified` | Exact 37-joint magic-controller binding and repeatable no-loss boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| scourge K | `enScourgeSiren` / 121462 | Pass, ordinary Attack 216→213, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint ghost-controller binding diagnostic complete; other scourge rows remain separate claims |
| leprechaun A | `leprechaun` / 121214 | Pass and native Attack capture; target reduced 58→0 before explicit fixture | Exact 37-joint leprechaun binding captured at a native target-removal boundary; no ordinary nonlethal damage, fixture death, loot or Ready acceptance |
| bandit B | `BanditWarrior` / 121201 | Pass, ordinary Attack 58→52, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; other bandit rows remain separate claims |
| bison undead | `enBisonUndead` / 121085 | Pass; eight bounded native attacks each observed 113→113 and stopped at `no_hp_loss_unclassified` | Exact 37-joint dual-weapon brute binding and repeatable no-loss boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| cultist E | `cultistC` / 121257 | Pass, ordinary Attack 86→76, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint blunt-controller binding diagnostic complete; cultist C remains separate despite the shared renderer path |
| cultist A2 | `cultist2B` / 121172 | One bounded no-loss retry, accepted ordinary Attack 58→48 on attempt 2, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint blunt-controller binding diagnostic complete; cultist A remains a separate source/controller claim |
| foxshaman A | `enFoxShaman` / 121246 | Pass, ordinary Attack 58→45, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; other magic rows remain separate claims |
| yeti A | `enYeti` / 121381 | Pass, ordinary Attack 89→87, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint brute-controller binding diagnostic complete; other brute rows remain separate claims |
| swampmonster A | `swampMonster` / 121114 | Pass, ordinary Attack 72→64, explicit `KillSingle`; two native Collects accepted but loot did not progress to strict Ready | Exact 37-joint blunt-controller binding diagnostic complete; once-only runner stopped at unchanged native loot state |
| skellyBard A | `skelly01naked` / 120980 | Pass, ordinary Attack 58→48, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint lute-controller binding diagnostic complete; other lute and skelly rows remain separate claims |
| cultist C2 | `cultistC2` / 121230 | Pass, ordinary Attack 58→45, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; other cultist rows remain separate claims |
| cultist D3 | `cultistC3` / 121096 | Pass, bounded native attacks, accepted ordinary Attack 70→68 on attempt 3 after two 70→70 outcomes, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; earlier no-loss outcomes are preserved and no cause is inferred |
| cultist boss 2 | `cultist3` / 121092 | Pass, ordinary Attack 81→76, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; cultist boss 1 and other magic rows remain separate claims |
| bard A | `enBard` / 121163 | Pass, ordinary Attack 58→53, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint lute-controller binding diagnostic complete; other lute and 37-joint rows remain separate claims |
| pirate A | `enPirate02` / 120970 | Pass, ordinary Attack 58→50, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint rapier-controller binding diagnostic complete; pirate B and other 37-joint rows remain separate claims |
| pirate B | `enPirate01` / 121205 | Pass capture; native Attack reduced HP 58→0 before the fixture | Exact 37-joint bladed-controller binding captured at a target-removal boundary; no ordinary nonlethal damage, fixture death, loot or Ready acceptance |
| mage imp A | `impWizard01` / 121149 | Pass, bounded native attacks, accepted ordinary Attack 58→48 on attempt 3 after two 58→58 outcomes, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; earlier no-loss outcomes are preserved and no cause is inferred |
| minion scourge B | `BanditWarrior` / 121201 | Pass, ordinary Attack 58→53, explicit `KillSingle`, strict Ready at room 2; no usable native Collect button | Exact 37-joint bladed-controller binding diagnostic complete; shared renderer evidence does not transfer across source/controller rows |
| minion scourge F | `enMinionHangman` / 121321 | Pass, ordinary Attack 58→48, explicit `KillSingle`, strict Ready at room 2; no usable native Collect button | Exact 37-joint ghost-controller binding diagnostic complete; other ghost and 37-joint rows remain separate claims |
| lich A | `enLichA` / 121651 | Pass, ordinary Attack 68→64, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; other magic-controller rows remain separate claims |
| Harazuel boss 2 | `cultistFinalBoss` / 121187 | Pass, eight bounded native attacks at 198→198, stopped at `no_hp_loss_unclassified` | Exact 37-joint Harazuel magic binding and repeatable armor/block boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| Harazuel boss 3 | `cultistFinalBoss` / 121188 | Pass, eight bounded native attacks at 207→207, stopped at `no_hp_loss_unclassified` | Exact 37-joint Harazuel magic binding and repeatable fire/interrupt/evade boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| Harazuel boss 4 | `cultistFinalBoss` / 121189 | Pass, eight bounded native attacks at 216→216, stopped at `no_hp_loss_unclassified` | Exact 37-joint Harazuel magic binding and repeatable lightning shield/resist/dodge boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| Harazuel minion C | `enWraith` / 121251 | Pass, bounded native attacks, accepted ordinary Attack 77→72 on attempt 2 after 77→77, explicit `KillSingle`, strict Ready at room 2; no usable native Collect button | Exact 37-joint Harazuel controller binding diagnostic complete; earlier no-loss outcome is preserved and no cause is inferred |
| goblin A | `enGoblinA` / 121514 | Pass, ordinary Attack 58→50, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint blunt-controller binding diagnostic complete; other goblin rows remain separate claims |
| goblin B | `enGoblinArcher` / 121487 | Pass, ordinary Attack 58→52, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; other goblin and bow rows remain separate claims |
| goblin C | `enGoblinWiz` / 121427 | Pass, ordinary Attack 58→53, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; other goblin and magic rows remain separate claims |
| goblin D | `enGoblinGrunt` / 121270 | Pass, ordinary Attack 58→57, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; other goblin and spear rows remain separate claims |
| goblin E | `enGoblinAssassin` / 121642 | Pass, ordinary Attack 58→51, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint bladed-controller binding diagnostic complete; other goblin and bladed rows remain separate claims |
| fox Fighter | `enFoxFighter` / 121590 | Pass, ordinary Attack 58→54, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; fox Fighter B remains a separate claim |
| fox Fighter B | `enFoxFighter` / 121591 | Pass, ordinary Attack 58→53, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint unarmed-controller binding diagnostic complete; fox Fighter remains separate despite the shared renderer mesh |
| beastman D | `beastman02` / 121355 | Pass, ordinary Attack 58→53, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; other beastman rows remain separate claims |
| drowned corpse A | `enDrownedSailor` / 121406 | Pass, ordinary Attack 58→53, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint zombie-controller binding diagnostic complete; other zombie and 37-joint rows remain separate claims |
| ghoul A | `enGhoul` / 121430 | Pass, ordinary Attack 58→56, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint ghoul-controller binding diagnostic complete; other ghoul and 37-joint rows remain separate claims |
| skelly G | `skelly01naked` / 120981 | Pass, ordinary Attack 58→48, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint blunt-controller binding diagnostic complete; other skelly rows remain separate claims |
| snow goblin A | `enSnowGoblinA` / 121383 | Pass, ordinary Attack 58→57, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint blunt-controller binding diagnostic complete; other snow-goblin rows remain separate claims |
| snow goblin B | `enSnowGoblinB` / 121466 | Pass, ordinary Attack 58→50, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; other snow-goblin rows remain separate claims |
| snow goblin D | `enSnowGoblinD` / 121358 | Pass, ordinary Attack 58→48, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint magic-controller binding diagnostic complete; other snow-goblin rows remain separate claims |
| snow goblin C | `enSnowGoblinC` / 121377 | Pass, ordinary Attack 58→55, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; other snow-goblin rows remain separate claims |
| pirate D01 | `enPirateD01` / 121304 | Pass; eight bounded native attacks each observed 90→90 and stopped at `no_hp_loss_unclassified` | Exact 37-joint shield-controller binding and repeatable no-loss boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| bandit E | `enBanditE` / 121650 | Pass, ordinary Attack 58→55, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; other bandit rows remain separate claims |
| bandit F | `enBanditF` / 121516 | Pass, ordinary Attack 58→56, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; other bandit rows remain separate claims |
| vampire A | `enVampireB` / 121399 | Pass, bounded native attacks, accepted ordinary Attack 72→62 on attempt 2 after 72→72, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint rapier-controller binding diagnostic complete; earlier no-loss outcome is preserved and no cause is inferred |
| snow beast A | `en_SnowBeastA` / 121318 | Pass, ordinary Attack 58→50, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; other snow-beast rows remain separate claims |
| snow beast B | `enSnowBeastManB` / 121507 | Pass, ordinary Attack 58→50, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint spear-controller binding diagnostic complete; other snow-beast rows remain separate claims |
| snow beast C | `enSnowBeastManC` / 121657 | Pass, ordinary Attack 58→48, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint blunt-controller binding diagnostic complete; other snow-beast rows remain separate claims |
| hobgoblin A | `enHobGoblinA` / 121422 | Pass, ordinary Attack 58→56, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint two-handed blunt-controller binding diagnostic complete; hobgoblin B and other blunt-controller rows remain separate claims |
| hobgoblin B | `enHobGoblinB` / 121565 | Pass, bounded native attacks, accepted ordinary Attack 58→56 on attempt 4 after three 58→58 outcomes, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint one-handed blunt-controller binding diagnostic complete; earlier no-loss outcomes are preserved and no cause is inferred |
| pirate D | `enDrunkPirateD` / 121273 | Pass, ordinary Attack 58→53, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint one-handed blunt-controller binding diagnostic complete; pirate variants remain separate claims |
| pirate E | `enDrunkPirateE` / 121683 | Pass, ordinary Attack 58→45, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint one-handed bladed-controller binding diagnostic complete; pirate variants remain separate claims |
| snow goblin E | `enSnowGoblinE` / 121360 | Pass, ordinary Attack 58→53, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint one-handed bladed-controller binding diagnostic complete; other snow-goblin rows remain separate claims |
| bandit G | `enFrozenBanditA` / 121412 | Pass, ordinary Attack 58→50, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; frozen bandit variants remain separate claims |
| bandit H | `enFrozenBanditB` / 121589 | Pass, ordinary Attack 58→53, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint one-handed blunt-controller binding diagnostic complete; frozen bandit variants remain separate claims |
| kobold C | `enKoboldB` / 121675 | Pass, ordinary Attack 58→50, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; other kobold rows remain separate claims |
| boomer A | `enPirtaeSkellyBoner` / 121655 | Pass, ordinary Attack 135→133, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint boomer-controller binding diagnostic complete; weapon-controller rows remain separate claims |
| pirate C | `enPirateC02` / 121608 | Pass, ordinary Attack 58→52, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint musket-controller binding diagnostic complete; other pirate rows remain separate claims |
| pirate C02 | `enPirateC02` / 121608 | Pass, ordinary Attack 59→53, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint cannon-controller binding diagnostic complete; shared pirate C renderer remains a separate controller claim |
| pirate A03 | `enDrunkPirateE` / 121683 | Pass, ordinary Attack 58→53, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint blunderbuss-controller binding diagnostic complete; shared pirate E renderer remains a separate controller claim |
| pirate B03 | `enPirate02` / 120971 | Pass, ordinary Attack 58→52, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint musket-controller binding diagnostic complete; other pirate rows remain separate claims |
| pirate C03 | `enPirateC03` / 121385 | Pass, ordinary Attack 72→62, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint dual-weapon-controller binding diagnostic complete; other pirate rows remain separate claims |
| golem A | `enAtztecGolem_Fire` / 121493 | Pass, ordinary Attack 58→54, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint unarmed-controller binding diagnostic complete; native fire emission obscures detailed selected-frame geometry |
| golem B | `enAtztecGolem_Frost` / 121332 | Pass, ordinary Attack 72→71, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint unarmed-controller binding diagnostic complete; frosted golem row remains separate from golem A |
| desert bandit A | `enDesertBanditA` / 121374 | Pass, ordinary Attack 58→50, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint dual-weapon-controller binding diagnostic complete; desert bandit rows remain separate claims |
| desert bandit C | `enDesertBanditC` / 121418 | Pass, ordinary Attack 58→48, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; desert bandit rows remain separate claims |
| jungle kobold A | `enJungleKoboldA` / 121473 | Pass, ordinary Attack 58→54, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint dual-weapon-controller binding diagnostic complete; jungle kobold rows remain separate claims |
| jungle kobold B | `enJungleKoboltB` / 121672 | Pass, ordinary Attack 58→48, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; jungle kobold rows remain separate claims |
| jungle kobold C | `enJungleKoboldC` / 121313 | Pass, ordinary Attack 58→48, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint wand-controller binding diagnostic complete; jungle kobold rows remain separate claims |
| warrior A | `enJungleWarriorA` / 121402 | Pass, ordinary Attack 58→52, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint dual-weapon-controller binding diagnostic complete; warrior rows remain separate claims |
| warrior B | `enAncientWarriorB` / 121353 | Pass, ordinary Attack 58→53, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; warrior rows remain separate claims |
| warrior C | `enJungleDruid_B` / 121337 | Pass, ordinary Attack 58→52, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint wand-controller binding diagnostic complete; warrior rows remain separate claims |
| jungle skelly A | `enJungleSkelly` / 121382 | Pass, ordinary Attack 58→57, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint dual-weapon-controller binding diagnostic complete; jungle skelly row remains separate from other skelly assignments |
| jungle zombie A | `enJungleCorpseA` / 121325 | Pass, ordinary Attack 59→58, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint zombie-controller binding diagnostic complete; corpse-controller rows remain separate claims |
| jungle troll A | `enJungleTroll` / 121520 | Pass, ordinary Attack 162→152, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint troll-controller binding diagnostic complete; troll row remains a separate claim |
| jungle zombie B | `enJungleCorpseB` / 121352 | Pass, ordinary Attack 81→71, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint ghoul-controller binding diagnostic complete; shared corpse rows remain separate claims |
| scourge minion J2 | `enJungleCorpseB` / 121352 | Pass, ordinary Attack 58→48, explicit `KillSingle`, no native Collect, strict Ready at room 2 | Exact 37-joint zombie-controller binding diagnostic complete; shared corpse renderer remains a separate controller claim |
| warrior D | `enAncientWarriorA` / 121417 | Pass, three bounded no-loss Attacks 58→58, ordinary Attack 58→56 on attempt 4, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint one-handed blunt-controller binding diagnostic complete; bounded retry and warrior rows remain separate claims |
| warrior E | `enJungleWarriorB_B` / 121689 | Pass, ordinary Attack 58→48, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint dual-weapon-controller binding diagnostic complete; warrior rows remain separate claims |
| warrior F | `enAncientDruid_B` / 121429 | Pass, two bounded no-loss Attacks 58→58, ordinary Attack 58→50 on attempt 3, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint wand-controller binding diagnostic complete; bounded retry and warrior rows remain separate claims |
| warrior B02 | `enAncientWarriorB_B` / 121576 | Pass, one bounded no-loss Attack 58→58, ordinary Attack 58→53 on attempt 2, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint bow-controller binding diagnostic complete; bounded retry and warrior rows remain separate claims |
| warrior C02 | `enJungleDruid` / 121498 | Pass, ordinary Attack 58→48, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint wand-controller binding diagnostic complete; warrior rows remain separate claims |
| warrior D02 | `enAncientWarriorA_B` / 121285 | Pass, eight bounded no-focus Attacks 68→68, and a fresh focused Attack 68→68; stopped before fixture, loot, or Ready | Exact 37-joint one-handed blunt-controller binding diagnostic; no damage cause inferred under regular or max-focus attacks, and no complete compatibility claim |
| warrior E02 | `enJungleWarriorB` / 121549 | Pass, ordinary Attack 63→50, explicit `KillSingle`, one native Collect, strict Ready at room 2 | Exact 37-joint dual-weapon-controller binding diagnostic complete; warrior rows remain separate claims |
| warrior F02 | `enAncientDruid` / 121423 | Pass, four bounded no-loss Attacks 63→63, ordinary Attack 63→59 on attempt 5, explicit `KillSingle`, two native Collects, strict Ready at room 2 | Exact 37-joint wand-controller binding diagnostic complete; bounded retry and warrior rows remain separate claims |
| Aztec boss | `enJungleElder` / 121503 | Pass, one bounded no-loss Attack 315→315, ordinary Attack 315→312 on attempt 2, explicit `KillSingle`, no native Collect, strict Ready at room 2 | Exact 37-joint runtime two-handed magic-controller binding diagnostic complete; serialized renderer animator remains one-handed wand |
| fish B02 | `enFishD` / 121620 | Pass, ordinary Attack 58→54, explicit `KillSingle`, native Ready 0/2 | Exact 34-joint spear binding diagnostic complete; other fish rows remain separate claims |
| fish B03 | `enFishE` / 121686 | Pass, ordinary Attack 58→48, explicit `KillSingle`, native Ready 0/2 | Exact 34-joint staff binding diagnostic complete; other fish rows remain separate claims |
| fish C01 | `enFishF` / 121649 | Pass, ordinary Attack 58→57, explicit `KillSingle`, native Ready 0/3 | Exact 34-joint spear binding diagnostic complete; other fish rows remain separate claims |
| fish C02 | `enFishG` / 121628 | Pass, ordinary Attack 58→50, explicit `KillSingle`, native Ready 0/2 | Exact 34-joint staff binding diagnostic complete; earlier protection-boundary attempts remain recorded separately |
| fish D01 | `enFishD01` / 121543 | Pass, bounded native attacks, accepted ordinary HP 72→71 on attempt 6, explicit `KillSingle`, native Ready 0/2 | Exact 34-joint spear binding diagnostic complete; earlier block outcomes are preserved and no cause is inferred |
| fish D02 | `enFishD02` / 121479 | Pass, bounded native attacks, accepted ordinary HP 68→56 on attempt 2, explicit `KillSingle`, native Ready 0/3 | Exact 34-joint staff binding diagnostic complete; earlier no-loss outcome is preserved and no cause is inferred |
| fish B01 | `enFishH` / 121298 | Pass, ordinary Attack 58→48, explicit `KillSingle`, native Ready 0/2 | Exact 34-joint unarmed binding diagnostic complete; other fish rows remain separate claims |
| fish A02 | `enFishB` / 121286 | Pass, ordinary Attack 58→51, explicit `KillSingle`, native Ready 0/3 | Exact 34-joint spear binding diagnostic complete; other fish rows remain separate claims |
| fish A03 | `enFishC` / 121594 | Pass, ordinary Attack 58→48, explicit `KillSingle`, native Ready 0/2 | Exact 34-joint staff binding diagnostic complete; other fish rows remain separate claims |
| mimicClam A | `enClam` / 121306 | Pass, ordinary Attack 58→48, explicit `KillSingle`; one native Collect click accepted but loot did not progress to Ready | Exact seven-joint binding diagnostic complete; once-only runner stopped at unchanged native loot state, with no second Collect or forced Ready; no finished-art acceptance |
| bat Jungle A | `enJungleBat` / 121437 | Pass, ordinary Attack 58→53, explicit `KillSingle`, native Ready 0/2 | Exact 53-joint bat binding diagnostic complete; other bat rows remain separate claims |
| bear A | `enBear02` / 121534 | Pass, ordinary Attack 58→48, explicit `KillSingle`, native Ready 0/2 | Exact 38-joint bear binding diagnostic complete; bearB and bearC remain separate claims |
| bear C | `enBear03` / 121486 | Pass, ordinary Attack 81→76, explicit `KillSingle`, native Ready 0/2 | Exact 38-joint bear binding diagnostic complete; bearA and bearB remain separate claims |
| wisp A | `enWisp` / 121121 | Pass; eight bounded native attacks each observed 58→58 and stopped at `no_hp_loss_unclassified` | Exact two-joint floating-eye binding and repeatable no-loss boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| crab A | `CrabGeo` / 121579 | Pass, bounded native attacks, accepted ordinary HP 58→53 on attempt 3, explicit `KillSingle`, native Ready at room 2 | Exact 63-joint crab binding diagnostic complete; earlier no-loss attempts are preserved and no combat cause is inferred |
| cockatrice A | `enChicken` / 121328 | Pass, ordinary Attack 58→48, explicit `KillSingle`, native Ready 0/2 | Exact 50-joint chicken-controller binding diagnostic complete; boss cockatrice remains a separate claim |
| boss cockatrice | `enCockatrice` / 121554 | Pass; eight bounded native attacks each observed 540→540 and stopped at `no_hp_loss_unclassified` | Exact 50-joint boss binding and repeatable no-loss boundary captured; no cause, fixture death, loot or Ready acceptance inferred |
| monkey A | `enMonkeyA` / 121679 | Pass; native `enSuicideBlast` applies secondary damage 58 before the hero attack and self-terminates the target | Exact 42-joint monkey binding and repeatable native self-termination boundary captured; no ordinary attack, fixture death or Ready acceptance inferred |
| modern Kraken head V4 | `kraken2` / 121035 plus rigid `kraken2_eye` / 101310 | Original composite: 120-frame Pass and ordinary Attack 324→316, 85-frame `KillSingle` prefix, strict Ready 0/2 | Exact skinned head plus one-`MeshFilter` static eye replacement are archived; the practical static eye is not art-approved |
| Kraken tentacle | `krakenTentacle` / 121595 | Canonical V2 exact binding, complete Pass and ordinary Attack 162→154, accepted 91-frame animated fixture-death prefix, native Ready 0/2 without Collect | Exact `krakenTentacleController` route is [canonical](../art-experiments/abyssal-kraken/live-validation-sargassum-primary-v2-canonical/README.md); the original art remains unapproved |
| Kraken tentacle mirror | `krakenTentacle` / 121595 | Fresh V2 Pass, ordinary Attack 162→152, explicit `KillSingle` fixture prefix, native Ready 0/2 | Exact `krakenTentacleControllerMirrored` binding is [archived](../art-experiments/abyssal-kraken/live-validation-v2-kraken-tentacles/README.md); the original art remains unapproved |
| Sea King tentacle A | `KrakenGodTentacle` / 121315 | Fresh V3 Pass, ordinary Attack 270→266, explicit `KillSingle` fixture prefix, native Ready 0/2 | Exact `krakenTentacleController` binding is [archived](../art-experiments/abyssal-kraken/live-validation-v3-seaking/README.md); the original art remains unapproved |
| Sea King tentacle B | `KrakenGodTentacle` / 121315 | Fresh V3 Pass, ordinary Attack 270→266, explicit `KillSingle` fixture prefix, native Ready 0/2 | Exact `krakenTentacleControllerMirrored` binding is [archived](../art-experiments/abyssal-kraken/live-validation-v3-seaking/README.md); the original art remains unapproved |

The stopped `no_hp_loss_unclassified` result is preserved as evidence rather
than relabeled as a dodge, block, immunity or tool failure. Fish D01 and D02
also preserve each bounded native retry and accept the row only after measured
nonlethal HP loss. MimicClam A preserves its one accepted Collect click and
the unchanged loot boundary; it does not authorize a second click or forced
Ready. The next repeatable step for stopped rows is a native
combat-cause trace; only after that trace should the ordinary damage, lethal,
loot and visual checks be retried. The generated [49-topology coverage plan](MODEL-TOPOLOGY-COVERAGE.md)
separates 43 groups with direct serialized enemy rows from six groups without
one. Those six have explicit ownership routes: two resource-prefab variants,
three player-avatar groups, and the unsupported empty Mayor renderer. This
resolves inventory ownership only; each exact source/controller pair still
needs its own binding, live, and art verdict.

The same report now records one or more indexed original-model record for all
43 direct-enemy topology groups. That is an authored-example floor, not
acceptance for every source pair, sibling, controller, live gate, or finished
art result.

The generated [validation evidence ledger](MODEL-VALIDATION-GATES.md) records
which indexed archives contain explicit structured binding, review, motion,
ordinary-damage, and Ready fields. Its `recorded` cells are evidence-presence
markers rather than approvals; use the linked archive and the completion rules
in [MODEL-AUTHORING.md](MODEL-AUTHORING.md#evidence-and-completion) for a real
verdict.

## Duskquill Raven live trial

Duskquill Raven has [scoped original crowC live evidence](../art-experiments/duskquill-raven/live-validation.json):
three 120-frame action/hit/explicit-death captures, a visible 13-point critical
hit, and one native Collect reaching strict Ready at level 0, room 2. Selected
flight and portrait views are readable; hero occlusion and native blur limit
fine hit/death review. This does not cover the other bird rows.

The fresh [Duskquill V2 supplement](../art-experiments/duskquill-raven/live-validation-v2/validation.json)
repeats the exact crowC `enCrow` binding in session
`594b337748914937b26473ee389ea71e`. The authored body is active on
owner `369188` at public visual-scale factor 1.0 (captured native CEL scale
0.9); selected idle and attack views retain the connected wings, head and beak.
Ordinary damage is58 to53 with `cheat=None` and no focus. The explicit
`KillSingle` fixture completes 120 frames, reaches strict Ready at level0 room2,
and exposes a direct Ready vote with no Collect action. The small/dark airborne
silhouette, native UI/effects and victory overlay limit fine feather/eye detail,
complete death deformation, culling, portraits, resource lifetime and finished-
art acceptance.

The canonical [Duskquill V3 supplement](../art-experiments/duskquill-raven/live-validation-v3/validation.json)
narrows credit to the exact `crowC / enCrow / renderer 120964` source in session
`8233b0fa916b47d78e7a6cf5a33ba625`. Three complete 120-frame captures preserve
the exact renderer through `BirdFlySlow`, native `birdAttack2`, ordinary
`BirdDamage`, recovery, a later native `birdAttack2`, and explicit-fixture
`BirdDeath`. Ordinary no-focus damage is 58 to 50. The separate fixture records
`m_DoRagdoll=false` and zero rigid bodies, so the sampled spread-wing floor pose
is animated death evidence, not ragdoll or ordinary lethal evidence. The exact
renderer remains active, enabled, and visible in all 360 frames; two native
Collect actions reach strict Ready at level 0 room 2. Twenty reviewed originals
accept the attached raven silhouette through the sampled intervals while small
scale, effects, foreground overlap, loot UI, depth blur, later corpse lifetime,
culling, portraits, resource lifetime, and final art approval remain open.

V3 also pins an earlier isolated run as rejected boundary evidence. That run
kept the exact binding through pass frame 50, then the native crow deactivated
from frame 51 and was destroyed after 107 of 120 requested frames without a
death trigger or combat event. The stochastic escape or self-removal is not a
binding failure and does not replace the successful fresh process.

## Sunspire Roc live trials

[Sunspire Roc](../art-experiments/sunspire-roc/README.md) is an original closed-
panel, articulated-wing model for the exact `rocA` `enRoc01` renderer 121238.
Its source generator reads only bone names and inverse bind matrices; the
separate proof reruns it while rejecting native surface reads. The model uses
the native 36-bone palette, including the full scapula-to-fingertip wing chains,
leg/talon chains, jaw, central tail, and side-tail chains.

The canonical [V4 exact-source archive](../art-experiments/sunspire-roc/live-validation-v4/README.md)
preserves the fresh [V2 ordinary archive](../art-experiments/sunspire-roc/live-validation-v2/validation.json).
That session records the original `ftkmf_glb_sunspire-roc.glb` under owner369188 with its
expected bone signature and native0.9 scale. Pass, ordinary `Attack` HP 81→71,
and explicit `KillSingle` each retain 120 frames; nine reviewed stills show the
head, beak, torso, wings, talons, and tail remaining connected. Two guarded
native Collect actions then reach strict Ready0/2. The separate [V1 archive](../art-experiments/sunspire-roc/live-validation-v1/validation.json)
preserves a fresh `Attack(focus)` HP 81→71 session without relabeling it as
ordinary evidence. Native UI/effects and victory overlay limit fine detail;
[V3](../art-experiments/sunspire-roc/live-validation-v3/validation.json) adds
one fresh constructed native `uiEnemyEncounterPortrait.Initialize` row-preview
at strict Ready. The trace identifies the exact custom 36-bone `enRoc01` clone,
the authored GLB and texture, and a reviewed 328×280 PNG; the temporary clone,
owned UI texture, and recorded preview lease assets are released. It is not an
opened encounter menu or combat-HUD layout.

V4 compares the selected historical and current catalog profiles before reuse,
pins all 360 combat images plus the portrait, adds a fresh root review of six
originals, and records every required source-specific gate in one independently
verified archive. The overall catalog changed only because unrelated profiles
were added; the selected profile, assets, source renderer, and motion renderer
remain identical. This is an evidence-layout repair, not a replay or a broader
source claim. Ordinary lethal damage, every
portrait layout/cache path, full culling, settled corpse/ragdoll, final resource
lifetime, full art approval, `rocB`, jungle Roc variants, and other bird rigs
remain separate checks.

## Tidecrown Sea King live trial

[Tidecrown Sovereign](../art-experiments/tidecrown-sea-king/README.md) is an
original 60-bone sea monarch for the exact `seaKing` `enSeaKing` renderer 121357.
Its generator consumes only the extracted palette names and inverse bind
matrices, and its proof reruns with native input restricted to those two keys.
The body deliberately omits the game-owned trident, shield, breakable props,
tentacles, and ragdoll geometry. Its 1.0 public visual scale preserves the
captured native root scale; the profile disables inherited `matLoot` emission
and keeps the head `PortraitCam` registration separate from portrait pixels.

The historical [V1 archive](../art-experiments/tidecrown-sea-king/live-validation-v1/validation.json)
binds the authored GLB and PNG to one exact `enSeaKing` owner with the expected
60-bone signature and `seaKingController`. A bounded native `Attack(focus)`
retry records a same-target HP change from720 to719 on attempt four. Its first
three focus attempts and a separate fresh eight-attempt ordinary no-focus run
are preserved as `no_hp_loss_unclassified`, so the focus result does not get
relabeled as ordinary evidence or a combat-cause explanation. The explicit
`KillSingle` fixture retains 91 frames: recorded ragdoll bodies become dynamic
at frame28, and the custom renderer is inactive/invisible in the final retained
frame. The guarded sequence observes strict Ready0/2 without a Collect
submission. Eight reviewed stills and two presentation videos establish sampled
body/trident coexistence and exposed torso/mantle continuity, but the giant
encounter camera, player overlap, UI and effects hide the head/crown, full robe
and lower body. It does not establish ordinary lethal damage, a settled corpse,
final cleanup/material lifetime, portrait pixels, all-angle culling, tentacle or
breakable-prop compatibility, another 60-bone humanoid, or finished-art
acceptance.

The newer [V2 canonical archive](../art-experiments/tidecrown-sea-king/live-validation-v2-canonical/validation.json)
repeats that exact source binding in session `8b09f62e7e9a411199a46fa6466a0556`
and replaces the focus-only gameplay gap. A prior fresh bounded run retained
eight exact Blocks at 720 HP with the unmodified hero skill and stopped. V2
then applies the documented disposable hero fixture, raising only the equipped
`bluntSmithHammer` toughness augmentation from 0.81 to FTK's native 0.95 cap.
The first two ordinary zero-focus actions still Block; attempt three records
native `Damaged`, one damage and HP 720 to 719. Four complete 120-frame captures
preserve idle, native attacks, both blocks, hit response and recovery. The
91-frame fixture-death prefix shows the animator and all bodies unchanged
through frame 27, all 12 active bodies dynamic at frame 28, the exact renderer
active and visible through frame 89, and inactive and not visible at frame 90.
No Collect occurs, and strict Ready is observed at level 0 room 2 with one
active button. Twenty-three reviewed originals establish coherent sampled
deformation and ragdoll collapse, while the giant camera crop and native effects
keep the head, crown, full robe, portraits and finished-art judgment outside the
result.

The fresh [Basilight V2 supplement](../art-experiments/basilight-cockatrice/live-validation-v2/validation.json)
repeats the exact cockatriceC `enChicken` binding in session
`db97671239c04b4e899fb14569fd72e7`. The 50-joint authored body is active on
owner `369188` at public visual-scale factor 1.0 (captured native CEL scale
0.55); selected idle and attack views retain the jade body, crest, beak, wings,
legs and claws. Ordinary damage is72 to62 with `cheat=None` and no focus. The
explicit `KillSingle` fixture completes 120 frames, one guarded native Collect is
accepted, and strict Ready is observed at level0 room2. Native UI/effects and
the victory loot surface limit fine crest/feather detail, complete death
deformation, culling, portraits, resource lifetime and finished-art acceptance.

The canonical [Basilight V3 exact-source supplement](../art-experiments/basilight-cockatrice/live-validation-v3/validation.json)
pins a fresh queue-route run to exact `cockatriceC / enChicken / 121484`, owner
369188, native scale 0.55, and the expected 50-bone signature. Three complete
120-frame captures preserve the hooked beak, crest, jade body, wing panels,
long legs, claws, and curled tail through idle, native `AttackProf1` and
`AttackProf`, an ordinary HP 72 to62 `Damaged`/`Cockatrice_HitSmall` response and
recovery, and the explicit-fixture `Death`/`Cockatrice_DeathHuge` recoil,
backward fall, and coherent prone finish. Eighteen reviewed original PNGs show
the custom renderer still visible behind the sampled loot handoff. The Animator
remains enabled and native `m_DoRagdoll` remains false, so this is an animated
death rather than physics-ragdoll evidence. Native feathers, the foreground
hero, and bright effects obscure the impact frames. Ordinary lethal behavior,
later corpse lifetime, other native attacks, portrait, collision, extended
culling, long-session lifetime, full campaign completion, final art approval,
and sibling cockatrice routes remain outside this exact-source result.

## Basilight Cockatrice live trial

Basilight Cockatrice has [scoped original cockatriceC live evidence](../art-experiments/basilight-cockatrice/live-validation.json):
three 120-frame captures, normal 10-point hit, explicit animated
`Cockatrice_DeathHuge` and two native Collect actions reaching Ready at level 0,
room 3. Five reviewed images show its colored body, custom portraits, recoil and
fallen form; effects, occlusion and blur limit finer checks. Other cockatrice
bind/controller/resource cases remain separate.

## Cube material dependency

The three-joint cube rig needs per-row material reconciliation. CubeA has a
stationary interior material in slot 0 and a native UV scroller targeting jelly
material slot 1. The pinned single-material replacement is incompatible with
that index. CubeE shares its exact binds and controller but has one material and
no scroller in the inspected subtree; testing CubeE cannot close CubeA's gap.
The [source evidence](evidence/cube-material-compatibility-source-v1/README.md)
records identities, implementation hashes and the required material ownership,
scrolling, rollback and live checks. This is source evidence, not live acceptance.

The canonical [Mossglass V5 exact-source supplement](../art-experiments/mossglass-reliquary/live-validation-v5/validation.json)
closes the Cube A representative with fresh runtime evidence. One owner binds
the exact two-primitive custom mesh to `cubeA / enJellyCube / 121012` at native
scale 1.1 and the expected three-bone signature. Three complete 120-frame captures
preserve idle, native `AttackCrit`/`AOE_jelly` compression and recovery, an
ordinary HP 58 to53 `Damaged`/`wobble_jelly` response and recovery, and the
explicit-fixture `Death`/`deathHeavy_jelly` disappearance. Two guarded Collects
reach strict Ready0/2. Per-frame readback keeps authored slot0 fixed and advances
the native slot1 `_MainTex` scroller at rate `(0,0.2)` while both material
regions remain visible. Eighteen reviewed originals show no sampled detached
rib, inverted cap, missing surface before disappearance, or viewport clipping.
The death keeps the renderer active and Animator enabled with
`m_DoRagdoll=false`; the geometry leaves view and no corpse is claimed. Ordinary
lethal behavior, perceptual scroll continuity, clone independence, other cube
routes, portraits, collision, extended culling, long-session lifetime, full
campaign completion, and final art approval remain outside this result.

## Kraken resource variant with a deliberate adapter

The [existing-driver live observation](evidence/kraken-existing-drivers-v1/README.md)
found six modern input paths absent from the old native hierarchy across the
241-frame appearance fixture. Only `Root_M` existed, with an approximately
1-unit maximum difference from the modern input. This prevents treating those
missing drivers as an available production adapter; it does not justify a
guessed root correction. The original Gloamfin blockout separately passed paired
constructed-fixture checks for appearance, damaged, damaged-heavy, death and
death-light, with sparse visual reviews and no gameplay acceptance.

The Kraken resource variant has a verified exact load basename
`enkrakenhead` (GameObject8268, CEL135962, renderer 121260 at `krakenHead`). It is
separate from the native enemy row `krakenHead`, which references the different
`enKraken` prefab through CEL136951. Resource registration does not establish
native combat use or artistic compatibility.

Its five-bone palette follows `Root_M/joint1/...`. Source binding analysis shows
`krakenAttack`, `krakenIdle`, `krakenDamage`, and `krakenDisappear` bind none of
those five palette bones. `kraken_appear` binds four. All five clips bind the
common `Root_M` ancestor, so the whole head may still move; this is not evidence
that the variant is unused or motionless. The analysis checks serialized path
bindings, not whether those ancestor curves vary, and scans clips in
`resources.assets` only.

This variant therefore uses a deliberate hierarchy adapter for articulated main
combat motion. It remains excluded from the ordinary generic resource-prefab
profile path. The [source index](model-skeleton-candidates.json)
records the exact scope and SHA-256 references for the binding analysis,
resource-path inventory, and enemy-base preflight. No native geometry or
animation curves are reproduced in the index.

The route now has an explicit [production adapter contract](evidence/kraken-production-adapter-design-v1/README.md)
and [machine-readable gates](evidence/kraken-production-adapter-design-v1/contract.json).
It specifies the exact old-to-modern endpoint map, native-controller authority,
event-disabled sampling surface, matrix and rollback guards, lifecycle ownership,
and the production observations required before this route can receive
canonical coverage credit.

The [canonical Gloamfin V2 archive](../art-experiments/gloamfin-kraken/live-validation-v2-canonical/validation.json)
now closes the exact `krakenHead / enkrakenhead / krakenHead / 121260` route.
It reconciles the two-owner production campaign with fresh reviewed combat
captures and a native portrait follow-up. The visual run preserves exact
binding through idle, native attack, ordinary HP 324 to316 damage and recovery,
and an explicit fixture-death prefix through renderer cleanup. Strict Ready is
observed at level0 room2. The production campaign separately proves ordinary
lethal death, enemy victory and natural teardown.

The pre-fix initiative portrait was a blank teal tile. Framework
`60917286ce044bfc57480ac34596cf54a596e6db0ec8a179c2518502f0847510`
now measures the owned posed mesh against the native 204x172 render target and
scales only the disposable portrait clone when translation cannot satisfy the
native 20-degree, 0.3-to-30 frustum. The accepted exact-route capture uses scale
0.8447812795639038, keeps the source at scale 1.0, shows the complete readable
face and upper body, and is referenced by six active initiative images. The
fallback is exact-resource and ownership guarded; explicit portrait markers
retain priority. Other projections, all-camera culling, a persistent corpse,
sibling sources and finished-art approval remain outside this result.

## Discovery snapshot

A read-only scan of the local `resources.assets` found 750 skinned renderers,
467 beneath a `CharacterEventListener`, and 49 candidate bone-topology groups.
The [metadata snapshot](model-skeleton-candidates.json) records all 49 groups,
representative object paths, controller names, renderer IDs, bind-pose variant
counts, and the asset-file hash. It contains no mesh geometry, textures,
controllers, or animation curves.

This is not a count of enemy skeletons. It includes player/NPC models, portraits,
accessories, and multipart subsets. It does not map enemy database rows to live
combat prefabs, and excludes external bundles, rigid renderers, and separate
weapons. A complete enemy register requires that mapping and inventory scope
expansion before its coverage can be called complete.

The 37-joint topology alone includes 228 renderers. Equal topology can conceal
different bind poses and proportions. Serialized controller associations may
also change at spawn: renderer 121152 has `Enemy_Controller` in the assets,
while the observed cave troll used `trollController` in combat.

These representative candidates provide varied anatomy for subsequent exercises.
Every row below is discovery-only, with no custom-model validation claimed:

| Serialized controller | Joint count | Example renderer ID |
|---|---:|---:|
| `wolfController` | 33 | 120975 |
| `spiderController` | 65 | 121386 |
| `batController` | 53 | 121104 |
| `bearController` | 38 | 121467 |
| `birdController` | 39 | 120960 |
| `dragonController` | 70 | 121525 |
| `crabController` | 63 | 121411 |
| `snakeController` | 44 / 46 | 121424 / 121365 |
| `mimicController` | 9 | 121192 |
| `jellyCubeController` | 3 | 121011 |

IDs apply only to the recorded asset/build. Verify the actual body renderer and
live controller before extraction or enemy registration. Do not substitute one
of these IDs into the troll content example and assume a valid enemy mapping.

## Extending coverage to every enemy skeleton

The cave-troll baseline now includes native hit, attack, ragdoll, and completed
combat evidence. Extend the register with one target from each
anatomical category to exercise the tooling: another humanoid, a quadruped,
a flying creature, a multi-legged creature, and a deforming/segmented body.
These are useful trial categories, not the final set of rigs.

Reconcile every enemy database row to its actual combat prefab/body renderers.
Classify each as a supported skinned profile candidate, a rigid/static model,
a multipart creature needing multiple renderers, or unresolved with a reason.
Deduplicate only after comparing names, hierarchy, bind/rest transforms,
coordinate space, and live controller. Maintain the mapping even for cosmetic
variants so coverage has a defined denominator.

For each resulting profile, repeat the [authoring exercise](MODEL-AUTHORING.md)
with an original model and attach separate export, binding, appearance, idle,
attack, hit, death, and gameplay evidence. Do not infer those results for the
rest of the topology group. Variants can reuse evidence only when equivalence
is established and a representative spawn check confirms the right renderer.

Use these status values per check: `pending`, `pass`, `fail`, or `not applicable`
with a reason. A discovered profile advances to export-tested, then live-loaded,
then motion-validated only as the corresponding evidence is collected.

Record profile key; enemy row/prefab mapping; asset build/hash; renderer IDs;
joint count/topology and bind fingerprints; live controller/state paths; original
asset/source hashes; validation reports; captures; known clipping/material
issues; and untested variants. Keep extracted references in ignored scratch and
publish only descriptive metadata and original art.

## Resource brute diagnostic evidence

`enbaseybrute` has a recorded native resource-fixture run on the `cragHulk`
base: renderer 121234 at exact CEL path `enBaseyBrute`, session
`033a44209e08452aba8858dcd3eda29c`, health 64 catalog `903d46b8...`.
Stage case `b0d9357fa5774b7ebf898be11c641636` established the original probe
binding. Native Pass, ordinary Attack (58 to56 HP), and explicit KillSingle
fixture (56 to0 HP) each recorded 120 unpaused frames over about 10.9083 seconds.
Death switched Animator off at frame28 with11 native rigidbodies and the native
ragdoll flag true. Frozen `deathHuge_bruteUnarmed` normalized0.165 after that
handoff is not failed death; per-body physics measurements are retained in
[the runtime validation index](model-runtime-validation.json).

The reviewed tall probe attacked/recoiled and collapsed without obvious
explosive deformation in visible areas. Hero/effect occlusion remains, and this
is diagnostic evidence rather than finished-art or complete-animation acceptance.
Two guarded Collect actions reached strict Ready at level0/room3. The index
records the exact assets, action/capture journals, six viewed frame hashes, and
scope limits; this single case does not validate the remaining resource groups.

## Armored wolf resource diagnostic evidence

`enarmoredwolf` on native `wolfA` bound original probe121531 at exact CEL path
`enArmoredWolf`, stage `1e06f4a672804a218a8e642d8e1bd3ec`, in session
`033a44209e08452aba8858dcd3eda29c` with the same health 64 catalog `903d46b8...`.
Pass sampled `attack_wolf` frames44..59; ordinary Attack reduced58 to47 HP,
with `damaged_wolf` frames25..37 and `attackProf_wolf` frames68..91. The explicit
KillSingle fixture reduced47 to0 HP. All three captures recorded 120 unpaused
frames over about 10.9083 seconds. Animator disabled at death frame27 with
14 native rigidbodies; `deathHeavy_wolf` freezes at normalized0.11 after the
physics handoff, which does not establish failed death.

Reviewed idle/return poses and death collapse were visible without obvious
explosive deformation in those areas. Attack and hit views had substantial hero
and effect occlusion, so motion and artistic acceptance remain incomplete. Two
guarded Collect actions reached strict Ready at level0/room4. The
[runtime index](model-runtime-validation.json) retains exact stage/assets,
three action/capture journals, six reviewed frame hashes, native physics metrics,
and the diagnostic-only limits.

## Snow goblin resource diagnostic evidence

`engoblinsnowa` on native `snowGoblinA` bound original probe121459 at exact CEL
path `enGoblinSnowA`, stage `712817733d554e14ad407dc261211e1d`, session
`033a44209e08452aba8858dcd3eda29c`, health 64 catalog `903d46b8...`.
Pass sampled `attack_blunt1H` frames45..68; ordinary Attack reduced58 to54 HP
with `damageLight` frames26..36. Explicit KillSingle reduced54 to0 HP. Each
capture recorded 120 unpaused frames over about 10.9083 seconds. Animator disabled
at death frame27 with15 native rigidbodies; `deathHeavy_blunt1H` stays at
normalized0.119565 after physics handoff, which does not establish failed death.

Reviewed humanoid calibration and the native weapon were clear, while the hero
partly occluded the lower body during attack/hit. Collapse was visible without
obvious explosion in reviewed areas. These are diagnostic observations, not
finished art or full-animation acceptance. One guarded Collect reached strict
Ready at level0/room5; this stair-boundary state is not a new enemy staging test.
The [runtime index](model-runtime-validation.json) records exact binding/assets,
three case/capture journals, five viewed frame hashes, and native physics metrics.

## Cinderbloom original prototype: facing review failed

The multipart original `Cinderbloom` v1 bound both `enPlant01` and
`enPlant01Leaves` and completed native attack, nonlethal hit and KillSingle death
captures with loot/Ready progression. Coherent petal/stem motion was observed,
but its mouth anatomy was authored facing opposite native head/jaw forward.
Valid rigs and fitted bounds did not establish anatomical orientation. Corrected
facing and small-detail readability need retesting; no final art acceptance.
[Archived v1 media and exact evidence](../art-experiments/cinderbloom-plant/live-validation-v1.json)
preserve the original asset hashes independently of subsequent source revisions.

## Emberjaw original prototype

The original multipart `Emberjaw` bound both native skull renderers and completed
recorded attack, nonlethal hit and explicit KillSingle death with loot/Ready
progression. Front-facing skull/jaw readability and wide independent jaw opening
were observed. Native purple effects change its appearance and obscure mouth/
death motion; the hero partly occludes hit motion and horns approach the upper
view edge. It is an integrated usable prototype, with final polish/culling and
full visible death deformation still pending. The
[archived media and live evidence](../art-experiments/emberjaw-skull/live-validation.json)
retain exact original asset hashes, three MP4s and eight reviewed PNGs.

## Cinderbloom v2 facing correction verified live

The corrected original Cinderbloom body now faces its mouth, teeth and throat
toward the player in native `plantA` combat. Both parts bound on the new body
hash; reviewed petal/stem motion, hit bend and collapse remained coherent.
The targeted facing fix passed in session `42f40ae3067344b786e959a92f35cbf2`.
This is an integrated usable prototype with small-detail polish, occlusion, full
culling and other variants still pending. [V2 live evidence and media](../art-experiments/cinderbloom-plant/live-validation-v2.json)
remain separate from the preserved v1 facing failure and original hashes.

## Fat resource diagnostic evidence

`enbaseyfat` on native `pirateD01` bound probe121340 at `enBaseyFat`, stage
`731a6ff74f04471baf825721fded8bd6`, session
`42f40ae3067344b786e959a92f35cbf2`, catalog `4b451316...`. This uses the existing
helper/sampler, not the subsequently planned sampler revision. Pass sampled
`FatGuyAxeShield_Attack2` frames49..77. Ordinary critical Attack reduced90 to87
HP, with HitSmall frames25..33 and Attack1 frames73..100. Explicit KillSingle
reduced87 to0 HP. All three captures recorded 120 unpaused frames over about
10.9083 seconds. Animator disabled at death frame27 with15 native rigidbodies;
DeathDirect normalized0.11 freezes after physics handoff, not failed death.

The reviewed humanoid probe, upper-body hit response, collapse and detached
native weapon on the ground were visible without obvious explosion in reviewed
areas. Attack effects and hero occlusion prevent full visual acceptance. One
guarded Collect reached strict Ready0/3 with queued Trap1 preserved; no forced
trap replacement was performed. The [runtime index](model-runtime-validation.json)
records exact assets and journals, five reviewed frame hashes, physics metrics,
and the diagnostic-only scope.

## Yeti resource calibration

The exact `enyeti` resource on `yetiBoss` bound the original `Yeti` calibration mesh.
Native attack and a nonlethal hit (153 to151 HP) were recorded in120-frame captures.
An explicit KillSingle death entered an11-body ragdoll with Animator disabled at
frame28. Native cleanup destroyed the renderer after95 recorded frames, so the
death recording remains partial, not a complete capture. Strict native Ready at
level0 room2 was subsequently observed without submitting Collect.

Reviewed images show attack bending, recoil and ragdoll collapse, with hero/effect
occlusion and the tall head near the HUD. This is diagnostic compatibility evidence,
not finished artwork or complete culling coverage. Exact hashes and partial-result
evidence are in [the runtime index](model-runtime-validation.json).

## Imp resource diagnostic evidence

`enbaseyimp` on native `impA` bound probe121117 at `enBaseyImp`, stage
`5de5aa38c01b4259ab04d6fcdcdffd3a`, session
`21f3cd9d92ce43e68762cad7948ca72b`, catalog `4b451316...`, helper `33c07cc7...`.
Pass recorded two `attack_impUnarmed` cycles at frames40..59 and75..94.
Ordinary Attack reduced58 to47 HP with `damageHeavy_imp` frames25..38 and a
subsequent attack. Explicit KillSingle reduced47 to0 HP. Each capture recorded
120 unpaused frames over about 10.9082 seconds. Animator disabled at death
frame25 with11 native rigidbodies; frozen `deathHeavy_imp` normalized0.055
reflects native physics handoff rather than failed death.

The small humanoid probe and ragdoll collapse were visible without obvious
explosion in reviewed regions, but the hero strongly occluded attack/hit motion.
This is diagnostic evidence, not finished art or full motion acceptance. One
guarded Collect reached strict Ready0/3 with queued Trap1 preserved. The
[runtime index](model-runtime-validation.json) records original asset hashes,
three case/capture journals, five reviewed frame hashes and native physics
metrics. Existing Yeti and Kraken findings retain their separate scope.

## Cockatrice boss resource baseline: fit failed

Unfitted `enbaseycockatriceboss` on `bossCockatrice`, renderer 121693, was recorded
in session `4fac21c62c7c4624a648ec782d88c34d`, old `df0cb...` framework and377
catalog. Stage `c10cea2a60d34b408ab95b7c932ff82e` verified binding, but the upper
body was hugely offscreen during Pass: baseline fit failed. Two ordinary attacks
were blocked for zero damage and left540HP unchanged; neither proves a received
hit. Pass and both blocked attacks each recorded 120 unpaused frames.

KillSingle death capture stopped after91 frames (8.25 game seconds) with
`Renderer destroyed during capture`. It remains partial. `deathHuge` sampled
frames27..89; reviewed 40/60 showed coherent prone collapse in visible regions
with hero/effect occlusion. Ready0/2 followed automatically without Collect.
The separately prepared fitted profile is not tested by this baseline. Exact
hashes, partial result and size diagnosis remain in the
[runtime index](model-runtime-validation.json).

## Fitted cockatrice resource diagnostic

The separate fitted profile uses native `cockatriceC`, resource
`enbaseycockatriceboss`, factor 0.55 and the unchanged probe121693. In session
`61d9e44ebed54190ae8b9e4e8b503d6f`, corrected framework `9e533...` and378 catalog,
stage `803ecc60c0f44731b3b52f4f923e88b4` bound the intended renderer. Measured
renderer world-matrix axis norms were approximately0.495, agreeing with native
0.9 times factor 0.55. This is not a direct CEL baseline/reapplication audit.

Whole-body fit improved in reviewed views, with remaining hero/effect occlusion.
The first ordinary attack was dodged at72HP; the second dealt10 (72 to62) with
HitSmall frames26..33. KillSingle reduced62 to0, with DeathHuge frames27..119
and coherent prone collapse in reviewed 40/60. All four captures completed 120
unpaused frames. Two guarded Collect actions reached strict Ready0/2. The
[runtime index](model-runtime-validation.json) preserves seven reviewed-frame
hashes and exact source evidence separately from the failed boss baseline.
This is fitted calibration evidence, with final art/culling/variant checks pending.

## Named-model scale regression checks remain pending

The source scale audit predicts changes from old neutral absolute1: Mirewarden
0.95, Cinderwing0.78, Cinderbloom1.6, and Emberjaw0.75. These four originals need
new-framework size/readability/camera checks, especially Cinderbloom's60% increase.
Ashfang's native scale 1 predicts no change from this fix alone. Source values do
not transfer historical visual acceptance to the corrected framework. The
runtime index links the hashed metadata audit and pending classifications.

Cinderbloom's pending native-scale regression is now resolved for its unchanged
v2 prototype: corrected Core preserves 1.6 native scale and reviewed front/mouth,
recoil and collapse remain readable/coherent without frame escape. This updates
only Cinderbloom's regression status. The named-original scale rechecks are now
recorded for Mirewarden, Cinderwing and Emberjaw; broad culling/variant checks
remain separate. Exact old/new framework capture evidence is preserved in each
model's `live-native-scale` archive.

Cinderwing's unchanged original bat model also passed the new-framework native
scale 0.78 regression in sampled attack/hit/death views: [V2 evidence](../art-experiments/cinderwing-bat/live-validation-v2/README.md).
All three captures completed 120 unpaused frames; small details and occlusion
remain limitations. Emberjaw's separate size recheck is also preserved in its
native-scale evidence.

The canonical [Cinderwing V3 supplement](../art-experiments/cinderwing-bat/live-validation-v3/validation.json)
pins the exact `batA / enBat01 / renderer 121104` source assignment in session
`ea0b19dbfc254df580c87e250e66e6e7`. Three complete 120-frame captures preserve
the exact body through `batFlySlow`, native `batAttackProf`, recovery,
`attackCrit_bat`, ordinary `batTakeHit`, a second recovery and retaliation, and
explicit-fixture `batDeath`. The renderer remains active, enabled and visible
in all 360 frames. Ordinary no-focus damage is 58 to 48; the separate
`KillSingle` fixture reports `m_DoRagdoll=false` and zero rigid bodies, so it is
animated death rather than ordinary lethal or ragdoll evidence. One Collect
reaches strict Ready at level 0 room 2. Twenty-two exact originals accept the
sampled dark body, pale muzzle, red-orange membranes, wing folds, attacks, hit,
recovery, fall and floor pose. Effects, hero overlap, small scale, sibling bat
sources, other clips, later corpse lifetime, portrait, collision, extended
culling, resource lifetime and final art approval remain open.
For the separate384 raw/230 CEL/154 additional profile boundary and reproducible
metadata audit, see [raw profile inventory](../tools/ai-model-pipeline/RAW_PROFILE_INVENTORY.md).

Emberjaw's unchanged original skull also passed its native0.75 size recheck:
[evidence](../art-experiments/emberjaw-skull/live-validation-native-scale.json).
Face/jaw coherence and nonlethal hit were reviewed; all three captures completed 120
unpaused frames. Native effects hide the reviewed death body, preserving the
earlier visibility limitation. Mirewarden0.95 also has the scoped recheck
recorded below; none of these passes establishes broad culling or all variants.

### Remaining raw renderer limits

The four excluded non-CEL diorama tentacles121042/121196/121232/121267 have
19 palette bones and20 inverse bind matrices. Index19 carries positive weight
on779 of6842 native vertices, so its matrix is not unused. Inserting Root_M
blindly still gives maximum absolute rest-matrix error5.2859226. Keep strict
rejection; an explicit adapter and verification of their unknown live ownership
are required. The modern20-bone renderer 121595 used by native krakenTentacle
and krakenTentacleMirror has rest error2.6029e-6 and provides a valid native
chassis alternative, without adding any live acceptance. Renderers121018 and
121680 remain excluded for missing mesh pointers. Source hashes and bounded
findings are in model-skeleton-candidates.json; native geometry remains scratch.

Renderer121018 is now positively resolved to Resources `player_mayor`,
CEL134799, with34 bone references, no mesh/IBMs and eight null material slots.
None of96 freshly parsed native skinsets references its avatar. It is a
resolved empty resource unsupported by the current strict replacement API,
not an untested enemy rig or proof of an obsolete asset. Supporting it would
require a reviewed custom avatar/skinset construction path and authored binding
baseline. [Pinned source findings](evidence/player-mayor-empty-renderer-source-v1/README.md)
retain the distinction between raw topology inventory and usable bind profiles.

Renderer121680 is also resolved: Resources `enbaseyfish`, CEL140194, its sole
`enFishA` renderer with34 bone references, a null mesh and no native bind
baseline. None of363 nonnull native enemy rows or96 skinsets references this
CEL. The current strict replacement API cannot use it; this does not establish
obsolescence or global non-use. Its shared topology/controller names do not
authorize borrowing fishA01's inverse binds. The
[source audit](evidence/basey-fish-empty-renderer-source-v1/README.md) preserves
this exclusion separately from valid fish renderers in the same topology.

Blacksmith backpack, helmet, shield and hammer are native MeshRenderers outside
the five custom SMRs present in the apparel fixture. The backpack can strongly
obscure the body. Original boots/default-armor/Gambeson marker meshes have
192/408/504 vertices respectively; these counts do not describe native exports.
Neither this metadata nor offline154-profile audits establish all-apparel or
all384-profile live coverage.

Mirewarden's unchanged original passes its reviewed native0.95 size regression
and now has a canonical [V3 exact-source archive](../art-experiments/mirewarden-ftk/live-validation-v3/README.md)
for `trollCaveA / enTroll01 / renderer 121153`. Three complete 120-frame
captures preserve settled idle, native proficiency attack, ordinary HP 58→48
with hit recovery, native counterattack, and the separate explicit-fixture
physical death. All 11 surviving bodies become nonkinematic from frame28, one
guarded Collect reaches strict Ready0/2, and 28 original PNGs were reviewed.
The fixture is not ordinary lethal evidence, corpse lifetime after frame119 is
not claimed, and this direct-enemy record does not credit the Gloamcap resource
route in the same topology group. Rock gaps, inherited emission, broad culling,
other variants and final art approval remain limited. All four affected named
originals now have scoped size rechecks (Cinderbloom1.6, Cinderwing0.78,
Emberjaw0.75, Mirewarden0.95).

[Abyssal Crown V4](../art-experiments/abyssal-kraken/live-validation-head-v5-canonical/README.md)
now has canonical exact-source evidence for the production modern Kraken
`kraken2` renderer 121035 and its required rigid `kraken2_eye` child 101310. The
fresh isolated trial pins both assignments on one owner, including the static
child's one `MeshFilter`. Pass and ordinary attack captures complete at 120
frames through `krakenIdle`, `krakenAttack`, ordinary HP 324→316
`krakenDamage`, and recovery. The explicit KillSingle fixture retains 91 frames:
`krakenDisappear` plays from frame 29 through frame 89, frame 90 retains the same
renderer inactive and not visible, and the next sample reports renderer
destruction. Strict Ready reaches 0/2 without Collect. Thirty-one reviewed
originals show the rigid eye remaining registered inside the animated assembly,
but native effects, hero overlap and the tall attack crop limit some views. The
rigid eye receives structural profile credit without a second skinned topology
claim. Fixture death is not ordinary lethal evidence, and the teardown boundary
does not establish cleanup causality, corpse lifetime or final disposal. The old
five-bone `enkrakenhead` resource, full transition coverage, portraits, collision,
all-camera culling, long-session resource lifetime and finished-art approval
remain separate.

SnowmanB now has a separate five-part markers-only live diagnostic in
session906ee3d8aef644929b629dcf0b13286f. The runner verifies all five assignments;
four middle-body-target captures complete 120 unpaused frames. The first attack
is dodged at58HP, the second deals10 (58 to48), and the kill fixture reaches 0
with animated snowman_deathDirect, no rigidbodies and no ragdoll flag. Reviewed
markers move without earlier connector stretch; upper orange markers fade and
lower magenta markers remain visible at frame60 amid native effects. One
Collect reaches strict Ready0/2. This is calibration evidence, with art, full
visibility and culling acceptance pending; source hashes remain in the runtime
index and earlier Snowman iterations are preserved.

BossGladiator has a four-SMR native diagnostic in session906ee3d8aef644929b629dcf0b13286f.
Pass and ordinary 11-damage hit (58 to47) complete 120 unpaused frames. The kill
fixture (47 to0) retains only91/120 frames over 8.25 game seconds before
RendererDestroyed; this remains a failed partial capture. Animator disables
at28, the Death clip freezes at0.1833333, and recorded rigidbody totals shift
from15 to11 during native ragdoll. Reviewed calibration fits, crouches and
twists; native helmet/weapons remain. Automatic strict Ready0/3 requires no
Collect. This is bounded diagnostic motion, not full death or finished-art
acceptance; hashed journals and retained screenshots are in the runtime index.

Vesper Eye, an original two-part beholderA model, binds and animates in its
first live run but fails material review: inherited material117 emission washes
the black pupil yellow. [V1 evidence](../art-experiments/vesper-eye/live-validation-v1.json)
preserves three complete 120-frame captures and the135 to127 ordinary hit.
Native effects hide reviewed death geometry. An explicit per-renderer emission
opt-out and corrected rerun remain pending; no finished-art acceptance.

Vesper Eye's same-geometry emission correction now passes scoped live material
and motion review: [V2 archive](../art-experiments/vesper-eye/live-validation-v2.json).
Both materials disable inherited emission; black pupil and amber iris are
readable in idle. Three 120-frame captures preserve normal 10 damage and death,
but native attack/death effects still obstruct full visible deformation. V1
material failure remains historical; no broad culling/all-animation acceptance.

The fresh [Vesper Eye V3 supplement](../art-experiments/vesper-eye/live-validation-v3/validation.json)
repeats the exact two-renderer Beholder A binding in session
`e1a785784d86426ba5e5da8624680123`. `EyeBody` and `EyeBody/EyeEye` are both
active on owner `369188` with separate observed bone signatures; both
replacement materials report emission disabled, black emission RGB and no
emission map. Ordinary damage is135 to125 with `cheat=None` and no focus;
explicit `KillSingle` completes 120 frames, one native Collect is accepted, and
strict Ready is observed at level0 room2. Native UI/effects and the victory
surface still limit fine eye-tracking, complete deformation, culling-envelope,
portrait/resource lifetime and finished-art acceptance.

The canonical [Vesper Eye V4 archive](../art-experiments/vesper-eye/live-validation-v4/validation.json)
pins the current explicit-emission profile and exact `beholderA` multipart source
assignment. `EyeBody` renderer 121031 and `EyeBody/EyeEye` renderer 121210 share
owner `369188` while retaining distinct two-bone signatures and private material
instances. Fresh live inventory records the authored base texture, emission
disabled, black emission RGB and no emission map on both parts. The complete
pass capture samples `eye_idle`, `eye_attack1`, `eye_attack2` and recovery; the
ordinary no-focus attack measures HP 135 to 125 and samples `eye_damage` before
recovery. One guarded Collect reaches strict Ready at level 0 room 2.

The separate explicit `KillSingle` fixture changes HP 125 to 0 and issues
`Death` at sample 23. Both custom renderers remain visible through sample 25,
become inactive at sample 26 and enter `eye_death` at sample 30 while inactive;
`m_DoRagdoll` stays false with zero rigid bodies. The V4 review therefore accepts
the sampled native trigger, controller state, renderer removal and Victory
handoff without claiming ordinary lethal damage, visible custom death
deformation, corpse, floor contact or corpse lifetime. Eighteen exact originals
and three videos are preserved; effects, hero/UI occlusion, other controller
clips, portraits, culling, collision, resource lifetime, natural-arena behavior,
unperturbed performance and finished-art approval remain outside scope.

The fresh [Cinderbloom V3 supplement](../art-experiments/cinderbloom-plant/live-validation-v3/validation.json)
repeats the corrected multipart plantA binding in session
`832c6a68d7084c4e967a53274884d574`. Body and leaves are both active on owner
`369188` with separate observed bone signatures at public visual-scale factor
1.0 (captured native CEL scale 1.6). Ordinary damage is58 to50 with
`cheat=None` and no focus; explicit KillSingle completes 120 frames, one native
Collect is accepted, and strict Ready is observed at level0 room2. Native
UI/effects and the victory item surface limit fine teeth, jaw,
leaf-intersection, full deformation, culling-envelope, portrait/resource
lifetime and finished-art acceptance.

The fresh [Emberjaw V2 supplement](../art-experiments/emberjaw-skull/live-validation-v2/validation.json)
repeats the multipart skullA binding in session
`2be3d41ce2c24361900069460e222deb`. Jaw and cranium are both active on owner
`369188` with separate observed bone signatures at public visual-scale factor
1.0 (captured native CEL scale 0.75). Ordinary damage is69 to59 with
`cheat=None` and no focus; explicit KillSingle completes 120 frames, two native
Collect actions are accepted, and strict Ready is observed at level0 room2.
Inherited `chaosBeastBody` emission and purple effects change the live palette;
targeting UI and the victory surface limit complete jaw/death deformation,
culling, portrait/resource lifetime and finished-art acceptance.

The fresh [Emberglass Bee V2 supplement](../art-experiments/emberglass-bee/live-validation-v2/validation.json)
repeats the exact beeA `Monster Bee` binding in session
`59cde8c216d54a79b5d22fab5ac82d0c`. The 47-joint authored body is active on
owner `369188` at public visual-scale factor 1.0 (captured native CEL scale
1.25); selected idle and attack views retain the abdomen, legs, antennae and
paired wings. Ordinary damage is58 to45 with `cheat=None` and no focus. The
explicit `KillSingle` fixture reaches strict Ready at level0 room2, but the
renderer is destroyed at death-capture frame104/120; the archive preserves that
expected prefix and makes no full-ragdoll claim. The direct Ready vote exposed no
Collect action. Small/dark silhouette, native UI/effects and the cleanup boundary
limit fine detail, culling, portraits, resource lifetime and finished-art
acceptance.

The fresh [Emberglass Bee V3 supplement](../art-experiments/emberglass-bee/live-validation-v3/validation.json)
repeats the exact `beeA` `Monster Bee` binding in session
`72ea10881809499883a38b7137a49480`. Pass and ordinary attack are complete
120-frame captures; ordinary damage is58 to53 with `cheat=None` and no focus.
The explicit `KillSingle` fixture commits53 to0, then native cleanup destroys
the renderer during frame94/120. The expected prefix remains separate from any
full corpse or settled-ragdoll claim. The post-death surface is strict Ready at
level0 room2 with no Collect vote; one guarded Ready advances room2, where the
next state contains Jelly Cube and the registered cultist probe. The first
native Standard material uses the authored basecolor with black emission and
no emission map; the native glow slot is not claimed. Small-scale effects and
cleanup still limit fine detail, culling, portraits, resource lifetime,
collision/sleeping and finished-art acceptance.

The canonical [Emberglass Bee V4 supplement](../art-experiments/emberglass-bee/live-validation-v4/validation.json)
pins topology `6fdb7ff148451731` to exact `beeA / Monster Bee / 121062` in
session `6b24887886c84815a06146b2cfa90494`. One owner binds the authored
`ftkmf_glb_emberglass.glb` with the expected 47-bone signature at public scale
1.0 and native CEL scale 1.25. The complete pass records `BeeIdle`,
`beeStingerAttack`, and recovery. Ordinary attempt 1 stays at HP 58 with a
native `Dodge` trigger, `dodge_bee`, and DODGED label; attempt 2 measures HP
58 to 50 with `BeeTakeDamage` before returning to idle. Both ordinary captures
also retain a native `attack2_bee` counterattack. The explicit fixture changes
HP 50 to 0 and drives `BeeDie`; `m_DoRagdoll=true` switches all 16 recorded
bodies dynamic at sample 27, the connected model reaches the floor by sample
40, and the grounded corpse remains visible through sample 102. The next
sample records the renderer inactive and terminates the 104-frame prefix.
Strict Ready is reached at level 0 room 2 with no usable Collect. The archive
independently verifies 22 metadata mappings, 465 source-image pins, all 464
retained capture frames, two assets, 24 reviewed originals, the root review,
and four videos. This exact-source result does not establish ordinary lethal
damage, cleanup causality, later corpse lifetime, final art approval, or the
two remaining beeB and dragonflyB representatives.

The fresh [Resinmaw V2 supplement](../art-experiments/resinmaw-bogling/live-validation-v2/validation.json)
repeats the exact acidBlobA `enAcidMonster` binding in session
`9b795beba7674d6e887bf6faef3529ba`. The 32-joint authored body is active on
owner `369188` at public visual-scale factor 1.0 (captured native CEL scale
basis 0.75/0.8/0.8); selected idle and attack views retain the body, eyes,
overlapping jaws and tusks. Ordinary damage is81 to73 with `cheat=None` and no
focus. The explicit `KillSingle` fixture completes 120 frames, reaches strict
Ready at level0 room2, and exposes a direct Ready vote with no Collect action.
The renderer stays active through the last recorded frame, but victory UI
limits fine corpse and settled-ragdoll review; native effects and the overlay
also limit culling, portrait/resource lifetime and finished-art acceptance.

Kraken adapter v2 now passes four independently checked owned-hierarchy
mechanics trials after composing actual local TRS ancestry at unchanged1e-5
tolerance. Attack/Idle/Damage/Disappear use32/32/6/27 exact sample times;
maximum errors are2.398e-6/1.614e-6/1.395e-6/2.623e-6. Four fresh-reference
bridges match with maximumerror0. Actual Transform writes, root preservation,
repeat/neutral checks, internal invalid-input checks, restoration and temporary
cleanup are measured. The earlier v1 readback failure remains historical.
This is not skinned-mesh, native-event, blending, appearance or production
old-rig support. Disappear still derives from the91/120-frame native death
prefix. Hashed raw requests/results and independent reports are indexed in
docs/model-runtime-validation.json; aggregate Ready/pin receipt is separate.

Bronzewake Champion's original four-part bossGladiator model has scoped live
fit/motion evidence: [archive](../art-experiments/bronzewake-champion/live-validation.json).
Armor/body remain readable under native darkening and retained helmet/weapons;
actual boots emission is disabled. Attack/hit complete 120 frames, while death
retains 91/120 before RendererDestroyed and progresses automatically to Ready0/2.
The partial death remains a capture failure, not full animation acceptance.

The fresh [Bronzewake V2 supplement](../art-experiments/bronzewake-champion/live-validation-v2/validation.json)
repeats all four bossGladiator renderer bindings in session
`9df826f02881492aa5be8b9098937942`. Body, hair, armor and boots are active on
owner `369188` at public visual-scale factor 1.0 (captured native CEL scale 1.1)
and remain coherent with the retained helmet and weapon/shield accessories in
selected idle and attack views. Ordinary damage is58 to48 with `cheat=None` and
no focus; explicit `KillSingle` reaches strict Ready at level0 room2, but the
renderer is destroyed at death-capture frame91/120. The archive preserves that
expected prefix and makes no full-ragdoll claim. The direct Ready vote exposed
no Collect action. Native lighting and the cleanup boundary limit fine detail,
culling, portraits, resource lifetime and finished-art acceptance.

The fresh [Bronzewake V3 supplement](../art-experiments/bronzewake-champion/live-validation-v3/validation.json)
repeats the exact four-renderer binding in session
`591ba03f086341369926e56e8f4dc444`. Pass and ordinary attack are complete
120-frame captures; ordinary damage is58 to48 with `cheat=None` and no focus.
The explicit `KillSingle` fixture commits48 to0, then native cleanup destroys
the renderer during frame91/120. The expected prefix is archived separately
from any full corpse or settled-ragdoll claim. The post-death surface is strict
Ready at level0 room2 with no Collect vote; one guarded Ready advances room2,
where the next state contains Jelly Cube and the registered cultist probe.
All four materials use the authored basecolor with no active emission, and
boots inherited emission is disabled. Native effects and cleanup still limit
fine plate/hair, culling, portraits, resource lifetime, collision/sleeping and
finished-art acceptance.

The canonical [Bronzewake V4 supplement](../art-experiments/bronzewake-champion/live-validation-v4/validation.json)
narrows coverage to the exact `bossGladiator / armorBossGladiator / 121522`
source assignment. One fresh process binds all four authored multipart meshes
to one owner, while only the armor topology receives credit. Pass and ordinary
hit captures complete 120 frames; native attack evidence includes `AttackProf`,
and ordinary no-focus damage is 11 to 1 with `DamagedHeavy`. The explicit
`KillSingle` records finalized native `Death` and retained `2HandWield_Death`,
then ends after 90 of 120 frames at the separately verified controller-unresolved
teardown boundary. Strict Ready follows at level 0 room 2. Twenty-one exact
original PNGs accept the sampled idle, attack, hit recovery, death entry, and
ragdoll prefix without sampled tearing, renderer loss, viewport clipping, or
multipart separation. The partial boundary remains a stated limit, and no
ordinary lethal, full-duration death, later controller state, sibling topology,
or finished-art conclusion is transferred from this archive.

The canonical [Bronzewake V5 body supplement](../art-experiments/bronzewake-champion/live-validation-v5/validation.json)
credits only `bossGladiator / enBossGladiator / 121272`, despite verifying all
four package assignments on owner `369188`. Pass and ordinary attack each
retain all 120 requested frames, covering `CombatIdle`, `BasicAttack`,
`HitSmall` and recovery with the exact body visible. Ordinary no-focus damage
changes HP 11 to 6. The separate `KillSingle` fixture starts at 6 HP and retains
90 frames through `Death` and `2HandWield_Death` before the same typed
controller-unresolved boundary. This native controller has `m_DoRagdoll=true`:
15 rigid bodies remain through frame 25, 11 remain from frame 26, and all 11
active survivors are nonkinematic from frame 28. The reviewed last sample is a
coherent low ragdoll pose, but no later corpse lifetime or cleanup causality is
claimed. Eighteen exact originals accept the sampled multipart alignment, and
strict Ready follows at level 0 room 2 without Collect. Hair and boots topology
credit, broader motion, culling, portraits, collision, resource lifetime and
finished-art acceptance remain open.

The canonical [Bronzewake V6 boots supplement](../art-experiments/bronzewake-champion/live-validation-v6/validation.json)
credits only `bossGladiator / bootsBossGladiator / 121661`, despite separately
verifying the package body, hair and armor assignments on owner `369188`. Pass
and ordinary attack each retain all 120 requested frames. They cover
`2HandWield_CombatIdle`, `2HandWield_CriticalAttack`, recovery, ordinary
`2HandWield_HitBig`, and native `2HandWield_BasicAttack` with the exact boots
active, enabled and visible. Ordinary no-focus damage changes HP 11 to 1 with
`DamagedHeavy`. The separate `KillSingle` fixture starts at 1 HP and retains 90
frames through `Death`, `2HandWield_Death`, and an actual physics transition:
`m_DoRagdoll=true`, the body set changes from 15 to 11 at frame 26, and all 11
surviving bodies are nonkinematic from frame 28. The reviewed final sample is a
coherent floor pose with both authored boots still attached. The typed
controller-unresolved boundary leaves full duration, later corpse lifetime and
cleanup causality unproven. Nineteen exact originals accept the sampled motion;
strict Ready follows at level 0 room 2 without Collect. Hair topology, broader
motion, culling, portraits, collision, resource lifetime and finished-art
acceptance remain open.

The canonical [Bronzewake V7 hair supplement](../art-experiments/bronzewake-champion/live-validation-v7/validation.json)
credits only `bossGladiator / hairBottomBossGladiator / 121500`, despite
separately verifying the body, armor and boots assignments on owner `369188`.
Pass and ordinary attack each retain all 120 requested frames. They cover
`2HandWield_CombatIdle`, `2HandWield_BasicAttack`, recovery, ordinary
`2HandWield_HitBig`, and `2HandWield_CriticalAttack` with the exact hair
renderer active, enabled and visible. Ordinary no-focus damage changes HP 11
to 1 with `DamagedHeavy`. The separate `KillSingle` fixture starts at 1 HP and
retains 90 frames through `Death`, `2HandWield_Death`, and an actual physics
transition: `m_DoRagdoll=true`, the body set changes from 15 to 11 at frame 26,
and all 11 surviving bodies are nonkinematic from frame 28. The reviewed final
sample keeps the red crest and lower hair accents aligned in the retained floor
pose. The typed controller-unresolved boundary leaves full duration, later
corpse lifetime and cleanup causality unproven. Twenty exact originals accept
the sampled motion; strict Ready follows at level 0 room 2 without Collect.
With V7, all four Bronzewake renderer topologies have an exact-source canonical
archive. Other proficiencies, `DeathLight`, culling, portraits, collision,
resource lifetime, full campaign behavior and finished-art acceptance remain
open.

Ashfang now has an exact-source [V2 canonical archive](../art-experiments/ashfang-wolf/live-validation-v2-canonical/validation.json)
for `wolfA / wolf01 / 121142`. Three complete 120-frame captures establish
settled idle, native proficiency, ordinary HP 58 to 50 with `damaged_wolf`, two
later native attacks, and a separate explicit-fixture death from 50 to 0. The
animator disables and all 14 bodies become dynamic at death frame 27; measured
motion ends after frame 48 and the exact renderer persists through frame 119.
Twenty-four reviewed originals accept the sampled custom silhouette within
documented effect, hero, UI and blur limits. One Collect reaches strict Ready
0/2. This exact route does not transfer art or runtime credit to the other
wolf-controller assignments below.

The remaining wolf-controller assignments now have [exact calibration evidence](evidence/unresolved-enemy-probes-v1/README.md): `hellhoundA`/`enHellHoundB` records ordinary HP 81→71, `pantherA`/`enPanther` records 99→89, and `jaguarA`/`enJaguar` requires a bounded retry after72→72 before recording72→62. Each has fixture death and strict Ready. `chaosHound`/`chaosWolf` has exact binding/pass evidence but eight bounded attacks all remain HP 180→180, so no damage, fixture or Ready claim is made. These synthetic probes do not transfer Ashfang's original-art acceptance.

Rimecrown's completed V1 baseline preserves the camera-fit failure despite
three complete 120-frame captures: [archive](../art-experiments/rimecrown-sentinel/live-validation-v1.json).
Ordinary hit58 to48 and native death48 to0 are observed; upper parts fall while
the base stays anchored. Native base121556 weights all7683 vertices toRoot_M,
so separation alone does not imply a binding/weight bug. Two Collect actions
reach Ready0/2. The prepared0.75 scale correction still needs live validation.

Briarback Guardian has scoped original body-motion evidence on old resource
Yeti120991, but its first portrait frames chest pads and fails review:
[V1 archive](../art-experiments/briarback-guardian/live-validation-v1.json).
Pass/hit complete 120 frames; death retains 95/120 before RendererDestroyed,
then automatically reaches Ready0/3. Fixed PortraitCam and head-attached
EncounterCam are distinct native landmarks. An explicit selector rerun is
pending; no portrait or complete-art acceptance follows from coherent body motion.

The382-enemy catalog passes strict runtime decode in session16074358 with
catalog75671881; decode remains distinct from live fit or controller acceptance.
Five owned Kraken controller scenarios (appear, damaged, damaged-heavy, death,
death-light) and their241-frame repeats independently match. These native graph
observations do not validate an old-rig blending adapter. In fact, the proposed
Dj * inverse(DsRoot) * OjNative policy fails the required pure-main endpoint
by up to6.817214574, despite matching pure appearance within 2.98023e-7. The
modern and old immutable root frames differ; changing tolerance cannot repair
that algebra. The proposal was only audited offline, never assigned to Unity.
Hashed numerical findings are retained in the runtime evidence index; a new
explicit blend policy and verification remain necessary.

Rimecrown's0.75 scale correction clears the previously cropped crown in reviewed
views, with native HUD/effect overlap retained; Briarback's explicit head marker
now frames its face in both native portrait UIs. Their V2 archives preserve
unchanged geometry and exact deployment hashes. Rime's three captures complete
120 frames, while Briar's death remains a95/120 RendererDestroyed failure.
Earlier fit/portrait failures remain historical; no broad culling/full-death
acceptance follows from these scoped corrections.

The fresh catalog-411 [Rimecrown V3 supplement](../art-experiments/rimecrown-sentinel/live-validation-v3/README.md)
rebinds all five authored renderers in one owner at the corrected scale. Root
reviewed idle, attack, smoke/falloff and loot views; the crown stays inside the
camera during attack, ordinary HP changes 58 to50, and explicit death completes
120 frames before two native Collects reach Ready0/2. The base remains
Root_M-weighted during upper-part falloff. This confirms the corrected binding
and selected fit in a fresh process, while finished-art, full culling, collision,
ordinary lethal damage and final resource teardown remain separate checks.

The canonical [Rimecrown V4 head archive](../art-experiments/rimecrown-sentinel/live-validation-v4-head/README.md)
isolates direct-enemy source `snowmanB`, topology `02a6f31412bd52ab`, exact
renderer `SnowMan_Geo/enSnowmanHead` 121415 and observed native CEL scale 1.0.
The same owner binds the other four authored parts, but this record grants them
no route credit. Pass, ordinary attack and explicit kill each retain 120 frames.
The zero-focus native hammer attack uses no skill-cap or damage fixture and
records `Damaged`, 13 damage and HP 58 to 45. The profile's minimum enemy health
and disposable party maximum-HP fixture make the trial unrepresentative of
balance. Explicit `KillSingle` records 1000 damage and HP 45 to 0, so death is
fixture evidence rather than ordinary lethal gameplay. `m_DoRagdoll=false` and
zero rigidbodies identify the reviewed stacked-part damage and fall-apart as
animation, not ragdoll. Head identity stays active, enabled and reported visible
through all 360 telemetry frames, but smoke, airborne travel, camera framing and
loot obscure later images; readable corpse, lifetime and cleanup remain open.
Two guarded Collect actions reach strict Ready0/2. Root review covers 17 original
PNGs and leaves 343 hashed captures unreviewed. That head record alone grants no
companion route credit; V5 and V6 below cover scarf and base separately.

The canonical [Rimecrown V5 scarf archive](../art-experiments/rimecrown-sentinel/live-validation-v5-scarf/README.md)
then isolates topology `1322fec3354db550`, exact renderer
`SnowMan_Geo/enSnowmanScarf` 121639 and the same `snowmanB` source at observed
native CEL scale 1.0. All three captures retain 120 frames. The ordinary
zero-focus native hammer attack records `Damaged`, 13 damage and HP 58 to 45
without a skill-cap or damage fixture; the minimum enemy health and party HP
fixtures still make balance unrepresentative. Explicit `KillSingle` records
1000 damage and HP 45 to 0, so death remains fixture evidence. The neck wrap and
long tail remain aligned in idle, articulate through `snowman_attack3`, and
recover coherently after `snowman_damage`. With `m_DoRagdoll=false` and zero
rigidbodies, later separation is animated `snowman_deathDirect`, not ragdoll.
Scarf identity remains active, enabled and reported visible through all 360
telemetry frames, but later smoke, offscreen travel and loot prevent a readable
corpse or lifetime claim. Two Collect actions reach strict Ready0/2; 17 originals
were reviewed and 343 hashed captures were not. At the V5 boundary, hat, base
and middle body remained; V6 below covers the base, and V4 covers the head.

The canonical [Rimecrown V6 base archive](../art-experiments/rimecrown-sentinel/live-validation-v6-base/README.md)
isolates topology `8c73c366b064726a`, exact renderer
`SnowMan_Geo/enSnowmanBase` 121557 and the same `snowmanB` source at observed
native CEL scale 1.0. All three captures retain 120 frames. The ordinary
zero-focus native hammer attack records `Damaged`, 10 damage and HP 58 to 48
without a skill-cap or damage fixture; the minimum enemy health and party HP
fixtures make balance unrepresentative. Explicit `KillSingle` records 1000
damage and HP 48 to 0. The broad faceted pedestal uses its actual native
Root_M weighting and stays coherent through idle, `snowman_attack2`, the hit
response, recovery and later `snowman_attack3`. It remains visible while the
upper stack leaves during animated `snowman_deathDirect`; `m_DoRagdoll=false`
and zero rigidbodies give no ragdoll credit. Exact base identity remains active,
enabled and reported visible through all 360 telemetry frames. One Collect
reaches strict Ready0/2; 17 originals were reviewed and 343 were not. Exact
terrain contact, collision, later corpse lifetime, cleanup and disposal remain
open. At the V6 boundary, hat and middle body remained; V7 below covers the hat.

The canonical [Rimecrown V7 hat archive](../art-experiments/rimecrown-sentinel/live-validation-v7-hat/README.md)
isolates topology `963e53f60643b796`, exact renderer
`SnowMan_Geo/enSnowmanHat` 121405 and the same `snowmanB` source at observed
native CEL scale 1.0. All three captures retain 120 frames. The ordinary
zero-focus native hammer attack records `Damaged`, 13 damage and HP 58 to 45
without a skill cap or damage fixture; the minimum enemy health and party HP
fixtures make balance unrepresentative. Explicit `KillSingle` records 1000
damage and HP 45 to 0, so death remains fixture evidence. The three-point crown
stays centered on the head in idle, follows it through `snowman_attack2`, keeps
its shape during `snowman_damage` and recovery, and travels with the upper
assembly during later native attacks and `snowman_deathDirect`.
`m_DoRagdoll=false` and zero rigidbodies give no ragdoll credit. Exact hat
identity remains active, enabled and reported visible through all 360 telemetry
frames, but later smoke, camera framing, foreground overlap and loot prevent a
readable corpse, collision or lifetime claim. Two Collect actions reach strict
Ready0/2; 17 originals were reviewed and 343 were not. At the V7 boundary the
middle body was the only unresolved Rimecrown renderer topology; V8 below
closes it.

The canonical [Rimecrown V8 middle-body archive](../art-experiments/rimecrown-sentinel/live-validation-v8-middle-body/README.md)
isolates topology `a158ab62f9dd430f`, exact renderer
`SnowMan_Geo/enSnowmanmiddleBody` 121697 and the same `snowmanB` source at
observed native CEL scale 1.0. All three captures retain 120 frames. The
ordinary zero-focus native hammer attack records `Damaged`, 8 damage and HP 58
to50 without a skill cap or damage fixture; the minimum enemy health and party
HP fixtures make balance unrepresentative. Explicit `KillSingle` records 1000
damage and HP 50 to0, so death remains fixture evidence. The faceted torso,
articulated arms and crystal hands remain coherent in idle and
`snowman_attack1`, separate without a long deformation during
`snowman_damage`, recover, and pose through later `snowman_attack3` and
`snowman_attack1`. They travel with the upper assembly in
`snowman_deathDirect`; `m_DoRagdoll=false` and zero rigidbodies give no ragdoll
credit. Exact middle-body identity remains active, enabled and reported visible
through all 360 telemetry frames, but later smoke, distance, camera framing,
foreground overlap and loot prevent a readable corpse, collision or lifetime
claim. Two Collect actions reach strict Ready0/2; 17 originals were reviewed
and 343 were not. V4 through V8 now provide separate canonical evidence for all
five Rimecrown renderer topologies.

Gloamcap Trickster now has scoped original resource-Imp appearance/motion
evidence: [archive](../art-experiments/gloamcap-imp/live-validation.json).
Four120-frame captures preserve the first dodged hit, successful normal 10
damage 58 to48, and native11-body ragdoll death. Small body/portraits remain
readable under native darkening, with hero occlusion explicit. Two Collect
actions reach Ready0/2; no unoccluded all-motion or culling acceptance.

The fresh [Gloamcap V2 supplement](../art-experiments/gloamcap-imp/live-validation-v2/README.md)
repeats the exact impA assignment in catalog-411 session `f83c72d3f15f4be7a96cd409c9acc91e`:
`enBaseyImp`, stable renderer 121117, owner369188 and captured native CEL scale 1.0.
Pass, ordinary attack HP 58 to48 and explicit `KillSingle` each have complete
120-frame captures; two guarded Collect calls reach strict Ready0/2. The
successful damage proof uses three independent `record_case.py` actions after
one fresh staging claim; conservative exercise runs that stopped on random
no-damage outcomes remain preserved as separate raw diagnostics. Native
targeting/UI/effects, hero occlusion and victory blur continue to bound fine
cap/face/hand, culling, portraits, resource lifetime and finished-art review.

The canonical [Gloamcap V3 archive](../art-experiments/gloamcap-imp/live-validation-v3/README.md)
records fresh session `db8b9c9d636e4d98a0670294d9f7010d` for the exact
`impA / enbaseyimp / enBaseyImp / renderer 121117` resource route at native
scale 1.0. Five complete 120-frame captures keep the exact renderer active,
enabled, visible and identity-stable through settled `cidle_impUnarmed`,
repeated `attack_impUnarmed`, two ordinary no-focus `dodge_imp` trials, a third
ordinary HP 58 to 48 hit with `damageHeavy_imp` and recovery, and the separate
explicit-fixture `deathHeavy_imp`. The fixture records `m_DoRagdoll=true`, with
all 11 bodies nonkinematic from frame 26; the body reaches the floor by frame 45
and remains partly visible through frame 119. One guarded Collect reaches strict
Ready0/2. Twenty-nine exact originals accept the sampled custom silhouette and
motions. The fixture does not establish ordinary lethal behavior or later corpse
lifetime, and this resource archive gives no credit to direct enemy rows, sibling
Imp sources or the Mirewarden troll route in the same topology group.

SpiderB's65-bone marker/connector diagnostic now has three 120-frame captures
in session59a504bc. Reviewed attack shows no obvious stretching, with overlapping
legs/effects. The attempted hit is BLOCKED and leaves63HP unchanged: successful
nonlethal damage remains pending. Kill63 to0 uses spiderDeathDirect with zero
rigidbodies and no ragdoll flag; upturned legs are visible in reviewed 40/60.
One Collect reaches Ready0/3. This is diagnostic evidence only; Copperveil's
original mesh still requires its separate live test and no art acceptance is inferred.

Copperveil Weaver's original spiderB model now has scoped art/motion evidence:
[archive](../art-experiments/copperveil-spider/live-validation.json). Five full
120-frame captures preserve two blocks at63HP and the third normal 2 hit63 to61.
Hit views remain hero-occluded. Death uses native animated spiderDeathDirect,
with no rigidbodies/ragdoll, and one Collect reaches Ready0/2. This original's
successful damage does not rewrite the separate diagnostic probe's blocked hit.

The fresh [Copperveil V2 supplement](../art-experiments/copperveil-spider/live-validation-v2/validation.json)
repeats the exact Spider B binding in session115d691f and records two settled idle
views, two poison-attack views, and the fixture loot endpoint. Ordinary damage is
63 to61 with cheatNone and no focus; explicit KillSingle completes 120 frames, one
native Collect is accepted, and strict Ready is observed at level0 room2. The
fixture death remains separate from ordinary lethal damage, and hero/effect
occlusion, floor collision, full culling, portrait/resource lifetime and
finished-art acceptance remain open.

The [Copperveil V3 exact-source archive](../art-experiments/copperveil-spider/live-validation-v3/README.md)
is the canonical `spiderB / enSpiderB / renderer 121386` record. Its fresh
single-owner process completes three 120-frame captures and pins settled idle,
native `AttackProf`, ordinary HP 63 to 59 with `Damaged`, explicit-fixture
`Death`, one guarded Collect, and strict Ready at level 0 room 2. Eighteen
reviewed originals preserve the custom body and eight-leg silhouette through
attack, hit recovery, airborne death, and a coherent collapsed corpse still
visible behind the sampled loot panel. The causal impact frames remain
hero/effect occluded, and the fixture does not establish ordinary lethal damage,
indirect death, later corpse lifetime, sibling spider coverage, or final art.

The two remaining 65-joint spider-controller rows now have [exact calibration evidence](evidence/unresolved-enemy-probes-v1/README.md): `spiderA`/`enSpiderA` records ordinary HP 58→50 and `spiderJungleA`/`enJungleSpider_A` records 58→48. Both have fixture death and strict Ready. They remain synthetic probes; Copperveil's Spider B original art does not transfer.

Honeyback's bearB original has scoped body-motion evidence but fails portrait
readability: [V1 archive](../art-experiments/honeyback-bear/live-validation-v1.json).
Three 120-frame captures establish normal 12 damage 72 to60 and animated death
without ragdoll/rigidbodies; one Collect reaches Ready0/3. Native portrait view
from below exposes muzzle/nose obstruction of an eye and small eye readability.
An art revision is pending; this is not established as a rig or camera-crop
failure and does not justify an EncounterCam swap.

Tideglass crabB now has scoped live original body/portrait evidence with a
retained native purple wizard hat: [archive](../art-experiments/tideglass-crab/live-validation.json).
Three 120-frame captures preserve normal 2 damage 58 to56 and native animated
death with ragdollfalse. Actual inherited emission is disabled; native hat,
FX and motionblur limit visibility. Two Collect actions reach Ready0/2.

The fresh [Tideglass V2 supplement](../art-experiments/tideglass-crab/live-validation-v2/validation.json)
repeats the exact Crab B binding in session0b328315 and records two settled idle
views, two ordinary-attack views and the fixture loot endpoint. Ordinary damage
is58 to56 with cheatNone and no focus; explicit KillSingle completes 120 frames,
two native Collects are accepted, and strict Ready is observed at level0 room2.
The fixture death remains separate from ordinary lethal damage, and hero/UI/effect
occlusion, the retained native hat, floor collision, full culling,
portrait/resource lifetime and finished-art acceptance remain open.

The canonical [Tideglass V3 supplement](../art-experiments/tideglass-crab/live-validation-v3/validation.json)
pins the exact `crabB / enCrabWizard / renderer 121411` source assignment in
session `a7ecd64edd784ef28493feee9ed55a57`. Three complete 120-frame captures
preserve the authored body through `Crab_Idle`, native `Crab_Attack1`, ordinary
`Crab_Hit` and recovery, native `Crab_Attack2`, and explicit-fixture
`Crab_DeathDirect`. The exact renderer remains active, enabled and visible in
all 360 retained frames. Ordinary no-focus damage is 58 to 56; the separate
`KillSingle` fixture uses animated death with `m_DoRagdoll=false`, so it is not
ordinary lethal or ragdoll evidence. One Collect reaches strict Ready at level
0 room 2. Twenty-one exact originals accept the coherent teal body and native
purple hat in the sampled views; indirect death, later corpse lifetime, other
controllers, portrait, collision, extended culling, resource lifetime and final
art approval remain open.

Mossglass Cube A's corrected caps-v3 [fresh supplement](../art-experiments/mossglass-reliquary/caps-v3/live-validation-v4/validation.json)
binds both authored primitives through the exact three-joint `enJellyCube`
renderer in session022120d5. The jade shell and ivory/amber interior remain
distinct in selected idle and attack views; ordinary damage is58 to50 with
cheatNone and no focus, explicit KillSingle completes 120 frames, one native
Collect is accepted, and strict Ready is observed at level0 room2. The archive
preserves the corrected cap lineage and material-slot mapping, while inherited
emission, native effects, scroll phase, full culling, floor contact,
portrait/resource lifetime and finished-art acceptance remain open.

The canonical [Mossglass V5 supplement](../art-experiments/mossglass-reliquary/live-validation-v5/validation.json)
adds explicit idle, native attack, ordinary-hit and native-death motion shapes,
18 reviewed originals, two Collects to strict Ready0/2, and direct per-frame
material evidence. Slot0 remains fixed while the native slot1 `_MainTex`
scroller advances at `(0,0.2)`. Native `deathHeavy_jelly` removes the custom
surface from view with an active renderer, enabled Animator, and
`m_DoRagdoll=false`, so the result records animated disappearance and no corpse.
Fixture death, impact occlusion, perceptual scroll continuity, other cube routes,
culling, collision, portrait/resource lifetime, and final art acceptance remain
bounded.

Kraken endpoint-policy v1 demonstrates why helper success and independent
agreement remain separate: all five241-frame scenarios and repeats report raw
success, but appearance fails independent comparison at1.06378e-4 against
1e-5 tolerance. The four other scenarios pass while appearance contribution
is zero, so they do not validate that interpolation. Unity's close-angle
Quaternion.Slerp approximation diverges from the explicit policy formula.
The reviewed exact-SLERP helper correction is source/build-only at this record
and requires a fresh live run. No failed appearance trial is promoted to PASS;
mesh deformation, native events and production old-rig acceptance remain outside
this owned-fixture numerical exercise. Hashes are in the runtime evidence index.

Honeyback's separate portrait-v2 art improves combat eye visibility but still
fails native portrait readability: [archive](../art-experiments/honeyback-portrait-v2/live-validation.json).
Three 120-frame body captures and normal 10 damage 72 to62 do not change that
failure. Source review does not demonstrate a shared-cache cause; the separate
encounter-row preview gap remains a distinct investigation. Actual portrait
pose/camera/mesh telemetry is pending, with no fix claimed.

The exact-SLERP appearance correction now passes its actual owned-fixture rerun
and repeat:241 frames each, maximum independent error2.36390344e-6 across
447703 numeric checks at unchanged1e-5 tolerance (session1ed421a5, helper3ec43281).
This replaces Unity's close-angle approximation with the explicit declared
formula; it does not relax tolerance. The V1 appearance failure and earlier
four nonappearance passes remain distinct historical results. This is authored
endpoint mechanics only: no mesh deformation, visual continuity, jaw model-space
equivalence, production live adapter or full-controller acceptance. Exact
requests, manifests, cleanup and Ready evidence are hashed in the runtime index.

A [49-topology planning snapshot](MODEL-TOPOLOGY-COVERAGE.md) links exact
direct-enemy and resource-prefab source pairs to indexed evidence while keeping
player-avatar and empty-renderer ownership separate. Its evidence-presence
counts include indexed original/diagnostic cases and failures; they are neither
topology acceptance nor the 384 raw-profile/395 staged test-row denominator.

Verdigrin mimicA's historical [baseline](../art-experiments/verdigrin-mimic/live-validation.json)
records three 120-sample original-model captures, normal damage 58→52 and
animated death 52→0. The fresh [V2 supplement](../art-experiments/verdigrin-mimic/live-validation-v2/validation.json)
repeats the exact binding with normal damage 58→54 and a complete fixture death;
one Collect was accepted but strict Ready did not appear before the bounded
runner stopped. Hero/effect occlusion and the clipped initial death interval
remain explicit. The canonical [V4 supplement](../art-experiments/verdigrin-mimic/live-validation-v4/validation.json)
reconciles a retained runner whose only current-campaign differences were queue
and stage snapshot hashes. The profile, catalog, assets, exact mimicA renderer
and motion renderer still match. Its complete captures add source-specific
`cidle_mimic`, native `attack_mimic`, ordinary HP 58 to 52 with
`damageSmall_mimic`, later `chompAOE_mimic`, and explicit-fixture
`deathHeavy_mimic`. V3 remains the separate source for two guarded Collects,
strict Ready and next-room progression. The custom renderer reports
`m_DoRagdoll=false`, so the fixture death is animated rather than a body
ragdoll. Honeyback portrait-v2 passive HUD tracing now observes the
actual custom mesh, texture, camera and pose; it still fails portrait
readability. One complete record is not a fix or encounter-row preview
coverage, and no named portrait clips does not establish absence of native pose
sampling.

ClamA now has an exact enClam diagnostic baseline: three 120-frame unpaused captures, normal damage 58→48, then48→0 and one Collect to Ready0/2. The all-palette probe has an intentional long tip-bone connector. Hero/effects obscure attack/hit, and the renderer becomes inactive by death frame28 despite continuing Clam_DeathDirect telemetry. The later source audit resolves this as native early whole-mesh hide: DeathFallOff invokes FallOffLimb and deactivates enClam, with no configured rigid corpse replacement. It is not a demonstrated custom binding/culling failure or missing corpse part. Only pre-hide visible motion is assessable; DeathLight/DeathRevive remain untested. No original-art acceptance is claimed. Exact source and capture hashes are in the runtime index.

AcidBlobA exact enAcidMonster121344 diagnostic now has three 120-frame unpaused captures: native attack, normal damage 81→73, and73→0. Reviewed probe contraction/downward scatter retains native brown chunks and puddle effects, which are not authored model parts. HUD/hero/effects obscure selected areas; no original-art acceptance. Two Collect actions reach Ready0/3 with queued Trap1. Exact hashes are indexed.

Lunacrest original clamA V1 has three 120-sample captures, normal damage 58→48 and native direct death48→0, followed by Ready0/2. Idle/return body and portraits are readable, but attack40 clips the top shell: camera fit FAIL at factor 1. Frame25 native DeathFallOff hides the whole renderer as expected. [V1 archive](../art-experiments/lunacrest-clam/live-validation-v1.json) preserves these separate results; unchanged geometry at0.80 is staged but untested.

Kraken original-skin V1 raw helper/repeat calls passed241 samples each with12 PNG and4 BakeMesh observations, but the full independent verifier rejected camera consistency: discrepancy2.98514e-5 exceeds unchanged1e-5 tolerance. Separate BakeMesh diagnosis max7.62718e-6 does not establish full acceptance. The origin-centered helper is deployed; a fresh pair remains pending. Exact raw requests, manifests, diagnosis, cleanup and Ready0/3 are indexed.

Honeyback’s first native pixel-readback request was refused by the passive-trace guard due to shipped Newtonsoft typed string-null behavior. The approved explicit-null/finalized-flag correction has24 focused tests; actual fresh pixels remain pending. New Core row-preview integration likewise has unit evidence but awaits its actual row fixture. Neither source fix establishes a native visual result.

Honeyback’s corrected guarded readback now returns actual native204x172 portrait pixels: the muzzle-dominated appearance is already in the rendered texture. Seven active RawImages share its ID with full UV and white tint. This resolves the readback guard, not portrait readability; it does not prove pixels unchanged since pre-render tracing. A separate CameraRoot/EncounterCam profile is staged using unchanged V2 assets after offline whole-bear framing review; the smaller face and native readability await live testing. Exact readback, comparison and Ready0/2 evidence are indexed.

Kraken owned five-marker skin V2 now passes the unchanged independent verifier: maximum BakeMesh error3.85966420e-7 and endpoint error2.36390344e-6, tolerance1e-5; repeat PNG hashes are identical. Parent separately inspected12 sparse steps0/16/28/40/41/80/104/105/112/119/120/240: all five markers visible, coherent positions, no observed clipping. This is calibration-marker acceptance only, not continuous transitions, organic skin, finished art, other scenarios or a production adapter. V1 camera-precision rejection remains preserved; raw verifier and later visual-review files are separately hashed.

Lunacrest factor 0.80 retest corrects the observed attack40 screen crop with unchanged geometry; healthbar overlap remains. Three 120-frame captures include blocked native attack animation, normal damage 58→47 and death47→0. Raw renderer active becomes false at25, matching native early hide; effects obscure much of pre-hide death24. One Collect reached Ready0/3. [V2 archive](../art-experiments/lunacrest-clam/live-validation-v2.json) preserves V1 fit failure and initial HTTP500 read refusal separately. No all-variant/full-campaign acceptance.

The fresh [Lunacrest V3 supplement](../art-experiments/lunacrest-clam/live-validation-v3/validation.json)
repeats the exact clamA `enClam` binding in session
`67212ee1c3ee4c80918e8dfffc0bae43`. The seven-joint authored clam is active on
owner `369188` at public visual-scale factor 1.0 (captured native CEL scale 0.8);
selected idle and attack views retain the opened shell, lower bowl and hinge
body. Ordinary damage is58 to50 with `cheat=None` and no focus. The explicit
`KillSingle` fixture completes 120 frames, one guarded native Collect is accepted,
and strict Ready is observed at level0 room2. Native hide behavior, UI/effects
and the victory loot surface limit full death deformation, culling, portraits,
resource lifetime and finished-art acceptance.

Resinmaw acidBlobA now has three 120-frame original-model captures with readable sampled body/portraits, normal 5 damage 81→76 and material verification. Death visibility is limited: all32 renderer-local bone matrices match the native diagnostic at aligned death phases within 1.43e-7, with active renderer/unit bone scales and native backward/downward sinking. Native chunks/puddle remain; this is not whole-death art acceptance. Startup busy500/once-only resume and Ready0/2 are preserved in the [archive](../art-experiments/resinmaw-bogling/live-validation.json).

Honeyback's separate `CameraRoot/EncounterCam` profile now has scoped actual raw-pixel and HUD portrait acceptance using unchanged v2geometry. Earlier PortraitCam failure is preserved; the smaller whole-bear view is not all-resolution or encounter-row coverage. Cleanup-only death and Ready0/3 accompany the [camera archive](../art-experiments/honeyback-portrait-v2/live-validation-encounter-camera.json). A separate native row-preview helper attempt rejected its incorrect204x172 assumption before Initialize; it provides no Core live PASS.

The 47-bone bee topology has exact calibration evidence for six of eight native source pairs: `Monster Bee`/beeA plus `enSwampFly01`/dragonflyA, `enMosquito_01`/mosquitoA, `enScarabB`/scarabA, `enScarabA`/scarabB and `enMosquito_02`/mosquitoB. The five follow-up rows each preserve a complete pass, accepted native attack, explicit `KillSingle` fixture death and strict Ready; mosquitoB required a bounded retry after its first attack measured 58→58. [Archived telemetry, media and source pins](evidence/unresolved-enemy-probes-v1/README.md) establish exact assignments only, not finished custom art or family-wide acceptance. The original beeA diagnostic remains separately archived [here](evidence/beea-diagnostic-v1/validation.json). Exact beeB and dragonflyB representatives remain outstanding; effects and small airborne silhouettes still limit fine visual review.

The later [native encounter-row preview V2 evidence](evidence/native-row-preview-v2/validation.json) is separate from the preserved pre-invocation dimension rejection and Honeyback's combat-portrait camera check; consult its own case limits.

Emberglass Bee now has its own [native-scale original live archive](../art-experiments/emberglass-bee/live-validation.json): three 120-frame captures, readable sampled insect/portraits, ordinary 10 damage 58→48 and selected-pose corpse appearance acceptance. Animator disables and16 bodies become dynamic at27; small motion remains at60, zero velocities at63/119. One Collect reaches Ready0/2. This original art acceptance remains scoped to beeA; the five exact follow-up bindings have calibration evidence only, and every clip, all-view culling and finished-art review remain explicit.

The newer [canonical Emberglass V4 archive](../art-experiments/emberglass-bee/live-validation-v4/README.md)
repeats exact `beeA / Monster Bee / renderer 121062` through the execution-queue
workflow and preserves all four accepted captures. Twenty-four reviewed
originals show coherent idle, stinger attack, dodge, hit recovery, counterattack,
physics fall, and a grounded custom corpse through the final active-renderer
sample. Its canonical credit remains one exact route; beeB and dragonflyB still
need their own representatives, and no family-wide art or gameplay acceptance
transfers from beeA.

The separate [Gloamfin organic blockout appearance fixture](../art-experiments/gloamfin-kraken/live-validation-appearance-v1.json) passes full-weight numerical checks (BakeMesh7.48e−7, endpoint2.36e−6, tolerance1e−5) with identical repeat images and unchanged Ready0/2. Parent reviewed 12 sparse appearance images, including connected open-mouth40/41. This is constructed-fixture blockout deformation/fit only; it is excluded from enemy topology acceptance. Marker V1 rejection remains preserved; other scenarios, continuous transitions, final art and the production adapter are pending.

The66-bone fairyA diagnostic now has [three complete 120-frame captures](evidence/fairya-diagnostic-v1/validation.json), observed HP 58→55 and selected prone death poses. Native m_DoRagdoll stays false and Animator stays enabled; two inactive weapon Break rigidbodies are not body ragdoll. One Collect reaches Ready0/3. Pink spell/hero/green effect occlusion and a first death sample at normalized.141 limit visual/cycle coverage; no original Moonreed or all-row acceptance follows from these calibration results.

CrowC's [two-encounter diagnostic archive](evidence/crowc-diagnostic-v1/validation.json) preserves the first wrapper stop after later endpointHP0. Reviewed hit30/60 proves nonlethal 8 damage 58→50; later removal is flee-consistent but its runtime flag was not recorded, and Ready0/2 followed without Collect. A separate explicit KillSingle encounter records 120-frame animated BirdDeath, no body ragdoll, selected prone poses and one Collect→Ready0/3. All three raw captures completed 120; the wrapper failure was not relabeled as a successful lethal hit.

The other five 39-joint `birdController` pairs now have [exact calibration evidence](evidence/unresolved-enemy-probes-v1/README.md): `seagullA`/`enSeagull` (ordinary HP 58→48), `seagullB`/`enSeagull2` (bounded retry, then 58→50), `parrotA`/`enParrotA` (58→45), `birdJungleA`/`enJungleBird_A` (58→48) and `birdJungleB`/`enJungleBird_B` (58→50). Each has complete pass, fixture death and strict Ready evidence. These rows remain synthetic calibration probes; Duskquill’s original art and crowC evidence do not transfer across the bird topology.

Moonreed's [original fairyA live archive](../art-experiments/moonreed-sylph/live-validation.json) contains four120-frame captures and six reviewed images: attached body/four wings and readable portraits, a visibly DODGED first attempt preserved as a stopped wrapper, later normal 5 damage 58→53, and selected coherent prone death poses. Animated death keeps Animator enabled/m_DoRagdoll false. One Collect reaches Ready0/2. NativeFX and warm lighting limit appearance judgments; postReady inventory had no matching renderer, so runtime material-property verification is not claimed.

The newer [Moonreed V2 canonical archive](../art-experiments/moonreed-sylph/live-validation-v2-canonical/validation.json) pins the current exact `fairyA / enFairy01 / 121395` queue route and source assets. Four complete 120-frame captures preserve exact binding through idle, native proficiency attacks, a native dodge, ordinary HP 58→55 `fairy_damaged`, recovery and animated fixture death. Thirty reviewed originals show a coherent four-wing body; Standard `matFairyA (Instance)` uses the authored main texture with emission disabled. `m_DoRagdoll=false`, the animator remains enabled through `fairy_die` frame119, and two inactive break props receive no ragdoll credit. One Collect reaches strict Ready0/2. Fixture death, small scale, native effects and UI, general corpse lifetime, cleanup, portraits, collision, broader culling, sibling sources, resource lifetime, every animation and final art approval remain outside this exact-source result.

Native Cockatrice C now has [bounded diagnostic evidence](evidence/cockatricec-diagnostic-v1/validation.json): exact121484/enChicken, controller5949, three 120-frame captures, normal 5 damage and explicit fixture death, Animator enabled/no native ragdoll, two collects to Ready0/3. Selected calibration poses only; native A controller5948 and resource121693 remain separate.

Reefstrider now has [exact fishA01 original live evidence](../art-experiments/reefstrider-fish/live-validation-v1.json): three 120-frame captures, normal 10 damage and a separate explicit death fixture, plus native11-body settling and guarded Ready progression. Six reviewed frames support visible body/portrait coherence with substantial hero/FX occlusion. Ordinary lethal damage, original collision fit and final teardown remain untested. The earlier story-related setup stop is preserved separately and is not a model failure. No sibling fish coverage transfers.

Exact monkeyC has historical [stopped pre-heroReady setup evidence](evidence/monkeyc-setup-removal-v1/validation.json), not captured motion: native58HP-to-removal was consistent with its verified suicide proficiency, but that stopped record did not observe attack attribution. The later Tamarind V2 passive-arrival archive supplies the canonical exact-source result. Retain native AI and weapon; the stopped setup record alone grants no original-art or calibration acceptance.

A separate [Reefstrider ordinary-lethal follow-up](../art-experiments/reefstrider-fish/live-validation-ordinary-lethal-v1.json) now records eight ordinary attempts, retaining one dodge, and final7→0 damage with cheatNone. Selected collapse/corpse views and native11-body settling were reviewed; two guarded Collect submissions reached Ready0/2. This supersedes only the earlier ordinary-lethal gap, not collision-fit or final-owner teardown limits. Earlier explicit-death evidence is preserved unchanged.

The fresh [Reefstrider V2 supplement](../art-experiments/reefstrider-fish/live-validation-v2/validation.json) repeats the exact fishA01 assignment from a clean catalog-411 run: `enFishA` renderer 121695, owner369188, public scale 1.0 and captured native CEL scale 1.0. Pass, ordinary attack HP 58→48 and explicit `KillSingle` each have complete 120-frame captures; selected body/attack/loot views show the authored turquoise fish, cream belly, crown, flippers and webbed feet remaining connected, with two guarded native Collect calls and strict Ready0/2. The archive preserves 370 source-image pins, 230 gzip-lossless mappings and three videos. Fixture death, foreground hero/UI/effect occlusion, victory depth blur, culling, collision/sleeping, portraits, resource lifetime and finished-art acceptance remain bounded; no sibling fish coverage transfers.

The canonical [Reefstrider V3 supplement](../art-experiments/reefstrider-fish/live-validation-v3/validation.json) pins a fresh queue-route run to exact `fishA01 / enFishA / 121695`, owner369188, native scale 1.0, and the expected 34-bone signature. Three complete 120-frame captures preserve the authored head, face, belly, flipper arms, long legs, and webbed feet through idle, native `AttackCrit`, an ordinary HP 58→48 `Damaged` response and recovery, and native `Death` launch and ragdoll collapse. Eighteen reviewed original PNGs show a coherent custom corpse still sampled behind the loot panel. The archive independently verifies19 metadata mappings,361 source-image pins,360 capture-image pins, two assets,18 selected originals, the root review, and three videos. The explicit fixture is not ordinary lethal evidence; impact occlusion, later corpse lifetime, indirect death, other native attacks, portrait, collision, extended culling, long-session lifetime, campaign completion, final art approval, and fishA02/A03 remain outside this exact-source result.

Mournglass Wraith now has a [canonical current-profile archive](../art-experiments/mournglass-wraith/live-validation-v2-canonical/validation.json)
for exact `chaosBeast / enChaosBeast / 121008`. The two authored material slots
retain their scrolling-cloth and fixed mask/hands partition with emission disabled.
Two complete 120-frame captures and one accepted94-frame death prefix preserve
`cidle_ghost`, native `attackProf_ghost`, an immediate ordinary 5-damage HP 58→53
`damageSmall_ghost` response and recovery, repeated native attacks, and the opening
of animator-driven `death_ghost`. A later Ready poll found HP 45 after additional
combat turns; the archive preserves that state without attributing its extra8 loss
to the captured action. The renderer becomes inactive after death frame19 and is
destroyed after frame93, so full visible death, cleanup causality and final disposal
remain unproven. Thirty originals were reviewed, and strict Ready0/2 followed
without Collect. The earlier V1 evidence remains historical.

The exact monkeyC 42-bone renderer 121301 now has a [native arrival diagnostic](evidence/monkeyc-arrival-native-v1/validation.json).
The prearmed observer recorded the actual enSuicideCurse544 attack and synchronous
secondary damage 58to0. Body inactivity starts at sample108; hidden DeathIndirect
samples117to119 do not establish visible death. Initial enDiseaseHit AttackInfo
was stale prior state. The120samples span64.23468wall seconds and6.283203game
seconds, so this is not unperturbed timing evidence. Two guarded native Collect
actions reached Ready0/3 with the next Trap1 unchanged. Selected distant or
FX-obscured calibration views do not accept the original Tamarind model.
The earlier setup-removal failure remains preserved separately.

The exact dragonFrost renderer 121561, with verified shared rig representative121525,
has [diagnostic evidence with cropping limitations](evidence/dragonfrost-diagnostic-v1/validation.json).
Its native boss row disables dungeon spawning; this remains an isolated dungeon
fixture. Native scale 1 and frost effects severely crop body, wings and portraits,
so the diagnostic does not accept original art.

The canonical [Amberwake V3 supplement](../art-experiments/amberwake-dragon/live-validation-v3/validation.json)
binds the original mesh to that exact `enDragon` renderer under one native owner
at deliberate visual scale 0.25. Its complete pass and ordinary-hit captures each
retain 120 frames through settled idle, native `AttackProf`, recovery, native
`Damaged` and a later enemy action. Twenty-three reviewed originals show the
slate body, ivory horns, cyan accents, four legs, tail and amber wings remaining
coherent in those sampled motions.

The ordinary zero-focus strike used an explicit disposable hero fixture: native
attack skill was capped at0.95 and native weapon maximum damage rose from10to30.
It reduced HP 675to664 for native `Damaged`, then the runner restored the exact
physical-augmentation and maximum-damage baseline at between-room Ready. This
establishes a repeatable ordinary-hit response, not representative balance or a
guaranteed hit. The separate explicit `KillSingle` records 988 damage and retains
frames0through90 before renderer destruction. The source has `m_DoRagdoll=false`
and zero rigidbodies, so the reviewed fall and fallen pose are animated death,
not ragdoll evidence. Frame90 marks the exact renderer inactive and not visible;
it does not establish an active corpse or later lifetime. No Collect occurred,
and strict Ready succeeded at level0 room2. Ordinary lethal damage, natural boss
placement, full campaign, culling, portraits, resource lifetime and finished-art
acceptance remain open. V1 and focused V2 remain immutable historical records.

The historical [Tamarind V1 archive](../art-experiments/tamarind-trickster/live-validation-v1.json)
is preserved as a separate earlier native-arrival record and is not merged with
the fresh run. The canonical [Tamarind V2 supplement](../art-experiments/tamarind-trickster/live-validation-v2/validation.json)
pins the exact `monkeyC / enMonkeyBasey / 121301` route, current source assets,
42-bone signature and visual scale 1.0. Its complete 120-sample passive-arrival
capture records settled idle at samples40/60, native `PlayAttackSequence` entry
for `enSuicideCurse` at game frame29945, secondary damage at frame29972, HP 58to0,
and renderer inactivity from sample108. Strict native Ready0/2 was observed
immediately before the single armed arrival and Ready submission.

The exact native route attacks and removes itself before a received hero-hit
phase or hero attack can occur. V2 therefore records `hitMotion` and
`ordinaryDamage` as `not_applicable_native_behavior` through the strict
`ftkmf.source-specific-evidence-applicability.v1` object, exact reason codes and
evidence pointers to the native suicide fixture. This exception is limited to
the exact source and workflow and does not transfer to sibling monkey rows.
Hidden terminal samples establish removal, not a visible full death animation,
corpse or ordinary lethal hit. Post-combat loot, portraits, continuous material
behavior, weapon overlap, full culling, collision/sleeping, final disposal and
finished-art acceptance remain open.

Deathknight is now [source reconciled](evidence/deathknight-native-source-v1/archive-validation.json)
across seven native rows, five exact renderers121217/121218/121221/121219/121220
and two combat controllers5983(blunt)/5982(bladed). All five share the same ordered
36 bones, inverse bind matrices and mesh2385; schedules, materials, native scale,
rigid helmet/shield and effects remain distinct. A/B/C, Dboss and harazuelMinionB
use the blunt controller; Eboss/EbossEasy use bladed. Boss/minion schedules shuffle,
so serialized first items are not actual first-action evidence. No reviewed weapon
proficiency sets m_Suicide, but external removal paths remain possible.

The [exact deathknightA diagnostic](evidence/deathknighta-diagnostic-v1/README.md)
now records renderer 121217 through 120-frame pass, blocked-attack and explicit
death-fixture captures. Runtime HP was58; the source base field is50, and the
scaling cause was not measured here. The ordinary strike left HP unchanged and
showed BLOCKED. Native daze and the transition to disabled-Animator ragdoll were
observed. Two Collects added20gold and reached Ready0/5; native helmet, shield and
weapon remain separate from the calibration body. These checks do not establish
ordinary damaging hit/lethal, floor physics, final disposal, original art quality
or acceptance of sibling renderers/controllers. The native staircase's later
[preparation vote](evidence/native-stair-preparation-v1/README.md) reached floor1
through a separate observed menu, without substituting the Stair room.

The remaining 36-joint Eboss row now has [exact calibration evidence](evidence/unresolved-enemy-probes-v1/README.md): `deathknightEboss`/`deathKnight`/121220 binds cleanly and records eight bounded native attacks at HP 158→158. No ordinary damage, fixture death, loot or Ready claim is made; the deathknightA original-art and blunt-controller evidence does not transfer to this distinct bladed controller row.

The canonical [Honeyback V3 archive](../art-experiments/honeyback-portrait-v2/live-validation-v3-canonical/validation.json)
now closes the exact `bearB / enBear01 / 121467` route with the unchanged
portrait-v2 GLB and texture. Three complete 120-frame captures preserve the
38-joint `Root_M` binding through `bear_idle`, `bear_attackBite`, an ordinary
HP 72 to64 `Damaged` response and recovery, later `bear_attackSwipe` and
`bear_attackPound`, and explicit-fixture `bear_deathHeavy`. Seventeen reviewed
original PNGs show a coherent dark-brown and golden bear through the sampled
motions and a readable prone pose. The exact renderer is active, enabled,
reported visible and identity-stable in all 360 frames. Its
`m_DoRagdoll=false` and zero rigidbodies make the observed death animated.
One guarded Collect reaches strict Ready0/2. KillSingle64 to0 is separate from
ordinary lethal evidence; later loot occlusion leaves collision, general corpse
lifetime, cleanup and final disposal unproven. The older PortraitCam failure
and scoped EncounterCam portrait acceptance remain separate and do not transfer
to other cameras, resolutions or the native encounter-row UI.

The canonical [Duneshade V2 archive](../art-experiments/duneshade-desert-asp/live-validation-v2-canonical/validation.json)
closes the exact `snakeDesertA / enDesertSnakeA / 121552` route with the original
Duneshade GLB and texture. Three complete 120-frame captures preserve the
46-joint `Root_M` binding through `Snake_Idle`, `Snake_BiteAttack`, an ordinary
HP 58 to48 `Damaged` response with `Snake_HitSmall`, recovery, a later bite, and
the separate explicit-fixture death. The source has `m_DoRagdoll=true`: all 13
bodies remain kinematic through frame 27, the animator disables and all become
dynamic at frame 28, motion begins at frame 29, and measured motion is zero from
frame 50 through 119. Seventeen reviewed originals show the custom serpent
coherent through sampled motion and settled as a complete layered ground pose.
The exact renderer stays active, enabled, reported visible and identity-stable
in all 360 frames. One guarded Collect reaches strict Ready0/2. KillSingle48 to0
is separate from ordinary lethal evidence; later loot occlusion leaves exact
collision, general corpse lifetime, cleanup and final disposal unproven.

The canonical [Sargassum primary V2 archive](../art-experiments/abyssal-kraken/live-validation-sargassum-primary-v2-canonical/validation.json)
closes the representative `krakenTentacle / krakenTentacle / 121595` topology
route with the unchanged original V2 GLB and texture. Two complete 120-frame
captures preserve the 20-joint `Root_M` binding through `Tentacle_Idle_M`,
native `Tentacle_Attack2_M`, an ordinary HP 162 to154 `Damaged` response with
`Tentacle_Damaged_M`, recovery, `Tentacle_AttackGrab_Mirrored`, and
`Tentacle_Attack1_M`. The separate explicit KillSingle fixture records HP 154
to0, 1000 damage, and `Tentacle_Death`. The source has `m_DoRagdoll=false` and
zero rigidbodies, so the withdrawal is animated. Its accepted 91-frame prefix
keeps exact identity through frame 90, when the renderer becomes inactive and
not visible and the animator disables; the next sample reports renderer
destruction. Seventeen reviewed originals show coherent sampled deformation but
also show that the tall body and broad attack arcs exceed the fixed camera.
Strict Ready is observed at level0 room2 with one active button and no Collect.
The prefix does not establish full death duration, cleanup causality, corpse
lifetime or final disposal, and the archive does not transfer to the mirrored
controller or another Kraken source.
