#!/usr/bin/env python3
"""Record the human review of the fresh native row-portrait PNG for Sunspire Roc."""
import hashlib
import json
import os
import struct
from pathlib import Path


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
BASE = ROOT / "scratch/mirewarden-game/model-test-output"
REPORT = ROOT / "scratch/sunspire-roc-portrait-v1.json"
FIXTURE = BASE / "a3d6778fbdd846958c2db0aecb29f53f.json"
OUT = ROOT / "scratch/sunspire-roc-root-portrait-review-v3.json"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def png_size(path: Path) -> tuple[int, int]:
    header = path.read_bytes()[:24]
    assert header[:8] == b"\x89PNG\r\n\x1a\n"
    assert header[12:16] == b"IHDR"
    return struct.unpack(">II", header[16:24])


assert not OUT.exists() or os.environ.get("FTK_REVIEW_REBUILD") == "1", "Refusing to overwrite a completed review."
report = json.loads(REPORT.read_text())
fixture = json.loads(FIXTURE.read_text())
assert report["session"] == "1ce25c30c43f41428094cefea08dae6b"
assert report["profile"] == "ftkmf_modeltest_sunspire_roc"
assert report["rendererPath"] == "enRoc01"
assert report["nativeRowFixture"]["path"] == str(FIXTURE)
assert report["nativeRowFixture"]["sha256"] == sha(FIXTURE)
assert fixture["ok"] is True and fixture["error"] is None
assert fixture["nativeInitializeInvoked"] is True and fixture["nativeInitializeReturned"] is True
assert fixture["sourceUnchanged"] is True
assert fixture["cleanup"]["complete"] is True
assert fixture["cleanup"]["nativeCloneUnityNull"] is True
assert fixture["cleanup"]["ownedTextureUnityNull"] is True
assert fixture["cleanup"]["ownedUiUnityNull"] is True
assert fixture["leaseObservation"]["wasNew"] is True
assert fixture["leaseObservation"]["leaseAbsent"] is True
assert fixture["leaseObservation"]["allPinnedResourcesUnityNull"] is True
assert fixture["nativeDimensions"]["cameraKey"] == "Portrait,328,280"
assert fixture["nativeDimensions"]["width"] == 328 and fixture["nativeDimensions"]["height"] == 280
trace = fixture["passiveTrace"]
assert trace["row"] == report["profile"]
assert trace["identityResolution"] == "exact_row_forwarded_to_native_source"
assert trace["capturePoint"] == "Immediately before native DoRender, after native pose sampling and marker placement"
assert trace["marker"]["firstArgumentAtLastPrefix"] == "PortraitCam"
target = trace["target"]
assert target["name"] == "enRocA(Clone)" and target["nodeCount"] == 55
assert len(target["renderers"]) == 1
renderer = target["renderers"][0]
assert renderer["path"] == "enRoc01" and renderer["boneCount"] == 36
assert renderer["meshName"] == "ftkmf_glb_sunspire-roc.glb"
assert renderer["sharedMaterials"][0]["mainTexture"]["name"] == "ftkmf_sunspire-roc_basecolor.png"
png = report["nativeRowFixture"]["png"]
png_path = Path(png["path"])
assert png_path.is_file() and sha(png_path) == png["sha256"]
assert png_size(png_path) == (328, 280)

review = {
    "status": "SCOPED_NATIVE_ROW_PORTRAIT_VISUAL_REVIEW_PASS_FOR_FRESH_ORIGINAL_ROC",
    "session": report["session"],
    "nativeChassis": "rocA",
    "profile": report["profile"],
    "rendererPath": report["rendererPath"],
    "fixture": {
        "source": str(FIXTURE.relative_to(ROOT)),
        "sha256": sha(FIXTURE),
        "nativeMethod": "uiEnemyEncounterPortrait.Initialize",
        "nativeInitializeReturned": fixture["nativeInitializeReturned"],
        "capturePoint": trace["capturePoint"],
        "identityResolution": trace["identityResolution"],
        "targetMesh": renderer["meshName"],
        "targetTexture": renderer["sharedMaterials"][0]["mainTexture"]["name"],
        "nativeDimensions": fixture["nativeDimensions"],
        "cleanup": fixture["cleanup"],
    },
    "image": {
        "source": str(png_path.relative_to(ROOT)),
        "sha256": sha(png_path),
        "width": 328,
        "height": 280,
    },
    "observations": [
        "The captured native row preview keeps the hooked ivory beak, pale eye, dark-blue faceted head, and neck mantle clearly readable against the black portrait background.",
        "The head is framed intentionally large, while the wing and tail are outside this portrait crop. The visible silhouette has no apparent disconnected panel, explosive deformation, or texture loss at this resolution.",
        "The image retains the authored low-poly surface language and contrasting beak/eye palette; it reads as the same original Roc design used by the separate combat-body trials.",
    ],
    "conclusion": "The exact native row-preview fixture produced a readable 328 by 280 portrait of the authored Sunspire Roc. Its visible head, beak, eye, and neck silhouette are coherent in the reviewed PNG.",
    "limits": [
        "This is one constructed native uiEnemyEncounterPortrait.Initialize caller, not an opened encounter menu or a live combat HUD layout.",
        "The portrait crop does not show the entire body, wings, talons, or tail; their motion and combat visibility remain supported by the separate V1 and V2 body trials.",
        "One output size and one pose were reviewed. This does not establish every portrait resolution, cache reuse path, UI theme, culling case, or full art-direction acceptance.",
        "Cleanup proves only the newly-created preview clone, its owned UI texture, and its recorded lease assets were released; the native portrait cache remains native-owned and was observed without mutation.",
    ],
}
OUT.write_text(json.dumps(review, indent=2) + "\n")
print(json.dumps({"review": str(OUT), "sha256": sha(OUT), "image": review["image"]}, indent=2))
