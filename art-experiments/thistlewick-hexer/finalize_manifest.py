import hashlib
import json
from pathlib import Path


OUT = Path(__file__).resolve().parent
ROOT = OUT.parent.parent


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def evidence(path: Path) -> dict:
    relative = path.relative_to(OUT) if path.is_relative_to(OUT) else path.relative_to(ROOT)
    return {"file": str(relative), "sha256": sha(path), "bytes": path.stat().st_size}


assert json.loads((OUT / "original-geometry-proof.json").read_text())["status"] == "PASS"
for name in ("thistlewick.validation.json", "reopened-validation.json"):
    validation = json.loads((OUT / name).read_text())
    assert validation["bones"] == 36
    assert validation["source_normal_agreement"]["positive_fraction"] == 1
    assert validation["max_bind_rest_error"] < 1e-5

manifest = {
    "model": "Thistlewick Hexer",
    "status": "ORIGINAL_BODY_OFFLINE_PASS_DODGE_CRITICAL_HIT_DEATH_FIT_LIVE_V3_CANONICAL_SCOPED_VALIDATED",
    "nativeEnemy": "scourgeG",
    "rendererId": 121222,
    "rendererPath": "enScourgeLeprechaun",
    "controllerId": 5934,
    "nativeCelScale": 1,
    "visualScaleFactor": 1,
    "sourceFindings": evidence(ROOT / "scratch" / "scourge-leprechaun-topology-analysis" / "findings.json"),
    "nativePassEvidence": evidence(ROOT / "docs" / "evidence" / "fergus-diagnostic-pass-v1" / "validation.json"),
    "assets": [evidence(OUT / name) for name in ("thistlewick.glb", "thistlewick_basecolor.png")],
    "runtimeProfile": evidence(OUT / "runtime-profile.json"),
    "surfaceBounds": evidence(OUT / "surface-bounds-audit.json"),
    "originalGeometryProof": evidence(OUT / "original-geometry-proof.json"),
    "liveV2": evidence(OUT / "live-validation-v2" / "validation.json"),
    "liveV3": evidence(OUT / "live-validation-v3" / "validation.json"),
    "files": [
        evidence(path)
        for path in sorted(OUT.iterdir())
        if path.is_file() and path.name != "manifest.json"
    ],
    "limits": [
        "Native rigid enLuckysHat remains on Hair_M; it is not copied or included in the studio file.",
        "Canonical V3 records exact binding, idle, native robbery attack, ordinary nonlethal hit, explicit animated death, loot and Ready. Native full-health flee is not death, and fixture death is not ordinary lethal evidence.",
        "Native hat clearance, culling, portraits, long-session resource lifetime, global Fergus behavior and finished-art acceptance remain open."
    ],
}

(OUT / "manifest.json").write_text(json.dumps(manifest, indent=2) + "\n")
print(sha(OUT / "manifest.json"))
