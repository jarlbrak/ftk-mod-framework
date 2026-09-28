# Blacksmith Forge Gear 0.2.0 delivery

The approved four-tier, 32-item redesign is prepared as an unpublished local
candidate. No production catalog entry or public compatibility claim is included.

## Helmet correction

The user identified floating headgear missed by the original review. Its helmet
fit approval is withdrawn. The replacement corrects forehead seating, proportions
and rear enclosure. Same-appearance vanilla comparison establishes that minor
Demon ear intersections also occur with the native Kettle Helm; they are recorded
as a limitation rather than addressed with distorted helmets or extra side caps.
See the [correction record](HELM-FIT-CORRECTION.md) and
[independent review](../../art-experiments/blacksmith-forge-rodin/approved-redesign/native-helmet-fit-review-v8/README.md).

## Delivered content

- Coalmark, Bellowsworn, Rivetwatch and Kilnward each contain a one-handed hammer,
  two-handed maul, shield, armor, helmet, boots, necklace and Fire Kit.
- The runtime package contains 106 GLBs and 56 textures/icons, with recorded Rodin
  provenance. Native white outlined combat icons are preserved.
- Blacksmith affinity and Set Hammer, Overhand and Temper implement the approved
  costs and timing. Other classes can wear the gear without receiving these
  class-specific benefits.
- The draft listing and aligned Kilnward banner are in
  [marketplace content](../../marketplace/packages/classgear/MARKETING.md).
  The banner is promotional illustration, not a game screenshot.

## Acceptance evidence

| Requirement | Verified coverage |
| --- | --- |
| Fitted equipment and cards | Corrected helmet fit has a separate scoped review; unchanged gear retains its historical presentation/card evidence |
| Combat motion | Sampled female human maul motion at every tier; Kilnward one-handed hammer and shield motion |
| Progression kits | Both weapon paths used in native combat at every tier, with ordinary rolls and no forced wins |
| Endgame comparison | Kilnward shield and maul plus native gear comparator from a saved checkpoint |
| Rule behavior | Native positive hits, blocked/dodged hits, Armor bonuses/penalties, scheduled expiry, targeting, cancellation and Focus; further rule cases tested without the game |
| Flexible equipment | Native shield plus themed hammer works; Hunter ordinary attacks work without class actions or affinity |
| Acquisition | Native loot awarded and collected a Coalmark shield |
| Tuning | Damage, defenses, costs and prices retained after native comparisons and controlled trials |

See [validation](REDESIGN-VALIDATION.md), [gear and tuning](GEAR.md),
[combat rules](COMBAT.md), and the approved
[design](REDESIGN-PROPOSAL.md). The validation document preserves individual
trial outcomes and their limitations. Randomized, short encounters do not prove
statistical campaign balance.

## Final checks

- Framework Release build passed, with seven existing warnings and no errors.
- Runtime helper Release build passed with no warnings or errors.
- 59 combat, 27 affinity, 71 apparel and 323 resource assertions passed.
- PlayerMods and launcher helper suites passed.
- 54 grant allowlist, 17 shipped-Newtonsoft preservation and 17 ownership checks
  passed; five studio boundary and seven candidate staging tests passed.
- The corrected grant fixture passed a fresh native reproduction.
- The helmet correction passed the strict 80-GLB validator, six studio boundary
  checks, and final native inspection of four helmet cards and four inventory views.
- The package validator passed 32 items and 162 referenced runtime assets.
  The archive contains 164 files. Staging check-only passed without mutation.
- Relative documentation links and `git diff --check` passed.
- The owned isolated game was stopped after testing. Other installations and
  game processes were not modified.

## Candidate identities

| Artifact | SHA-256 |
| --- | --- |
| Runtime ZIP | `e4740690d57f3f0111ed55c05329da57247c39b7bc78150f243e6db11a8b4958` |
| Framework | `7367b7964600614ddab7bbe9c729f89fd99757ebda9c35f7499a0afe9f076454` |
| Final test helper | `5c045ca16ce36b7bfc40f81963229b187edc365040e4bed7f4147059baaa0d00` |
| Banner | `bd129c59d2a0c2df7f09432ac852e690396200c501eabf397768cfe505b26a2b` |

The helmet correction changes only four items' visual fields. Stats, actions and
all other content entries are unchanged. The historical combat motion records
predate these helmet meshes; the new fit review establishes static presentation.

The prepare-only candidate receipt pins all binary and runtime file identities
and has `releaseEligible: false`. See the
[candidate workflow](../../marketplace/packages/BLACKSMITH-CANDIDATE.md).

## Public release boundaries

Compatible public framework/launcher versions and applicable Rodin distribution
entitlement must be established before publishing. Co-op, Windows/Linux,
mid-combat resume, all skin/weapon motion combinations, break behavior, obscured
contact and hidden clearance are not verified. Ordinary acquisition was observed;
natural campaign shop/drop frequencies were not measured. These are explicit
limits of this unpublished delivery, not claims of completed public release QA.
