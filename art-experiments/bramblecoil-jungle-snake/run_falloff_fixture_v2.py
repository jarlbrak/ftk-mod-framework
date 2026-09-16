#!/usr/bin/env python3
"""Record one pinned normal-death fixture for each Bramblecoil fall-off policy.

``--variant opted`` uses the explicit ``preserve-custom-body`` row;
``--variant native-control`` uses the otherwise identical, policy-omitted row.
Both runs use native ``KillSingle`` once and are fixtures, never ordinary
lethal-damage evidence.  The output preserves raw per-frame body and ragdoll
telemetry for the paired verifier.
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
import time


ROOT = next(path for path in Path(__file__).resolve().parents if (path / "FTKModFramework").is_dir())
GAME = ROOT / "scratch/mirewarden-game"
OUT = GAME / "model-test-output"
PORT = 8788
TARGET_RENDERER = "enJungleSnakeC"
EXPECTED_MESH = "ftkmf_glb_bramblecoil-jungle-snake.glb"
POLICY = "preserve-custom-body"
VARIANTS = {
    "opted": ("ftkmf_modeltest_bramblecoil_jungle_snake", POLICY),
    "native-control": ("ftkmf_modeltest_bramblecoil_jungle_snake_native_falloff_control", None),
}

parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument("--variant", choices=tuple(VARIANTS), required=True)
parser.add_argument("--deployment-receipt", type=Path, required=True)
ARGS = parser.parse_args()
PROFILE, EXPECTED_POLICY = VARIANTS[ARGS.variant]
RECEIPT = ARGS.deployment_receipt.resolve()


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


def state() -> dict:
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
            if fresh:
                if registration.get("status") != "registered" or registration.get("error") is not None:
                    raise RuntimeError("Fresh content registration did not succeed: " + repr(registration.get("error")))
                entries = {row.get("key"): row for row in registration.get("registered", [])}
                if entries.get("ftkmf_modeltest_bramblecoil_jungle_snake", {}).get("fallOffPolicy") != POLICY:
                    raise RuntimeError("Fresh registration does not report the opted Bramblecoil policy")
                target = entries.get(PROFILE)
                if target is None or target.get("fallOffPolicy") != EXPECTED_POLICY:
                    raise RuntimeError("Fresh registration does not match the requested policy variant")
                return session, registration
        except (FileNotFoundError, KeyError, ValueError):
            pass
        time.sleep(1)
    raise TimeoutError("fresh bridge registration with both Bramblecoil policy states was not observed")


def wait_menu(previous_session: str | None, prior_mtime: float) -> tuple[str, dict]:
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


def newest_json(after: float) -> Path:
    candidates = [path for path in OUT.glob("*.json") if path.stat().st_mtime >= after]
    if not candidates:
        raise FileNotFoundError("No raw capture JSON created for the requested operation")
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
launch_log = GAME / f"launch-bramblecoil-falloff-{ARGS.variant}-v2-{timestamp}.log"
player_log = GAME / f"player-bramblecoil-falloff-{ARGS.variant}-v2-{timestamp}.log"
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
    "purpose": "Fresh explicit KillSingle fixture for a Bramblecoil fall-off policy comparison; not ordinary damage, ordinary lethal, corpse-quality, loot, or Ready evidence.",
    "variant": ARGS.variant,
    "profile": PROFILE,
    "rendererPath": TARGET_RENDERER,
    "expectedPolicy": EXPECTED_POLICY,
    "deployment": deployment,
    "launchLog": str(launch_log.relative_to(ROOT)),
    "playerLog": str(player_log.relative_to(ROOT)),
}
summary = ROOT / "scratch" / f"bramblecoil-falloff-{ARGS.variant}-v2-{timestamp}.json"
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
    fixture_started = time.time()
    fixture = subprocess.run(
        ["python3", "tools/ai-model-pipeline/runtime-test/record_case.py", "--root", str(GAME), "--port", str(PORT),
         "--enemy", PROFILE, "--renderer-path", TARGET_RENDERER, "--profile-sha256", deployment["catalog"]["sha256"], "--action", "kill-fixture"],
        cwd=ROOT, text=True, capture_output=True, timeout=300, check=False,
    )
    fixture_result = newest_result(fixture_started)
    fixture_doc = read(fixture_result)
    action = fixture_doc.get("actionResult", {}).get("result", {})
    if fixture.returncode != 0 or fixture_doc.get("ok") is not True or action.get("committed") != "KillSingle":
        raise RuntimeError("fixture did not report exactly one accepted KillSingle action")
    capture_path = Path(fixture_doc["capture"]["result"])
    raw = read(capture_path)
    frames = raw.get("frames") or []
    if raw.get("ok") is not True or len(frames) != 120 or raw.get("session") != session:
        raise RuntimeError("fixture capture is incomplete or belongs to another session")
    expected_owner = frames[0].get("ownerInstanceId")
    expected_instance = frames[0].get("instanceId")
    expected_bones = frames[0].get("boneSignature")
    for frame in frames:
        if (frame.get("mesh") != EXPECTED_MESH
                or frame.get("ownerInstanceId") != expected_owner
                or frame.get("instanceId") != expected_instance
                or frame.get("boneSignature") != expected_bones):
            raise RuntimeError("fixture capture lost exact custom renderer identity")
    death_frames = [index for index, frame in enumerate(frames) if death_state(frame)]
    if not death_frames:
        raise RuntimeError("fixture capture never reached a native Snake_Death state")
    first_death = death_frames[0]
    record.update({
        "status": "RECORDED_FALLOFF_FIXTURE_PENDING_VERIFICATION",
        "session": session,
        "registration": pin(registration_path),
        "registrationEntry": next(row for row in registration["registered"] if row.get("key") == PROFILE),
        "stage": pin(stage_result),
        "fixture": pin(fixture_result),
        "fixtureAction": action,
        "capture": pin(capture_path),
        "firstNativeDeathFrame": first_death,
        "firstNativeDeathGameSeconds": frames[first_death]["gameSeconds"],
        "bodyPostDeath": [{"frame": index, "active": frame["active"], "visible": frame["isVisible"], "enabled": frame["enabled"],
                           "ragdollBodies": (frame.get("ragdoll") or {}).get("rigidbodyCount"),
                           "dynamicBodies": sum(1 for body in (frame.get("ragdoll") or {}).get("rigidbodies", []) if body.get("isKinematic") is False)}
                          for index, frame in enumerate(frames) if index in (first_death, min(first_death + 1, 119), min(first_death + 2, 119), 30, 60, 119)],
    })
except Exception as error:
    record.update(status="STOPPED", error=repr(error))
    raise
finally:
    summary.write_text(json.dumps(record, indent=2) + "\n")
    stop_owned_game(process, handle)

print(json.dumps({"summary": str(summary), "capture": record.get("capture"), "status": record["status"]}, indent=2))
