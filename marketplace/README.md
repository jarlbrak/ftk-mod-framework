# Curated community catalog

`catalog.json` is the production discovery source. It contains the reviewed Paladin 1.0.0 package, whose archive and banner are published as immutable release assets. Do not add the interface preview's fictional listings or integration fixtures.

The in-game client consumes this catalog through the installed native helper. See [the player and maintainer guide](../docs/MARKETPLACE.md) for installation, recovery and review requirements. The implementation contract is [spec #101](https://github.com/jarlbrak/ftk-mod-framework/issues/101).

## Local package validation

Build the helper with `go build` in `launcher/helper`, then run:

```sh
ftkmf-launcher-helper marketplace-catalog-validate --catalog /absolute/catalog.json
ftkmf-launcher-helper marketplace-validate --descriptor /absolute/package.json --archive /absolute/package.zip
```

The descriptor uses the catalog package contract in `launcher/helper/marketplace.go`. Required identity and compatibility metadata includes packageId, modGuid, name, author, description, category, version, license, frameworkVersion, frameworkRange, gameFingerprints, platforms and classification. Artifact identity consists of packageUrl, sha256, compressedSize, expandedSize and fileCount. Dependencies pin packageId/version pairs. Keep source/support links, changelog, requirements, content changes and screenshot references meaningful to players.

`frameworkVersion` is the author-confirmed minimum. The canonical `frameworkRange` is derived from it, for example `0.1.2` uses `>=0.1.2 <1.0.0`. New major compatibility requires a new mod release with an updated declaration. See [mod versioning](../docs/MOD-VERSIONING.md). Game fingerprints are SHA-256 hashes of the installed `Assembly-CSharp.dll`; do not upload that DLL. Platform identifiers are `macos`, `linux` and `windows`. A fingerprint/platform claim must have corresponding game-test evidence.

Archives place `manifest.json` and content JSON at their root. Stable manifest modGuid, version, and frameworkVersion must match the descriptor. Screenshots and other approved images belong to the package's asset files. Code, links, unsafe paths and unsupported content are rejected. A validation pass is not game-test evidence.

For an isolated integration run, the separate developer-only command `marketplace-fixture` accepts `--descriptor`, `--archive`, `--request` and `--result` file paths. It stages a locally supplied fixture using the same validation and generation preparation. This command is not exposed by the in-game operation whitelist. Use a dedicated test state and never publish fixture catalog data.

Submit changes for maintainer review, publish the reviewed archive without replacing an existing version, and only then add its descriptor to the catalog. Package revocation blocks new preparation and produces a notice for installed copies; it does not silently delete player files.
