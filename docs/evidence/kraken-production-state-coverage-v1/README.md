# Old Kraken production state inventory and native reachability

Native source is
Assembly-CSharp SHA256 `94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`.
Controller5973 krakenHeadController extraction:
`kraken-controller-5973-local.json` SHA256
`a25b275d92ca64b2afa9d0e3e1c5ab80e832209bcee023157d9649a73ee66c09`;
decoded graph `kraken-controller-transitions.json` SHA256
`b4a0141ad1d37dd6815fa1a3851e9942a87c31c1bebd360690d7521e1db5779b`.
Behaviour audit `kraken-behaviour-lifecycle-audit.json` SHA256
`ea796ada98961b3d2609b878fcc04ab4c280ae43258735922dec43fa9486521b`.
The decoded graph's source_sha256 identifies its controller JSON, not the whole
resources.assets file. Existing extraction uses pinned resources.assets e33be4e6….

The exact-route production campaign is preserved in
[`../kraken-production-adapter-v1`](../kraken-production-adapter-v1/README.md).
Its two fresh helper sessions are `9f69423c0ada46f6820d320343b384e1`
and `47bf1934e3724bb1b2c6ffdd894b7dc7`. The aggregate verification SHA256 is
`db95ec3ef9045f83ce266c1286e0a0728ddd5b275a4c4d114d143c2d882a0318`,
and the archive validation SHA256 is
`6154ad173ef779a04c443ad0016093f28eb222d79614ea682efbe409ae4bcdd9`.

## Exact controller inventory

There are15 states and5 distinct clips. Every extracted state has speed1 and
WriteDefaults=true. Shared clips do not establish shared transition coverage.

| States | Native clip | Trigger/entry | Exact-route status |
|---|---|---|---|
| IDLE |4921 krakenIdle, looping|default|live observed|
| INTRO |4921 krakenIdle|AnyState Intro|source-reachable during combat initialization, before the production observer can arm|
| DEFEND |5488 krakenDamage|IDLE Defend|live observed from a blocked ordinary hit|
| DAMAGED / DAMAGEDHEAVY |5488 krakenDamage|IDLE Damaged / DamagedHeavy|both live observed through ordinary hits|
| DODGE / DAMAGED STUN |5488 krakenDamage|IDLE Dodge / Stun|structural only; observed `RespondToDodge` damage-phase callbacks did not produce a Dodge response|
| ATTACK / ATTACKCRIT / ATTACKPROF |4771 krakenAttack|IDLE Attack / AttackCrit / AttackProf|ATTACK live observed; other roles structural only|
| VICTORY / PASSIVE VICTORY |5501 krakenDisappear|IDLE Victory / PassiveVictory|VICTORY live observed through an ordinary party loss; PASSIVE VICTORY structural only|
| DEATH / DEATHLIGHT |5501 krakenDisappear|AnyState Death / DeathLight|DEATH live observed through an ordinary non-cheat lethal hit; DEATHLIGHT needs an actual secondary lethal gameplay source|
| OverworldAppear |5513 kraken_appear|AnyState Appear|separate MiniHexEnemy owner route; unreachable on the combat owner|

The 15 states and five clips are the pinned serialized-controller inventory. The
inventory is not a live-reachability claim. Native pose inputs are absent on the
old resource's modern-driver paths, so the fixed sampler remains necessary. Do
not substitute a modern skeleton for the old five-bone palette or force a state
to make the live trace resemble the static inventory.

## Attack clip and gameplay authority

krakenAttack4771 is nonlooping, length7.1666669845581055 seconds. Events:

- .1669676304 Foley PLAY_SFX_WATER_SUBREMERGE
-1.6413075924 PlayWeaponAnimation
-1.9387031794 Foley PLAY_VO_ATTACK
-2.2750992775 Dodge
-3.71093177795 AttackHit
-4.2796897888 Foley PLAY_SFX_WATER_SUBREMERGE

Three attack states each attach CombatAction and AttackStart. Native
decompilation confirms CombatAction.OnStateEnter/Exit and AttackStart.OnStateEnter/
Move call animator.GetComponent<CharacterEventListener>() and then dereference
component.m_Dummy. Logging a missing component does NOT return before dereference.
Animator.fireEvents=false does not disable these StateMachineBehaviour callbacks.
Do not attach fake CEL or dummy objects just to silence the callbacks.

