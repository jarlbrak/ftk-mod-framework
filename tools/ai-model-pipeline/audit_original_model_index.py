#!/usr/bin/env python3
"""Reconcile original-model live evidence with the exact native-rig index.

This is an evidence-presence audit. It never treats an indexed trial as an art or
gameplay PASS. Its purpose is to catch an original-model archive that has been
created but was not added to docs/model-runtime-validation.json. Explicitly
indexed constructed-fixture records outside the original-model collections are
reported separately so they are not mistaken for orphaned runtime archives.
"""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys
from typing import Any

import audit_topology_coverage as coverage


def read_json(path: Path) -> Any:
    return json.loads(path.read_text())


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path)


def first_nonempty(*values: Any) -> Any:
    for value in values:
        if value not in (None, "", [], {}):
            return value
    return None


def as_dict(value: Any) -> dict[str, Any]:
    return value if isinstance(value, dict) else {}


def dedupe(values: list[Any]) -> list[Any]:
    seen: set[Any] = set()
    out: list[Any] = []
    for value in values:
        key = json.dumps(value, sort_keys=True) if isinstance(value, (dict, list)) else value
        if key not in seen:
            seen.add(key)
            out.append(value)
    return out


def renderer_path(renderer: dict[str, Any], enemy: dict[str, Any]) -> str | None:
    """Return the exact CEL-relative renderer path used by the mapping audit."""
    names = list(reversed(renderer.get("ancestor_names", [])))
    prefab = enemy.get("prefab_name")
    if not names or names[0] != prefab:
        return None
    return "/".join(names[1:]) or "."


def mapping_tables(
    mapping: dict[str, Any], resources: dict[str, Any]
) -> tuple[dict[tuple[str, str], list[dict[str, Any]]], dict[int, list[dict[str, Any]]], dict[str, list[dict[str, Any]]]]:
    by_assignment: dict[tuple[str, str], list[dict[str, Any]]] = {}
    by_renderer_id: dict[int, list[dict[str, Any]]] = {}
    by_resource: dict[str, list[dict[str, Any]]] = {}
    for enemy in mapping.get("enemies", []):
        base = enemy.get("enemy_id")
        if not isinstance(base, str):
            continue
        for renderer in enemy.get("renderers", []):
            path = renderer_path(renderer, enemy)
            renderer_id = renderer.get("renderer_path_id")
            if not isinstance(path, str) or not isinstance(renderer_id, int):
                continue
            record = {
                "nativeEnemy": base,
                "rendererPath": path,
                "sourceRendererId": renderer_id,
                "rigProfile": renderer.get("rig_profile_fingerprint"),
                "combatProfile": renderer.get("combat_profile_fingerprint"),
            }
            by_assignment.setdefault((base, path), []).append(record)
            by_renderer_id.setdefault(renderer_id, []).append(record)
    for row in resources.get("rows", []):
        base = row.get("base_enemy_id")
        path = row.get("renderer_path")
        renderer_id = row.get("renderer_id")
        resource = row.get("resource_load_path")
        if not all(isinstance(value, str) and value for value in (base, path, resource)) or not isinstance(renderer_id, int):
            continue
        controller = as_dict(row.get("weapon_controller"))
        record = {
            "nativeEnemy": base,
            "rendererPath": path,
            "sourceRendererId": renderer_id,
            "rigProfile": row.get("rig_profile_fingerprint"),
            "combatProfile": None,
            "resourcePrefab": resource,
            "controllerId": controller.get("controller_path_id"),
            "controllerName": controller.get("controller_name"),
        }
        by_assignment.setdefault((base, path), []).append(record)
        by_renderer_id.setdefault(renderer_id, []).append(record)
        by_resource.setdefault(resource, []).append(record)
    return by_assignment, by_renderer_id, by_resource


