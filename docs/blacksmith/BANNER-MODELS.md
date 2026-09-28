# Kilnward banner model correction

## Design authority

The original [marketplace banner](../../marketplace/packages/classgear/promo/blacksmith-banner.png)
is unchanged. Its SHA-256 is
`bd129c59d2a0c2df7f09432ac852e690396200c501eabf397768cfe505b26a2b`.
The replacement equipment preserves its layered steel shoulders, brass edging,
leather bib and apron, square buckle, plated boots, banded block hammer, pointed
anvil shield, and open headband. Native faces, hair and backpacks remain native.

The [campaign](../../art-experiments/blacksmith-forge-rodin/banner-faithful/README.md)
retains original reference images, untouched Rodin sources, fitted Blender scenes,
repeatable exports and validation receipts. Astra High agents authored the art.
Four Rodin jobs used explicit generation budgets: 15,000 triangles for armor,
7,500 for one boot before mirroring, and 8,000 each for hammer and shield.
The two-handed hammer shares the generated hammer source; the headband is original
Blender geometry. The four jobs total 2 quoted credits; actual billing is not
confirmed by these receipts. Fitting revisions used no additional paid jobs. The fitted armor preserves the
visible generated surfaces and adds an original concealed trouser underlayer
for continuous coverage during bent-knee poses.

## Native correction findings

| Trial | Observation | Disposition |
|---|---|---|
| First replacement, female inventory | Headband floated above the hair; metal rendered pale. | Lowered and widened equipped headband; converted materials to darker steel, brass and leather. |
| Second replacement, female inventory and male preview | Headband and materials improved; user identified hovering bracers. | Forearm centerline and cuff endpoint correction required. |
| Second replacement, bent combat stance | Trouser coverage separated from boot tops around bent knees. | Subsequent apparel fitting revised concealed leg coverage. |
| Apparel v11, ordinary solo Vale Imp encounter | Block, attack, and victory captures completed. | Limited ordinary combat evidence; no end-game balance claim. |
| Apparel v12, native female front, three-quarter, and rear views | Bracer no longer hovered; right elbow patch was absent. | Inspected female fitting passed these views; v12 block, ordinary one-handed attack, and victory passed the sampled views. |
| Male v11 native review | Male appearance visually inspected. | Male model bytes are unchanged in v12; broader male motion remains open. |

The selected apparel revision is `fitted-v12`; the selected rigid revision is
`native-fit-v1`. The rigid correction lowered and widened the equipped circlet
and adjusted the diffuse materials. The source manifest pins are:

| Manifest | SHA-256 |
|---|---|
| [Apparel](../../art-experiments/blacksmith-forge-rodin/banner-faithful/apparel/manifest.json) | `1c4c15abebe28442127a467110d322c604db2a1368daac23aeef42733ab6a924` |
| [Rigid](../../art-experiments/blacksmith-forge-rodin/banner-faithful/rigid/manifest.json) | `6f1994b7a57141fbda7f2e210f7ab58cd17aea3f20fdc096da106f7baacbd049` |

Final v12 ordinary combat used a solo Vale Imp with 9 HP. The incoming turn
produced block frames 47-95 in a 120-frame, 10.908-simulated-second capture;
frames 49, 60, 75, and 90 were inspected with the supplemental studio camera.
The ordinary one-handed attack used no Focus and no cheat action, dealt 29
damage, and ended in victory. Attack frames 15-38 and victory frames 56-72 were
captured; native frames 18 and 30 and studio victory frame 60 were inspected.
No hovering bracers or knee holes were visible in those sampled views.

The supplemental studio camera loses framing during the attack lunge, so this
does not establish all close attack views. It is ordinary encounter motion
evidence, not a representative end-game balance test. No additional generation
spend was used for the fitting or material revisions.

These observations supersede any earlier acceptance of the rejected Kilnward
appearance. Idle views alone do not establish fit during attacks. The first
attempt to initialize a three-hero encounter stalled before enemy setup and
does not establish combat motion or balance.

The [final native fitting receipt](../../art-experiments/blacksmith-forge-rodin/banner-faithful/native-final-fit-review.json)
records the selected hashes, observed views, and limits without embedding game
geometry or captures. Acceptance is limited to the observed visual scope.

## Package and release boundary

The source package retains all 30 authored item identities and gameplay values.
Its six selected Kilnward visual identities are pinned by the explicit
[source selection](../../marketplace/packages/classgear_blacksmith.sources.json).
The complete runtime inventory is 141 assets, comprising 81 GLBs and 60 PNGs.
The circlet has an equipped route in addition to its separate item display.

The [release review](RELEASE-REVIEW.md) remains the authority for compatibility,
balance, distribution rights and untested runtime/platform gates. A corrected
appearance does not establish compatibility with a published framework release.
Nothing in this correction publishes the package or changes the production catalog.
