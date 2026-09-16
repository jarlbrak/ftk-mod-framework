"""Refresh Verdigrin mechanical hashes without erasing recorded live scope."""
from __future__ import annotations

import hashlib
import json
from pathlib import Path

import numpy as np


OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def evidence(path: Path) -> dict:
    relative = path.relative_to(OUT) if path.is_relative_to(OUT) else path.relative_to(ROOT)
    return {
        "path": str(relative),
        "sha256": sha(path),
        "bytes": path.stat().st_size,
    }


reference_path = ROOT / "scratch" / "skeleton-audit" / "121192" / "reference.npz"
reference = np.load(reference_path, allow_pickle=False)
source = json.loads((OUT / "verdigrin.source.json").read_text())
positions = np.asarray(source["positions"])
assert (positions.min(0) >= reference["positions"].min(0)).all()
assert (positions.max(0) <= reference["positions"].max(0)).all()

validation = json.loads((OUT / "verdigrin.validation.json").read_text())
roundtrip = json.loads((ROOT / "scratch" / "verdigrin-roundtrip" / "verdigrin.validation.json").read_text())
assert validation["status"].startswith("PASS")
assert roundtrip["status"].startswith("PASS")

v2 = json.loads((OUT / "live-validation-v2" / "validation.json").read_text())
v3 = json.loads((OUT / "live-validation-v3" / "validation.json").read_text())
v4 = json.loads((OUT / "live-validation-v4" / "validation.json").read_text())
assert v4["nativeChassis"] == "mimicA"
assert v4["sourceRendererIds"] == [121192]
assert v4["ordinaryHit"]["beforeHp"] == 58 and v4["ordinaryHit"]["afterHp"] == 52
assert v4["explicitKillFixture"]["method"] == "KillSingle"
assert v4["finalReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}

manifest = {
    "name": "Verdigrin Coffer",
    "status": "ORIGINAL_MIMIC_OFFLINE_PASS_LIVE_V4_CANONICAL_SCOPED_VALIDATED",
    "native_chassis": "mimicA",
    "reference_renderer": 121192,
    "renderer_path": "mimic01",
    "native_surface_copied": False,
    "art_status": "Original multipart surface with canonical exact-source binding, appearance, idle, attack, hit, animated death and gameplay evidence; studio and remaining scoped acceptance pending",
    "native_forward": "+UnityZ from mid/front surface and rear upright lid envelope",
    "rig_boundary": "Exact nine-joint palette and inverse binds. Lid follows weighted lidHinge, unused lidTop remains unweighted, lower chest follows mid and Root_M, and the tongue follows the native vertical rest chain. No surface bridge crosses the hinge.",
    "remaining": [
        "Studio review and finished-art acceptance",
        "Every hinge and tongue interval, floor collision and sleeping",
        "Full culling, portraits, resource lifetime and final disposal",
        "Sibling mimic source pairs",
    ],
    "parts": [{
        "name": "verdigrin",
        "vertices": validation["vertices"],
        "triangles": validation["triangles"],
        "palette_joints": validation["bones"],
        "max_bind_rest_error": validation["max_bind_rest_error"],
        "blender_reopen_max_bind_rest_error": roundtrip["max_bind_rest_error"],
        "normal_agreement": validation["source_normal_agreement"]["positive_fraction"],
        "bind_bounds_min": validation["bounds_min"],
        "bind_bounds_max": validation["bounds_max"],
        "blender_roundtrip_glb_sha256": sha(ROOT / "scratch" / "verdigrin-roundtrip" / "verdigrin.glb"),
    }],
    "references": [{
        "renderer_id": 121192,
        "local_reference_path": str(reference_path.relative_to(ROOT)),
        "sha256": sha(reference_path),
    }],
    "checks": {
        "independent_export": "pass for the part",
        "saved_blender_reopen_export": "pass for the part",
        "native_bind_bounds": "Original surface remains inside the corresponding native rest-surface bounds; this is not animated-envelope proof.",
        "studio_review": "pending",
        "live_binding": "exact mimicA / mimic01 / renderer 121192 observed in canonical V4",
        "live_animation": "V4 records complete native idle, attack, ordinary hit and explicit animated death captures",
        "live_material_and_culling": "native material readback observed; full culling remains pending",
    },
    "runtimeProfile": evidence(OUT / "runtime-profile.json"),
    "liveV1": evidence(OUT / "live-validation.json"),
    "liveV2": {
        **evidence(OUT / "live-validation-v2" / "validation.json"),
        "status": v2["status"],
        "session": v2["session"],
    },
    "liveV3": {
        **evidence(OUT / "live-validation-v3" / "validation.json"),
        "status": v3["status"],
        "session": v3["session"],
    },
    "liveV4": {
        **evidence(OUT / "live-validation-v4" / "validation.json"),
        "status": v4["status"],
        "sessions": v4["sessions"],
        "ordinaryHit": "58_to_52",
        "nativeCollects": v4["nativeCollects"],
        "strictReady": "level0_room2",
    },
    "assets": {
        path.name: {"sha256": sha(path), "bytes": path.stat().st_size}
        for path in sorted(OUT.iterdir())
        if path.is_file() and path.name != "manifest.json"
    },
    "limits": v4["limits"],
}

(OUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(sha(OUT / "manifest.json"))
