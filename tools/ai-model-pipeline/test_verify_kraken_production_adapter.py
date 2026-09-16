import copy
import hashlib
import json
import sys
import tempfile
import unittest
from pathlib import Path

sys.path.insert(0, str(Path(__file__).parent))
from verify_kraken_production_adapter import (
    ADAPTER_SHA256, ADAPTER_TEST_SHA256, CLIPS, CONTRACT_SHA256,
    CONTROLLER_SHA256, DECOMPILE_SHA256, GAME_SHA256, GLB_SHA256, MANIFEST_SHA256,
    CATALOG_SHA256, COMBAT_PROFILE, NATIVE_ENEMY, PNG_SHA256, PROFILE_SHA256, REGISTERED_ENEMY,
    REQUIRED_NATURAL_STATES, REQUIRED_PROFICIENCIES, RESOURCES_SHA256, RUNS, STATES,
    STATE_COVERAGE_SHA256, load_campaign, verify_campaign,
)

HASHES = {name: 1000 + index for index, name in enumerate(STATES)}
LENGTHS = {"krakenIdle": 4.0, "krakenDamage": 3.0, "krakenDisappear": 2.0,
           "kraken_appear": 1.0, "krakenAttack": 7.1666669845581055}
BINARIES = {"framework": "1" * 64, "runtimeHelper": "2" * 64, "runtimeContent": "3" * 64}
PROFICIENCIES = ("enKrakenResistUp", "enKrakenInterrupt", "enKrakenConfuse", "enKrakenArmorUp")


def state(name, role, weight, normalized=.25):
    clip, loop = STATES[name]
    return {"role": role, "fullPathHash": HASHES[name], "shortNameHash": HASHES[name] + 100,
            "normalizedTime": normalized, "length": LENGTHS[clip], "loop": loop, "speed": 1.0,
            "speedMultiplier": 1.0, "state": name, "certified": True, "expectedClip": clip,
            "clips": [{"name": clip, "instanceId": 500 + CLIPS.index(clip), "length": LENGTHS[clip],
                       "loop": loop, "weight": weight}]}


def seconds(value):
    phase = value["normalizedTime"] % 1 if value["loop"] else max(0.0, min(1.0, value["normalizedTime"]))
    return phase * value["length"]


def empty_sampler():
    inputs = [{"input": index, "bank": "current" if index < 5 else "next",
               "clip": CLIPS[index % 5], "seconds": 0.0, "weight": 0.0, "valid": True}
              for index in range(10)]
    return {"rootInstanceId": 1, "rootUnityNull": False, "samplerDisposed": False,
            "graphValid": True, "graphMode": "Manual", "graphOutputCount": 1, "inputs": inputs}


def single(sampler, value):
    index = CLIPS.index(value["clips"][0]["name"])
    sampler["inputs"][index]["seconds"] = seconds(value)
    sampler["inputs"][index]["weight"] = 1.0


def frame(number, current_name, next_name=None):
    current = state(current_name, "current", .5 if next_name else 1.0)
    next_value = state(next_name, "next", .5, .75) if next_name else None
    modern, old = empty_sampler(), empty_sampler()
    current_appearance = current_name == "OverworldAppear"
    next_appearance = next_name == "OverworldAppear"
    if current_appearance or next_appearance:
        appearance = current if current_appearance else next_value
        main = next_value if current_appearance else current
        single(old, appearance)
        if next_value is not None:
            single(modern, main)
    else:
        index = CLIPS.index(current["clips"][0]["name"])
        modern["inputs"][index].update(seconds=seconds(current), weight=current["clips"][0]["weight"])
        if next_value:
            index = 5 + CLIPS.index(next_value["clips"][0]["name"])
            modern["inputs"][index].update(seconds=seconds(next_value), weight=next_value["clips"][0]["weight"])
    return {"frame": number, "realtime": number / 60.0, "postLateUpdate": True,
            "inTransition": next_value is not None, "current": current, "next": next_value,
            "adapter": {"configured": True, "initialized": True, "disabled": False, "disposed": False,
                        "ownerRegistered": True, "enabled": True},
            "modernSampler": modern, "oldSampler": old}


