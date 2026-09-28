# Blacksmith redesign validation

## Helmet fit correction supersedes original acceptance

User review identified floating headgear in the final native screenshots. Prior
helmet fit PASS statements below are historical. The corrected v8 shell and v7
circlet use native multi-angle evidence and a same-appearance vanilla comparison.
Minor Demon ear intersections comparable to the vanilla Kettle Helm are disclosed;
floating placement and major skull breakthrough remain unacceptable. See the
[helmet correction record](HELM-FIT-CORRECTION.md) for the selected result and
rejected attempts. This correction changes four items' visual fields only.

## Candidate inventory

Four tiers each provide armor, boots, helmet, pendant, trinket, shield, one-hand
hammer and two-hand maul. The approved art ledger contains 170 assets. The runtime package uses 162;
eight original weapon icon renders are excluded to preserve native combat icons.
All original geometry has recorded Rodin lineage. The original rigid selection was final-v4; corrected helmet geometry and
presentations now come from helm-fit-v8. Other rigid equipment retains its
previous geometry, and boot cards use boot-cards-v1. The 32 diffuse maps use
1024px runtime exports with original 2048px sources preserved. The rebuilt unpublished archive
passes the unchanged 100 MiB marketplace limit and archive validation via a
temporary descriptor. The delivered local candidate receipt is deliberately not
a marketplace descriptor and pins the exact required framework binary. All exports satisfy the verified
16-bit importer vertex boundary and the category triangle budgets.

The first complete native load rejected eight display-only entries because the
builder emitted empty equipped-model lists. The builder now omits empty routes;
the subsequent load registered the package without those errors.

Native accuracy review caught another declaration error: `toughness` selects
Strength, not Vitality. The installed enum, CharacterStats mapping and JSON
alias confirm that all eight weapons must declare `skill: vitality`. The builder
is corrected. Earlier combat captures used the wrong governing stat; their
measured accuracy and damage must not be used as balance evidence.

## Visual checks so far

- All 32 native item cards were captured on final-v3. All 24 rigid cards had
  readable, unclipped model framing. Armor cards fit. Four boot previews were
  too small and were enlarged in the next revision.
- Eight equipped Female Blacksmith loadouts were captured, covering both weapon
  choices at each tier. The first three helmets sat too high and were lowered
  by 0.10 local Y in final-v4. Fresh native recaptures pass for the same Female inventory pose.
- The hammer head orientation is corrected from the previous sideways export.
  Idle presentation alone does not establish every attack or break pose.
- Long weapon bonus text overlapped the More Info prompt. The revised tooltip
  uses shorter lines; fresh native cards for the hammer, maul and trinket fit.

## Controlled mechanics smoke

The initial trial used a saved level-14 party against native Fire Cave enemies
at levels 6 and 7. This is mechanics evidence, not matched-level balance evidence.
Ordinary native attack rolls and AI ran; no forced hits or health boosts were used.

- Kilnward Temper granted the Scholar +5 Armor, raising the visible total from
  10 to 15, consumed the Blacksmith action, and spent no Focus.
- Temper was visibly disabled on the next Blacksmith turn. Its bonus remained
  after the Scholar's first subsequent completed turn and cleared after the second.
- Kilnward Overhand dealt 22 HP damage on an ordinary partial roll and lowered
  Blacksmith Armor from 34 to 28. Armor returned to 34 at the next Blacksmith turn.
- The first bridge Cancel invocation hit an inactive native dialog and failed.
  A later explicit keyboard selection of Cancel closed the picker, preserved
  Focus and the current turn, and left Temper available. The bridge visibility
  correction was subsequently deployed and passed the Bellowsworn cancel/retry
  check recorded below.
- A subsequent Set Hammer attack targeted the correct enemy and was dodged.
  No positive Armor bonus was observed, as expected for a dodge. Native logs
  identify both the dodge and the later enemy attack that damaged the Blacksmith.
