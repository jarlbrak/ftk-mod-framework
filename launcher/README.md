# For The King Modded launcher

The launcher installs FTK Mod Framework into your owned Steam copy of the
original *For The King*. It includes the matching marketplace helper and opens
the game through Steam. It does not include the game or its assemblies.

## Add it to Steam

1. Download the archive for your platform from the
   [framework 1.0.1 release](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/v1.0.1).
   Extract it to a permanent folder and keep the files together.
2. In Steam, choose **Games > Add a Non-Steam Game > Browse**.
3. Select **For The King Modded.app** on macOS, **For The King Modded.sh** on
   Linux, or **FtkModdedLauncher.exe** on Windows. Name the shortcut
   **For The King Modded**.
4. Start that shortcut. First Play installs the bundled framework and the
   matching BepInEx loader. Later starts check for compatible framework updates
   before opening the game.

The launcher applies its artwork to the Steam shortcut. Steam may need a
restart to refresh cached artwork. Existing custom artwork is preserved.
On macOS and Linux, first-time setup may briefly restart Steam to configure
the original game's launch option. The launcher prompts before doing so.
The game may appear under its original Steam entry while playing.

The separate **Add to Steam** command in each platform archive can create the
shortcut and apply artwork together. It preserves existing shortcut settings.

## Updates and recovery

At the game title screen, choose **Mods > Updates**. You can follow Stable
releases, include Preview releases, or pin an exact framework version. The
launcher applies a selected update on the next Play, never while the game is
running. It verifies the framework DLL and matching helper together, checks
managed and configured manifest-mod compatibility, and keeps the verified
installed pair if GitHub is unavailable. Arbitrary BepInEx plugins are outside
that compatibility check. A version change preserves settings, mods and saves.

To restore the archive's framework version, close the game and choose
**Restore bundled** in the Windows launcher, or run
**Restore bundled.command** on macOS or **Restore bundled.sh** on Linux.
Recovery returns the update choice to Stable without removing installed mods.
Launching the original game shortcut directly skips the Modded launcher's
prelaunch update check.

For mod installation and marketplace repair, see the
[installation guide](../docs/INSTALL.md) and [marketplace guide](../docs/MARKETPLACE.md).

## Platform status

- **macOS:** Intel and Apple Silicon, macOS 13 or later. The community app is
  not notarized; a download may require right-click > Open. Gameplay has been
  tested on the verified Steam game build.
- **Linux:** x64 launcher for PCs and Steam Deck. The arm64 helper archive is
  for tooling compatibility, not a native ARM game port. Native and Proton
  gameplay remain unverified.
- **Windows:** x64 game on Windows 10/11 with .NET Framework 4.8. The launcher
  and installer have offline checks, but Windows installation, Steam
  integration and gameplay remain unverified.

The [1.0.1 release notes](../docs/releases/v1.0.1.md) state the tested scope.
Maintainers can find packaging and release procedures in
[Releasing](../docs/RELEASING.md).
