#!/usr/bin/env python3
"""Build the immutable Sunspire Roc V4 evidence supplement offline."""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "sunspire-roc"
OUT = ASSET / "live-validation-v4"
V2 = ASSET / "live-validation-v2"
V3 = ASSET / "live-validation-v3"
CASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "case-ac099200b3214d8384ad1c981fff8c16" / "case-result.json"
CASE_JOURNAL = CASE.with_name("journal.jsonl")
SESSION_RECORD = ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "new-run-session-243d7c94fca146d7a68bf03af5ba5f7f.json"
REVIEW = ROOT / "scratch" / "sunspire-roc-v4-root-visual-review.json"
RECONCILIATION = ROOT / "scratch" / "sunspire-roc-v4-catalog-reconciliation.json"
PROFILE = ASSET / "runtime-profile.json"
MANIFEST = ASSET / "manifest.json"
GLB = ASSET / "sunspire-roc.glb"
TEXTURE = ASSET / "sunspire-roc_basecolor.png"
CURRENT_CATALOG = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
HISTORICAL_CATALOG_GZ = V2 / "metadata" / "scratch" / "mirewarden-game" / "model-test-profiles.json.gz"
SESSION = "243d7c94fca146d7a68bf03af5ba5f7f"
PORTRAIT_SESSION = "1ce25c30c43f41428094cefea08dae6b"
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
        -245150,
        369188,
        "ftkmf_glb_sunspire-roc.glb",
        "5d3d131f0a7af9374d85c47b4895bf5df48c6a387141b0849c3d26e4400a910b",
    )
    observed = {
        (frame["instanceId"], frame["ownerInstanceId"], frame["mesh"], frame["boneSignature"])
        for frame in raw["frames"]
    }
    assert observed == {expected}, observed
    assert {frame["celRelativeRendererPath"] for frame in raw["frames"]} == {"enRoc01"}
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
v3 = read(V3 / "validation.json")
historical_catalog_raw = gzip.decompress(HISTORICAL_CATALOG_GZ.read_bytes())
historical_catalog = json.loads(historical_catalog_raw)