- The next Set Hammer hit reduced the Assassin from 54 to 22 HP and increased
  Blacksmith Armor from 39 to 44. Armor returned to 39 on the next scheduled
  Blacksmith turn. Native motion frames were captured for review.
- Retained weapon roll slots obscured the fourth picker row. Temper now clears
  the local slot display before opening its picker; the fresh native picker shows
  all four rows clearly.
- A temporary bridge state-read timeout occurred before an action was submitted.
  The game recovered, and the captured stack showed the game loop still running.
  This did not establish a Temper deadlock.

The trial stopped before finishing combat to deploy the next revision. Runtime
captures and exact deployment hashes are kept in the local evidence workspace.

## Combat icon correction

An earlier build incorrectly used custom artwork on combat action buttons. The
corrected build removes the embedded PNG resource and custom sprite-loading path,
and borrows native `taunt`, `heavyattack` and `protect` sprites for the three
actions. Repository instructions, authoring skills and verification now explicitly
require native all-white outlined combat icons. The package validator rejects
weapon icon overrides, since native basic attacks use the weapon item icon.
The corrected native hammer basic-attack symbol, Set Hammer and Temper buttons
were observed beside vanilla actions on the corrected binary. Native selection
colors remain intact. Temper visibly greys out after use. Overhand was subsequently observed with its native heavy-attack icon in both
normal and selected states.

## Revised native receipts

A fresh level-2 party with Bellowsworn shield gear entered a synthetically staged
Bandit/Bone Charmer encounter. Native attacks use Vitality at 90% per slot. The
revised bridge successfully cancelled Temper, preserving all HP, Focus and the
Blacksmith turn. Retrying Temper on the Scholar granted +3 Armor, recorded two
remaining target turns, and marked the Blacksmith use spent. Set Hammer subsequently dealt 14 HP damage and recorded +3 Armor on the
Blacksmith. The party won with HP 55/59, 43/53 and 34/51, no deaths and no Focus
spent. Native loot awarded a Coalmark Forge Shield. The encounter rows were
staged, while reward selection and collection used native flows. A matched
basic-action comparison was not isolated by that trial; later native equipment
comparisons and the retained tuning decision are recorded below.

The same party then completed the next unmodified Cave room using ordinary
attacks against native enemy rows `skellymageA`, `ratthiefA` and `ghostA`. All three survived with HP 55/59, 43/53 and
25/51, and the royal chest and dungeon exit completed through native controls.
This is consecutive-encounter evidence on Easy difficulty, not a matched balance
comparison. Both session combat flags cleared normally on returning to the map.

## Final appearance observations

The corrected helper completed an initial 28 native inventory presentations: four tiers
across Female, Male, Undead, Cat, Demon, Fish and Goblin. Coalmark used its maul;
the other tiers used hammer and shield. Matching avatar identities across
appearance, inventory and studio receipts prove that the requested temporary
skins were rendered. Inventory and character fields were preserved, and each
appearance was restored after capture.

The visual review found no concrete static-fit defect in headgear, forearms,
grip or boots, including Cat and Demon bracers. This covers these 28 presentations,
not all weapon/skin combinations or moving poses. The private review ledger is
`art-experiments/blacksmith-forge-rodin/approved-redesign/native-final-skin-review/review.json`.

## Duplicate ownership helper correction

The native Coalmark shield reward created a legitimate second owned copy. The
isolated test helper previously required exactly one copy and consequently
rejected card inspection and equipment staging. It now accepts one or more owned
copies for cards and reports `ownedCount`. Granting already-owned items adds
nothing. Equipment staging preflights ownership in Backpack and the expected
slot, preserves one equipped copy and all extra Backpack copies, and verifies
aggregate counts of every item after native swaps, including displaced shields.
Already-equipped items do not invoke another swap.

The production guard and swap wrapper passed 17 executable offline checks with
a one-copy native-swap stand-in, covering duplicate Backpack ownership, an
already-equipped copy plus a spare, switching away and back, maul shield
preservation, and rejection of unexpected containers or changed ownership.
Three card boundary tests and the isolated helper Release build also passed
with no build warnings or errors. These offline checks were followed by the
native duplicate card and swap checks recorded below. The prepare-only
candidate was regenerated with the corrected helper pinned, preserving the
same package archive bytes. No public framework compatibility is implied.

