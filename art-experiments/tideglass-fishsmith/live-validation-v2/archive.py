#!/usr/bin/env python3
"""Build Tideglass Fishsmith's immutable V2 native-preview evidence archive."""
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
OUT = ASSET / "live-validation-v2"
GAME = ROOT / "scratch" / "mirewarden-game"
BASE = GAME / "model-test-output"
CATALOG = GAME / "model-test-player-profiles.json"
REGISTRATION = GAME / "model-test-player-registration.json"
STAGE = ROOT / "scratch" / "tideglass-fishsmith-stage-v2" / "receipt.json"
DEPLOYMENT = GAME / "deployment-backups" / "tideglass-fishsmith-v2-20260912-20260912-081746" / "deployment.json"

PROFILE_KEY = "ftkmf_modeltest_player_tideglass_fishsmith"
CLASS_ID = 115
SKINSET = "blacksmith_Fish"
CATALOG_SHA256 = "915ac5717f740834e52c2f29728cd737ea85de97d52469bd01135882ad7ee905"
SESSION = "87afc0f579a344db992fd74d69d84ef8"
OWNER = -18950
CEL = -260066
BODY = -260084
BODY_SIGNATURE = "a1a4221b983f030e292771fbc23bae5e34f687c0864c6bff793ea510816eb584"
ASSETS = {
    "tideglass-body.glb": "499e6aa71ff2469184c899cb03a93b2aac4bd0d5b5186197c61861389c1af941",
    "tideglass-hair-top.glb": "28f8dcfe9e24a3271b096fa59d037258ee03fd8c70c0cbca3c2f33fcad9bc1be",
    "tideglass-hair-bottom.glb": "f7a2a720035805d905c9cc1641b3aa9842c4ad4d1d58e340c2c1273da66d0654",
    "tideglass-palette.png": "1e70f760d752d9e2c4b304aba75abafedc16df70877013da4056077a63a70b83",
}
EXPECTED = {
    "playerFIsh": "ftkmf_glb_tideglass-body.glb",
    "hairTop": "ftkmf_glb_tideglass-hair-top.glb",
    "hairBottom": "ftkmf_glb_tideglass-hair-bottom.glb",
}
RAW = {
    "createPreflight": "ae7db57e280a469dab6a5dba4754eb8c",
    "nativeCreate": "7da225f33ffa44b7b5f40ba187036657",
    "inputInitial": "fcd9467f5c184498969e3b13afb34be8",
    "inputNameButton": "1c56d9af86fa436783842fe3dfb917cd",
    "inputToggle": "5b67e95cf28d49a69071e7596f8bb201",
    "inputHerbalist": "bbf282b781ce4d72b5f9707a6f1dd269",
    "inputFishsmith": "5bd804a83c954dfab33ec04313e8338b",
    "previewState": "ffb3afab3c794bf4bcf241e944535ff3",
    "previewIdle": "19cd93f63ae64534a4d1925e14729164",
}
RAW_PATHS = {name: BASE / f"{identifier}.json" for name, identifier in RAW.items()}
SUMMARY = ROOT / "scratch" / "tideglass-v2e-preview-idle-summary.json"
STATE_REQUEST = ROOT / "scratch" / "tideglass-v2e-preview-state-request.json"
CAPTURE_REQUEST = ROOT / "scratch" / "tideglass-v2e-preview-idle-capture-request.json"
CONTACT_SHEET = ROOT / "scratch" / "tideglass-v2e-preview-idle-contact-sheet.png"
FORBIDDEN_SUFFIXES = {".dll", ".assets", ".resources", ".bundle", ".exe", ".app", ".unity3d", ".log"}
AUTHORING = [
    "README.md", "manifest.json", "runtime-profile.json", "build_geometry.py",
    "verify_original_geometry.py", "render_studio.py", "build-report.json",
    "original-geometry-proof.json", "tideglass-body.glb", "tideglass-body.source.json",
    "tideglass-body.pieces.json", "tideglass-body.validation.json", "tideglass-hair-top.glb",
    "tideglass-hair-top.source.json", "tideglass-hair-top.pieces.json",
    "tideglass-hair-top.validation.json", "tideglass-hair-bottom.glb",
    "tideglass-hair-bottom.source.json", "tideglass-hair-bottom.pieces.json",
    "tideglass-hair-bottom.validation.json", "tideglass-palette.png", "tideglass-hero.png",
    "tideglass-side.png",
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


def copy_source_image(path: Path, pins: dict[str, dict]) -> None:
    source = relative(path)
    destination = OUT / "source-images" / source
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(path, destination)
    assert sha(path) == sha(destination)
    pins[source] = {"sha256": sha(destination), "bytes": destination.stat().st_size,
                    "archive": str(destination.relative_to(OUT))}


def select_frame(index: int) -> dict:
    source = RAW_PATHS["previewIdle"].with_suffix("") / f"{index:04d}.png"
    destination = OUT / "selected" / "native-preview-idle.png"
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    assert sha(source) == sha(destination)
    return {"label": "native-preview-idle", "source": relative(source),
            "archive": str(destination.relative_to(OUT)), "sha256": sha(destination),
            "observation": "Game-owned Player 1 Party Select pedestal during Tideglass V2's selected native idle."}


def video_from_frames(frame_dir: Path) -> dict:
    output = OUT / "video" / "native-preview-idle.mp4"
    output.parent.mkdir(parents=True, exist_ok=True)
    subprocess.run([
        "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", "12",
        "-i", str(frame_dir / "%04d.png"), "-frames:v", "24", "-c:v", "libx264",
        "-crf", "18", "-pix_fmt", "yuv420p", str(output),
    ], check=True)
    stream = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(output),
    ]))["streams"][0]
    assert int(stream["nb_read_frames"]) == 24
    return {"label": "native-preview-idle", "path": str(output.relative_to(OUT)),
            "sha256": sha(output), "frames": 24, "width": int(stream["width"]),
            "height": int(stream["height"]), "playbackFps": 12,
            "timing": "Presentation derivative; raw capture metadata and source-image pins retain measured timing."}


