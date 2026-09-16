#!/usr/bin/env python3
"""Build exact commands for every unfinished FTK model-validation route.

The campaign planner consumes the generated execution queue and one current
stage-readiness report. It validates every selected profile, catalog row,
declared asset, source assignment, and motion renderer through the same code as
the one-route runner. It never launches FTK, changes a catalog, performs a
player UI action, reviews an image, builds an archive, or awards coverage.
"""
from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import shlex
import sys
from typing import Any


TOOL_DIR = Path(__file__).resolve().parent
sys.path.insert(0, str(TOOL_DIR / "runtime-test"))
import run_execution_queue_route as route_runner
import plan_execution_queue_player_route as player_planner


Json = dict[str, Any]


def command_text(arguments: list[str]) -> str:
    return " ".join(shlex.quote(value) for value in arguments)


def route_output(root: Path, topology_group: str, route_kind: str) -> Path:
    name = f"model-route-{topology_group}-{route_kind.lower()}-run.json"
    return root / "scratch" / name


def route_review_output(root: Path, topology_group: str, route_kind: str) -> Path:
    name = f"model-route-{topology_group}-{route_kind.lower()}-root-review.json"
    return root / "scratch" / name


def player_plan_output(root: Path, topology_group: str, plan: Json) -> Path:
    encoded = json.dumps(plan, sort_keys=True, separators=(",", ":"), allow_nan=False).encode()
    plan_id = hashlib.sha256(encoded).hexdigest()[:12]
    return root / "scratch" / f"model-route-{topology_group}-playerskinset-plan-{plan_id}.json"


def existing_player_plan_state(root: Path, output: Path, expected_plan: Json) -> Json:
    reference = route_runner.input_reference(root, output)
    try:
        recorded = route_runner.read_object(output)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
        return {
            "status": "native_player_plan_requires_inspection",
            "playerPlan": reference,
            "inspectionError": f"{type(error).__name__}: {error}",
            "nextAction": "Inspect the existing player-plan artifact. Do not overwrite it or start dependent native UI work.",
        }
    if recorded != expected_plan:
        return {
            "status": "native_player_plan_requires_inspection",
            "playerPlan": reference,
            "inspectionError": "Recorded player plan differs from the current exact route plan.",
            "nextAction": "Inspect the stale or mismatched player plan and create a separately named plan only when justified.",
        }
    return {
        "status": "native_player_plan_current",
        "playerPlan": reference,
        "nextAction": (
            "Use this exact current plan for native preview, overworld, combat, equipment, lifetime, motion, visual review, and player archive steps."
        ),
    }


