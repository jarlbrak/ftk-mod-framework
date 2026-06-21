# FTK Agent Harness (MCP server)

An agentic test harness that lets a live Claude session **play For The King** to
verify custom content. It is the client half of a two-part bridge:

```
Claude session  <-- stdio MCP -->  ftk_mcp_server.py  <-- loopback HTTP -->  in-game bridge (BepInEx plugin)
```

- **In-game bridge**: `FTKModFramework.Agent.AgentBridge`, shipped inside
  `FTKModFramework.dll`. It exposes a tiny loopback HTTP API
  (`/state`, `/action`, `/screenshot`, `/health`) and only starts when the game
  is launched with env `FTK_AGENT_BRIDGE=1`. It binds `127.0.0.1` only and is
  **single-player test use only** (direct calls can desync co-op).
- **This server** (`ftk_mcp_server.py`): a stdio MCP server (official `mcp`
  Python SDK / FastMCP) that wraps that HTTP API as four tools. HTTP calls use
  only the Python standard library (`urllib`); the only dependency is `mcp`.

## Tools

| Tool | Wraps | Purpose |
|---|---|---|
| `ftk_observe()` | `GET /state` | Full observe snapshot (phase, party, combat, map, choices, signals). The primary read. |
| `ftk_act(action, args)` | `POST /action` | Perform a verified game action. Returns `{ok, error, result}`. The primary write. |
| `ftk_wait_for(predicate, timeout_s=60, poll_s=1.0)` | `GET /health` then `GET /state` | Poll until a predicate over the snapshot holds (keeps the loop from busy-spinning). |
| `ftk_screenshot(save_path=None)` | `GET /screenshot` | Capture a PNG to disk; returns `{ok, bytes_len, path}`. |

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

### Action set (`ftk_act`)

Grouped by phase. All are fully defensive (a missing precondition returns
`{ok:false, error:"at <action>.<step>: ..."}` and never throws into the game).

- **Run / flow**: `start_run {adventure?}` (autonomous title -> in-world),
  `list_adventures`, `dismiss_message` / `dismiss_dialog`, `select_choice {index}`,
  `advance`, `enter_tile`, `end_turn`.
- **Overworld**: `move_to {big,small}`, `snap_to {big,small}`, `engage` (snap onto
  the nearest enemy POI and start an overworld fight).
- **Combat**: `set_target {enemyFid}`, `choose_ability {profId}`, `set_focus {n}`,
  `attack`, `resolve_turn {attackerFid?,targetFid?,profId?,hit?}`, `combat_turn`,
  `force_win` / `win_combat` / `auto_combat` / `auto_combat_turn`, `combat_status`.
- **Dungeon**: `enter_dungeon {dungeon?}`, `dungeon_encounter` (start the current
  room's fight via `MiniHexDungeon.Encounter`), `cleared_room` (advance the room
  pointer), `force_clear` (deactivate a cleared dungeon), `dungeon_scroll_complete`
  (init player dummies + force the deferred dungeon-scroll ack). Introspection:
  `dungeon_debug`, `dungeon_regen`, `session_debug`.
- **Quest / victory**: `quest_info`, `quest_advance` (force-complete the current
  quest + advance), `force_victory` (force-complete the questline + fire the
  end-game), `show_endgame` (drive the credit/CONGRATULATIONS screen directly).

`resolve_turn` / `force_win` are the deterministic kill paths (they drive
`DamageCalculator.StartEngageAttack`), preferred over `choose_ability`/`attack`
when the combat FSM may stall. They only commit while the active hero dummy is in
the FSM state `"Wait For Stance"` (the authoritative readiness gate).

### In-dungeon combat and true-victory (verified findings)

Driving the boss-in-crypt design to a real victory required solving three
decompile-verified gates (full RE in ninum `kb_c5ca36f8`, `kb_d8b843fd`):

1. **Start the fight with the right mechanism.** Inside a `MiniHexDungeon`, the
   overworld `engage` path (`GameFlow.LocalInitCombatSession`) builds an
   `EncounterLocation.Overworld` session whose round loop never starts. The correct
   trigger is `MiniHexDungeon.Encounter(fid, null)` (`EncounterLocation.Dungeon`) ->
   `dungeon_encounter`.
2. **Populate the rooms.** The bridge's synthetic `OnLoadParty` entry silently
   fails `GenerateDungeonEncounters`, leaving the room list empty; `dungeon_regen`
   re-runs it (the Flooded Crypt is 2 levels x 6 rooms; the boss is level 1 room 4).
3. **Fire the deferred ack.** The dungeon round loop is gated behind the
   `m_DungeonFlow` camera-scroll FSM; `dungeon_scroll_complete` replicates its
   `InitPlayerDummiesForCombat` + `DungeonScrollComplete` so `CommenceBattle` runs
   and the hero reaches `"Wait For Stance"`.

For victory: the active quest is `GameEventManager.GetCurrentQuest()` (not
`GameLogic.GetQuestByID(m_QuestID)`, an allocation counter). `quest_advance`
force-completes the arrive quest so the clear-crypt `DungeonQuest` (the last quest)
is active; clearing the crypt deactivates its POI (the `IsCompleteState` condition);
`force_victory` / `show_endgame` then fire the end-game CreditScreen
(`/state.signals.victoryShowing==true`), honoring `m_EndGameAfterLastQuest`.

**Known limitation**: enemy-first combat turns stall under the synthetic entry (the
dungeon camera FSM leaves the diorama black so the enemy cannot animate). Combat is
winnable when the player wins initiative; the crypt-clear-to-victory path does not
depend on it.

