#!/usr/bin/env python3
"""Record raw Animator.Play playback of Jungle C's DeathLight state.

This is intentionally distinct from a real CEL CombatTrigger(DeathLight),
normal combat, ordinary lethal damage, and the separate KillSingle fall-off
fixture. The runtime bridge plays the exact `Base Layer.DEATHLIGHT` state but
does not set CharacterEventListener.m_LastTrigger, so the capture proves only
raw-state/clip-event behavior against the injected body.
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
TARGET_RENDERER = "enJungleSnakeC"
STATE = "Base Layer.DEATHLIGHT"


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


def newest_json(after: float) -> Path:
    candidates = [path for path in OUT.glob("*.json") if path.stat().st_mtime >= after]
    if not candidates:
        raise FileNotFoundError("No command result JSON created for the requested operation")
    return max(candidates, key=lambda path: path.stat().st_mtime)


def newest_case_result(after: float) -> Path:
    candidates = [path for path in OUT.rglob("result.json") if path.stat().st_mtime >= after]
    if not candidates:
        raise FileNotFoundError("No case result.json created for the requested operation")
    return max(candidates, key=lambda path: path.stat().st_mtime)


def run_command(op: str, payload: dict, label: str) -> tuple[Path, dict]:
    payload_path = GAME / f"bramblecoil-deathlight-{label}.json"
    payload_path.write_text(json.dumps(payload, indent=2) + "\n")
    started = time.time()
    result = subprocess.run(
        ["python3", "tools/ai-model-pipeline/runtime-test/command.py", "--root", str(GAME), op, "--payload", str(payload_path), "--timeout", "180"],
        cwd=ROOT, text=True, capture_output=True, timeout=210, check=False,
    )
    path = newest_json(started)
    doc = read(path)
    if result.returncode != 0 or doc.get("ok") is not True:
        raise RuntimeError(f"{op} failed: {result.stderr[-2000:] or result.stdout[-2000:] or doc}")
    return path, doc


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
launch_log = GAME / f"launch-bramblecoil-deathlight-v1-{timestamp}.log"
player_log = GAME / f"player-bramblecoil-deathlight-v1-{timestamp}.log"
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
    "purpose": "Fresh raw Animator.Play playback of Base Layer.DEATHLIGHT / Snake_Death against the injected Jungle C body. It does not set the CEL CombatTrigger state, so it is neither genuine DeathLight-branch, normal-combat, nor ordinary-lethal evidence.",
    "profile": PROFILE,
    "rendererPath": TARGET_RENDERER,
    "state": STATE,
    "catalog": pin(GAME / "model-test-profiles.json"),
    "launchLog": str(launch_log.relative_to(ROOT)),
    "playerLog": str(player_log.relative_to(ROOT)),
}
summary = ROOT / "scratch" / f"bramblecoil-deathlight-playback-v1-{timestamp}.json"
try:
    session = wait_menu(previous_session, prior_mtime)
    stage_started = time.time()
    stage = subprocess.run(
        ["python3", "tools/ai-model-pipeline/runtime-test/run_case.py", "--root", str(GAME), "--port", str(PORT), "--enemy", PROFILE, "new-run"],
        cwd=ROOT, text=True, capture_output=True, timeout=240, check=False,
    )
    if stage.returncode != 0:
        raise RuntimeError("new-run failed: " + (stage.stderr[-2000:] or stage.stdout[-2000:]))
    stage_result = newest_case_result(stage_started)
    inventory_path, inventory = run_command("inventory", {"scope": "enemies"}, "inventory")
    matches = [renderer for renderer in inventory["renderers"] if renderer["celRelativeRendererPath"] == TARGET_RENDERER and renderer["mesh"] == "ftkmf_glb_bramblecoil-jungle-snake.glb"]
    if len(matches) != 1:
        raise RuntimeError("Expected exactly one current Bramblecoil renderer in fresh inventory")
    renderer = matches[0]
    playback_payload = {
        "scope": "enemies",
        "ownerInstanceId": renderer["ownerInstanceId"],
        "rendererId": renderer["instanceId"],
        "rendererPath": renderer["rendererPath"],
        "expectedMesh": renderer["mesh"],
        "boneSignature": renderer["boneSignature"],
        "state": STATE,
        "layer": 0,
        "seconds": 10,
        "fps": 12,
        "fixedStep": True,
        "materialObservation": True,
    }
    playback_path, playback = run_command("play", playback_payload, "play")
    if playback.get("provenance") != "native-state-playback" or len(playback.get("frames", [])) != 120:
        raise RuntimeError("DeathLight playback did not preserve the requested native-state 120-frame capture")
    record.update({
        "status": "RECORDED_NATIVE_STATE_PLAYBACK_PENDING_REVIEW",
        "session": session,
        "stage": pin(stage_result),
        "inventory": pin(inventory_path),
        "playback": pin(playback_path),
        "renderer": renderer,
        "payload": playback_payload,
    })
except Exception as error:
    record.update(status="STOPPED", error=repr(error))
    raise
finally:
    summary.write_text(json.dumps(record, indent=2) + "\n")
    stop_owned_game(process, handle)

print(json.dumps({"summary": str(summary), "playback": record["playback"]}, indent=2))
