#!/usr/bin/env python3
"""Prove Duneshade's authoring generator reads binding metadata only."""
import hashlib
import json
import sys
from pathlib import Path

import numpy as np


OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent
TARGET = ROOT / "scratch/duneshade-desert-asp-original-proof"
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
    # This provenance proof reruns only source generation. The normal build and
    # Blender bridge independently exercise the real writer and validator.
    export_ftk_glb.write_glb = lambda *args, **kwargs: None
    validate_glb.validate = lambda *args, **kwargs: {}
    code = (OUT / "build_geometry.py").read_text().replace(
        "OUT = Path(__file__).resolve().parent\nROOT = OUT.parent.parent",
        f"OUT = Path({str(TARGET)!r})\nROOT = Path({str(ROOT)!r})",
    )
    exec(
        compile(code, str(OUT / "build_geometry.py"), "exec"),
        {"__file__": str(OUT / "build_geometry.py"), "__name__": "__original_proof__"},
    )
finally:
    np.load = original_load
    export_ftk_glb.write_glb = original_write
    validate_glb.validate = original_validate


files = []
for name in (
    "duneshade-desert-asp.source.json",
    "duneshade-desert-asp_basecolor.png",
    "duneshade-desert-asp.pieces.json",
):
    authored = hashlib.sha256((OUT / name).read_bytes()).hexdigest()
    regenerated = hashlib.sha256((TARGET / name).read_bytes()).hexdigest()
    assert authored == regenerated, name
    files.append({"file": name, "sha256": authored, "reproduced_identical": True})
result = {
    "status": "PASS",
    "method": "Reran the exact authoring generator with output redirected and native NPZ reads limited to bone_names and bindposes. Writer and validator were stubbed only for this source-generation proof.",
    "binding_keys_accessed": sorted(set(access)),
    "files": files,
    "generator_sha256": hashlib.sha256((OUT / "build_geometry.py").read_bytes()).hexdigest(),
    "scope": "Original-provenance proof only. Real FTK GLB validation, Blender round trip, native animation, portrait, and live art remain separate gates.",
}
(OUT / "original-geometry-proof.json").write_text(json.dumps(result, indent=2) + "\n")
print(result["status"])