## Matched-level balance plan

These are controlled test anchors, not conversions from native item bands.
Use the same native companion equipment and ordinary rolls for the basic-action
and kit-action comparisons. Record actual live totals rather than substituting
serialized base damage for measured damage.

| Tier | Hero and enemy level | Physical row | Magic row |
| --- | ---: | --- | --- |
| Coalmark | 0 | wolfA | hagA |
| Bellowsworn | 2 | banditA | skellymageB |
| Rivetwatch | 6 | banditD | hagC |
| Kilnward | 10 | deathknightC | wraithC |

Exercise shield and maul paths, the cost of Temper's lost attacking turn, and a
late native-gear comparator plus mixed outfit. Enemy-row staging is synthetic
encounter setup and must be labeled accordingly. It is not natural acquisition
or a completed campaign.

## Current completion status and evidence boundaries

Both weapon paths now have native combat trials at every tier after the Vitality
correction. Later entries record the native endgame comparator, the Rivetwatch
absorbed hit, current scheduled-turn expiry, selected action layouts and mixed
equipment/class eligibility. These supersede the earlier pending checklists.
The retained tuning decision is in [Gear](GEAR.md). Random outcomes and the
different Bellowsworn companion loadouts limit comparisons between those trials.

The corrected grant fixture passed a fresh native reproduction with preservation
verified. The mixed-wearer encounter finished with all characters alive. Final
verification and archive identities are recorded in [Delivery](DELIVERY.md); the
package remains unpublished.

The 35 static presentations and 32 cards cover current models, materials and
text. Following-camera Overhand reviews now cover all four tiers; the Kilnward
Set Hammer review also covers the shield route. Each motion review is scoped to
its sampled female human capture, camera angle and action. Every skin/weapon
motion combination, break behavior, exact obscured enemy contact and hidden or
between-frame clearance are not claimed.

Public framework/launcher compatibility, Rodin distribution entitlement,
co-op agreement and other platforms remain release boundaries. The native loot
receipt demonstrates ordinary acquisition, not campaign drop/shop frequencies.
Statistical campaign balance, live interoperability with other Armor providers,
and exhaustive interrupt/death/revive cases are unverified. Mid-combat
save/resume and reconnect restoration are explicitly unsupported by this
feature's current contract. These limits must remain visible in release claims;
they do not turn the scoped observations below into broader acceptance.

## Native duplicate ownership and final card completion

The multiplicity helper passed in the isolated game with two legitimately owned
Coalmark shields, including the native loot reward. Equipping the shield kit,
repeating that equip, switching to the maul and switching back all preserved the
aggregate inventory. Exactly one shield was equipped and the other remained in
Backpack. The final Coalmark shield card reported ownedCount 2 and rendered with
readable text and complete model framing. Together with the prior 31 final
cards, all 32 current item cards now have native rendered evidence.

The helper SHA-256 for these duplicate and card checks is
`bfbb0459a271dc36183ed869c55b4d7129f1e42646386909e040597e3543c544`.
Receipts are under the private release-pass `native-duplicates` and `final-cards`
folders. This verifies test setup and card rendering, not combat balance.

## Additional final native coverage

Seven further Coalmark hammer-and-shield appearance captures passed review,
bringing static presentation coverage to 35. All 32 final item cards passed
visual review. Full moving-pose coverage remains separate.

A level-2 Bellowsworn maul Blacksmith entered a native level-0 Timberwolf fight
through explicit test-party placement. Companions skipped their turns to leave
the strike available for observation. Overhand used the native heavy-attack
icon in normal and selected states. Its native Focus callback spent exactly
one point (4 to 3; reserved slots 0 to 1). The attack then logged the -3 Armor
penalty and defeated the wolf. Native and studio motion frames were captured.
This is UI, Focus and motion evidence, not a matched-level balance trial.

