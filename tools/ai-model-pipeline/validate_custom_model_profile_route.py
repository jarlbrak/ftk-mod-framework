#!/usr/bin/env python3
"""Read-only exact-route preflight for direct-enemy custom model profiles.

Schema validation proves that a profile is shaped correctly. This companion
preflight proves that every declared renderer path and combat-profile fingerprint
still belongs to the exact serialized native enemy row in the selected local
mapping. It does not stage or deploy a game, load a GLB, or establish live
binding, appearance, animation, gameplay, or art quality.

Resource-prefab overrides have distinct source identity and intentionally use
their own preparation path; this direct-enemy preflight rejects them rather than
silently treating them as base-prefab rows.  A profile may also include a rigid
MeshRenderer child when a matching read-only static inventory proves its exact
one-filter, one-material direct-enemy route.  Its linked skinned assignment
continues to carry the native combat fingerprint.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import jsonschema

from prepare_runtime_profiles import combined, renderer_path


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


def validate_mapping_source(mapping: Json) -> str:
    source = Path(mapping.get("source_file", ""))
    expected = mapping.get("source_sha256")
    if not source.is_file() or not isinstance(expected, str) or len(expected) != 64:
        raise ValueError("mapping lacks a readable, pinned source asset")
    actual = sha256(source)
    if actual != expected:
        raise ValueError("mapping source asset hash has changed; regenerate the enemy mapping")
    return actual


def declared_assets(profile: Json, asset_dir: Path | None) -> list[Json]:
    if asset_dir is None:
        return []
    # Multipart models commonly use one authored palette across body, hair, and
    # apparel-like renderer assignments.  A deployment needs one copy of that
    # ordinary file, so report each declared basename once instead of rejecting
    # a valid shared texture (or intentionally shared GLB).
    results: dict[str, Json] = {}
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
            results[name] = {"file": name, "sha256": sha256(path), "bytes": path.stat().st_size}
    return [results[name] for name in sorted(results)]


def static_renderer_targets(static_inventory: Json | None, enemy: str) -> dict[str, Json]:
    """Return only exact, strict rigid-child routes for one direct enemy.

    The static inventory intentionally carries no compatible-rig inference.  A
    profile may use a MeshRenderer only when the inventory explicitly names the
    same direct enemy and path, and proves the narrow one-filter/one-material
    contract needed by the runtime static loader.
    """
    if static_inventory is None:
        return {}
    rows = static_inventory.get("renderers")
    if not isinstance(rows, list):
        raise ValueError("static renderer inventory has no renderer rows")
    result: dict[str, Json] = {}
    for row in rows:
        if not isinstance(row, dict) or enemy not in row.get("nativeEnemyCandidates", []):
            continue
        path = row.get("rendererPath")
        renderer_id = row.get("rendererId")
        if (not isinstance(path, str) or not path or not isinstance(renderer_id, int) or renderer_id <= 0):
            raise ValueError(f"static renderer inventory has incomplete route identity for {enemy!r}")
        if row.get("rendererKind") != "MeshRenderer":
            raise ValueError(f"static renderer inventory has wrong renderer kind for {path!r}")
        if path in result:
            raise ValueError(f"ambiguous static renderer path for {enemy}: {path}")
        result[path] = {
            "rendererPath": path,
            "sourceRendererId": renderer_id,
            "rendererKind": "MeshRenderer",
            "meshFilterCount": row.get("meshFilterCount"),
            "nativeMaterialSlotCount": row.get("nativeMaterialSlotCount"),
            "strictStaticEligible": row.get("strictStaticEligible"),
        }
    return result


def validate_static_inventory_source(mapping: Json, static_inventory: Json) -> None:
    """Require the static inventory to pin the same native source hash."""
    expected = mapping.get("source_sha256")
    recorded = static_inventory.get("mapping", {})
    recorded_source = recorded.get("sourceAssetSha256") if isinstance(recorded, dict) else None
    source = static_inventory.get("source", {})
    source_hash = source.get("sha256") if isinstance(source, dict) else None
    if not isinstance(expected, str) or len(expected) != 64:
        raise ValueError("mapping lacks a valid source asset hash")
    if recorded_source != expected or source_hash != expected:
        raise ValueError("static renderer inventory does not pin the exact mapping source asset")


def validate_document(mapping: Json, document: Json, asset_dir: Path | None = None,
                      static_inventory: Json | None = None) -> Json:
    rows = mapping.get("enemies")
    if not isinstance(rows, list):
        raise ValueError("mapping enemies must be a list")
    native: dict[str, Json] = {}
    for row in rows:
        enemy = row.get("enemy_id") if isinstance(row, dict) else None
        if not isinstance(enemy, str) or not enemy or enemy in native:
            raise ValueError("mapping has an invalid or duplicate enemy ID")
        native[enemy] = row
    profiles = document.get("profiles")
    if not isinstance(profiles, list) or not profiles:
        raise ValueError("profile document has no profiles")
    results: list[Json] = []
    for profile in profiles:
        if not isinstance(profile, dict):
            raise ValueError("profile must be an object")
        if profile.get("resourcePrefab") is not None:
            raise ValueError("resource-prefab profile needs the separate resource route preflight")
        enemy = profile.get("baseEnemy")
        if enemy not in native:
            raise ValueError(f"unknown direct native enemy: {enemy!r}")
        row = native[enemy]
        available: dict[tuple[str, str], Json] = {}
        for renderer in row.get("renderers", []):
            path = renderer_path(row, renderer)
            key = ("SkinnedMeshRenderer", path)
            if key in available:
                raise ValueError(f"ambiguous mapped renderer path for {enemy}: {path}")
            available[key] = {**renderer, "rendererKind": "SkinnedMeshRenderer"}
        for path, target in static_renderer_targets(static_inventory, enemy).items():
            key = ("MeshRenderer", path)
            if key in available:
                raise ValueError(f"ambiguous static renderer path for {enemy}: {path}")
            available[key] = target
        assignments = profile.get("renderers")
        if not isinstance(assignments, list) or not assignments:
            raise ValueError(f"profile has no renderer assignments: {profile.get('key')!r}")
        expected: list[Json] = []
        static_expected: list[Json] = []
        for assignment in assignments:
            if not isinstance(assignment, dict):
                raise ValueError("renderer assignment must be an object")
            path = assignment.get("rendererPath")
            kind = assignment.get("rendererKind", "SkinnedMeshRenderer")
            if kind not in ("SkinnedMeshRenderer", "MeshRenderer"):
                raise ValueError(f"unsupported renderer kind for {profile.get('key')!r}: {kind!r}")
            if kind == "MeshRenderer" and static_inventory is None:
                raise ValueError("static MeshRenderer assignment needs a pinned static renderer inventory")
            target = available.get((kind, path))
            if target is None:
                raise ValueError(f"unmapped renderer path for {enemy} ({kind}): {path!r}")
            if kind == "MeshRenderer":
                if target.get("strictStaticEligible") is not True:
                    raise ValueError(f"static renderer route is not strict-eligible: {enemy!r} / {path!r}")
                static_expected.append({
                    "rendererPath": path,
                    "sourceRendererId": target["sourceRendererId"],
                    "rendererKind": "MeshRenderer",
                    "meshFilterCount": target["meshFilterCount"],
                    "nativeMaterialSlotCount": target["nativeMaterialSlotCount"],
                })
                continue
            expected.append({
                "rendererPath": path,
                "sourceRendererId": target["renderer_path_id"],
                "rendererKind": "SkinnedMeshRenderer",
                "rigProfile": target["rig_profile_fingerprint"],
                "combatProfile": target["combat_profile_fingerprint"],
                "controllerIds": sorted({candidate["controller_path_id"]
                                         for candidate in target.get("animators", [])
                                         if isinstance(candidate.get("controller_path_id"), int)}),
            })
        if not expected:
            raise ValueError(
                f"static-only profile lacks the exact linked SkinnedMeshRenderer motion route: {profile.get('key')!r}")
        expected_profile = combined(expected)
        if profile.get("combatProfile") != expected_profile:
            raise ValueError(
                f"combat profile mismatch for {profile.get('key')!r}: "
                f"declared {profile.get('combatProfile')!r}, expected {expected_profile!r}"
            )
        results.append({
            "key": profile.get("key"),
            "baseEnemy": enemy,
            "prefab": row.get("prefab_name"),
            "controllerCandidates": [
                {"id": item.get("controller_path_id"), "name": item.get("controller_name")}
                for item in row.get("weapon_animation_controller_candidates", [])
            ],
            "renderers": sorted([*expected, *static_expected],
                                key=lambda item: (item["rendererPath"], item["rendererKind"])),
            "declaredAssets": declared_assets(profile, asset_dir),
        })
    keys = [row["key"] for row in results]
    if len(keys) != len(set(keys)):
        raise ValueError("duplicate profile key in preflight document")
    return {
        "status": "PASS_EXACT_DIRECT_ROUTE_STATIC_PREFLIGHT",
        "scope": "Read-only direct-enemy route/profile/asset preflight. A rigid MeshRenderer row is only a pinned one-filter/one-material identity prerequisite; no stage, deployment, runtime, visual, motion, or gameplay claim.",
        "profiles": results,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--mapping", type=Path, default=Path("scratch/enemy-rig-mapping-reproducible.json"))
    parser.add_argument("--static-inventory", type=Path,
                        help="Read-only exact direct-enemy MeshRenderer inventory for rigid child assignments.")
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--asset-dir", type=Path)
    parser.add_argument("--output", type=Path, help="Write a new immutable preflight record.")
    args = parser.parse_args()
    root = args.root.resolve()
    mapping_path = (root / args.mapping).resolve() if not args.mapping.is_absolute() else args.mapping.resolve()
    profile_path = (root / args.profile).resolve() if not args.profile.is_absolute() else args.profile.resolve()
    asset_dir = None if args.asset_dir is None else ((root / args.asset_dir).resolve()
                                                      if not args.asset_dir.is_absolute() else args.asset_dir.resolve())
    schema_path = root / "tools/ai-model-pipeline/runtime-test-content/profiles.schema.json"
    mapping = read_object(mapping_path)
    document = read_object(profile_path)
    jsonschema.validate(document, read_object(schema_path))
    source_hash = validate_mapping_source(mapping)
    static_inventory = None
    static_inventory_path = None
    if args.static_inventory is not None:
        static_inventory_path = ((root / args.static_inventory).resolve()
                                 if not args.static_inventory.is_absolute() else args.static_inventory.resolve())
        static_inventory = read_object(static_inventory_path)
        validate_static_inventory_source(mapping, static_inventory)
    report = validate_document(mapping, document, asset_dir, static_inventory)
    report["mapping"] = {"path": relative(root, mapping_path), "sha256": sha256(mapping_path),
                         "sourceAssetSha256": source_hash}
    report["profileDocument"] = {"path": relative(root, profile_path), "sha256": sha256(profile_path)}
    if asset_dir is not None:
        report["assetDirectory"] = relative(root, asset_dir)
    if static_inventory_path is not None:
        report["staticRendererInventory"] = {
            "path": relative(root, static_inventory_path),
            "sha256": sha256(static_inventory_path),
        }
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