assert case["schema"] == "ftkmf.exercise-case.v1"
assert case["status"] == "needs_visual_review"
assert case["session"] == SESSION and case["enemy"] == "ftkmf_modeltest_sunspire_roc"
assert case["rendererPath"] == "enRoc01" and case["focusedAttack"] is False
assert case["profileSha256"] == hashlib.sha256(historical_catalog_raw).hexdigest()
assert case["profileSha256"] == "f4584844c7a7b3b1bbefaf3a988c76f7147e860e62f142c635934a119769d2a2"
assert [item["action"] for item in case["actions"]] == ["pass", "attack", "kill-fixture"]
assert all(item["actionAccepted"] is True for item in case["actions"])
assert all(item["capture"]["boundary"]["completeCapture"] is True for item in case["actions"])
assert case["actions"][1]["focus"] is False
assert case["actions"][1]["actionResult"]["result"]["committed"] == "Attack"
assert case["actions"][1]["hpOutcome"]["beforeHp"] == 81
assert case["actions"][1]["hpOutcome"]["afterHp"] == 71
assert case["actions"][2]["actionResult"]["result"]["committed"] == "KillSingle"
assert case["actions"][2]["before"]["combat"]["enemies"][0]["hp"] == 71
assert case["actions"][2]["after"]["combat"]["enemies"][0]["hp"] == 0
assert len(case["collects"]) == 2
assert case["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}

raw_paths = [repo_path(item["capture"]["rawCapture"]["path"]) for item in case["actions"]]
raws = [read(path) for path in raw_paths]
for raw in raws:
    verify_identity(raw)
assert clip_ranges(raws[0]) == [
    (0, 43, ("cidle_roc",)),
    (44, 73, ("attackProf_roc",)),
    (74, 119, ("cidle_roc",)),
]
assert clip_ranges(raws[1]) == [
    (0, 25, ("cidle_roc",)),
    (26, 40, ("damageLight_roc",)),
    (41, 71, ("cidle_roc",)),
    (72, 91, ("attackCrit_roc",)),
    (92, 119, ("cidle_roc",)),
]
assert clip_ranges(raws[2]) == [
    (0, 25, ("cidle_roc",)),
    (26, 119, ("deathHeavy_roc",)),
]

assert sha(V2 / "validation.json") == "6e61239c6d22ee301764fda70136f3e7098c31f2f250aa2a0e6db9e4e693067f"
assert v2["session"] == SESSION and v2["nativeChassis"] == "rocA"
assert v2["rendererPath"] == "enRoc01"
assert v2["ordinaryNoFocusHit"].startswith("OBSERVED:")
assert v2["nativeCollects"] == 2 and v2["finalReady"]["ok"] is True
assert sha(V3 / "validation.json") == "487cc15f5db1d786efe3534106f692deda2a63e088e43a160f0c676242348a9d"
assert v3["session"] == PORTRAIT_SESSION and v3["nativeChassis"] == "rocA"
assert v3["rendererPath"] == "enRoc01"
assert v3["nativeRowPortraitFixture"]["nativeInitializeReturned"] is True
assert v3["nativeRowPortraitFixture"]["targetBinding"]["meshName"] == "ftkmf_glb_sunspire-roc.glb"
assert v3["nativeRowPortraitFixture"]["targetBinding"]["boneCount"] == 36
assert v3["nativeRowPortraitFixture"]["cleanup"]["complete"] is True

profile = profile_document["profiles"][0]
assert profile["key"] == "ftkmf_modeltest_sunspire_roc"
assert profile["baseEnemy"] == "rocA"
assert profile["renderers"][0]["rendererPath"] == "enRoc01"
assert catalog_profile(historical_catalog, profile["key"]) == profile
assert catalog_profile(current_catalog, profile["key"]) == profile
assert sha(CURRENT_CATALOG) == "6c6c04041425c0e7fd7f983d3e96c0ab35ade5e7dbf11754b5052eba1dd2f144"
assert sha(PROFILE) == "49c7d8f7fbbbb63f5fc471b53d0bddcebaa8d1c69cb9a570d28bb2c12e9781f6"
assert sha(GLB) == "da6b90793027a3f33feae317da1efdc1982176e6cee6b704ab4a1b223d7c0c67"
assert sha(TEXTURE) == "911b8202261edc17a1098677b63d69ba960b26a66d2cd5027281d03368762f83"
assert manifest["originalAssets"]["sunspire-roc.glb"] == sha(GLB)
assert manifest["originalAssets"]["sunspire-roc_basecolor.png"] == sha(TEXTURE)

assert reconciliation["status"] == "retained_trial_selected_profile_matches_current_catalog_exactly"
assert reconciliation["retainedTrial"]["sha256"] == sha(V2 / "validation.json")
assert reconciliation["profileDocument"]["sha256"] == sha(PROFILE)
assert reconciliation["currentCatalog"]["sha256"] == sha(CURRENT_CATALOG)
assert reconciliation["matchingSemanticFields"]["glbSha256"] == sha(GLB)
assert reconciliation["matchingSemanticFields"]["textureSha256"] == sha(TEXTURE)
assert review["reviewStatus"] == "reviewed_exact_source_with_scoped_limits"
assert review["session"] == SESSION and review["portraitSession"] == PORTRAIT_SESSION
assert len(review["frames"]) == 6
assert review["binding"]["rendererInstanceId"] == -245150
assert review["binding"]["m_DoRagdoll"] is False

sources = {
    Path(__file__), CASE, CASE_JOURNAL, SESSION_RECORD, REVIEW, RECONCILIATION,
    PROFILE, MANIFEST, GLB, TEXTURE, CURRENT_CATALOG, HISTORICAL_CATALOG_GZ,
    V2 / "validation.json", V2 / "source-image-pins.json", V2 / "asset-pins.json", V2 / "README.md",
    V3 / "validation.json", V3 / "source-image-pins.json", V3 / "asset-pins.json", V3 / "README.md",
    repo_path(v3["nativeRowPortraitFixture"]["source"]),
    repo_path(v3["portraitWatch"]["source"]),
    repo_path(v3["rootPortraitReview"]),
}
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
    name = "native-row-portrait.png" if frame["action"] == "native-row-portrait" else f"{frame['action']}-{frame['index']:04d}.png"
    destination = OUT / "selected" / name
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
    "sunspire-roc.glb": sha(GLB),
    "sunspire-roc_basecolor.png": sha(TEXTURE),
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
    "nativeChassis": "rocA",
    "sourceKind": "native_enemy_row",
    "nativeEnemy": "rocA",
    "resourcePrefab": None,
    "ownerInstanceId": 369188,
    "rendererPath": "enRoc01",
    "rendererId": 121238,
    "sourceRendererId": 121238,
    "rendererKind": "SkinnedMeshRenderer",
    "rendererInstanceId": -245150,
    "mesh": "ftkmf_glb_sunspire-roc.glb",
    "boneSignature": "5d3d131f0a7af9374d85c47b4895bf5df48c6a387141b0849c3d26e4400a910b",
    "rootBone": "Root_M",
    "nativeRootScale": 0.9,
    "active": True,
    "enabled": True,
    "visibleAtInitialReview": True,
    "m_DoRagdoll": False,
    "rigidbodyCount": 0,
    "material": v2["binding"]["materials"][0],
}

