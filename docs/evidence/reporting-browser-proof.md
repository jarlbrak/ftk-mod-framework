# Reporting browser proof preparation

Spec A3 for [epic #161](https://github.com/jarlbrak/ftk-mod-framework/issues/161).
Prepared on 2026-09-23 against repository baseline `40529301`. **Live browser
feasibility is not yet proved.** The designated test repository and ordinary
non-maintainer account/session are still prerequisites. No issue has been posted,
no file uploaded, and no production form changed by this preparation.

## Verified documentation, not observed browser behavior

GitHub documents custom issue-form field prefills using URL parameters, template
selection, permission-dependent parameters and server rejection of excessive URL
lengths. This supports testing the proposed route; it does not establish a
universal safe limit or login preservation. The proof uses D's conservative
6000-byte whole-URL product cap. Runtime query plans omit labels, assignees,
projects and milestones. [Creating an issue](https://docs.github.com/en/issues/tracking-your-work-with-issues/using-issues/creating-an-issue).

The form schema supports attachments in textareas, but `render` disables that
editing/attachment expansion. Required-field validation is documented for public
repositories, so use a designated **public** synthetic test repository for that
trial. Retain required summary, repro and expected/actual fields; diagnostics
remains optional and has no `render` property. [Form schema](https://docs.github.com/en/communities/using-templates-to-encourage-useful-issues-and-pull-requests/syntax-for-githubs-form-schema).

GitHub supports text attachments, uploads immediately when a file is selected,
and makes uploads to public repositories accessible without authentication.
Cancelling issue submission must not be presented as deleting an upload.
[Attaching files](https://docs.github.com/en/get-started/writing-on-github/working-with-advanced-formatting/attaching-files).

## Prepared artifacts and offline checks

The [preparation helper](../../tools/reporting-browser-proof/README.md) derives a
synthetic form from the production bug form, preserving the production field IDs,
requiredness, dropdown options, and diagnostics textarea shape. Its dedicated config disables blank
issues. The output is local only; install it on the designated repository's default
branch before attempting the native browser trial. Retained label/assignee policy
must be checked as repository configuration, never added as query parameters.

Synthetic cases include Unicode, line breaks, literal ampersands, percent signs
and fragments; a complete prefill; a report exceeding the URL cap with template-only
fallback and complete per-field recovery; and text-only reporting with no capture
attachment. Correlation IDs and artifact hashes permit checking the actual uploaded
file. The helper does not implement runtime review, storage or sanitization.

Offline verification checks field IDs against the source YAML, required fields,
optional attachment behavior in the proposed schema, query parse roundtrip,
6000/6001-byte mode selection, and rejection of production/malformed destinations.
These checks cannot prove GitHub rendered the values or accepted a file.
Architecture review found no blocking preparation findings; its requested
120-Unicode-scalar title/visible-shortening case was added. Deterministic output,
artifact hashes, full fallback text, output-overwrite rejection, relative links
and whitespace checks also passed.

## Production adapter status

The framework has a fixed-destination formatter and Continue to GitHub UI path.
The editor collects separate summary, reproduction, and expected/actual fields, plus
an explicit cannot-reproduce choice. Frequency uses the form's valid `Not sure`
dropdown choice; affected content is explicitly `Not sure`. The reviewed values are
the same values used for URL prefilling and per-field copy recovery. Full sanitized
metadata never enters the URL: it is saved as a separate optional diagnostics file
with matching report/capture IDs. Text-only review has no diagnostics file. The
complete URL is percent-encoded and bounded to 6,000 ASCII bytes; an oversized plan
uses the blank template and exposes copy controls for every form field. The title
has a visible scalar-safe shortening notice. Before opening GitHub, the player sees
that form values may remain in browser history and that selecting a diagnostics file
uploads it publicly before issue submission. A successful local draft and reviewed
artifact save are required before `Application.OpenURL`; the status says the browser
was requested, never that an issue was created.

Game-free verification passed for the runtime/storage path, field mapping, safe
encoding, metadata exclusion from URLs, optional diagnostics behavior, oversized
fallback, title limit, and the 6,000-byte helper boundary. The net35 framework build
passes. In the original draft trial, live field rendering, signed-in/out recovery,
attachment behavior, browser launch, cancellation, and final issue submission were
still open. A later user-requested maintainer-session submission is recorded below;
it does not qualify the remaining ordinary-account or attachment gates. The offline
helper prepares synthetic proof artifacts; it does not stand in for the production
formatter or a live browser trial.

An isolated in-game smoke exercised the production field editor and review controls
with synthetic text, including the metadata-excluded message, per-field copies, and
Back navigation. It stopped before Continue, so it did not open GitHub. The build,
guarded app identity, observations and process-isolation result are recorded in the
[draft and structured-form evidence](reporting-draft-proof.md#structured-github-form-ui-smoke-2026-09-24-utc).

## Live default-branch form inspection, 2026-09-24

Opened the framework repository's current `bug_report.yml` form in GitHub. It renders
only `summary`, `repro`, `expected-actual`, `log`, and `affected`. The locally changed
`frequency`, `environment`, and `diagnostics` fields are not present on the default
branch, so this page cannot validate the current feature's complete prefill or its
diagnostics attachment workflow. The browser session is a maintainer session, not the
ordinary-account class required by this trial. No fields were entered, no file was
selected, and no issue was created. A test submission to this production tracker is
not an acceptable substitute for the designated public test repository in this
protocol.

## User-requested synthetic submission, 2026-09-24

At the user's explicit direction, one synthetic report was submitted to the framework
repository from the isolated in-game reporting flow. [Issue #187](https://github.com/jarlbrak/ftk-mod-framework/issues/187)
is clearly marked as a test and states that no product defect is being reported. The
reporter built a reviewed report, saved its local recovery draft and diagnostics
artifact, opened GitHub, and prefilled the title, summary, reproduction,
expected/actual, and affected fields. GitHub accepted the issue. The live default form
ignored the newer frequency, environment, and diagnostics field identifiers because
they are not yet on its default branch.

The diagnostics file was reviewed and generated with logs omitted and no player or
save data. Its browser file selection failed because the Edge extension does not have
file URL access enabled. No attachment was uploaded. The issue comment records the
report/capture correlation and these limitations. This maintainer-session test proves
issue creation through the current in-game handoff, but does not satisfy the ordinary
account, deployed-form, or attachment requirements below. This was an explicit
one-off production-tracker test; use a designated public fixture and ordinary account
for subsequent browser qualification.

## Native trial protocol

Record browser/version, OS, date, public test repository, default-branch form
commit/hash, ordinary-account permission class, local artifact hashes and the
resulting synthetic issue URLs. Do not record credentials or account identifiers
in public evidence. Use separate test browser sessions for signed-in/out cases;
do not sign the user's normal session out to manufacture the latter.

| Trial | Actions and required observable evidence | Current result |
| --- | --- | --- |
| Form deployment | Verify proposed form is on default branch, blank issues disabled, new IDs visible, diagnostics accepts file selection, optional fields genuinely optional | Blocked: live default-branch form still has only the original five IDs |
| Ordinary signed-in | Open normal URL, compare every actual field and title with fields.json, attach diagnostics.txt, submit, inspect created issue and downloaded attachment correlation/hash | Partial maintainer-session production test: [#187](https://github.com/jarlbrak/ftk-mod-framework/issues/187) confirms issue creation and five current form fields; new fields and attachment are not qualified |
| Signed-out recovery | Open same normal URL signed out, sign in through GitHub, verify preserved values or reconstruct from exact local per-field copies without retyping, attach and submit, inspect issue/file | Not run |
| Oversized fallback | Confirm template-only URL, copy every field from preserved report, optionally attach report.txt and diagnostics.txt, submit and compare complete narrative and correlation | Not run |
| Title boundary | Open title-shortening case, verify the 120-scalar title, visible local 121-to-120 shortening notice and intact 116-scalar summary, then compare the submitted issue | Not run |
| Text-only/manual | Submit required fields with no attachment and explicit diagnostics-not-shared text; ordinary manual visitor needs no fabricated IDs | Not run |
| Required fields | Leave a required field blank in the public fixture and observe submission blocked; fill it and continue | Not run |
| Interrupted login/tab | Abandon the test tab/login before file selection; local report remains unchanged and can reconstruct the form | Not run |
| Cancel after attachment | Using synthetic data only, record immediate upload before abandoning issue; do not claim remote deletion | Not run |
| Unavailable browser/offline | Preserve report/copy recovery and requested/failed status only; do not change network settings affecting another running game | Not run |
| Return to game | Independently scoped A1 proof returns to the owned reporting child and preserves native pause/focus; never drive another FTK instance | Not run |

A live ordinary-account pass requires observed issue content and attachment, not
just a successful URL open. Signed-out prefill loss is acceptable only after the
copy-recovery route itself succeeds. Do not infer Windows/Linux, controller,
fullscreen or co-op evidence from a macOS browser trial. A4 must record menu and
GitHub decisions independently; both remain gated by the outstanding evidence.
