# Installing mods for For The King (macOS, SteamOS, Bazzite, Linux)

One command sets up everything: it finds your Steam copy of For The King, installs the BepInEx mod
loader that matches your build, installs the FTK Mod Framework, and sets the Steam launch option
that turns the loader on. No mod manager, no manual file copying, no editing Steam settings.

## Install

Open a terminal (macOS: Terminal; SteamOS or Bazzite: switch to Desktop Mode and open Konsole) and
paste:

```bash
curl -fsSL https://raw.githubusercontent.com/jarlbrak/ftk-mod-framework/master/install.sh | bash
```

Then launch For The King from Steam as usual. The installer prints what it did and what to look for:

- a **splash card** when the title screen first appears: the framework logo, its version, and the
  mods that loaded (any key skips it; `UI / ShowSplash` in the framework config turns it off),
- a **Mods** button on the title screen (toggle mods on and off; changes apply on the next launch),
- the **Thief** and **Innkeeper** classes at character select,
- **Smuggler's Run** and **The Hollow Mire** in the adventure list,
- the Blacksmith starts with the **Emberbrand** shortsword.

Steam must be closed for a moment while the launch option is written (Steam only reads that setting at
startup and rewrites it on exit). The installer asks before closing it and reopens it afterwards. If
you would rather not let it, answer "n" and it prints the one line to paste into
Steam > Library > For The King > Manage > Properties > Launch Options.

What the installer needs: `curl` (or `wget`), `unzip` (or `bsdtar` / `python3`), and a POSIX shell.
Every one of those ships with macOS, SteamOS, and Bazzite.

### Other content mods

Drop a mod folder (one holding a `manifest.json`) or a plugin `.dll` into
`<game>/BepInEx/plugins/`. The framework discovers data mods there at startup; see
[`WRITING-CONTENT.md`](WRITING-CONTENT.md) for how to make one.

## Check, update, remove

```bash
bash install.sh --status      # what is installed, the launch option, and the last log's verdict
bash install.sh               # run again any time: updates the framework to the latest release
bash install.sh --uninstall   # removes the framework and restores your launch option
bash install.sh --uninstall --purge   # also removes BepInEx (only if this installer put it there)
```

Run `bash install.sh --help` for every option (`--game-dir`, `--framework`, `--release`, `--dry-run`,
`--no-launch-options`, `-y`).

## What it does, per platform

| Platform | Game build it finds | Loader it installs | Launch option it sets |
|---|---|---|---|
| macOS (Intel or Apple Silicon) | `FTK.app` | BepInEx macOS universal + `run_bepinex.sh` + `libdoorstop.dylib` | `"<game>/run_bepinex.sh" %command%` |
| Linux, native build | ELF `FTK.exe` (yes, IronOak named the Linux binary `.exe`) | BepInEx linux x64 + `run_bepinex.sh` + `libdoorstop.so` | `"<game>/run_bepinex.sh" %command%` |
| Linux, Proton (the usual Steam Deck setup) | Windows `FTK.exe` under `steamapps/compatdata` | BepInEx win x64 + `winhttp.dll` + `doorstop_config.ini` | `WINEDLLOVERRIDES="winhttp=n,b" %command%` |

It tells the two Linux cases apart by reading the first bytes of `FTK.exe` (ELF vs PE), so it does
the right thing whether Steam gave you the native depot or you forced a Proton version.

The framework plugin itself is the same managed DLL on every platform.

Details worth knowing:

- **Where things go.** Loader files sit next to the game; the framework lands in
  `<game>/BepInEx/plugins/FTKModFramework.dll`; its settings are in
  `<game>/BepInEx/config/com.ftkmf.framework.cfg`; the log is `<game>/BepInEx/LogOutput.log`.
- **Steam libraries.** The installer reads Steam's `libraryfolders.vdf`, so the game is found on a
  second drive or an SD card (`/run/media/...`) too. It knows the native, Flatpak, and Snap Steam
  layouts on Linux.
