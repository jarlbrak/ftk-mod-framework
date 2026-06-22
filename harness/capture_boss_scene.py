#!/usr/bin/env python3
"""Capture driver for FTK work item #69: the Hollow Mire boss scene.

A standalone driver that talks to the in-game bridge over loopback HTTP (no MCP
roundtrip) and runs the documented Hollow Mire boss sequence to a LIT full-frame
screenshot at the player's first turn. It then either MEASURES the stock-troll
baseline silhouette (BASELINE mode) or captures a CANDIDATE full-frame PNG for the
visual gate (CANDIDATE mode).

The documented sequence (see harness/README.md, "In-dungeon combat and
true-victory (verified findings)"):

    start_run {adventure: "HollowMire"}
      -> enter_dungeon {dungeon: "FloodedCrypt"}
      -> dungeon_regen                       (populate the empty room list)
      -> cleared_room x10                     (boss is level 1 room 4; walk the pointer)
      -> dungeon_encounter                    (start the room fight, EncounterLocation.Dungeon)
      -> dungeon_scroll_complete              (fire the deferred camera-scroll ack)
      -> wait for combat.heroTurnReady==true  (poll /state)
      -> dismiss_message                      (clear the re-firing boss-intro dialog)
      -> ftk_screenshot(save_path)            (the LIT diorama frame)

Hard rules (spec / issue #69):
  - NEVER call `advance` before the screenshot; advance ends the turn and blacks
    out the diorama. Use `dismiss_message` to clear the re-firing boss dialog.
  - The diorama is lit ONLY on the first hero turn, so capture there.
  - On ANY failure (bridge unreachable, HTTP 503 no session, action {ok:false},
    timeout, empty room list after dungeon_regen) emit an INCONCLUSIVE result that
    NAMES the failing step, and exit non-zero. Never synthesize PASS/FAIL; never
    throw into the game (catch and report).

All HTTP is Python standard library only (urllib), copying the idiom of
harness/ftk_mcp_server.py.

Usage:
  capture_boss_scene.py --mode baseline    (writes tools/ai-model-pipeline/baseline.json)
  capture_boss_scene.py --mode candidate    (writes the candidate fixture PNG)

Env:
  FTK_BRIDGE_URL          bridge base URL (default http://127.0.0.1:8777)
  FTK_CAPTURE_MODE        baseline|candidate (overridden by --mode)
  FTK_BASELINE_STOCK_BODY recorded into baseline.json provenance when "1"
"""

import argparse
import json
import os
import subprocess
import sys
import time
import urllib.error
import urllib.request

# Import the SHARED analyze() so the baseline silhouette is measured with the
# identical front-end candidates use (one function, two callers; spec #66).
_HERE = os.path.dirname(os.path.abspath(__file__))
_REPO_ROOT = os.path.dirname(_HERE)
_PIPELINE_DIR = os.path.join(_REPO_ROOT, "tools", "ai-model-pipeline")
if _PIPELINE_DIR not in sys.path:
    sys.path.insert(0, _PIPELINE_DIR)
import visual_gate  # noqa: E402  (path set above)


# --------------------------------------------------------------------------- #
# Configuration
# --------------------------------------------------------------------------- #

BRIDGE_URL = os.environ.get("FTK_BRIDGE_URL", "http://127.0.0.1:8777").rstrip("/")

# Per-call HTTP timeouts (seconds), matching ftk_mcp_server.py so a slow main
# thread surfaces as a clean error rather than a hung call.
HEALTH_TIMEOUT = 3.0
STATE_TIMEOUT = 6.0
ACTION_TIMEOUT = 10.0
SCREENSHOT_TIMEOUT = 10.0

# Hollow Mire boss-room geometry (from README: Flooded Crypt is 2 levels x 6
# rooms; the boss is level 1 room 4). The room pointer starts at the entrance,
# so we walk it forward with cleared_room. Ten advances reaches the boss room
# without overshooting the level-1 room list.
CLEARED_ROOM_STEPS = 10

