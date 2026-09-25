# Native input validation

## Current protocol 3 retirement verification

The September 24, 2026 retirement build has framework SHA-256
`62413ed887683595ec842f88acb385930cb33ed6be528bedfbdf9e39947e5159`
and loaded MVID `c935f675-93d4-4848-b68a-798bf65e0667`. Build and deployed bytes
matched. It used the same isolated installation and platform described below.

The expanded smoke passed all 60 assertions: explicit rejection of all 40 retired
commands followed by the 20 native-input checks, including native adventure
creation. A final state read confirmed a single-player session and absence of
the removed driver fields. Local evidence is in `scratch/input-retirement-verified`.
An initial smoke attempt before bridge startup failed with connection refused;
the complete rerun began after startup and passed. The owned game process was
stopped after verification.

The framework Release build passed with seven existing warnings, and all 13
Python client checks passed, including the reduced MCP surface and explicit
offline configuration. The PlayerMods suite, 41 NativeInput checks and AgentQueue
suite (509 checks, including 200 race iterations) passed. Protocol 3 intentionally removes the legacy fixture used
for the earlier combat check below. That check is historical evidence for its
identified protocol 2 binary, not a new combat result on the retirement build.

The updated model-pipeline Python suite passed 222 tests. Retired CLI routes
returned an actionable error before accessing a nonexistent root, including when
given legacy options. The isolated runtime helper built with zero warnings and
errors after diagnostic text updates; that diagnostic-only helper change was not
deployed or live-tested. Its scoped fixture behavior was unchanged.

See [retirement decisions and migration](LEGACY-HARNESS-RETIREMENT.md).

## Original protocol 2 input verification

Validated on September 24, 2026, using a disposable macOS game copy with an
isolated save namespace and application preferences. The installed Steam game
and its saves were not used for mutations. The owned test process was stopped
after verification. Deployment remains in the disposable copy.

### Build identity

| Component | Verified identity |
|---|---|
| Host | macOS 26.6.2, arm64 |
| Game | For The King 1.1.00.11378, Steam, Unity 2017.2.2p2 |
| Game Assembly-CSharp SHA-256 | `94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8` |
| Framework SHA-256, build and deployed | `2d4bf3d9b886b05db82e20c0e5fe9a92ec78cacac3dd92926c7834fe150f828f` |
| Loaded framework MVID | `d4b7ae38-b676-4d0f-a19a-e648409ba4a8` |
| Bridge protocol | 2 |

The loaded bridge reported 91 patched methods: 77 game, eight Unity UI, two
framework UI, two Rewired hardware readers and two lifecycle/input-field hooks.
Installed assembly inspection established the hardware polling, action cache,
native input-field editing and pointer cancellation paths.

### Game-free verification

- Framework Release build passed, with seven existing EnemyVisualPatch warnings.
- NativeInput tests passed 41 checks covering validation, frame progression,
  edges, pulses, neutral release and editing control characters.
- AgentQueue tests passed, including 200 timeout/execution race iterations.
  Assertion totals vary with the race outcomes; the recorded run passed 567.
- Python client tests passed all 11 tests, covering polling, terminal failures,
  timeout identity, deduplication and no automatic mutation retry.
- Python compilation and `git diff --check` passed.

### Live verification on the identified framework

The reproducible [smoke runner](native_input_smoke.py) passed 20 assertions:
hook installation, framework Mods search text and Backspace, native click and
Escape, duplicate request suppression, invalid-plan rejection, overlap rejection,
cancellation without a stale click, recovery, disconnected offline configuration,
campaign scrolling, a scrollbar drag, character-name editing and Return, and
starting a single-player adventure through native UI clicks.

Additional live observations established:

- The configured held camera key, mouse wheel and middle-button drag changed the
  overworld camera after startup dialogues were dismissed.
- The inventory key selected a native quick-use item control. An exploratory
  assertion expecting the visible item count to change failed: the native action
  focuses that control, so visible-count changes are not its contract.
- A native attack click dealt six damage, reducing Beastman HP from 11 to five.
  Combat entry used the existing `engage` fixture with party placement. This
  proves native attack delivery in that fight, not encounter traversal or a
  complete combat playthrough.
- Text with no active consumer failed explicitly. An OS-delivered Escape then
  opened the native pause menu, verifying ordinary input after the failed request.
- With the bridge environment flag removed, the same framework loaded and
  completed startup without bridge initialization or a listener on the test port.

Local protocol traces, screenshots, checks and identity receipts are retained in
ignored `scratch/input-verified-smoke`, `scratch/input-verified-controls`,
`scratch/input-verified-combat`, `scratch/input-verified-rejection` and
`scratch/input-disabled-check.json`. Game assets and logs are not committed.

## Coverage limits

This is operational offline single-player input on the tested macOS build, not
exhaustive coverage of every screen or gameplay route. Windows and Linux live
checks remain outstanding. A real focus-loss transition could not be induced by
the available background automation, so that fail-safe is source-reviewed but
not live-verified. The 15-second cutoff was observed on an earlier build before
the final framework text-input change, not repeated on the identified final build.
Online and co-op input are intentionally rejected; no multiplayer claim is made.

Use the [documented input protocol](README.md#native-input) and inspect observable
state after delivery. A completed input receipt alone does not prove an intended
gameplay outcome. The smoke runner requires an already launched, focused,
authorized disposable game at its English title screen.
