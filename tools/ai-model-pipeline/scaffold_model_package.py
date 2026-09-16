#!/usr/bin/env python3
"""Create a safe original-asset package workspace for one authoring route.

The scaffolder validates every pinned catalog input before writing a new direct
child of art-experiments/. It creates metadata and instructions only. It never
copies native geometry, invents asset files, stages a game, or claims runtime or
visual acceptance.
"""
from __future__ import annotations

import argparse
import copy
import hashlib
import json
from pathlib import Path
import re
import shlex
from typing import Any


Json = dict[str, Any]
ROUTE_KINDS = {"directEnemy", "resourcePrefab", "playerSkinset"}
SLUG_PATTERN = re.compile(r"[a-z0-9]+(?:-[a-z0-9]+)*")
KEY_PATTERN = re.compile(r"ftkmf_modeltest_[a-z0-9]+(?:_[a-z0-9]+)*")


def read_object(path: Path) -> Json:
    value = json.loads(path.read_text())
    if not isinstance(value, dict):
        raise ValueError(f"expected JSON object: {path}")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def sha256_text(value: str) -> str:
    return hashlib.sha256(value.encode()).hexdigest()


def find_root(start: Path) -> Path:
    for candidate in (start.resolve(), *start.resolve().parents):
        if (candidate / "FTKModFramework").is_dir() and (candidate / "tools").is_dir():
            return candidate
    raise ValueError(f"could not locate FTK repository above {start}")


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root))
    except ValueError:
        return str(path.resolve())


def pinned_path(root: Path, pin: Any, label: str) -> Path:
    if not isinstance(pin, dict):
        raise ValueError(f"{label} pin is not an object")
    path_text = pin.get("path")
    expected = pin.get("sha256")
    if not isinstance(path_text, str) or not path_text or not isinstance(expected, str):
        raise ValueError(f"{label} pin lacks path and sha256")
    raw = Path(path_text)
    path = (raw if raw.is_absolute() else root / raw).resolve(strict=False)
    if not path.is_file() or path.is_symlink():
        raise ValueError(f"{label} is missing or linked: {path}")
    if sha256(path) != expected:
        raise ValueError(f"{label} hash differs from the authoring catalog: {path}")
    return path


def target_rows(route: Json) -> list[Json]:
    integration = route.get("integration")
    if not isinstance(integration, dict):
        raise ValueError("authoring route lacks integration data")
    rows = [
        *route.get("targets", []),
        *integration.get("companionRigTargets", []),
        *integration.get("apparelRigTargets", []),
    ]
    if not all(isinstance(row, dict) for row in rows):
        raise ValueError("authoring route has a nonobject rig target")
    return rows


def validate_catalog(root: Path, catalog_path: Path) -> Json:
    catalog_path = catalog_path.resolve(strict=False)
    if (not catalog_path.is_file() or catalog_path.is_symlink()
            or not catalog_path.is_relative_to(root / "scratch")):
        raise ValueError("authoring catalog must be a real file under repository scratch/")
    catalog = read_object(catalog_path)
    if catalog.get("schemaVersion") != 1:
        raise ValueError("unsupported authoring catalog schema")
    inputs = catalog.get("inputs")
    if not isinstance(inputs, dict) or not inputs:
        raise ValueError("authoring catalog has no pinned inputs")
    for name, pin in inputs.items():
        if isinstance(pin, list):
            for index, item in enumerate(pin):
                pinned_path(root, item, f"catalog input {name}/{index}")
        else:
            pinned_path(root, pin, f"catalog input {name}")
    routes = catalog.get("routes")
    if not isinstance(routes, list) or not routes:
        raise ValueError("authoring catalog has no routes")

    checked_pins: set[tuple[str, str]] = set()
    for raw_route in routes:
        if not isinstance(raw_route, dict):
            raise ValueError("authoring catalog route is not an object")
        route = raw_route.get("route")
        if not isinstance(route, dict):
            raise ValueError("authoring catalog route identity is missing")
        for name in ("catalog",):
            pin = route.get(name)
            if isinstance(pin, dict):
                identity = (str(pin.get("path")), str(pin.get("sha256")))
                if identity not in checked_pins:
                    pinned_path(root, pin, f"route {name}")
                    checked_pins.add(identity)
        profile = route.get("profile")
        if isinstance(profile, dict) and isinstance(profile.get("document"), dict):
            pin = profile["document"]
            identity = (str(pin.get("path")), str(pin.get("sha256")))
            if identity not in checked_pins:
                pinned_path(root, pin, "route profile document")
                checked_pins.add(identity)
        for target in target_rows(raw_route):
            local = target.get("existingLocalReference")
            if not isinstance(local, dict):
                raise ValueError("rig target lacks an existing local reference")
            for name in ("reference", "skeleton"):
                pin = local.get(name)
                identity = (str(pin.get("path")) if isinstance(pin, dict) else "",
                            str(pin.get("sha256")) if isinstance(pin, dict) else "")
                if identity not in checked_pins:
                    pinned_path(root, pin, f"target {target.get('sourceRendererId')} {name}")
                    checked_pins.add(identity)
            bridge = target.get("verifiedBlenderBridge")
            bridge_pin = bridge.get("audit") if isinstance(bridge, dict) else None
            identity = (
                str(bridge_pin.get("path")) if isinstance(bridge_pin, dict) else "",
                str(bridge_pin.get("sha256")) if isinstance(bridge_pin, dict) else "",
            )
            if identity not in checked_pins:
                pinned_path(root, bridge_pin, f"target {target.get('sourceRendererId')} Blender audit")
                checked_pins.add(identity)
    return catalog