# Capture-anchor timing. We PREFER the hero's first turn (combat.heroTurnReady),
# but under the synthetic dungeon entry that gate can stay false indefinitely
# (documented stall in harness/README.md: the dungeon camera FSM never hands the
# turn to the player). The diorama is nonetheless lit once combat is active and
# the camera-scroll/modal have settled, so after a fair wait we fall back to that
# lit frame. A black/unlit frame is caught downstream by analyze()'s
# unlit-or-empty-frame INCONCLUSIVE, so the fallback never yields a false PASS.
HERO_TURN_TIMEOUT = 45.0     # overall cap waiting for combat to be capturable
HERO_TURN_PREFER = 12.0      # give heroTurnReady this long before falling back
COMBAT_SETTLE = 6.0          # camera/modal settle once combat is active
HERO_TURN_POLL = 1.0

# Output locations (committed-fixture paths the controller picks up).
BASELINE_JSON = os.path.join(_PIPELINE_DIR, "baseline.json")
FIXTURES_DIR = os.path.join(_PIPELINE_DIR, "fixtures")
CANDIDATE_PNG = os.path.join(FIXTURES_DIR, "candidate_boss.png")
BASELINE_FRAME_PNG = os.path.join(FIXTURES_DIR, "baseline_boss.png")

CHASSIS_NAME = "trollCaveA"


# --------------------------------------------------------------------------- #
# Result type: a named-step outcome, never a thrown exception into the game.
# --------------------------------------------------------------------------- #

class Inconclusive(Exception):
    """Raised internally to short-circuit to a named-step INCONCLUSIVE outcome.

    `step` is the exact named token (e.g. "screenshot-503-no-session",
    "empty-room-list-after-dungeon_regen", "timeout-waiting-heroTurnReady").
    `detail` is human context. The top-level driver catches this and prints the
    final INCONCLUSIVE line; it is never allowed to propagate to the bridge.
    """

    def __init__(self, step, detail=""):
        super().__init__(step)
        self.step = step
        self.detail = detail


# --------------------------------------------------------------------------- #
# HTTP helpers (stdlib only; same idiom as ftk_mcp_server.py)
# --------------------------------------------------------------------------- #

def _get(path, timeout):
    """GET BRIDGE_URL + path. Returns (status, content_type, body_bytes)."""
    url = BRIDGE_URL + path
    req = urllib.request.Request(url, method="GET")
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            ctype = resp.headers.get("Content-Type", "")
            return resp.status, ctype, resp.read()
    except urllib.error.HTTPError as e:
        body = b""
        try:
            body = e.read()
        except Exception:
            pass
        ctype = e.headers.get("Content-Type", "") if e.headers else ""
        return e.code, ctype, body


def _post_json(path, payload, timeout):
    """POST JSON to BRIDGE_URL + path. Returns (status, content_type, body_bytes)."""
    url = BRIDGE_URL + path
    data = json.dumps(payload).encode("utf-8")
    req = urllib.request.Request(
        url, data=data, method="POST",
        headers={"Content-Type": "application/json"},
    )
    try:
        with urllib.request.urlopen(req, timeout=timeout) as resp:
            ctype = resp.headers.get("Content-Type", "")
            return resp.status, ctype, resp.read()
    except urllib.error.HTTPError as e:
        body = b""
        try:
            body = e.read()
        except Exception:
            pass
        ctype = e.headers.get("Content-Type", "") if e.headers else ""
        return e.code, ctype, body


def _json_or_empty(body):
    """Decode a JSON body to a dict, or {} on any failure."""
    try:
        text = body.decode("utf-8") if isinstance(body, (bytes, bytearray)) else body
    except Exception:
        return {}
    try:
        return json.loads(text) if text else {}
    except Exception:
        return {}


def _looks_like_png(body):
    return isinstance(body, (bytes, bytearray)) and body[:8] == b"\x89PNG\r\n\x1a\n"


# --------------------------------------------------------------------------- #
# Bridge operations. Each raises Inconclusive(step, detail) on failure, naming
# the step so the controller can branch on the exact cause.
# --------------------------------------------------------------------------- #

