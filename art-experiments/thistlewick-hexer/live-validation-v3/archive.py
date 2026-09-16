#!/usr/bin/env python3
"""Build the immutable Thistlewick V3 evidence supplement offline."""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "thistlewick-hexer"
OUT = ASSET / "live-validation-v3"
V2 = ASSET / "live-validation-v2"
RUNNER = ROOT / "scratch" / "model-route-f50b09e31a8484cf-directenemy-run.json"
CASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "case-f9dfb7be518c43c9b3701731924cadda" / "case-result.json"
CASE_JOURNAL = CASE.with_name("journal.jsonl")
FLEE_RAW = ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "d93d421c906e4d74b4f3c7d20eb2f30f.json"
FLEE_SUMMARY = ROOT / "scratch" / "thistlewick-v3-flee-summary.json"
HIT_RAW = ROOT / "scratch" / "mirewarden-game" / "model-test-output" / "037e0c63a28a4f8eb16b0a149b00eaf9.json"
HIT_SUMMARY = ROOT / "scratch" / "thistlewick-v3-hit-summary.json"
HIT_RESULT = ASSET / "live-v1" / "case-f39d42b665384371bf1d3a04690f0c72" / "result.json"
HIT_CAPTURE_SUMMARY = HIT_RESULT.with_name("capture-summary.json")
HIT_REVIEW = ASSET / "live-v1" / "thistlewick-first-root-review.json"
REVIEW = ROOT / "scratch" / "thistlewick-v3-root-visual-review.json"
PROFILE = ASSET / "runtime-profile.json"
MANIFEST = ASSET / "manifest.json"
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


def pin(path: Path) -> dict:
    assert path.is_file() and not path.is_symlink(), path
    return {
        "path": str(path.relative_to(ROOT)),
        "sha256": sha(path),
        "bytes": path.stat().st_size,
    }


def clip_ranges(raw: dict) -> list[tuple[int, int, tuple[str, ...]]]:
    ranges: list[list[object]] = []
    for index, frame in enumerate(raw["frames"]):
        names: list[str] = []
        for layer in (frame.get("animator") or {}).get("layers", []):
            names.extend(item["name"] for item in layer.get("playing", []) if item.get("name"))
        key = tuple(names)
        if not ranges or ranges[-1][2] != key:
            ranges.append([index, index, key])
        else:
            ranges[-1][1] = index
    return [(int(first), int(last), tuple(clips)) for first, last, clips in ranges]


def verify_identity(raw: dict, instance_id: int) -> None:
    assert raw["ok"] is True and len(raw["frames"]) == 120
    expected = (
        instance_id,
        369188,
        "ftkmf_glb_thistlewick.glb",
        "ff578d822b39be20026a8ea349afc605024816801a454f5e4f79a52c3b03140a",
    )
    observed = {
        (frame["instanceId"], frame["ownerInstanceId"], frame["mesh"], frame["boneSignature"])
        for frame in raw["frames"]
    }
    assert observed == {expected}, observed


def video_entry(source: Path, destination: Path, action: str, copy: bool = True) -> dict:
    if copy:
        shutil.copy2(source, destination)
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

runner = read(RUNNER)
case = read(CASE)
flee_raw = read(FLEE_RAW)
flee_summary = read(FLEE_SUMMARY)
hit_raw = read(HIT_RAW)
hit_summary = read(HIT_SUMMARY)
hit_result = read(HIT_RESULT)
hit_review = read(HIT_REVIEW)
review = read(REVIEW)
v2 = read(V2 / "validation.json")
profile_document = read(PROFILE)
manifest = read(MANIFEST)

assert runner["status"] == "stopped" and runner["exerciseExitCode"] == 1
assert runner["session"] == "958c0fdda0ec45449333353a5edfda9a"
assert case["status"] == "stopped" and case["error"] == "Target no longer alive before next action"
assert case["session"] == runner["session"] and case["enemy"] == "ftkmf_modeltest_thistlewick"
assert len(case["actions"]) == 1 and case["actions"][0]["action"] == "pass"
assert case["actions"][0]["actionAccepted"] is True
assert case["actions"][0]["actionResult"]["result"] == {"ended": "combat"}
assert case["actions"][0]["capture"]["boundary"]["completeCapture"] is True
assert case["actions"][0]["motionEvidence"]["schema"] == "ftkmf.exercise-motion-evidence.v1"
assert case["actions"][0]["motionEvidence"]["attack"]["nativeAction"]["primary"]["damage"] == 20
assert case["actions"][0]["motionEvidence"]["attack"]["nativeAction"]["postTargetHealth"] == 58
assert case["initialState"]["combat"]["enemies"][0]["hp"] == 58
assert runner["initialRenderer"]["mesh"] == "ftkmf_glb_thistlewick.glb"
assert runner["initialRenderer"]["boneSignature"] == "ff578d822b39be20026a8ea349afc605024816801a454f5e4f79a52c3b03140a"
assert runner["initialRenderer"]["celRelativeRendererPath"] == "enScourgeLeprechaun"
assert runner["stageBindingMatches"][0]["rendererPath"] == "enScourgeLeprechaun"
assert runner["stageBindingMatches"][0]["visualScale"]["actualSpawnedCelRootLocalScale"] == [1.0, 1.0, 1.0]

