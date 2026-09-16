#!/usr/bin/env python3
"""Run one owned isolated Kraken HUD portrait capture after a framework change."""

from __future__ import annotations

import argparse
import hashlib
import json
from pathlib import Path
import subprocess
import time
import uuid

import run_execution_queue_route as route_runner


ENEMY = "ftkmf_modeltest_gloamfin_kraken_legacy"


def sha256(path: Path) -> str:
    return hashlib.sha256(path.read_bytes()).hexdigest()


def issue(game: Path, session: str, operation: str, fields: dict | None = None,
          timeout: float = 60) -> tuple[str, dict]:
    command_id = uuid.uuid4().hex
    command = dict(fields or {})
    command.update(id=command_id, session=session, op=operation)
    temporary = game / f"model-test-{command_id}.tmp"
    temporary.write_text(json.dumps(command))
    temporary.replace(game / "model-test-command.json")
    result_path = game / "model-test-output" / f"{command_id}.json"
    deadline = time.monotonic() + timeout
    while time.monotonic() < deadline:
        if result_path.is_file():
            result = json.loads(result_path.read_text())
            if not result.get("ok"):
                raise RuntimeError(f"{operation} failed: {result}")
            return command_id, result
        time.sleep(.2)
    raise TimeoutError(f"{operation} result was not observed: {result_path}")


def main() -> int:
    parser = argparse.ArgumentParser(description=__doc__)
    parser.add_argument("--game-root", required=True, type=Path)
    parser.add_argument("--framework-sha256", required=True)
    parser.add_argument("--output", required=True, type=Path)
    parser.add_argument("--port", type=int, default=8794)
    args = parser.parse_args()

    root = Path(__file__).resolve().parents[3]
    game = args.game_root.resolve()
    output = args.output.resolve()
    if game.parent != root / "scratch" or output.parent != root / "scratch" or output.exists():
        raise ValueError("game root and new output must be direct children of repository scratch/")
    framework = game / "BepInEx/plugins/FTKModFramework.dll"
    if sha256(framework) != args.framework_sha256:
        raise ValueError("deployed framework hash differs from the requested portrait candidate")
    route_runner.assert_fresh_launch_environment(game, args.port)
    previous_session, previous_mtime = route_runner.read_session(game)
    process = None
    handle = None
    record: dict = {
        "schema": "ftkmf.kraken-portrait-followup.v1",
        "status": "issued",
        "enemy": ENEMY,
        "framework": {"path": str(framework.relative_to(game)), "sha256": args.framework_sha256},
    }
    try:
        process, handle = route_runner.launch_owned_game(game, args.port, output, {})
        route_runner.wait_menu(game, args.port, previous_session, previous_mtime)
        session = json.loads((game / "model-test-session.json").read_text())["session"]
        record["session"] = session
        arm_id, arm = issue(game, session, "portrait-watch", {"enemy": ENEMY})
        record["arm"] = arm
        command = [
            "python3", "tools/ai-model-pipeline/runtime-test/run_case.py",
            "--root", str(game), "--port", str(args.port), "--enemy", ENEMY,
            "--skip-fortify", "new-run",
        ]
        completed = subprocess.run(command, cwd=root, text=True, capture_output=True, timeout=240)
        record["stage"] = {"returncode": completed.returncode, "stdout": completed.stdout, "stderr": completed.stderr}
        if completed.returncode:
            raise RuntimeError("run_case failed: " + completed.stderr[-2000:])
        _, state = issue(game, session, "portrait-watch-state")
        record["trace"] = state
        matches = [item for item in state["records"]
                   if item.get("identityResolution") == "verified_live_enemy_dummy"
                   and item.get("telemetryComplete") is True
                   and item.get("nativeSnapshotFinalized") is True
                   and item.get("nativeSnapshotException") is None]
        if len(matches) != 1:
            raise RuntimeError(f"expected one finalized live HUD portrait trace, found {len(matches)}")
        trace = matches[0]
        requested = trace["textureRequested"]
        target = trace["target"]
        source = trace["source"]
        if requested != trace["textureCurrent"] or requested["width"] != 204 or requested["height"] != 172:
            raise RuntimeError("native HUD texture identity/dimensions changed")
        if target.get("traversalOrRenderersTruncated") or source.get("traversalOrRenderersTruncated"):
            raise RuntimeError("portrait transform/renderer telemetry truncated")
        dummy_id = trace.get("enemyDummyInstanceId")
        fid = trace.get("fid")
        if type(dummy_id) is not int or not isinstance(fid, dict):
            raise RuntimeError("exact live Kraken trace identity unavailable")
        capture_id, capture = issue(game, session, "portrait-texture-capture", {
            "enemy": ENEMY,
            "enemyDummyInstanceId": dummy_id,
            "photonId": fid["photonId"],
            "turnIndex": fid["turnIndex"],
            "expectedTextureId": requested["instanceId"],
            "traceFrame": trace["frame"],
            "traceArmCommandId": arm_id,
        })
        record["capture"] = capture
        record["captureResultPath"] = str((game / "model-test-output" / f"{capture_id}.json").relative_to(root))
        _, stopped = issue(game, session, "portrait-watch-stop")
        record["stop"] = stopped
        record["status"] = "needs_manual_portrait_review"
        record["limits"] = [
            "The native portrait texture is a live CPU readback; manual pixel review remains required.",
            "This follow-up does not replay combat, death, victory, or natural teardown evidence.",
            "Source/live invariance is limited to the passive trace fields and framework ownership tests.",
        ]
    except Exception as error:
        record["status"] = "failed"
        record["error"] = repr(error)
        raise
    finally:
        output.write_text(json.dumps(record, indent=2) + "\n")
        route_runner.stop_owned_game(process, handle)
    print(output)
    print(json.dumps({"status": record["status"], "session": record.get("session"),
                      "png": record.get("capture", {}).get("png")}, indent=2))
    return 0


if __name__ == "__main__":
    raise SystemExit(main())
