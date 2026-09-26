# Thunderstore distribution

Thunderstore has an existing community for the original *For The King* and accepts BepInEx packages. This repository can publish the framework and the released content packages listed in `marketplace/catalog.json` there, alongside the existing GitHub releases and curated in-game marketplace.

## Package layout

Thunderstore reserves the root `manifest.json`, `README.md`, and 256 by 256 PNG `icon.png` for package metadata and presentation. A Thunderstore BepInEx profile installs each package below its own `BepInEx/plugins/<namespace>-<package>` folder. Our content packages therefore place the framework's own manifest, content JSON, and assets under `plugins/FTKMFContent/`. `ModDiscovery` recognizes Thunderstore metadata by `version_number` and scans only that reserved child folder. Code-only framework packages have no content child and are ignored by content discovery.

The framework package contains the exact `FTKModFramework.dll` attached to its published GitHub release and depends on `BepInEx-BepInExPack_ForTheKing-5.4.19001`. A content package depends on the latest stable FTK Mod Framework package already published on Thunderstore. The release workflow checks that dependency exists before it uploads the content package. The content version must match both its source `manifest.json` and the production catalog entry.

The package builder is game-free for content packages:

```sh
python3 marketplace/packages/build_thunderstore.py \
  --all-content \
  --namespace JarlBrak \
  --output /tmp/ftk-thunderstore-candidates
```

This creates deterministic candidates and a SHA-256 receipt. It does not upload them. The framework DLL itself is built by the existing release process against the installed game assemblies; GitHub Actions cannot compile that project from this repository alone. The Thunderstore release workflow consumes the verified DLL already attached to the GitHub release. Game assemblies are not copied into either package.

## GitHub Actions setup and release flow

Before the first upload, configure the repository:

1. Create or select the Thunderstore team that will own these packages. From the team's settings, create a service account and save its API token as the `THUNDERSTORE_API_TOKEN` Actions secret. Keep that token out of source and workflow logs.
2. Add the exact team namespace as the `THUNDERSTORE_NAMESPACE` Actions variable.
3. Optionally configure the `thunderstore` environment with required reviewers if each upload should wait for a release approval.
4. After this change is merged, publish the next stable framework release with the normal version bump, from a commit that includes Thunderstore content discovery. GitHub's current `v1.5.0` release predates this loader support and the package builder rejects it. The `release.published` event uploads the supported framework package automatically.
5. After that workflow succeeds, use **Actions → Thunderstore release → Run workflow** to bootstrap the currently released content tags one by one. Dispatch requires an already published, non-prerelease GitHub Release. Do not bootstrap an unreleased candidate.

After bootstrap, publishing a stable GitHub release automatically builds and uploads the matching Thunderstore package. Supported tags are `vX.Y.Z` for the framework and `paladin-vX.Y.Z`, `thief-vX.Y.Z`, `possum-vX.Y.Z`, or `lore-store-unlocked-vX.Y.Z` for the content packages. Draft and prerelease releases are excluded. If a content upload races ahead of its framework package, it fails the dependency preflight; rerun the workflow after the framework upload completes.

CI builds and inspects all current content package candidates on pull requests without a game install or Thunderstore token. Publishing uses Thunderstore CLI `0.2.4`, pinned in the workflow. This version is marked pre-release by the CLI project, so the pin avoids silently changing the publishing client; review its release notes before deliberately upgrading it. Thunderstore currently returns no category slugs for the For The King community, so the publisher does not assign categories.

## Tradeoffs and distribution rules

Thunderstore adds a familiar package browser, manager profile installation, and dependency installation for players already using its For The King community. It also creates another public copy and update path to maintain.

- A package version is immutable, the highest semantic version is shown as latest, and version strings do not take prerelease suffixes. Correcting an upload needs a higher version. A published bad version cannot be replaced byte-for-byte in place.
- Thunderstore dependencies identify exact package versions. A content upload depends on the framework release present when it is published; republish content with a new version if it later needs a newer framework.
- Thunderstore metadata and the existing marketplace descriptor are different formats. Thunderstore uploads do not pass through the launcher's catalog hash, platform/game-build compatibility, or approval checks. Keep the existing package validation and gameplay gates before publishing, and keep both listings aligned.
- Thunderstore package installs live in manager profiles, while the launcher installs the framework and marketplace packages through its own paths. Do not install the framework or a content mod twice in one active profile through different channels. Duplicate copies of a content package share the same `modGuid` and will be reported by the framework loader.
- The Thunderstore community is a separate service and can reject or deprecate listings under its moderation rules. The Thunderstore Mod Manager itself is documented for Windows; compatible community tools have their own platform coverage. Do not imply the same manager is available on every platform.
- The For The King community currently has no category filters, so listings rely on search and the community's All view. Its audience and browsing surface are separate from the curated in-game marketplace and the project website.

Thunderstore requires authors to follow copyright and license rules and prohibits distributing game assemblies such as `Assembly-CSharp.dll`. The packages produced here contain original project files and the framework's own plugin only; the BepInEx pack remains a package dependency.

## Website release handoff

No Thunderstore listing is live as part of this code change, so the current website remains unchanged. When the first listings are published, update the player-facing distribution choice and links in `website/src/content/docs/installation.md`, the platform and source explanation in `website/src/content/docs/compatibility.mdx`, duplicate-install guidance in `website/src/content/docs/troubleshooting.md`, each affected `website/src/content/docs/mods/*.mdx` page, and `website/src/content/docs/releases.mdx`. Keep package availability separate from gameplay verification, and link the exact Thunderstore package pages only after their public URLs work.

## References

- [Creating a Thunderstore package](https://wiki.thunderstore.io/mods/creating-a-package)
- [Thunderstore package layout for BepInEx](https://wiki.thunderstore.io/mods/packaging-your-mods)
- [Updating an immutable package](https://wiki.thunderstore.io/mods/updating-a-package)
- [Thunderstore CLI](https://github.com/thunderstore-io/thunderstore-cli)
- [For The King Thunderstore community](https://thunderstore.io/c/for-the-king/)
