#!/usr/bin/env python3
"""Archive the fresh V2 primary and mirrored Kraken-tentacle trials.

The archive contains reproducible metadata and review derivatives, never a game
binary or copied game payload.  It refuses to overwrite completed evidence
unless a maintainer explicitly sets FTK_ARCHIVE_REBUILD=1.
"""
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
OUT = ASSET / "live-validation-v2-kraken-tentacles"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
CATALOG = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
CATALOG_SHA256 = "51f22c64607feebb3241961023b7c642c3e30f30b3b7f5478794da399253df79"
BONE_SIGNATURE = "01c1b372052d71050f30a65512c4578a217e10ea3a9e777e7a985a0314ed78bd"
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}

TRIALS = {
    "primary": {
        "nativeChassis": "krakenTentacle",
        "enemy": "ftkmf_modeltest_sargassum_kraken_tentacle",
        "controller": "krakenTentacleController",
        "case": "case-928f9307866843bda5491dd90e09e097",
        "stage": "case-a584863a0aff41ba966caf615abe5b1b",
        "session": "85a00fa867f5444484e9588353a866f1",
        "manifest": ROOT / "scratch" / "coverage-batch-20260911-213630.json",
        "attack": {"beforeHp": 162, "afterHp": 154},
    },
    "mirror": {
        "nativeChassis": "krakenTentacleMirror",
        "enemy": "ftkmf_modeltest_sargassum_kraken_tentacle_mirror",
        "controller": "krakenTentacleControllerMirrored",
        "case": "case-87312325b15348e2a01e29037343c87b",
        "stage": "case-eb667e4a4ca34c88984e90c6cc54e0f1",
        "session": "3cff64dbfc4343d0a06534ee937a0078",
        "manifest": ROOT / "scratch" / "coverage-batch-20260911-213903.json",
        "attack": {"beforeHp": 162, "afterHp": 152},
    },
}

AUTHORING = [
    "runtime-profiles-v2.json",
    "build-report-v2.json",
    "build_geometry-v2.py",
    "build_blender-v2.py",
    "verify_original_geometry-v2.py",
    "verify_target_bindings-v2.py",
    "stage_runtime_profiles-v2.py",
    "deploy_runtime_profiles-v2.py",
    "sargassum-kraken-tentacle-v2.glb",
    "sargassum-kraken-tentacle-v2.png",
    "sargassum-kraken-tentacle-v2.source.json",
    "sargassum-kraken-tentacle-v2.pieces.json",
    "sargassum-kraken-tentacle-v2.validation.json",
    "sargassum-kraken-tentacle-v2-reopened.glb",
    "sargassum-kraken-tentacle-v2-reopened.png",
    "sargassum-kraken-tentacle-v2-reopened.source.json",
    "sargassum-kraken-tentacle-v2-reopened.validation.json",
    "sargassum-kraken-tentacle-v2-reopened-validation.json",
    "sargassum-kraken-tentacle-v2.blend",
    "sargassum-kraken-tentacle-v2-studio.blend",
    "sargassum-kraken-tentacle-v2-hero.png",
    "sargassum-kraken-tentacle-v2-side.png",
]
STAGE_RECEIPT = ROOT / "scratch" / "runtime-profile-418-abyssal-kraken-v2" / "receipt.json"
DEPLOYMENT_RECEIPT = ROOT / "scratch" / "mirewarden-game" / "deployment-backups" / "abyssal-kraken-v2-20260911-194317" / "deployment.json"
ASSETS = {
    "sargassum-kraken-tentacle-v2.glb": "6815b322fd2b3e22d067f3822aaf1ed90a9de4949f3d55093f9d95e4ab8084d7",
    "sargassum-kraken-tentacle-v2.png": "93f244d48f6dd53e3b6ff7ed8d4ec10deb8fdddb6b3ed3618bb0d17d2ad12639",
}


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


