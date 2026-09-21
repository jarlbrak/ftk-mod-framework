# Paladin local beta acceptance

This guide records bounded evidence against the [design](DESIGN.md) and
[39-entry source package](../../marketplace/packages/paladin/content.json).
**Beta delivery is not yet accepted.** The previously tested Guardian integration
cleared spent rescue usage during native revival. The fix is built in framework
`dd078282b5bfb8f8c55b3eea0837eeae70b8b4d2fe922539c542870ec45e5c7e`,
with 133 pure-rule and 21 installed-metadata checks passing. Its [native spent-charge revival and Guard-recast regression passed](guardian-revival-live.json). See [verified native callers and live gates](guardian-revival-native.json)
and [earlier lifecycle observations](guard-lifecycle-observations.json).

The current unpublished package SHA-256 is
`92dd4d19d8efbcaa177423182c9d4ac9476b2f79f35ac6d98c4779b294c24295`.
It contains **137 runtime files: 135 original equipment assets, manifest and
content**, with one class, two Censure actions and 36 gear entries. It corrects
all 36 equipment `m_CollectLoreItemUnlock` values to empty strings. The earlier
`43ad600ec7ca` archive is historical and must not be used as the current candidate.
Each receipt pins its own framework, helper, package or fixture bytes. A later
framework fix needs its own proportional evidence; no receipt approves untested
bytes or release publication.

## Current character and art scope

The user's revised scope preserves native FTK bodies, faces and hair, all seven
Blacksmith appearance entries and normal race unlocks. Only equipment is custom.
There are no class body or backpack replacements in this package. Earlier
custom-body, custom-backpack and portrait work is not a current acceptance gate.
See the [native appearance contract](native-appearance-contract.json).

The [native character preview receipt](native-character-armor-preview.json) and
[third armor revision](armor-revision3-review.json) cover the six sets on actual
Female and Male avatars, including front and three-quarter views in revision 3.
The slimmer novice silhouette and shaped toes/soles supersede earlier art. A
[Mercy race preview](native-race-preview.json) covers front views on Female, Male,
Undead, Cat, Demon, Fish and Goblin and restoration without inventory changes.
These are pose/fit observations, not full animation or all-set nonhuman fit
acceptance. Final art approval remains separate. See [redesign notes](NOVICE-REDESIGN.md).

## Current evidence matrix

“Bounded pass” means only the named observation passed. Composite checklists below
remain open when they include unobserved cases. Fixture equipment, supplied damage
outcomes and controlled loot are identified separately from ordinary acquisition.

