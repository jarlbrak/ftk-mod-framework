# Paladin: complete class design

Status: design contract for the published Paladin 1.0.1 package and the
unpublished 1.1.0 Cleansing March candidate. It defines 51
equipment items, including twelve accessories. The design and historical plans
below do not establish full live-game acceptance; see the
[launch record](LAUNCH-1.0.0.md) for observed coverage.

## Read the design

| Document | Purpose |
| --- | --- |
| [Combat](COMBAT.md) | Actions, costs, timing, healing, protection and counterplay |
| [Equipment](EQUIPMENT.md) | Every ordinary item, accessories and combined loadout totals |
| [Artifacts](ARTIFACTS.md) | The Last Vigil, Kingsfall and The Last Bastion |
| [Art direction](ART-DIRECTION.md) | Progression silhouettes, accessory art and native character boundaries |
| [Native baseline](NATIVE-BASELINE.md) | Verified slots, templates, inherited actions and stat limits |
| [Gap plan](GAPS.md) | Historical accessory implementation plan and remaining validation gates |
| [Validation](VALIDATION.md) | Historical acceptance evidence plus current verification entry points |

## The promise

**A deliberate protector who gives up an attack to keep an ally standing, then
turns focused strikes into recovery.** High Vitality makes hammer attacks
reliable; modest Speed makes anticipating danger important. The shield route
invests in the ally. The great-hammer route gives up shield support for damage.
Mercy, Censure and Verdict are interchangeable equipment choices, not permanent
subclasses or hidden matching-set bonuses.

In a typical fight, Guard names the ally most exposed to the next enemy attack.
On the Paladin's next turn protection expires, but the designation remains. The
player can renew Guard, switch the protected ally, or spend Focus on an attack
that heals the previously designated ally. Enemies attacking someone else,
incapacitating the Paladin, or using damage over time keep that decision costly.
Guard does not force enemy targeting, and the Paladin cannot protect itself.

## Class sheet

These are authored starting values in the package, before native difficulty,
equipment, sanctum and other derived modifiers. They are not a promise that
every in-game stat panel displays exactly these numbers.

| Stat | Base value | Intent |
| --- | ---: | --- |
| Strength | 70 | Secondary physical checks and fallback weapons |
| Intelligence | 40 | Clear exploration weakness |
| Awareness | 60 | Ordinary scouting capability |
| Talent | 50 | Limited utility specialization |
| Speed | 60 | Protection requires anticipation rather than guaranteed initiative |
| Vitality | 84 | Primary hammer stat and durable class identity |
| Focus | 3 | Native resource shared between reliable attacks and exploration |
| Gold | 3 | Modest starting purse |

The six main stats total **364**. The class clones Blacksmith, declares Vitality
as its primary stat and explicitly disables inherited Steadfast. Its starting
equipment is Novice Hammer, Novice Aegis, Novice Plate, Novice Sabatons and Novice
Helm. The new accessories are found or bought, not additional starting
grants. [Native baseline](NATIVE-BASELINE.md) explains inherited weapon actions,
difficulty bonuses and the stat cap; equip/remove verification remains in
[the gap plan](GAPS.md).

