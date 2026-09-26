# In-game reporting service

This small Go service accepts manual and default-on automatic reports, filters diagnostic content,
creates a public issue in one fixed GitHub repository, and serves a public diagnostic
bundle for 30 days. Players need no GitHub account. Credentials exist only on the
service. The client exposes an in-game setting to turn automatic reports off, and
manual reports require a Send action. The privacy page discloses both paths.
Deploy this service schema before distributing a framework build with automatic
reports enabled. Older service builds reject the automatic submission fields;
the client preserves those reports locally for retry and keeps the manual queue separate.

## Railway deployment

The deployment definition is [`.railway/railway.ts`](.railway/railway.ts).
It uses the current Railway Infrastructure as Code SDK. Legacy `railway.json` and
`railway.toml` are deprecated and cannot configure newly created services.
IaC is evaluated by `railway config`, not read automatically by a source deployment.

1. Install Node.js 22 or later, Railway CLI 5.42.1 or later, and the locked
   authoring dependencies with `npm ci --ignore-scripts` in `reporting-service`.
   Run `npm run check:infra`. These Node dependencies are only deployment tooling;
   the runtime Docker image contains the Go service.
2. Authenticate Railway and link a **dedicated reporting project/environment** with
   `railway link`. The file manages the `reporting-api` service and
   `reporting-api-volume` volume by name, with no project, service, or workspace IDs
   committed. Its project name comes from the linked environment. Do not apply it
   to a project with unrelated services: omitted resources can be deleted.
3. The definition uses one replica in `us-west2`, a 5000 MB persistent volume at
   `/data`, the Dockerfile builder, repository root `/reporting-service`, `/healthz`,
   and no autosleep. On an existing setup, verify the volume name, size, and region
   match before planning. Do not move, replace, shrink, or detach a receipt volume.
4. The GitHub source is `jarlbrak/ftk-mod-framework`, with branch `master` by default.
   To validate an unmerged branch, set `FTK_REPORTING_SOURCE_BRANCH` in the local
   planning/apply process. Push that branch before applying. The plan connects the
   GitHub source and deployment settings in the same operation. Also register the
   GitHub deployment trigger with `railway service source connect --repo
   jarlbrak/ftk-mod-framework --branch <deployment-branch> --service reporting-api`.
   Source connection requires the Railway GitHub integration to have access to
   the repository. Verify a subsequent push starts a deployment.
5. Generate a public HTTPS domain and set `PUBLIC_BASE_URL` in the Railway service
   to that origin. Add `GITHUB_TOKEN` directly as a Railway secret when ready. Use
   a fine-grained token restricted to this repository with Issues read/write
   permission. Do not reuse a developer CLI credential or put it in source,
   command arguments, logs, or the game. The IaC file uses `preserve()` for both
   values; it never reads or exports their contents. GitHub App token refresh is
   not implemented in this version.
6. Preview with `railway config plan`, inspect the exact changes, then apply with
   `railway config apply`. Missing optional preserved variables remain absent.
   Additional Railway variables must also be preserved explicitly: add them to
   the authoring file, or set `FTK_REPORTING_PRESERVE_VARIABLES` to their
   comma-separated names in both plan and apply commands. Omitting an existing
   variable proposes deletion. Never enable `--show-values`, `--decrypt-variables`,
   or `config pull --include-variables` when recording public evidence.
7. Verify `/healthz` returns `{"configured":true,"status":"ok"}`. Without a token,
   the service starts for disclosure review but submissions return 503. Verify a
   synthetic report against the deployed service before enabling the game endpoint.
8. Verify forwarding behavior before setting `TRUSTED_PROXY_HOPS`. Default `0`
   ignores forwarded headers and limits by socket address. Use `1` only when one
   trusted proxy appends the real client as the rightmost `X-Forwarded-For` entry
   and direct origin access is prevented. Global limits apply regardless.

From the linked `reporting-service` directory, a branch verification uses:

```bash
npm ci --ignore-scripts
npm run check:infra
FTK_REPORTING_SOURCE_BRANCH=docs/reporting-feasibility railway config plan
FTK_REPORTING_SOURCE_BRANCH=docs/reporting-feasibility railway config apply
```