if (OUT / "validation.json").exists() and os.environ.get("FTK_ARCHIVE_REBUILD") != "1":
    raise AssertionError("Refusing to overwrite a completed archive. Set FTK_ARCHIVE_REBUILD=1 to rebuild intentionally.")

assert sha(CATALOG) == CATALOG_SHA256
assert {name: sha(ASSET / name) for name in ASSETS} == ASSETS
game_models = GAME / "BepInEx" / "plugins" / "FTKModFramework_content" / "models"
assert {name: sha(game_models / name) for name in ASSETS} == ASSETS
profile = read(ASSET / "runtime-profile.json")["profiles"][0]
assert profile["key"] == PROFILE_KEY and profile["skinset"] == SKINSET
catalog = read(CATALOG)
assert next(row for row in catalog["profiles"] if row["key"] == PROFILE_KEY) == profile
registration = read(REGISTRATION)
assert next(row for row in registration["registered"] if row["key"] == PROFILE_KEY)["id"] == CLASS_ID
stage, deployment = read(STAGE), read(DEPLOYMENT)
assert stage["status"] == "STAGED_NOT_DEPLOYED_CUSTOM_MODEL_V1"
assert stage["catalog"]["sha256"] == CATALOG_SHA256
assert deployment["status"] == "VERIFIED_COMPLETE"
assert deployment["review"]["candidateCatalogSha256"] == CATALOG_SHA256
assert deployment["review"]["liveAcceptance"] is False

raw = {name: read(path) for name, path in RAW_PATHS.items()}
assert all(document["session"] == SESSION for document in raw.values())
assert raw["createPreflight"]["eligible"] is True and raw["createPreflight"]["readOnly"] is True
assert raw["nativeCreate"]["ok"] is True and raw["nativeCreate"]["mapReady"] is True
assert raw["nativeCreate"]["characterCreateRootActive"] is True
assert raw["inputInitial"]["input"]["currentSelected"]["name"] == "ClassInfo"
assert raw["inputNameButton"]["input"]["currentSelected"]["name"] == "Button"
assert raw["inputToggle"]["input"]["currentSelected"]["name"] == "toggleClass"
assert raw["inputHerbalist"]["candidates"][0]["classId"] == 114
assert raw["inputFishsmith"]["candidates"][0]["classId"] == CLASS_ID
assert raw["inputFishsmith"]["candidates"][0]["ownerInstanceId"] == OWNER

preview = raw["previewState"]
assert preview["ok"] is True and preview["classKey"] == PROFILE_KEY and preview["classId"] == CLASS_ID
assert preview["skinset"] == SKINSET and preview["catalogSha256"] == CATALOG_SHA256
assert preview["ownerInstanceId"] == OWNER and preview["celInstanceId"] == CEL
assert preview["nativeMenuMembership"] is True and preview["reciprocalPreviewReference"] is True
assert preview["parentIsNativePedestal"] is True and preview["lease"]["applied"] is True
renderers = {row["celRelativeRendererPath"]: row for row in preview["renderers"]}
assert {path: renderers[path]["mesh"] for path in EXPECTED} == EXPECTED
assert renderers["playerFIsh"]["instanceId"] == BODY and renderers["playerFIsh"]["boneSignature"] == BODY_SIGNATURE
assert renderers["playerFIsh"]["active"] is True and renderers["playerFIsh"]["isVisible"] is True
assert renderers["hairBottom"]["active"] is True and renderers["hairBottom"]["isVisible"] is True
assert renderers["hairTop"]["active"] is False and renderers["hairTop"]["enabled"] is True and renderers["hairTop"]["isVisible"] is False

