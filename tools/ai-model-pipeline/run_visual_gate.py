#!/usr/bin/env python3
"""Single-command end-to-end visual-verification gate (spec #66; work item #71).

One command runs the WHOLE gate and prints BOTH verdicts side by side, never
reconciled into one number (NFR-6). It ties together the three pieces already in
this directory without re-implementing any of their logic:

  1. OFFLINE PREVIEW (the launch gate): 03b_preview_metrics.py renders the
     candidate .glb through Blender and runs the SHARED visual_gate.analyze() to
     produce preview_verdict.json. Its exit code gates the launch.
  2. GATE: if the preview is FAIL or INCONCLUSIVE, the game is NOT launched and
     NO in-game capture is attempted; the offline result is printed with its
     per-criterion reasons and the run exits non-zero.
  3. IN-GAME (only if the preview PASSED): ensure the FTK_AGENT_BRIDGE=1 game is
     reachable (launch it detached and poll /health unless --assume-bridge), run
     the capture driver (harness/capture_boss_scene.py --mode candidate) to grab
     the LIT boss frame, then run visual_verdict.py over that frame against
     baseline.json to produce the in-game verdict.json. If this run launched the
     game, it is shut down afterward; an externally launched bridge is left up.
  4. PRINT both verdicts side by side: for each of the four mechanical criteria,
     the measured value AND the threshold applied, for the PREVIEW and the
     IN-GAME verdict. A divergence (PREVIEW=pass IN-GAME=fail) is surfaced
     explicitly as a runtime-only shatter, never collapsed into one number.

Deterministic: the same .glb plus the same captured frame yield the same
mechanical verdict (analyze() is pure; this script only orchestrates).

Run it with the venv interpreter (numpy/scipy/Pillow live there); the render and
capture stages are launched as child processes with the SAME sys.executable:

  /Users/tbrack/Documents/Projects/FTK/.venv-3dgen/bin/python \
    tools/ai-model-pipeline/run_visual_gate.py \
    [--glb ai-model-gen/mudwretch_rigged.glb] \
    [--texture ai-model-gen/mudwretch_basecolor.png] \
    [--baseline tools/ai-model-pipeline/baseline.json] \
    [--frame tools/ai-model-pipeline/fixtures/candidate_boss.png] \
    [--out-dir /tmp/ftk_gate] \
    [--assume-bridge]            # use an already-running FTK_AGENT_BRIDGE=1 game
"""

import argparse
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

HERE = os.path.dirname(os.path.abspath(__file__))
REPO_ROOT = os.path.dirname(os.path.dirname(HERE))
HARNESS_DIR = os.path.join(REPO_ROOT, "harness")
sys.path.insert(0, HERE)

import visual_gate  # noqa: E402  (after sys.path setup so the venv run finds it)

# Default candidate asset (the Mudwretch Foreman) and committed fixtures.
DEFAULT_GLB = os.path.join(REPO_ROOT, "ai-model-gen", "mudwretch_rigged.glb")
DEFAULT_TEXTURE = os.path.join(REPO_ROOT, "ai-model-gen", "mudwretch_basecolor.png")
DEFAULT_BASELINE = os.path.join(HERE, "baseline.json")
DEFAULT_FRAME = os.path.join(HERE, "fixtures", "candidate_boss.png")

PREVIEW_SCRIPT = os.path.join(HERE, "03b_preview_metrics.py")
VERDICT_SCRIPT = os.path.join(HERE, "visual_verdict.py")
CAPTURE_SCRIPT = os.path.join(HARNESS_DIR, "capture_boss_scene.py")

# Bridge / launch configuration (mirrors harness/capture_boss_scene.py and README).
BRIDGE_URL = os.environ.get("FTK_BRIDGE_URL", "http://127.0.0.1:8777").rstrip("/")
GAME_DIR = os.path.join(
    os.path.expanduser("~"),
    "Library", "Application Support", "Steam", "steamapps", "common", "For The King",
)
RUN_BEPINEX = os.path.join(GAME_DIR, "run_bepinex.sh")
GAME_APP = os.path.join(GAME_DIR, "FTK.app")
GAME_PROC_PATTERN = "FTK.app/Contents/MacOS"  # for pkill on a self-launched game

