#!/usr/bin/env python3
"""Build the immutable Wildbloom native-preview evidence archive.

This archive preserves a fresh native Party Select observation for the original
Wildbloom Herbalist package. It only packages authored assets, recorded command
metadata, and review derivatives; it never copies a game binary, Unity asset,
native mesh, or local reference data.
"""
from __future__ import annotations

import gzip
import hashlib
import json
import os
from pathlib import Path
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents
            if (path / "FTKModFramework").is_dir() and (path / "tools").is_dir())
ASSET = ROOT / "art-experiments" / "wildbloom-herbalist"
OUT = ASSET / "live-validation-v1"
GAME = ROOT / "scratch" / "mirewarden-game"
BASE = GAME / "model-test-output"
PLAYER_CATALOG = GAME / "model-test-player-profiles.json"
PLAYER_REGISTRATION = GAME / "model-test-player-registration.json"
STAGE = ROOT / "scratch" / "wildbloom-herbalist-stage" / "receipt.json"
DEPLOYMENT = GAME / "deployment-backups" / "wildbloom-herbalist-20260912-070230" / "deployment.json"

PROFILE_KEY = "ftkmf_modeltest_player_wildbloom_herbalist_female"
CLASS_ID = 114
SKINSET = "herbalist_Female"
PLAYER_CATALOG_SHA256 = "eb04ac829876fd3bbe94de0e4d4f8e393c4714605301a684d95b8e48e17e1c99"
PREVIEW_SESSION = "4cae860e36f640099f5a2dbf161fc7db"
PREVIEW_OWNER_ID = -19002
PREVIEW_CEL_ID = -262668
PREVIEW_BODY_RENDERER_ID = -262686
PREVIEW_BODY_SIGNATURE = "33ef1d59071b4cb809e65f6c63ea4d925e2f7dc75a0703033e7e8177f9137149"

RAW = {
    "nativeCreateInitial": "7b31120ffb2143c686fc682cbb4a56c9",
    "previewState": "30fdc6b162294458b35866533046d4e2",
    "previewInventory": "60bef7c1b9834412a5ac80c5cb0323c8",
    "previewIdle": "f361cc19fdcd47fabd4e37e3ca7c216a",
}
RAW_PATHS = {name: BASE / f"{identifier}.json" for name, identifier in RAW.items()}

ASSETS = {
    "wildbloom-body.glb": "e2ac7bb1bcb6668d1d210791f69519f70ae710a93a119dd1989f2be514f4f29b",
    "wildbloom-hair-top.glb": "ffc5cd75b06e36a77fae54462764e068ca8e741330d581788a775f0ec6f4dbbd",
    "wildbloom-hair-bottom.glb": "e1382cddc4bcb069856bc4aef41bb98dfe507e76a5e3d0ac528e5f3ea7db3e8e",
    "wildbloom-palette.png": "a953dd503880c8a719a45b90cdf9004f3db715e740910022da0d7d10f4f3cbd7",
}
EXPECTED_CUSTOM = {
    "player_Herbalist": "ftkmf_glb_wildbloom-body.glb",
    "hairTop": "ftkmf_glb_wildbloom-hair-top.glb",
    "hairBottom": "ftkmf_glb_wildbloom-hair-bottom.glb",
}
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
AUTHORING = [
    "README.md", "manifest.json", "runtime-profile.json", "build_geometry.py",
    "verify_original_geometry.py", "render_studio.py", "build-report.json",
    "original-geometry-proof.json",
    "wildbloom-body.glb", "wildbloom-body.source.json", "wildbloom-body.pieces.json", "wildbloom-body.validation.json",
    "wildbloom-hair-top.glb", "wildbloom-hair-top.source.json", "wildbloom-hair-top.pieces.json", "wildbloom-hair-top.validation.json",
    "wildbloom-hair-bottom.glb", "wildbloom-hair-bottom.source.json", "wildbloom-hair-bottom.pieces.json", "wildbloom-hair-bottom.validation.json",
    "wildbloom-palette.png", "wildbloom-hero.png", "wildbloom-side.png",
]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict | list:
    return json.loads(path.read_text())


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def relative(path: Path) -> str:
    return str(path.resolve().relative_to(ROOT))