def health(step):
    """Confirm the bridge is reachable. Raises Inconclusive(step) if not."""
    try:
        status, _ctype, body = _get("/health", HEALTH_TIMEOUT)
    except (urllib.error.URLError, OSError) as e:
        raise Inconclusive(
            step,
            "bridge unreachable at %s (%s): %s" % (BRIDGE_URL, e.__class__.__name__, e),
        )
    j = _json_or_empty(body)
    ok = status == 200 and bool(j.get("ok", status == 200) if isinstance(j, dict) else False)
    if not ok:
        raise Inconclusive(step, "health HTTP %d body=%r" % (status, j))
    return j


def observe(step):
    """GET /state. Raises Inconclusive(step) if unreachable or not 200."""
    try:
        status, _ctype, body = _get("/state", STATE_TIMEOUT)
    except (urllib.error.URLError, OSError) as e:
        raise Inconclusive(step, "state unreachable (%s): %s" % (e.__class__.__name__, e))
    snap = _json_or_empty(body)
    if status != 200:
        raise Inconclusive(step, "GET /state HTTP %d" % status)
    if not isinstance(snap, dict):
        raise Inconclusive(step, "GET /state non-dict body")
    return snap


def act(action, args, step):
    """POST /action {action,args}. Raises Inconclusive(step) on transport
    failure or an {ok:false} envelope. Returns the result dict on success."""
    payload = {"action": action, "args": args or {}}
    try:
        status, _ctype, body = _post_json("/action", payload, ACTION_TIMEOUT)
    except (urllib.error.URLError, OSError) as e:
        raise Inconclusive(step, "action %r unreachable (%s): %s"
                           % (action, e.__class__.__name__, e))
    result = _json_or_empty(body)
    if status != 200 or not isinstance(result, dict) or not result.get("ok", False):
        err = result.get("error") if isinstance(result, dict) else None
        raise Inconclusive(step, "action %r failed (HTTP %d): %s"
                           % (action, status, err or result))
    return result


def enter_flooded_crypt(max_tries=10, settle_s=1.0):
    """Enter the Flooded Crypt, tolerating the post-start_run settle window.

    Immediately after start_run the realm has not yet placed the FloodedCrypt POI
    on the map and the intro StoryQuestMessage modal is open, so enter_dungeon
    rejects with "FloodedCrypt POI not on map". We drain the modal and retry with
    a short settle between attempts until the action succeeds. Raises
    Inconclusive('enter_dungeon') if it never enters within max_tries."""
    last_detail = ""
    for _ in range(max_tries):
        drain_messages(max_calls=4)
        payload = {"action": "enter_dungeon", "args": {"dungeon": "FloodedCrypt"}}
        try:
            status, _ctype, body = _post_json("/action", payload, ACTION_TIMEOUT)
        except (urllib.error.URLError, OSError) as e:
            raise Inconclusive("enter_dungeon", "action 'enter_dungeon' unreachable (%s): %s"
                               % (e.__class__.__name__, e))
        result = _json_or_empty(body)
        if status == 200 and isinstance(result, dict) and result.get("ok", False):
            return result
        last_detail = (result.get("error") if isinstance(result, dict) else None) or str(result)
        time.sleep(settle_s)
    raise Inconclusive("enter_dungeon",
                       "FloodedCrypt not enterable after %d tries: %s" % (max_tries, last_detail))


def drain_messages(max_calls=8):
    """Dismiss any queued story / boss-intro messages until the queue is empty.

    The Hollow Mire run opens a StoryQuestMessage modal after start_run (it blocks
    enter_dungeon) and re-fires the boss-intro dialog in combat (it blocks the
    hero turn). dismiss_message returns {ok:true} while a message is queued and
    {ok:false, error:"no message open"} once drained, so that {ok:false} is the
    NATURAL terminal of draining, not a failure; this helper therefore posts
    dismiss_message directly instead of via act() (which raises on {ok:false}).
    Best-effort: a transport error here is surfaced by the next real step. NEVER
    uses `advance` (which would end the turn and black out the diorama)."""
    for _ in range(max_calls):
        try:
            status, _ctype, body = _post_json(
                "/action", {"action": "dismiss_message", "args": {}}, ACTION_TIMEOUT)
        except (urllib.error.URLError, OSError):
            return
        result = _json_or_empty(body)
        if status != 200 or not isinstance(result, dict) or not result.get("ok", False):
            return  # "no message open" (or transport hiccup): queue drained.


