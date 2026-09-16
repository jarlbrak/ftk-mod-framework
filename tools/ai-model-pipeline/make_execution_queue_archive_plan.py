#!/usr/bin/env python3
"""Create a reviewed archive plan from one pinned execution-queue run record.

This tool only writes a new plan. It verifies the fresh runner record, case,
profile, current source assets, and supplied root visual review before handing
the result to ``archive_model_validation_case.py``. It never launches FTK,
selects review frames, renders video, builds an archive, or awards coverage.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
from typing import Any

import archive_model_validation_case as archiver


Json = dict[str, Any]


class PlanDraftError(ValueError):
    """The supplied fresh-run inputs cannot become an archive plan."""


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
    raise PlanDraftError(f"could not locate FTK repository above {start}")


def root_file(root: Path, value: str | Path, label: str, *, required: bool = True) -> Path:
    raw = Path(value)
    path = raw if raw.is_absolute() else root / raw
    path = path.resolve(strict=False)
    if not path.is_relative_to(root):
        raise PlanDraftError(f"{label} must stay inside the repository: {value}")
    if required and (not path.is_file() or path.is_symlink()):
        raise PlanDraftError(f"{label} must be a real file: {path}")
    return path


def read_object(path: Path, label: str) -> Json:
    try:
        value = json.loads(path.read_text())
    except (OSError, UnicodeDecodeError, json.JSONDecodeError) as error:
        raise PlanDraftError(f"{label} is not a JSON object: {path}") from error
    if not isinstance(value, dict):
        raise PlanDraftError(f"{label} is not a JSON object: {path}")
    return value


def relative(root: Path, path: Path) -> str:
    return str(path.resolve().relative_to(root))


def required_binding(review: Json, motion_renderer: str) -> Json:
    binding = as_dict(review.get("binding"))
    renderer = binding.get("celRelativeRendererPath", binding.get("rendererPath"))
    mesh = binding.get("mesh")
    bones = binding.get("boneSignature")
    if (not isinstance(renderer, str) or renderer != motion_renderer
            or not isinstance(mesh, str) or not mesh
            or not isinstance(bones, str) or not bones):
        raise PlanDraftError("visual review must pin the selected motion renderer, mesh, and bone signature")
    return {
        "rendererPath": renderer,
        "mesh": mesh,
        "boneSignature": bones,
    }


def capture_label(key: str, action: Json) -> tuple[str, str]:
    case_action = action.get("action")
    if case_action == "pass":
        return "idle-and-native-attack", "Settled idle and exact native enemy attack capture."
    if case_action == "attack":
        return key, "Ordinary no-focus player attack capture; preserve its measured outcome separately."
    if case_action == "kill-fixture":
        return "explicit-fixture-death", "Explicit KillSingle fixture capture only; it is not ordinary lethal damage."
    return key, "Exact accepted case action capture; its scope is limited to the recorded native event."


def build_plan(
    record_path: Path,
    review_path: Path,
    plan_path: Path,
    archive_output: str,
    revision: str,
    limits: list[str],
    *,
    workspace_root: Path | None = None,
) -> Json:
    root = workspace_root.resolve() if workspace_root is not None else find_root(record_path.parent)
    record_path = root_file(root, record_path, "runner record")
    review_path = root_file(root, review_path, "visual review")
    plan_path = root_file(root, plan_path, "plan output", required=False)
    if plan_path.exists():
        raise PlanDraftError(f"refusing to overwrite archive plan: {plan_path}")
    if plan_path.suffix.lower() != ".json":
        raise PlanDraftError("plan output must use a .json filename")
    output = root_file(root, archive_output, "archive output", required=False)
    if output.exists() or output.suffix:
        raise PlanDraftError("archive output must be a new directory path")
    if not revision or not limits:
        raise PlanDraftError("revision and at least one explicit limit are required")

    record = read_object(record_path, "runner record")
    if record.get("status") != "needs_visual_review" or record.get("needsManualVisualReview") is not True:
        raise PlanDraftError("runner record is not a completed machine exercise awaiting visual review")
    if record.get("needsImmutableArchive") is not True:
        raise PlanDraftError("runner record does not require a new immutable archive")
    run_plan = as_dict(record.get("plan"))
    profile_ref = as_dict(run_plan.get("profile"))
    profile_key = profile_ref.get("key")
    document_ref = as_dict(profile_ref.get("document"))
    document_path = root_file(root, str(document_ref.get("path", "")), "profile document")
    if sha256(document_path) != document_ref.get("sha256"):
        raise PlanDraftError("profile document changed after the runner record")
    document = read_object(document_path, "profile document")
    profiles = [as_dict(value) for value in as_list(document.get("profiles"))]
    matches = [profile for profile in profiles if profile.get("key") == profile_key]
    if len(matches) != 1:
        raise PlanDraftError("runner profile key is not unique in the pinned profile document")
    profile = matches[0]
    display_name = profile.get("displayName")
    if not isinstance(profile_key, str) or not isinstance(display_name, str) or not display_name:
        raise PlanDraftError("pinned profile lacks a key or display name")

    case_path = root_file(root, str(record.get("caseResult", "")), "case result")
    case = read_object(case_path, "case result")
    if case.get("enemy") != profile_key or case.get("session") != record.get("session"):
        raise PlanDraftError("case result identity differs from the runner record")
    catalog = as_dict(run_plan.get("catalog"))
    if case.get("profileSha256") != catalog.get("sha256"):
        raise PlanDraftError("case profile catalog hash differs from the runner record")
    review = read_object(review_path, "visual review")
    review_status = review.get("reviewStatus")
    if (review.get("session") != case.get("session") or not as_list(review.get("frames"))
            or not isinstance(review_status, str) or not review_status.startswith("reviewed")):
        raise PlanDraftError("visual review needs the exact case session, selected frames, and a reviewed status")
    motion_renderer = run_plan.get("motionRendererPath")
    if not isinstance(motion_renderer, str) or not motion_renderer:
        raise PlanDraftError("runner record lacks its exact motion renderer")
    binding = required_binding(review, motion_renderer)

    source_assignments = [as_dict(value) for value in as_list(run_plan.get("sourceAssignments"))]
    if not source_assignments:
        raise PlanDraftError("runner record lacks exact source assignments")
    source_kind = source_assignments[0].get("sourceKind")
    native_enemy = source_assignments[0].get("nativeEnemy")
    resource_prefab = source_assignments[0].get("resourcePrefab")
    if (not isinstance(source_kind, str) or not isinstance(native_enemy, str)
            or any(item.get("sourceKind") != source_kind or item.get("nativeEnemy") != native_enemy
                   or item.get("resourcePrefab") != resource_prefab for item in source_assignments)):
        raise PlanDraftError("runner record source assignments are not one exact archive source identity")
    renderer_paths = [item.get("rendererPath") for item in source_assignments]
    renderer_ids = [item.get("sourceRendererId") for item in source_assignments]
    if (any(not isinstance(value, str) or not value for value in renderer_paths)
            or any(not isinstance(value, int) for value in renderer_ids)):
        raise PlanDraftError("runner record source assignments lack renderer identities")

    asset_files: list[str] = []
    for item in as_list(run_plan.get("assets")):
        asset = as_dict(item)
        name = asset.get("name")
        expected = asset.get("sha256")
        if not isinstance(name, str) or Path(name).name != name or not isinstance(expected, str):
            raise PlanDraftError("runner record has an invalid declared asset")
        source = document_path.parent / name
        if not source.is_file() or source.is_symlink() or sha256(source) != expected:
            raise PlanDraftError(f"declared source asset changed after the runner record: {name}")
        asset_files.append(relative(root, source))
    if not asset_files:
        raise PlanDraftError("runner record has no declared original assets")

    actions = archiver.action_map(case)
    identity = {
        "celRelativeRendererPath": binding["rendererPath"],
        "mesh": binding["mesh"],
        "boneSignature": binding["boneSignature"],
    }
    captures: Json = {}
    for key, action in actions.items():
        label, scope = capture_label(key, action)
        captures[key] = {"label": label, "scope": scope, "frameIdentity": identity}

    metadata: list[Json] = [
        {"path": relative(root, document_path), "pinAs": "runtimeProfile"},
        {"path": relative(root, record_path), "pinAs": "runnerRecord"},
    ]
    for name, pin_as in (("queue", "executionQueue"), ("stageReadiness", "stageReadiness"), ("stageResult", "stageBindingResult")):
        row = as_dict(run_plan.get(name)) if name != "stageResult" else {"path": record.get(name)}
        source_name = row.get("path")
        if isinstance(source_name, str) and source_name:
            source = root_file(root, source_name, name)
            metadata.append({"path": relative(root, source), "pinAs": pin_as})
            if name == "stageResult":
                journal = source.parent / "journal.jsonl"
                if journal.is_file() and not journal.is_symlink():
                    metadata.append({"path": relative(root, journal), "pinAs": "stageBindingJournal"})

    validation: Json = {
        "status": "reviewed_exact_source_pending_any_remaining_limits",
        "revision": revision,
        "displayName": display_name,
        "sourceKind": source_kind,
        "nativeEnemy": native_enemy,
        "nativeChassis": native_enemy,
        "rendererPaths": renderer_paths,
        "sourceRendererIds": renderer_ids,
        "binding": binding,
        "ordinaryLethal": False,
        "limits": limits,
    }
    if resource_prefab is not None:
        validation["resourcePrefab"] = resource_prefab
    draft = {
        "schema": "ftkmf.model-validation-archive-plan.v1",
        "output": relative(root, output),
        "caseResult": relative(root, case_path),
        "visualReview": relative(root, review_path),
        "assetFiles": asset_files,
        "metadata": metadata,
        "captures": captures,
        "validation": validation,
    }
    plan_path.parent.mkdir(parents=True, exist_ok=True)
    plan_path.write_text(json.dumps(draft, indent=2) + "\n")
    return {
        "status": "PLAN_CREATED",
        "plan": relative(root, plan_path),
        "archiveOutput": draft["output"],
        "caseResult": draft["caseResult"],
        "captures": sorted(captures),
    }


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--record", type=Path, required=True)
    parser.add_argument("--visual-review", type=Path, required=True)
    parser.add_argument("--plan-output", type=Path, required=True)
    parser.add_argument("--archive-output", required=True)
    parser.add_argument("--revision", required=True)
    parser.add_argument("--limit", action="append", required=True)
    args = parser.parse_args()
    try:
        report = build_plan(
            args.record, args.visual_review, args.plan_output, args.archive_output,
            args.revision, args.limit,
        )
    except PlanDraftError as error:
        parser.error(str(error))
    print(json.dumps(report, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
