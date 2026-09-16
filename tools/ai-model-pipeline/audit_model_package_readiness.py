#!/usr/bin/env python3
"""Build a conservative readiness ledger for authored FTK model packages.

This audit joins every ``art-experiments/*/runtime-profile*.json`` document to
the current direct-enemy, resource-prefab, player-skinset, and rigid-child
preflight checks.  It then reports whether the runtime evidence index contains
an exact skinned/resource source assignment for each validated profile.

It does not promote an offline preflight, an archive's existence, a raw package
status, or an exact-source index match to an art, motion, gameplay, lifetime,
or all-family PASS.  Historical profile revisions remain separate rows so a
newer package does not silently rewrite earlier evidence.
"""
from __future__ import annotations

import argparse
from collections import defaultdict
import hashlib
import json
from pathlib import Path
from typing import Any

import jsonschema

import audit_topology_coverage as coverage
import validate_custom_model_profile_route as direct_route
import validate_player_model_profile_route as player_route
import validate_resource_model_profile_route as resource_route


Json = dict[str, Any]


def read_object(path: Path) -> Json:
    value = json.loads(path.read_text())
    if not isinstance(value, dict):
        raise ValueError(f"expected JSON object: {path}")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def resolve(root: Path, value: Path) -> Path:
    return value.resolve() if value.is_absolute() else (root / value).resolve()


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path.resolve())


def evidence_ref(root: Path, path: Path) -> Json:
    if not path.is_file():
        raise FileNotFoundError(path)
    return {"path": relative(root, path), "sha256": sha256(path)}


def first_text(*values: Any) -> str | None:
    return next((value for value in values if isinstance(value, str) and value), None)


def profile_kind(document: Json) -> str:
    profiles = document.get("profiles")
    if not isinstance(profiles, list) or not profiles:
        raise ValueError("profile document has no profiles")
    kinds: set[str] = set()
    for profile in profiles:
        if not isinstance(profile, dict):
            raise ValueError("profile document contains a non-object profile")
        if "skinset" in profile:
            kinds.add("player")
        elif profile.get("resourcePrefab") is not None:
            kinds.add("resource")
        else:
            kinds.add("direct")
    if len(kinds) != 1:
        raise ValueError(f"profile document mixes route kinds: {sorted(kinds)}")
    return next(iter(kinds))


def profile_assets(report_profile: Json) -> list[Json]:
    values = report_profile.get("declaredAssets")
    return values if isinstance(values, list) else []


def input_record(root: Path, path: Path) -> Json:
    return evidence_ref(root, path) if path.is_file() else {"path": relative(root, path), "missing": True}


def runtime_enemy_matches(root: Path, index: Json, mapping: Json, resources: Json) -> tuple[dict[tuple[Any, ...], list[Json]], list[Json]]:
    """Index evidence only by an explicit exact skinned/resource source target."""
    records, unresolved = coverage.indexed_enemy_evidence(root, index)
    targets = [*coverage.direct_enemy_targets(mapping), *coverage.resource_targets(resources)]
    result: dict[tuple[Any, ...], list[Json]] = defaultdict(list)
    for record in records:
        for matched in coverage.evidence_targets(record, targets):
            target = matched["target"]
            key = (
                target.get("nativeEnemy"),
                target.get("resourcePrefab") or "",
                target.get("sourceRendererId"),
                target.get("rendererPath"),
            )
            result[key].append({
                "indexPointer": matched.get("indexPointer"),
                "name": matched.get("name"),
                "iteration": matched.get("iteration"),
                "status": matched.get("status"),
                "artifact": matched.get("artifact"),
                "kind": matched.get("kind"),
            })
    for key in result:
        result[key].sort(key=lambda item: (str(item.get("indexPointer")), str(item.get("iteration"))))
    return result, unresolved


