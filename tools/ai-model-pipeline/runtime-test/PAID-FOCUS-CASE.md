# Paid focus CLI candidate

Standalone reviewed wrapper. One live CLI completion on native turn9 was independently verified; the earlier turn6 completion used the manual helper route. This does not establish broader weapon/class or failure-path coverage. This wrapper spends at most one focus slot and never attacks, selects another action, refunds, dismisses a modal, advances a turn, or invokes a fallback.

```sh
python3 tools/ai-model-pipeline/runtime-test/paid_focus_case.py \
  --root /absolute/owned/scratch/game \
  --session EXACT_NONCE --level 0 --room 1 \
  --catalog-sha256 EXACT_CATALOG_SHA256 \
  --helper-sha256 EXACT_HELPER_SHA256 \
  --outcome-timeout 30
```

Requires the reviewed helper's paid-focus-state/paid-focus-submit protocol. Root/session/catalog/helper are explicit; framework binaries and this source are measured/pinned before any helper command and rechecked during observations. SHA measurements describe on-disk files, not loaded memory identity.

The initial state must show the exact usable normal Attack, native hero/target/weapon/turn identity, no pending focus, and current input/stance/payment guards. A permanent exclusive local claim is created for root/session/dungeon/level/room/ES/MC/native turn/hero. Target/button/profile changes cannot bypass it. The fresh helper-issued ticket is submitted once within a conservative4-second local window; native5-second/600-frame expiry remains authoritative. The file protocol assumes one operator/writer; it rejects an unresolved current-session command, permits replacement of a stale old-session command, and cannot lock out arbitrary concurrent game operators.

Missing/uncertain submission results stop all further commands. The journal preserves the exact request, predetermined result ID/path and raw result/hash when present. Never retry a missing action, even if it later completes. Read-only observation timeout is nonterminal payment-pending, not failed payment or completed action. Exit0 means full payment-plus-readiness proof; exit2 means inspect the preserved result before doing anything else.

Confirmed submission is followed only by paid-focus-state observations. The exact same submissionId must retain one untruncated, exception-free native callback with identity/profile/weapon and raw callback joins; submission baseline must match callback-before counts, and actual callback-after must debit available focus by1 and increase spent focus by1. Duplicate, mismatched, interrupted, refunded or uncertain evidence stops. Payment proof may precede HUD animation completion: success additionally requires a fresh same-turn state with unchanged paid counts, focusingfalse, animationCount0 and current native hero/input/normal-Attack readiness. A separately authorized attack must use its own fresh guard; this wrapper never commits one.

Tests use fake file-protocol responses and clocks, not Unity. They verify one native submission, immutable local claims, expiry, missing results, rejection, callback debit/baseline/identity corruption, duplicate evidence, changed session/turn, timeout and delayed animation readiness. The saved real Bronzehollow callback parses successfully as a schema check only; it was produced by the parent's manual helper operation, not this CLI.

Live evidence: [native-paid-focus-v1](../../../docs/evidence/native-paid-focus-v1/README.md). Reproduce offline tests from this directory with `python3 -m unittest test_paid_focus_case -q`.
