# Bounded Censure visual integration

`censure_visual_revision.py` supports the existing local visual integration workflow.
It does not generate models, change gameplay, run the game or publish packages.
Dry-run is the default. The local coordinator may call its `prepare` function and
choose `apply` only after explicit native and artwork acceptance. The current
Censure pilots have not been adopted by this implementation change.

## Required inputs

- Frozen `ftkmf.censure-visual-delivery.v1` deliveries for exactly armor, helmet,
  boots, one-hand hammer, two-hand hammer and shield. Each supplies exact previous
  canonical hashes, new model/hash-named texture hashes, unchanged native routes,
  visual overrides and pinned source/recipe lineage. Experimental folder names and
  revision numbers are not part of the adoption contract.
- A pinned baseline of the canonical package files and a prospective content hash.
- Frozen studio manifest and freeze receipt. Supply six matching Censure rows or
  the full gallery with every other portrait copied from the current canonical
  studio revision. Rows use the existing `id`, `package`, `content`, `sources`,
  `assignments`, `image`, `sha256` and `camera` fields. Content and source bytes must
  match the accepted prospective package. Generator, renderer, decoder and recipe
  identities are retained.
  Hammer portraits use the existing intact display-root assignment; hidden fracture
  children remain pinned by the complete delivery/content and are not added to the
  studio portrait's visible source list.
- Frozen icon manifest and freeze receipt using the existing delivery fields.
  Exactly four 512 by 512 RGBA images are copied from matching studio portraits:
  armor, helmet, boots and shield. Existing icon paths and canonical-before hashes
  are required. Weapon, class, proficiency and combat icons cannot enter this route.
- A separately pinned parent native review receipt, described below.

The icon delivery must name the exact studio manifest and its freeze receipt in
`sourceStudioManifest` and `sourceStudioFrozenReceipt`, and repeat the same `recipe`.
Every icon row supplies `canonicalIconPath`, `canonicalBeforeSha256`, `outputPath`,
`outputSha256`, `sourceRenderPath`, `sourceSha256`, `exactCopy: true`,
`sourceModelAndTexturePins`, `sourceRenderCamera`, `native64Bounds` and
`native64AlphaThreshold`. Source files and all frozen files are checked before writes.

## Native acceptance receipt

The receipt schema is `ftkmf.censure-native-acceptance.v1`. It must contain:

- `approved: true` and `authority: "parent-native-review"`.
- `baselineSha256`, sorted `deliveryManifestSha256s`, `contentSha256`, and
  `assetSha256s` mapping every prospective delivered canonical asset path to its hash.
- Exactly six `items` rows with `itemId`, `nativeEquipped: true`,
  `nativeDisplay: true`, a nonempty unique `wearers` array and nonempty `evidence`
  references of `{path, sha256}`. Armor also requires `nativeCombatMotion: true`.
- A nonempty `limitations` array. Wearer lists describe actual observed scope;
  accepting a receipt does not infer all-class, all-appearance, lifecycle or release
  coverage. The parent reviewer supplies the acceptance decision; the generator
  checks identity and required fields rather than judging screenshots.

## Preservation and verification

Before changing a byte, the workflow checks all baseline files, complete native
route structure, nonvisual fields, exact art inputs and current destination hashes.
Full previous Paladin, display and icon ledgers and previous content are retained
under a content-addressed history directory. Subsequent revisions also retain the
previous Censure revision receipt. Retired textures stay on disk with their original
records. Packaging continues to include only active references.

Only declared Censure model/texture assets, four nonweapon icons, visual content and
derived metadata may change. The other 24 display records and 38 current icon rows
stay byte-equivalent as JSON values. The resulting current icon union contains 42
rows. Shared Thief provenance is generated against the prospective ledgers without
changing any Thief package bytes. Neither rejected old Censure geometry nor held
Oathkeeper work is adopted implicitly.

### Optional separately accepted Oathkeeper successor

`sync_display_framing.prepare_oathkeeper_successor` selects only the accepted
Oathkeeper row from the retained original-shape manifest and studio-v4 icon
delivery. It verifies the two exact model hashes, unchanged texture, public recipe
pins, fifteen distinct native class-default review rows and the native display
review. The other helmet row in those historical manifests is excluded.

The local coordinator accepts this prepared, hash-pinned receipt through
`--oath-successor PATH SHA`. The parent acceptance receipt must explicitly include
the same `oathkeeperSuccessorSha256`. Preparation revalidates the receipt from its
original inputs. Its two models and one icon join the Censure transaction before
any write, so the original package baseline is checked once for the entire change.
The complete gallery may then contain six new Censure portraits, one exact accepted
Oathkeeper studio-v4 portrait, and 89 exact portraits from the previous studio.
The four-row Censure icon delivery remains separate; the Oathkeeper icon comes
from its preserved delivery. The resulting union still has 42 icon rows, with 37
unrelated rows and 23 unrelated display records retained. No Oathkeeper content,
texture, gameplay or renderer-route field changes are permitted.

The local coordinator requires an exclusive adoption lock, rechecks the baseline,
and runs both package validators after replacement. A failed write or postcheck
restores the preflight bytes and removes only files created by that transaction.
A leftover lock or temporary file requires inspection, not automatic retry.

Focused checks:

```sh
python3 -m unittest discover -s marketplace/packages -p test_censure_visual_revision.py
python3 marketplace/packages/validate_paladin.py
python3 marketplace/packages/validate_thief.py
```

Website impact: implementation alone changes no published behavior or images.
After accepted adoption, refresh the Paladin preview's six Censure portraits and
source pins from the same studio freeze. Release pages and availability remain a
separate publication gate. Historical evidence must retain its original asset pins.