HEALTH_TIMEOUT = 3.0          # per /health probe
BRIDGE_UP_TIMEOUT = 120.0     # overall cap waiting for a self-launched bridge
BRIDGE_POLL = 2.0


# --------------------------------------------------------------------------- #
# Small helpers
# --------------------------------------------------------------------------- #

def _read_verdict(path):
    """Load a verdict JSON, or None if it is missing / unreadable."""
    try:
        with open(path, "r") as f:
            return json.load(f)
    except Exception:
        return None


def _bridge_reachable():
    """True if GET BRIDGE_URL/health returns an ok envelope, else False."""
    try:
        req = urllib.request.Request(BRIDGE_URL + "/health", method="GET")
        with urllib.request.urlopen(req, timeout=HEALTH_TIMEOUT) as resp:
            if resp.status != 200:
                return False
            body = resp.read()
    except (urllib.error.URLError, OSError):
        return False
    try:
        j = json.loads(body.decode("utf-8")) if body else {}
    except Exception:
        return False
    return bool(j.get("ok", True)) if isinstance(j, dict) else False


def _crit_line(name, criterion):
    """One side-by-side criterion line: PASS/FAIL, measured value, threshold."""
    if not isinstance(criterion, dict):
        return "    %-10s n/a" % name
    mark = "PASS" if criterion.get("pass") else "FAIL"
    val = criterion.get("value")
    val_s = ("%.4f" % val) if isinstance(val, (int, float)) else str(val)
    return "    %-10s %-4s  value=%-10s  threshold: %s" % (
        name, mark, val_s, criterion.get("threshold"))


def _print_verdict_block(title, verdict, artifacts):
    """Print one verdict block: overall, the four criteria, then the artifacts."""
    print("=" * 78)
    print(title)
    print("=" * 78)
    if verdict is None:
        print("    (no verdict produced)")
    else:
        overall = verdict.get("verdict", "?").upper()
        reason = verdict.get("inconclusive_reason")
        suffix = (" (%s)" % reason) if reason else ""
        print("  VERDICT: %s%s" % (overall, suffix))
        criteria = verdict.get("criteria") or {}
        for name in ("connected", "upright", "centered", "scaled"):
            print(_crit_line(name, criteria.get(name)))
    if artifacts:
        print("  artifacts:")
        for label, path in artifacts:
            print("    %-18s %s" % (label + ":", path if path else "(none)"))


# --------------------------------------------------------------------------- #
# Stage 1: OFFLINE PREVIEW (the launch gate)
# --------------------------------------------------------------------------- #

def run_offline_preview(glb, texture, out_dir):
    """Run 03b_preview_metrics.py as a child process. Returns (rc, verdict_dict).

    The preview owns the Blender render and the shared analyze(); we only invoke
    it and read back its preview_verdict.json (no duplication of analyze()).
    Exit codes: 0 pass / 1 fail / 2 arg-or-render-error / 3 inconclusive.

    IMPORTANT: the preview is run with NO in-game baseline (analyze(baseline=None),
    its offline self-referential defaults). The offline Blender render has its own
    camera/framing/resolution, so anchoring its centered/scaled to the in-game
    baseline.json centroid/height would be invalid (NFR-5: preview-vs-in-game
    agreement is on the NORMALIZED criteria, never raw pixels). The in-game
    baseline.json is used ONLY for the in-game verdict (run_ingame_verdict)."""
    verdict_path = os.path.join(out_dir, "preview_verdict.json")
    cmd = [
        sys.executable, PREVIEW_SCRIPT,
        "--glb", glb,
        "--out-dir", out_dir,
    ]
    if texture:
        cmd += ["--texture", texture]
    print("OFFLINE PREVIEW: " + " ".join(cmd))
    rc = subprocess.call(cmd)
    return rc, _read_verdict(verdict_path)


# --------------------------------------------------------------------------- #
# Stage 3: IN-GAME bridge handling + capture + verdict
# --------------------------------------------------------------------------- #