def select_route(catalog: Json, topology_group: str, route_kind: str) -> Json:
    if not re.fullmatch(r"[0-9a-f]{16}", topology_group):
        raise ValueError("topology group must be an exact 16-character lowercase hexadecimal value")
    if route_kind not in ROUTE_KINDS:
        raise ValueError(f"unsupported route kind: {route_kind}")
    matches = [
        route for route in catalog["routes"]
        if route["route"].get("topologyGroup") == topology_group
        and route["route"].get("routeKind") == route_kind
    ]
    if len(matches) != 1:
        raise ValueError(f"expected one exact authoring route, found {len(matches)}")
    selected = matches[0]
    integration = selected["integration"]
    if selected["route"].get("nextStagingAction") != "stage_ready_revision_available":
        raise ValueError("selected route is adapter-bound and cannot use a generic package scaffold")
    if not isinstance(integration.get("profileTemplate"), dict) or not isinstance(integration.get("commands"), dict):
        raise ValueError("selected route lacks a stageable integration template")
    return selected


def current_identities(root: Path, catalog: Json) -> tuple[set[str], set[str], set[str]]:
    keys: set[str] = set()
    displays: set[str] = set()
    assets: set[str] = set()
    seen: set[tuple[str, str]] = set()
    for raw in catalog["routes"]:
        route = raw["route"]
        pin = route.get("catalog")
        if not isinstance(pin, dict):
            continue
        identity = (str(pin.get("path")), str(pin.get("sha256")))
        if identity in seen:
            continue
        seen.add(identity)
        document = read_object(pinned_path(root, pin, "current isolated profile catalog"))
        for profile in document.get("profiles", []):
            if not isinstance(profile, dict):
                continue
            if isinstance(profile.get("key"), str):
                keys.add(profile["key"])
            if isinstance(profile.get("displayName"), str):
                displays.add(profile["displayName"].casefold())
            for assignment in [*profile.get("renderers", []), *profile.get("apparel", [])]:
                if not isinstance(assignment, dict):
                    continue
                for field in ("glbFile", "textureFile"):
                    if isinstance(assignment.get(field), str):
                        assets.add(assignment[field])
                for slot in assignment.get("materialSlots", []):
                    if isinstance(slot, dict) and isinstance(slot.get("textureFile"), str):
                        assets.add(slot["textureFile"])
    return keys, displays, assets


def validate_identity(
    key: str,
    display_name: str,
    asset_prefix: str,
    existing_keys: set[str],
    existing_displays: set[str],
    existing_assets: set[str],
) -> None:
    if not KEY_PATTERN.fullmatch(key):
        raise ValueError("key must start with ftkmf_modeltest_ and contain lowercase words separated by underscores")
    if key in existing_keys:
        raise ValueError(f"model key already exists in a current isolated catalog: {key}")
    if not display_name.strip() or display_name != display_name.strip() or any(ord(c) < 32 for c in display_name):
        raise ValueError("display name must be nonempty, trimmed, and contain no control characters")
    if display_name.casefold() in existing_displays:
        raise ValueError(f"display name already exists in a current isolated catalog: {display_name}")
    if not SLUG_PATTERN.fullmatch(asset_prefix):
        raise ValueError("asset prefix must contain lowercase words separated by hyphens")
    if any(name == asset_prefix or name.startswith(asset_prefix + "-") for name in existing_assets):
        raise ValueError(f"asset prefix collides with a current isolated catalog: {asset_prefix}")


