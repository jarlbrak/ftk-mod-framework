#!/usr/bin/env python3
"""Resolve one FTK validation route into exact local rig-authoring inputs.

This planner reads the current queue, stage ledger, renderer inventory, and
Blender bridge audit. It never extracts native data, launches Blender, writes a
game file, or claims live or artistic acceptance. Its command templates keep
copyrighted reference data under ignored scratch storage.
"""
from __future__ import annotations

import argparse
import copy
import json
from pathlib import Path
import shlex
import sys
from typing import Any


TOOL_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOL_DIR / "runtime-test"))
import run_execution_queue_route as route_runner


Json = dict[str, Any]
ADAPTER_ACTIONS = {
    "adapter_or_retarget_design_required",
    "adapter_implementation_required",
    "adapter_validation_required",
    "adapter_visual_archive_review_required",
}


def command_text(arguments: list[str]) -> str:
    return " ".join(shlex.quote(value) for value in arguments)


def reference_pair(root: Path, renderer: Json) -> Json | None:
    renderer_id = renderer.get("renderer_path_id")
    if not isinstance(renderer_id, int):
        raise ValueError("renderer inventory row lacks its path ID")
    for base in (root / "scratch/skeleton-audit", root / "scratch/all-raw-rig-audit"):
        reference = base / str(renderer_id) / "reference.npz"
        skeleton = base / str(renderer_id) / "skeleton.json"
        if reference.is_file() and skeleton.is_file() and not reference.is_symlink() and not skeleton.is_symlink():
            skeleton_data = route_runner.read_object(skeleton)
            if (skeleton_data.get("renderer_path_id") != renderer_id
                    or skeleton_data.get("mesh_name") != renderer.get("mesh_name")
                    or skeleton_data.get("bone_names") != renderer.get("bone_names")
                    or len(route_runner.as_list(skeleton_data.get("bone_names"))) != renderer.get("joint_count")):
                raise ValueError(f"local reference identity differs from renderer inventory path ID {renderer_id}")
            return {
                "reference": route_runner.input_reference(root, reference),
                "skeleton": route_runner.input_reference(root, skeleton),
                "identityVerifiedAgainstInventory": True,
            }
    return None


def selected_preflight_assignments(route: Json, choice: Json) -> tuple[Json, list[Json]]:
    selected = route_runner.as_dict(choice.get("selectedRevision"))
    selected_document = route_runner.as_dict(selected.get("profileDocument"))
    matches = [
        route_runner.as_dict(raw)
        for raw in route_runner.as_list(route.get("preflightedProfileCandidates"))
        if route_runner.as_dict(route_runner.as_dict(raw).get("profileDocument")) == selected_document
        and route_runner.as_dict(raw).get("key") == selected.get("key")
    ]
    if len(matches) != 1:
        raise ValueError("route lacks one exact preflight candidate for its selected revision")
    assignments = [route_runner.as_dict(raw) for raw in route_runner.as_list(matches[0].get("assignments"))]
    if not assignments:
        raise ValueError("selected preflight candidate has no renderer assignments")
    return selected, assignments


