#!/usr/bin/env python3
"""Verify a pinned, multi-run Kraken production-adapter campaign offline."""
import argparse
import hashlib
import json
import math
import re
from pathlib import Path

SCHEMA = "ftkmf.kraken-production-adapter-observation.v1"
CAMPAIGN_SCHEMA = "ftkmf.kraken-production-adapter-campaign.v1"
GAME_SHA256 = "94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8"
RESOURCES_SHA256 = "e33be4e6a3add9c15bc1d778f8b2162c8d9835f62b2764b75b30d49deb036117"
CONTRACT_SHA256 = "5acb620409e3a6a8807870f51f4b5a8435f83681623934ea353b1c4702ee7333"
ADAPTER_SHA256 = "393447685551eedfff19fb63e2a8384be56627375ce1c7a670de24850a475c5b"
STATE_COVERAGE_SHA256 = "a9c76ef8c6822379ce19b70742b0d0b53b79460cd56ed584cb97859848530efb"
CONTROLLER_SHA256 = "b4a0141ad1d37dd6815fa1a3851e9942a87c31c1bebd360690d7521e1db5779b"
DECOMPILE_SHA256 = "b1c351a88947cefc9674dee9ac1e24f5278b825545b7ac5b8a92c0ff5cf3edb0"
ADAPTER_TEST_SHA256 = "7c5fbec363e892692f8d98a397944bcadca22ba658e6d32e9b7de93eb81c36a3"
MANIFEST_SHA256 = "8ae418087754cb73da2e73d42ad31802565a36adb6849d720b226e3b121db84e"
GLB_SHA256 = "d4be118d44d63f48a12e9c5fbde9dcac5d4b9cc73787182be41ec39030e7dc00"
PNG_SHA256 = "44ae801b21329874afa84c98ffcc455e529c25ef61da0f0420a3536b4c0a8d9a"
PROFILE_SHA256 = "bcf8ee395f39d99759a36a9f48dbe19810db7e3d42fa98e285593a3ec0748495"
CATALOG_SHA256 = "bcc03747bfc293e47645b0548a7f6eb9d4f660084e422fb8e20aee6a33b14cee"
REGISTERED_ENEMY = "ftkmf_modeltest_gloamfin_kraken_legacy"
NATIVE_ENEMY = "krakenHead"
COMBAT_PROFILE = "03a4df71df2226d0ec711f80c3f1f3e0922c211ff18d3efb797a6501efe423fa"
EPSILON = 1e-5

STATES = {
    "IDLE": ("krakenIdle", True), "INTRO": ("krakenIdle", True),
    "DEFEND": ("krakenDamage", False), "ATTACK": ("krakenAttack", False),
    "VICTORY": ("krakenDisappear", False), "PASSIVE VICTORY": ("krakenDisappear", False),
    "DAMAGEDHEAVY": ("krakenDamage", False), "DEATH": ("krakenDisappear", False),
    "ATTACKCRIT": ("krakenAttack", False), "ATTACKPROF": ("krakenAttack", False),
    "DODGE": ("krakenDamage", False), "DAMAGED": ("krakenDamage", False),
    "DAMAGED STUN": ("krakenDamage", False), "OverworldAppear": ("kraken_appear", False),
    "DEATHLIGHT": ("krakenDisappear", False),
}
CLIPS = ("krakenIdle", "krakenDamage", "krakenDisappear", "kraken_appear", "krakenAttack")
IDLE_OUTBOUND = {"DEFEND", "ATTACK", "VICTORY", "PASSIVE VICTORY", "DAMAGEDHEAVY",
                 "ATTACKCRIT", "ATTACKPROF", "DODGE", "DAMAGED", "DAMAGED STUN"}
NONTERMINAL_RETURNS = {"INTRO", "DEFEND", "ATTACK", "DAMAGEDHEAVY", "ATTACKCRIT",
                       "ATTACKPROF", "DODGE", "DAMAGED", "DAMAGED STUN", "OverworldAppear"}
