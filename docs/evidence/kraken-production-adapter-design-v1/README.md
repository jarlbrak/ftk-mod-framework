# Old Kraken production adapter contract

This evidence turns topology `6a28ac3cf4523c24` from a general adapter note
into a concrete implementation and acceptance contract. The dedicated internal
adapter now exists and has passed offline architecture review, fixture tests,
and the Release build. The separate
[production observer archive](../kraken-production-adapter-v1/README.md) now
proves exact live integration, native behavior, ordinary lethal death,
party-loss victory, and two-owner natural teardown. Root visual, camera,
culling, portrait, progression, endpoint-composition, and canonical route
archive review remain pending. The machine-readable [contract](contract.json)
keeps those statuses explicit.

## Offline implementation status

The route-specific implementation is
`FTKModFramework/Core/LegacyKrakenResourceAdapter.cs`, SHA256
`393447685551eedfff19fb63e2a8384be56627375ce1c7a670de24850a475c5b`.
Its focused implementation regression test is
`tools/ai-model-pipeline/test_kraken_production_adapter_implementation.py`,
SHA256 `7c5fbec363e892692f8d98a397944bcadca22ba658e6d32e9b7de93eb81c36a3`.
The reviewed implementation binds only the exact resource route, preserves the
real CEL and weapon controller as gameplay authority, samples from
controller-free owned graphs, authorizes the 15 pinned state identities and
time conventions, verifies owner and target identity before writes, validates
and reads back every commit or rollback, and destroys owned graphs before their
sampler hierarchies. The 15-state set is a static controller contract. It is not
a claim that the exact Kraken row naturally reaches every serialized state.

This implementation remains internal. It adds no generic `Content.*` API and
does not permit a generic `runtime-profile.json` for the resource route.

## Why the generic model route is invalid

The old resource prefab `enkrakenhead` uses renderer 121260 and the palette
`Root_M/joint1 -> neck -> head -> topHead`, with `jaw` under `neck`. The modern
Kraken controller drives `Root_M/base/body -> neck -> head -> topHead`, plus
modern base and eye paths. Its four main clips miss every articulated old
palette path. Only the shared `Root_M` can move both hierarchies directly.

The real combat setup also replaces the prefab Animator controller from the
equipped weapon. `CharacterEventListener.HookupWeaponForBattle()` assigns
`m_Weapon.m_AnimationController` to the real CEL Animator. Supplying a different
controller on a replacement prefab cannot remain authoritative through native
combat initialization. `Content.SetEnemyBodyMeshesFromGlb` only replaces
meshes, and `Content.SetEnemyBodyFromBundle` only supplies a CEL template.
Neither API implements the sampling, retargeting, scheduling, or cleanup below.

## Minimal safe runtime design

Keep the real CEL, weapon controller, state clocks, transitions, callbacks,
victim context, and weapon effects authoritative. Build one separate owned
playable graph with a controller-free modern transform hierarchy. The sampler
has events disabled and contains no attack state-machine behaviours. It
observes pose only and never dispatches gameplay events.

Map the four modern articulated endpoints to the four old articulated
endpoints. For each endpoint, compute:

```text
desiredOldModel = modernAnimatedModel * inverse(modernRestModel) * oldRestModel
desiredOldLocal = inverse(actualDesiredParentModel) * desiredOldModel
```

Snapshot every source before writing any target. Validate every output first,
then commit old locals from parent to child. Preserve the shared old `Root_M`,
the five-bone palette order, inverse-bind bytes, transform identities, and
parents. Reject nonfinite, singular, negative-determinant, or sheared matrices.
Require reconstruction and Unity readback error at or below `1e-5`. Any failure
restores every saved target local and disables the adapter for that owner.

Main-state sampling keeps the jaw at its old rest local transform. The old
appearance clip may animate the jaw directly. Current state, next state,
appearance, and main pose contributions must be sampled independently at the
real native clocks, then blended under a measured policy. A guessed wall-clock
offset or one combined sampler is outside this contract.

## Lifecycle and validation boundary

Use one adapter lease for each exact real CEL and renderer pair. Dispose the
owned graph before destroying its sampler hierarchy. Remove update registration
once, and let the existing visual owner release meshes, materials, and textures.
Native clones do not inherit acceptance: every clone needs its own identity,
controller, scheduling, and lifetime observations.

The existing fixtures prove the endpoint algebra, neutral recovery, rollback,
TRS rejection, repeat stability, and owned cleanup mechanics. The production
campaign used two fresh native owners. `native-combat-death` naturally observed
IDLE, ATTACK, DEFEND, DAMAGED, DAMAGEDHEAVY, and DEATH, the required idle edges,
all four Kraken proficiencies through the row's native Attack override, an
ordinary non-cheat lethal hit, native death, and final teardown.
`enemy-victory-terminal` reached native VICTORY through an ordinary party loss
and then tore down.

`PASSIVE VICTORY`, `DEATHLIGHT`, `ATTACKCRIT`, and `ATTACKPROF` remain in the
15-state structural inventory. They are not required live roles for this exact
row because no source-backed native route has been established for them. The
observer must not force them with Animator APIs, fake CELs, or altered weapon
authority. A `KillSingle` fixture may prove native DEATH entry and cleanup, but
it cannot satisfy the ordinary lethal gameplay gate.

The production archive verifies ordered native events and damage,
current-to-next sampling, exact Gloamfin binding on `enkrakenhead`, ordinary
gameplay terminals, and final resource disposal. Acceptance still requires
root-reviewed appearance, continuous idle, attack, hit, and death motion,
camera, culling, portrait, progression, endpoint composition, and a canonical
route archive.

Until those gates pass, the execution queue must retain
`adapter_visual_archive_review_required`, Gloamfin must not have a generic
`runtime-profile.json`, and this topology must receive no canonical-route
credit.