def launch_bridge_game():
    """Launch the FTK_AGENT_BRIDGE=1 game detached. Returns the Popen handle.

    run_bepinex.sh REQUIRES the .app path as its first arg; the bridge starts
    only when FTK_AGENT_BRIDGE=1 is set in the environment."""
    env = dict(os.environ)
    env["FTK_AGENT_BRIDGE"] = "1"
    cmd = ["bash", RUN_BEPINEX, GAME_APP]
    print("LAUNCH: FTK_AGENT_BRIDGE=1 " + " ".join(cmd))
    return subprocess.Popen(
        cmd, env=env,
        stdout=subprocess.DEVNULL, stderr=subprocess.DEVNULL,
        start_new_session=True,
    )


def wait_for_bridge(timeout_s):
    """Poll BRIDGE_URL/health until reachable or timeout. Returns True/False."""
    deadline = time.time() + timeout_s
    while time.time() < deadline:
        if _bridge_reachable():
            return True
        time.sleep(BRIDGE_POLL)
    return _bridge_reachable()


def shutdown_self_launched_game():
    """Kill a game this script launched (best-effort; never raises)."""
    print("SHUTDOWN: pkill -f %r (self-launched game)" % GAME_PROC_PATTERN)
    try:
        subprocess.call(["pkill", "-f", GAME_PROC_PATTERN])
    except Exception as e:
        print("  (pkill failed: %s)" % e)


def run_capture(out_dir):
    """Run the candidate capture driver as a child. Returns (rc, stdout_text).

    The driver writes fixtures/candidate_boss.png and prints CAPTURED ... on
    success or INCONCLUSIVE: <step> on failure (exit non-zero either way)."""
    cmd = [sys.executable, CAPTURE_SCRIPT, "--mode", "candidate"]
    print("CAPTURE: " + " ".join(cmd))
    proc = subprocess.run(cmd, stdout=subprocess.PIPE, stderr=subprocess.STDOUT)
    text = proc.stdout.decode("utf-8", "replace")
    sys.stdout.write(text)
    return proc.returncode, text


def run_ingame_verdict(frame, baseline, reference_render, out_dir):
    """Run visual_verdict.py over the captured frame. Returns (rc, verdict_dict).

    visual_verdict.py owns the in-game analyze() + annotate + crop; we invoke it
    and read back verdict.json (no duplication)."""
    verdict_path = os.path.join(out_dir, "verdict.json")
    cmd = [
        sys.executable, VERDICT_SCRIPT,
        "--frame", frame,
        "--baseline", baseline,
        "--out-dir", out_dir,
    ]
    if reference_render:
        cmd += ["--reference-render", reference_render]
    print("IN-GAME VERDICT: " + " ".join(cmd))
    rc = subprocess.call(cmd)
    return rc, _read_verdict(verdict_path)


# --------------------------------------------------------------------------- #
# Side-by-side report (never reconciles the two verdicts; NFR-6)
# --------------------------------------------------------------------------- #

def print_side_by_side(preview_verdict, preview_artifacts,
                       ingame_verdict, ingame_artifacts):
    """Print the PREVIEW and IN-GAME verdicts side by side and flag divergence."""
    print("\n")
    print("#" * 78)
    print("# VISUAL GATE: OFFLINE PREVIEW vs IN-GAME (never reconciled; NFR-6)")
    print("#" * 78)
    _print_verdict_block("OFFLINE PREVIEW VERDICT (the launch gate)",
                         preview_verdict, preview_artifacts)
    print("")
    _print_verdict_block("IN-GAME VERDICT (full-frame capture)",
                         ingame_verdict, ingame_artifacts)

    pv = (preview_verdict or {}).get("verdict")
    iv = (ingame_verdict or {}).get("verdict")
    print("\n" + "-" * 78)
    print("DIVERGENCE CHECK (the two verdicts stand on their own):")
    print("    PREVIEW=%s   IN-GAME=%s" % (pv or "n/a", iv or "n/a"))
    if pv == "pass" and iv == "fail":
        failed = [k for k, d in ((ingame_verdict or {}).get("criteria") or {}).items()
                  if isinstance(d, dict) and not d.get("pass")]
        print("    PREVIEW=pass IN-GAME=fail -> RUNTIME-ONLY SHATTER "
              "(in-game criteria failing: %s)." % (", ".join(failed) or "?"))
        print("    The asset is geometrically sound offline but the runtime "
              "reconstruction breaks it. This is the exact failure the gate exists "
              "to catch; crops used to hide it.")
    elif pv == iv and pv is not None:
        print("    Verdicts AGREE (%s). No offline/in-game divergence." % pv)
    elif iv is None:
        print("    No in-game verdict (capture not run or inconclusive); the two "
              "are reported separately, not merged.")
    else:
        print("    Verdicts DIFFER; reported separately, never collapsed into one "
              "number.")
    print("-" * 78)


