<p align="center">
  <img src="assets/brand/ftk-logo.svg" alt="FTK Mod Framework" width="620">
</p>

<p align="center">
  <a href="https://github.com/jarlbrak/ftk-mod-framework/releases/latest"><img src="https://img.shields.io/github/v/release/jarlbrak/ftk-mod-framework" alt="Latest release"></a>
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="MIT license"></a>
  <a href="https://github.com/jarlbrak/ftk-mod-framework/actions/workflows/ci.yml"><img src="https://github.com/jarlbrak/ftk-mod-framework/actions/workflows/ci.yml/badge.svg" alt="Build status"></a>
</p>

**FTK Mod Framework 1.0.0** brings community mods to the original [For The King](https://store.steampowered.com/app/527230/) (2018). Its launcher installs the framework into your Steam copy, and the game's **Mods** menu lets you discover and manage content. The first marketplace mod is [Paladin](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/paladin-v1.0.0), a protector class with original equipment and 3D art.

## Install and play

1. Download the launcher archive for your platform from the [framework 1.0.0 release](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/v1.0.0) and extract it to a permanent folder.
2. In Steam, choose **Games > Add a Non-Steam Game > Browse**. Select **For The King Modded.app** on macOS, **For The King Modded.sh** on Linux, or **FtkModdedLauncher.exe** on Windows. Keep the extracted files together.
3. Start that Steam shortcut. The first launch installs the mod loader and framework, then opens your owned game.
4. Open **Mods** on the game title screen to browse, install, enable, disable, or remove mods. The menu tells you when a change takes effect on the next game launch.

The launcher checks for compatible framework updates before Play and can use the installed version when offline. **Mods > Updates** lets you follow Stable or Preview releases, or pin a version. Your game saves and mod settings are kept outside the launcher bundle. For manual installation, removal, and troubleshooting, see the [installation guide](docs/INSTALL.md).

## Make a mod

You can author content with JSON files and a manifest, or use the C# `Content.*` API for classes, equipment, abilities, enemies, encounters, and adventures. The framework provides deterministic content IDs and compatibility checks. Start with [Writing Content](docs/WRITING-CONTENT.md), then use the guides for [mod versioning](docs/MOD-VERSIONING.md), [adventures](docs/ADVENTURES.md), and [custom 3D models](docs/CUSTOM-MODELS.md). See [Contributing](CONTRIBUTING.md) if you want to work on the framework itself.

## Compatibility

The framework targets the original *For The King*, not *For The King II*. macOS gameplay has been tested with the verified Steam game build. Windows and Linux/Proton launcher packages are available, but gameplay on those platforms remains unverified. Online co-op has not been verified for Paladin; players in a modded session should use the same framework version and mod set. The macOS launcher is not notarized, so Gatekeeper may require **right-click > Open**.

Questions, feedback, and mod ideas are welcome in [Discussions](https://github.com/jarlbrak/ftk-mod-framework/discussions). The framework is MIT licensed; the game and its assets are not included.
