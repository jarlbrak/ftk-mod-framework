#!/usr/bin/env python3
"""Verify every local authoring reference in a generated route catalog.

The verifier decodes each exact renderer again from the catalog-pinned native
asset and compares every NPZ array plus the complete skeleton JSON. It is
read-only except for an optional new report directly under repository scratch/.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import numpy as np
import UnityPy

from extract_reference import decode_reference


Json = dict[str, Any]


def read_object(path: Path) -> Json:
    value = json.loads(path.read_text())
    if not isinstance(value, dict):
        raise ValueError(f"expected JSON object: {path}")
    return value


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


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


def reference_path(root: Path, value: str, label: str) -> Path:
    raw = Path(value)
    path = raw if raw.is_absolute() else root / raw
    path = path.resolve(strict=False)
    if not path.is_file() or path.is_symlink():
        raise ValueError(f"{label} must be a real nonsymlink file: {path}")
    if not path.is_relative_to(root / "scratch"):
        raise ValueError(f"{label} must remain under repository scratch/: {path}")
    return path


def catalog_source(root: Path, catalog: Json) -> tuple[Path, str]:
    source = catalog.get("inputs", {}).get("nativeSourceAsset", {})
    path_text = source.get("path") if isinstance(source, dict) else None
    expected = source.get("sha256") if isinstance(source, dict) else None
    if not isinstance(path_text, str) or not isinstance(expected, str):
        raise ValueError("authoring catalog lacks its exact native source asset")
    path = Path(path_text).resolve(strict=False)
    if not path.is_file() or path.is_symlink() or sha256(path) != expected:
        raise ValueError("authoring catalog native source asset is missing, linked, or changed")
    return path, expected


def unique_targets(catalog: Json) -> list[Json]:
    by_id: dict[int, Json] = {}
    for route in catalog.get("routes", []):
        if not isinstance(route, dict):
            raise ValueError("authoring catalog route is not an object")
        integration = route.get("integration", {})
        if not isinstance(integration, dict):
            raise ValueError("authoring catalog route integration is not an object")
        targets = [
            *route.get("targets", []),
            *integration.get("companionRigTargets", []),
            *integration.get("apparelRigTargets", []),
        ]
        for target in targets:
            if not isinstance(target, dict):
                raise ValueError("authoring catalog target is not an object")
            renderer_id = target.get("sourceRendererId")
            if not isinstance(renderer_id, int):
                raise ValueError("authoring target lacks an exact renderer ID")
            existing = by_id.get(renderer_id)
            if existing is not None:
                fields = ("meshName", "jointCount", "topologyFingerprint", "bindposeFingerprint", "rigProfileFingerprint")
                if any(existing.get(field) != target.get(field) for field in fields):
                    raise ValueError(f"renderer ID {renderer_id} has conflicting catalog identities")
            else:
                by_id[renderer_id] = target
    return [by_id[key] for key in sorted(by_id)]


def verify_catalog(root: Path, catalog_path: Path) -> Json:
    catalog_path = catalog_path.resolve()
    if not catalog_path.is_file() or catalog_path.is_symlink() or not catalog_path.is_relative_to(root / "scratch"):
        raise ValueError("authoring catalog must be a real file under repository scratch/")
    catalog = read_object(catalog_path)
    source, source_hash = catalog_source(root, catalog)
    targets = unique_targets(catalog)
    environment = UnityPy.load(str(source))
    results: list[Json] = []
    for target in targets:
        renderer_id = int(target["sourceRendererId"])
        local = target.get("existingLocalReference")
        failures: list[str] = []
        if not isinstance(local, dict):
            results.append({"sourceRendererId": renderer_id, "status": "FAIL", "failures": ["missing local reference"]})
            continue
        try:
            reference_info = local.get("reference", {})
            skeleton_info = local.get("skeleton", {})
            reference = reference_path(root, str(reference_info.get("path", "")), "reference NPZ")
            skeleton = reference_path(root, str(skeleton_info.get("path", "")), "skeleton JSON")
            if sha256(reference) != reference_info.get("sha256"):
                failures.append("reference hash differs from catalog")
            if sha256(skeleton) != skeleton_info.get("sha256"):
                failures.append("skeleton hash differs from catalog")
            expected_skeleton, expected_arrays = decode_reference(source, renderer_id, environment)
            actual_skeleton = read_object(skeleton)
            if actual_skeleton != expected_skeleton:
                failures.append("skeleton JSON differs from current native decode")
            with np.load(reference, allow_pickle=False) as actual:
                if set(actual.files) != set(expected_arrays):
                    failures.append("reference NPZ members differ from current extractor")
                else:
                    for name, expected in expected_arrays.items():
                        if not np.array_equal(actual[name], expected):
                            failures.append(f"reference array differs: {name}")
        except Exception as error:
            failures.append(f"{type(error).__name__}: {error}")
        results.append({
            "sourceRendererId": renderer_id,
            "rendererPath": target.get("rendererPath"),
            "rigProfileFingerprint": target.get("rigProfileFingerprint"),
            "status": "PASS" if not failures else "FAIL",
            "failures": failures,
        })
    failed = sum(result["status"] != "PASS" for result in results)
    return {
        "schemaVersion": 1,
        "scope": (
            "Byte-for-byte current-native verification of local authoring references. "
            "It does not establish original art, export, runtime, motion, gameplay, or visual acceptance."
        ),
        "catalog": {"path": relative(root, catalog_path), "sha256": sha256(catalog_path)},
        "nativeSourceAsset": {"path": str(source), "sha256": source_hash},
        "summary": {"rendererTargets": len(results), "verified": len(results) - failed, "failed": failed},
        "results": results,
    }


def output_path(root: Path, value: Path) -> Path:
    candidate = value if value.is_absolute() else root / value
    if candidate.is_symlink():
        raise ValueError("verification output must not be a symlink")
    output = candidate.resolve(strict=False)
    if output.parent != root / "scratch" or output.suffix.lower() != ".json":
        raise ValueError("verification output must be a .json file directly under repository scratch/")
    return output


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--catalog", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    args = parser.parse_args()
    try:
        root = find_root(args.root)
        raw_catalog = args.catalog if args.catalog.is_absolute() else root / args.catalog
        report = verify_catalog(root, raw_catalog)
    except (ValueError, OSError, json.JSONDecodeError) as error:
        parser.error(str(error))
    content = json.dumps(report, indent=2, allow_nan=False) + "\n"
    if args.output is None:
        print(content, end="")
    else:
        try:
            destination = output_path(root, args.output)
        except ValueError as error:
            parser.error(str(error))
        if destination.exists():
            parser.error(f"refusing to overwrite verification report: {destination}")
        destination.write_text(content)
        print(json.dumps({"status": "PASS" if report["summary"]["failed"] == 0 else "FAIL",
                          "output": relative(root, destination), **report["summary"]}, indent=2))
    return 0 if report["summary"]["failed"] == 0 else 1


if __name__ == "__main__":
    raise SystemExit(main())