def integration_profile_template(profile: Json) -> tuple[Json, Json, list[str]]:
    template = copy.deepcopy(profile)
    template["key"] = (
        "ftkmf_modeltest_player_replace_unique_key"
        if "baseClass" in profile else "ftkmf_modeltest_replace_with_unique_key"
    )
    template["displayName"] = "Replace With Original Model Name"
    asset_names: dict[tuple[str, str], str] = {}
    counters = {"glbFile": 0, "textureFile": 0}

    def replacement(field: str, value: Any) -> Any:
        if not isinstance(value, str):
            return value
        identity = (field, value)
        if identity not in asset_names:
            counters[field] += 1
            if field == "glbFile":
                asset_names[identity] = f"original-model-{counters[field]:02d}.glb"
            else:
                asset_names[identity] = f"original-texture-{counters[field]:02d}.png"
        return asset_names[identity]

    assignments = [*route_runner.as_list(template.get("renderers")), *route_runner.as_list(template.get("apparel"))]
    for raw in assignments:
        assignment = route_runner.as_dict(raw)
        for field in ("glbFile", "textureFile"):
            if field in assignment:
                assignment[field] = replacement(field, assignment[field])
        for raw_slot in route_runner.as_list(assignment.get("materialSlots")):
            slot = route_runner.as_dict(raw_slot)
            if "textureFile" in slot:
                slot["textureFile"] = replacement("textureFile", slot["textureFile"])
    asset_map = {
        original: replacement_name
        for (field, original), replacement_name in sorted(asset_names.items())
    }
    route_fields = {
        "key", "displayName", "baseEnemy", "resourcePrefab", "combatProfile",
        "baseClass", "skinset", "defaultSkinType", "renderers", "apparel",
    }
    review_fields = sorted(field for field in profile if field not in route_fields)
    for index, raw in enumerate(route_runner.as_list(profile.get("renderers"))):
        assignment = route_runner.as_dict(raw)
        for field in assignment:
            if field not in {"rendererPath", "rendererKind", "glbFile", "textureFile", "materialSlots"}:
                review_fields.append(f"renderers/{index}/{field}")
        for slot_index, raw_slot in enumerate(route_runner.as_list(assignment.get("materialSlots"))):
            for field in route_runner.as_dict(raw_slot):
                if field not in {"primitiveIndex", "nativeMaterialSlot", "textureFile"}:
                    review_fields.append(f"renderers/{index}/materialSlots/{slot_index}/{field}")
    return {"version": 1, "profiles": [template]}, asset_map, sorted(set(review_fields))


def integration_commands(route_kind: str, catalog_kind: str) -> Json:
    validator = {
        "directEnemy": "validate_custom_model_profile_route.py",
        "resourcePrefab": "validate_resource_model_profile_route.py",
        "playerSkinset": "validate_player_model_profile_route.py",
    }.get(route_kind)
    if validator is None:
        raise ValueError(f"unsupported profile route kind: {route_kind}")
    preflight = [
        "python3",
        f"tools/ai-model-pipeline/{validator}",
        "--profile",
        "PACKAGE_DIR/runtime-profile.json",
        "--asset-dir",
        "PACKAGE_DIR",
        "--output",
        "scratch/model-preflight-NEW_LABEL.json",
    ]
    stage = [
        "python3",
        "tools/ai-model-pipeline/stage_custom_model_profile.py",
        "--catalog-kind",
        catalog_kind,
        "--game-root",
        "scratch/my-isolated-game",
        "--profile",
        "PACKAGE_DIR/runtime-profile.json",
        "--asset-dir",
        "PACKAGE_DIR",
        "--pin",
        "scratch/model-preflight-NEW_LABEL.json",
        "--output",
        "scratch/model-stage-NEW_LABEL",
    ]
    return {
        "preflightCommand": preflight,
        "preflightCommandText": command_text(preflight),
        "stageCommand": stage,
        "stageCommandText": command_text(stage),
    }


def apparel_inventory_rows(exact_profile: Json | None, renderers: dict[int, Json]) -> list[tuple[Json, Json]]:
    if exact_profile is None:
        return []
    rows: list[tuple[Json, Json]] = []
    for raw in route_runner.as_list(exact_profile.get("apparel")):
        apparel = route_runner.as_dict(raw)
        mesh_name = apparel.get("expectedNativeMeshName")
        renderer_path = apparel.get("rendererPath")
        if not isinstance(mesh_name, str) or not mesh_name or not isinstance(renderer_path, str) or not renderer_path:
            raise ValueError("apparel assignment lacks an exact renderer path or native mesh name")
        matches = [renderer for renderer in renderers.values() if renderer.get("mesh_name") == mesh_name]
        if len(matches) != 1:
            raise ValueError(
                f"apparel native mesh must resolve to one exact rig renderer: {mesh_name!r} found {len(matches)}"
            )
        renderer = matches[0]
        assignment = {
            "rendererPath": renderer_path,
            "sourceRendererId": renderer["renderer_path_id"],
            "rendererKind": "SkinnedMeshRenderer",
            "expectedNativeMeshName": mesh_name,
            "conditionalApparel": True,
        }
        rows.append((assignment, renderer))
    return rows


