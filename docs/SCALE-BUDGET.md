# Scale-budget gate

The scale-budget gate is a diagnostics check that measures one content load against a persisted
calibration baseline and tunable budgets, then emits exactly one line per content load. It exists to
catch regressions in load time, managed-heap use, and registration footprint as a mod set grows. The
gate never blocks or alters the load: it only measures, persists the baseline, and logs a verdict.

## The verdict line

Each content load emits exactly one line via the framework log:

```
SCALE-BUDGET PASS|FAIL|CALIBRATED: load=<ms>/<budget> heap=<bytes>/<budget> saveProxy=<bytes>/<budget> (N=<count>)
```

- `PASS` (info): every measured metric was within budget.
- `FAIL` (error): at least one metric breached its budget; the line is followed by `breached: ...` naming each tripped metric.
- `CALIBRATED` (info): this run wrote the baseline (no comparison was made). The budget slots show the metrics' own values, since a calibration run has nothing to compare against.

`N` is the high-band registered row count (the ids that pass through `IdAllocator`).

`saveProxy` is a registration-footprint ESTIMATE (high-band registered rows times a per-id byte cost),
not a measured save file. The proxy is only meaningful while synthetic enum ids round-trip through saves
as their integer value, which the framework guarantees by setting `SerializeEnumsAsInteger = true`.

## Config fields (`[Diagnostics]` section)

| Key | Default | Meaning |
|---|---|---|
| `EnableScaleBudgetGate` | `true` | Master switch. When false, no SCALE-BUDGET line is emitted at all. |
| `OutputDirectory` | `BepInEx/FTKPerfProbe` | Folder for `scale-baseline.json`. Relative paths root at the game folder. |
| `LoadMsHeadroomMultiplier` | `2.0` | Load budget = max(baselineLoadMs * this, LoadMsAbsoluteFloorMs). |
| `LoadMsAbsoluteFloorMs` | `1000` | Absolute floor (ms) for the load budget, so a fast vanilla load never trips the gate. |
| `MemoryHeadroomMultiplier` | `2.0` | Memory budget = max(baselineHeapBytes * this, MemoryAbsoluteFloorBytes). |
| `MemoryAbsoluteFloorBytes` | `67108864` | Absolute floor (bytes) for the memory budget (64 MiB). |
| `SaveSizePerEntryBudgetBytes` | `64` | Per-high-band-id footprint (bytes) used by the save-size proxy. |
| `RecalibrateBaseline` | `false` | Set true for ONE run to re-measure and overwrite the baseline (see below). |
| `SyntheticContentCount` | `0` | DEBUG/stress: generate this many throwaway synthetic entries before the load. 0 = off. |
| `SyntheticContentKind` | `weapon` | Kind for each generated synthetic entry. |
| `SyntheticContentTemplate` | `bladeDagger` | Template (vanilla row to clone) for each generated synthetic entry. |

The baseline file (`scale-baseline.json`) is the ONLY persisted artifact. There is no run-over-run
history, no deltas, and no trend data: just one baseline that a calibration run writes and every later
run reads.

## Calibration procedure

The baseline anchors the budgets. It is written ONLY on a calibration run, never auto-updated on a
normal run.

- First run with no baseline auto-calibrates: it writes `scale-baseline.json` and emits `CALIBRATED`.
- A normal run with a readable, valid baseline never modifies the file.

To RE-calibrate cleanly (for example after changing your installed content set, or to clear a poisoned
anchor), do this for ONE run:

1. Set `RecalibrateBaseline = true`.
2. Disable custom content so the baseline anchors on vanilla load only:
   - `EnableSampleContent = false`
   - `EnableDataContent = false`
   - `SyntheticContentCount = 0`
3. Launch once. The gate emits `CALIBRATED` (preceded by a one-line reason: `RecalibrateBaseline=true`).
4. Set `RecalibrateBaseline = false` again.

After that, normal PASS/FAIL gating resumes against the fresh vanilla-anchored baseline. The gate never
rewrites the config file: the flag stays whatever you set it to, so you must flip it back to false
yourself.

## Staleness (auto-recalibration across versions)

A baseline whose `SchemaVersion` or `FrameworkVersion` does not match the current build is treated as
ABSENT: the gate recalibrates (writes a fresh baseline, emits `CALIBRATED`) rather than comparing against
a stale anchor. It never FAILs because of staleness. The reason is logged on a single info line before
`CALIBRATED` (for example `reason: framework-version mismatch`). This means a framework upgrade does not
leave you gated against numbers from the previous build.

## Poisoned-baseline warning

If the loaded baseline was calibrated with custom rows present (`CustomRowCountAtCalibration` is nonzero),
the gate logs a warning on every normal gating run, ALONGSIDE the normal PASS/FAIL line:

```
SCALE-BUDGET: baseline was calibrated with N custom row(s) present (poisoned anchor); budgets may be inflated. ...
```

What it means: the baseline absorbed the cost of your custom content, so the budgets derived from it are
inflated and the gate will not catch regressions that fit inside that inflated headroom.

How to fix it: recalibrate with content disabled, following the calibration procedure above (set
`RecalibrateBaseline = true` and disable `EnableSampleContent`, `EnableDataContent`, and
`SyntheticContentCount` for one run). The warning is not emitted on a calibration, stale, or recalibrate
run, because no trusted baseline is being relied on then.