def profile_from_journal(value: dict[str, Any], root: Path) -> dict[str, Any]:
    """Recover stage provenance when a historical validation omitted it directly."""
    for field in ("stageJournal", "journal"):
        reference = as_dict(value.get(field))
        raw_path = reference.get("path")
        if not isinstance(raw_path, str):
            continue
        journal = Path(raw_path)
        journal = journal if journal.is_absolute() else root / journal
        if not journal.is_file():
            continue
        for line in journal.read_text().splitlines():
            try:
                row = json.loads(line)
            except json.JSONDecodeError:
                continue
            profile = as_dict(as_dict(row.get("data")).get("profile"))
            if row.get("kind") == "provenance" and profile:
                return profile
    return {}


def evidence_identity(value: Any, root: Path) -> dict[str, Any] | None:
    """Extract only declared native identity, never infer a compatible family."""
    if not isinstance(value, dict):
        return None
    provenance = as_dict(value.get("provenance"))
    profile = as_dict(provenance.get("profile")) or as_dict(value.get("profile")) or profile_from_journal(value, root)
    scope = as_dict(value.get("scope"))
    binding = as_dict(value.get("binding"))
    sources = [profile, value, binding, scope]
    base = first_nonempty(
        profile.get("baseEnemy"),
        profile.get("nativeEnemy"),
        profile.get("native_chassis"),
        profile.get("nativeChassis"),
        value.get("nativeEnemy"),
        value.get("native_chassis"),
        value.get("nativeChassis"),
        value.get("native_template"),
        scope.get("nativeChassis"),
    )
    resource = first_nonempty(
        profile.get("resourcePrefab"),
        value.get("resourcePrefab"),
        value.get("resource_prefab"),
        scope.get("resourcePrefab"),
    )
    paths: list[str] = []
    renderer_ids: list[int] = []
    for source in sources:
        for key in ("rendererPath", "celRelativeRendererPath"):
            path = source.get(key)
            if isinstance(path, str) and path:
                paths.append(path)
        listed_paths = first_nonempty(source.get("renderer_paths"), source.get("rendererPaths"))
        if isinstance(listed_paths, list):
            paths.extend(path for path in listed_paths if isinstance(path, str) and path)
        renderers = source.get("renderers")
        if isinstance(renderers, list):
            for renderer in renderers:
                if isinstance(renderer, dict) and isinstance(renderer.get("rendererPath"), str):
                    paths.append(renderer["rendererPath"])
        for key in ("rendererId", "sourceRendererId"):
            renderer_id = source.get(key)
            if isinstance(renderer_id, int):
                renderer_ids.append(renderer_id)
        for key in ("source_renderer_ids", "sourceRendererIds"):
            listed_ids = source.get(key)
            if isinstance(listed_ids, list):
                renderer_ids.extend(renderer_id for renderer_id in listed_ids if isinstance(renderer_id, int))
    member_identities: list[dict[str, Any]] = []
    for container_name in ("bindings", "trials"):
        container = value.get(container_name)
        members = container.values() if isinstance(container, dict) else container if isinstance(container, list) else []
        for member in members:
            if not isinstance(member, dict):
                continue
            member_profile = as_dict(member.get("profile"))
            member_owner = as_dict(member.get("owner"))
            member_binding = as_dict(member.get("binding"))
            member_base = first_nonempty(
                member_profile.get("baseEnemy"),
                member.get("nativeBase"),
                member.get("native_chassis"),
                member.get("nativeChassis"),
                member.get("nativeEnemy"),
            )
            if not isinstance(member_base, str) or not member_base:
                continue
            member_paths: list[str] = []
            member_ids: list[int] = []
            for source in (member_profile, member, member_owner, member_binding):
                for key in ("rendererPath", "celRelativeRendererPath"):
                    path = source.get(key)
                    if isinstance(path, str) and path:
                        member_paths.append(path)
                listed_paths = first_nonempty(source.get("renderer_paths"), source.get("rendererPaths"))
                if isinstance(listed_paths, list):
                    member_paths.extend(path for path in listed_paths if isinstance(path, str) and path)
                for key in ("rendererId", "sourceRendererId"):
                    renderer_id = source.get(key)
                    if isinstance(renderer_id, int):
                        member_ids.append(renderer_id)
                listed_ids = first_nonempty(source.get("source_renderer_ids"), source.get("sourceRendererIds"))
                if isinstance(listed_ids, list):
                    member_ids.extend(renderer_id for renderer_id in listed_ids if isinstance(renderer_id, int))
            member_identities.append({
                "nativeEnemy": member_base,
                "resourcePrefab": first_nonempty(member_profile.get("resourcePrefab"), member.get("resourcePrefab")),
                "declaredRendererPaths": dedupe(member_paths),
                "declaredRendererIds": dedupe(member_ids),
            })
    return {
        "nativeEnemy": base,
        "resourcePrefab": resource,
        "declaredRendererPaths": dedupe(paths),
        "declaredRendererIds": dedupe(renderer_ids),
        "status": first_nonempty(value.get("status"), value.get("artStatus"), value.get("art_status")),
        "memberIdentities": dedupe(member_identities),
    }