When running from a different linked directory, pass
`--file /path/to/checkout/reporting-service/.railway/railway.ts` to both commands.
The first plan must contain no resource deletion, volume relocation/detachment, or
secret replacement. An unattended `apply --yes` is appropriate only for an already
reviewed non-destructive change; do not add `--confirm-destructive` to this workflow.
After the branch merges, plan and apply without the source-branch override to return
its source to `master`, then reconnect the deployment trigger with `--branch master`.

A public repository can build by URL without the Railway GitHub App having access.
A successful source connection alone therefore does not prove automatic deployment.
Verify that the source branch has a deployment trigger and that a subsequent push
starts a deployment after authorizing the app for the selected repository.

The local `.railway` importer, lock, binding, plan, and generated type files are
ignored. Only `railway.ts` is source-controlled. Keep project linkage in the CLI's
local state. Use a read-only `railway config pull` into an ignored scratch directory
when inspecting another environment; never inline its variables into public code.

Railway supplies `PORT`. Other settings are `DATA_DIR` (default `/data`),
`MAX_REPORTS` (default 1000 permanent receipts), `REPORTS_PER_IP_HOUR` (5),
`REPORTS_PER_HOUR` (100 globally), and `REPORTS_PER_DAY` (500 globally).
Limits are in-memory fixed windows and reset on restart. There is no unbounded
background queue. Storage refuses new IDs at capacity; at the default limit,
each serialized receipt is capped at 2 MiB plus 4 KiB, including its report.
At 1000 receipts the maximum is about 2.0 GiB plus filesystem overhead and one
temporary replacement file; JSON and readable log downloads are generated from the
same receipt and do not create extra stored copies. This stays below the configured
5000 MB volume. The service also retains active report payloads in memory, so size
the runtime memory for expected retained volume. Existing deployments that explicitly
set a higher `MAX_REPORTS` must lower it or provision equivalent storage headroom.
Lowering the cap does not delete existing receipts; it stops accepting new IDs.
Use a volume with headroom and alert on disk usage, 429/503 responses, and capacity.
The Docker image uses the standard CA trust store; never disable TLS verification.

