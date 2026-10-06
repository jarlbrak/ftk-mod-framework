# Warlock art direction and asset brief

Status: paper art brief, 2026-10-06. No concept art, mesh, icon, texture, fitting, or animation exists or is claimed. [Design](DESIGN.md) owns the class promise and phases, [Equipment](EQUIPMENT.md) owns item names, slots, bands, and stats, [Artifacts](ARTIFACTS.md) owns artifact identities, and [Demons](DEMONS.md) owns familiar rules. [Validation](VALIDATION.md) holds the acceptance ledger.

## Visual identity

The Warlock is a hedge-caster who keeps a polite ledger with their own shadow. Read it as a light Halloween tabletop figure in FTK's chunky, faceted, hand-painted style: candle smoke, dripping wax, moth-eaten cloth, patched hems, dried herbs, pumpkin-lantern glow, and a book that looks slightly alive. The mood is cozy-spooky, not frightening.

| Keep | Avoid |
| --- | --- |
| Wax drips, candle stubs, singed page edges, moth holes, mended patches, string-tied bundles | Gore, blood, flayed skin, exposed organs, realistic skulls, horror realism |
| Rounded, readable silhouettes with a few large shapes | Spiky edgelord armor, dense filigree, tiny ornament noise |
| Painted glow in albedo (a warm lantern core, a faint page glow) | Permanent particle systems, real-time lights, bloom-dependent reads |
| Impish, sleepy, or dignified demons | Menacing, fanged, or body-horror demons |
| Native faces visible under hoods | Hoods or cowls that hide the face or substitute for fit |

World of Warcraft warlock imagery is a broad archetype reference only. Do not copy Blizzard meshes, textures, set silhouettes, icons, or named characters. Native FTK assets (the pumpkin lore set, Shadow Cloak, Shadow Hood, Shadow Boots, imp, hell hound, and wraith enemies) are viewed as local style references and are never extracted into the package.

## Palette

| Role | Colors | Use |
| --- | --- | --- |
| Shared base | Oatmeal homespun, soot brown, candle-wax cream, faded plum | Every band; keeps mixed outfits coherent |
| Grimoire path | Ember orange, pumpkin amber, charcoal, brass | Fire tomes, Cindercall set, Cinder Imp |
| Hexbook path | Gloom violet, moss green, bone white, tarnished pewter | Shadow tomes, Gloamhunt set, Gloam Hound |
| Neutral pact | Ash gray, deep indigo, pale cold blue, old silver | Hollowward set, Hollow Warden |
| Accents | Wax red seal, twine tan, bottle green glass | Small fastenings, seals, lantern panes |

Path color is a supporting cue. Every path distinction must also read through shape (flame and gourd forms for the Grimoire, crescent, eye, and sprig forms for the Hexbook) so no reading depends on color alone.

## Progression readability

Progression reads through construction first, then trim, then color. The [Paladin novice lesson](../paladin/NOVICE-REDESIGN.md) applies from day one: the user rejected ornate early looks and blocky modeling. Early gear is plain, fitted, and modest. Richness is earned by band, not front-loaded.

| Band | Read | Apparel (phase 2) | Tomes and lanterns | Palette |
| --- | --- | --- | --- | --- |
| 1 | Hedge-witch homespun | Soft unstructured hood, short fitted homespun tunic-robe to mid-thigh with a rope belt, wrapped cloth shoes; one patch, no trim | Plain board-and-cord book, one candle-stub bookmark; a small carved gourd lantern with a twig handle | Oatmeal, soot brown, one path accent |
| 2 | Village cunning-folk | Shaped hood with a stitched edge, robe with a single overlapped front and short shoulder cape, ankle boots with a turned cuff | Leather-cornered book, wax seal, ribbon markers; a tin lantern with a single colored pane | Adds faded plum, brass or pewter fastenings |
| 3 | Coven scholar | Pointed hood with a stiffened brim fold, layered robe with split front panels clearing the legs, laced boots with a defined sole | Metal-clasped book, path motif on the cover (flame or crescent), page edges tinted; a framed lantern with path-colored glass | Path color becomes primary |
| 4 | Pact adept | Tall hood with a sculpted opening, robe with a fitted bodice and layered tabard panels, buckled boots with heel and toe shaping | Bound book with a carved cover face, chained or strapped; an ornate lantern with a glowing core and cage | Deep path colors, polished metal accents |
| Final | Bound pact (demon sets) | Three distinct horizontal branches; see below | Three Grimoire and three Hexbook alternatives, each echoing one demon set | Set palettes |