ANY_STATE_TARGETS = {"INTRO", "DEATH", "DEATHLIGHT", "OverworldAppear"}
SELF_INTERRUPTS = {"INTRO", "DEATH", "DEATHLIGHT"}
TERMINALS = {"VICTORY", "PASSIVE VICTORY", "DEATH", "DEATHLIGHT"}
RUNS = {"native-combat-death": "DEATH", "enemy-victory-terminal": "VICTORY"}
REQUIRED_NATURAL_STATES = {"IDLE", "ATTACK", "DEFEND", "DAMAGED", "DEATH", "VICTORY"}
REQUIRED_NATURAL_EDGES = {
    ("IDLE", "ATTACK"), ("ATTACK", "IDLE"),
    ("IDLE", "DEFEND"), ("DEFEND", "IDLE"),
    ("IDLE", "DAMAGED"), ("DAMAGED", "IDLE"),
}
REQUIRED_PROFICIENCIES = {
    "enKrakenResistUp", "enKrakenInterrupt", "enKrakenConfuse", "enKrakenArmorUp"
}
EFFECTIVE_ATTACK_VARIANT = "Attack"
WATER = "PLAY_SFX_WATER_SUBREMERGE"
VOICE = "PLAY_VO_ATTACK"
STATE_FOLEY = {
    "IDLE": {"PLAY_VO_TAUNT"}, "INTRO": {"PLAY_VO_TAUNT"},
    "DEFEND": {"PLAY_VO_DAMAGE"}, "DAMAGEDHEAVY": {"PLAY_VO_DAMAGE"},
    "DODGE": {"PLAY_VO_DAMAGE"}, "DAMAGED": {"PLAY_VO_DAMAGE"}, "DAMAGED STUN": {"PLAY_VO_DAMAGE"},
    "VICTORY": {"PLAY_FLY_KRAKEN_DEATH", "PLAY_VO_DEATH"},
    "PASSIVE VICTORY": {"PLAY_FLY_KRAKEN_DEATH", "PLAY_VO_DEATH"},
    "DEATH": {"PLAY_FLY_KRAKEN_DEATH", "PLAY_VO_DEATH"},
    "DEATHLIGHT": {"PLAY_FLY_KRAKEN_DEATH", "PLAY_VO_DEATH"},
    "OverworldAppear": {"PLAY_VO_AMB_APPEAR", WATER},
}
STATE_TRIGGERS = {"Intro": "INTRO", "Defend": "DEFEND", "Victory": "VICTORY",
                  "PassiveVictory": "PASSIVE VICTORY", "DamagedHeavy": "DAMAGEDHEAVY",
                  "Death": "DEATH", "Dodge": "DODGE", "Damaged": "DAMAGED", "Stun": "DAMAGED STUN",
                  "Appear": "OverworldAppear", "DeathLight": "DEATHLIGHT"}
BINARY_KEYS = {"framework", "runtimeHelper", "runtimeContent"}


def require(condition, message):
    if not condition:
        raise ValueError(message)


def number(value, label):
    require(isinstance(value, (int, float)) and not isinstance(value, bool) and math.isfinite(value),
            "finite number required: " + label)
    return float(value)