## Install

```bash
cd /Users/tbrack/Documents/Projects/FTK/harness
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
project-scoped MCP server. To verify the four tools loaded:

```bash
claude mcp list
```

## The loop: launch and run the D1 verification scenario

D1 is the bespoke custom realm + boss adventure ("Hollow Mire") used to prove
the harness. Build, deploy, launch with the bridge enabled, then drive a
playthrough to victory.

### 0. Build and deploy the framework DLL

```bash
cd /Users/tbrack/Documents/Projects/FTK/FTKModFramework
dotnet build -c Release
# copy bin/Release/net35/FTKModFramework.dll into <game>/BepInEx/plugins/
```

### 1. Launch For The King with the bridge enabled

Launch the game with env `FTK_AGENT_BRIDGE=1` so the in-game bridge starts
(e.g. from a shell that has the env set, or via Steam launch options). Confirm
in `BepInEx/LogOutput.log`:

```
FTKAgentBridge listening on http://127.0.0.1:8777/
```

With the env unset, no listener/thread/GameObject is created and shipped
behavior is byte-identical.

### 2. Start the MCP server / open the Claude session

Start Claude Code in the repo so it picks up `.mcp.json`. The agent then runs an
observe-decide-act loop using the four tools. A representative scenario:

1. **Liveness**: `ftk_wait_for("health")` until `/health` is ok.
2. **Oracle short-circuit** (no gameplay needed): grep
   `BepInEx/LogOutput.log` for `SELF-TEST PASS [realm-boss]` and assert no
   `SELF-TEST FAIL`. This proves D1 content registered.
3. **At menu**: `ftk_observe()` -> expect `phase==menu`, `inSession==false`.
4. **Start the run**: select the D1 custom adventure and start a single-player
   run (`ftk_act("start_run", {...})`, or drive the start menu via
   `ftk_act("select_choice", {index})` over `choices[]`). Assert
   `singlePlayer==true` before any further action; the bridge rejects acts
   otherwise.
5. **Overworld**: `ftk_wait_for("phase==overworld", 120)`. `ftk_observe()` ->
   confirm `party[0].realmId == map.realmId ==` the D1 Hollow Mire realm id
   (cross-check the realm name in the snapshot). Record `currentTurnFid`.
6. **Navigate to the boss tile**: read `map.neighbors` and the party hex, then
   `ftk_act("move_to", {big,small})` toward the boss POI (or
   `ftk_act("snap_to", {big,small})` for deterministic placement). After each
   move, `ftk_wait_for("phase==overworld")` to let the per-hex FSM settle.
7. **Combat**: on reaching the boss tile combat fires.
   `ftk_wait_for("phase==combat", 60)`. `ftk_observe()` -> assert `enemies[]`
   contains the D1 boss, `fightOrder` non-empty, `abilities[]` listed.
8. **Combat loop** until `combat.liveEnemies==0`: each player turn ->
   `ftk_act("set_target", {enemyFid})`, then a deterministic kill via
   `ftk_act("resolve_turn", {attackerFid, targetFid, hit:1.0})` (or
   `choose_ability`/`attack`). For enemy turns, just re-observe; the engine
   resolves them. `ftk_wait_for("phase==combat OR phase==overworld")`.
9. **Victory**: the boss quest is the last quest of the last stage, so its
   completion arms victory. `ftk_wait_for("signals.modalOpen==true OR phase==victory")`,
   then `ftk_act("advance")` / `ftk_act("select_choice", {index})` to clear the
   end-game modal.
10. **Assert victory**: `ftk_observe()` -> `phase==victory` AND
    `signals.victoryArmed==true` AND `questComplete==true`. `ftk_screenshot()`
    to capture the victory screen. **PASS** = `SELF-TEST PASS [realm-boss]`
    present AND `phase==victory` reached by direct-call play.

> Note (current D1 design): the boss is no longer an overworld set-piece, it is
> the culmination of the Flooded Crypt. So steps 6-8 are not overworld navigation
> to a boss tile but `enter_dungeon` -> (`dungeon_regen`) -> per-room
> `dungeon_encounter` + `dungeon_scroll_complete` + `force_win`, and step 9 is
> `quest_advance` to the clear-crypt quest then `force_victory` / `show_endgame`.
> See "In-dungeon combat and true-victory (verified findings)" above for the
> reliable sequence and the enemy-first-turn limitation.

## Safety

Three independent guards keep co-op safe:

1. **Env gate**: the bridge no-ops unless `FTK_AGENT_BRIDGE=1`.
2. **Loopback bind**: `HttpListener` binds `127.0.0.1` only (no firewall ACL,
   not reachable off-host).
3. **Per-action single-player check**: every `/action` first verifies
   `GameLogic.Instance.IsSinglePlayer()` and returns `{ok:false}` otherwise, so
   Photon co-op state is never perturbed.

## Troubleshooting

- **`ftk bridge unreachable`**: the game is not running with
  `FTK_AGENT_BRIDGE=1`, or it crashed, or the port differs. Check
  `BepInEx/LogOutput.log` for the listening line. The bridge port can be
  overridden in-game via `FTK_AGENT_BRIDGE_PORT`; match it with `FTK_BRIDGE_URL`.
- **`screenshot 503`**: no active game session yet; reach `phase==overworld`
  first.
- **action `{ok:false, error:"not-single-player"}`**: you are in co-op; the
  bridge refuses to act. Use a single-player run.