Rules for every band:

- Fitted waist, visible native face, connected boots with soles. A long robe never substitutes for fit, and hems must clear native walk and combat leg motion; front splits or knee-length panels are preferred over full-length skirts.
- Each band is a visible step in geometry (garment count, layering, hood structure, fastening complexity), not a recolor of the previous band.
- Mixed pieces from different bands and paths still look like the same tradition.
- The final band is a horizontal choice. The three demon sets differ in silhouette and purpose, not just palette.

## Tome silhouettes

Tomes are hand-held weapon models. Book scale follows the actual native hand grip and caster motion; [Native baseline](NATIVE-BASELINE.md) open check 1 (one-handed or two-handed spellbook) must be answered before grip stations are fixed.

| Path | Silhouette family | Cover motifs | Progression | Avoid |
| --- | --- | --- | --- | --- |
| Grimoire | Squat, thick, warm; scorched page edges; a candle or wick element on the spine or top edge | Flame, gourd, ember-eye, brass corners | Band 1 plain cord binding with a candle stub; band 4 carved ember face with a painted glowing mouth | Literal fire geometry, flame particles, skulls |
| Hexbook | Tall, narrow, cool; ragged ribbon markers; a cloth or bark cover | Crescent, closed eye, wilted sprig, pewter clasp | Band 1 cloth-covered and string-tied; band 4 pewter-strapped with a sleepy carved eye | Tentacles, dripping slime, realistic eyes |
| Final alternatives | One Grimoire and one Hexbook per demon set | Cindercall imp-tail clasp; Gloamhunt paw or collar strap; Hollowward folded-hands relief | Echo the set without needing the set to read | Pure recolors of band 4 |

The two paths must be distinguishable at combat-camera distance by outline alone: squat-and-warm against tall-and-narrow. Artifact tome silhouettes follow [Artifacts](ARTIFACTS.md) and must stay visually above band 4 without legendary-sized gems or permanent effects.

## Lantern trinkets

Lanterns are the Warlock's trinket family. The Gourdlight Lantern starts the class and the Witchlight Lantern ships beside it in phase 1 ([Design](DESIGN.md)); the band labels below assume they are bands 1 and 2, and [Equipment](EQUIPMENT.md) is authoritative for names and bands.

| Band | Lantern brief |
| --- | --- |
| 1 Gourdlight | Small carved gourd with a friendly face cut, twig handle, cream candle core |
| 2 Witchlight | Tin lantern, one green or violet pane, wax drips on the cap |
| 3 | Framed lantern with path-colored glass and a small crescent or flame finial |
| 4 | Caged lantern with a painted glowing core and a pewter or brass ring |

The [Paladin accessory finding](../paladin/ART-DIRECTION.md) applies: the native accessory route has no wearable prefab and no branch in avatar rebuilding. Lanterns are therefore icons plus inventory, loot, shop, and item-card display objects. No belt or hand attachment is promised, and no neck or body rigging is added to make a trinket visibly worn. The three binding trinkets of the demon sets follow the same rule; the familiar itself is a separate model governed by the mount spike below.

## Demon sets

| Set | Silhouette | Palette | Hood | Robe | Boots | Binding trinket |
| --- | --- | --- | --- | --- | --- | --- |
| Cindercall Regalia | Warm, compact, upward: short flared shoulders, rising collar, shorter hem | Ember orange, charcoal, brass | Short pointed hood with a singed rim | Layered short robe, scorched hem scallops, brass toggles | Stout boots with brass toe caps | Coal-holding brass censer |
| Gloamhunt Raiment | Lean, low, forward: hood drawn into a muzzle point, long narrow tabard, strapped legs | Gloom violet, moss green, pewter | Long hood with ear-like folds | Narrow coat-robe with a collar strap and paw-print clasp | Soft wrapped boots with pewter buckles | Pewter collar tag on a short leash loop |
| Hollowward Vestments | Tall, still, vertical: high stiff collar, long straight panels, folded-arms motif | Ash gray, deep indigo, pale cold blue | Deep cowl with a stiffened face frame that keeps the face visible | Long vertical-paneled robe with split front, folded-sleeve detail | Tall narrow boots with silver bands | Small silver reliquary with folded-hands relief |

