#!/usr/bin/env python3
"""Prove the Abyssal Kraken generator consumes palette metadata only."""
from __future__ import annotations

import hashlib
import json
import sys
from pathlib import Path

import numpy as np

OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent
TARGET = ROOT / "scratch/abyssal-kraken-original-proof"
TARGET.mkdir(exist_ok=True)
sys.path.insert(0, str(ROOT / "tools/ai-model-pipeline"))
import export_ftk_glb
import validate_glb

original_load = np.load
original_write = export_ftk_glb.write_glb
original_validate = validate_glb.validate
access: list[str] = []


class BindingOnly:
    def __init__(self, reference):
        self.reference = reference

    def __getitem__(self, key):
        access.append(key)
        if key not in ("bone_names", "bindposes"):
            raise AssertionError("Generator attempted native surface access: " + key)
        return self.reference[key]


try:
    np.load = lambda *args, **kwargs: BindingOnly(original_load(*args, **kwargs))
    # The normal export and Blender bridge separately run the actual FTK writer
    # and validator. Stubbing them here isolates only authoring provenance.
    export_ftk_glb.write_glb = lambda *args, **kwargs: None
    validate_glb.validate = lambda *args, **kwargs: {}
    source = (OUT / "build_geometry.py").read_text().replace(
        "OUT = Path(__file__).resolve().parent\nROOT = OUT.parent.parent",
        f"OUT = Path({str(TARGET)!r})\nROOT = Path({str(ROOT)!r})",
    )
    namespace = {"__file__": str(OUT / "build_geometry.py"), "__name__": "__original_proof__"}
    exec(compile(source, str(OUT / "build_geometry.py"), "exec"), namespace)
    namespace["main"]()
finally:
    np.load = original_load
    export_ftk_glb.write_glb = original_write
    validate_glb.validate = original_validate

files = []
for name in (
    "abyssal-crown-kraken-head.source.json", "abyssal-crown-kraken-head.png", "abyssal-crown-kraken-head.pieces.json",
    "sargassum-kraken-tentacle.source.json", "sargassum-kraken-tentacle.png", "sargassum-kraken-tentacle.pieces.json",
    "sargassum-seaking-tentacle.source.json", "sargassum-seaking-tentacle.png", "sargassum-seaking-tentacle.pieces.json",
):
    authored = hashlib.sha256((OUT / name).read_bytes()).hexdigest()
    rebuilt = hashlib.sha256((TARGET / name).read_bytes()).hexdigest()
    assert authored == rebuilt, name
    files.append({"file": name, "sha256": authored, "reproduced_identical": True})
result = {
    "status": "PASS",
    "method": "Reran the exact generator with output redirected and NPZ reads limited to bone_names and bindposes. The writer and validator were stubbed only for the provenance run.",
    "bindingKeysAccessed": sorted(set(access)),
    "files": files,
    "generatorSha256": hashlib.sha256((OUT / "build_geometry.py").read_bytes()).hexdigest(),
    "scope": "Original-provenance proof only. Binary validation, Blender round trip, live binding, animation, gameplay and art review remain separate gates.",
}
(OUT / "original-geometry-proof.json").write_text(json.dumps(result, indent=2) + "\n")
print(result["status"])