def authoring_target(root: Path, context: Json, workspace: Path, assignment: Json, renderer: Json) -> Json:
    if assignment.get("rendererKind", "SkinnedMeshRenderer") != "SkinnedMeshRenderer":
        raise ValueError("rig authoring targets must be SkinnedMeshRenderer assignments")
    renderer_id = int(assignment["sourceRendererId"])
    rig = renderer.get("rig_profile_fingerprint")
    bridge = route_runner.as_dict(context["bridgeByRig"].get(rig))
    if bridge.get("status") != "offline_blender_bridge_pass":
        raise ValueError(f"exact rig profile lacks a passing Blender bridge result: {rig}")
    target_root = workspace / str(renderer_id)
    reference_root = target_root / "reference"
    scene = target_root / "authoring.blend"
    extract_arguments = [
        "scratch/model-venv/bin/python",
        "tools/ai-model-pipeline/extract_reference.py",
        "--assets",
        str(context["source"]),
        "--renderer-id",
        str(renderer_id),
        "--output",
        str(reference_root),
    ]
    blender_executable = Path("/Applications/Blender.app/Contents/MacOS/Blender")
    blender_name = str(blender_executable) if blender_executable.is_file() else "blender"
    scaffold_arguments = [
        blender_name,
        "--background",
        "--factory-startup",
        "--python-exit-code",
        "1",
        "--python",
        "tools/ai-model-pipeline/create_blender_template.py",
        "--",
        "--reference",
        str(reference_root / "reference.npz"),
        "--skeleton",
        str(reference_root / "skeleton.json"),
        "--output",
        str(scene),
        "--python-executable",
        "scratch/model-venv/bin/python",
    ]
    bridge_renderer = bridge.get("renderer_path_id")
    bridge_audit_path = Path(str(bridge.get("_auditPath", context["bridgePath"])))
    bridge_reference = bridge_audit_path.parent / str(bridge_renderer)
    return {
        "rendererPath": assignment.get("rendererPath"),
        "sourceRendererId": renderer_id,
        "rendererKind": assignment.get("rendererKind", "SkinnedMeshRenderer"),
        "conditionalApparel": assignment.get("conditionalApparel", False),
        "expectedNativeMeshName": assignment.get("expectedNativeMeshName"),
        "meshName": renderer.get("mesh_name"),
        "jointCount": renderer.get("joint_count"),
        "topologyGroup": str(renderer.get("topology_fingerprint"))[:16],
        "topologyFingerprint": renderer.get("topology_fingerprint"),
        "bindposeFingerprint": renderer.get("bindpose_fingerprint"),
        "rigProfileFingerprint": rig,
        "controllerNames": sorted({
            str(row.get("controller_name"))
            for row in route_runner.as_list(renderer.get("animators"))
            if route_runner.as_dict(row).get("controller_name")
        }),
        "existingLocalReference": reference_pair(root, renderer),
        "verifiedBlenderBridge": {
            "representativeRendererId": bridge_renderer,
            "status": bridge.get("status"),
            "referenceSha256": bridge.get("reference_sha256"),
            "skeletonSha256": bridge.get("skeleton_sha256"),
            "sceneSha256": bridge.get("scene_sha256"),
            "glbSha256": bridge.get("glb_sha256"),
            "localAuditDirectory": route_runner.relative(root, bridge_reference),
            "audit": route_runner.input_reference(root, bridge_audit_path),
        },
        "freshReferenceOutput": str(reference_root),
        "freshSceneOutput": str(scene),
        "extractCommand": extract_arguments,
        "extractCommandText": command_text(extract_arguments),
        "scaffoldCommand": scaffold_arguments,
        "scaffoldCommandText": command_text(scaffold_arguments),
    }


