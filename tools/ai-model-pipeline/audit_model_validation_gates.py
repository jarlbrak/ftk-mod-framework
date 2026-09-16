#!/usr/bin/env python3
"""Build a conservative ledger of explicitly structured model-validation evidence.

The runtime index answers whether an original model archive is registered for an
exact native source assignment.  It deliberately does not answer whether the
archive proves every required live gate.  This companion audit records only
machine-readable evidence actually present in each indexed archive.  It never
promotes a capture, a status string, or an archive's existence to a PASS.

Historical archives predate a common schema.  The adapters below recognize a
small, documented set of structured shapes (direct capture labels, runtime
binding objects, numeric ordinary-damage records, Ready objects, and named
visual-review fields).  Anything outside those shapes stays unrecorded rather
than being guessed from prose.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import re
import sys
from typing import Any, Iterable

import audit_original_model_index as index_audit


Json = dict[str, Any]


VISUAL_REVIEW_KEYS = (
    "rootVisualReview",
    "visualReview",
    "visual_review",
    "sampledVisualReview",
    "rootPortraitReview",
    "rootReview",
    "review",
    "materialReview",
    "visual_status",
)
FIXTURE_FIELDS = (
    "explicitKillFixture",
    "explicitDeathFixture",
    "fixtureDeath",
    "semanticDeathLight",
    "pairedNormalDeath",
    "nativeDeath",
    "native_death",
    "nativeSuicideFixture",
)
GAMEPLAY_FIXTURE_FIELDS = (
    # Kept deliberately narrow: a named KillSingle fixture HP pair proves that
    # the fixture action was recorded, but it is never ordinary damage proof.
    "explicit_KillSingle_fixture_hp",
)
ORDINARY_DAMAGE_FIELDS = (
    "ordinaryAttack",
    "ordinaryHit",
    "ordinaryNoFocusHit",
    "ordinary_no_focus_hit",
    "ordinary_lethal_followup",
    "normalPlayerAttack",
)


SOURCE_SPECIFIC_ENEMY_CORE_EVIDENCE = (
    ("runtimeBinding", ("runtimeBinding", "sourceSpecificBindingRecorded")),
    ("appearanceReview", ("appearance", "sourceSpecificReviewRecorded")),
    ("idleMotion", ("idle", "sourceSpecificMotionCaptureRecorded")),
    ("attackMotion", ("attack", "sourceSpecificMotionCaptureRecorded")),
    ("hitMotion", ("hit", "sourceSpecificMotionCaptureRecorded")),
    ("deathMotion", ("death", "sourceSpecificDeathCaptureRecorded")),
    ("deathFixtureAction", ("death", "sourceSpecificFixtureActionRecorded")),
    ("ordinaryDamage", ("gameplay", "sourceSpecificOrdinaryDamageRecorded")),
    ("nativeReady", ("gameplay", "sourceSpecificReadyRecorded")),
)
SOURCE_SPECIFIC_PLAYER_CORE_EVIDENCE = (
    *SOURCE_SPECIFIC_ENEMY_CORE_EVIDENCE,
    ("previewAvatar", ("playerPreview", "sourceSpecificPreviewAvatarObserved")),
)
ALLOWED_NATIVE_NOT_APPLICABLE = {
    "hitMotion": {"native_self_removal_has_no_received_hit_phase"},
    "ordinaryDamage": {"native_self_removal_precedes_hero_attack"},
}


def read_json(path: Path) -> Any:
    return json.loads(path.read_text())


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path)


def as_dict(value: Any) -> Json:
    return value if isinstance(value, dict) else {}


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def distinct(values: Iterable[Any]) -> list[Any]:
    seen: set[str] = set()
    result: list[Any] = []
    for value in values:
        key = json.dumps(value, sort_keys=True) if isinstance(value, (dict, list)) else repr(value)
        if key not in seen:
            seen.add(key)
            result.append(value)
    return result


def pointer_join(pointer: str, key: str | int) -> str:
    escaped = str(key).replace("~", "~0").replace("/", "~1")
    return f"{pointer}/{escaped}" if pointer else f"/{escaped}"


def pointer_value(content: Any, pointer: str) -> Any:
    if not isinstance(pointer, str) or not pointer.startswith("/"):
        raise KeyError(pointer)
    value = content
    for raw in pointer[1:].split("/"):
        key = raw.replace("~1", "/").replace("~0", "~")
        if isinstance(value, list):
            value = value[int(key)]
        elif isinstance(value, dict):
            value = value[key]
        else:
            raise KeyError(pointer)
    return value


def applicability_evidence(content: Json, native_chassis: str | None) -> Json:
    declaration = as_dict(content.get("sourceSpecificEvidenceApplicability"))
    accepted: list[Json] = []
    rejected: list[Json] = []
    if not declaration:
        return {"schemaRecorded": False, "accepted": accepted, "rejected": rejected}
    header_ok = (
        declaration.get("schema") == "ftkmf.source-specific-evidence-applicability.v1"
        and declaration.get("nativeChassis") == native_chassis
        and declaration.get("workflow") == "passive_enemy_arrival"
    )
    requirements = as_dict(declaration.get("requirements"))
    for name, raw in requirements.items():
        row = as_dict(raw)
        paths = as_list(row.get("evidencePaths"))
        reason = row.get("reasonCode")
        valid_paths = []
        for path in paths:
            try:
                value = pointer_value(content, path)
                if value not in (None, False, "", [], {}):
                    valid_paths.append(path)
            except (KeyError, IndexError, ValueError, TypeError):
                pass
        valid = (
            header_ok
            and name in ALLOWED_NATIVE_NOT_APPLICABLE
            and row.get("status") == "not_applicable_native_behavior"
            and reason in ALLOWED_NATIVE_NOT_APPLICABLE.get(name, set())
            and len(valid_paths) == len(paths)
            and bool(paths)
        )
        target = accepted if valid else rejected
        target.append({
            "requirement": name,
            "status": row.get("status"),
            "reasonCode": reason,
            "evidencePaths": paths,
        })
    return {
        "schemaRecorded": True,
        "headerAccepted": header_ok,
        "accepted": accepted,
        "rejected": rejected,
    }


def normalized_words(value: Any) -> set[str]:
    """Split a capture label or clip name without interpreting prose evidence."""
    if not isinstance(value, str):
        return set()
    separated = re.sub(r"([a-z0-9])([A-Z])", r"\1 \2", value)
    return set(re.findall(r"[a-z0-9]+", separated.lower()))


def node_native_chassis(node: Json) -> str | None:
    profile = as_dict(node.get("profile"))
    provenance = as_dict(node.get("provenance"))
    provenance_profile = as_dict(provenance.get("profile"))
    for value in (
        node.get("nativeChassis"),
        node.get("native_chassis"),
        node.get("nativeBase"),
        node.get("baseEnemy"),
        profile.get("baseEnemy"),
        profile.get("nativeEnemy"),
        provenance_profile.get("baseEnemy"),
    ):
        if isinstance(value, str) and value:
            return value
    return None


def evidence_units(content: Json, native_chassis: str | None) -> tuple[list[tuple[str, Json, str]], list[str]]:
    """Select source-specific nested trial/binding objects when they declare one.

    Root-level metadata applies to the indexed source record.  A nested trial or
    binding collection can contain several exact sources, so only an object that
    explicitly declares the requested native chassis is included.  This avoids
    granting the mirrored Kraken tentacle the primary tentacle's evidence.
    """
    units: list[tuple[str, Json, str]] = [("", content, "source")]
    notes: list[str] = []
    if not native_chassis:
        return units, notes
    for collection_key in ("trials", "bindings"):
        collection = content.get(collection_key)
        if not isinstance(collection, dict):
            continue
        candidates: list[tuple[str, Json]] = []
        for key, value in collection.items():
            node = as_dict(value)
            if node_native_chassis(node) == native_chassis:
                candidates.append((pointer_join(pointer_join("", collection_key), key), node))
        if candidates:
            # The root can still contain useful shared review metadata, but any
            # gate record there is archive-level once source-specific siblings
            # exist. Do not silently attribute it to one selected source.
            units[0] = ("", content, "archive")
            units.extend((path, node, "source") for path, node in candidates)
        elif any(isinstance(value, dict) for value in collection.values()):
            notes.append(f"/{collection_key} contains source-specific records but none declared {native_chassis!r}")
    return units, notes


def capture_records(units: list[tuple[str, Json, str]]) -> list[Json]:
    """Normalize only explicit capture entries embedded in the selected unit."""
    records: list[Json] = []
    for unit_path, node, scope in units:
        captures = node.get("captures")
        if isinstance(captures, list):
            for index, capture in enumerate(captures):
                value = as_dict(capture)
                if not value:
                    continue
                records.append({
                    "path": pointer_join(pointer_join(unit_path, "captures"), index),
                    "scope": scope,
                    "label": value.get("label", value.get("kind", value.get("action"))),
                    "complete": value.get("complete"),
                    "termination": value.get("termination"),
                    "stateClips": state_clips(value),
                    "causalMotionKinds": causal_motion_kinds(value),
                })
        elif isinstance(captures, dict):
            for key, capture in captures.items():
                value = as_dict(capture)
                if not value:
                    continue
                records.append({
                    "path": pointer_join(pointer_join(unit_path, "captures"), key),
                    "scope": scope,
                    "label": value.get("label", value.get("kind", value.get("action", key))),
                    "complete": value.get("complete"),
                    "termination": value.get("termination"),
                    "stateClips": state_clips(value),
                    "causalMotionKinds": causal_motion_kinds(value),
                })
    return records


def causal_motion_kinds(capture: Json) -> list[str]:
    """Read explicit event categories from the current exercise evidence schema."""
    evidence = as_dict(capture.get("causalMotion"))
    if evidence.get("schema") != "ftkmf.exercise-motion-evidence.v1":
        return []
    return [kind for kind in ("idle", "attack", "hit", "death") if as_dict(evidence.get(kind))]


def state_clips(capture: Json) -> list[str]:
    clips: list[str] = []
    for field in ("clip", "state", "stateName"):
        value = capture.get(field)
        if isinstance(value, str) and value:
            clips.append(value)
    for field in ("clips", "sampledClips"):
        raw_clips = capture.get(field)
        if isinstance(raw_clips, list):
            clips.extend(value for value in raw_clips if isinstance(value, str) and value)
    states = capture.get("states")
    if isinstance(states, list):
        for state in states:
            row = as_dict(state)
            listed = row.get("clips")
            if isinstance(listed, list):
                clips.extend(value for value in listed if isinstance(value, str) and value)
            for field in ("clip", "state", "stateName"):
                value = row.get(field)
                if isinstance(value, str) and value:
                    clips.append(value)
    return distinct(clips)


def capture_matches(record: Json, category: str) -> bool:
    words = normalized_words(record.get("label"))
    for clip in as_list(record.get("stateClips")):
        words.update(normalized_words(clip))
    words.update(kind for kind in as_list(record.get("causalMotionKinds")) if isinstance(kind, str))
    # Older archives retain native controller clip names such as cidle_wolf,
    # attack1, damageSmall, and deathHeavy.  These are explicit state names,
    # unlike a generic capture label such as "pass", so recognize their stable
    # category stems without treating a prose summary as evidence.
    def has_stem(*stems: str) -> bool:
        return any(word == stem or word.startswith(stem) for word in words for stem in stems)
    if category == "idle":
        return has_stem("idle", "cidle")
    if category == "attack":
        return has_stem("attack")
    if category == "hit":
        return has_stem("hit", "damage", "hurt")
    if category == "death":
        return has_stem("death", "die", "dead", "kill", "lethal")
    raise ValueError(f"unknown capture category: {category}")


def binding_locations(units: list[tuple[str, Json, str]], player: bool) -> list[Json]:
    locations: list[Json] = []
    for unit_path, node, scope in units:
        binding = as_dict(node.get("binding"))
        if binding and is_binding_shape(binding):
            locations.append({"path": pointer_join(unit_path, "binding"), "kind": "binding-object", "scope": scope})
        initial_renderer = as_dict(node.get("initialRenderer"))
        if initial_renderer and is_binding_shape(initial_renderer):
            locations.append({"path": pointer_join(unit_path, "initialRenderer"), "kind": "runtime-renderer-inventory", "scope": scope})
        if unit_path.startswith("/bindings/") and is_binding_shape(node):
            locations.append({"path": unit_path, "kind": "source-specific-binding", "scope": scope})
        # Several archive generations wrote the inspected live renderer as
        # root fields instead of a nested binding object.  Require all four
        # identity components so an offline profile declaration, a lone path,
        # or an asset hash cannot be promoted to a runtime binding record.
        if (
            isinstance(node.get("rendererPath"), str) and node.get("rendererPath")
            and number(node.get("rendererId"))
            and number(node.get("ownerInstanceId"))
            and isinstance(node.get("boneSignature"), str) and node.get("boneSignature")
        ):
            locations.append({"path": unit_path or "/", "kind": "root-runtime-renderer-identity", "scope": scope})
        renderers = node.get("renderers")
        if isinstance(renderers, list) and renderers and (
            "ownerInstanceId" in node or "losslessMappings" in node or binding
        ):
            locations.append({"path": pointer_join(unit_path, "renderers"), "kind": "renderer-list-with-runtime-owner", "scope": scope})
        if player and isinstance(node.get("avatarOwners"), dict) and isinstance(node.get("profile"), dict):
            locations.append({"path": pointer_join(unit_path, "avatarOwners"), "kind": "player-avatar-owner-inventory", "scope": scope})
    return distinct(locations)


def is_binding_shape(value: Json) -> bool:
    return any(key in value for key in (
        "rendererPath", "celRelativeRendererPath", "rendererKind", "renderers",
        "matches", "mesh", "boneSignature", "owner", "ownerInstanceId",
    ))


def scope_summary(locations: list[Json]) -> Json:
    return {
        "recorded": bool(locations),
        "sourceSpecificRecorded": any(item.get("scope") == "source" for item in locations),
        "archiveLevelRecorded": any(item.get("scope") == "archive" for item in locations),
    }


def visual_evidence(units: list[tuple[str, Json, str]]) -> Json:
    reviews: list[Json] = []
    samples: list[Json] = []
    for unit_path, node, scope in units:
        for key in VISUAL_REVIEW_KEYS:
            if key in node and node[key] not in (None, "", [], {}):
                reviews.append({"path": pointer_join(unit_path, key), "field": key, "scope": scope})
        for key in ("selectedPNGs", "selectedFrames", "sourceImages"):
            value = node.get(key)
            if isinstance(value, list) and value:
                samples.append({"path": pointer_join(unit_path, key), "field": key, "count": len(value), "scope": scope})
        review = as_dict(node.get("sampledVisualReview"))
        selected = review.get("selectedPNGs")
        if isinstance(selected, list) and selected:
            samples.append({
                "path": pointer_join(pointer_join(unit_path, "sampledVisualReview"), "selectedPNGs"),
                "field": "sampledVisualReview.selectedPNGs",
                "count": len(selected),
                "scope": scope,
            })
    review_scope = scope_summary(reviews)
    sample_scope = scope_summary(samples)
    return {
        "reviewRecorded": review_scope["recorded"],
        "sourceSpecificReviewRecorded": review_scope["sourceSpecificRecorded"],
        "archiveLevelReviewRecorded": review_scope["archiveLevelRecorded"],
        "reviewLocations": distinct(reviews),
        "sampleFramesRecorded": sample_scope["recorded"],
        "sourceSpecificSampleFramesRecorded": sample_scope["sourceSpecificRecorded"],
        "archiveLevelSampleFramesRecorded": sample_scope["archiveLevelRecorded"],
        "sampleLocations": distinct(samples),
    }


def motion_evidence(captures: list[Json], category: str) -> Json:
    matches = [
        {
            "path": capture["path"],
            "scope": capture.get("scope"),
            "label": capture.get("label"),
            "complete": capture.get("complete"),
            "termination": capture.get("termination"),
            "stateClips": capture.get("stateClips", []),
            "causalMotionKinds": capture.get("causalMotionKinds", []),
        }
        for capture in captures if capture_matches(capture, category)
    ]
    scope = scope_summary(matches)
    return {
        "motionCaptureRecorded": scope["recorded"],
        "sourceSpecificMotionCaptureRecorded": scope["sourceSpecificRecorded"],
        "archiveLevelMotionCaptureRecorded": scope["archiveLevelRecorded"],
        "captures": matches,
    }


def fixture_evidence(units: list[tuple[str, Json, str]], captures: list[Json]) -> Json:
    fixture_locations: list[Json] = []
    for unit_path, node, scope in units:
        for key in FIXTURE_FIELDS:
            if key in node and node[key] not in (None, False, "", [], {}):
                fixture_locations.append({"path": pointer_join(unit_path, key), "field": key, "scope": scope})
        gameplay = as_dict(node.get("gameplay"))
        for key in GAMEPLAY_FIXTURE_FIELDS:
            if key not in gameplay:
                continue
            # Do not credit a string or a nonnumeric note.  The two-number
            # shape is a recorded fixture health transition, not a verdict
            # about ordinary death or visual completeness.
            if hp_transitions(gameplay[key], pointer_join(pointer_join(unit_path, "gameplay"), key)):
                fixture_locations.append({
                    "path": pointer_join(pointer_join(unit_path, "gameplay"), key),
                    "field": key,
                    "scope": scope,
                })
        death_action = node.get("deathAction")
        if isinstance(death_action, str):
            words = normalized_words(death_action)
            if "fixture" in words and any(word.startswith("kill") for word in words):
                fixture_locations.append({"path": pointer_join(unit_path, "deathAction"), "field": "deathAction", "scope": scope})
    motion = motion_evidence(captures, "death")
    fixture_scope = scope_summary(fixture_locations)
    return {
        "deathCaptureRecorded": motion["motionCaptureRecorded"],
        "sourceSpecificDeathCaptureRecorded": motion["sourceSpecificMotionCaptureRecorded"],
        "archiveLevelDeathCaptureRecorded": motion["archiveLevelMotionCaptureRecorded"],
        "captures": motion["captures"],
        "fixtureActionRecorded": fixture_scope["recorded"],
        "sourceSpecificFixtureActionRecorded": fixture_scope["sourceSpecificRecorded"],
        "archiveLevelFixtureActionRecorded": fixture_scope["archiveLevelRecorded"],
        "fixtureLocations": distinct(fixture_locations),
    }


def number(value: Any) -> bool:
    return isinstance(value, (int, float)) and not isinstance(value, bool)


def hp_transitions(value: Any, pointer: str) -> list[Json]:
    """Extract explicit before/after pairs from a named ordinary-damage record."""
    transitions: list[Json] = []
    if isinstance(value, list):
        if len(value) == 2 and all(number(item) for item in value):
            return [{"path": pointer, "beforeHp": value[0], "afterHp": value[1]}]
        for index, item in enumerate(value):
            transitions.extend(hp_transitions(item, pointer_join(pointer, index)))
        return transitions
    row = as_dict(value)
    if not row:
        return transitions
    pairs = (
        ("beforeHp", "afterHp"),
        ("enemyHpBefore", "enemyHpAfter"),
        ("hpBefore", "hpAfter"),
        ("enemyHp", "enemyHp"),
    )
    for before_key, after_key in pairs:
        before, after = row.get(before_key), row.get(after_key)
        if before_key == after_key and isinstance(before, list) and len(before) == 2 and all(number(item) for item in before):
            transitions.append({"path": pointer_join(pointer, before_key), "beforeHp": before[0], "afterHp": before[1]})
        elif before_key != after_key and number(before) and number(after):
            transitions.append({"path": pointer, "beforeHp": before, "afterHp": after})
    if transitions:
        return transitions
    for key, child in row.items():
        if isinstance(child, (dict, list)):
            transitions.extend(hp_transitions(child, pointer_join(pointer, key)))
    return transitions


def ready_records(value: Any, pointer: str) -> list[Json]:
    rows: list[Json] = []
    if isinstance(value, list):
        for index, child in enumerate(value):
            rows.extend(ready_records(child, pointer_join(pointer, index)))
        return rows
    row = as_dict(value)
    if not row:
        return rows
    if row.get("ok") is True:
        rows.append({"path": pointer, "ok": True, "level": row.get("level"), "room": row.get("room")})
    for key in ("finalReady", "ready"):
        if key in row:
            rows.extend(ready_records(row[key], pointer_join(pointer, key)))
    return rows


def gameplay_evidence(units: list[tuple[str, Json, str]]) -> Json:
    transitions: list[Json] = []
    ready: list[Json] = []
    for unit_path, node, scope in units:
        for field in ORDINARY_DAMAGE_FIELDS:
            if field in node:
                transitions.extend({**item, "scope": scope} for item in hp_transitions(node[field], pointer_join(unit_path, field)))
        gameplay = as_dict(node.get("gameplay"))
        if gameplay:
            for field in ORDINARY_DAMAGE_FIELDS:
                if field in gameplay:
                    transitions.extend({**item, "scope": scope} for item in hp_transitions(gameplay[field], pointer_join(pointer_join(unit_path, "gameplay"), field)))
            if "ordinary_attack_hp" in gameplay:
                transitions.extend({**item, "scope": scope} for item in hp_transitions(gameplay["ordinary_attack_hp"], pointer_join(pointer_join(unit_path, "gameplay"), "ordinary_attack_hp")))
            if "ordinaryAttack" in gameplay:
                transitions.extend({**item, "scope": scope} for item in hp_transitions(gameplay["ordinaryAttack"], pointer_join(pointer_join(unit_path, "gameplay"), "ordinaryAttack")))
        for field in ("finalReady", "ready", "nextReady", "nativeProgression"):
            if field in node:
                ready.extend({**item, "scope": scope} for item in ready_records(node[field], pointer_join(unit_path, field)))
        if gameplay:
            ready.extend({**item, "scope": scope} for item in ready_records(gameplay, pointer_join(unit_path, "gameplay")))
        after_ready = as_dict(node.get("afterReadyState"))
        if after_ready.get("strictReady") is True:
            ready.append({
                "path": pointer_join(unit_path, "afterReadyState"),
                "ok": True,
                "level": after_ready.get("level"),
                "room": after_ready.get("room"),
                "strictReady": True,
                "scope": scope,
            })
    transitions = distinct(transitions)
    ready = distinct(ready)
    damage = [row for row in transitions if row["afterHp"] < row["beforeHp"]]
    damage_scope = scope_summary(damage)
    ready_scope = scope_summary(ready)
    return {
        "ordinaryDamageRecorded": damage_scope["recorded"],
        "sourceSpecificOrdinaryDamageRecorded": damage_scope["sourceSpecificRecorded"],
        "archiveLevelOrdinaryDamageRecorded": damage_scope["archiveLevelRecorded"],
        "ordinaryDamageTransitions": damage,
        "recordedNonDamageTransitions": [row for row in transitions if row["afterHp"] >= row["beforeHp"]],
        "readyRecorded": ready_scope["recorded"],
        "sourceSpecificReadyRecorded": ready_scope["sourceSpecificRecorded"],
        "archiveLevelReadyRecorded": ready_scope["archiveLevelRecorded"],
        "readyRecords": ready,
    }


def player_preview_evidence(units: list[tuple[str, Json, str]], player: bool) -> Json:
    if not player:
        return {
            "applicable": False,
            "previewStateRecorded": False,
            "previewAvatarObserved": False,
            "locations": [],
        }
    locations: list[Json] = []
    observed: list[Json] = []
    for unit_path, node, scope in units:
        preview = as_dict(as_dict(node.get("avatarOwners")).get("preview"))
        if not preview:
            continue
        location = {
            "path": pointer_join(pointer_join(unit_path, "avatarOwners"), "preview"),
            "scope": scope,
            "status": preview.get("status"),
            "observedAvatars": preview.get("observedAvatars"),
        }
        locations.append(location)
        count = preview.get("observedAvatars")
        if number(count) and count > 0:
            observed.append(location)
    state_scope = scope_summary(locations)
    observed_scope = scope_summary(observed)
    return {
        "applicable": True,
        "previewStateRecorded": state_scope["recorded"],
        "sourceSpecificPreviewStateRecorded": state_scope["sourceSpecificRecorded"],
        "archiveLevelPreviewStateRecorded": state_scope["archiveLevelRecorded"],
        "previewAvatarObserved": observed_scope["recorded"],
        "sourceSpecificPreviewAvatarObserved": observed_scope["sourceSpecificRecorded"],
        "archiveLevelPreviewAvatarObserved": observed_scope["archiveLevelRecorded"],
        "locations": distinct(locations),
    }


def gate_evidence(content: Json, native_chassis: str | None, player: bool = False) -> Json:
    units, notes = evidence_units(content, native_chassis)
    captures = capture_records(units)
    binding = binding_locations(units, player)
    binding_scope = scope_summary(binding)
    idle = motion_evidence(captures, "idle")
    attack = motion_evidence(captures, "attack")
    hit = motion_evidence(captures, "hit")
    return {
        "runtimeBinding": {
            "bindingRecorded": binding_scope["recorded"],
            "sourceSpecificBindingRecorded": binding_scope["sourceSpecificRecorded"],
            "archiveLevelBindingRecorded": binding_scope["archiveLevelRecorded"],
            "locations": binding,
        },
        "appearance": visual_evidence(units),
        "idle": idle,
        "attack": attack,
        "hit": hit,
        "death": fixture_evidence(units, captures),
        "gameplay": gameplay_evidence(units),
        "playerPreview": player_preview_evidence(units, player),
        "applicability": applicability_evidence(content, native_chassis),
        "captureRecords": captures,
        "selectedEvidenceUnits": [{"path": path or "/", "scope": scope} for path, _, scope in units],
        "selectionNotes": notes,
    }


def unrecorded_source_specific_core_evidence(gates: Json, player: bool) -> list[str]:
    """List absent structured evidence shapes without making a failure verdict.

    An archive-level record can be useful history, but it cannot be attributed to
    one exact source when a multi-source archive is retained. This helper keeps
    the evidence boundary explicit for planning: an item is listed only when the
    current archive lacks a source-specific machine-readable shape.
    """
    requirements = SOURCE_SPECIFIC_PLAYER_CORE_EVIDENCE if player else SOURCE_SPECIFIC_ENEMY_CORE_EVIDENCE
    not_applicable = {
        row.get("requirement")
        for row in as_list(as_dict(gates.get("applicability")).get("accepted"))
        if isinstance(row, dict)
    }
    missing: list[str] = []
    for name, (section, field) in requirements:
        if as_dict(gates.get(section)).get(field) is not True and name not in not_applicable:
            missing.append(name)
    return missing


def archive_reference(entry: Json) -> Json:
    return index_audit.archive_evidence_reference(entry)


def archive_path(root: Path, reference: Json) -> Path | None:
    value = reference.get("path")
    if not isinstance(value, str) or not value:
        return None
    path = Path(value)
    return path if path.is_absolute() else root / path


def compact_assignment(value: Json) -> Json:
    compact = {
        key: value.get(key)
        for key in (
            "sourceKind", "nativeEnemy", "resourcePrefab", "rendererPath",
            "sourceRendererId", "rigProfile", "combatProfile", "controllerId", "controllerName",
        )
    }
    if compact["sourceKind"] is None:
        compact["sourceKind"] = "resource_prefab_override" if compact["resourcePrefab"] else "native_enemy_row"
    return compact


def assignments_for_index_entry(assignments: list[Json], entry: Json) -> list[Json]:
    """Keep a multi-trial archive on the exact source row named by its index entry.

    A single archive may retain several source-specific trial objects. The
    original-index audit resolves all declared members for reconciliation, but a
    ledger row is one index entry and must not render a sibling source as that
    entry's route. Only apply a narrowing filter when the index itself declares
    the corresponding native chassis/path/ID; otherwise preserve the complete
    exact resolution.
    """
    filtered = list(assignments)
    native_chassis = entry.get("native_chassis")
    if isinstance(native_chassis, str) and native_chassis:
        narrowed = [item for item in filtered if item.get("nativeEnemy") == native_chassis]
        if narrowed:
            filtered = narrowed
    paths = [item for item in as_list(entry.get("renderer_paths")) if isinstance(item, str) and item]
    if paths:
        narrowed = [item for item in filtered if item.get("rendererPath") in paths]
        if narrowed:
            filtered = narrowed
    source_ids = [item for item in as_list(entry.get("source_renderer_ids")) if isinstance(item, int)]
    if source_ids:
        narrowed = [item for item in filtered if item.get("sourceRendererId") in source_ids]
        if narrowed:
            filtered = narrowed
    return distinct(filtered)


def enemy_records(root: Path, index: Json, mapping: Json, resources: Json) -> tuple[list[Json], list[Json], list[Json]]:
    by_assignment, by_renderer_id, by_resource = index_audit.mapping_tables(mapping, resources)
    records: list[Json] = []
    unresolved: list[Json] = []
    hash_mismatches: list[Json] = []
    for position, raw_entry in enumerate(as_list(index.get("enemy_models"))):
        entry = as_dict(raw_entry)
        pointer = f"/enemy_models/{position}"
        reference = archive_reference(entry)
        path = archive_path(root, reference)
        if path is None:
            unresolved.append({"indexPointer": pointer, "reason": "missing evidence archive path"})
            continue
        archive = {"path": relative(root, path), "expectedSha256": reference.get("sha256")}
        if not path.is_file():
            unresolved.append({"indexPointer": pointer, "archive": archive, "reason": "evidence archive is missing"})
            continue
        archive["actualSha256"] = sha256(path)
        if isinstance(reference.get("sha256"), str) and reference["sha256"] != archive["actualSha256"]:
            hash_mismatches.append({"indexPointer": pointer, **archive})
        try:
            content = read_json(path)
        except (OSError, json.JSONDecodeError) as error:
            unresolved.append({"indexPointer": pointer, "archive": archive, "reason": str(error)})
            continue
        identity = index_audit.add_index_fallback(index_audit.evidence_identity(content, root), entry)
        assignments = assignments_for_index_entry(
            index_audit.resolve_assignments(identity, by_assignment, by_renderer_id, by_resource), entry
        )
        if not assignments:
            unresolved.append({
                "indexPointer": pointer,
                "archive": archive,
                "nativeChassis": identity.get("nativeEnemy"),
                "declaredRendererPaths": identity.get("declaredRendererPaths", []),
                "declaredRendererIds": identity.get("declaredRendererIds", []),
                "reason": "no unambiguous exact native renderer mapping",
            })
        gates = gate_evidence(content, identity.get("nativeEnemy"))
        records.append({
            "indexPointer": pointer,
            "name": entry.get("name") or entry.get("model"),
            "status": entry.get("status"),
            "archive": archive,
            "nativeChassis": identity.get("nativeEnemy"),
            "exactAssignments": [compact_assignment(item) for item in assignments],
            "artifactTopLevelKeys": sorted(content.keys()),
            "gates": gates,
            "unrecordedSourceSpecificCoreEvidence": unrecorded_source_specific_core_evidence(gates, player=False),
        })
    return records, unresolved, hash_mismatches


def player_records(root: Path, index: Json) -> tuple[list[Json], list[Json], list[Json]]:
    records: list[Json] = []
    unresolved: list[Json] = []
    hash_mismatches: list[Json] = []
    for position, raw_entry in enumerate(as_list(index.get("original_player_models"))):
        entry = as_dict(raw_entry)
        pointer = f"/original_player_models/{position}"
        reference = archive_reference(entry)
        path = archive_path(root, reference)
        if path is None:
            unresolved.append({"indexPointer": pointer, "reason": "missing evidence archive path"})
            continue
        archive = {"path": relative(root, path), "expectedSha256": reference.get("sha256")}
        if not path.is_file():
            unresolved.append({"indexPointer": pointer, "archive": archive, "reason": "evidence archive is missing"})
            continue
        archive["actualSha256"] = sha256(path)
        if isinstance(reference.get("sha256"), str) and reference["sha256"] != archive["actualSha256"]:
            hash_mismatches.append({"indexPointer": pointer, **archive})
        try:
            content = read_json(path)
        except (OSError, json.JSONDecodeError) as error:
            unresolved.append({"indexPointer": pointer, "archive": archive, "reason": str(error)})
            continue
        identity = index_audit.player_evidence_identity(content)
        if identity is None:
            unresolved.append({"indexPointer": pointer, "archive": archive, "reason": "archive does not declare a player profile"})
            continue
        gates = gate_evidence(content, None, player=True)
        records.append({
            "indexPointer": pointer,
            "name": entry.get("name") or identity.get("profile"),
            "status": entry.get("status"),
            "archive": archive,
            "playerProfile": identity.get("profile"),
            "skinset": identity.get("skinset"),
            "rendererPaths": identity.get("rendererPaths", []),
            "apparelPaths": identity.get("apparelPaths", []),
            "artifactTopLevelKeys": sorted(content.keys()),
            "gates": gates,
            "unrecordedSourceSpecificCoreEvidence": unrecorded_source_specific_core_evidence(gates, player=True),
        })
    return records, unresolved, hash_mismatches


def count(records: list[Json], path: tuple[str, ...]) -> int:
    value = 0
    for record in records:
        cursor: Any = record
        for key in path:
            cursor = as_dict(cursor).get(key)
        if cursor is True:
            value += 1
    return value


def audit(root: Path, index_path: Path, mapping_path: Path, resources_path: Path) -> Json:
    index = read_json(index_path)
    mapping = read_json(mapping_path)
    resources = read_json(resources_path)
    enemies, enemy_unresolved, enemy_hashes = enemy_records(root, index, mapping, resources)
    players, player_unresolved, player_hashes = player_records(root, index)
    records = enemies + players
    summary = {
        "indexedEnemyRecords": len(enemies),
        "indexedPlayerRecords": len(players),
        "recordsWithExplicitBindingEvidence": count(records, ("gates", "runtimeBinding", "bindingRecorded")),
        "recordsWithAppearanceReviewRecord": count(records, ("gates", "appearance", "reviewRecorded")),
        "recordsWithExplicitIdleMotionCapture": count(records, ("gates", "idle", "motionCaptureRecorded")),
        "recordsWithExplicitAttackMotionCapture": count(records, ("gates", "attack", "motionCaptureRecorded")),
        "recordsWithExplicitHitMotionCapture": count(records, ("gates", "hit", "motionCaptureRecorded")),
        "recordsWithDeathCapture": count(records, ("gates", "death", "deathCaptureRecorded")),
        "recordsWithFixtureActionRecord": count(records, ("gates", "death", "fixtureActionRecorded")),
        "recordsWithOrdinaryDamageTransition": count(records, ("gates", "gameplay", "ordinaryDamageRecorded")),
        "recordsWithReadyRecord": count(records, ("gates", "gameplay", "readyRecorded")),
        "playerRecordsWithPreviewState": count(players, ("gates", "playerPreview", "previewStateRecorded")),
        "playerRecordsWithObservedPreviewAvatar": count(players, ("gates", "playerPreview", "previewAvatarObserved")),
        "enemyRecordsWithNoUnrecordedSourceSpecificCoreEvidence": sum(
            not as_list(record.get("unrecordedSourceSpecificCoreEvidence")) for record in enemies),
        "playerRecordsWithNoUnrecordedSourceSpecificCoreEvidence": sum(
            not as_list(record.get("unrecordedSourceSpecificCoreEvidence")) for record in players),
        "unresolvedIndexRecords": len(enemy_unresolved) + len(player_unresolved),
        "archiveHashMismatches": len(enemy_hashes) + len(player_hashes),
    }
    return {
        "schemaVersion": 2,
        "scope": (
            "Indexed original-model archives only. This is an explicit structured-evidence ledger, "
            "not a completion or acceptance audit. A recorded cell means the archive contains the "
            "named machine-readable evidence shape; it does not mean the model passed that gate. "
            "An unrecorded source-specific core field is a documentation gap, not a behavior failure."
        ),
        "gateDefinitions": {
            "runtimeBinding": "A direct runtime binding object, source-specific binding object, complete root live-renderer identity (path, ID, owner, and bone signature), or player owner inventory with declared profile.",
            "appearance": "A named visual-review field; the ledger does not parse its prose or infer an approval verdict.",
            "idle": "A capture label or captured state clip explicitly names idle/cidle.",
            "attack": "A capture label or captured state clip explicitly names attack or an attack-prefixed native clip.",
            "hit": "A capture label or captured state clip explicitly names hit, damage, or hurt, including a matching native-clip stem.",
            "death": "A capture label or captured state clip explicitly names death/kill/lethal. Fixture action records are reported separately.",
            "ordinaryDamage": "A named ordinary attack/hit record contains an explicit numeric HP decrease. Focus-only evidence is excluded.",
            "ready": "A structured Ready/finalReady object explicitly reports ok: true, or an after-Ready snapshot explicitly reports strictReady: true.",
        "unrecordedSourceSpecificCoreEvidence": "Required source-specific machine-readable shapes absent from this individual archive; this does not infer a behavior failure or invalidate another revision.",
        },
        "inputs": {
            "index": {"path": relative(root, index_path), "sha256": sha256(index_path)},
            "mapping": {"path": relative(root, mapping_path), "sha256": sha256(mapping_path)},
            "resources": {"path": relative(root, resources_path), "sha256": sha256(resources_path)},
        },
        "summary": summary,
        "enemyRecords": enemies,
        "playerRecords": players,
        "unresolvedIndexRecords": enemy_unresolved + player_unresolved,
        "archiveHashMismatches": enemy_hashes + player_hashes,
    }


def marker(source_specific: bool, archive_level: bool = False, fixture: bool = False) -> str:
    if source_specific:
        return "recorded"
    if archive_level:
        return "archive-level"
    if fixture:
        return "fixture only"
    return "not recorded"


def preview_marker(value: Json, player: bool) -> str:
    if not player:
        return "n/a"
    if value.get("sourceSpecificPreviewAvatarObserved") is True:
        return "recorded"
    if value.get("archiveLevelPreviewAvatarObserved") is True:
        return "archive-level"
    if value.get("previewStateRecorded") is True:
        return "state only"
    return "not recorded"


def markdown_path(archive: str) -> str:
    return f"../{archive}" if not archive.startswith("../") else archive


def route(record: Json, player: bool) -> str:
    if player:
        paths = list(record.get("rendererPaths", [])) + list(record.get("apparelPaths", []))
        return f"`{record.get('skinset')}`: " + ", ".join(f"`{path}`" for path in paths)
    assignments = as_list(record.get("exactAssignments"))
    if not assignments:
        return "unresolved exact source"
    parts = []
    for item in assignments:
        source = item.get("nativeEnemy") or "?"
        path = item.get("rendererPath") or "?"
        renderer_id = item.get("sourceRendererId")
        suffix = f" #{renderer_id}" if isinstance(renderer_id, int) else ""
        if item.get("sourceKind") == "resource_prefab_override":
            resource = item.get("resourcePrefab") or "?"
            parts.append(f"`resource:{resource}` → `{source}:{path}{suffix}`")
        else:
            parts.append(f"`direct:{source}:{path}{suffix}`")
    return "<br>".join(parts)


def render_markdown(report: Json) -> str:
    summary = as_dict(report.get("summary"))
    lines = [
        "# Model validation evidence ledger",
        "",
        "This ledger reads the original-model runtime index and reports only explicit, machine-readable evidence present in each indexed archive. `recorded` means the field is source-specific; `archive-level` means a multi-source archive records it only at the shared root. Neither label means a gate passed, that the artwork is approved, or that a sibling source assignment is covered. `not recorded` means this conservative adapter found no matching structured field; it does not mean the behavior failed. Read the linked archive and its stated limits before making a verdict.",
        "",
        "The source index reconciles archive registration and exact native identity. This ledger adds no inferred proof from free-text statuses, capture names such as `pass`, archive presence, or topology similarity.",
        "",
        "The route column names `direct:` serialized enemy rows separately from `resource:` prefab overrides. Player rows name the exact skinset and configured renderer paths.",
        "",
        "| Indexed records | Binding | Appearance review | Idle capture | Attack capture | Hit capture | Death capture | Fixture action | Ordinary damage | Ready | Player preview state / observed |",
        "|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
        "| {indexedEnemyRecords} enemy + {indexedPlayerRecords} player | {recordsWithExplicitBindingEvidence} | {recordsWithAppearanceReviewRecord} | {recordsWithExplicitIdleMotionCapture} | {recordsWithExplicitAttackMotionCapture} | {recordsWithExplicitHitMotionCapture} | {recordsWithDeathCapture} | {recordsWithFixtureActionRecord} | {recordsWithOrdinaryDamageTransition} | {recordsWithReadyRecord} | {playerRecordsWithPreviewState} / {playerRecordsWithObservedPreviewAvatar} |".format(**summary),
        "",
        "The counters include both source-specific and archive-level records; the per-archive table preserves that distinction. A death fixture is deliberately separate from a death capture. Ordinary damage excludes focus-only trials. A Ready record is progression evidence, but does not by itself prove a full campaign or cleanup behavior. Preview applies only to indexed player archives: `state only` records the owner snapshot without claiming a rendered native character-creation avatar.",
        "",
        "The JSON ledger also lists `unrecordedSourceSpecificCoreEvidence` for each archive. This is a planning fact: it names the required machine-readable shape that this archive lacks, and does not say the behavior failed, that a later revision is invalid, or that another source assignment is covered.",
        "",
        "## Indexed evidence records",
        "",
        "| Kind | Model / route | Archive | Bind | Review | Idle | Attack | Hit | Death | Damage | Ready | Preview |",
        "|---|---|---|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
    ]
    combined: list[tuple[Json, bool]] = [(record, False) for record in as_list(report.get("enemyRecords"))]
    combined.extend((record, True) for record in as_list(report.get("playerRecords")))
    for record, player in combined:
        gates = as_dict(record.get("gates"))
        binding = as_dict(gates.get("runtimeBinding"))
        appearance = as_dict(gates.get("appearance"))
        idle = as_dict(gates.get("idle"))
        attack = as_dict(gates.get("attack"))
        hit = as_dict(gates.get("hit"))
        death = as_dict(gates.get("death"))
        gameplay = as_dict(gates.get("gameplay"))
        preview = as_dict(gates.get("playerPreview"))
        archive = as_dict(record.get("archive"))
        archive_path_value = archive.get("path", "")
        filename = Path(archive_path_value).name if isinstance(archive_path_value, str) else "archive"
        link = f"[{filename}]({markdown_path(archive_path_value)})" if archive_path_value else "missing archive"
        title = record.get("name") or record.get("nativeChassis") or record.get("playerProfile") or "unnamed"
        kind = "player" if player else "enemy"
        lines.append(
            "| {kind} | **{title}**<br>{route} | {link} | {binding} | {appearance} | {idle} | {attack} | {hit} | {death} | {damage} | {ready} | {preview} |".format(
                kind=kind,
                title=title,
                route=route(record, player),
                link=link,
                binding=marker(binding.get("sourceSpecificBindingRecorded") is True, binding.get("archiveLevelBindingRecorded") is True),
                appearance=marker(appearance.get("sourceSpecificReviewRecorded") is True, appearance.get("archiveLevelReviewRecorded") is True),
                idle=marker(idle.get("sourceSpecificMotionCaptureRecorded") is True, idle.get("archiveLevelMotionCaptureRecorded") is True),
                attack=marker(attack.get("sourceSpecificMotionCaptureRecorded") is True, attack.get("archiveLevelMotionCaptureRecorded") is True),
                hit=marker(hit.get("sourceSpecificMotionCaptureRecorded") is True, hit.get("archiveLevelMotionCaptureRecorded") is True),
                death=marker(death.get("sourceSpecificDeathCaptureRecorded") is True, death.get("archiveLevelDeathCaptureRecorded") is True, death.get("fixtureActionRecorded") is True),
                damage=marker(gameplay.get("sourceSpecificOrdinaryDamageRecorded") is True, gameplay.get("archiveLevelOrdinaryDamageRecorded") is True),
                ready=marker(gameplay.get("sourceSpecificReadyRecorded") is True, gameplay.get("archiveLevelReadyRecorded") is True),
                preview=preview_marker(preview, player),
            )
        )
    unresolved = as_list(report.get("unresolvedIndexRecords"))
    enemy_gaps = summary.get("indexedEnemyRecords", 0) - summary.get("enemyRecordsWithNoUnrecordedSourceSpecificCoreEvidence", 0)
    player_gaps = summary.get("indexedPlayerRecords", 0) - summary.get("playerRecordsWithNoUnrecordedSourceSpecificCoreEvidence", 0)
    lines.extend([
        "",
        "## Audit boundaries",
        "",
        "The automated ledger cannot decide whether a captured frame has acceptable art, attachment, bounds, culling, material quality, pose readability, or final resource disposal. It also does not infer idle from a generic `pass` capture, attack/hit/death from an HP result, or normal damage from a focused trial. Use [MODEL-AUTHORING.md](MODEL-AUTHORING.md#evidence-and-completion) for the actual gate criteria and update the archive with structured records when a new live trial establishes one.",
        "",
        f"Archives with one or more unrecorded source-specific core fields: **{enemy_gaps} enemy** and **{player_gaps} player**. Consult each JSON record's `unrecordedSourceSpecificCoreEvidence` list before selecting a follow-up; this count is not a failure count.",
        "",
        f"Unresolved indexed records: **{len(unresolved)}**. Archive hash mismatches: **{len(as_list(report.get('archiveHashMismatches')))}**.",
        "",
    ])
    if unresolved:
        lines.extend(["### Unresolved index records", ""])
        for item in unresolved:
            lines.append(f"- `{item.get('indexPointer')}`: {item.get('reason')}")
        lines.append("")
    return "\n".join(lines)


def write_new(path: Path, content: str, overwrite: bool) -> None:
    if path.exists() and not overwrite:
        raise FileExistsError(f"refusing to overwrite existing output: {path}; pass --overwrite")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd(), help="Repository root, default: current directory")
    parser.add_argument("--index", type=Path, default=Path("docs/model-runtime-validation.json"))
    parser.add_argument("--mapping", type=Path, default=Path("scratch/enemy-rig-mapping-reproducible.json"))
    parser.add_argument("--resources", type=Path, default=Path("scratch/resource-enemy-base-preflight.json"))
    parser.add_argument("--output-json", type=Path, help="Write the complete JSON report")
    parser.add_argument("--output-markdown", type=Path, help="Write the Markdown ledger")
    parser.add_argument("--overwrite", action="store_true", help="Allow replacing an existing requested output")
    parser.add_argument("--summary", action="store_true", help="Print only the stable summary")
    parser.add_argument("--fail-on-unresolved", action="store_true", help="Exit nonzero for missing, invalid, or unmappable index records")
    parser.add_argument("--fail-on-integrity", action="store_true", help="Exit nonzero when an indexed archive differs from its recorded SHA-256")
    args = parser.parse_args()
    root = args.root.resolve()
    index_path = args.index if args.index.is_absolute() else root / args.index
    mapping_path = args.mapping if args.mapping.is_absolute() else root / args.mapping
    resources_path = args.resources if args.resources.is_absolute() else root / args.resources
    for path in (index_path, mapping_path, resources_path):
        if not path.is_file():
            parser.error(f"missing input: {path}")
    report = audit(root, index_path, mapping_path, resources_path)
    if args.output_json:
        output = args.output_json if args.output_json.is_absolute() else root / args.output_json
        write_new(output, json.dumps(report, indent=2) + "\n", args.overwrite)
    if args.output_markdown:
        output = args.output_markdown if args.output_markdown.is_absolute() else root / args.output_markdown
        write_new(output, render_markdown(report), args.overwrite)
    if args.summary:
        print(json.dumps(report["summary"], indent=2))
    elif not args.output_json and not args.output_markdown:
        print(json.dumps(report, indent=2))
    if args.fail_on_unresolved and report["summary"]["unresolvedIndexRecords"]:
        return 1
    if args.fail_on_integrity and report["summary"]["archiveHashMismatches"]:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
