#!/usr/bin/env python3
"""FTK Agent MCP server.

A stdio MCP server (official `mcp` Python SDK, FastMCP) that exposes four tools
for a live Claude session to PLAY For The King and test custom content:

    ftk_observe     -> GET  /state
    ftk_act         -> POST /action
    ftk_wait_for    -> poll GET /health then GET /state until a predicate holds
    ftk_screenshot  -> GET  /screenshot (raw PNG bytes)

All HTTP calls are thin stdlib wrappers (urllib only) around the in-game
loopback bridge. No dependencies beyond `mcp`.

The bridge is the BepInEx plugin's FTKModFramework.Agent.AgentBridge, which only
listens when the game was launched with env FTK_AGENT_BRIDGE=1. It binds
127.0.0.1 (loopback) and is single-player test use only.

Configure the bridge base URL via env FTK_BRIDGE_URL (default
http://127.0.0.1:8777).
"""

import json
import os
import sys
import tempfile
import time
import urllib.error
import urllib.request

from mcp.server.fastmcp import FastMCP

# --------------------------------------------------------------------------- #
# Configuration
# --------------------------------------------------------------------------- #

BRIDGE_URL = os.environ.get("FTK_BRIDGE_URL", "http://127.0.0.1:8777").rstrip("/")

# Per-call HTTP timeouts (seconds). These sit just above the bridge-side
# RunOnMainThread timeouts so a slow main thread surfaces as a clean error
# rather than a hung tool call.
STATE_TIMEOUT = 6.0       # bridge /state RunOnMainThread = 4000 ms
ACTION_TIMEOUT = 10.0     # bridge /action RunOnMainThread = 8000 ms
SCREENSHOT_TIMEOUT = 10.0  # bridge /screenshot coroutine = 8000 ms
HEALTH_TIMEOUT = 3.0

mcp = FastMCP("ftk-agent")


# --------------------------------------------------------------------------- #
# HTTP helpers (stdlib only)
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
        # Bridge returned a non-2xx status (e.g. 503 no session). Surface body.
        body = b""
        try:
            body = e.read()
        except Exception:
            pass
        return e.code, e.headers.get("Content-Type", "") if e.headers else "", body


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
        return e.code, e.headers.get("Content-Type", "") if e.headers else "", body


def _json_or_error(status, body):
    """Decode a JSON body, or return a structured error dict on failure."""
    try:
        text = body.decode("utf-8") if isinstance(body, (bytes, bytearray)) else body
    except Exception:
        text = ""
    try:
        return json.loads(text) if text else {}
    except Exception:
        return {
            "ok": False,
            "error": "bridge returned non-JSON body (status %d): %s"
            % (status, (text or "")[:300]),
        }


def _bridge_unreachable(exc):
    return {
        "ok": False,
        "error": "ftk bridge unreachable at %s (%s). Launch For The King with "
        "FTK_AGENT_BRIDGE=1 and the FTKModFramework DLL installed."
        % (BRIDGE_URL, exc.__class__.__name__),
        "detail": str(exc),
    }


# --------------------------------------------------------------------------- #
# Predicate evaluation for ftk_wait_for
# --------------------------------------------------------------------------- #

def _resolve_path(snapshot, dotted):
    """Resolve a dotted key path against a nested dict (e.g. 'signals.modalOpen')."""
    cur = snapshot
    for part in dotted.split("."):
        if isinstance(cur, dict) and part in cur:
            cur = cur[part]
        else:
            return None, False
    return cur, True


def _coerce(literal):
    """Coerce a predicate RHS literal string into a Python value."""
    s = literal.strip()
    low = s.lower()
    if low == "true":
        return True
    if low == "false":
        return False
    if low == "null" or low == "none":
        return None
    # int
    try:
        return int(s)
    except ValueError:
        pass
    # float
    try:
        return float(s)
    except ValueError:
        pass
    # strip optional quotes, else bare string
    if len(s) >= 2 and s[0] in "\"'" and s[-1] == s[0]:
        return s[1:-1]
    return s


def _eval_predicate(snapshot, predicate):
    """Evaluate a restricted predicate against a /state snapshot.

    Supported forms:
      - "health"                  -> True (handled by caller via /health)
      - "phase==combat"           -> equality on a dotted key path
      - "phase!=menu"             -> inequality
      - "signals.modalOpen==true" -> bool/null/number/string RHS
      - "A==x OR B==y"            -> OR of clauses (any true)
      - "A==x AND B==y"          -> AND of clauses (all true)

    Returns (matched: bool, detail: str).
    """
    pred = predicate.strip()
    if not pred:
        return False, "empty predicate"

    if pred.lower() == "health":
        # Liveness only; caller treats /health ok as satisfaction.
        return True, "health"

    # Split on OR / AND (case-insensitive, whitespace-delimited word).
    # We do not support mixing OR and AND in one predicate.
    upper = pred.upper()
    if " OR " in upper:
        clauses = _split_on(pred, " OR ")
        results = [_eval_clause(snapshot, c) for c in clauses]
        return any(r[0] for r in results), "; ".join(r[1] for r in results)
    if " AND " in upper:
        clauses = _split_on(pred, " AND ")
        results = [_eval_clause(snapshot, c) for c in clauses]
        return all(r[0] for r in results), "; ".join(r[1] for r in results)
    return _eval_clause(snapshot, pred)