def load_context(
    root: Path,
    queue_path: Path,
    stage_path: Path,
    game: Path,
    inventory_path: Path,
    bridge_path: Path,
    supplemental_bridge_paths: list[Path] | None = None,
) -> Json:
    queue = route_runner.read_object(queue_path)
    stage = route_runner.read_object(stage_path)
    inventory = route_runner.read_object(inventory_path)
    bridge = route_runner.read_object(bridge_path)
    supplemental_bridge_paths = supplemental_bridge_paths or []
    supplemental_bridges = [route_runner.read_object(path) for path in supplemental_bridge_paths]
    stage_queue = route_runner.as_dict(route_runner.as_dict(stage.get("queue")).get("input"))
    if (stage_queue.get("path") != route_runner.relative(root, queue_path)
            or stage_queue.get("sha256") != route_runner.sha256(queue_path)):
        raise ValueError("stage-readiness ledger is stale for the current execution queue")
    source_text = inventory.get("source_file")
    source_hash = inventory.get("source_sha256")
    if not isinstance(source_text, str) or not isinstance(source_hash, str):
        raise ValueError("renderer inventory lacks its exact native source identity")
    source = Path(source_text).resolve(strict=False)
    if not source.is_file() or source.is_symlink() or route_runner.sha256(source) != source_hash:
        raise ValueError("renderer inventory is stale for the current native source asset")
    if bridge.get("source_sha256") != source_hash:
        raise ValueError("Blender bridge audit belongs to a different native source build")
    if any(report.get("source_sha256") != source_hash for report in supplemental_bridges):
        raise ValueError("supplemental Blender bridge audit belongs to a different native source build")
    renderers: dict[int, Json] = {}
    for raw in route_runner.as_list(inventory.get("renderers")):
        renderer = route_runner.as_dict(raw)
        renderer_id = renderer.get("renderer_path_id")
        if isinstance(renderer_id, int):
            if renderer_id in renderers:
                raise ValueError(f"renderer inventory repeats path ID {renderer_id}")
            renderers[renderer_id] = renderer
    bridge_by_rig: dict[str, Json] = {}
    for audit_path, report in [(bridge_path, bridge), *zip(supplemental_bridge_paths, supplemental_bridges)]:
        for raw in route_runner.as_list(report.get("results")):
            result = route_runner.as_dict(raw)
            rig = result.get("rig_profile_fingerprint")
            if not isinstance(rig, str) or not rig:
                continue
            if rig in bridge_by_rig:
                raise ValueError(f"Blender bridge audits repeat rig profile {rig}")
            bridge_by_rig[rig] = {**result, "_auditPath": str(audit_path)}
    return {
        "queuePath": queue_path,
        "queue": queue,
        "stagePath": stage_path,
        "stage": stage,
        "game": game,
        "inventoryPath": inventory_path,
        "inventory": inventory,
        "bridgePath": bridge_path,
        "bridge": bridge,
        "supplementalBridgePaths": supplemental_bridge_paths,
        "supplementalBridges": supplemental_bridges,
        "source": source,
        "renderers": renderers,
        "bridgeByRig": bridge_by_rig,
    }


