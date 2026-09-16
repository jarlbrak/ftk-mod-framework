#!/usr/bin/env python3
"""Archive the V4 modern Kraken head and rigid eye trial without game payloads."""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess

ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())
ASSET = ROOT / "art-experiments" / "abyssal-kraken"
OUT = ASSET / "live-validation-v4-head-static"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CASE_ID = "case-29704ec7260e4ac0bf05270822394e06"
SESSION = "4bda953c836d442f999f53e7f30f9c30"
STAGE_CASE_ID = "case-5271c04723e0432e9a00a50883372b0f"
PROFILE_SHA256 = "51f22c64607feebb3241961023b7c642c3e30f30b3b7f5478794da399253df79"
BONE_SIGNATURE = "35674ecb6b315d65a7e6f8f987d1ad921d80930a81001d3eb648db6004a8d201"
STATIC_PATH = "Root_M/base/body/neck/eye/kraken2_eye"
STAGE_RECEIPT = ROOT / "scratch" / "runtime-profile-423-abyssal-kraken-v4-head-compact-eye" / "receipt.json"
DEPLOYMENT_RECEIPT = ROOT / "scratch" / "mirewarden-game" / "deployment-backups" / "abyssal-kraken-v4-head-compact-eye-20260911-205050" / "deployment.json"
RESTORE_STAGE_RECEIPT = ROOT / "scratch" / "abyssal-kraken-v4-restore-from-probe-stage-20260911-211800" / "receipt.json"
RESTORE_DEPLOYMENT_RECEIPT = ROOT / "scratch" / "mirewarden-game" / "deployment-backups" / "abyssal-kraken-v4-restore-from-probe-20260911-211940" / "deployment.json"
CATALOG = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
INVENTORIES = [
    BASE / "4f131f5db9044e19999b2f13d3019943.json",
    BASE / "73948e9172554c7094127010425cb611.json",
    BASE / "dceef6f03033412dbb01a898757d9ce5.json",
    BASE / "1dbe21e6438541c39a78a6591068b03d.json",
]
AUTHORING = [
    "runtime-profiles-v3-head-static-eye.json",
    "runtime-profiles-v4-head-compact-eye.json",
    "runtime-profiles-v5-head-reattached-eye.json",
    "runtime-profiles-placement-probe-v1.json",
    "runtime-profiles.json",
    "abyssal-crown-kraken-head-v2.glb",
    "abyssal-crown-kraken-head-v2.png",
    "abyssal-crown-kraken-head-v2.source.json",
    "abyssal-crown-kraken-head-v2.pieces.json",
    "abyssal-crown-kraken-head-v2.validation.json",
    "abyssal-crown-kraken-head-v2-reopened.glb",
    "abyssal-crown-kraken-head-v2-reopened.png",
    "abyssal-crown-kraken-head-v2-reopened.source.json",
    "abyssal-crown-kraken-head-v2-reopened.validation.json",
    "abyssal-crown-kraken-head-v2.blend",
    "abyssal-crown-kraken-head-v2-studio.blend",
    "abyssal-crown-kraken-head-v2-hero.png",
    "abyssal-crown-kraken-head-v2-side.png",
    "abyssal-crown-kraken-eye-v4.glb",
    "abyssal-crown-kraken-eye-v4.png",
    "abyssal-crown-kraken-eye-v4.source.json",
    "abyssal-crown-kraken-eye-v4.pieces.json",
    "abyssal-crown-kraken-eye-v4.validation.json",
    "abyssal-crown-kraken-eye-v4-blend-validation.json",
    "abyssal-crown-kraken-eye-v4.blend",
    "abyssal-crown-kraken-eye-v4-studio.blend",
    "abyssal-crown-kraken-eye-v4-hero.png",
    "abyssal-crown-kraken-eye-v4-side.png",
    "build_geometry.py",
    "build_blender.py",
    "verify_original_geometry.py",
    "verify_target_bindings.py",
    "build_static_eye_v4.py",
    "build_static_eye_v4_blender.py",
    "build_static_eye_v5.py",
    "build_static_eye_v5_blender.py",
    "build_static_eye_placement_probe.py",
    "stage_runtime_profiles-v4-head-compact-eye.py",
    "deploy_runtime_profiles-v4-head-compact-eye.py",
    "stage_restore_v4_from_static_probe.py",
    "deploy_restore_v4_from_static_probe.py",
]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def relative(path: Path) -> str:
    return str(path.resolve().relative_to(ROOT))