def player_evidence_identity(value: Any) -> dict[str, Any] | None:
    """Recognize a declared player archive without forcing it through enemy maps."""
    if not isinstance(value, dict):
        return None
    profile = as_dict(value.get("profile"))
    if not profile or not isinstance(profile.get("key"), str):
        return None
    if not all(isinstance(profile.get(key), str) and profile[key]
               for key in ("baseClass", "skinset", "defaultSkinType")):
        return None
    if any(key in profile for key in ("baseEnemy", "nativeEnemy", "resourcePrefab")):
        return None
    renderer_paths = [
        row["rendererPath"]
        for row in profile.get("renderers", [])
        if isinstance(row, dict) and isinstance(row.get("rendererPath"), str)
    ]
    apparel_paths = [
        row["rendererPath"]
        for row in profile.get("apparel", [])
        if isinstance(row, dict) and isinstance(row.get("rendererPath"), str)
    ]
    if not renderer_paths:
        return None
    return {
        "profile": profile["key"],
        "baseClass": profile["baseClass"],
        "skinset": profile["skinset"],
        "rendererPaths": dedupe(renderer_paths),
        "apparelPaths": dedupe(apparel_paths),
        "status": first_nonempty(value.get("status"), value.get("artStatus"), value.get("art_status")),
    }


def resolve_assignments(
    identity: dict[str, Any],
    by_assignment: dict[tuple[str, str], list[dict[str, Any]]],
    by_renderer_id: dict[int, list[dict[str, Any]]],
    by_resource: dict[str, list[dict[str, Any]]],
) -> list[dict[str, Any]]:
    resolved: list[dict[str, Any]] = []
    for member in identity.get("memberIdentities", []):
        if isinstance(member, dict):
            resolved.extend(resolve_assignments(member, by_assignment, by_renderer_id, by_resource))
    base = identity.get("nativeEnemy")
    resource = identity.get("resourcePrefab")
    declared_paths = identity.get("declaredRendererPaths", [])
    if isinstance(resource, str):
        resource_records = by_resource.get(resource, [])
        for record in resource_records:
            if base is not None and record["nativeEnemy"] != base:
                continue
            if declared_paths and record["rendererPath"] not in declared_paths:
                continue
            resolved.append(record)
    for path in declared_paths:
        if isinstance(base, str):
            resolved.extend(by_assignment.get((base, path), []))
    for renderer_id in identity.get("declaredRendererIds", []):
        for record in by_renderer_id.get(renderer_id, []):
            if base is None or record["nativeEnemy"] == base:
                resolved.append(record)
    return dedupe(resolved)


def discover_artifacts(experiments: Path) -> list[Path]:
    paths = set(experiments.rglob("live-validation*.json"))
    paths.update(path for path in experiments.rglob("validation.json") if path.parent.name.startswith("live-validation"))
    return sorted(
        path for path in paths
        if "offline-history" not in path.parts and "metadata" not in path.parts
    )