def _split_on(text, sep_spaced):
    """Case-insensitive split of text on a spaced separator like ' OR '."""
    out = []
    rest = text
    sep_lower = sep_spaced.lower()
    while True:
        low = rest.lower()
        idx = low.find(sep_lower)
        if idx == -1:
            out.append(rest)
            break
        out.append(rest[:idx])
        rest = rest[idx + len(sep_spaced):]
    return [c.strip() for c in out if c.strip()]


def _eval_clause(snapshot, clause):
    """Evaluate a single 'key==val' or 'key!=val' clause."""
    if "!=" in clause:
        key, _, rhs = clause.partition("!=")
        op = "!="
    elif "==" in clause:
        key, _, rhs = clause.partition("==")
        op = "=="
    else:
        return False, "unparsable clause: %r" % clause

    key = key.strip()
    expected = _coerce(rhs)
    actual, found = _resolve_path(snapshot, key)
    if not found:
        return False, "%s missing" % key

    if op == "==":
        matched = _values_equal(actual, expected)
    else:
        matched = not _values_equal(actual, expected)
    return matched, "%s%s%r (actual=%r)" % (key, op, expected, actual)


def _values_equal(actual, expected):
    if actual == expected:
        return True
    # Lenient numeric/bool/string comparison so 'phase==combat' matches a string
    # and 'day==3' matches an int parsed from JSON.
    try:
        if isinstance(expected, bool) or isinstance(actual, bool):
            return bool(actual) == bool(expected)
        if isinstance(expected, (int, float)) and isinstance(actual, (int, float)):
            return float(actual) == float(expected)
    except Exception:
        pass
    return str(actual) == str(expected)


# --------------------------------------------------------------------------- #
# Tools
# --------------------------------------------------------------------------- #

@mcp.tool()
def ftk_observe() -> dict:
    """Observe the full For The King game state (the agent's primary read).

    Wraps GET /state on the in-game bridge. Returns the complete snapshot dict
    verbatim: phase, inSession, singlePlayer, day/round, quest fields, party[],
    combat{}, map{}, choices[], and signals{} (including signals.warnings[] for
    any member that could not be read). Always returns even at the main menu.
    """
    try:
        status, _ctype, body = _get("/state", STATE_TIMEOUT)
    except (urllib.error.URLError, OSError) as e:
        return _bridge_unreachable(e)
    snap = _json_or_error(status, body)
    if status != 200 and isinstance(snap, dict) and "error" not in snap:
        snap["error"] = "GET /state returned HTTP %d" % status
    return snap


@mcp.tool()
def ftk_act(action: str, args: dict = None) -> dict:
    """Perform a game action (the agent's primary write).

    Wraps POST /action with body {action, args}. Returns {ok, error, result}.
    The bridge validates single-player and all preconditions BEFORE the game
    call, so a rejected action returns {ok:false, error:...} (HTTP 200), never
    an exception.

    Action set (see README for full arg shapes):
      start_run, move_to {big,small}, snap_to {big,small},
      set_target {enemyFid}, choose_ability {profId}, set_focus {n},
      attack {}, resolve_turn {attackerFid?,targetFid?,profId?,hit?},
      end_turn {}, select_choice {index}, advance {}, enter_tile {}.
    """
    payload = {"action": action, "args": args or {}}
    try:
        status, _ctype, body = _post_json("/action", payload, ACTION_TIMEOUT)
    except (urllib.error.URLError, OSError) as e:
        return _bridge_unreachable(e)
    result = _json_or_error(status, body)
    if status != 200 and isinstance(result, dict):
        result.setdefault("ok", False)
        result.setdefault("error", "POST /action returned HTTP %d" % status)
    return result