capture = raw["previewIdle"]
frames = capture["frames"]
frame_dir = RAW_PATHS["previewIdle"].with_suffix("")
pngs = sorted(frame_dir.glob("*.png"))
assert capture["ok"] is True and capture["scope"] == "player-preview"
assert capture["timingMode"] == "offline-fixed-step-gameplay" and capture["fixedStep"] is True
assert capture["requestedSeconds"] == 2.0 and capture["requestedFps"] == 12.0
assert len(frames) == len(pngs) == 24
assert all(
    frame["ownerKind"] == "player-preview" and frame["ownerInstanceId"] == OWNER
    and frame["celInstanceId"] == CEL and frame["instanceId"] == BODY
    and frame["rendererPath"] == "@cel/playerFIsh" and frame["mesh"] == EXPECTED["playerFIsh"]
    and frame["boneSignature"] == BODY_SIGNATURE and frame["active"] is True
    and frame["enabled"] is True and frame["isVisible"] is True and frame["captureFramerate"] == 12
    for frame in frames
)
assert frames[-1]["gameSeconds"] >= 1.8
for frame in frames:
    layer = next(row for row in frame["animator"]["layers"] if row["layer"] == 0)
    assert [clip["name"] for clip in layer["playing"]] == ["standardIdle_Extra03"]
summary = read(SUMMARY)
assert summary["frameCount"] == 24 and summary["timingStatus"] == "measured_progress"

sources: set[Path] = set()
for path in [
    OUT / "README.md", OUT / "archive.py", OUT / "verify.py", CATALOG, REGISTRATION,
    STAGE, DEPLOYMENT, STATE_REQUEST, CAPTURE_REQUEST, SUMMARY, *RAW_PATHS.values(),
    ROOT / "tools/ai-model-pipeline/runtime-test/command.py",
    ROOT / "tools/ai-model-pipeline/runtime-test/PlayerPreviewObservation.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/NativeCreateCharacterScreen.cs",
    ROOT / "tools/ai-model-pipeline/runtime-test/README.md",
    ROOT / "tools/ai-model-pipeline/summarize_capture.py",
    ROOT / "skills/ftk-custom-models/SKILL.md",
]:
    add_source(sources, path)

image_pins: dict[str, dict] = {}
for name in AUTHORING:
    path = ASSET / name
    if path.suffix.lower() == ".png":
        copy_source_image(path, image_pins)
    else:
        add_source(sources, path)
for path in pngs:
    copy_source_image(path, image_pins)
copy_source_image(CONTACT_SHEET, image_pins)

selected = [select_frame(11)]
sheet_source = relative(CONTACT_SHEET)
sheet_destination = OUT / "selected" / "native-preview-idle-contact-sheet.png"
shutil.copy2(CONTACT_SHEET, sheet_destination)
assert sha(CONTACT_SHEET) == sha(sheet_destination)
contact_sheet = {"source": sheet_source, "archive": str(sheet_destination.relative_to(OUT)),
                 "sha256": sha(sheet_destination), "frames": [0, 5, 11, 17, 23],
                 "scope": "Review derivative assembled from the five named native capture frames."}
videos = [video_from_frames(frame_dir)]

metadata = []
for source in sorted(sources):
    raw_bytes = source.read_bytes()
    destination = OUT / "metadata" / (relative(source) + ".gz")
    destination.parent.mkdir(parents=True, exist_ok=True)
    destination.write_bytes(gzip.compress(raw_bytes, mtime=0))
    assert gzip.decompress(destination.read_bytes()) == raw_bytes
    metadata.append({"source": relative(source), "sourceSha256": sha(source),
                     "archive": str(destination.relative_to(OUT)), "archiveSha256": sha(destination),
                     "encoding": "gzip-lossless"})