def source_record(path: Path) -> dict:
    return {"source": relative(path), "sha256": sha(path)}


def add_source(sources: set[Path], path: Path) -> None:
    value = path.resolve()
    assert value.is_file() and not value.is_symlink(), value
    assert value.is_relative_to(ROOT), value
    assert value.suffix.lower() not in FORBIDDEN_SUFFIXES, value
    sources.add(value)


def profile() -> dict:
    document = read(ASSET / "runtime-profile.json")
    assert isinstance(document, dict) and document["version"] == 1
    rows = document["profiles"]
    assert isinstance(rows, list) and len(rows) == 1
    return rows[0]


def active_clips(frame: dict) -> set[str]:
    result: set[str] = set()
    for layer in (frame.get("animator") or {}).get("layers", []):
        for clip in layer.get("playing", []):
            if isinstance(clip.get("name"), str):
                result.add(clip["name"])
    return result


def video_from_frames(frame_dir: Path, frame_count: int, label: str) -> dict:
    output = OUT / "video" / f"{label}.mp4"
    output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(frame_dir / "%04d.png"), "-frames:v", str(frame_count),
        "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(output),
    ], check=True)
    probe = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(output),
    ]))["streams"][0]
    assert int(probe["nb_read_frames"]) == frame_count
    return {
        "label": label, "path": str(output.relative_to(OUT)), "sha256": sha(output),
        "frames": frame_count, "width": int(probe["width"]), "height": int(probe["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative. Raw capture metadata and source-image hashes preserve the measured timing record.",
    }


def select_frame(raw_id: str, index: int, label: str, observation: str) -> dict:
    source = BASE / raw_id / f"{index:04d}.png"
    destination = OUT / "selected" / f"{label}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha(source) == sha(destination)
    return {
        "label": label, "source": relative(source), "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination), "observation": observation,
    }


if (OUT / "validation.json").exists() and os.environ.get("FTK_ARCHIVE_REBUILD") != "1":
    raise AssertionError("Refusing to overwrite a completed archive. Set FTK_ARCHIVE_REBUILD=1 to rebuild intentionally.")

assert sha(PLAYER_CATALOG) == PLAYER_CATALOG_SHA256
assert {name: sha(ASSET / name) for name in ASSETS} == ASSETS
GAME_MODELS = GAME / "BepInEx" / "plugins" / "FTKModFramework_content" / "models"
assert {name: sha(GAME_MODELS / name) for name in ASSETS} == ASSETS

PROFILE = profile()
assert PROFILE["key"] == PROFILE_KEY and PROFILE["skinset"] == SKINSET
catalog = read(PLAYER_CATALOG)
assert next(row for row in catalog["profiles"] if row["key"] == PROFILE_KEY) == PROFILE
registration = read(PLAYER_REGISTRATION)
registered = next(row for row in registration["registered"] if row["key"] == PROFILE_KEY)
assert registered["id"] == CLASS_ID and registered["skinset"] == SKINSET

stage = read(STAGE)
deployment = read(DEPLOYMENT)
assert stage["status"] == "STAGED_NOT_DEPLOYED_CUSTOM_MODEL_V1"
assert stage["catalog"]["sha256"] == PLAYER_CATALOG_SHA256 and stage["newAssets"] == ASSETS
assert deployment["status"] == "VERIFIED_COMPLETE"
assert deployment["new"]["model-test-player-profiles.json"] == PLAYER_CATALOG_SHA256
assert deployment["review"]["liveAcceptance"] is False

native_create = read(RAW_PATHS["nativeCreateInitial"])
preview_state = read(RAW_PATHS["previewState"])
preview_inventory = read(RAW_PATHS["previewInventory"])
preview_idle = read(RAW_PATHS["previewIdle"])
assert all(document["session"] == PREVIEW_SESSION for document in (native_create, preview_state, preview_inventory, preview_idle))
assert native_create["ok"] is True and native_create["status"] == "native_create_game_reached_actual_character_create"
assert native_create["method"] == "StartGameFE.GameConfig.OnStartGame"
assert native_create["mapReady"] is True and native_create["characterCreateRootActive"] is True
initial_owner = next(row for row in native_create["candidates"] if row["turnIndex"] == 0)
assert initial_owner["ownerInstanceId"] == PREVIEW_OWNER_ID

assert preview_state["ok"] is True and preview_state["status"] == "observed_actual_native_player_preview"
assert preview_state["classKey"] == PROFILE_KEY and preview_state["classId"] == CLASS_ID
assert preview_state["classLabel"] == "Wildbloom Herbalist" and preview_state["skinset"] == SKINSET
assert preview_state["catalogSha256"] == PLAYER_CATALOG_SHA256
assert preview_state["ownerInstanceId"] == PREVIEW_OWNER_ID and preview_state["celInstanceId"] == PREVIEW_CEL_ID
assert preview_state["nativeMenuMembership"] is True and preview_state["reciprocalPreviewReference"] is True
assert preview_state["parentIsNativePedestal"] is True
assert preview_state["lease"]["present"] is True and preview_state["lease"]["references"] == 1
assert {row["file"]: row["sha256"] for row in preview_state["assetFiles"]} == ASSETS
state_renderers = {row["celRelativeRendererPath"]: row for row in preview_state["renderers"]}
assert EXPECTED_CUSTOM.keys() <= state_renderers.keys()
assert {path: state_renderers[path]["mesh"] for path in EXPECTED_CUSTOM} == EXPECTED_CUSTOM
assert all(state_renderers[path]["active"] is True and state_renderers[path]["enabled"] is True
           and state_renderers[path]["isVisible"] is True for path in EXPECTED_CUSTOM)

assert preview_inventory["ok"] is True and preview_inventory["scope"] == "player-preview"
body_rows = [row for row in preview_inventory["renderers"]
             if row.get("ownerInstanceId") == PREVIEW_OWNER_ID and row.get("rendererPath") == "@cel/player_Herbalist"]
assert len(body_rows) == 1
body = body_rows[0]
assert body["instanceId"] == PREVIEW_BODY_RENDERER_ID and body["celInstanceId"] == PREVIEW_CEL_ID
assert body["mesh"] == EXPECTED_CUSTOM["player_Herbalist"] and body["boneSignature"] == PREVIEW_BODY_SIGNATURE
assert body["active"] is True and body["enabled"] is True and body["isVisible"] is True

frames = preview_idle["frames"]
frame_dir = RAW_PATHS["previewIdle"].with_suffix("")
pngs = sorted(frame_dir.glob("*.png"))
assert preview_idle["ok"] is True and preview_idle["error"] is None
assert preview_idle["scope"] == "player-preview" and preview_idle["timingMode"] == "offline-fixed-step-gameplay"
assert preview_idle["fixedStep"] is True and preview_idle["requestedSeconds"] == 2.0 and preview_idle["requestedFps"] == 12.0
assert preview_idle["ownerInstanceId"] == PREVIEW_OWNER_ID and preview_idle["celInstanceId"] == PREVIEW_CEL_ID
assert len(frames) == len(pngs) == 24
assert all(frame["ownerKind"] == "player-preview" and frame["ownerInstanceId"] == PREVIEW_OWNER_ID
           and frame["celInstanceId"] == PREVIEW_CEL_ID and frame["instanceId"] == PREVIEW_BODY_RENDERER_ID
           and frame["celRelativeRendererPath"] == "player_Herbalist" and frame["mesh"] == EXPECTED_CUSTOM["player_Herbalist"]
           and frame["boneSignature"] == PREVIEW_BODY_SIGNATURE and frame["active"] is True
           and frame["enabled"] is True and frame["isVisible"] is True and frame["captureFramerate"] == 12
           and "standardIdle_handsDown" in active_clips(frame) for frame in frames)
assert frames[-1]["gameSeconds"] >= 1.8

summary_path = ROOT / "scratch" / "wildbloom-preview-idle-settled-capture-summary.json"
summary = read(summary_path)
assert summary["captureId"] == RAW["previewIdle"] and summary["frameCount"] == 24
assert summary["meshIdentityStable"] is True and summary["allScreenshotsPresent"] is True
assert summary["timingStatus"] == "measured_progress"

SOURCES: set[Path] = set()
IMAGE_PINS: dict[str, str] = {}
for path in [
    Path(__file__), OUT / "README.md", PLAYER_CATALOG, PLAYER_REGISTRATION, STAGE, DEPLOYMENT,
    ROOT / "tools/ai-model-pipeline/stage_custom_model_profile.py",
    ROOT / "tools/ai-model-pipeline/deploy_custom_model_stage.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/command.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/AvatarInventory.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/PlayerPreviewObservation.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/NativeCreateCharacterScreen.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/README.md",
    ROOT / "tools/ai-model-pipeline/summarize_capture.py",
    ROOT / "skills/ftk-custom-models/SKILL.md",
    ROOT / "scratch/wildbloom-preview-state-request.json",
    ROOT / "scratch/player-preview-inventory-request.json",
    ROOT / "scratch/wildbloom-preview-idle-settled-capture-request.json",
    summary_path, *RAW_PATHS.values(),
]:
    add_source(SOURCES, path)
for name in AUTHORING:
    path = ASSET / name
    add_source(SOURCES, path)
    if path.suffix.lower() == ".png":
        IMAGE_PINS[relative(path)] = sha(path)
for png in pngs:
    add_source(SOURCES, png)
    IMAGE_PINS[relative(png)] = sha(png)

selected = [
    select_frame(RAW["previewIdle"], 12, "native-preview-idle",
                 "Wildbloom Herbalist on the game-owned Player 1 Party Select pedestal during the measured native idle capture."),
]
videos = [video_from_frames(frame_dir, 24, "native-preview-idle")]

metadata = []
for source in sorted(SOURCES):
    if source.suffix.lower() == ".png":
        continue
    raw = source.read_bytes()
    destination = OUT / "metadata" / (relative(source) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw
    metadata.append({
        "source": relative(source), "sourceSha256": sha(source),
        "archive": str(destination.relative_to(OUT)), "archiveSha256": sha(destination),
        "encoding": "gzip-lossless",
    })

capture = {
    "label": "native-preview-idle", "complete": True, "termination": "complete_24_fixed_step_frames",
    "capture": source_record(RAW_PATHS["previewIdle"]), "frameCount": 24,
    "scope": "player-preview", "rendererPath": "player_Herbalist",
    "mesh": EXPECTED_CUSTOM["player_Herbalist"], "boneSignature": PREVIEW_BODY_SIGNATURE,
    "state": "standardIdle_handsDown", "clips": ["standardIdle_handsDown"],
    "timingMode": preview_idle["timingMode"], "actualGameSeconds": frames[-1]["gameSeconds"],
    "summary": source_record(summary_path),
}
validation = {
    "status": "native_character_creation_preview_and_idle_observed",
    "scope": "Fresh isolated Party Select evidence for the original three-mesh Wildbloom Herbalist. The game created its own room, map, character-create UI, pedestal, and preview owner through the real Create Game callback. A visible native Party Select selection then chose Wildbloom; this archive records that strict preview join and its settled body idle capture. It makes no broader player lifecycle or final-art claim.",
    "profile": PROFILE,
    "session": PREVIEW_SESSION,
    "identity": {
        "classId": CLASS_ID, "skinset": SKINSET, "playerCatalogSha256": PLAYER_CATALOG_SHA256,
        "ownerInstanceId": PREVIEW_OWNER_ID, "celInstanceId": PREVIEW_CEL_ID,
    },
    "provenance": {
        "stage": source_record(STAGE), "deployment": source_record(DEPLOYMENT),
        "playerRegistration": source_record(PLAYER_REGISTRATION), "gameAssets": ASSETS,
    },
    "nativeCreateInitial": {
        "status": native_create["status"], "method": native_create["method"], "mapReady": True,
        "characterCreateRootActive": True, "waitedFrames": native_create["waitedFrames"],
        "initialPlayerOneClassLabel": initial_owner["classLabel"],
        "initialPlayerOneOwnerInstanceId": initial_owner["ownerInstanceId"],
        "source": source_record(RAW_PATHS["nativeCreateInitial"]),
        "interpretation": "This source proves the real Create Game route created the native Party Select owner. The later strict preview state and visible capture establish Wildbloom's actual selection.",
    },
    "binding": {
        "ownerKind": "player-preview", "ownerInstanceId": PREVIEW_OWNER_ID, "celInstanceId": PREVIEW_CEL_ID,
        "rendererPath": "player_Herbalist", "mesh": EXPECTED_CUSTOM["player_Herbalist"],
        "boneSignature": PREVIEW_BODY_SIGNATURE, "state": source_record(RAW_PATHS["previewState"]),
        "inventory": source_record(RAW_PATHS["previewInventory"]),
    },
    "avatarOwners": {
        "preview": {
            "status": preview_state["status"], "observedAvatars": 1,
            "ownerInstanceId": PREVIEW_OWNER_ID, "celInstanceId": PREVIEW_CEL_ID,
            "classId": CLASS_ID, "classLabel": preview_state["classLabel"],
            "nativeMenuMembership": True, "reciprocalPreviewReference": True,
            "parentIsNativePedestal": True, "leaseId": preview_state["lease"]["leaseId"],
            "leaseReferences": preview_state["lease"]["references"],
            "visibleSkinnedRenderers": sum(row["isVisible"] for row in preview_state["renderers"]),
            "source": source_record(RAW_PATHS["previewState"]),
        },
    },
    "renderers": [
        {key: row[key] for key in ("celRelativeRendererPath", "mesh", "instanceId", "boneSignature", "active", "enabled", "isVisible")}
        for row in preview_state["renderers"]
    ],
    "captures": [capture], "selectedFrames": selected, "videos": videos,
    "visualReview": {
        "status": "sampled_native_character_creation_preview_reviewed_final_art_unapproved",
        "observed": "The captured game-owned Party Select screen labels Player 1 as Wildbloom Herbalist and shows a complete green, petal-crowned woodland figure. The custom body, crown, and lower hair remain visible while the sampled native idle advances; native Herbalist armor and boots are also present.",
        "notAccepted": "This sampled preview does not establish every camera, culling condition, overworld or combat animation, equipment branch, player death, final-owner teardown, portrait behavior, multiplayer layout, or a final-art verdict.",
    },
    "limitations": [
        "The actual Create Game callback was invoked only after normal visible navigation reached the enabled offline Create Game screen; the helper did not construct a preview avatar or select a class.",
        "Wildbloom was selected through the visible native Party Select UI after that initial screen was created. The strict state and selected capture prove the resulting class; the initial helper result intentionally records its pre-selection default.",
        "The 24-frame idle capture uses fixed simulation steps. Its measured game time establishes the recorded idle interval, not real-time rendering performance or full loop coverage.",
        "No conditional apparel assignment is declared for this profile. Native Herbalist armor and boots visible in this default preview do not establish a custom apparel path.",
    ],
}
write(OUT / "asset-pins.json", ASSETS)
write(OUT / "source-image-pins.json", IMAGE_PINS)
write(OUT / "validation.json", validation)
integrity = {
    "status": "PASS", "validation": {"path": "validation.json", "sha256": sha(OUT / "validation.json")},
    "metadata": metadata, "sourceImages": {"count": len(IMAGE_PINS), "pins": "source-image-pins.json"},
    "selected": selected, "videos": videos,
}
write(OUT / "integrity.json", integrity)
print(json.dumps({
    "status": "PASS", "metadata": len(metadata), "sourceImages": len(IMAGE_PINS),
    "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json"),
}, indent=2))
