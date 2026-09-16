#!/usr/bin/env python3
"""Create a non-overwriting root-review template from one completed case result.

The template pins the runner-selected source PNGs and observed renderer identity
without making a visual conclusion. A reviewer must inspect the images, replace
the pending observations, and set a reviewed status before it can seed an
immutable archive plan.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any


Json = dict[str, Any]


class ReviewTemplateError(ValueError):
    """A case result cannot produce a trustworthy review template."""


def as_dict(value: Any) -> Json:
    return value if isinstance(value, dict) else {}


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def find_root(start: Path) -> Path:
    for candidate in (start.resolve(), *start.resolve().parents):
        if (candidate / "FTKModFramework").is_dir() and (candidate / "tools").is_dir():
            return candidate
    raise ReviewTemplateError(f"could not locate FTK repository above {start}")


def workspace_file(root: Path, value: str | Path, label: str, *, required: bool = True) -> Path:
    candidate = Path(value)
    candidate = candidate if candidate.is_absolute() else root / candidate
    if candidate.is_symlink():
        raise ReviewTemplateError(f"{label} must not be a symlink")
    path = candidate.resolve(strict=False)
    if not path.is_relative_to(root):
        raise ReviewTemplateError(f"{label} must stay inside the repository: {value}")
    if required and not path.is_file():
        raise ReviewTemplateError(f"{label} is missing: {path}")
    return path


def read_object(path: Path, label: str) -> Json:
    try:
        value = json.loads(path.read_text())
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise ReviewTemplateError(f"{label} is not a JSON object: {path}") from error
    if not isinstance(value, dict):
        raise ReviewTemplateError(f"{label} is not a JSON object: {path}")
    return value


def relative(root: Path, path: Path) -> str:
    return str(path.resolve().relative_to(root))


def archive_action_keys(case: Json) -> dict[tuple[str, int | None], str]:
    rows = [as_dict(item) for item in as_list(case.get("actions"))]
    counts: dict[str, int] = {}
    for row in rows:
        name = row.get("action")
        if isinstance(name, str):
            counts[name] = counts.get(name, 0) + 1
    result: dict[tuple[str, int | None], str] = {}
    for row in rows:
        name = row.get("action")
        attempt = row.get("attempt")
        if not isinstance(name, str) or not name:
            continue
        if counts[name] == 1:
            result[(name, attempt if isinstance(attempt, int) else None)] = name
        elif isinstance(attempt, int) and attempt > 0:
            result[(name, attempt)] = f"{name}-attempt-{attempt}"
    return result


def build_template(case_path: Path, output_path: Path, *, workspace_root: Path | None = None) -> Json:
    root = workspace_root.resolve() if workspace_root is not None else find_root(case_path.parent)
    case_path = workspace_file(root, case_path, "case result")
    output_path = workspace_file(root, output_path, "review output", required=False)
    if output_path.exists():
        raise ReviewTemplateError(f"refusing to overwrite review template: {output_path}")
    if output_path.suffix.lower() != ".json":
        raise ReviewTemplateError("review output must use a .json filename")
    case = read_object(case_path, "case result")
    if case.get("status") != "needs_visual_review":
        raise ReviewTemplateError("case result is not awaiting manual visual review")
    session = case.get("session")
    renderer = as_dict(case.get("initialRenderer"))
    renderer_path = renderer.get("celRelativeRendererPath", renderer.get("rendererPath"))
    mesh = renderer.get("mesh")
    bones = renderer.get("boneSignature")
    if (not isinstance(session, str) or not session or not isinstance(renderer_path, str) or not renderer_path
            or not isinstance(mesh, str) or not mesh or not isinstance(bones, str) or not bones):
        raise ReviewTemplateError("case result lacks a complete observed renderer identity")
    keys = archive_action_keys(case)
    frames: list[Json] = []
    seen: set[tuple[str, int]] = set()
    for index, value in enumerate(as_list(case.get("selectedFrames"))):
        row = as_dict(value)
        source = workspace_file(root, str(row.get("path", "")), f"selected frame {index}")
        expected = row.get("sha256")
        action = row.get("action")
        frame_index = row.get("index")
        attempt = row.get("attempt") if isinstance(row.get("attempt"), int) else None
        if (not isinstance(expected, str) or len(expected) != 64 or sha256(source) != expected
                or not isinstance(action, str) or not isinstance(frame_index, int) or frame_index < 0):
            raise ReviewTemplateError(f"selected frame {index} has incomplete or changed identity")
        archive_action = keys.get((action, attempt))
        if archive_action is None:
            raise ReviewTemplateError(f"selected frame {index} does not map to an accepted case action")
        identity = (archive_action, frame_index)
        if identity in seen:
            raise ReviewTemplateError(f"selected frame {index} duplicates {archive_action} frame {frame_index}")
        seen.add(identity)
        frame: Json = {
            "path": relative(root, source),
            "sha256": expected,
            "action": archive_action,
            "index": frame_index,
            "observation": "PENDING: inspect this exact source frame and record the observed result and limits.",
        }
        if attempt is not None:
            frame["attempt"] = attempt
        frames.append(frame)
    if not frames:
        raise ReviewTemplateError("case result has no selected frames for manual review")
    review = {
        "schema": "ftkmf.model-root-visual-review-template.v1",
        "session": session,
        "reviewStatus": "pending_manual_root_review",
        "binding": {"rendererPath": renderer_path, "mesh": mesh, "boneSignature": bones},
        "frames": frames,
        "requirements": [
            "Inspect each referenced original PNG before changing reviewStatus to a reviewed value.",
            "Replace every pending observation with the exact observed result and state its limits.",
            "Add selected source PNGs only when their hash, action, and frame index are verified from the fresh case.",
        ],
    }
    output_path.parent.mkdir(parents=True, exist_ok=True)
    output_path.write_text(json.dumps(review, indent=2) + "\n")
    return {"status": "TEMPLATE_CREATED", "review": relative(root, output_path), "frames": len(frames)}


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("case_result", type=Path)
    parser.add_argument("--output", type=Path, required=True)
    args = parser.parse_args()
    try:
        report = build_template(args.case_result, args.output)
    except ReviewTemplateError as error:
        parser.error(str(error))
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