The 1.1.0 candidate adds **Cleansing March**, an equipment-independent passive.
During exploration it prevents new Poison and Curse from tiles and other
noncombat sources. It neither cures existing conditions nor prevents fire tile
damage or chaos losses. In combat the native status rules remain in force.
The implementation and release gates are specified in [issue #182](https://github.com/jarlbrak/ftk-mod-framework/issues/182).

## Vision
Deliver one marketplace mod that enables a fully playable Paladin, its complete equipment progression, and entirely original custom 3D art. The class is a Vitality-based protector that spends actions safeguarding a chosen teammate and focused attacks restoring that teammate.

## Motivation
Build on the support-tank idea explored by DehydratedMud's Community DLC while redesigning its mechanics around explicit defensive decisions, predictable outcomes, and a complete campaign equipment progression. Credit the design inspiration without importing upstream code, models, textures, or bundles.

## Agreed design contract
- Paladin is immediately available when the single mod is enabled. Native Female, Male, Undead, Cat, Demon, Fish and Goblin appearances remain available through normal unlock checks.
- Guard is a guaranteed, equipment-independent action targeting another party member only. It consumes the action and reduces direct attack damage by 50% until the guardian's next turn. Area attacks qualify; damage over time does not.
- Only one ally can be guarded by each Paladin. Switching moves both designation and protection. Protection ends immediately on guardian incapacitation, including stun or death.
- The ally designation persists after protection expires, until another ally is selected. A focused attack that lands heals that ally for 8% of the recipient's maximum HP, once per attack regardless of Focus spent. Healing is combat-only.
- Divine Intervention automatically leaves the guarded ally at 1 HP when a qualifying direct attack would kill them. It requires active Guard and is available once per Paladin per combat.
- Multiple Paladins' reductions do not stack. A lethal hit consumes only one eligible Paladin's rescue, selected deterministically.
- Full Vitality-based progression for two-handed hammers and one-handed hammers plus dedicated shields. All equipment is unrestricted by class.
- Shields have real tradeoffs that make them unattractive as conventional shields, with value concentrated in Paladin mechanics. They never increase Guard above 50%.
- One-handed hammer/shield emphasizes defense; two-handed hammers emphasize offense and focused healing. Both have several equally viable endgame specialties for horizontal progression.
- Gear enters normal level-appropriate shops and loot. No special unlock or quest requirement.
- Complete accessory coverage means the native Trinket and Neck equipment slots.
  The native Belt container holds consumables, not wearable belt armor. The
  [equipment inventory](EQUIPMENT.md) defines six trinkets and six necklaces;
  [accessory validation](ACCESSORY-VALIDATION.md) separates their offline
  implementation from pending native tests.
- Original armor draws aesthetic inspiration from classic WoW Paladin tier sets: slimmer early Lightforge-inspired direction and endgame Judgment-inspired direction, adapted to FTK. Endgame branches share an order identity with distinct silhouettes, colors, details and defensive/offensive motifs. No copied Blizzard or Community DLC assets.
- Every added equipment item receives new custom 3D assets, editable sources, reproducible exports, and provenance. Bodies, faces and hair use native FTK models; no custom body replacement or two-appearance restriction is registered. Gear icons and textures are original.
- Armor progression must read through geometry: Oathkeeper is a lighter field harness with restrained shoulders; Highward has a visibly more protective layered silhouette. Both retain a fitted waist. Greaves, ankle guards and articulated foot plates form a coherent boot, without detached round toe ornaments. Judge the result on native characters from the front and an angled view.
- Global taunt, enemy behavior, and unrelated classes remain unchanged.

## Scope and constraints
Framework mechanics belong in Core and reusable public Content APIs; Paladin content belongs in one separately enabled marketplace package. Preserve net35 compatibility, deterministic identities, save/mod-set behavior, vanilla assets, and host authority. Inspect installed game assemblies before selecting hooks. Any marketplace capability extension must preserve package validation and fail closed.

No borrowed assets, placeholder geometry represented as final art, blanket model compatibility claims, or unverified production co-op claims. Stats besides the agreed Vitality focus, exact item counts/numbers, rounding, tie-breaking, durations of gear effects, and detailed endgame perks are implementation design decisions requiring explicit documentation and proportional verification.

## Success criteria
- [ ] A single validated marketplace package installs, enables, disables and exposes the Paladin and all associated content together.
- [ ] Guard is available with every supported equipment loadout and has correct targeting, action cost, expiry, incapacity handling and visible feedback.
- [ ] Focus healing and Divine Intervention meet the contract, including multi-hit/AoE deduplication, multiple guardians, full/dead/ineligible targets, and combat reset.
- [ ] Complete 1H/shield and 2H campaign progression plus distinct horizontal endgame options are registered in normal shop/loot paths with documented balance rationale.
- [ ] Every added equipment item has original runtime art and editable/reproducible sources, fitted to native character appearances. Native bodies are intentional; native equipment fallback cannot pass acceptance.
- [ ] Preview, overworld, combat, actual weapon animations, equipment rebuilds and resource ownership are verified on an isolated local installation with exact package/binary hashes.
- [ ] Local gameplay and package checks pass, including saves and a normal acquisition path; remaining platform and campaign coverage is stated precisely.
- [ ] Beta is clearly labeled. Online co-op is unverified until community evidence covers host/client parity, multiple Paladins, guard switching, non-stacking and single-rescue consumption. Production waits for that evidence.

## Proposed child specs and dependency order
1. Guard action, deterministic defensive state, focused healing and rescue primitives, with game-source anchors and tests.
2. Paladin class, complete item progression and horizontal endgame balance, authored through public APIs.
3. Original armor, hammer and shield asset pipeline, fitted to native FTK characters and all normal race choices.
4. Single-package marketplace delivery, validation, local end-to-end beta evidence and community production checklist.

Mechanics and model-route discovery can proceed independently. Final content depends on their reviewed public contracts. Packaging and beta acceptance depend on all three.

## Risks and evidence expectations
- Core Guardian hooks and typed marketplace equipment capabilities now exist.
  Review new effects against their exact native action and damage flow rather
  than treating earlier integration evidence as coverage for new behavior.
- Marketplace packages remain data-only. Accessory planning should reuse the
  existing bounded stat and display capabilities where the native route fits.
- Player body, apparel, rigid weapon and shield routes have distinct validation requirements; evidence does not transfer across sex, renderer, weapon or outfit.
- Online co-op is designed for determinism but is not locally verified by single-player tests. Collect structured community reports for production.
- Test pure state/math first, then framework and package suites, then live isolated gameplay and visual acceptance. Build success never substitutes for live proof.

## Inspiration
- https://github.com/Dehydrated-Mud/FTK-Community-DLC
- https://thunderstore.io/c/for-the-king/p/Theta_Hat_Society/CommunityDLC/
