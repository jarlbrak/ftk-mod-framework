# Framework and Paladin 1.0.0 launch

Target: framework `v1.0.0` and package `paladin-v1.0.0`. These are unpublished
release candidates. Historical beta receipts remain evidence for their recorded
builds; they do not prove the final release bytes.

## Required work

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
- [ ] Review source and complete the clean-tree framework packaging dry run.
- [ ] Publish immutable framework/package assets and verify downloaded hashes.
- [ ] Add the verified package to the production catalog and test Discover/install/restart.

Publish `paladin-v1.0.0` with `--latest=false`. This repository's latest stable
release must remain framework `v1.0.0`: the updater expects a framework `vX.Y.Z`
tag and framework update assets, not a content-package release.

## Coverage boundaries

The locally configured game is macOS. Online co-op and Windows/Linux/Proton
gameplay are not verified. Version 1.0.0 does not change those evidence limits.
Advertised platforms and game fingerprints must match actual verification.

The initial framework disables bundled development examples by default. Existing
explicit preferences remain intact. Paladin is a separate marketplace package.

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

For repeatable checks and publication mechanics, see [Releasing](../RELEASING.md)
and the [Paladin acceptance matrix](VALIDATION.md).
