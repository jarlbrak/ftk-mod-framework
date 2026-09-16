#!/usr/bin/env python3
"""Verify Tidecrown V1's immutable archive and its intentionally historic inputs."""
import gzip
import hashlib
import json
import subprocess
from pathlib import Path


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
ASSET = ROOT / "art-experiments/tidecrown-sea-king"
OUT = ASSET / "live-validation-v1"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


validation = read(OUT / "validation.json")
assert validation["status"] == "fresh_catalog_413_original_binding_focus_hit_explicit_ragdoll_fixture_and_strict_ready"
assert validation["assetHashes"] == read(OUT / "asset-pins.json")

manifest = read(ASSET / "manifest.json")
assert manifest["liveEvidence"] == {"live-validation-v1/validation.json": sha(OUT / "validation.json")}

historical_manifest = validation["historicalSourceSnapshots"]
assert len(historical_manifest) == 1
assert historical_manifest[0]["source"] == "art-experiments/tidecrown-sea-king/manifest.json"
assert len(historical_manifest[0]["snapshotSha256"]) == 64
assert historical_manifest[0]["reason"].startswith("The root manifest is finalized")
expected_historical_drift = {
    "art-experiments/tidecrown-sea-king/manifest.json": "The archive preserves the pre-finalization manifest; finalization adds its own validation hash.",
    "art-experiments/tidecrown-sea-king/README.md": "The authoring README gained the completed V1 trial description after the capture-time archive snapshot.",
    "art-experiments/tidecrown-sea-king/finalize_manifest.py": "The manifest finalizer was extended after capture to pin this independent archive verifier.",
}
assert historical_manifest[0]["source"] in expected_historical_drift

gzip_count = 0
current_mismatches = []
for mapping in validation["losslessMappings"]:
    archived = OUT / mapping["archive"]
    assert sha(archived) == mapping["archiveSha256"]
    payload = gzip.decompress(archived.read_bytes())
    assert hashlib.sha256(payload).hexdigest() == mapping["sourceSha256"]
    source = ROOT / mapping["source"]
    assert source.is_file()
    if sha(source) != mapping["sourceSha256"]:
        current_mismatches.append(mapping["source"])
    gzip_count += 1
assert set(current_mismatches) == set(expected_historical_drift)

image_pins = read(OUT / validation["sourceImagePins"])
for source_name, pin in image_pins.items():
    source = ROOT / source_name
    assert source.is_file() and sha(source) == pin["sha256"] and source.stat().st_size == pin["bytes"]

for name, digest in validation["assetHashes"].items():
    assert sha(ASSET / name) == digest
for selected in validation["selectedPNGs"]:
    source = ROOT / selected["source"]
    archived = OUT / selected["archive"]
    assert sha(source) == sha(archived) == selected["sha256"]
for video in validation["videos"]:
    path = OUT / video["path"]
    assert sha(path) == video["sha256"]
    probe = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames", "-of", "json", str(path),
    ]))["streams"][0]
    assert int(probe["nb_read_frames"]) == video["frames"]

print(json.dumps({
    "status": "PASS",
    "validation": str((OUT / "validation.json").relative_to(ROOT)),
    "gzipMappings": gzip_count,
    "sourceImages": len(image_pins),
    "selectedPNGs": len(validation["selectedPNGs"]),
    "videos": [{"path": video["path"], "frames": video["frames"]} for video in validation["videos"]],
    "expectedHistoricalDrift": expected_historical_drift,
}, indent=2))
