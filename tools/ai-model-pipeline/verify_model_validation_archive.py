#!/usr/bin/env python3
"""Verify a self-contained FTK model-validation evidence archive.

This checks immutable artifact integrity only.  It does not infer that a model
works, that a motion is acceptable, or that a source-specific validation gate
has passed.  Those questions stay with the runtime evidence index and the
validation-gate ledger.
"""
from __future__ import annotations

import argparse
import gzip
import hashlib
import json
from pathlib import Path
import shutil
import subprocess
import sys
from typing import Any


Json = dict[str, Any]
FORBIDDEN_SOURCE_SUFFIXES = {
    ".app",
    ".assets",
    ".bundle",
    ".dll",
    ".exe",
    ".resources",
    ".unity3d",
}
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


class ArchiveIntegrityError(ValueError):
    """A deterministic archive-integrity failure with actionable messages."""


def sha256_bytes(value: bytes) -> str:
    return hashlib.sha256(value).hexdigest()


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def as_dict(value: Any) -> Json:
    return value if isinstance(value, dict) else {}


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def is_sha256(value: Any) -> bool:
    return isinstance(value, str) and len(value) == 64 and all(character in "0123456789abcdef" for character in value)


def relative_text(value: Any, label: str) -> Path:
    if not isinstance(value, str) or not value:
        raise ArchiveIntegrityError(f"{label} must be a nonempty relative path")
    path = Path(value)
    if path.is_absolute() or ".." in path.parts or path == Path("."):
        raise ArchiveIntegrityError(f"{label} must stay below archive root: {value!r}")
    return path


def no_symlink_components(root: Path, path: Path, label: str) -> None:
    try:
        relative = path.relative_to(root)
    except ValueError as error:
        raise ArchiveIntegrityError(f"{label} escapes archive root: {path}") from error
    cursor = root
    if cursor.is_symlink():
        raise ArchiveIntegrityError(f"archive root is a symlink: {root}")
    for part in relative.parts:
        cursor = cursor / part
        if cursor.is_symlink():
            raise ArchiveIntegrityError(f"{label} contains a symlink: {cursor}")


def archive_file(root: Path, value: Any, label: str) -> Path:
    path = root / relative_text(value, label)
    no_symlink_components(root, path, label)
    if not path.is_file():
        raise ArchiveIntegrityError(f"{label} is not a regular file: {path}")
    try:
        path.resolve().relative_to(root.resolve())
    except ValueError as error:
        raise ArchiveIntegrityError(f"{label} resolves outside archive root: {path}") from error
    return path


def read_json_file(path: Path, label: str) -> Json:
    try:
        value = json.loads(path.read_text())
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ArchiveIntegrityError(f"{label} is not a JSON object: {path}") from error
    if not isinstance(value, dict):
        raise ArchiveIntegrityError(f"{label} must contain a JSON object: {path}")
    return value


def read_pins(root: Path, validation: Json, field: str, *, archived_images: bool = False) -> Json | None:
    pointer = validation.get(field)
    if pointer is None:
        return None
    path = archive_file(root, pointer, f"validation.{field}")
    raw_pins = read_json_file(path, f"validation.{field}")
    pins: Json = {}
    for source, value in raw_pins.items():
        if not isinstance(source, str) or not source or Path(source).is_absolute() or ".." in Path(source).parts:
            raise ArchiveIntegrityError(f"{field} contains an invalid source path: {source!r}")
        # A few historical asset-pin files carry a provenance pointer beside
        # the actual filename-to-hash map. It is not an asset hash and cannot
        # silently count as one.
        if field == "assetPins" and source == "source" and isinstance(value, str):
            continue
        digest = value
        if archived_images and isinstance(value, dict):
            image = as_dict(value)
            digest = image.get("sha256")
            archive_value = image.get("archive")
            archive = None
            if archive_value is not None:
                archive = archive_file(root, archive_value, f"{field}[{source!r}].archive")
                if sha256_file(archive) != digest:
                    raise ArchiveIntegrityError(f"{field} archived image SHA-256 mismatch: {source}")
            expected_bytes = image.get("bytes")
            if expected_bytes is not None and (not isinstance(expected_bytes, int) or expected_bytes < 0):
                raise ArchiveIntegrityError(f"{field} archived image byte count is invalid: {source}")
            if archive is not None:
                if expected_bytes is not None and archive.stat().st_size != expected_bytes:
                    raise ArchiveIntegrityError(f"{field} archived image byte count mismatch: {source}")
                with archive.open("rb") as stream:
                    if stream.read(len(PNG_SIGNATURE)) != PNG_SIGNATURE:
                        raise ArchiveIntegrityError(f"{field} archived source is not a PNG: {source}")
        if not is_sha256(digest):
            raise ArchiveIntegrityError(f"{field} has an invalid SHA-256 for {source!r}")
        pins[source] = digest
    return pins


