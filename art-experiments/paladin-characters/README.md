# Original Paladin armor equipment

This package authors only equipment. Native FTK bodies, faces, hair and character
appearance choices remain native. Obsolete original character/default-outfit and
portrait experiments are archived in ignored scratch and are not runtime assets.

There are 24 runtime models: female and male armor, shared boots and a rigid helmet
for each of Novice, Oathkeeper, Highward, Mercy, Censure and Verdict. The current Novice candidate uses
a fitted oxblood padded jack over cream linen short sleeves, a dark leather yoke,
joined calf-height brown boots and a small iron travel helm. Gold borders and order
seals are reserved for later progression. Later sets retain their own colors
and ornaments with a fitted faceted chest and defined waist. All surfaces,
textures and symbols are original; no native surface geometry is read or copied.

`build_geometry.py` consumes only names and inverse bind matrices. The manifest
lists the exact 24 output triples (source JSON, piece JSON and GLB) plus the
original palette. Unrelated files cannot enter its inventory implicitly.

```sh
scratch/paladin-venv/bin/python art-experiments/paladin-characters/build_geometry.py
scratch/paladin-venv/bin/python art-experiments/paladin-characters/validate.py
scratch/paladin-venv/bin/python art-experiments/paladin-characters/verify_original_geometry.py
scratch/paladin-venv/bin/python art-experiments/paladin-characters/verify_helmet_mount.py
scratch/paladin-venv/bin/python art-experiments/paladin-characters/loot-display/build.py
scratch/paladin-venv/bin/python art-experiments/paladin-characters/loot-display/validate.py
blender --background --factory-startup --python-exit-code 1 \
  --python art-experiments/paladin-equipment/render_icons.py -- --characters-only
```

The icon route renders 18 armor-item icons and no portraits. The separate
`loot-display` route derives 12 rigid armor/boot card models from these original
surfaces. Item filenames stay stable across this revision.

## Helmet mount correction

Helmets remain in native Hair attachment space using
`inverse(nativeMountEulerMatrix) * HairInverseBind * Translate(HeadBindOrigin)`.
The six-helmet offline check uses permitted transform metadata from a retained
observation. This is mathematical placement evidence, not current native visual,
all-race, animation or resource-lifetime acceptance. The previous custom-body
studio lineups are historical previews; new fit review must use native characters
wearing the custom gear in the isolated game.

## Distinct early tiers and connected boots

Oathkeeper now has a low blue open-face helm, single-layer compact steel shoulder
caps and a short narrow blue apron. Highward has taller gold crests, two ridged
shoulder layers, blue rib guards, silver side tassets and a longer flared apron.
Both retain the fitted chest and waist rather than the former rounded breastplate.

Every boot set now has a continuous dark underboot and heel/sole, a fitted shin
shell with an inset colored stripe, an ankle cuff, and a shaped sabaton with shallow articulation seams. The armor's knee and toe underlayers no longer add floating ornaments.
Native joint metadata determines articulation only; all surfaces remain original.
These revised exports require new native fit observations.

The subsequent toe correction gives the foot one continuous heel/instep/toe volume
above a dark sole. Shallow transverse seams imply layered articulation without
intersecting closed plate caps, and narrow side piping replaces the bright gold
toe cap. Lower-leg armor stays inside the boot shell. Native fit remains a separate
check after this revision.

## Plain Novice revision

Novice now uses a fitted dark gambeson with a shallow iron chest sheet, narrow
leather straps, a belt buckle, small shoulder caps and a simple open-face cap.
The torso extends to the neckline instead of relying on a projecting breastplate
to cover a gap. Leather boots use horizontal shoe sections for flat soles and
rounded toes. There are no gold borders, order seals or layered shoulder ornaments.
Oathkeeper retains its previous helmet; later sets retain their geometry and palette.

The [native fitted preview receipt](../../docs/paladin/novice-fitted-preview.json)
records Female/Male three-quarter views. The earlier flat-plate and recolored-boot
attempts were rejected. This replacement is a review candidate: offline geometry,
binding and package checks pass, but user approval, movement and nonhuman fit
remain open. Previous Novice screenshots do not establish the new revision's fit.

Short sleeves now end above the elbow, retaining native forearms and race details.
The helmet sits slightly higher to clear the Demon forehead. The
[seven-appearance angled check](../../docs/paladin/novice-sleeve-fit.json) records
the preceding candidate. Its fixed-size wrist wraps appeared loose on Undead.
The subsequent geometry removes optional wrist and thumb guards, leaving native
hands and forearms exposed. The [new seven-appearance preview](../../docs/paladin/novice-bare-wrist-fit.json)
confirms the floating wrist rings are absent in sampled views. Combat chest
clearance and final art acceptance remain open.

## Tailored Novice candidate