| Boundary | Evidence and scope | Remaining boundary |
| --- | --- | --- |
| Source and export | [Equipment manifest](../../art-experiments/paladin-equipment/manifest.json), [delivery validation](../../art-experiments/paladin-equipment/delivery-validation.json), [package provenance](../../marketplace/packages/paladin-assets.provenance.json), and [native field audit](field-type-audit.json) | Rerun relevant checks after source/asset changes; offline checks do not prove live fit |
| Current package lifecycle | [92dd lifecycle](package-lifecycle-loot-fix.json): real helper install/activate, exact 137-file bytes, disable, enable, remove and rollback; managed-set export | Export is not a ZIP exporter. No public catalog download, disabled-save loading or online claim |
| Production startup and new game | [Production new-game receipt](production-new-game-observation.json): 39/39 registrations, five novice pieces equipped in their native slots, no all-gear grants | Earlier unexplained configuration crash remains in that receipt; final framework changes need proportional startup checks |
| Enabled save/resume | Same [production receipt](production-new-game-observation.json): matching starting equipment after same-process save/resume and application restart. [Collected loot receipt](custom-loot-collection.json): collected count retained after same-process resume | Disabled-package recovery, every appearance and full-campaign saves are not established |
| Acquisition and world return | [Native shop purchase](native-shop-purchase.json): one ordinary starter great-hammer purchase. [Current one-handed purchase](native-hammer-purchase-current.json): native spare-boots sale funds an 11-gold Novice Hammer purchase, with matching stock and backpack deltas. [Existing native stock](acquisition-stock-live.json): five families selected into shops. [Corrected custom loot](custom-loot-collection.json): one controlled Novice Helm append, native Collect adds exactly one and returns to world | Shield, armor, boots and helmet purchases remain unobserved. Existing stock alone does not prove purchase; controlled loot does not prove random-drop distribution. Current hammer purchase persistence remains untested |
| Guard and rescue | [Native outcome fixtures](guardian-native-fixtures.json): 9 damage becomes 5, lethal hit leaves 1 HP. [Multi-Guardian fixtures](multi-guardian-native-fixtures.json): no reduction stacking, one ordinal-first charge, later lethal death after charge spent. [Ordinary hit](ordinary-guard-hit.json) confirms unforced protected-hit route | Outcome fixtures bypass native damage calculation. [Revival charge preservation](guardian-revival-live.json) passed after native Revive and Guard recast; online unverified |
| Guard lifecycle | [Lifecycle observations](guard-lifecycle-observations.json): switching, next-turn expiry with retained designation, death disables protection, revival does not restore active Guard | Spent-charge revival passed in the linked regression; [Same-process ordinary next-combat recharge](next-combat-live.json) passed; [Dungeon reused-dummy recharge](dungeon-recharge-live.json) passed; [Native stun apply/remove](incapacity-live.json) passed; natural timed recovery and petrification remain separate |
| Base focused healing | [Focused hit](focused-hit-native.json): native Focus debit and attack, chosen ally 9 to 11 HP after Guard expiry | Miss/dodge, absorbed-hit, multi-target deduplication and caps are not all proven live |
| Mercy | [Guard healing](mercy-guard-live.json) and [minimum heal](guard-heal-minimum-native.json) pass bounded casts. [2H focused healing](mercy-focused-live.json): ally 30/39 to 34/39, matching 12% floor rather than base 8% | Focus helper returned unknown after selection change; separate ready snapshot confirms debit. No 1H bonus, cap or acquisition claim |
| Censure | [Both weapon effects](censure-live.json) apply armor -4/-6 and expire; [native lifetime](censure-native-lifetime.json) establishes timed combat expiry | Slot/damage variants and equipment changes remain separate checks; not target-turn expiry |
| Ward | [Representative native group Poison](ward-live.json): protected Paladins reject effect, unguarded non-Guardian wearing same shield receives it | No live proof of all four categories or damaging Poison bite; no pre-transform outcome or poison-counter snapshot, individual immunity not separately queried |
| Verdict | [Native retaliation](verdict-live.json): one 4-damage retaliation for a two-target attack and unguarded control | Not every outcome/target combination; stronger weapon damage remains a separate comparison |
| Current art and resources | Native previews/races above; framework renderer transaction and lease checks below | [Mercy motion](armor-combat-motion.json) and [apparel retirement](apparel-retirement-live.json) passed within their scope; separate rigid-instance disposal and final visual acceptance remain open |
| Marketplace UI | [Managed UI](managed-ui-native.json), [title recreation](mods-title-recreation-native.json), [same-process resume](same-process-resume-observation.json) establish their pinned revisions | Earlier package bytes; current CLI lifecycle is separate. No public download claim |
| Online and platforms | Unverified | Explicit beta disclosure; production requires community host/client evidence. macOS trials do not establish other platforms |

## Coverage of all 39 entries

The class is `paladin`; actions are `paladin_censure_fracture` and
`paladin_censure_fracture_1h`. The other IDs are `paladin_{family}_{set}` for
`hammer_1h`, `hammer_2h`, `shield`, `armor`, `boots` and `helmet`.

| Set | Acquisition levels | Current visual evidence |
| --- | --- | --- |
| novice | 0-1 | Native Female/Male front and three-quarter revision-3 previews |
| oathkeeper | 2-3 | Native Female/Male front and three-quarter revision-3 previews |
| highward | 4-6 | Native Female/Male front and three-quarter revision-3 previews |
| mercy | 7-13 | Same previews; seven native appearances in a separate front-view trial |
| censure | 7-13 | Native Female/Male front and three-quarter revision-3 previews |
| verdict | 7-13 | Native Female/Male front and three-quarter revision-3 previews |

Retain actual IDs, equipped slots, native appearance and asset hashes. An outfit
preview does not establish both hand controllers, every loot card or animation.
Earlier all-gear fixture captures remain useful loading evidence for their pinned
revision, but cannot accept current art or ordinary acquisition.

## Native gameplay checklist

- [ ] `paladin`: immediately selectable with all native appearance choices; native body/hair,
  start weapon, shield and three novice armor items present. Record whether native
  starting items equip or enter the backpack. Guard works without a shield and
  with both weapon types; other classes and global taunt keep native behavior.
- [ ] Guard targets another living ally, always succeeds and consumes one action.
  One active target per guardian; switching moves designation/protection. Direct
  attacks, including AoE, receive one 50% reduction after native defenses;
  damage-over-time does not. Record odd-damage rounding and one-point hits.
- [ ] Guard expires at the guardian's next turn. Stun/incapacitation/death cancels
  it immediately and recovery does not restore it. Designation survives ordinary
  expiry; combat teardown clears all transient state.
- [ ] A focused hit heals the designated living ally once per committed attack
  for 8% maximum HP, rounded down and capped by missing HP. Test miss/dodge,
  multiple Focus, multi-hit/AoE, full/dead target and no overworld healing.
