#!/usr/bin/env python3
"""Run one exact stage-ready enemy route from the validation ledgers.

This runner is deliberately narrow: it chooses a single route that already has
one unambiguous stage-ready profile revision, verifies the current isolated
catalog and declared assets against the read-only stage-readiness ledger, then
owns one FTK process for one ``run_case`` plus ``exercise_case`` sequence. It
never deploys a stage, chooses among historical profile revisions, reads logs,
or treats the machine result as a visual review or immutable archive.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from pathlib import Path
import signal
import socket
import subprocess
import time
import urllib.request
from datetime import datetime, timezone
from typing import Any


Json = dict[str, Any]
MODELS_RELATIVE = Path("BepInEx/plugins/FTKModFramework_content/models")
PROFILE_ROUTES = {
    "direct_enemy": {"catalogKind": "enemy", "catalogFile": "model-test-profiles.json"},
    "resource_prefab_override": {"catalogKind": "enemy", "catalogFile": "model-test-profiles.json"},
    "player_skinset_avatar": {"catalogKind": "player", "catalogFile": "model-test-player-profiles.json"},
}
COVERAGE_TO_PROFILE_ROUTE = {
    "directEnemy": "direct_enemy",
    "resourcePrefab": "resource_prefab_override",
    "playerSkinset": "player_skinset_avatar",
}


def as_dict(value: Any) -> Json:
    return value if isinstance(value, dict) else {}


def as_list(value: Any) -> list[Any]:
    return value if isinstance(value, list) else []


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read_object(path: Path) -> Json:
    value = json.loads(path.read_text())
    if not isinstance(value, dict):
        raise ValueError(f"expected JSON object: {path}")
    return value


def find_root(start: Path) -> Path:
    for candidate in (start.resolve(), *start.resolve().parents):
        if (candidate / "FTKModFramework").is_dir() and (candidate / "tools").is_dir():
            return candidate
    raise ValueError(f"could not locate FTK repository above {start}")


def relative(root: Path, path: Path) -> str:
    try:
        return str(path.resolve().relative_to(root.resolve()))
    except ValueError:
        return str(path.resolve())


def input_reference(root: Path, path: Path) -> Json:
    return {"path": relative(root, path), "sha256": sha256(path)}


def repository_file(root: Path, value: Path, label: str) -> Path:
    root = root.resolve()
    candidate = value if value.is_absolute() else root / value
    if candidate.is_symlink():
        raise ValueError(f"{label} must not be a symlink")
    path = candidate.resolve(strict=False)
    if not path.is_relative_to(root):
        raise ValueError(f"{label} must be a real repository file")
    if not path.is_file():
        raise FileNotFoundError(f"{label} is missing: {path}")
    return path


def isolated_game(root: Path, value: Path) -> Path:
    root = root.resolve()
    candidate = value if value.is_absolute() else root / value
    if candidate.is_symlink():
        raise ValueError("game root must not be a symlink")
    game = candidate.resolve(strict=False)
    if not game.is_dir() or game.parent != root / "scratch":
        raise ValueError("game root must be a real direct child of repository scratch/")
    return game


def runner_record_path(root: Path, value: Path) -> Path:
    root = root.resolve()
    candidate = value if value.is_absolute() else root / value
    if candidate.is_symlink():
        raise ValueError("runner record output must not be a symlink")
    output = candidate.resolve(strict=False)
    if output.parent != root / "scratch" or output.suffix.lower() != ".json":
        raise ValueError("runner record output must be a new .json file directly under repository scratch/")
    return output


def route_key(group: str, route_kind: str) -> tuple[str, str]:
    return group, route_kind


def selected_route(queue: Json, topology_group: str, route_kind: str | None) -> Json:
    matches = [
        as_dict(raw) for raw in as_list(queue.get("routes"))
        if as_dict(raw).get("topologyGroup") == topology_group
        and (route_kind is None or as_dict(raw).get("routeKind") == route_kind)
    ]
    if len(matches) != 1:
        qualifier = route_kind if route_kind is not None else "any route kind"
        raise ValueError(f"expected one queue route for {topology_group!r} / {qualifier!r}, found {len(matches)}")
    return matches[0]


def selected_choice(stage: Json, topology_group: str, route_kind: str) -> Json:
    matches = [
        as_dict(raw) for raw in as_list(stage.get("routeChoices"))
        if as_dict(raw).get("topologyGroup") == topology_group
        and as_dict(raw).get("routeKind") == route_kind
    ]
    if len(matches) != 1:
        raise ValueError(f"expected one stage route choice for {topology_group!r} / {route_kind!r}")
    return matches[0]


def choose_profile(choice: Json, profile_document: str | None) -> Json:
    if choice.get("nextStagingAction") != "stage_ready_revision_available":
        raise ValueError(
            f"route is not ready for an isolated launch: {choice.get('nextStagingAction')!r}"
        )
    candidates = [
        as_dict(raw) for raw in as_list(choice.get("candidateRevisions"))
        if as_dict(raw).get("stageState") == "ready_without_catalog_or_asset_stage"
    ]
    selected = as_dict(choice.get("selectedRevision"))
    if profile_document is None and selected:
        selected_document = as_dict(selected.get("profileDocument")).get("path")
        selected_key = selected.get("key")
        candidates = [
            candidate for candidate in candidates
            if as_dict(candidate.get("profileDocument")).get("path") == selected_document
            and candidate.get("key") == selected_key
        ]
    if profile_document is not None:
        candidates = [
            candidate for candidate in candidates
            if as_dict(candidate.get("profileDocument")).get("path") == profile_document
        ]
    if len(candidates) != 1:
        if profile_document is None:
            raise ValueError(
                "route has zero or multiple stage-ready revisions and no pinned selection; "
                "pass --profile-document to choose one explicitly"
            )
        raise ValueError(f"selected profile document is not the one stage-ready route revision: {profile_document!r}")
    return candidates[0]


def source_targets(route: Json, profile: Json) -> list[Json]:
    targets = [
        as_dict(raw) for raw in as_list(route.get("selectedValidationTargets"))
        if as_dict(raw).get("targetType") == "source_assignment"
    ]
    if not targets:
        raise ValueError("enemy route has no selected source assignment")
    assignments = {
        assignment.get("rendererPath"): assignment
        for assignment in (as_dict(raw) for raw in as_list(profile.get("renderers")))
        if isinstance(assignment.get("rendererPath"), str) and assignment.get("rendererPath")
    }
    paths: set[str] = set()
    for target in targets:
        renderer = target.get("rendererPath")
        if not isinstance(renderer, str) or not renderer or renderer in paths:
            raise ValueError("selected source assignments need distinct nonempty renderer paths")
        if renderer not in assignments:
            raise ValueError("selected source assignment is absent from the exact profile document: " + renderer)
        paths.add(renderer)
    return targets


def player_profile_target(route: Json, profile: Json) -> Json:
    targets = [
        as_dict(raw) for raw in as_list(route.get("selectedValidationTargets"))
        if as_dict(raw).get("targetType") == "player_profile_evidence"
    ]
    if len(targets) != 1:
        raise ValueError("player route must have exactly one selected profile-evidence target")
    target = targets[0]
    paths = target.get("rendererPaths")
    required = [as_dict(raw).get("rendererPath") for raw in as_list(profile.get("renderers"))]
    if (target.get("profile") != profile.get("key") or target.get("skinset") != profile.get("skinset")
            or not isinstance(paths, list) or any(not isinstance(path, str) or not path for path in paths)
            or len(paths) != len(set(paths)) or set(paths) != set(required)):
        raise ValueError("selected player evidence target differs from the exact required profile renderers")
    return target


def eligible_motion_renderers(targets: list[Json], profile: Json) -> list[str]:
    assignments = {
        assignment.get("rendererPath"): assignment
        for assignment in (as_dict(raw) for raw in as_list(profile.get("renderers")))
    }
    return [
        target["rendererPath"] for target in targets
        if as_dict(assignments.get(target.get("rendererPath"))).get("rendererKind", "SkinnedMeshRenderer")
        == "SkinnedMeshRenderer"
    ]


def choose_motion_renderer(targets: list[Json], profile: Json, requested: str | None) -> str:
    eligible = eligible_motion_renderers(targets, profile)
    if requested is not None:
        if requested not in eligible:
            raise ValueError("--motion-renderer-path must name a selected SkinnedMeshRenderer source assignment")
        return requested
    if len(eligible) == 1:
        return eligible[0]
    if not eligible:
        raise ValueError("selected route has no SkinnedMeshRenderer source assignment for the motion exercise")
    raise ValueError("route has multiple selected SkinnedMeshRenderer assignments; pass --motion-renderer-path explicitly")


def revision_row(stage: Json, profile_document: str, key: str) -> Json:
    matches = [
        as_dict(raw) for raw in as_list(stage.get("revisions"))
        if as_dict(as_dict(raw).get("profileDocument")).get("path") == profile_document
        and as_dict(raw.get("profile")).get("key") == key
    ]
    if len(matches) != 1:
        raise ValueError("stage ledger lacks the selected exact profile revision")
    return matches[0]


def document_route_kind(profile: Json) -> str:
    if profile.get("skinset") is not None:
        return "player_skinset_avatar"
    if profile.get("resourcePrefab") is not None:
        return "resource_prefab_override"
    return "direct_enemy"


def catalog_path(game: Path, route_kind: str) -> Path:
    return game / str(PROFILE_ROUTES[route_kind]["catalogFile"])


def validate_stage_ledger(
    root: Path,
    queue_path: Path,
    queue: Json,
    stage_path: Path,
    stage: Json,
    game: Path,
    route: Json,
    candidate: Json,
    motion_renderer_path: str | None = None,
) -> Json:
    stage_queue_input = as_dict(as_dict(stage.get("queue")).get("input"))
    if stage_queue_input.get("path") != relative(root, queue_path) or stage_queue_input.get("sha256") != sha256(queue_path):
        raise ValueError("stage-readiness ledger is stale for the current execution queue")
    selection_input = as_dict(as_dict(stage.get("profileSelections")).get("input"))
    if selection_input:
        selection_path_text = selection_input.get("path")
        selection_hash = selection_input.get("sha256")
        if not isinstance(selection_path_text, str) or not isinstance(selection_hash, str):
            raise ValueError("stage-readiness ledger has incomplete profile-selection identity")
        selection_path = repository_file(root, Path(selection_path_text), "profile selection ledger")
        if sha256(selection_path) != selection_hash:
            raise ValueError("stage-readiness ledger is stale for the current profile selections")
    if stage.get("isolatedGameRoot") != relative(root, game):
        raise ValueError("stage-readiness ledger belongs to a different isolated game root")
    profile_document = as_dict(candidate.get("profileDocument"))
    document_path_text = profile_document.get("path")
    expected_document_hash = profile_document.get("sha256")
    key = candidate.get("key")
    if not isinstance(document_path_text, str) or not isinstance(expected_document_hash, str) or not isinstance(key, str):
        raise ValueError("stage choice has incomplete exact profile identity")
    document_path = repository_file(root, Path(document_path_text), "selected profile document")
    if sha256(document_path) != expected_document_hash:
        raise ValueError("selected profile document changed after stage readiness was recorded")
    document = read_object(document_path)
    profiles = [as_dict(raw) for raw in as_list(document.get("profiles"))]
    matching_profiles = [profile for profile in profiles if profile.get("key") == key]
    if len(matching_profiles) != 1:
        raise ValueError("selected key is not unique in the profile document")
    profile = matching_profiles[0]
    expected_route_kind = document_route_kind(profile)
    coverage_route_kind = route.get("routeKind")
    if COVERAGE_TO_PROFILE_ROUTE.get(coverage_route_kind) != expected_route_kind:
        raise ValueError("selected profile document route differs from the selected coverage route")
    catalog_kind = str(PROFILE_ROUTES[expected_route_kind]["catalogKind"])
    if candidate.get("catalogKind") != catalog_kind:
        raise ValueError("stage choice catalog kind differs from the selected profile document")
    current_catalog = catalog_path(game, expected_route_kind)
    catalog_input = next(
        (as_dict(raw) for raw in as_list(stage.get("catalogInputs")) if as_dict(raw).get("kind") == catalog_kind),
        None,
    )
    if catalog_input is None or catalog_input.get("path") != relative(root, current_catalog):
        raise ValueError(f"stage ledger lacks the exact {catalog_kind} catalog input")
    if sha256(current_catalog) != catalog_input.get("sha256"):
        raise ValueError(f"isolated {catalog_kind} catalog changed after stage readiness was recorded")
    current = read_object(current_catalog)
    current_profile = next(
        (as_dict(raw) for raw in as_list(current.get("profiles")) if as_dict(raw).get("key") == key),
        None,
    )
    if current_profile != profile:
        raise ValueError("isolated catalog profile differs from the selected stage-ready document")
    revision = revision_row(stage, document_path_text, key)
    if revision.get("routeKind") != expected_route_kind or revision.get("profile") != profile:
        raise ValueError("selected profile document differs from the stage ledger revision")
    if revision.get("stageState") != "ready_without_catalog_or_asset_stage":
        raise ValueError("selected profile revision is no longer stage-ready")
    models = game / MODELS_RELATIVE
    asset_checks: list[Json] = []
    for raw_asset in as_list(revision.get("assets")):
        asset = as_dict(raw_asset)
        name = asset.get("name")
        source_hash = asset.get("sourceSha256")
        target_hash = asset.get("targetSha256")
        if not isinstance(name, str) or not isinstance(source_hash, str) or not isinstance(target_hash, str):
            raise ValueError("stage ledger asset record is incomplete")
        source = document_path.parent / name
        target = models / name
        if (not source.is_file() or source.is_symlink()
                or not target.is_file() or target.is_symlink()):
            raise ValueError(f"selected stage-ready asset is missing or unsafe: {name}")
        if sha256(source) != source_hash or sha256(target) != target_hash or source_hash != target_hash:
            raise ValueError(f"selected asset changed after stage readiness was recorded: {name}")
        asset_checks.append({"name": name, "sha256": source_hash})
    result: Json = {
        "profile": {
            "key": key,
            "document": {"path": document_path_text, "sha256": expected_document_hash},
            "routeKind": expected_route_kind,
        },
        "catalog": {"path": relative(root, current_catalog), "sha256": sha256(current_catalog)},
        "assets": asset_checks,
        "queue": input_reference(root, queue_path),
        "stageReadiness": input_reference(root, stage_path),
    }
    if catalog_kind == "enemy":
        targets = source_targets(route, profile)
        result["sourceAssignments"] = targets
        result["motionRendererPath"] = choose_motion_renderer(targets, profile, motion_renderer_path)
    else:
        result["playerProfileEvidence"] = player_profile_target(route, profile)
        result["conditionalApparel"] = as_list(profile.get("apparel"))
    return result


def active_game_pids(game: Path) -> list[int]:
    executable = str(game / "FTK.app/Contents/MacOS/FTK")
    result = subprocess.run(
        ["ps", "-ax", "-o", "pid=,command="],
        check=True,
        text=True,
        capture_output=True,
    )
    pids: list[int] = []
    for line in result.stdout.splitlines():
        fields = line.strip().split(maxsplit=1)
        if len(fields) == 2 and (fields[1] == executable or fields[1].startswith(executable + " ")):
            pids.append(int(fields[0]))
    return pids


def active_ftk_processes() -> list[tuple[int, str]]:
    result = subprocess.run(
        ["ps", "-ax", "-o", "pid=,command="],
        check=True,
        text=True,
        capture_output=True,
    )
    suffix = "/FTK.app/Contents/MacOS/FTK"
    result_rows: list[tuple[int, str]] = []
    for line in result.stdout.splitlines():
        fields = line.strip().split(maxsplit=1)
        if len(fields) == 2 and (fields[1].endswith(suffix) or (suffix + " ") in fields[1]):
            result_rows.append((int(fields[0]), fields[1]))
    return result_rows


def assert_fresh_launch_environment(game: Path, port: int) -> None:
    executable = str(game / "FTK.app/Contents/MacOS/FTK")
    foreign = [pid for pid, command in active_ftk_processes()
               if command != executable and not command.startswith(executable + " ")]
    if foreign:
        raise RuntimeError("another FTK session is running; refusing a concurrent isolated launch: " + ", ".join(map(str, foreign)))
    pids = active_game_pids(game)
    if pids:
        raise RuntimeError("isolated FTK already has a running owner: " + ", ".join(map(str, pids)))
    try:
        connection = socket.create_connection(("127.0.0.1", port), timeout=0.2)
    except OSError:
        return
    connection.close()
    raise RuntimeError(f"bridge port {port} is already in use; refusing to attach to an unknown session")


def bridge_state(port: int) -> Json:
    with urllib.request.urlopen(f"http://127.0.0.1:{port}/state", timeout=1) as response:
        return as_dict(json.load(response))


def read_session(game: Path) -> tuple[str | None, float | None]:
    path = game / "model-test-session.json"
    if not path.is_file():
        return None, None
    try:
        return as_dict(json.loads(path.read_text())).get("session"), path.stat().st_mtime
    except (OSError, ValueError):
        return None, None


def wait_registration(game: Path, previous_session: str | None, previous_mtime: float | None) -> None:
    session_path = game / "model-test-session.json"
    registration_path = game / "model-test-registration.json"
    for _ in range(60):
        try:
            session = as_dict(json.loads(session_path.read_text())).get("session")
            mtime = session_path.stat().st_mtime
            registration = as_dict(json.loads(registration_path.read_text()))
            updated = datetime.fromisoformat(str(registration["updatedUtc"]).replace("Z", "+00:00")).timestamp()
            if session != previous_session and (previous_mtime is None or mtime > previous_mtime) and mtime <= updated <= time.time():
                return
        except (OSError, KeyError, ValueError, TypeError):
            pass
        time.sleep(1)
    raise TimeoutError("fresh bridge registration with a new helper session was not observed")


def wait_menu(game: Path, port: int, previous_session: str | None, previous_mtime: float | None) -> None:
    for _ in range(60):
        try:
            if bridge_state(port).get("phase") == "menu":
                wait_registration(game, previous_session, previous_mtime)
                return
        except Exception:
            pass
        time.sleep(1)
    raise TimeoutError("fresh isolated game did not reach menu")


def newest_case_file(output: Path, suffix: str, after: float) -> Path:
    candidates = [path for path in output.rglob(suffix) if path.stat().st_mtime >= after]
    if not candidates:
        raise FileNotFoundError(f"no {suffix} newer than the issued action")
    return max(candidates, key=lambda path: path.stat().st_mtime)


def launch_owned_game(game: Path, port: int, output: Path, launch_env: dict[str, str]) -> tuple[subprocess.Popen, Any]:
    stamp = time.strftime("%Y%m%d-%H%M%S")
    launch_log = output.parent / f"launch-{stamp}.log"
    player_log = output.parent / f"player-{stamp}.log"
    handle = launch_log.open("w")
    environment = {
        **os.environ,
        "FTK_MODEL_TEST": "1",
        "FTK_MODEL_TEST_ROOT": str(game),
        "FTK_AGENT_BRIDGE": "1",
        "FTK_AGENT_BRIDGE_PORT": str(port),
        **launch_env,
    }
    process = subprocess.Popen(
        [
            "./run_bepinex.sh", "FTK.app", "-screen-width", "1280", "-screen-height", "832",
            "-screen-fullscreen", "0", "-logFile", str(player_log),
        ],
        cwd=game,
        env=environment,
        stdout=handle,
        stderr=subprocess.STDOUT,
        start_new_session=True,
    )
    return process, handle


def stop_owned_game(process: subprocess.Popen | None, handle: Any | None) -> None:
    if process is not None:
        try:
            os.killpg(process.pid, signal.SIGTERM)
        except ProcessLookupError:
            pass
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            try:
                os.killpg(process.pid, signal.SIGKILL)
            except ProcessLookupError:
                pass
            process.wait(timeout=5)
    if handle is not None:
        handle.close()


def parse_environment(value: str) -> tuple[str, str]:
    name, separator, content = value.partition("=")
    if not separator or not name or not name.replace("_", "").isalnum() or not name[0].isalpha():
        raise argparse.ArgumentTypeError("launch environment must be NAME=VALUE with an alphanumeric underscore name")
    return name, content


def review_requirements(status: Any) -> Json:
    completed = status == "needs_visual_review"
    return {
        "needsManualVisualReview": completed,
        "needsImmutableArchive": completed,
    }


def apply_selected_workflow(plan: Json, choice: Json) -> Json:
    workflow = choice.get("selectedWorkflow") or "standard_enemy_exercise"
    if workflow == "standard_enemy_exercise":
        plan["workflow"] = {"kind": workflow}
        return plan
    if workflow != "passive_enemy_arrival":
        raise ValueError(f"unsupported selected enemy workflow: {workflow!r}")
    level = choice.get("arrivalLevel")
    room = choice.get("arrivalRoom")
    if type(level) is not int or level < 0 or type(room) is not int or room < 0:
        raise ValueError("passive arrival workflow requires nonnegative integer arrivalLevel/arrivalRoom")
    plan["workflow"] = {
        "kind": workflow,
        "level": level,
        "room": room,
        "boundary": (
            "Preserve native AI and initiative. The setup enemy may remove itself before a hero turn; "
            "wait read-only for the next strict Ready slot, then arm and issue one passive arrival."
        ),
    }
    return plan


def run_exercise(root: Path, game: Path, port: int, plan: Json, attack_attempts: int,
                 cap_equipped_attack_skill: bool, minimum_native_weapon_max_damage: int | None,
                 launch_env: dict[str, str]) -> Json:
    output = game / "model-test-output"
    if not output.is_dir():
        raise FileNotFoundError(output)
    profile = as_dict(plan.get("profile"))
    motion_renderer = plan["motionRendererPath"]
    key = profile["key"]
    process = None
    handle = None
    issued_at = time.time()
    record: Json = {"plan": plan, "status": "issued"}
    try:
        assert_fresh_launch_environment(game, port)
        previous_session, previous_mtime = read_session(game)
        process, handle = launch_owned_game(game, port, output, launch_env)
        wait_menu(game, port, previous_session, previous_mtime)
        stage_started = time.time()
        stage_command = [
                "python3", "tools/ai-model-pipeline/runtime-test/run_case.py", "--root", str(game),
                "--port", str(port), "--enemy", key, "new-run",
            ]
        if cap_equipped_attack_skill:
            stage_command.append("--cap-equipped-attack-skill")
        if minimum_native_weapon_max_damage is not None:
            stage_command.extend(["--minimum-native-weapon-max-damage", str(minimum_native_weapon_max_damage)])
        stage = subprocess.run(
            stage_command,
            cwd=root,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=240,
            check=False,
        )
        record["stageExitCode"] = stage.returncode
        if stage.returncode != 0:
            raise RuntimeError(f"run_case exited {stage.returncode}")
        stage_result = newest_case_file(output, "result.json", stage_started)
        stage_data = read_object(stage_result)
        if stage_data.get("status") != "binding_metadata_observed":
            raise RuntimeError("run_case did not preserve a complete binding metadata result")
        damage_fixture = as_dict(stage_data.get("heroDamageFixture"))
        if minimum_native_weapon_max_damage is not None:
            applied = as_dict(damage_fixture.get("apply"))
            if (damage_fixture.get("status") != "applied_outside_combat"
                    or applied.get("minimumNativeWeaponMaxDamage") != minimum_native_weapon_max_damage
                    or not isinstance(applied.get("receipt"), str) or not applied.get("receipt")):
                raise RuntimeError("run_case did not preserve the requested outside-combat damage fixture receipt")
        exercise_started = time.time()
        exercise_command = [
                "python3", "tools/ai-model-pipeline/runtime-test/exercise_case.py", "--root", str(game),
                "--port", str(port), "--enemy", key, "--renderer-path", motion_renderer,
                "--profile-sha256", as_dict(plan.get("catalog"))["sha256"],
                "--attack-attempts", str(attack_attempts),
            ]
        if minimum_native_weapon_max_damage is not None:
            exercise_command.extend(["--staged-damage-fixture-result", str(stage_result)])
        exercise = subprocess.run(
            exercise_command,
            cwd=root,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=720,
            check=False,
        )
        case_result = newest_case_file(output, "case-result.json", exercise_started)
        result = read_object(case_result)
        record.update({
            "status": result.get("status"),
            "stageResult": relative(root, stage_result),
            "stageBindingMatches": as_list(stage_data.get("matches")),
            "partyFixture": stage_data.get("partyFixture"),
            "heroDamageFixture": stage_data.get("heroDamageFixture"),
            "caseResult": relative(root, case_result),
            "exerciseExitCode": exercise.returncode,
            "session": result.get("session"),
            "initialRenderer": result.get("initialRenderer"),
            "actions": [
                {"action": as_dict(raw).get("action"), "attempt": as_dict(raw).get("attempt"),
                 "hpOutcome": as_dict(raw).get("hpOutcome")}
                for raw in as_list(result.get("actions"))
            ],
            "collectCount": len(as_list(result.get("collects"))),
            "strictReady": as_dict(result.get("finalReady")).get("strictReady"),
            **review_requirements(result.get("status")),
        })
    except Exception as error:
        record.update(status="runner_error", error=f"{type(error).__name__}: {error}")
    finally:
        stop_owned_game(process, handle)
    record["finishedUtc"] = datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    record["issuedUnixSeconds"] = issued_at
    return record


def run_passive_arrival(root: Path, game: Path, port: int, plan: Json,
                        launch_env: dict[str, str]) -> Json:
    output = game / "model-test-output"
    if not output.is_dir():
        raise FileNotFoundError(output)
    profile = as_dict(plan.get("profile"))
    workflow = as_dict(plan.get("workflow"))
    key = profile["key"]
    motion_renderer = plan["motionRendererPath"]
    process = None
    handle = None
    issued_at = time.time()
    record: Json = {"plan": plan, "status": "issued"}
    try:
        assert_fresh_launch_environment(game, port)
        previous_session, previous_mtime = read_session(game)
        process, handle = launch_owned_game(game, port, output, launch_env)
        wait_menu(game, port, previous_session, previous_mtime)

        stage_started = time.time()
        stage = subprocess.run(
            [
                "python3", "tools/ai-model-pipeline/runtime-test/run_case.py",
                "--root", str(game), "--port", str(port), "--enemy", key,
                "--wait-timeout", "30", "new-run",
            ],
            cwd=root,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=240,
            check=False,
        )
        stage_result = newest_case_file(output, "result.json", stage_started)
        stage_data = read_object(stage_result)
        expected_timeout = (
            stage_data.get("status") == "stopped"
            and stage_data.get("error")
            == "Timed out waiting for exact single enemy and heroTurnReady or pending story; no action retried"
        )
        if stage_data.get("status") != "binding_metadata_observed" and not expected_timeout:
            raise RuntimeError("passive setup did not reach the expected native pre-hero-turn boundary")
        session = stage_data.get("session")
        if not isinstance(session, str) or len(session) != 32:
            raise RuntimeError("passive setup did not preserve its exact helper session")

        pass_result = None
        pass_exit_code = None
        if stage_data.get("status") == "binding_metadata_observed":
            pass_started = time.time()
            passed = subprocess.run(
                [
                    "python3", "tools/ai-model-pipeline/runtime-test/record_case.py",
                    "--root", str(game), "--port", str(port), "--enemy", key,
                    "--renderer-path", motion_renderer,
                    "--profile-sha256", as_dict(plan.get("catalog"))["sha256"],
                    "--action", "pass", "--motion-evidence", "--capture-timeout", "360",
                ],
                cwd=root,
                stdout=subprocess.DEVNULL,
                stderr=subprocess.DEVNULL,
                timeout=600,
                check=False,
            )
            pass_exit_code = passed.returncode
            pass_result = newest_case_file(output, "result.json", pass_started)
            pass_data = read_object(pass_result)
            action_result = as_dict(pass_data.get("actionResult"))
            action_body = as_dict(action_result.get("result")) or action_result
            if (pass_data.get("action") != "pass" or action_result.get("ok") is not True
                    or action_body.get("ended") != "combat"):
                raise RuntimeError("native setup Pass was not confirmed exactly once")

        arrival_started = time.time()
        arrival = subprocess.run(
            [
                "python3", "tools/ai-model-pipeline/runtime-test/arrival_case.py",
                "--root", str(game), "--port", str(port), "--session", session,
                "--enemy", key, "--renderer-path", motion_renderer,
                "--profile-sha256", as_dict(plan.get("catalog"))["sha256"],
                "--level", str(workflow["level"]), "--room", str(workflow["room"]),
                "--ready-timeout", "420", "--capture-timeout", "360",
                "--post-ready-level", str(workflow["level"]),
                "--post-ready-room", str(workflow["room"] + 1),
                "--post-ready-timeout", "300",
            ],
            cwd=root,
            stdout=subprocess.DEVNULL,
            stderr=subprocess.DEVNULL,
            timeout=1200,
            check=False,
        )
        arrival_result = newest_case_file(output, "arrival-case-result.json", arrival_started)
        result = read_object(arrival_result)
        complete = (
            arrival.returncode == 0
            and result.get("status") == "terminal_capture_observed_no_acceptance"
            and result.get("terminal") is True
            and result.get("rawCaptureOk") is True
            and result.get("retainedFrames") == 120
            and as_dict(result.get("readyPreflight")).get("strictReady") is not None
            and as_dict(as_dict(result.get("postArrivalReady")).get("strictReady")).get("ok") is True
        )
        record.update({
            "status": "needs_visual_review" if complete else "stopped",
            "stageExitCode": stage.returncode,
            "stageResult": relative(root, stage_result),
            "stageBoundary": "native_self_removal_before_hero_turn" if expected_timeout else "binding_metadata_observed",
            "setupPassExitCode": pass_exit_code,
            "setupPassResult": relative(root, pass_result) if pass_result is not None else None,
            "arrivalExitCode": arrival.returncode,
            "arrivalResult": relative(root, arrival_result),
            "session": session,
            "readyPreflight": result.get("readyPreflight"),
            "postArrivalReady": result.get("postArrivalReady"),
            "rawCapturePath": result.get("rawCapturePath"),
            "rawCaptureSha256": result.get("rawCaptureSha256"),
            "rawCaptureOk": result.get("rawCaptureOk"),
            "retainedFrames": result.get("retainedFrames"),
            **review_requirements("needs_visual_review" if complete else "stopped"),
        })
        if not complete:
            record["arrivalErrors"] = as_list(result.get("errors"))
    except Exception as error:
        record.update(status="runner_error", error=f"{type(error).__name__}: {error}")
    finally:
        stop_owned_game(process, handle)
    record["finishedUtc"] = datetime.now(timezone.utc).replace(microsecond=0).isoformat().replace("+00:00", "Z")
    record["issuedUnixSeconds"] = issued_at
    return record


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--root", type=Path, default=Path.cwd())
    parser.add_argument("--queue", type=Path, default=Path("docs/model-validation-execution-queue.json"))
    parser.add_argument("--stage-readiness", type=Path, required=True)
    parser.add_argument("--game-root", type=Path, required=True)
    parser.add_argument("--topology-group", required=True)
    parser.add_argument("--route-kind", choices=("directEnemy", "resourcePrefab"))
    parser.add_argument("--profile-document", help="Required only to disambiguate multiple stage-ready historical revisions.")
    parser.add_argument("--motion-renderer-path", help="Required when the exact route selects multiple skinned renderer assignments.")
    parser.add_argument("--port", type=int, default=8788)
    parser.add_argument("--attack-attempts", type=int, default=8)
    parser.add_argument("--cap-equipped-attack-skill", action="store_true",
                        help="Use the recorded native-stat-cap party fixture before the ordinary no-focus exercise.")
    parser.add_argument("--minimum-native-weapon-max-damage", type=int,
                        help="Explicit opt-in temporary hero physical-damage target for the ordinary no-focus attack gate.")
    parser.add_argument("--launch-env", action="append", type=parse_environment, default=[])
    parser.add_argument("--run", action="store_true", help="Launch one owned isolated FTK process and issue the bounded exercise.")
    parser.add_argument("--output", type=Path, help="New immutable runner record required with --run.")
    args = parser.parse_args()
    if not 1 <= args.attack_attempts <= 8:
        parser.error("--attack-attempts must be in 1..8")
    if args.minimum_native_weapon_max_damage is not None and not 1 <= args.minimum_native_weapon_max_damage <= 100:
        parser.error("--minimum-native-weapon-max-damage must be in 1..100")
    if not 1 <= args.port <= 65535:
        parser.error("--port must be in 1..65535")
    if args.run and args.output is None:
        parser.error("--run requires a new --output path")
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
        route = selected_route(queue, args.topology_group, args.route_kind)
        if route.get("routeKind") not in ("directEnemy", "resourcePrefab"):
            raise ValueError("selected route needs the separate player native-preview workflow")
        choice = selected_choice(stage, args.topology_group, route["routeKind"])
        candidate = choose_profile(choice, args.profile_document)
        selected_motion_renderer = args.motion_renderer_path
        if selected_motion_renderer is None:
            selected_motion_renderer = choice.get("selectedMotionRendererPath")
        plan = validate_stage_ledger(
            root, queue_path, queue, stage_path, stage, game, route, candidate, selected_motion_renderer
        )
        apply_selected_workflow(plan, choice)
    except (ValueError, FileNotFoundError) as error:
        parser.error(str(error))
    plan["topologyGroup"] = route["topologyGroup"]
    plan["coverageRouteKind"] = route["routeKind"]
    plan["partyFixture"] = {
        "capEquippedAttackSkill": args.cap_equipped_attack_skill,
        "minimumNativeWeaponMaxDamage": args.minimum_native_weapon_max_damage,
        "boundary": "Optional disposable hero-side stat fixtures. Enemy stats, focus, RNG, native weapon, action, response, and animation authority remain native. A requested maximum damage is a bounded fixture and does not guarantee actual damage.",
    }
    plan["limits"] = [
        "The runner performs one bounded machine exercise only.",
        "Inspect retained capture poses manually and build a new immutable archive before recording any canonical coverage credit.",
        "A nonzero or partial exercise result remains evidence to inspect; it is not permission to retry uncertain gameplay actions.",
    ]
    if as_dict(plan.get("workflow")).get("kind") == "passive_enemy_arrival":
        if args.cap_equipped_attack_skill or args.minimum_native_weapon_max_damage is not None:
            parser.error("passive arrival does not accept hero skill or damage fixtures")
        plan["limits"] = [
            "The runner preserves native AI and initiative and issues no hero combat action.",
            "The passive arrival can establish exact binding, appearance, native self-removal motion, and its strict pre-arrival Ready boundary. It cannot establish an ordinary hero hit or ordinary lethal damage.",
            "Inspect retained frames manually and build a new immutable archive before recording canonical coverage credit.",
        ]
    if not args.run:
        print(json.dumps(plan, indent=2))
        return 0
    try:
        output = runner_record_path(root, args.output)
    except ValueError as error:
        parser.error(str(error))
    if output.exists():
        parser.error(f"refusing to overwrite runner record: {output}")
    output.parent.mkdir(parents=True, exist_ok=True)
    if as_dict(plan.get("workflow")).get("kind") == "passive_enemy_arrival":
        record = run_passive_arrival(root, game, args.port, plan, dict(args.launch_env))
    else:
        record = run_exercise(root, game, args.port, plan, args.attack_attempts,
                              args.cap_equipped_attack_skill, args.minimum_native_weapon_max_damage,
                              dict(args.launch_env))
    output.write_text(json.dumps(record, indent=2) + "\n")
    print(json.dumps({"status": record.get("status"), "record": relative(root, output)}, indent=2))
    return 0 if record.get("status") != "runner_error" else 1


if __name__ == "__main__":
    raise SystemExit(main())
