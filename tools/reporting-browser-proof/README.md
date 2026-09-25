# Synthetic GitHub browser proof

This A3 helper prepares a proposed test form and synthetic local artifacts. It is
not the production formatter or browser adapter. It never opens a browser, writes
to a repository, uses credentials or submits an issue. The production form stays
unchanged. See the [trial protocol](../../docs/evidence/reporting-browser-proof.md).

Requires Python 3.11+ and PyYAML 6.0.3. Use a dedicated environment if needed:

```sh
python3 -m venv scratch/reporting-browser-venv
scratch/reporting-browser-venv/bin/pip install PyYAML==6.0.3
scratch/reporting-browser-venv/bin/python tools/reporting-browser-proof/prepare.py
```

After the user identifies the test repository, prepare into a new directory:

```sh
python tools/reporting-browser-proof/prepare.py \
  --repository TEST_OWNER/TEST_REPOSITORY --output scratch/browser-proof-trial
```

The helper rejects the production tracker and malformed repository names. This
check does not establish ownership, consent, permissions or that a destination is
a test repository; verify the user's designation before use. Output includes:

- Proposed `.github/ISSUE_TEMPLATE` files for the test repository's default branch.
  Existing form IDs, required narrative fields, label and assignee policy are
  preserved. The new diagnostics textarea deliberately has no `render` key.
- Normal Unicode/query-escaping, oversized fallback, text-only and visible
  title-shortening cases, each
  with `fields.json`, `url.txt` and a complete `report.txt` recovery copy.
- A synthetic `diagnostics.txt` and artifact SHA-256 manifest. No real diagnostics
  or log data are collected. These are minimal A3 correlation examples, not full
  B collector envelopes or validated review manifests.

Open URLs and attach files only during an authorized native-browser trial. Read
all synthetic bytes first. Use the ordinary non-maintainer account for submission;
the configured CLI account is not evidence of that permission class. Test form
installation and actual posting are separate actions requiring the designated
repository/session. Do not use a production issue for this test.

The focused check verifies schema field IDs, attachment-capable textarea settings,
Unicode and query roundtrips, exact 6000/6001-byte selection, and rejected
destinations. It proves Python proof-fixture behavior only. It does not qualify
net35 encoding, browser launch, sign-in recovery, upload or final submission.