@mcp.tool()
def ftk_wait_for(predicate: str, timeout_s: int = 60, poll_s: float = 1.0) -> dict:
    """Poll game state until a predicate is true or a timeout elapses.

    Keeps the observe-decide-act loop from busy-spinning. First checks GET
    /health for liveness, then polls GET /state and evaluates `predicate`.

    predicate is a restricted key expression over the /state snapshot:
      "phase==overworld", "phase==combat", "phase!=menu",
      "signals.modalOpen==true", "day==3", "map.realmId==42",
      "phase==victory", and OR/AND of such clauses
      ("phase==combat OR phase==overworld").
    The bare predicate "health" waits only for the bridge to be reachable.

    Returns the matching snapshot dict (with _waited_s) on success, or
    {ok:false, timeout:true, ...} on timeout.
    """
    deadline = time.time() + max(0, timeout_s)
    poll = max(0.05, poll_s)
    started = time.time()
    last_detail = ""
    last_snapshot = None
    health_ok = False
    hjson = None

    while True:
        # Liveness first so we fail fast and do not call /state on a dead bridge.
        try:
            hstatus, _hc, hbody = _get("/health", HEALTH_TIMEOUT)
            hjson = _json_or_error(hstatus, hbody)
            health_ok = hstatus == 200 and bool(
                hjson.get("ok", hstatus == 200) if isinstance(hjson, dict) else False
            )
        except (urllib.error.URLError, OSError) as e:
            health_ok = False
            hjson = None
            last_detail = "health unreachable: %s" % e.__class__.__name__

        if predicate.strip().lower() == "health":
            if health_ok:
                return {
                    "ok": True,
                    "matched": True,
                    "predicate": predicate,
                    "_waited_s": round(time.time() - started, 3),
                    "health": hjson,
                }
        elif health_ok:
            try:
                sstatus, _sc, sbody = _get("/state", STATE_TIMEOUT)
                snap = _json_or_error(sstatus, sbody)
                last_snapshot = snap
                if isinstance(snap, dict):
                    matched, detail = _eval_predicate(snap, predicate)
                    last_detail = detail
                    if matched:
                        snap["_matched_predicate"] = predicate
                        snap["_waited_s"] = round(time.time() - started, 3)
                        return snap
            except (urllib.error.URLError, OSError) as e:
                last_detail = "state unreachable: %s" % e.__class__.__name__

        if time.time() >= deadline:
            return {
                "ok": False,
                "timeout": True,
                "predicate": predicate,
                "waited_s": round(time.time() - started, 3),
                "health_ok": health_ok,
                "last_detail": last_detail,
                "last_snapshot": last_snapshot,
                "error": "predicate %r not satisfied within %ds"
                % (predicate, timeout_s),
            }
        time.sleep(min(poll, max(0.0, deadline - time.time())))


@mcp.tool()
def ftk_screenshot(save_path: str = None) -> dict:
    """Capture an in-game screenshot as PNG.

    Wraps GET /screenshot (raw image/png bytes, or 503 if no session). Writes
    the PNG to save_path (or a temp file) so the agent can inspect/attach it.

    Returns {ok, bytes_len, path, content_type} on success, or
    {ok:false, error, status} on failure (e.g. 503 no active session).
    """
    try:
        status, ctype, body = _get("/screenshot", SCREENSHOT_TIMEOUT)
    except (urllib.error.URLError, OSError) as e:
        return _bridge_unreachable(e)

    if status != 200:
        # Body may carry a JSON error (e.g. {"error":"no session"}).
        err = _json_or_error(status, body)
        msg = err.get("error") if isinstance(err, dict) else None
        return {
            "ok": False,
            "status": status,
            "error": msg or "GET /screenshot returned HTTP %d "
            "(503 means no active game session)" % status,
        }

    if "png" not in (ctype or "").lower() and not _looks_like_png(body):
        return {
            "ok": False,
            "status": status,
            "error": "screenshot response was not PNG (content-type=%r)" % ctype,
        }

    if save_path:
        path = os.path.abspath(os.path.expanduser(save_path))
        d = os.path.dirname(path)
        if d:
            os.makedirs(d, exist_ok=True)
    else:
        fd, path = tempfile.mkstemp(prefix="ftk_screenshot_", suffix=".png")
        os.close(fd)

    try:
        with open(path, "wb") as f:
            f.write(body)
    except OSError as e:
        return {"ok": False, "error": "could not write screenshot: %s" % e}

    return {
        "ok": True,
        "bytes_len": len(body),
        "path": path,
        "content_type": ctype or "image/png",
    }


def _looks_like_png(body):
    return isinstance(body, (bytes, bytearray)) and body[:8] == b"\x89PNG\r\n\x1a\n"


# --------------------------------------------------------------------------- #
# Entry point
# --------------------------------------------------------------------------- #

def main():
    # FastMCP speaks MCP over stdio; all diagnostics must go to stderr so they
    # do not corrupt the stdio JSON-RPC stream.
    print("ftk-agent MCP server starting (bridge=%s)" % BRIDGE_URL, file=sys.stderr)
    mcp.run()


if __name__ == "__main__":
    main()
