# One native combat Focus input

`native-combat-focus` is an opt-in isolated single-player engine fixture. It calls
`OnRightClick()` once on the currently selected native attack/proficiency button.
The ordinary Focus animation callback performs the debit. This operation never
attacks, selects another button, writes Focus fields, invokes callbacks, or retries.
It is fixture-assisted UI evidence, not keyboard or mouse coverage.

Read the current hero FID, `focusPoints`, and `spentFocus` with `guardian-state`.
Select an ordinary attack through native UI, then send:

```json
{"op":"native-combat-focus","action":"spend","expectedHeroFID":"1:1","expectedFocus":3,"expectedSpentFocus":0}
```

Use the actual observed values, plus the normal command ID/session envelope. The
operation requires a live hero at stable `Wait For Stance`, matching selected
button/profile, available Focus and slot capacity, and no animation in progress.
Guard and non-attack actions are rejected. A returned `pending` receipt pins the
encounter, scene diorama, hero, avatar, button and action slot count for ten seconds.
Inspect it without dispatching another input:

```json
{"op":"native-combat-focus","action":"inspect","receipt":"exact-returned-token"}
```

`passed` means Focus fell by exactly one and spent Focus rose by exactly one after
the native animation finished. Changed actors/selection, an unexpected debit,
exceptions or timeout produce `unknown`. An unknown result blocks further spends
in this helper process. Do not retry or roll back: native Focus may already have
been spent. A subsequent native attack is a separate action and observation.
