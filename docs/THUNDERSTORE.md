# Thin Thunderstore bootstrap

Thunderstore distributes a small, independently versioned Windows bootstrap. It contains no framework DLL, launcher archive, or content mods. The bootstrap downloads the current stable launcher from official GitHub release assets; that launcher installs and updates the framework/helper pair through the existing managed flow. Individual mods retain their existing releases and in-game marketplace.

Ordinary framework releases do not require a Thunderstore update. Bootstrap bugs or changes to the download/launcher contract may still require a new bootstrap version. The initial publication is a Windows tester preview authorized before native Windows acceptance. Set `THUNDERSTORE_BOOTSTRAP_READY=true` only for an authorized publication; this switch does not record a successful live test.

## Player flow

1. Install the Thunderstore package and its For The King BepInEx dependency, then start the profile.
2. Choose **Set up and open FTK Modded Launcher**. First setup requires internet access.
3. Setup installs a durable thin launcher entry outside the manager profile, creates its Windows Start menu shortcut, downloads and verifies the current launcher, and opens it. FTK closes only after that launcher acknowledges that its window is ready.
4. Once the original game exits, choose **Play**. The normal installer provisions the framework/helper pair in the actual game folder, and the existing updater checks for updates before launching.
5. For future play use the **For The King Modded** entry under **FTK Mod Framework** in the Start menu. Optionally choose **Add to Steam / Art** in the launcher; Steam must be closed for safe shortcut registration. That Steam entry also targets the thin bootstrap.

Both persistent entries check for a current stable launcher before opening it. The downloaded launcher continues to honor the existing framework update selection, including pins; refreshing the launcher does not reset preferences or installed mods. A changed launcher archive is downloaded once and cached. The last launcher that successfully acknowledged startup is retained for offline fallback and rejected updates.

The exact game directory is saved. Durable files live under `%LOCALAPPDATA%/FTKModFramework/Bootstrap/<bootstrap-and-game-identity>/`, including the thin executable, cache, and versioned launcher directories. Deleting the initial manager profile does not remove this entry or the managed installation. Reopening that profile offers setup again. Removing the Thunderstore package does not uninstall the normal framework. Other mods in the initial profile are not migrated.

Native Windows and .NET Framework 4.8 are required for this supported route. macOS, Linux, and Proton retain the existing platform launchers. Platform detection is not a claim of Wine/Proton compatibility.

## Stable bootstrap contract

The thin entry resolves the official GitHub `/releases/latest` endpoint and accepts only a stable `vX.Y.Z` framework release. It reads `SHA256SUMS` from that exact tag and verifies `FTKModdedLauncher-windows-x64.zip` before extraction. The archive must contain the existing Windows launcher, installer, framework DLL, helper, and a schema-1 `bundle-manifest.json` whose version and framework/helper hashes agree.

The launcher contract is `--game-dir`, optional `--wait-for-process`, and `--ready-event`. The copied launcher receives validated game and bootstrap-entry sidecars. Its Start menu and Steam actions preserve the durable thin entry instead of linking directly to a cached version. Keep these interfaces backward compatible across ordinary framework releases. The bootstrap does not interpret the framework updater's helper protocol or select mod versions.

The first compatible stable framework release must include this launcher contract. Older launchers will not acknowledge readiness and are not a supported first-run target. Do not publish the bootstrap before a compatible stable framework release is available.

## Integrity, fallback, and ownership

Only the setup plugin loads in the manager profile. The framework is downloaded inside an opaque launcher archive and is installed normally after the game exits. No framework runtime content discovery changes are needed.

Download selection is serialized. Network requests have timeout and size bounds. Archives are checksum verified, inspected for unsafe paths and links, and staged into immutable directories. Repeat setup verifies existing bytes and preserves modified files. The cache pointer advances only after launcher readiness; an initialization failure tries the previous verified launcher. A missing or damaged cache cannot make a first offline installation succeed.

The launcher holds a process handle and disables installation actions until the originating game exits. The existing updater retains its own running-game checks, paired framework/helper integrity, compatibility preflight, recovery, and offline behavior. Setup failures leave FTK running. There is no updater exception for a live game or a manager profile.

SHA-256 establishes consistency with official release metadata, not a separate publisher signature. GitHub and Thunderstore account security remain part of the trust model. Cached versions are retained; automatic cache cleanup is outside this first implementation.

## Independent build and publication

`launcher/thunderstore/bootstrap-version.txt` owns the bootstrap version, independently of `Plugin.cs`. Build only its two binaries and checksums:

```sh
bash launcher/thunderstore/build.sh /tmp/ftkmf-bootstrap
```