captures = [
    {
        "action": "pass",
        "label": "idle-and-native-attack",
        "complete": True,
        "termination": None,
        "rawCapture": pin(raw_paths[0]),
        "frameCount": 120,
        "sampledClips": ["cidle_roc", "attackProf_roc"],
        "captureScope": "Settled idle, native attack proficiency motion, and return to idle on the exact custom renderer.",
    },
    {
        "action": "attack",
        "label": "ordinary-nonlethal-hit-and-native-attack",
        "complete": True,
        "termination": None,
        "rawCapture": pin(raw_paths[1]),
        "frameCount": 120,
        "sampledClips": ["cidle_roc", "damageLight_roc", "attackCrit_roc"],
        "captureScope": "Ordinary no-focus player hit from 81 to 71, native light-damage response, recovery, later native critical attack, and return to idle.",
    },
    {
        "action": "kill-fixture",
        "label": "kill-fixture-death",
        "complete": True,
        "termination": None,
        "rawCapture": pin(raw_paths[2]),
        "frameCount": 120,
        "sampledClips": ["cidle_roc", "deathHeavy_roc"],
        "captureScope": "Explicit KillSingle transition from 71 to 0 and native animated heavy death across the complete capture.",
    },
]

limits = [
    "The ordinary no-focus native Attack records same-target HP 81 to 71 and damageLight_roc. The recorded action and numeric transition establish the scoped ordinary damage observation.",
    "The explicit KillSingle fixture records HP 71 to 0 and deathHeavy_roc. Fixture death is not ordinary lethal-damage evidence.",
    "The exact custom renderer reports m_DoRagdoll false and no rigidbodies across all 360 combat frames. Its death is animated, not a body ragdoll.",
    "The retained V2 process supplies two guarded native Collects and strict Ready at level 0 room 2.",
    "The retained V3 process supplies one constructed native uiEnemyEncounterPortrait.Initialize result and scoped cleanup evidence. It is not an opened encounter menu or a live combat HUD.",
    "Combat UI, the hero, effects, camera crop, and the victory state obscure some surfaces. Full culling coverage, every animation interval, settled corpse state, precise visible corpse lifetime, final resource disposal, and finished art direction remain outside this archive.",
    "This archive covers only rocA and enRoc01 renderer 121238. It does not establish rocB, rocJungleA, or another bird rig.",
]