The latest unapproved candidate replaces the separate chest sheet with a fitted
leather jack, center closure detail and a continuous short hem with a front vent.
The iron helmet is now one connected rounded shell with a narrow lower edge.
Native bodies and appearance choices are unchanged. Boots and later sets retain
their geometry. Earlier preview and attack receipts describe earlier Novice assets
and do not validate this candidate. Offline binding, deterministic rebuild and
helmet mount checks pass. Native fit, motion and visual review are pending because
the local preview could not be reached while the workstation was locked.

The [native follow-up](../../docs/paladin/novice-tailored-native-preview.json)
records seven appearance previews after lowering the helmet crown and front
opening. The original preview selection was restored. These static fit samples
do not establish combat movement or user approval.

## Earlier slim starter candidate

The preceding unapproved source replaced the heavy rounded chest and flared apron
with a close leather jerkin, short straight hem, small matching sleeves and
slim travel boots. It also reduces the simple iron cap. The generated source,
display models and icons pass their offline checks. A native Party Select review
was performed in the isolated fixture, but this candidate has not been copied
into the marketplace package and has no user art approval.

## Astra Novice art candidate

This original equipment revision uses oxblood cloth, cream sleeves, leather side
gussets and a fitted shoulder yoke. The higher neckline joins the sleeves to the
jack. Thin closed coat tails continue its front and back below the belt. Torso
panels use outward-facing triangles and a shallow front instead of a rounded
barrel shape. The boots have a continuous shaft, heel and instep, with subdued
ankle straps and flat dark soles. A modest iron travel helm has a shallow brow
rim and narrow reinforcing band. Later sets and the shared palette are unchanged.

The first open-browguard experiment was rejected during native preview because
the equipment branch hides the upper native hair covering. The replacement
closed crown preserves the visible native face and lower hair. The native body,
hands, beard, backpack and character appearance choices remain game-owned.

Offline binary/binding, deterministic original rebuild and six-helmet mount
checks pass. Native art captures are retained locally under ignored scratch.
This remains an unapproved art candidate. Current static previews do not prove
combat movement, equipment rebuilds, nonhuman fit or resource lifetime. The
marketplace package, item icons and loot-display derivatives have not been
updated to this candidate.

## Oathkeeper guardian candidate

Oathkeeper builds on the fitted Novice garment pattern with original compact
steel shoulder caps and split chest plates, a blue padded jack, narrow pointed
surcoat halves, small forearm guards and articulated knee protection. Its closed
cloth panels have blended center weights. The continuous leather boots gain
tapered front greaves and toe plates; leather remains visible at the ankle and
instep. The low open sallet has a complete crown, guarded temples, a reinforced
plain brow bar, a narrow dark crown strap and blue rear lining. It leaves the face open and has no tall crest, horns,
gold borders or layered shoulder wings.

This tier is distinct from Novice's short oxblood jack and bare cream sleeves,
and from Highward's larger layered shoulder plates and taller helm ornaments.
All four Oathkeeper assets are original equipment for the existing female/male
armor, shared boot and rigid helmet routes. Native body, face, beard and hair
remain game-owned, with visibility controlled by the native equipment branch.

Binary and exact-binding validation, deterministic rebuild and helmet mount
checks pass. Offline construction renders are retained in ignored scratch and
show equipment in bind pose with the native body omitted.

Two isolated native Party Select visual-fit passes sampled Female and Male
front, three-quarter and back views. The first pass prompted a narrow dark crown
strap and stronger brow bar, plus lower chest-plate weights blended into BackB
to follow the jacket in idle. The second pass shows the corrected chest edges,
readable helm structure, clear faces and continuous boots in those views. Raw
captures, source receipts and an unretouched review board are retained locally.

These are visual-fit samples through a temporary isolated starter-slot mapping
only. They do not establish Oathkeeper item acquisition or native equipment
transfers, combat motion, nonhuman fit, resource lifetime or user art approval.
Item icons, loot-display derivatives and the marketplace package have not been
updated.

## Highward ceremonial guardian candidate

Highward progresses from Oathkeeper's two plain chest plates and short pointed
surcoat to a single fitted silver cuirass, a blue central panel, a small order
seal and a narrow gold frame. The breastplate weights follow the authored
garment's spine rows, including its lower edge. Two shallow overlapping shoulder
shells provide a stepped silhouette without the previous oversized wings.

The divided royal-blue tabard reaches the knees in front and hangs longer behind
the hips, with narrow gold hems. The closed panels retain separate fronts and
reverses, and their lower weights blend through the hip and knee. Steel-faced
boots preserve the continuous heel and ankle construction while adding gold
greave frames and two articulated instep plates. The reinforced open helm keeps
Oathkeeper's face opening, adds a solid low crown ridge, and uses a gold brow,
temple bindings and a small original order seal. It has no plume or tall horns.

All non-Highward asset and palette hashes remain identical to the pre-pass
snapshot, including the finished Novice and Oathkeeper models. Binary and
exact-binding validation, deterministic rebuild and six-helmet mount checks
pass. Offline construction renders show only original apparel in bind pose;
no native body or animation data informs the geometry. Fresh Female and Male
native Party Select captures must still verify face clearance, collar and
shoulder fit, long-tabard behavior in idle, material response and boot clearance.
Combat motion, nonhuman fit, real equipment transfers, resource lifetime and
user art approval remain separate gates. Icons, loot-display derivatives and
the marketplace package have not been updated to this candidate.

