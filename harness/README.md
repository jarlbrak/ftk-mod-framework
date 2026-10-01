# FTK Agent Harness (MCP server)

An agentic test harness that lets an agent session **play For The King** to
verify custom content. It is the client half of a two-part bridge:

```
Agent session   <-- stdio MCP -->  ftk_mcp_server.py  <-- loopback HTTP -->  in-game bridge (BepInEx plugin)
```

- **In-game bridge**: `FTKModFramework.Agent.AgentBridge`, shipped inside
  `FTKModFramework.dll`. It exposes a tiny loopback HTTP API
  (`/state`, `/ui`, `/input`, `/action`, `/screenshot`, `/health`) and only starts when the game
  is launched with env `FTK_AGENT_BRIDGE=1`. It binds `127.0.0.1` only and is
  **single-player test use only** (direct calls can desync co-op).
- **This server** (`ftk_mcp_server.py`): a stdio MCP server (official `mcp`
  Python SDK / FastMCP) that wraps that HTTP API as MCP tools. HTTP calls use
  only the Python standard library (`urllib`); the only dependency is `mcp`.

## Tools

| Tool | Wraps | Purpose |
|---|---|---|
| `ftk_observe()` | `GET /state` | Full observe snapshot (phase, party, combat, map, choices, signals). The primary read. |
| `ftk_prepare_offline()` | `POST /action` | Configure disconnected title/setup for native offline single-player. Does not start a run. |
| `ftk_wait_for(predicate, timeout_s=60, poll_s=1.0)` | `GET /health` then `GET /state` | Poll until a predicate over the snapshot holds (keeps the loop from busy-spinning). |
| `ftk_ui()` | `GET /ui` | Native controls, labels, focus, raycast reachability and screen coordinates. |
| `ftk_input(steps, request_id=None, wait=True, timeout_s=20)` | `POST /action`, `GET /input` | Bounded native mouse/keyboard sequence with deduplicated request ID. |
| `ftk_input_status()` | `GET /input` | Hook availability and latest sequence progress/outcome. |
| `ftk_input_cancel()` | `POST /action` | Cancel the active sequence and release synthetic controls. |
| `ftk_screenshot(save_path=None)` | `GET /screenshot` | Capture a PNG to disk; returns `{ok, bytes_len, path}`. |

### Native input

Use native input for UI and gameplay. The old direct gameplay commands and
automatic start/combat/dungeon drivers have been removed.
Before creating a run, `prepare_offline` explicitly sets the title-screen
`m_UseOnlineSinglePlayer` flag to false. It requires a disconnected title/setup
screen, reports the previous value, and does not start a run or choose a class.
This flag also affects native single-player resume. Use only disposable test saves.
This is test configuration; subsequent clicks follow the native new-game path.

The bridge redirects managed hardware reads in the game, Unity UI and framework
UI, including Mods search, and below Rewired's action cache, so keys
follow the game's configured mappings. Unity UI keeps its normal pointer
press/release, raycast, drag/drop and scroll processing.

```python
ftk_prepare_offline()  # title/setup only: select native offline single-player path
ftk_ui()  # find the control and inspect its interactable/centerHit fields
ftk_input([{"x": 400, "y": 300, "buttons": [0], "frames": 2}, {"frames": 2}])
ftk_input([{"keys": ["Escape"], "frames": 2}, {"frames": 2}])
ftk_input([{"x": 400, "y": 300, "frames": 2},
           {"buttons": [0], "frames": 2},
           {"x": 420, "y": 300, "buttons": [0], "frames": 2},
           {"x": 550, "y": 300, "buttons": [0], "frames": 4}, {"frames": 2}])
```

Coordinates use actual game pixels with origin at the **bottom left**, not desktop
coordinates. Read width/height from `ftk_ui`; screenshots display top-down, so
convert screenshot Y to `height - y`. Each step replaces held `keys` and `buttons`;
omitted sets are empty, while pointer position persists. `frames` determines hold
duration (1..120 per step, at most 600 total frames and 15 seconds).
`scroll` is bounded to -10..10, text to 256 characters per step and 2048 total,
and only mouse buttons 0..2 are accepted. Unknown fields are rejected.
`scroll` and `text` are pulses on the first frame of a step. Keys are
Unity `KeyCode` names. Text goes through the focused native input field's key handler, including its
validation and change callbacks, or an active `Input.inputString` consumer such
as Mods search. An unconsumed text pulse fails explicitly. `keys` represent
physical controls; use `text` for typed characters rather than relying on a
keyboard layout. Backspace, Return, KeypadEnter and Tab key-down edges also
produce their native control characters for `inputString` consumers. Use separate
steps when ordering edits and text matters.