def runtime_player_matches(root: Path, index: Json, ownership: Json) -> dict[tuple[str, str], list[Json]]:
    result: dict[tuple[str, str], list[Json]] = defaultdict(list)
    for record in coverage.indexed_player_evidence(root, index, ownership):
        profile = record.get("profile")
        skinset = record.get("skinset")
        if not isinstance(profile, str) or not isinstance(skinset, str):
            continue
        result[(profile, skinset)].append({
            "indexPointer": record.get("indexPointer"),
            "status": record.get("status"),
            "artifact": record.get("artifact"),
            "kind": "original",
        })
    for key in result:
        result[key].sort(key=lambda item: str(item.get("indexPointer")))
    return result


def runtime_index_artifact_references(index: Json) -> dict[str, list[Json]]:
    """List every explicit original-index reference independently of route matching.

    An older index entry can point at an archive whose structured profile identity
    is too sparse to join it to a current resource route. It can also pin a
    historical follow-up under a named nested field. Either is a real index
    reference, so distinguish it from a package-local file that the index never
    names at all.
    """
    result: dict[str, list[Json]] = defaultdict(list)
    for collection in ("enemy_models", "original_player_models"):
        entries = index.get(collection, [])
        if not isinstance(entries, list):
            continue
        for position, entry in enumerate(entries):
            if not isinstance(entry, dict):
                continue
            for reference in coverage.indexed_path_references(entry):
                path = reference["path"]
                artifact = {key: reference[key] for key in ("path", "sha256") if key in reference}
                suffix = reference["jsonPointer"]
                result[path].append({
                    "indexPointer": f"/{collection}/{position}",
                    "evidencePointer": f"/{collection}/{position}" if suffix == "/"
                    else f"/{collection}/{position}{suffix}",
                    "status": first_text(entry.get("status"), entry.get("art_status")),
                    "artifact": artifact,
                })
    for path in result:
        result[path].sort(key=lambda item: (str(item["indexPointer"]), str(item["evidencePointer"])))
    return result


def is_archive_plan(value: Any) -> bool:
    schema = value.get("schema") if isinstance(value, dict) else None
    return isinstance(schema, str) and schema.startswith("ftkmf.model-validation-archive-plan.")


def archive_refs(root: Path, package: Path) -> list[Json]:
    """List package-local structured validation files without interpreting them."""
    found: list[Path] = []
    for path in package.glob("live-validation*.json"):
        try:
            content = read_object(path)
        except (OSError, ValueError, json.JSONDecodeError):
            found.append(path)
            continue
        if not is_archive_plan(content):
            found.append(path)
    for child in package.iterdir():
        if child.is_dir() and child.name.startswith("live"):
            validation = child / "validation.json"
            if validation.is_file():
                found.append(validation)
    unique = sorted({path.resolve() for path in found})
    return [evidence_ref(root, path) for path in unique]


def manifest_summary(root: Path, package: Path) -> Json | None:
    path = package / "manifest.json"
    if not path.is_file():
        return None
    try:
        manifest = read_object(path)
    except (OSError, json.JSONDecodeError) as error:
        return {"reference": evidence_ref(root, path), "readError": f"{type(error).__name__}: {error}"}
    return {
        "reference": evidence_ref(root, path),
        "name": first_text(manifest.get("name")),
        "rawStatus": first_text(manifest.get("status"), manifest.get("art_status")),
        "nativeSurfaceCopied": manifest.get("nativeSurfaceCopied", manifest.get("native_surface_copied")),
    }


