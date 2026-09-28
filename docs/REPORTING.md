# Reporting a problem

Direct sending requires a deployed reporting service and a matched framework/helper
build. The [isolated macOS direct-send trial](evidence/reporting-direct-send.md)
created real issues through the game panel. Expanded log dump, draft management,
physical input routes and other platforms retain the limits in that receipt.

Open **Options > Report Bugs** from the title screen or an active game. Add a short
description if you can, then choose **Send report**. A GitHub account, browser form,
or manual file attachment is not required. The game shows the issue number only
after receiving a validated submission receipt. **View issue** opens that issue.

**Automatic bug reports** are on by default under **Mods > Settings & Help**.
Detected errors and unexpected previous exits send filtered diagnostics to the same
public GitHub tracker without opening the report panel. Turn the setting off to use
manual reporting only. The installation remembers the 32 most recent successful
automatic error signatures for 30 days to avoid repeated reports. A failed
automatic submission keeps its exact local payload and retries at most once per
later launch for seven days; it does not replace a pending manual report.
Turning the setting off stops new automatic sends; a send already in progress may
finish. The manual reporting panel remains available in either setting.

Diagnostics are selected by default for new reports. Choose **What will be sent?** to inspect the
outgoing content, or turn off **Include diagnostics** to send only your description
and report identifiers. A saved draft remembers your diagnostics choice, including
an explicit opt-out, when you reopen it. Opening the panel, saving a draft, or
opening the manual editor uploads nothing. Manual reports require an explicit Send action.
The panel opens with the description selected, or another control that cannot send when
the description is hidden, so a single confirm press never sends or retries a report.
Switching views selects a safe control too: the first draft's **Open** (or **Back to report**)
in Drafts, **Keep editing** when asked to save, and **Keep draft** when confirming a deletion.

## What is collected and shared

The framework keeps bounded local metadata and process log snapshots. With diagnostics
enabled, Send uploads versions, mod inventory and registration context, framework
tweak settings, session context, and a recent filtered game/framework log dump through the Railway service. The
service creates a **public GitHub issue** in `jarlbrak/ftk-mod-framework`, includes a
short diagnostic excerpt, and links both a public diagnostic JSON download and a
readable **.log** download. The downloads retain the uploaded log dump, beyond the
short excerpt displayed in the issue. There are no client-side
GitHub credentials. The [service source and deployment instructions](../reporting-service/README.md)
are in this same repository.

The log snapshot includes informational messages, warnings, errors and other
messages observed from this game's Unity and BepInEx logging after reporting starts.
It retains up to **128 KiB of UTF-8 text per session**, keeping the recent tail when
older entries must be dropped. A correlated previous-session capture can add another
128 KiB. The report envelope is bounded to 2 MiB, including JSON encoding and metadata.
This is not a complete on-disk BepInEx or game log: the reporter does not scan shared
log files or logs from another running game. Saves, screenshots and native crash
dumps are not collected. Early startup messages may precede capture. Older saved
reports without logs cannot recover them later, and reopening a saved draft does not
replace its diagnostic snapshot with the current game's logs.
Redaction filters common credentials, account identifiers, addresses and home-folder
names, but arbitrary mod messages may still contain personal information. Review
the preview or exclude diagnostics if needed. Your typed description is public too.

Downloads and the service's stored report payload expire after 30 days. GitHub issue
text and its diagnostic excerpt remain public until removed by a maintainer; public
copies cannot be recalled. Minimal service receipts remain to prevent duplicate
issues. Hosting providers process network requests, including the connecting IP;
the service uses addresses for rate limits and does not log report bodies or tokens.
See the deployed service's `/privacy` page for the same disclosure.

## Automatic detection

Automatic reports use a generated description and the same bounded filtered logs and
metadata as a manual report. The public issue text identifies an automatic submission.
The error detector deduplicates and throttles reports within a session. Detection
means an error was logged; it does not prove which mod caused it or detect every
gameplay bug. The manual panel remains available for context that logs cannot tell us.

An unexpected-exit report includes the previous session's saved metadata and matching
process log snapshot. Force quit and power loss look the same, so this is not proof
of a crash. Abrupt exit can lose recent entries since the last successful log
checkpoint, normally taken every two seconds. Previous-session data is never replaced
by unrelated current-session logs. The incident is acknowledged after a confirmed send.

## Stuck-turn snapshots

