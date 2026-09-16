#!/usr/bin/env python3
"""Read-only staging readiness for the validation execution queue.

The execution queue proves only that an exact profile document passed static
route preflight. This companion audit compares each queued document and its
ordinary declared assets with one stopped, isolated test catalog. It separates
profiles that are already present byte-for-byte from appendable profiles and
from deliberate catalog or asset migrations. It never stages, deploys, launches
FTK, or promotes any result to live evidence.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import subprocess
from collections import defaultdict
from pathlib import Path
from typing import Any

import jsonschema


Json = dict[str, Any]
MODELS_RELATIVE = Path("BepInEx/plugins/FTKModFramework_content/models")
CATALOGS = {
    "enemy": ("model-test-profiles.json", "profiles.schema.json"),
    "player": ("model-test-player-profiles.json", "player-profiles.schema.json"),
}


def as_dict(value: Any) -> Json:
    return value if isinstance(value, dict) else {}


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_object(path: Path) -> Json:
    value = json.loads(path.read_text())
    if not isinstance(value, dict):
        raise ValueError(f"expected JSON object: {path}")
    return value


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path.resolve())


def input_reference(root: Path, path: Path) -> Json:
    return {"path": relative(root, path), "sha256": sha256(path)}


def write_new(path: Path, content: str, overwrite: bool) -> None:
    if path.exists() and not overwrite:
        raise FileExistsError(f"refusing to overwrite existing output: {path}; pass --overwrite")
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(content)


def route_catalog_kind(route_kind: str) -> str:
    if route_kind in ("direct_enemy", "resource_prefab_override"):
        return "enemy"
    if route_kind == "player_skinset_avatar":
        return "player"
    raise ValueError(f"unknown queued profile route kind: {route_kind!r}")


def document_route_kind(profile: Json) -> str:
    if profile.get("skinset") is not None:
        return "player_skinset_avatar"
    if profile.get("resourcePrefab") is not None:
        return "resource_prefab_override"
    return "direct_enemy"


def declared_assets(profile: Json) -> list[str]:
    names: set[str] = set()
    renderers = [*as_list(profile.get("renderers")), *as_list(profile.get("apparel"))]
    for raw_renderer in renderers:
        renderer = as_dict(raw_renderer)
        for field in ("glbFile", "textureFile"):
            name = renderer.get(field)
            if name is None:
                continue
            if not isinstance(name, str) or not name or Path(name).name != name:
                raise ValueError(f"unsafe declared {field}: {name!r}")
            names.add(name)
        for raw_slot in as_list(renderer.get("materialSlots")):
            slot = as_dict(raw_slot)
            name = slot.get("textureFile")
            if name is None:
                continue
            if not isinstance(name, str) or not name or Path(name).name != name:
                raise ValueError(f"unsafe declared material slot texture: {name!r}")
            names.add(name)
    if not names:
        raise ValueError(f"profile {profile.get('key')!r} declares no stageable assets")
    return sorted(names)


def profile_from_candidate(root: Path, candidate: Json) -> tuple[Json, Json]:
    document_ref = as_dict(candidate.get("profileDocument"))
    document_path = document_ref.get("path")
    document_hash = document_ref.get("sha256")
    key = candidate.get("key")
    if not isinstance(document_path, str) or not document_path:
        raise ValueError("queued candidate lacks a profile document path")
    if not isinstance(document_hash, str) or len(document_hash) != 64:
        raise ValueError(f"queued candidate lacks a profile document hash: {document_path!r}")
    if not isinstance(key, str) or not key:
        raise ValueError("queued candidate lacks a profile key")
    path = (root / document_path).resolve()
    if not path.is_file() or path.is_symlink() or not path.is_relative_to(root.resolve()):
        raise ValueError(f"unsafe or missing profile document: {document_path!r}")
    actual_hash = sha256(path)
    if actual_hash != document_hash:
        raise ValueError(f"queued profile document hash changed: {document_path!r}")
    document = read_object(path)
    profiles = [as_dict(raw) for raw in as_list(document.get("profiles"))]
    matches = [profile for profile in profiles if profile.get("key") == key]
    if len(matches) != 1:
        raise ValueError(f"queued key {key!r} is not unique in {document_path!r}")
    profile = matches[0]
    route_kind = candidate.get("routeKind")
    if profile.get("key") != key or document_route_kind(profile) != route_kind:
        raise ValueError(
            f"queued profile route identity differs from its source document: {document_path!r} / {key!r}"
        )
    return profile, {"path": document_path, "sha256": actual_hash}


def queue_revisions(root: Path, queue: Json) -> list[Json]:
    revisions: dict[tuple[str, str], Json] = {}
    for raw_route in as_list(queue.get("routes")):
        route = as_dict(raw_route)
        group = route.get("topologyGroup")
        route_kind = route.get("routeKind")
        if not isinstance(group, str) or not isinstance(route_kind, str):
            raise ValueError("execution queue route lacks topology group or route kind")
        for raw_candidate in as_list(route.get("preflightedProfileCandidates")):
            candidate = as_dict(raw_candidate)
            profile, document_ref = profile_from_candidate(root, candidate)
            key = profile.get("key")
            profile_route_kind = candidate.get("routeKind")
            if not isinstance(profile_route_kind, str):
                raise ValueError(f"queued profile has no package route kind: {key!r}")
            identity = (document_ref["path"], str(key))
            reference = {
                "topologyGroup": group,
                "coverageRouteKind": route_kind,
                "selectedValidationTargets": as_list(route.get("selectedValidationTargets")),
            }
            if identity in revisions:
                existing = revisions[identity]
                if existing["profile"] != profile:
                    raise ValueError(f"profile document changed while being read: {identity[0]}")
                existing["routeReferences"].append(reference)
                continue
            revisions[identity] = {
                "profileDocument": document_ref,
                "profile": profile,
                "routeKind": profile_route_kind,
                "routeReferences": [reference],
            }
    result = list(revisions.values())
    result.sort(key=lambda item: (item["profileDocument"]["path"], item["profile"]["key"]))
    return result


def catalog_index(game_root: Path, catalog_kind: str, schema: Json) -> tuple[Json, dict[str, Json], Path]:
    catalog_name = CATALOGS[catalog_kind][0]
    path = game_root / catalog_name
    if not path.is_file() or path.is_symlink():
        raise FileNotFoundError(f"missing ordinary isolated catalog: {path}")
    document = read_object(path)
    jsonschema.validate(document, schema)
    result: dict[str, Json] = {}
    for raw_profile in as_list(document.get("profiles")):
        profile = as_dict(raw_profile)
        key = profile.get("key")
        if not isinstance(key, str) or not key or key in result:
            raise ValueError(f"invalid or duplicate key in {catalog_name}: {key!r}")
        result[key] = profile
    return document, result, path


def asset_state(models: Path, asset_dir: Path, name: str) -> Json:
    source = asset_dir / name
    if not source.is_file() or source.is_symlink():
        raise FileNotFoundError(f"missing ordinary declared source asset: {source}")
    source_hash = sha256(source)
    target = models / name
    if target.exists() and (not target.is_file() or target.is_symlink()):
        raise ValueError(f"isolated model target is not an ordinary file: {target}")
    if not target.exists():
        state = "new_asset_copy_required"
        target_hash = None
    else:
        target_hash = sha256(target)
        state = "identical_asset_already_present" if target_hash == source_hash else "asset_replacement_required"
    return {
        "name": name,
        "sourceSha256": source_hash,
        "targetSha256": target_hash,
        "state": state,
    }


def revision_report(root: Path, game_root: Path, catalogs: dict[str, dict[str, Json]], models: Path, revision: Json) -> Json:
    profile = as_dict(revision.get("profile"))
    key = profile.get("key")
    route_kind = revision.get("routeKind")
    if not isinstance(key, str) or not isinstance(route_kind, str):
        raise ValueError("invalid queued revision")
    catalog_kind = route_catalog_kind(route_kind)
    existing = catalogs[catalog_kind].get(key)
    if existing is None:
        catalog_state = "profile_append_required"
    elif existing == profile:
        catalog_state = "identical_profile_already_present"
    else:
        catalog_state = "explicit_profile_migration_required"
    document_path = root / as_dict(revision.get("profileDocument")).get("path", "")
    assets = [asset_state(models, document_path.parent, name) for name in declared_assets(profile)]
    asset_states = {item["state"] for item in assets}
    if catalog_state == "explicit_profile_migration_required" or "asset_replacement_required" in asset_states:
        stage_state = "explicit_isolated_migration_required"
    elif catalog_state == "identical_profile_already_present" and asset_states == {"identical_asset_already_present"}:
        stage_state = "ready_without_catalog_or_asset_stage"
    else:
        stage_state = "append_or_asset_stage_available"
    return {
        "profileDocument": revision["profileDocument"],
        "profile": profile,
        "routeKind": route_kind,
        "catalogKind": catalog_kind,
        "catalogState": catalog_state,
        "assets": assets,
        "stageState": stage_state,
        "routeReferences": revision["routeReferences"],
        "limits": (
            "Read-only catalog and ordinary asset comparison. It does not stage, deploy, launch the game, "
            "observe binding, or establish any live behavior or visual result."
        ),
    }


def conflicts(rows: list[Json]) -> Json:
    keys: dict[tuple[str, str], set[str]] = defaultdict(set)
    assets: dict[str, set[str]] = defaultdict(set)
    for row in rows:
        profile = as_dict(row.get("profile"))
        document = as_dict(row.get("profileDocument"))
        identity = f"{document.get('path')}::{profile.get('key')}"
        keys[(str(row.get("catalogKind")), str(profile.get("key")))].add(
            json.dumps(profile, sort_keys=True, separators=(",", ":"))
        )
        for asset in as_list(row.get("assets")):
            value = as_dict(asset)
            assets[str(value.get("name"))].add(str(value.get("sourceSha256")))
    profile_conflicts = [
        {"catalogKind": kind, "key": key, "distinctProfileVariants": len(values)}
        for (kind, key), values in keys.items() if len(values) > 1
    ]
    asset_conflicts = [
        {"asset": name, "distinctSourceHashes": len(values)}
        for name, values in assets.items() if len(values) > 1
    ]
    profile_conflicts.sort(key=lambda item: (item["catalogKind"], item["key"]))
    asset_conflicts.sort(key=lambda item: item["asset"])
    return {"profileKeyVariants": profile_conflicts, "assetNameVariants": asset_conflicts}


def selection_index(document: Json) -> dict[tuple[str, str], Json]:
    if document and document.get("schemaVersion") != 1:
        raise ValueError("unsupported profile-selection schema")
    result: dict[tuple[str, str], Json] = {}
    for raw in as_list(document.get("selections")):
        selection = as_dict(raw)
        group = selection.get("topologyGroup")
        kind = selection.get("routeKind")
        if not isinstance(group, str) or not group or not isinstance(kind, str) or not kind:
            raise ValueError("profile selection lacks route identity")
        identity = (group, kind)
        if identity in result:
            raise ValueError(f"duplicate profile selection: {group} / {kind}")
        document_ref = as_dict(selection.get("profileDocument"))
        if not isinstance(selection.get("key"), str) or not isinstance(document_ref.get("path"), str) or not isinstance(document_ref.get("sha256"), str):
            raise ValueError(f"profile selection lacks exact profile identity: {group} / {kind}")
        result[identity] = selection
    return result


def route_choices(queue: Json, rows: list[Json], selections: Json | None = None) -> list[Json]:
    """Group profile revisions back into their topology-route choices.

    The output intentionally reports every viable revision instead of selecting
    one. A stage-ready choice only means the current isolated catalog already
    contains that exact document and its declared asset bytes.
    """
    choices: dict[tuple[str, str], Json] = {}
    for raw_route in as_list(queue.get("routes")):
        route = as_dict(raw_route)
        group = route.get("topologyGroup")
        kind = route.get("routeKind")
        if not isinstance(group, str) or not isinstance(kind, str):
            raise ValueError("execution queue route lacks identity")
        identity = (group, kind)
        if identity in choices:
            raise ValueError(f"duplicate execution queue route: {group} / {kind}")
        choices[identity] = {
            "topologyGroup": group,
            "routeKind": kind,
            "executionQueueStatus": as_dict(route.get("executionStatus")).get("status"),
            "candidateRevisions": [],
        }
    for row in rows:
        profile = as_dict(row.get("profile"))
        candidate = {
            "profileDocument": as_dict(row.get("profileDocument")),
            "key": profile.get("key"),
            "catalogKind": row.get("catalogKind"),
            "catalogState": row.get("catalogState"),
            "stageState": row.get("stageState"),
        }
        for raw_reference in as_list(row.get("routeReferences")):
            reference = as_dict(raw_reference)
            group = reference.get("topologyGroup")
            kind = reference.get("coverageRouteKind")
            identity = (group, kind)
            if identity not in choices:
                raise ValueError(f"stage revision references unknown queue route: {identity!r}")
            choices[identity]["candidateRevisions"].append(candidate)
    indexed_selections = selection_index(selections or {})
    unknown_selections = set(indexed_selections) - set(choices)
    if unknown_selections:
        raise ValueError(f"profile selection references unknown queue route: {sorted(unknown_selections)!r}")
    result: list[Json] = []
    for choice in choices.values():
        candidates = as_list(choice.get("candidateRevisions"))
        states = {as_dict(candidate).get("stageState") for candidate in candidates}
        if "ready_without_catalog_or_asset_stage" in states:
            action = "stage_ready_revision_available"
        elif "append_or_asset_stage_available" in states:
            action = "append_or_asset_stage_available"
        elif "explicit_isolated_migration_required" in states:
            action = "choose_one_explicit_isolated_migration"
        elif choice.get("executionQueueStatus") == "adapter_or_retarget_design_required":
            action = "adapter_or_retarget_design_required"
        elif choice.get("executionQueueStatus") == "adapter_implementation_required":
            action = "adapter_implementation_required"
        elif choice.get("executionQueueStatus") == "adapter_validation_required":
            action = "adapter_validation_required"
        elif choice.get("executionQueueStatus") == "adapter_visual_archive_review_required":
            action = "adapter_visual_archive_review_required"
        else:
            action = "profile_authoring_or_queue_reconciliation_required"
        choice["candidateRevisions"] = sorted(
            candidates,
            key=lambda item: (
                str(as_dict(item.get("profileDocument")).get("path")),
                str(item.get("key")),
            ),
        )
        identity = (choice["topologyGroup"], choice["routeKind"])
        selection = indexed_selections.get(identity)
        if selection is not None:
            document = as_dict(selection.get("profileDocument"))
            matches = [
                candidate for candidate in choice["candidateRevisions"]
                if candidate.get("key") == selection.get("key")
                and as_dict(candidate.get("profileDocument")) == document
            ]
            if len(matches) != 1:
                raise ValueError(
                    f"profile selection does not match one exact queued revision: {identity!r}"
                )
            if matches[0].get("stageState") != "ready_without_catalog_or_asset_stage":
                raise ValueError(f"selected profile revision is not stage-ready: {identity!r}")
            choice["selectedRevision"] = matches[0]
            choice["selectionReason"] = selection.get("reason")
            if selection.get("motionRendererPath") is not None:
                if not isinstance(selection.get("motionRendererPath"), str) or not selection.get("motionRendererPath"):
                    raise ValueError(f"profile selection has invalid motion renderer: {identity!r}")
                choice["selectedMotionRendererPath"] = selection.get("motionRendererPath")
            if selection.get("workflow") is not None:
                workflow = selection.get("workflow")
                if workflow != "passive_enemy_arrival":
                    raise ValueError(f"profile selection has unsupported workflow: {identity!r}")
                level = selection.get("arrivalLevel")
                room = selection.get("arrivalRoom")
                if not isinstance(level, int) or isinstance(level, bool) or level < 0:
                    raise ValueError(f"passive arrival selection has invalid level: {identity!r}")
                if not isinstance(room, int) or isinstance(room, bool) or room < 0:
                    raise ValueError(f"passive arrival selection has invalid room: {identity!r}")
                choice["selectedWorkflow"] = workflow
                choice["arrivalLevel"] = level
                choice["arrivalRoom"] = room
        elif len(choice["candidateRevisions"]) == 1:
            choice["selectedRevision"] = choice["candidateRevisions"][0]
            choice["selectionReason"] = "The route has exactly one stage-ready candidate revision."
        choice["nextStagingAction"] = action
        result.append(choice)
    result.sort(key=lambda item: (item["topologyGroup"], item["routeKind"]))
    return result


def build_report(root: Path, queue: Json, game_root: Path, selections: Json | None = None) -> Json:
    revisions = queue_revisions(root, queue)
    schemas: dict[str, Json] = {}
    catalog_rows: dict[str, dict[str, Json]] = {}
    catalog_inputs: list[Json] = []
    for kind, (catalog_name, schema_name) in CATALOGS.items():
        schema_path = root / "tools/ai-model-pipeline/runtime-test-content" / schema_name
        if not schema_path.is_file():
            raise FileNotFoundError(schema_path)
        schema = read_object(schema_path)
        schemas[kind] = schema
        document, rows, catalog_path = catalog_index(game_root, kind, schema)
        catalog_rows[kind] = rows
        catalog_inputs.append({
            "kind": kind,
            "path": relative(root, catalog_path),
            "sha256": sha256(catalog_path),
            "profileCount": len(as_list(document.get("profiles"))),
        })
    models = game_root / MODELS_RELATIVE
    if not models.is_dir() or models.is_symlink():
        raise FileNotFoundError(f"missing real isolated model directory: {models}")
    rows = [revision_report(root, game_root, catalog_rows, models, revision) for revision in revisions]
    rows.sort(key=lambda item: (item["catalogKind"], item["profileDocument"]["path"], item["profile"]["key"]))
    conflict_report = conflicts(rows)
    choices = route_choices(queue, rows, selections)
    return {
        "schemaVersion": 1,
        "scope": (
            "Read-only staging readiness for profile revisions selected by the validation execution queue. "
            "It compares exact profile JSON and ordinary declared asset hashes with one isolated game copy. "
            "It is not staging, deployment, runtime, motion, gameplay, or art evidence."
        ),
        "queue": {
            "schemaVersion": queue.get("schemaVersion"),
        },
        "profileSelections": {
            "schemaVersion": as_dict(selections).get("schemaVersion") if selections else None,
            "selectedRoutes": len(selection_index(selections or {})),
        },
        "isolatedGameRoot": relative(root, game_root),
        "catalogInputs": catalog_inputs,
        "modelDirectory": relative(root, models),
        "summary": {
            "uniqueQueuedProfileRevisions": len(rows),
            "readyWithoutCatalogOrAssetStage": sum(row["stageState"] == "ready_without_catalog_or_asset_stage" for row in rows),
            "appendOrAssetStageAvailable": sum(row["stageState"] == "append_or_asset_stage_available" for row in rows),
            "explicitIsolatedMigrationRequired": sum(row["stageState"] == "explicit_isolated_migration_required" for row in rows),
            "profileKeyVariantConflicts": len(conflict_report["profileKeyVariants"]),
            "assetNameVariantConflicts": len(conflict_report["assetNameVariants"]),
            "routesWithStageReadyRevision": sum(
                choice["nextStagingAction"] == "stage_ready_revision_available" for choice in choices
            ),
            "stageReadyRoutesWithSelectedRevision": sum(
                choice["nextStagingAction"] == "stage_ready_revision_available"
                and bool(as_dict(choice.get("selectedRevision")))
                for choice in choices
            ),
            "routesWithAppendOrAssetStageAvailable": sum(
                choice["nextStagingAction"] == "append_or_asset_stage_available" for choice in choices
            ),
            "routesRequiringExplicitIsolatedMigration": sum(
                choice["nextStagingAction"] == "choose_one_explicit_isolated_migration" for choice in choices
            ),
            "routesRequiringAdapterOrRetargetDesign": sum(
                choice["nextStagingAction"] == "adapter_or_retarget_design_required" for choice in choices
            ),
            "routesRequiringAdapterImplementation": sum(
                choice["nextStagingAction"] == "adapter_implementation_required" for choice in choices
            ),
            "routesRequiringAdapterValidation": sum(
                choice["nextStagingAction"] == "adapter_validation_required" for choice in choices
            ),
            "routesRequiringAdapterVisualArchiveReview": sum(
                choice["nextStagingAction"] == "adapter_visual_archive_review_required"
                for choice in choices
            ),
        },
        "revisions": rows,
        "routeChoices": choices,
        "conflicts": conflict_report,
        "limits": [
            "A matching profile and asset row only means no catalog or asset copy is presently needed for this isolated game root.",
            "A profile or asset migration must remain an explicit isolated staging operation with its own pinned before-and-after receipt.",
            "Multiple historical documents for one profile key are listed as alternatives. This audit does not choose a revision or merge them.",
            "A stage-ready route may still require a fresh isolated launch, manual visual review, and a new archive before it can receive live credit.",
            "Never use this report to claim that an asset is bound, animated, accepted, or covered by a historical archive.",
        ],
    }


def markdown(report: Json) -> str:
    summary = as_dict(report.get("summary"))
    lines = [
        "# Model validation stage readiness",
        "",
        "This is a read-only comparison between the validation execution queue and one stopped isolated game catalog. It identifies catalog and asset copy work only. It does not stage, deploy, launch FTK, or prove a live result.",
        "",
        "| Unique queued profile revisions | Ready without stage | Append or asset stage available | Explicit isolated migration required | Routes with stage-ready revision | Stage-ready routes with selected revision | Routes requiring migration | Adapter design | Adapter implementation | Adapter validation | Adapter visual/archive review |",
        "|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|",
        "| {uniqueQueuedProfileRevisions} | {readyWithoutCatalogOrAssetStage} | {appendOrAssetStageAvailable} | {explicitIsolatedMigrationRequired} | {routesWithStageReadyRevision} | {stageReadyRoutesWithSelectedRevision} | {routesRequiringExplicitIsolatedMigration} | {routesRequiringAdapterOrRetargetDesign} | {routesRequiringAdapterImplementation} | {routesRequiringAdapterValidation} | {routesRequiringAdapterVisualArchiveReview} |".format(**summary),
        "",
        "## Revisions",
        "",
        "| Catalog | Profile document | Key | Catalog state | Asset states | Stage state | Route references |",
        "|---|---|---|---|---|---|---:|",
    ]
    for raw_row in as_list(report.get("revisions")):
        row = as_dict(raw_row)
        profile = as_dict(row.get("profile"))
        asset_states = ", ".join(
            "{name}: {state}".format(**as_dict(raw_asset))
            for raw_asset in as_list(row.get("assets"))
        ) or "none"
        lines.append(
            "| `{catalog}` | `{document}` | `{key}` | `{catalog_state}` | {assets} | `{stage}` | {references} |".format(
                catalog=row.get("catalogKind"),
                document=as_dict(row.get("profileDocument")).get("path"),
                key=profile.get("key"),
                catalog_state=row.get("catalogState"),
                assets=asset_states,
                stage=row.get("stageState"),
                references=len(as_list(row.get("routeReferences"))),
            )
        )
    lines.extend([
        "",
        "## Route choices",
        "",
        "Each row keeps its viable historical profile revisions visible. A selected revision is either the route's only stage-ready candidate or an exact hash-pinned choice from the selection ledger. This never transfers an archive result between revisions.",
        "",
        "| Group | Route | Next staging action | Selected revision | Candidate revisions |",
        "|---|---|---|---|---:|",
    ])
    for raw_choice in as_list(report.get("routeChoices")):
        choice = as_dict(raw_choice)
        lines.append(
            "| `{group}` | `{route}` | `{action}` | {selected} | {candidates} |".format(
                group=choice.get("topologyGroup"),
                route=choice.get("routeKind"),
                action=choice.get("nextStagingAction"),
                selected=(
                    "`{key}` [{path}]".format(
                        key=as_dict(choice.get("selectedRevision")).get("key"),
                        path=as_dict(as_dict(choice.get("selectedRevision")).get("profileDocument")).get("path"),
                    ) if as_dict(choice.get("selectedRevision")) else "none"
                ),
                candidates=len(as_list(choice.get("candidateRevisions"))),
            )
        )
    conflict_report = as_dict(report.get("conflicts"))
    lines.extend([
        "",
        "## Conflicts to resolve explicitly",
        "",
        "Profile-key variants: " + (", ".join(
            "`{catalogKind}:{key}` ({distinctProfileVariants})".format(**as_dict(item))
            for item in as_list(conflict_report.get("profileKeyVariants"))
        ) or "none"),
        "",
        "Asset-name variants: " + (", ".join(
            "`{asset}` ({distinctSourceHashes})".format(**as_dict(item))
            for item in as_list(conflict_report.get("assetNameVariants"))
        ) or "none"),
        "",
    ])
    return "\n".join(lines)


def isolated_game(root: Path, value: Path) -> Path:
    game = value.resolve()
    if not game.is_dir() or game.is_symlink() or game.parent != root / "scratch":
        raise ValueError("game root must be a real direct child of repository scratch/")
    return game


def assert_game_stopped(game: Path) -> None:
    executable = str(game / "FTK.app/Contents/MacOS/FTK")
    commands = subprocess.check_output(["ps", "-axo", "command="], text=True).splitlines()
    if any(command == executable or command.startswith(executable + " ") for command in commands):
        raise ValueError("isolated FTK is running; stop its owned process before reading a stage baseline")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--queue", type=Path, default=Path("docs/model-validation-execution-queue.json"))
    parser.add_argument("--selections", type=Path, default=Path("docs/model-validation-profile-selections.json"))
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--output-json", type=Path, required=True)
    parser.add_argument("--output-markdown", type=Path, required=True)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()
    root = args.root.resolve()
    queue_path = args.queue if args.queue.is_absolute() else root / args.queue
    if not queue_path.is_file():
        parser.error(f"missing execution queue: {queue_path}")
    selections_path = args.selections if args.selections.is_absolute() else root / args.selections
    if not selections_path.is_file():
        parser.error(f"missing profile selections: {selections_path}")
    game = isolated_game(root, args.game_root if args.game_root.is_absolute() else root / args.game_root)
    assert_game_stopped(game)
    report = build_report(root, read_object(queue_path), game, read_object(selections_path))
    report["queue"]["input"] = input_reference(root, queue_path)
    report["profileSelections"]["input"] = input_reference(root, selections_path)
    output_json = args.output_json if args.output_json.is_absolute() else root / args.output_json
    output_markdown = args.output_markdown if args.output_markdown.is_absolute() else root / args.output_markdown
    write_new(output_json, json.dumps(report, indent=2) + "\n", args.overwrite)
    write_new(output_markdown, markdown(report), args.overwrite)
    print(json.dumps(report["summary"], indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