def read_gzip(path: Path, label: str) -> bytes:
    try:
        with gzip.open(path, "rb") as stream:
            return stream.read()
    except (OSError, EOFError, gzip.BadGzipFile) as error:
        raise ArchiveIntegrityError(f"{label} is not a readable gzip stream: {path}") from error


def verify_mapping(root: Path, mapping: Json, index: int) -> Json:
    prefix = f"losslessMappings[{index}]"
    source = mapping.get("source")
    if not isinstance(source, str) or not source or Path(source).is_absolute() or ".." in Path(source).parts:
        raise ArchiveIntegrityError(f"{prefix}.source must be a workspace-relative path")
    suffix = Path(source).suffix.lower()
    if suffix in FORBIDDEN_SOURCE_SUFFIXES:
        raise ArchiveIntegrityError(f"{prefix}.source has forbidden game payload suffix {suffix!r}: {source}")
    if mapping.get("encoding") != "gzip-lossless":
        raise ArchiveIntegrityError(f"{prefix}.encoding must be gzip-lossless")
    expected_source_hash = mapping.get("sourceSha256")
    expected_archive_hash = mapping.get("archiveSha256")
    if not is_sha256(expected_source_hash) or not is_sha256(expected_archive_hash):
        raise ArchiveIntegrityError(f"{prefix} must include lowercase SHA-256 values")
    archive = archive_file(root, mapping.get("archive"), f"{prefix}.archive")
    if archive.suffix != ".gz":
        raise ArchiveIntegrityError(f"{prefix}.archive must end in .gz: {archive}")
    actual_archive_hash = sha256_file(archive)
    if actual_archive_hash != expected_archive_hash:
        raise ArchiveIntegrityError(f"{prefix}.archive SHA-256 mismatch for {archive}")
    raw = read_gzip(archive, f"{prefix}.archive")
    if sha256_bytes(raw) != expected_source_hash:
        raise ArchiveIntegrityError(f"{prefix}.source SHA-256 mismatch after gzip round-trip: {source}")
    expected_bytes = mapping.get("bytes")
    if expected_bytes is not None and (not isinstance(expected_bytes, int) or expected_bytes < 0 or len(raw) != expected_bytes):
        raise ArchiveIntegrityError(f"{prefix}.bytes does not match decompressed content: {source}")
    return {
        "source": source,
        "archive": str(archive.relative_to(root)),
        "sourceSha256": expected_source_hash,
    }


def verify_selected_pngs(root: Path, validation: Json, source_pins: Json | None) -> int:
    selected = validation.get("selectedPNGs")
    if selected is None:
        return 0
    if not isinstance(selected, list):
        raise ArchiveIntegrityError("validation.selectedPNGs must be an array when present")
    names: set[str] = set()
    for index, value in enumerate(selected):
        row = as_dict(value)
        if not row:
            raise ArchiveIntegrityError(f"selectedPNGs[{index}] must be an object")
        archive = archive_file(root, row.get("archive"), f"selectedPNGs[{index}].archive")
        name = str(archive.relative_to(root))
        if name in names:
            raise ArchiveIntegrityError(f"selectedPNGs has duplicate archive path: {name}")
        names.add(name)
        digest = row.get("sha256")
        if not is_sha256(digest) or sha256_file(archive) != digest:
            raise ArchiveIntegrityError(f"selectedPNGs[{index}] has a SHA-256 mismatch: {archive}")
        with archive.open("rb") as stream:
            if stream.read(len(PNG_SIGNATURE)) != PNG_SIGNATURE:
                raise ArchiveIntegrityError(f"selectedPNGs[{index}] is not a PNG: {archive}")
        source = row.get("source", row.get("path"))
        if source is not None:
            if not isinstance(source, str) or not source:
                raise ArchiveIntegrityError(f"selectedPNGs[{index}] source must be a nonempty string")
            if source_pins is not None:
                if source not in source_pins:
                    raise ArchiveIntegrityError(f"selectedPNGs[{index}] source is not pinned: {source}")
                if source_pins[source] != digest:
                    raise ArchiveIntegrityError(f"selectedPNGs[{index}] differs from its pinned source: {source}")
    return len(selected)