When a turn seems stuck, send a report from **Options > Report Bugs**. The log dump may
already hold a `STUCK-TURN` line describing the stall
([Spec #265](https://github.com/jarlbrak/ftk-mod-framework/issues/265) FR-1). The framework
writes one such Warning line to `LogOutput.log`:

- when it is this machine's overworld turn and **End Turn** stays unavailable for 20 seconds
  with nothing on screen explaining it (`reason=unexplained-20s`);
- when it stays unavailable for 90 seconds behind the location menu, inventory, a global
  message or a portrait message (`reason=panel-90s`);
- on the host, when a client acknowledgement is still pending after 15 seconds
  (`reason=host-ack-15s`).

For the turn lines, time does not count while Options is open or chat has focus, and
nothing is written during combat or after a disconnect has ended the run. Each stall writes
at most one line, and one launch of the game writes at most ten of each kind. The line is a
Warning, so it never raises the automatic error report or a report prompt. It records only the game's own state: the
Movement, turn and game-flow state names, the gate's panel flags, the input focus, the
popup wait and whether the HUD canvases accept input, the pending acknowledgement IDs, the coordinated message, the world-update
flags, and each character's dungeon, combat and action-point state. It is one line of
`key=value` pairs under 2 KB, for example (state names illustrative):

```text
STUCK-TURN reason=unexplained-20s waited=20.0s master=1 mode=SinglePlayer myTurn=1 turn=0 endTurns=7 button=0 gate=1 movement=Moving movementSub=OnStopAtHex:Wait turnEngage=Idle gameFlowMC=Wait_Turn panels=loc:0,inv:0,global:0,portrait:0,spectator:0 options=0 chat=0 aborted=0 popupWait=1 focus=uiPlayerMainHud/HUD uiInput=other:11,main:11 acks=none message=None/from:1 logicUpdating=gl:0,mc:0 camera=1 encounter=menu:0,combat:0 cows=t0:dungeon0,combat0,ap2
```

A field the framework cannot read shows `unavailable`. The watchdog changes nothing in the
game. To turn it off, set `StuckTurnWatchdog = false` under `[Diagnostics]` in
`BepInEx/config/com.ftkmf.framework.cfg`; it is read at startup.

## Save and manage drafts

Choose **Save draft** to keep the report on this computer, then use **Drafts** to
reopen, edit or delete it. The list holds up to ten drafts within its local storage
budget. Drafts expire seven days after they are saved. If storage is full, delete
an older draft and save again; an unsuccessful save leaves the editor open.

Saved drafts preserve the description, diagnostics choice, report identifiers and
original metadata/log snapshots, including a matching previous session when present.
Editing the description or diagnostics choice does not recapture logs from a different
session. New reports start with diagnostics enabled; a reopened draft retains its
saved choice. Saving never sends a report.

When leaving an editor with unsaved changes, choose **Save and continue**,
**Discard edits**, or **Keep editing**. Discarding edits restores the last saved
version when one exists. Deleting a draft removes that local draft only and cannot
remove an issue already posted on GitHub.

## If sending fails

A report is frozen and saved locally before transmission. Retry sends those same
bytes with the same ID. A timeout does not mean that no issue was created; the
service reconciles uncertain results before returning a receipt. Manual reports
retry only after the player chooses Retry in the editor.

The frozen outgoing copy is separate from editable drafts. Its description,
diagnostics choice and captured content cannot change during retries. If a previous
submission is waiting, the panel handles that pending copy first; use its **Retry**
or **Discard local copy** controls. A draft corresponding to that pending submission
cannot be deleted through the draft list while it is pending.

The local outgoing copy expires after seven days. **Discard local copy** removes
the pending manual copy and any corresponding local draft; it cannot delete an issue
that already reached GitHub. Other saved drafts remain available.

Automatic reports use a separate local queue. While automatic reporting is enabled,
a saved automatic payload retries at most once on each later launch for up to seven
days. Turning the setting off pauses these retries; the payload remains local until
it expires or the setting is turned back on. Every attempt uses the same report ID
and bytes.

## Maintainer verification

Game-free tests cover bounded capture, redaction, exact-session provenance, opt-out,
local helper transport, durable retries, duplicate prevention, useful log dumps
larger than the former transport limit, readable log/JSON downloads, draft editing
and snapshot preservation, opt-out persistence, deletion and expiry.
They do not establish live UI appearance, Steam-launch compatibility,
Railway connectivity or real GitHub issue creation. A release must separately prove
those paths with an identified synthetic test report and downloaded diagnostics.
Earlier live direct-send reports do not establish the expanded log dump or draft
manager behavior in a later build.

Earlier [browser handoff contracts](REPORTING-CONTRACT.md) and
[evidence records](evidence/reporting-browser-proof.md) describe the preceding
implementation. They are not evidence for the direct-send transport.