def direct_entries(report: Json, matches: dict[tuple[Any, ...], list[Json]]) -> list[Json]:
    entries: list[Json] = []
    for profile in report["profiles"]:
        base = profile["baseEnemy"]
        renderers = profile.get("renderers", [])
        assignments: list[Json] = []
        index_records: list[Json] = []
        for renderer in renderers:
            kind = renderer.get("rendererKind", "SkinnedMeshRenderer")
            assignment = {
                "rendererPath": renderer.get("rendererPath"),
                "sourceRendererId": renderer.get("sourceRendererId"),
                "rendererKind": kind,
            }
            if kind == "MeshRenderer":
                assignment["strictStaticPreflight"] = {
                    "meshFilterCount": renderer.get("meshFilterCount"),
                    "nativeMaterialSlotCount": renderer.get("nativeMaterialSlotCount"),
                }
                assignment["indexedExactSourceEvidence"] = []
                assignment["indexedExactOriginalEvidence"] = []
                assignment["indexScope"] = "Rigid child identity is preflighted separately; this audit does not infer static live evidence from a skinned index entry."
            else:
                key = (base, "", renderer.get("sourceRendererId"), renderer.get("rendererPath"))
                assignment["indexedExactSourceEvidence"] = matches.get(key, [])
                assignment["indexedExactOriginalEvidence"] = [
                    record for record in assignment["indexedExactSourceEvidence"]
                    if record.get("kind") == "original"
                ]
                index_records.extend(assignment["indexedExactSourceEvidence"])
            assignments.append(assignment)
        entries.append({
            "key": profile.get("key"),
            "baseEnemy": base,
            "resourcePrefab": None,
            "routeKind": "direct_enemy",
            "assets": profile_assets(profile),
            "assignments": assignments,
            "indexedExactSourceEvidence": distinct_records(index_records),
            "indexedExactOriginalEvidence": distinct_records([
                record for record in index_records if record.get("kind") == "original"
            ]),
        })
    return entries


def resource_entries(report: Json, matches: dict[tuple[Any, ...], list[Json]]) -> list[Json]:
    entries: list[Json] = []
    for profile in report["profiles"]:
        renderer = profile["renderer"]
        key = (profile["baseEnemy"], profile["resourcePrefab"], renderer.get("sourceRendererId"),
               renderer.get("rendererPath"))
        entries.append({
            "key": profile.get("key"),
            "baseEnemy": profile.get("baseEnemy"),
            "resourcePrefab": profile.get("resourcePrefab"),
            "routeKind": "resource_prefab_override",
            "assets": profile_assets(profile),
            "assignments": [{
                "rendererPath": renderer.get("rendererPath"),
                "sourceRendererId": renderer.get("sourceRendererId"),
                "rendererKind": "SkinnedMeshRenderer",
                "indexedExactSourceEvidence": matches.get(key, []),
                "indexedExactOriginalEvidence": [record for record in matches.get(key, [])
                                                 if record.get("kind") == "original"],
            }],
            "indexedExactSourceEvidence": matches.get(key, []),
            "indexedExactOriginalEvidence": [record for record in matches.get(key, [])
                                             if record.get("kind") == "original"],
        })
    return entries


def player_entries(report: Json, matches: dict[tuple[str, str], list[Json]]) -> list[Json]:
    entries: list[Json] = []
    for profile in report["profiles"]:
        key = profile.get("key")
        skinset = profile.get("skinset")
        entries.append({
            "key": key,
            "baseClass": profile.get("baseClass"),
            "skinset": skinset,
            "routeKind": "player_skinset_avatar",
            "assets": profile_assets(profile),
            "assignments": [{
                "rendererPath": renderer.get("rendererPath"),
                "sourceRendererId": renderer.get("sourceRendererId"),
                "rendererKind": "SkinnedMeshRenderer",
            } for renderer in profile.get("renderers", [])],
            "apparel": profile.get("apparel", []),
            "indexedExactSourceEvidence": matches.get((key, skinset), []),
            "indexedExactOriginalEvidence": matches.get((key, skinset), []),
        })
    return entries


def distinct_records(records: list[Json]) -> list[Json]:
    seen: set[str] = set()
    result: list[Json] = []
    for record in records:
        key = json.dumps(record, sort_keys=True)
        if key not in seen:
            seen.add(key)
            result.append(record)
    return result


def validate_document(kind: str, document: Json, package: Path, mapping: Json, resources: Json,
                      classification: Json, static_inventory: Json | None,
                      schemas: dict[str, Json]) -> Json:
    jsonschema.validate(document, schemas[kind])
    if kind == "direct":
        return direct_route.validate_document(mapping, document, package, static_inventory)
    if kind == "resource":
        return resource_route.validate_document(resources, document, package)
    if kind == "player":
        return player_route.validate_document(classification, document, package)
    raise AssertionError(kind)