- [ ] Divine Intervention requires active Guard and a qualifying lethal direct
  attack after reduction, leaves one HP, and spends one charge per Paladin per
  encounter, including after the guardian dies and is revived. Multiple guardians do not stack reduction or consume two charges
  for one lethal hit. Next combat restores charge availability.
- [ ] Shield variants apply their declared Guard recovery, debuff ward or
  retaliation and retain the personal defense/Speed tradeoff. Guard stays 50%.
  Mercy weapons add the correct focused-heal percentage; Verdict damage differs.
- [ ] Both Censure IDs appear only on their intended weapons. Observe the native
  timed armor effect, actual duration and damage, three versus four slots and
  magnitudes four versus six. Both native armor deltas and timed expiry are
  observed in [Censure live evidence](censure-live.json). The verified lifetime
  is approximately 3.333333 synchronized combat-time units, not a target-turn
  duration; see [native lifetime evidence](censure-native-lifetime.json). Remaining
  slot/damage and equipment-change cases keep this composite requirement open.

## Visual, lifecycle and save checklist

[Representative Mercy combat captures](armor-combat-motion.json) record native
male one-handed attack and damage reaction, plus female two-handed attack and
death animation samples on current armor. Reviewed boot volumes remain attached
in the sampled poses. This is one set on two native appearances, not complete
set/race or lifecycle acceptance.

- [ ] Native Party Select, overworld and combat display the intended original
  equipment on native appearances; visible Guard/item icons are readable.
  Custom bodies, faces, hair, backpacks and class portraits are not current
  asset acceptance requirements.
- [ ] Every set's body/boot/head equipment swaps correctly; default apparel returns
  on unequip. No native fallback, wrong branch, dropped joints, unwanted tint,
  clipping, detached pieces or camera culling in observed states.
- [ ] Capture ordinary idle, attacks, incoming damage and applicable death/break
  behavior. Keep explicit animation playback and damage/kill fixtures separate
  from ordinary combat. Natural hammer break is not applicable: all 12 rows
  inherit `m_CanBreak=false` from `bluntSmithHammer` or `bluntWarHammer`.
  Native `DamageCalculator.StartEngageAttack` requires that flag for a zero-slot
  success weapon break; the forced-miss cheat bypasses eligibility. Original
  fragment coverage is still checked offline, without claiming a normal break.
- [ ] Native avatar rebuilds retain the correct resources. Watch retired leases
  and verify final-owner disposal without manually invoking cleanup. A Ready
  state or disappearance alone is not disposal evidence.
- [ ] Observe a normal level-appropriate shop or loot acquisition for each family;
  for shops, record campaign-stage item level and shop object-type pool, not just
  hero level. The [native selection audit](acquisition-native.json) explains the
  inclusive level filter and limited shuffled selection from eligible items.
  Distinguish this from a forced inventory fixture. Save/load the actual equipped
  custom items and both class appearances. Confirm no stale combat protection
  enters the next encounter; record package disable/re-enable limitations.

Use the [player model workflow](../MODEL-PLAYER-API.md),
[model authoring workflow](../MODEL-AUTHORING.md), and
[isolated runtime helper guide](../../tools/ai-model-pipeline/runtime-test/README.md)
for native discovery, captures and guarded actions. Keep bulk media and game data
outside public history. Record bounded failures as failures, not implied coverage.

## Reproduce game-free checks

Run from the repository root on a configured machine. These commands do not
launch FTK and do not establish any live checkbox above.

```sh
dotnet build FTKModFramework/FTKModFramework.csproj -c Release
dotnet run --project FTKModFramework/Tests/GuardianCombat/GuardianCombat.csproj -c Release
dotnet run --project FTKModFramework/Tests/PackageModels/PackageModels.csproj -c Release
dotnet run --project FTKModFramework/Tests/ItemApparel/ItemApparel.csproj -c Release
dotnet run --project tools/ai-model-pipeline/mesh-transaction-tests/MeshTransactionTests.csproj -c Release
(cd launcher/helper && go test ./...)
python3 art-experiments/paladin-equipment/validate.py
scratch/paladin-venv/bin/python art-experiments/paladin-characters/loot-display/validate.py
scratch/paladin-venv/bin/python art-experiments/paladin-equipment/loot-display/validate.py
scratch/paladin-venv/bin/python art-experiments/paladin-equipment/shield-display/validate.py
python3 art-experiments/paladin-equipment/validate_delivery.py
python3 marketplace/packages/validate_paladin.py
git diff --check
```