validation = {
    "schema": "ftkmf.model-validation.v3",
    "revision": "sunspire-roc-live-validation-v4",
    "status": "reviewed_exact_rocA_binding_appearance_idle_attack_hit_death_gameplay_and_row_portrait_observed",
    "displayName": "Sunspire Roc",
    "enemy": "ftkmf_modeltest_sunspire_roc",
    "nativeChassis": "rocA",
    "nativeEnemy": "rocA",
    "sourceKind": "native_enemy_row",
    "resourcePrefab": None,
    "rendererPaths": ["enRoc01"],
    "sourceRendererIds": [121238],
    "sessions": [SESSION, PORTRAIT_SESSION],
    "profile": profile,
    "profileDocumentSha256": sha(PROFILE),
    "catalogSha256": sha(CURRENT_CATALOG),
    "historicalCatalogSha256": hashlib.sha256(historical_catalog_raw).hexdigest(),
    "assetHashes": {
        "sunspire-roc.glb": sha(GLB),
        "sunspire-roc_basecolor.png": sha(TEXTURE),
    },
    "binding": binding,
    "renderers": [binding],
    "captures": captures,
    "ordinaryHit": {
        "beforeHp": 81,
        "afterHp": 71,
        "focus": False,
        "method": "Attack",
        "status": "ordinary_no_focus_nonlethal_hit_with_damageLight_roc_observed",
        "sourceCapture": str(raw_paths[1].relative_to(ROOT)),
    },
    "ordinaryLethal": False,
    "explicitKillFixture": {
        "beforeHp": 71,
        "afterHp": 0,
        "method": "KillSingle",
        "deathCaptureComplete": True,
    },
    "nativeCollects": 2,
    "finalReady": case["finalReady"]["strictReady"],
    "portraitMarkerRegistration": profile["portraitMarkerPath"],
    "nativeRowPortraitFixture": v3["nativeRowPortraitFixture"],
    "priorArchives": [
        {
            "path": "art-experiments/sunspire-roc/live-validation-v2/validation.json",
            "sha256": sha(V2 / "validation.json"),
            "scope": "Separate fresh ordinary body, combat motion, fixture death, Collect, and Ready evidence.",
        },
        {
            "path": "art-experiments/sunspire-roc/live-validation-v3/validation.json",
            "sha256": sha(V3 / "validation.json"),
            "scope": "Separate fresh constructed native row portrait and cleanup evidence.",
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
        "kind": "rearchive_complete_retained_combat_and_portrait_evidence",
        "rule": "When retained trials preserve every required runtime observation but use an older archive schema, compare the selected historical catalog profile with the current profile and asset pins before reuse. Preserve all prior records, conduct a new root review of original pixels, and create a fresh immutable archive with standardized structured gates.",
        "replayPolicy": "Do not replay a complete retained trial solely to repair archive metadata. Run again only for an unobserved evidence gate or a changed profile, asset, source renderer, motion renderer, or selected catalog profile.",
    },
    "limits": limits,
}
write(OUT / "validation.json", validation)

(OUT / "README.md").write_text(
    """# Sunspire Roc live validation V4

This immutable supplement closes the current archive-integrity and structured
gate gaps for the exact `rocA` / `enRoc01` renderer 121238 route. It reuses the
complete V2 combat session and V3 portrait session after proving that the
selected historical and current catalog profiles are identical. The profile,
authored GLB, texture, source renderer, and motion renderer have not changed.

The retained V2 pass capture records `cidle_roc`, native `attackProf_roc`, and
return to idle. Its ordinary no-focus player attack records HP 81 to 71 with
`damageLight_roc`, followed by recovery, native `attackCrit_roc`, and another
idle. The explicit `KillSingle` fixture records HP 71 to 0 and
`deathHeavy_roc` across a complete 120-frame capture. The custom renderer
reports `m_DoRagdoll=false` and no rigidbodies across all 360 combat frames, so
the death is animated rather than a body ragdoll. Fixture death is not ordinary
lethal-damage evidence. Two guarded native Collects reach strict Ready at
level 0 room 2.

The retained V3 session separately records one constructed native
`uiEnemyEncounterPortrait.Initialize` caller. Its exact 36-bone custom clone
produces a reviewed 328 by 280 row portrait and releases the recorded temporary
clone, owned UI texture, and newly-created preview lease assets. This fixture is
not an opened encounter menu or a live combat HUD.

Root-reviewed originals show the authored crown, eyes, beak, chest, wings,
legs, talons, and tail remaining coherent through idle, attacks, ordinary hit,
animated death, and the row portrait. Combat UI, the hero, effects, camera crop,
and the victory state obscure some surfaces. Full culling coverage, every
animation interval, settled corpse state, precise corpse lifetime, final
resource disposal, and finished art direction remain separate gates. The
archive covers only `rocA`, not `rocB`, `rocJungleA`, or another bird rig.

`archive.py` is offline-only and refuses to overwrite a completed archive. It
pins all 360 combat images plus the portrait, copies the reviewed originals,
preserves metadata with gzip-lossless mappings, and records three verified
120-frame presentation videos. Native payloads and DLLs are excluded.
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