def fresh_trial_candidates(packages: list[Json]) -> list[Json]:
    """List exact revisions that passed preflight but lack original live evidence.

    This is a deterministic execution queue, not a coverage or acceptance
    result. It keeps each profile revision and every declared renderer assignment
    intact so an operator can stage and exercise precisely that source route.
    """
    candidates: list[Json] = []
    for package in packages:
        if package.get("preflight", {}).get("status") != "pass":
            continue
        document = package.get("profileDocument")
        if not isinstance(document, dict):
            continue
        for entry in package.get("profiles", []):
            if not isinstance(entry, dict) or entry.get("indexedExactOriginalEvidence"):
                continue
            assignments = [
                {
                    "rendererPath": assignment.get("rendererPath"),
                    "sourceRendererId": assignment.get("sourceRendererId"),
                    "rendererKind": assignment.get("rendererKind"),
                }
                for assignment in entry.get("assignments", [])
                if isinstance(assignment, dict)
            ]
            candidates.append({
                "status": "STATIC_PREFLIGHT_PASS_NO_INDEXED_EXACT_ORIGINAL_EVIDENCE",
                "package": package.get("package"),
                "profileDocument": document,
                "routeKind": entry.get("routeKind"),
                "key": entry.get("key"),
                "baseEnemy": entry.get("baseEnemy"),
                "resourcePrefab": entry.get("resourcePrefab"),
                "baseClass": entry.get("baseClass"),
                "skinset": entry.get("skinset"),
                "assignments": assignments,
                "nextAction": "Stage or verify this exact profile in a fresh isolated game process, then archive its own binding, motion, fixture/death, and native-progression observations without borrowing a sibling route.",
            })
    candidates.sort(key=lambda item: (
        str(item.get("profileDocument", {}).get("path")),
        str(item.get("key")),
    ))
    return candidates