Each set must be identifiable without stats from a three-quarter view at combat-camera distance. Mixed pieces across sets should still read as one pact tradition.

## Familiars

[Demons](DEMONS.md) is authoritative: a familiar has no turn, health, or slot and appears only while its set is complete and its wearer is a Warlock.

| Familiar | Placement | Silhouette | Palette | Motion | Trigger feedback |
| --- | --- | --- | --- | --- | --- |
| Cinder Imp | Perched on one shoulder | Cat-sized, round belly, stubby horns, curled tail, juggling one coal | Ember orange skin, charcoal horns, amber painted coal | Static pose acceptable; idle bob is a stretch goal | Native fire hit effect on the target; combat text |
| Gloam Hound | Seated at heel, offset from the root | Lean greyhound-like, long ears, two lantern-glow eyes, wisp tail | Gloom violet body, moss-green eyes, pewter collar | Static pose acceptable | Combat text |
| Hollow Warden | Hovering behind the shoulders | Tall hooded shade, no face, arms folded, tapering hem with no legs | Deep indigo to ash gray, pale cold blue rim | Static pose acceptable; slow drift is a stretch goal | Combat text |

Constraints:

- Native imp, hell hound, wraith, and lich enemy models are style references only. They are not mounted, cloned, or extracted.
- **The P5 mount feasibility spike in [Gaps](GAPS.md) gates all familiar modeling.** No familiar mesh is authored until the spike proves a static, equipment-bound original model can sit at the shoulder, a root offset, and behind the back of native avatars in overworld and combat without disturbing native animation or mutating vanilla prefabs. If the spike fails, phase 3 ships familiars as effects, text, and icons first.
- Familiars must not obscure the Warlock's face, the tome, or the enemy-facing silhouette in combat framing, and must clear native attack and hit motion on every tested appearance.
- Hollow Warden translucency depends on what the runtime GLB route and resolved native material support. Until proven, author an opaque fallback (dark desaturated body, painted pale rim) that reads correctly without transparency.
- Presentation while the Warlock is incapacitated, and overworld visibility ([Demons](DEMONS.md) open question 2), are undecided. Default proposal: hide the familiar while its Warlock is incapacitated.

## Icons

Every item gets one original PNG icon readable at native UI size, within the framework icon size limit (the Thief package reduced an icon to 1024 pixels to pass it). Use the item silhouette on a simple backdrop. No tiny text, landscapes, portraits, or baked rarity borders; rarity comes from native UI.

| Surface | Direction |
| --- | --- |
| Grimoire items | Warm backdrop, squat book silhouette; band shown by binding complexity |
| Hexbook items | Cool backdrop, tall book silhouette; band shown by clasp and strap complexity |
| Lanterns and binding trinkets | Object silhouette centered; glow painted, not bloomed |
| Apparel | Garment silhouette in a consistent three-quarter view per family |
| Hexfire | Open book with one rising flame tongue |
| Cinderbrand | Small brand mark with a curling ember |
| Cinderstorm | Three falling embers over a book edge |
| Hollow Fright | Hood shape with a startled spiral |
| Curses | One shared crescent frame; inner glyph per curse (cracked shield, cracked drop, snail, limp sword) |
| Siphon Soul | Wisp arcing from target toward an open hand |
| Blight and Rot actions | Wilted sprig; mushroom cluster with spores |
| Enemy Blight and Rot HUD icons (phase 2) | Framework-owned images matching native status icon size and outline weight: violet-green wilted sprig, olive mushroom. Must read beside native Burning and Bleeding |
| Shadow's Due cue | Drop shape inside a ledger tick; used only if a UI surface later needs it |

Each band's icons keep a family resemblance. Path and status icons pair color with a distinct shape, and the UI text supplies exact meaning. Curses reuse native status icons and arrows in the enemy HUD as [Combat](COMBAT.md) specifies; only their action buttons need original art.

## Fitting across appearances

Native bodies, faces, hair, skeletons, race choices, and backpacks remain the foundation. Only equipment and familiars are custom.

