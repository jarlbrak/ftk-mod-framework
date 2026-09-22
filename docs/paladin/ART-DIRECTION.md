# Paladin art direction

Status: current equipment direction plus authored accessory objects,
2026-09-22. Existing geometry has bounded acceptance records in
[Art acceptance](ART-ACCEPTANCE.md); the twelve new accessories have original models
and icons in [their source campaign](../../art-experiments/paladin-accessories/README.md).
Native display fit and gameplay remain untested. [Equipment](EQUIPMENT.md) owns names, slots and stat values.

## Visual promise

Keep native FTK bodies, faces, hair, proportions and race choices. The Paladin
is recognized through original equipment, not a replacement human body or an
oversized torso. Classic WoW Paladin armor is a visual reference for readable
plate, cloth, heraldry and ceremonial craftsmanship. Do not copy or distribute
Blizzard meshes, textures or bundles.

Progression must read through construction and silhouette. A novice carries
plain service equipment; a veteran has layered protection; endgame branches
have different forms and purposes rather than recolored versions of one suit.
Preserve a fitted waist, connected boots and clear faces. Hand-held equipment
must retain usable grips and recognizable striking faces.

## Families

| Family | Existing direction to preserve | Accessory craftsmanship |
| --- | --- | --- |
| Novice | Fitted oxblood jack, short cream sleeves, plain iron cap, brown travel boots | Tin, simple cord, plain stamped shapes; no floating gems or grand reliquaries |
| Oathkeeper | Short blue field coat, split steel chest plates, small shoulders and partial greaves | Restrained brass seals, compact links and simple fastenings |
| Highward | Complete framed breastplate, layered shoulders, long blue tabard and plated insteps | Silver architectural shapes and stronger framing, visibly above Oathkeeper |
| Mercy | Ivory mantle, rounded prayer panels, subdued bronze bindings and ivory gaiters | Rounded lantern and locket forms, blue glass and gentle ivory surfaces |
| Censure | Dark chest chevrons, compact shoulders, pointed red panels and folded greaves | Angular seals, dark steel and restrained red enamel or wax |
| Verdict | Bright heraldic breastplate, layered hip plates, navy tabard and open coronet | Symmetrical scales and collars, navy, gold and dark stone |

The branches are horizontal endgame choices. Mixed pieces should look like
equipment from the same order without requiring every color or motif to match.
Existing armor revisions are not reopened merely to add accessories.

## Twelve accessory objects

| Item | Original object brief |
| --- | --- |
| Tin Oath Token | Small stamped tin disc with a simple oath notch and short cord loop |
| Keeper's Seal | Brass signet seal with a short handle and inset order mark |
| Watchtower Reliquary | Compact silver tower-shaped case with framed blue inset |
| Lantern of Mercy | Small ivory lantern with a protected blue-glass center |
| Seal of Censure | Angular dark-metal case framing a red seal |
| Scales of Verdict | Compact gold balance with broad readable pans and black-stone base |
| Pilgrim's Pendant | Plain iron pendant on a short, readable cord |
| Oath Chain | Modest brass oath badge with a few broad chain links |
| Highward Gorget | Small silver neck-guard object with blue lining and a strong central plate |
| Mercy Locket | Rounded ivory-and-bronze locket with a restrained blue clasp |
| Censure Medallion | Faceted dark-steel medallion with a red central mark |
| Judge's Collar | Symmetrical navy-and-gold ceremonial collar object with a simple clasp |

These are silhouettes for inventory and loot presentation. The native accessory
route has no wearable prefabs and no accessory branch in avatar rebuilding.
Do not add neck rigging, body attachments or cloth simulation to make an
invisible equipment slot visibly worn. A gorget or collar item still needs a
complete original display object and icon.

## Artifacts

[Artifacts](ARTIFACTS.md) is authoritative for the three existing designs:
Last Vigil's compact ivory reliquary and sapphire, Kingsfall's dark crown-framed
hammer and long crimson grip, and Last Bastion's thin battlement kite shield.
Keep their visual prominence above ordinary accessories. New trinkets do not
need legendary-sized gems, permanent particle effects or new sound work.

## Production and acceptance

Use the Astra High art specialist for asset creation and creative revisions,
as requested. Its deliverable is reproducible original geometry and icons,
not only a concept preview. Retain editable sources, piece maps, provenance,
palette inputs and export instructions. Modular original parts can be reused
without copying native surfaces.

Each of the twelve additions needs one original icon and a complete original
object mesh for its supported native display route. Verify actual renderer
paths and whether a template has multiple visible parts before declaring the
asset count final. The audited candidate paths are `trinketHorn2` under the
Defense Trinket prefab and `amuletLocket1` under the Vitality Amulet prefab.
Names alone do not prove fit or successful replacement.

Inspect icons at actual UI size and objects in the native item card, merchant,
loot, collection and inventory flows where present. No tiny text is needed in
icons. Check facing, framing, scale, normals, textures, missing parts and native
fallbacks. The display object must remain recognizable without glow.

Equipping an accessory should change its documented stats and leave the native
character appearance intact. Verify save/load, item replacement, full inventory
and repeated UI creation separately from an offline render. Existing hammer,
shield or apparel acceptance does not prove the accessory display route.