def build_report(root: Path, art_root: Path, mapping: Json, resources: Json, classification: Json,
                 static_inventory: Json | None, runtime_index: Json, ownership: Json,
                 inputs: list[Path]) -> Json:
    if not art_root.is_dir():
        raise FileNotFoundError(art_root)
    # Validate the current source boundaries once before accepting package-level
    # individual results.  Resource validation pins its own preflight rows; the
    # source hashes captured in the input list keep that boundary visible.
    direct_route.validate_mapping_source(mapping)
    resource_route.validated_rows(resources)
    player_route.validate_classification_sources(classification, root)
    if static_inventory is not None:
        direct_route.validate_static_inventory_source(mapping, static_inventory)

    enemy_matches, unresolved_index = runtime_enemy_matches(root, runtime_index, mapping, resources)
    player_matches = runtime_player_matches(root, runtime_index, ownership)
    indexed_artifacts = runtime_index_artifact_references(runtime_index)
    tool_root = root / "tools/ai-model-pipeline"
    schemas = {
        "direct": read_object(tool_root / "runtime-test-content/profiles.schema.json"),
        "resource": read_object(tool_root / "runtime-test-content/profiles.schema.json"),
        "player": read_object(tool_root / "runtime-test-content/player-profiles.schema.json"),
    }
    packages: list[Json] = []
    documents = sorted(art_root.glob("*/runtime-profile*.json"))
    for path in documents:
        package = path.parent
        item: Json = {
            "package": relative(root, package),
            "profileDocument": evidence_ref(root, path),
            "manifest": manifest_summary(root, package),
            "localValidationArtifacts": archive_refs(root, package),
        }
        try:
            document = read_object(path)
            kind = profile_kind(document)
            preflight = validate_document(kind, document, package, mapping, resources, classification,
                                          static_inventory, schemas)
            if kind == "direct":
                entries = direct_entries(preflight, enemy_matches)
            elif kind == "resource":
                entries = resource_entries(preflight, enemy_matches)
            else:
                entries = player_entries(preflight, player_matches)
            item.update({
                "routeKind": kind,
                "preflight": {"status": "pass", "result": preflight.get("status"), "scope": preflight.get("scope")},
                "profiles": entries,
            })
        except (OSError, ValueError, KeyError, TypeError, json.JSONDecodeError, jsonschema.ValidationError) as error:
            item.update({
                "routeKind": None,
                "preflight": {"status": "failed", "error": f"{type(error).__name__}: {error}"},
                "profiles": [],
            })
        item["directlyIndexedLocalValidationArtifacts"] = [
            {**reference, "indexReferences": indexed_artifacts[reference["path"]]}
            for reference in item["localValidationArtifacts"]
            if reference["path"] in indexed_artifacts
        ]
        item["unindexedLocalValidationArtifacts"] = [
            reference for reference in item["localValidationArtifacts"]
            if reference["path"] not in indexed_artifacts
        ]
        packages.append(item)
    profile_entries = [entry for package in packages for entry in package["profiles"]]
    static_assignments = [assignment for entry in profile_entries for assignment in entry["assignments"]
                          if assignment.get("rendererKind") == "MeshRenderer"]
    indexed_profiles = sum(bool(entry["indexedExactSourceEvidence"]) for entry in profile_entries)
    indexed_original_profiles = sum(bool(entry["indexedExactOriginalEvidence"]) for entry in profile_entries)
    local_artifacts = [reference for package in packages for reference in package["localValidationArtifacts"]]
    unindexed_local = [reference for package in packages for reference in package["unindexedLocalValidationArtifacts"]]
    fresh_trials = fresh_trial_candidates(packages)
    summary = {
        "packageDirectoriesWithProfileDocuments": len({item["package"] for item in packages}),
        "profileDocuments": len(packages),
        "preflightPassedDocuments": sum(item["preflight"]["status"] == "pass" for item in packages),
        "preflightFailedDocuments": sum(item["preflight"]["status"] == "failed" for item in packages),
        "validatedProfileEntries": len(profile_entries),
        "directEnemyProfileEntries": sum(entry["routeKind"] == "direct_enemy" for entry in profile_entries),
        "resourcePrefabProfileEntries": sum(entry["routeKind"] == "resource_prefab_override" for entry in profile_entries),
        "playerSkinsetProfileEntries": sum(entry["routeKind"] == "player_skinset_avatar" for entry in profile_entries),
        "rigidStaticAssignments": len(static_assignments),
        "profileEntriesWithIndexedExactSourceEvidence": indexed_profiles,
        "profileEntriesWithoutIndexedExactSourceEvidence": len(profile_entries) - indexed_profiles,
        "profileEntriesWithIndexedExactOriginalEvidence": indexed_original_profiles,
        "profileEntriesWithoutIndexedExactOriginalEvidence": len(profile_entries) - indexed_original_profiles,
        "freshIsolatedTrialProfileEntries": len(fresh_trials),
        "localStructuredValidationArtifacts": len(local_artifacts),
        "localValidationArtifactsDirectlyIndexed": sum(
            len(package["directlyIndexedLocalValidationArtifacts"]) for package in packages),
        "localValidationArtifactsNotDirectlyIndexed": len(unindexed_local),
        "unresolvedRuntimeIndexRecords": len(unresolved_index),
    }
    return {
        "schemaVersion": 2,
        "scope": "Package readiness ledger. Static preflight, raw package status, a local validation file, and an exact source index match are separate facts. None is an art, motion, gameplay, lifetime, culling, portrait, or all-candidate PASS.",
        "inputs": [input_record(root, path) for path in inputs],
        "summary": summary,
        "packages": packages,
        "freshIsolatedTrialCandidates": fresh_trials,
        "unresolvedRuntimeIndexRecords": unresolved_index,
        "limits": [
            "A profile revision is not linked to a historical archive merely because its native route matches; compare pinned catalog/profile hashes before reuse.",
            "Rigid MeshRenderer assignments are only checked for a strict serialized route. Their live binding and appearance require their own recorded evidence.",
            "A local validation artifact not directly indexed may be historical, supplemental, or incomplete; this audit does not infer why it is absent from the index.",
            "The current static inventory covers direct-enemy MeshRenderer children serialized in the selected game asset, not every possible external or bundled renderer.",
        ],
    }


