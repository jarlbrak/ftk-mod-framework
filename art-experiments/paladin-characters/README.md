# Original Paladin armor equipment

This package authors only equipment. Native FTK bodies, faces, hair and character
appearance choices remain native. Obsolete original character/default-outfit and
portrait experiments are archived in ignored scratch and are not runtime assets.

There are 24 runtime models: female and male armor, shared boots and a rigid helmet
for each of Novice, Oathkeeper, Highward, Mercy, Censure and Verdict. Novice uses
a fitted leather jack over short slate sleeves, single compact shoulder caps,
brown leather boots and a continuous open-face iron skullcap. Gold borders and order seals
are reserved for later progression. Later sets retain their own colors
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