capture_record = {
    "label": "native-preview-idle", "complete": True, "termination": "complete_24_fixed_step_frames",
    "capture": source_record(RAW_PATHS["previewIdle"]), "frameCount": 24,
    "scope": "player-preview", "rendererPath": "playerFIsh", "mesh": EXPECTED["playerFIsh"],
    "boneSignature": BODY_SIGNATURE, "state": "standardIdle_Extra03", "clips": ["standardIdle_Extra03"],
    "timingMode": capture["timingMode"], "actualGameSeconds": frames[-1]["gameSeconds"],
    "summary": source_record(SUMMARY),
}
validation = {
    "status": "native_character_creation_preview_and_idle_observed_v2_preview_art_accepted",
    "scope": "Fresh isolated native Party Select evidence for Tideglass Fishsmith V2. It records the real Create Game route, observable class navigation, strict selected preview state, and the selected body idle. It is not a combat, equipment, lifecycle, or final all-context art acceptance.",
    "profile": profile, "session": SESSION,
    "identity": {"classId": CLASS_ID, "skinset": SKINSET, "playerCatalogSha256": CATALOG_SHA256,
                 "ownerInstanceId": OWNER, "celInstanceId": CEL},
    "provenance": {"stage": source_record(STAGE), "deployment": source_record(DEPLOYMENT),
                   "playerRegistration": source_record(REGISTRATION), "gameAssets": ASSETS,
                   "runtimeHelperSha256": "c6f319d607b5c75e919bc0b88bfacc9033b92a357e401be689170682fbf3f1ae"},
    "nativeCreate": {"preflight": source_record(RAW_PATHS["createPreflight"]),
                     "create": source_record(RAW_PATHS["nativeCreate"]),
                     "method": raw["nativeCreate"]["method"], "mapReady": True,
                     "characterCreateRootActive": True},
    "visibleClassNavigation": {"initial": source_record(RAW_PATHS["inputInitial"]),
                                "nameButton": source_record(RAW_PATHS["inputNameButton"]),
                                "toggle": source_record(RAW_PATHS["inputToggle"]),
                                "afterHerbalist": source_record(RAW_PATHS["inputHerbalist"]),
                                "afterFishsmith": source_record(RAW_PATHS["inputFishsmith"]),
                                "sequence": ["ClassInfo", "Button", "toggleClass", "Return: 113 to 114", "Return: 114 to 115"]},
    "binding": {"ownerKind": "player-preview", "ownerInstanceId": OWNER, "celInstanceId": CEL,
                "rendererPath": "playerFIsh", "mesh": EXPECTED["playerFIsh"], "boneSignature": BODY_SIGNATURE,
                "state": source_record(RAW_PATHS["previewState"])},
    "avatarOwners": {
        "preview": {
            "status": "observed_actual_native_player_preview",
            "observedAvatars": 1,
            "ownerInstanceId": OWNER,
            "celInstanceId": CEL,
            "rendererPath": "playerFIsh",
            "visibleCustomRenderers": ["playerFIsh", "hairBottom"],
            "boundInactiveCustomRenderers": ["hairTop"],
        }
    },
    "renderers": [{key: row[key] for key in ("celRelativeRendererPath", "mesh", "instanceId", "boneSignature", "active", "enabled", "isVisible")}
                  for row in preview["renderers"]],
    "captures": [capture_record], "selectedFrames": selected, "contactSheet": contact_sheet, "videos": videos,
    "visualReview": {"status": "sampled_native_character_creation_preview_reviewed_v2_acceptable",
                     "observed": "At game scale, the compact teal-and-brass Tideglass silhouette reads as a deliberate armored fish-smith. The revised darker visor and restrained face treatment avoid V1's pale mask-like read. The custom body and hairBottom remain visibly coherent throughout the sampled native idle; native helm, backpack, armor, and boots remain assembled accessories.",
                     "notEstablished": "Combat, overworld, equipment branches, portrait, culling, player death, teardown, multiplayer, every camera, and hairTop visibility are outside this sampled preview record."},
    "limitations": [
        "The Create Game callback was invoked only after the visible offline Create Game screen met its read-only eligibility gate; the helper did not construct an avatar or select a class.",
        "The class changed only through native visible keyboard focus. Each control and the intermediate class-114 state was reread before the next Return.",
        "The 24-frame idle uses fixed simulation steps. Measured game time records this interval, not real-time rendering performance or whole-loop coverage.",
        "hairTop is bound to the exact configured renderer but is inactive in this default native Fish preview; that does not establish all presentation states.",
    ],
    "sourceImagePins": "source-image-pins.json", "losslessMappings": metadata,
}

write(OUT / "asset-pins.json", ASSETS)
write(OUT / "source-image-pins.json", image_pins)
integrity = {"status": "PASS", "validation": {"path": "validation.json", "sha256": hashlib.sha256(encode(validation)).hexdigest()},
             "metadata": metadata, "sourceImages": {"count": len(image_pins), "pins": "source-image-pins.json"},
             "selected": selected, "contactSheet": contact_sheet, "videos": videos}
write(OUT / "integrity.json", integrity)
write(OUT / "validation.json", validation)
assert sha(OUT / "validation.json") == integrity["validation"]["sha256"]
print(json.dumps({"status": "PASS", "metadata": len(metadata), "sourceImages": len(image_pins),
                  "selected": len(selected), "videos": len(videos), "validationSha256": sha(OUT / "validation.json")}, indent=2))