def pins():
    def assembly(value): return {"assemblyFileSha256": value}
    return {"framework": assembly(BINARIES["framework"]), "runtimeHelper": assembly(BINARIES["runtimeHelper"]),
            "runtimeContent": assembly(BINARIES["runtimeContent"]), "game": assembly(GAME_SHA256),
            "resourcesAssets": {"sha256": RESOURCES_SHA256}, "contract": {"sha256": CONTRACT_SHA256},
            "adapterSource": {"sha256": ADAPTER_SHA256}, "stateCoverage": {"sha256": STATE_COVERAGE_SHA256},
            "controllerExtraction": {"sha256": CONTROLLER_SHA256}, "decompile": {"sha256": DECOMPILE_SHA256},
            "adapterImplementationTest": {"sha256": ADAPTER_TEST_SHA256},
            "routeProfile": {"sha256": PROFILE_SHA256}, "modelCatalog": {"sha256": CATALOG_SHA256},
            "contentRegistration": {"sha256": "4" * 64},
            "registeredRoute": {"key": REGISTERED_ENEMY, "id": 1234567890, "baseEnemy": NATIVE_ENEMY,
                "resourcePrefab": "enkrakenhead", "combatProfile": COMBAT_PROFILE,
                "bindingKind": "explicit-plural", "status": "registered_spawn_validation_pending"},
            "gloamfinManifest": {"sha256": MANIFEST_SHA256,
                "glb": {"file": "gloamfin.glb", "sha256": GLB_SHA256},
                "png": {"file": "gloamfin_basecolor.png", "sha256": PNG_SHA256}}}


def callback(sequence, frame_no, kind, window, **values):
    result = {"sequence": sequence, "frame": frame_no, "realtime": frame_no / 60.0,
              "source": "passive-native-callback", "kind": kind, "attackWindow": window,
              "finalized": True, "exception": None}
    result.update(values)
    return result


def attack_callbacks(frame_starts):
    values, sequence = [], 0
    for window, proficiency in enumerate(PROFICIENCIES):
        start = frame_starts[proficiency]
        victim = {"photonId": 7, "turnIndex": window}
        primary = {"victimFid": victim, "newHealth": 20 - window,
                   "proficiency": proficiency, "proficiencySuccess": True}
        rows = [
            ("CharacterDummy.PlayAttackSequence", {"variant": "Attack", "attackAnim": "AttackProf",
                "override": "Attack", "primary": primary, "secondary": None, "tertiary": None}),
            ("CharacterEventListener.CombatTrigger", {"trigger": "Attack", "force": False}),
            ("CharacterEventListener.Foley", {"sound": "PLAY_SFX_WATER_SUBREMERGE"}),
            ("CharacterEventListener.PlayWeaponAnimation", {"argument": ""}),
            ("CharacterEventListener.Foley", {"sound": "PLAY_VO_ATTACK"}),
            ("CharacterEventListener.Dodge", {}),
            ("CharacterDummy.RespondToDodge", {"mainVictim": True, "fid": victim, "postHealth": 25}),
            ("CharacterEventListener.AttackHit", {}),
            ("CharacterDummy.RespondToHit", {"mainVictim": True, "fid": victim, "postHealth": 20 - window}),
            ("CharacterEventListener.Foley", {"sound": "PLAY_SFX_WATER_SUBREMERGE"}),
            ("CharacterEventListener.Foley", {"sound": "PLAY_VO_TAUNT"}),
            ("CharacterDummy.ActionCompleted", {}),
        ]
        for offset, (kind, data) in enumerate(rows):
            values.append(callback(sequence, start + offset, kind, window, **data)); sequence += 1
    return values


def cleanup():
    resources = [{"instanceId": 11, "name": "mesh", "type": "UnityEngine.Mesh", "unityNull": True},
                 {"instanceId": 12, "name": "material", "type": "UnityEngine.Material", "unityNull": True},
                 {"instanceId": 13, "name": "texture", "type": "UnityEngine.Texture2D", "unityNull": True}]
    return [
        {"sequence": 0, "kind": "LegacyKrakenResourceAdapterLease.DisposeOwned", "effective": True,
         "finalized": True, "exception": None},
        {"sequence": 1, "kind": "LegacyKrakenResourceAdapterLease.ClipSampler.Dispose", "sampler": "modern",
         "effective": True, "finalized": True, "exception": None},
        {"sequence": 2, "kind": "LegacyKrakenResourceAdapterLease.ClipSampler.Dispose", "sampler": "old",
         "effective": True, "finalized": True, "exception": None},
        {"sequence": 3, "kind": "natural-teardown-poll", "ownerRegistered": False,
         "modernGraphValid": False, "oldGraphValid": False, "modernRootUnityNull": True,
         "oldRootUnityNull": True, "meshLeasePresent": False, "allGloamfinResourcesUnityNull": True,
         "resources": resources},
    ]