def assert_renderer(renderer: dict, trial: dict) -> None:
    assert renderer["ownerRootName"] == "Enemy Dummy"
    assert renderer["celRootName"] == "enKrakenTentacleNew(Clone)"
    assert renderer["celRelativeRendererPath"] == "krakenTentacle"
    assert renderer["rendererKind"] == "SkinnedMeshRenderer"
    assert renderer["mesh"] == "ftkmf_glb_sargassum-kraken-tentacle-v2.glb"
    assert renderer["boneSignature"] == BONE_SIGNATURE
    assert renderer["rootBone"] == "Root_M"
    assert renderer["animator"]["controller"] == trial["controller"]
    assert renderer["resourceLease"]["available"] is True
    assert renderer["resourceLease"]["present"] is True
    assert renderer["resourceLease"]["acquired"] is True
    assert renderer["resourceLease"]["applied"] is True
    assert len(renderer["materials"]) == 1
    material = renderer["materials"][0]
    assert material["emissionKeyword"] is False
    assert material["_EmissionMap"] is None
    assert material["_MainTex"]["name"] == "ftkmf_sargassum-kraken-tentacle-v2.png"


def expected_profile(trial: dict) -> dict:
    return {
        "key": trial["enemy"],
        "baseEnemy": trial["nativeChassis"],
        "displayName": "Sargassum Lash V2" if trial["nativeChassis"] == "krakenTentacle" else "Sargassum Lash V2, Mirrored",
        "combatProfile": "c415ffbaa3d1977a40283b36d0b9bea4fbd2295df45de7b16daa9a7cdb668386" if trial["nativeChassis"] == "krakenTentacle" else "fa962d81c18292667cd1fee784ca2336f1a9308e3cf0db645dc6979324c246bf",
        "renderers": [{
            "rendererPath": "krakenTentacle",
            "glbFile": "sargassum-kraken-tentacle-v2.glb",
            "textureFile": "sargassum-kraken-tentacle-v2.png",
        }],
    }


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "Refusing to overwrite a completed archive."
assert sha(CATALOG) == CATALOG_SHA256
assert {name: sha(ASSET / name) for name in ASSETS} == ASSETS
GAME_MODELS = ROOT / "scratch" / "mirewarden-game" / "BepInEx" / "plugins" / "FTKModFramework_content" / "models"
assert {name: sha(GAME_MODELS / name) for name in ASSETS} == ASSETS

catalog = read(CATALOG)
assert catalog["version"] == 1
for trial in TRIALS.values():
    assert next(row for row in catalog["profiles"] if row["key"] == trial["enemy"]) == expected_profile(trial)

stage_receipt = read(STAGE_RECEIPT)
deployment = read(DEPLOYMENT_RECEIPT)
assert stage_receipt["status"] == "STAGED_NOT_DEPLOYED_ABYSSAL_KRAKEN_V2_CORRECTION"
assert stage_receipt["newAssets"] == {
    **{name: stage_receipt["newAssets"][name] for name in stage_receipt["newAssets"] if name not in ASSETS},
    **ASSETS,
}
assert deployment["status"] == "VERIFIED_COMPLETE"
assert {Path(name).name: digest for name, digest in deployment["new"].items() if Path(name).name in ASSETS} == ASSETS

sources: set[Path] = set()
for path in [Path(__file__), CATALOG, STAGE_RECEIPT, DEPLOYMENT_RECEIPT]:
    add_source(sources, path)
for name in AUTHORING:
    add_source(sources, ASSET / name)

