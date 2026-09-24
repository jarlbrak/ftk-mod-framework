# Reporting a problem

Direct sending requires a deployed reporting service and a matched framework/helper
build. The expanded log dump and draft manager described here require their own
live qualification before release.

Open **Options > Report Bugs** from the title screen or an active game. Add a short
description if you can, then choose **Send report**. A GitHub account, browser form,
or manual file attachment is not required. The game shows the issue number only
after receiving a validated submission receipt. **View issue** opens that issue.

Diagnostics are selected by default for new reports. Choose **What will be sent?** to inspect the
outgoing content, or turn off **Include diagnostics** to send only your description
and report identifiers. A saved draft remembers your diagnostics choice, including
an explicit opt-out, when you reopen it. Opening the panel, saving a draft, or
receiving an offer uploads nothing. Each report requires its own explicit Send action.

## What is collected and shared

The framework keeps bounded local metadata and process log snapshots. With diagnostics
enabled, Send uploads versions, mod inventory and registration context, session
context, and a recent filtered game/framework log dump through the Railway service. The
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

## Automatic offers

A detected error can offer a report at the title screen or when opening Options.
During play, offers wait for Options instead of interrupting combat. Repeated errors
are deduplicated and offers are throttled. Detection means an error was logged; it
does not prove which mod caused it or detect every gameplay bug.

After an unexpected exit, the next eligible title screen offers the previous
session's saved metadata and matching process log snapshot. Force quit and power loss
can also produce this offer, so it is not labelled proof of a crash. Abrupt exit can
lose recent entries since the last successful log checkpoint, normally taken every
two seconds. **Not now** dismisses the offer without
uploading anything. Previous-session data is never replaced by unrelated current
session logs.

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
service reconciles uncertain results before returning a receipt. There is no silent
background upload on the next launch. Choose Retry explicitly.

The frozen outgoing copy is separate from editable drafts. Its description,
diagnostics choice and captured content cannot change during retries. If a previous
submission is waiting, the panel handles that pending copy first; use its **Retry**
or **Discard local copy** controls. A draft corresponding to that pending submission
cannot be deleted through the draft list while it is pending.

The local outgoing copy expires after seven days. **Discard local copy** removes
the pending copy and any corresponding local draft; it cannot delete an issue that
already reached GitHub. Other saved drafts remain available.

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