def markdown(report: Json) -> str:
    lines = [
        "# Model package readiness ledger",
        "",
        json.dumps(report["summary"], sort_keys=True),
        "",
        "**Readiness facts, not acceptance results.** Each profile document is independently statically preflighted against the current local game metadata. An indexed exact original-source record still needs the gate ledger and its own human review; diagnostic records and local validation files are listed without being promoted to original-model evidence.",
        "",
        "| Package | Profile document | Route | Static preflight | Profiles / indexed exact original evidence | Local validation files | Raw package status | Next action |",
        "|---|---|---|---|---:|---:|---|---|",
    ]
    for item in report["packages"]:
        manifest = item.get("manifest") or {}
        raw_status = manifest.get("rawStatus") if isinstance(manifest, dict) else None
        entries = item["profiles"]
        indexed = sum(bool(entry.get("indexedExactOriginalEvidence")) for entry in entries)
        preflight = item["preflight"]
        if preflight["status"] == "failed":
            action = "Repair the static preflight error before staging this document."
        elif indexed:
            action = "Exact original-source index evidence is present; compare pinned revision hashes and inspect the gate ledger."
        else:
            action = "Static preflight passed; a fresh isolated trial and indexed archive remain pending."
        lines.append("|" + "|".join([
            item["package"],
            item["profileDocument"]["path"],
            item.get("routeKind") or "unresolved",
            preflight["status"],
            f"{len(entries)} / {indexed}",
            str(len(item["localValidationArtifacts"])),
            raw_status or "none",
            action,
        ]) + "|")
    failed = [item for item in report["packages"] if item["preflight"]["status"] == "failed"]
    if failed:
        lines += ["", "## Preflight gaps", ""]
        for item in failed:
            lines.append(f"- `{item['profileDocument']['path']}`: {item['preflight']['error']}")
    candidates = report.get("freshIsolatedTrialCandidates", [])
    if candidates:
        lines += ["", "## Fresh isolated trial queue", "",
                  "Each row passed only its current static preflight and has no indexed exact original-model record. Run each profile in its own fresh isolated process and keep every listed source assignment scoped to that profile.", "",
                  "| Profile | Native route | Renderer assignments | Profile document |", "|---|---|---|---|"]
        for candidate in candidates:
            route = candidate.get("baseEnemy") or candidate.get("baseClass") or "none"
            if candidate.get("resourcePrefab"):
                route += f" → {candidate['resourcePrefab']}"
            elif candidate.get("skinset"):
                route += f" → {candidate['skinset']}"
            assignments = "; ".join(
                f"`{assignment.get('rendererPath')}` / {assignment.get('sourceRendererId')}"
                for assignment in candidate.get("assignments", [])
            ) or "none"
            lines.append("|" + "|".join([
                str(candidate.get("key")), str(route), assignments,
                str(candidate.get("profileDocument", {}).get("path")),
            ]) + "|")
    static = [
        (item, entry, assignment)
        for item in report["packages"] for entry in item["profiles"] for assignment in entry.get("assignments", [])
        if assignment.get("rendererKind") == "MeshRenderer"
    ]
    if static:
        collapsed: dict[tuple[Any, ...], Json] = {}
        for item, entry, assignment in static:
            prerequisites = assignment.get("strictStaticPreflight", {})
            key = (
                entry.get("key"), entry.get("baseEnemy"), entry.get("resourcePrefab"),
                assignment.get("rendererPath"), assignment.get("sourceRendererId"),
                prerequisites.get("meshFilterCount"), prerequisites.get("nativeMaterialSlotCount"),
            )
            value = collapsed.setdefault(key, {
                "entry": entry,
                "assignment": assignment,
                "documents": [],
            })
            value["documents"].append(item["profileDocument"]["path"])
        lines += ["", "## Rigid MeshRenderer assignments", "",
                  "These rows passed only the strict one-filter/one-native-material serialized-route prerequisite. Their normal runtime motion capture must use the linked skinned assignment, and their own live inventory/appearance evidence remains separate.", "",
                  "| Profile | Native route | Rigid path / source | Serialized prerequisites | Profile documents |", "|---|---|---|---|---:|"]
        for value in collapsed.values():
            entry = value["entry"]
            assignment = value["assignment"]
            route = entry.get("baseEnemy") or entry.get("baseClass") or "none"
            if entry.get("resourcePrefab"):
                route += f" → {entry['resourcePrefab']}"
            prereq = assignment.get("strictStaticPreflight", {})
            lines.append("|" + "|".join([
                str(entry.get("key")), str(route),
                f"`{assignment.get('rendererPath')}` / {assignment.get('sourceRendererId')}",
                f"MeshFilters={prereq.get('meshFilterCount')}; native material slots={prereq.get('nativeMaterialSlotCount')}",
                str(len(value["documents"])),
            ]) + "|")
    lines += [
        "",
        "Use this ledger to choose a package and profile revision. It does not make a sibling source pair, controller, resource prefab, player skinset, static child, or historical revision covered.",
        "",
    ]
    return "\n".join(lines)