trial_records = {}
image_pins: dict[str, str] = {}
selected = []
videos = []
for label, trial in TRIALS.items():
    case_path = BASE / trial["case"] / "case-result.json"
    stage_path = BASE / trial["stage"] / "result.json"
    session_path = BASE / f"new-run-session-{trial['session']}.json"
    case = read(case_path)
    stage = read(stage_path)
    session = read(session_path)
    manifest = read(trial["manifest"])
    assert case["schema"] == "ftkmf.exercise-case.v1"
    assert case["session"] == trial["session"]
    assert case["enemy"] == trial["enemy"]
    assert case["profileSha256"] == CATALOG_SHA256
    assert case["status"] == "needs_visual_review"
    assert case["focusedAttack"] is False
    assert_renderer(case["initialRenderer"], trial)
    assert case["finalReady"]["strictReady"] == {"ok": True, "level": 0, "room": 2, "buttonCount": 1}
    assert case["attackRetrySummary"] == [{
        "attempt": 1,
        "status": "nonlethal_hp_loss",
        "beforeHp": trial["attack"]["beforeHp"],
        "afterHp": trial["attack"]["afterHp"],
        "boundary": "Observed same-target HP only; no inferred block, dodge, hit animation, or damage source.",
    }]
    assert session == {"session": trial["session"], "case": trial["stage"].removeprefix("case-"), "journal": str(BASE / trial["stage"] / "journal.jsonl")}
    assert stage["status"] == "binding_metadata_observed"
    assert stage["session"] == trial["session"] and stage["enemy"] == trial["enemy"]
    assert [(row["rendererPath"], row["glbFile"], row["boneSignature"]) for row in stage["matches"]] == [
        ("krakenTentacle", "sargassum-kraken-tentacle-v2.glb", BONE_SIGNATURE),
    ]
    assert len(manifest) == 1 and manifest[0]["case"] == trial["case"] and manifest[0]["session"] == trial["session"]
    assert manifest[0]["profile"] == trial["enemy"] and manifest[0]["error"] is None

    for path in [case_path, BASE / trial["case"] / "journal.jsonl", stage_path, BASE / trial["stage"] / "journal.jsonl", session_path, Path(session["journal"]), trial["manifest"]]:
        add_source(sources, path)

    captures = []
    for action in case["actions"]:
        action_label = action["action"]
        capture = action["capture"]
        raw = capture["rawCapture"]
        raw_path = Path(raw["path"])
        raw_data = read(raw_path)
        frame_dir = raw_path.with_suffix("")
        pngs = sorted(frame_dir.glob("*.png"))
        boundary = capture["boundary"]
        assert raw_path.is_file() and sha(raw_path) == raw["sha256"]
        assert len(raw_data["frames"]) == len(pngs) == boundary["retainedFrameCount"]
        before = enemy_snapshot(action["before"], trial["enemy"])
        after = enemy_snapshot(action["after"], trial["enemy"])
        if action_label in {"pass", "attack"}:
            assert raw_data["ok"] is True
            assert boundary["completeCapture"] is True and boundary["rawCaptureOk"] is True
            assert boundary["retainedFrameCount"] == 120 and boundary["termination"] is None
        elif action_label == "kill-fixture":
            assert raw_data["ok"] is False
            assert boundary["completeCapture"] is False and boundary["rawCaptureOk"] is False
            assert boundary["allowRendererDestroyedPrefix"] is True
            assert boundary["retainedFrameCount"] == 91 and boundary["termination"] == "renderer_destroyed"
        else:
            raise AssertionError(action_label)
        if action_label == "pass":
            assert (before["hp"], after["hp"], before["alive"], after["alive"]) == (162, 162, True, True)
        elif action_label == "attack":
            assert (before["hp"], after["hp"], before["alive"], after["alive"]) == (trial["attack"]["beforeHp"], trial["attack"]["afterHp"], True, True)
        else:
            assert (before["hp"], after["hp"], before["alive"], after["alive"]) == (trial["attack"]["afterHp"], 0, True, False)
        for path in [raw_path, Path(action["journal"]["path"]), Path(action["rawResult"]["path"])]:
            add_source(sources, path)
        for png in pngs:
            add_source(sources, png)
            image_pins[relative(png)] = sha(png)
        captures.append({
            "label": action_label,
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
        video = OUT / "video" / f"{label}-{action_label}.mp4"
        video.parent.mkdir(parents=True, exist_ok=True)
        subprocess.run([
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
            "-i", str(frame_dir / "%04d.png"), "-frames:v", str(len(pngs)),
            "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(video),
        ], check=True)
        probe = json.loads(subprocess.check_output([
            "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
            "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(video),
        ]))["streams"][0]
        assert int(probe["nb_read_frames"]) == len(pngs)
        videos.append({
            "trial": label,
            "label": action_label,
            "path": str(video.relative_to(OUT)),
            "sha256": sha(video),
            "frames": len(pngs),
            "width": int(probe["width"]),
            "height": int(probe["height"]),
            "playbackFps": 12,
            "timing": "Presentation derivative. Raw capture metadata and PNG hashes preserve the evidence timing record.",
        })
    assert [(item["label"], item["frames"], item["complete"]) for item in captures] == [
        ("pass", 120, True), ("attack", 120, True), ("kill-fixture", 91, False),
    ]

    claim_path = Path(case["claim"]["path"])
    assert sha(claim_path) == case["claim"]["sha256"]
    add_source(sources, claim_path)
    for frame in case["selectedFrames"]:
        source = Path(frame["path"])
        assert sha(source) == frame["sha256"]
        destination = OUT / "selected" / f"{label}-{frame['action']}-{frame['index']:04d}.png"
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        assert sha(destination) == frame["sha256"]
        selected.append({
            "trial": label,
            "source": relative(source),
            "archive": str(destination.relative_to(OUT)),
            "sha256": sha(destination),
            "action": frame["action"],
            "index": frame["index"],
            "observation": "Sampled original V2 tentacle render. It is visual evidence for this frame only and does not grant an all-camera or final-art verdict.",
        })
    assert len(case["selectedFrames"]) == 6
    trial_records[label] = {
        "nativeChassis": trial["nativeChassis"],
        "enemy": trial["enemy"],
        "profile": expected_profile(trial),
        "session": trial["session"],
        "case": {"path": relative(case_path), "sha256": sha(case_path), "status": case["status"]},
        "stageCase": {"path": relative(stage_path), "sha256": sha(stage_path), "status": stage["status"]},
        "coverageManifest": {"path": relative(trial["manifest"]), "sha256": sha(trial["manifest"])},
        "binding": {
            "rendererKind": case["initialRenderer"]["rendererKind"],
            "celRelativeRendererPath": "krakenTentacle",
            "mesh": case["initialRenderer"]["mesh"],
            "boneSignature": case["initialRenderer"]["boneSignature"],
            "controller": case["initialRenderer"]["animator"]["controller"],
            "material": compact_material(case["initialRenderer"]["materials"][0]),
            "lease": case["initialRenderer"]["resourceLease"],
        },
        "captures": captures,
        "ordinaryAttack": case["attackRetrySummary"],
        "ready": case["finalReady"]["strictReady"],
    }

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

for name in AUTHORING:
    source = ASSET / name
    if source.suffix.lower() == ".png":
        image_pins[relative(source)] = sha(source)

write(OUT / "asset-pins.json", ASSETS)
write(OUT / "source-image-pins.json", image_pins)
validation = {
    "status": "fresh_v4_catalog_sargassum_lash_v2_exact_primary_and_mirrored_bindings_motion_gameplay_ready_observed_art_unapproved",
    "scope": "Two fresh isolated V2 Kraken-tentacle trials against the current catalog. Each exact native chassis is tested separately for source binding, its controller, sampled motion, one ordinary hit, fixture-death prefix, and Ready progression. This is not final-art approval.",
    "renderer_paths": ["krakenTentacle"],
    "source_renderer_ids": [121595],
    "provenance": {
        "catalog": {"path": relative(CATALOG), "sha256": sha(CATALOG)},
        "initialV2StageReceipt": {"path": relative(STAGE_RECEIPT), "sha256": sha(STAGE_RECEIPT)},
        "initialV2DeploymentReceipt": {"path": relative(DEPLOYMENT_RECEIPT), "sha256": sha(DEPLOYMENT_RECEIPT)},
        "currentGameAssets": ASSETS,
    },
    "trials": trial_records,
    "assets": ASSETS,
    "selectedFrames": selected,
    "videos": videos,
    "visualReview": {
        "status": "rendered_in_live_combat_art_unapproved",
        "observed": "Both exact controller rows render the original V2 model in the live isolated combat camera and retain separate sampled frame evidence.",
        "notAccepted": "The captures do not establish all camera angles, animation intervals, culling behavior, portrait framing, or final aesthetic approval.",
    },
    "limitations": [
        "Each ordinary attack records same-target HP only; no block, dodge, hit animation, or damage source is inferred.",
        "KillSingle is an explicit fixture, not normal lethal gameplay. Each death capture stops after 91 retained frames because the renderer is destroyed.",
        "The two controllers are independently observed but share one source renderer identity and one authored V2 asset pair.",
        "No portrait, full resource-disposal, all-camera, all-animation, culling, or final-art acceptance is claimed.",
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
