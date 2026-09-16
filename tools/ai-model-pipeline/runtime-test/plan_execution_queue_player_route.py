#!/usr/bin/env python3
"""Print one current, exact native-preview plan from the execution ledgers.

The player workflow requires a game-owned Party Select preview and separately
observed combat/equipment behavior, so this tool deliberately does not launch
FTK or drive UI. It verifies one stage-ready player-skinset revision against the
current isolated player catalog and assets, then emits the exact input plan for
the documented native-preview sequence.
"""
from __future__ import annotations

import argparse
import json
from pathlib import Path

from run_execution_queue_route import (
    isolated_game,
    read_object,
    relative,
    repository_file,
    selected_choice,
    selected_route,
    choose_profile,
    validate_stage_ledger,
)


PLAN_LIMITS = [
    "This is a read-only current-input plan; it does not launch FTK or select a class.",
    "Reach the game-owned Party Select screen, observe the exact native preview, then capture and archive it through the player workflow.",
    "Combat, apparel, lifecycle, visual review, and canonical-route coverage remain separate fresh observations.",
]


def complete_plan(plan: dict, route: dict) -> dict:
    result = dict(plan)
    result["topologyGroup"] = route["topologyGroup"]
    result["coverageRouteKind"] = route["routeKind"]
    result["limits"] = list(PLAN_LIMITS)
    return result


def plan_output(root: Path, value: Path) -> Path:
    candidate = value if value.is_absolute() else root / value
    if candidate.is_symlink():
        raise ValueError("player plan output must not be a symlink")
    output = candidate.resolve(strict=False)
    if output.parent != root / "scratch" or output.suffix.lower() != ".json":
        raise ValueError("player plan output must be a .json file directly under repository scratch/")
    return output


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--queue", type=Path, default=Path("docs/model-validation-execution-queue.json"))
    parser.add_argument("--stage-readiness", type=Path, required=True)
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--topology-group", required=True)
    parser.add_argument("--profile-document", help="Required only to disambiguate multiple stage-ready historical revisions.")
    parser.add_argument("--output", type=Path, help="Optional new immutable player-plan JSON under scratch/.")
    args = parser.parse_args()
    root = args.root.resolve()
    try:
        queue_path = repository_file(root, args.queue, "queue")
        stage_path = repository_file(root, args.stage_readiness, "stage-readiness")
        game = isolated_game(root, args.game_root)
    except (ValueError, FileNotFoundError) as error:
        parser.error(str(error))
    queue = read_object(queue_path)
    stage = read_object(stage_path)
    try:
        route = selected_route(queue, args.topology_group, "playerSkinset")
        choice = selected_choice(stage, args.topology_group, "playerSkinset")
        candidate = choose_profile(choice, args.profile_document)
        plan = complete_plan(
            validate_stage_ledger(root, queue_path, queue, stage_path, stage, game, route, candidate), route
        )
    except (ValueError, FileNotFoundError) as error:
        parser.error(str(error))
    content = json.dumps(plan, indent=2) + "\n"
    if args.output is None:
        print(content, end="")
        return 0
    try:
        output = plan_output(root, args.output)
    except ValueError as error:
        parser.error(str(error))
    if output.exists():
        parser.error(f"refusing to overwrite player plan: {output}")
    output.parent.mkdir(parents=True, exist_ok=True)
    output.write_text(content)
    print(json.dumps({"status": "PLAYER_PLAN_CREATED", "plan": relative(root, output)}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
