#!/usr/bin/env python3
"""Prove Tideglass authoring consumes only selected binding metadata."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import sys

ROOT = next(path for path in Path(__file__).resolve().parents
            if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())
PACKAGE = ROOT / "art-experiments" / "tideglass-fishsmith"
sys.path.insert(0, str(PACKAGE))
import build_geometry


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class BindingOnly:
    """Reject accidental native surface reads during authoring reproduction."""

    def __init__(self, reference) -> None:
        self.reference = reference

    def __getitem__(self, key):
        if key not in {"bone_names", "bindposes"}:
            raise AssertionError("Generator attempted native surface access: " + key)
        return self.reference[key]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path,
                        default=ROOT / "scratch" / "tideglass-fishsmith-original-proof")
    args = parser.parse_args()
    output = args.output_dir.resolve()
    if output.exists():
        raise FileExistsError("Use a fresh ignored proof directory; a prior proof is evidence.")
    original_load = build_geometry.np.load
    try:
        build_geometry.np.load = lambda *call_args, **call_kwargs: BindingOnly(original_load(*call_args, **call_kwargs))
        build_geometry.build(build_geometry.BODY_REFERENCE, build_geometry.HAIR_TOP_REFERENCE,
                             build_geometry.HAIR_BOTTOM_REFERENCE, output, write_runtime=False)
    finally:
        build_geometry.np.load = original_load
    names = ["tideglass-palette.png"]
    for asset in ("tideglass-body", "tideglass-hair-top", "tideglass-hair-bottom"):
        names.extend((f"{asset}.source.json", f"{asset}.pieces.json"))
    records = []
    for name in names:
        authored, rebuilt = PACKAGE / name, output / name
        if sha(authored) != sha(rebuilt):
            raise AssertionError(name)
        records.append({"file": name, "sha256": sha(authored), "reproduced_identical": True})
    result = {
        "status": "PASS",
        "method": "Reran the deterministic original generator while every NPZ reference was restricted to bone_names and bindposes. Runtime export is deliberately outside this source-provenance check.",
        "files": records,
        "generatorSha256": sha(PACKAGE / "build_geometry.py"),
        "references": {
            "body": sha(build_geometry.BODY_REFERENCE),
            "hairTop": sha(build_geometry.HAIR_TOP_REFERENCE),
            "hairBottom": sha(build_geometry.HAIR_BOTTOM_REFERENCE),
        },
        "scope": "Original-provenance proof only. Binary contract validation, exact player binding, preview, motion, equipment assembly, gameplay, and art approval remain separate gates.",
    }
    (PACKAGE / "original-geometry-proof.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
