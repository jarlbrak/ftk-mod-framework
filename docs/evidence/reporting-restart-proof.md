# Automatic metadata and restart offer integration

## Scope and binary identity

Observed on 2026-09-24 UTC in an isolated macOS copy of FTK 1.1.00, Unity
2017.2.2p2, BepInEx 5.4.23.5. The proof disabled Steam initialization and redirected
saves to its own root. Production reporting ran with `FTK_REPORTING_NATIVE=1`;
the proof did not replace its native Report Bugs interception. Another running
FTK process was excluded from deployment, input and process signals.

Final framework SHA-256: `7dd148738536996604d02e179319c742431fcc6290850772523b72a778db3c3f`.
Proof SHA-256: `498f8a7967dabc8f0846a09ff070d7977e7ffa6751e00290d5e1ec344e111a47`.
Steam guard SHA-256: `005c6e8c2e0803be027bf836b4ab23050ed45fd64dbbe50488025a4baa6a4854`.

## Observations

- A normal `Application.Quit` followed by launch reached the native title with
  tracking available and no pending incident.
- Native Options > Report Bugs opened the framework editor with metadata already
  attached. The capture included game/framework/Unity versions and explicit
  unavailable inventory sources. This installed BepInEx version is outside the
  adapter's audited plugin-inventory version, so that inventory stayed unavailable.
- Programmatic text edits invalidated review. An 8193-character attempt remained
  in the actual InputField, with review/copy disabled; shortening it restored
  review. Back restored Options and cleared the blocker. Reopening retained
  report/capture identities, metadata and narrative, while requiring review again.
- A signal terminated only the executable/PID verified as the owned test process.
  The next launch presented an unexpected-exit offer after title readiness.
  Review retained the pending session/checkpoint identifiers and separate previous
  and current metadata. Review did not acknowledge the incident. Back restored
  the title and removed the report blocker.
- After review and normal quit, the next launch offered the unacknowledged
  incident again. Dismiss restored the title. A further normal quit/relaunch
  reached the title with tracking available and no pending incident.
- Targeted keyboard input entered a synthetic description through the native
  InputField. On the final build, keyboard Down/Return advanced the preview,
  Up/Return reversed it, and Escape returned to Options with no report blocker.
  Full keyboard completion/navigation and mouse routing remain unqualified. The
  coordinate-click tool failed before dispatch; editor assertions used native
  fixture calls.
- The final prompt screenshot showed an opaque, readable owned panel. Earlier
  runs exposed parent CanvasGroup dimming, corrected with an owned group that
  ignores parent groups. No native group was modified.

The complete editor assertions were repeated successfully on the final build.
The initial editor assertions ran on framework hash
`0edf00a8107d74895fba54503ad3ec453dedb9d49941f19b7fecb1246c1619b0`; the final
build removes one duplicate navigation refresh and isolates panel opacity.
The forced exit was from that earlier binary and recovery used the final binary.
The owned game process was stopped normally after testing. The protected FTK
process retained its executable identity and original start time. Raw receipts,
captures and logs remain outside committed evidence. One startup
spent several minutes loading before reaching the native warning and title; no
reporting exception was observed, and this does not establish a startup budget.

## Game-free verification and remaining gates

Release build passed with seven existing EnemyVisualPatch field warnings.
Metadata, session-store, runtime normal/busy-owner and PlayerMods checks passed.
The linked-source checks and standalone shipped-Mono ownership evidence have
different scopes from this native Unity run.

Remaining gates include complete mouse/keyboard/controller navigation, interaction
blocking under the owned CanvasGroup, clipboard contents, active solo/combat and
co-op, scene teardown, Windows/Linux ownership, startup/capture frame and allocation
budgets, log collection, durable editor drafts and GitHub/browser handoff.
An unexpected exit cannot distinguish a crash from force quit or power loss.
Coverage begins after the worker acquires its lease; shutdown observation is
bounded and cannot prove that every later part of process termination succeeded.
This evidence does not close the epic or its specifications.