def digest(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def assembly_hash(pin, label):
    require(isinstance(pin, dict) and isinstance(pin.get("assemblyFileSha256"), str),
            "assembly identity required: " + label)
    return pin["assemblyFileSha256"]


def check_pins(report, expected_binaries):
    before, after = report.get("pinsAtArm"), report.get("pinsLastChecked")
    require(isinstance(before, dict) and before == after, "arm and final pin snapshots must be exact")
    require(isinstance(expected_binaries, dict) and set(expected_binaries) == BINARY_KEYS,
            "exact expected binary pins required")
    for key in BINARY_KEYS:
        require(assembly_hash(before.get(key), key) == expected_binaries[key], "wrong binary pin: " + key)
    fixed = {
        "game": ("assemblyFileSha256", GAME_SHA256), "resourcesAssets": ("sha256", RESOURCES_SHA256),
        "contract": ("sha256", CONTRACT_SHA256), "adapterSource": ("sha256", ADAPTER_SHA256),
        "stateCoverage": ("sha256", STATE_COVERAGE_SHA256),
        "controllerExtraction": ("sha256", CONTROLLER_SHA256), "decompile": ("sha256", DECOMPILE_SHA256),
        "adapterImplementationTest": ("sha256", ADAPTER_TEST_SHA256),
        "routeProfile": ("sha256", PROFILE_SHA256), "modelCatalog": ("sha256", CATALOG_SHA256),
    }
    for key, (field, expected) in fixed.items():
        require(isinstance(before.get(key), dict) and before[key].get(field) == expected, "wrong pin: " + key)
    manifest = before.get("gloamfinManifest")
    require(isinstance(manifest, dict) and manifest.get("sha256") == MANIFEST_SHA256, "wrong Gloamfin manifest pin")
    require(manifest.get("glb", {}).get("file") == "gloamfin.glb" and
            manifest.get("glb", {}).get("sha256") == GLB_SHA256, "wrong Gloamfin GLB pin")
    require(manifest.get("png", {}).get("file") == "gloamfin_basecolor.png" and
            manifest.get("png", {}).get("sha256") == PNG_SHA256, "wrong Gloamfin texture pin")
    registration = before.get("contentRegistration")
    require(isinstance(registration, dict) and
            isinstance(registration.get("sha256"), str) and
            re.fullmatch(r"[0-9a-f]{64}", registration["sha256"]),
            "current content registration pin required")
    registered = before.get("registeredRoute")
    require(isinstance(registered, dict) and registered.get("key") == REGISTERED_ENEMY and
            registered.get("baseEnemy") == NATIVE_ENEMY and registered.get("resourcePrefab") == "enkrakenhead" and
            registered.get("combatProfile") == COMBAT_PROFILE and registered.get("bindingKind") == "explicit-plural" and
            registered.get("status") == "registered_spawn_validation_pending" and
            isinstance(registered.get("id"), int) and not isinstance(registered["id"], bool) and registered["id"] > 0,
            "wrong registered Gloamfin production route")


def check_state(state, role):
    require(isinstance(state, dict), "missing " + role + " state")
    name = state.get("state")
    require(name in STATES and state.get("certified") is True, "uncertified state: " + role)
    clip, loop = STATES[name]
    require(state.get("expectedClip") == clip and state.get("loop") is loop, "state contract changed: " + name)
    for key in ("normalizedTime", "length", "speed", "speedMultiplier"):
        number(state.get(key), role + "." + key)
    require(state["speed"] == 1 and state["speedMultiplier"] == 1, "unsupported state speed: " + name)
    clips = state.get("clips")
    require(isinstance(clips, list) and len(clips) == 1, "one raw clip required: " + role)
    raw = clips[0]
    require(raw.get("name") == clip and raw.get("loop") is loop, "wrong raw clip: " + name)
    require(number(raw.get("weight"), role + ".weight") >= 0, "negative raw clip weight")
    require(abs(number(state["length"], role + ".length") - number(raw.get("length"), role + ".clipLength")) <= EPSILON,
            "state length convention changed: " + name)
    require(isinstance(state.get("fullPathHash"), int), "state fullPathHash must be an integer")
    return name, raw


def expected_seconds(state):
    phase = number(state["normalizedTime"], "normalizedTime")
    phase = phase - math.floor(phase) if state["loop"] else min(1.0, max(0.0, phase))
    return phase * number(state["length"], "length")


def sampler_inputs(sampler, label):
    require(isinstance(sampler, dict), "missing " + label)
    require(sampler.get("rootUnityNull") is False and sampler.get("samplerDisposed") is False and
            sampler.get("graphValid") is True and sampler.get("graphMode") == "Manual" and
            sampler.get("graphOutputCount") == 1, "unhealthy " + label)
    inputs = sampler.get("inputs")
    require(isinstance(inputs, list) and len(inputs) == 10, "two five-clip banks required: " + label)
    for index, item in enumerate(inputs):
        require(item.get("input") == index and item.get("clip") == CLIPS[index % 5] and
                item.get("bank") == ("current" if index < 5 else "next") and item.get("valid") is True,
                "sampler topology changed: " + label)
        require(number(item.get("seconds"), label + ".seconds") >= 0 and
                0 <= number(item.get("weight"), label + ".weight") <= 1, "invalid sampler input")
    return inputs


def require_single_sampler_input(inputs, state, label):
    index = CLIPS.index(state["clips"][0]["name"])
    for item in inputs:
        expected_weight = 1.0 if item["input"] == index else 0.0
        require(abs(item["weight"] - expected_weight) <= EPSILON, "wrong single-input policy: " + label)
        if item["input"] != index:
            require(abs(item["seconds"]) <= EPSILON, "inactive single-input clock is not zero: " + label)
    require(abs(inputs[index]["seconds"] - expected_seconds(state)) <= 1e-4,
            "wrong single-input clock: " + label)


def check_sampler_policy(frame, current, next_state, transition):
    modern = sampler_inputs(frame.get("modernSampler"), "modernSampler")
    old = sampler_inputs(frame.get("oldSampler"), "oldSampler")
    current_appearance = current["state"] == "OverworldAppear"
    next_appearance = transition and next_state["state"] == "OverworldAppear"
    require(not (current_appearance and next_appearance), "two appearance contributors are unsupported")
    if current_appearance or next_appearance:
        appearance = current if current_appearance else next_state
        main = next_state if current_appearance else current
        require_single_sampler_input(old, appearance, "oldSampler appearance")
        if transition:
            require_single_sampler_input(modern, main, "modernSampler main")
        return
    current_index = CLIPS.index(current["clips"][0]["name"])
    require(abs(modern[current_index]["seconds"] - expected_seconds(current)) <= 1e-4 and
            abs(modern[current_index]["weight"] - current["clips"][0]["weight"]) <= EPSILON,
            "modern current bank does not preserve native role")
    if transition:
        next_index = 5 + CLIPS.index(next_state["clips"][0]["name"])
        require(abs(modern[next_index]["seconds"] - expected_seconds(next_state)) <= 1e-4 and
                abs(modern[next_index]["weight"] - next_state["clips"][0]["weight"]) <= EPSILON,
                "modern next bank does not preserve native role")
    else:
        next_index = -1
    for item in modern:
        if item["input"] not in (current_index, next_index):
            require(abs(item["weight"]) <= EPSILON and abs(item["seconds"]) <= EPSILON,
                    "inactive modern sampler input is not zero")


def check_frames(report):
    frames = report.get("frames")
    require(isinstance(frames, list) and frames, "post-LateUpdate trace required")
    observed, transitions, hashes = set(), set(), {}
    previous = -1
    for frame in frames:
        require(frame.get("postLateUpdate") is True and isinstance(frame.get("frame"), int) and frame["frame"] > previous,
                "post-LateUpdate frames must increase")
        previous = frame["frame"]
        current_name, current_clip = check_state(frame.get("current"), "current")
        current = frame["current"]
        observed.add(current_name); hashes.setdefault(current_name, current["fullPathHash"])
        require(hashes[current_name] == current["fullPathHash"], "state hash changed: " + current_name)
        transition = frame.get("inTransition") is True
        next_state = None
        if transition:
            next_name, next_clip = check_state(frame.get("next"), "next")
            next_state = frame["next"]
            observed.add(next_name); hashes.setdefault(next_name, next_state["fullPathHash"])
            require(hashes[next_name] == next_state["fullPathHash"], "state hash changed: " + next_name)
            require(abs(current_clip["weight"] + next_clip["weight"] - 1) <= EPSILON,
                    "transition raw weights must sum to one")
            transitions.add((current_name, next_name))
        else:
            require(frame.get("next") is None and abs(current_clip["weight"] - 1) <= EPSILON,
                    "non-transition current weight must be one")
        flags = frame.get("adapter")
        require(isinstance(flags, dict) and flags.get("configured") is True and flags.get("initialized") is True and
                flags.get("disabled") is False and flags.get("disposed") is False and flags.get("ownerRegistered") is True,
                "adapter must remain healthy on every frame")
        require(flags.get("enabled") is True, "adapter component must remain enabled on every frame")
        check_sampler_policy(frame, current, next_state, transition)
    terminal = report["route"]["expectedTerminal"]
    if terminal is not None:
        require(terminal in observed and any(target == terminal for _, target in transitions),
                "declared terminal was not entered: " + terminal)
        require(not any(source == terminal and target != terminal for source, target in transitions),
                "terminal emitted an outgoing edge: " + terminal)
    return observed, transitions, hashes


def callback_windows(report):
    events = report.get("nativeCallbacks")
    require(isinstance(events, list), "native callback array required")
    windows, previous = {}, -1
    for event in events:
        require(event.get("source") == "passive-native-callback" and event.get("finalized") is True and event.get("exception") is None,
                "callback must be finalized passive evidence")
        sequence = event.get("sequence")
        require(isinstance(sequence, int) and sequence > previous, "callback ordering changed")
        previous = sequence
        key = event.get("attackWindow", -1)
        require(isinstance(key, int) and key >= 0, "unassigned attack callback")
        windows.setdefault(key, []).append(event)
    return windows


def check_state_callbacks(report, transitions):
    events = report.get("nativeStateCallbacks")
    require(isinstance(events, list), "native state callback array required")
    previous = -1
    triggers = set()
    for event in events:
        require(event.get("source") == "passive-native-state-callback" and event.get("attackWindow") == -1 and
                event.get("finalized") is True and event.get("exception") is None,
                "state callback must be finalized passive evidence")
        sequence = event.get("sequence")
        require(isinstance(sequence, int) and sequence > previous, "state callback ordering changed")
        previous = sequence
        animator = event.get("animator")
        require(isinstance(animator, dict), "state callback needs native animator state")
        states = {animator.get("currentState"), animator.get("nextState")} - {None}
        require(states and states <= set(STATES), "state callback has unknown animator state")
        kind = event.get("kind")
        if kind == "CharacterEventListener.Foley":
            require(any(event.get("sound") in STATE_FOLEY.get(state, set()) for state in states),
                    "outside-attack Foley is not a pinned state event")
        elif kind == "CharacterEventListener.CombatTrigger":
            target = STATE_TRIGGERS.get(event.get("trigger"))
            require(target is not None and any(destination == target for _, destination in transitions),
                    "state trigger lacks its native transition")
            triggers.add(event.get("trigger"))
        elif kind == "CharacterDummy.ActionCompleted":
            require(not states.intersection({"ATTACK", "ATTACKCRIT", "ATTACKPROF"}),
                    "attack completion escaped its attack window")
        elif kind == "CharacterDummy.RespondToDodge":
            damage = event.get("damageInfo")
            require(isinstance(damage, dict) and isinstance(damage.get("attackResponse"), str),
                    "dodge-phase callback lacks its damage result")
            if damage["attackResponse"] == "Dodge":
                require(any(destination == "DODGE" for _, destination in transitions),
                        "actual dodge response lacks DODGE transition")
        elif kind == "CharacterDummy.RespondToHit":
            damage = event.get("damageInfo")
            require(isinstance(damage, dict) and isinstance(damage.get("newHealth"), int) and
                    event.get("postHealth") == damage["newHealth"] and
                    any(destination in {"DAMAGED", "DAMAGEDHEAVY", "DAMAGED STUN"} for _, destination in transitions),
                    "hit response lacks damage result and damage transition")
        else:
            raise ValueError("unaccounted native state callback: " + str(kind))
    return triggers


def check_native_combat_death(report):
    incoming = report.get("incomingAttacks")
    require(isinstance(incoming, list) and incoming, "native death run needs incoming attack authority")
    by_sequence, previous = {}, -1
    for event in incoming:
        require(event.get("source") == "passive-native-engage-attack" and
                event.get("kind") == "DamageCalculator.StartEngageAttack" and
                event.get("finalized") is True and event.get("exception") is None,
                "incoming attack must be finalized passive StartEngageAttack evidence")
        sequence = event.get("sequence")
        require(isinstance(sequence, int) and sequence > previous, "incoming attack sequence changed")
        previous = sequence; by_sequence[sequence] = event
        require(event.get("cheatType") == "None" and event.get("consumable") is False and
                event.get("boundToDamage") is True,
                "death campaign permits only bound ordinary non-consumable attacks")
        number(event.get("slotSuccessPercent"), "incoming slotSuccessPercent")
        require(isinstance(event.get("focusedSlots"), int) and event["focusedSlots"] >= 0,
                "focused slot count required")
        damage = event.get("damage")
        require(isinstance(damage, list) and len(damage) == 1 and isinstance(damage[0], dict) and
                damage[0].get("attackerFid") == event.get("attackerFid") and
                damage[0].get("victimFid") == event.get("targetFid"),
                "incoming attack needs its exact single-target damage calculation")

    hits = [event for event in report.get("nativeStateCallbacks", [])
            if event.get("kind") == "CharacterDummy.RespondToHit"]
    require(hits, "native death run needs RespondToHit evidence")
    matched, previous_index = {}, -1
    for hit in hits:
        damage = hit.get("damageInfo")
        require(isinstance(damage, dict) and hit.get("postHealth") == damage.get("newHealth"),
                "RespondToHit needs its applied damage result")
        candidates = [index for index in range(previous_index + 1, len(incoming))
                      if incoming[index]["frame"] <= hit.get("frame", -1) and
                      incoming[index]["damage"][0] == damage]
        require(candidates, "RespondToHit lacks ordered StartEngageAttack damage authority")
        index = candidates[0]
        previous_index = index
        matched[id(hit)] = incoming[index]
        explicit = hit.get("incomingAttackSequence")
        require(explicit is None or explicit == incoming[index]["sequence"],
                "serialized incoming attack link contradicts exact damage authority")
        if explicit is not None:
            require(hit.get("incomingAttackCheat") == incoming[index]["cheatType"] and
                    hit.get("incomingAttackConsumable") == incoming[index]["consumable"] and
                    hit.get("incomingAttackProficiency") == incoming[index]["proficiency"],
                    "serialized incoming attack authority fields changed")
    lethal = []
    for event in hits:
        damage = event.get("damageInfo")
        if damage.get("newHealth") != 0:
            continue
        require(event.get("postHealth") == 0 and damage.get("attackResponse") == "Death" and
                isinstance(damage.get("damage"), int) and damage["damage"] > 0,
                "lethal response data changed")
        authority = matched[id(event)]
        require(authority["sequence"] in by_sequence and authority["cheatType"] == "None" and
                authority["consumable"] is False,
                "lethal response lacks ordinary StartEngageAttack damage authority")
        lethal.append(event)
    require(len(lethal) == 1, "exactly one ordinary lethal Kraken response required")
    return len(incoming) - len(matched)


def check_enemy_victory_authority(report):
    events = report.get("terminalAuthority")
    require(isinstance(events, list) and events, "enemy victory needs terminal authority evidence")
    previous, qualifying_cycle = -1, None
    for event in events:
        require(event.get("source") == "passive-native-terminal-authority" and
                event.get("finalized") is True and event.get("exception") is None,
                "terminal authority event must be finalized passive evidence")
        sequence = event.get("sequence")
        require(isinstance(sequence, int) and sequence > previous, "terminal authority sequence changed")
        previous = sequence
        if event.get("kind") == "EncounterSessionMC.CombatCycleEnd":
            state = event.get("preconditions")
            require(isinstance(state, dict), "CombatCycleEnd preconditions required")
            if (state.get("noAlivePlayer") is True and state.get("anyAliveEnemy") is True and
                    state.get("boatDestroyed") is False):
                qualifying_cycle = sequence
        elif event.get("kind") == "EncounterSession.StartEndCombatSequence":
            require(event.get("playEnemyVictory") is True and event.get("ackID") == "Wait for Return To Overworld" and
                    number(event.get("waitTime"), "victory waitTime") >= 0,
                    "native enemy-victory request changed")
            require(qualifying_cycle is not None and qualifying_cycle < sequence,
                    "enemy victory request lacks preceding ordinary party-loss CombatCycleEnd")
        else:
            raise ValueError("unaccounted terminal authority event: " + str(event.get("kind")))
    require(any(event.get("kind") == "EncounterSession.StartEndCombatSequence" for event in events),
            "native StartEndCombatSequence enemy-victory request required")


def check_attack_window(events):
    starts = [event for event in events if event.get("kind") == "CharacterDummy.PlayAttackSequence"]
    require(len(starts) == 1, "one native PlayAttackSequence required per attack window")
    start = starts[0]; variant = start.get("variant")
    require(variant == EFFECTIVE_ATTACK_VARIANT and start.get("attackAnim") == "AttackProf" and
            start.get("override") == EFFECTIVE_ATTACK_VARIANT and
            start.get("secondary") is None and start.get("tertiary") is None,
            "Kraken attack request, override, or victim count changed")
    primary = start.get("primary")
    require(isinstance(primary, dict) and isinstance(primary.get("victimFid"), dict) and isinstance(primary.get("newHealth"), int),
            "primary victim and newHealth required")
    proficiency = primary.get("proficiency")
    proficiency_success = primary.get("proficiencySuccess")
    require(proficiency in REQUIRED_PROFICIENCIES and isinstance(proficiency_success, bool),
            "credited Kraken attack needs one pinned proficiency result")
    victim, expected_health = primary["victimFid"], primary["newHealth"]
    expected = ["CharacterDummy.PlayAttackSequence", "CharacterEventListener.CombatTrigger",
                "CharacterEventListener.Foley", "CharacterEventListener.PlayWeaponAnimation",
                "CharacterEventListener.Foley", "CharacterEventListener.Dodge", "CharacterDummy.RespondToDodge",
                "CharacterEventListener.AttackHit", "CharacterDummy.RespondToHit",
                "CharacterEventListener.Foley", "CharacterEventListener.Foley",
                "CharacterDummy.ActionCompleted"]
    require([event.get("kind") for event in events] == expected, "native callback sequence changed: " + variant)
    require(events[1].get("trigger") == variant and events[2].get("sound") == WATER and
            events[3].get("argument") == "" and events[4].get("sound") == VOICE and
            events[6].get("mainVictim") is True and events[6].get("fid") == victim and
            events[8].get("mainVictim") is True and events[8].get("fid") == victim and
            events[8].get("postHealth") == expected_health and events[9].get("sound") == WATER and
            events[10].get("sound") == "PLAY_VO_TAUNT",
            "native callback data changed: " + variant)
    return proficiency, proficiency_success, events[0]["frame"], events[9]["frame"], events[-1]["frame"]


def check_attack_authority(report, transitions):
    require(report.get("authority") == {"attackEvidenceSource": "passive_native_callbacks", "observerOnly": True,
            "directAnimatorPlayUsed": False, "directAnimatorSetTriggerUsed": False, "destructiveCleanupUsed": False},
            "attack authority must be passive")
    windows = callback_windows(report)
    declared = report.get("attackWindows")
    require(isinstance(declared, list) and len(declared) == len(windows), "attack window ledger mismatch")
    declared_by_id = {item.get("id"): item for item in declared if isinstance(item, dict)}
    require(set(declared_by_id) == set(windows), "attack window IDs changed")
    successful_proficiencies, failed_attempts = [], 0
    for window_id, events in windows.items():
        proficiency, proficiency_success, start_frame, final_clip_frame, end_frame = check_attack_window(events)
        row = declared_by_id[window_id]
        require(row.get("variant") == EFFECTIVE_ATTACK_VARIANT and row.get("completed") is True and row.get("secondary") is None and
                row.get("tertiary") is None and row.get("primary") == events[0]["primary"]["victimFid"],
                "attack window summary does not match callbacks")
        entry = [frame for frame in report["frames"] if start_frame <= frame["frame"] <= final_clip_frame and
                 frame["current"]["state"] == "IDLE" and frame["next"] and frame["next"]["state"] == "ATTACK"]
        exit_frames = [frame for frame in report["frames"] if final_clip_frame <= frame["frame"] <= end_frame and
                       frame["current"]["state"] == "ATTACK" and frame["next"] and frame["next"]["state"] == "IDLE"]
        idle_frames = [frame for frame in report["frames"] if frame["frame"] >= end_frame and
                       not frame["inTransition"] and frame["current"]["state"] == "IDLE"]
        require(entry and exit_frames and idle_frames,
                "attack window lacks entry, native exit, or post-completion idle: " + proficiency)
        require(("IDLE", "ATTACK") in transitions and ("ATTACK", "IDLE") in transitions,
                "ATTACK state edges missing")
        if proficiency_success:
            successful_proficiencies.append(proficiency)
        else:
            failed_attempts += 1
    return successful_proficiencies, failed_attempts


def check_teardown(report):
    counts = report.get("cleanupCounts")
    require(isinstance(counts, dict) and counts.get("teardownObserved") is True and counts.get("teardownStarted") is True,
            "natural teardown observation required")
    require(counts.get("effectiveDisposeOwned") == 1 and counts.get("effectiveSamplerDispose") == 2 and
            counts.get("effectiveDisableForOwner") == 0, "wrong effective disposal or disable count")
    cleanup = report.get("cleanup")
    require(isinstance(cleanup, list), "cleanup telemetry required")
    lifecycle = [event for event in cleanup if event.get("kind", "").startswith("LegacyKrakenResourceAdapterLease")]
    require(all(event.get("finalized") is True and event.get("exception") is None for event in lifecycle),
            "lifecycle hook did not finalize cleanly")
    effective_samplers = [event.get("sampler") for event in lifecycle if
                          event.get("kind") == "LegacyKrakenResourceAdapterLease.ClipSampler.Dispose" and event.get("effective")]
    require(sorted(effective_samplers) == ["modern", "old"], "each sampler must dispose exactly once")
    effective_adapter = [event for event in lifecycle if
                         event.get("kind") == "LegacyKrakenResourceAdapterLease.DisposeOwned" and event.get("effective")]
    require(len(effective_adapter) == 1, "adapter must have one finalized effective disposal event")
    effective_disable = [event for event in lifecycle if
                         event.get("kind") == "LegacyKrakenResourceAdapterLease.DisableForOwner" and event.get("effective")]
    require(not effective_disable, "accepted campaign cannot include an effective adapter disable")
    polls = [event for event in cleanup if event.get("kind") == "natural-teardown-poll"]
    require(polls, "natural teardown poll required")
    final = polls[-1]
    require(final.get("ownerRegistered") is False and final.get("modernGraphValid") is False and
            final.get("oldGraphValid") is False and final.get("modernRootUnityNull") is True and
            final.get("oldRootUnityNull") is True and final.get("meshLeasePresent") is False and
            final.get("allGloamfinResourcesUnityNull") is True, "natural teardown incomplete")
    resources = final.get("resources")
    require(isinstance(resources, list) and len(resources) >= 3 and all(row.get("unityNull") is True for row in resources),
            "Gloamfin resources are not Unity-null")


def check_report(report, expected_binaries):
    require(isinstance(report, dict) and report.get("schema") == SCHEMA and report.get("ok") is True and
            report.get("error") is None and report.get("overflow") is False and report.get("identityDrift") is False,
            "failed or drifted observer report")
    route = report.get("route"); run = route.get("campaignRun") if isinstance(route, dict) else None
    require(run in RUNS and route.get("expectedTerminal") == RUNS[run] and
            route.get("topologyGroup") == "6a28ac3cf4523c24" and route.get("resourcePrefab") == "enkrakenhead" and
            route.get("registeredEnemy") == REGISTERED_ENEMY and route.get("nativeEnemy") == NATIVE_ENEMY and
            route.get("rendererPath") == "krakenHead" and
            route.get("sourceRendererId") == 121260 and route.get("sourceRendererIdKind") == "serialized_path_id",
            "wrong resource-prefab campaign route")
    check_pins(report, expected_binaries)
    observed, transitions, hashes = check_frames(report)
    proficiencies, failed_proficiency_attempts = check_attack_authority(report, transitions)
    triggers = check_state_callbacks(report, transitions)
    expected_trigger = "Death" if run == "native-combat-death" else "Victory"
    require(expected_trigger in triggers, "native terminal trigger missing: " + expected_trigger)
    if run == "native-combat-death":
        unused_incoming_calculations = check_native_combat_death(report)
    else:
        check_enemy_victory_authority(report)
        unused_incoming_calculations = 0
    check_teardown(report)
    return (run, observed, transitions, hashes, proficiencies, failed_proficiency_attempts,
            unused_incoming_calculations)


def verify_campaign(campaign, reports):
    require(isinstance(campaign, dict) and campaign.get("schema") == CAMPAIGN_SCHEMA, "campaign schema required")
    expected = campaign.get("expectedBinaries")
    require(isinstance(expected, dict) and set(expected) == BINARY_KEYS and
            all(isinstance(value, str) and re.fullmatch(r"[0-9a-f]{64}", value) for value in expected.values()),
            "exact lowercase SHA256 binary receipt pins required")
    require(isinstance(reports, list) and len(reports) == len(RUNS), "exactly two campaign reports required")
    runs, observed, edges, hashes, proficiencies = set(), set(), set(), {}, []
    failed_proficiency_attempts, unused_incoming_calculations = 0, 0
    for report in reports:
        (run, report_states, report_edges, report_hashes, report_proficiencies,
         report_failed_attempts, report_unused_calculations) = check_report(report, expected)
        require(run not in runs, "duplicate campaign run: " + run)
        runs.add(run); observed.update(report_states); edges.update(report_edges); proficiencies.extend(report_proficiencies)
        failed_proficiency_attempts += report_failed_attempts
        unused_incoming_calculations += report_unused_calculations
        for name, value in report_hashes.items():
            hashes.setdefault(name, value)
            require(hashes[name] == value, "state hash changed across reports: " + name)
    require(runs == set(RUNS), "both campaign runs required")
    require(REQUIRED_NATURAL_STATES <= observed and set(hashes) == observed and
            len(set(hashes.values())) == len(hashes),
            "required natural states and distinct observed identities are required")
    allowed = {("IDLE", target) for target in IDLE_OUTBOUND} | {(source, "IDLE") for source in NONTERMINAL_RETURNS}
    allowed |= {(source, target) for source in STATES for target in ANY_STATE_TARGETS}
    require(edges <= allowed, "observer captured an undocumented controller edge")
    require(REQUIRED_NATURAL_EDGES <= edges, "required natural controller edge missing")
    require(REQUIRED_PROFICIENCIES <= set(proficiencies), "all four native Kraken proficiencies required")
    unexercised = sorted(set(STATES) - observed)
    return {"status": "production_observation_campaign_satisfied", "reportCount": len(reports),
            "serializedStateCount": len(STATES), "requiredNaturalStates": sorted(REQUIRED_NATURAL_STATES),
            "observedNaturalStates": sorted(observed), "requiredNaturalEdges": sorted([list(edge) for edge in REQUIRED_NATURAL_EDGES]),
            "observedNaturalEdges": sorted([list(edge) for edge in edges]),
            "unexercisedStructuralRoles": unexercised, "nativeKrakenProficiencies": sorted(set(proficiencies)),
            "nativeKrakenFailedProficiencyAttempts": failed_proficiency_attempts,
            "unusedIncomingAttackCalculations": unused_incoming_calculations,
            "limitations": "Observer evidence only. Visual, progression, endpoint composition, and immutable archive review remain separate gates."}


def load_campaign(path):
    campaign = json.loads(path.read_text()); descriptors = campaign.get("reports")
    receipt_descriptor = campaign.get("deploymentReceipt")
    require(isinstance(receipt_descriptor, dict) and set(receipt_descriptor) == {"path", "sha256"},
            "deployment receipt descriptor required")
    receipt_relative = Path(receipt_descriptor["path"])
    require(not receipt_relative.is_absolute() and ".." not in receipt_relative.parts,
            "deployment receipt must be a contained relative path")
    receipt_path = (path.parent / receipt_relative).resolve(strict=True)
    require(digest(receipt_path) == receipt_descriptor["sha256"], "deployment receipt digest mismatch")
    receipt = json.loads(receipt_path.read_text())
    require(receipt.get("status") == "VERIFIED_COMPLETE" and isinstance(receipt.get("new"), dict),
            "verified binary deployment receipt required")
    destinations = {"framework": "BepInEx/plugins/FTKModFramework.dll",
                    "runtimeHelper": "BepInEx/plugins/FtkRuntimeModelTest.dll",
                    "runtimeContent": "BepInEx/plugins/FtkRuntimeModelTestContent.dll"}
    receipt_pins = {key: receipt["new"].get(destination, {}).get("sha256") for key, destination in destinations.items()}
    require(receipt_pins == campaign.get("expectedBinaries"), "campaign binaries do not match deployment receipt")
    require(isinstance(descriptors, list), "campaign report descriptors required")
    reports = []
    for item in descriptors:
        require(isinstance(item, dict) and set(item) == {"campaignRun", "path", "sha256"},
                "exact report descriptor required")
        relative = Path(item["path"])
        require(not relative.is_absolute() and ".." not in relative.parts, "report must be a contained relative path")
        report_path = (path.parent / relative).resolve(strict=True)
        require(digest(report_path) == item["sha256"], "report digest mismatch: " + item["path"])
        report = json.loads(report_path.read_text())
        require(report.get("route", {}).get("campaignRun") == item["campaignRun"], "report role mismatch")
        reports.append(report)
    return campaign, reports


def main():
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--campaign", type=Path, required=True)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    campaign, reports = load_campaign(args.campaign.resolve(strict=True))
    result = verify_campaign(campaign, reports)
    args.output.write_text(json.dumps(result, indent=2, allow_nan=False) + "\n")
    print(result["status"])


if __name__ == "__main__":
    main()
