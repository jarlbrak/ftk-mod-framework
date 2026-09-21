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
- Original armor draws aesthetic inspiration from classic WoW Paladin tier sets: slimmer early Lightforge-inspired direction and endgame Judgment-inspired direction, adapted to FTK. Endgame branches share a silhouette with distinct colors/details and defensive/offensive motifs. No copied Blizzard or Community DLC assets.
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
- Existing passives do not expose the agreed triggers; exact native action and damage flow must be investigated before implementing hooks.
- Marketplace currently rejects code and unsupported assets. Review the smallest safe declarative/asset support extension before selecting packaging architecture.
- Player body, apparel, rigid weapon and shield routes have distinct validation requirements; evidence does not transfer across sex, renderer, weapon or outfit.
- Online co-op is designed for determinism but is not locally verified by single-player tests. Collect structured community reports for production.
- Test pure state/math first, then framework and package suites, then live isolated gameplay and visual acceptance. Build success never substitutes for live proof.

## Inspiration
- https://github.com/Dehydrated-Mud/FTK-Community-DLC
- https://thunderstore.io/c/for-the-king/p/Theta_Hat_Society/CommunityDLC/
