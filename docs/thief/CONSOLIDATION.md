# Gear consolidation candidate

The Thief advanced-model working changes are consolidated onto the current
framework alongside Blacksmith and the released Paladin art. This is an unpublished
development candidate. Prior native captures in [validation](VALIDATION.md) describe
the source worktree and exact older archives, not acceptance of the combined runtime.
The production catalog, released archives and published website data are unchanged.

## Combat icon correction

All class, weapon and proficiency `icon` overrides are omitted. Native sprites are
inherited from the existing gameplay templates, preserving their mechanics:

| Action or gear | Inherited native source |
| --- | --- |
| Thief Slip Away | Runtime `taunt` proficiency clone |
| Fire, Feint, Bait Shot, Pierce, Deadeye and stronger variants | `musicArmorDown` proficiency clone |
| Paired daggers, including artifacts | `dualKnife` weapon clone |
| Pistols, including Unlost Road | `gunTreasureHunter` weapon clone |
| Paladin Guard | Runtime `taunt` proficiency clone |
| Paladin Censure Armor | `musicArmorDown` proficiency clone |
| Paladin Censure Resistance | `magicResistDown` proficiency clone |
| Paladin Smite | `magicdamage` proficiency clone |
| Paladin hammers | Existing native weapon templates, unchanged |

The template icon choices are source-backed inheritance, not a claim of native
visual approval. Normal, selected and disabled buttons must still be inspected
beside vanilla actions in the combined runtime. No new symbols or native sprite
extraction are introduced. Generic inherited glyphs may not depict each custom
ability literally; gameplay templates are preserved to avoid changing behavior.

Non-weapon inventory artwork, studio portraits, models and textures remain intact.
Historical generated action art and provenance remain available locally but are
excluded from runtime references and candidate archives. Paladin's original
published asset provenance is retained unchanged; the validator explicitly excludes
its four historical combat art files from the new candidate's active references.

## Preserved sources and offline checks

The ignored concept, Rodin, editable Blender and native-review sources were copied
into this worktree without modifying the original worktree. The initial consolidation
[provenance](../../art-experiments/thief-advanced/provenance.json) recorded 30 concepts,
36 source groups containing 130 files, 139 active advanced-model asset references,
and six historical action-icon sources. All 305 recorded file sizes and SHA-256
hashes were checked against the copied bytes. Historical action sources remain
provenance only and are absent from the candidate runtime asset list.

The current Thief source validator passes 55 entries with 184 active assets; the
Paladin validator passes 57 entries with 236 active assets. The earlier
consolidation draft archives contained 179 and 232 files respectively; the Thief
archive predates the later replacement integration. Their inspection found no
class, proficiency or weapon icon override, and no historical custom combat PNG.
Current source integration and bounded native observations are recorded in
[Thief validation](VALIDATION.md) and [Paladin validation](../paladin/VALIDATION.md).
Asset counts and hashes establish identity, not native visual acceptance.

## Remaining gates

The combined development build needs native registration, equipment fit and motion,
icon-state review, pistol turn behavior, and migration from published bow saves.
Earlier Thief evidence covers representative loadouts, not every tier or appearance.
Co-op, lifecycle and redistribution gates in the original validation record remain
open. No package publication or release readiness follows from consolidation.
