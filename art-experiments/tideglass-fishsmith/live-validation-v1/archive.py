#!/usr/bin/env python3
"""Build Tideglass Fishsmith's immutable V1 native-preview evidence archive.

The archive preserves authored assets, recorded command metadata, screenshots,
and review derivatives. It deliberately excludes game binaries, Unity assets,
native meshes, local reference inputs, and logs.
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
ASSET = ROOT / "art-experiments" / "tideglass-fishsmith"
OUT = ASSET / "live-validation-v1"
GAME = ROOT / "scratch" / "mirewarden-game"
BASE = GAME / "model-test-output"
PLAYER_CATALOG = GAME / "model-test-player-profiles.json"
PLAYER_REGISTRATION = GAME / "model-test-player-registration.json"
STAGE = ROOT / "scratch" / "tideglass-fishsmith-stage-v1" / "receipt.json"
DEPLOYMENT = GAME / "deployment-backups" / "tideglass-fishsmith-20260912-074324" / "deployment.json"
ROUTE_PROOF = ROOT / "scratch" / "player-fishperson-route.json"

PROFILE_KEY = "ftkmf_modeltest_player_tideglass_fishsmith"
CLASS_ID = 115
SKINSET = "blacksmith_Fish"
PLAYER_CATALOG_SHA256 = "915ac5717f740834e52c2f29728cd737ea85de97d52469bd01135882ad7ee905"
PREVIEW_SESSION = "5f5b8369e48441c29f9243c60ac7f264"
PREVIEW_OWNER_ID = -18966
PREVIEW_CEL_ID = -293128
PREVIEW_BODY_RENDERER_ID = -293146
PREVIEW_BODY_SIGNATURE = "a1a4221b983f030e292771fbc23bae5e34f687c0864c6bff793ea510816eb584"

RAW = {
    "nativeCreateInitial": "a0275ed63f5549f89d23e2e1104f3fb3",
    "previewState": "3e26d17cb8cb421cb1f52a7019d4dacd",
    "previewInventory": "feb81ddb1e1a48ee80f431e35e7ed221",
    "previewIdlePreliminary": "fbf82d8eb3f448edbc7c8bc2af66c79e",
    "previewIdleSettled": "5601324818a749a58d1849fec99f9295",
}
RAW_PATHS = {name: BASE / f"{identifier}.json" for name, identifier in RAW.items()}
PRELIMINARY_SUMMARY = ROOT / "scratch" / "tideglass-preview-idle-settled-capture-summary.json"
SETTLED_SUMMARY = ROOT / "scratch" / "tideglass-preview-idle-settled-capture-v2-summary.json"

ASSETS = {
    "tideglass-body.glb": "dc701b4939c06c9e101c62a1df75d088ac4f1f1f9788c67c57affc93e6665161",
    "tideglass-hair-top.glb": "639b3cd6e462b0c61c5c9886698603b11911b447cdab6ccfdafbf236a63def91",
    "tideglass-hair-bottom.glb": "ed4fc9b3fe5e42fbf361ba9540202d65770efd3828cd27c4b2a52d5710940260",
    "tideglass-palette.png": "1e70f760d752d9e2c4b304aba75abafedc16df70877013da4056077a63a70b83",
}
EXPECTED_CUSTOM = {
    "playerFIsh": "ftkmf_glb_tideglass-body.glb",
    "hairTop": "ftkmf_glb_tideglass-hair-top.glb",
    "hairBottom": "ftkmf_glb_tideglass-hair-bottom.glb",
}
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d"}
AUTHORING = [
    "README.md", "manifest.json", "runtime-profile.json", "build_geometry.py",
    "verify_original_geometry.py", "render_studio.py", "build-report.json",
    "original-geometry-proof.json",
    "tideglass-body.glb", "tideglass-body.source.json", "tideglass-body.pieces.json", "tideglass-body.validation.json",
    "tideglass-hair-top.glb", "tideglass-hair-top.source.json", "tideglass-hair-top.pieces.json", "tideglass-hair-top.validation.json",
    "tideglass-hair-bottom.glb", "tideglass-hair-bottom.source.json", "tideglass-hair-bottom.pieces.json", "tideglass-hair-bottom.validation.json",
    "tideglass-palette.png", "tideglass-hero.png", "tideglass-side.png",
]


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict | list:
    return json.loads(path.read_text())


def encode(value: object) -> bytes:
    return (json.dumps(value, indent=2) + "\n").encode()


def write(path: Path, value: object) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_bytes(encode(value))


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


def renderer_map(rows: list[dict]) -> dict[str, dict]:
    return {row["celRelativeRendererPath"]: row for row in rows}


def layer_clips(frame: dict, layer_index: int) -> set[str]:
    for layer in (frame.get("animator") or {}).get("layers", []):
        if layer.get("layer") == layer_index:
            return {clip["name"] for clip in layer.get("playing", []) if isinstance(clip.get("name"), str)}
    return set()


def assert_stable_body_capture(document: dict, raw_name: str) -> tuple[list[dict], list[Path]]:
    frames = document["frames"]
    frame_dir = RAW_PATHS[raw_name].with_suffix("")
    pngs = sorted(frame_dir.glob("*.png"))
    assert document["ok"] is True and document["error"] is None
    assert document["scope"] == "player-preview"
    assert document["timingMode"] == "offline-fixed-step-gameplay" and document["fixedStep"] is True
    assert document["requestedSeconds"] == 2.0 and document["requestedFps"] == 12.0
    assert document["ownerInstanceId"] == PREVIEW_OWNER_ID and document["celInstanceId"] == PREVIEW_CEL_ID
    assert len(frames) == len(pngs) == 24
    assert all(
        frame["ownerKind"] == "player-preview" and frame["ownerInstanceId"] == PREVIEW_OWNER_ID
        and frame["celInstanceId"] == PREVIEW_CEL_ID and frame["instanceId"] == PREVIEW_BODY_RENDERER_ID
        and frame["celRelativeRendererPath"] == "playerFIsh" and frame["mesh"] == EXPECTED_CUSTOM["playerFIsh"]
        and frame["boneSignature"] == PREVIEW_BODY_SIGNATURE and frame["active"] is True
        and frame["enabled"] is True and frame["isVisible"] is True and frame["captureFramerate"] == 12
        for frame in frames
    )
    assert frames[-1]["gameSeconds"] >= 1.8
    return frames, pngs


def copy_source_image(path: Path, pins: dict[str, dict]) -> None:
    source = relative(path)
    destination = OUT / "source-images" / source
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, destination)
    assert sha(path) == sha(destination)
    pins[source] = {"sha256": sha(destination), "bytes": destination.stat().st_size,
                    "archive": str(destination.relative_to(OUT))}


def select_frame(raw_name: str, index: int, label: str, observation: str) -> dict:
    source = RAW_PATHS[raw_name].with_suffix("") / f"{index:04d}.png"
    destination = OUT / "selected" / f"{label}.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha(source) == sha(destination)
    return {
        "label": label, "source": relative(source), "archive": str(destination.relative_to(OUT)),
        "sha256": sha(destination), "observation": observation,
    }


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
assert deployment["review"]["candidateCatalogSha256"] == PLAYER_CATALOG_SHA256
assert deployment["review"]["liveAcceptance"] is False
assert deployment["new"]["model-test-player-profiles.json"] == PLAYER_CATALOG_SHA256

native_create = read(RAW_PATHS["nativeCreateInitial"])
preview_state = read(RAW_PATHS["previewState"])
preview_inventory = read(RAW_PATHS["previewInventory"])
preliminary_idle = read(RAW_PATHS["previewIdlePreliminary"])
settled_idle = read(RAW_PATHS["previewIdleSettled"])
assert all(document["session"] == PREVIEW_SESSION for document in (
    native_create, preview_state, preview_inventory, preliminary_idle, settled_idle,
))
assert native_create["ok"] is True and native_create["status"] == "native_create_game_reached_actual_character_create"
assert native_create["method"] == "StartGameFE.GameConfig.OnStartGame"
assert native_create["mapReady"] is True and native_create["characterCreateRootActive"] is True
initial_owner = next(row for row in native_create["candidates"] if row["turnIndex"] == 0)
assert initial_owner["ownerInstanceId"] == PREVIEW_OWNER_ID and initial_owner["classLabel"] == "Hearthveil Blacksmith"

assert preview_state["ok"] is True and preview_state["status"] == "observed_actual_native_player_preview"
assert preview_state["classKey"] == PROFILE_KEY and preview_state["classId"] == CLASS_ID
assert preview_state["classLabel"] == "Tideglass Fishsmith" and preview_state["skinset"] == SKINSET
assert preview_state["catalogSha256"] == PLAYER_CATALOG_SHA256
assert preview_state["ownerInstanceId"] == PREVIEW_OWNER_ID and preview_state["celInstanceId"] == PREVIEW_CEL_ID
assert preview_state["nativeMenuMembership"] is True and preview_state["reciprocalPreviewReference"] is True
assert preview_state["parentIsNativePedestal"] is True
assert preview_state["lease"]["present"] is True and preview_state["lease"]["references"] == 1
assert {row["file"]: row["sha256"] for row in preview_state["assetFiles"]} == ASSETS
state_renderers = renderer_map(preview_state["renderers"])
assert EXPECTED_CUSTOM.keys() <= state_renderers.keys()
assert {path: state_renderers[path]["mesh"] for path in EXPECTED_CUSTOM} == EXPECTED_CUSTOM
assert state_renderers["playerFIsh"]["active"] is True and state_renderers["playerFIsh"]["isVisible"] is True
assert state_renderers["hairBottom"]["active"] is True and state_renderers["hairBottom"]["isVisible"] is True
assert state_renderers["hairTop"]["active"] is False and state_renderers["hairTop"]["enabled"] is True
assert state_renderers["hairTop"]["isVisible"] is False

assert preview_inventory["ok"] is True and preview_inventory["scope"] == "player-preview"
inventory_rows = [row for row in preview_inventory["renderers"] if row.get("ownerInstanceId") == PREVIEW_OWNER_ID]
body_rows = [row for row in inventory_rows if row.get("rendererPath") == "@cel/playerFIsh"]
assert len(body_rows) == 1
body = body_rows[0]
assert body["instanceId"] == PREVIEW_BODY_RENDERER_ID and body["celInstanceId"] == PREVIEW_CEL_ID
assert body["mesh"] == EXPECTED_CUSTOM["playerFIsh"] and body["boneSignature"] == PREVIEW_BODY_SIGNATURE
assert body["active"] is True and body["enabled"] is True and body["isVisible"] is True
assert {"armorBlacksmithF(Clone)", "bootsBlacksmith(Clone)"} <= {row["celRelativeRendererPath"] for row in inventory_rows}
assert any(row["celRelativeRendererPath"].endswith("helmBlacksmith(Clone)") for row in inventory_rows)
assert any(row["celRelativeRendererPath"].endswith("backpackSmith(Clone)") for row in inventory_rows)

preliminary_frames, preliminary_pngs = assert_stable_body_capture(preliminary_idle, "previewIdlePreliminary")
settled_frames, settled_pngs = assert_stable_body_capture(settled_idle, "previewIdleSettled")
assert all("standarIdle_Extra04" in layer_clips(frame, 0) for frame in preliminary_frames[:-1])
assert "standardIdle_handsDown" in layer_clips(preliminary_frames[-1], 0)
assert all("standardIdle_handsDown" in layer_clips(frame, 0) for frame in settled_frames)
preliminary_summary = read(PRELIMINARY_SUMMARY)
settled_summary = read(SETTLED_SUMMARY)
assert preliminary_summary["captureId"] == RAW["previewIdlePreliminary"]
assert preliminary_summary["frameCount"] == 24 and preliminary_summary["timingStatus"] == "measured_progress"
assert any(row["clips"] == ["standarIdle_Extra04"] and row["sampleCount"] == 23 for row in preliminary_summary["states"])
assert settled_summary["captureId"] == RAW["previewIdleSettled"]
assert settled_summary["frameCount"] == 24 and settled_summary["timingStatus"] == "measured_progress"
assert settled_summary["meshIdentityStable"] is True and settled_summary["allScreenshotsPresent"] is True
assert any(row["clips"] == ["standardIdle_handsDown"] and row["sampleCount"] == 24 for row in settled_summary["states"])

SOURCES: set[Path] = set()
for path in [
    Path(__file__), OUT / "README.md", OUT / "verify.py", PLAYER_CATALOG, PLAYER_REGISTRATION,
    STAGE, DEPLOYMENT, ROUTE_PROOF,
    ROOT / "tools/ai-model-pipeline/stage_custom_model_profile.py",
    ROOT / "tools/ai-model-pipeline/deploy_custom_model_stage.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/command.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/AvatarInventory.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/PlayerPreviewObservation.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/NativeCreateCharacterScreen.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/README.md",
    ROOT / "tools/ai-model-pipeline/summarize_capture.py",
    ROOT / "skills/ftk-custom-models/SKILL.md",
    ROOT / "scratch/tideglass-preview-state-request.json",
    ROOT / "scratch/tideglass-preview-inventory-request.json",
    ROOT / "scratch/tideglass-preview-idle-settled-capture-request.json",
    PRELIMINARY_SUMMARY, SETTLED_SUMMARY, *RAW_PATHS.values(),
]:
    add_source(SOURCES, path)

IMAGE_PINS: dict[str, dict] = {}
for name in AUTHORING:
    path = ASSET / name
    if path.suffix.lower() == ".png":
        copy_source_image(path, IMAGE_PINS)
    else:
        add_source(SOURCES, path)
for png in preliminary_pngs + settled_pngs:
    copy_source_image(png, IMAGE_PINS)

selected = [
    select_frame(
        "previewIdleSettled", 12, "native-preview-idle",
        "Tideglass Fishsmith on the game-owned Player 1 Party Select pedestal during the selected settled native idle capture.",
    ),
]
videos = [video_from_frames(RAW_PATHS["previewIdleSettled"].with_suffix(""), 24, "native-preview-idle")]

metadata = []
for source in sorted(SOURCES):
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
    "capture": source_record(RAW_PATHS["previewIdleSettled"]), "frameCount": 24,
    "scope": "player-preview", "rendererPath": "playerFIsh", "mesh": EXPECTED_CUSTOM["playerFIsh"],
    "boneSignature": PREVIEW_BODY_SIGNATURE, "state": "standardIdle_handsDown", "clips": ["standardIdle_handsDown"],
    "timingMode": settled_idle["timingMode"], "actualGameSeconds": settled_frames[-1]["gameSeconds"],
    "summary": source_record(SETTLED_SUMMARY),
}
validation = {
    "status": "native_character_creation_preview_and_idle_observed_art_revision_pending",
    "scope": "Fresh isolated Party Select evidence for the original three-mesh Tideglass Fishsmith. The game created its own room, map, character-create UI, pedestal, and preview owner through the real Create Game callback. A visible native Party Select selection then chose Tideglass; this archive records that strict preview join and its selected settled body idle capture. It makes no broader player lifecycle, full renderer-visibility, or final-art claim.",
    "profile": PROFILE,
    "session": PREVIEW_SESSION,
    "identity": {
        "classId": CLASS_ID, "skinset": SKINSET, "playerCatalogSha256": PLAYER_CATALOG_SHA256,
        "ownerInstanceId": PREVIEW_OWNER_ID, "celInstanceId": PREVIEW_CEL_ID,
    },
    "provenance": {
        "stage": source_record(STAGE), "deployment": source_record(DEPLOYMENT),
        "playerRegistration": source_record(PLAYER_REGISTRATION), "fishRouteProof": source_record(ROUTE_PROOF),
        "gameAssets": ASSETS,
    },
    "nativeCreateInitial": {
        "status": native_create["status"], "method": native_create["method"], "mapReady": True,
        "characterCreateRootActive": True, "waitedFrames": native_create["waitedFrames"],
        "initialPlayerOneClassLabel": initial_owner["classLabel"],
        "initialPlayerOneOwnerInstanceId": initial_owner["ownerInstanceId"],
        "source": source_record(RAW_PATHS["nativeCreateInitial"]),
        "interpretation": "This source proves the real Create Game route created the native Party Select owner. The later strict preview state and visible capture establish Tideglass's actual selection.",
    },
    "binding": {
        "ownerKind": "player-preview", "ownerInstanceId": PREVIEW_OWNER_ID, "celInstanceId": PREVIEW_CEL_ID,
        "rendererPath": "playerFIsh", "mesh": EXPECTED_CUSTOM["playerFIsh"], "boneSignature": PREVIEW_BODY_SIGNATURE,
        "state": source_record(RAW_PATHS["previewState"]), "inventory": source_record(RAW_PATHS["previewInventory"]),
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
    "assembledNativeAccessories": [
        {key: row.get(key) for key in ("celRelativeRendererPath", "mesh", "instanceId", "active", "enabled", "isVisible")}
        for row in inventory_rows
        if row["celRelativeRendererPath"] not in EXPECTED_CUSTOM
    ],
    "preliminaryCapture": {
        "label": "pre-settle-preview-idle", "selectedForIdleReview": False,
        "status": "completed_24_fixed_steps_not_selected_for_idle_review",
        "reason": "Layer 0 remained in standarIdle_Extra04 for frames 0 through 22 and entered standardIdle_handsDown only at frame 23; the later settled capture was used for idle review.",
        "capture": source_record(RAW_PATHS["previewIdlePreliminary"]), "frameCount": 24,
        "timingMode": preliminary_idle["timingMode"], "actualGameSeconds": preliminary_frames[-1]["gameSeconds"],
        "summary": source_record(PRELIMINARY_SUMMARY),
    },
    "captures": [capture], "selectedFrames": selected, "videos": videos,
    "visualReview": {
        "status": "sampled_native_character_creation_preview_reviewed_art_revision_pending",
        "observed": "The captured game-owned Party Select screen labels Player 1 as Tideglass Fishsmith. The custom playerFIsh body and hairBottom mantle are visible throughout the selected settled native idle, while native Blacksmith armor, boots, helm, and backpack remain assembled. hairTop is bound to its exact six-bone renderer but that renderer is inactive in this default native Fish preview. The silhouette is readable, but the pale snout and eye treatment reads too much like a mask and is not accepted as finished art.",
        "notAccepted": "This sampled preview does not establish finished art, every camera or culling condition, overworld or combat animation, equipment branch, player death, final-owner teardown, portrait behavior, multiplayer layout, or a complete renderer-visibility guarantee.",
    },
    "limitations": [
        "The actual Create Game callback was invoked only after normal visible navigation reached the enabled offline Create Game screen; the helper did not construct a preview avatar or select a class.",
        "Tideglass was selected through the visible native Party Select UI after that initial screen was created. The strict state and selected capture prove the resulting class; the initial helper result intentionally records its pre-selection default.",
        "The 24-frame idle capture uses fixed simulation steps. Its measured game time establishes the recorded idle interval, not real-time rendering performance or full loop coverage.",
        "No conditional apparel assignment is declared for this profile. Native Blacksmith armor, boots, helm, and backpack visible in this default Fish preview do not establish a custom apparel path.",
        "The root authoring assets are expected to change for V2. This archive holds V1 source bytes and source-image copies so later art changes cannot alter this evidence.",
    ],
    "sourceImagePins": "source-image-pins.json",
    "losslessMappings": metadata,
}

write(OUT / "asset-pins.json", ASSETS)
write(OUT / "source-image-pins.json", IMAGE_PINS)
integrity = {
    "status": "PASS", "validation": {"path": "validation.json", "sha256": hashlib.sha256(encode(validation)).hexdigest()},
    "metadata": metadata, "sourceImages": {"count": len(IMAGE_PINS), "pins": "source-image-pins.json"},
    "selected": selected, "videos": videos,
}
write(OUT / "integrity.json", integrity)
write(OUT / "validation.json", validation)
assert sha(OUT / "validation.json") == integrity["validation"]["sha256"]
print(json.dumps({
    "status": "PASS", "metadata": len(metadata), "sourceImages": len(IMAGE_PINS),
    "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json"),
}, indent=2))
