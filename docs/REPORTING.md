# Reporting a problem

This guide describes the direct-send reporting implementation under development.
A deployed service and a matched framework/helper build are required. It has not
yet passed the live game-to-Railway-to-GitHub qualification gate.

Open **Options > Report Bugs** from the title screen or an active game. Add a short
description if you can, then choose **Send report**. A GitHub account, browser form,
or manual file attachment is not required. The game shows the issue number only
after receiving a validated submission receipt. **View issue** opens that issue.

Diagnostics are selected by default. Choose **What will be sent?** to inspect the
outgoing content, or turn off **Include diagnostics** to send only your description
and report identifiers. Opening the panel or receiving an offer uploads nothing.
Each report requires its own explicit Send action.

## What is collected and shared

The framework keeps bounded local metadata and error snapshots. With diagnostics
enabled, Send uploads versions, mod inventory and registration context, session
context, and recent filtered errors through the framework's Railway service. The
service creates a **public GitHub issue** in `jarlbrak/ftk-mod-framework`, includes a
diagnostic excerpt, and links a public JSON download. There are no client-side
GitHub credentials. The [service source and deployment instructions](../reporting-service/README.md)
are in this same repository.

The error snapshot contains errors, exceptions and asserts observed after the
reporting subsystem starts. It is bounded, can be truncated, and is not a complete
BepInEx or game log. Informational/debug logs, saves, screenshots and native crash
dumps are not collected. Early startup failures may occur before capture begins.
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
session's saved metadata and matching error snapshot. Force quit and power loss
can also produce this offer, so it is not labelled proof of a crash. Abrupt exit can
lose the latest two seconds of error capture. **Not now** dismisses the offer without
uploading anything. Previous-session data is never replaced by unrelated current
session logs.

## If sending fails

A report is frozen and saved locally before transmission. Retry sends those same
bytes with the same ID. A timeout does not mean that no issue was created; the
service reconciles uncertain results before returning a receipt. There is no silent
background upload on the next launch. Choose Retry explicitly.

The local outgoing copy expires after seven days. **Discard local copy** removes
that copy only; it cannot delete an issue that already reached GitHub. If another
local report is waiting, the panel handles it first and keeps the newer report in
memory for afterwards. Keep the panel/game open to preserve that unsent newer report.

## Maintainer verification

Game-free tests cover bounded capture, redaction, exact-session provenance, opt-out,
local helper transport, durable retries, duplicate prevention, public downloads and
expiry. They do not establish live UI appearance, Steam-launch compatibility,
Railway connectivity or real GitHub issue creation. A release must separately prove
those paths with an identified synthetic test report and downloaded diagnostics.

Earlier [browser handoff contracts](REPORTING-CONTRACT.md) and
[evidence records](evidence/reporting-browser-proof.md) describe the preceding
implementation. They are not evidence for the direct-send transport.
