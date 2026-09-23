# Releasing the launcher and framework

This guide is for framework and launcher maintainers. To publish a content mod
to the marketplace, use [Publishing Mods](PUBLISHING-MODS.md).

Players download one platform archive, extract it permanently, and add the launcher
inside it as a non-Steam game. The archive includes the framework, native helper,
installer, and Steam artwork. No game assemblies are redistributed. First setup fetches
the appropriate BepInEx loader; subsequent launcher starts check for framework updates.

| Platform | Release archive | File to select in Steam |
| --- | --- | --- |
| macOS 13+, Intel or Apple Silicon | `FTKModdedLauncher-macos-universal.zip` | `For The King Modded.app` |
| Linux x64, including Steam Deck | `FTKModdedLauncher-linux-amd64.tar.gz` | `For The King Modded.sh` |
| Windows x64 preview | `FTKModdedLauncher-windows-x64.zip` | `FtkModdedLauncher.exe` |

The Linux arm64 archive supports helper tooling; it is not a native ARM game port.
Windows and Linux/Proton gameplay need real platform testing. macOS downloads are not
notarized, so Gatekeeper may require right-click > Open. Steam may need one restart to
refresh shortcut artwork. These limits belong in every early release's notes.

## Prepare a release

1. Set the same numeric `X.Y.Z` version in `FTKModFramework/Plugin.cs` and the framework
   `.csproj`. Every published tag and asset set is permanent; use a new version for changes.
2. Review `launcher/update-policy.json`. `autoUpdateFrom` is the range of existing framework
   versions this release can safely upgrade automatically. Add only game assembly hashes
   verified against the release. The initial policy contains the tested macOS game hash;
   other builds keep their bundled version until explicitly supported. The range governs
   automatic channels; an explicitly pinned version may bypass that range while retaining
   game, protocol, and managed-mod compatibility checks.
3. Run the framework Release build, game smoke with `SELF-TEST PASS`, installer tests,
   Go helper tests, launcher tests, and release manifest tests. Inspect `git diff --check`
   and confirm no game assemblies or private development files are staged.
4. Commit the reviewed changes. Build all packages on macOS with Go, Python 3.9+, dotnet,
   `lipo`, and `codesign`. Run `bash release.sh vX.Y.Z --dry-run` from a clean tree.
   This prints the retained staging directory and verifies the artifact set without upload.
5. Push the reviewed source branch using the `jarlbrak` GitHub account. The release script
   targets its exact commit and refuses source that is absent from GitHub.

## Publish

For an early preview, excluded from automatic updates:

```bash
bash release.sh vX.Y.Z --prerelease --notes-file release-notes.md
```

For a stable release, available to compatible automatic-update clients:

```bash
bash release.sh vX.Y.Z --notes-file release-notes.md
```

Add `--draft` to either command to upload the complete package for review without publishing.
The script asks before upload, uploads as a draft, verifies every asset name and size, then
publishes unless `--draft` was requested. Failed uploads or verification leave a draft for
inspection. Do not publish a partially uploaded draft. GitHub's immutable release setting
can protect tags and assets once published; attaching everything before publication is also
[GitHub's recommended workflow](https://docs.github.com/en/repositories/releasing-projects-on-github/managing-releases-in-a-repository?tool=cli).

## Release assets and update contract

`release.sh` attaches the four platform launcher archives, the framework DLL, four raw
platform helpers, both installers, `update.json`, and `SHA256SUMS`. The manifest is generated
from the finished DLL/helper bytes, after macOS helper signing. Every archive includes its
own `bundle-manifest.json` so the launcher can recognize an installation of its bundled pair.

`update.json` schema 1 contains the framework version, helper protocol, compatible upgrade
range, verified game assembly hashes, and exact asset names, sizes, and SHA-256 hashes.
The updater uses only the official repository: Stable filters out prereleases, Preview
allows them, and Pin resolves an exact selected release identity. It derives download
locations from that release. It does not execute a downloaded installer or update script.
The DLL and installed marketplace helper are staged together, validated, and replaced under
a recoverable transaction before Steam app 527230 starts. Existing and pending marketplace
package constraints can defer an otherwise valid update. Settings and user content are not
part of the replacement transaction.

GitHub's [latest release endpoint](https://docs.github.com/en/rest/releases/releases#get-the-latest-release)
excludes drafts and prereleases. Preview clients use the release list, while pinned clients
resolve their selected tag and verify its release identity. Publishing a preview does not
test the stable feed. Local automated fixtures exercise newer releases, bad hashes, interrupted writes, and
offline fallback; a first stable publication still needs an end-to-end download/launch check.
Hashes establish consistency with the official release metadata, not an independent publisher
signature. Account/repository security remains part of the distribution trust model.

Automatic updates do not replace the launcher bootstrap or BepInEx and do not update community
mods. Unknown local builds are preserved. For an incompatible launcher protocol, ship a new
launcher archive and explain the required manual download in release notes.

## Version-selection bootstrap

Launcher 0.1.1 introduces Stable, Preview, and pinned-version selection. Launcher 0.1.0
cannot read these preferences; existing users need the new bundle once. The new helper
records launcher capabilities so the in-game picker can report this requirement accurately.
Metadata browsing and preference changes never replace the running framework.

Keep the external Restore bundled action in every future package. Pinning an older release
can remove the in-game version picker. Restore verifies the package, reinstalls its pair,
and resets the update choice to Stable under the same launcher lock. Do not silently reset
preferences during normal Play or rewrite an immutable older release to add this feature.