- Hoods and robes need supported bindings for the native Female and Male bodies and must be tested on all seven native appearances (Female, Male, Undead, Cat, Demon, Fish, Goblin). Hood fit is keyed by head shape and checked front, both sides, and rear, per the [Paladin lesson](../paladin/LESSONS-2.0.0.md) on the Undead crown defect.
- One preview, one appearance, or one pose never establishes race or sex coverage. Record each tested appearance, view, and animation explicitly. Fit evidence does not transfer from Paladin or Thief bindings.
- Inspect resolved materials on equipped, combat-clone, and display routes. Material scalars and PNG validity do not prove appearance.
- Verify skin tones, faces, hair, ears, hands, and feet remain intact. Check hood and hair interplay on every appearance.

## Animation

Native combat motion is reused. No new animation, rig, or retarget is in scope. Tomes ride the native caster controller selected by the template weapon; the grip origin, book axis, and facing are declared in source and compared against the native spellbook in the same pose before geometry changes. Observe windup, impact, recovery, and return to idle in real simulation time, plus walk, incoming hit, death, and revive. Familiars are static unless the spike proves motion is safe; stretch-goal motion never modifies the avatar's animator.

## Provenance and editable source

Follow the Paladin standard ([Paladin art direction](../paladin/ART-DIRECTION.md), [model authoring](../MODEL-AUTHORING.md), [player model API](../MODEL-PLAYER-API.md)).

- One `art-experiments/` campaign per family with source description, piece map, and validation record kept consistent.
- Editable source: reproducible generator scripts or retained source files, palette inputs, masks, and deterministic export commands. Blender work files stay outside ordinary git history but are hashed in the manifest.
- Original GLB and PNG outputs exported through the framework runtime contract, independently revalidated, with a SHA-256 manifest and a package provenance record like `paladin-assets.provenance.json`.
- If a generation service is used, record its name, terms check, inputs, raw outputs, and every manual edit. Reference images are direction, never geometry.
- No game assets, extracted meshes, copied commercial assets, or palette-only vanilla reskins ship or are committed.
- Freeze model, texture, mask, display transform, and render recipe hashes together, and generate promotional media only after native model acceptance.

## Acceptance checklist

- [ ] Bands 1 to 4 read as successive craftsmanship; band 1 is plain, fitted, and modest.
- [ ] Grimoire and Hexbook are distinguishable by outline at combat-camera distance.
- [ ] The three demon sets are distinguishable without stats; mixed outfits stay coherent.
- [ ] Every item has an original icon readable at UI size; path and status cues pair shape with color.
- [ ] Enemy Blight and Rot icons read beside native status icons and follow application, refresh, expiry, death, and encounter exit.
- [ ] Every tome holds correctly through native caster motion on each tested appearance.
- [ ] Lanterns and binding trinkets display correctly in item card, shop, loot, and inventory, and leave native appearance intact.
- [ ] Apparel fits every recorded appearance, view, and animation; no fallback, clipping, detached parts, or culling.
- [ ] Familiars pass the P5 spike before modeling, then clear native motion and framing on each tested appearance.
- [ ] No gore, horror realism, permanent particles, or copied assets.
- [ ] Every shipped asset has editable source, export commands, provenance, hashes, and package-relative paths.

## Phase mapping

| Phase | Art scope | Counts |
| --- | --- | --- |
| 1. Hallow's Eve playtest | Grimoire bands 1 and 2, Hexbook bands 1 and 2, Gourdlight and Witchlight Lanterns; action icons for phase 1 actions. Native armor; no apparel, set, or familiar art | 6 items: 4 tome models, 2 lantern display objects, 6 item icons |
| 2. Afflictions | Tome and lantern bands 3 and 4; hood, robe, and boots bands 1 to 4; three artifact tomes; Cinderstorm, Blight, and Rot action icons; enemy Blight and Rot HUD icons | 21 items |
| 3. Pacts | P5 spike first; three demon sets (hood, robe, boots, binding trinket each); three Grimoire and three Hexbook final alternatives; three familiar models if the spike passes | 18 items plus up to 3 familiars |

Phase totals equal the 45-item inventory in [Design](DESIGN.md). Each phase's art acceptance is separate; earlier acceptance never transfers to later geometry.