def verify_root_visual_review(root: Path, validation: Json) -> bool:
    pin = validation.get("rootVisualReviewPin")
    if pin is None:
        return False
    row = as_dict(pin)
    if not row:
        raise ArchiveIntegrityError("validation.rootVisualReviewPin must be an object")
    path = archive_file(root, row.get("path"), "validation.rootVisualReviewPin.path")
    digest = row.get("sha256")
    if not is_sha256(digest) or sha256_file(path) != digest:
        raise ArchiveIntegrityError("validation.rootVisualReviewPin has a SHA-256 mismatch")
    expected_bytes = row.get("bytes")
    if expected_bytes is not None and (not isinstance(expected_bytes, int) or expected_bytes < 0 or path.stat().st_size != expected_bytes):
        raise ArchiveIntegrityError("validation.rootVisualReviewPin has a byte-count mismatch")
    root_review = validation.get("rootVisualReview")
    if isinstance(root_review, str) and root_review != row.get("path"):
        raise ArchiveIntegrityError("rootVisualReviewPin does not name validation.rootVisualReview")
    read_json_file(path, "root visual review")
    return True


def ffprobe(path: Path) -> Json:
    result = subprocess.run(
        [
            "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
            "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(path),
        ],
        check=False,
        capture_output=True,
        text=True,
    )
    if result.returncode != 0:
        raise ArchiveIntegrityError(f"ffprobe could not read presentation video: {path}")
    try:
        value = json.loads(result.stdout)
        streams = value["streams"]
        stream = streams[0]
    except (KeyError, IndexError, TypeError, json.JSONDecodeError) as error:
        raise ArchiveIntegrityError(f"ffprobe returned no video stream: {path}") from error
    return as_dict(stream)


def verify_videos(root: Path, validation: Json, check_video_metadata: bool) -> int:
    videos = validation.get("videos")
    if videos is None:
        return 0
    if not isinstance(videos, list):
        raise ArchiveIntegrityError("validation.videos must be an array when present")
    names: set[str] = set()
    for index, value in enumerate(videos):
        row = as_dict(value)
        if not row:
            raise ArchiveIntegrityError(f"videos[{index}] must be an object")
        path = archive_file(root, row.get("path"), f"videos[{index}].path")
        name = str(path.relative_to(root))
        if name in names:
            raise ArchiveIntegrityError(f"videos has duplicate path: {name}")
        names.add(name)
        digest = row.get("sha256")
        if not is_sha256(digest) or sha256_file(path) != digest:
            raise ArchiveIntegrityError(f"videos[{index}] has a SHA-256 mismatch: {path}")
        if check_video_metadata:
            stream = ffprobe(path)
            for key, field in (("frames", "nb_read_frames"), ("width", "width"), ("height", "height")):
                expected = row.get(key)
                if expected is None:
                    continue
                if not isinstance(expected, int) or expected < 1:
                    raise ArchiveIntegrityError(f"videos[{index}].{key} must be a positive integer")
                try:
                    actual = int(stream[field])
                except (KeyError, TypeError, ValueError) as error:
                    raise ArchiveIntegrityError(f"ffprobe did not provide {field} for {path}") from error
                if actual != expected:
                    raise ArchiveIntegrityError(f"videos[{index}] {key} mismatch for {path}: {actual} != {expected}")
    return len(videos)


def normalized_archive_layout(root: Path, validation_path: Path, validation: Json) -> tuple[Json, str]:
    """Normalize the current embedded layout and the older integrity sidecar.

    Earlier player archives kept immutable mappings and selected-frame pins in
    `integrity.json`. The sidecar remains a hash-pinned artifact layout, so
    normalize it rather than treating the archive as invisible. This adapter
    does not interpret the sidecar's status or its review prose.
    """
    normalized = dict(validation)
    if not isinstance(normalized.get("losslessMappings"), list):
        integrity_path = archive_file(root, "integrity.json", "legacy integrity")
        integrity = read_json_file(integrity_path, "legacy integrity")
        validation_pin = as_dict(integrity.get("validation"))
        if validation_pin.get("path") != "validation.json" or validation_pin.get("sha256") != sha256_file(validation_path):
            raise ArchiveIntegrityError("legacy integrity does not pin the current validation.json")
        mappings = integrity.get("metadata")
        if not isinstance(mappings, list):
            raise ArchiveIntegrityError("legacy integrity.metadata must be an array")
        normalized["losslessMappings"] = mappings
        source_images = as_dict(integrity.get("sourceImages"))
        if "sourceImagePins" not in normalized and isinstance(source_images.get("pins"), str):
            normalized["sourceImagePins"] = source_images["pins"]
        if "sourceImageCount" not in normalized and isinstance(source_images.get("count"), int):
            normalized["sourceImageCount"] = source_images["count"]
        if "selectedPNGs" not in normalized and isinstance(integrity.get("selected"), list):
            normalized["selectedPNGs"] = integrity["selected"]
        if "videos" not in normalized and isinstance(integrity.get("videos"), list):
            normalized["videos"] = integrity["videos"]
        layout = "legacy-integrity-sidecar"
    else:
        layout = "embedded-validation"
    if "selectedPNGs" not in normalized and isinstance(normalized.get("selectedFrames"), list):
        normalized["selectedPNGs"] = normalized["selectedFrames"]
    if "videos" not in normalized and isinstance(normalized.get("video"), dict):
        normalized["videos"] = [normalized["video"]]
    if "assetPins" not in normalized and (root / "asset-pins.json").is_file():
        normalized["assetPins"] = "asset-pins.json"
    return normalized, layout