def write_new(path: Path, content: str, overwrite: bool) -> None:
    if path.exists() and not overwrite:
        raise FileExistsError(f"refusing to overwrite {path}; pass --overwrite after reviewing the generated result")
    path.parent.mkdir(parents=True, exist_ok=True)
    temporary = path.with_name(path.name + ".tmp")
    temporary.write_text(content)
    temporary.replace(path)


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--art-root", type=Path, default=Path("art-experiments"))
    parser.add_argument("--mapping", type=Path, default=Path("scratch/enemy-rig-mapping-reproducible.json"))
    parser.add_argument("--resources", type=Path, default=Path("scratch/resource-enemy-base-preflight.json"))
    parser.add_argument("--classification", type=Path, default=Path("scratch/rig-candidate-classification.json"))
    parser.add_argument("--static-inventory", type=Path, default=Path("scratch/static-renderer-inventory.json"))
    parser.add_argument("--runtime-index", type=Path, default=Path("docs/model-runtime-validation.json"))
    parser.add_argument("--ownership", type=Path,
                        default=Path("docs/evidence/nonenemy-topology-ownership-v1/findings.json"))
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    parser.add_argument("--overwrite", action="store_true")
    parser.add_argument("--fail-on-preflight", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    paths = {name: resolve(root, value) for name, value in {
        "art": args.art_root, "mapping": args.mapping, "resources": args.resources,
        "classification": args.classification, "static": args.static_inventory,
        "index": args.runtime_index, "ownership": args.ownership,
    }.items()}
    mapping = read_object(paths["mapping"])
    resources = read_object(paths["resources"])
    classification = read_object(paths["classification"])
    static_inventory = read_object(paths["static"]) if paths["static"].is_file() else None
    index = read_object(paths["index"])
    ownership = read_object(paths["ownership"])
    report = build_report(root, paths["art"], mapping, resources, classification, static_inventory, index, ownership,
                          [paths[name] for name in ("mapping", "resources", "classification", "static", "index", "ownership")])
    output_json = resolve(root, args.output_json)
    output_markdown = resolve(root, args.output_markdown)
    write_new(output_json, json.dumps(report, indent=2) + "\n", args.overwrite)
    write_new(output_markdown, markdown(report), args.overwrite)
    print(json.dumps(report["summary"], indent=2))
    if args.fail_on_preflight and report["summary"]["preflightFailedDocuments"]:
        raise SystemExit(1)


if __name__ == "__main__":
    main()