def state_trigger(sequence, frame_no, trigger, current, next_state):
    result = callback(sequence, frame_no, "CharacterEventListener.CombatTrigger", -1,
                      trigger=trigger, force=False,
                      animator={"currentState": current, "nextState": next_state})
    result["source"] = "passive-native-state-callback"
    return result


def incoming_attack(frame_no):
    damage = {"attackerFid": {"photonId": 1, "turnIndex": 0},
              "victimFid": {"photonId": -1, "turnIndex": 0},
              "damage": 10, "newHealth": 0, "attackResponse": "Death",
              "proficiency": "None", "proficiencySuccess": False}
    return {"sequence": 0, "frame": frame_no, "realtime": frame_no / 60.0,
            "source": "passive-native-engage-attack", "kind": "DamageCalculator.StartEngageAttack",
            "attackerInstanceId": 10, "attackerFid": {"photonId": 1, "turnIndex": 0},
            "targetInstanceId": 20, "targetFid": {"photonId": -1, "turnIndex": 0},
            "slotSuccessPercent": 1.0, "focusedSlots": 0, "proficiency": "None",
            "consumable": False, "cheatType": "None", "boundToDamage": True,
            "respondToHitObserved": True, "damage": [damage], "finalized": True, "exception": None}


def lethal_response(sequence, frame_no):
    event = callback(sequence, frame_no, "CharacterDummy.RespondToHit", -1,
                     dummyInstanceId=20, fid={"photonId": -1, "turnIndex": 0}, mainVictim=True,
                     preHealth=10, postHealth=0,
                     damageInfo={"attackerFid": {"photonId": 1, "turnIndex": 0},
                                 "victimFid": {"photonId": -1, "turnIndex": 0},
                                 "damage": 10, "newHealth": 0, "attackResponse": "Death",
                                 "proficiency": "None", "proficiencySuccess": False},
                     animator={"currentState": "IDLE", "nextState": "DEATH"},
                     incomingAttackSequence=0, incomingAttackCheat="None",
                     incomingAttackProficiency="None", incomingAttackConsumable=False)
    event["source"] = "passive-native-state-callback"
    return event


def terminal_authority():
    return [
        {"sequence": 0, "frame": 1, "realtime": 1 / 60.0,
         "source": "passive-native-terminal-authority", "kind": "EncounterSessionMC.CombatCycleEnd",
         "preconditions": {"noAlivePlayer": True, "anyAliveEnemy": True, "boatPresent": False,
                           "boatHealth": None, "boatDestroyed": False},
         "finalized": True, "exception": None},
        {"sequence": 1, "frame": 2, "realtime": 2 / 60.0,
         "source": "passive-native-terminal-authority", "kind": "EncounterSession.StartEndCombatSequence",
         "ackID": "Wait for Return To Overworld", "playEnemyVictory": True, "waitTime": 2.0,
         "finalized": True, "exception": None},
    ]