def existing_trial_state(
    root: Path,
    output: Path,
    expected_plan: Json,
    topology_group: str,
    route_kind: str,
) -> Json:
    """Classify one immutable runner record without treating it as acceptance."""
    reference = route_runner.input_reference(root, output)
    try:
        record = route_runner.read_object(output)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
        return {
            "status": "enemy_trial_record_invalid",
            "runnerRecord": reference,
            "inspectionError": f"{type(error).__name__}: {error}",
            "nextAction": "Inspect the existing immutable runner record. Do not overwrite or automatically rerun it.",
        }
    if record.get("status") == "runner_error":
        return {
            "status": "enemy_trial_recorded_error_requires_inspection",
            "runnerRecord": reference,
            "runnerError": record.get("error"),
            "nextAction": (
                "Inspect the recorded runner error and retained outputs before deciding whether a new, explicitly named trial is justified."
            ),
        }
    actual_plan = route_runner.as_dict(record.get("plan"))
    identity_fields = (
        "profile", "catalog", "assets", "sourceAssignments", "motionRendererPath", "queue", "stageReadiness",
    )
    mismatches = [field for field in identity_fields if actual_plan.get(field) != expected_plan.get(field)]
    if actual_plan.get("topologyGroup") != topology_group:
        mismatches.append("topologyGroup")
    if actual_plan.get("coverageRouteKind") != route_kind:
        mismatches.append("coverageRouteKind")
    if mismatches:
        return {
            "status": "enemy_trial_record_identity_mismatch",
            "runnerRecord": reference,
            "mismatchedFields": sorted(set(mismatches)),
            "nextAction": (
                "Inspect the existing immutable runner record against the current campaign. Do not overwrite or automatically rerun it."
            ),
        }
    if (record.get("status") != "needs_visual_review"
            or record.get("needsManualVisualReview") is not True
            or record.get("needsImmutableArchive") is not True):
        return {
            "status": "enemy_trial_record_requires_inspection",
            "runnerRecord": reference,
            "recordedStatus": record.get("status"),
            "nextAction": (
                "Inspect the completed or partial runner record and retained outputs before choosing any further action."
            ),
        }
    case_text = record.get("caseResult")
    try:
        if not isinstance(case_text, str) or not case_text:
            raise ValueError("runner record lacks a case-result path")
        case_path = route_runner.repository_file(root, Path(case_text), "case result")
        case = route_runner.read_object(case_path)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, ValueError, FileNotFoundError) as error:
        return {
            "status": "enemy_trial_record_invalid",
            "runnerRecord": reference,
            "inspectionError": f"{type(error).__name__}: {error}",
            "nextAction": "Inspect the existing record and missing or invalid case result. Do not automatically rerun it.",
        }
    profile_key = route_runner.as_dict(expected_plan.get("profile")).get("key")
    catalog_hash = route_runner.as_dict(expected_plan.get("catalog")).get("sha256")
    case_identity_ok = (
        case.get("status") == "needs_visual_review"
        and case.get("enemy") == profile_key
        and case.get("session") == record.get("session")
        and case.get("profileSha256") == catalog_hash
        and case.get("initialRenderer") == record.get("initialRenderer")
    )
    if not case_identity_ok:
        return {
            "status": "enemy_trial_record_identity_mismatch",
            "runnerRecord": reference,
            "caseResult": route_runner.input_reference(root, case_path),
            "mismatchedFields": ["caseResultIdentity"],
            "nextAction": "Inspect the runner and case identity mismatch. Do not overwrite or automatically rerun either record.",
        }
    review_path = route_review_output(root, topology_group, route_kind)
    case_reference = route_runner.input_reference(root, case_path)
    if not review_path.exists():
        arguments = [
            "python3",
            "tools/ai-model-pipeline/prepare_model_visual_review.py",
            route_runner.relative(root, case_path),
            "--output",
            route_runner.relative(root, review_path),
        ]
        return {
            "status": "enemy_trial_awaiting_visual_review",
            "runnerRecord": reference,
            "caseResult": case_reference,
            "reviewTemplateCommand": arguments,
            "reviewTemplateCommandText": command_text(arguments),
            "nextAction": (
                "Create the pinned review template, inspect every selected original PNG, replace pending observations, and set a reviewed status."
            ),
        }
    review_reference = route_runner.input_reference(root, review_path)
    try:
        review = route_runner.read_object(review_path)
    except (OSError, UnicodeDecodeError, json.JSONDecodeError, ValueError) as error:
        return {
            "status": "enemy_trial_review_invalid",
            "runnerRecord": reference,
            "caseResult": case_reference,
            "visualReview": review_reference,
            "inspectionError": f"{type(error).__name__}: {error}",
            "nextAction": "Inspect the existing visual-review file. Do not overwrite it or advance to archive planning.",
        }
    binding = route_runner.as_dict(review.get("binding"))
    renderer_path = binding.get("celRelativeRendererPath", binding.get("rendererPath"))
    review_identity_ok = (
        review.get("session") == case.get("session")
        and renderer_path == expected_plan.get("motionRendererPath")
        and isinstance(binding.get("mesh"), str) and bool(binding.get("mesh"))
        and isinstance(binding.get("boneSignature"), str) and bool(binding.get("boneSignature"))
        and bool(route_runner.as_list(review.get("frames")))
    )
    if not review_identity_ok:
        return {
            "status": "enemy_trial_review_identity_mismatch",
            "runnerRecord": reference,
            "caseResult": case_reference,
            "visualReview": review_reference,
            "nextAction": "Inspect the visual-review identity mismatch. Do not advance it to an archive plan.",
        }
    review_status = review.get("reviewStatus")
    if review_status == "pending_manual_root_review":
        return {
            "status": "enemy_trial_manual_review_pending",
            "runnerRecord": reference,
            "caseResult": case_reference,
            "visualReview": review_reference,
            "nextAction": "Inspect every pinned original PNG, replace each pending observation, and set an honest reviewed status.",
        }
    if isinstance(review_status, str) and review_status.startswith("reviewed"):
        return {
            "status": "enemy_trial_reviewed_archive_plan_required",
            "runnerRecord": reference,
            "caseResult": case_reference,
            "visualReview": review_reference,
            "nextAction": (
                "Choose a new package revision and explicit remaining limits, then create, build, and independently verify an immutable archive."
            ),
        }
    return {
        "status": "enemy_trial_review_requires_inspection",
        "runnerRecord": reference,
        "caseResult": case_reference,
        "visualReview": review_reference,
        "recordedReviewStatus": review_status,
        "nextAction": "Inspect the recorded visual-review status before archive planning or any new trial.",
    }