def is_archive_plan(value: Any) -> bool:
    """Keep archive-builder inputs out of the runtime-evidence inventory."""
    schema = as_dict(value).get("schema")
    return isinstance(schema, str) and schema.startswith("ftkmf.model-validation-archive-plan.")


def add_index_fallback(identity: dict[str, Any] | None, entry: dict[str, Any]) -> dict[str, Any]:
    identity = dict(identity or {})
    identity["nativeEnemy"] = first_nonempty(identity.get("nativeEnemy"), entry.get("native_chassis"))
    identity["resourcePrefab"] = first_nonempty(identity.get("resourcePrefab"), entry.get("resourcePrefab"))
    paths = list(identity.get("declaredRendererPaths", []))
    paths.extend(path for path in entry.get("renderer_paths", []) if isinstance(path, str))
    ids = list(identity.get("declaredRendererIds", []))
    ids.extend(renderer_id for renderer_id in entry.get("source_renderer_ids", []) if isinstance(renderer_id, int))
    identity["declaredRendererPaths"] = dedupe(paths)
    identity["declaredRendererIds"] = dedupe(ids)
    identity["status"] = first_nonempty(entry.get("status"), identity.get("status"))
    return identity


def archive_evidence_reference(entry: dict[str, Any]) -> dict[str, Any]:
    """Accept the direct enemy shape and the nested player archive shape."""
    evidence = as_dict(entry.get("evidence"))
    archive = as_dict(evidence.get("archive"))
    return archive if isinstance(archive.get("path"), str) else evidence


def indexed_entry_paths(root: Path, entry: dict[str, Any]) -> set[str]:
    """Return every path explicitly pinned by one runtime-index entry.

    The conventional root archive remains the only record used to resolve the
    entry's exact native assignment. Extra paths are retained here solely so a
    named historical supplement is not falsely reported as an unindexed local
    archive.
    """
    paths: set[str] = set()
    for reference in coverage.indexed_path_references(entry):
        raw_path = reference["path"]
        path = Path(raw_path)
        path = path if path.is_absolute() else root / path
        paths.add(relative(root, path))
    return paths


def separately_indexed_references(root: Path, index: dict[str, Any]) -> dict[str, list[dict[str, Any]]]:
    """Find explicitly pinned artifacts in non-original runtime-index sections.

    These references can describe a constructed deformation fixture or another
    intentionally non-runtime trial. They are preserved as separately indexed
    facts, rather than silently entering the original enemy/player assignment
    ledger.
    """
    result: dict[str, list[dict[str, Any]]] = {}
    original_collections = {"enemy_models", "original_player_models"}
    for collection, value in index.items():
        if collection in original_collections:
            continue
        if isinstance(value, list):
            members = [(f"/{collection}/{position}", entry) for position, entry in enumerate(value)]
        elif isinstance(value, dict):
            members = [(f"/{collection}", value)]
        else:
            continue
        for pointer, entry in members:
            if not isinstance(entry, dict):
                continue
            for reference in coverage.indexed_path_references(entry):
                path = Path(reference["path"])
                path = path if path.is_absolute() else root / path
                relative_path = relative(root, path)
                suffix = reference["jsonPointer"]
                result.setdefault(relative_path, []).append({
                    "indexPointer": pointer,
                    "evidencePointer": pointer if suffix == "/" else f"{pointer}{suffix}",
                    "sha256": reference.get("sha256"),
                })
    for references in result.values():
        references.sort(key=lambda item: (item["indexPointer"], item["evidencePointer"]))
    return result