def verify_archive(archive: Path, *, check_video_metadata: bool = False) -> Json:
    root = archive.resolve()
    if not root.is_dir() or archive.is_symlink():
        raise ArchiveIntegrityError(f"archive must be a real directory: {archive}")
    validation_path = archive_file(root, "validation.json", "validation")
    validation = read_json_file(validation_path, "validation")
    validation, layout = normalized_archive_layout(root, validation_path, validation)

    raw_mappings = validation.get("losslessMappings")
    if not isinstance(raw_mappings, list):
        raise ArchiveIntegrityError("validation.losslessMappings must be an array")
    mappings: list[Json] = []
    sources: set[str] = set()
    archives: set[str] = set()
    for index, value in enumerate(raw_mappings):
        mapping = as_dict(value)
        if not mapping:
            raise ArchiveIntegrityError(f"losslessMappings[{index}] must be an object")
        verified = verify_mapping(root, mapping, index)
        if verified["source"] in sources:
            raise ArchiveIntegrityError(f"losslessMappings has duplicate source: {verified['source']}")
        if verified["archive"] in archives:
            raise ArchiveIntegrityError(f"losslessMappings has duplicate archive: {verified['archive']}")
        sources.add(verified["source"])
        archives.add(verified["archive"])
        mappings.append(verified)

    source_pins = read_pins(root, validation, "sourceImagePins", archived_images=True)
    capture_pins = read_pins(root, validation, "captureImagePins")
    asset_pins = read_pins(root, validation, "assetPins")
    if source_pins is not None and validation.get("sourceImageCount") is not None:
        if validation["sourceImageCount"] != len(source_pins):
            raise ArchiveIntegrityError("validation.sourceImageCount does not match sourceImagePins")
    if capture_pins is not None:
        if source_pins is None:
            raise ArchiveIntegrityError("captureImagePins requires sourceImagePins")
        for source, digest in capture_pins.items():
            if source_pins.get(source) != digest:
                raise ArchiveIntegrityError(f"captureImagePins does not match sourceImagePins: {source}")
        if validation.get("captureImageCount") is not None and validation["captureImageCount"] != len(capture_pins):
            raise ArchiveIntegrityError("validation.captureImageCount does not match captureImagePins")
    if asset_pins is not None and isinstance(validation.get("assetHashes"), dict):
        for name, digest in asset_pins.items():
            if name in validation["assetHashes"] and validation["assetHashes"][name] != digest:
                raise ArchiveIntegrityError(f"assetPins does not match validation.assetHashes: {name}")

    selected = verify_selected_pngs(root, validation, source_pins)
    root_review_pinned = verify_root_visual_review(root, validation)
    videos = verify_videos(root, validation, check_video_metadata)
    return {
        "schema": "ftkmf.model-validation-archive-integrity.v1",
        "status": "PASS",
        "archive": str(root),
        "validation": str(validation_path.relative_to(root)),
        "layout": layout,
        "metadataMappings": len(mappings),
        "sourceImagePins": 0 if source_pins is None else len(source_pins),
        "captureImagePins": 0 if capture_pins is None else len(capture_pins),
        "assetPins": 0 if asset_pins is None else len(asset_pins),
        "selectedPNGs": selected,
        "rootVisualReviewPinned": root_review_pinned,
        "videos": videos,
        "videoMetadataChecked": check_video_metadata,
        "scope": "Artifact integrity only. This result does not accept binding, motion, gameplay, or art quality.",
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("archive", type=Path, help="Directory containing validation.json and archived artifacts.")
    parser.add_argument(
        "--check-video-metadata",
        action="store_true",
        help="Use ffprobe to verify listed presentation video frame counts and dimensions.",
    )
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if args.check_video_metadata and shutil.which("ffprobe") is None:
        print(json.dumps({"status": "FAIL", "errors": ["--check-video-metadata requires ffprobe on PATH"]}, indent=2), file=sys.stderr)
        return 1
    try:
        report = verify_archive(args.archive, check_video_metadata=args.check_video_metadata)
    except ArchiveIntegrityError as error:
        print(json.dumps({"status": "FAIL", "archive": str(args.archive), "errors": [str(error)]}, indent=2), file=sys.stderr)
        return 1
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
