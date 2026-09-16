#!/usr/bin/env python3
"""Build the immutable Belladusk Pitcher V3 evidence supplement offline."""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "belladusk-pitcher"
OUT = ASSET / "live-validation-v3"
V1 = ASSET / "live-validation-v1.json"
V2 = ASSET / "live-validation-v2"
CASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "case-80c6539234ca4a8b9bbfe64831b0d5a6" / "case-result.json"
CASE_JOURNAL = CASE.with_name("journal.jsonl")
SESSION_RECORD = ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "new-run-session-8e7350b348ba499993a11279be206fb2.json"
REVIEW = ROOT / "scratch" / "belladusk-v3-root-visual-review.json"
OLD_REVIEW = ROOT / "scratch" / "belladusk-root-visual-review.json"
RECONCILIATION = ROOT / "scratch" / "belladusk-v3-catalog-reconciliation.json"
PROFILE = ASSET / "runtime-profile.json"
MANIFEST = ASSET / "manifest.json"
GLB = ASSET / "belladusk.glb"
TEXTURE = ASSET / "belladusk_basecolor.png"
CURRENT_CATALOG = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
HISTORICAL_CATALOG_GZ = V2 / "metadata" / "scratch" / "mirewarden-game" / "model-test-profiles.json.gz"
SESSION = "8e7350b348ba499993a11279be206fb2"
FORBIDDEN = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    value = json.loads(path.read_text())
    assert isinstance(value, dict), path
    return value


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def repo_path(value: str) -> Path:
    path = Path(value)
    if not path.is_absolute():
        path = ROOT / path
    path = path.resolve()
    assert path.is_relative_to(ROOT), path
    return path


def pin(path: Path) -> dict:
    assert path.is_file() and not path.is_symlink(), path
    return {
        "path": str(path.relative_to(ROOT)),
        "sha256": sha(path),
        "bytes": path.stat().st_size,
    }


def catalog_profile(catalog: dict, key: str) -> dict:
    matches = [row for row in catalog.get("profiles", []) if row.get("key") == key]
    assert len(matches) == 1, (key, len(matches))
    return matches[0]


def clip_ranges(raw: dict) -> list[tuple[int, int, tuple[str, ...]]]:
    ranges: list[list[object]] = []
    for index, frame in enumerate(raw["frames"]):
        names: list[str] = []
        for layer in (frame.get("animator") or {}).get("layers", []):
            names.extend(
                item["name"]
                for item in layer.get("playing", [])
                if item.get("name") and item.get("weight", 0) > 0
            )
        key = tuple(names)
        if not ranges or ranges[-1][2] != key:
            ranges.append([index, index, key])
        else:
            ranges[-1][1] = index
    return [(int(first), int(last), tuple(clips)) for first, last, clips in ranges]


def verify_identity(raw: dict) -> None:
    assert raw["ok"] is True and raw["session"] == SESSION
    assert len(raw["frames"]) == 120
    expected = (
        -245294,
        369188,
        "ftkmf_glb_belladusk.glb",
        "d5f27b166e2e9db8c2f9fc8aa2d17742ec00ec6fbec1c951901c1118896d1904",
    )
    observed = {
        (frame["instanceId"], frame["ownerInstanceId"], frame["mesh"], frame["boneSignature"])
        for frame in raw["frames"]
    }
    assert observed == {expected}, observed
    assert {frame["celRelativeRendererPath"] for frame in raw["frames"]} == {"enJungleNibbler_A"}
    assert all(frame["active"] is True and frame["enabled"] is True for frame in raw["frames"])
    assert all(frame["ragdoll"]["m_DoRagdoll"] is False for frame in raw["frames"])
    assert all(frame["ragdoll"]["rigidbodyCount"] == 0 for frame in raw["frames"])