def advance_enemy_entry(entry: Json, trial_state: Json) -> Json:
    """Remove executable game actions once an immutable trial record exists."""
    result = dict(entry)
    result.pop("liveCommand", None)
    result.pop("liveCommandText", None)
    result.update(trial_state)
    return result


def build_campaign(
    root: Path,
    queue_path: Path,
    stage_path: Path,
    game: Path,
) -> Json:
    queue = route_runner.read_object(queue_path)
    stage = route_runner.read_object(stage_path)
    entries: list[Json] = []
    for raw_route in route_runner.as_list(queue.get("routes")):
        route = route_runner.as_dict(raw_route)
        group = route.get("topologyGroup")
        kind = route.get("routeKind")
        if not isinstance(group, str) or not isinstance(kind, str):
            raise ValueError("execution queue route lacks exact identity")
        choice = route_runner.selected_choice(stage, group, kind)
        action = choice.get("nextStagingAction")
        if action == "stage_ready_revision_available":
            candidate = route_runner.choose_profile(choice, None)
            if kind == "playerSkinset":
                plan = route_runner.validate_stage_ledger(
                    root, queue_path, queue, stage_path, stage, game, route, candidate
                )
                complete_player_plan = player_planner.complete_plan(plan, route)
                output = player_plan_output(root, group, complete_player_plan)
                arguments = [
                    "python3",
                    "tools/ai-model-pipeline/runtime-test/plan_execution_queue_player_route.py",
                    "--stage-readiness",
                    route_runner.relative(root, stage_path),
                    "--game-root",
                    route_runner.relative(root, game),
                    "--topology-group",
                    group,
                    "--output",
                    route_runner.relative(root, output),
                ]
                entry = {
                    "topologyGroup": group,
                    "routeKind": kind,
                    "status": "native_player_workflow_required",
                    "profile": plan["profile"],
                    "playerProfileEvidence": plan["playerProfileEvidence"],
                    "planCommand": arguments,
                    "planCommandText": command_text(arguments),
                    "proposedPlayerPlan": route_runner.relative(root, output),
                    "proposedPlayerPlanExists": output.exists(),
                    "nextAction": (
                        "Run the pinned player plan, then complete native preview, overworld, combat, "
                        "equipment, lifetime, motion, visual review, and player archive steps."
                    ),
                }
                if output.exists():
                    entry.pop("planCommand")
                    entry.pop("planCommandText")
                    entry.update(existing_player_plan_state(root, output, complete_player_plan))
                entries.append(entry)
                continue
            motion = choice.get("selectedMotionRendererPath")
            plan = route_runner.validate_stage_ledger(
                root, queue_path, queue, stage_path, stage, game, route, candidate, motion
            )
            if choice.get("selectedWorkflow") == "passive_enemy_arrival":
                assignments = route_runner.as_list(plan.get("sourceAssignments"))
                if len(assignments) != 1:
                    raise ValueError("passive arrival workflow requires one exact source assignment")
                target = route_runner.as_dict(assignments[0])
                arguments = [
                    "python3",
                    "tools/ai-model-pipeline/runtime-test/arrival_case.py",
                    "--root",
                    route_runner.relative(root, game),
                    "--port",
                    "8788",
                    "--session",
                    "EXACT_CURRENT_32_HEX_SESSION",
                    "--enemy",
                    plan["profile"]["key"],
                    "--renderer-path",
                    target["rendererPath"],
                    "--profile-sha256",
                    plan["catalog"]["sha256"],
                    "--level",
                    str(choice["arrivalLevel"]),
                    "--room",
                    str(choice["arrivalRoom"]),
                    "--capture-timeout",
                    "360",
                ]
                entries.append({
                    "topologyGroup": group,
                    "routeKind": kind,
                    "status": "passive_arrival_workflow_required",
                    "profile": plan["profile"],
                    "catalog": plan["catalog"],
                    "assets": plan["assets"],
                    "sourceAssignments": plan["sourceAssignments"],
                    "motionRendererPath": plan["motionRendererPath"],
                    "arrivalLevel": choice["arrivalLevel"],
                    "arrivalRoom": choice["arrivalRoom"],
                    "commandTemplate": arguments,
                    "commandTemplateText": command_text(arguments),
                    "nextAction": (
                        "Launch a fresh isolated setup, reach the exact eligible native Ready Enemy slot, "
                        "substitute its current session once, and issue the passive arrival command once."
                    ),
                })
                continue
            output = route_output(root, group, kind)
            base_arguments = [
                "python3",
                "tools/ai-model-pipeline/runtime-test/run_execution_queue_route.py",
                "--stage-readiness",
                route_runner.relative(root, stage_path),
                "--game-root",
                route_runner.relative(root, game),
                "--topology-group",
                group,
                "--route-kind",
                kind,
                "--profile-document",
                route_runner.as_dict(plan["profile"].get("document"))["path"],
                "--motion-renderer-path",
                plan["motionRendererPath"],
            ]
            live_arguments = [
                *base_arguments,
                "--attack-attempts",
                "8",
                "--run",
                "--output",
                route_runner.relative(root, output),
            ]
            entry = {
                "topologyGroup": group,
                "routeKind": kind,
                "status": "isolated_enemy_trial_ready",
                "profile": plan["profile"],
                "catalog": plan["catalog"],
                "assets": plan["assets"],
                "sourceAssignments": plan["sourceAssignments"],
                "motionRendererPath": plan["motionRendererPath"],
                "dryCommand": base_arguments,
                "dryCommandText": command_text(base_arguments),
                "liveCommand": live_arguments,
                "liveCommandText": command_text(live_arguments),
                "proposedOutput": route_runner.relative(root, output),
                "proposedOutputExists": output.exists(),
                "nextAction": (
                    "Run only when no FTK process exists, inspect the retained PNGs manually, "
                    "then generate and verify a fresh immutable archive."
                ),
            }
            if output.exists():
                entry = advance_enemy_entry(entry, existing_trial_state(root, output, plan, group, kind))
            entries.append(entry)
            continue
        status = route_runner.as_dict(route.get("executionStatus"))
        entries.append({
            "topologyGroup": group,
            "routeKind": kind,
            "status": action,
            "adapterContract": route_runner.as_dict(status.get("adapterContract")) or None,
            "adapterCampaign": route_runner.as_dict(status.get("adapterCampaign")) or None,
            "adapterCampaignIntegrity": route_runner.as_dict(
                status.get("adapterCampaignIntegrity")
            ) or None,
            "nextAction": route.get("nextAction"),
        })
    status_counts: dict[str, int] = {}
    for entry in entries:
        status = str(entry.get("status"))
        status_counts[status] = status_counts.get(status, 0) + 1
    return {
        "schemaVersion": 2,
        "scope": (
            "Exact route-by-route campaign plan for the current unfinished model-validation queue. "
            "Commands are dry or bounded launch inputs only. They are not runtime, visual, gameplay, or archive evidence."
        ),
        "queue": route_runner.input_reference(root, queue_path),
        "stageReadiness": route_runner.input_reference(root, stage_path),
        "isolatedGameRoot": route_runner.relative(root, game),
        "summary": {
            "routes": len(entries),
            "isolatedEnemyTrialsReady": status_counts.get("isolated_enemy_trial_ready", 0),
            "passiveArrivalWorkflowsRequired": status_counts.get("passive_arrival_workflow_required", 0),
            "nativePlayerWorkflowsRequired": sum(entry.get("routeKind") == "playerSkinset" for entry in entries),
            "nativePlayerPlansCurrent": status_counts.get("native_player_plan_current", 0),
            "nativePlayerPlansRequiringInspection": status_counts.get("native_player_plan_requires_inspection", 0),
            "profileAuthoringRequired": status_counts.get("profile_authoring_or_queue_reconciliation_required", 0),
            "adapterDesignsRequired": status_counts.get("adapter_or_retarget_design_required", 0),
            "adapterImplementationsRequired": status_counts.get("adapter_implementation_required", 0),
            "adapterValidationsRequired": status_counts.get("adapter_validation_required", 0),
            "adapterVisualArchiveReviewsRequired": status_counts.get(
                "adapter_visual_archive_review_required", 0
            ),
            "unresolvedRoutePlans": sum(
                count for status, count in status_counts.items()
                if status not in {
                    "isolated_enemy_trial_ready",
                    "passive_arrival_workflow_required",
                    "native_player_workflow_required",
                    "native_player_plan_current",
                    "native_player_plan_requires_inspection",
                    "profile_authoring_or_queue_reconciliation_required",
                    "adapter_or_retarget_design_required",
                    "adapter_implementation_required",
                    "adapter_validation_required",
                    "adapter_visual_archive_review_required",
                    "enemy_trial_record_invalid",
                    "enemy_trial_recorded_error_requires_inspection",
                    "enemy_trial_record_identity_mismatch",
                    "enemy_trial_record_requires_inspection",
                    "enemy_trial_awaiting_visual_review",
                    "enemy_trial_review_invalid",
                    "enemy_trial_review_identity_mismatch",
                    "enemy_trial_manual_review_pending",
                    "enemy_trial_reviewed_archive_plan_required",
                    "enemy_trial_review_requires_inspection",
                }
            ),
            "proposedEnemyOutputsAlreadyExist": sum(
                bool(entry.get("proposedOutputExists")) for entry in entries
            ),
            "enemyTrialsAwaitingVisualReview": status_counts.get("enemy_trial_awaiting_visual_review", 0),
            "enemyTrialsWithManualReviewPending": status_counts.get("enemy_trial_manual_review_pending", 0),
            "enemyTrialsReadyForArchivePlanning": status_counts.get("enemy_trial_reviewed_archive_plan_required", 0),
            "enemyTrialRecordsRequiringInspection": sum(
                count for status, count in status_counts.items()
                if status.startswith("enemy_trial_")
                and status not in {
                    "enemy_trial_awaiting_visual_review",
                    "enemy_trial_manual_review_pending",
                    "enemy_trial_reviewed_archive_plan_required",
                }
            ),
        },
        "routes": entries,
        "limits": [
            "Execute one enemy route in a fresh isolated process and finish its manual review and immutable archive before assigning canonical credit.",
            "A player plan is read-only. It does not navigate the native UI or establish preview, equipment, combat, or lifetime behavior.",
            "Adapter routes use their dedicated exact runner. A satisfied production-observation campaign still requires manual visual, camera, culling, portrait, progression, and canonical archive review.",
            "An existing proposed output must be reviewed or given a new explicit filename. Never overwrite it.",
        ],
    }


