# One staged enemy exercise

`exercise_case.py` composes the unchanged `Runner` and `Recorder` for one exact
profile and CEL-relative renderer. Use the model Python environment (NumPy is
needed by the existing capture-boundary verifier). Example for an already staged,
native hero-ready enemy:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/runtime-test/exercise_case.py \
  --root /absolute/path/to/scratch/game-copy --port 8788 \
  --enemy exact_registered_key --renderer-path exact_CEL_relative_path \
  --profile-sha256 exact_model_test_profiles_sha256
```

To stage an eligible slot in the existing dungeon, add `--from-ready --level N
--room N`. This invokes the existing guarded next-case staging flow, including its
explicit 999-HP party fixture. It rejects Stair, terminal and nonEnemy slots. There
is no new campaign, restart, floor advance, next-profile loop, automatic equip,
modal dismissal, or deployment. Run one controller at a time; the chosen bridge
port is operator-owned, as in the existing runners. The bridge has no root/session
identity endpoint.

The sequence is fixed:

1. Capture one native pass; require the same living target and fresh native hero
   readiness afterward. Record actual party HP, without claiming retaliation
   damage when none was observed. The passive target observer must record the
   selected enemy as the actual `PlayAttackSequence` attacker and a following
   target CEL attack trigger. Its first PNG must retain the same bound renderer,
   the CEL's native Animator, and a settled idle state before the runner sends
   end turn.
2. Capture one ordinary attack (`cheat: None`, no focus). Compare the target HP
   immediately before submission with the same-target HP in the action's own
   post-capture `after` state. Only a positive, lower HP in that bounded action
   window is a nonlethal HP-loss gate. Store the later native hero-readiness HP
   separately as `readyHpOutcome`; intervening enemy turns or status ticks must
   not be attributed to the submitted attack.
   The default `--attack-attempts 1` stops at `no_hp_loss_unclassified`; the
   bridge's commit response contains no authoritative block/dodge result.
   With `--attack-attempts N` (1 through 8), another attack is allowed only
   after a complete same-target no-loss observation. A recorded `Block`,
   `Dodge`, or absent exact victim response is retained as an
   `attackResponseObservation`; it does not become a hit claim. Any other HP
   outcome stops, and the sequence still requires measured nonlethal HP loss.
   Increased, absent or zero-HP outcomes are never retried. Every attempt is
   retained in `attackRetrySummary` and the action captures. Only the accepted
   ordinary attack also requires the selected enemy as the native victim with a
   `Damaged` or `DamagedHeavy` response and the matching exact CEL trigger.
3. Capture one explicit `KillSingle` death fixture. This is not ordinary-damage
   death coverage. A completed capture is preferred. The existing exact
   `Renderer destroyed during capture` predicate can admit a nonempty contiguous,
   same-owner PNG/pose prefix only after definite action acceptance and with no
   other recorder failure. Raw `ok:false` and the full error remain unchanged;
   classification is `expected_death_capture_boundary`, not full capture success.
   The fixture must retain the native victim `Death` response and exact target
   CEL `Death` trigger before its available prefix can become a death review
   pointer.
4. After capture completion, observe native death/loot state. Each fresh owned
   Collect button receives one guarded native `collect-loot` command. Require an
   observed unavailability followed by a fresh usable Collect, a changed Collect
   item/button identity, or actual gold/XP/level reward advancement before another
   Collect. Unrelated Ready-button focus, FSM or dungeon UI changes do not count.
   Unchanged or unknown state stops at the deadline. Eight collects is the limit. The next
   strict Ready slot ends the case; Ready is not clicked and Stair is not advanced.
   During the native Loot handoff, `combat.active` can remain true and
   `heroTurnReady` false even though the enemy is dead. The strict Loot observation,
   rather than another combat action, is the only authorization to continue.

Each dependent operation rechecks session, profile, asset, binary and native
owner/party identity. Motion evidence also pins the target FID, CEL, renderer and
CEL-native Animator at arm, event and retained-frame time. An exclusive session/dungeon/slot claim prevents another
exercise of the same slot. Claims remain after failure: inspect the immutable
journals and continue manually if appropriate, never rerun the whole sequence to
retry an uncertain action. Ordinary Recorder behavior remains unchanged. If it
returns with an already-issued capture still pending, this wrapper may only wait
for that same result and pin it; it permanently stops the action sequence even
if the capture later finishes. Other failures also stop without retries.

The output `case-result.json` links hashed child journals/results, raw captures,
every available validated PNG, actual HP evidence, native loot observations and
final Ready. `selected-frames.html` references original images without changing
their pixels: pass indices12/24, attack6/12, death0/last retained prefix frame.
Missing views are reported as missing; a one-frame prefix is not duplicated.
All selected frames start with viewed count0. A completed case is labeled
`needs_visual_review`, never automatic art, animation or model-support acceptance.
The six-frame sheet is a small review aid, not evidence that every animation frame
was inspected. Each action's `motionEvidence` carries additional hashed PNG
pointers selected after the native event chronology. Review those original
frames and the full raw sequence; the pointers establish neither full clip
coverage nor a visual-quality verdict.

Defaults: 120 frames at12fps/10game-seconds per action, one attack attempt,
operation40seconds, capture360seconds, native transition120seconds, extra
pending-capture observation60seconds. Full-size PNG encoding can take several
minutes of wall time even though the capture requests only10 game-seconds; do
not stop a still-running capture just because the game visibly slows. The
360-second budget preserves that same issued capture rather than authorizing a
replacement. `--attack-attempts` is bounded to1..8.
All timeout overrides are finite and bounded. Offline tests cover
slot claims, Stair rejection, HP outcomes, once-only/uncertain actions and Collect,
partial capture boundaries, changed pins/owners, unknown modals, missing PNGs and
short-prefix selection. These tests do not operate the game.

Measured isolated trial: Fairy A case `9fa034b1acf246eaa5b8b8dc144aaf01`
completed pass, one ordinary attack (target HP 58→55), and explicit KillSingle
fixture, with all three raw captures retaining120 frames. One native Collect
returned to strict Ready at level0/room3. The immutable result is under
`scratch/mirewarden-game/model-test-output/case-9fa034b1acf246eaa5b8b8dc144aaf01/`.
This validates the bounded orchestration on that case; its result remains
`needs_visual_review` and does not establish normal-damage death or other rigs.

A zero-HP endpoint is labeled `target_removed_or_zero_hp_after_action`: native
enemy removal can also set HP to zero, so this does not attribute lethal damage
to the attack. It still stops before KillSingle or Collect. Intermediate hit
evidence and any authoritative removal reason remain separate observations.