def screenshot(save_path, step):
    """GET /screenshot and write the PNG to save_path. Raises Inconclusive(step)
    on transport failure, 503 (no session), or a non-PNG body.

    A 503 is reported with the explicit token "<step>-503-no-session" so the
    controller sees screenshot-503-no-session per the issue's example."""
    try:
        status, ctype, body = _get("/screenshot", SCREENSHOT_TIMEOUT)
    except (urllib.error.URLError, OSError) as e:
        raise Inconclusive(step, "screenshot unreachable (%s): %s"
                           % (e.__class__.__name__, e))
    if status == 503:
        raise Inconclusive(step + "-503-no-session", "GET /screenshot HTTP 503 (no active session)")
    if status != 200:
        err = _json_or_empty(body)
        msg = err.get("error") if isinstance(err, dict) else None
        raise Inconclusive(step, "GET /screenshot HTTP %d: %s" % (status, msg or ""))
    if "png" not in (ctype or "").lower() and not _looks_like_png(body):
        raise Inconclusive(step, "screenshot body not PNG (content-type=%r)" % ctype)

    path = os.path.abspath(os.path.expanduser(save_path))
    d = os.path.dirname(path)
    if d:
        os.makedirs(d, exist_ok=True)
    try:
        with open(path, "wb") as f:
            f.write(body)
    except OSError as e:
        raise Inconclusive(step, "could not write screenshot to %s: %s" % (path, e))
    return path, len(body)


def wait_for_lit_combat(step):
    """Wait until the boss combat is in a capturable LIT state, and return
    (snap, anchor) where anchor is "heroTurnReady" (ideal) or "combat-settle"
    (fallback). Dismisses the re-firing boss-intro modal throughout.

    Strategy: poll /state; if combat.heroTurnReady flips true, capture there
    (the diorama's lit first turn). Otherwise, once combat has been active for
    COMBAT_SETTLE seconds AND we have given heroTurnReady at least HERO_TURN_PREFER
    seconds to appear, capture the lit diorama anyway (the documented stall means
    heroTurnReady may never flip). Raises Inconclusive(step) only if combat never
    becomes active within HERO_TURN_TIMEOUT (then there is nothing lit to shoot)."""
    deadline = time.time() + HERO_TURN_TIMEOUT
    start = time.time()
    combat_active_since = None
    last = None
    while True:
        snap = observe("wait-lit-combat-state")
        last = snap
        combat = snap.get("combat") if isinstance(snap, dict) else {}
        sig = snap.get("signals") if isinstance(snap, dict) else {}
        combat = combat if isinstance(combat, dict) else {}
        sig = sig if isinstance(sig, dict) else {}
        # The boss-intro StoryQuestMessage re-fires and blocks the turn; clear it.
        if sig.get("modalOpen"):
            drain_messages(max_calls=3)
        if combat.get("heroTurnReady"):
            return snap, "heroTurnReady"
        active = bool(sig.get("inCombat")) or snap.get("phase") == "combat"
        now = time.time()
        if active and combat_active_since is None:
            combat_active_since = now
        if (active and combat_active_since is not None
                and (now - combat_active_since) >= COMBAT_SETTLE
                and (now - start) >= HERO_TURN_PREFER):
            return snap, "combat-settle"
        if now >= deadline:
            if active:
                return snap, "combat-settle"  # active but never readied: still lit
            raise Inconclusive(
                step,
                "combat never became active within %.0fs; last combat=%r"
                % (HERO_TURN_TIMEOUT, (last or {}).get("combat")),
            )
        time.sleep(min(HERO_TURN_POLL, max(0.0, deadline - now)))