def make_report(run, frames, callbacks=None, state_callbacks=None, incoming=None, terminal=None):
    snapshot = pins()
    callbacks = callbacks or []
    starts = [value for value in callbacks if value["kind"] == "CharacterDummy.PlayAttackSequence"]
    attack_windows = [{"id": value["attackWindow"], "variant": value["variant"], "completed": True,
                       "primary": value["primary"]["victimFid"], "secondary": None, "tertiary": None}
                      for value in starts]
    return {"ok": True, "schema": "ftkmf.kraken-production-adapter-observation.v1",
            "status": "natural-teardown-observed", "error": None, "overflow": False, "identityDrift": False,
            "pinsAtArm": snapshot, "pinsLastChecked": copy.deepcopy(snapshot),
            "route": {"topologyGroup": "6a28ac3cf4523c24", "resourcePrefab": "enkrakenhead",
                      "registeredEnemy": REGISTERED_ENEMY, "nativeEnemy": NATIVE_ENEMY,
                      "rendererPath": "krakenHead", "sourceRendererId": 121260,
                      "sourceRendererIdKind": "serialized_path_id", "campaignRun": run,
                      "expectedTerminal": RUNS[run]},
            "authority": {"attackEvidenceSource": "passive_native_callbacks", "observerOnly": True,
                          "directAnimatorPlayUsed": False, "directAnimatorSetTriggerUsed": False,
                          "destructiveCleanupUsed": False},
            "frames": sorted(frames, key=lambda value: value["frame"]), "nativeCallbacks": callbacks,
            "nativeStateCallbacks": state_callbacks or [], "attackWindows": attack_windows,
            "incomingAttacks": incoming or [], "terminalAuthority": terminal or [],
            "cleanup": cleanup(), "cleanupCounts": {"effectiveDisposeOwned": 1, "effectiveSamplerDispose": 2,
                "effectiveDisableForOwner": 0, "teardownObserved": True, "teardownStarted": True}}


def campaign_fixture():
    attack_frames, starts, number = [], {}, 10
    for proficiency in PROFICIENCIES:
        starts[proficiency] = number
        attack_frames += [frame(number, "IDLE", "ATTACK"), frame(number + 9, "ATTACK", "IDLE"),
                          frame(number + 12, "IDLE")]
        number += 20
    callbacks = attack_callbacks(starts)
    failed_window = len(PROFICIENCIES)
    failed_callbacks = copy.deepcopy(callbacks[:12])
    for offset, event in enumerate(failed_callbacks):
        event["sequence"] = len(callbacks) + offset
        event["frame"] = number + offset
        event["realtime"] = event["frame"] / 60.0
        event["attackWindow"] = failed_window
    failed_callbacks[0]["primary"]["proficiencySuccess"] = False
    callbacks.extend(failed_callbacks)
    attack_frames += [frame(number, "IDLE", "ATTACK"), frame(number + 9, "ATTACK", "IDLE"),
                      frame(number + 12, "IDLE")]
    number += 20
    for state_name in ("DEFEND", "DAMAGED"):
        attack_frames += [frame(number, "IDLE", state_name), frame(number + 1, state_name, "IDLE")]; number += 3
    attack_frames += [frame(number, "IDLE", "DEATH"), frame(number + 1, "DEATH")]
    reports = [make_report("native-combat-death", attack_frames, callbacks,
                           [lethal_response(0, number), state_trigger(1, number, "Death", "IDLE", "DEATH")],
                           incoming=[incoming_attack(number - 1)]),
               make_report("enemy-victory-terminal", [frame(1, "IDLE", "VICTORY"), frame(2, "VICTORY")],
                           state_callbacks=[state_trigger(0, 1, "Victory", "IDLE", "VICTORY")],
                           terminal=terminal_authority())]
    return {"schema": "ftkmf.kraken-production-adapter-campaign.v1", "expectedBinaries": BINARIES}, reports


