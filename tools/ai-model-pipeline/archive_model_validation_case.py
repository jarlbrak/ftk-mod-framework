#!/usr/bin/env python3
"""Archive one reviewed isolated model-validation case from an explicit plan.

The plan supplies the source-specific identity and review conclusions. This
tool only freezes the reviewed inputs, verifies the declared frame identity,
pins source artifacts, and writes a non-overwriteable archive. It never treats
an archive build as evidence that an unreviewed model works.
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
from uuid import uuid4


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
RESERVED_VALIDATION_FIELDS = {
    "archivePlan",
    "assetHashes",
    "assetPins",
    "captureImageCount",
    "captureImagePins",
    "captures",
    "caseResult",
    "catalogSha256",
    "enemy",
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
PIN_AS_RESERVED_FIELDS = RESERVED_VALIDATION_FIELDS | {
    "baseEnemy",
    "binding",
    "displayName",
    "explicitKillFixture",
    "finalReady",
    "limits",
    "nativeChassis",
    "nativeEnemy",
    "ordinaryHit",
    "ordinaryLethal",
    "profile",
    "profileDocumentSha256",
    "rendererPaths",
    "renderers",
    "resourcePrefab",
    "revision",
    "schema",
    "sourceKind",
    "sourceRendererIds",
    "status",
}
PNG_SIGNATURE = b"\x89PNG\r\n\x1a\n"


class ArchiveBuildError(ValueError):
    """A plan or input artifact cannot produce a trustworthy archive."""


def as_dict(value: Any) -> Json:
    return value if isinstance(value, dict) else {}


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def sha256_file(path: Path) -> str:
    digest = hashlib.sha256()
    with path.open("rb") as stream:
        for chunk in iter(lambda: stream.read(1024 * 1024), b""):
            digest.update(chunk)
    return digest.hexdigest()


def is_sha256(value: Any) -> bool:
    return isinstance(value, str) and len(value) == 64 and all(character in "0123456789abcdef" for character in value)


def find_workspace_root(start: Path) -> Path:
    for candidate in (start.resolve(), *start.resolve().parents):
        if (candidate / "FTKModFramework").is_dir():
            return candidate
    raise ArchiveBuildError(f"could not locate FTK workspace above {start}")


def workspace_path(root: Path, value: Any, label: str, *, must_exist: bool = True) -> Path:
    if not isinstance(value, str) or not value:
        raise ArchiveBuildError(f"{label} must be a nonempty path")
    candidate = Path(value)
    candidate = candidate if candidate.is_absolute() else root / candidate
    resolved = candidate.resolve(strict=False)
    try:
        resolved.relative_to(root.resolve())
    except ValueError as error:
        raise ArchiveBuildError(f"{label} must stay inside the workspace: {value!r}") from error
    if must_exist and not resolved.is_file():
        raise ArchiveBuildError(f"{label} is not a regular file: {resolved}")
    if resolved.suffix.lower() in FORBIDDEN_SOURCE_SUFFIXES:
        raise ArchiveBuildError(f"{label} has forbidden game payload suffix {resolved.suffix!r}: {resolved}")
    return resolved


def workspace_relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError as error:
        raise ArchiveBuildError(f"path is outside workspace: {path}") from error


def read_json(path: Path, label: str) -> Json:
    try:
        value = json.loads(path.read_text())
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ArchiveBuildError(f"{label} is not a JSON object: {path}") from error
    if not isinstance(value, dict):
        raise ArchiveBuildError(f"{label} must contain a JSON object: {path}")
    return value


def write_json(path: Path, value: Any) -> None:
    path.parent.mkdir(parents=True, exist_ok=True)
    path.write_text(json.dumps(value, indent=2) + "\n")


def pin(root: Path, path: Path) -> Json:
    return {
        "path": workspace_relative(root, path),
        "sha256": sha256_file(path),
        "bytes": path.stat().st_size,
    }


def require_subset(expected: Any, actual: Any, label: str) -> None:
    if isinstance(expected, dict):
        if not isinstance(actual, dict):
            raise ArchiveBuildError(f"{label} expected an object in case result")
        for key, value in expected.items():
            if key not in actual:
                raise ArchiveBuildError(f"{label}.{key} is absent from case result")
            require_subset(value, actual[key], f"{label}.{key}")
        return
    if expected != actual:
        raise ArchiveBuildError(f"{label} does not match case result: {expected!r} != {actual!r}")


def action_artifact(root: Path, action: Json, name: str, action_name: str) -> Path:
    capture = as_dict(action.get("capture"))
    value = capture.get(name, action.get(name))
    row = as_dict(value)
    return workspace_path(root, row.get("path"), f"case action {action_name!r} {name}")


def action_map(case: Json) -> dict[str, Json]:
    rows: list[Json] = []
    names: dict[str, int] = {}
    for value in as_list(case.get("actions")):
        action = as_dict(value)
        name = action.get("action")
        if not isinstance(name, str) or not name:
            raise ArchiveBuildError("case actions must each name an action")
        if action.get("actionAccepted") is not True:
            raise ArchiveBuildError(f"case action {name!r} was not accepted")
        rows.append(action)
        names[name] = names.get(name, 0) + 1
    actions: dict[str, Json] = {}
    for action in rows:
        name = str(action["action"])
        if names[name] == 1:
            key = name
        else:
            attempt = action.get("attempt")
            if not isinstance(attempt, int) or attempt < 1:
                raise ArchiveBuildError(
                    f"repeated case action {name!r} needs a positive integer attempt number"
                )
            key = f"{name}-attempt-{attempt}"
        if key in actions:
            raise ArchiveBuildError(f"case has duplicate archive action key {key!r}")
        actions[key] = action
    if not actions:
        raise ArchiveBuildError("case has no accepted actions to archive")
    return actions


def ordinary_attack(actions: dict[str, Json]) -> tuple[str, Json] | None:
    attempts = [(key, action) for key, action in actions.items() if action.get("action") == "attack"]
    if len(attempts) == 1:
        return attempts[0]
    successful = [
        item for item in attempts
        if as_dict(item[1].get("hpOutcome")).get("status") == "nonlethal_hp_loss"
    ]
    if len(successful) == 1:
        return successful[0]
    if len(successful) > 1:
        raise ArchiveBuildError("case has more than one accepted nonlethal ordinary attack")
    return None


def fixture_kill(actions: dict[str, Json]) -> tuple[str, Json] | None:
    matches = [(key, action) for key, action in actions.items() if action.get("action") == "kill-fixture"]
    if len(matches) > 1:
        raise ArchiveBuildError("case has more than one accepted kill-fixture action")
    return matches[0] if matches else None


def action_window_hp_outcome(action: Json, enemy: str) -> Json:
    """Prefer the immediate post-capture state over a later ready-state poll.

    Exercise records created before the action-window fix can contain an
    hpOutcome measured after waiting for another player turn. That wait may
    include unrelated combat turns or status ticks. The action's own before
    and after states bound the retained capture and remain the causal window.
    """
    fallback = as_dict(action.get("hpOutcome"))
    result = as_dict(as_dict(action.get("actionResult")).get("result"))
    fid = result.get("target")
    if not isinstance(fid, dict):
        return fallback

    def matching_hp(state: object) -> int | float | None:
        combat = as_dict(as_dict(state).get("combat"))
        matches = [
            row for row in as_list(combat.get("enemies"))
            if as_dict(row).get("type") == enemy and as_dict(row).get("fid") == fid
        ]
        if len(matches) != 1:
            return None
        value = as_dict(matches[0]).get("hp")
        if not isinstance(value, (int, float)) or isinstance(value, bool):
            return None
        return value

    before_hp = matching_hp(action.get("before"))
    after_hp = matching_hp(action.get("after"))
    if before_hp is None or after_hp is None or before_hp <= 0:
        return fallback
    if 0 < after_hp < before_hp:
        status = "nonlethal_hp_loss"
    elif after_hp <= 0:
        status = "target_removed_or_zero_hp_after_action"
    elif after_hp == before_hp:
        status = "no_hp_loss_unclassified"
    else:
        status = "hp_increase_unclassified"
    outcome: Json = {
        "status": status,
        "beforeHp": before_hp,
        "afterHp": after_hp,
        "boundary": "Immediate action-window states around the retained capture; later Ready-state drift is not attributed to this attack.",
    }
    native = fallback.get("nativeOutcome")
    if isinstance(native, dict):
        outcome["nativeOutcome"] = native
    if fallback.get("afterHp") != after_hp:
        outcome["laterReadyObservedHp"] = fallback.get("afterHp")
    return outcome


def capture_spec_map(plan: Json, actions: dict[str, Json]) -> dict[str, Json]:
    raw_specs = plan.get("captures")
    if not isinstance(raw_specs, dict):
        raise ArchiveBuildError("plan.captures must be an object keyed by archive action")
    if set(raw_specs) != set(actions):
        raise ArchiveBuildError(
            f"plan.captures must name exactly the archive actions: {sorted(actions)}"
        )
    specs: dict[str, Json] = {}
    for name, value in raw_specs.items():
        spec = as_dict(value)
        if not spec or not isinstance(spec.get("label"), str) or not spec["label"]:
            raise ArchiveBuildError(f"plan.captures.{name} needs a nonempty label")
        if not isinstance(spec.get("scope"), str) or not spec["scope"]:
            raise ArchiveBuildError(f"plan.captures.{name} needs a nonempty scope")
        identity = spec.get("frameIdentity", {})
        if not isinstance(identity, dict) or not identity:
            raise ArchiveBuildError(f"plan.captures.{name}.frameIdentity must name expected frame fields")
        specs[name] = spec
    return specs


def validate_frame_identity(raw_capture: Json, identity: Json, action_name: str) -> None:
    frames = raw_capture.get("frames")
    if not isinstance(frames, list) or not frames:
        raise ArchiveBuildError(f"raw capture for {action_name!r} has no frames")
    for index, value in enumerate(frames):
        frame = as_dict(value)
        if not frame:
            raise ArchiveBuildError(f"raw capture {action_name!r} frame {index} is not an object")
        for key, expected in identity.items():
            if frame.get(key) != expected:
                raise ArchiveBuildError(
                    f"raw capture {action_name!r} frame {index} changed {key!r}: "
                    f"{frame.get(key)!r} != {expected!r}"
                )


def metadata_mapping(root: Path, source: Path, output: Path) -> Json:
    relative = workspace_relative(root, source)
    archive = output / "metadata" / f"{relative}.gz"
    archive.parent.mkdir(parents=True, exist_ok=True)
    raw = source.read_bytes()
    archive.write_bytes(gzip.compress(raw, mtime=0))
    if gzip.decompress(archive.read_bytes()) != raw:
        raise ArchiveBuildError(f"gzip round-trip failed for {source}")
    return {
        "source": relative,
        "sourceSha256": hashlib.sha256(raw).hexdigest(),
        "archive": str(archive.relative_to(output)),
        "archiveSha256": sha256_file(archive),
        "encoding": "gzip-lossless",
        "bytes": len(raw),
    }


def add_metadata_source(sources: dict[Path, set[str]], path: Path, pin_as: str | None = None) -> None:
    roles = sources.setdefault(path, set())
    if pin_as:
        roles.add(pin_as)


def profile_for(runtime_profile: Json, enemy: str) -> Json | None:
    profiles = runtime_profile.get("profiles")
    if not isinstance(profiles, list):
        return None
    matches = [as_dict(value) for value in profiles if as_dict(value).get("key") == enemy]
    if len(matches) != 1:
        return None
    return matches[0]


def render_video(source_dir: Path, frames: int, output: Path, fps: int) -> Json:
    subprocess.run(
        [
            "ffmpeg", "-y", "-hide_banner", "-loglevel", "error", "-framerate", str(fps),
            "-i", str(source_dir / "%04d.png"), "-frames:v", str(frames),
            "-c:v", "libx264", "-crf", "18", "-pix_fmt", "yuv420p", str(output),
        ],
        check=True,
    )
    result = subprocess.run(
        [
            "ffprobe", "-v", "error", "-count_frames", "-select_streams", "v:0",
            "-show_entries", "stream=nb_read_frames,width,height", "-of", "json", str(output),
        ],
        check=True,
        capture_output=True,
        text=True,
    )
    try:
        stream = json.loads(result.stdout)["streams"][0]
        observed_frames = int(stream["nb_read_frames"])
        width = int(stream["width"])
        height = int(stream["height"])
    except (KeyError, IndexError, TypeError, ValueError, json.JSONDecodeError) as error:
        raise ArchiveBuildError(f"ffprobe returned no usable video stream for {output}") from error
    if observed_frames != frames:
        raise ArchiveBuildError(f"presentation video frame count mismatch for {output}: {observed_frames} != {frames}")
    return {
        "path": output.name,
        "sha256": sha256_file(output),
        "frames": frames,
        "width": width,
        "height": height,
        "playbackFps": fps,
        "timing": "Presentation derivative; raw capture metadata is the timing evidence.",
    }


def prepare_metadata_sources(root: Path, plan_path: Path, plan: Json, case_path: Path, review_path: Path, actions: dict[str, Json], static_fields: set[str]) -> tuple[dict[Path, set[str]], Json | None]:
    sources: dict[Path, set[str]] = {}
    add_metadata_source(sources, plan_path, "archivePlan")
    add_metadata_source(sources, case_path, "caseResult")
    add_metadata_source(sources, review_path, "visualReviewSource")
    runtime_profile: Json | None = None
    metadata = plan.get("metadata", [])
    if not isinstance(metadata, list):
        raise ArchiveBuildError("plan.metadata must be an array when present")
    for index, value in enumerate(metadata):
        row = {"path": value} if isinstance(value, str) else as_dict(value)
        if not row:
            raise ArchiveBuildError(f"plan.metadata[{index}] must be a path or object")
        path = workspace_path(root, row.get("path"), f"plan.metadata[{index}].path")
        pin_as = row.get("pinAs")
        if pin_as is not None:
            if not isinstance(pin_as, str) or not pin_as.isidentifier() or pin_as in PIN_AS_RESERVED_FIELDS or pin_as in static_fields:
                raise ArchiveBuildError(f"plan.metadata[{index}].pinAs is invalid or reserved: {pin_as!r}")
        add_metadata_source(sources, path, pin_as)
        if pin_as == "runtimeProfile":
            runtime_profile = read_json(path, "runtimeProfile")
    for name, action in actions.items():
        add_metadata_source(sources, action_artifact(root, action, "rawCapture", name))
        add_metadata_source(sources, action_artifact(root, action, "rawResult", name))
        add_metadata_source(sources, action_artifact(root, action, "journal", name))
    # Production plans run from this repository, so retaining the exact
    # archiver source makes a replay audit easier. Unit and external callers
    # may supply a separate workspace root, where that source is intentionally
    # outside the archive provenance boundary.
    tool_source = Path(__file__).resolve()
    try:
        workspace_relative(root, tool_source)
    except ArchiveBuildError:
        pass
    else:
        add_metadata_source(sources, tool_source)
    return sources, runtime_profile


def build_archive(plan_path: Path, *, workspace_root: Path | None = None, render_videos: bool = True) -> Json:
    root = workspace_root.resolve() if workspace_root is not None else find_workspace_root(plan_path.parent)
    plan_path = workspace_path(root, str(plan_path), "plan")
    plan = read_json(plan_path, "plan")
    if plan.get("schema") != "ftkmf.model-validation-archive-plan.v1":
        raise ArchiveBuildError("plan.schema must be ftkmf.model-validation-archive-plan.v1")
    output_value = plan.get("output")
    if not isinstance(output_value, str) or not output_value:
        raise ArchiveBuildError("plan.output must be a nonempty workspace-relative directory")
    output = workspace_path(root, output_value, "plan.output", must_exist=False)
    if output.exists():
        raise ArchiveBuildError(f"archive output already exists and will not be overwritten: {output}")
    if output.suffix:
        raise ArchiveBuildError(f"archive output must be a directory path: {output}")

    case_path = workspace_path(root, plan.get("caseResult"), "plan.caseResult")
    review_path = workspace_path(root, plan.get("visualReview"), "plan.visualReview")
    case = read_json(case_path, "case result")
    review = read_json(review_path, "visual review")
    session = case.get("session")
    enemy = case.get("enemy")
    if not isinstance(session, str) or not session or not isinstance(enemy, str) or not enemy:
        raise ArchiveBuildError("case result must name a session and enemy")
    if review.get("session") != session:
        raise ArchiveBuildError("visual review session does not match case result")
    actions = action_map(case)
    capture_specs = capture_spec_map(plan, actions)
    static_validation = as_dict(plan.get("validation"))
    if not static_validation:
        raise ArchiveBuildError("plan.validation must be a nonempty object")
    reserved = sorted(set(static_validation) & RESERVED_VALIDATION_FIELDS)
    if reserved:
        raise ArchiveBuildError(f"plan.validation may not set derived fields: {', '.join(reserved)}")
    for field in ("status", "revision", "displayName"):
        if not isinstance(static_validation.get(field), str) or not static_validation[field]:
            raise ArchiveBuildError(f"plan.validation.{field} must be a nonempty string")

    sources, runtime_profile = prepare_metadata_sources(
        root, plan_path, plan, case_path, review_path, actions, set(static_validation)
    )
    asset_files = plan.get("assetFiles")
    if not isinstance(asset_files, list) or not asset_files:
        raise ArchiveBuildError("plan.assetFiles must list at least one original asset")
    assets: dict[str, Path] = {}
    for index, value in enumerate(asset_files):
        path = workspace_path(root, value, f"plan.assetFiles[{index}]")
        if path.name in assets:
            raise ArchiveBuildError(f"plan.assetFiles has duplicate basename {path.name!r}")
        assets[path.name] = path
        if path.suffix.lower() != ".png":
            add_metadata_source(sources, path)

    binding = as_dict(static_validation.get("binding")) or as_dict(review.get("binding"))
    if not binding:
        raise ArchiveBuildError("plan.validation.binding or visualReview.binding must be an object")
    review_binding = as_dict(review.get("binding"))
    if not review_binding:
        raise ArchiveBuildError("visualReview.binding must be an object")
    require_subset(binding, review_binding, "validation.binding")
    static_validation = {**static_validation, "binding": binding}
    if "renderers" not in static_validation:
        static_validation["renderers"] = [binding]
    if "finalReady" not in static_validation and "finalReady" in case:
        static_validation["finalReady"] = case["finalReady"]
    if "finalReady" in static_validation and "finalReady" in case:
        require_subset(static_validation["finalReady"], case["finalReady"], "validation.finalReady")
    if "profile" not in static_validation and runtime_profile is not None:
        profile = profile_for(runtime_profile, enemy)
        if profile is not None:
            static_validation["profile"] = profile
    if runtime_profile is not None and isinstance(static_validation.get("profile"), dict):
        profile = profile_for(runtime_profile, enemy)
        if profile is not None and static_validation["profile"] != profile:
            raise ArchiveBuildError("plan.validation.profile does not match runtimeProfile for the case enemy")

    ordinary_action = ordinary_attack(actions)
    if "ordinaryHit" not in static_validation and ordinary_action is not None:
        _, action = ordinary_action
        outcome = action_window_hp_outcome(action, enemy)
        if outcome:
            static_validation["ordinaryHit"] = {
                key: outcome[key] for key in ("beforeHp", "afterHp") if key in outcome
            }
            static_validation["ordinaryHit"]["focus"] = action.get("focus")
            if "laterReadyObservedHp" in outcome:
                static_validation["ordinaryHit"]["laterReadyObservedHp"] = outcome["laterReadyObservedHp"]
                static_validation["ordinaryHit"]["boundary"] = outcome["boundary"]
    if isinstance(static_validation.get("ordinaryHit"), dict):
        if ordinary_action is None:
            raise ArchiveBuildError("validation.ordinaryHit needs one accepted nonlethal ordinary attack")
        _, action = ordinary_action
        outcome = action_window_hp_outcome(action, enemy)
        ordinary = static_validation["ordinaryHit"]
        for key in ("beforeHp", "afterHp"):
            if key in ordinary and ordinary[key] != outcome.get(key):
                raise ArchiveBuildError(f"validation.ordinaryHit.{key} does not match attack hpOutcome")
        if "focus" in ordinary and ordinary["focus"] != action.get("focus"):
            raise ArchiveBuildError("validation.ordinaryHit.focus does not match attack action")
    kill_action = fixture_kill(actions)
    if "explicitKillFixture" not in static_validation and kill_action is not None:
        key, _ = kill_action
        static_validation["explicitKillFixture"] = {
            "action": key,
            "accepted": True,
        }

    stage = output.with_name(f".{output.name}.archive-stage-{uuid4().hex}")
    if stage.exists():
        raise ArchiveBuildError(f"unexpected existing archive stage: {stage}")
    stage.mkdir(parents=True)
    try:
        source_image_pins: dict[str, str] = {}
        capture_image_pins: dict[str, str] = {}
        captures: list[Json] = []
        videos: list[Json] = []
        for name, action in actions.items():
            spec = capture_specs[name]
            raw_path = action_artifact(root, action, "rawCapture", name)
            raw = read_json(raw_path, f"raw capture {name!r}")
            validate_frame_identity(raw, as_dict(spec["frameIdentity"]), name)
            frames = as_list(raw.get("frames"))
            frame_dir = raw_path.with_suffix("")
            pngs = sorted(frame_dir.glob("*.png"))
            if len(pngs) != len(frames) or [path.name for path in pngs] != [f"{index:04d}.png" for index in range(len(frames))]:
                raise ArchiveBuildError(f"raw capture {name!r} PNG sequence does not match frames")
            for image in pngs:
                if not image.is_file() or image.is_symlink():
                    raise ArchiveBuildError(f"raw capture {name!r} image is not a real file: {image}")
                with image.open("rb") as stream:
                    if stream.read(len(PNG_SIGNATURE)) != PNG_SIGNATURE:
                        raise ArchiveBuildError(f"raw capture {name!r} image is not PNG: {image}")
                image_path = workspace_relative(root, image)
                digest = sha256_file(image)
                source_image_pins[image_path] = digest
                capture_image_pins[image_path] = digest
            boundary = as_dict(as_dict(action.get("capture")).get("boundary"))
            if not boundary:
                raise ArchiveBuildError(f"case action {name!r} has no capture boundary")
            capture = {
                "action": name,
                "caseAction": action.get("action"),
                "attempt": action.get("attempt"),
                "label": spec["label"],
                "complete": boundary.get("completeCapture") is True,
                "termination": boundary.get("termination"),
                "rawCapture": pin(root, raw_path),
                "rawCaptureReportedOk": raw.get("ok"),
                "rawCaptureError": raw.get("error"),
                "captureBoundary": boundary,
                "frameCount": len(frames),
                "width": raw.get("width"),
                "height": raw.get("height"),
                "timingMode": raw.get("timingMode"),
                "causalMotion": action.get("motionEvidence"),
                "captureScope": spec["scope"],
            }
            captures.append(capture)
            if render_videos:
                video = render_video(frame_dir, len(frames), stage / f"{name}.mp4", int(spec.get("playbackFps", 12)))
                video["action"] = name
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
                raise ArchiveBuildError(f"visualReview.frames[{index}] is not one of the pinned capture or asset PNGs")
            action = row.get("action")
            frame_index = row.get("index")
            if not isinstance(action, str) or not action or not isinstance(frame_index, int) or frame_index < 0:
                raise ArchiveBuildError(f"visualReview.frames[{index}] must name an action and nonnegative index")
            destination_name = f"{action}-{frame_index:04d}.png"
            if destination_name in selected_names:
                raise ArchiveBuildError(f"visualReview.frames selects duplicate archive image {destination_name}")
            selected_names.add(destination_name)
            destination = stage / "selected" / destination_name
            destination.parent.mkdir(parents=True, exist_ok=True)
            shutil.copy2(source, destination)
            if sha256_file(destination) != digest:
                raise ArchiveBuildError(f"selected copy hash mismatch for {source}")
            selected.append({
                **row,
                "source": source_name,
                "archive": str(destination.relative_to(stage)),
                "sha256": digest,
            })

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
            **static_validation,
            "schema": "ftkmf.model-validation-archive.v1",
            "session": session,
            "enemy": enemy,
            "catalogSha256": case.get("profileSha256"),
            "assetHashes": asset_hashes,
            "captures": captures,
            "rootVisualReview": "visual-review.json",
            "rootVisualReviewPin": {
                "path": "visual-review.json",
                "sha256": sha256_file(stage / "visual-review.json"),
                "bytes": (stage / "visual-review.json").stat().st_size,
            },
            "visualReview": review.get("reviewStatus", static_validation.get("status")),
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
            validation["profileDocumentSha256"] = sha256_file(next(
                source for source, roles in sources.items() if "runtimeProfile" in roles
            ))
        write_json(stage / "validation.json", validation)
        display_name = static_validation["displayName"]
        (stage / "README.md").write_text(
            f"# {display_name} live validation\n\n"
            f"This immutable archive preserves the reviewed isolated session `{session}` for "
            f"`{enemy}`. `validation.json` pins source metadata, original assets, every "
            f"capture PNG, selected root-reviewed originals, and presentation derivatives. "
            "`visual-review.json` records the observed scope and limits.\n\n"
            "Recheck archive integrity with:\n\n"
            "```sh\n"
            "python3 tools/ai-model-pipeline/verify_model_validation_archive.py "
            f"{workspace_relative(root, output)} --check-video-metadata\n"
            "```\n\n"
            "An integrity pass verifies frozen artifacts only. It does not accept binding, "
            "motion, gameplay, or art quality beyond the specific reviewed records.\n"
        )
        stage.rename(output)
    except Exception:
        # Preserve a failed stage for diagnosis rather than deleting evidence.
        raise
    return {
        "status": "PASS",
        "archive": workspace_relative(root, output),
        "validation": workspace_relative(root, output / "validation.json"),
        "session": session,
        "enemy": enemy,
        "captures": len(actions),
        "staging": "renamed_to_final",
    }


def parse_args() -> argparse.Namespace:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("plan", type=Path, help="JSON plan with schema ftkmf.model-validation-archive-plan.v1")
    parser.add_argument("--skip-videos", action="store_true", help="Do not create presentation MP4 derivatives.")
    return parser.parse_args()


def main() -> int:
    args = parse_args()
    if not args.skip_videos and (shutil.which("ffmpeg") is None or shutil.which("ffprobe") is None):
        print(json.dumps({"status": "FAIL", "errors": ["ffmpeg and ffprobe are required unless --skip-videos is used"]}, indent=2), file=sys.stderr)
        return 1
    try:
        report = build_archive(args.plan, render_videos=not args.skip_videos)
    except (ArchiveBuildError, subprocess.CalledProcessError) as error:
        print(json.dumps({"status": "FAIL", "plan": str(args.plan), "errors": [str(error)]}, indent=2), file=sys.stderr)
        return 1
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