def audit(root: Path, index_path: Path, mapping_path: Path, resources_path: Path, experiments: Path) -> dict[str, Any]:
    index = read_json(index_path)
    mapping = read_json(mapping_path)
    resources = read_json(resources_path)
    by_assignment, by_renderer_id, by_resource = mapping_tables(mapping, resources)
    indexed_paths: set[str] = set()
    indexed_assignments: set[tuple[str, str]] = set()
    missing_index_artifacts: list[dict[str, Any]] = []
    hash_mismatches: list[dict[str, Any]] = []
    unresolved_index_entries: list[dict[str, Any]] = []
    indexed_player_paths: set[str] = set()
    missing_player_index_artifacts: list[dict[str, Any]] = []
    player_hash_mismatches: list[dict[str, Any]] = []
    unresolved_player_index_entries: list[dict[str, Any]] = []
    non_original_index_references = separately_indexed_references(root, index)

    for position, entry in enumerate(index.get("enemy_models", [])):
        if isinstance(entry, dict):
            indexed_paths.update(indexed_entry_paths(root, entry))
        evidence = archive_evidence_reference(entry)
        evidence_path = evidence.get("path")
        if not isinstance(evidence_path, str):
            unresolved_index_entries.append({"index": position, "reason": "missing evidence.path"})
            continue
        path = Path(evidence_path)
        path = path if path.is_absolute() else root / path
        rel_path = relative(root, path)
        indexed_paths.add(rel_path)
        if not path.is_file():
            missing_index_artifacts.append({"index": position, "path": rel_path})
            continue
        expected_hash = evidence.get("sha256")
        actual_hash = sha256(path)
        if isinstance(expected_hash, str) and expected_hash != actual_hash:
            hash_mismatches.append({"index": position, "path": rel_path, "expected": expected_hash, "actual": actual_hash})
        try:
            identity = add_index_fallback(evidence_identity(read_json(path), root), entry)
        except (OSError, json.JSONDecodeError) as error:
            unresolved_index_entries.append({"index": position, "path": rel_path, "reason": str(error)})
            continue
        assignments = resolve_assignments(identity, by_assignment, by_renderer_id, by_resource)
        if not assignments:
            unresolved_index_entries.append({
                "index": position,
                "path": rel_path,
                "nativeEnemy": identity.get("nativeEnemy"),
                "declaredRendererPaths": identity.get("declaredRendererPaths", []),
                "declaredRendererIds": identity.get("declaredRendererIds", []),
                "reason": "no unambiguous exact native renderer mapping",
            })
            continue
        indexed_assignments.update((item["nativeEnemy"], item["rendererPath"]) for item in assignments)

    for position, entry in enumerate(index.get("original_player_models", [])):
        if isinstance(entry, dict):
            indexed_player_paths.update(indexed_entry_paths(root, entry))
        evidence = archive_evidence_reference(entry)
        evidence_path = evidence.get("path")
        if not isinstance(evidence_path, str):
            unresolved_player_index_entries.append({"index": position, "reason": "missing evidence.archive.path"})
            continue
        path = Path(evidence_path)
        path = path if path.is_absolute() else root / path
        rel_path = relative(root, path)
        indexed_player_paths.add(rel_path)
        if not path.is_file():
            missing_player_index_artifacts.append({"index": position, "path": rel_path})
            continue
        expected_hash = evidence.get("sha256")
        actual_hash = sha256(path)
        if isinstance(expected_hash, str) and expected_hash != actual_hash:
            player_hash_mismatches.append({"index": position, "path": rel_path, "expected": expected_hash, "actual": actual_hash})
        try:
            identity = player_evidence_identity(read_json(path))
        except (OSError, json.JSONDecodeError) as error:
            unresolved_player_index_entries.append({"index": position, "path": rel_path, "reason": str(error)})
            continue
        if identity is None:
            unresolved_player_index_entries.append({"index": position, "path": rel_path, "reason": "not a declared player archive"})
            continue
        if entry.get("profile") != identity["profile"] or entry.get("skinset") != identity["skinset"]:
            unresolved_player_index_entries.append({
                "index": position,
                "path": rel_path,
                "reason": "index profile or skinset differs from archive identity",
            })

    direct_indexed: list[dict[str, Any]] = []
    unindexed_known: list[dict[str, Any]] = []
    unindexed_novel: list[dict[str, Any]] = []
    directly_indexed_players: list[dict[str, Any]] = []
    unindexed_players: list[dict[str, Any]] = []
    separately_indexed_non_runtime: list[dict[str, Any]] = []
    unresolved_artifacts: list[dict[str, Any]] = []
    skipped_nonobjects: list[dict[str, Any]] = []
    invalid_json: list[dict[str, Any]] = []
    for artifact in discover_artifacts(experiments):
        rel_path = relative(root, artifact)
        try:
            content = read_json(artifact)
        except (OSError, json.JSONDecodeError) as error:
            invalid_json.append({"path": rel_path, "reason": str(error)})
            continue
        if is_archive_plan(content):
            continue
        if (rel_path not in indexed_paths and rel_path not in indexed_player_paths
                and rel_path in non_original_index_references):
            separately_indexed_non_runtime.append({
                "path": rel_path,
                "sha256": sha256(artifact),
                "indexReferences": non_original_index_references[rel_path],
            })
            continue
        identity = evidence_identity(content, root)
        player_identity = player_evidence_identity(content)
        if player_identity is not None:
            player_record = {
                "path": rel_path,
                "sha256": sha256(artifact),
                **player_identity,
            }
            if rel_path in indexed_player_paths:
                directly_indexed_players.append(player_record)
            else:
                unindexed_players.append(player_record)
            continue
        if identity is None:
            skipped_nonobjects.append({"path": rel_path, "jsonType": type(content).__name__})
            continue
        assignments = resolve_assignments(identity, by_assignment, by_renderer_id, by_resource)
        record = {
            "path": rel_path,
            "sha256": sha256(artifact),
            "status": identity.get("status"),
            "nativeEnemy": identity.get("nativeEnemy"),
            "declaredRendererPaths": identity.get("declaredRendererPaths", []),
            "declaredRendererIds": identity.get("declaredRendererIds", []),
            "exactAssignments": assignments,
        }
        if rel_path in indexed_paths:
            direct_indexed.append(record)
        elif not assignments:
            unresolved_artifacts.append(record)
        elif any((item["nativeEnemy"], item["rendererPath"]) in indexed_assignments for item in assignments):
            unindexed_known.append(record)
        else:
            unindexed_novel.append(record)

    summary = {
        "discoveredOriginalEvidenceArtifacts": len(direct_indexed) + len(unindexed_known) + len(unindexed_novel) + len(unresolved_artifacts),
        "directlyIndexedArtifacts": len(direct_indexed),
        "unindexedKnownExactAssignments": len(unindexed_known),
        "unindexedNovelExactAssignments": len(unindexed_novel),
        "directlyIndexedPlayerArtifacts": len(directly_indexed_players),
        "unindexedPlayerArtifacts": len(unindexed_players),
        "separatelyIndexedNonRuntimeArtifacts": len(separately_indexed_non_runtime),
        "unresolvedArtifacts": len(unresolved_artifacts),
        "skippedNonObjectJson": len(skipped_nonobjects),
        "invalidJson": len(invalid_json),
        "indexedEnemyModelEntries": len(index.get("enemy_models", [])),
        "indexedOriginalPlayerModelEntries": len(index.get("original_player_models", [])),
        "indexEntriesWithMissingArtifact": len(missing_index_artifacts),
        "indexEvidenceHashMismatches": len(hash_mismatches),
        "indexEntriesWithoutExactNativeMapping": len(unresolved_index_entries),
        "playerIndexEntriesWithMissingArtifact": len(missing_player_index_artifacts),
        "playerIndexEvidenceHashMismatches": len(player_hash_mismatches),
        "playerIndexEntriesWithoutDeclaredArchiveIdentity": len(unresolved_player_index_entries),
    }
    return {
        "scope": "Original-model archive/index reconciliation. Presence and exact native assignment only; this does not create gameplay, visual or art acceptance.",
        "inputs": {
            "index": {"path": relative(root, index_path), "sha256": sha256(index_path)},
            "mapping": {"path": relative(root, mapping_path), "sha256": sha256(mapping_path)},
            "resources": {"path": relative(root, resources_path), "sha256": sha256(resources_path)},
            "experimentsRoot": relative(root, experiments),
        },
        "summary": summary,
        "directlyIndexedArtifacts": direct_indexed,
        "unindexedKnownExactAssignments": unindexed_known,
        "unindexedNovelExactAssignments": unindexed_novel,
        "directlyIndexedPlayerArtifacts": directly_indexed_players,
        "unindexedPlayerArtifacts": unindexed_players,
        "separatelyIndexedNonRuntimeArtifacts": separately_indexed_non_runtime,
        "unresolvedArtifacts": unresolved_artifacts,
        "skippedNonObjectJson": skipped_nonobjects,
        "invalidJson": invalid_json,
        "indexEntriesWithMissingArtifact": missing_index_artifacts,
        "indexEvidenceHashMismatches": hash_mismatches,
        "indexEntriesWithoutExactNativeMapping": unresolved_index_entries,
        "playerIndexEntriesWithMissingArtifact": missing_player_index_artifacts,
        "playerIndexEvidenceHashMismatches": player_hash_mismatches,
        "playerIndexEntriesWithoutDeclaredArchiveIdentity": unresolved_player_index_entries,
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd(), help="Repository root, default: current directory")
    parser.add_argument("--index", type=Path, default=Path("docs/model-runtime-validation.json"))
    parser.add_argument("--mapping", type=Path, default=Path("scratch/enemy-rig-mapping-reproducible.json"))
    parser.add_argument("--resources", type=Path, default=Path("scratch/resource-enemy-base-preflight.json"))
    parser.add_argument("--experiments-root", type=Path, default=Path("art-experiments"))
    parser.add_argument("--output", type=Path, help="Write the complete JSON report to a new path")
    parser.add_argument("--summary", action="store_true", help="Print only the stable summary and the novel artifact paths")
    parser.add_argument("--fail-on-novel", action="store_true", help="Exit nonzero if a novel enemy assignment or declared player archive lacks indexed evidence")
    parser.add_argument("--fail-on-unresolved", action="store_true", help="Also exit nonzero for artifacts whose native identity cannot be resolved")
    args = parser.parse_args()
    root = args.root.resolve()
    index_path = args.index if args.index.is_absolute() else root / args.index
    mapping_path = args.mapping if args.mapping.is_absolute() else root / args.mapping
    resources_path = args.resources if args.resources.is_absolute() else root / args.resources
    experiments = args.experiments_root if args.experiments_root.is_absolute() else root / args.experiments_root
    for path in (index_path, mapping_path, resources_path):
        if not path.is_file():
            parser.error(f"missing input: {path}")
    if not experiments.is_dir():
        parser.error(f"missing experiments root: {experiments}")
    report = audit(root, index_path, mapping_path, resources_path, experiments)
    if args.output:
        output = args.output if args.output.is_absolute() else root / args.output
        if output.exists():
            parser.error(f"refusing to overwrite existing output: {output}")
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(json.dumps(report, indent=2) + "\n")
    if args.summary:
        print(json.dumps({
            "summary": report["summary"],
            "unindexedNovelExactAssignmentPaths": [item["path"] for item in report["unindexedNovelExactAssignments"]],
            "unindexedPlayerArtifactPaths": [item["path"] for item in report["unindexedPlayerArtifacts"]],
            "separatelyIndexedNonRuntimeArtifactPaths": [
                item["path"] for item in report["separatelyIndexedNonRuntimeArtifacts"]],
            "unresolvedArtifactPaths": [item["path"] for item in report["unresolvedArtifacts"]],
        }, indent=2))
    elif not args.output:
        print(json.dumps(report, indent=2))
    if args.fail_on_novel and (report["summary"]["unindexedNovelExactAssignments"]
                                or report["summary"]["unindexedPlayerArtifacts"]):
        return 1
    if args.fail_on_unresolved and report["summary"]["unresolvedArtifacts"]:
        return 1
    return 0


if __name__ == "__main__":
    sys.exit(main())
