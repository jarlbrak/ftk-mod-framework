#!/usr/bin/env python3
"""Read-only exact-route preflight for player skinset custom model profiles.

The player route is distinct from direct-enemy rows and ResourceManager prefab
overrides.  This checker proves that every required body/hair assignment names
the complete, exact renderer set of one serialized native skinset avatar.  It
also pins the source assets, authored profile, and declared runtime files.

Conditional apparel is schema- and asset-checked here, but its native equipment
branch remains a separate live check.  This tool never stages or deploys a game,
loads a GLB, or establishes class registration, preview, combat, lifetime,
appearance, animation, gameplay, or art acceptance.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import re
from pathlib import Path
from typing import Any

import jsonschema


Json = dict[str, Any]
HEX = re.compile(r"[a-f0-9]{64}")


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


def classification_path(root: Path, value: Any, label: str) -> Path:
    if not isinstance(value, str) or not value:
        raise ValueError(f"classification lacks {label}")
    path = Path(value)
    return path if path.is_absolute() else root / path


def validate_classification_sources(classification: Json, root: Path) -> tuple[Path, str, Path, str]:
    source = classification_path(root, classification.get("source_file"), "a readable source asset")
    source_expected = classification.get("source_sha256")
    skinset_database = classification_path(
        root, classification.get("skinset_database_file"), "a readable skinset database")
    skinset_expected = classification.get("skinset_database_sha256")
    for path, expected, label in (
        (source, source_expected, "classification source asset"),
        (skinset_database, skinset_expected, "skinset database"),
    ):
        if not path.is_file() or not isinstance(expected, str) or not HEX.fullmatch(expected):
            raise ValueError(f"classification lacks a readable, pinned {label}")
        if sha256(path) != expected:
            raise ValueError(f"{label} hash has changed; regenerate the rig classification")
    return source, source_expected, skinset_database, skinset_expected


def avatar_rows(classification: Json) -> dict[str, Json]:
    rows = classification.get("skinset_rows")
    if not isinstance(rows, list) or not rows:
        raise ValueError("classification has no skinset rows")
    result: dict[str, Json] = {}
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("classification skinset row must be an object")
        skinset = row.get("skinset_id")
        avatar = row.get("avatar_prefab_name")
        cel = row.get("avatar_cel_path_id")
        if (not isinstance(skinset, str) or not skinset or skinset in result or
                not isinstance(avatar, str) or not avatar or
                not isinstance(cel, int) or cel <= 0):
            raise ValueError("classification has an invalid or duplicate skinset avatar row")
        result[skinset] = row
    return result


def native_renderer_path(row: Json, avatar: Json, skinset: str) -> str:
    if row.get("positive_role") != "player_skinset_avatar":
        raise ValueError(f"non-player renderer selected for skinset {skinset}")
    references = row.get("skinset_avatar_references")
    if not isinstance(references, list) or skinset not in references:
        raise ValueError(f"renderer lacks exact skinset reference: {skinset}")
    ancestors = row.get("ancestor_names")
    if not isinstance(ancestors, list) or len(ancestors) < 2 or not all(isinstance(item, str) and item for item in ancestors):
        raise ValueError(f"renderer has no usable avatar ancestry: {skinset}")
    if ancestors[-1] != avatar["avatar_prefab_name"]:
        raise ValueError(f"renderer ancestry does not end at the exact avatar prefab: {skinset}")
    evidence = row.get("ancestor_component_evidence")
    if not isinstance(evidence, list) or not any(
        isinstance(item, dict)
        and item.get("component_class") == "CharacterEventListener"
        and item.get("component_path_id") == avatar["avatar_cel_path_id"]
        for item in evidence
    ):
        raise ValueError(f"renderer lacks exact native CharacterEventListener ancestry: {skinset}")
    parts = list(reversed(ancestors[:-1]))
    if any(part in ("", ".", "..") or "/" in part or "\\" in part for part in parts):
        raise ValueError(f"renderer ancestry cannot be represented as a player renderer path: {skinset}")
    return "/".join(parts) or "."


def expected_renderers(classification: Json, avatar: Json, skinset: str) -> list[Json]:
    rows = classification.get("renderers")
    if not isinstance(rows, list):
        raise ValueError("classification renderers must be a list")
    expected: list[Json] = []
    for row in rows:
        if not isinstance(row, dict):
            raise ValueError("classification renderer must be an object")
        if skinset not in row.get("skinset_avatar_references", []):
            continue
        path = native_renderer_path(row, avatar, skinset)
        renderer_id = row.get("renderer_path_id")
        name = row.get("renderer_name")
        rig = row.get("rig_profile_fingerprint")
        joints = row.get("joint_count")
        if (not isinstance(renderer_id, int) or renderer_id <= 0 or
                not isinstance(name, str) or not name or
                not isinstance(rig, str) or not HEX.fullmatch(rig) or
                not isinstance(joints, int) or joints <= 0):
            raise ValueError(f"classification has incomplete native renderer identity: {skinset}")
        expected.append({
            "rendererPath": path,
            "sourceRendererId": renderer_id,
            "rendererName": name,
            "rigProfile": rig,
            "jointCount": joints,
        })
    if not expected:
        raise ValueError(f"skinset has no classified native avatar renderers: {skinset}")
    paths = [row["rendererPath"] for row in expected]
    ids = [row["sourceRendererId"] for row in expected]
    if len(paths) != len(set(paths)) or len(ids) != len(set(ids)):
        raise ValueError(f"skinset has ambiguous native avatar renderer paths: {skinset}")
    return sorted(expected, key=lambda row: row["rendererPath"])


def declared_assets(profile: Json, asset_dir: Path | None) -> list[Json]:
    if asset_dir is None:
        return []
    assignments = [*profile.get("renderers", []), *profile.get("apparel", [])]
    result: dict[str, Json] = {}
    for assignment in assignments:
        if not isinstance(assignment, dict):
            raise ValueError("player assignment must be an object")
        for field, extension in (("glbFile", ".glb"), ("textureFile", ".png")):
            name = assignment.get(field)
            if name is None:
                continue
            if (not isinstance(name, str) or not name.endswith(extension) or
                    Path(name).name != name):
                raise ValueError(f"unsafe declared {field}: {name!r}")
            if name in result:
                continue
            path = asset_dir / name
            if not path.is_file() or path.is_symlink():
                raise ValueError(f"missing ordinary declared asset: {path}")
            result[name] = {"file": name, "sha256": sha256(path), "bytes": path.stat().st_size}
    return [result[name] for name in sorted(result)]


def validate_apparel(profile: Json, body_paths: set[str]) -> list[Json]:
    apparel = profile.get("apparel", [])
    if not isinstance(apparel, list):
        raise ValueError(f"apparel must be a list: {profile.get('key')!r}")
    result: list[Json] = []
    paths: set[str] = set()
    for assignment in apparel:
        if not isinstance(assignment, dict):
            raise ValueError("apparel assignment must be an object")
        path = assignment.get("rendererPath")
        expected_mesh = assignment.get("expectedNativeMeshName")
        if (not isinstance(path, str) or not path or path in paths or path in body_paths or
                not isinstance(expected_mesh, str) or not expected_mesh.strip()):
            raise ValueError(f"invalid or overlapping conditional apparel assignment: {profile.get('key')!r}")
        paths.add(path)
        result.append({
            "rendererPath": path,
            "expectedNativeMeshName": expected_mesh,
            "glbFile": assignment.get("glbFile"),
            "textureFile": assignment.get("textureFile"),
            "validation": "schema-and-asset-only; exact native equipment branch remains a live check",
        })
    return sorted(result, key=lambda row: row["rendererPath"])


def validate_document(classification: Json, document: Json, asset_dir: Path | None = None) -> Json:
    skins = avatar_rows(classification)
    profiles = document.get("profiles")
    if not isinstance(profiles, list) or not profiles:
        raise ValueError("player profile document has no profiles")
    results: list[Json] = []
    for profile in profiles:
        if not isinstance(profile, dict):
            raise ValueError("player profile must be an object")
        skinset = profile.get("skinset")
        if skinset not in skins:
            raise ValueError(f"unknown classified player skinset: {skinset!r}")
        avatar = skins[skinset]
        expected = expected_renderers(classification, avatar, skinset)
        assignments = profile.get("renderers")
        if not isinstance(assignments, list) or not assignments:
            raise ValueError(f"player profile has no renderer assignments: {profile.get('key')!r}")
        declared: dict[str, Json] = {}
        for assignment in assignments:
            if not isinstance(assignment, dict):
                raise ValueError("player renderer assignment must be an object")
            path = assignment.get("rendererPath")
            if not isinstance(path, str) or not path or path in declared:
                raise ValueError(f"duplicate or invalid player renderer path: {profile.get('key')!r}")
            declared[path] = assignment
        expected_paths = {row["rendererPath"] for row in expected}
        declared_paths = set(declared)
        if declared_paths != expected_paths:
            missing = sorted(expected_paths - declared_paths)
            unexpected = sorted(declared_paths - expected_paths)
            raise ValueError(
                f"player renderer paths do not exactly match native skinset {skinset}: "
                f"missing={missing}, unexpected={unexpected}")
        renderers = []
        for native in expected:
            assignment = declared[native["rendererPath"]]
            renderers.append({
                **native,
                "glbFile": assignment.get("glbFile"),
                "textureFile": assignment.get("textureFile"),
            })
        results.append({
            "key": profile.get("key"),
            "baseClass": profile.get("baseClass"),
            "defaultSkinType": profile.get("defaultSkinType"),
            "skinset": skinset,
            "avatar": {
                "prefab": avatar["avatar_prefab_name"],
                "celPathId": avatar["avatar_cel_path_id"],
                "assetFile": avatar.get("avatar_asset_file"),
            },
            "renderers": renderers,
            "apparel": validate_apparel(profile, expected_paths),
            "declaredAssets": declared_assets(profile, asset_dir),
        })
    keys = [row["key"] for row in results]
    if len(keys) != len(set(keys)):
        raise ValueError("duplicate player profile key in preflight document")
    return {
        "status": "PASS_EXACT_PLAYER_SKINSET_ROUTE_STATIC_PREFLIGHT",
        "scope": "Read-only player skinset/avatar route/profile/asset preflight. Class registration, native equipment branches, preview, runtime, visual, motion, and gameplay remain separate checks.",
        "profiles": results,
    }


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--classification", type=Path, default=Path("scratch/rig-candidate-classification.json"))
    parser.add_argument("--profile", type=Path, required=True)
    parser.add_argument("--asset-dir", type=Path)
    parser.add_argument("--output", type=Path, help="Write a new immutable preflight record.")
    args = parser.parse_args()
    root = args.root.resolve()
    classification_path_value = ((root / args.classification).resolve()
                                 if not args.classification.is_absolute() else args.classification.resolve())
    profile_path = ((root / args.profile).resolve()
                    if not args.profile.is_absolute() else args.profile.resolve())
    asset_dir = None if args.asset_dir is None else ((root / args.asset_dir).resolve()
                                                      if not args.asset_dir.is_absolute() else args.asset_dir.resolve())
    schema_path = root / "tools/ai-model-pipeline/runtime-test-content/player-profiles.schema.json"
    classification = read_object(classification_path_value)
    document = read_object(profile_path)
    jsonschema.validate(document, read_object(schema_path))
    source, source_hash, skinset_database, skinset_hash = validate_classification_sources(classification, root)
    report = validate_document(classification, document, asset_dir)
    report["rigClassification"] = {
        "path": relative(root, classification_path_value),
        "sha256": sha256(classification_path_value),
        "sourceAssetSha256": source_hash,
        "skinsetDatabase": {"path": relative(root, skinset_database), "sha256": skinset_hash},
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