def customize_profile(template: Json, key: str, display_name: str, asset_prefix: str) -> tuple[Json, Json, list[Json]]:
    profile_document = copy.deepcopy(template)
    profiles = profile_document.get("profiles")
    if not isinstance(profiles, list) or len(profiles) != 1 or not isinstance(profiles[0], dict):
        raise ValueError("profile template must contain exactly one profile")
    profile = profiles[0]
    profile["key"] = key
    profile["displayName"] = display_name
    renames: Json = {}
    assets_by_name: dict[str, Json] = {}
    counters = {"glbFile": 0, "textureFile": 0}

    def rename(field: str, old: Any, used_by: str) -> Any:
        if not isinstance(old, str):
            return old
        identity = f"{field}:{old}"
        if identity not in renames:
            counters[field] += 1
            suffix = "glb" if field == "glbFile" else "png"
            noun = "model" if field == "glbFile" else "texture"
            renames[identity] = f"{asset_prefix}-{noun}-{counters[field]:02d}.{suffix}"
        name = renames[identity]
        asset = assets_by_name.setdefault(name, {
            "file": name,
            "kind": "originalModel" if field == "glbFile" else "originalTexture",
            "usedBy": [],
        })
        asset["usedBy"].append(used_by)
        return name

    for collection_name in ("renderers", "apparel"):
        for index, assignment in enumerate(profile.get(collection_name, [])):
            if not isinstance(assignment, dict):
                raise ValueError(f"profile {collection_name} assignment is not an object")
            path = assignment.get("rendererPath")
            for field in ("glbFile", "textureFile"):
                if field in assignment:
                    assignment[field] = rename(field, assignment[field], f"{collection_name}/{index}/{path}/{field}")
            for slot_index, slot in enumerate(assignment.get("materialSlots", [])):
                if not isinstance(slot, dict):
                    raise ValueError("material slot is not an object")
                if "textureFile" in slot:
                    slot["textureFile"] = rename(
                        "textureFile", slot["textureFile"],
                        f"{collection_name}/{index}/{path}/materialSlots/{slot_index}/textureFile",
                    )
    assets = [assets_by_name[name] for name in sorted(assets_by_name)]
    return profile_document, renames, assets


def replace_tokens(value: Any, replacements: dict[str, str]) -> Any:
    if isinstance(value, str):
        for old, new in replacements.items():
            value = value.replace(old, new)
        return value
    if isinstance(value, list):
        return [replace_tokens(item, replacements) for item in value]
    if isinstance(value, dict):
        return {key: replace_tokens(item, replacements) for key, item in value.items()}
    return value


def package_destination(root: Path, slug: str) -> Path:
    if not SLUG_PATTERN.fullmatch(slug):
        raise ValueError("package slug must contain lowercase words separated by hyphens")
    destination = (root / "art-experiments" / slug).resolve(strict=False)
    if destination.parent != (root / "art-experiments").resolve():
        raise ValueError("package destination must be a direct child of art-experiments/")
    if destination.exists() or destination.is_symlink():
        raise ValueError(f"refusing to overwrite existing package destination: {destination}")
    return destination


def command_text(arguments: list[str]) -> str:
    return shlex.join(arguments)