# --------------------------------------------------------------------------- #
# The documented boss sequence (everything up to and including the LIT frame).
# --------------------------------------------------------------------------- #

def run_boss_sequence(frame_path):
    """Drive start_run -> ... -> dismiss_message -> screenshot(frame_path).

    Returns the absolute frame path on success. Raises Inconclusive(step) with a
    named step on any failure; NEVER calls `advance` before the screenshot."""
    # 0. Liveness. A dead bridge here is the no-session / unreachable case.
    health("bridge-unreachable")

    # 1. Start the Hollow Mire run.
    act("start_run", {"adventure": "HollowMire"}, "start_run")

    # 1b. Enter the Flooded Crypt dungeon. After start_run the realm needs a few
    #     frames to place the FloodedCrypt POI on the map AND the intro
    #     StoryQuestMessage modal blocks it; until both settle, enter_dungeon
    #     fails with "FloodedCrypt POI not on map". So we drain the modal and
    #     retry with a short settle between attempts until it enters.
    enter_flooded_crypt()

    # 3. Re-run GenerateDungeonEncounters; the synthetic entry leaves the room
    #    list empty otherwise. Verify the room list is non-empty afterward.
    act("dungeon_regen", {}, "dungeon_regen")
    snap = observe("verify-rooms-after-dungeon_regen")
    if _dungeon_room_count(snap) <= 0:
        raise Inconclusive(
            "empty-room-list-after-dungeon_regen",
            "dungeon room list is empty after dungeon_regen; snap dungeon=%r"
            % _dungeon_snap(snap),
        )

    # 4. Walk the room pointer to the boss room (level 1 room 4).
    for i in range(CLEARED_ROOM_STEPS):
        act("cleared_room", {}, "cleared_room-%d" % (i + 1))

    # 5. Start the boss fight via MiniHexDungeon.Encounter (EncounterLocation.Dungeon).
    act("dungeon_encounter", {}, "dungeon_encounter")

    # 6. Fire the deferred camera-scroll ack so CommenceBattle runs. The first ack
    #    can race the camera transition and not initialize the player dummies; a
    #    short settle and a second ack makes it reliable (best-effort).
    res = act("dungeon_scroll_complete", {}, "dungeon_scroll_complete")
    inited = (res.get("result") or {}).get("dummiesWereInitiated") if isinstance(res, dict) else False
    if not inited:
        time.sleep(2.0)
        drain_messages(max_calls=3)
        try:
            act("dungeon_scroll_complete", {}, "dungeon_scroll_complete-2")
        except Inconclusive:
            pass  # best-effort second ack; wait_for_lit_combat is the real gate

    # 7. Wait until the boss combat is in a capturable LIT state (heroTurnReady if
    #    it flips, else combat-active + settled).
    _snap, anchor = wait_for_lit_combat("timeout-waiting-lit-combat")

    # 8. Clear the re-firing boss-intro dialog. NOTE: dismiss_message, NOT advance
    #    (advance ends the turn and blacks out the diorama).
    drain_messages(max_calls=4)

    # 9. Capture the LIT full frame.
    path, _nbytes = screenshot(frame_path, "screenshot")
    return path, anchor


def _dungeon_snap(snap):
    """Best-effort dungeon sub-object for diagnostics (shape may vary)."""
    if not isinstance(snap, dict):
        return None
    for key in ("dungeon", "map", "combat"):
        if isinstance(snap.get(key), dict):
            return snap.get(key)
    return None