def add_source(sources: set[Path], path: Path) -> None:
    path = path.resolve()
    assert path.is_file() and not path.is_symlink(), path
    assert path.is_relative_to(ROOT), path
    assert path.suffix.lower() not in FORBIDDEN_SUFFIXES, path
    sources.add(path)


def enemy_snapshot(snapshot: dict, enemy: str) -> dict:
    matches = [item for item in snapshot.get("combat", {}).get("enemies", []) if item.get("type") == enemy]
    assert len(matches) == 1, matches
    return matches[0]


def compact_material(material: dict) -> dict:
    return {
        "name": material["name"],
        "shader": material["shader"],
        "emissionKeyword": material["emissionKeyword"],
        "emissionColor": material["emissionColor"],
        "mainTexture": material["_MainTex"],
        "emissionMap": material["_EmissionMap"],
    }


def assert_head(renderer: dict) -> None:
    assert renderer["ownerRootName"] == "Enemy Dummy"
    assert renderer["celRootName"] == "enKraken(Clone)"
    assert renderer["celRelativeRendererPath"] == "kraken2"
    assert renderer["rendererKind"] == "SkinnedMeshRenderer"
    assert renderer["mesh"] == "ftkmf_glb_abyssal-crown-kraken-head-v2.glb"
    assert renderer["boneSignature"] == BONE_SIGNATURE
    assert renderer["rootBone"] == "Root_M"
    assert renderer["animator"]["controller"] == "krakenHeadController"
    assert len(renderer["materials"]) == 1
    material = renderer["materials"][0]
    assert material["emissionKeyword"] is False
    assert material["_EmissionMap"] is None
    assert material["_MainTex"]["name"] == "ftkmf_abyssal-crown-kraken-head-v2.png"


def static_renderer(snapshot: dict) -> dict:
    matches = [renderer for renderer in snapshot["renderers"] if renderer.get("celRelativeRendererPath") == STATIC_PATH]
    assert len(matches) == 1, matches
    renderer = matches[0]
    assert renderer["ownerRootName"] == "Enemy Dummy"
    assert renderer["celRootName"] == "enKraken(Clone)"
    assert renderer["rendererKind"] == "MeshRenderer"
    assert renderer["meshFilterCount"] == 1
    assert renderer["meshFilterInstanceId"] is not None
    assert renderer["mesh"] == "ftkmf_static_glb_abyssal-crown-kraken-eye-v4.glb"
    assert renderer["enabled"] is True and renderer["active"] is True and renderer["isVisible"] is True
    assert renderer["resourceLease"]["available"] is True
    assert renderer["resourceLease"]["present"] is True
    assert renderer["resourceLease"]["acquired"] is True
    assert renderer["resourceLease"]["applied"] is True
    assert len(renderer["materials"]) == 1
    material = renderer["materials"][0]
    assert material["emissionKeyword"] is False
    assert material["_EmissionMap"] is None
    assert material["_MainTex"]["name"] == "ftkmf_abyssal-crown-kraken-eye-v4.png"
    return renderer


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "Refusing to overwrite a completed archive."
OUT.mkdir(parents=True, exist_ok=True)
case_path = BASE / CASE_ID / "case-result.json"
case = read(case_path)
stage_result_path = BASE / STAGE_CASE_ID / "result.json"
stage_result = read(stage_result_path)
session_path = BASE / f"new-run-session-{SESSION}.json"
session = read(session_path)

