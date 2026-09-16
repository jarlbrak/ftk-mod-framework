#!/usr/bin/env python3
"""Prove Cairnfire Troll authoring consumes Troll B binding metadata only."""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shutil
import sys


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())
PACKAGE = ROOT / "art-experiments" / "cairnfire-troll"
sys.path.insert(0, str(PACKAGE))
import build_geometry


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


class BindingOnly:
    def __init__(self, reference):
        self.reference = reference

    def __getitem__(self, key):
        if key not in {"bone_names", "bindposes"}:
            raise AssertionError("Generator attempted native surface access: " + key)
        return self.reference[key]


def main() -> None:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--output-dir", type=Path, default=ROOT / "scratch" / "cairnfire-troll-original-proof")
    args = parser.parse_args()
    target = args.output_dir.resolve()
    assert not target.exists(), "Use a fresh ignored proof directory; prior proof is evidence."
    original_load = build_geometry.np.load
    try:
        build_geometry.np.load = lambda *args, **kwargs: BindingOnly(original_load(*args, **kwargs))
        build_geometry.build(ROOT / "scratch" / "trollb-reference" / "reference.npz", target, write_runtime=False)
    finally:
        build_geometry.np.load = original_load

    files = ["cairnfire-trollb.source.json", "cairnfire-trollb.png", "cairnfire-trollb.pieces.json"]
    records = []
    for name in files:
        authored = PACKAGE / name
        rebuilt = target / name
        assert sha(authored) == sha(rebuilt), name
        records.append({"file": name, "sha256": sha(authored), "reproduced_identical": True})
    result = {
        "status": "PASS",
        "method": "Reran the deterministic original generator with NPZ reads limited to bone_names and bindposes. Runtime export is intentionally outside this source-provenance check.",
        "files": records,
        "generatorSha256": sha(PACKAGE / "build_geometry.py"),
        "referenceSha256": sha(ROOT / "scratch" / "trollb-reference" / "reference.npz"),
        "scope": "Original-provenance proof only. Binary validation, exact live binding, sampled motion, gameplay, and art review remain separate gates.",
    }
    (PACKAGE / "original-geometry-proof.json").write_text(json.dumps(result, indent=2) + "\n")
    print(json.dumps(result, indent=2))


if __name__ == "__main__":
    main()