CharacterEventListener.AttackHit1326 resolves the current attack victim from
EncounterSession and calls RespondToHit; Dodge1335 calls RespondToDodge.
PlayWeaponAnimation1204 drives the equipped weapon when present. Native event
timing and victim/weapon context must remain authoritative on the real CEL.
An added clip-only sampler has no controller/SMBs and fireEvents=false; it must
never forward these events itself. Source-proven event presence is not proof
of one-time live dispatch, damage application, or complete weapon effects.

The exact registered route supplies stronger evidence. Every observed Kraken
proficiency request used `attackAnim=AttackProf` with `override=Attack`, so native
override precedence correctly entered ATTACK. The campaign completed 36 native
attack windows. Successful windows covered `enKrakenResistUp`,
`enKrakenInterrupt`, `enKrakenConfuse`, and `enKrakenArmorUp`; two failed native
proficiency attempts remain preserved as failed attempts. Requiring live
ATTACKCRIT or ATTACKPROF would require interference with the native controller or
weapon request and is prohibited.

The death run issued 43 ordinary no-focus hero attacks. Across both reports, 45
`StartEngageAttack` calculations precede 44 applied `RespondToHit` results in
exact order. One calculation was never applied and remains explicit instead of
being rewritten as a hit. The last applied ordinary attack reduced Kraken health
to zero, entered DEATH, never returned from the terminal, and completed natural
adapter, sampler, mesh, material, and texture teardown. The separate victory run
entered VICTORY after an ordinary party loss and completed the same teardown.

Attack state exits differ despite sharing4771:

| State | ExitTime | TransitionDuration | Destination offset |
|---|---:|---:|---:|
|ATTACK|.9420166612|.0495798476|.0045462879|
|ATTACKCRIT|.9304009080|.0695991218|.0047062663|
|ATTACKPROF|.9194134474|.0805865303|.0023531930|

Durations are normalized source-state durations (HasFixedDuration=false), not
seconds. Four seconds/241frames cannot cover the entire7.17-second attack and
return to idle. The production observer therefore retains entry, the full action,
all listed callback windows, native exit blend, and post-exit idle. State-clock
sampling retains the observed destination offset; never align by guessed
wall-clock seconds or visually fit a time shift.

## Structural-only roles and unsupported conditions

AnyState Intro targets INTRO, which shares krakenIdle with IDLE; INTRO also exits
to IDLE. These can require the same clip evaluated at two distinct clocks.
AnyState Appear/Death/DeathLight permit self-transition; repeated appearance or
death-variant transitions may likewise produce duplicate clip roles. The current
fixed-four rejection is correct for its measured scope but cannot cover these
production states by aggregating their weights or choosing one clock.

A bounded sampler uses two permanently connected banks of all5 clips (10 clip
playables plus mixer), one bank per current/next role. Bind before initialization;
all unused weights/time remain zero.
Two appearance contributors also require an explicit policy decision and proof;
the existing one-main/one-appearance policy does not establish that case.

Retain explicit rejection for changed controller/avatar/source palette, additional
layers or blend trees, multiple clips per state, unknown states, unsupported speed/
time conventions, invalid raw weight sums, missing bones/binds, and unproved
interruption/re-entry combinations. Rejection must remain visible and must not
be reported as full production compatibility. Paused native animation is a
synchronization condition, not authority to advance independent time.

## Satisfied campaign and remaining acceptance

The production observer campaign now satisfies exact-route binding, natural
IDLE, ATTACK, DEFEND, DAMAGED, DAMAGEDHEAVY, DEATH, and VICTORY observations;
all required natural edges; all four successful Kraken proficiencies; ordinary
non-cheat lethal gameplay; ordinary party-loss victory; and natural teardown in
both fresh owners. The compact immutable production-observation archive verifies
independently.

Canonical candidate coverage still requires root visual review for appearance,
continuous idle, attack, hit, and death motion, camera fit, culling, portrait,
and gameplay progression, followed by the canonical route archive. The included
Discord screenshot is supplemental still evidence and does not satisfy those
gates. Historical `KillSingle` evidence remains fixture-only; the new ordinary
lethal campaign is the authoritative gameplay proof. No production credit is
claimed for structural-only roles unless future source and exact-route evidence
establishes their natural reachability.