assert case["schema"] == "ftkmf.exercise-case.v1"
assert case["session"] == SESSION
assert case["enemy"] == "ftkmf_modeltest_abyssal_crown_kraken_head"
assert case["profileSha256"] == PROFILE_SHA256
assert case["status"] == "needs_visual_review"
assert case["focusedAttack"] is False
assert_head(case["initialRenderer"])
assert case["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
assert case["attackRetrySummary"] == [{
    "attempt": 1,
    "status": "nonlethal_hp_loss",
    "beforeHp": 324,
    "afterHp": 316,
    "boundary": "Observed same-target HP only; no inferred block, dodge, hit animation, or damage source.",
}]
assert session == {"session": SESSION, "case": STAGE_CASE_ID.removeprefix("case-"), "journal": str(BASE / STAGE_CASE_ID / "journal.jsonl")}
assert stage_result["status"] == "binding_metadata_observed"
assert stage_result["session"] == SESSION
assert [(item["rendererPath"], item["rendererKind"], item["glbFile"]) for item in stage_result["matches"]] == [
    ("kraken2", "SkinnedMeshRenderer", "abyssal-crown-kraken-head-v2.glb"),
    (STATIC_PATH, "MeshRenderer", "abyssal-crown-kraken-eye-v4.glb"),
]

profiles = read(ASSET / "runtime-profiles-v4-head-compact-eye.json")
profile = next(row for row in profiles["profiles"] if row["key"] == case["enemy"])
assert profile == {
    "key": case["enemy"],
    "baseEnemy": "krakenHead",
    "displayName": "Abyssal Crown V4",
    "combatProfile": "cec974e3be711225508204ce60ba00ebc9f5cc604ea7d5a573e1a3269bc9038d",
    "renderers": [
        {"rendererPath": "kraken2", "glbFile": "abyssal-crown-kraken-head-v2.glb", "textureFile": "abyssal-crown-kraken-head-v2.png", "disableNativeEmission": True},
        {"rendererPath": STATIC_PATH, "rendererKind": "MeshRenderer", "glbFile": "abyssal-crown-kraken-eye-v4.glb", "textureFile": "abyssal-crown-kraken-eye-v4.png", "disableNativeEmission": True},
    ],
}
assert sha(CATALOG) == PROFILE_SHA256
assert next(row for row in read(CATALOG)["profiles"] if row["key"] == case["enemy"]) == profile

stage_receipt = read(STAGE_RECEIPT)
deployment = read(DEPLOYMENT_RECEIPT)
restore_stage = read(RESTORE_STAGE_RECEIPT)
restore_deployment = read(RESTORE_DEPLOYMENT_RECEIPT)
assert stage_receipt["status"] == "STAGED_NOT_DEPLOYED_ABYSSAL_KRAKEN_V4_HEAD_COMPACT_EYE"
assert stage_receipt["catalog"]["sha256"] == PROFILE_SHA256
assert deployment["status"] == "VERIFIED_COMPLETE"
assert deployment["new"]["model-test-profiles.json"] == PROFILE_SHA256
assert restore_stage["status"] == "STAGED_NOT_DEPLOYED_ABYSSAL_KRAKEN_V4_RESTORE_FROM_PROBE"
assert restore_stage["catalog"]["sha256"] == PROFILE_SHA256
assert restore_stage["newAssets"] == {}
assert restore_deployment["status"] == "VERIFIED_COMPLETE"
assert restore_deployment["new"]["model-test-profiles.json"] == PROFILE_SHA256

source_assets = {
    "abyssal-crown-kraken-head-v2.glb": "33d24c5c0b393ab3e4df44a2ae2f704bc691eff17c528d5e30ea5e1ab47e3719",
    "abyssal-crown-kraken-head-v2.png": "d4ac6a37fb7158e4b915164417cd4f90f3cd71163c787185db6b267563cbf7e9",
    "abyssal-crown-kraken-eye-v4.glb": "be73a29ad59cd7bbca7a0724656f4c735847f839ca8d53f2600fab675e508c9b",
    "abyssal-crown-kraken-eye-v4.png": "b4cd950d22611517451a882fdc6afd4218477d3f70a1d7953b9e57b110952611",
}
assert {name: sha(ASSET / name) for name in source_assets} == source_assets
assert stage_receipt["newAssets"] == {name: source_assets[name] for name in ["abyssal-crown-kraken-eye-v4.glb", "abyssal-crown-kraken-eye-v4.png"]}
assert restore_stage["existingCandidateAssets"] == {name: source_assets[name] for name in ["abyssal-crown-kraken-eye-v4.glb", "abyssal-crown-kraken-eye-v4.png"]}

inventory_records = []
for path in INVENTORIES:
    snapshot = read(path)
    assert snapshot["ok"] is True and snapshot["session"] == SESSION
    head = next(renderer for renderer in snapshot["renderers"] if renderer.get("celRelativeRendererPath") == "kraken2")
    assert_head(head)
    static = static_renderer(snapshot)
    assert static["ownerInstanceId"] == head["ownerInstanceId"] == 369188
    inventory_records.append({
        "source": relative(path),
        "sha256": sha(path),
        "frame": snapshot["frame"],
        "head": {"mesh": head["mesh"], "rendererKind": head["rendererKind"], "boneSignature": head["boneSignature"]},
        "static": {
            "mesh": static["mesh"],
            "rendererKind": static["rendererKind"],
            "meshFilterCount": static["meshFilterCount"],
            "lastCombatTrigger": static["lastCombatTrigger"],
            "boundsSize": static["boundsSize"],
            "material": compact_material(static["materials"][0]),
            "lease": static["resourceLease"],
        },
    })

sources: set[Path] = set()
for path in [Path(__file__), CATALOG, case_path, BASE / CASE_ID / "journal.jsonl", stage_result_path, BASE / STAGE_CASE_ID / "journal.jsonl", session_path, Path(session["journal"]), STAGE_RECEIPT, DEPLOYMENT_RECEIPT, RESTORE_STAGE_RECEIPT, RESTORE_DEPLOYMENT_RECEIPT, *INVENTORIES]:
    add_source(sources, path)
for name in AUTHORING:
    add_source(sources, ASSET / name)

capture_records = []
image_pins: dict[str, str] = {}
for action in case["actions"]:
    label = action["action"]
    capture = action["capture"]
    raw = capture["rawCapture"]
    raw_path = Path(raw["path"])
    raw_data = read(raw_path)
    image_dir = raw_path.with_suffix("")
    pngs = sorted(image_dir.glob("*.png"))
    boundary = capture["boundary"]
    assert raw_path.is_file() and sha(raw_path) == raw["sha256"]
    assert len(raw_data["frames"]) == len(pngs) == boundary["retainedFrameCount"]
    if label in {"pass", "attack"}:
        assert raw_data["ok"] is True
        assert boundary["completeCapture"] is True and boundary["rawCaptureOk"] is True
        assert boundary["retainedFrameCount"] == 120 and boundary["termination"] is None
    elif label == "kill-fixture":
        assert raw_data["ok"] is False
        assert boundary["completeCapture"] is False and boundary["rawCaptureOk"] is False
        assert boundary["allowRendererDestroyedPrefix"] is True
        assert boundary["retainedFrameCount"] == 85 and boundary["termination"] == "renderer_destroyed"
    else:
        raise AssertionError(label)
    before = enemy_snapshot(action["before"], case["enemy"])
    after = enemy_snapshot(action["after"], case["enemy"])
    if label == "attack":
        assert before["hp"] == 324 and after["hp"] == 316 and after["alive"] is True
    if label == "kill-fixture":
        assert before["hp"] == 316 and after["hp"] == 0 and after["alive"] is False
    for path in [raw_path, Path(action["journal"]["path"]), Path(action["rawResult"]["path"])]:
        add_source(sources, path)
    for png in pngs:
        add_source(sources, png)
        image_pins[relative(png)] = sha(png)
    capture_records.append({
        "label": label,
        "captureId": raw_path.stem,
        "rawCapture": {"source": relative(raw_path), "sha256": sha(raw_path)},
        "frames": len(pngs),
        "complete": boundary["completeCapture"],
        "rawCaptureOk": boundary["rawCaptureOk"],
        "termination": boundary["termination"],
        "limitation": boundary["limitation"],
        "dimensions": {"width": raw_data["width"], "height": raw_data["height"], "requestedFps": raw_data["requestedFps"]},
        "enemyHpBefore": before["hp"],
        "enemyHpAfter": after["hp"],
    })
assert [(item["label"], item["frames"], item["complete"]) for item in capture_records] == [
    ("pass", 120, True), ("attack", 120, True), ("kill-fixture", 85, False),
]

claim_path = Path(case["claim"]["path"])
assert sha(claim_path) == case["claim"]["sha256"]
add_source(sources, claim_path)

metadata = []
for source in sorted(sources):
    if source.suffix.lower() == ".png":
        continue
    raw = source.read_bytes()
    destination = OUT / "metadata" / (relative(source) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    metadata.append({
        "source": relative(source),
        "sourceSha256": sha(source),
        "archive": str(destination.relative_to(OUT)),
        "archiveSha256": sha(destination),
        "encoding": "gzip-lossless",
    })

selected = []
observations = {
    ("pass", 12): "Idle/pass sample. The V4 eye is a small teal form under the authored head, replacing the earlier oversized native pink-orange static surface.",
    ("pass", 24): "Second idle/pass sample. It confirms the sampled camera read only; it does not accept all camera angles or animation intervals.",
    ("attack", 6): "Ordinary no-focus attack sample from the accepted 324 to 316 HP capture.",
    ("attack", 12): "Second ordinary attack sample. The linked skinned head is the motion source; the static eye has separate inventory evidence.",
    ("kill-fixture", 0): "First frame of the explicit KillSingle fixture prefix, not ordinary lethal damage.",
    ("kill-fixture", 84): "Last retained frame before native renderer destruction stopped the capture.",
}
for frame in case["selectedFrames"]:
    source = Path(frame["path"])
    assert sha(source) == frame["sha256"]
    destination = OUT / "selected" / f"{frame['action']}-{frame['index']:04d}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha(destination) == frame["sha256"]
    selected.append({
        "source": relative(source),
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "action": frame["action"],
        "index": frame["index"],
        "observation": observations[(frame["action"], frame["index"])],
    })
assert len(selected) == 6

videos = []
for capture in capture_records:
    output = OUT / "video" / f"{capture['label']}.mp4"
    output.parent.mkdir(parents=True, exist_ok=True)
    source_dir = BASE / capture["captureId"]
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(source_dir / "%04d.png"), "-frames:v", str(capture["frames"]),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(output),
    ], check=True)
    probe = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(output),
    ]))["streams"][0]
    assert int(probe["nb_read_frames"]) == capture["frames"]
    videos.append({
        "label": capture["label"],
        "path": str(output.relative_to(OUT)),
        "sha256": sha(output),
        "frames": capture["frames"],
        "width": int(probe["width"]),
        "height": int(probe["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative. Raw capture metadata and PNG hashes preserve the evidence timing record.",
    })

write(OUT / "source-image-pins.json", image_pins)
write(OUT / "asset-pins.json", source_assets)
validation = {
    "status": "fresh_v4_modern_kraken_head_and_static_eye_exact_binding_motion_gameplay_ready_observed_practical_static_art_unapproved",
    "scope": "One fresh V4 Kraken-head trial. It validates the exact skinned head plus rigid eye renderer assignment, material state, sampled parent motion, ordinary hit, explicit fixture-death prefix, and Ready progression. It is not a finished-art approval.",
    "native_chassis": "krakenHead",
    "enemy_id": "krakenHead",
    "renderer_paths": ["kraken2", STATIC_PATH],
    "source_renderer_ids": [121035, 101310],
    "provenance": {
        "profile": profile,
        "catalog": {"path": relative(CATALOG), "sha256": sha(CATALOG)},
        "stageReceipt": {"path": relative(STAGE_RECEIPT), "sha256": sha(STAGE_RECEIPT)},
        "deploymentReceipt": {"path": relative(DEPLOYMENT_RECEIPT), "sha256": sha(DEPLOYMENT_RECEIPT)},
        "restoreAfterProbe": {
            "stageReceipt": {"path": relative(RESTORE_STAGE_RECEIPT), "sha256": sha(RESTORE_STAGE_RECEIPT)},
            "deploymentReceipt": {"path": relative(RESTORE_DEPLOYMENT_RECEIPT), "sha256": sha(RESTORE_DEPLOYMENT_RECEIPT)},
            "catalogOnly": True,
            "assetChanges": {},
        },
    },
    "session": SESSION,
    "case": {"path": relative(case_path), "sha256": sha(case_path), "status": case["status"]},
    "stageCase": {"path": relative(stage_result_path), "sha256": sha(stage_result_path), "status": stage_result["status"]},
    "bindings": {
        "skinnedHead": {
            "rendererKind": case["initialRenderer"]["rendererKind"],
            "celRelativeRendererPath": "kraken2",
            "mesh": case["initialRenderer"]["mesh"],
            "boneSignature": case["initialRenderer"]["boneSignature"],
            "controller": case["initialRenderer"]["animator"]["controller"],
            "material": compact_material(case["initialRenderer"]["materials"][0]),
        },
        "staticEye": {
            "rendererKind": "MeshRenderer",
            "celRelativeRendererPath": STATIC_PATH,
            "mesh": "ftkmf_static_glb_abyssal-crown-kraken-eye-v4.glb",
            "requiredMeshFilterCount": 1,
            "material": inventory_records[0]["static"]["material"],
            "inventoryRecords": inventory_records,
        },
    },
    "captures": capture_records,
    "ordinaryAttack": case["attackRetrySummary"],
    "ready": case["finalReady"]["strictReady"],
    "assets": source_assets,
    "selectedFrames": selected,
    "videos": videos,
    "visualReview": {
        "status": "practical_static_replacement_unapproved_art",
        "observed": "The original V4 static mesh removes the large native pink-orange foreground dome that remained after the skinned head swap. At the sampled combat camera it reads as a small dark teal lower-eye form under the authored head.",
        "notAccepted": "The eye does not yet read as a fully integrated final character feature. The sampled views do not establish all animation intervals, camera angles, culling, or general art approval.",
        "placementFinding": "The later original marker probe found that offsets deeper into the skinned head are occluded; the safe visible region is near the static child pivot. The probe is preserved separately and does not upgrade this art verdict.",
    },
    "limitations": [
        "KillSingle is an explicit fixture death, not normal lethal damage.",
        "The kill capture stops after 85 retained frames because the renderer is destroyed; it is a death prefix only.",
        "The linked skinned renderer supplies motion capture. Static inventory confirms the rigid target under sampled Intro and Attack states, not all animation or culling behavior.",
        "No portrait, full resource-disposal, all-camera, or final-art acceptance is claimed.",
    ],
}
write(OUT / "validation.json", validation)

integrity = {
    "status": "PASS",
    "metadata": metadata,
    "sourceImages": {"count": len(image_pins), "pins": "source-image-pins.json"},
    "selected": selected,
    "videos": videos,
    "validationSha256": sha(OUT / "validation.json"),
    "assetPinsSha256": sha(OUT / "asset-pins.json"),
    "sourceImagePinsSha256": sha(OUT / "source-image-pins.json"),
}
write(OUT / "integrity.json", integrity)

for row in metadata:
    archived = OUT / row["archive"]
    source = ROOT / row["source"]
    assert sha(archived) == row["archiveSha256"]
    assert hashlib.sha256(gzip.decompress(archived.read_bytes())).hexdigest() == row["sourceSha256"] == sha(source)
for source, digest in image_pins.items():
    assert sha(ROOT / source) == digest
for row in selected:
    assert sha(OUT / row["archive"]) == row["sha256"]
for row in videos:
    assert sha(OUT / row["path"]) == row["sha256"]
print(json.dumps({"status": "PASS", "archive": str(OUT), "metadata": len(metadata), "sourceImages": len(image_pins), "videos": len(videos)}, indent=2))
