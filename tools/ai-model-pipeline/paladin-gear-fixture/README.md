# Paladin all-gear starting-inventory fixture

This test-only generator creates a separate package copy in a new scratch
directory. It never deploys, launches, publishes, edits the production package,
mutates game databases or grants items to a running character.

```sh
python3 tools/ai-model-pipeline/paladin-gear-fixture/test_build.py
python3 tools/ai-model-pipeline/paladin-gear-fixture/build.py \
  --output scratch/paladin-gear-fixture-v1
```

Existing output is refused. Choose a new scratch directory for another run. The
output contains `package/`, an external `receipt.json`, and a NEVER-PUBLISH notice.
The current source includes original GLB/PNG assets for the progression sets and
three legendary items. Only the package's
`content.json` differs from the production source; all current
original GLB/PNG assets and the manifest are copied byte-for-byte. The receipt
records every source and copied file hash and the exact content delta.

## Explicit fixture delta

- Existing class `paladin`: replace only `fields.startitems` with all 39 equipment
  keys, each appearing once. Preserve its original starting weapon, stats and
  Guardian capability. Paladin retains native FTK bodies, faces and hair; this
  fixture supplies only the original equipment assets.
- Add `paladin_gear_fixture_hunter`: `kind: class`, `template: hunter`, display name
  `Gear Fixture Hunter`, `guardian: false`, and fields `dlc: None` plus the same
  39 `startitems`. The native Hunter appearance and starting weapon remain
  inherited. No Paladin model plan or Guard icon is attached to this class.

The 36 progression keys are `paladin_{family}_{set}` for families `hammer_1h`, `hammer_2h`,
`shield`, `armor`, `boots`, `helmet` and sets `novice`, `oathkeeper`, `highward`,
`mercy`, `censure`, `verdict`. The three Artifact keys are
`paladin_hammer_1h_last_vigil`, `paladin_hammer_2h_kingsfall` and
`paladin_shield_last_bastion`. The fixture has 43 rows, versus the production 42.
The existing `startweapon` is intentionally unchanged, so native creation may
supply an additional instance of that starting weapon besides the start-items
list. Observe actual inventory instead of assuming equipped slots or quantities.

The public data loader maps `startitems` to `m_StartItems` and resolves the
`FTK_itembase.ID[]` references in phase two after registering all rows. The
fixture checks every reference against its 39 authored item/weapon rows. It uses
that existing declarative route rather than adding a registration hook or
hard-coding custom integer IDs. Native registration/creation remains a live gate.

## Isolated visual coverage plan

Use this copy as the sole Paladin package in a separate isolated trial and create
a new save. It deliberately retains `com.ftkmf.paladin` to preserve original
package-relative resources and item identities. Never load it beside production,
reuse a production save, publish its manifest or interpret it as the release.
The extra class changes the enabled content set and positional class identities.

Select both male and female Paladin appearances through native Party Select in
separate trials, then repeat with both Hunter fixture appearances. Inspect actual
backpack/equipped inventories before changing equipment. For each of the six
sets, equip body, boots and helmet, then test both 1H/shield and 2H loadouts.
The existing opt-in `equip_item` action accepts an already-owned stable key and
optional hero turn index, for example arguments
`{"item":"paladin_armor_mercy","hero":0}`. It refuses absent backpack items and
uses native equipment guards. Label that action as fixture-assisted equipping;
it does not establish ordinary UI interaction or acquisition.

Record exact class/skinset/item identities and package/framework hashes for
preview, overworld, combat, native attacks, break fragments, rebuild and cleanup.
The Hunter trials check unrestricted apparel/weapon appearance without Guardian
mechanics. Shared helmet or boot assets do not transfer fit evidence between
appearances. Nothing here establishes native animation, fit, acquisition, balance,
resource disposal or multiplayer until those observations are actually made.

All gear supplied by this package is **granted at character creation as a
fixture**. Normal shop/drop acceptance must use the unmodified production package
and separate evidence. Do not refresh or generate market stock to turn this
fixture into acquisition evidence.
