# Model validation stage readiness

This is a read-only comparison between the validation execution queue and one stopped isolated game catalog. It identifies catalog and asset copy work only. It does not stage, deploy, launch FTK, or prove a live result.

| Unique queued profile revisions | Ready without stage | Append or asset stage available | Explicit isolated migration required | Routes with stage-ready revision | Stage-ready routes with selected revision | Routes requiring migration | Adapter design | Adapter implementation | Adapter validation | Adapter visual/archive review |
|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|---:|
| 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 | 0 |

## Revisions

| Catalog | Profile document | Key | Catalog state | Asset states | Stage state | Route references |
|---|---|---|---|---|---|---:|

## Route choices

Each row keeps its viable historical profile revisions visible. A selected revision is either the route's only stage-ready candidate or an exact hash-pinned choice from the selection ledger. This never transfers an archive result between revisions.

| Group | Route | Next staging action | Selected revision | Candidate revisions |
|---|---|---|---|---:|

## Conflicts to resolve explicitly

Profile-key variants: none

Asset-name variants: none
