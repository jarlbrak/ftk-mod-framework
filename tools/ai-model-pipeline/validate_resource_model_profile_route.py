#!/usr/bin/env python3
"""Read-only exact-route preflight for resource-prefab custom model profiles.

ResourceManager prefab overrides have a source identity distinct from a direct
serialized enemy row. This checker proves that an authored profile names the
exact preflighted resource, base chassis, renderer path, controller/rig
fingerprint, and ordinary declared assets. It does not stage, deploy, load a
GLB, or establish runtime binding, appearance, animation, gameplay, or art.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any

import jsonschema

from prepare_resource_profiles import resource_combat_profile


Json = dict[str, Any]


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path.resolve())


def read_object(path: Path) -> Json:
    value = json.loads(path.read_text())
    if not isinstance(value, dict):
        raise ValueError(f"expected JSON object: {path}")
    return value


def declared_assets(profile: Json, asset_dir: Path | None) -> list[Json]:
    if asset_dir is None:
        return []
    results: list[Json] = []
    for assignment in profile["renderers"]:
        for field in ("glbFile", "textureFile"):
            name = assignment.get(field)
            if name is None:
                continue
            if not isinstance(name, str) or not name or Path(name).name != name:
                raise ValueError(f"unsafe declared {field}: {name!r}")
            path = asset_dir / name
            if not path.is_file() or path.is_symlink():
                raise ValueError(f"missing ordinary declared asset: {path}")
            results.append({"file": name, "sha256": sha256(path), "bytes": path.stat().st_size})
    names = [row["file"] for row in results]
    if len(names) != len(set(names)):
        raise ValueError("a resource profile declares the same asset more than once")
    return sorted(results, key=lambda row: row["file"])


def validated_rows(preflight: Json) -> dict[str, Json]:
    rows = preflight.get("rows")
    if not isinstance(rows, list) or not rows:
        raise ValueError("resource preflight has no rows")
    result: dict[str, Json] = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("resource preflight row must be an object")
        path = row.get("resource_load_path")
        if not isinstance(path, str) or not re.fullmatch(r"[A-Za-z0-9_-]{1,120}", path):
            raise ValueError("resource preflight has an invalid load path")
        if path in result:
            raise ValueError(f"resource preflight has a duplicate load path: {path}")
        base_enemy = row.get("base_enemy_id")
        renderer_path = row.get("renderer_path")
        renderer_id = row.get("renderer_id")
        rig = row.get("rig_profile_fingerprint")
        cel_ids = row.get("cel_ids")
        root_components = row.get("root_components")
        if (not isinstance(base_enemy, str) or not base_enemy or
                not isinstance(renderer_path, str) or not renderer_path or
                not isinstance(renderer_id, int) or renderer_id <= 0 or
                not isinstance(rig, str) or not re.fullmatch(r"[a-f0-9]{64}", rig) or
                not isinstance(cel_ids, list) or len(cel_ids) != 1 or
                not isinstance(root_components, list)):
            raise ValueError(f"resource preflight has incomplete route identity: {path}")
        resource_combat_profile(row)
        result[path] = row
    return result


def validate_selected_row(row: Json) -> None:
    path = row["resource_load_path"]
    if not row.get("has_Root_M") or not row.get("weapon_holder_present"):
        raise ValueError(f"resource preflight lacks required root or weapon holder: {path}")
    if not {"Animator", "CharacterEventListener"}.issubset(set(row["root_components"])):
        raise ValueError(f"resource preflight lacks required root components: {path}")
    if row.get("new_missing_vs_base") != []:
        raise ValueError(f"resource preflight has new controller binding gaps: {path}")


def validate_document(preflight: Json, document: Json, asset_dir: Path | None = None) -> Json:
    available = validated_rows(preflight)
    profiles = document.get("profiles")
    if not isinstance(profiles, list) or not profiles:
        raise ValueError("profile document has no profiles")
    results: list[Json] = []
    for profile in profiles:
        if not isinstance(profile, dict):
            raise ValueError("profile must be an object")
        resource = profile.get("resourcePrefab")
        if not isinstance(resource, str) or not resource:
            raise ValueError("direct-enemy profile needs the separate direct route preflight")
        if resource not in available:
            raise ValueError(f"unknown preflighted resource prefab: {resource!r}")
        row = available[resource]
        validate_selected_row(row)
        if profile.get("baseEnemy") != row["base_enemy_id"]:
            raise ValueError(
                f"base enemy mismatch for {resource}: declared {profile.get('baseEnemy')!r}, "
                f"expected {row['base_enemy_id']!r}")
        assignments = profile.get("renderers")
        if not isinstance(assignments, list) or len(assignments) != 1:
            raise ValueError(f"resource profile needs exactly one preflighted renderer: {profile.get('key')!r}")
        assignment = assignments[0]
        if not isinstance(assignment, dict) or assignment.get("rendererPath") != row["renderer_path"]:
            raise ValueError(
                f"renderer path mismatch for {resource}: declared "
                f"{assignment.get('rendererPath') if isinstance(assignment, dict) else None!r}, "
                f"expected {row['renderer_path']!r}")
        expected_combat = resource_combat_profile(row)
        if profile.get("combatProfile") != expected_combat:
            raise ValueError(
                f"combat profile mismatch for {profile.get('key')!r}: "
                f"declared {profile.get('combatProfile')!r}, expected {expected_combat!r}")
        results.append({
            "key": profile.get("key"),
            "baseEnemy": row["base_enemy_id"],
            "resourcePrefab": resource,
            "gameObjectId": row["game_object_id"],
            "celIds": row["cel_ids"],
            "renderer": {
                "rendererPath": row["renderer_path"],
                "sourceRendererId": row["renderer_id"],
                "rigProfile": row["rig_profile_fingerprint"],
            },
            "controller": row["weapon_controller"],
            "combatProfile": expected_combat,
            "declaredAssets": declared_assets(profile, asset_dir),
        })
    keys = [row["key"] for row in results]
    if len(keys) != len(set(keys)):
        raise ValueError("duplicate profile key in preflight document")
    return {
        "status": "PASS_EXACT_RESOURCE_PREFAB_ROUTE_STATIC_PREFLIGHT",
        "scope": "Read-only resource-prefab route/profile/asset preflight. No stage, deployment, runtime, visual, motion, or gameplay claim.",
        "profiles": results,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--preflight", type=Path, default=Path("scratch/resource-enemy-base-preflight.json"))
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--asset-dir", type=Path)
    parser.add_argument("--output", type=Path, help="Write a new immutable preflight record.")
    args = parser.parse_args()
    root = args.root.resolve()
    preflight_path = ((root / args.preflight).resolve()
                      if not args.preflight.is_absolute() else args.preflight.resolve())
    profile_path = ((root / args.profile).resolve()
                    if not args.profile.is_absolute() else args.profile.resolve())
    asset_dir = None if args.asset_dir is None else ((root / args.asset_dir).resolve()
                                                      if not args.asset_dir.is_absolute() else args.asset_dir.resolve())
    schema_path = root / "tools/ai-model-pipeline/runtime-test-content/profiles.schema.json"
    preflight = read_object(preflight_path)
    document = read_object(profile_path)
    jsonschema.validate(document, read_object(schema_path))
    report = validate_document(preflight, document, asset_dir)
    report["resourcePreflight"] = {
        "path": relative(root, preflight_path),
        "sha256": sha256(preflight_path),
    }
    report["profileDocument"] = {"path": relative(root, profile_path), "sha256": sha256(profile_path)}
    if asset_dir is not None:
        report["assetDirectory"] = relative(root, asset_dir)
    encoded = json.dumps(report, indent=2) + "\n"
    if args.output is not None:
        output = (root / args.output).resolve() if not args.output.is_absolute() else args.output.resolve()
        if output.exists():
            raise SystemExit(f"refusing to overwrite route preflight: {output}")
        output.parent.mkdir(parents=True, exist_ok=True)
        output.write_text(encoded)
        print(output)
    print(encoded, end="")


if __name__ == "__main__":
    main()