class KrakenProductionCampaignTests(unittest.TestCase):
    def test_complete_campaign_passes(self):
        campaign, reports = campaign_fixture()
        result = verify_campaign(campaign, reports)
        self.assertEqual(result["serializedStateCount"], 15)
        self.assertEqual(set(result["requiredNaturalStates"]), REQUIRED_NATURAL_STATES)
        self.assertEqual(set(result["nativeKrakenProficiencies"]), REQUIRED_PROFICIENCIES)
        self.assertEqual(result["nativeKrakenFailedProficiencyAttempts"], 1)
        self.assertEqual(result["unusedIncomingAttackCalculations"], 0)
        self.assertIn("ATTACKCRIT", result["unexercisedStructuralRoles"])
        self.assertIn("ATTACKPROF", result["unexercisedStructuralRoles"])
        self.assertIn("PASSIVE VICTORY", result["unexercisedStructuralRoles"])
        self.assertIn("DEATHLIGHT", result["unexercisedStructuralRoles"])

    def test_all_four_native_kraken_proficiencies_are_required(self):
        campaign, reports = campaign_fixture()
        starts = [event for event in reports[0]["nativeCallbacks"]
                  if event["kind"] == "CharacterDummy.PlayAttackSequence"
                  and event["primary"]["proficiencySuccess"]]
        starts[-1]["primary"]["proficiency"] = PROFICIENCIES[0]
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_failed_native_proficiency_attempt_is_preserved_without_invalidating_attack(self):
        campaign, reports = campaign_fixture()
        result = verify_campaign(campaign, reports)
        self.assertEqual(result["nativeKrakenFailedProficiencyAttempts"], 1)

    def test_native_attackprof_request_and_attack_override_are_required(self):
        campaign, reports = campaign_fixture()
        start = next(event for event in reports[0]["nativeCallbacks"]
                     if event["kind"] == "CharacterDummy.PlayAttackSequence")
        start["override"] = "AttackProf"
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_terminal_combat_trigger_is_required(self):
        campaign, reports = campaign_fixture(); reports[1]["nativeStateCallbacks"].clear()
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_killsingle_cannot_satisfy_ordinary_lethal_death(self):
        campaign, reports = campaign_fixture()
        reports[0]["incomingAttacks"][0]["cheatType"] = "KillSingle"
        lethal = next(event for event in reports[0]["nativeStateCallbacks"]
                      if event["kind"] == "CharacterDummy.RespondToHit")
        lethal["incomingAttackCheat"] = "KillSingle"
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_hit_requires_exact_ordered_incoming_damage_authority(self):
        campaign, reports = campaign_fixture()
        reports[0]["incomingAttacks"][0]["damage"][0]["damage"] = 9
        with self.assertRaises(ValueError):
            verify_campaign(campaign, reports)

    def test_unused_nonlethal_incoming_calculation_is_reported(self):
        campaign, reports = campaign_fixture()
        extra = copy.deepcopy(reports[0]["incomingAttacks"][0])
        extra["sequence"] = 1
        extra["frame"] -= 1
        extra["damage"][0]["damage"] = 1
        extra["damage"][0]["newHealth"] = 9
        extra["damage"][0]["attackResponse"] = "Damaged"
        extra.pop("respondToHitObserved", None)
        reports[0]["incomingAttacks"].insert(0, extra)
        reports[0]["incomingAttacks"][1]["sequence"] = 2
        lethal = next(event for event in reports[0]["nativeStateCallbacks"]
                      if event["kind"] == "CharacterDummy.RespondToHit")
        lethal["incomingAttackSequence"] = 2
        result = verify_campaign(campaign, reports)
        self.assertEqual(result["unusedIncomingAttackCalculations"], 1)

    def test_enemy_victory_requires_native_party_loss_preconditions(self):
        campaign, reports = campaign_fixture()
        reports[1]["terminalAuthority"][0]["preconditions"]["noAlivePlayer"] = False
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_destroyed_boat_cannot_substitute_for_party_loss(self):
        campaign, reports = campaign_fixture()
        reports[1]["terminalAuthority"][0]["preconditions"]["boatDestroyed"] = True
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_single_report_cannot_claim_campaign(self):
        campaign, reports = campaign_fixture()
        with self.assertRaises(ValueError): verify_campaign(campaign, reports[:1])

    def test_serialized_path_id_kind_is_required(self):
        campaign, reports = campaign_fixture(); reports[0]["route"]["sourceRendererIdKind"] = "runtime_instance_id"
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_registered_custom_enemy_is_required(self):
        campaign, reports = campaign_fixture(); reports[0]["route"]["registeredEnemy"] = NATIVE_ENEMY
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_registered_route_pin_is_required(self):
        campaign, reports = campaign_fixture(); reports[0]["pinsAtArm"]["registeredRoute"]["key"] = NATIVE_ENEMY
        reports[0]["pinsLastChecked"] = copy.deepcopy(reports[0]["pinsAtArm"])
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_appearance_requires_old_single_sampler(self):
        campaign, reports = campaign_fixture(); appearance = frame(500, "IDLE", "OverworldAppear")
        reports[0]["frames"].append(appearance); reports[0]["frames"].sort(key=lambda value: value["frame"])
        appearance["oldSampler"]["inputs"][3]["weight"] = .5
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_two_appearance_contributors_rejected(self):
        campaign, reports = campaign_fixture(); reports[0]["frames"].append(frame(500, "OverworldAppear", "OverworldAppear"))
        reports[0]["frames"].sort(key=lambda value: value["frame"])
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_transition_endpoint_raw_weights_are_preserved(self):
        campaign, reports = campaign_fixture()
        transition = next(value for value in reports[0]["frames"]
                          if value["current"]["state"] == "IDLE" and value["next"]
                          and value["next"]["state"] == "ATTACK")
        transition["current"]["clips"][0]["weight"] = 1.0
        transition["next"]["clips"][0]["weight"] = 0.0
        current_index = CLIPS.index(transition["current"]["clips"][0]["name"])
        next_index = 5 + CLIPS.index(transition["next"]["clips"][0]["name"])
        transition["modernSampler"]["inputs"][current_index]["weight"] = 1.0
        transition["modernSampler"]["inputs"][next_index]["weight"] = 0.0
        self.assertEqual(verify_campaign(campaign, reports)["serializedStateCount"], 15)

    def test_unassigned_callback_rejected(self):
        campaign, reports = campaign_fixture(); reports[0]["nativeCallbacks"][1]["attackWindow"] = -1
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_pinned_outside_attack_foley_is_accounted(self):
        campaign, reports = campaign_fixture()
        event = callback(1000, 1000, "CharacterEventListener.Foley", -1, sound="PLAY_VO_TAUNT",
                         animator={"currentState": "IDLE", "nextState": None})
        event["source"] = "passive-native-state-callback"
        reports[0]["nativeStateCallbacks"].append(event)
        self.assertEqual(verify_campaign(campaign, reports)["serializedStateCount"], 15)

    def test_wrong_outside_attack_foley_rejected(self):
        campaign, reports = campaign_fixture()
        event = callback(1000, 1000, "CharacterEventListener.Foley", -1, sound="PLAY_VO_ATTACK",
                         animator={"currentState": "IDLE", "nextState": None})
        event["source"] = "passive-native-state-callback"
        reports[0]["nativeStateCallbacks"].append(event)
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_respond_to_dodge_callback_can_precede_a_non_dodge_result(self):
        campaign, reports = campaign_fixture()
        event = callback(1000, 1000, "CharacterDummy.RespondToDodge", -1,
                         damageInfo={"attackResponse": "Damaged"},
                         animator={"currentState": "IDLE", "nextState": None})
        event["source"] = "passive-native-state-callback"
        reports[0]["nativeStateCallbacks"].append(event)
        self.assertEqual(verify_campaign(campaign, reports)["serializedStateCount"], 15)

    def test_actual_dodge_callback_requires_dodge_transition(self):
        campaign, reports = campaign_fixture()
        event = callback(1000, 1000, "CharacterDummy.RespondToDodge", -1,
                         damageInfo={"attackResponse": "Dodge"},
                         animator={"currentState": "IDLE", "nextState": None})
        event["source"] = "passive-native-state-callback"
        reports[0]["nativeStateCallbacks"].append(event)
        with self.assertRaises(ValueError):
            verify_campaign(campaign, reports)

    def test_unused_modern_sampler_input_must_be_zero(self):
        campaign, reports = campaign_fixture()
        reports[0]["frames"][0]["modernSampler"]["inputs"][2]["weight"] = .1
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_effective_adapter_disposal_event_is_required(self):
        campaign, reports = campaign_fixture(); reports[0]["cleanup"].pop(0)
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_effective_adapter_disable_rejected(self):
        campaign, reports = campaign_fixture()
        reports[0]["cleanupCounts"]["effectiveDisableForOwner"] = 1
        reports[0]["cleanup"].insert(0, {"sequence": -1,
            "kind": "LegacyKrakenResourceAdapterLease.DisableForOwner", "effective": True,
            "reason": "System.InvalidOperationException: fixture", "finalized": True, "exception": None})
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_disabled_adapter_component_rejected(self):
        campaign, reports = campaign_fixture(); reports[0]["frames"][0]["adapter"]["enabled"] = False
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_exact_weapon_argument_required(self):
        campaign, reports = campaign_fixture()
        event = next(value for value in reports[0]["nativeCallbacks"] if value["kind"] == "CharacterEventListener.PlayWeaponAnimation")
        event["argument"] = "attack"
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_callback_sequence_is_exact(self):
        campaign, reports = campaign_fixture(); reports[0]["nativeCallbacks"].pop(6)
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_binary_receipt_pin_required(self):
        campaign, reports = campaign_fixture(); campaign["expectedBinaries"]["runtimeHelper"] = "9" * 64
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_campaign_loader_binds_reports_to_deployment_receipt(self):
        campaign, reports = campaign_fixture()
        with tempfile.TemporaryDirectory() as temporary:
            root = Path(temporary)
            receipt = {"status": "VERIFIED_COMPLETE", "new": {
                "BepInEx/plugins/FTKModFramework.dll": {"sha256": BINARIES["framework"]},
                "BepInEx/plugins/FtkRuntimeModelTest.dll": {"sha256": BINARIES["runtimeHelper"]},
                "BepInEx/plugins/FtkRuntimeModelTestContent.dll": {"sha256": BINARIES["runtimeContent"]}}}
            receipt_path = root / "deployment.json"; receipt_path.write_text(json.dumps(receipt))
            campaign["deploymentReceipt"] = {"path": receipt_path.name,
                "sha256": hashlib.sha256(receipt_path.read_bytes()).hexdigest()}
            campaign["reports"] = []
            for index, report in enumerate(reports):
                report_path = root / (str(index) + ".json"); report_path.write_text(json.dumps(report))
                campaign["reports"].append({"campaignRun": report["route"]["campaignRun"], "path": report_path.name,
                    "sha256": hashlib.sha256(report_path.read_bytes()).hexdigest()})
            campaign_path = root / "campaign.json"; campaign_path.write_text(json.dumps(campaign))
            loaded, loaded_reports = load_campaign(campaign_path)
            self.assertEqual(verify_campaign(loaded, loaded_reports)["reportCount"], 2)

    def test_terminal_cannot_return_to_idle(self):
        campaign, reports = campaign_fixture(); reports[0]["frames"].append(frame(1000, "DEATH", "IDLE"))
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_all_lease_resources_must_be_destroyed(self):
        campaign, reports = campaign_fixture(); reports[0]["cleanup"][-1]["resources"][0]["unityNull"] = False
        with self.assertRaises(ValueError): verify_campaign(campaign, reports)

    def test_runtime_observer_is_passive_and_avoids_per_frame_file_hashing(self):
        source = (Path(__file__).parent / "runtime-test" / "KrakenProductionAdapterObservation.cs").read_text()
        tick = source.split("void KrakenProductionAdapterTick()", 1)[1].split("JObject KrakenProductionAdapterState()", 1)[0]
        self.assertNotIn("KrakenProductionPins()", tick)
        self.assertNotIn("Animator.Play(", source); self.assertNotIn(".SetTrigger(", source); self.assertNotIn("Destroy(", source)
        self.assertIn("KrakenProductionObserverFailure", source)

    def test_runtime_observer_accepts_negative_unity_instance_ids(self):
        source = (Path(__file__).parent / "runtime-test" / "KrakenProductionAdapterObservation.cs").read_text()
        validator = source.split("static int KrakenProductionRequiredId", 1)[1].split(
            "static string KrakenProductionRepositoryRoot", 1)[0]
        self.assertIn("(long)value == 0", validator)
        self.assertIn("Int32.MinValue", validator)
        self.assertNotIn("(long)value < 1", validator)

    def test_runtime_observer_deduplicates_fifteen_state_clip_references(self):
        source = (Path(__file__).parent / "runtime-test" / "KrakenProductionAdapterObservation.cs").read_text()
        inventory = source.split("static Dictionary<string, AnimationClip> KrakenProductionControllerClips", 1)[1].split(
            "static void KrakenProductionRequireSamplerTopology", 1)[0]
        self.assertIn("values.Length != KrakenProductionStates.Length", inventory)
        self.assertIn("result.TryGetValue(clip.name, out existing)", inventory)
        self.assertIn("ReferenceEquals(existing, clip)", inventory)
        self.assertIn("result.Count != KrakenProductionClipNames.Length", inventory)

    def test_runtime_observer_validates_every_frame_but_retains_bounded_checkpoints(self):
        source = (Path(__file__).parent / "runtime-test" / "KrakenProductionAdapterObservation.cs").read_text()
        recorder = source.split("void KrakenProductionRecordFrame", 1)[1].split(
            "void KrakenProductionRecordCallback", 1)[0]
        require_index = recorder.index("KrakenProductionRequireOwner(arm)")
        sampler_index = recorder.index("KrakenProductionSamplerView(arm, arm.modernWatch)")
        cadence_index = recorder.index("Time.frameCount - arm.lastRecordedFrame >= 30")
        self.assertLess(require_index, cadence_index)
        self.assertLess(sampler_index, cadence_index)
        self.assertIn("transition != arm.lastTransition", recorder)
        self.assertIn("currentHash != arm.lastCurrentHash", recorder)
        self.assertIn("if (!changed && !periodic) return", recorder)

    def test_runtime_observer_stops_cleanup_polling_after_settlement(self):
        source = (Path(__file__).parent / "runtime-test" / "KrakenProductionAdapterObservation.cs").read_text()
        tick = source.split("void KrakenProductionAdapterTick()", 1)[1].split(
            "JObject KrakenProductionAdapterState()", 1)[0]
        self.assertIn("if (arm.teardownObserved) return;", tick)

    def test_runtime_observer_captures_cheat_authority_before_damage_serialization(self):
        source = (Path(__file__).parent / "runtime-test" / "KrakenProductionAdapterObservation.cs").read_text()
        prefix = source.split("static void KrakenProductionStartEngagePrefix", 1)[1].split(
            "static Exception KrakenProductionStartEngageFinalizer", 1)[0]
        binding = source.split("static void KrakenProductionBindIncomingAttack", 1)[1].split(
            "static JObject KrakenProductionCallbackState", 1)[0]
        self.assertIn("SlotControl.AttackCheatType _cheatType", prefix)
        self.assertIn('{ "cheatType", _cheatType.ToString() }', prefix)
        self.assertNotIn("m_CheatAttack", source)
        self.assertIn("if (ReferenceEquals(attacker, arm.owner)) return;", binding)
        self.assertIn("arm.incomingDamage[value] = incoming", binding)
        self.assertIn("incomingAttackCheat", source)
        resolver = source.split("static KrakenProductionIncomingAttack KrakenProductionResolveIncomingAttack", 1)[1].split(
            "static JObject KrakenProductionCallbackState", 1)[0]
        self.assertIn("JToken.DeepEquals(calculated[j], serialized)", resolver)
        self.assertIn("candidate.responded", resolver)

    def test_runtime_observer_resolves_static_engage_attack_with_static_flags(self):
        source = (Path(__file__).parent / "runtime-test" / "KrakenProductionAdapterObservation.cs").read_text()
        hooks = source.split("void InstallKrakenProductionAdapterHooks", 1)[1].split(
            "static void KrakenProductionLateUpdatePostfix", 1)[0]
        lookup = hooks.split('GetMethod("StartEngageAttack",', 1)[1].split(") }, null);", 1)[0]
        self.assertIn("Statics, null", lookup)
        self.assertNotIn("Members, null", lookup)

    def test_runtime_observer_captures_native_party_loss_authority(self):
        source = (Path(__file__).parent / "runtime-test" / "KrakenProductionAdapterObservation.cs").read_text()
        snapshot = source.split("static JObject KrakenProductionPartyLossSnapshot", 1)[1].split(
            "static void KrakenProductionCombatCycleEndPrefix", 1)[0]
        self.assertIn('KrakenProductionField(sessionMc, "m_AllCombtatantsAlive")', snapshot)
        self.assertIn('KrakenProductionField(sessionMc, "m_EnemyStatuses")', snapshot)
        self.assertIn('{ "boatDestroyed", boatPresent && boatHealth <= 0f }', snapshot)
        self.assertIn('arm.campaignRun != "enemy-victory-terminal"', source)
        self.assertIn('"EncounterSession.StartEndCombatSequence"', source)


if __name__ == "__main__":
    unittest.main()
