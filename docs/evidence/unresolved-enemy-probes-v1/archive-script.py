#!/usr/bin/env python3
"""Archive exact renderer probes without game payloads."""
from pathlib import Path
import gzip, hashlib, json, os, shutil, subprocess

ROOT = next(p for p in Path(__file__).resolve().parents if (p / "FTKModFramework").is_dir())
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
OUT = ROOT / "docs" / "evidence" / "unresolved-enemy-probes-v1"
INDEX = ROOT / "docs" / "model-runtime-validation.json"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
MAPPING = ROOT / "scratch" / "enemy-rig-mapping-reproducible.json"

CASES = [
    dict(name="rocA", native="rocA", profile="ftkmf_modeltest_probe_roca", renderer="enRoc01", source_id=121238,
         case="case-7a3fd57ee2634cd89bc08e2a4ae0d927", stage="case-71a41e1d639a47289c30da1d666ab206",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom Roc art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Selected views and videos do not establish full motion or culling acceptance."]),
    dict(name="seaKing", native="seaKing", profile="ftkmf_modeltest_probe_seaking", renderer="enSeaKing", source_id=121357,
         case="case-d2931085f38f43f782e1ee8341d1d896", stage="case-b3b0c19a9046458fbbc8748a45e0a218",
         partial="case-65e0457691df44939d8e553827ebe495",
         status="exact_binding_pass_attack_hp_unchanged_stopped_before_lethal",
         limits=["Pass and ordinary Attack captures are complete; observed HP stayed 720 to 720.",
                 "The recorder stopped at no_hp_loss_unclassified without guessing block, dodge, or immunity.",
                 "The separate KillSingle capture ended when the renderer was destroyed at frame 91; it is retained as a partial fixture only.",
                 "No ordinary damaging/lethal or finished-art acceptance is claimed."]),
    dict(name="snakeJungleC", native="snakeJungleC", profile="ftkmf_modeltest_probe_snakejunglec", renderer="enJungleSnakeC", source_id=121424,
         case="case-7d7419ffd8d34cceb49a1c47bdaf5baf", stage="case-242c7a3f650d41098bcab77ae659a906",
         status="exact_binding_pass_attack_hp_unchanged_stopped_before_lethal",
         limits=["Pass and ordinary Attack captures are complete; observed HP stayed 86 to 86.",
                 "The recorder stopped at no_hp_loss_unclassified without inferring a combat cause.",
                 "No KillSingle, ordinary lethal, or finished-art acceptance is claimed."]),
    dict(name="snakeDesertA", native="snakeDesertA", profile="ftkmf_modeltest_probe_snakedeserta", renderer="enDesertSnakeA", source_id=121552,
         case="case-42d8bdc26a4844618e77b8f0996a172a", stage="case-2aae50c189184bb983992f874353a752",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom Desert Snake art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Sibling Snake rows retain their own exact renderer/controller scope."]),
    dict(name="krakenHead", native="krakenHead", profile="ftkmf_modeltest_probe_krakenhead", renderer="kraken2", source_id=121035,
         case="case-e17344059db94e13ae38847628fa5702", stage="case-3780ae6cbad34e25a706fccfea443204",
         status="exact_binding_pass_attack_hp_unchanged_stopped_before_lethal",
         limits=["This is modern krakenHead/kraken2 evidence, separate from old enkrakenhead resource fixtures.",
                 "Pass and ordinary Attack captures are complete; observed HP stayed 324 to 324.",
                 "No modern Kraken lethal, tentacle, adapter, or finished-art acceptance is claimed."]),
    dict(name="krakenTentacle", native="krakenTentacle", profile="ftkmf_modeltest_probe_krakententacle", renderer="krakenTentacle", source_id=121595,
         case="case-ca9d3a6d548e4cd08137f6eae699290c", stage="case-a8d36943d38047fb9e02e8e2fa97c8b4",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom tentacle art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "The mirrored controller row retains its own exact evidence."]),
    dict(name="krakenTentacleMirror", native="krakenTentacleMirror", profile="ftkmf_modeltest_probe_krakententaclemirror", renderer="krakenTentacle", source_id=121595,
         case="case-a021e22834ef4db9b5a96c692ea3adf6", stage="case-75b7fe8f6a6d404a8d09dd6edc46a6e0",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom mirrored tentacle art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "The non-mirrored controller row retains its own exact evidence."]),
    dict(name="seaKingTentacleA", native="seaKingTentacleA", profile="ftkmf_modeltest_probe_seakingtentaclea", renderer="KrakenGodTentacle", source_id=121315,
         case="case-87e8e1a0b5e04c35a56e8962eb55531c", stage="case-d70fbef61efa44c1abc0f29d27c9d057",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom Sea King tentacle art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Tentacle B retains its own exact controller evidence."]),
    dict(name="seaKingTentacleB", native="seaKingTentacleB", profile="ftkmf_modeltest_probe_seakingtentacleb", renderer="KrakenGodTentacle", source_id=121315,
         case="case-8f7e356cbdc240b9b8271ff8217bc23c", stage="case-7245b4316f5c47378325d039d25e4f8d",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom Sea King tentacle art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Tentacle A retains its own exact controller evidence."]),
    dict(name="plantG", native="plantG", profile="ftkmf_modeltest_probe_plantg", renderer="enJungleNibbler_B", source_id=121584,
         case="case-f018d2a4f6b8456f909f01bb883d775d", stage="case-fd62cbc77acb449f9eb7abe64aebd021",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom plantG art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other plant rows retain their own exact renderer/controller evidence."]),
    dict(name="skellyA", native="skellyA", profile="ftkmf_modeltest_probe_skellya", renderer="skelly01naked", source_id=120979,
         case="case-552e1d9aa5a342cab867e4f07c47794a", stage="case-508ae03d125a46ed9a3da1469c52cdd3",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom skellyA art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other 37-joint rows retain their own exact renderer/controller evidence."]),
    dict(name="hagA", native="hagA", profile="ftkmf_modeltest_probe_haga", renderer="hag01", source_id=121109,
         case="case-13667bb700ac4efaa34ec123d63bb085", stage="case-8712d7e6d5bf45ae962bf56563909f2a",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom hagA art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other hag and 37-joint rows retain their own exact renderer/controller evidence."]),
    dict(name="bearmanA", native="bearmanA", profile="ftkmf_modeltest_probe_bearmana", renderer="bearman", source_id=121195,
         case="case-c20410f13bf2480c96bfeca4e4d03184", stage="case-0cff61c28015433dacbc9b85a8c788df",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom bearmanA art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other bear and 37-joint rows retain their own exact renderer/controller evidence."]),
    dict(name="banditA", native="banditA", profile="ftkmf_modeltest_probe_bandita", renderer="bandit02", source_id=121005,
         case="case-7c8831fbccde432abc9e80a1a4f7f7e6", stage="case-73cbf046bbee49469362337774919584",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditA art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other 37-joint rows retain their own exact renderer/controller scope."]),
    dict(name="thiefA", native="thiefA", profile="ftkmf_modeltest_probe_thiefa", renderer="enThief01", source_id=120997,
         case="case-b9c395662f9240169d9a1fc6129bcd22", stage="case-b9c395662f9240169d9a1fc6129bcd22",
         status="exact_binding_pass_stopped_native_self_termination_before_attack",
         limits=["Exact 37-joint binding and the complete pass capture are recorded; no finished custom thiefA art is accepted.",
                 "The native thiefA controller reduced the staged target from HP 58 to 0 during the pass action before any hero attack; the recorder preserves this observed native self-termination boundary.",
                 "No ordinary hero attack, nonlethal HP-loss, KillSingle fixture, loot, Ready, or finished-art acceptance is claimed."]),
    dict(name="hagB", native="hagB", profile="ftkmf_modeltest_probe_hagb", renderer="hag02", source_id=120995,
         case="case-589e39d2c83c438aa8643781e9dd4412", stage="case-800f58fad0134564954954da88814ee9",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom hagB art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; hagA and other 37-joint rows retain their own exact scope."]),
    dict(name="skellyB", native="skellyB", profile="ftkmf_modeltest_probe_skellyb", renderer="skelly04archer", source_id=121169,
         case="case-cc6cbe580cbf45468469746defb98016", stage="case-cc6cbe580cbf45468469746defb98016",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom skellyB art is accepted.",
                 "Ordinary HP 58 to 55 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; skellyA and other 37-joint rows retain their own exact scope."]),
    dict(name="witchA", native="witchA", profile="ftkmf_modeltest_probe_witcha", renderer="witch01", source_id=121039,
         case="case-cadc697e0ccc41d1b2ae4c51672fb4f8", stage="case-20378a97afce4974bfdddaf7f0b8a94f",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom witchA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other magic-controller and 37-joint rows retain their own exact scope."]),
    dict(name="banditD", native="banditD", profile="ftkmf_modeltest_probe_banditd", renderer="enBanditB02", source_id=121235,
         case="case-77f2b29dc6b14bbaa113799b7807948c", stage="case-a9114c981b334cf7be0ccc9249cb4d18",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditD art is accepted.",
                 "Ordinary HP 58 to 56 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other bandit and 37-joint rows retain their own exact scope."]),
    dict(name="banditC", native="banditC", profile="ftkmf_modeltest_probe_banditc", renderer="enBanditB01", source_id=121156,
         case="case-a27b8b9c088e499ea6b8b48c582b92f5", stage="case-9aec46a2aad344fc86f4b1b7b7135318",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditC art is accepted.",
                 "The first five bounded native attacks observed HP 58 to 58; attempt 6 measured nonlethal HP loss to 57 and was required before fixture death and Ready acceptance.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other bandit and 37-joint rows retain their own exact scope."]),
    dict(name="assassinA", native="assassinA", profile="ftkmf_modeltest_probe_assassina", renderer="bandit01", source_id=121075,
         case="case-2bee38ceb0da47c6a0b82b76c0dbf884", stage="case-98c0a84e755e41219ed4b5be9324288d",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom assassinA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other bow-controller and 37-joint rows retain their own exact scope."]),
    dict(name="hagC", native="hagC", profile="ftkmf_modeltest_probe_hagc", renderer="hag03", source_id=121089,
         case="case-dd4260c0b56b4e4cb118a970a4c49c5b", stage="case-7b07b1e67a3448c59713d9c8f916a652",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom hagC art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; hagA/hagB and other 37-joint rows retain their own exact scope."]),
    dict(name="skellyC", native="skellyC", profile="ftkmf_modeltest_probe_skellyc", renderer="skelly03fighter", source_id=121228,
         case="case-529ee4e4763d421891977b11b4f5c725", stage="case-c52534529003412a976480743b932bdd",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom skellyC art is accepted.",
                 "Ordinary HP 58 to 54 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; skellyA/skellyB and other 37-joint rows retain their own exact scope."]),
    dict(name="assassinB", native="assassinB", profile="ftkmf_modeltest_probe_assassinb", renderer="enAssassinB", source_id=121266,
         case="case-c4c35b59144e4dc7a2904a4cc91ef441", stage="case-2e4ae723fc6c4bcba675b31eb3eb1570",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom assassinB art is accepted.",
                 "Ordinary HP 68 to 58 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other bow-controller and 37-joint rows retain their own exact scope."]),
    dict(name="witchC", native="witchC", profile="ftkmf_modeltest_probe_witchc", renderer="enWitchC", source_id=121547,
         case="case-3611e2eb860b403ab6f0d969453dbf7f", stage="case-a6fb639e729b4481b647acd80614de40",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom witchC art is accepted.",
                 "Ordinary HP 63 to 55 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other magic-controller and 37-joint rows retain their own exact scope."]),
    dict(name="demonA", native="demonA", profile="ftkmf_modeltest_probe_demona", renderer="demon", source_id=121166,
         case="case-a8cb60a21d9c437eba956dcd4c4d2c74", stage="case-570f5d2c98874716b2987ea0cbd6a696",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass capture are recorded; no finished custom demonA art is accepted.",
                 "Eight bounded native hero attacks all observed HP 108 to 108; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed."]),
    dict(name="ghostA", native="ghostA", profile="ftkmf_modeltest_probe_ghosta", renderer="ghost01", source_id=121160,
         case="case-066c227891bb41a3956c80f176111cf3", stage="case-a1af6ba961434658ae3f83a85f0f5ee0",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom ghostA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the renderer-destroyed capture prefix is retained with native Ready observed."]),
    dict(name="wildmageA", native="wildmageA", profile="ftkmf_modeltest_probe_wildmagea", renderer="wildMage02", source_id=121030,
         case="case-8c964220a64a4d67bdb6bce94bc33627", stage="case-8e0edad0b34d45778e9f32219723033e",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom wildmageA art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other magic-controller and 37-joint rows retain their own exact scope."]),
    dict(name="scourgeA", native="scourgeA", profile="ftkmf_modeltest_probe_scourgea", renderer="enDisciple", source_id=121535,
         case="case-e03f75aa1580425cb873842f33de265b", stage="case-cc73665ad0db41d0a6132d76f8341e5d",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom scourgeA art is accepted.",
                 "Ordinary HP 126 to 118 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the ghost-controller row remains a separate exact assignment."]),
    dict(name="scourgeB", native="scourgeB", profile="ftkmf_modeltest_probe_scourgeb", renderer="enScourgeBanditKing", source_id=120988,
         case="case-398c79a054b14073833ee32505d33dcc", stage="case-3d1a7c7ca7554ce6b464f1127ce23467",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom scourgeB art is accepted.",
                 "Ordinary HP 81 to 79 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other 37-joint blunt-controller rows retain their own exact scope."]),
    dict(name="beastmanA", native="beastmanA", profile="ftkmf_modeltest_probe_beastmana", renderer="beastman01", source_id=121227,
         case="case-18d11ba51d8649de8638bcf941026c10", stage="case-d55ac89191b84cfaa8a448d5a7c53667",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom beastmanA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; beastmanB retains its own controller/source assignment despite the shared renderer mesh."]),
    dict(name="beastmanB", native="beastmanB", profile="ftkmf_modeltest_probe_beastmanb", renderer="beastman01", source_id=121139,
         case="case-ab89b0a41803480da6f79ed52c50c71e", stage="case-ec4ce87bf4c2422d99ee3f023b9f4361",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom beastmanB art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; beastmanA retains its own controller/source assignment despite the shared renderer mesh."]),
    dict(name="cragHulk", native="cragHulk", profile="ftkmf_modeltest_probe_craghulk", renderer="rockman01", source_id=121126,
         case="case-63dd05788247498d8845ccecbbb319f5", stage="case-728884bfd54141d1a0ce3b6cddea5977",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cragHulk art is accepted.",
                 "A prior fresh run stopped after HP 58 to 58; a separate fresh bounded-retry run then measured ordinary HP 58 to 56 on attempt 1.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the earlier no-loss boundary is preserved and no combat cause is inferred."]),
    dict(name="beastmanC", native="beastmanC", profile="ftkmf_modeltest_probe_beastmanc", renderer="beastman01", source_id=121069,
         case="case-f07eb6a6740f4bf9ba1b1e33d2f85ca8", stage="case-0e8ce9d6c1ea47f684ce02838c49c2cc",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom beastmanC art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; beastmanA and beastmanB retain their own source/controller assignments."]),
    dict(name="drycorpseA", native="drycorpseA", profile="ftkmf_modeltest_probe_drycorpsea", renderer="enDryCorpse01", source_id=120994,
         case="case-dd09b069c9204627b4c9bb95ec4039c8", stage="case-1b9a6a1525bf47d4a2182f781fa69e4a",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom drycorpseA art is accepted.",
                 "A prior fresh run stopped after HP 58 to 58; a separate fresh bounded-retry run measured ordinary HP 58 to 48 on attempt 1.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the earlier no-loss boundary is preserved and no combat cause is inferred."]),
    dict(name="lizardmanA", native="lizardmanA", profile="ftkmf_modeltest_probe_lizardmana", renderer="boggling", source_id=121028,
         case="case-20f19e04b29149398ae5e51669d4a034", stage="case-ca4281e774444d1eaaa53cc4cc1ba07f",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom lizardmanA art is accepted.",
                 "Ordinary HP 58 to 56 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other spear-controller and 37-joint rows retain their own exact scope."]),
    dict(name="catmageA", native="catmageA", profile="ftkmf_modeltest_probe_catmagea", renderer="mysticCat", source_id=121022,
         case="case-d8cee2681e7b4156b2369cfc16cee619", stage="case-aca4df72d34447979d0576e8b38874e9",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom catmageA art is accepted.",
                 "The first seven bounded native attacks observed HP 58 to 58; attempt 8 measured ordinary HP 58 to 48.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; earlier no-loss outcomes are preserved and no combat cause is inferred."]),
    dict(name="wildmageB", native="wildmageB", profile="ftkmf_modeltest_probe_wildmageb", renderer="wildmage01", source_id=121112,
         case="case-766bb8b3411c4293baa5ec3429247e1c", stage="case-afed23d390054dbe88b1ee3c8075d589",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom wildmageB art is accepted.",
                 "Ordinary HP 70 to 62 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; wildmageA retains its own exact renderer/controller assignment."]),
    dict(name="bisonA", native="bisonA", profile="ftkmf_modeltest_probe_bisona", renderer="bisontaur", source_id=120958,
         case="case-40c175f6196c41ea964c326698f8d82f", stage="case-2c028a83f202432c9590561c4fc038bd",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom bisonA art is accepted.",
                 "Ordinary HP 58 to 54 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other dual-weapon and 37-joint rows retain their own exact scope."]),
    dict(name="cyclopsA", native="cyclopsA", profile="ftkmf_modeltest_probe_cyclopsa", renderer="triclops", source_id=121006,
         case="case-04711f914f2042f89e59fd907a6f3f17", stage="case-b1664bcc50704086b8ff935b0260076b",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cyclopsA art is accepted.",
                 "Ordinary HP 90 to 82 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; cyclopsC retains its own giant-controller assignment."]),
    dict(name="cyclopsC", native="cyclopsC", profile="ftkmf_modeltest_probe_cyclopsc", renderer="triclopsBaby", source_id=121102,
         case="case-8de8e428fdc44d0d8d647307765af9a8", stage="case-28e97481690f4795a76ae9c98e942592",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cyclopsC art is accepted.",
                 "Ordinary HP 68 to 57 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; cyclopsA retains its own giant-controller assignment."]),
    dict(name="mummyA", native="mummyA", profile="ftkmf_modeltest_probe_mummya", renderer="mummy", source_id=121097,
         case="case-0404b9147b0e42bdae351d9f5afc2521", stage="case-17122ebd817547efb1dd628354f06a72",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom mummyA art is accepted.",
                 "A prior fresh run stopped after HP 81 to 81; a separate fresh bounded-retry run then measured ordinary HP 81 to 77 on attempt 2.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the earlier no-loss boundary is preserved and no combat cause is inferred."]),
    dict(name="mummyB", native="mummyB", profile="ftkmf_modeltest_probe_mummyb", renderer="mummy02", source_id=120969,
         case="case-a9ae37fe7d01442e8fe275fc66b5c5b7", stage="case-40b9268058504a3b8a118322c795419b",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom mummyB art is accepted.",
                 "Ordinary HP 90 to 87 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; mummyA retains its own zombie-controller assignment."]),
    dict(name="owlbearA", native="owlbearA", profile="ftkmf_modeltest_probe_owlbeara", renderer="enOwlBear", source_id=121209,
         case="case-0972851ae90e4579b0996f01637e2fe6", stage="case-577b92e6bfa640feb3d06c52e7a0134a",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom owlbearA art is accepted.",
                 "Ordinary HP 126 to 118 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other brute-controller and 37-joint rows retain their own exact scope."]),
    dict(name="cultistA", native="cultistA", profile="ftkmf_modeltest_probe_cultista", renderer="cultist2", source_id=120950,
         case="case-a7c85a757fd74d039a05527f599e36bb", stage="case-232e9f068a3b49939dd42b4f184465d7",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cultistA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other spear, magic and 37-joint rows retain their own exact scope."]),
    dict(name="cultistBoss1", native="cultistBoss1", profile="ftkmf_modeltest_probe_cultistboss1", renderer="cultist3", source_id=121091,
         case="case-dd1ed958658442c1892f49c138ca38b8", stage="case-431d1d1c563847c490e32955f40335a8",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cultistBoss1 art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; cultistA and cultistC retain their own exact assignments."]),
    dict(name="cultistC", native="cultistC", profile="ftkmf_modeltest_probe_cultistc", renderer="cultistC", source_id=121257,
         case="case-e66f777a766a4b2aaa08524286c3112d", stage="case-319965cc71104072b2c5250ef403a469",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass capture are recorded; no finished custom cultistC art is accepted.",
                 "Eight bounded native hero attacks all observed HP 58 to 58; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed; cultistA and cultistBoss1 retain their own exact assignments."]),
    dict(name="skellymageA", native="skellymageA", profile="ftkmf_modeltest_probe_skellymagea", renderer="skelly07mage", source_id=121146,
         case="case-5722623da7f74ea3839201610283e79d", stage="case-2bf6233239164d5fb7970caca2d6b459",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom skellymageA art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other magic-controller and 37-joint rows retain their own exact scope."]),
    dict(name="skellymageD", native="skellymageD", profile="ftkmf_modeltest_probe_skellymaged", renderer="skellyMage02", source_id=121469,
         case="case-21e0888febc341959fbc20c4e1a53920", stage="case-1b838f449d494cd6b6f7b332369c2507",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom skellymageD art is accepted.",
                 "A prior fresh run stopped after HP 58 to 58; a separate fresh bounded-retry run measured ordinary HP 58 to 48 on attempt 1.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the earlier no-loss boundary is preserved and no combat cause is inferred."]),
    dict(name="wraithA", native="wraithA", profile="ftkmf_modeltest_probe_wraitha", renderer="enWraith", source_id=121252,
         case="case-fe19ac77203e418eb4a5a5233e9f7225", stage="case-a7e256dea1a24ab6a8f847edd181007f",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom wraithA art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; native Ready was observed with no usable Collect button exposed."]),
    dict(name="druidA", native="druidA", profile="ftkmf_modeltest_probe_druida", renderer="enDruid", source_id=121130,
         case="case-17bfe65f83e6471f86b4ff2a9abd1d5d", stage="case-58087baeeeff4dcbad62556ebab51f41",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom druidA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other bow-controller and 37-joint rows retain their own exact scope."]),
    dict(name="sharkBruteA", native="sharkBruteA", profile="ftkmf_modeltest_probe_sharkbrutea", renderer="enSharkBruteA", source_id=121432,
         case="case-06ab05ff602d48a4ad38049659d9d904", stage="case-fa332cd71cfe460fa23cd42a781f2947",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom sharkBruteA art is accepted.",
                 "A prior fresh run stopped after HP 99 to 99; a separate fresh bounded-retry run then measured ordinary HP 99 to 97 on attempt 2.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the earlier no-loss boundary is preserved and no combat cause is inferred."]),
    dict(name="mindflayerA", native="mindflayerA", profile="ftkmf_modeltest_probe_mindflayera", renderer="mindMelter", source_id=121258,
         case="case-6f19eb2bfc04403993090b469f31ffbc", stage="case-cc06b997250c42bd910de5bb81ce33a3",
         status="exact_binding_complete_pass_attack_kill_fixture_loot_ready_observed",
         limits=["Calibration probe only; no finished custom mindflayerA art is accepted.",
                 "Ordinary HP 63 to 53 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; this exact source/controller assignment remains separate from other 37-joint rows."]),
    dict(name="ogreA", native="ogreA", profile="ftkmf_modeltest_probe_ogrea", renderer="enOgre", source_id=121076,
         case="case-65aebbab2f5d4557b369b5238d7a3981", stage="case-74de1143b7cc43c68c10386f62a7a330",
         status="exact_binding_complete_pass_attack_kill_fixture_loot_ready_observed",
         limits=["Calibration probe only; no finished custom ogreA art is accepted.",
                 "Ordinary HP 108 to 104 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the armored ogre row remains a separate source/controller assignment."]),
    dict(name="ogreC", native="ogreC", profile="ftkmf_modeltest_probe_ogrec", renderer="enOgre_Armored", source_id=121350,
         case="case-38bcb474ac1c4acf8177420380ea3a62", stage="case-891c435c1fad46c89a9f9d0b6d48c41c",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass/attack captures are recorded; no finished custom ogreC art is accepted.",
                 "Eight bounded native hero attacks all observed HP 158 to 158; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed; ogreA remains a separate renderer/source assignment."]),
    dict(name="ratthiefA", native="ratthiefA", profile="ftkmf_modeltest_probe_ratthiefa", renderer="ratThief", source_id=121128,
         case="case-0bd6903eb9aa49b8b6e681b2e160bd67", stage="case-3bf5c7a2066247e4949995b7806e9c4b",
         status="exact_binding_pass_attack_kill_fixture_loot_stopped_no_progress",
         limits=["Exact 37-joint binding, pass, ordinary Attack 58 to 48 and explicit KillSingle fixture are complete; no finished custom ratthiefA art is accepted.",
                 "One native Collect click was accepted, but no item/button or reward progress changed and strict Ready never appeared.",
                 "The once-only runner stopped without a second Collect or forced Ready; no finished-art acceptance is claimed."]),
    dict(name="scourgeC", native="scourgeC", profile="ftkmf_modeltest_probe_scourgec", renderer="scourgeJester", source_id=121203,
         case="case-6e2c1b3b13a8444eafd0de7174809d3c", stage="case-e765c38ca2a04d62ba422e9c820d0742",
         status="exact_binding_pass_attack_kill_fixture_loot_stopped_no_progress",
         limits=["Exact 37-joint binding, pass, ordinary Attack 63 to 58 and explicit KillSingle fixture are complete; no finished custom scourgeC art is accepted.",
                 "Two native Collect clicks were accepted, but no item/button or reward progress changed and strict Ready never appeared.",
                 "The once-only runner stopped without another Collect or forced Ready; scourgeA/scourgeB and other scourge rows retain separate assignments."]),
    dict(name="scourgeD", native="scourgeD", profile="ftkmf_modeltest_probe_scourged", renderer="enScourgeFraybee", source_id=121034,
         case="case-764fd78492734280ac009f849302cc26", stage="case-340d571f689346c5b0c0190e79c4cef6",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom scourgeD art is accepted.",
                 "Ordinary HP 126 to 116 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other scourge and 37-joint rows remain separate assignments."]),
    dict(name="scourgeE", native="scourgeE", profile="ftkmf_modeltest_probe_scourgee", renderer="scourgeTime", source_id=121138,
         case="case-213a4dbb0abd45c9b93633c546484ec8", stage="case-f22a8e77d06c44d89ff470c16352cf7d",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom scourgeE art is accepted.",
                 "Ordinary HP 90 to 13 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other scourge and 37-joint rows remain separate assignments."]),
    dict(name="scourgeH", native="scourgeH", profile="ftkmf_modeltest_probe_scourgeh", renderer="enScourgeVolcanoWizard", source_id=121208,
         case="case-6bb81815765d424ca57f7b6877addda7", stage="case-7a1329b8e5e747479f34141ed3789d6f",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom scourgeH art is accepted.",
                 "Ordinary HP 81 to 73 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other scourge and magic-controller rows remain separate assignments."]),
    dict(name="scourgeI", native="scourgeI", profile="ftkmf_modeltest_probe_scourgei", renderer="scourgeDemis", source_id=121445,
         case="case-aadd9508a2ae426e8ff0790aaac22b06", stage="case-bcd3c01bc79d49829aaad1fe1d5362c3",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom scourgeI art is accepted.",
                 "A prior fresh run stopped after HP 108 to 108; a separate fresh bounded-retry run measured ordinary HP 108 to 105 on attempt 1, followed by two native Collect clicks and strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the earlier no-loss boundary is preserved and no combat cause is inferred."]),
    dict(name="scourgeJ", native="scourgeJ", profile="ftkmf_modeltest_probe_scourgej", renderer="enScourgeWitchdoctor", source_id=121614,
         case="case-3ba448b036ff4e34b13bac5666ba7804", stage="case-243c88f816c34a1b9035e1eebcf4d9c3",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass/attack captures are recorded; no finished custom scourgeJ art is accepted.",
                 "Eight bounded native hero attacks all observed HP 108 to 108; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed; other scourge rows remain separate assignments."]),
    dict(name="scourgeK", native="scourgeK", profile="ftkmf_modeltest_probe_scourgek", renderer="enScourgeSiren", source_id=121462,
         case="case-8d005e5adbb84b99ad17f9f29cb96a49", stage="case-c1c02b20503443c09d4380fb822045de",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom scourgeK art is accepted.",
                 "Ordinary HP 216 to 213 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other scourge and ghost-controller rows remain separate assignments."]),
    dict(name="leprechaunA", native="leprechaunA", profile="ftkmf_modeltest_probe_leprechauna", renderer="leprechaun", source_id=121214,
         case="case-58d2175d49a043e4bbee36299e48d3bb", stage="case-6ac50107abd04df0918d95e9b0259d50",
         status="exact_binding_pass_attack_target_removed_stopped_before_fixture",
         limits=["Exact 37-joint binding, complete pass capture and native Attack capture are recorded; no finished custom leprechaunA art is accepted.",
                 "The native attack reduced the staged target from HP 58 to 0 before the explicit KillSingle fixture; the recorder preserves this target-removal boundary.",
                 "No ordinary nonlethal damage, fixture death, loot, Ready or finished-art acceptance is claimed."]),
    dict(name="banditB", native="banditB", profile="ftkmf_modeltest_probe_banditb", renderer="BanditWarrior", source_id=121201,
         case="case-18b293b8bfd54ca9aba609cc40cf6950", stage="case-e9f27ac13161455a9d84d457d250f691",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditB art is accepted.",
                 "Ordinary HP 58 to 52 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other bandit and 37-joint rows remain separate assignments."]),
    dict(name="bisonUndead", native="bisonUndead", profile="ftkmf_modeltest_probe_bisonundead", renderer="enBisonUndead", source_id=121085,
         case="case-6bbfd6f6e54843f799f43b6642c6e33b", stage="case-bc163ba0ae4a4a469d8fb2462714699b",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass/attack captures are recorded; no finished custom bisonUndead art is accepted.",
                 "Eight bounded native hero attacks all observed HP 113 to 113; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed."]),
    dict(name="cultistE", native="cultistE", profile="ftkmf_modeltest_probe_cultiste", renderer="cultistC", source_id=121257,
         case="case-bba930026fed4d9883eb3af37528ec18", stage="case-a737f0a792a9496b95d089f88ee26d0c",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cultistE art is accepted.",
                 "Ordinary HP 86 to 76 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; cultistC remains a separate blunt-controller assignment despite the shared renderer path."]),
    dict(name="cultistA2", native="cultistA2", profile="ftkmf_modeltest_probe_cultista2", renderer="cultist2B", source_id=121172,
         case="case-285ea5fac01e4ec28226d67a7fbbe801", stage="case-a64c1eb049154778a79908d60c0a1f34",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cultistA2 art is accepted.",
                 "The first bounded native attack observed HP 58 to 58; attempt 2 measured ordinary HP 58 to 48, followed by fixture death, one native Collect and strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; cultistA retains its own source/controller assignment."]),
    dict(name="foxshamanA", native="foxshamanA", profile="ftkmf_modeltest_probe_foxshamana", renderer="enFoxShaman", source_id=121246,
         case="case-b61b6c503e6b46328bbd58c81f645561", stage="case-7eb5885f8ff6432f9a56a53cf220538e",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom foxshamanA art is accepted.",
                 "Ordinary HP 58 to 45 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other magic-controller and 37-joint rows remain separate assignments."]),
    dict(name="yetiA", native="yetiA", profile="ftkmf_modeltest_probe_yetia", renderer="enYeti", source_id=121381,
         case="case-1afb740cec34492a8bcbb3370b954c67", stage="case-90336346ae4b4156b14c707a5625cb0c",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom yetiA art is accepted.",
                 "Ordinary HP 89 to 87 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other brute-controller and 37-joint rows remain separate assignments."]),
    dict(name="swampmonsterA", native="swampmonsterA", profile="ftkmf_modeltest_probe_swampmonstera", renderer="swampMonster", source_id=121114,
         case="case-4a9fd608b08542e4a5d2e22a303db305", stage="case-1827c6ffd17c40bd8c31d40676c64a4d",
         status="exact_binding_pass_attack_kill_fixture_loot_stopped_no_progress",
         limits=["Exact 37-joint binding, pass, ordinary Attack 72 to 64 and explicit KillSingle fixture are complete; no finished custom swampmonsterA art is accepted.",
                 "Two native Collect clicks were accepted, but no item/button or reward progress changed and strict Ready never appeared.",
                 "The once-only runner stopped without another Collect or forced Ready; other blunt-controller rows retain separate assignments."]),
    dict(name="skellyBardA", native="skellyBardA", profile="ftkmf_modeltest_probe_skellybarda", renderer="skelly01naked", source_id=120980,
         case="case-17dc183dcc7c4f6eba16e34c7adb3166", stage="case-008922018b9f4cfea248683fc6ed2d0e",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom skellyBardA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other lute-controller and 37-joint rows remain separate assignments."]),
    dict(name="cultistC2", native="cultistC2", profile="ftkmf_modeltest_probe_cultistc2", renderer="cultistC2", source_id=121230,
         case="case-2a13d8dff9d64cee9ad3fffbf36f514a", stage="case-14f31c8b379b47d98b14bbb499034827",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cultistC2 art is accepted.",
                 "Ordinary HP 58 to 45 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other cultist and magic-controller rows remain separate assignments."]),
    dict(name="cultistD3", native="cultistD3", profile="ftkmf_modeltest_probe_cultistd3", renderer="cultistC3", source_id=121096,
         case="case-a45f165ff520413fb09f469e498eedf1", stage="case-90862d2aee34499eb4c88cd10c417996",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cultistD3 art is accepted.",
                 "The first two bounded native attacks observed HP 70 to 70; attempt 3 measured ordinary HP 70 to 68, followed by fixture death, one native Collect and strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other cultist and spear-controller rows remain separate assignments."]),
    dict(name="cultistBoss2", native="cultistBoss2", profile="ftkmf_modeltest_probe_cultistboss2", renderer="cultist3", source_id=121092,
         case="case-1890d7d7e7e246dea2f1058aa38715b9", stage="case-41b2ad180abd4f0db13bba92b715c5d6",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cultistBoss2 art is accepted.",
                 "Ordinary HP 81 to 76 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; cultistBoss1 and other magic-controller rows remain separate assignments."]),
    dict(name="bardA", native="bardA", profile="ftkmf_modeltest_probe_barda", renderer="enBard", source_id=121163,
         case="case-30e5bacae67849299727942ef933a490", stage="case-63af071683c541abb8a2b4c509d0f115",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom bardA art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other lute-controller and 37-joint rows remain separate assignments."]),
    dict(name="pirateA", native="pirateA", profile="ftkmf_modeltest_probe_piratea", renderer="enPirate02", source_id=120970,
         case="case-14c9f947640643eea98695f56d7852b9", stage="case-f2c052b53a0443acad54fc13fea8edc1",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom pirateA art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; pirateB and other 37-joint rows remain separate assignments."]),
    dict(name="pirateB", native="pirateB", profile="ftkmf_modeltest_probe_pirateb", renderer="enPirate01", source_id=121205,
         case="case-c42b5f1381214ebebda809fa7ec32688", stage="case-273a382f661440469f7cc2f5caa9c16c",
         status="exact_binding_pass_attack_target_removed_stopped_before_fixture",
         limits=["Exact 37-joint binding, complete pass capture and native Attack capture are recorded; no finished custom pirateB art is accepted.",
                 "The native attack reduced the staged target from HP 58 to 0 before the explicit KillSingle fixture; the recorder preserves this target-removal boundary.",
                 "No ordinary nonlethal damage, fixture death, loot, Ready or finished-art acceptance is claimed; pirateA remains a separate source/controller assignment."]),
    dict(name="mageImpA", native="mageImpA", profile="ftkmf_modeltest_probe_mageimpa", renderer="impWizard01", source_id=121149,
         case="case-7a7119a8250a4ba096393dae05420518", stage="case-13cceff1ec71459e90947ddcb3c04446",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom mageImpA art is accepted.",
                 "The first two bounded native attacks observed HP 58 to 58; attempt 3 measured ordinary HP 58 to 48, followed by fixture death, one native Collect and strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other magic-controller and 37-joint rows remain separate assignments."]),
    dict(name="minionScourgeB", native="minionScourgeB", profile="ftkmf_modeltest_probe_minionscourgeb", renderer="BanditWarrior", source_id=121201,
         case="case-14469f8093d049e4bd648a820246a8a7", stage="case-230594e7bfb548ecafa343d5bb38228b",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom minionScourgeB art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; the native post-fixture loot surface exposed no usable Collect button, while strict Ready was observed.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the shared BanditWarrior renderer does not transfer another source/controller claim."]),
    dict(name="minionScourgeF", native="minionScourgeF", profile="ftkmf_modeltest_probe_minionscourgef", renderer="enMinionHangman", source_id=121321,
         case="case-83e3aafa89854fa0aa65a2c1195a6ef0", stage="case-04986f1b37d2435a8033f3c166802720",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom minionScourgeF art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; the native post-fixture loot surface exposed no usable Collect button, while strict Ready was observed.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other ghost-controller and 37-joint rows remain separate assignments."]),
    dict(name="lichA", native="lichA", profile="ftkmf_modeltest_probe_licha", renderer="enLichA", source_id=121651,
         case="case-6ea8d6f0f24c43a5bea914f07cd283d9", stage="case-9b70f26eadb54066a38ccdf713e67aef",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom lichA art is accepted.",
                 "Ordinary HP 68 to 64 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other magic-controller and 37-joint rows remain separate assignments."]),
    dict(name="harazuelBoss2", native="harazuelBoss2", profile="ftkmf_modeltest_probe_harazuelboss2", renderer="cultistFinalBoss", source_id=121187,
         case="case-ceb49a21ff324d22816e66bab297f078", stage="case-364081792ac24785806fcf36a6197be1",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass/attack captures are recorded; no finished custom harazuelBoss2 art is accepted.",
                 "Eight bounded native hero attacks all observed HP 198 to 198 under the native armor/block path; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed; HarazuelBoss3/4 and other magic-controller rows remain separate assignments."]),
    dict(name="harazuelBoss3", native="harazuelBoss3", profile="ftkmf_modeltest_probe_harazuelboss3", renderer="cultistFinalBoss", source_id=121188,
         case="case-0b591042a2a34de0a3496b176a4ad1bc", stage="case-4d2d23a84749463b925a2cb44c840254",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass/attack captures are recorded; no finished custom harazuelBoss3 art is accepted.",
                 "Eight bounded native hero attacks all observed HP 207 to 207 through the native fire/interrupt/evade path; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed; HarazuelBoss2/4 remain separate source/controller assignments."]),
    dict(name="harazuelBoss4", native="harazuelBoss4", profile="ftkmf_modeltest_probe_harazuelboss4", renderer="cultistFinalBoss", source_id=121189,
         case="case-bf2d5ce438f84850952c785c64744c3d", stage="case-13529d8011104c8f84802f361db121ba",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass/attack captures are recorded; no finished custom harazuelBoss4 art is accepted.",
                 "Eight bounded native hero attacks all observed HP 216 to 216 through the native lightning shield/resist/dodge path; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed; HarazuelBoss2/3 remain separate source/controller assignments."]),
    dict(name="harazuelMinionC", native="harazuelMinionC", profile="ftkmf_modeltest_probe_harazuelminionc", renderer="enWraith", source_id=121251,
         case="case-11d700b578c54d43b4b14bb7d90ccca7", stage="case-229d8206b74b474e8e4d1bd73a29cc89",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom harazuelMinionC art is accepted.",
                 "The first bounded native attack observed HP 77 to 77; attempt 2 measured ordinary HP 77 to 72, followed by fixture death and strict Ready, with no usable native Collect button.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the shared enWraith renderer does not transfer another source/controller claim."]),
    dict(name="goblinA", native="goblinA", profile="ftkmf_modeltest_probe_goblina", renderer="enGoblinA", source_id=121514,
         case="case-88e0869d6831469aa4e7fe8ad525e15f", stage="case-3728333c8c174403a23b8418642fc03e",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom goblinA art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other goblin and 37-joint rows remain separate assignments."]),
    dict(name="goblinB", native="goblinB", profile="ftkmf_modeltest_probe_goblinb", renderer="enGoblinArcher", source_id=121487,
         case="case-e2d3c9b5a50045e49b37bcc73fc16acb", stage="case-6d05ab3cf53b424ba5c6709a2ed21c4e",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom goblinB art is accepted.",
                 "Ordinary HP 58 to 52 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other goblin and bow-controller rows remain separate assignments."]),
    dict(name="goblinC", native="goblinC", profile="ftkmf_modeltest_probe_goblinc", renderer="enGoblinWiz", source_id=121427,
         case="case-febf7877ee0a45329e1026d4520b2797", stage="case-1a5b08b9f4bd45afb3314067170e51ce",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom goblinC art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other goblin and magic-controller rows remain separate assignments."]),
    dict(name="goblinD", native="goblinD", profile="ftkmf_modeltest_probe_goblind", renderer="enGoblinGrunt", source_id=121270,
         case="case-c7910f994d7d4bcbbfebc09c74cda4e8", stage="case-9d149b020def419e91f8ddc8e25a5899",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom goblinD art is accepted.",
                 "Ordinary HP 58 to 57 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other goblin and spear-controller rows remain separate assignments."]),
    dict(name="goblinE", native="goblinE", profile="ftkmf_modeltest_probe_gobline", renderer="enGoblinAssassin", source_id=121642,
         case="case-05c9d1e7094a43408ad4bada0facdc8d", stage="case-7a25305958e84b1eafde1a78ffd02265",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom goblinE art is accepted.",
                 "Ordinary HP 58 to 51 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other goblin and bladed-controller rows remain separate assignments."]),
    dict(name="foxFighter", native="foxFighter", profile="ftkmf_modeltest_probe_foxfighter", renderer="enFoxFighter", source_id=121590,
         case="case-06d47ded1942434f871be21f541da80a", stage="case-4594fb8029194b57ad68d7263464237a",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom foxFighter art is accepted.",
                 "Ordinary HP 58 to 54 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; foxFighterB and other 37-joint rows remain separate assignments."]),
    dict(name="foxFighterB", native="foxFighterB", profile="ftkmf_modeltest_probe_foxfighterb", renderer="enFoxFighter", source_id=121591,
         case="case-c4e667cb3cb74fb18aed83b6773af1d3", stage="case-610f3607792c45eb9b3fd71977bc6660",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom foxFighterB art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; foxFighter and other 37-joint rows remain separate assignments despite the shared mesh."]),
    dict(name="beastmanD", native="beastmanD", profile="ftkmf_modeltest_probe_beastmand", renderer="beastman02", source_id=121355,
         case="case-32731be11ab14669914aaccf60ede798", stage="case-c6b06916f0f64a3486f8b3fedce961e4",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom beastmanD art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other beastman and spear-controller rows remain separate assignments."]),
    dict(name="drownedCorpseA", native="drownedCorpseA", profile="ftkmf_modeltest_probe_drownedcorpsea", renderer="enDrownedSailor", source_id=121406,
         case="case-ff59cc66ae214fa6b99fd81f49a7579c", stage="case-a3991c3599d7407c925b90fe202b18b4",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom drownedCorpseA art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other zombie-controller and 37-joint rows remain separate assignments."]),
    dict(name="ghoulA", native="ghoulA", profile="ftkmf_modeltest_probe_ghoula", renderer="enGhoul", source_id=121430,
         case="case-2997e2e72bb74a2dad8d48dd39e6542c", stage="case-6da221b6635e4e2d88a7438a30e9f5c8",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom ghoulA art is accepted.",
                 "Ordinary HP 58 to 56 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other ghoul-controller and 37-joint rows remain separate assignments."]),
    dict(name="skellyG", native="skellyG", profile="ftkmf_modeltest_probe_skellyg", renderer="skelly01naked", source_id=120981,
         case="case-239597488db8444388f1fd28ece01804", stage="case-6c8f099a6a7b42018de739c2c8b85a88",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom skellyG art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other skelly and blunt-controller rows remain separate assignments."]),
    dict(name="snowGoblinA", native="snowGoblinA", profile="ftkmf_modeltest_probe_snowgoblina", renderer="enSnowGoblinA", source_id=121383,
         case="case-80f86c6a592d4c0fa5c333d7dff6b700", stage="case-f5ca23dcc91d400fa71e8a09609ddf8a",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom snowGoblinA art is accepted.",
                 "Ordinary HP 58 to 57 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other snow-goblin and blunt-controller rows remain separate assignments."]),
    dict(name="snowGoblinB", native="snowGoblinB", profile="ftkmf_modeltest_probe_snowgoblinb", renderer="enSnowGoblinB", source_id=121466,
         case="case-cd81953cbae6452c9238885df6ecf9eb", stage="case-86dccacf6c9e43bd89b820790c84e618",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom snowGoblinB art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; snowGoblinA/C/D and other bow-controller rows remain separate assignments."]),
    dict(name="snowGoblinD", native="snowGoblinD", profile="ftkmf_modeltest_probe_snowgoblind", renderer="enSnowGoblinD", source_id=121358,
         case="case-ef910a37f8fb453ab48b4af44cfdebf3", stage="case-2986b38fc80a4e4f8868d07f7fc6613d",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom snowGoblinD art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; snowGoblinA/B/C and other magic-controller rows remain separate assignments."]),
    dict(name="snowGoblinC", native="snowGoblinC", profile="ftkmf_modeltest_probe_snowgoblinc", renderer="enSnowGoblinC", source_id=121377,
         case="case-dc3d59a4c7e9475b8c727efa9340bda7", stage="case-0446bb3204ab48b6a5985c02f2903793",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom snowGoblinC art is accepted.",
                 "Ordinary HP 58 to 55 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; snowGoblinA/B/D and other spear-controller rows remain separate assignments."]),
    dict(name="pirateD01", native="pirateD01", profile="ftkmf_modeltest_probe_pirated01", renderer="enPirateD01", source_id=121304,
         case="case-52ee205635014ce38ac76d5ab0713c15", stage="case-84e951f0921f4434aac95cf65fb48669",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass/attack captures are recorded; no finished custom pirateD01 art is accepted.",
                 "Eight bounded native hero attacks all observed HP 90 to 90; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed; other pirate and shield-controller rows remain separate assignments."]),
    dict(name="banditE", native="banditE", profile="ftkmf_modeltest_probe_bandite", renderer="enBanditE", source_id=121650,
         case="case-0012b3fbf81e452d9539678651f9a9db", stage="case-5e4f398717f740128948888a528cae88",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditE art is accepted.",
                 "Ordinary HP 58 to 55 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other bandit and bow-controller rows remain separate assignments."]),
    dict(name="banditF", native="banditF", profile="ftkmf_modeltest_probe_banditf", renderer="enBanditF", source_id=121516,
         case="case-99c40f09a5e74d14a691d45258b5815d", stage="case-b6abcc2ada08417faab746b4d00ba02e",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditF art is accepted.",
                 "Ordinary HP 58 to 56 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other bandit and spear-controller rows remain separate assignments."]),
    dict(name="vampireA", native="vampireA", profile="ftkmf_modeltest_probe_vampirea", renderer="enVampireB", source_id=121399,
         case="case-50d9af0cbb8b45d5bcfc9952029e4df2", stage="case-a09cce92bf4e4363ae652913da69d433",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom vampireA art is accepted.",
                 "The first bounded native attack observed HP 72 to 72; attempt 2 measured ordinary HP 72 to 62, followed by fixture death, two native Collect clicks and strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the earlier no-loss outcome is preserved and no combat cause is inferred."]),
    dict(name="snowBeastA", native="snowBeastA", profile="ftkmf_modeltest_probe_snowbeasta", renderer="en_SnowBeastA", source_id=121318,
         case="case-7389b8b4663c4deeb0d605c77c08e043", stage="case-10c842db4ba349e7b9d4cdbee76afac8",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom snowBeastA art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other snow-beast and spear-controller rows remain separate assignments."]),
    dict(name="snowBeastB", native="snowBeastB", profile="ftkmf_modeltest_probe_snowbeastb", renderer="enSnowBeastManB", source_id=121507,
         case="case-52810a8f6eaf475389c3e5dcca1ef5b7", stage="case-9cc532dc4ecd4abc909a57d15a376bee",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom snowBeastB art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other snow-beast and spear-controller rows remain separate assignments."]),
    dict(name="snowBeastC", native="snowBeastC", profile="ftkmf_modeltest_probe_snowbeastc", renderer="enSnowBeastManC", source_id=121657,
         case="case-c17e0b6597fc4e1a90752ee197f5222e", stage="case-c7c04117c19e44a4861a89a871126d34",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom snowBeastC art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other snow-beast and blunt-controller rows remain separate assignments."]),
    dict(name="hobgoblinA", native="hobgoblinA", profile="ftkmf_modeltest_probe_hobgoblina", renderer="enHobGoblinA", source_id=121422,
         case="case-52b44a574ea641c5846a556de894b87f", stage="case-74c0f3edcfc94d9180eb67fd47806023",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom hobgoblinA art is accepted.",
                 "Ordinary HP 58 to 56 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; hobgoblinB and other blunt-controller rows remain separate assignments."]),
    dict(name="hobgoblinB", native="hobgoblinB", profile="ftkmf_modeltest_probe_hobgoblinb", renderer="enHobGoblinB", source_id=121565,
         case="case-c084d0adab314c16a2b0341cbcf06f91", stage="case-514d0545a5e24765a231f248f5fd432a",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom hobgoblinB art is accepted.",
                 "The first three bounded native attacks observed HP 58 to 58; attempt 4 measured ordinary HP 58 to 56, followed by fixture death, one native Collect click and strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the earlier no-loss outcomes are preserved and no combat cause is inferred."]),
    dict(name="pirateD", native="pirateD", profile="ftkmf_modeltest_probe_pirated", renderer="enDrunkPirateD", source_id=121273,
         case="case-85bbb1872a6e4b118f525ef9573d348f", stage="case-73a8778b09dd4baa9cbc7285e477e2df",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom pirateD art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; pirate variants and other blunt-controller rows remain separate assignments."]),
    dict(name="pirateE", native="pirateE", profile="ftkmf_modeltest_probe_piratee", renderer="enDrunkPirateE", source_id=121683,
         case="case-c68dbb3e03ae4ed682aa5cd6e4c5e426", stage="case-1083b393d17f4924b34243ababe6ae0c",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom pirateE art is accepted.",
                 "Ordinary HP 58 to 45 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; pirate variants and other bladed-controller rows remain separate assignments."]),
    dict(name="snowGoblinE", native="snowGoblinE", profile="ftkmf_modeltest_probe_snowgobline", renderer="enSnowGoblinE", source_id=121360,
         case="case-36a9cb00f8514c4487344dd5a5dba660", stage="case-cd770a6d7ac04cca94d49965d7da6dfd",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom snowGoblinE art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other snow-goblin and bladed-controller rows remain separate assignments."]),
    dict(name="banditG", native="banditG", profile="ftkmf_modeltest_probe_banditg", renderer="enFrozenBanditA", source_id=121412,
         case="case-2ddc53851edb4bdd8ad98a4df5c714e2", stage="case-eeb3a0f7059a47a2adf9deb1a314755a",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditG art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; frozen bandit variants and other bow-controller rows remain separate assignments."]),
    dict(name="banditH", native="banditH", profile="ftkmf_modeltest_probe_bandith", renderer="enFrozenBanditB", source_id=121589,
         case="case-e706a263061a437199305d773218ee84", stage="case-6ce5917978af4e3daafb3446a3864c42",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditH art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; frozen bandit variants and other blunt-controller rows remain separate assignments."]),
    dict(name="koboldC", native="koboldC", profile="ftkmf_modeltest_probe_koboldc", renderer="enKoboldB", source_id=121675,
         case="case-21769844dd304c87be245d2fb3ada793", stage="case-e137c858b4774842a546e99d15def7b8",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom koboldC art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other kobold and bow-controller rows remain separate assignments."]),
    dict(name="boomerA", native="boomerA", profile="ftkmf_modeltest_probe_boomera", renderer="enPirtaeSkellyBoner", source_id=121655,
         case="case-3d7e9b9fa95e4804bf80490476c78695", stage="case-f7c76dda88bd4bc8abc4972373e3f6d1",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom boomerA art is accepted.",
                 "Ordinary HP 135 to 133 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the boomer-controller assignment remains separate from all weapon-controller rows."]),
    dict(name="pirateC", native="pirateC", profile="ftkmf_modeltest_probe_piratec", renderer="enPirateC02", source_id=121608,
         case="case-d272d3cf0f9e49f3989d9968adcd092a", stage="case-d173113839ea49df99ca44abebcd9476",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom pirateC art is accepted.",
                 "Ordinary HP 58 to 52 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other pirate and musket-controller rows remain separate assignments."]),
    dict(name="pirateC02", native="pirateC02", profile="ftkmf_modeltest_probe_piratec02", renderer="enPirateC02", source_id=121608,
         case="case-a2dc3e24554e4908b18890cf59c3bc8f", stage="case-b61920cae2c74b2ea86d6dbbb0313561",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom pirateC02 art is accepted.",
                 "Ordinary HP 59 to 53 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the shared pirateC renderer remains a separate cannon-controller assignment."]),
    dict(name="pirateA03", native="pirateA03", profile="ftkmf_modeltest_probe_piratea03", renderer="enDrunkPirateE", source_id=121683,
         case="case-d020d2ed84ff48ea9afc0207d4607b28", stage="case-0701106d0d6d4a22a35cc643b752c8fb",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom pirateA03 art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the shared pirateE renderer remains a separate blunderbuss-controller assignment."]),
    dict(name="pirateB03", native="pirateB03", profile="ftkmf_modeltest_probe_pirateb03", renderer="enPirate02", source_id=120971,
         case="case-a4678a3827d54fb182524921e8ccac30", stage="case-c1cc6255a39f469db457f94e4cad02e7",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom pirateB03 art is accepted.",
                 "Ordinary HP 58 to 52 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; other pirate and musket-controller rows remain separate assignments."]),
    dict(name="pirateC03", native="pirateC03", profile="ftkmf_modeltest_probe_piratec03", renderer="enPirateC03", source_id=121385,
         case="case-c06da5a570df47e180449b6fd5bef0a3", stage="case-caaeb1dfc3dd4362814283d68aab10bd",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom pirateC03 art is accepted.",
                 "Ordinary HP 72 to 62 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the dual-weapon controller assignment remains separate from other pirate rows."]),
    dict(name="golemA", native="golemA", profile="ftkmf_modeltest_probe_golema", renderer="enAtztecGolem_Fire", source_id=121493,
         case="case-183472f6df51447889b4c4055d1b636a", stage="case-7df9f1d67b03479ea51a9fe5edfe12cd",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom golemA art is accepted.",
                 "Ordinary HP 58 to 54 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "Native fire emission obscures detailed selected-frame probe geometry; this establishes exact binding and gameplay sequence only, not visual art acceptance or ordinary lethal damage."]),
    dict(name="golemB", native="golemB", profile="ftkmf_modeltest_probe_golemb", renderer="enAtztecGolem_Frost", source_id=121332,
         case="case-a0304b88ba55404cb62bb4b9aec6c041", stage="case-144dbfbeb1154e228f7e578f9f124036",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom golemB art is accepted.",
                 "Ordinary HP 72 to 71 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the frosted unarmed-controller assignment remains separate from golemA."]),
    dict(name="banditDesertA", native="banditDesertA", profile="ftkmf_modeltest_probe_banditdeserta", renderer="enDesertBanditA", source_id=121374,
         case="case-afd88881d0244106b8f07860045a468b", stage="case-ad24e766191f40438c51f0d96d0e402c",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditDesertA art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; desert bandit and other dual-weapon rows remain separate assignments."]),
    dict(name="banditDesertC", native="banditDesertC", profile="ftkmf_modeltest_probe_banditdesertc", renderer="enDesertBanditC", source_id=121418,
         case="case-b9e95f3d6d774e288cbe0b1a7daf8e97", stage="case-30573edfd1ab436a8221bd04b550c293",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom banditDesertC art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; desert bandit and other bow-controller rows remain separate assignments."]),
    dict(name="koboldJungleA", native="koboldJungleA", profile="ftkmf_modeltest_probe_koboldjunglea", renderer="enJungleKoboldA", source_id=121473,
         case="case-063542e3baed4210a34a3997bcf3f9a5", stage="case-909b13a5388d46b2b33e515615f906d1",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom koboldJungleA art is accepted.",
                 "Ordinary HP 58 to 54 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; jungle kobold and other dual-weapon rows remain separate assignments."]),
    dict(name="koboldJungleB", native="koboldJungleB", profile="ftkmf_modeltest_probe_koboldjungleb", renderer="enJungleKoboltB", source_id=121672,
         case="case-f4ed8654780e4676bcf017800f05a0e4", stage="case-564853463a264815868474671bc14e2f",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom koboldJungleB art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; jungle kobold and other bow-controller rows remain separate assignments."]),
    dict(name="koboldJungleC", native="koboldJungleC", profile="ftkmf_modeltest_probe_koboldjunglec", renderer="enJungleKoboldC", source_id=121313,
         case="case-a8d01d93a0524be2815ef0125c6f366f", stage="case-f233b52dfa8b4b20bf373967d91d80c4",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom koboldJungleC art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; jungle kobold and other wand-controller rows remain separate assignments."]),
    dict(name="warriorA", native="warriorA", profile="ftkmf_modeltest_probe_warriora", renderer="enJungleWarriorA", source_id=121402,
         case="case-6356f8302c7049f1bb2c436318230efb", stage="case-39bb225d03ca40308c004e2bab5f4774",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorA art is accepted.",
                 "Ordinary HP 58 to 52 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other dual-weapon rows remain separate assignments."]),
    dict(name="warriorB", native="warriorB", profile="ftkmf_modeltest_probe_warriorb", renderer="enAncientWarriorB", source_id=121353,
         case="case-326ef059f29d4ea194badc2765095b01", stage="case-dceb802f767b4f1c9278dfa335ecac85",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorB art is accepted.",
                 "Ordinary HP 58 to 53 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other bow-controller rows remain separate assignments."]),
    dict(name="warriorC", native="warriorC", profile="ftkmf_modeltest_probe_warriorc", renderer="enJungleDruid_B", source_id=121337,
         case="case-b898f6a9d2964fc4893a33c9e3d32cbc", stage="case-85f28995c34147b98547af341f32f118",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorC art is accepted.",
                 "Ordinary HP 58 to 52 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other wand-controller rows remain separate assignments."]),
    dict(name="skellyJungleA", native="skellyJungleA", profile="ftkmf_modeltest_probe_skellyjunglea", renderer="enJungleSkelly", source_id=121382,
         case="case-7120fb57c5b84e7ca24dd5bb96c2df06", stage="case-54dcf61b2759477195f000c0b48ffc0d",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom skellyJungleA art is accepted.",
                 "Ordinary HP 58 to 57 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; jungle skelly and other dual-weapon rows remain separate assignments."]),
    dict(name="zombieJungleA", native="zombieJungleA", profile="ftkmf_modeltest_probe_zombiejunglea", renderer="enJungleCorpseA", source_id=121325,
         case="case-dd95919b2de042d9b9fbe08d13a17a38", stage="case-7fc976398d59436e9926529c70db9a5e",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom zombieJungleA art is accepted.",
                 "Ordinary HP 59 to 58 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; zombie and other corpse-controller rows remain separate assignments."]),
    dict(name="trollJungleA", native="trollJungleA", profile="ftkmf_modeltest_probe_trolljunglea", renderer="enJungleTroll", source_id=121520,
         case="case-4c8c1ee5fdab43cd99300e5fd07752a9", stage="case-32e5952e208f40ae88f7ddc1a1821ed6",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom trollJungleA art is accepted.",
                 "Ordinary HP 162 to 152 was measured on the accepted native attack; two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the troll row remains a separate exact assignment."]),
    dict(name="zombieJungleB", native="zombieJungleB", profile="ftkmf_modeltest_probe_zombiejungleb", renderer="enJungleCorpseB", source_id=121352,
         case="case-cbc7eeb79bb04e29b2a99f303603de98", stage="case-27cdbd6f6a774dff9a8ebf5b2482c16b",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom zombieJungleB art is accepted.",
                 "Ordinary HP 81 to 71 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; ghoul-controller and other shared corpse rows remain separate assignments."]),
    dict(name="minionScourgeJ2", native="minionScourgeJ2", profile="ftkmf_modeltest_probe_minionscourgej2", renderer="enJungleCorpseB", source_id=121352,
         case="case-502d9ba106874edfa12e813b154beba6", stage="case-3dc9480328a14428aed56f00f8f74473",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom minionScourgeJ2 art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; no native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; the shared renderer remains separate from its ghoul-controller row."]),
    dict(name="warriorD", native="warriorD", profile="ftkmf_modeltest_probe_warriord", renderer="enAncientWarriorA", source_id=121417,
         case="case-4ebf48c58a514deba8d3a8e8dbd99603", stage="case-d0a7166057524b05ad3859dc8e408f5a",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorD art is accepted.",
                 "Three bounded native attack attempts remained at HP 58 to 58; attempt 4 measured ordinary HP 58 to 56, then two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other one-handed blunt-controller rows remain separate assignments."]),
    dict(name="warriorE", native="warriorE", profile="ftkmf_modeltest_probe_warriore", renderer="enJungleWarriorB_B", source_id=121689,
         case="case-28dbc0b9a8ba4dd0ae77f69f62c428cb", stage="case-39f6bdea97554c2ca76a972cc0767f9c",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorE art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other dual-weapon-controller rows remain separate assignments."]),
    dict(name="warriorF", native="warriorF", profile="ftkmf_modeltest_probe_warriorf", renderer="enAncientDruid_B", source_id=121429,
         case="case-c571e519dd18425b8e2a82c106ea3a10", stage="case-e1edcd3402c5411d857150e4704e49fb",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorF art is accepted.",
                 "The first two bounded native attacks remained at HP 58 to 58; attempt 3 measured ordinary HP 58 to 50, then one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other wand-controller rows remain separate assignments."]),
    dict(name="warriorB02", native="warriorB02", profile="ftkmf_modeltest_probe_warriorb02", renderer="enAncientWarriorB_B", source_id=121576,
         case="case-239a780bbee54af39aa56755bcf6d53d", stage="case-753ca1d1b4694d4c888a915992eae317",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorB02 art is accepted.",
                 "The first bounded native attack remained at HP 58 to 58; attempt 2 measured ordinary HP 58 to 53, then two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other bow-controller rows remain separate assignments."]),
    dict(name="warriorC02", native="warriorC02", profile="ftkmf_modeltest_probe_warriorc02", renderer="enJungleDruid", source_id=121498,
         case="case-57120750e2c747829882f366445d34f4", stage="case-6f76c939fe72475a8f8e494ac8e52148",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorC02 art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other wand-controller rows remain separate assignments."]),
    dict(name="warriorD02", native="warriorD02", profile="ftkmf_modeltest_probe_warriord02", renderer="enAncientWarriorA_B", source_id=121285,
         case="case-e3d1b63dea06447a872a88e9bef4d11a", stage="case-33bbc1832b8c46b981888be41fde5eb7",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass/attack captures are recorded; no finished custom warriorD02 art is accepted.",
                 "Eight bounded native hero attacks all observed HP 68 to 68; the recorder preserves no_hp_loss_unclassified without inferring block, dodge, immunity, animation or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed; warrior and other one-handed blunt-controller rows remain separate assignments."]),
    dict(name="warriorD02Focus", native="warriorD02", profile="ftkmf_modeltest_probe_warriord02", renderer="enAncientWarriorA_B", source_id=121285,
         case="case-fc76d7f5d43741c6866619f98bf134e9", stage="case-ffb073a5723941b6a1ca19de173a8bae",
         status="exact_binding_pass_focused_attack_stopped_no_hp_loss",
         limits=["Exact 37-joint binding and complete pass/focused-attack captures are recorded; no finished custom warriorD02 art is accepted.",
                 "After the separately archived eight no-focus 68 to 68 attempts, a fresh strongest legitimate native Attack(focus) also observed HP 68 to 68; no combat cause is inferred.",
                 "No ordinary no-focus or focused damaging/lethal result, KillSingle fixture, loot, Ready or finished-art acceptance is claimed; this supplement does not replace the no-focus boundary."]),
    dict(name="warriorE02", native="warriorE02", profile="ftkmf_modeltest_probe_warriore02", renderer="enJungleWarriorB", source_id=121549,
         case="case-730234d77fe347fca6662292cf65ae13", stage="case-e0b0a6b3a4ae4d9b8569c10b9ed0d9e2",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorE02 art is accepted.",
                 "Ordinary HP 63 to 50 was measured on the accepted native attack; one native Collect click was accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other dual-weapon-controller rows remain separate assignments."]),
    dict(name="warriorF02", native="warriorF02", profile="ftkmf_modeltest_probe_warriorf02", renderer="enAncientDruid", source_id=121423,
         case="case-cb658098079c4ad886a2df4b0cd5154c", stage="case-38454b21cad24f87903ffb0c00a62ad9",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom warriorF02 art is accepted.",
                 "The first four bounded native attacks remained at HP 63 to 63; attempt 5 measured ordinary HP 63 to 59, then two native Collect clicks were accepted before strict Ready.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; warrior and other wand-controller rows remain separate assignments."]),
    dict(name="aztecBoss", native="aztecBoss", profile="ftkmf_modeltest_probe_aztecboss", renderer="enJungleElder", source_id=121503,
         case="case-fa6217f770b44121a56faf2efa0b94a8", stage="case-5e262f3c4e8147efac46429687dda2b2",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom aztecBoss art is accepted.",
                 "The first bounded native attack remained at HP 315 to 315; attempt 2 measured ordinary HP 315 to 312, then strict Ready was observed with no native Collect click.",
                 "The serialized renderer animator is player_1H_Wand_Combat while the weapon-controller candidate and initialized runtime animator are player_2H_Magic_Combat; both facts remain part of this exact assignment.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage; native effects limit fine visual-art assessment."]),
    dict(name="fishB02", native="fishB02", profile="ftkmf_modeltest_probe_fishb02", renderer="enFishD", source_id=121620,
         case="case-ec4478d4f5cb420798e9ae64514a881a", stage="case-37d6a3e5dd23413c8f480cce7db70f06",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom fishB02 art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other fish rows retain their own exact renderer/controller evidence."]),
    dict(name="fishB03", native="fishB03", profile="ftkmf_modeltest_probe_fishb03", renderer="enFishE", source_id=121686,
         case="case-bf2c2c4505b04f728505539f18b3311e", stage="case-fd5d218edb304202b47015a6c759f124",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom fishB03 art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other fish rows retain their own exact renderer/controller evidence."]),
    dict(name="fishC01", native="fishC01", profile="ftkmf_modeltest_probe_fishc01", renderer="enFishF", source_id=121649,
         case="case-aac3cb2a77a94820bca8e18834157589", stage="case-a10019133111490e9f7a2734af461a9d",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom fishC01 art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other fish rows retain their own exact renderer/controller evidence."]),
    dict(name="fishC02", native="fishC02", profile="ftkmf_modeltest_probe_fishc02", renderer="enFishG", source_id=121628,
         case="case-0f5b0e711de84370b13d0c2fbcca4ad9", stage="case-e5ba96b1f35e4cbd82321b07384f8757",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom fishC02 art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "A fresh accepted run measured HP 58 to 50 after earlier protection-boundary attempts; no combat cause is inferred."]),
    dict(name="fishD01", native="fishD01", profile="ftkmf_modeltest_probe_fishd01", renderer="enFishD01", source_id=121543,
         case="case-417cda13de0c4ee79e8dd56e3ec44a11", stage="case-8d3a390af04a419693eec7623f2d1ff1",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom fishD01 art is accepted.",
                 "Bounded repeated native hero attacks were allowed after no_hp_loss_unclassified block outcomes; acceptance still requires measured nonlethal HP loss (72 to 71 on attempt 6).",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="fishD02", native="fishD02", profile="ftkmf_modeltest_probe_fishd02", renderer="enFishD02", source_id=121479,
         case="case-935017bd16cf4d23bc93f55b72b2e9a4", stage="case-37eff44dd1ca44ae9f127455a7a0ab0c",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom fishD02 art is accepted.",
                 "A bounded second native hero attack was required after a no_hp_loss_unclassified outcome; acceptance requires measured nonlethal HP loss (68 to 56 on attempt 2).",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="fishB01", native="fishB01", profile="ftkmf_modeltest_probe_fishb01", renderer="enFishH", source_id=121298,
         case="case-40965cee3fd34ce69ff901be0d4f34a9", stage="case-b57be509197547298faa11d39e66d477",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom fishB01 art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other fish rows retain their own exact renderer/controller evidence."]),
    dict(name="fishA02", native="fishA02", profile="ftkmf_modeltest_probe_fisha02", renderer="enFishB", source_id=121286,
         case="case-0dc698450c0c47edbe25c287be211cb9", stage="case-089bb87f26a64512bad72c5216e423f6",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom fishA02 art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other fish rows retain their own exact renderer/controller evidence."]),
    dict(name="fishA03", native="fishA03", profile="ftkmf_modeltest_probe_fisha03", renderer="enFishC", source_id=121594,
         case="case-a6a0398304d24251978e37ef5f1142a5", stage="case-8dcc955b7edc4df7ab024cc292ddd945",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom fishA03 art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other fish rows retain their own exact renderer/controller evidence."]),
    dict(name="mimicClamA", native="mimicClamA", profile="ftkmf_modeltest_probe_mimicclama", renderer="enClam", source_id=121306,
         case="case-775dd3e257f2425a95e509b7c31800fc", stage="case-e0a71382aeec49a1b261cb9db4e228aa",
         status="exact_binding_pass_attack_kill_fixture_loot_stopped_no_progress",
         limits=["Exact seven-joint binding, pass, ordinary Attack 58 to 48, and explicit KillSingle fixture are complete.",
                 "One native Collect click was accepted, but no item/button or reward progress changed and strict Ready never appeared.",
                 "The once-only runner stopped without a second Collect or forced Ready; no finished custom mimicClamA art is accepted."]),
    dict(name="batJungleA", native="batJungleA", profile="ftkmf_modeltest_probe_batjunglea", renderer="enJungleBat", source_id=121437,
         case="case-cb06f2dfb0bb482e80576f5a55990591", stage="case-1a68b074b57f4daba939f197bd9fb93d",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom batJungleA art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other bat rows retain their own exact renderer/controller evidence."]),
    dict(name="bearA", native="bearA", profile="ftkmf_modeltest_probe_beara", renderer="enBear02", source_id=121534,
         case="case-11f994152bdf4f1e989c1d59f235da55", stage="case-08c56f95390c44618aba2686cde8e2db",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom bearA art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other bear rows retain their own exact renderer/controller evidence."]),
    dict(name="bearC", native="bearC", profile="ftkmf_modeltest_probe_bearc", renderer="enBear03", source_id=121486,
         case="case-9ef6cc6006814efdb5e99a01e76f3b31", stage="case-ca95f268a22445caad89fec47f482f67",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom bearC art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "Other bear rows retain their own exact renderer/controller evidence."]),
    dict(name="wispA", native="wispA", profile="ftkmf_modeltest_probe_wispa", renderer="enWisp", source_id=121121,
         case="case-220f77791f244ffc919a0a2b0d51a28e", stage="case-bbba279a497f401291fa35f02585748c",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact two-joint binding and the complete pass capture are recorded; no finished custom wispA art is accepted.",
                 "Eight bounded native hero attacks all observed the same target at HP 58 to 58; the recorder preserves the no_hp_loss_unclassified boundary without inferring block, dodge, immunity, animation, or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready, or finished-art acceptance is claimed."]),
    dict(name="crabA", native="crabA", profile="ftkmf_modeltest_probe_craba", renderer="CrabGeo", source_id=121579,
         case="case-447ad96197924db8a5a6837d9cc7ba2c", stage="case-0d75ade6efab409c8bac942cd1e0e025",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom crabA art is accepted.",
                 "The first two bounded native hero attacks observed HP 58 to 58; attempt 3 measured nonlethal HP loss to 53 and was required before fixture death and Ready acceptance.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="cockatriceA", native="cockatriceA", profile="ftkmf_modeltest_probe_cockatricea", renderer="enChicken", source_id=121328,
         case="case-0c567fb6c6484070bf9c1531ccd6c5db", stage="case-9a2378c2dd8e4fd7beba27a2feb377ed",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom cockatriceA art is accepted.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage.",
                 "The bossCockatrice/enCockatrice row retains separate exact renderer/controller evidence."]),
    dict(name="bossCockatrice", native="bossCockatrice", profile="ftkmf_modeltest_probe_bosscockatrice", renderer="enCockatrice", source_id=121554,
         case="case-b1be5bcbeb8f481d936e8871826903e8", stage="case-ca916e9d5c7946478efef261d4b725a0",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 50-joint binding and the complete pass capture are recorded; no finished custom bossCockatrice art is accepted.",
                 "Eight bounded native hero attacks all observed the same target at HP 540 to 540; the recorder preserves the no_hp_loss_unclassified boundary without inferring block, dodge, immunity, animation, or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready, or finished-art acceptance is claimed."]),
    dict(name="dragonflyA", native="dragonflyA", profile="ftkmf_modeltest_probe_dragonflya", renderer="enSwampFly01", source_id=121101,
         case="case-69ce015ea28946a1a0f976977043f54d", stage="case-5dba0d9bbc024f7a81dd2f1853021730",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom dragonflyA art is accepted.",
                 "The first staged run was allowed to settle before the untouched exercise; ordinary HP 55 to 45 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="mosquitoA", native="mosquitoA", profile="ftkmf_modeltest_probe_mosquitoa", renderer="enMosquito_01", source_id=121569,
         case="case-17022e380c5f4c4a8ebfcf4b2ea6d664", stage="case-abc8fef736d84f0ebe34766b41e14a93",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom mosquitoA art is accepted.",
                 "Ordinary HP 58 to 45 was measured on the accepted native attack; the native enemy has additional disease-only proficiencies whose effects are not inferred from the model result.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="scarabA", native="scarabA", profile="ftkmf_modeltest_probe_scaraba", renderer="enScarabB", source_id=121518,
         case="case-cb1159391854494393409df66b7938aa", stage="case-cbb2d9da5fb94219afee4497e281ff9",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom scarabA art is accepted.",
                 "Ordinary HP 58 to 52 was measured on the accepted native attack; native enDiseaseOnly and SteadFast outcomes remain controller observations without inferred causes.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="scarabB", native="scarabB", profile="ftkmf_modeltest_probe_scarabb", renderer="enScarabA", source_id=121389,
         case="case-d64c7b2add1142a78ca64f9705a95629", stage="case-8aa5730e6972403dbbb666e957125798",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom scarabB art is accepted.",
                 "The staged native Enemy room required one explicit native encounter trigger after the stair pointer advanced; ordinary HP 58 to 54 was then measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="mosquitoB", native="mosquitoB", profile="ftkmf_modeltest_probe_mosquitob", renderer="enMosquito_02", source_id=121481,
         case="case-5a67fda7bcff4ec7a0e1ca01ab25fa1f", stage="case-518316a194704c2e8ec4e4bf160a3ad9",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom mosquitoB art is accepted.",
                 "The first bounded native attack observed HP 58 to 58; attempt 2 measured nonlethal HP loss to 47 and was required before fixture death and Ready acceptance.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="seagullA", native="seagullA", profile="ftkmf_modeltest_probe_seagulla", renderer="enSeagull", source_id=120999,
         case="case-9fe16c923b734b6c9ec483809f59bb67", stage="case-0f9cf37079694696add3581191403ae3",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom seagullA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="seagullB", native="seagullB", profile="ftkmf_modeltest_probe_seagullb", renderer="enSeagull2", source_id=121346,
         case="case-7c3b8b138d8545fbb1cfd5c5f0e68da2", stage="case-75a2f482b3e94051a50514ff2cf71bc1",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom seagullB art is accepted.",
                 "The first bounded native attack observed HP 58 to 58; attempt 2 measured nonlethal HP loss to 50 and was required before fixture death and Ready acceptance.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="parrotA", native="parrotA", profile="ftkmf_modeltest_probe_parrota", renderer="enParrotA", source_id=121369,
         case="case-9645736cf2814108909633575244ded8", stage="case-35dc3dc1577947bf9fd76f5cd4908b87",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom parrotA art is accepted.",
                 "Ordinary HP 58 to 45 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="birdJungleA", native="birdJungleA", profile="ftkmf_modeltest_probe_birdjunglea", renderer="enJungleBird_A", source_id=121339,
         case="case-a43e9102e9aa44f1813a3ba1bb94abf1", stage="case-b24c11e7f7a548c8a5654eb2def5a20f",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom birdJungleA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="birdJungleB", native="birdJungleB", profile="ftkmf_modeltest_probe_birdjungleb", renderer="enJungleBird_B", source_id=121670,
         case="case-f143f055ec1547a98b12fea877033b74", stage="case-41c94a65385046d9bad6c5026e42e53e",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom birdJungleB art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="spiderA", native="spiderA", profile="ftkmf_modeltest_probe_spidera", renderer="enSpiderA", source_id=121453,
         case="case-a2f395be4d54497e94e0b89260e16942", stage="case-4259ae0568454b238a1d82e541884ff3",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom spiderA art is accepted.",
                 "Ordinary HP 58 to 50 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="spiderJungleA", native="spiderJungleA", profile="ftkmf_modeltest_probe_spiderjunglea", renderer="enJungleSpider_A", source_id=121580,
         case="case-795ea793b30a4624a9600fe862dcec43", stage="case-06dd32a7505c4e0c8c252efdcbf6b415",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom spiderJungleA art is accepted.",
                 "Ordinary HP 58 to 48 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="chaosHound", native="chaosHound", profile="ftkmf_modeltest_probe_chaoshound", renderer="chaosWolf", source_id=121191,
         case="case-99f2f131ca114c8b98e7e72cfe8c67ca", stage="case-ca25165526224c519003cd7226e1e31e",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 33-joint binding and the complete pass capture are recorded; no finished custom chaosHound art is accepted.",
                 "Eight bounded native hero attacks all observed the same target at HP 180 to 180; the recorder preserves the no_hp_loss_unclassified boundary without inferring block, dodge, immunity, animation, or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready, or finished-art acceptance is claimed."]),
    dict(name="hellhoundA", native="hellhoundA", profile="ftkmf_modeltest_probe_hellhounda", renderer="enHellHoundB", source_id=121303,
         case="case-b78f71eba3784703889b000387b55e87", stage="case-ad2f0d6d736443f187669f86f132d30a",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom hellhoundA art is accepted.",
                 "Ordinary HP 81 to 71 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="pantherA", native="pantherA", profile="ftkmf_modeltest_probe_panthera", renderer="enPanther", source_id=121319,
         case="case-4724da5d37904b16b0398561375e719d", stage="case-0dd3a4c5af454687bcd94800e61bf6e4",
         status="exact_binding_complete_pass_attack_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom pantherA art is accepted.",
                 "Ordinary HP 99 to 89 was measured on the accepted native attack.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="jaguarA", native="jaguarA", profile="ftkmf_modeltest_probe_jaguara", renderer="enJaguar", source_id=121571,
         case="case-6faa891c21964444a6fbc3d4cf153817", stage="case-9a21c57a11ed495daed1ce1a728196b9",
         status="exact_binding_complete_pass_bounded_attack_retry_kill_fixture_ready_observed",
         limits=["Calibration probe only; no finished custom jaguarA art is accepted.",
                 "The first bounded native attack observed HP 72 to 72; attempt 2 measured nonlethal HP loss to 62 and was required before fixture death and Ready acceptance.",
                 "KillSingle is an explicit fixture, not ordinary lethal damage."]),
    dict(name="deathknightEboss", native="deathknightEboss", profile="ftkmf_modeltest_probe_deathknighteboss", renderer="deathKnight", source_id=121220,
         case="case-189a65c7f40c4c8d835767e302fe139b", stage="case-b372f8a227544c54bc85a7893e3235f4",
         status="exact_binding_pass_bounded_attack_retry_stopped_no_hp_loss",
         limits=["Exact 36-joint binding and the complete pass capture are recorded; no finished custom deathknightEboss art is accepted.",
                 "Eight bounded native hero attacks all observed the same target at HP 158 to 158; the recorder preserves the no_hp_loss_unclassified boundary without inferring block, dodge, immunity, animation, or damage-source cause.",
                 "No ordinary damaging/lethal result, KillSingle fixture, loot, Ready, or finished-art acceptance is claimed. The deathknightA original-art evidence does not transfer to this distinct Eboss controller row."]),
    dict(name="monkeyA", native="monkeyA", profile="ftkmf_modeltest_probe_monkeya", renderer="enMonkeyA", source_id=121679,
         case="case-7a8614eeb48043d985f5a330f0c0f5b0", stage="case-8ba0b0f59ee3470ebb8998b0a93de37c",
         status="exact_binding_pass_stopped_native_self_termination_before_attack",
         limits=["Exact 42-joint binding and the complete pass capture are recorded; no finished custom monkeyA art is accepted.",
                 "The native Prof0 enSuicideBlast executes before the hero attack and applies secondary damage 58, leaving the target no longer alive; this is preserved as the observed native boundary.",
                 "No ordinary hero attack, nonlethal HP-loss, KillSingle fixture, loot, Ready, or finished-art acceptance is claimed."]),
    dict(name="monkeyD", native="monkeyD", profile="ftkmf_modeltest_probe_monkeyd", renderer="enMonkeyDiseased", source_id=121441,
         kind="arrival", arrival_case="case-c7266c731d524ed28a7086d68001ed65",
         status="exact_binding_passive_arrival_native_self_termination_observed",
         limits=["Exact 42-joint binding, live plural renderer/lease membership and a complete 120-frame passive native-arrival capture are recorded; no finished custom monkeyD art is accepted.",
                 "The native Prof0 enSuicideDisease attacks before the hero turn and then applies secondary damage 58, taking the target from 58 HP to 0; the capture preserves this controller boundary without substituting a hero action.",
                 "No ordinary hero attack, nonlethal HP-loss, KillSingle fixture, loot or Ready acceptance is claimed; arrival timing is realtime observation and does not establish full motion or visual acceptance."]),
]


def sha(path):
    return hashlib.sha256(Path(path).read_bytes()).hexdigest()


def read(path):
    return json.loads(Path(path).read_text())


def write(path, value):
    Path(path).parent.mkdir(parents=True, exist_ok=True)
    Path(path).write_text(json.dumps(value, indent=2, allow_nan=False) + "\n")


def rel(path):
    return str(Path(path).resolve().relative_to(ROOT))


def gz(source, destination):
    raw = Path(source).read_bytes()
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    return dict(source=rel(source), sourceSha256=hashlib.sha256(raw).hexdigest(),
                archive=str(destination.relative_to(OUT)), archiveSha256=sha(destination),
                encoding="gzip-lossless")


def png(source, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    return dict(source=rel(source), archive=str(destination.relative_to(OUT)), sha256=sha(destination))


def probe_video(destination, count):
    info = json.loads(subprocess.check_output(
        ["ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
         "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(destination)],
        stderr=subprocess.DEVNULL))["streams"][0]
    assert int(info["nb_read_frames"]) == count
    return info


def video(frame_dir, count, destination):
    destination.parent.mkdir(parents=True, exist_ok=True)
    # Each raw capture uses a unique immutable case directory. Rebuilds may
    # therefore reuse a prior derivative after independently verifying its
    # frame count. Set FTK_ARCHIVE_REBUILD_MEDIA=1 to regenerate all videos.
    try:
        info = None if os.environ.get("FTK_ARCHIVE_REBUILD_MEDIA") == "1" else probe_video(destination, count)
    except (AssertionError, subprocess.CalledProcessError, IndexError, KeyError, ValueError):
        info = None
    if info is None:
        subprocess.run(["ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
                        "-i", str(Path(frame_dir) / "%04d.png"), "-frames:v", str(count),
                        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(destination)], check=True)
        info = probe_video(destination, count)
    return dict(path=str(destination.relative_to(OUT)), sha256=sha(destination), frames=count,
                width=int(info["width"]), height=int(info["height"]), playbackFps=12,
                timing="presentation derivative; raw capture preserves measured timing")


def archive_action(entry, folder, files, selected):
    capture = entry.get("capture") or {}
    raw = Path((capture.get("rawCapture") or {}).get("path", ""))
    if not raw.is_file():
        return None
    doc = read(raw)
    frames = doc.get("frames") or []
    frame_dir = raw.with_suffix("")
    attempt = entry.get("attempt")
    label = entry["action"] if attempt is None else f"{entry['action']}-{int(attempt):02d}"
    files.append(gz(raw, folder / ("kill-fixture.json.gz" if entry["action"] == "kill-fixture" else label + ".json.gz")))
    for index in capture.get("selectedIndices", []):
        image = frame_dir / f"{index:04d}.png"
        if image.is_file():
            selected.append(png(image, folder / "selected" / f"{label}-{index:04d}.png"))
    if frames and all((frame_dir / f"{i:04d}.png").is_file() for i in range(len(frames))):
        files.append(dict(video=video(frame_dir, len(frames), folder / ("kill-fixture.mp4" if entry["action"] == "kill-fixture" else label + ".mp4"))))
    return dict(action=entry["action"], attempt=attempt, rawCapture=dict(source=rel(raw), sha256=sha(raw), frames=len(frames),
                ok=doc.get("ok"), error=doc.get("error")), boundary=capture.get("boundary"),
                hpOutcome=entry.get("hpOutcome"))


def archive_partial(spec, folder, files, selected):
    case_dir = BASE / spec["partial"]
    result = case_dir / "result.json"
    journal = case_dir / "journal.jsonl"
    archived_result = folder / "kill-fixture-partial-result.json.gz"
    files.extend([gz(result, archived_result),
                  gz(journal, folder / "kill-fixture-partial-journal.jsonl.gz")])
    raw = None
    for line in journal.read_text().splitlines():
        row = json.loads(line)
        if row.get("kind") == "capture-result":
            raw = Path(row["data"]["path"])
            break
    assert raw and raw.is_file()
    doc = read(raw)
    files.append(gz(raw, folder / "kill-fixture-partial.json.gz"))
    frame_dir = raw.with_suffix("")
    available = [i for i in range(len(doc.get("frames") or [])) if (frame_dir / f"{i:04d}.png").is_file()]
    for i in sorted(set(available[:1] + available[-1:])):
        selected.append(png(frame_dir / f"{i:04d}.png", folder / "selected" / f"kill-fixture-partial-{i:04d}.png"))
    if available:
        files.append(dict(video=video(frame_dir, len(available), folder / "kill-fixture-partial.mp4")))
    return dict(result=str(archived_result.relative_to(OUT)), rawCapture=dict(source=rel(raw), sha256=sha(raw),
                frames=len(doc.get("frames") or []), availablePngs=len(available),
                ok=doc.get("ok"), error=doc.get("error")),
                boundary="Renderer destroyed during capture; retained prefix only.")


def archive_arrival(spec, folder, files, selected):
    """Archive one immutable passive enemy-arrival result and its raw frames."""
    case_dir = BASE / spec["arrival_case"]
    result_path = case_dir / "arrival-case-result.json"
    journal = case_dir / "journal.jsonl"
    result = read(result_path)
    files.extend([gz(result_path, folder / "arrival-case-result.json.gz"),
                  gz(journal, folder / "arrival-journal.jsonl.gz")])
    raw = Path(result["rawCapturePath"])
    assert raw.is_file() and sha(raw) == result["rawCaptureSha256"]
    doc = read(raw)
    frames = doc.get("frames") or []
    assert result.get("terminal") is True and result.get("rawCaptureOk") is True and len(frames) == 120
    frame_dir = raw.with_suffix("")
    files.append(gz(raw, folder / "arrival.json.gz"))
    for index in (0, len(frames) // 2, len(frames) - 1):
        image = frame_dir / f"{index:04d}.png"
        assert image.is_file()
        selected.append(png(image, folder / "selected" / f"arrival-{index:04d}.png"))
    assert all((frame_dir / f"{i:04d}.png").is_file() for i in range(len(frames)))
    files.append(dict(video=video(frame_dir, len(frames), folder / "arrival.mp4")))
    pose = frames[0]
    binding = {k: pose.get(k) for k in ("celRelativeRendererPath", "mesh", "boneSignature", "ownerInstanceId", "instanceId")}
    return dict(mode="passive-arrival", rawCapture=dict(source=rel(raw), sha256=sha(raw), frames=len(frames),
                ok=doc.get("ok"), error=doc.get("error")), boundary=result.get("boundary"),
                arrival=doc.get("arrival"), binding=binding)


def main():
    assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "refusing to overwrite completed supplement"
    index = read(INDEX)
    names = {x["native"] for x in CASES}
    if os.environ.get("FTK_ARCHIVE_REBUILD") == "1":
        # Rebuilding is explicit and replaces this supplement's rows, so a
        # later rerun cannot silently duplicate evidence in the runtime index.
        index["calibration_probes"] = [x for x in index.get("calibration_probes", [])
                                        if x.get("enemy_id") not in names]
    else:
        assert not any(x.get("enemy_id") in names for x in index.get("calibration_probes", [])), "runtime index already contains a target row"
    OUT.mkdir(parents=True, exist_ok=True)
    files, result_cases = [], []
    for spec in CASES:
        source_dir = BASE / spec.get("case", spec.get("arrival_case"))
        result = read(source_dir / ("arrival-case-result.json" if spec.get("kind") == "arrival" else "case-result.json"))
        folder = OUT / spec["name"]
        folder.mkdir(parents=True, exist_ok=True)
        if spec.get("kind") == "arrival":
            selected, captures = [], []
            capture_record = archive_arrival(spec, folder, files, selected)
            captures.append(capture_record)
            raw = read(BASE / spec["arrival_case"] / "arrival-case-result.json")["rawCapturePath"]
            raw_doc = read(Path(raw))
            pose = raw_doc["frames"][0]
            binding = {"rendererPath": pose.get("celRelativeRendererPath"), "mesh": pose.get("mesh"),
                       "boneSignature": pose.get("boneSignature"), "ownerInstanceId": pose.get("ownerInstanceId"),
                       "instanceId": pose.get("instanceId")}
            asset_hashes = (result.get("arm") or {}).get("assets", {})
            result_cases.append(dict(name=spec["name"], nativeEnemy=spec["native"], profileKey=spec["profile"],
                rendererPath=spec["renderer"], sourceRendererId=spec["source_id"], session=result.get("session"),
                status=spec["status"], binding=binding, assetHashes=asset_hashes, attack=None,
                attackRetrySummary=None, captures=captures, partialKill=None, selectedFrames=selected, limits=spec["limits"]))
            continue
        files.extend([gz(source_dir / "case-result.json", folder / "case-result.json.gz"),
                      gz(source_dir / "journal.jsonl", folder / "journal.jsonl.gz")])
        stage_dir = BASE / spec["stage"]
        if (stage_dir / "journal.jsonl").is_file():
            files.append(gz(stage_dir / "journal.jsonl", folder / "stage-journal.jsonl.gz"))
        selected, captures = [], []
        for entry in result.get("actions", []):
            record = archive_action(entry, folder, files, selected)
            if record:
                captures.append(record)
        partial = archive_partial(spec, folder, files, selected) if spec.get("partial") else None
        binding = result.get("initialRenderer") or {}
        attacks = [x for x in result.get("actions", []) if x.get("action") == "attack"]
        accepted_attack = next((x.get("hpOutcome") for x in attacks
                                if (x.get("hpOutcome") or {}).get("status") == "nonlethal_hp_loss"),
                               next((x.get("hpOutcome") for x in attacks), None))
        result_cases.append(dict(name=spec["name"], nativeEnemy=spec["native"], profileKey=spec["profile"],
            rendererPath=spec["renderer"], sourceRendererId=spec["source_id"], session=result.get("session"),
            status=spec["status"], binding={k: binding.get(k) for k in ("rendererPath", "mesh", "boneSignature", "ownerInstanceId", "instanceId")},
            assetHashes=result.get("assetHashes", {}),
            attack=accepted_attack, attackRetrySummary=result.get("attackRetrySummary"),
            captures=captures, partialKill=partial, selectedFrames=selected, limits=spec["limits"]))
    validation = dict(schema="ftkmf.unresolved-enemy-probes.v1", status="diagnostic_exact_binding_follow_up",
        catalogSha256=sha(PROFILE), catalog=dict(path=rel(PROFILE), sha256=sha(PROFILE)),
        mapping=dict(path=rel(MAPPING), sha256=sha(MAPPING)), cases=result_cases, files=files,
        selectedFrames=sum((x["selectedFrames"] for x in result_cases), []),
        commonLimits=["Exact renderer/controller diagnostics are not topology-wide acceptance.",
                      "No custom finished-art acceptance is inferred from the probe palette.",
                      "A stopped no_hp_loss case preserves the observed boundary and does not infer its cause.",
                      "Fish staff/spear and other rows may use bounded repeated native attacks after an observed no_hp_loss outcome; acceptance still requires measured nonlethal HP loss.",
                      "Modern krakenHead evidence is separate from old enkrakenhead resource evidence."])
    # Keep the cross-reference one-way: the validation document pins the index
    # path, while each appended index row pins the final validation hash.  A
    # hash on both sides would require an impossible circular rewrite.
    validation["runtimeIndex"] = dict(path=rel(INDEX))
    write(OUT / "validation.json", validation)
    write(OUT / "source-image-pins.json", {x["archive"]: x["sha256"] for x in validation["selectedFrames"]})
    asset_pins = {"catalog": sha(PROFILE), "mapping": sha(MAPPING)}
    for item in result_cases:
        for name, digest in item["assetHashes"].items():
            asset_pins[f"{item['name']}/{name}"] = digest
    write(OUT / "asset-pins.json", asset_pins)
    evidence = dict(path="docs/evidence/unresolved-enemy-probes-v1/validation.json", sha256=sha(OUT / "validation.json"))
    for item in result_cases:
        index["calibration_probes"].append(dict(native_chassis=item["nativeEnemy"], enemy_id=item["nativeEnemy"],
            iteration="catalog-411 unresolved topology follow-up", purpose="Exact production renderer binding diagnostic; not finished artwork.",
            status=item["status"], session=item["session"], renderer_paths=[item["rendererPath"]],
            source_renderer_ids=[item["sourceRendererId"]], binding=item["binding"], attack=item["attack"],
            attackRetrySummary=item["attackRetrySummary"], captures=item["captures"], partialKill=item["partialKill"], evidence=evidence,
            runtime_asset_sha256=item["assetHashes"], limits=item["limits"]))
    write(INDEX, index)
    (OUT / "README.md").write_text(f"""# Unresolved enemy probe follow-up V1

This supplement records fresh catalog-411 exact-renderer trials for ordinary enemy topology groups that did not have an indexed enemy-model run when the coverage plan was first generated. It is a binding and gameplay-boundary diagnostic, not a finished-art acceptance set.

Roc A and Desert Snake A completed fixed pass, ordinary Attack, explicit KillSingle fixture and native Ready sequences. Roc reduced HP 81 to 71; Desert Snake A reduced HP 58 to 48. Sea King, Jungle Snake C and modern Kraken head completed pass and Attack captures, but observed HP stayed unchanged (720 to 720, 86 to 86 and 324 to 324), so the recorder stopped at no_hp_loss_unclassified without guessing why. Sea King's separate KillSingle capture is retained only as a renderer-destroyed prefix.

Modern krakenHead (kraken2, renderer 121035) remains separate from the old five-bone enkrakenhead resource/owned-fixture evidence. No adapter or finished multipart-art acceptance transfers.

The four native tentacle/controller rows are covered independently: `krakenTentacle` and its mirror use renderer 121595 and record ordinary HP 162 to 152; `seaKingTentacleA` and `seaKingTentacleB` use renderer 121315 and record 270 to 264 and 270 to 266. Each has its own exact binding, complete pass/attack/fixture captures and native Ready observation. These remain calibration probes rather than finished multipart Kraken art acceptance.

The last unrecorded plant-controller row, `plantG` at `enJungleNibbler_B`/121584, is included as well: ordinary HP 58 to 50, fixture death, and Ready 0/2. Existing plant rows retain independent claims.

Two independent 37-joint rows are included: `skellyA` at `skelly01naked`/120979 records ordinary HP 58 to 55 and Ready 0/2; `hagA` at `hag01`/121109 records 58 to 50 and Ready 0/3. Their rig/controller bindings remain separate from each other and from every other 37-joint row.

The next 37-joint assignments are covered independently: `banditA` at `bandit02`/121005 records ordinary HP 58 to 50, fixture death and Ready 0/2; `thiefA` at `enThief01`/120997 reaches native HP 0 from 58 during the pass action before a hero attack, so the recorder stops at the self-termination boundary without substituting ordinary damage, fixture death or Ready; `hagB` at `hag02`/120995 records ordinary HP 58 to 48, fixture death and Ready 0/2; `skellyB` at `skelly04archer`/121169 records 58 to 55, fixture death and Ready 0/2; `witchA` at `witch01`/121039 records 58 to 48, fixture death and Ready 0/2; `banditD` at `enBanditB02`/121235 records 58 to 56, fixture death and Ready 0/2; `banditC` at `enBanditB01`/121156 requires five bounded no-loss attempts before recording 58 to 57 on attempt 6, followed by fixture death and Ready 0/2; `assassinA` at `bandit01`/121075 records 58 to 48, fixture death and Ready 0/2; `hagC` at `hag03`/121089 records 58 to 50, fixture death and Ready 0/2; `skellyC` at `skelly03fighter`/121228 records 58 to 54, fixture death and Ready 0/2; `assassinB` at `enAssassinB`/121266 records 68 to 58, fixture death and Ready 0/2; and `witchC` at `enWitchC`/121547 records 63 to 55, fixture death and Ready 0/2. `demonA` at `demon`/121166 has exact binding and eight bounded native attacks at HP 108 to 108, so the recorder stops at the repeatable no-loss boundary without a fixture or Ready claim; `ghostA` at `ghost01`/121160 records ordinary HP 58 to 48, fixture death and native Ready, with its renderer-destroyed capture prefix retained; `wildmageA` at `wildMage02`/121030 records 58 to 53, fixture death and Ready; `scourgeA` at `enDisciple`/121535 records 126 to 118, fixture death and Ready; `scourgeB` at `enScourgeBanditKing`/120988 records 81 to 79, fixture death and Ready; `beastmanA` at `beastman01`/121227 records 58 to 48, fixture death and Ready; `beastmanB` at the same `beastman01` mesh but source 121139 records 58 to 50, fixture death and Ready; `cragHulk` at `rockman01`/121126 records 58 to 56 on a fresh bounded retry after an earlier fresh 58→58 no-loss stop; `beastmanC` at the same mesh but source 121069 records 58 to 53, fixture death and Ready; `drycorpseA` at `enDryCorpse01`/120994 records 58 to 48 on a fresh bounded retry after an earlier fresh 58→58 no-loss stop; `lizardmanA` at `boggling`/121028 records 58 to 56, fixture death and Ready; and `catmageA` at `mysticCat`/121022 requires seven no-loss attempts before recording 58 to 48 on attempt 8, followed by fixture death and Ready. These rows do not transfer the skellyA, hagA or bearman evidence.

`wildmageB` at `wildmage01`/121112 records ordinary HP 70 to 62, fixture death, and Ready; and `bisonA` at `bisontaur`/120958 records ordinary HP 58 to 54, fixture death, and Ready. These remain independent exact assignments alongside `wildmageA` and the other dual-weapon/37-joint rows.

The next six 37-joint assignments are covered independently: `cyclopsA` at `triclops`/121006 records ordinary HP 90 to 82, fixture death and Ready; `cyclopsC` at `triclopsBaby`/121102 records 68 to 57, fixture death and Ready; `mummyA` at `mummy`/121097 requires a fresh bounded retry after an earlier 81 to 81 no-loss stop and then records 81 to 77 on attempt 2, fixture death and Ready; `mummyB` at `mummy02`/120969 records 90 to 87, fixture death and Ready; `owlbearA` at `enOwlBear`/121209 records 126 to 118, fixture death and Ready; and `cultistA` at `cultist2`/120950 records 58 to 48, fixture death and Ready. Each remains an independent renderer/controller assignment.

`cultistBoss1` at `cultist3`/121091 adds ordinary HP 58 to 53, fixture death and Ready. `cultistC` at `cultistC`/121257 has exact binding and eight bounded native attacks at HP 58 to 58, so the recorder stops at the repeatable no-loss boundary without a fixture or Ready claim; the two cultist rows retain separate exact scopes.

The next five 37-joint assignments are covered independently: `skellymageA` at `skelly07mage`/121146 records ordinary HP 58 to 50, fixture death and Ready; `skellymageD` at `skellyMage02`/121469 requires a fresh bounded retry after an earlier 58 to 58 no-loss stop and then records 58 to 48 on attempt 1, fixture death and Ready; `wraithA` at `enWraith`/121252 records 58 to 50, fixture death and Ready with no usable Collect button; `druidA` at `enDruid`/121130 records 58 to 48, fixture death and Ready; and `sharkBruteA` at `enSharkBruteA`/121432 requires a fresh bounded retry after an earlier 99 to 99 no-loss stop and then records 99 to 97 on attempt 2, fixture death and Ready. These remain independent renderer/controller assignments.

The next three 37-joint assignments are covered independently: `mindflayerA` at `mindMelter`/121258 records ordinary HP 63 to 53, fixture death, two native Collect clicks and strict Ready; `ogreA` at `enOgre`/121076 records 108 to 104, fixture death, one native Collect and strict Ready; and `ogreC` at `enOgre_Armored`/121350 records eight bounded native attacks at HP 158 to 158 before stopping at the repeatable no-loss boundary. Ogre A and the armored Ogre C remain separate renderer/source assignments, and the stopped Ogre C row has no fixture, loot or Ready claim.

The next three 37-joint assignments are covered independently: `ratthiefA` at `ratThief`/121128 records ordinary HP 58 to 48 and fixture death, but its one accepted native Collect never changed the loot state or exposed strict Ready; `scourgeC` at `scourgeJester`/121203 records 63 to 58 and fixture death, but its two accepted Collect clicks likewise stopped at unchanged loot; and `scourgeD` at `enScourgeFraybee`/121034 records 126 to 116, fixture death, two native Collects and strict Ready. The stopped loot boundaries remain once-only observations with no forced progression.

The next four scourge assignments are covered independently: `scourgeE` at `scourgeTime`/121138 records ordinary HP 90 to 13, fixture death, two native Collects and strict Ready; `scourgeH` at `enScourgeVolcanoWizard`/121208 records 81 to 73, fixture death, two native Collects and strict Ready; and `scourgeI` at `scourgeDemis`/121445 requires a fresh bounded retry after an earlier 108 to 108 no-loss stop, then records 108 to 105 on attempt 1, fixture death, two native Collects and strict Ready. These exact renderer/source assignments remain separate.

`scourgeJ` at `enScourgeWitchdoctor`/121614 adds another exact 37-joint magic assignment: eight bounded native attacks all observed 108 to 108, so the recorder stops at the repeatable no-loss boundary without fixture, loot or Ready acceptance. `scourgeK` at `enScourgeSiren`/121462 records ordinary HP 216 to 213, fixture death, two native Collects and strict Ready. These rows retain separate renderer/source/controller scope.

The next five 37-joint rows are covered independently: `leprechaunA` at `leprechaun`/121214 reaches native HP 0 from 58 during the hero attack before the fixture, so the recorder preserves the target-removal boundary; `banditB` at `BanditWarrior`/121201 records ordinary HP 58 to 52, fixture death, one native Collect and strict Ready; `bisonUndead` at `enBisonUndead`/121085 records eight bounded attacks at HP 113 to 113 and stops at the repeatable no-loss boundary; `cultistE` at `cultistC`/121257 records 86 to 76, fixture death, one native Collect and strict Ready; and `cultistA2` at `cultist2B`/121172 requires one no-loss retry before recording 58 to 48, fixture death, one native Collect and strict Ready. Shared renderer paths do not transfer these source/controller claims.

The next five exact 37-joint assignments are covered independently: `foxshamanA` at `enFoxShaman`/121246 records ordinary HP 58 to 45, fixture death, two native Collects and strict Ready; `yetiA` at `enYeti`/121381 records 89 to 87, fixture death, two native Collects and strict Ready; `swampmonsterA` at `swampMonster`/121114 records 72 to 64 and fixture death, but two accepted Collects stop at unchanged loot without strict Ready; `skellyBardA` at `skelly01naked`/120980 records 58 to 48, fixture death, one native Collect and strict Ready; and `cultistC2` at `cultistC2`/121230 records 58 to 45, fixture death, two native Collects and strict Ready. These rows remain separate despite shared meshes or controller families.

The next five exact 37-joint assignments are covered independently: `cultistD3` at `cultistC3`/121096 records two bounded no-loss attacks at 70 to 70, then ordinary HP 70 to 68 on attempt 3, fixture death, one native Collect and strict Ready; `cultistBoss2` at `cultist3`/121092 records 81 to 76, fixture death, two native Collects and strict Ready; `bardA` at `enBard`/121163 records 58 to 53, fixture death, two native Collects and strict Ready; `pirateA` at `enPirate02`/120970 records 58 to 50, fixture death, one native Collect and strict Ready; and `pirateB` at `enPirate01`/121205 reaches native HP 0 from 58 during the hero attack before the fixture, so the recorder preserves the target-removal boundary without loot or Ready acceptance. These rows remain separate despite shared 37-joint meshes and controller families.

The next five exact 37-joint assignments are covered independently: `mageImpA` at `impWizard01`/121149 records two bounded no-loss attacks at 58 to 58, then ordinary HP 58 to 48 on attempt 3, fixture death, one native Collect and strict Ready; `minionScourgeB` at `BanditWarrior`/121201 records 58 to 53, fixture death and strict Ready, with no usable native Collect button; `minionScourgeF` at `enMinionHangman`/121321 records 58 to 48, fixture death and strict Ready, also with no usable native Collect button; `lichA` at `enLichA`/121651 records 68 to 64, fixture death, one native Collect and strict Ready; and `harazuelBoss2` at `cultistFinalBoss`/121187 records eight bounded attacks at 198 to 198 under native armor/block and stops at the repeatable no-loss boundary without fixture, loot or Ready acceptance. These rows remain separate despite shared 37-joint meshes and controller families.

The next five exact 37-joint assignments are covered independently: `harazuelBoss3` at `cultistFinalBoss`/121188 records eight bounded attacks at 207 to 207 through the native fire/interrupt/evade path; `harazuelBoss4` at `cultistFinalBoss`/121189 records eight bounded attacks at 216 to 216 through native lightning shield/resist/dodge; both stop at the repeatable no-loss boundary without fixture, loot or Ready acceptance; `harazuelMinionC` at `enWraith`/121251 records one no-loss attempt at 77 to 77, then 77 to 72 on attempt 2, fixture death and strict Ready with no usable native Collect button; `goblinA` at `enGoblinA`/121514 records 58 to 50, fixture death, two native Collects and strict Ready; and `goblinB` at `enGoblinArcher`/121487 records 58 to 52, fixture death, one native Collect and strict Ready. These rows remain separate despite shared meshes and controller families.

The next five exact 37-joint assignments are covered independently: `goblinC` at `enGoblinWiz`/121427 records ordinary HP 58 to 53, fixture death, one native Collect and strict Ready; `goblinD` at `enGoblinGrunt`/121270 records 58 to 57, fixture death, two native Collects and strict Ready; `goblinE` at `enGoblinAssassin`/121642 records 58 to 51, fixture death, one native Collect and strict Ready; `foxFighter` at `enFoxFighter`/121590 records 58 to 54, fixture death, two native Collects and strict Ready; and `foxFighterB` at the same renderer path but source 121591 records 58 to 53, fixture death, one native Collect and strict Ready. These rows remain separate despite shared meshes and controller families.

The next five exact 37-joint assignments are covered independently: `beastmanD` at `beastman02`/121355 records ordinary HP 58 to 53, fixture death, two native Collects and strict Ready; `drownedCorpseA` at `enDrownedSailor`/121406 records 58 to 53, fixture death, one native Collect and strict Ready; `ghoulA` at `enGhoul`/121430 records 58 to 56, fixture death, one native Collect and strict Ready; `skellyG` at `skelly01naked`/120981 records 58 to 48, fixture death, one native Collect and strict Ready; and `snowGoblinA` at `enSnowGoblinA`/121383 records 58 to 57, fixture death, two native Collects and strict Ready. These rows remain separate despite shared meshes and controller families.

The next five exact 37-joint assignments are covered independently: `snowGoblinB` at `enSnowGoblinB`/121466 records ordinary HP 58 to 50, fixture death, two native Collects and strict Ready; `snowGoblinD` at `enSnowGoblinD`/121358 records 58 to 48, fixture death, two native Collects and strict Ready; `snowGoblinC` at `enSnowGoblinC`/121377 records 58 to 55, fixture death, one native Collect and strict Ready; `pirateD01` at `enPirateD01`/121304 records eight bounded native attacks at HP 90 to 90 and stops at the repeatable `no_hp_loss_unclassified` boundary without fixture, loot or Ready acceptance; and `banditE` at `enBanditE`/121650 records 58 to 55, fixture death, one native Collect and strict Ready. These rows remain separate despite shared meshes and controller families; the pirateD01 no-loss result does not infer block, dodge or immunity.

The next five exact 37-joint assignments are covered independently: `banditF` at `enBanditF`/121516 records ordinary HP 58 to 56, fixture death, one native Collect and strict Ready; `vampireA` at `enVampireB`/121399 records one 72 to 72 no-loss attempt then 72 to 62 on attempt 2, fixture death, two native Collects and strict Ready; `snowBeastA` at `en_SnowBeastA`/121318 records 58 to 50, fixture death, two native Collects and strict Ready; `snowBeastB` at `enSnowBeastManB`/121507 records 58 to 50, fixture death, one native Collect and strict Ready; and `snowBeastC` at `enSnowBeastManC`/121657 records 58 to 48, fixture death, one native Collect and strict Ready. Each selected idle and attack frame visibly retained the original probe's connected joints and native weapon attachment, while the post-fixture frames reach the native loot surface; they remain calibration probes, not finished creature-art acceptance.

The next five exact 37-joint assignments are covered independently: `hobgoblinA` at `enHobGoblinA`/121422 records ordinary HP 58 to 56, fixture death, two native Collects and strict Ready; `hobgoblinB` at `enHobGoblinB`/121565 records three 58 to 58 no-loss attempts, then 58 to 56 on attempt 4, fixture death, one native Collect and strict Ready; `pirateD` at `enDrunkPirateD`/121273 records 58 to 53, fixture death, two native Collects and strict Ready; `pirateE` at `enDrunkPirateE`/121683 records 58 to 45, fixture death, two native Collects and strict Ready; and `snowGoblinE` at `enSnowGoblinE`/121360 records 58 to 53, fixture death, one native Collect and strict Ready. Each selected idle and attack frame visibly retained the original probe's connected joints and native weapon attachment, while the reviewed post-fixture frames expose the native loot surface; they remain calibration probes, not finished creature-art acceptance.

The next five exact 37-joint assignments are covered independently: `banditG` at `enFrozenBanditA`/121412 records ordinary HP 58 to 50, fixture death, one native Collect and strict Ready; `banditH` at `enFrozenBanditB`/121589 records 58 to 53, fixture death, two native Collects and strict Ready; `koboldC` at `enKoboldB`/121675 records 58 to 50, fixture death, one native Collect and strict Ready; `boomerA` at `enPirtaeSkellyBoner`/121655 records 135 to 133, fixture death, two native Collects and strict Ready; and `pirateC` at `enPirateC02`/121608 records 58 to 52, fixture death, one native Collect and strict Ready. Selected idle and attack frames retain each probe's connected joints and the native bow, blunt, boomer, or musket attachment, while the reviewed post-fixture frames expose the native loot surface; they remain calibration probes, not finished creature-art acceptance.

The next five exact 37-joint assignments are covered independently: `pirateC02` at `enPirateC02`/121608 records ordinary HP 59 to 53, fixture death, one native Collect and strict Ready; `pirateA03` at `enDrunkPirateE`/121683 records 58 to 53, fixture death, two native Collects and strict Ready; `pirateB03` at `enPirate02`/120971 records 58 to 52, fixture death, two native Collects and strict Ready; `pirateC03` at `enPirateC03`/121385 records 72 to 62, fixture death, two native Collects and strict Ready; and `golemA` at `enAtztecGolem_Fire`/121493 records 58 to 54, fixture death, two native Collects and strict Ready. Selected pirate views retain connected probes and their native cannon, blunderbuss, musket or dual-weapon attachments. Golem A's native fire emission obscures detailed probe geometry in the selected views, so its row is binding and gameplay evidence only. The reviewed post-fixture frames expose the native loot surface; none of these calibration probes is finished creature-art acceptance.

The next five exact 37-joint assignments are covered independently: `golemB` at `enAtztecGolem_Frost`/121332 records ordinary HP 72 to 71, fixture death, two native Collects and strict Ready; `banditDesertA` at `enDesertBanditA`/121374 records 58 to 50, fixture death, one native Collect and strict Ready; `banditDesertC` at `enDesertBanditC`/121418 records 58 to 48, fixture death, one native Collect and strict Ready; `koboldJungleA` at `enJungleKoboldA`/121473 records 58 to 54, fixture death, one native Collect and strict Ready; and `koboldJungleB` at `enJungleKoboltB`/121672 records 58 to 48, fixture death, one native Collect and strict Ready. Selected idle and attack frames visibly retain the original probes' connected joints and the native frost, dual-weapon, or bow behavior, while reviewed post-fixture frames expose the native loot surface. They remain calibration probes, not finished creature-art acceptance.

The next five exact 37-joint assignments are covered independently: `koboldJungleC` at `enJungleKoboldC`/121313 records ordinary HP 58 to 48, fixture death, one native Collect and strict Ready; `warriorA` at `enJungleWarriorA`/121402 records 58 to 52, fixture death, two native Collects and strict Ready; `warriorB` at `enAncientWarriorB`/121353 records 58 to 53, fixture death, one native Collect and strict Ready; `warriorC` at `enJungleDruid_B`/121337 records 58 to 52, fixture death, two native Collects and strict Ready; and `skellyJungleA` at `enJungleSkelly`/121382 records 58 to 57, fixture death, two native Collects and strict Ready. Selected idle and attack frames visibly retain the original probes' connected joints and native wand, dual-weapon or bow attachments, while reviewed post-fixture frames expose the native loot surface. They remain calibration probes, not finished creature-art acceptance.

The next five exact 37-joint assignments are covered independently: `zombieJungleA` at `enJungleCorpseA`/121325 records ordinary HP 59 to 58, fixture death, one native Collect and strict Ready; `trollJungleA` at `enJungleTroll`/121520 records 162 to 152, fixture death, two native Collects and strict Ready; `zombieJungleB` at `enJungleCorpseB`/121352 records 81 to 71, fixture death, one native Collect and strict Ready; `minionScourgeJ2` at the same renderer path and source records 58 to 48, fixture death, no native Collect and strict Ready; and `warriorD` at `enAncientWarriorA`/121417 records three bounded 58 to 58 no-loss attempts, then 58 to 56 on attempt 4, fixture death, two native Collects and strict Ready. Selected idle and attack frames retain connected joints, the troll club, the warrior weapon and shield, and the distinct upright or low corpse-controller poses; reviewed post-fixture frames expose the native loot or Ready surface. These remain calibration probes, not finished creature-art acceptance.

The next five exact 37-joint assignments are covered independently: `warriorE` at `enJungleWarriorB_B`/121689 records ordinary HP 58 to 48, fixture death, one native Collect and strict Ready; `warriorF` at `enAncientDruid_B`/121429 records two bounded 58 to 58 no-loss attempts, then 58 to 50 on attempt 3, fixture death, one native Collect and strict Ready; `warriorB02` at `enAncientWarriorB_B`/121576 records one 58 to 58 no-loss attempt, then 58 to 53 on attempt 2, fixture death, two native Collects and strict Ready; and `warriorC02` at `enJungleDruid`/121498 records 58 to 48, fixture death, one native Collect and strict Ready. `warriorD02` at `enAncientWarriorA_B`/121285 retains complete pass and attack capture but all eight no-focus attempts recorded 68 to 68. A fresh focus=true supplement also records a native `Attack(focus)` at 68 to 68, so both stop before fixture, loot or Ready without inferring a combat cause. Selected idle and attack frames retain connected joints and native dual, wand, bow, or blunt attachments; the four complete post-fixture frames expose the native loot surface. These remain calibration probes, not finished creature-art acceptance.

The final three exact 37-joint assignments are covered independently: `warriorE02` at `enJungleWarriorB`/121549 records ordinary HP 63 to 50, fixture death, one native Collect and strict Ready; `warriorF02` at `enAncientDruid`/121423 records four bounded 63 to 63 no-loss attempts, then 63 to 59 on attempt 5, fixture death, two native Collects and strict Ready; and `aztecBoss` at `enJungleElder`/121503 records one 315 to 315 no-loss attempt, then 315 to 312 on attempt 2, fixture death, no native Collect and strict Ready. The Aztec source distinguishes a serialized one-handed wand renderer animator from the two-handed magic weapon-controller candidate; its initialized runtime animator reports the two-handed magic controller. Selected frames retain connected joints and native dual, wand, or magic attachments. Native Aztec effects limit fine visual-art assessment; all rows remain calibration probes, not finished creature-art acceptance.

`bearmanA` adds the dual-weapon branch at `bearman`/121195, recording ordinary HP 58 to 50, fixture death, and Ready 0/2. It remains an independent exact assignment.

The nine remaining fish rows are covered independently: `fishB02`/`enFishD`/121620, `fishB03`/`enFishE`/121686, `fishC01`/`enFishF`/121649, `fishC02`/`enFishG`/121628, `fishD01`/`enFishD01`/121543, `fishD02`/`enFishD02`/121479, `fishB01`/`enFishH`/121298, `fishA02`/`enFishB`/121286, and `fishA03`/`enFishC`/121594. Each records its own exact 34-joint binding, controller exercise, fixture death and Ready observation. `fishD01` and `fishD02` required bounded repeated native hero attacks after earlier block/no-loss outcomes; acceptance still required measured nonlethal HP loss. These remain calibration probes with synthetic probe assets, not finished fish art acceptance.

`mimicClamA` at `enClam`/121306 closes the remaining clam exact pair: binding, pass, ordinary HP 58 to 48 and fixture death are complete. One native Collect click was accepted, but the item/button and reward state never progressed to strict Ready; the once-only runner preserved that boundary and stopped without a second click or forced progression.

`batJungleA` at `enJungleBat`/121437 closes the remaining bat exact pair with ordinary HP 58 to 53, fixture death and Ready 0/2. It remains a calibration probe with a synthetic asset; bat siblings keep independent evidence.

`bearA` at `enBear02`/121534 adds the first remaining 38-joint bear variant with ordinary HP 58 to 48, fixture death and Ready 0/2. Its controller and binding remain independent from the existing bearB evidence and from bearC.

`bearC` at `enBear03`/121486 completes the remaining bear exact pair with native HP 81 to 76, fixture death and Ready 0/2. Bear A, B and C remain separate renderer/bind assignments despite their shared controller.

`wispA` at `enWisp`/121121 completes the remaining two-joint eyes exact pair at the binding/pass boundary. A fresh retry run captured eight bounded native hero attacks, each observing HP 58 to 58; the recorder stopped at the repeatable `no_hp_loss_unclassified` boundary without assigning a combat cause, and no fixture death, loot or Ready claim is made.

`crabA` at `CrabGeo`/121579 completes the remaining 63-joint crab exact pair. The first two bounded attacks observed HP 58 to 58; attempt 3 measured HP 58 to 53, after which the explicit fixture death and strict Ready at room 2 were captured. The earlier no-loss attempts remain separate evidence and no combat cause is inferred.

`cockatriceA` at `enChicken`/121328 completes one of the two remaining 50-joint cockatrice assignments with ordinary HP 58 to 48, explicit fixture death and strict Ready. The boss `enCockatrice` row remains an independent renderer/controller claim.

`bossCockatrice` at `enCockatrice`/121554 completes the remaining 50-joint cockatrice assignment at the binding/pass boundary. Eight bounded native attacks all observed HP 540 to 540; the recorder stopped without assigning a combat cause, and no fixture death, loot or Ready claim is made.

The five remaining 39-joint bird-controller rows are now covered independently: `seagullA`/`enSeagull`/120999 records ordinary HP 58 to 48; `seagullB`/`enSeagull2`/121346 required a bounded retry after 58 to 58 and then recorded 58 to 50; `parrotA`/`enParrotA`/121369 records 58 to 45; `birdJungleA`/`enJungleBird_A`/121339 records 58 to 48; and `birdJungleB`/`enJungleBird_B`/121670 records 58 to 50. Each exact pair has complete pass, fixture death and strict Ready evidence. These remain calibration probes with synthetic assets; the existing crowC original-art evidence does not transfer to the five rows.

The remaining 65-joint spider-controller rows are covered independently: `spiderA` at `enSpiderA`/121453 records ordinary HP 58 to 50, and `spiderJungleA` at `enJungleSpider_A`/121580 records 58 to 48. Both have complete pass, fixture death and strict Ready evidence. These remain calibration probes with synthetic assets; the existing spiderB original-art evidence does not transfer.

The four remaining 33-joint wolf-controller rows are covered independently: `chaosHound` at `chaosWolf`/121191 has exact binding/pass and eight bounded no-loss attacks at HP 180 to 180, so no fixture or Ready claim is made; `hellhoundA` at `enHellHoundB`/121303 records 81 to 71; `pantherA` at `enPanther`/121319 records 99 to 89; and `jaguarA` at `enJaguar`/121571 required a bounded retry after 72 to 72 and then records 72 to 62. The three damaging rows have fixture death and strict Ready evidence. These remain calibration probes with synthetic assets; Ashfang's wolfA original art does not transfer.

The remaining 36-joint deathknight controller row is covered independently: `deathknightEboss` at `deathKnight`/121220 has exact binding/pass and eight bounded no-loss attacks at HP 158 to 158, so no fixture or Ready claim is made. It remains a calibration probe with a synthetic asset; deathknightA's original art and blunt-controller evidence do not transfer to this distinct Eboss row.

`monkeyA` at `enMonkeyA`/121679 records the exact 42-joint binding and pass boundary. Its native Prof0 `enSuicideBlast` runs before the hero attack and applies secondary damage 58, so the target self-terminates before an ordinary attack capture; no forced replacement action or death/Ready claim is made.

`monkeyD` at `enMonkeyDiseased`/121441 uses the passive arrival path because its native Prof0 `enSuicideDisease` also acts before the hero turn. The complete 120-frame arrival capture records the exact 42-joint live lease and the native sequence: the enemy attacks, then takes 58 secondary damage from the disease path and reaches 0 HP. No hero action, ordinary damage acceptance or loot/Ready claim is substituted for that controller boundary.

validation.json links exact renderer metadata, source IDs, case journals, complete or stopped raw captures as gzip-lossless metadata, selected PNGs, and presentation videos. The archive omits game payloads and DLLs. archive-script.py is offline-only and refuses to overwrite a completed supplement.

The current validation hash is `{sha(OUT / "validation.json")}`; the runtime-index rows point back to it, and `asset-pins.json` includes the shared probe GLB/palette hashes. Rebuilds are explicit (`FTK_ARCHIVE_REBUILD=1`) and replace only these {len(CASES)} supplement rows.
""")
    print(json.dumps(dict(validation=str(OUT / "validation.json"), cases=len(result_cases), files=len(files),
                          indexSha256=sha(INDEX), validationSha256=sha(OUT / "validation.json")), indent=2))


if __name__ == "__main__":
    main()
