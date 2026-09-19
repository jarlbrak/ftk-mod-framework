# In-game community marketplace

The title-screen Mods panel is the entry point for free community content. Discover lists curated packages; Installed distinguishes managed downloads, bundled content and manually installed mods. The catalog starts empty. Test fixtures and the interface preview's concept listings are not downloadable community mods.

The interface puts gameplay descriptions and the next available action first. Mod cards distinguish what is enabled now from changes waiting for a restart. Package metadata and dependency details are available from secondary views. Required components stay separate from the gameplay list, under Components in Installed. Settings & Help is reachable from the footer of every view, including while an operation is running, and holds marketplace and registration status, the next-launch review, restore and the mod-list export.

Back is contextual. It leaves an open requirements page, gallery or release review first, then returns to the view and mod selection it came from, and closes the panel from a tab with nothing behind it. Returning to a view already behind you unwinds to it instead of stacking another copy, so Back always reaches the title screen in a bounded number of presses.

Players review the complete package/dependency plan before preparing a change. Prepared content applies on the next game launch, including launches from the original Steam entry. Installing, updating, disabling and removing managed packages never changes registrations inside the running game. Updates are explicit; cancellation leaves the active content unchanged. Rollback selects the retained previous generation for a subsequent launch.

Existing saves can depend on the current mod set, especially playable classes with positional IDs. Start a new run when changing content. Marketplace operations do not edit saves. A managed-set export records exact versions and hashes, but it does not prove compatibility with saves or another player's complete installation.

## Installation and repair

The player installers deploy `ftkmf-launcher-helper` (Windows/Proton: `.exe`) and a protocol/checksum record into the game's `BepInEx/ftkmf` directory, outside plugin discovery. Release installs verify the helper against the release's `SHA256SUMS`. The framework verifies its installed checksum before invoking it. Install / Repair restores missing or mismatched files.

A trusted local DLL can still be installed by itself. Installed content remains available; marketplace operations require the matching helper. Supply it with `install.sh --framework PATH --helper PATH` or Windows `install.ps1 -Framework PATH -Helper PATH`. Packaged launchers include the appropriate helper beside their framework DLL. `deploy.sh` builds native and Windows helpers for local development and chooses the executable appropriate to the detected game build.

## Content submission

V1 accepts supported JSON content and approved image assets. It does not distribute behavior DLLs, arbitrary BepInEx plugins, native libraries, scripts or campaign files. JSON content still requires review and game testing; file hashes verify bytes, not author trust or gameplay correctness.

Submit proposed packages through the repository's GitHub review workflow using the [package submission issue template](../.github/ISSUE_TEMPLATE/package_submission.yml). Each template field corresponds to one of these maintainer checks:

1. Permanent package ID and mod GUID, semantic version, license and author attribution.
2. Description, content changes, category, requirements, changelog, source/support links and screenshots or other content evidence.
3. Exact dependency versions, author-confirmed frameworkVersion and its derived same-major range, platform/build and game assembly fingerprint.
4. Archive hash, file inventory and size limits, with no unsafe paths, links, code files or unsupported JSON fields.
5. Successful registration and visible behavior in an actual game launch for each advertised build.

Published versions retain their bytes and SHA-256. Corrections use a new version. Initial hosting uses reviewed release archives in this repository; catalog edits belong in `marketplace/catalog.json`. Enable immutable releases before publishing package versions. Publication is a separate maintainer action from building the client.

See [mod versioning](MOD-VERSIONING.md) for the manifest compatibility policy, missing declarations, and confirming a new framework major.

## Preview images

A catalog listing can provide up to three curated PNG or JPEG images in its `screenshots` list. These may be item renders, character portraits, artwork, or in-game screenshots that accurately represent the mod. Put the cover image first; the interface uses it for the card thumbnail and main preview, with the remaining images available in the gallery. Preview images are optional.

Host previews as approved repository release assets. Each image must be at most 2 MiB, no more than 4096 pixels on either side, and at most 8,388,608 pixels total. The helper validates and caches images before the interface displays them. Keep labels and important details readable at thumbnail size; use the larger gallery for inspection. Credit image creators and include the rights to distribute their work during review.

The bundled Adventure Pack reuses its existing Hollow Mire artwork and Reeve Maddow portrait. These are illustrations included in the content, not screenshots or interactive 3D previews.

## Storage and recovery

Marketplace state lives under `BepInEx/ftkmf/marketplace`. Prepared generations are immutable and outside manually scanned plugin roots. A single activation record selects current, pending and previous generations. A process-held transaction lock prevents concurrent changes; operations retain incomplete staging for diagnosis instead of reusing it as an active generation.

The framework selects one generation before discovery, merges it with manual content in deterministic order, then freezes its enabled state for that process. A duplicate GUID blocks activation instead of overwriting manual content. Runtime registration errors remain visible even after disk activation succeeds; rollback cannot undo rows already registered in the current game process.

Do not manually edit activation records or remove generations while the game runs. If helper startup times out, the framework keeps the previously captured generation for that process and reconciles the disk state on a later launch. Catalog requests and archive downloads run outside Unity's main thread.

## Validation evidence

Helper unit tests run without game files on macOS, Linux and Windows CI. Installer fixtures exercise release hashes and platform helper placement without launching Steam. Game runtime validation still requires an installed copy of For The King; cross-compilation and PowerShell tests on macOS are not Windows/Proton gameplay evidence.

The local package in `marketplace/fixtures` exists only for integration tests. Keep it out of the production catalog.

### Mods panel at 1280x800 and controller navigation

Live evidence for the panel's layout and controller focus comes from driving the Mods panel through the `marketplace_ui` agent bridge (`FTK_AGENT_BRIDGE=1`) in a real game launch on macOS at 1280x800, reading each text element's preferred height against its rect and checking the `clipped` flag. It is not a screenshot diff and does not cover other resolutions or Windows/Proton.

| Date | Views checked | Result | Record |
|---|---|---|---|
| 2026-09-16 | Requirements page, preview gallery, Settings & Help status pane, stacked views with the footer | Zero clipped text; footer anchored at the panel edge in stacked views and unmoved in column views; controller focus restored to the control Back came from | commit `cc135c04` |
| 2026-09-16 | Next-launch review pane with a real pending change | Nothing clipped; pane reports rect 54 for 48 of content | commit `30dcd993` |
| 2026-09-19 | Installed, the next-launch review view, Discover with the real catalog fetched through the installed helper (post-merge pass of PR #118) | Zero clipped text across the views inspected; the Quit guidance line measured preferred height 24 in a 32 rect | PR #118, last comment |

An eight-line review page was not reproduced live because the test installation carried a single mod. Controller coverage is limited to Back's focus restoration; full pad traversal of every view is not recorded.
