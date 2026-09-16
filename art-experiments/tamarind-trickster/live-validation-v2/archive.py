#!/usr/bin/env python3
"""Archive the fresh Tamarind passive-arrival trial without game payloads."""

from pathlib import Path
import gzip
import hashlib
import json
import os
import shutil
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments" / "tamarind-trickster"
OUT = ASSET / "live-validation-v2"
BASE = ROOT / "scratch" / "mirewarden-game" / "model-test-output"
SESSION = "d09d285440894f2f8eeca877568975b3"
RAW = BASE / "103dff7e84024987a121739f63e38052.json"
RAW_DIR = RAW.with_suffix("")
CASE_DIR = BASE / "case-e8edddb73a4a4b18bf37587c72d1e2f2"
ARRIVAL_CASE = CASE_DIR / "arrival-case-result.json"
ARRIVAL_JOURNAL = CASE_DIR / "journal.jsonl"
SETUP_JOURNAL = BASE / "case-9224c072bcc04fcc9c70b028028fe849" / "journal.jsonl"
REVIEW = ROOT / "scratch" / "tamarind-root-visual-review.json"
PROFILE = ROOT / "scratch" / "mirewarden-game" / "model-test-profiles.json"
MANIFEST = ASSET / "manifest.json"
CANDIDATE = ASSET / "candidate-manifest.json"


def sha(path):
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path):
    return json.loads(path.read_text())


def write(path, value):
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


assert not (OUT / "validation.json").exists() or os.environ.get("FTK_ARCHIVE_REBUILD") == "1", "refusing to overwrite a completed archive"
OUT.mkdir(parents=True, exist_ok=True)

case = read(ARRIVAL_CASE)
raw = read(RAW)
review = read(REVIEW)
manifest = read(MANIFEST)
assert case["session"] == SESSION and case["status"] == "terminal_capture_observed_no_acceptance"
assert case["rawCaptureSha256"] == sha(RAW) and case["rawCaptureOk"] is True
assert raw["session"] == SESSION and raw["ok"] is True and len(raw["frames"]) == 120
assert raw["arrival"]["captureId"] == RAW.stem
assert review["session"] == SESSION and review["rawCapture"]["sha256"] == sha(RAW)
assert len(review["frames"]) == 7
assert manifest["originalAssets"] == {
    "tamarind.glb": "be02036b2bd96d2c10905ed808197aab1205ba636cb0a6931693886ca595c0a7",
    "tamarind_basecolor.png": "1a932c5c2aebbfc37f2ffe682f9a707cfbf0ad8434dd07a34fd4d4d30ace5288",
}

sources = {
    ARRIVAL_CASE,
    ARRIVAL_JOURNAL,
    SETUP_JOURNAL,
    BASE / f"arrival-session-{SESSION}.json",
    BASE / f"new-run-session-{SESSION}.json",
    RAW,
    REVIEW,
    PROFILE,
    MANIFEST,
    CANDIDATE,
}
sources.update(RAW_DIR.glob("*.png"))
for filename in (
    "tamarind.source.json",
    "tamarind.validation.json",
    "root-native-pose-review.json",
    "original-geometry-proof.json",
    "reopened-validation.json",
):
    path = ASSET / filename
    if path.is_file():
        sources.add(path)


def add_journal_sources(journal):
    for line in journal.read_text().splitlines():
        entry = json.loads(line)
        path = entry.get("data", {}).get("path")
        if path:
            candidate = Path(path)
            if candidate.is_file() and candidate.is_relative_to(ROOT):
                sources.add(candidate)


add_journal_sources(ARRIVAL_JOURNAL)
add_journal_sources(SETUP_JOURNAL)
for source in sources:
    assert source.is_file(), source

pins = {}
mappings = []
for source in sorted(sources):
    raw_bytes = source.read_bytes()
    relative = source.relative_to(ROOT)
    if relative.suffix.lower() == ".png":
        pins[str(relative)] = hashlib.sha256(raw_bytes).hexdigest()
        continue
    archive = OUT / "metadata" / (str(relative) + ".gz")
    archive.parent.mkdir(parents=True, exist_ok=True)
    archive.write_bytes(gzip.compress(raw_bytes, mtime=0))
    assert gzip.decompress(archive.read_bytes()) == raw_bytes
    mappings.append(
        {
            "source": str(relative),
            "sourceSha256": hashlib.sha256(raw_bytes).hexdigest(),
            "archive": str(archive.relative_to(OUT)),
            "archiveSha256": sha(archive),
            "encoding": "gzip-lossless",
        }
    )

write(OUT / "source-image-pins.json", pins)
write(
    OUT / "asset-pins.json",
    {
        "tamarind.glb": manifest["originalAssets"]["tamarind.glb"],
        "tamarind_basecolor.png": manifest["originalAssets"]["tamarind_basecolor.png"],
        "source": "art-experiments/tamarind-trickster/manifest.json",
    },
)

selected = []
for frame in review["frames"]:
    source = ROOT / frame["path"]
    assert source.is_file() and sha(source) == frame["sha256"]
    destination = OUT / "selected" / source.name
    destination.parent.mkdir(parents=True, exist_ok=True)
    shutil.copy2(source, destination)
    selected.append(
        {
            "source": frame["path"],
            "archive": str(destination.relative_to(OUT)),
            "sha256": sha(destination),
            "action": frame["action"],
            "index": frame["index"],
            "observation": frame["observation"],
        }
    )