def video_entry(raw_path: Path, destination: Path, action: str) -> dict:
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(raw_path.with_suffix("") / "%04d.png"), "-frames:v", "120",
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(destination),
    ], check=True)
    stream = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(destination),
    ]))["streams"][0]
    assert int(stream["nb_read_frames"]) == 120
    return {
        "action": action,
        "path": destination.name,
        "sha256": sha(destination),
        "frames": 120,
        "width": int(stream["width"]),
        "height": int(stream["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative, not unperturbed timing.",
    }


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", (
    "refusing to overwrite a completed archive"
)
OUT.mkdir(parents=True, exist_ok=True)

case = read(CASE)
review = read(REVIEW)
reconciliation = read(RECONCILIATION)
profile_document = read(PROFILE)
manifest = read(MANIFEST)
current_catalog = read(CURRENT_CATALOG)
v2 = read(V2 / "validation.json")
historical_catalog_raw = gzip.decompress(HISTORICAL_CATALOG_GZ.read_bytes())
historical_catalog = json.loads(historical_catalog_raw)

assert case["schema"] == "ftkmf.exercise-case.v1"
assert case["status"] == "needs_visual_review"
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_belladusk"
assert case["rendererPath"] == "enJungleNibbler_A"
assert case["profileSha256"] == hashlib.sha256(historical_catalog_raw).hexdigest()
assert case["profileSha256"] == "f30d96713384e94e97ffca1dca4fc719374eac0e401a98e69ca95eacbb47cc12"
assert [item["action"] for item in case["actions"]] == ["pass", "attack", "kill-fixture"]
assert all(item["actionAccepted"] is True for item in case["actions"])
assert all(item["capture"]["boundary"]["completeCapture"] is True for item in case["actions"])
assert case["actions"][1]["actionResult"]["result"]["committed"] == "Attack"
assert case["actions"][1]["hpOutcome"]["beforeHp"] == 58
assert case["actions"][1]["hpOutcome"]["afterHp"] == 50
assert case["actions"][1]["before"]["party"][0]["focus"] == 4
assert case["actions"][1]["after"]["party"][0]["focus"] == 4
assert case["actions"][2]["actionResult"]["result"]["committed"] == "KillSingle"
assert case["actions"][2]["before"]["combat"]["enemies"][0]["hp"] == 50
assert case["actions"][2]["after"]["combat"]["enemies"][0]["hp"] == 0
assert len(case["collects"]) == 1 and case["collects"][0]["result"]["ok"] is True
assert case["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}

raw_paths = [repo_path(item["capture"]["rawCapture"]["path"]) for item in case["actions"]]
raws = [read(path) for path in raw_paths]
for raw in raws:
    verify_identity(raw)
assert clip_ranges(raws[0]) == [
    (0, 45, ("idle",)),
    (46, 55, ("attack1",)),
    (56, 119, ("idle",)),
]
assert clip_ranges(raws[1]) == [
    (0, 26, ("idle",)),
    (27, 31, ("hit1",)),
    (32, 80, ("idle",)),
    (81, 90, ("attack1",)),
    (91, 119, ("idle",)),
]
assert clip_ranges(raws[2]) == [
    (0, 27, ("idle",)),
    (28, 119, ("death",)),
]

assert sha(V1) == "95dae5b7286f86a6a08af2cb8cd9e738b5febde5b65d626a2f7fef08195e24a3"
assert sha(V2 / "validation.json") == "bd66b6c50f6aeb77961abfd86eb17d265a97a70ec03ce62acf0d49d7e8d943a5"
assert v2["session"] == SESSION and v2["nativeChassis"] == "plantE"
assert v2["rendererPath"] == "enJungleNibbler_A"
assert v2["ordinaryHit"] == {"beforeHp": 58, "afterHp": 50, "cheat": "None", "focus": False}
assert v2["nativeCollects"] == 1 and v2["finalReady"] == {"level": 0, "room": 2}

profile = profile_document["profiles"][0]
assert profile["key"] == "ftkmf_modeltest_belladusk"
assert profile["baseEnemy"] == "plantE"
assert profile["renderers"][0]["rendererPath"] == "enJungleNibbler_A"
assert catalog_profile(historical_catalog, profile["key"]) == profile
assert catalog_profile(current_catalog, profile["key"]) == profile
assert sha(CURRENT_CATALOG) == "6c6c04041425c0e7fd7f983d3e96c0ab35ade5e7dbf11754b5052eba1dd2f144"
assert sha(PROFILE) == "3ce70025a7ae787cc9b1253825ca02cb1e747696b2347f6cdf6626c001e6d703"
assert sha(GLB) == "0678780baaf1f709bf43dabaca1fd30a469f8f443624a57bb9dbd506da064fd9"
assert sha(TEXTURE) == "51a84eb77be58235914d77b6030c5922d0de4baf3fe4ba2797d4b7a67e8ce493"
manifest_assets = {Path(item["path"]).name: item["sha256"] for item in manifest["assets"]}
assert manifest_assets[GLB.name] == sha(GLB)
assert manifest_assets[TEXTURE.name] == sha(TEXTURE)

assert reconciliation["status"] == "retained_trial_selected_profile_matches_current_catalog_exactly"
assert reconciliation["retainedTrial"]["sha256"] == sha(V2 / "validation.json")
assert reconciliation["profileDocument"]["sha256"] == sha(PROFILE)
assert reconciliation["currentCatalog"]["sha256"] == sha(CURRENT_CATALOG)
assert reconciliation["matchingSemanticFields"]["glbSha256"] == sha(GLB)
assert reconciliation["matchingSemanticFields"]["textureSha256"] == sha(TEXTURE)
assert review["reviewStatus"] == "reviewed_exact_source_with_scoped_limits"
assert review["session"] == SESSION and len(review["frames"]) == 6
assert review["binding"]["rendererInstanceId"] == -245294
assert review["binding"]["m_DoRagdoll"] is False

sources = {
    Path(__file__), CASE, CASE_JOURNAL, SESSION_RECORD, REVIEW, OLD_REVIEW,
    RECONCILIATION, PROFILE, MANIFEST, GLB, TEXTURE, CURRENT_CATALOG,
    HISTORICAL_CATALOG_GZ, V1, V2 / "validation.json", V2 / "source-image-pins.json",
    V2 / "README.md", V2 / "archive.py",
}
claim_path = case.get("claim", {}).get("path")
if claim_path:
    sources.add(repo_path(claim_path))
for action in case["actions"]:
    sources.add(repo_path(action["capture"]["rawCapture"]["path"]))
    sources.add(repo_path(action["journal"]["path"]))
    sources.add(repo_path(action["rawResult"]["path"]))
for source in sources:
    assert source.is_file() and not source.is_symlink(), source
    assert source.is_relative_to(ROOT), source
    assert source.suffix.lower() not in FORBIDDEN, source

source_image_pins: dict[str, str] = {}
capture_image_pins: dict[str, str] = {}
for raw_path in raw_paths:
    images = sorted(raw_path.with_suffix("").glob("*.png"))
    assert len(images) == 120
    for image in images:
        relative = str(image.relative_to(ROOT))
        digest = sha(image)
        source_image_pins[relative] = digest
        capture_image_pins[relative] = digest

lossless_mappings: list[dict] = []
for source in sorted(sources):
    raw = source.read_bytes()
    relative = source.relative_to(ROOT)
    destination = OUT / "metadata" / (str(relative) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    lossless_mappings.append({
        "source": str(relative),
        "sourceSha256": hashlib.sha256(raw).hexdigest(),
        "archive": str(destination.relative_to(OUT)),
        "archiveSha256": sha(destination),
        "bytes": len(raw),
        "encoding": "gzip-lossless",
    })

selected: list[dict] = []
for frame in review["frames"]:
    source = ROOT / frame["path"]
    assert source.is_file() and sha(source) == frame["sha256"]
    source_image_pins[frame["path"]] = frame["sha256"]
    destination = OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    selected.append({
        "action": frame["action"],
        "index": frame["index"],
        "clip": frame["clip"],
        "source": frame["path"],
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "observation": frame["observation"],
    })

write(OUT / "source-image-pins.json", source_image_pins)
write(OUT / "capture-image-pins.json", capture_image_pins)
write(OUT / "asset-pins.json", {
    "belladusk.glb": sha(GLB),
    "belladusk_basecolor.png": sha(TEXTURE),
})

review_copy = OUT / "visual-review.json"
shutil.copy2(REVIEW, review_copy)
reconciliation_copy = OUT / "catalog-reconciliation.json"
shutil.copy2(RECONCILIATION, reconciliation_copy)

videos = [
    video_entry(raw_paths[0], OUT / "native-idle-attack.mp4", "native-idle-attack"),
    video_entry(raw_paths[1], OUT / "ordinary-nonlethal-hit.mp4", "ordinary-nonlethal-hit"),
    video_entry(raw_paths[2], OUT / "kill-fixture.mp4", "kill-fixture"),
]

binding = {
    "nativeChassis": "plantE",
    "sourceKind": "native_enemy_row",
    "nativeEnemy": "plantE",
    "resourcePrefab": None,
    "ownerInstanceId": 369188,
    "rendererPath": "enJungleNibbler_A",
    "rendererId": 121530,
    "sourceRendererId": 121530,
    "rendererKind": "SkinnedMeshRenderer",
    "rendererInstanceId": -245294,
    "mesh": "ftkmf_glb_belladusk.glb",
    "boneSignature": "d5f27b166e2e9db8c2f9fc8aa2d17742ec00ec6fbec1c951901c1118896d1904",
    "rootBone": "Root_M",
    "nativeRootScale": 1.2,
    "active": True,
    "enabled": True,
    "visibleAtInitialReview": True,
    "m_DoRagdoll": False,
    "rigidbodyCount": 0,
    "material": case["initialRenderer"]["materials"][0],
}

captures = [
    {
        "action": "pass",
        "label": "idle-and-native-attack",
        "complete": True,
        "termination": None,
        "rawCapture": pin(raw_paths[0]),
        "frameCount": 120,
        "sampledClips": ["idle", "attack1"],
        "captureScope": "Settled idle, native attack1 motion, and return to idle on the exact custom renderer.",
    },
    {
        "action": "attack",
        "label": "ordinary-nonlethal-hit-and-native-attack",
        "complete": True,
        "termination": None,
        "rawCapture": pin(raw_paths[1]),
        "frameCount": 120,
        "sampledClips": ["idle", "hit1", "attack1"],
        "captureScope": "Ordinary player hit from 58 to 50 with no focus spent, native hit1 response, recovery, later native attack1, and return to idle.",
    },
    {
        "action": "kill-fixture",
        "label": "kill-fixture-death",
        "complete": True,
        "termination": None,
        "rawCapture": pin(raw_paths[2]),
        "frameCount": 120,
        "sampledClips": ["idle", "death"],
        "captureScope": "Explicit KillSingle transition from 50 to 0 and native animated death across the complete capture.",
    },
]

limits = [
    "The ordinary native Attack records same-target HP 58 to 50 with no focus spent and hit1. The recorded action and numeric transition establish the scoped ordinary damage observation.",
    "The explicit KillSingle fixture records HP 50 to 0 and death. Fixture death is not ordinary lethal-damage evidence.",
    "The exact custom renderer reports m_DoRagdoll false and no rigidbodies across all 360 frames. Its death is animated, not a body ragdoll.",
    "The retained process supplies one guarded native Collect and strict Ready at level 0 room 2.",
    "Combat UI, the hero, effects, depth blur, and victory progression obscure some surfaces. Full culling coverage, every animation interval, collision, physics sleeping, settled corpse state, precise visible corpse lifetime, portrait behavior, final resource disposal, and finished art direction remain outside this archive.",
    "This archive covers only plantE and enJungleNibbler_A renderer 121530. It does not establish another plant rig.",
]

validation = {
    "schema": "ftkmf.model-validation.v3",
    "revision": "belladusk-pitcher-live-validation-v3",
    "status": "reviewed_exact_plantE_binding_appearance_idle_attack_hit_death_and_gameplay_observed",
    "displayName": "Belladusk Pitcher",
    "enemy": "ftkmf_modeltest_belladusk",
    "nativeChassis": "plantE",
    "nativeEnemy": "plantE",
    "sourceKind": "native_enemy_row",
    "resourcePrefab": None,
    "rendererPaths": ["enJungleNibbler_A"],
    "sourceRendererIds": [121530],
    "sessions": [SESSION],
    "profile": profile,
    "profileDocumentSha256": sha(PROFILE),
    "catalogSha256": sha(CURRENT_CATALOG),
    "historicalCatalogSha256": hashlib.sha256(historical_catalog_raw).hexdigest(),
    "assetHashes": {
        "belladusk.glb": sha(GLB),
        "belladusk_basecolor.png": sha(TEXTURE),
    },
    "binding": binding,
    "renderers": [binding],
    "captures": captures,
    "ordinaryHit": {
        "beforeHp": 58,
        "afterHp": 50,
        "focus": False,
        "focusSpent": 0,
        "method": "Attack",
        "status": "ordinary_nonlethal_hit_with_hit1_observed",
        "sourceCapture": str(raw_paths[1].relative_to(ROOT)),
    },
    "ordinaryLethal": False,
    "explicitKillFixture": {
        "beforeHp": 50,
        "afterHp": 0,
        "method": "KillSingle",
        "deathCaptureComplete": True,
    },
    "nativeCollects": 1,
    "finalReady": case["finalReady"]["strictReady"],
    "priorArchives": [
        {
            "path": "art-experiments/belladusk-pitcher/live-validation-v1.json",
            "sha256": sha(V1),
            "scope": "Historical selected-view binding, appearance, motion, fixture death, and progression evidence.",
        },
        {
            "path": "art-experiments/belladusk-pitcher/live-validation-v2/validation.json",
            "sha256": sha(V2 / "validation.json"),
            "scope": "Fresh ordinary body, combat motion, fixture death, Collect, and Ready evidence retained under an older structured schema.",
        },
    ],
    "catalogReconciliation": {
        "path": "catalog-reconciliation.json",
        "sha256": sha(reconciliation_copy),
        "bytes": reconciliation_copy.stat().st_size,
    },
    "caseResult": pin(CASE),
    "runtimeProfile": pin(PROFILE),
    "visualReview": review["reviewStatus"],
    "rootVisualReview": "visual-review.json",
    "rootVisualReviewPin": {
        "path": "visual-review.json",
        "sha256": sha(review_copy),
        "bytes": review_copy.stat().st_size,
    },
    "selectedPNGs": selected,
    "videos": videos,
    "sourceImageCount": len(source_image_pins),
    "sourceImagePins": "source-image-pins.json",
    "captureImageCount": len(capture_image_pins),
    "captureImagePins": "capture-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": lossless_mappings,
    "workflow": {
        "kind": "rearchive_complete_retained_combat_evidence",
        "rule": "When retained trials preserve every required runtime observation but use an older archive schema, compare the selected historical catalog profile with the current profile and asset pins before reuse. Preserve all prior records, review original pixels against exact animator intervals, and create a fresh immutable archive with standardized structured gates.",
        "replayPolicy": "Do not replay a complete retained trial solely to repair archive metadata. Run again only for an unobserved evidence gate or a changed profile, asset, source renderer, motion renderer, or selected catalog profile.",
    },
    "limits": limits,
}
write(OUT / "validation.json", validation)

(OUT / "README.md").write_text(
    """# Belladusk Pitcher live validation V3

This immutable supplement closes the structured gate gaps for the exact
`plantE` / `enJungleNibbler_A` renderer 121530 route. It reuses the complete V2
combat session after proving that the selected historical and current catalog
profiles are identical. The profile, authored GLB, texture, source renderer,
and motion renderer have not changed.

The retained pass capture records settled `idle`, native `attack1`, and return
to idle. Its ordinary player attack records HP 58 to 50 with no focus spent,
followed by native `hit1`, recovery, a later `attack1`, and another idle. The
explicit `KillSingle` fixture records HP 50 to 0 and `death` across a complete
120-frame capture. The custom renderer reports `m_DoRagdoll=false` and no
rigidbodies across all 360 frames, so the death is animated rather than a body
ragdoll. Fixture death is not ordinary lethal-damage evidence. One guarded
native Collect reaches strict Ready at level 0 room 2.

Root-reviewed originals show the authored hood, mouth, teeth, throat, stalk,
and four-leaf base remaining coherent through idle, two attacks, ordinary hit,
and animated fixture death. Combat UI, the hero, effects, depth blur, and
victory progression obscure some surfaces. Full culling coverage, every
animation interval, collision, physics sleeping, settled corpse state, precise
corpse lifetime, portrait behavior, final resource disposal, and finished art
direction remain separate gates. The archive covers only `plantE`, not another
plant rig.

The older V2 review mislabeled two early idle frames as attack views. V3 derives
every motion label from the raw animator timeline and selects frames inside the
recorded `attack1`, `hit1`, and `death` intervals.

`archive.py` is offline-only and refuses to overwrite a completed archive. It
pins all 360 source images, copies six reviewed originals, preserves metadata
with gzip-lossless mappings, and records three verified 120-frame presentation
videos. Native payloads and DLLs are excluded.
"""
)

print(json.dumps({
    "validation": str(OUT / "validation.json"),
    "validationSha256": sha(OUT / "validation.json"),
    "selected": len(selected),
    "sourceImages": len(source_image_pins),
    "captureImages": len(capture_image_pins),
    "videos": len(videos),
    "losslessMappings": len(lossless_mappings),
}, indent=2))
