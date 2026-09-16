#!/usr/bin/env python3
"""Reconcile FTK topology candidates with exact enemy, resource, and player routes.

This is an evidence-presence planner. It never promotes a binding record to art,
motion, gameplay, or all-family acceptance. It distinguishes a direct enemy row
from a resource-prefab override and from an explicitly classified player-only or
unsupported topology, so a zero direct enemy pair is not silently treated as a
missing enemy fixture.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any, Iterable


Json = dict[str, Any]


def read(path: Path) -> Any:
    return json.loads(path.read_text())


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resolve(root: Path, value: str | Path) -> Path:
    path = Path(value)
    return path if path.is_absolute() else root / path


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path)


def evidence_ref(root: Path, path: Path) -> Json:
    path = path.resolve()
    if not path.is_file():
        raise FileNotFoundError(path)
    return {"path": relative(root, path), "sha256": sha256(path)}


def first(*values: Any) -> Any:
    return next((value for value in values if value not in (None, "", [], {})), None)


def object_or_empty(value: Any) -> Json:
    return value if isinstance(value, dict) else {}


def distinct(values: Iterable[Any]) -> list[Any]:
    seen: set[str] = set()
    result: list[Any] = []
    for value in values:
        key = json.dumps(value, sort_keys=True) if isinstance(value, (dict, list)) else repr(value)
        if key not in seen:
            seen.add(key)
            result.append(value)
    return result


def renderer_path(renderer: Json, enemy: Json) -> str | None:
    names = list(reversed(renderer.get("ancestor_names", [])))
    prefab = enemy.get("prefab_name")
    if not names or names[0] != prefab:
        return None
    return "/".join(names[1:]) or "."


def direct_enemy_targets(mapping: Json) -> list[Json]:
    targets: list[Json] = []
    for enemy in mapping.get("enemies", []):
        base = enemy.get("enemy_id")
        if not isinstance(base, str) or not base:
            continue
        controllers = enemy.get("weapon_animation_controller_candidates", [])
        for renderer in enemy.get("renderers", []):
            path = renderer_path(renderer, enemy)
            renderer_id = renderer.get("renderer_path_id")
            if not isinstance(path, str) or not isinstance(renderer_id, int):
                continue
            for controller in controllers:
                controller_id = controller.get("controller_path_id")
                if not isinstance(controller_id, int):
                    continue
                targets.append({
                    "sourceKind": "native_enemy_row",
                    "nativeEnemy": base,
                    "resourcePrefab": None,
                    "rendererPath": path,
                    "sourceRendererId": renderer_id,
                    "rigProfile": renderer.get("rig_profile_fingerprint"),
                    "combatProfile": renderer.get("combat_profile_fingerprint"),
                    "controllerId": controller_id,
                    "controllerName": controller.get("controller_name"),
                })
    return distinct(targets)


def resource_targets(resources: Json) -> list[Json]:
    targets: list[Json] = []
    for row in resources.get("rows", []):
        base = row.get("base_enemy_id")
        resource = row.get("resource_load_path")
        path = row.get("renderer_path")
        renderer_id = row.get("renderer_id")
        controller = object_or_empty(row.get("weapon_controller"))
        controller_id = controller.get("controller_path_id")
        if not all(isinstance(value, str) and value for value in (base, resource, path)):
            continue
        if not isinstance(renderer_id, int) or not isinstance(controller_id, int):
            continue
        targets.append({
            "sourceKind": "resource_prefab_override",
            "nativeEnemy": base,
            "resourcePrefab": resource,
            "rendererPath": path,
            "sourceRendererId": renderer_id,
            "rigProfile": row.get("rig_profile_fingerprint"),
            "combatProfile": None,
            "controllerId": controller_id,
            "controllerName": controller.get("controller_name"),
        })
    return distinct(targets)


def profile_from_journal(value: Json, root: Path) -> Json:
    """Recover direct profile provenance from older JSONL evidence where present."""
    for field in ("stageJournal", "journal"):
        ref = object_or_empty(value.get(field))
        raw_path = ref.get("path")
        if not isinstance(raw_path, str):
            continue
        journal = resolve(root, raw_path)
        if not journal.is_file():
            continue
        for line in journal.read_text().splitlines():
            try:
                event = json.loads(line)
            except json.JSONDecodeError:
                continue
            profile = object_or_empty(object_or_empty(event.get("data")).get("profile"))
            if event.get("kind") == "provenance" and profile:
                return profile
    return {}


def declared_identity(value: Json, root: Path) -> Json:
    """Extract only declared exact source identity; never infer a compatible rig."""
    provenance = object_or_empty(value.get("provenance"))
    profile = object_or_empty(provenance.get("profile")) or object_or_empty(value.get("profile")) or profile_from_journal(value, root)
    scope = object_or_empty(value.get("scope"))
    binding = object_or_empty(value.get("binding"))
    sources = (profile, value, binding, scope)
    base = first(
        profile.get("baseEnemy"), profile.get("nativeEnemy"), profile.get("native_chassis"),
        value.get("nativeEnemy"), value.get("native_chassis"), value.get("nativeChassis"),
        value.get("native_template"), scope.get("nativeChassis"),
    )
    resource = first(profile.get("resourcePrefab"), value.get("resourcePrefab"), value.get("resource_prefab"), scope.get("resourcePrefab"))
    paths: list[str] = []
    source_ids: list[int] = []
    for source in sources:
        for key in ("rendererPath", "celRelativeRendererPath"):
            path = source.get(key)
            if isinstance(path, str) and path:
                paths.append(path)
        for key in ("rendererPaths", "renderer_paths"):
            values = source.get(key)
            if isinstance(values, list):
                paths.extend(item for item in values if isinstance(item, str) and item)
        renderers = source.get("renderers")
        if isinstance(renderers, list):
            for renderer in renderers:
                if isinstance(renderer, dict):
                    path = renderer.get("rendererPath")
                    if isinstance(path, str) and path:
                        paths.append(path)
        for key in ("rendererId", "sourceRendererId"):
            source_id = source.get(key)
            if isinstance(source_id, int):
                source_ids.append(source_id)
        for key in ("sourceRendererIds", "source_renderer_ids"):
            values = source.get(key)
            if isinstance(values, list):
                source_ids.extend(item for item in values if isinstance(item, int))
    return {
        "nativeEnemy": base,
        "resourcePrefab": resource,
        "rendererPaths": distinct(paths),
        "sourceRendererIds": distinct(source_ids),
        "status": first(value.get("status"), value.get("artStatus"), value.get("art_status")),
    }


def artifact_reference(entry: Json) -> Json:
    evidence = object_or_empty(entry.get("evidence"))
    archive = object_or_empty(evidence.get("archive"))
    if isinstance(archive.get("path"), str):
        return archive
    return evidence


def indexed_path_references(value: Any) -> list[Json]:
    """Inventory explicit path references nested in one runtime-index record.

    The runtime index keeps a primary archive in a conventional ``evidence``
    field, but historical results can also be pinned under a named follow-up or
    supplement. This helper inventories every explicit ``path`` object without
    deciding whether it is a primary archive, a supplement, or evidence for an
    acceptance gate. Consumers can therefore reconcile a package-local
    validation file with an explicitly named historical record without
    promoting that record to a new source-pair result.
    """
    references: list[Json] = []

    def escape(segment: str) -> str:
        return segment.replace("~", "~0").replace("/", "~1")

    def visit(node: Any, pointer: str) -> None:
        if isinstance(node, dict):
            path = node.get("path")
            if isinstance(path, str) and path:
                reference: Json = {"path": path, "jsonPointer": pointer or "/"}
                digest = node.get("sha256")
                if isinstance(digest, str) and digest:
                    reference["sha256"] = digest
                references.append(reference)
            for key, child in node.items():
                visit(child, f"{pointer}/{escape(str(key))}")
        elif isinstance(node, list):
            for index, child in enumerate(node):
                visit(child, f"{pointer}/{index}")

    visit(value, "")
    return references


def indexed_enemy_evidence(root: Path, index: Json) -> tuple[list[Json], list[Json]]:
    records: list[Json] = []
    unresolved: list[Json] = []
    for kind, key in (("diagnostic", "calibration_probes"), ("original", "enemy_models")):
        items = index.get(key, [])
        if not isinstance(items, list):
            continue
        for position, entry in enumerate(items):
            if not isinstance(entry, dict):
                unresolved.append({"kind": kind, "indexPointer": f"/{key}/{position}", "reason": "entry is not an object"})
                continue
            payload = entry
            artifact: Json | None = None
            if kind == "original":
                artifact = artifact_reference(entry)
                raw_path = artifact.get("path")
                if not isinstance(raw_path, str):
                    unresolved.append({"kind": kind, "indexPointer": f"/{key}/{position}", "reason": "missing evidence path"})
                    continue
                path = resolve(root, raw_path)
                if not path.is_file():
                    unresolved.append({"kind": kind, "indexPointer": f"/{key}/{position}", "reason": f"missing evidence artifact: {relative(root, path)}"})
                    continue
                try:
                    payload = read(path)
                except (OSError, json.JSONDecodeError) as error:
                    unresolved.append({"kind": kind, "indexPointer": f"/{key}/{position}", "reason": str(error)})
                    continue
            identity = declared_identity(payload, root)
            identity["nativeEnemy"] = first(identity["nativeEnemy"], entry.get("native_chassis"), entry.get("nativeEnemy"))
            identity["resourcePrefab"] = first(identity["resourcePrefab"], entry.get("resourcePrefab"))
            entry_paths = entry.get("renderer_paths", [])
            if isinstance(entry_paths, list):
                identity["rendererPaths"] = distinct(identity["rendererPaths"] + [item for item in entry_paths if isinstance(item, str)])
            entry_ids = entry.get("source_renderer_ids", [])
            if isinstance(entry_ids, list):
                identity["sourceRendererIds"] = distinct(identity["sourceRendererIds"] + [item for item in entry_ids if isinstance(item, int)])
            if not isinstance(identity["nativeEnemy"], str) or not identity["nativeEnemy"]:
                unresolved.append({"kind": kind, "indexPointer": f"/{key}/{position}", "reason": "no declared native enemy identity"})
                continue
            records.append({
                "kind": kind,
                "indexPointer": f"/{key}/{position}",
                "name": first(entry.get("model"), entry.get("name"), entry.get("native_chassis"), identity["nativeEnemy"]),
                "iteration": first(payload.get("iteration"), entry.get("iteration")),
                "identity": identity,
                "artifact": artifact,
                "status": first(entry.get("status"), identity.get("status"), "see indexed record"),
                "scope": "Indexed evidence for this exact source assignment only; it may be partial, failed, occluded, or diagnostic.",
            })
    return records, unresolved


def target_key(target: Json) -> tuple[Any, ...]:
    """Resource basename keeps an old/override source distinct from its direct row."""
    return (
        target.get("nativeEnemy"),
        target.get("resourcePrefab") or "",
        target.get("sourceRendererId"),
        target.get("rendererPath"),
        target.get("rigProfile"),
        target.get("controllerId"),
    )


def evidence_targets(record: Json, targets: list[Json]) -> list[Json]:
    identity = record["identity"]
    result = []
    for target in targets:
        if target["nativeEnemy"] != identity["nativeEnemy"]:
            continue
        if target.get("resourcePrefab") != identity.get("resourcePrefab"):
            continue
        paths = identity["rendererPaths"]
        source_ids = identity["sourceRendererIds"]
        if paths and target["rendererPath"] not in paths:
            continue
        if source_ids and target["sourceRendererId"] not in source_ids:
            continue
        result.append({**record, "target": target})
    return result


def indexed_player_evidence(root: Path, index: Json, ownership: Json) -> list[Json]:
    records: list[Json] = []
    classifications = ownership.get("classifications", [])
    if not isinstance(classifications, list):
        return records
    for position, entry in enumerate(index.get("original_player_models", [])):
        if not isinstance(entry, dict):
            continue
        artifact = artifact_reference(entry)
        raw_path = artifact.get("path")
        if not isinstance(raw_path, str):
            continue
        path = resolve(root, raw_path)
        if not path.is_file():
            continue
        try:
            payload = read(path)
        except (OSError, json.JSONDecodeError):
            continue
        profile = object_or_empty(payload.get("profile"))
        key = profile.get("key")
        if not isinstance(key, str):
            continue
        supplied_paths = {
            row.get("rendererPath") for section in ("renderers", "apparel")
            for row in profile.get(section, []) if isinstance(row, dict) and isinstance(row.get("rendererPath"), str)
        }
        for classification in classifications:
            profiles = classification.get("profileKeys", [])
            route_paths = set(classification.get("rendererPaths", []))
            if key in profiles and route_paths and route_paths <= supplied_paths:
                records.append({
                    "topologyGroup": classification["topologyGroup"],
                    "indexPointer": f"/original_player_models/{position}",
                    "name": entry.get("name", key),
                    "profile": key,
                    "skinset": profile.get("skinset"),
                    "rendererPaths": sorted(route_paths),
                    "artifact": artifact,
                    "status": entry.get("status", payload.get("status")),
                    "scope": "Separate player-avatar evidence. It is not an enemy-row assignment or topology-wide player acceptance.",
                })
    return records


def classification_by_group(ownership: Json) -> dict[str, Json]:
    rows = ownership.get("classifications", [])
    if not isinstance(rows, list):
        raise ValueError("ownership.classifications must be a list")
    result: dict[str, Json] = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("ownership classification must be an object")
        group = row.get("topologyGroup")
        if not isinstance(group, str) or not group:
            raise ValueError("ownership classification lacks topologyGroup")
        if group in result:
            raise ValueError(f"duplicate ownership classification: {group}")
        result[group] = row
    return result


def build_plan(root: Path, candidates: Json, mapping: Json, index: Json, resources: Json,
               catalog: Json, ownership: Json, inputs: list[Path]) -> Json:
    families = candidates.get("candidate_families", [])
    if not isinstance(families, list):
        raise ValueError("candidate_families must be a list")
    ownership_by_group = classification_by_group(ownership)
    targets = direct_enemy_targets(mapping) + resource_targets(resources)
    evidence, unresolved = indexed_enemy_evidence(root, index)
    events = [event for record in evidence for event in evidence_targets(record, targets)]
    player_events = indexed_player_evidence(root, index, ownership)
    groups: list[Json] = []
    for family in families:
        group = family.get("topology_fingerprint")
        source_ids = family.get("renderer_path_ids", [])
        if not isinstance(group, str) or not isinstance(source_ids, list):
            raise ValueError("candidate family lacks topology_fingerprint or renderer_path_ids")
        native = [target for target in targets if target["sourceRendererId"] in source_ids]
        direct = [target for target in native if target["sourceKind"] == "native_enemy_row"]
        resources_for_group = [target for target in native if target["sourceKind"] == "resource_prefab_override"]
        group_events = [event for event in events if event["target"] in native]
        direct_events = [event for event in group_events if event["target"]["sourceKind"] == "native_enemy_row"]
        resource_events = [event for event in group_events if event["target"]["sourceKind"] == "resource_prefab_override"]
        original_events = [event for event in group_events if event["kind"] == "original"]
        direct_original_events = [event for event in direct_events if event["kind"] == "original"]
        resource_original_events = [event for event in resource_events if event["kind"] == "original"]
        player = [event for event in player_events if event["topologyGroup"] == group]
        keys = {target_key(target) for target in native}
        seen = {target_key(event["target"]) for event in group_events}
        original_seen = {target_key(event["target"]) for event in original_events}
        representatives: dict[tuple[Any, ...], Json] = {}
        for target in native:
            representatives.setdefault(target_key(target), target)
        remaining = [target for key, target in representatives.items() if key not in seen]
        classification = ownership_by_group.get(group)
        if direct_events:
            status = "direct_enemy_evidence_not_family_acceptance"
            remaining_checks = [
                "Retain partial, failed, occluded, and diagnostic limits for every indexed record.",
                "Test each remaining exact source/bind/controller pair; no acceptance transfers within a topology.",
                "For a selected original, verify appearance, idle, native attack, successful nonlethal damage, death, progression, material/portrait, culling, and lifetime separately.",
            ]
        elif resource_events:
            status = "resource_prefab_evidence_separate_not_native_enemy_acceptance"
            remaining_checks = [
                "Retain the resource-prefab evidence with its exact diagnostic/partial limits.",
                "Do not count this route as direct native-enemy-row coverage or infer compatibility with the base prefab.",
            ]
        elif native:
            status = "exact_source_pair_without_indexed_evidence"
            remaining_checks = [
                "The exact source route is known but has no indexed evidence. Plan a route-specific trial without borrowing a direct-enemy or resource result.",
            ]
        elif classification:
            status = classification.get("status", "explicit_non_enemy_or_unsupported_classification")
            remaining_checks = classification.get("remainingChecks", [])
        else:
            status = "unresolved_ownership_no_native_enemy_row"
            remaining_checks = [
                "Resolve ownership or resource provenance before proposing an enemy fixture.",
                "A zero native pair is neither completed coverage nor proof that the candidate is unused.",
            ]
        group_data = {
            "topologyGroup": group,
            "joints": family.get("joint_count"),
            "sourceRendererCount": family.get("renderer_count"),
            "sourceRendererIds": source_ids,
            "meshExamples": family.get("mesh_names", [])[:6],
            "controllerNames": family.get("controller_names", []),
            "directEnemyEvidence": direct_events,
            "resourcePrefabEvidence": resource_events,
            "directEnemyOriginalEvidence": direct_original_events,
            "resourcePrefabOriginalEvidence": resource_original_events,
            "playerEvidence": player,
            "nativeDirectEnemyPairs": len({target_key(target) for target in direct}),
            "nativeResourcePrefabPairs": len({target_key(target) for target in resources_for_group}),
            "exactSourcePairs": len(keys),
            "exactSourcePairsWithIndexedOriginalEvidence": len(keys & original_seen),
            "exactSourcePairsWithIndexedEvidence": len(keys & seen),
            "remainingExactSourcePairRepresentatives": remaining,
            "status": status,
            "remainingChecks": remaining_checks,
            "evidenceInterpretation": "evidence_presence_not_PASS_counts; original means an indexed authored-model record rather than calibration-only evidence",
        }
        if classification:
            group_data["ownershipClassification"] = classification
        groups.append(group_data)

    direct_groups = sum(bool(group["nativeDirectEnemyPairs"]) for group in groups)
    resource_groups = sum(bool(group["nativeResourcePrefabPairs"]) for group in groups)
    explicit = [group for group in groups if "ownershipClassification" in group]
    summary = {
        "topologyGroups": len(groups),
        "groupsWithAnyIndexedDirectEnemyEvidence": sum(bool(group["directEnemyEvidence"]) for group in groups),
        "groupsWithAnyIndexedResourcePrefabEvidence": sum(bool(group["resourcePrefabEvidence"]) for group in groups),
        "groupsWithAnyIndexedPlayerEvidence": sum(bool(group["playerEvidence"]) for group in groups),
        "groupsWithAnyIndexedOriginalDirectEnemyEvidence": sum(bool(group["directEnemyOriginalEvidence"]) for group in groups),
        "groupsWithAnyIndexedOriginalResourcePrefabEvidence": sum(bool(group["resourcePrefabOriginalEvidence"]) for group in groups),
        "groupsWithDirectNativeEnemyRows": direct_groups,
        "groupsWithResourcePrefabRows": resource_groups,
        "groupsWithExplicitOwnershipClassification": len(explicit),
        "explicitPlayerAvatarGroups": sum(group["ownershipClassification"].get("category") in {"player_avatar", "player_avatar_plus_unbound_resource"} for group in explicit),
        "explicitUnsupportedGroups": sum(group["ownershipClassification"].get("category") == "unsupported_empty" for group in explicit),
        "explicitResourcePrefabGroups": sum(group["ownershipClassification"].get("category") in {"resource_prefab_variant", "player_avatar_plus_unbound_resource"} for group in explicit),
        "unresolvedOwnershipGroups": sum(group["status"] == "unresolved_ownership_no_native_enemy_row" for group in groups),
        "unresolvedEvidenceRecords": len(unresolved),
    }
    return {
        "schemaVersion": 3,
        "scope": "Planning snapshot of candidate topology groups, joined by exact direct enemy rows, resource-prefab overrides, and explicit non-enemy ownership classifications. Not an acceptance ledger.",
        "inputs": [evidence_ref(root, path) for path in inputs],
        "denominators": {
            "candidateTopologyGroups": len(groups),
            "validRawBindProfiles": 384,
            "baselineCelBindProfiles": 230,
            "additionalRawBindProfiles": 154,
            "stagedEnemyTestProfiles": len(catalog.get("profiles", [])),
            "meaning": "Distinct populations. Source pairs include direct enemy rows and resource-prefab overrides; topology does not transfer acceptance across variants.",
        },
        "summary": summary,
        "evidencePresence": summary,
        "countInterpretation": "Evidence presence only, never a PASS count. Original evidence identifies an indexed authored-model record; it does not establish every source pair or a live acceptance gate.",
        "groups": groups,
        "unresolvedRecords": unresolved,
        "separateEvidenceSections": [
            {"indexPointer": "/original_player_models", "recordCount": len(index.get("original_player_models", [])), "scope": "Player archives are separately joined only through explicit ownership routes."},
            {"indexPointer": "/kraken_native_controller_reference_trials", "recordCount": len(index.get("kraken_native_controller_reference_trials", [])), "scope": "Numerical/fixture evidence remains separate from live source injection."},
        ],
        "limits": [
            "Evidence presence does not establish art, full motion, gameplay, culling, portraits, or lifetime acceptance.",
            "An exact resource-prefab override is not interchangeable with its direct native enemy prefab.",
            "Player evidence is never counted as enemy-row evidence.",
            "Unclassified zero-pair groups remain unresolved rather than silently excluded.",
        ],
    }


def markdown(plan: Json) -> str:
    summary = json.dumps(plan["summary"], sort_keys=True)
    lines = [
        "# Candidate topology coverage planning snapshot",
        "",
        summary,
        "",
        "**Evidence presence, not PASS counts.** Direct enemy rows, resource-prefab overrides, player avatars, and unsupported empty renderers have separate routes. `Original` means an indexed authored-model record, not a full live verdict. A zero source pair is not complete coverage.",
        "",
        "| Topology | Joints | Examples | Direct / resource pairs | Original / any / known pairs | Route or remaining work |",
        "|---|---:|---|---:|---:|---|",
    ]
    for group in plan["groups"]:
        ownership = group.get("ownershipClassification", {})
        remaining = len(group["remainingExactSourcePairRepresentatives"])
        if not group["nativeDirectEnemyPairs"] and ownership:
            route = ownership.get("summary", group["status"])
            if group["nativeResourcePrefabPairs"]:
                route += f" ({remaining} unrecorded resource source pair(s))"
        elif group["exactSourcePairs"]:
            route = str(remaining) + " unrecorded exact source pair(s)"
        else:
            route = ownership.get("summary", group["status"])
        lines.append("|" + "|".join([
            str(group["topologyGroup"]), str(group["joints"]), ", ".join(group["meshExamples"][:3]),
            f"{group['nativeDirectEnemyPairs']} / {group['nativeResourcePrefabPairs']}",
            f"{group['exactSourcePairsWithIndexedOriginalEvidence']}/{group['exactSourcePairsWithIndexedEvidence']}/{group['exactSourcePairs']}", route,
        ]) + "|")
    backlog: list[tuple[Json, Json, int]] = []
    for group in plan["groups"]:
        remaining = group["remainingExactSourcePairRepresentatives"]
        if not remaining:
            continue
        first_route = sorted(
            remaining,
            key=lambda target: (
                str(target.get("sourceKind") or ""),
                str(target.get("nativeEnemy") or ""),
                str(target.get("resourcePrefab") or ""),
                str(target.get("rendererPath") or ""),
                str(target.get("controllerName") or ""),
                str(target.get("sourceRendererId") or ""),
            ),
        )[0]
        backlog.append((group, first_route, len(remaining) - 1))
    if backlog:
        lines += [
            "",
            "## First unrecorded exact route per incomplete topology",
            "",
            "Each row is a deterministic starting point for the next authoring and live-trial package. It does not make sibling routes covered; the final column states how many further exact routes remain in that topology.",
            "",
            "| Topology | Native route | Renderer / source | Controller | Further exact routes |",
            "|---|---|---|---|---:|",
        ]
        for group, target, additional in backlog:
            resource = target.get("resourcePrefab")
            native = f"`{target.get('nativeEnemy')}`"
            if resource:
                native += f" → resource `{resource}`"
            renderer = f"`{target.get('rendererPath')}` / {target.get('sourceRendererId')}"
            controller = f"`{target.get('controllerName')}` / {target.get('controllerId')}"
            lines.append("|" + "|".join([
                str(group["topologyGroup"]), native, renderer, controller, str(additional),
            ]) + "|")
    lines += [
        "",
        "Inspect every exact record before planning a new capture. A source-pair evidence count includes failures and partial trials; it does not approve a topology, related controller, or sibling renderer.",
        "",
    ]
    return "\n".join(lines)


def write_new(path: Path, content: str, overwrite: bool) -> None:
    if path.exists() and not overwrite:
        raise FileExistsError(f"Refusing to overwrite {path}; pass --overwrite after reviewing the generated result.")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(content)
    temporary.replace(path)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--candidates", type=Path, default=Path("docs/model-skeleton-candidates.json"))
    parser.add_argument("--mapping", type=Path, default=Path("scratch/enemy-rig-mapping-reproducible.json"))
    parser.add_argument("--runtime-index", type=Path, default=Path("docs/model-runtime-validation.json"))
    parser.add_argument("--resources", type=Path, default=Path("scratch/resource-enemy-base-preflight.json"))
    parser.add_argument("--catalog", type=Path, default=Path("scratch/mirewarden-game/model-test-profiles.json"))
    parser.add_argument("--ownership", type=Path, required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    paths = [resolve(root, value) for value in (args.candidates, args.mapping, args.runtime_index, args.resources, args.catalog, args.ownership)]
    candidates, mapping, index, resources, catalog, ownership = (read(path) for path in paths)
    plan = build_plan(root, candidates, mapping, index, resources, catalog, ownership, paths)
    write_new(resolve(root, args.output_json), json.dumps(plan, indent=2) + "\n", args.overwrite)
    write_new(resolve(root, args.output_markdown), markdown(plan), args.overwrite)
    print(json.dumps(plan["summary"], indent=2))


if __name__ == "__main__":
    main()
