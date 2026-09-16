#!/usr/bin/env python3
"""Run one fresh, explicitly labelled Bramblecoil KillSingle fixture.

This intentionally does not represent ordinary damage or an ordinary lethal
combat result. It starts a fresh isolated process, stages the exact custom
profile, records one native KillSingle action, and preserves whatever complete
or renderer-destroyed capture the bridge reports.
"""
from __future__ import annotations

import hashlib
import json
import os
from datetime import datetime
from pathlib import Path
import socket
import subprocess
import time


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
GAME = ROOT / "scratch/mirewarden-game"
OUT = GAME / "model-test-output"
PORT = 8788
PROFILE = "ftkmf_modeltest_bramblecoil_jungle_snake"
RENDERER = "enJungleSnakeC"


def sha(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def read(path: Path) -> dict:
    return json.loads(path.read_text())


def pin(path: Path) -> dict:
    return {"path": str(path.relative_to(ROOT)), "sha256": sha(path), "bytes": path.stat().st_size}


def active_game_pids() -> list[int]:
    needle = str(GAME / "FTK.app/Contents/MacOS/FTK")
    result = subprocess.run(["ps", "-ax", "-o", "pid=,command="], check=True, text=True, capture_output=True)
    return [int(fields[0]) for line in result.stdout.splitlines() if len(fields := line.strip().split(maxsplit=1)) == 2 and needle in fields[1]]


def assert_fresh_launch_environment() -> None:
    pids = active_game_pids()
    assert not pids, "An existing FTK process owns the isolated root; refusing to attach or stop it: " + ", ".join(map(str, pids))
    try:
        connection = socket.create_connection(("127.0.0.1", PORT), timeout=0.2)
    except OSError:
        return
    connection.close()
    raise RuntimeError(f"Bridge port {PORT} is already in use; refusing to attach to an unknown session")


def state() -> dict:
    import urllib.request
    with urllib.request.urlopen(f"http://127.0.0.1:{PORT}/state", timeout=1) as response:
        return json.load(response)


def wait_registration(previous_session: str | None, prior_mtime: float) -> str:
    for _ in range(60):
        try:
            session_path = GAME / "model-test-session.json"
            registration = read(GAME / "model-test-registration.json")
            session = read(session_path)["session"]
            updated = datetime.fromisoformat(registration["updatedUtc"].replace("Z", "+00:00")).timestamp()
            if session != previous_session and session_path.stat().st_mtime > prior_mtime and session_path.stat().st_mtime <= updated <= time.time():
                return session
        except (FileNotFoundError, KeyError, ValueError):
            pass
        time.sleep(1)
    raise TimeoutError("fresh bridge registration with a new helper session was not observed")


def wait_menu(previous_session: str | None, prior_mtime: float) -> str:
    for _ in range(60):
        try:
            if state().get("phase") == "menu":
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


assert_fresh_launch_environment()
session_path = GAME / "model-test-session.json"
previous_session = read(session_path).get("session")
prior_mtime = session_path.stat().st_mtime
timestamp = time.strftime("%Y%m%d-%H%M%S")
launch_log = GAME / f"launch-bramblecoil-death-fixture-v1-{timestamp}.log"
player_log = GAME / f"player-bramblecoil-death-fixture-v1-{timestamp}.log"
handle = launch_log.open("w")
process = subprocess.Popen(
    ["./run_bepinex.sh", "FTK.app", "-screen-width", "1280", "-screen-height", "832", "-screen-fullscreen", "0", "-logFile", str(player_log)],
    cwd=GAME,
    env={
        **os.environ,
        "FTK_MODEL_TEST": "1",
        "FTK_MODEL_TEST_ROOT": str(GAME),
        "FTK_AGENT_BRIDGE": "1",
        "FTK_AGENT_BRIDGE_PORT": str(PORT),
        "FTK_MIREWARDEN_BODY": "1",
    },
    stdout=handle,
    stderr=subprocess.STDOUT,
    start_new_session=True,
)

record = {
    "status": "IN_PROGRESS",
    "purpose": "Fresh explicit KillSingle fixture for Jungle C normal-death/fall-off review; not ordinary damage or ordinary lethal validation.",
    "profile": PROFILE,
    "rendererPath": RENDERER,
    "catalog": pin(GAME / "model-test-profiles.json"),
    "launchLog": str(launch_log.relative_to(ROOT)),
    "playerLog": str(player_log.relative_to(ROOT)),
}
summary = ROOT / "scratch" / f"bramblecoil-death-fixture-v1-{timestamp}.json"
try:
    session = wait_menu(previous_session, prior_mtime)
    stage_started = time.time()
    stage = subprocess.run(
        ["python3", "tools/ai-model-pipeline/runtime-test/run_case.py", "--root", str(GAME), "--port", str(PORT), "--enemy", PROFILE, "new-run"],
        cwd=ROOT, text=True, capture_output=True, timeout=240, check=False,
    )
    if stage.returncode != 0:
        raise RuntimeError("new-run failed: " + (stage.stderr[-2000:] or stage.stdout[-2000:]))
    stage_result = newest_result(stage_started)
    fixture_started = time.time()
    fixture = subprocess.run(
        [
            "python3", "tools/ai-model-pipeline/runtime-test/record_case.py", "--root", str(GAME), "--port", str(PORT),
            "--enemy", PROFILE, "--renderer-path", RENDERER,
            "--profile-sha256", record["catalog"]["sha256"], "--action", "kill-fixture",
        ],
        cwd=ROOT, text=True, capture_output=True, timeout=300, check=False,
    )
    fixture_result = newest_result(fixture_started)
    fixture_doc = read(fixture_result)
    action = fixture_doc.get("actionResult", {}).get("result", {})
    if action.get("committed") != "KillSingle":
        raise RuntimeError("fixture did not report an accepted KillSingle action")
    record.update({
        "status": "RECORDED_EXPLICIT_FIXTURE_PENDING_REVIEW",
        "session": session,
        "stage": pin(stage_result),
        "fixture": pin(fixture_result),
        "fixtureExitCode": fixture.returncode,
        "fixtureAction": action,
        "fixtureErrors": fixture_doc.get("errors"),
        "capture": fixture_doc.get("capture"),
    })
except Exception as error:
    record.update(status="STOPPED", error=repr(error))
    raise
finally:
    summary.write_text(json.dumps(record, indent=2) + "\n")
    stop_owned_game(process, handle)

print(json.dumps({"summary": str(summary), "fixture": record["fixture"], "fixtureExitCode": record["fixtureExitCode"]}, indent=2))
