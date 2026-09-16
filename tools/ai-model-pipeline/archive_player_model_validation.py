#!/usr/bin/env python3
"""Archive a reviewed native player-preview validation from an explicit plan.

Player previews do not use the enemy `exercise_case.py` action ledger. This
builder preserves the separate native preview capture shape while producing the
same immutable artifact layout as new enemy archives.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path
import shutil
import subprocess
import sys
from typing import Any
from uuid import uuid4

from archive_model_validation_case import (
    ArchiveBuildError,
    PIN_AS_RESERVED_FIELDS,
    PNG_SIGNATURE,
    as_dict,
    as_list,
    find_workspace_root,
    is_sha256,
    metadata_mapping,
    pin,
    profile_for,
    render_video,
    require_subset,
    sha256_file,
    workspace_path,
    workspace_relative,
    write_json,
)


Json = dict[str, Any]
DERIVED_FIELDS = {
    "archivePlan",
    "assetHashes",
    "assetPins",
    "captureImageCount",
    "captureImagePins",
    "captures",
    "losslessMappings",
    "rootVisualReview",
    "rootVisualReviewPin",
    "selectedPNGs",
    "session",
    "sourceImageCount",
    "sourceImagePins",
    "videos",
    "visualReview",
}


def frame_clips(frame: Json) -> set[str]:
    result: set[str] = set()
    animator = as_dict(frame.get("animator"))
    for layer in as_list(animator.get("layers")):
        for playing in as_list(as_dict(layer).get("playing")):
            name = as_dict(playing).get("name")
            if isinstance(name, str) and name:
                result.add(name)
    return result


def capture_specs(plan: Json) -> list[Json]:
    values = plan.get("captures")
    if not isinstance(values, list) or not values:
        raise ArchiveBuildError("plan.captures must be a nonempty array for a player preview")
    names: set[str] = set()
    result: list[Json] = []
    for index, value in enumerate(values):
        spec = as_dict(value)
        label = spec.get("label")
        if not isinstance(label, str) or not label:
            raise ArchiveBuildError(f"plan.captures[{index}].label must be a nonempty string")
        if label in names:
            raise ArchiveBuildError(f"plan.captures has duplicate label {label!r}")
        names.add(label)
        if not isinstance(spec.get("rawCapture"), str) or not spec["rawCapture"]:
            raise ArchiveBuildError(f"plan.captures[{index}].rawCapture must be a path")
        if not isinstance(spec.get("scope"), str) or not spec["scope"]:
            raise ArchiveBuildError(f"plan.captures[{index}].scope must be a nonempty string")
        identity = spec.get("frameIdentity")
        if not isinstance(identity, dict) or not identity:
            raise ArchiveBuildError(f"plan.captures[{index}].frameIdentity must name expected frame fields")
        clips = spec.get("requiredClips", [])
        if not isinstance(clips, list) or not all(isinstance(clip, str) and clip for clip in clips):
            raise ArchiveBuildError(f"plan.captures[{index}].requiredClips must be strings when present")
        result.append(spec)
    return result


def add_metadata_source(sources: dict[Path, set[str]], source: Path, pin_as: str | None = None) -> None:
    roles = sources.setdefault(source, set())
    if pin_as:
        roles.add(pin_as)


def prepare_metadata(root: Path, plan_path: Path, plan: Json, review_path: Path, specs: list[Json], static_fields: set[str]) -> tuple[dict[Path, set[str]], Json | None]:
    sources: dict[Path, set[str]] = {}
    add_metadata_source(sources, plan_path, "archivePlan")
    add_metadata_source(sources, review_path, "visualReviewSource")
    profile_document: Json | None = None
    metadata = plan.get("metadata", [])
    if not isinstance(metadata, list):
        raise ArchiveBuildError("plan.metadata must be an array when present")
    for index, value in enumerate(metadata):
        row = {"path": value} if isinstance(value, str) else as_dict(value)
        if not row:
            raise ArchiveBuildError(f"plan.metadata[{index}] must be a path or object")
        source = workspace_path(root, row.get("path"), f"plan.metadata[{index}].path")
        pin_as = row.get("pinAs")
        if pin_as is not None:
            if not isinstance(pin_as, str) or not pin_as.isidentifier() or pin_as in PIN_AS_RESERVED_FIELDS or pin_as in static_fields:
                raise ArchiveBuildError(f"plan.metadata[{index}].pinAs is invalid or reserved: {pin_as!r}")
        add_metadata_source(sources, source, pin_as)
        if pin_as == "runtimeProfile":
            profile_document = json.loads(source.read_text())
            if not isinstance(profile_document, dict):
                raise ArchiveBuildError("runtimeProfile must contain a JSON object")
    for spec in specs:
        add_metadata_source(sources, workspace_path(root, spec["rawCapture"], f"capture {spec['label']!r} rawCapture"))
    tool_source = Path(__file__).resolve()
    try:
        workspace_relative(root, tool_source)
    except ArchiveBuildError:
        pass
    else:
        add_metadata_source(sources, tool_source)
    return sources, profile_document


def validate_player_profile(profile: Json, runtime_profile: Json | None) -> None:
    if not all(isinstance(profile.get(key), str) and profile[key] for key in ("key", "baseClass", "skinset", "defaultSkinType")):
        raise ArchiveBuildError("plan.validation.profile must declare key, baseClass, skinset, and defaultSkinType")
    if not isinstance(profile.get("renderers"), list) or not profile["renderers"]:
        raise ArchiveBuildError("plan.validation.profile must declare at least one renderer")
    if runtime_profile is not None:
        observed = profile_for(runtime_profile, profile["key"])
        if observed is None or observed != profile:
            raise ArchiveBuildError("plan.validation.profile does not match runtimeProfile")


def build_archive(plan_path: Path, *, workspace_root: Path | None = None, render_videos: bool = True) -> Json:
    root = workspace_root.resolve() if workspace_root is not None else find_workspace_root(plan_path.parent)
    plan_path = workspace_path(root, str(plan_path), "plan")
    plan = json.loads(plan_path.read_text())
    if not isinstance(plan, dict) or plan.get("schema") != "ftkmf.player-model-validation-archive-plan.v1":
        raise ArchiveBuildError("plan.schema must be ftkmf.player-model-validation-archive-plan.v1")
    session = plan.get("session")
    if not isinstance(session, str) or not session:
        raise ArchiveBuildError("plan.session must be a nonempty string")
    output_value = plan.get("output")
    if not isinstance(output_value, str) or not output_value:
        raise ArchiveBuildError("plan.output must be a nonempty workspace-relative directory")
    output = workspace_path(root, output_value, "plan.output", must_exist=False)
    if output.exists():
        raise ArchiveBuildError(f"archive output already exists and will not be overwritten: {output}")
    if output.suffix:
        raise ArchiveBuildError(f"archive output must be a directory path: {output}")
    review_path = workspace_path(root, plan.get("visualReview"), "plan.visualReview")
    review = json.loads(review_path.read_text())
    if not isinstance(review, dict) or review.get("session") != session:
        raise ArchiveBuildError("visual review session does not match plan.session")
    static = as_dict(plan.get("validation"))
    if not static:
        raise ArchiveBuildError("plan.validation must be a nonempty object")
    reserved = sorted(set(static) & DERIVED_FIELDS)
    if reserved:
        raise ArchiveBuildError(f"plan.validation may not set derived fields: {', '.join(reserved)}")
    for field in ("status", "revision", "displayName"):
        if not isinstance(static.get(field), str) or not static[field]:
            raise ArchiveBuildError(f"plan.validation.{field} must be a nonempty string")
    profile = as_dict(static.get("profile"))
    specs = capture_specs(plan)
    sources, runtime_profile = prepare_metadata(root, plan_path, plan, review_path, specs, set(static))
    validate_player_profile(profile, runtime_profile)
    preview = as_dict(as_dict(static.get("avatarOwners")).get("preview"))
    if not isinstance(preview.get("observedAvatars"), int) or preview["observedAvatars"] < 0:
        raise ArchiveBuildError("plan.validation.avatarOwners.preview must record observedAvatars")
    binding = as_dict(static.get("binding")) or as_dict(review.get("binding"))
    review_binding = as_dict(review.get("binding"))
    if not binding or not review_binding:
        raise ArchiveBuildError("plan.validation.binding and visualReview.binding must be objects")
    require_subset(binding, review_binding, "validation.binding")
    static = {**static, "binding": binding}
    if "renderers" not in static:
        static["renderers"] = [binding]

    raw_assets = plan.get("assetFiles")
    if not isinstance(raw_assets, list) or not raw_assets:
        raise ArchiveBuildError("plan.assetFiles must list at least one original asset")
    assets: dict[str, Path] = {}
    for index, value in enumerate(raw_assets):
        source = workspace_path(root, value, f"plan.assetFiles[{index}]")
        if source.name in assets:
            raise ArchiveBuildError(f"plan.assetFiles has duplicate basename {source.name!r}")
        assets[source.name] = source
        if source.suffix.lower() != ".png":
            add_metadata_source(sources, source)

    stage = output.with_name(f".{output.name}.archive-stage-{uuid4().hex}")
    if stage.exists():
        raise ArchiveBuildError(f"unexpected existing archive stage: {stage}")
    stage.mkdir(parents=True)
    source_image_pins: dict[str, str] = {}
    capture_image_pins: dict[str, str] = {}
    captures: list[Json] = []
    videos: list[Json] = []
    for spec in specs:
        label = spec["label"]
        raw_path = workspace_path(root, spec["rawCapture"], f"capture {label!r} rawCapture")
        raw = json.loads(raw_path.read_text())
        if not isinstance(raw, dict) or raw.get("session") != session:
            raise ArchiveBuildError(f"capture {label!r} session does not match plan.session")
        if raw.get("ok") is not True:
            raise ArchiveBuildError(f"capture {label!r} did not complete successfully")
        frames = raw.get("frames")
        if not isinstance(frames, list) or not frames:
            raise ArchiveBuildError(f"capture {label!r} has no frames")
        for index, value in enumerate(frames):
            frame = as_dict(value)
            for key, expected in as_dict(spec["frameIdentity"]).items():
                if frame.get(key) != expected:
                    raise ArchiveBuildError(f"capture {label!r} frame {index} changed {key!r}")
            clips = frame_clips(frame)
            if not set(spec.get("requiredClips", [])).issubset(clips):
                raise ArchiveBuildError(f"capture {label!r} frame {index} lacks a required native clip")
        frame_dir = raw_path.with_suffix("")
        pngs = sorted(frame_dir.glob("*.png"))
        if len(pngs) != len(frames) or [path.name for path in pngs] != [f"{index:04d}.png" for index in range(len(frames))]:
            raise ArchiveBuildError(f"capture {label!r} PNG sequence does not match frames")
        for image in pngs:
            with image.open("rb") as stream:
                if stream.read(len(PNG_SIGNATURE)) != PNG_SIGNATURE:
                    raise ArchiveBuildError(f"capture {label!r} has a non-PNG image")
            name = workspace_relative(root, image)
            digest = sha256_file(image)
            source_image_pins[name] = digest
            capture_image_pins[name] = digest
        capture = {
            "label": label,
            "complete": True,
            "termination": spec.get("termination", "complete"),
            "rawCapture": pin(root, raw_path),
            "frameCount": len(frames),
            "scope": spec["scope"],
            "clips": list(spec.get("requiredClips", [])),
            "stateClips": list(spec.get("requiredClips", [])),
            "timingMode": raw.get("timingMode"),
            "requestedFps": raw.get("requestedFps"),
        }
        captures.append(capture)
        if render_videos:
            video_path = stage / "video" / f"{label}.mp4"
            video_path.parent.mkdir(parents=True, exist_ok=True)
            video = render_video(frame_dir, len(frames), video_path, int(spec.get("playbackFps", 12)))
            video["label"] = label
            video["path"] = f"video/{label}.mp4"
            videos.append(video)
    for asset in assets.values():
        if asset.suffix.lower() == ".png":
            source_image_pins[workspace_relative(root, asset)] = sha256_file(asset)

    selected: list[Json] = []
    review_frames = review.get("frames")
    if not isinstance(review_frames, list) or not review_frames:
        raise ArchiveBuildError("visualReview.frames must be a nonempty array")
    selected_names: set[str] = set()
    for index, value in enumerate(review_frames):
        row = as_dict(value)
        source = workspace_path(root, row.get("path"), f"visualReview.frames[{index}].path")
        digest = row.get("sha256")
        if not is_sha256(digest) or sha256_file(source) != digest:
            raise ArchiveBuildError(f"visualReview.frames[{index}] source hash does not match")
        source_name = workspace_relative(root, source)
        if source_image_pins.get(source_name) != digest:
            raise ArchiveBuildError(f"visualReview.frames[{index}] is not a pinned capture or asset PNG")
        label = row.get("label")
        frame_index = row.get("index")
        if not isinstance(label, str) or not label or not isinstance(frame_index, int) or frame_index < 0:
            raise ArchiveBuildError(f"visualReview.frames[{index}] must name a label and nonnegative index")
        filename = f"{label}-{frame_index:04d}.png"
        if filename in selected_names:
            raise ArchiveBuildError(f"visualReview.frames selects duplicate archive image {filename}")
        selected_names.add(filename)
        destination = stage / "selected" / filename
        destination.parent.mkdir(parents=True, exist_ok=True)
        shutil.copy2(source, destination)
        if sha256_file(destination) != digest:
            raise ArchiveBuildError(f"selected copy hash mismatch for {source}")
        selected.append({**row, "source": source_name, "archive": str(destination.relative_to(stage)), "sha256": digest})

    mappings = [metadata_mapping(root, source, stage) for source in sorted(sources)]
    direct_pins: Json = {}
    for source, roles in sources.items():
        source_pin = pin(root, source)
        for role in roles:
            direct_pins[role] = source_pin
    asset_hashes = {name: sha256_file(path) for name, path in sorted(assets.items())}
    write_json(stage / "asset-pins.json", asset_hashes)
    write_json(stage / "source-image-pins.json", source_image_pins)
    write_json(stage / "capture-image-pins.json", capture_image_pins)
    (stage / "visual-review.json").write_bytes(review_path.read_bytes())
    if sha256_file(stage / "visual-review.json") != sha256_file(review_path):
        raise ArchiveBuildError("visual-review copy hash mismatch")
    validation: Json = {
        **static,
        "schema": "ftkmf.player-model-validation-archive.v1",
        "session": session,
        "assetHashes": asset_hashes,
        "captures": captures,
        "rootVisualReview": "visual-review.json",
        "rootVisualReviewPin": {
            "path": "visual-review.json",
            "sha256": sha256_file(stage / "visual-review.json"),
            "bytes": (stage / "visual-review.json").stat().st_size,
        },
        "visualReview": review.get("reviewStatus", static["status"]),
        "selectedPNGs": selected,
        "videos": videos,
        "sourceImageCount": len(source_image_pins),
        "captureImageCount": len(capture_image_pins),
        "sourceImagePins": "source-image-pins.json",
        "captureImagePins": "capture-image-pins.json",
        "assetPins": "asset-pins.json",
        "losslessMappings": mappings,
        **direct_pins,
    }
    if runtime_profile is not None:
        runtime_path = next(source for source, roles in sources.items() if "runtimeProfile" in roles)
        validation["profileDocumentSha256"] = sha256_file(runtime_path)
    write_json(stage / "validation.json", validation)
    (stage / "README.md").write_text(
        f"# {static['displayName']} native player-preview validation\n\n"
        f"This immutable archive preserves reviewed native preview evidence from session `{session}`. "
        "`validation.json` pins source metadata, original assets, every capture PNG, selected "
        "root-reviewed originals, and presentation derivatives. `visual-review.json` records "
        "the observed scope and limits.\n\n"
        "Recheck artifact integrity with:\n\n"
        "```sh\n"
        "python3 tools/ai-model-pipeline/verify_model_validation_archive.py "
        f"{workspace_relative(root, output)} --check-video-metadata\n"
        "```\n\n"
        "An integrity pass does not accept combat, equipment, teardown, portraits, or final art beyond "
        "the exact reviewed preview records.\n"
    )
    stage.rename(output)
    return {
        "status": "PASS",
        "archive": workspace_relative(root, output),
        "validation": workspace_relative(root, output / "validation.json"),
        "session": session,
        "captures": len(captures),
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("plan", type=Path, help="JSON plan with schema ftkmf.player-model-validation-archive-plan.v1")
    parser.add_argument("--skip-videos", action="store_true", help="Do not create presentation MP4 derivatives.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if not args.skip_videos and (shutil.which("ffmpeg") is None or shutil.which("ffprobe") is None):
        print(json.dumps({"status": "FAIL", "errors": ["ffmpeg and ffprobe are required unless --skip-videos is used"]}, indent=2), file=sys.stderr)
        return 1
    try:
        report = build_archive(args.plan, render_videos=not args.skip_videos)
    except (ArchiveBuildError, OSError, json.JSONDecodeError, subprocess.CalledProcessError) as error:
        print(json.dumps({"status": "FAIL", "plan": str(args.plan), "errors": [str(error)]}, indent=2), file=sys.stderr)
        return 1
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