## Mercy compassionate guardian candidate

Mercy is a horizontal max-level protector option. Its identity comes from a
thin, closed ivory prayer mantle with a folded blue collar and narrow muted-gold
binding. Long ivory prayer panels cover a fitted blue jack, with a small round
mantle clasp and divided rounded hems. The broad cloth collar provides the
sheltering silhouette; shoulders have no stacked plate wings or aggressive
ornaments. Small silver knee guards and ivory-wrapped bracers keep protection
close to the body.

The continuous dark travel boots gain ivory calf gaiters, short silver shin
guards and subdued bronze binding. A newly authored rounded chapel helm covers
the crown while leaving the face opening clear, with flush ivory side bindings,
a restrained bronze brow frame and a blue nape lining. It has no raised crest,
lantern or horns. The native body, face, beard, hair and backpack remain
controlled by the game's existing equipment route.

All 61 non-Mercy asset and palette hashes match the pre-pass snapshot, including
Novice, Oathkeeper and Highward. Binary and exact-binding validation,
deterministic rebuild of all 73 generated files and six-helmet mount checks
pass. Offline construction renders use original apparel in bind pose with the
native body omitted. A first Female and Male native Party Select pass showed a
clear face opening, a distinct mantle silhouette and continuous boots. Close
review prompted removal of covered starter coat tails and boot straps that
intersected the new cloth in idle, ivory reverse faces for the prayer panels,
and a small ivory gaiter binding. A fresh native pass must verify those changes.
These previews use a temporary isolated starter-slot mapping for visual fit
only. Combat movement, nonhuman fit, real Mercy acquisition and equipment
transfers, resource lifetime and user art approval remain separate gates.
Icons, loot-display derivatives and marketplace assets are unchanged.

## Censure disciplined guardian candidate

Censure is an offensive horizontal max-level option with a fitted oxblood jack,
three overlapping charcoal and slate chest chevrons, and short angular steel
shoulder caps over a compact red battle mantle. Angled hip guards flank narrow
pointed cloth panels, keeping the legs separate and the silhouette compact.
The chest plates follow the original garment's spine weights, and the shoulder
caps blend through the original scapula and shoulder bindings. A small badge,
belt clasp and rivets restrict the gold to functional-looking fittings.

The continuous dark boots carry tall folded greaves with oxblood side lining
and three squared, overlapping foot plates. The original open battle helm has
a faceted complete crown, a strong slate brow frame, two small bronze rivets
and red nape lining. It leaves the face open and has no visor, horns or tall
crest. Censure is distinguished by angular protective plate and restrained
color, rather than a larger body or threatening ornaments.

All 61 non-Censure asset and palette hashes match the pre-pass snapshot,
including Novice, Oathkeeper, Highward and Mercy. Binary and exact-binding
validation, deterministic rebuild of all 73 generated files and six-helmet
mount checks pass. Offline construction renders show original equipment in
bind pose with the native body omitted. Fresh Female and Male native Party
Select previews must check chest-plate overlap in idle, shoulder and collar
fit, tasset and cloth separation, face clearance, and greave/foot articulation.
Real Censure acquisition and equipment transfers, combat motion, nonhuman fit,
resource lifetime and user art approval remain separate gates. Icons,
loot-display derivatives and the marketplace package are unchanged.

## Verdict commanding guardian candidate

Verdict is a horizontal max-level protector option built around a single bright
steel breastplate, broad navy chest heraldry and burnished gold binding. A small
order seal joins the chest chevron. Compact silver shoulder guards sit over
short navy mantle folds, with small heraldic fields and restrained gold edges.
Two articulated tasset plates on each side overlap a divided navy tabard. Its
proportions stay close to the other guardian sets; the hierarchy comes from the
structured plate, heraldry and color rather than oversized shoulders.

Bright calf armor has navy inset fields and burnished upper and lower bindings.
The continuous dark instep remains visible above a low burnished toe guard.
The original crowned helm has a complete steel and navy crown, a low gold band
and three blunt plaques fitted against the crown. It leaves the native face
opening clear and has no tall crest, visor or projecting horns. Original male
and female armor routes retain the native body, face, hair and backpack.

All 61 non-Verdict asset and palette hashes match the pre-pass snapshot,
including every preceding set. Binary and exact-binding validation,
deterministic rebuild of all 73 generated files and six-helmet mount checks
pass. Offline construction renders contain original apparel in bind pose with
the native body omitted. Fresh Female and Male native Party Select previews
must check breastplate and heraldic-panel seams in idle, collar and shoulder
fit, layered tasset/tabard clearance, the low coronet and face opening, and
calf/toe guard fit. Actual Verdict acquisition and equipment transfers, combat
motion, nonhuman fit, resource lifetime and user art approval remain separate
gates. Icons, loot-display derivatives and the marketplace package are unchanged.