def build_route_kit(root: Path, context: Json, topology_group: str, route_kind: str | None) -> Json:
    if len(topology_group) != 16 or any(character not in "0123456789abcdef" for character in topology_group):
        raise ValueError("topology group must be an exact 16-character lowercase hexadecimal prefix")
    queue = route_runner.as_dict(context["queue"])
    stage = route_runner.as_dict(context["stage"])
    route = route_runner.selected_route(queue, topology_group, route_kind)
    kind = str(route.get("routeKind"))
    choice = route_runner.selected_choice(stage, topology_group, kind)
    action = choice.get("nextStagingAction")
    profile: Json | None = None
    catalog: Json | None = None
    exact_profile: Json | None = None
    catalog_kind: str | None = None
    if action == "stage_ready_revision_available":
        candidate = route_runner.choose_profile(choice, None)
        validated = route_runner.validate_stage_ledger(
            root,
            context["queuePath"],
            queue,
            context["stagePath"],
            stage,
            context["game"],
            route,
            candidate,
            choice.get("selectedMotionRendererPath"),
        )
        selected, raw_assignments = selected_preflight_assignments(route, choice)
        profile = {"key": selected["key"], "document": selected["profileDocument"]}
        catalog = route_runner.as_dict(validated.get("catalog"))
        catalog_kind = str(candidate.get("catalogKind"))
        revision = route_runner.revision_row(
            stage, selected["profileDocument"]["path"], selected["key"]
        )
        exact_profile = route_runner.as_dict(revision.get("profile"))
    elif action in ADAPTER_ACTIONS:
        raw_assignments = [
            route_runner.as_dict(raw)
            for raw in route_runner.as_list(route.get("selectedValidationTargets"))
            if route_runner.as_dict(raw).get("targetType") == "source_assignment"
        ]
    else:
        raise ValueError(f"route has no exact stage or adapter authoring input: {action!r}")

    all_inventory_rows: list[tuple[Json, Json]] = []
    for assignment in raw_assignments:
        renderer_id = assignment.get("sourceRendererId")
        if not isinstance(renderer_id, int):
            continue
        renderer = route_runner.as_dict(context["renderers"].get(renderer_id))
        fingerprint = renderer.get("topology_fingerprint")
        if isinstance(fingerprint, str) and fingerprint:
            all_inventory_rows.append((assignment, renderer))
    inventory_rows = [
        (assignment, renderer) for assignment, renderer in all_inventory_rows
        if str(renderer.get("topology_fingerprint")).startswith(topology_group)
    ]
    if not inventory_rows:
        raise ValueError("selected route has no exact renderer assignment in this topology group")

    profile_template: Json | None = None
    asset_map: Json | None = None
    copied_settings: list[str] = []
    commands: Json | None = None
    if exact_profile is not None and catalog_kind is not None:
        profile_template, asset_map, copied_settings = integration_profile_template(exact_profile)
        commands = integration_commands(kind, catalog_kind)

    workspace = Path("scratch") / "model-authoring-NEW_LABEL" / topology_group
    targets = [authoring_target(root, context, workspace, assignment, renderer) for assignment, renderer in inventory_rows]
    companion_targets = [
        authoring_target(root, context, workspace, assignment, renderer)
        for assignment, renderer in all_inventory_rows
        if assignment.get("rendererKind", "SkinnedMeshRenderer") == "SkinnedMeshRenderer"
        and not str(renderer.get("topology_fingerprint")).startswith(topology_group)
    ]
    apparel_targets = [
        authoring_target(root, context, workspace, assignment, renderer)
        for assignment, renderer in apparel_inventory_rows(exact_profile, context["renderers"])
    ]
    return {
        "schemaVersion": 1,
        "scope": (
            "Exact local rig-authoring inputs for one current route. Extracted references remain copyrighted local data. "
            "Passing offline bridge evidence does not establish live binding, motion, gameplay, appearance, or art acceptance."
        ),
        "route": {
            "topologyGroup": topology_group,
            "routeKind": kind,
            "nextStagingAction": action,
            "profile": profile,
            "catalog": catalog,
            "adapterContract": route_runner.as_dict(route_runner.as_dict(route.get("executionStatus")).get("adapterContract")) or None,
        },
        "integration": {
            "profileTemplate": profile_template,
            "selectedProfileAssetMap": asset_map,
            "copiedOptionalSettingsRequiringReview": copied_settings,
            "commands": commands,
            "selectedTopologyRendererPaths": [target["rendererPath"] for target in targets],
            "completeProfileRendererPaths": [
                route_runner.as_dict(raw).get("rendererPath")
                for raw in route_runner.as_list(exact_profile.get("renderers") if exact_profile else [])
            ],
            "additionalProfileRendererPaths": sorted(set(
                route_runner.as_dict(raw).get("rendererPath")
                for raw in route_runner.as_list(exact_profile.get("renderers") if exact_profile else [])
                if route_runner.as_dict(raw).get("rendererPath") not in {target["rendererPath"] for target in targets}
            )),
            "companionRigTargets": companion_targets,
            "apparelRigTargets": apparel_targets,
        },
        "inputs": {
            "executionQueue": route_runner.input_reference(root, context["queuePath"]),
            "stageReadiness": route_runner.input_reference(root, context["stagePath"]),
            "rendererInventory": route_runner.input_reference(root, context["inventoryPath"]),
            "blenderBridgeAudit": route_runner.input_reference(root, context["bridgePath"]),
            "supplementalBlenderBridgeAudits": [
                route_runner.input_reference(root, path) for path in context["supplementalBridgePaths"]
            ],
            "nativeSourceAsset": route_runner.input_reference(root, context["source"]),
        },
        "workspaceTemplate": str(workspace),
        "summary": {
            "rendererTargets": len(targets),
            "existingLocalReferences": sum(target["existingLocalReference"] is not None for target in targets),
            "verifiedRigBridgeProfiles": len({target["rigProfileFingerprint"] for target in targets}),
            "companionRigTargets": len(companion_targets),
            "apparelRigTargets": len(apparel_targets),
        },
        "targets": targets,
        "limits": [
            "Replace NEW_LABEL before running either command and use a new ignored scratch destination.",
            "Do not commit or redistribute reference.npz, skeleton.json, native reference geometry, or native assets.",
            "The generated Blender scene starts with original calibration geometry only; replace it with original authored art.",
            "Replace PACKAGE_DIR, NEW_LABEL, the starter key, display name, and every original-model or original-texture filename before preflight.",
            "Review every copied optional setting explicitly; it is preserved from the selected working profile rather than declared universally correct.",
            "Companion rig targets provide exact bind variants for skinned assignments outside the selected topology group.",
            "Apparel rig targets provide exact bind variants for conditional skinned equipment assignments.",
            "Rigid MeshRenderer assignments remain structural profile data and do not receive rig-authoring scaffolds.",
            "Run exact route preflight, isolated deployment, live behavior capture, manual visual review, and immutable archive validation separately.",
        ],
    }