verify_identity(flee_raw, -245426)
verify_identity(hit_raw, -245450)
assert flee_raw["session"] == runner["session"]
assert hit_raw["session"] == "621d536f4b7847b396d5334e2a05075d"
assert clip_ranges(flee_raw) == [
    (0, 45, ("cidle_impUnarmed",)),
    (46, 64, ("attackProf_leprechaun",)),
    (65, 90, ("cidle_impUnarmed",)),
    (91, 119, ()),
]
assert clip_ranges(hit_raw) == [
    (0, 24, ("cidle_impUnarmed",)),
    (25, 31, ("damageHeavy_imp",)),
    (32, 68, ("cidle_impUnarmed",)),
    (69, 86, ("attackProf_leprechaun",)),
    (87, 112, ("cidle_impUnarmed",)),
    (113, 119, ()),
]
assert flee_summary["meshIdentityStable"] is True and flee_summary["frameCount"] == 120
assert hit_summary["meshIdentityStable"] is True and hit_summary["frameCount"] == 120
assert hit_result["action"] == "attack" and hit_result["status"] == "recorded_action_and_frames"
assert "HP48 from58" in hit_review["hit"]
assert "damageHeavy_imp25" in hit_review["hit"]
assert review["reviewStatus"] == "reviewed_exact_source_with_scoped_limits"
assert review["binding"]["mesh"] == "ftkmf_glb_thistlewick.glb"
assert review["binding"]["boneSignature"] == "ff578d822b39be20026a8ea349afc605024816801a454f5e4f79a52c3b03140a"

assert sha(V2 / "validation.json") == "c0b501d128be15b1631b8b80eea7a83d9e413340f8c0167c78fad06560158721"
assert v2["ordinaryAttack"]["beforeHp"] == 58 and v2["ordinaryAttack"]["afterHp"] == 0
assert v2["ordinaryAttack"]["cheat"] == "None" and v2["ordinaryAttack"]["focus"] is False
assert v2["explicitKillFixture"] is True and v2["finalReady"]["ok"] is True
assert len(v2["captures"]) == 3 and all(item["complete"] is True for item in v2["captures"])
assert profile_document["profiles"][0]["baseEnemy"] == "scourgeG"
assert profile_document["profiles"][0]["renderers"][0]["rendererPath"] == "enScourgeLeprechaun"

asset_hashes = {item["file"]: item["sha256"] for item in manifest["assets"]}
assert asset_hashes == {
    "thistlewick.glb": "c808bf91b52f9ffefae0d4b365f569bcc83bcbb3fd60317ed79e3bc47ad0165c",
    "thistlewick_basecolor.png": "3ae7dd8434a28252dad251363051eaf0b031b0e0ca6b4268f8ff7e7e07adf476",
}
for name, digest in asset_hashes.items():
    assert sha(ASSET / name) == digest

sources = {
    Path(__file__), RUNNER, CASE, CASE_JOURNAL, FLEE_RAW, FLEE_SUMMARY,
    HIT_RAW, HIT_SUMMARY, HIT_RESULT, HIT_CAPTURE_SUMMARY, HIT_REVIEW, REVIEW,
    PROFILE, MANIFEST, ASSET / "thistlewick.glb", V2 / "validation.json",
    V2 / "asset-pins.json", V2 / "source-image-pins.json", V2 / "README.md",
}
for source in sources:
    assert source.is_file() and not source.is_symlink(), source
    assert source.is_relative_to(ROOT), source
    assert source.suffix.lower() not in FORBIDDEN, source

source_image_pins: dict[str, str] = {}
capture_image_pins: dict[str, str] = {}
for raw_path in (FLEE_RAW, HIT_RAW):
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

