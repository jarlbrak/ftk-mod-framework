# Curated marketplace catalog

[`catalog.json`](catalog.json) is the production discovery source. Paladin 1.1.0
is its current published package. The archive and banner are immutable release
assets. Integration fixtures and fictional UI previews do not belong in this
catalog.

Players should use the [Mods marketplace guide](../docs/MARKETPLACE.md).
Mod authors can start with [Writing Content](../docs/WRITING-CONTENT.md) and
the shipped [Paladin package](packages/paladin/README.md).

## Validate a package

Build the helper with `go build` in `launcher/helper`, then run:

```sh
ftkmf-launcher-helper marketplace-catalog-validate --catalog /absolute/catalog.json
ftkmf-launcher-helper marketplace-validate --descriptor /absolute/package.json --archive /absolute/package.zip
```

A descriptor records identity, author, license, version, description,
requirements, compatibility, dependencies, links and preview images. Its
artifact fields record the exact archive URL, SHA-256, sizes and file count.
The stable manifest identity and `frameworkVersion` must match the descriptor.
For Paladin 1.1.0, the supported range is `>=1.0.3 <2.0.0`.
[Mod versioning](../docs/MOD-VERSIONING.md) defines the range rule.

Archives contain a root `manifest.json`, content JSON and approved assets.
Code, links, unsafe paths and unsupported content are rejected. A validator
pass proves structural consistency, not author trust or gameplay behavior.
Game and platform gameplay claims require corresponding live evidence. Paladin's
Windows/Linux install eligibility is separate from its gameplay verification.

Submit a new version for review through the
[package submission template](../.github/ISSUE_TEMPLATE/package_submission.yml).
Publish its reviewed archive before adding the descriptor to the catalog.
Never replace published bytes; corrections require a new version and hash.
The repository's [marketplace guide](../docs/MARKETPLACE.md#submit-a-mod)
summarizes submission and preview requirements.