def build_catalog(root: Path, context: Json) -> Json:
    kits: list[Json] = []
    for raw in route_runner.as_list(route_runner.as_dict(context["queue"]).get("routes")):
        route = route_runner.as_dict(raw)
        group = route.get("topologyGroup")
        kind = route.get("routeKind")
        if not isinstance(group, str) or not isinstance(kind, str):
            raise ValueError("execution queue route lacks an exact topology group or route kind")
        kit = build_route_kit(root, context, group, kind)
        kits.append({
            "route": kit["route"],
            "integration": kit["integration"],
            "workspaceTemplate": kit["workspaceTemplate"],
            "summary": kit["summary"],
            "targets": kit["targets"],
            "limits": kit["limits"],
        })
    primary_targets = [
        route_runner.as_dict(target)
        for kit in kits
        for target in route_runner.as_list(kit.get("targets"))
    ]
    companion_targets = [
        route_runner.as_dict(target)
        for kit in kits
        for target in route_runner.as_list(route_runner.as_dict(kit.get("integration")).get("companionRigTargets"))
    ]
    apparel_targets = [
        route_runner.as_dict(target)
        for kit in kits
        for target in route_runner.as_list(route_runner.as_dict(kit.get("integration")).get("apparelRigTargets"))
    ]
    rig_profiles = {
        target["rigProfileFingerprint"]
        for target in primary_targets
    }
    all_rig_profiles = {
        target["rigProfileFingerprint"]
        for target in [*primary_targets, *companion_targets, *apparel_targets]
    }
    return {
        "schemaVersion": 1,
        "scope": (
            "Current exact local authoring-kit catalog for every unfinished validation route. "
            "It maps routes to reproducible rig extraction and Blender scaffold commands only."
        ),
        "inputs": {
            "executionQueue": route_runner.input_reference(root, context["queuePath"]),
            "stageReadiness": route_runner.input_reference(root, context["stagePath"]),
            "rendererInventory": route_runner.input_reference(root, context["inventoryPath"]),
            "blenderBridgeAudit": route_runner.input_reference(root, context["bridgePath"]),
            "supplementalBlenderBridgeAudits": [
                route_runner.input_reference(root, path) for path in context["supplementalBridgePaths"]
            ],
            "nativeSourceAsset": route_runner.input_reference(root, context["source"]),
        },
        "summary": {
            "routes": len(kits),
            "rendererTargets": sum(int(kit["summary"]["rendererTargets"]) for kit in kits),
            "routesWithAllExistingLocalReferences": sum(
                kit["summary"]["existingLocalReferences"] == kit["summary"]["rendererTargets"] for kit in kits
            ),
            "verifiedRigBridgeProfiles": len(rig_profiles),
            "companionRigTargets": len(companion_targets),
            "uniqueCompanionRendererTargets": len({target["sourceRendererId"] for target in companion_targets}),
            "apparelRigTargets": len(apparel_targets),
            "uniqueApparelRendererTargets": len({target["sourceRendererId"] for target in apparel_targets}),
            "allUniqueRigTargets": len({
                target["sourceRendererId"]
                for target in [*primary_targets, *companion_targets, *apparel_targets]
            }),
            "allVerifiedRigBridgeProfiles": len(all_rig_profiles),
            "routesWithAllRequiredLocalReferences": sum(
                all(
                    route_runner.as_dict(target).get("existingLocalReference") is not None
                    for target in [
                        *route_runner.as_list(kit.get("targets")),
                        *route_runner.as_list(route_runner.as_dict(kit.get("integration")).get("companionRigTargets")),
                        *route_runner.as_list(route_runner.as_dict(kit.get("integration")).get("apparelRigTargets")),
                    ]
                )
                for kit in kits
            ),
            "routesWithoutAuthoringTargets": sum(not route_runner.as_list(kit.get("targets")) for kit in kits),
        },
        "routes": kits,
        "limits": [
            "Replace NEW_LABEL independently for each chosen route and use new ignored scratch destinations.",
            "This catalog does not transfer evidence between routes or award live, motion, gameplay, visual, or archive credit.",
            "Adapter-bound routes retain their adapter requirement even when their old rig can be scaffolded.",
        ],
    }


