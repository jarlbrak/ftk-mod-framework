# Legacy harness retirement

Protocol 3 removes all 40 legacy `/action` commands. The only mutation commands
are `native_input`, `input_cancel` and `prepare_offline`. The MCP surface replaces
the arbitrary `ftk_act` tool with explicit `ftk_prepare_offline`; input tools use
an internal transport function. Unsupported commands fail without game calls.

## Review and decisions

| Old surface | Critique | Decision |
|---|---|---|
| `start_run`, `list_adventures` | The driver bypassed native setup. The read-like listing could initialize and inject caches. | Remove driver and commands; native setup clicks and read-only state remain. |
| `move_to`, `end_turn`, `enter_tile` | Parallel gameplay paths with different preconditions; `enter_tile` reported success without doing anything. | Remove; drive native controls and inspect outcomes. |
| `set_target`, `choose_ability`, `set_focus`, `attack`, `combat_turn` | Direct calls bypassed input and had their own readiness/target handling. | Remove; retain read-only readiness information. |
| `resolve_turn`, `win_combat`, `force_win`, `auto_combat`, `auto_combat_turn` | Overlapping forced-outcome paths and a coroutine driver could make tests pass without playing combat. | Remove all aliases and the driver. |
| `use_item`, `equip_item` | Duplicated native inventory interactions; direct equip did not consume a combat turn. | Remove. |
| `select_choice`, `advance`, `dismiss_message`, `dismiss_dialog`, `marketplace_ui` | Direct callback invocation did not test pointer reachability or native interaction. | Remove, including the production Mods panel test seam and unused metric helpers. |
| `snap_to`, `snap_party`, `engage` | Teleport and encounter fixtures contaminated ordinary gameplay evidence. | Remove from the general harness. No renamed replacement cheat surface. |
| `enter_dungeon`, `advance_room`, `dungeon_encounter`, `cleared_room`, `dungeon_regen`, `dungeon_scroll_complete` | Synthetic entry needed repair commands for state it had bypassed. | Remove entry, progression and repair operations together. |
| `force_clear`, `clear_dungeon`, `quest_advance`, `force_victory`, `show_endgame` | Manufactured completion rather than proving traversal, quest logic or victory. | Remove, including the dungeon driver. |
| `combat_status`, `dungeon_debug`, `session_debug`, `quest_info` | Read-only commands embedded in the mutation dispatcher overlapped state observations. | Remove command variants; retain `/state`, `/ui` and screenshots. |
| `ftk_observe`, `ftk_ui`, `ftk_screenshot`, `ftk_wait_for` | Independent observation and bounded waiting are still required to prove input outcomes. | Keep. |
| Offline setup, loopback/env/network guards, queue and JSON transport | Required to run controlled native tests and return reliable receipts. | Keep. |

`CombatDriver`, `DungeonDriver`, `StartRunDriver` and `DungeonOps` are deleted.
State snapshots no longer expose their `combat.driver` or `dungeon.driver`
fields. The small remaining adventure cache helper only reads an already built
cache. Combat readiness probes are read-only and local to `StateReader`.

## Dependent automation

Legacy model-pipeline modes that depended on direct start, dungeon entry, combat
or turn operations must not fail halfway through a mutation or capture. Those
execution modes now reject before starting work, with native preparation guidance.
Observation-only capture and independent, explicitly scoped model fixtures remain.
Exact-target native combat automation has not been substituted with guessed
coordinates. A future native scenario runner needs its own verified interaction
and outcome checks.

This is an intentional breaking test-tool contract. Existing scripts must use
native input and observations; there is no legacy fallback. Historical evidence
snapshots remain unchanged and describe their original builds. See the active
model-pipeline guides for the supported capture and staging modes.

## Verification

The live smoke exercises every removed action name and requires an explicit
rejection, then exercises native UI/input and starts a run. See
[native input validation](NATIVE-INPUT-VALIDATION.md) for build identity and
executed checks. Removal does not establish untested platform or gameplay routes.