Rebuild and validate the actual local archive with the commands in the
[package README](../../marketplace/packages/paladin/README.md#build-an-unpublished-local-candidate).
Retain the builder's complete receipt, not only console success. Fixture
preparation is a package gate; it does not activate the package or run gameplay.

## Beta and production decision

The built revival charge fix passed the native same-combat revive and Guard-recast
regression. Before beta delivery, complete the remaining applicable local
gameplay/visual/lifecycle checks, and pair the final
framework with the immutable package and honest evidence summary. Do not rerun
unaffected package or save checks solely to expand coverage. Any remaining
campaign, platform and appearance limits must be explicit. A build or an offline
asset validation is not live acceptance.

Production additionally waits for community host/client evidence for matching
content, Guard switching/expiry, non-stacking, single rescue consumption, focused
healing, equipment effects, save transitions and multiple Paladins. Retain game
build, package/version, roles and outcomes without personal saves or credentials.
No release or publication approval is recorded here.

## Historical investigations and superseded candidates

These receipts remain available for their exact claims, failures and fixes. They
do not approve current asset bytes or superseded custom-body requirements.

- [Initial registration](startup-registration.json), [combat-entry failure and retention fix](combat-entry-investigation.json), and [modifier-card failure](modifier-card-failure.json).
- [Initial package lifecycle](package-lifecycle.json), [helmet binding](package-lifecycle-helmet-binding.json), [helmet mount](package-lifecycle-helmet-mount.json), [hammer fit](package-lifecycle-hammer-fit.json), [shield fit](package-lifecycle-shield-fit.json), and [43ad armor revision](package-lifecycle-armor-revision3.json). The shield-fit archive contained 163 files; it is not the current 137-file package.
- [Earlier item-card trials](item-display-native.json), [corrected custom-body gear trial](corrected-gear-native.json), and [all-gear inventory resume](all-gear-save-native.json). These predate the native-body/equipment-only scope and current art.
- [Rejected flat foot plates](armor-revision2-review.json), superseded by revision 3. The character-directory [geometry proof](../../art-experiments/paladin-characters/original-geometry-proof.json) and [validation](../../art-experiments/paladin-characters/validation.json) now describe current equipment exports; their directory name does not imply shipped custom bodies.
- [Post-revive input observation](post-revive-input-observation.json), [native Fight continuation](native-fight-continuation.json), and [FSM trace](native-fight-fsm.json). Direct enemy placement selected an unsuitable encounter path; adjacent placement followed by native walking resolved entry. These trials do not justify a production event substitution.
- [Native walk and loot failure](native-walk-and-loot-failure.json): invalid `False` lore identifier caused repeated collection attempts. The trial stopped without saving. The corrected 92dd [exact-once collection and world-return receipt](custom-loot-collection.json) supersedes that failed boundary, without erasing it.

## Reproduce isolated package lifecycle

Use the actual helper against a fresh scratch state, without a game launch or
production catalog mutation:

```sh
python3 marketplace/packages/verify_paladin_lifecycle.py \
  --helper scratch/paladin-package-helper \
  --archive PATH_TO_LOCAL_ARCHIVE --descriptor PATH_TO_LOCAL_DESCRIPTOR \
  --game-assembly PATH_TO_ISOLATED_ASSEMBLY
```

The [current lifecycle receipt](package-lifecycle-loot-fix.json) pins package and
helper identities. Retain exact inventory and request/results, not console success
alone. This procedure establishes package handling, not gameplay or save behavior.

### Apparel replacement resource lifetime

[Live apparel retirement](apparel-retirement-live.json) observes replacement on one native Male avatar in the overworld and again with its combat clone present. The old body/foot lease disappears and all six pinned Unity resources become null after each replacement. Combat entry retains the new lease with two references; replacing it updates both avatars while the other two heroes retain their leases. Equipment changes use the bridge and already-owned fixture gear. This does not validate ordinary combat equip cost or separate weapon, shield, and helmet resource retirement.

[Dungeon rescue consumption](dungeon-rescue-live.json) passed in an isolated three-hero Crypt fixture. Native reward collection and post-combat revival reached Ready, followed by a generated trap room. Recharge at the next dungeon combat remains unverified.

[Dungeon recharge](dungeon-recharge-live.json) passes with the same native Paladin dummy retained between two staged wolf fights. Rescue is spent at the first victory and ready before Guard in the next combat; designation and active protection are cleared. The native reward/Ready transitions are exercised, with the two-room layout and lethal outcome explicitly identified as fixtures.

[Incapacity fixture](incapacity-live.json) observes native stun cancelling active Guard and native effect removal restoring eligibility without restoring Guard, while retaining designation and ready rescue. Petrification did not apply in the direct fixture because native surviving-hit context was absent; it remains a live gate. The synchronous helper now rejects that unsupported path.
