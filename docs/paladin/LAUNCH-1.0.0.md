# Framework and Paladin 1.0.0 launch

This is the historical 1.0.0 launch record. Its release entries were withdrawn
when framework and Paladin 1.0.1 became the first supported public release.
Historical beta receipts remain evidence for their recorded builds; they do not
prove the 1.0.1 release bytes.

## Required work

- [ ] Close the [accessory gap plan](GAPS.md): six trinkets and six necklaces,
  original display art/icons, acquisition, equip/remove and persistence checks.
  The [source inventory](EQUIPMENT.md) now contains 51 equipment items / 54 entries;
  [offline accessory checks](ACCESSORY-VALIDATION.md) do not replace live testing.
  The previous native registration remains 39 equipment items / 42 entries.

- [x] Native Create Game-style mod selector with a large banner and concise metadata.
  Observed in the normal Steam installation in windowed mode. The UI borrows
  native visual assets onto its own objects and preserves reviewed mod actions.
- [ ] Resolve the sanctum scope: native Grand Sanctum of Life already grants
  +5 Vitality, +10 health and +2 regeneration; a separate native hidden Vitality
  sanctum does not exist. Verify the selected route in play.
- [x] Correct native item tiers and verify shop acquisition without a Paladin.
  [Recorded trial](launch-1.0.0-no-paladin.json): native purchase plus fresh-process
  resume; all 36 items present in shared loot-category caches. Random combat
  drops and later-tier purchase coverage remain open.
- [x] Run framework, player-mod, helper, installer, launcher and manifest checks.
- [x] Validate the current package, all referenced assets and package lifecycle.
  Repeat these checks if the package changes again.
- [ ] Exercise fresh game, combat, acquisition, sanctum and save/resume on final artifacts.
- [x] Review source and complete the clean-tree framework packaging dry run.
- [x] Publish framework/package assets and verify downloaded hashes. The Paladin
  release is immutable. Framework `v1.0.0` remains the latest stable release.
- [x] Add the verified package to the production catalog.
- [ ] Test public Discover/install/restart against the published catalog and artifacts.

`paladin-v1.0.0` was published with `--latest=false`. This repository's latest
stable release remains framework `v1.0.0`: the updater expects a framework
`vX.Y.Z` tag and framework update assets, not a content-package release.

## Coverage boundaries

The locally configured game is macOS. Online co-op and Windows/Linux/Proton
gameplay are not verified. Version 1.0.0 does not change those evidence limits.
Advertised platforms and game fingerprints must match actual verification.

Framework 1.0.0 still contains a bundled development pack and its default
setting enables it. This is a known release defect. Paladin is the only
published marketplace package; the bundled pack is being removed in a new
framework patch rather than changing immutable 1.0.0 release assets.

The current candidates also load in the normal Steam installation: framework
1.0.0, matching installed DLL/helper receipt, Paladin as the sole enabled mod,
and 42/42 registered entries with zero errors or warnings, including the three
legendary artifacts. Their [validation record](LEGENDARY-VALIDATION.md) separates
the observed combat effects from remaining acquisition, save and coverage gates.
The marketplace visual
check passed after unlocking and switching to windowed mode. This local
installation uses reviewed candidate files and a cached banner, not a public
catalog download. The [marketplace lifecycle trial](MARKETPLACE-LIFECYCLE.md)
verified off/on, uninstall, cached reinstall, cancellation, restore preparation,
discard and populated next-launch information. The helper now retains the exact
installed descriptor for toggles after a package leaves the catalog. Public
catalog installation and fresh artifact download remain release gates.

## Final-candidate live observations

The isolated macOS test copy loaded the final `85aaad10576f44e86ed81f047c50170a8982aed31bb770716079a6eccd9dafec`
Paladin archive with framework 1.0.0. A native Create Game run with a Paladin,
Hunter and Scholar entered the overworld, saved through the native menu, and
resumed in a fresh process. The 54 package entries registered and all 51 equipment
rows appeared in the native category caches. Both Life sanctum variants were
observed with their native modifiers; a sanctum visit remains untested.

Native combat produced a regular victory and loot vote. On a later encounter,
the Paladin selected Guard through the native ally picker. A one-shot test fixture
targeted the Hunter with a direct 9-damage enemy hit. The production Guard patch
reduced it to 5, and native hit playback left the Hunter at 33 from 38 HP.
This proves that controlled direct-hit route, not random enemy targeting or
multiplayer behavior.

A separate one-shot loot fixture appended the registered Novice Helm after the
native enemy loot generator ran. Its card rendered in the ordinary victory
screen; the Hunter collected it through the native vote and equipped it from
the backpack. This proves item display, collection and cross-class equip on the
final package. The fixture does not prove natural drop frequency, the other 50
equipment cards, or the twelve accessory stat and persistence checks.

For repeatable checks and publication mechanics, see [Releasing](../RELEASING.md)
and the [Paladin acceptance matrix](VALIDATION.md).
