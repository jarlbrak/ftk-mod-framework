#!/usr/bin/env python3
"""Build the immutable Hearthveil character-creation preview supplement.

The V1 archive remains the historical combat/equipment/lifecycle trial.  This
V2 supplement preserves the later, separate native Party Select observation:
the game created its own preview avatar and the exact Hearthveil body was
captured while its native idle controller advanced.  It never copies a game
binary, Unity asset bundle, extracted native mesh, or local reference data.
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
ASSET = ROOT / "art-experiments" / "hearthveil-blacksmith"
OUT = ASSET / "live-validation-v2"
GAME = ROOT / "scratch" / "mirewarden-game"
BASE = GAME / "model-test-output"
PLAYER_CATALOG = GAME / "model-test-player-profiles.json"
PLAYER_REGISTRATION = GAME / "model-test-player-registration.json"
STAGE = ROOT / "scratch" / "hearthveil-blacksmith-stage" / "receipt.json"
DEPLOYMENT = GAME / "deployment-backups" / "hearthveil-blacksmith-20260911-225349" / "deployment.json"
NATIVE_CREATE_HELPER_DEPLOYMENT = GAME / "deployment-backups" / "native-create-character-screen-helper-20260912-072751" / "deployment.json"
V1_VALIDATION = ASSET / "live-validation-v1" / "validation.json"
V1_INTEGRITY = ASSET / "live-validation-v1" / "integrity.json"

PROFILE_KEY = "ftkmf_modeltest_player_hearthveil_blacksmith_female"
CLASS_ID = 113
SKINSET = "blacksmith_Female"
PLAYER_CATALOG_SHA256 = "2cb8a092702691735ae2b2900acf4ba37f6050e559048e4f7a10ff91eba7472c"
PREVIEW_SESSION = "67ca8d0d0a704d68a0559a0aa19ac3c4"
PREVIEW_OWNER_ID = -18954
PREVIEW_CEL_ID = -250908
PREVIEW_BODY_RENDERER_ID = -250926
PREVIEW_BODY_SIGNATURE = "c5145eb658efedf4e2e984e600fb9bb3d39843332ada65f8defe30ffcb306b93"

RAW = {
    "nativeCreate": "2772ec7b729a469baf623ebbca179805",
    "previewState": "b9505173816147e7852e77993bbf1010",
    "previewInventory": "401767d3da1342818673e81bf3fa1376",
    "previewIdle": "55d8b24604964d6bab4f7306114525e3",
}
RAW_PATHS = {name: BASE / f"{identifier}.json" for name, identifier in RAW.items()}

ASSETS = {
    "hearthveil-body.glb": "ddb8691011173d8de1a1e48260dd99b9b2d88f91ecabb207bc922286d439f634",
    "hearthveil-hair-top.glb": "40f55b523dd9d2a5827b6e9bdced2dfb70a69faab0a86bd894462ed3c155dbae",
    "hearthveil-hair-bottom.glb": "855233f93cd71ecf1173251723d76def70d3cf8ef33657b4f9003728ffd07973",
    "hearthveil-default-armor.glb": "d0bdb4ee5b1c4afae68d93345df7a9601fc1b628406733cec59ecea396214298",
    "hearthveil-boots.glb": "5b91bc4e437e5b238ca0b3eb8f1c1c83d21110cb4a89f86ed92bbed38e814d16",
    "hearthveil-gambeson.glb": "704d3fc7b57eb94d9b4e522f9584f51078245f7ba5da2683a0fe0ce8363da207",
    "hearthveil-palette.png": "ba99d7ab57a505286b323a7054a9376172b241fa3d51694a5f159c26d072e7fb",
}
EXPECTED_PREVIEW = {
    "playerBlacksmith": "ftkmf_glb_hearthveil-body.glb",
    "hairTop": "ftkmf_glb_hearthveil-hair-top.glb",
    "hairBottom": "ftkmf_glb_hearthveil-hair-bottom.glb",
    "armorBlacksmithF(Clone)": "ftkmf_glb_hearthveil-default-armor.glb",
    "bootsBlacksmith(Clone)": "ftkmf_glb_hearthveil-boots.glb",
}
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
AUTHORING = [
    "README.md", "manifest.json", "runtime-profile.json", "build_geometry.py",
    "verify_original_geometry.py", "render_studio.py", "build-report.json",
    "original-geometry-proof.json",
    "hearthveil-body.glb", "hearthveil-body.source.json", "hearthveil-body.pieces.json", "hearthveil-body.validation.json",
    "hearthveil-hair-top.glb", "hearthveil-hair-top.source.json", "hearthveil-hair-top.pieces.json", "hearthveil-hair-top.validation.json",
    "hearthveil-hair-bottom.glb", "hearthveil-hair-bottom.source.json", "hearthveil-hair-bottom.pieces.json", "hearthveil-hair-bottom.validation.json",
    "hearthveil-default-armor.glb", "hearthveil-default-armor.source.json", "hearthveil-default-armor.pieces.json", "hearthveil-default-armor.validation.json",
    "hearthveil-boots.glb", "hearthveil-boots.source.json", "hearthveil-boots.pieces.json", "hearthveil-boots.validation.json",
    "hearthveil-gambeson.glb", "hearthveil-gambeson.source.json", "hearthveil-gambeson.pieces.json", "hearthveil-gambeson.validation.json",
    "hearthveil-palette.png", "hearthveil-hero.png", "hearthveil-side.png",
    "hearthveil-gambeson-hero.png", "hearthveil-gambeson-side.png",
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
    animator = frame.get("animator")
    if not isinstance(animator, dict):
        return result
    layers = animator.get("layers")
    if not isinstance(layers, list):
        return result
    for layer in layers:
        if not isinstance(layer, dict):
            continue
        playing = layer.get("playing")
        if not isinstance(playing, list):
            continue
        for clip in playing:
            if isinstance(clip, dict) and isinstance(clip.get("name"), str):
                result.add(clip["name"])
    return result


def source_record(path: Path) -> dict:
    return {"source": relative(path), "sha256": sha(path)}


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
        "label": label,
        "path": str(output.relative_to(OUT)),
        "sha256": sha(output),
        "frames": frame_count,
        "width": int(probe["width"]),
        "height": int(probe["height"]),
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
        "label": label,
        "source": relative(source),
        "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination),
        "observation": observation,
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
assert isinstance(catalog, dict) and next(row for row in catalog["profiles"] if row["key"] == PROFILE_KEY) == PROFILE
registration = read(PLAYER_REGISTRATION)
registered = next(row for row in registration["registered"] if row["key"] == PROFILE_KEY)
assert registered["id"] == CLASS_ID and registered["skinset"] == SKINSET
assert registered["startingArmor"] == "armorCloth1" and registered["startingArmorAppendedCount"] == 1
stage = read(STAGE)
deployment = read(DEPLOYMENT)
native_helper_deployment = read(NATIVE_CREATE_HELPER_DEPLOYMENT)
assert stage["status"] == "STAGED_NOT_DEPLOYED_CUSTOM_MODEL_V1" and stage["newAssets"] == ASSETS
assert deployment["status"] == "VERIFIED_COMPLETE"
assert deployment["new"]["model-test-player-profiles.json"] == PLAYER_CATALOG_SHA256
assert native_helper_deployment["status"] == "VERIFIED_COMPLETE"
assert native_helper_deployment["new"]["sha256"] == "f1b3f4a4de75b13cfa75a78c673fba5f48bbc7152240e4468bcda72ea6415f18"
assert read(V1_INTEGRITY)["status"] == "PASS"

native_create = read(RAW_PATHS["nativeCreate"])
preview_state = read(RAW_PATHS["previewState"])
preview_inventory = read(RAW_PATHS["previewInventory"])
preview_idle = read(RAW_PATHS["previewIdle"])
assert all(document["session"] == PREVIEW_SESSION for document in (native_create, preview_state, preview_inventory, preview_idle))
assert native_create["ok"] is True and native_create["status"] == "native_create_game_reached_actual_character_create"
assert native_create["method"] == "StartGameFE.GameConfig.OnStartGame"
assert native_create["mapReady"] is True and native_create["characterCreateRootActive"] is True
candidate = next(row for row in native_create["candidates"] if row["classId"] == CLASS_ID)
assert candidate == {
    "active": True, "avatarActive": True, "avatarInstanceId": PREVIEW_CEL_ID,
    "classId": CLASS_ID, "classLabel": "Hearthveil Blacksmith", "ownerInstanceId": PREVIEW_OWNER_ID,
    "parentIsNativePedestal": True, "reciprocalPreviewReference": True, "skinType": 0, "turnIndex": 0,
}

assert preview_state["ok"] is True and preview_state["status"] == "observed_actual_native_player_preview"
assert preview_state["classKey"] == PROFILE_KEY and preview_state["classId"] == CLASS_ID
assert preview_state["skinset"] == SKINSET and preview_state["catalogSha256"] == PLAYER_CATALOG_SHA256
assert preview_state["ownerInstanceId"] == PREVIEW_OWNER_ID and preview_state["celInstanceId"] == PREVIEW_CEL_ID
assert preview_state["nativeMenuMembership"] is True and preview_state["reciprocalPreviewReference"] is True
assert preview_state["parentIsNativePedestal"] is True
assert preview_state["outfit"] == {"armorId": -1, "backpackId": -1, "helmetId": -1}
assert preview_state["lease"]["present"] is True and preview_state["lease"]["references"] == 1
assert {row["file"]: row["sha256"] for row in preview_state["assetFiles"]} == ASSETS
state_renderers = {row["celRelativeRendererPath"]: row for row in preview_state["renderers"]}
assert set(state_renderers) == set(EXPECTED_PREVIEW)
assert {path: row["mesh"] for path, row in state_renderers.items()} == EXPECTED_PREVIEW
assert state_renderers["hairTop"]["active"] is False
assert all(row["enabled"] is True for row in state_renderers.values())

assert preview_inventory["ok"] is True and preview_inventory["scope"] == "player-preview"
body_rows = [row for row in preview_inventory["renderers"]
             if row.get("ownerInstanceId") == PREVIEW_OWNER_ID and row.get("rendererPath") == "@cel/playerBlacksmith"]
assert len(body_rows) == 1
body = body_rows[0]
assert body["instanceId"] == PREVIEW_BODY_RENDERER_ID and body["celInstanceId"] == PREVIEW_CEL_ID
assert body["mesh"] == EXPECTED_PREVIEW["playerBlacksmith"] and body["boneSignature"] == PREVIEW_BODY_SIGNATURE
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
           and frame["celRelativeRendererPath"] == "playerBlacksmith" and frame["mesh"] == EXPECTED_PREVIEW["playerBlacksmith"]
           and frame["boneSignature"] == PREVIEW_BODY_SIGNATURE and frame["active"] is True
           and frame["enabled"] is True and frame["isVisible"] is True and frame["captureFramerate"] == 12
           for frame in frames)
assert all("standardIdle_handsDown" in active_clips(frame) for frame in frames)
assert frames[-1]["gameSeconds"] >= 1.8

SOURCES: set[Path] = set()
IMAGE_PINS: dict[str, str] = {}
for path in [
    Path(__file__), OUT / "README.md", PLAYER_CATALOG, PLAYER_REGISTRATION, STAGE, DEPLOYMENT,
    NATIVE_CREATE_HELPER_DEPLOYMENT, V1_VALIDATION, V1_INTEGRITY,
    ROOT / "tools/ai-model-pipeline/stage_custom_model_profile.py",
    ROOT / "tools/ai-model-pipeline/deploy_custom_model_stage.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/command.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/Plugin.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/AvatarInventory.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/PlayerPreviewObservation.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/NativeCreateCharacterScreen.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/test_native_create_character_screen_boundary.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/README.md",
    ROOT / "tools/ai-model-pipeline/summarize_capture.py",
    ROOT / "skills/ftk-custom-models/SKILL.md",
    ROOT / "docs/MODEL-PLAYER-API.md",
    ROOT / "docs/MODEL-AUTHORING.md",
    ROOT / "scratch/hearthveil-preview-state-request.json",
    ROOT / "scratch/hearthveil-preview-inventory-request.json",
    ROOT / "scratch/hearthveil-preview-idle-capture-request.json",
    ROOT / "scratch/hearthveil-preview-idle-capture-summary.json",
    *RAW_PATHS.values(),
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
                 "Hearthveil Blacksmith on the game-owned Party Select pedestal during the measured native idle capture."),
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

preview_capture = {
    "label": "native-preview-idle",
    "complete": True,
    "termination": "complete_24_fixed_step_frames",
    "capture": source_record(RAW_PATHS["previewIdle"]),
    "frameCount": 24,
    "scope": "player-preview",
    "rendererPath": "playerBlacksmith",
    "mesh": EXPECTED_PREVIEW["playerBlacksmith"],
    "boneSignature": PREVIEW_BODY_SIGNATURE,
    "state": "standardIdle_handsDown",
    "clips": ["standardIdle_handsDown"],
    "timingMode": preview_idle["timingMode"],
    "actualGameSeconds": frames[-1]["gameSeconds"],
    "summary": source_record(ROOT / "scratch/hearthveil-preview-idle-capture-summary.json"),
}

write(OUT / "asset-pins.json", ASSETS)
write(OUT / "source-image-pins.json", IMAGE_PINS)
validation = {
    "status": "supplemental_native_character_creation_preview_and_idle_observed",
    "scope": "Fresh isolated Party Select supplement for the original six-mesh Hearthveil Blacksmith. The game, through its actual Create Game callback, created the room, map, character-create UI, pedestal, and preview avatar. This archive records the selected native preview and an exact body idle capture. It supplements V1 rather than transferring combat, equipment, teardown, or final-art claims into this session.",
    "profile": PROFILE,
    "session": PREVIEW_SESSION,
    "identity": {
        "classId": CLASS_ID,
        "skinset": SKINSET,
        "playerCatalogSha256": PLAYER_CATALOG_SHA256,
        "ownerInstanceId": PREVIEW_OWNER_ID,
        "celInstanceId": PREVIEW_CEL_ID,
    },
    "provenance": {
        "stage": source_record(STAGE),
        "deployment": source_record(DEPLOYMENT),
        "playerRegistration": source_record(PLAYER_REGISTRATION),
        "nativeCreateHelperDeployment": source_record(NATIVE_CREATE_HELPER_DEPLOYMENT),
        "priorV1": {"validation": source_record(V1_VALIDATION), "integrity": source_record(V1_INTEGRITY)},
        "gameAssets": ASSETS,
    },
    "binding": {
        "ownerKind": "player-preview",
        "ownerInstanceId": PREVIEW_OWNER_ID,
        "celInstanceId": PREVIEW_CEL_ID,
        "rendererPath": "playerBlacksmith",
        "mesh": EXPECTED_PREVIEW["playerBlacksmith"],
        "boneSignature": PREVIEW_BODY_SIGNATURE,
        "state": source_record(RAW_PATHS["previewState"]),
        "inventory": source_record(RAW_PATHS["previewInventory"]),
    },
    "avatarOwners": {
        "preview": {
            "status": "observed_actual_native_player_preview",
            "observedAvatars": 1,
            "ownerInstanceId": PREVIEW_OWNER_ID,
            "celInstanceId": PREVIEW_CEL_ID,
            "classId": CLASS_ID,
            "classLabel": "Hearthveil Blacksmith",
            "nativeMenuMembership": True,
            "reciprocalPreviewReference": True,
            "parentIsNativePedestal": True,
            "leaseId": preview_state["lease"]["leaseId"],
            "leaseReferences": preview_state["lease"]["references"],
            "visibleSkinnedRenderers": 4,
            "source": source_record(RAW_PATHS["previewState"]),
        },
    },
    "nativeCreate": {
        "status": native_create["status"],
        "method": native_create["method"],
        "mapReady": True,
        "characterCreateRootActive": True,
        "waitedFrames": native_create["waitedFrames"],
        "source": source_record(RAW_PATHS["nativeCreate"]),
    },
    "renderers": [
        {key: row[key] for key in ("celRelativeRendererPath", "mesh", "instanceId", "boneSignature", "active", "enabled", "isVisible")}
        for row in preview_state["renderers"]
    ],
    "captures": [preview_capture],
    "selectedFrames": selected,
    "videos": videos,
    "visualReview": {
        "status": "sampled_native_character_creation_preview_reviewed_final_art_unapproved",
        "observed": "The captured game-owned Party Select screen labels Player 1 as Hearthveil Blacksmith. The custom body, active lower hair, default armor, and boots are visibly assembled on the native pedestal alongside the game-owned helmet and backpack; the preview remains present while the sampled native idle state advances.",
        "notAccepted": "This sampled preview does not establish every camera, culling condition, alternate apparel branch, player death, final-owner teardown, portrait, multiplayer layout, or a final-art verdict.",
    },
    "limitations": [
        "The actual Create Game callback was invoked only after normal visible navigation reached the enabled offline Create Game screen; this helper did not construct a preview avatar or select a class.",
        "The 24-frame idle capture uses fixed simulation steps. Its measured game time establishes the recorded idle interval, not real-time rendering performance or full loop coverage.",
        "V1 remains the separate evidence source for combat attack, incoming damage, item59 apparel rebuilds, lease disposal, and Loot-to-Ready progression.",
        "The selected default preview outfit does not prove the equipped Gambeson branch, another skinset, another controller, or another player class.",
    ],
}
write(OUT / "validation.json", validation)
integrity = {
    "status": "PASS",
    "validation": {"path": "validation.json", "sha256": sha(OUT / "validation.json")},
    "metadata": metadata,
    "sourceImages": {"count": len(IMAGE_PINS), "pins": "source-image-pins.json"},
    "selected": selected,
    "videos": videos,
}
write(OUT / "integrity.json", integrity)
print(json.dumps({
    "status": "PASS", "metadata": len(metadata), "sourceImages": len(IMAGE_PINS),
    "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json"),
}, indent=2))
