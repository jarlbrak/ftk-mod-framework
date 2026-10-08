# Thief release-readiness checkpoint

Status: 2026-10-08, unreleased Thief 1.1.0 pistol candidate with framework/helper 1.9.0 dependencies. Public source and accepted assets are preserved on `content/thief-equipment-polish`. This checkpoint does not approve release or establish crash fixes.

## Issues and next work

The parent [Thief epic #157](https://github.com/jarlbrak/ftk-mod-framework/issues/157) retains the original design history. [Release-readiness spec #303](https://github.com/jarlbrak/ftk-mod-framework/issues/303) tracks the current candidate and its proof plan.

| Work item | Current result and remaining acceptance |
| --- | --- |
| [World-entry crash #304](https://github.com/jarlbrak/ftk-mod-framework/issues/304) | V393 failed before the new world observer. The recovered primary report has corrupted unwind metadata; no reliable managed callee or causal correction is established. V397 succeeded on a bounded cold route. |
| [Combat crash #305](https://github.com/jarlbrak/ftk-mod-framework/issues/305) | V169 failed at a native stack guard after the next Prof1 scheduling marker. V171 used a different proficiency and diagnostic layout. Cause, correction and corresponding-route retest remain open. |
| [Natural balance #306](https://github.com/jarlbrak/ftk-mod-framework/issues/306) | Two authorized natural fights remain, zero consumed: female Nightblade core versus mixed Locksmith opening, and male mixed Locksmith perfect positive Prepared hit with actual Focus cost and no core refund. |
| [Bow-save migration #307](https://github.com/jarlbrak/ftk-mod-framework/issues/307) | Genuine published-bow ownership/save and retained-ID pistol cold migration remain unverified. The additional isolated campaign needs specific approval. |
| [Artifact and website parity #308](https://github.com/jarlbrak/ftk-mod-framework/issues/308) | Accepted candidate bytes are pinned. Exact final committed release builds, clean packaging dry runs and prepared release/site parity remain gates. Platform and co-op coverage must match the final claims. |
| [Durable checkpoint #309](https://github.com/jarlbrak/ftk-mod-framework/issues/309) | Public branch plus a verified private snapshot, portable Git bundle, integrity manifests and private resume guide preserve the current work. The issue receipt records completion and exact remote commit. |

Read the [candidate contract](CANDIDATE.md) for mechanics and accepted presentation, the [validation history](VALIDATION.md) for bounded native evidence, and [framework candidate notes](../releases/v1.9.0.md) for shared dependencies. Earlier bow documents are historical 1.0.0 material.

## Verified checkpoint and limits

V397 cold-verified the preceding native autosave and completed-roll state, then completed genuine native Save and Exit, saved-title observation and separate native Exit0. Male action points naturally rerolled to three; the other 35 checked party scalars matched. Nullable per-hero location fields do not establish full transform equality. Original runtime, exact six-plugin inventory, protected profiles and six-save guards passed after restoration. No movement, EndTurn, combat, comparison fight or trace arming occurred in V397.

The newer checkpoint produced by that native Save and Exit still needs cold verification before gameplay. Its exact hash, size and original location belong to the private authority receipt. Historical recovery records contain superseded checkpoints and authorizations; use the current resume guide and latest authority rather than replaying an older handoff.

Game-free verification at this checkpoint includes the net35 Release build, 22 focused C# suites, helper Go tests, Thief package/generator validation, installer and launcher fixtures, release-manifest checks and instruction checks. HeadFaceClipper first failed because its required Managed-directory argument was omitted; the corrected actual synthetic-geometry invocation passed without launching the game. The current AgentQueue invocation passed 515 checks; earlier 535-check records retain their own invocation scope. Website build, dependency audit and desktop/mobile page checks are recorded in the checkpoint issue receipt. Sharp 0.35.5 replaces the advised 0.35.4 dependency. These checks establish their stated game-free scope only.

## Accepted package pins

| Artifact | SHA-256 | Scope |
| --- | --- | --- |
| Gameplay candidate ZIP | `2418e48b482577f0a9f04092c1f32a584dfb0ba0f5cf43f731beacbadc7dd2bf` | 93,492,583 bytes; 235 entries, including 233 assets and two JSON files; one class, 45 equipment identities and nine actions. Archive entries match the accepted package sources. |
| Marketing candidate ZIP | `8ddf27c2f31cfe450c2367d5fdfd848eb08bdcc2b3fd24350505c53293b916b7` | 19 entries, including 18 PNGs; separate from gameplay and native evidence. |
| Latest tested Core build | `16bfa4f792198a5577053d00c65784cd0b2313265263796435799bacda713e79` | Source-matching candidate used for the latest bounded native checkpoint. This is not a published release artifact. |

Accepted art and original immutable release archives remain preserved. Some ignored V179/V180 artifacts were absent from the recovered backup; surviving exact tool facts and hashes retain that qualification. The reconstructed V178 handoff has a separate origin receipt, rather than a claim that it came from the backup.

## Resuming safely

1. Use the private checkpoint's `RESUME.md`, integrity receipts, recovery state, V397 authority and retained latest evidence. Verify copied files and source commit before working in a new checkout.
2. Establish fresh original protected-file, six-plugin, normal-profile and six-save guards. External saves permit hash and size checks only; never parse, copy, edit or restore them. The recorded invalid checkpoint must never be resumed.
3. Cold-verify the latest native checkpoint before any gameplay. Keep V393 world entry and V169 combat investigations separate and within existing diagnostic scope.
4. Resolve the outstanding specific approvals for a one-time repeat of the last unsaved male movement/EndTurn segment and one additional isolated published-bow campaign before those actions. General progress requests and elapsed time are not substitutes for these approvals.
5. Complete the linked live gates and final clean release checks. Committing this source checkpoint does not satisfy the final release artifact gate. Publishing, tagging, feed/catalog updates, public deployment, paid generation, shared skill changes and expanded diagnostics need separate authorization.

Private raw diagnostics, game assemblies, local setup, authority records and captures are stored outside GitHub and outside app-managed worktrees. The portable Git bundle and public branch independently preserve reviewable source. The private snapshot remains local; the verified copy is not an off-device backup.

## Website impact

Candidate documentation and preview/projection tooling are preserved. Published catalog and Thief projections remain the released 1.0.0 bow collection. At an authorized release, update the catalog, Thief guide/cards/library, art/provenance, migration instructions, minimum framework/helper version, online limits, downloads and release notes together. Bounded macOS solo evidence does not establish other-platform or co-op acceptance.
