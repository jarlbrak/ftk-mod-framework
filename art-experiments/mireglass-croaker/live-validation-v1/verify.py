#!/usr/bin/env python3
"""Independently verify Mireglass Croaker's immutable V1 live archive."""
from __future__ import annotations

import gzip
import hashlib
import json
from pathlib import Path
import subprocess


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
OUT = ROOT / "art-experiments" / "mireglass-croaker" / "live-validation-v1"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    value = json.loads(path.read_text())
    assert isinstance(value, dict), path
    return value


def child(root: Path, value: str) -> Path:
    relative = Path(value)
    assert not relative.is_absolute() and ".." not in relative.parts, value
    result = root / relative
    assert result.resolve().is_relative_to(root.resolve()), result
    return result


def verify_snapshot(entry: dict, expected_source_sha256: str | None = None) -> dict:
    path = child(OUT, entry["archive"])
    assert path.is_file() and not path.is_symlink(), path
    assert sha(path) == entry["archiveSha256"]
    raw = gzip.decompress(path.read_bytes())
    assert hashlib.sha256(raw).hexdigest() == entry["sourceSha256"]
    if expected_source_sha256 is not None:
        assert entry["sourceSha256"] == expected_source_sha256
    assert entry["bytes"] == len(raw)
    value = json.loads(raw)
    assert isinstance(value, dict), path
    return value


validation = read(OUT / "validation.json")
assets = read(OUT / "asset-pins.json")
images = read(OUT / "source-image-pins.json")

assert validation["status"] == "reviewed_direct_acidblob_b_binding_appearance_idle_attack_hit_death_and_gameplay_observed"
assert validation["assetHashes"] == assets
assert validation["nativeChassis"] == validation["baseEnemy"] == "acidBlobB"
assert validation["rendererPaths"] == ["enAcidMonster"]
assert validation["profile"]["key"] == validation["enemy"] == "ftkmf_modeltest_mireglass_croaker_acidblob_b"

catalog = verify_snapshot(validation["catalogSnapshot"], validation["catalogSha256"])
registration = verify_snapshot(validation["registrationSnapshot"])
assert [profile for profile in catalog["profiles"] if profile["key"] == validation["enemy"]] == [validation["profile"]]
assert isinstance(registration, dict)

mappings = {entry["source"]: entry for entry in validation["losslessMappings"]}
assert len(mappings) == len(validation["losslessMappings"])
for source, entry in mappings.items():
    assert source.startswith(("art-experiments/", "scratch/")), source
    archived = child(OUT, entry["archive"])
    assert archived.is_file() and not archived.is_symlink(), archived
    assert sha(archived) == entry["archiveSha256"]
    assert hashlib.sha256(gzip.decompress(archived.read_bytes())).hexdigest() == entry["sourceSha256"]

assert validation["sourceImageCount"] == len(images)
for source, digest in images.items():
    assert source.startswith(("art-experiments/", "scratch/")), source
    assert isinstance(digest, str) and len(digest) == 64, source

selected = validation["selectedPNGs"]
assert len(selected) == 11
assert len({row["archive"] for row in selected}) == len(selected)
for row in selected:
    source = row["source"]
    assert images[source] == row["sha256"]
    archived = child(OUT, row["archive"])
    assert archived.is_file() and not archived.is_symlink(), archived
    assert sha(archived) == row["sha256"]

captures = validation["captures"]
assert [capture["action"] for capture in captures] == ["pass", "attack", "kill-fixture"]
for capture in captures:
    assert capture["complete"] is True and capture["frameCount"] == 120
    assert capture["pausedFrameCount"] == 0 and capture["meshIdentityStable"] is True
    raw = capture["rawCapture"]
    assert raw["path"] in mappings and mappings[raw["path"]]["sourceSha256"] == raw["sha256"]
    summary = ROOT / capture["summary"]["path"]
    assert summary.is_file() and not summary.is_symlink(), summary
    assert sha(summary) == capture["summary"]["sha256"]
    summarized = read(summary)
    assert summarized["captureId"] == capture["rawCapture"]["path"].split("/")[-1].removesuffix(".json")
    assert summarized["frameCount"] == capture["frameCount"]
    assert summarized["meshIdentityStable"] is True

for video in validation["videos"]:
    path = child(OUT, video["path"])
    assert path.is_file() and not path.is_symlink(), path
    assert sha(path) == video["sha256"]
    stream = json.loads(subprocess.check_output([
        "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
        "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(path),
    ]))["streams"][0]
    assert int(stream["nb_read_frames"]) == video["frames"] == 120
    assert int(stream["width"]) == video["width"] and int(stream["height"]) == video["height"]

binding = validation["binding"]
assert binding["rendererPath"] == "enAcidMonster" and binding["mesh"] == "mireglass.glb"
assert binding["catalogSha256"] == validation["catalogSha256"]
ordinary = validation["ordinaryHit"]
assert ordinary == {
    "action": "attack", "cheat": "None", "focus": False,
    "beforeHp": 86, "afterHp": 76, "clip": "AcidBlob_HitSmall",
    "scope": "same-target observed nonlethal native damage",
}
assert validation["explicitKillFixture"]["action"] == "kill-fixture"
assert validation["finalReady"]["ok"] is True and validation["finalReady"]["strictReady"]["ok"] is True

print(json.dumps({
    "status": "PASS",
    "validation": str((OUT / "validation.json").relative_to(ROOT)),
    "metadataRoundTrips": len(mappings),
    "sourceImages": len(images),
    "selected": len(selected),
    "captures": len(captures),
    "videos": len(validation["videos"]),
}, indent=2))
