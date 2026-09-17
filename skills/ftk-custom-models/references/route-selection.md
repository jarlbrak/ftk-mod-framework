## Find the project and current evidence

Part of the [ftk-custom-models skill](../SKILL.md). Read that entry point first: it
owns route selection, the working method, and the non-negotiables that apply here.

Use the active FTK checkout. If invoked elsewhere, resolve this skill's real
filesystem path and locate the repository containing `FTKModFramework/` and
`tools/ai-model-pipeline/`. Do not guess a Steam installation or deploy to a
second checkout's game. Read its `AGENTS.md` and `CLAUDE.md` where present.

Read [the model guide](../../../docs/CUSTOM-MODELS.md) for integration and
[the authoring workflow](../../../docs/MODEL-AUTHORING.md) for the actual exercise.
For a different rig, also read [the skeleton register](../../../docs/MODEL-SKELETONS.md).
Enemy multipart assignments use [the renderer API](../../../docs/MODEL-RENDERER-API.md).
Player avatars use [the class/skinset API](../../../docs/MODEL-PLAYER-API.md);
equipment assembly and cloned-avatar lifetime need separate validation.
The exporter contract and executable commands live in
[the tools guide](../../../tools/ai-model-pipeline/README.md).
Use [the topology coverage plan](../../../docs/MODEL-TOPOLOGY-COVERAGE.md) and its
linked ownership finding to select the real route: direct enemy row,
resource-prefab override, or player skinset avatar. An unsupported empty
renderer is not a strict-swap candidate. Ownership resolution classifies the
inventory; it never transfers evidence between routes or approves a model.
The plan's `Original / any / known pairs` column identifies whether an exact
topology has an indexed authored example, broader evidence, and known source
pairs. It never approves an unrecorded pair or transfers a live result.
When selecting among already-authored packages, regenerate and read the
[package readiness ledger](../../../docs/MODEL-PACKAGE-READINESS.md). It runs each
profile document through the current route preflight and distinguishes an exact
indexed original record from diagnostic-only evidence and a merely local
validation file. A named, pinned historical follow-up in the runtime index is
an explicitly indexed supplement, not evidence for a newer profile revision
without comparing its pinned profile/catalog hashes.
Then read the [candidate validation coverage report](../../../docs/MODEL-CANDIDATE-VALIDATION-COVERAGE.md).
It joins the topology ownership plan to the structured gate and archive-
integrity ledgers and gives a strict route-specific follow-up queue. A canonical
representative is one exact-source-identity archive with all required evidence
shapes, exactly the complete renderer set for that identity within one topology
group, and an independently verified immutable artifact. Single-renderer and
multipart sources follow the same rule. It never combines fields from separate
archives, approves an unrecorded sibling pair, or makes an art-quality verdict.
Bronzewake V4 through V7 are the multipart reference: body, hair, armor and
boots each needed a separate exact-source canonical archive even though every
stage verified all four assignments on the same owner.
When one source profile binds renderer paths from different topology groups,
scope each canonical archive to the selected source renderer and topology.
Preserve companion assignments and assets as same-owner context, but grant no
route credit from their presence alone. Repeat live motion capture and root image
review with each companion renderer selected before closing its topology route.
A whole-owner archive can remain useful historical evidence while still being
noncanonical across topology boundaries. Rimecrown V4 through V8 are the
head, scarf, base, hat and middle-body references for this source-scoped
multipart rule.
Honeyback V3 is the single-renderer counterpart: one exact
`bearB / enBear01 / 121467` archive carries the complete route evidence, while
its earlier portrait-camera records remain separate presentation checks and do
not replace the canonical full-body run.
Duneshade V2 is the single-renderer ragdoll counterpart. Its exact
`snakeDesertA / enDesertSnakeA / 121552` route shows how to distinguish the
short native `Snake_DeathBig` handoff from the physical phase: prove the
animator-disable frame, per-body kinematic transition, measured movement and
settling boundary from telemetry, then use reviewed originals to judge whether
the authored body remains visually coherent. `m_DoRagdoll=true` alone is not
enough to claim observed ragdoll behavior.
Ashfang V2 is the same ragdoll method on the exact `wolfA / wolf01 / 121142`
route. Its animator remains enabled with all 14 bodies kinematic through death
frame 26, then disables as all 14 bodies become dynamic at frame 27. Measured
motion spans frames 28 through 48 and is zero through frame 119. Keep the
ordinary 58 to 50 HP hit separate from the explicit 50 to 0 KillSingle fixture,
and record that its authored main texture still inherits native `wolfA_e`
emission rather than claiming a base-color-only material.
Sargassum primary V2 is the single-renderer animated teardown counterpart. Its
exact `krakenTentacle / krakenTentacle / 121595` archive records complete idle,
enemy attack and ordinary-hit captures before an accepted 91-frame fixture
death prefix. Because `m_DoRagdoll=false`, use the observed `Tentacle_Death`
animator state and the exact renderer boundary. Frame 90 is inactive and not
visible with the animator disabled, and the next sample reports renderer
destruction. This proves sampled animated withdrawal only; it does not prove
full death duration, cleanup causality, corpse lifetime or final disposal.
Abyssal Crown V5 is the mixed skinned-and-rigid Kraken Head counterpart. Its
exact motion topology is `krakenHead / kraken2 / 121035`; the same-owner rigid
`Root_M/base/body/neck/eye/kraken2_eye` MeshRenderer is required structural
profile context and does not receive a second skinned topology or motion claim.
Archive all four authored assets and pin the stage result that proves both
assignments. Review the rigid insert across idle, attack, hit recovery and
disappearance even though motion telemetry targets only `kraken2`. The accepted
death prefix keeps `krakenDisappear` on frames 29 through 89 and retains the
same motion renderer inactive and not visible at frame 90 before the next sample
reports destruction. Because `m_DoRagdoll=false` and the source has zero
rigidbodies, describe this as animated disappearance. Do not infer cleanup
causality, corpse lifetime or final disposal from the teardown boundary.
Moonreed V2 is the single-renderer animated persistent-death counterpart. Its
exact `fairyA / enFairy01 / 121395` archive preserves four complete captures
because the first ordinary hammer attempt is a native dodge and the second is
the required 58 to 55 HP damage sample. Keep both attempts with distinct archive
keys. `m_DoRagdoll=false`, and the animator remains enabled while `fairy_die`
persists through frame 119. Two nonkinematic rigid bodies belong to inactive
break props, so they are not body ragdoll evidence. Review the small model and
strong native attack effects with explicit visibility limits, and do not turn
bounded death persistence into a general corpse-lifetime or cleanup claim.
Mirewarden V3 and Gloamcap V3 are the route-boundary pair for a topology group
shared by two integration kinds. The exact `trollCaveA / enTroll01 / 121153`
archive credits the direct-enemy route only. The separate exact
`impA / enbaseyimp / enBaseyImp / 121117` archive credits the resource-prefab
route only. Neither route transfers evidence to the other or to a sibling source.
Generate and read the [execution queue](../../../docs/MODEL-VALIDATION-EXECUTION-QUEUE.md)
after candidate coverage. It starts with the candidate ledger's one priority
target, matches only an exact static-passing profile document, and lists an
unmatched target as a profile-authoring task. An explicit resource ownership
finding can instead require an adapter or retarget design. Enemy matching
requires route identity plus renderer ID/path; player matching also requires
the named profile and skinset. Choose one listed profile revision explicitly.
The queue is an execution list, never proof that a sibling profile or source
pair is covered.
Treat local source and game assets as authority over prior summaries.