# --------------------------------------------------------------------------- #
# Entry point
# --------------------------------------------------------------------------- #

def parse_args(argv):
    p = argparse.ArgumentParser(
        description="Single-command end-to-end visual gate: offline preview "
                    "(the launch gate) then in-game capture + verdict, with both "
                    "verdicts printed side by side and never reconciled.")
    p.add_argument("--glb", default=DEFAULT_GLB,
                   help="candidate .glb to preview-render and analyze "
                        "(default: ai-model-gen/mudwretch_rigged.glb)")
    p.add_argument("--texture", default=DEFAULT_TEXTURE,
                   help="optional base-color PNG recorded with the preview "
                        "(default: ai-model-gen/mudwretch_basecolor.png)")
    p.add_argument("--baseline", default=DEFAULT_BASELINE,
                   help="stock-troll baseline.json for the baseline-anchored "
                        "criteria (default: tools/ai-model-pipeline/baseline.json)")
    p.add_argument("--frame", default=DEFAULT_FRAME,
                   help="captured in-game frame path the driver writes and the "
                        "in-game verdict reads "
                        "(default: fixtures/candidate_boss.png)")
    p.add_argument("--out-dir", default="/tmp/ftk_gate",
                   help="directory for preview_render.png, preview_verdict.json, "
                        "verdict.json, the annotated frame, and the crop")
    p.add_argument("--assume-bridge", action="store_true",
                   help="use an already-running FTK_AGENT_BRIDGE=1 game instead of "
                        "launching one. The controller uses this since it manages "
                        "launches; if the bridge is down under this flag, the run "
                        "reports it clearly and exits non-zero (it does NOT launch).")
    return p.parse_args(argv)


