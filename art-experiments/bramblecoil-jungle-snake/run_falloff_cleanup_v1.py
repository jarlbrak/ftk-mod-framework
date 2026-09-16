#!/usr/bin/env python3
"""Exercise Bramblecoil's opted fall-off fixture through guarded native Collect → Ready.

This is deliberately separate from the paired fall-off comparison.  It runs a
fresh isolated game, proves the exact opted registration and renderer identity,
issues one explicit KillSingle fixture, then observes and submits only native
Collect votes until the game's strict Ready predicate is reached.  It never
calls a raw FSM event, advances a room directly, or relabels the fixture as
ordinary lethal damage.
"""
from __future__ import annotations

import argparse
import hashlib
import json
import os
from datetime import datetime
from pathlib import Path
import socket
import subprocess
import sys
import time


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
GAME = ROOT / "scratch/mirewarden-game"
OUT = GAME / "model-test-output"
PORT = 8788
PROFILE = "ftkmf_modeltest_bramblecoil_jungle_snake"
RENDERER = "enJungleSnakeC"
MESH = "ftkmf_glb_bramblecoil-jungle-snake.glb"
POLICY = "preserve-custom-body"
RUNTIME_TEST = ROOT / "tools/ai-model-pipeline/runtime-test"
sys.path.insert(0, str(RUNTIME_TEST))
from exercise_case import collect_button, loot_fingerprint, loot_progress  # noqa: E402
from run_case import Runner  # noqa: E402


parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--deployment-receipt", type=Path, required=True)
args = parser.parse_args()
RECEIPT = args.deployment_receipt.resolve(strict=True)


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def pin(path: Path) -> dict:
    return {"path": str(path.relative_to(ROOT)), "sha256": sha(path), "bytes": path.stat().st_size}


def active_game_pids() -> list[int]:
    needle = str(GAME / "FTK.app/Contents/MacOS/FTK")
    result = subprocess.run(["ps", "-ax", "-o", "pid=,command="], check=True, text=True, capture_output=True)
    return [int(parts[0]) for line in result.stdout.splitlines()
            if len(parts := line.strip().split(maxsplit=1)) == 2 and needle in parts[1]]


def assert_fresh_launch_environment() -> None:
    pids = active_game_pids()
    if pids:
        raise RuntimeError("An existing FTK process owns the isolated root; refusing to attach or stop it: " + ", ".join(map(str, pids)))
    try:
        connection = socket.create_connection(("127.0.0.1", PORT), timeout=0.2)
    except OSError:
        return
    connection.close()
    raise RuntimeError(f"Bridge port {PORT} is already in use; refusing to attach to an unknown session")


def assert_deployment() -> dict:
    receipt = read(RECEIPT)
    expected = {"model-test-profiles.json": receipt["catalog"]["sha256"]}
    expected.update({name: item["sha256"] for name, item in receipt["newPluginDlls"].items()})
    paths = {
        "model-test-profiles.json": GAME / "model-test-profiles.json",
        "FTKModFramework.dll": GAME / "BepInEx/plugins/FTKModFramework.dll",
        "FtkRuntimeModelTest.dll": GAME / "BepInEx/plugins/FtkRuntimeModelTest.dll",
        "FtkRuntimeModelTestContent.dll": GAME / "BepInEx/plugins/FtkRuntimeModelTestContent.dll",
    }
    for name, path in paths.items():
        if name not in expected or not path.is_file() or sha(path) != expected[name]:
            raise RuntimeError(f"Current isolated deployment does not match {RECEIPT.name}: {name}")
    return {"receipt": pin(RECEIPT), "catalog": pin(paths["model-test-profiles.json"]),
            "plugins": {name: pin(path) for name, path in paths.items() if name != "model-test-profiles.json"}}


def http_state() -> dict:
    import urllib.request
    with urllib.request.urlopen(f"http://127.0.0.1:{PORT}/state", timeout=1) as response:
        return json.load(response)


def wait_registration(previous_session: str | None, prior_mtime: float) -> tuple[str, dict]:
    registration_path = GAME / "model-test-registration.json"
    session_path = GAME / "model-test-session.json"
    for _ in range(60):
        try:
            registration = read(registration_path)
            session = read(session_path)["session"]
            updated = datetime.fromisoformat(registration["updatedUtc"].replace("Z", "+00:00")).timestamp()
            fresh = session != previous_session and session_path.stat().st_mtime > prior_mtime and session_path.stat().st_mtime <= updated <= time.time()
            entries = {row.get("key"): row for row in registration.get("registered", [])}
            row = entries.get(PROFILE)
            if fresh and registration.get("status") == "registered" and registration.get("error") is None and row:
                if row.get("fallOffPolicy") != POLICY:
                    raise RuntimeError("Fresh registration does not report preserve-custom-body for Bramblecoil")
                return session, registration
        except (FileNotFoundError, KeyError, ValueError):
            pass
        time.sleep(1)
    raise TimeoutError("fresh opted Bramblecoil registration was not observed")