def output_path(root: Path, value: Path) -> Path:
    candidate = value if value.is_absolute() else root / value
    if candidate.is_symlink():
        raise ValueError("authoring-kit output must not be a symlink")
    output = candidate.resolve(strict=False)
    if output.parent != root / "scratch" or output.suffix.lower() != ".json":
        raise ValueError("authoring-kit output must be a .json file directly under repository scratch/")
    return output


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--queue", type=Path, default=Path("docs/model-validation-execution-queue.json"))
    parser.add_argument("--stage-readiness", type=Path, required=True)
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--inventory", type=Path, default=Path("scratch/skeleton-inventory-reproducible.json"))
    parser.add_argument("--blender-audit", type=Path, default=Path("scratch/blender-all-rigs/bridge-audit.json"))
    parser.add_argument("--supplemental-blender-audit", type=Path, action="append", default=[])
    selection = parser.add_mutually_exclusive_group(required=True)
    selection.add_argument("--topology-group")
    selection.add_argument("--all-routes", action="store_true")
    parser.add_argument("--route-kind", choices=("directEnemy", "resourcePrefab", "playerSkinset"))
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    root = route_runner.find_root(args.root)
    try:
        queue_path = route_runner.repository_file(root, args.queue, "execution queue")
        stage_path = route_runner.repository_file(root, args.stage_readiness, "stage readiness")
        inventory_path = route_runner.repository_file(root, args.inventory, "renderer inventory")
        bridge_path = route_runner.repository_file(root, args.blender_audit, "Blender bridge audit")
        supplemental_bridge_paths = [
            route_runner.repository_file(root, path, "supplemental Blender bridge audit")
            for path in args.supplemental_blender_audit
        ]
        game = route_runner.isolated_game(root, args.game_root)
        context = load_context(
            root, queue_path, stage_path, game, inventory_path, bridge_path, supplemental_bridge_paths
        )
        if args.all_routes:
            if args.route_kind is not None:
                raise ValueError("--route-kind is valid only with --topology-group")
            report = build_catalog(root, context)
        else:
            report = build_route_kit(root, context, args.topology_group, args.route_kind)
    except (ValueError, FileNotFoundError) as error:
        parser.error(str(error))
    content = json.dumps(report, indent=2, allow_nan=False) + "\n"
    if args.output is None:
        print(content, end="")
        return 0
    try:
        destination = output_path(root, args.output)
    except ValueError as error:
        parser.error(str(error))
    if destination.exists():
        parser.error(f"refusing to overwrite authoring-kit plan: {destination}")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_text(content)
    print(json.dumps({"status": "AUTHORING_KIT_PLANNED", "output": route_runner.relative(root, destination), **report["summary"]}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
