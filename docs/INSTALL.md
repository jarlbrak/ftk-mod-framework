# Install FTK Mod Framework

FTK Mod Framework adds a **Mods** menu to the original *For The King*. The game is
required and is not included. The [1.0.0 release](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/v1.0.0)
provides a launcher for macOS, Windows and Linux. Paladin is the first curated
marketplace mod; install it separately from the in-game catalog.

## Recommended: Modded launcher

1. Download and extract the launcher archive for your platform from the
   [framework release](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/v1.0.0).
   Keep the extracted files together in a permanent folder.
2. In Steam, choose **Games > Add a Non-Steam Game > Browse**. Select
   **For The King Modded.app** on macOS, **For The King Modded.sh** on Linux,
   or **FtkModdedLauncher.exe** on Windows.
3. Start that shortcut. The launcher installs the matching BepInEx loader,
   framework and marketplace helper, then opens your owned game.
4. At the title screen, open **Mods**. Choose Paladin in Discover, review the
   installation, and follow the menu's guidance on when the change takes effect.

The launcher checks for compatible framework updates before Play and can keep a
verified installed version when offline. **Mods > Updates** selects Stable,
Preview or a pinned version. See the [launcher guide](../launcher/README.md) for
version selection and recovery. See [Marketplace](MARKETPLACE.md) for mod
management, save compatibility and repair.

## Manual installation on macOS or Linux

The terminal installer uses the latest stable release:

```sh
curl -fsSL https://raw.githubusercontent.com/jarlbrak/ftk-mod-framework/master/install.sh | bash
```

Launch the original *For The King* entry in Steam afterwards. The installer
configures its Steam launch option. Steam must briefly close while that option
is written; the installer asks before closing it. If you decline, it prints
the launch option to enter yourself.

```sh
bash install.sh --status      # Show installation and last-load status
bash install.sh               # Update to the latest stable framework
bash install.sh --uninstall   # Remove the framework and restore the launch option
```

Run `bash install.sh --help` for game-directory, release, dry-run and other
options. Windows players should use the launcher bundle, which includes the
Windows installer.

A manually obtained content mod belongs in `<game>/BepInEx/plugins/` as a folder
containing `manifest.json`. The marketplace provides reviewed managed packages
through the Mods menu; [Writing Content](WRITING-CONTENT.md) explains the manual
format.

## Troubleshooting

**No Mods button appears.** Run `bash install.sh --status` for a manual install,
or use **Restore bundled** from the launcher package. Check
`<game>/BepInEx/LogOutput.log` for a loader or framework error.

**The Mods menu says repair is needed.** Use **Install / Repair** in Mods or
**Restore bundled** from the launcher package. The marketplace requires the
matching native helper as well as the framework DLL.

**A mod change is not active yet.** Check the next-launch review in Mods. Normal
install, enable, disable and remove operations apply on the next game launch.
An optional, restricted [title-screen activation mode](HOT-RELOAD.md) supports
same-process changes on the audited macOS build before starting or resuming an
adventure.

**The game cannot open a save after changing mods.** Restore the exact mod set
used by that save or start a new adventure. Marketplace operations do not edit
save files.

**macOS blocks the launcher.** The community app is not notarized. Try
right-clicking it and choosing **Open**. The installer clears quarantine from
the downloaded loader files.

Windows and Linux/Proton gameplay are not yet verified; online co-op with
Paladin is unverified. See the [1.0.0 release notes](releases/v1.0.0.md) for the
tested scope. When reporting a problem, include the platform, game build,
framework and mod versions, and relevant lines from `LogOutput.log`.