def build_package(
    root: Path,
    catalog_path: Path,
    catalog: Json,
    selected: Json,
    destination: Path,
    slug: str,
    key: str,
    display_name: str,
    asset_prefix: str,
) -> tuple[Json, Json, str]:
    existing_keys, existing_displays, existing_assets = current_identities(root, catalog)
    validate_identity(key, display_name, asset_prefix, existing_keys, existing_displays, existing_assets)
    profile, renames, expected_assets = customize_profile(
        selected["integration"]["profileTemplate"], key, display_name, asset_prefix
    )
    collisions = sorted({row["file"] for row in expected_assets} & existing_assets)
    if collisions:
        raise ValueError(f"generated asset names already exist in a current isolated catalog: {collisions}")
    package_rel = relative(root, destination)
    replacements = {"PACKAGE_DIR": package_rel, "NEW_LABEL": slug}
    commands = replace_tokens(selected["integration"]["commands"], replacements)
    targets = replace_tokens(target_rows(selected), {"NEW_LABEL": slug})
    profile_text = json.dumps(profile, indent=2, allow_nan=False) + "\n"
    plan = {
        "schemaVersion": 1,
        "scope": (
            "Original-asset package scaffold for one exact FTK route. No listed asset is created by this plan, "
            "and no runtime, motion, gameplay, appearance, or art acceptance is claimed."
        ),
        "sourceCatalog": {"path": relative(root, catalog_path), "sha256": sha256(catalog_path)},
        "route": copy.deepcopy(selected["route"]),
        "package": {
            "slug": slug,
            "directory": package_rel,
            "key": key,
            "displayName": display_name,
            "assetPrefix": asset_prefix,
            "runtimeProfile": {"path": f"{package_rel}/runtime-profile.json", "sha256": sha256_text(profile_text)},
        },
        "expectedOriginalAssets": expected_assets,
        "templateAssetRenames": renames,
        "copiedOptionalSettingsRequiringReview": selected["integration"].get(
            "copiedOptionalSettingsRequiringReview", []
        ),
        "commands": commands,
        "authoringTargets": {
            "primaryCount": len(selected.get("targets", [])),
            "companionCount": len(selected["integration"].get("companionRigTargets", [])),
            "apparelCount": len(selected["integration"].get("apparelRigTargets", [])),
            "rows": targets,
        },
        "acceptanceStillRequired": [
            "Create every expected GLB and PNG from original authored work.",
            "Run the exact route preflight and isolated staging commands.",
            "Verify live binding, appearance, applicable motion, gameplay, and lifecycle behavior.",
            "Complete manual visual review and immutable evidence archive validation.",
        ],
    }
    lines = [
        f"# {display_name}",
        "",
        "This directory is an original-asset authoring scaffold. It contains no model or texture assets and carries no live or visual acceptance.",
        "",
        "## Exact route",
        "",
        f"- Topology group: `{selected['route']['topologyGroup']}`",
        f"- Route kind: `{selected['route']['routeKind']}`",
        f"- Model key: `{key}`",
        f"- Profile: `runtime-profile.json`",
        "",
        "## Required original assets",
        "",
    ]
    lines.extend(f"- `{row['file']}` ({row['kind']})" for row in expected_assets)
    lines.extend(["", "## Rig targets", "", "| Role | Renderer path | Source ID | Joints | Topology |", "|---|---|---:|---:|---|"])
    primary_ids = {row.get("sourceRendererId") for row in selected.get("targets", [])}
    apparel_ids = {
        row.get("sourceRendererId") for row in selected["integration"].get("apparelRigTargets", [])
    }
    for row in targets:
        role = (
            "primary" if row.get("sourceRendererId") in primary_ids
            else "apparel" if row.get("sourceRendererId") in apparel_ids
            else "companion"
        )
        lines.append(
            f"| {role} | `{row.get('rendererPath')}` | {row.get('sourceRendererId')} | "
            f"{row.get('jointCount')} | `{row.get('topologyGroup')}` |"
        )
    lines.extend(["", "Use each target's commands in `authoring-plan.json` to create an ignored Blender workspace. Replace calibration geometry with original art. Do not copy native reference geometry into this package."])
    optional = plan["copiedOptionalSettingsRequiringReview"]
    if optional:
        lines.extend(["", "## Copied settings requiring review", ""])
        lines.extend(f"- `{value}`" for value in optional)
    lines.extend([
        "", "## Preflight", "", "Create every expected asset first, then run:", "",
        "```sh", command_text(commands["preflightCommand"]), "```",
        "", "## Isolated staging", "", "After preflight passes, run:", "",
        "```sh", command_text(commands["stageCommand"]), "```",
        "", "Live validation, manual visual review, and immutable archive verification remain required before this route can receive acceptance credit.", "",
    ])
    return profile, plan, "\n".join(lines)


def write_package(destination: Path, profile: Json, plan: Json, readme: str) -> None:
    if destination.exists() or destination.is_symlink():
        raise ValueError(f"refusing to overwrite existing package destination: {destination}")
    destination.mkdir(parents=False)
    (destination / "runtime-profile.json").write_text(json.dumps(profile, indent=2, allow_nan=False) + "\n")
    (destination / "authoring-plan.json").write_text(json.dumps(plan, indent=2, allow_nan=False) + "\n")
    (destination / "README.md").write_text(readme)


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--topology-group", required=True)
    parser.add_argument("--route-kind", choices=sorted(ROUTE_KINDS), required=True)
    parser.add_argument("--slug", required=True)
    parser.add_argument("--key", required=True)
    parser.add_argument("--display-name", required=True)
    parser.add_argument("--asset-prefix", required=True)
    args = parser.parse_args()
    try:
        root = find_root(args.root)
        raw_catalog = args.catalog if args.catalog.is_absolute() else root / args.catalog
        catalog_path = raw_catalog.resolve(strict=False)
        catalog = validate_catalog(root, catalog_path)
        selected = select_route(catalog, args.topology_group, args.route_kind)
        destination = package_destination(root, args.slug)
        profile, plan, readme = build_package(
            root, catalog_path, catalog, selected, destination,
            args.slug, args.key, args.display_name, args.asset_prefix,
        )
        write_package(destination, profile, plan, readme)
    except (ValueError, OSError, json.JSONDecodeError) as error:
        parser.error(str(error))
    print(json.dumps({
        "status": "MODEL_PACKAGE_SCAFFOLDED",
        "output": relative(root, destination),
        "topologyGroup": args.topology_group,
        "routeKind": args.route_kind,
        "expectedOriginalAssets": len(plan["expectedOriginalAssets"]),
        "rigTargets": len(plan["authoringTargets"]["rows"]),
    }, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
