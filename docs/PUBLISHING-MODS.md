# Publish a marketplace mod

The FTK marketplace is a curated catalog of content packages. [Paladin](../marketplace/packages/paladin/) is the published 1.2.0 example. A package submission proposes content for review; a maintainer publishes its archive and adds it to the production [catalog](../marketplace/catalog.json). The [player marketplace guide](MARKETPLACE.md) explains installation and recovery, while [mod versioning](MOD-VERSIONING.md) defines compatibility and immutable versions.

## Submit a package

Use the [package submission issue](../.github/ISSUE_TEMPLATE/package_submission.yml). Supply a permanent `packageId` and `modGuid`, semantic version, author and license, player-facing description and changes, exact dependency versions, source and support links, and distribution rights for every asset. Declare the earliest framework version you have confirmed and the tested game assembly fingerprints and platforms. Include actual in-game registration and behavior evidence for each advertised build. Do not attach game assemblies or decompiled game source; report only their SHA-256 fingerprints.

The archive contains root-level `manifest.json` and supported content JSON, with referenced PNG, JPEG, or bounded GLB assets under `assets/`. The marketplace accepts declarative content, not behavior DLLs, native libraries, scripts, or campaign files. The [content capability guide](GUARDIAN-AND-EQUIPMENT.md) describes currently supported typed declarations and model limits. Provide up to three accurate PNG/JPEG previews as separate release assets, credit their creators, and identify artwork that is not a screenshot. Each preview may be at most 2 MiB, 4096 pixels on either side, and 8,388,608 pixels in total. The submission must include the complete archive inventory and its measured SHA-256, compressed size, expanded size, and file count.

Run the package validator with the current native helper before submission:

```sh
cd launcher/helper
go run . marketplace-validate --descriptor /absolute/package.json --archive /absolute/package.zip
```

The validator checks descriptor structure, archive identity, hashes, paths, file types, model declarations, and size limits. Its success is separate from author rights review, native gameplay, and public download verification. The Paladin [package builder](../marketplace/packages/build_paladin.py) illustrates a deterministic archive and measured descriptor; it is specific to Paladin and its `--release` flag prepares URLs without uploading assets.

## Maintainer publication order

1. Review the source, submitted evidence, attribution, compatibility claims, player copy, archive inventory, and previews. Test the candidate in a real game for every platform and game fingerprint that the descriptor advertises. Narrow claims that lack evidence. Keep the package ID and mod GUID stable across versions; changes to published bytes require a new version.
2. Build or independently reproduce the final archive and descriptor. Run `marketplace-validate` on those exact bytes. Freeze the SHA-256, compressed and expanded sizes, file count, manifest identity, dependency pins, and source revision. Confirm the archive has no game DLLs, executable code, unsafe paths, local files, or secrets.
3. Create a draft GitHub release in this repository, upload the archive and approved preview images, and inspect their names, sizes and uploaded bytes. Publish only a complete release with immutable release protection enabled. Then download each public asset and compare its SHA-256 with the frozen local copy before catalog inclusion. Keep a mod-only release out of the framework updater's latest stable release selection; the Paladin release uses `--latest=false`.
4. Once the public archive and preview URLs work and their bytes match, add the reviewed descriptor to [`marketplace/catalog.json`](../marketplace/catalog.json). Validate it and merge the catalog review:

   ```sh
   cd launcher/helper
   go run . marketplace-catalog-validate --catalog ../../marketplace/catalog.json
   ```

5. On the merged catalog, verify the public Discover listing and banner in a normal game installation. Download and install through the Mods panel, restart or use only an eligible title-screen activation path, and confirm registration and visible behavior. Exercise disable, enable, uninstall, and reinstall for the published artifact. Record the game build, platform, package hash, framework version, and any untested behavior.

The catalog validator checks catalog metadata; it does not download the release asset. The package validator checks a local archive; it does not prove public availability or gameplay. Never replace a published archive in place. Revoke a broken listing to block new preparation while preparing a corrected version; revocation does not delete existing players' files. See [marketplace operations](MARKETPLACE.md) and [framework releasing](RELEASING.md) for their separate contracts.