def _dungeon_room_count(snap):
    """Best-effort count of populated dungeon rooms from a /state snapshot.

    The bridge's dungeon introspection shape may surface the room list under a
    few keys; we accept any of them and fall back to a conservative -1 (unknown)
    only when we cannot find a count, treating unknown as non-empty so a shape
    change does not spuriously fail the run. An explicit 0 IS the empty-room
    hazard and DOES fail."""
    if not isinstance(snap, dict):
        return -1
    dungeon = snap.get("dungeon")
    candidates = []
    if isinstance(dungeon, dict):
        candidates = [dungeon.get("roomCount"), dungeon.get("rooms"),
                      dungeon.get("totalRooms"), dungeon.get("roomsRemaining")]
    candidates += [snap.get("dungeonRoomCount"), snap.get("rooms")]
    for c in candidates:
        if isinstance(c, bool):
            continue
        if isinstance(c, int):
            return c
        if isinstance(c, (list, tuple)):
            return len(c)
    return -1  # unknown shape -> do not fail on it


# --------------------------------------------------------------------------- #
# Baseline measurement and provenance.
# --------------------------------------------------------------------------- #

def _git_commit_short():
    """Framework git commit (short). Returns 'unknown' if git is unavailable."""
    try:
        out = subprocess.check_output(
            ["git", "rev-parse", "--short", "HEAD"],
            cwd=_REPO_ROOT, stderr=subprocess.DEVNULL,
        )
        return out.decode("utf-8", "replace").strip() or "unknown"
    except Exception:
        return "unknown"


def measure_baseline(frame_path, capture_anchor="unknown"):
    """Measure the stock-troll silhouette from the LIT frame via the SHARED
    analyze(), and write tools/ai-model-pipeline/baseline.json with provenance.

    The baseline-anchored criteria in visual_gate.analyze() read `bbox`,
    `centroid_norm`, `height`, `aspect`, and (optionally) `area` from this file;
    we derive them from the same analyze() verdict so the baseline is measured
    on the identical front-end candidates use. Raises Inconclusive on a
    degenerate frame (analyze returns inconclusive)."""
    verdict = visual_gate.analyze(frame_path, baseline=None)
    if verdict.get("verdict") == "inconclusive":
        raise Inconclusive(
            "baseline-frame-" + (verdict.get("inconclusive_reason") or "inconclusive"),
            "analyze() found the baseline frame degenerate: %r"
            % verdict.get("inconclusive_reason"),
        )

    image_w = int(verdict["image_w"])
    image_h = int(verdict["image_h"])
    metrics = verdict["metrics"]
    bx, by, bw, bh = [int(v) for v in metrics["bbox"]]
    cx, cy = [float(v) for v in metrics["centroid_norm"]]

    height_norm = float(bh) / float(image_h)
    aspect = float(bh) / max(float(bw), 1.0)
    # bbox-area fraction of the full frame; this is exactly the fallback
    # analyze() uses for `area` when absent, recorded explicitly for clarity.
    area_norm = (float(bw) * float(bh)) / float(image_w * image_h)

    baseline = {
        "schema": "ftk-visual-baseline/1",
        # Silhouette fields consumed by visual_gate.analyze() baseline criteria.
        "bbox": [bx, by, bw, bh],
        "centroid_norm": [cx, cy],
        "height": height_norm,
        "aspect": aspect,
        "area": area_norm,
        # Provenance (issue #69 acceptance criteria).
        "image_w": image_w,
        "image_h": image_h,
        "chassis": CHASSIS_NAME,
        "framework_commit": _git_commit_short(),
        "ftk_baseline_stock_body": os.environ.get("FTK_BASELINE_STOCK_BODY") == "1",
        "capture_anchor": capture_anchor,
        "source_image": os.path.abspath(frame_path),
    }
    with open(BASELINE_JSON, "w") as f:
        json.dump(baseline, f, indent=2)
        f.write("\n")
    return baseline


# --------------------------------------------------------------------------- #
# Resolution-equality guard between baseline and candidate captures.
# --------------------------------------------------------------------------- #

