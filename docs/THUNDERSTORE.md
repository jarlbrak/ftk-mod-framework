# Thunderstore framework setup

Thunderstore provides the initial Windows setup for FTK Mod Framework. The package contains a small setup plugin and the normal Windows launcher bundle. After setup, players use **For The King Modded** to play and update the framework, and the in-game marketplace to manage mods. Individual mods retain their existing GitHub release and catalog process.

This is an unreleased integration. Publishing remains disabled until the Windows live acceptance gate below passes and the repository variable `THUNDERSTORE_BOOTSTRAP_READY` is set to `true`.

## Player flow

1. Install the framework package in a Thunderstore-compatible For The King profile. Its dependency supplies the For The King BepInEx pack.
2. Start that profile and choose **Set up and open FTK Modded Launcher** in the setup panel.
3. Setup checks the bundled launcher archive, copies it outside the manager profile, creates a Windows Start menu entry under **FTK Mod Framework**, and opens the launcher. The game closes only after the launcher confirms its waiting window is ready.
4. Once the game exits, choose **Play** in the launcher. The existing installer provisions the framework and verified helper in the actual game folder; the existing updater then checks for updates and launches the game.
5. Use the Start menu launcher entry for subsequent play. Optionally use **Add to Steam / Art** in the launcher to create a Steam shortcut. Manage mods through the in-game marketplace as usual.

The launcher remembers the exact game directory supplied by setup. Its durable location is `%LOCALAPPDATA%/FTKModFramework/Launcher/<archive-and-game-identity>/`. Removing the Thunderstore profile after setup does not remove this launcher or the normal managed installation. Reopening the initial profile shows setup again; it does not load the framework from that profile. Updating or reinstalling the Thunderstore package refreshes the setup bundle, not the managed framework. Removing the package is not an uninstall of the normal framework installation.

This route targets native Windows and requires .NET Framework 4.8 for the launcher. macOS, Linux, and Proton continue to use the existing platform launchers. The setup plugin shows an unsupported-platform message outside Windows; it does not attempt installation there. Other mods installed solely in the initial manager profile are not migrated to the normal installation.

## Ownership and failure behavior

Only `FTKThunderstoreBootstrap.dll` is exposed to BepInEx in the initial profile. The framework DLL stays inside the opaque launcher ZIP, preventing it from loading alongside the setup plugin. No runtime content discovery changes are needed.

The bootstrap helper verifies the archive digest, rejects unsafe ZIP paths and unexpected filesystem links, and stages a new durable directory atomically. Repeated setup verifies existing bytes before execution and never replaces a changed launcher directory. A checksum or staging failure keeps the game open and reports an error. A launcher readiness timeout also keeps the game open. Remove or move aside a damaged durable launcher directory before retrying; setup preserves it for inspection.

The Windows launcher holds a handle to the originating game process and disables its actions until that process exits. The normal updater retains its independent running-game checks, paired framework/helper verification, compatibility preflight, journal recovery, and offline fallback. No running-game exception or profile-specific updater is introduced.

SHA-256 checks establish consistency with the release assets and bundled metadata. They are not a separate publisher signature. The GitHub and Thunderstore accounts remain part of the distribution trust model.

## Release preparation and GitHub Actions

`launcher/build.sh` builds the bootstrap plugin alongside platform launchers. `release.sh` includes its DLL in the GitHub release and `SHA256SUMS`. A new stable framework release from this change is required; older releases lack the setup plugin and handoff support.

Configure the repository before enabling uploads:

1. Create or select the Thunderstore team and service account. Save its token in the `THUNDERSTORE_API_TOKEN` Actions secret.
2. Set `THUNDERSTORE_NAMESPACE` to the team namespace and configure the `thunderstore` environment as needed.
3. Complete the live acceptance gate below, then set `THUNDERSTORE_BOOTSTRAP_READY=true`.
4. Publish a stable framework GitHub release or manually dispatch its already published `vX.Y.Z` tag.

The workflow downloads the framework DLL, bootstrap DLL, Windows helper, Windows launcher ZIP, and `SHA256SUMS` from that exact release. It verifies hashes and the framework/helper bundle identity before creating the Thunderstore package, then uploads using pinned Thunderstore CLI `0.2.4`. Content tags, draft releases, and prereleases cannot publish. The For The King community currently has no category slugs, so no categories are assigned.

Local packaging uses the same builder without uploading:

```sh
python3 marketplace/packages/build_thunderstore.py \
  --tag vX.Y.Z \
  --namespace JarlBrak \
  --release-dir /path/to/downloaded-release-assets \
  --source-root /path/to/exact-release-checkout \
  --output /tmp/ftk-thunderstore-candidate
```

The ZIP contains Thunderstore metadata and `plugins/FTKSetup/` with the bootstrap DLL, setup helper, launcher ZIP, and its digest. It depends on `BepInEx-BepInExPack_ForTheKing-5.4.19001`. Neither game assemblies nor individual marketplace mods are included.

## Verification and live release gate

Game-free checks cover archive integrity and layout, rejection of content tags and unsafe paths, repeat setup, changed launcher preservation, durable storage after profile removal, argument validation, and existing installer/update behavior. Windows CI also exercises the launcher readiness event and game-exit control gating. Cross-builds do not establish in-game behavior.

Before enabling publication, validate on native Windows with a fresh manager profile:

- Install the package and confirm only the setup plugin loads. Verify the setup panel is visible and usable.
- Complete setup, observe the waiting launcher, and confirm installation starts only after the game exits and Play is selected.
- Confirm one framework loads from the normal game installation. Install and activate a catalog mod, select a framework update, and relaunch through the durable launcher.
- Reopen the Start menu and optional Steam entries and verify they target the original game directory.
- Test offline repeat setup, damaged archive failure, and failed launcher startup. Failures must leave the running game available.
- Remove or reinstall the original profile and verify the managed installation and launcher remain usable. Check that a refreshed bootstrap bundle preserves existing update preferences and installed mods.

No Windows in-game handoff or Thunderstore publication has been verified in this change. The repository has no configured local Windows game validation route.

## Tradeoffs

Thunderstore adds discovery and an initial BepInEx setup for its For The King audience. Keeping individual mods in our marketplace avoids duplicating their listings and release process.

The cost is one visible handoff from the manager to our launcher. Players must understand that subsequent play uses our launcher and that deleting the initial profile does not uninstall the managed framework. Thunderstore hosting and moderation add a service dependency. Published package versions are immutable, so packaging corrections require a higher version. New bootstrap packages can track stable framework releases without taking ownership of existing installations.

## Website impact and release handoff

No listing is live. At release, update `website/src/content/docs/installation.md` with the Windows setup flow, Start menu entry, subsequent launcher usage, and exact listing URL; `website/src/content/docs/compatibility.mdx` with verified manager/platform coverage; `website/src/content/docs/troubleshooting.md` with bootstrap retry and uninstall distinctions; and `website/src/content/docs/releases.mdx` with availability. Mod pages retain their existing distribution method. Verify published pages and links after deployment.

## References

- [Creating a Thunderstore package](https://wiki.thunderstore.io/mods/creating-a-package)
- [BepInEx package layout](https://wiki.thunderstore.io/mods/packaging-your-mods)
- [Updating a package](https://wiki.thunderstore.io/mods/updating-a-package)
- [Thunderstore CLI](https://github.com/thunderstore-io/thunderstore-cli)
- [For The King community](https://thunderstore.io/c/for-the-king/)