Highlighting Overhand exposed overlapping combat-description text. The next
framework build shortened action-panel copy without changing UI geometry or full
item descriptions. All three selected-state layouts subsequently passed the
native checks recorded below.

## Compact combat copy and Coalmark trial

The compact-text framework build
`7367b7964600614ddab7bbe9c729f89fd99757ebda9c35f7499a0afe9f076454`
was deployed to the isolated copy. Set Hammer's selected description now fits on
one line above the damage and accuracy numbers. Subsequent level-10 selected-state
review also confirmed that Overhand and Temper fit without overlapping the native
panel on this exact build.

A fresh level-0 party used Coalmark hammer-and-shield gear against staged native
level-0 Timberwolf and Hag rows. Ordinary Hunter and Scholar attacks preceded
Set Hammer; all enemies died in those three party actions. The party retained
39/39, 37/37 and 36/36 HP and all Focus. This is one controlled early-tier kit
trial on Easy difficulty. It does not establish representative balance by itself.
Native action receipts and motion frames were preserved, together with a stable
isolated save snapshot for a later comparator.

## Combat studio framing correction

The previous observer camera followed bone height but kept its horizontal
center at the avatar root. Existing native bone-transform receipts show that
Overhand moves the animated body several world units forward during its lunge,
which left the studio view. The corrected helper frames live bone extents in
three camera axes while retaining initial facing and a nonshrinking span. It
changes only temporary observer-camera framing, not native poses, geometry,
game cameras or the capture timing mode. Five studio boundary checks and the
helper Release build passed with no warnings or errors. Subsequent Kilnward
recaptures passed the scoped motion review described below.

The local prepare-only candidate now pins compact-text framework
`7367b7964600614ddab7bbe9c729f89fd99757ebda9c35f7499a0afe9f076454`
and lunge-framing helper
`2218cd2b8cb894677b2f2e73ffae633e70289b8a9e0b16bc5548aaf057da96c9`.
Its package archive and approved artwork are unchanged. These binary pins are
local staging requirements, not a public compatibility or release claim.

## Kilnward native motion review

The current female human Kilnward hammer-and-shield Set Hammer and two-handed
Overhand captures passed sampled guard, windup, lunge, stroke and recovery review.
Grip, hammer head orientation, bracers, circlet and boots remained coherent; the
shield tracked the left arm without visible detachment. The review covers 240
frames and 480 source image hashes, with all 240 studio hashes matching receipts.
See the [combined motion report](../../art-experiments/blacksmith-forge-rodin/approved-redesign/native-motion-coverage-final/README.md).

This is one avatar and one camera angle at 12 fps. Native effects obscure detailed
enemy contact, and studio views exclude the opponent. It does not prove hidden
clearance, all-tier motion, every skinset, break behavior or multiplayer.

## Kilnward level-10 maul trial

From a saved pre-encounter checkpoint, a level-10 party fought staged native
Death Knight Champion and Death Wraith enemies with 90 and 86 starting HP.
Companions used fixed native equipment. Nine ordinary party actions included one
Overhand and one self-Temper; no Focus was spent or attack rolls forced. All
characters survived, with HP changing from 137/117/111 to 137/117/95.

Overhand dealt 30 damage and imposed the expected six-point Armor penalty, which
cleared at the next scheduled Blacksmith turn. Self-Temper retained both target
turns after its casting turn; combat ended before its expiry could be observed.
Scholar Focus rose from five at the checkpoint to six in combat; a specific
Refocus proc was not logged. This single Easy-difficulty controlled trial is
mechanical and playability evidence, not a general balance verdict. The private
trial ledger pins source receipts and all nine actions.

## Kilnward level-10 shield trial

The same pre-encounter checkpoint and companion equipment were used for the
hammer-and-shield trial against the same two native enemy rows. Eight party
actions included Set Hammer and Temper on the Scholar. All survived; party HP
changed from 137/117/111 to 112/111/89, with no Focus spent. Native Steadfast
blocked an attack before Set Hammer. Set Hammer dealt 16 damage and raised
Blacksmith Armor from 39 to 44, returning to 39 at the next scheduled own turn.