for row in v2["selectedPNGs"]:
    source = V2 / row["archive"]
    source_relative = str(source.relative_to(ROOT))
    assert source.is_file() and sha(source) == row["sha256"]
    source_image_pins[source_relative] = row["sha256"]
    destination = OUT / "selected" / ("v2-" + Path(row["archive"]).name)
    shutil.copy2(source, destination)
    selected.append({
        "action": "v2-" + row["action"],
        "index": row["index"],
        "source": source_relative,
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "observation": row["observation"],
        "priorArchive": "art-experiments/thistlewick-hexer/live-validation-v2/validation.json",
    })

write(OUT / "source-image-pins.json", source_image_pins)
write(OUT / "capture-image-pins.json", capture_image_pins)
write(OUT / "asset-pins.json", asset_hashes)

review_copy = OUT / "visual-review.json"
shutil.copy2(REVIEW, review_copy)

flee_video = OUT / "native-robbery-flee.mp4"
subprocess.run([
    "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
    "-i", str(FLEE_RAW.with_suffix("") / "%04d.png"), "-frames:v", "120",
    "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(flee_video),
], check=True)
videos = [
    video_entry(flee_video, flee_video, "native-robbery-flee", copy=False),
    video_entry(ASSET / "live-v1" / "normal-hit-attack-flee.mp4", OUT / "ordinary-nonlethal-hit.mp4", "ordinary-nonlethal-hit"),
    video_entry(V2 / "attack.mp4", OUT / "ordinary-lethal-attack.mp4", "ordinary-hp-zero-attack"),
    video_entry(V2 / "kill-fixture.mp4", OUT / "kill-fixture.mp4", "kill-fixture"),
]

binding = {
    "nativeChassis": "scourgeG",
    "sourceKind": "native_enemy_row",
    "nativeEnemy": "scourgeG",
    "resourcePrefab": None,
    "ownerInstanceId": 369188,
    "rendererPath": "enScourgeLeprechaun",
    "rendererId": 121222,
    "rendererKind": "SkinnedMeshRenderer",
    "rendererInstanceId": -245426,
    "mesh": "ftkmf_glb_thistlewick.glb",
    "boneSignature": "ff578d822b39be20026a8ea349afc605024816801a454f5e4f79a52c3b03140a",
    "visualScale": runner["stageBindingMatches"][0]["visualScale"],
    "material": review["materialReview"],
}

captures = [
    {
        "action": "pass",
        "label": "idle-and-native-robbery-flee",
        "complete": True,
        "termination": None,
        "rawCapture": pin(FLEE_RAW),
        "summary": pin(FLEE_SUMMARY),
        "frameCount": 120,
        "states": flee_summary["states"],
        "meshIdentityStable": flee_summary["meshIdentityStable"],
        "meshSignatures": flee_summary["meshSignatures"],
        "causalMotion": case["actions"][0]["motionEvidence"],
        "captureScope": "Settled native idle, exact robbery attack, upright recovery and full-health flee removal. The removal is not death.",
    },
    {
        "action": "attack",
        "label": "ordinary-nonlethal-hit-and-later-native-flee",
        "complete": True,
        "termination": None,
        "rawCapture": pin(HIT_RAW),
        "summary": pin(HIT_SUMMARY),
        "frameCount": 120,
        "states": hit_summary["states"],
        "meshIdentityStable": hit_summary["meshIdentityStable"],
        "meshSignatures": hit_summary["meshSignatures"],
        "captureScope": "Ordinary no-focus player hit 58 to 48 with damageHeavy_imp, recovery, then native robbery attack and full-health flee removal.",
    },
    dict(v2["captures"][1], label="ordinary-hp-zero-attack", priorArchive="art-experiments/thistlewick-hexer/live-validation-v2/validation.json"),
    dict(v2["captures"][2], label="kill-fixture", priorArchive="art-experiments/thistlewick-hexer/live-validation-v2/validation.json"),
]

limits = [
    "The fresh pass capture ends through the native robbery/flee proficiency at HP 58. This is removal behavior, not death.",
    "The ordinary nonlethal hit capture records HP 58 to 48 and damageHeavy_imp. It later reaches the same native flee boundary and does not supply death evidence.",
    "The prior V2 ordinary lethal HP 58 to 0 record remains separate from the explicit KillSingle deathHeavy_imp capture. Fixture death is not ordinary lethal-damage evidence.",
    "The exact custom renderer uses m_DoRagdoll false. Its death is animated, not a body ragdoll.",
    "Hero, weapon, UI and native effects obscure some selected surfaces. Native hat clearance, portraits, culling envelope, long-session resource lifetime, global Fergus haunt behavior and finished-art acceptance remain outside this archive.",
]