Railway [volumes](https://docs.railway.com/volumes) preserve receipts across
deployments. [Health checks](https://docs.railway.com/deployments/healthchecks)
gate deployment readiness; volume deployments may briefly interrupt service.
[Infrastructure as Code](https://docs.railway.com/infrastructure-as-code) and its
[reference](https://docs.railway.com/infrastructure-as-code/reference) document the
deployment configuration. Do not enable multiple replicas or autosleep while
relying on this file-backed store.

## HTTP contract

`POST /v1/reports`, `Content-Type: application/json`, maximum 2 MiB:

```json
{
  "schemaVersion": 1,
  "reportId": "0123456789abcdef0123456789abcdef",
  "captureId": "fedcba9876543210fedcba9876543210",
  "kind": "error",
  "submissionMode": "automatic",
  "fingerprint": "0123456789abcdef0123456789abcdef0123456789abcdef0123456789abcdef",
  "description": "Optional player description",
  "includeDiagnostics": true,
  "diagnostics": {
    "versions": { "framework": "example" },
    "logs": "[Info] Example process context\n[Warning] Example warning\n[Error] Example failure"
  }
}
```

IDs are random 32-character lowercase hexadecimal identifiers. `kind` is `manual`,
`error`, or `unexpected_exit`. `submissionMode` is omitted for manual reports and set
to `automatic` for background reports. Automatic reports require diagnostics and
cannot use `manual` kind. Automatic error reports also require a 64-character
lowercase hexadecimal `fingerprint`, used by clients to suppress repeated errors.
Description is optional, up to 4000 Unicode code
points. Diagnostics must be an object when enabled and omitted or null when
disabled. JSON depth is bounded. The client owns the diagnostic schema and collects only disclosed fields, never
arbitrary files. The game attaches up to 128 KiB of UTF-8 process log text per
current and correlated previous session, including informational messages, warnings,
and errors observed since reporting initialized. It is a filtered tail, not a
complete on-disk game log. Previous sessions use the last persisted matching capture;
abrupt exit may lose recent messages and old saved drafts cannot backfill missing
logs. JSON escaping has room within the 2 MiB transport envelope; filtering/encoding
that would exceed the actual receipt size limit returns 413 before creating an issue.
The service additionally drops known credential/player fields and redacts common
credentials, home-folder names, email, IPv4, Steam ID, and URL patterns. This is
defense in depth, not a guarantee that arbitrary mod logs contain no personal data.

Success is 201 for creation, or 200 for an existing submission:

```json
{"schemaVersion":1,"status":"submitted","reportId":"0123456789abcdef0123456789abcdef","issueNumber":123,"issueUrl":"https://github.com/jarlbrak/ftk-mod-framework/issues/123"}
```

Failures use `{"schemaVersion":1,"status":"error","error":"code"}`:

| HTTP | Code | Client behavior |
| --- | --- | --- |
| 400 | `invalid_report` | Keep local report; fix invalid input. |
| 409 | `report_conflict` | Do not silently generate a new ID or overwrite the original. |
| 413 | `payload_too_large` | Bound collection before retry. |
| 415 | `unsupported_media_type` | Send JSON. |
| 429 | `rate_limited` | Keep local report; respect Retry-After. |
| 503 | `submission_pending` | GitHub result is uncertain. Retry the exact same payload and ID. |
| 503 | `service_unavailable` | Keep local report; retry same payload and ID later. |
| 503 | `capacity_reached` | Operator action needed; keep local report. |

The service does not send arbitrary GitHub error bodies to clients and does not log
payloads, tokens, or client addresses. `/healthz` reports process/configuration
readiness, not GitHub connectivity. `/privacy` is the public disclosure.
`GET /diagnostics/<reportId>.json` returns the filtered metadata/log bundle.
`GET /diagnostics/<reportId>.log` returns readable UTF-8 text with current and
previous session headings and coverage notes. Both are public only for submitted
reports with diagnostic consent and expire 30 days after the first receipt. Issue
text contains up to 12000 bytes of filtered diagnostic excerpts and both download
links; the attachment retains the bounded uploaded dump rather than only the short
issue excerpt. Excerpts remain in GitHub after download expiry.

## Duplicate prevention and operations

A synchronized receipt is written before attempting a GitHub POST. Successful
receipts survive process restart. Same-ID changed content returns 409. After an
uncertain POST outcome, retries only scan existing repository issues for the hidden
report marker and record the matching issue. They never blindly repeat an uncertain creation.
The scan is bounded to 2000 issues and 25 seconds. If the issue is not yet visible,
the response remains `submission_pending`. An explicit GitHub client-error rejection (400, 401, 403, 404, 415, 422, or 429)
is durably marked rejected. The same report can retry creation after the problem is
fixed; its receipt is synchronized back to pending before each attempt. Timeouts,
5xx responses, and invalid success responses never authorize a second POST.

For an unresolved pending receipt, inspect the repository for its marker and inspect
service configuration. Do not delete the receipt or tell a player to generate a new
ID unless a maintainer has verified no issue was created. There is no automated
repost or admin mutation API. Copying/replacing volumes, restoring an old snapshot,
or removing receipt files can break deduplication. Back up the volume with the same
access controls and retention policy as live data.

Descriptions and diagnostic payloads are erased from receipts after 30 days by
startup cleanup and hourly cleanup. Downloads expire immediately at the deadline.
Minimal receipts remain indefinitely for idempotency and count toward capacity.
Privacy-removal requests require a maintainer to remove the GitHub content and
purge the receipt's report payload while preserving its ID/hash/status metadata.
Public copies and third-party retention cannot be recalled.

An unauthenticated reporting endpoint can still receive spam. Rate and storage caps
bound immediate impact but do not establish player identity; monitor the initial
rollout and add edge abuse controls if needed. Do not claim stronger abuse defense.

## Verification

Run `go test -race ./...`, `go vet ./...`, and `go build ./...` in this directory.
For infrastructure changes, run `npm ci --ignore-scripts`, `npm run check:infra`, and
a read-only `railway config plan` against the explicitly linked environment. A
successful plan validates the SDK graph and proposed changes without deploying.
Tests use a fake GitHub server and cover automatic bundle inclusion, redaction,
receipt restart, conflict detection, uncertain-response reconciliation without
duplicate creation, input and serialized-storage bounds, a useful log dump larger
than the former 128 KiB request limit, readable log downloads, diagnostic opt-out,
rate/storage limits, trusted
proxy boundaries, unconfigured startup, and expiry with retained receipts.
These checks do not prove Railway deployment, game transport, or real GitHub issue
creation; those require a separately identified synthetic live report.