Temper granted the Scholar five Armor. The final observer showed zero bonus
with two stored turns: the native victory sequence removes proficiencies before
the combat-finished flags settle, and the framework clears the positive bonus
on that callback. This observation does not demonstrate a duration defect or
prove ordinary two-turn expiry.

Random rolls, enemy targeting, different action counts and different Temper
targets prevent treating the HP difference from the maul trial as a causal
balance comparison. Both runs demonstrate working endgame kits in these
controlled Easy-difficulty encounters. Broader balance coverage remains pending.

## Native endgame comparator and candidate refresh

The restored level-10 checkpoint was also played with native Flanged Mace, Royal
Shield, tier-five heavy apparel, Toughness amulet and defense trinket. Companions
retained the same native equipment as the Kilnward trials. Ordinary attacks,
without Focus spending or forced rolls, defeated the same staged enemy pair.
All party members survived. The Blacksmith began at 133/133 HP after equipment
changed its maximum, and ended at 100/100; the Hunter and Scholar ended at
84/117 and 87/111. The Wraith used its native curse splash, and the Blacksmith's
maximum HP changed during combat. The current-HP difference must not be reported
as pure attack damage without accounting for that effect.

The draft listing and marketing coverage now include 35 static presentations,
all 32 item cards and the scoped Kilnward motion review. Rebuilding the local
prepare-only candidate passed the 32-item/162-asset validator and archive checks.
The runtime archive hash remained unchanged, and staging check-only validated
all 164 archive files without mutating the isolated installation.

## Rivetwatch level-6 stress trial

A fresh Apprentice party was advanced through native level calculations to hero
level 6, equipped with Rivetwatch hammer-and-shield gear and documented native
companion loadouts, and saved before entry for further trials. The substituted
Death Knight Champion and Death Wraith rows retained native level 10. The combat
UI verified this explicitly; these rows do not scale down to the party level.
This is therefore a stress test, not a matched level-6 balance comparison.

The party won, ending at 58/99, 63/85 and 25/81 HP. The first Set Hammer was
fully blocked and correctly granted no Armor. Temper granted the Scholar four
Armor, with two remaining turns decreasing to one after its next completed turn.
The saved checkpoint permits subsequent testing against verified native level-6
Bandit and Hag rows. Native enemy level, hero level, equipment band and the
fixture's dungeon-floor parameter are distinct quantities.

## Rivetwatch matched-level maul trial

The saved hero-level-6 checkpoint was restored and the Blacksmith equipped the
Rivetwatch maul. The Hunter and Scholar retained their recorded native loadouts.
Native `banditD` and `hagC` rows were verified as level 6, with 45 HP each on
Apprentice. Ordinary companion attacks defeated the Raider before Blacksmith's
first action. Overhand dealt 41 damage to the Hag and applied the expected
four-point Armor penalty, reducing the stored pre-disease subtotal from 26 to
22. The Hunter then finished the encounter.

All survived at 99/99, 85/85 and 73/81 HP, with no Focus spent. This short
four-action trial ended before another Blacksmith turn, so it does not establish
normal penalty expiry for this tier. The native following-camera Overhand capture
passed [scoped visual review](../../art-experiments/blacksmith-forge-rodin/approved-redesign/native-rivetwatch-overhand-review/README.md):
all 120 sampled studio frames retained coherent grip, head orientation and apparel
through the visible lunge and downstroke. Exact enemy contact and hidden or
between-frame clearance remain outside this single-angle review.

## Rivetwatch matched-level shield trial

The same level-6 checkpoint was restored with its original Rivetwatch
hammer-and-shield equipment and unchanged companion loadouts. Against the same
level-6 Raider/Hag pair, Set Hammer dealt 23 HP damage to the Hag and granted
four Armor. A subsequent Hunter basic attack ended the encounter. All survived
at 82/99, 85/85 and 60/81 HP, with no Focus spent. Both matched trials took four
party actions, but enemy outcomes and targeting differed; their HP totals do not
establish that one kit is intrinsically safer. The shield trial ended before
the Blacksmith's next scheduled turn, so its positive bonus expiry is supported
by other current native trials and the rule tests rather than this short fight.