def main(argv=None):
    args = parse_args(argv if argv is not None else sys.argv[1:])

    glb = os.path.abspath(args.glb)
    texture = os.path.abspath(args.texture) if args.texture else None
    baseline = os.path.abspath(args.baseline)
    frame = os.path.abspath(args.frame)
    out_dir = os.path.abspath(args.out_dir)
    os.makedirs(out_dir, exist_ok=True)

    preview_render = os.path.join(out_dir, "preview_render.png")
    preview_verdict_path = os.path.join(out_dir, "preview_verdict.json")
    ingame_verdict_path = os.path.join(out_dir, "verdict.json")

    if texture and not os.path.exists(texture):
        texture = None  # an absent texture is recorded as None, not an error

    # ----- Stage 1: OFFLINE PREVIEW (the launch gate) ----------------------- #
    if not os.path.exists(glb):
        print("ERROR: candidate .glb does not exist: %s" % glb)
        return 2
    preview_rc, preview_verdict = run_offline_preview(glb, texture, out_dir)
    pv = (preview_verdict or {}).get("verdict")

    preview_artifacts = [
        ("preview render", preview_render if os.path.exists(preview_render) else None),
        ("preview verdict", preview_verdict_path if preview_verdict else None),
    ]

    # ----- Stage 2: GATE ---------------------------------------------------- #
    # The launch only happens on an offline PASS. A FAIL or INCONCLUSIVE preview
    # blocks the launch: no bridge call, no capture; the offline result stands.
    if pv != "pass":
        print_side_by_side(preview_verdict, preview_artifacts, None, None)
        print("\nGATE: offline preview verdict is %s -> LAUNCH BLOCKED. "
              "No in-game capture attempted." % (str(pv).upper()))
        if pv == "fail":
            failed = [k for k, d in ((preview_verdict or {}).get("criteria") or {}).items()
                      if isinstance(d, dict) and not d.get("pass")]
            print("  Offline criteria failing: %s. See the per-criterion thresholds "
                  "above and the preview render at %s." % (", ".join(failed) or "?", preview_render))
        # Non-zero exit; mirror the preview rc when it is non-zero, else 1.
        return preview_rc if preview_rc != 0 else 1

    # ----- Stage 3: IN-GAME (only reached on an offline PASS) --------------- #
    print("\nGATE: offline preview PASSED -> proceeding to the in-game capture.")
    launched_here = False

    if _bridge_reachable():
        print("BRIDGE: already reachable at %s." % BRIDGE_URL)
    elif args.assume_bridge:
        # --assume-bridge means the controller owns the launch; do NOT launch.
        print("BRIDGE: not reachable at %s and --assume-bridge was given; "
              "NOT launching. Start the game with FTK_AGENT_BRIDGE=1 first."
              % BRIDGE_URL)
        print_side_by_side(preview_verdict, preview_artifacts, None, None)
        return 2
    else:
        if not os.path.exists(RUN_BEPINEX):
            print("ERROR: cannot launch; run_bepinex.sh not found at %s" % RUN_BEPINEX)
            print_side_by_side(preview_verdict, preview_artifacts, None, None)
            return 2
        proc = launch_bridge_game()
        launched_here = True
        print("BRIDGE: waiting up to %.0fs for %s/health ..." % (BRIDGE_UP_TIMEOUT, BRIDGE_URL))
        if not wait_for_bridge(BRIDGE_UP_TIMEOUT):
            print("ERROR: bridge never came up within %.0fs (pid=%s). "
                  "Not faking a verdict." % (BRIDGE_UP_TIMEOUT, getattr(proc, "pid", "?")))
            shutdown_self_launched_game()
            print_side_by_side(preview_verdict, preview_artifacts, None, None)
            return 2
        print("BRIDGE: up.")

    ingame_verdict = None
    ingame_rc = 3
    try:
        capture_rc, capture_text = run_capture(out_dir)
        if capture_rc != 0:
            # The driver names the failing step on its INCONCLUSIVE line; surface it.
            step = "capture-error"
            for line in capture_text.splitlines():
                if line.startswith("INCONCLUSIVE:"):
                    step = line.split("INCONCLUSIVE:", 1)[1].strip()
                    break
            print("\nIN-GAME: capture INCONCLUSIVE (%s); no in-game verdict produced." % step)
            ingame_verdict = None
            ingame_rc = 3
        else:
            ref = preview_render if os.path.exists(preview_render) else None
            ingame_rc, ingame_verdict = run_ingame_verdict(frame, baseline, ref, out_dir)
    finally:
        # Shut down ONLY a game we launched; leave an external bridge running.
        if launched_here:
            shutdown_self_launched_game()
        else:
            print("BRIDGE: left running (externally launched / --assume-bridge).")

    annotated = None
    crop = None
    if isinstance(ingame_verdict, dict):
        annotated = ingame_verdict.get("annotated_image")
        crop = ingame_verdict.get("crop_image")
    ingame_artifacts = [
        ("in-game verdict", ingame_verdict_path if ingame_verdict else None),
        ("annotated frame", annotated),
        ("boss crop", crop),
        ("reference render", preview_render if os.path.exists(preview_render) else None),
    ]

    # ----- Stage 4: print BOTH verdicts side by side ------------------------ #
    print_side_by_side(preview_verdict, preview_artifacts,
                       ingame_verdict, ingame_artifacts)

    # Exit code: PASS only if BOTH the preview and the in-game verdict pass.
    iv = (ingame_verdict or {}).get("verdict")
    if pv == "pass" and iv == "pass":
        return 0
    if iv is None:
        return 3  # in-game inconclusive (capture failed / no verdict)
    if iv == "inconclusive":
        return 3
    return 1  # in-game fail (e.g. the runtime-only shatter)


if __name__ == "__main__":
    sys.exit(main())