validation = {
    "schema": "ftkmf.model-validation.v3",
    "revision": "thistlewick-live-validation-v3",
    "status": "reviewed_exact_scourgeG_binding_appearance_idle_attack_hit_death_gameplay_and_native_flee_observed",
    "displayName": "Thistlewick Hexer",
    "enemy": "ftkmf_modeltest_thistlewick",
    "nativeChassis": "scourgeG",
    "nativeEnemy": "scourgeG",
    "sourceKind": "native_enemy_row",
    "resourcePrefab": None,
    "rendererPaths": ["enScourgeLeprechaun"],
    "sourceRendererIds": [121222],
    "sessions": [runner["session"], hit_raw["session"]] + v2["sessions"],
    "profile": profile_document["profiles"][0],
    "profileDocumentSha256": sha(PROFILE),
    "catalogSha256": runner["plan"]["catalog"]["sha256"],
    "assetHashes": asset_hashes,
    "binding": binding,
    "renderers": [binding],
    "captures": captures,
    "ordinaryHit": {
        "beforeHp": 58,
        "afterHp": 48,
        "cheat": "None",
        "focus": False,
        "status": "ordinary_nonlethal_hit_with_damageHeavy_imp_observed",
        "sourceCapture": str(HIT_RAW.relative_to(ROOT)),
    },
    "ordinaryLethal": True,
    "ordinaryLethalAttack": v2["ordinaryAttack"],
    "explicitKillFixture": True,
    "nativeCollects": v2["nativeCollects"],
    "finalReady": v2["finalReady"],
    "nativeSelfRemoval": {
        "status": "robbery_flee_at_full_health_observed",
        "action": "pass",
        "targetHpBefore": 58,
        "targetHpAfterNativeAction": 58,
        "heroDamage": 20,
        "heroGoldBefore": 11,
        "heroGoldAfter": 0,
        "rendererVisibleFrames": [0, 90],
        "rendererDisabledFrames": [91, 119],
        "classification": "native_flee_removal_not_death",
    },
    "priorArchive": {
        "path": "art-experiments/thistlewick-hexer/live-validation-v2/validation.json",
        "sha256": sha(V2 / "validation.json"),
        "scope": "Separate ordinary lethal attack, explicit animated death fixture, native loot and strict Ready evidence.",
    },
    "runnerRecord": pin(RUNNER),
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
        "kind": "self_removing_enemy_multi_process",
        "rule": "Record native robbery/flee separately. Capture an ordinary nonlethal hit before any pass that can remove the enemy. Keep explicit death fixture and loot/Ready progression in another fresh process.",
        "replayPolicy": "Do not automatically retry a terminal self-removal action. Reuse the retained complete capture and start only a separately planned process for an unobserved gate.",
    },
    "limits": limits,
}
write(OUT / "validation.json", validation)

(OUT / "README.md").write_text(
    """# Thistlewick Hexer live validation V3

This immutable supplement closes the current source-specific idle and hit
evidence gaps for the exact `scourgeG` / `enScourgeLeprechaun` route. It uses
the retained complete captures and does not replay the uncertain native action.

The fresh pass process records `cidle_impUnarmed`, the native
`attackProf_leprechaun` robbery proficiency, upright recovery, and full-health
flee removal. Thistlewick remains at HP 58 after dealing 20 hero damage and
robbing 11 gold. The renderer then becomes translucent and disables as the
encounter ends. This is native flee behavior, not death.

The retained ordinary no-focus process records HP 58 to 48 and
`damageHeavy_imp` before the same later flee boundary. The prior V2 archive
continues to supply the separate ordinary lethal HP record, explicit
`KillSingle` `deathHeavy_imp` capture, two guarded native Collect actions and
strict Ready. `m_DoRagdoll` is false, so the explicit death is animated rather
than a body ragdoll.

The reviewed originals show a coherent authored body through idle, hit,
robbery attack, recovery and flee. Hero, weapon, UI and native effects obscure
some surfaces. Native hat clearance, portraits, culling envelope, long-session
resource lifetime, global Fergus haunt behavior and finished-art acceptance
remain separate gates.

`archive.py` is offline-only and refuses to overwrite a completed archive. It
pins all 240 incremental capture images, copies the reviewed originals,
preserves metadata with gzip-lossless mappings and records four verified
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
