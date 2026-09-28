# Blacksmith full release pass, 2026-09-26

**NO GO: not release ready.** The frozen candidate passes source checks and the
recorded isolated macOS trials, but the published helper rejects its affinity
field, Cat and Demon forearms intersect the armor, and distribution entitlement
is not established. No package payload, banner, catalog, or publication is
changed by this review.

## Frozen candidate

`blacksmith-forge-gear-0.1.0-dd48a48e0c2f.zip` has SHA-256
`dd48a48e0c2f385dca8b72802111c6f15c83b45300fe775b563d50a6e2e6b732`.
Its 143 files comprise manifest and content JSON plus 141 runtime assets
(81 GLBs and 60 PNGs), defining 30 equipment entries. All archive files match
the package bytes. The original banner remains unchanged, SHA-256
`bd129c59d2a0c2df7f09432ac852e690396200c501eabf397768cfe505b26a2b`.
See the [offline pass](OFFLINE-RELEASE-PASS.md) for exact build, unit-test,
archive, and published-helper evidence and [model correction](BANNER-MODELS.md)
for source lineage and earlier fitting observations.

## Gate matrix

| Gate | Result | Evidence and limit |
|---|---|---|
| Source definitions, assets, and local archive | PASS | 30 entries, four bands, 141 pinned assets; current helper validation and 143-file byte comparison passed. |
| Tooltip and text presentation | OPEN BLOCKER | User reports unclean tooltip rendering and text requiring cleanup; correction and native review remain outstanding. |
| Hammer wield orientation | OPEN BLOCKER | User reports the hammer is wielded sideways; grip/orientation correction and native attack review remain outstanding. |
| Published compatibility | FAIL | Actual published macOS helper v1.5.0 exits 1 with `content.json: json: unknown field "classAffinity"`. Package minimum 1.0.1 is not verified compatible. |
| Human Kilnward fitting | SAMPLED PASS | Female v12 front, three-quarter, rear, block, one-handed attack, and victory views; inspected male v11 model bytes are unchanged in v12. Studio camera loses attack-lunge framing. |
| Cat and Demon fitting | FAIL | Both show native forearm details intersecting the authored bracers. These are release blockers. |
| Skeleton, Merling, Goblin fitting | SAMPLED ONLY | Supplied views show clean bracer steel; full pose, route, and race-motion acceptance is not established. |
| Two-handed motion | OBSERVED | Native attack and victory motion captured in `release1twohand`; this is motion evidence, not balance or complete animation coverage. |
| Shared acquisition registration | PASS, LIMITED | All 30 rows register in the intended shared pools. No natural purchase or drop was observed; rates remain unverified. |
| Native panel disable and enable | PASS, ISOLATED | Manual disable followed by restart registered zero package rows; re-enable registered 30. |
| Manual removal and reinstall | PASS, ISOLATED | Removal registered zero rows; reinstall restored the exact 143 files and 30 rows. Native Resume passed. |
| Managed public install and update | BLOCKED | Published-helper compatibility failure prevents a supported public flow. Manual isolated lifecycle checks do not substitute for this gate. |
| Native Fire Cave combat smoke | TWO ROOMS PASSED, LIMITED | Native menu entry and two ordinary fights completed; all three heroes survived. Party level 14 versus enemies 6-7 is rendering/combat smoke, not end-game balance. |
| Other native classes | NOT TESTED | Unrestricted definitions do not prove fitting on other classes. |
| Multiplayer, Windows, Linux | NOT TESTED | No parity, online co-op, or cross-platform acceptance. |
| Distribution entitlement | MISSING EVIDENCE | Applicable account/export entitlement and agreements are not recorded. Generation receipts and spending do not establish distribution rights. |

## Native Fire Cave scope

Native menu entry and its ordinary click handler entered Fire Cave. Room 0 was
won using five hero actions with `cheat=None` and no Focus. All three heroes
survived: Blacksmith 186/186 HP, Hunter 124/150, and Scholar 119/141. A native
hammer hit dealt 32 damage to Mind Bender. The party was level 14 against
level 6-7 enemies, so this is combat and rendering smoke evidence, not a
representative end-game balance pass. Room 1 was won using four ordinary hero actions. After native loot collection,
room 2 Ready was observed with Blacksmith 168/186 HP, Hunter 98/144, and
Scholar 119/141. No forced acknowledgements or victory actions were used.
The final dungeon, Harazuel, remains locked by campaign progression; final-boss
balance is not established. The isolated fight save was preserved before
restoring the clean overworld checkpoint for tooltip and grip corrections.

All 81 runtime helper boundary tests passed with
`python3 -m unittest discover -s tools/ai-model-pipeline/runtime-test -p 'test_*boundary.py'`.
The latest deployed test-helper SHA-256 is
`cb58718dfe3da90998d793baaf83488970a4ffa69653dd61a20176b8aac25366`.
These checks do not alter the frozen package archive or public compatibility
failure.

## Race fitting blocker

The [race review](../../art-experiments/blacksmith-forge-rodin/banner-faithful/apparel/race-fit-review-v12.json)
records the actual runtime visual findings without inspecting native surface
geometry. Cat and Demon forearm protrusions break through both steel bracers;
Demon details extend beyond their silhouette. The captured renderer identities
show no separate forearm-detail renderer. The review recommends a separately
scoped race-aware apparel selection capability and deliberately authored variants,
followed by native race/sex pose and lifecycle checks. It makes no geometry change
and does not claim that a universal clearance increase resolves the issue.

## Rights and release decision

No saved applicable distribution grant or account/export entitlement accompanies
the model receipts. Rodin-specific legal language includes a broad rights clause
subject to applicable agreements; general plan conditions may also apply. This
review does not assert that every Rodin export requires a paid subscription.
The account's actual entitlement and applicable terms remain unknown and must be
established before distribution.

Four corrective generation jobs total 2 quoted credits, not confirmed billing.
Fitting and material revisions used no further paid generation. Those facts
establish cost provenance only. Published-framework compatibility, race fitting,
rights evidence, and the remaining matrix gates prevent a release-ready verdict.