assert len(list(RAW_DIR.glob("*.png"))) == 120
video = OUT / "arrival.mp4"
subprocess.run(
    [
        "ffmpeg",
        "-y",
        "-hide_banner",
        "-loglevel",
        "error",
        "-framerate",
        "12",
        "-i",
        str(RAW_DIR / "%04d.png"),
        "-frames:v",
        "120",
        "-c:v",
        "libx264",
        "-crf",
        "18",
        "-pix_fmt",
        "yuv420p",
        str(video),
    ],
    check=True,
)
video_info = json.loads(
    subprocess.check_output(
        [
            "ffprobe",
            "-v",
            "error",
            "-count_frames",
            "-select_streams",
            "v:0",
            "-show_entries",
            "stream=nb_read_frames,width,height,r_frame_rate",
            "-of",
            "json",
            str(video),
        ]
    )
)["streams"][0]
assert int(video_info["nb_read_frames"]) == 120
assert int(video_info["width"]) == 960 and int(video_info["height"]) == 624
assert video_info["r_frame_rate"] == "12/1"

arrival = raw["arrival"]
validation = {
    "status": "fresh_catalog_411_original_arrival_binding_appearance_and_native_suicide_motion_observed",
    "session": SESSION,
    "enemy": "ftkmf_modeltest_tamarind",
    "displayName": "Tamarind Trickster",
    "nativeChassis": "monkeyC",
    "rendererPath": "enMonkeyBasey",
    "rendererId": 121301,
    "controllerId": 5979,
    "ownerInstanceId": 369188,
    "boneSignature": "3fa26f0f6ae1e210b9b1e1126d3365aa41e40e9a479f03d52b712ad08dd5e3ed",
    "visualScale": 1.0,
    "catalogSha256": arrival["catalogSha256"],
    "profileSha256": sha(PROFILE),
    "assetHashes": arrival["assets"],
    "arrival": {
        "status": case["status"],
        "terminal": case["terminal"],
        "armAccepted": case["arm"]["ok"],
        "readyClickedOnce": case["ready"]["ok"],
        "captureId": arrival["captureId"],
        "armFrame": arrival["armFrame"],
        "nativeInitFrame": arrival["nativeInitFrame"],
        "captureLaunchFrame": arrival["captureLaunchFrame"],
        "firstPngFrame": arrival["firstPngFrame"],
        "retainedFrames": case["retainedFrames"],
        "rawCaptureSha256": sha(RAW),
    },
    "nativeSuicideFixture": {
        "observed": True,
        "initialHealth": raw["arrival"]["initialSnapshot"]["health"],
        "terminalHealth": raw["frames"][-1]["arrivalObservation"]["health"],
        "terminalAlive": raw["frames"][-1]["arrivalObservation"]["alive"],
        "terminalRendererVisible": raw["frames"][-1]["isVisible"],
        "nativeEventCountAtAction": raw["frames"][90]["nativeEventCount"],
        "nativeEventCountAtTerminal": raw["frames"][-1]["nativeEventCount"],
        "proficiency": "enSuicideCurse",
        "proficiencyId": 544,
        "qualification": "The arrival capture records the native fixture transition and event counts; the initial currentAttackInfo field is retained as potentially stale. The setup journal separately records the native self-sacrifice path that made ordinary hero actions unavailable.",
    },
    "ordinaryHitObserved": False,
    "ordinaryLethal": False,
    "nativeCollects": 0,
    "rawCapture": {
        "archive": "metadata/scratch/mirewarden-game/model-test-output/103dff7e84024987a121739f63e38052.json.gz",
        "sha256": sha(RAW),
        "frames": 120,
        "width": 960,
        "height": 624,
        "fps": 12,
    },
    "selectedPNGs": selected,
    "video": {
        "path": video.name,
        "sha256": sha(video),
        "frames": 120,
        "width": int(video_info["width"]),
        "height": int(video_info["height"]),
        "playbackFps": 12,
        "timing": "Presentation derivative, not unperturbed timing",
    },
    "sourceImageCount": len(pins),
    "sourceImagePins": "source-image-pins.json",
    "assetPins": "asset-pins.json",
    "losslessMappings": mappings,
    "rootVisualReview": str(REVIEW.relative_to(ROOT)),
    "limits": review["limits"],
}
write(OUT / "validation.json", validation)
(OUT / "README.md").write_text(
    """# Tamarind Trickster fresh live arrival V2

This supplement records the fresh catalog-411 Tamarind run in session `d09d285440894f2f8eeca877568975b3` using `monkeyC`, renderer `enMonkeyBasey` (121301), and visual scale `1.0`. The custom mesh bound to the native enemy owner with 42 captured bones and remained readable in the settled arrival views.

The runner used the passive native-arrival protocol: it armed once, clicked native Ready once, and retained 120 PNG samples. The first 25 samples document entry-camera settling. Samples 40 and 60 show the authored monkey and held barrel clearly; sample 90 includes the native purple suicide effect; sample 119 records the renderer inactive after the native fixture transition.

Tamarind's native suicide behavior means this run cannot claim an ordinary hero hit or ordinary lethal damage. The capture records the bounded native health/visibility transition and event counts, while preserving the stale-field warning from the runner and the initial setup journal. No loot progression is attributed to this arrival capture.

`validation.json` preserves the case result, raw capture, setup and arrival journals, helper responses, profile and authoring manifests as gzip-lossless metadata, all 120 source-image hashes, seven root-reviewed originals and a 120-frame presentation video. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
"""
)
print(
    json.dumps(
        {
            "validation": str(OUT / "validation.json"),
            "sourceImages": len(pins),
            "losslessMappings": len(mappings),
            "selected": len(selected),
            "videoFrames": 120,
            "validationSha256": sha(OUT / "validation.json"),
        }
    )
)
