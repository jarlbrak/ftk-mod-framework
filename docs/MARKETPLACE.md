# In-game community marketplace

The title-screen Mods panel is the entry point for free community content. Discover lists curated packages; Installed distinguishes managed downloads, bundled content and manually installed mods. The catalog starts empty. Test fixtures and the interface preview's concept listings are not downloadable community mods.

Players review the complete package/dependency plan before preparing a change. Prepared content applies on the next game launch, including launches from the original Steam entry. Installing, updating, disabling and removing managed packages never changes registrations inside the running game. Updates are explicit; cancellation leaves the active content unchanged. Rollback selects the retained previous generation for a subsequent launch.

Existing saves can depend on the current mod set, especially playable classes with positional IDs. Start a new run when changing content. Marketplace operations do not edit saves. A managed-set export records exact versions and hashes, but it does not prove compatibility with saves or another player's complete installation.

## Installation and repair

The player installers deploy `ftkmf-launcher-helper` (Windows/Proton: `.exe`) and a protocol/checksum record into the game's `BepInEx/ftkmf` directory, outside plugin discovery. Release installs verify the helper against the release's `SHA256SUMS`. The framework verifies its installed checksum before invoking it. Install / Repair restores missing or mismatched files.

A trusted local DLL can still be installed by itself. Installed content remains available; marketplace operations require the matching helper. Supply it with `install.sh --framework PATH --helper PATH` or Windows `install.ps1 -Framework PATH -Helper PATH`. Packaged launchers include the appropriate helper beside their framework DLL. `deploy.sh` builds native and Windows helpers for local development and chooses the executable appropriate to the detected game build.

## Content submission

V1 accepts supported JSON content and approved image assets. It does not distribute behavior DLLs, arbitrary BepInEx plugins, native libraries, scripts or campaign files. JSON content still requires review and game testing; file hashes verify bytes, not author trust or gameplay correctness.

Submit proposed packages through the repository's GitHub review workflow. A maintainer must check:

1. Permanent package ID and mod GUID, semantic version, license and author attribution.
2. Description, content changes, category, requirements, changelog, source/support links and screenshots or other content evidence.
3. Exact dependency versions and tested framework range, platform/build and game assembly fingerprint.
4. Archive hash, file inventory and size limits, with no unsafe paths, links, code files or unsupported JSON fields.
5. Successful registration and visible behavior in an actual game launch for each advertised build.

Published versions retain their bytes and SHA-256. Corrections use a new version. Initial hosting uses reviewed release archives in this repository; catalog edits belong in `marketplace/catalog.json`. Enable immutable releases before publishing package versions. Publication is a separate maintainer action from building the client.

## Storage and recovery

Marketplace state lives under `BepInEx/ftkmf/marketplace`. Prepared generations are immutable and outside manually scanned plugin roots. A single activation record selects current, pending and previous generations. A process-held transaction lock prevents concurrent changes; operations retain incomplete staging for diagnosis instead of reusing it as an active generation.

The framework selects one generation before discovery, merges it with manual content in deterministic order, then freezes its enabled state for that process. A duplicate GUID blocks activation instead of overwriting manual content. Runtime registration errors remain visible even after disk activation succeeds; rollback cannot undo rows already registered in the current game process.

Do not manually edit activation records or remove generations while the game runs. If helper startup times out, the framework keeps the previously captured generation for that process and reconciles the disk state on a later launch. Catalog requests and archive downloads run outside Unity's main thread.

## Validation evidence

Helper unit tests run without game files on macOS, Linux and Windows CI. Installer fixtures exercise release hashes and platform helper placement without launching Steam. Game runtime validation still requires an installed copy of For The King; cross-compilation and PowerShell tests on macOS are not Windows/Proton gameplay evidence.

The local package in `marketplace/fixtures` exists only for integration tests. Keep it out of the production catalog.
