# One-shot Guardian damage fixture

This isolated, single-player test helper replaces one committed enemy outcome.
It does not call Guardian mutation methods, set HP directly, alter database rows,
write fixture state into saves, or invoke attack playback outside a native turn.
The resulting hit is real combat and can have ordinary native consequences,
including death if the behavior under test fails. It is not ordinary damage-roll,
targeting-AI, or multiplayer evidence.

First cast Guard through the real UI. On the following stable hero stance, inspect
`guardian-state` and `inventory` for exact encounter and dummy instance IDs. The
pinned guardian set must still actively protect the victim, with exactly the
case-specific rescue states below. Native Resist Death and Sanctum protection
are excluded. Use a plain single-target enemy attack.

Send `guardian-damage-fixture` through the normal command client with:

```json
{
  "action": "arm",
  "case": "reduction",
  "encounterInstanceId": 123,
  "enemyInstanceId": 456,
  "victimInstanceId": 789,
  "guardianInstanceId": 101,
  "expectedVictimHp": 30
}
```

The example IDs are placeholders. `reduction` supplies 9 direct damage and
requires victim HP above 5. The expected native HP is prior HP minus 5, with
rescue still available. `rescue` supplies twice the pinned current HP and expects
1 HP, alive, with the rescue spent. Both cases require HP in 1..999. The fixture
expires after 120 real-time seconds. It does not advance turns: use ordinary UI
or separately authorized existing native actions to reach the selected enemy.

Additional cases use an explicit `guardians` list instead of `guardianInstanceId`:

```json
{
  "action": "arm",
  "case": "nonstack",
  "encounterInstanceId": 123,
  "enemyInstanceId": 456,
  "victimInstanceId": 789,
  "guardians": [
    {"instanceId": 101, "rescueAvailable": true},
    {"instanceId": 102, "rescueAvailable": true}
  ],
  "expectedVictimHp": 30
}
```

- `nonstack` requires exactly two active guardians with available rescues. It
  supplies 9 damage and expects exactly 5 retained damage, with both charges intact.
- `multi-rescue` has the same two-guardian requirement and supplies twice current
  HP. It expects the victim alive at 1 HP and only the first guardian in stable
  ordinal FID order to spend its charge. Spending both or choosing the other fails.
- `spent-rescue` requires exactly one active guardian whose explicit
  `rescueAvailable` is false. It supplies twice current HP and expects 0 HP and
  native death, with the charge still spent. Prepare that state through a previous
  native rescue and another ordinary Guard cast; the fixture never sets charges.
  This case intentionally kills the guarded hero through native combat.

All cases use HP 1..999; `nonstack` also requires HP above 5. Every guardian's
identity, avatar and HP are pinned. The full active protector set and each rescue
state must match before commit. Input order does not decide which rescue is used.
Receipts record guardian IDs and charge states at arm and at the native hit. A
spent-rescue victim may leave the native active-dummy lookup after death; its
pinned object/avatar and the exact observed hit remain required after commit.

At that enemy's `EngageBattle`, the fixture changes the target parameter before
native code binds its target, globals, and FSM. At the same enemy's native
`DamageCalculator._playAttackSequence` commit, it requires the pinned target and
no secondary damage outcomes or AoE targets. It consumes its receipt before substituting
a plain known outcome and selecting native direct playback. No proficiency,
critical, special, or secondary-health effect is supplied. The production Guard
prefix, native `SetDefendType`, actual hit callback, and HP/death pipeline run.
A received payload is recorded after the production transformation and checked
against the victim's hit payload and final health/charge state.

Use `{"action":"inspect"}` to retrieve the receipt, status, and at most 24
records: arm, retarget, original/replacement commit, transformed outcome, and
native final hit. `passed` requires both transformed damage/HP and native final
HP/alive/charge results to agree. A retargeted attempt can still abort before
replacement if its generated attack is unsuitable; its normal native attack
then proceeds at the already-selected victim. The record reports that boundary.

Use `{"action":"disarm","receipt":"EXACT_RECEIPT"}` before commit to cancel
an armed fixture. Cancellation does not undo an already chosen native target.
Committed attacks cannot be cancelled or automatically retried. Scene/actor/avatar,
health, or Guard changes before commit abort the fixture. Expiry, completion, or
abort removes only this fixture owner's hooks on the next helper tick. Cleanup
also runs at helper destruction. No original native exceptions are suppressed.

The helper uses no production finalizers. These transient test patches still
change JIT behavior; report this as fixture evidence alongside separate natural,
uninstrumented combat observations. Policy and authority-boundary tests do not
prove native playback, animation, or networking behavior.

Native `m_RepeatCount` belongs to proficiency-effect duration, not attack count.
For example, `enBiteReg` has duration 1 with no proficiency prefab and still
produces one main outcome with null secondary outcomes. The fixture therefore
checks actual outcome/target shape and records it before substitution.
