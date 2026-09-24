# Reporting service instructions

Read the root `AGENTS.md` first.

- Keep the service, Dockerfile, deployment definition, disclosure and tests in this repository.
- Credentials belong in Railway secrets. Never commit, print or copy a developer CLI credential.
- Each client report requires an explicit player Send action. Detecting an error must not upload.
- Preserve bounded collection, diagnostic exclusion, public-sharing disclosure and retention limits.
- Keep durable submission receipts on the existing volume. An uncertain GitHub creation must be
  reconciled; never retry it as a new creation or discard its receipt to unblock a deployment.
- The file-backed store requires one replica in one region. Do not enable autosleep or scale out
  without changing and verifying the storage model.
- Review the Railway infrastructure plan before applying. Preserve existing secrets and variables,
  and never replace or detach the receipt volume as a routine configuration fix.
- Run `go test -race ./...`, `go vet ./...`, `go build ./...` and `npm run check:infra` for relevant
  changes. Offline fake-GitHub tests do not prove live submission or game behavior.