For a drag, move to the start point before pressing, then supply at least two
moved samples while held. Native controls such as Scrollbar establish their drag
offset at the first movement and need the next movement to change value.

Sequences start on a subsequent frame and finish with a neutral release frame.
A completed sequence proves delivery only. Inspect UI, gameplay state and a
screenshot to establish the result. `centerHit` is a current raycast observation,
not a guarantee that the control will still be present when input executes.

The client generates a request ID and returns it even on transport failure. Inspect `ftk_input_status()` first after transport failure. Resubmitting the same
ID is deduplicated only in the same bridge session while that ID is retained in
the recent 64-request history. Restart or eviction removes that guarantee; do not
resubmit after either, and do not blindly generate a new ID after a timeout.
Status includes a session ID so a restart is observable. Only one
sequence may run at once. Use status or cancellation when a wait expires.
Main-thread work that expires before execution is cancelled without executing;
a timeout after execution starts reports an unknown outcome and must not be
retried automatically. The MCP client never retries a mutation automatically.

`GET /health` includes `protocolVersion` and `frameworkMvid` to identify the loaded
bridge. By default the game window must report focus, and losing focus fails the sequence and
clears native pointer state without activating a stale target. Automated runs use background
mode instead (next section) so they never need focus. The input status reports
whether native hooks installed successfully;
failed initialization leaves ordinary input intact and rejects synthetic input.
Requests require a Content-Length and are limited to 256 KiB.
The environment and loopback gates remain in force. Input also checks the native
network state every frame and refuses online transitions or sessions.

### Background mode

For unattended background tests, also set `FTK_AGENT_INPUT_ISOLATED=1`. This
additional opt-in takes effect only with background mode and a successfully
initialized bridge. Patched game, Rewired keyboard/mouse, UI and non-agent
framework input consumers receive neutral keys/buttons/axes/text between native
input requests. The virtual pointer retains its last synthetic position, starting
at the screen center. Active requests still use the ordinary bounded native input
timeline. Completing, cancelling or failing a request does not restore hardware
reads while isolation remains enabled. The agent namespace and OS input are not
patched by this option.

`GET /input` reports `inputIsolated` (requested with background mode) and
`hardwareInputSuppressed` (the input bridge is available and isolation is active).
Require both before an isolated capture campaign. Without the new flag, existing
foreground and background hardware behavior is unchanged. The flag does not
claim isolation of input devices or native callsites outside the bridge's existing
patched read surface. Verify a fresh isolated session before relying on it for
unattended evidence. Run `python3 harness/test_native_input_isolation.py` for the
noisy-hardware wrapper checks and the NativeInput test project for timeline rules.

Set `FTK_AGENT_BACKGROUND=1` together with `FTK_AGENT_BRIDGE=1` to accept native input while the
game window is unfocused. This is the required mode for agent-driven play tests: the user keeps
working in other apps while the test runs. Synthetic input is injected at the game's own input
reads, so clicks, keys, hover, and text behave exactly as in focused play; only the OS focus
requirement is removed. The mode:

- sets `Application.runInBackground` so the player loop keeps running unfocused;
- clears Rewired's `ignoreInputWhenAppNotInFocus` once Rewired is ready (logged as
  `[agent] background mode: Rewired ignoreInputWhenAppNotInFocus True -> False`), because Rewired
  and its UI input module otherwise drop unfocused input;
- skips the focus guard. Status and `/ui` report `"background": true` and the real `focused` value.

The startup log line `[agent] native input ready: ...; background mode` confirms it. The game's
own focus flag still follows the OS, so mouse-wheel zoom and screen-edge scrolling stay inactive
while unfocused; drive the camera with keys or clicks instead.

A newly launched game activates its window once. Record the frontmost app before launching and
reactivate it only when the game itself is frontmost. Compare against the game copy's bundle
identifier, never against "anything other than the app I recorded": the user may switch apps
while the game starts, and a guard that reacts to any change takes focus away from them. On macOS:

```sh
front=$(osascript -e 'tell application "System Events" to get name of first process whose frontmost is true')
game_id=$(defaults read "$PWD/TweaksTest.app/Contents/Info" CFBundleIdentifier)  # this copy's app bundle
# ...launch the isolated copy with FTK_AGENT_BRIDGE=1 FTK_AGENT_BACKGROUND=1...
for _ in $(seq 1 80); do
  now_id=$(osascript -e 'tell application "System Events" to get bundle identifier of first process whose frontmost is true')
  if [ "$now_id" = "$game_id" ]; then
    osascript -e "tell application \"$front\" to activate"
    break
  fi
  sleep 0.5
done
```

After that, never activate or raise the game window. Read the screen through `ftk_screenshot`,
not a desktop capture.

Game-free protocol checks:

```sh
python3 -m unittest discover -s harness/tests -v
dotnet run --project FTKModFramework/Tests/AgentQueue -c Release
dotnet run --project FTKModFramework/Tests/NativeInput -c Release
```

Run the reproducible live smoke only against an already launched, authorized isolated copy in
background mode (or focused) at its fresh English title screen:

```sh
python3 harness/native_input_smoke.py --url http://127.0.0.1:8777 \
  --output scratch/native-input-smoke --start-run
```

The output directory must be new. This records protocol responses, screenshots,
loaded module/session identity, and passing assertions. `--start-run` creates a
disposable adventure through the native UI; omit it for menu/input checks only.
It never launches or stops a game and must not be pointed at real saves.

See [native input validation](NATIVE-INPUT-VALIDATION.md) for the tested build,
live results and remaining coverage limits.

### `ftk_wait_for` predicates

A restricted key expression over the `/state` snapshot. Examples:

- `phase==overworld`, `phase==combat`, `phase==victory`
- `phase!=menu`
- `signals.modalOpen==true`
- `day==3`, `map.realmId==42`
- `phase==combat OR phase==overworld`
- `inSession==true AND singlePlayer==true`
- `health` (waits only for the bridge to be reachable)

RHS literals coerce to bool/null/int/float, else string. You may not mix `OR`
and `AND` in one predicate. On timeout it returns
`{ok:false, timeout:true, last_snapshot, last_detail, ...}`.

### Removed legacy actions (protocol 3)

`ftk_act` is no longer an MCP tool. `POST /action` accepts only `native_input`,
`input_cancel` and `prepare_offline`; other names return `ok:false` without game
calls. No compatibility aliases or hidden fixture dispatch remain.

The removal includes direct movement, combat, inventory and dialog calls;
teleports and forced outcomes; automatic run/combat/dungeon drivers; dungeon
repair operations; and the marketplace button-invocation seam. Use `ftk_ui`,
`ftk_input`, state and screenshots to exercise and verify the native route.
Read-only combat readiness and dungeon state remain in `/state`; obsolete
`combat.driver` and `dungeon.driver` fields are removed.

[The retirement review](LEGACY-HARNESS-RETIREMENT.md) explains the decisions and
migration of dependent model-pipeline automation. Existing historical evidence
still describes its original build and cannot establish behavior of protocol 3.

## Install

```bash
cd harness
python3 -m pip install -r requirements.txt   # only the mcp SDK; HTTP is stdlib
```

Configure the bridge URL via env (defaults to `http://127.0.0.1:8777`):

```bash
export FTK_BRIDGE_URL="http://127.0.0.1:8777"
```

## Register with Claude Code

`.mcp.json` at the repo root already registers this server:

```json
{
  "mcpServers": {
    "ftk-agent": {
      "command": "python3",
      "args": ["harness/ftk_mcp_server.py"],
      "env": { "FTK_BRIDGE_URL": "http://127.0.0.1:8777" }
    }
  }
}
```

`args` is relative to the repo root (Claude Code launches MCP servers from the
project directory). On first use Claude Code will prompt to approve the
project-scoped MCP server. To verify the tools loaded:

```bash
claude mcp list
```

## The loop: launch and inspect a local verification scenario

Build and deploy the framework to an authorized isolated test copy, launch with
`FTK_AGENT_BRIDGE=1`, then use the bridge to inspect a selected single-player run.
Choose the registered content and checks for the feature under test; the harness does
not treat a previously bundled adventure as a default scenario.

## Safety

Three independent guards keep co-op safe:

1. **Env gate**: the bridge no-ops unless `FTK_AGENT_BRIDGE=1`.
2. **Loopback bind**: `HttpListener` binds `127.0.0.1` only (no firewall ACL,
   not reachable off-host).
3. **Action guards**: native input permits disconnected title/setup screens and offline
   single-player sessions, and checks network state each frame.
   `prepare_offline` requires disconnected title/setup before a run starts.
   Online transitions and co-op sessions are rejected by native input.

## Troubleshooting

- **`ftk bridge unreachable`**: the game is not running with
  `FTK_AGENT_BRIDGE=1`, or it crashed, or the port differs. Check
  `BepInEx/LogOutput.log` for the listening line. The bridge port can be
  overridden in-game via `FTK_AGENT_BRIDGE_PORT`; match it with `FTK_BRIDGE_URL`.
- **`screenshot 503`**: the bridge host or rendered frame is unavailable, or capture
  timed out. Title-screen capture is supported when the game is rendering.
- **unknown action**: old gameplay commands were removed in protocol 3. Use native
  input and inspect the result.
- **native input rejected by network guard**: use a disconnected title/setup screen
  or an offline single-player run. Online sessions are unsupported.