def output_path(root: Path, value: Path) -> Path:
    candidate = value if value.is_absolute() else root / value
    if candidate.is_symlink():
        raise ValueError("campaign output must not be a symlink")
    result = candidate.resolve(strict=False)
    if result.parent != root / "scratch" or result.suffix.lower() != ".json":
        raise ValueError("campaign output must be a .json file directly under repository scratch/")
    return result


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--queue", type=Path, default=Path("docs/model-validation-execution-queue.json"))
    parser.add_argument("--stage-readiness", type=Path, required=True)
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--output", type=Path)
    parser.add_argument("--overwrite", action="store_true")
    args = parser.parse_args()
    root = route_runner.find_root(args.root)
    queue_path = route_runner.repository_file(root, args.queue, "execution queue")
    stage_path = route_runner.repository_file(root, args.stage_readiness, "stage-readiness report")
    game = route_runner.isolated_game(root, args.game_root)
    report = build_campaign(root, queue_path, stage_path, game)
    content = json.dumps(report, indent=2, allow_nan=False) + "\n"
    if args.output is None:
        print(content, end="")
        return 0
    destination = output_path(root, args.output)
    if destination.exists() and not args.overwrite:
        parser.error(f"refusing to overwrite existing output: {destination}; pass --overwrite")
    destination.write_text(content)
    print(json.dumps(report["summary"], indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