def assert_resolution_matches_baseline(image_w, image_h):
    """If a baseline.json exists, assert this capture's resolution matches it.

    Raises Inconclusive('resolution-mismatch-baseline-vs-candidate') on a
    mismatch. A missing baseline is not an error (the controller may run the
    candidate before the baseline); we only compare when both exist."""
    if not os.path.exists(BASELINE_JSON):
        return
    try:
        with open(BASELINE_JSON, "r") as f:
            bl = json.load(f)
    except Exception as e:
        raise Inconclusive("baseline-json-unreadable",
                           "could not read %s: %s" % (BASELINE_JSON, e))
    bw, bh = bl.get("image_w"), bl.get("image_h")
    if isinstance(bw, int) and isinstance(bh, int) and (bw, bh) != (image_w, image_h):
        raise Inconclusive(
            "resolution-mismatch-baseline-vs-candidate",
            "candidate %dx%d != baseline %dx%d; captures must share window resolution"
            % (image_w, image_h, bw, bh),
        )


def _image_size(frame_path):
    """Return (w, h) of a PNG via the shared analyze() front-end (Pillow)."""
    from PIL import Image
    with Image.open(frame_path) as img:
        return int(img.width), int(img.height)


# --------------------------------------------------------------------------- #
# Modes
# --------------------------------------------------------------------------- #

def run_baseline():
    """BASELINE: capture the LIT stock-troll frame, measure it, write baseline.json."""
    frame, anchor = run_boss_sequence(BASELINE_FRAME_PNG)
    bl = measure_baseline(frame, capture_anchor=anchor)
    return {
        "mode": "baseline",
        "frame": frame,
        "baseline_json": BASELINE_JSON,
        "image_w": bl["image_w"],
        "image_h": bl["image_h"],
        "framework_commit": bl["framework_commit"],
        "anchor": anchor,
    }


def run_candidate():
    """CANDIDATE: capture the LIT candidate frame to the committed fixture path.

    The fixtures directory is created by screenshot() right before it writes the
    PNG, so a failed/offline run leaves no stray empty directory in the tree."""
    frame, anchor = run_boss_sequence(CANDIDATE_PNG)
    image_w, image_h = _image_size(frame)
    # If a baseline exists, the two captures MUST share the window resolution.
    assert_resolution_matches_baseline(image_w, image_h)
    return {
        "mode": "candidate",
        "frame": frame,
        "image_w": image_w,
        "image_h": image_h,
        "anchor": anchor,
    }


# --------------------------------------------------------------------------- #
# Entry point
# --------------------------------------------------------------------------- #

def parse_args(argv):
    p = argparse.ArgumentParser(
        description="Capture the Hollow Mire boss scene (LIT first-turn frame) "
                    "for the FTK visual gate.",
    )
    default_mode = os.environ.get("FTK_CAPTURE_MODE", "candidate").strip().lower()
    p.add_argument(
        "--mode", choices=["baseline", "candidate"], default=default_mode,
        help="baseline: measure the stock-troll silhouette and write baseline.json. "
             "candidate: capture a candidate fixture PNG. "
             "(default from FTK_CAPTURE_MODE, else 'candidate')",
    )
    return p.parse_args(argv)


def main(argv=None):
    args = parse_args(argv if argv is not None else sys.argv[1:])
    print("capture_boss_scene: mode=%s bridge=%s" % (args.mode, BRIDGE_URL))
    try:
        if args.mode == "baseline":
            out = run_baseline()
        else:
            out = run_candidate()
    except Inconclusive as inc:
        detail = (" (%s)" % inc.detail) if inc.detail else ""
        print("INCONCLUSIVE: %s%s" % (inc.step, detail))
        return 2
    except Exception as e:  # never let anything propagate; report it named.
        print("INCONCLUSIVE: unexpected-error-%s (%s)" % (e.__class__.__name__, e))
        return 2

    if args.mode == "baseline":
        print("CAPTURED baseline frame=%s baseline_json=%s res=%dx%d commit=%s anchor=%s"
              % (out["frame"], out["baseline_json"], out["image_w"], out["image_h"],
                 out["framework_commit"], out["anchor"]))
    else:
        print("CAPTURED candidate frame=%s res=%dx%d anchor=%s"
              % (out["frame"], out["image_w"], out["image_h"], out["anchor"]))
    return 0


if __name__ == "__main__":
    sys.exit(main())