- **Existing setups are respected.** If BepInEx is already installed (by r2modman, gib, or by hand)
  it is left alone and only the framework is added; pass `--reinstall-bepinex` to replace it with the
  pinned version (your old `run_bepinex.sh` is backed up first). A launch option that already runs
  BepInEx is left unchanged. Any other launch option you had is kept and merged, and restored on
  uninstall.
- **Downloads are verified.** The BepInEx archive is checked against a pinned SHA-256; the framework
  DLL is checked against the release's `SHA256SUMS`.
- **macOS specifics.** Downloaded loader files get their quarantine flag cleared, because macOS would
  otherwise refuse to inject the loader library. Steam's build of the game is not code-signed; should
  a future build ship with a hardened-runtime signature (which blocks injection), the installer says so
  and offers to strip it. On Apple Silicon the game runs under Rosetta; BepInEx 5.4.23.5's universal
  loader handles that without any extra launch-option trickery.
- **Multiple Steam accounts** on one machine each get the launch option.

## Branded non-Steam shortcut

For an identifiable **For The King Modded** library entry, use the platform launcher
package and follow [the launcher guide](../launcher/README.md). It applies bundled
library artwork on launch. Windows has a separate native installer in that package;
`install.sh` remains the macOS/Linux installer. Windows runtime testing is pending.

## Troubleshooting

**Nothing changed in the game.** Run `bash install.sh --status`. The last line reports whether the
framework loaded on the last launch. If the launch option is missing, Steam probably rewrote its
settings file because it was running while the installer wrote it: close Steam, run the installer
again, or paste the launch option by hand (see the table above).

**"Steam launch option does not run BepInEx".** Something else set a launch option after the
installer did. Add the loader line in front of it, or re-run the installer, which merges them.

**Proton (Steam Deck): the game starts but no Mods button.** Check that the launch option is exactly
`WINEDLLOVERRIDES="winhttp=n,b" %command%` (plus anything of your own after it) and that
`winhttp.dll` sits next to `FTK.exe`. Some Proton versions need the game to be launched once after the
files land before the override takes.

**macOS: "libdoorstop.dylib" cannot be opened / developer cannot be verified.** The quarantine flag
came back (for example after copying the folder with Finder). Run
`xattr -dr com.apple.quarantine "<game>"` or re-run the installer, which clears it.

**Native Linux build will not start at all, even unmodded.** That is a long-standing issue with the
2018 Linux build on some distributions; the community fix is to force a Proton version in
Steam > Properties > Compatibility. Re-run the installer afterwards: it detects the Windows build and
switches to the Proton setup.

**Send a bug report** with `<game>/BepInEx/LogOutput.log` attached and the output of
`bash install.sh --status`.

## For framework developers

`./deploy.sh` builds the framework and runs `install.sh --framework <your build> --dev` against your
Steam copy. `--dev` sets `Diagnostics/RunSelfTests = true` in the framework config so the load-time
self-tests run and the `SELF-TEST PASS` gate lines appear in the log. Those self-tests register
throwaway probe adventures and rows, so they are off for players by default.

The installer has its own test suite (mock Steam layouts, real BepInEx archives, no game needed):

```bash
bash tests/installer/test-install.sh
```

CI runs it on Ubuntu and macOS.

### Player content and developer fixtures

Normal installs reset developer self-tests, scale probes and forced enemy/encounter overrides. The Mods screen lists gameplay content, including the bundled **FTK Adventure Pack**, with descriptions. Developer example packages and intentional failure fixtures are excluded before loading in player mode. Files and saves are retained; saves made with developer-only content still require that content and its original configuration.

Use `./deploy.sh --player` to return a development install to player mode, or `install.sh --dev` (Windows: `install.ps1 -Dev`) for framework diagnostics. Gameplay preferences such as `EnableSampleContent` are preserved. Disabling a mod takes effect on the next game launch.