## Coalmark maul level-0 trial

A fresh Apprentice party equipped the Coalmark maul kit while companions retained
native starting equipment. Against native level-0 Timberwolf and Hag enemies,
ordinary companion attacks preceded Overhand, which killed the wounded wolf and
applied the expected two-point Armor penalty. Two further companion attacks
finished the Hag. All survived at 35/39, 31/37 and 36/36 HP, with no Focus spent.
Five party actions were used. This encounter ended before another Blacksmith
turn; normal penalty expiry is covered by other current native trials. The full
Coalmark Overhand capture passed [scoped motion review](../../art-experiments/blacksmith-forge-rodin/approved-redesign/native-coalmark-overhand-review/README.md):
coherent grip, head orientation and apparel through all sampled stroke phases.
Exact contact and hidden clearance remain outside the single-angle review.

## Bellowsworn maul level-2 trial

The clean early-tier checkpoint was restored and advanced through native level
calculations to hero level 2. The Blacksmith equipped Bellowsworn maul gear;
companions received the documented native tier-two apparel and ordinary weapons.
Against native level-2 Bandit and Bone Charmer enemies, two ordinary companion
attacks killed the Bandit, and Overhand killed the full-health Bone Charmer.
All survived at 57/59, 53/53 and 41/51 HP, with no Focus spent. Three party
actions were used. The fight ended on Overhand, so this run does not establish
normal penalty expiry. The full following-camera capture passed
[scoped motion review](../../art-experiments/blacksmith-forge-rodin/approved-redesign/native-bellowsworn-overhand-final-review/README.md),
closing the earlier Bellowsworn lunge visibility gap. All 120 studio frames kept
the actor and maul framed, with coherent grip, head orientation and apparel.
Exact enemy contact and hidden or between-frame clearance remain outside this
single-angle review.

Companion equipment differs from the earlier Bellowsworn shield trial, which
used starting equipment. Their action counts and HP outcomes must not be treated
as a controlled shield-versus-maul comparison. Both remain native kit playtests
against level-appropriate enemies.

## Mixed equipment and non-Blacksmith eligibility

A native Blacksmith wore the Rivetwatch one-handed hammer and Fire Kit with
Royal Shield and native heavy apparel. Set Hammer dealt 23 HP damage and
granted four Armor. This demonstrates that the action accepts a native shield
and does not require a matching outfit.

The Hunter equipped the same themed weapon and Fire Kit with a native Royal
Shield. Its combat menu had no Blacksmith equipment actions, and its ordinary
attack dealt 15 HP damage to the Raider. Before/after character views showed
the Blacksmith's Vitality change from 85 to 86 and Resistance from 8 to 11;
the Hunter's Vitality remained 71 and Resistance changed from 17 to 19. The
Hunter received the Fire Kit's ordinary two Resistance, without the additional
Blacksmith affinity point or weapon Vitality affinity. These observations cover
class eligibility and mixed equipment, not a comparison of class power.

The non-Blacksmith grant fixture initially reported a preservation failure
because equal inventory objects had different JSON property ordering. Separate
ownership inspection confirmed that only the requested two item IDs were added
and prior counts and equipped slots remained intact. The helper comparison was
corrected to ignore object property ordering while retaining exact keys, token
types, values and array order. A fresh native reproduction passed with exactly
two requested additions and preservation verified.
This test-helper issue does not establish a production equipment mutation.

The mixed-wearer party finished at 95/95, 85/85 and 79/81 HP, with no Focus
spent. The final helper build
`5c0dcbeda4592f052b8713ddbbdb89ab5da54d68be12cec4dcc1e4c78b7a551b`
passed the fresh grant reproduction. This helper-only correction does not change
framework gameplay or model bytes. The isolated game was stopped afterward.