An optional second argument supplies the installed game's managed-assembly directory. The build references Unity/BepInEx but does not package game assemblies. Output is `FTKThunderstoreBootstrap.dll`, `ftkmf-bootstrap-helper.exe`, and `SHA256SUMS`. Normal `release.sh` and `launcher/build.sh` do not build or publish these bootstrap artifacts.

Publish reviewed bootstrap artifacts under `bootstrap-vX.Y.Z` with **`--latest=false`**. Never make a bootstrap release GitHub's latest release: both the framework updater and thin entry use that endpoint for stable framework releases. Framework history filters non-framework tags.

```sh
gh release create bootstrap-vX.Y.Z \
  /tmp/ftkmf-bootstrap/FTKThunderstoreBootstrap.dll \
  /tmp/ftkmf-bootstrap/ftkmf-bootstrap-helper.exe \
  /tmp/ftkmf-bootstrap/SHA256SUMS \
  --target REVIEWED_COMMIT --latest=false --draft --notes-file RELEASE_NOTES
# After checking the uploaded assets and release notes:
gh release edit bootstrap-vX.Y.Z --draft=false --latest=false
```

Configure `THUNDERSTORE_API_TOKEN` as an Actions secret, `THUNDERSTORE_NAMESPACE` as the team namespace variable, and the `thunderstore` environment. After authorizing a clearly labeled tester release or completing live acceptance, set `THUNDERSTORE_BOOTSTRAP_READY=true` and manually run **Thunderstore bootstrap release** with the published `bootstrap-vX.Y.Z` tag. The workflow verifies source version and downloaded artifact checksums, then publishes with pinned CLI `0.2.4`. Framework and content release events do not trigger it.

Local package construction does not upload:

```sh
python3 marketplace/packages/build_thunderstore.py \
  --tag bootstrap-v1.0.0 --namespace JarlBrak \
  --release-dir /tmp/ftkmf-bootstrap \
  --source-root /path/to/exact-bootstrap-release-checkout \
  --output /tmp/ftk-thunderstore-candidate
```

The resulting ZIP contains Thunderstore metadata and only the two bootstrap binaries under `plugins/FTKSetup/`. Its package version is the bootstrap version and it depends on `BepInEx-BepInExPack_ForTheKing-5.4.19001`.

## Validation and release gate

Game-free checks cover thin package inventory, independent versioning, checksums, archive traversal rejection, first online setup, fresh offline failure, cached offline reuse, changed-file preservation, failed-update fallback, concurrent download exclusion, and exact game/shortcut associations. Windows CI checks both initial game-exit gating and subsequent launcher-readiness acknowledgement. Cross-builds and these fixtures are not in-game evidence.

For full Windows acceptance, validate a fresh native Windows manager profile through first download, readiness, game exit, normal installation, one framework instance, catalog mod activation, and a framework update/relaunch. Exercise the Start menu and Steam entries after publishing a newer launcher while leaving the Thunderstore package unchanged. Test offline reuse, failed startup, a damaged cache, pins/preferences preservation, and manager profile removal/reinstallation.

Native Windows in-game handoff remains unverified. The initial tester publication intentionally precedes this acceptance so volunteers can supply evidence. Framework 1.5.1 supplies the required launcher contract. Credentials are configured separately from source; a release enablement switch is not evidence of live acceptance.

## Initial submission status

Framework 1.5.1 and bootstrap 1.0.0 are published on GitHub. The first CI upload succeeded, but Thunderstore marked `JarlBrak-FTKModFramework-1.0.0` as **Rejected: Invalid submission**. No more specific reason was displayed. The package is not available in the mod manager; successful `tcli publish` output alone does not establish moderation approval. Publication is disabled again pending resolution. Do not retry unchanged uploads or claim public Thunderstore availability.

Thunderstore's [rejection guidance](https://wiki.thunderstore.io/mods/mod-not-visible) directs maintainers to its Discord rejected-uploads forum for clarification. Contacting moderators requires separate authorization. Native Windows acceptance remains outstanding.

## Website impact

At first publication update `website/src/content/docs/installation.md` with first-run internet access, the thin Start menu/Steam entries, and the actual listing URL; `compatibility.mdx` with verified Windows manager coverage; `troubleshooting.md` with cache failure/retry, offline behavior, and uninstall distinctions; and `releases.mdx` with availability. Mod pages keep their current distribution flow. Until then the public site must not advertise a live Thunderstore listing.

## References

- [Creating a Thunderstore package](https://wiki.thunderstore.io/mods/creating-a-package)
- [BepInEx package layout](https://wiki.thunderstore.io/mods/packaging-your-mods)
- [Updating a package](https://wiki.thunderstore.io/mods/updating-a-package)
- [Thunderstore CLI](https://github.com/thunderstore-io/thunderstore-cli)
