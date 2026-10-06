# Warlock

Status: paper design approved, 2026-10-06. Nothing is implemented or published. The Warlock is a self-sacrificing caster of fire and shadow who withers enemies with curses and diseases and, in the endgame, binds demons through equipment sets.

## Warlock document set

| Document | Purpose | State |
| --- | --- | --- |
| [Design](DESIGN.md) | Class promise, identity, stats, kit summary, power budget | Approved |
| [Native baseline](NATIVE-BASELINE.md) | Installed-assembly facts the design depends on | Approved |
| [Combat](COMBAT.md) | Exact rules for Shadow's Due, fire and shadow spells, afflictions, and demons | Approved |
| [Demons](DEMONS.md) | Summoned demon model, options, and the chosen representation | Approved |
| [Equipment](EQUIPMENT.md) | Ordinary tomes, apparel, accessories, and the endgame demon sets | Approved |
| [Artifacts](ARTIFACTS.md) | Artifact weapons and their signatures | Approved |
| [Art direction](ART-DIRECTION.md) | Visual language, progression readability, model and icon acceptance | Approved |
| [Gaps](GAPS.md) | Missing framework primitives, their contracts, and delivery order | Approved |
| [Validation](VALIDATION.md) | Offline, simulated, and live acceptance scenarios | Approved |

An implementation record and tracking epic follow once the design is approved, as Paladin did with [its implementation record](../paladin/IMPLEMENTATION.md).

## Groundwork from Paladin and Thief

The Warlock document set follows the design-phase documents that preceded the Paladin and Thief packages. Their mechanics and balance are not Warlock defaults. Their structure, evidence standards, and lessons are.

### Design-phase documents

| Purpose | Paladin | Thief | Warlock counterpart |
| --- | --- | --- | --- |
| Class promise, stats, kit, power budget | [Design](../paladin/DESIGN.md) | [Design](../thief/DESIGN.md) | [Design](DESIGN.md) |
| Installed-game facts behind the design | [Native baseline](../paladin/NATIVE-BASELINE.md), [1.2.0 balance review](../paladin/BALANCE-1.2.0.md) | Recorded inside [Validation](../thief/VALIDATION.md) | [Native baseline](NATIVE-BASELINE.md) |
| Exact combat rules | [Combat](../paladin/COMBAT.md) | [Combat](../thief/COMBAT.md) | [Combat](COMBAT.md), [Demons](DEMONS.md) |
| Equipment inventory and progression | [Equipment](../paladin/EQUIPMENT.md) | [Equipment](../thief/EQUIPMENT.md) | [Equipment](EQUIPMENT.md) |
| Artifact weapons | [Artifacts](../paladin/ARTIFACTS.md), [legendary concepts](../paladin/LEGENDARY-CONCEPTS.md) | [Artifacts](../thief/ARTIFACTS.md) | [Artifacts](ARTIFACTS.md) |
| Visual direction | [Art direction](../paladin/ART-DIRECTION.md), [novice redesign](../paladin/NOVICE-REDESIGN.md) | [Art direction](../thief/ART-DIRECTION.md) | [Art direction](ART-DIRECTION.md) |
| Coverage ledger and missing work | [Gaps](../paladin/GAPS.md) | Recorded inside [Validation](../thief/VALIDATION.md) | [Gaps](GAPS.md) |
| Loot and shop acquisition | [Acquisition audit](../paladin/ACQUISITION-AUDIT.md) | Recorded inside [Equipment](../thief/EQUIPMENT.md) | [Equipment](EQUIPMENT.md) acquisition section |
| Delivery plan and epic | [Implementation](../paladin/IMPLEMENTATION.md) | n/a | Implementation, after approval |
| Evidence and acceptance | [Validation](../paladin/VALIDATION.md) | [Validation](../thief/VALIDATION.md) | [Validation](VALIDATION.md) |
| Lessons for the next class | [Lessons 2.0.0](../paladin/LESSONS-2.0.0.md) | n/a | Applied from day one |

### Later-phase records (not groundwork)

These document validation and release of shipped packages. They are the model for the Warlock's release records, not for its design: Paladin [accessory validation](../paladin/ACCESSORY-VALIDATION.md), [legendary validation](../paladin/LEGENDARY-VALIDATION.md), [art acceptance](../paladin/ART-ACCEPTANCE.md), [beta notes](../paladin/BETA-NOTES.md), [launch 1.0.0](../paladin/LAUNCH-1.0.0.md), [marketplace lifecycle](../paladin/MARKETPLACE-LIFECYCLE.md), the [publication](../paladin/PUBLICATION-1.2.0.md) and [release](../paladin/RELEASE-2.0.1-VALIDATION.md) records, and Thief [framework 1.0.4](../thief/FRAMEWORK-1.0.4.md) and [playtest 1.0.0](../thief/PLAYTEST-1.0.0.md).

### Shared framework references

- [Writing content](../WRITING-CONTENT.md): class, item, and proficiency authoring.
- [Combat proficiencies](../COMBAT-PROFICIENCIES.md): action and effect surfaces.
- [Guardian and equipment](../GUARDIAN-AND-EQUIPMENT.md): how Paladin's class mechanic became a reusable primitive.
- [Marketplace](../MARKETPLACE.md) and [publishing mods](../PUBLISHING-MODS.md): package admission and release.
- [Equipment exchange assets](../EQUIPMENT-EXCHANGE-ASSETS.md): shared original art catalog.
