# Blacksmith helmet fit correction

## Reopened defect

User review identified floating headgear in the screenshots supplied with the
unpublished delivery. The prior 35-presentation review repeated one three-quarter
view and missed depth errors. Its helmet fit acceptance is withdrawn.

Fresh close-up native-avatar views show:

- The Kilnward circlet projects forward of the forehead instead of sitting on it.
- The lower-tier shells are perched forward and high, with cheek guards extending
  too far toward the face.
- Side views expose hair intersections that the full-body inventory images hid.

These are visual defects in the equipped model fit. Passing package validation,
binding checks, or combat rules tests does not establish acceptable appearance.

## Correction and evidence requirements

Fit the existing original Rodin meshes to the observed native avatars. Preserve
native appearance choices and record the authored mesh transforms. Keep failed
trial captures and source hashes; do not reinterpret them as accepted results.

For every claimed tier and appearance, inspect front, both side, and rear views
at head scale, plus the normal inventory presentation. If the backpack obscures
the rear rim, use an additional oblique view and retain any remaining occlusion
as an explicit limit. Check crown seating, brow and temple contact, face opening,
and hair intersections. A separate reviewer must inspect the corrected evidence.

Standalone display models must reflect the corrected helmet proportions. Their
framing transforms can differ from equipped attachment transforms.

## Retained trial results

| Trial | Result |
| --- | --- |
| Original final-v4 | Rejected: forward/perched helmets and floating circlet missed by prior review |
| Fit v5 | Rejected: seated shells introduced hair clipping and circlet was buried in hair |
| Fit v6 | Rejected: residual circlet gap and forward cheek guards |
| Fit v7 | Circlet seating corrected; enclosed helmets rejected for rear skull and local hair intersections |
| Fit v8 | Rear skull enclosure corrected; Demon ear intersections remain |
| Cutout prototypes | Rejected offline for edge/topology defects; not accepted for deployment |
| Fit v13 | Ear coverage improved but oversized temple flares fail appearance review |

The v7 nonhuman rear-shell failures demonstrate why fitting the human appearance
alone cannot establish support for every native skin.

## Native reference and acceptance scope

The user challenged the expanding ear-clearance work. A same-appearance native
comparison showed Demon ears passing through the vanilla Kettle Helm band/crown,
visible in both normal inventory and side close-ups. The deeper native Royal Helm
encloses the ears. This does not establish that every native armor clips, but it
does establish that a zero-intersection rule exceeds the observed vanilla standard.

The universal cutout, crown bulge and side-cap experiments are rejected. They
added complexity or weakened the silhouette while trying to remove a minor
intersection that vanilla itself exhibits. The selected correction is the v8
seated shell and rear enclosure, with the corrected v7 Kilnward circlet retained.
Small Demon ear intersections comparable to the Kettle Helm are an explicit
limitation. Floating forehead gaps and major skull breakthrough remain failures.

Future fit work starts with normal-scale native comparisons before close-up
inspection expands the scope.

## Selected correction and verification

The independent review covers all 28 tier/appearance combinations using mixed
angle coverage: 72 pilot close-ups for the lower tiers, 18 circlet views, and 27
remaining lower-tier inventory/side/rear-quarter views. It also records three
normal-scale Demon inventory views and two vanilla reference images. Lower rear
contact partly obscured by the backpack remains outside direct observation.
See the [pinned review](../../art-experiments/blacksmith-forge-rodin/approved-redesign/native-helmet-fit-review-v8/README.md).

The selected original meshes retain their seated crowns, clear face openings and
corrected rear enclosure. No experimental ear caps, cutouts or crown bulges are
included. The four standalone helmet models and inventory icons reflect the
corrected shapes. Combat icons are unchanged.

The strict rigid validator passed all 80 GLBs. The rebuilt package passed 32-item,
162-asset and 164-file archive validation, plus candidate staging checks. A
before/after content comparison confirms that only four helmet visual entries
changed; all stats, actions and other item entries are identical. The test helper
build and six studio boundary tests pass. These are scoped static fit checks;
historical combat motion records do not establish motion for the revised helmets.

The replacement unpublished archive SHA-256 is
`e4740690d57f3f0111ed55c05329da57247c39b7bc78150f243e6db11a8b4958`.
The original candidate is retained for traceability and is superseded.
All four final helmet card previews and four normal human inventory views were
captured from the staged candidate and inspected for complete framing, readable
text and matching equipped silhouettes.