def wait_menu(previous_session: str | None, prior_mtime: float) -> tuple[str, dict]:
    for _ in range(60):
        try:
            if http_state().get("phase") == "menu":
                return wait_registration(previous_session, prior_mtime)
        except Exception:
            pass
        time.sleep(1)
    raise TimeoutError("fresh game did not reach menu")


def newest_result(after: float) -> Path:
    candidates = [path for path in OUT.rglob("result.json") if path.stat().st_mtime >= after]
    if not candidates:
        raise FileNotFoundError("No result.json created for the requested operation")
    return max(candidates, key=lambda path: path.stat().st_mtime)


def stop_owned_game(process: subprocess.Popen, handle) -> None:
    if process.poll() is None:
        process.terminate()
        try:
            process.wait(timeout=5)
        except subprocess.TimeoutExpired:
            process.kill()
            process.wait(timeout=5)
    handle.close()


def death_state(frame: dict) -> bool:
    layers = (frame.get("animator") or {}).get("layers") or []
    playing = layers[0].get("playing", []) if layers else []
    return any(str(clip.get("name", "")).startswith("Snake_Death") for clip in playing)


assert_fresh_launch_environment()
deployment = assert_deployment()
session_path = GAME / "model-test-session.json"
previous_session = read(session_path).get("session")
prior_mtime = session_path.stat().st_mtime
timestamp = time.strftime("%Y%m%d-%H%M%S")
launch_log = GAME / f"launch-bramblecoil-falloff-cleanup-v1-{timestamp}.log"
player_log = GAME / f"player-bramblecoil-falloff-cleanup-v1-{timestamp}.log"
handle = launch_log.open("w")
process = subprocess.Popen(
    ["./run_bepinex.sh", "FTK.app", "-screen-width", "1280", "-screen-height", "832", "-screen-fullscreen", "0", "-logFile", str(player_log)],
    cwd=GAME,
    env={**os.environ, "FTK_MODEL_TEST": "1", "FTK_MODEL_TEST_ROOT": str(GAME),
         "FTK_AGENT_BRIDGE": "1", "FTK_AGENT_BRIDGE_PORT": str(PORT), "FTK_MIREWARDEN_BODY": "1"},
    stdout=handle, stderr=subprocess.STDOUT, start_new_session=True,
)
summary = ROOT / "scratch" / f"bramblecoil-falloff-cleanup-v1-{timestamp}.json"
record = {
    "status": "IN_PROGRESS",
    "purpose": "Fresh explicit KillSingle fixture plus guarded native Collect to strict Ready for the opted Bramblecoil fall-off profile; not ordinary damage or ordinary lethal evidence.",
    "profile": PROFILE,
    "rendererPath": RENDERER,
    "expectedPolicy": POLICY,
    "deployment": deployment,
    "launchLog": str(launch_log.relative_to(ROOT)),
    "playerLog": str(player_log.relative_to(ROOT)),
}
try:
    session, registration = wait_menu(previous_session, prior_mtime)
    registration_path = GAME / "model-test-registration.json"
    stage_started = time.time()
    stage = subprocess.run(
        ["python3", "tools/ai-model-pipeline/runtime-test/run_case.py", "--root", str(GAME), "--port", str(PORT), "--enemy", PROFILE, "new-run"],
        cwd=ROOT, text=True, capture_output=True, timeout=240, check=False,
    )
    if stage.returncode != 0:
        raise RuntimeError("new-run failed: " + (stage.stderr[-2000:] or stage.stdout[-2000:]))
    stage_result = newest_result(stage_started)
    before_fixture = http_state()
    dungeon = before_fixture.get("dungeon") or {}
    start_level, start_room = dungeon.get("level"), dungeon.get("room")
    if not isinstance(start_level, int) or not isinstance(start_room, int):
        raise RuntimeError("native dungeon indices were unavailable before the fixture")

    fixture_started = time.time()
    fixture = subprocess.run(
        ["python3", "tools/ai-model-pipeline/runtime-test/record_case.py", "--root", str(GAME), "--port", str(PORT),
         "--enemy", PROFILE, "--renderer-path", RENDERER, "--profile-sha256", deployment["catalog"]["sha256"], "--action", "kill-fixture"],
        cwd=ROOT, text=True, capture_output=True, timeout=300, check=False,
    )
    fixture_result = newest_result(fixture_started)
    fixture_doc = read(fixture_result)
    action = fixture_doc.get("actionResult", {}).get("result", {})
    if fixture.returncode != 0 or fixture_doc.get("ok") is not True or action.get("committed") != "KillSingle":
        raise RuntimeError("fixture did not report exactly one accepted KillSingle action")
    capture = Path(fixture_doc["capture"]["result"])
    raw = read(capture)
    frames = raw.get("frames") or []
    if raw.get("ok") is not True or raw.get("session") != session or len(frames) != 120:
        raise RuntimeError("fixture capture is incomplete or belongs to another session")
    identity = {key: frames[0].get(key) for key in ("mesh", "ownerInstanceId", "instanceId", "boneSignature")}
    if identity["mesh"] != MESH or not identity["ownerInstanceId"] or not identity["boneSignature"]:
        raise RuntimeError("fixture did not begin on the exact leased Bramblecoil renderer")
    if not all(all(frame.get(key) == value for key, value in identity.items()) for frame in frames):
        raise RuntimeError("fixture capture lost renderer identity")
    deaths = [index for index, frame in enumerate(frames) if death_state(frame)]
    if not deaths:
        raise RuntimeError("fixture capture never reached native Snake_Death")
    first_death = deaths[0]
    if not all(frame.get("active") is True and frame.get("isVisible") is True and frame.get("enabled") is True for frame in frames[first_death:]):
        raise RuntimeError("opted custom renderer did not remain visible through the captured native death handoff")

    runner_args = argparse.Namespace(root=GAME, port=PORT, enemy=PROFILE, class_key=None,
                                     mode="bramblecoil-falloff-cleanup", operation_timeout=40, wait_timeout=120)
    cleanup = Runner(runner_args)
    observations, collects = [], []
    deadline = time.monotonic() + 120
    awaiting_progress = False
    last_fingerprint = None
    while time.monotonic() < deadline:
        state = cleanup.state()
        fixture_state = cleanup.helper("fixture-state")
        strict_ready = fixture_state.get("strictReady") or {}
        strict_collect = fixture_state.get("strictLootCollect") or {}
        observation = {
            "dungeon": state.get("dungeon"),
            "signals": state.get("signals"),
            "livingEnemies": [enemy for enemy in ((state.get("combat") or {}).get("enemies") or []) if enemy.get("alive") is True],
            "strictLootCollect": strict_collect,
            "strictReady": strict_ready,
        }
        observations.append(observation)
        signals = state.get("signals") or {}
        if signals.get("choiceOpen") is not False or signals.get("modalOpen") is not False:
            raise RuntimeError("Unknown modal appeared during native loot progression")
        if strict_ready.get("ok") is True:
            if strict_ready.get("level") != start_level or strict_ready.get("room") != start_room + 1:
                raise RuntimeError("Strict Ready reached unexpected dungeon slot")
            break
        if observation["livingEnemies"]:
            raise RuntimeError("A living enemy remained during native loot progression")
        fingerprint = loot_fingerprint(state, fixture_state)
        if awaiting_progress:
            if not loot_progress(last_fingerprint, fingerprint):
                time.sleep(.25)
                continue
            awaiting_progress = False
        if strict_collect.get("ok") is not True:
            time.sleep(.25)
            continue
        if len(collects) >= 8:
            raise RuntimeError("Eight native collect limit reached")
        button = collect_button(fixture_state)
        result = cleanup.helper("collect-loot", {"heroInstanceId": button["heroInstanceId"]})
        if not (result.get("status") == "clicked" and result.get("method") == "VoteButton.OnLeftClick(Collect)"
                and all(result.get(key) == value for key, value in button.items())):
            raise RuntimeError("Collect response identity was uncertain; no retry")
        collects.append({"before": button, "result": result})
        last_fingerprint = fingerprint
        awaiting_progress = True
        time.sleep(.25)
    else:
        raise TimeoutError("Native loot progress/strict Ready not observed; no collect retried")

    record.update({
        "status": "PASS_EXPLICIT_FALLOFF_FIXTURE_AND_NATIVE_COLLECT_READY",
        "session": session,
        "registration": pin(registration_path),
        "registrationEntry": next(row for row in registration["registered"] if row.get("key") == PROFILE),
        "stage": pin(stage_result),
        "fixture": pin(fixture_result),
        "fixtureAction": action,
        "capture": pin(capture),
        "cleanupJournal": pin(cleanup.journal),
        "startDungeon": {"level": start_level, "room": start_room},
        "finalReady": strict_ready,
        "collects": collects,
        "lootObservations": observations,
        "identity": identity,
        "firstNativeDeathFrame": first_death,
        "firstNativeDeathGameSeconds": frames[first_death]["gameSeconds"],
        "postDeathBody": [{"frame": index, "active": frames[index]["active"], "visible": frames[index]["isVisible"],
                            "enabled": frames[index]["enabled"],
                            "dynamicBodies": sum(body.get("isKinematic") is False for body in ((frames[index].get("ragdoll") or {}).get("rigidbodies") or []))}
                           for index in (first_death, min(first_death + 1, 119), 60, 119)],
    })
except Exception as error:
    record.update(status="STOPPED", error=repr(error))
    raise
finally:
    summary.write_text(json.dumps(record, indent=2) + "\n")
    stop_owned_game(process, handle)

print(json.dumps({"summary": str(summary), "status": record["status"], "collects": len(record.get("collects", []))}, indent=2))
