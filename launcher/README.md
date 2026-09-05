# For The King Modded

A branded launcher for your Steam copy of For The King, powered by FTK Mod Framework.
The base game must already be installed and owned on Steam.

## Add it through Steam

1. Download the archive for your platform from [GitHub Releases](https://github.com/jarlbrak/ftk-mod-framework/releases). Extract it to a permanent location, such as Applications on macOS or a Games folder on Windows/Linux. Keep its files together.
2. In Steam, choose **Games > Add a Non-Steam Game > Browse**.
3. Select **For The King Modded.app** on macOS, **For The King Modded.sh** on Linux
   (choose All Files if necessary), or **FtkModdedLauncher.exe** on Windows.
4. Name the shortcut **For The King Modded**, then launch it.

Artwork is applied automatically on launch: portrait cover, landscape tile, hero banner,
transparent logo, and icon asset. Existing custom artwork is preserved. If Steam has not
saved the new shortcut yet, the default artwork ID is prepared; restarting Steam and
launching once more resolves the saved ID. Steam may need a restart to refresh cached art.
There is no background service and normal launch never closes Steam or edits shortcuts.
First-time setup on macOS/Linux may restart Steam to configure the game launch option.

Alternatively, close Steam and run **Add to Steam.command** (macOS), **Add to Steam.sh**
(Linux), or **Add to Steam / Art** inside the Windows launcher. This creates the shortcut
and all artwork together. Existing shortcuts and their names, IDs, and settings are preserved.
Backups are saved next to shortcuts.vdf before changes.

The launcher runs first-time setup automatically if the framework is missing.
On Windows, choose **Play** to begin. Setup installs the bundled
framework and the matching BepInEx loader. Then the launcher checks for updates and opens the original Steam game,
so Steam retains its native/Proton choice and ownership checks. Game activity may appear
under the original For The King entry while playing.

## Automatic updates

Each **Play** through this launcher checks the official GitHub repository for a newer
stable framework release before starting the game. The framework DLL and its matching
marketplace helper update together. Downloads are checked against the release's SHA-256
metadata before installation. Your settings, mods, and saves are preserved.

If GitHub is unavailable, the launcher uses the verified installed version. A release
that does not support your game build or conflicts with an installed or pending marketplace
mod is deferred. Interrupted updates are recovered before the next launch. A damaged
installation blocks launch and needs **Install / Repair** (Windows) or **setup.command**
(macOS/Linux). Unrecognized development installations are preserved instead of overwritten.

Preview releases are downloaded manually and do not enter the stable update feed. The
launcher itself and BepInEx are bundled bootstrap components; this updater replaces the
framework and paired helper only. A future incompatible bootstrap change requires a new
launcher download. Launching the original For The King shortcut directly does not run
this launcher's update check.

## Platform status

- macOS: universal launcher helper for Intel and Apple Silicon, macOS 13 or later.
  Unsigned community app; downloaded builds may require right-click > Open.
- Linux: amd64 package for Steam Deck and standard PCs; arm64 helper package is for
  tooling compatibility, not a claim that the x64 game runs natively on ARM.
- Windows: x64 game, Windows 10/11, .NET Framework 4.8. Cross-built on macOS;
  Windows installation, SmartScreen, Steam integration, and gameplay are **not runtime-tested**.

A non-Steam shortcut supports local artwork and a name, not a Steam store description,
achievements, or an official product page. This README supplies the launcher information.
The artwork extends the project's original crown branding; it does not redistribute game art.

## Verification

On 2026-09-05, the installed macOS non-Steam shortcut launched the owned FTK copy.
Steam's process log recorded the shortcut and then app 527230; the fresh BepInEx log
reported 5.4.23.5, framework 0.1.0, 29 SELF-TEST PASS, and zero SELF-TEST FAIL.
The existing arch-prefixed Steam option was retained. Artwork files and shortcut
preservation were checked on disk, but library rendering and the manual picker were
not visually verified because Steam screen capture failed.

The automatic-update launcher was also packaged and launched through that Steam shortcut.
With no stable release available, it verified and enrolled the bundled pair, recorded the
expected update-feed 404, and launched app 527230 with 13 sample-content SELF-TEST PASS,
zero failures, and zero BepInEx errors. Configuration and other plugin hashes remained unchanged.
Updater tests cover newer-release installation, interrupted replacements, offline fallback,
compatibility holds, and shared install/repair locking. This is not a live stable-feed upgrade.

The Linux helper was executed in a Debian ARM64 container; native Linux/Proton gameplay
is still untested. Windows GUI cross-build and offline PowerShell installer checks passed
on macOS; no Windows game runtime was available.

## Building

Build the framework in Release, then run `bash launcher/build.sh` on macOS with Go, Python 3.9 or later, and
dotnet installed. Go helpers have no player-side Python, Go, or .NET dependency. Only the
Windows GUI uses the system .NET Framework runtime. The package includes the framework
plugin, never the game's assemblies. `assets/steam` contains ready-to-ship artwork;
`launcher/tools/build-art.py` regenerates it from the existing crown geometry using Pillow
and macOS Avenir Next fonts.

Validation: `cd launcher/helper && go test ./...`; installer tests remain under
`tests/installer`. Artwork filenames follow the local Steam grid convention. This is a
private Steam format, so malformed or unsupported shortcut files are rejected.

Maintainers: the source repository includes `docs/RELEASING.md` with the compatibility
policy, preview and stable release commands, and complete verified asset set.
