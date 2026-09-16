#!/usr/bin/env python3
"""Record the real native DeathLight trigger path for the Bramblecoil body.

This is a bounded, disposable bridge fixture.  It invokes
``EnemyDummy.PlayAnim(DeathLight)`` through ``combat-trigger-capture`` after
the bridge has verified a sole, alive, idle native enemy.  It is useful for
the CEL trigger branch and its animation event; it is neither ordinary lethal
damage nor combat/loot/Ready progression evidence.
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
PROFILE = "ftkmf_modeltest_bramblecoil_jungle_snake"
TARGET_RENDERER = "enJungleSnakeC"
EXPECTED_MESH = "ftkmf_glb_bramblecoil-jungle-snake.glb"
EXPECTED_POLICY = "preserve-custom-body"
parser = argparse.ArgumentParser(description=__doc__)
parser.add_argument(
    "--deployment-receipt",
    type=Path,
    default=ROOT / "scratch/snake-policy-and-desert-417/receipt.json",
    help="Pinned isolated-game deployment receipt. Defaults to the original policy build.",
)
ARGS = parser.parse_args()
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
    expected.update({name: value["sha256"] for name, value in receipt["newPluginDlls"].items()})
    actual_paths = {
        "model-test-profiles.json": GAME / "model-test-profiles.json",
        "FTKModFramework.dll": GAME / "BepInEx/plugins/FTKModFramework.dll",
        "FtkRuntimeModelTest.dll": GAME / "BepInEx/plugins/FtkRuntimeModelTest.dll",
        "FtkRuntimeModelTestContent.dll": GAME / "BepInEx/plugins/FtkRuntimeModelTestContent.dll",
    }
    for name, path in actual_paths.items():
        if name not in expected or not path.is_file() or sha(path) != expected[name]:
            raise RuntimeError(f"Current isolated deployment does not match {RECEIPT.name}: {name}")
    profile = next((p for p in read(actual_paths["model-test-profiles.json"])["profiles"] if p["key"] == PROFILE), None)
    if profile is None or profile.get("fallOffPolicy") != EXPECTED_POLICY:
        raise RuntimeError("Pinned Bramblecoil catalog row does not opt into preserve-custom-body")
    return {"receipt": pin(RECEIPT), "catalog": pin(actual_paths["model-test-profiles.json"]),
            "plugins": {name: pin(path) for name, path in actual_paths.items() if name != "model-test-profiles.json"}}


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
                entry = next((row for row in registration.get("registered", []) if row.get("key") == PROFILE), None)
                if entry is None or entry.get("fallOffPolicy") != EXPECTED_POLICY:
                    raise RuntimeError("Fresh registration did not report Bramblecoil preserve-custom-body policy")
                return session, registration
        except (FileNotFoundError, KeyError, ValueError):
            pass
        time.sleep(1)
    raise TimeoutError("fresh bridge registration with the Bramblecoil policy was not observed")


def wait_menu(previous_session: str | None, prior_mtime: float) -> tuple[str, dict]:
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
    payload_path = GAME / f"bramblecoil-deathlight-trigger-{label}.json"
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


def verify_trigger_capture(capture: dict, renderer: dict) -> dict:
    if capture.get("provenance") != "native-combat-trigger:DeathLight" or capture.get("fixedStep") is not True:
        raise RuntimeError("Capture did not identify the native DeathLight trigger fixture")
    trigger = capture.get("combatTrigger") or {}
    if trigger.get("issuedMethod") != "EnemyDummy.PlayAnim" or trigger.get("postIssueLastTrigger") != "DeathLight":
        raise RuntimeError("Native trigger fixture did not establish CEL DeathLight")
    gate = trigger.get("preIssueGate") or {}
    target = gate.get("target") or {}
    player_turn = gate.get("playerTurn") or {}
    if target.get("isAttacking") is not False or target.get("baseLayerIdle") is not True or target.get("baseLayerTransition") is not False:
        raise RuntimeError("Native trigger fixture was not issued from a settled non-attacking target")
    if player_turn.get("actionAnimationPlayed") is not False or player_turn.get("fsmState") != "Wait For Stance":
        raise RuntimeError("Native trigger fixture was not issued from a stable player turn")
    frames = capture.get("frames") or []
    if len(frames) != 24:
        raise RuntimeError("Expected a bounded 24-frame, two-second trigger capture")
    unexpected = [index for index, frame in enumerate(frames) if frame.get("lastCombatTrigger") != "DeathLight"
                  or frame.get("mesh") != EXPECTED_MESH or frame.get("instanceId") != renderer["instanceId"]
                  or frame.get("ownerInstanceId") != renderer["ownerInstanceId"]]
    if unexpected:
        raise RuntimeError("Trigger capture lost CEL DeathLight or exact custom renderer at frames: " + repr(unexpected[:8]))
    inactive = [index for index, frame in enumerate(frames) if not frame.get("active") or not frame.get("isVisible") or not frame.get("enabled")]
    if inactive:
        raise RuntimeError("Custom body became inactive/invisible during semantic DeathLight at frames: " + repr(inactive[:8]))
    return {
        "frames": len(frames),
        "firstGameSeconds": frames[0]["gameSeconds"],
        "lastGameSeconds": frames[-1]["gameSeconds"],
        "allFramesExactCustomBody": True,
        "allFramesDeathLight": True,
        "allFramesVisible": True,
    }


assert_fresh_launch_environment()
deployment = assert_deployment()
session_path = GAME / "model-test-session.json"
previous_session = read(session_path).get("session")
prior_mtime = session_path.stat().st_mtime
timestamp = time.strftime("%Y%m%d-%H%M%S")
launch_log = GAME / f"launch-bramblecoil-deathlight-trigger-v1-{timestamp}.log"
player_log = GAME / f"player-bramblecoil-deathlight-trigger-v1-{timestamp}.log"
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
    "purpose": "Fresh semantic EnemyDummy.PlayAnim(DeathLight) fixture against the opt-in Bramblecoil body; neither ordinary lethal damage nor combat/loot/Ready evidence.",
    "profile": PROFILE,
    "rendererPath": TARGET_RENDERER,
    "expectedPolicy": EXPECTED_POLICY,
    "deployment": deployment,
    "launchLog": str(launch_log.relative_to(ROOT)),
    "playerLog": str(player_log.relative_to(ROOT)),
}
summary = ROOT / "scratch" / f"bramblecoil-deathlight-trigger-v1-{timestamp}.json"
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
    stage_result = newest_case_result(stage_started)
    inventory_path, inventory = run_command("inventory", {"scope": "enemies"}, "inventory")
    matches = [renderer for renderer in inventory["renderers"] if renderer["celRelativeRendererPath"] == TARGET_RENDERER and renderer["mesh"] == EXPECTED_MESH]
    if len(matches) != 1:
        raise RuntimeError("Expected exactly one current Bramblecoil renderer in fresh inventory")
    renderer = matches[0]
    payload = {
        "scope": "enemies",
        "ownerInstanceId": renderer["ownerInstanceId"],
        "rendererId": renderer["instanceId"],
        "rendererPath": renderer["rendererPath"],
        "expectedMesh": renderer["mesh"],
        "boneSignature": renderer["boneSignature"],
        "seconds": 2,
        "fps": 12,
        "fixedStep": True,
    }
    capture_path, capture = run_command("combat-trigger-capture", payload, "capture")
    capture_verdict = verify_trigger_capture(capture, renderer)
    material_path, material = run_command("material-state", {
        "scope": "enemies", "ownerInstanceId": renderer["ownerInstanceId"], "rendererId": renderer["instanceId"],
        "rendererPath": renderer["rendererPath"], "expectedMesh": renderer["mesh"], "boneSignature": renderer["boneSignature"],
    }, "material")
    if material.get("rendererActive") is not True or material.get("rendererEnabled") is not True:
        raise RuntimeError("Custom body was not active after semantic DeathLight")
    slot_names = [slot.get("name") for slot in material.get("slots", [])]
    if any(name == "matCutOutInvisible" for name in slot_names):
        raise RuntimeError("Semantic DeathLight left the custom body on native cutout-invisible material")
    record.update({
        "status": "PASS_SEMANTIC_DEATHLIGHT_TRIGGER_FIXTURE",
        "session": session,
        "registration": pin(registration_path),
        "registrationEntry": next(row for row in registration["registered"] if row.get("key") == PROFILE),
        "stage": pin(stage_result),
        "inventory": pin(inventory_path),
        "renderer": renderer,
        "payload": payload,
        "capture": pin(capture_path),
        "captureVerdict": capture_verdict,
        "materialState": pin(material_path),
        "materialSlotNamesAfter": slot_names,
    })
except Exception as error:
    record.update(status="STOPPED", error=repr(error))
    raise
finally:
    summary.write_text(json.dumps(record, indent=2) + "\n")
    stop_owned_game(process, handle)

print(json.dumps({"summary": str(summary), "capture": record["capture"], "status": record["status"]}, indent=2))
