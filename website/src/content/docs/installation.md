---
title: Start your adventure
description: Install FTK Mod Framework and add published mods through the in-game Mods menu.
---
You need an owned Steam copy of the **original For The King (2018)**. The framework does not include the game and does not target For The King II.

## 1. Get the launcher

Download **framework 1.4.0**, extract it into a permanent folder, and keep its files together.

- [macOS launcher](https://github.com/jarlbrak/ftk-mod-framework/releases/download/v1.4.0/FTKModdedLauncher-macos-universal.zip)
- [Windows x64 launcher (preview)](https://github.com/jarlbrak/ftk-mod-framework/releases/download/v1.4.0/FTKModdedLauncher-windows-x64.zip)
- [Linux AMD64 launcher](https://github.com/jarlbrak/ftk-mod-framework/releases/download/v1.4.0/FTKModdedLauncher-linux-amd64.tar.gz)
- [Linux ARM64 bundle (helper tooling)](https://github.com/jarlbrak/ftk-mod-framework/releases/download/v1.4.0/FTKModdedLauncher-linux-arm64.tar.gz)

Linux ARM64 provides helper tooling, not a native ARM game port. Windows bundles remain a preview. The launcher packages exist for these platforms; native Windows/Linux gameplay remains unverified. Paladin, Thief, Possum, and Lore Store Unlocked are published for Windows, macOS, and Linux. The same mod archive serves all three platforms; installation still requires a listed game build. Read [compatibility](../compatibility/) before installing a mod.

## 2. Add it to Steam

Choose **Games → Add a Non-Steam Game → Browse**. Select **For The King Modded.app** on macOS, **FtkModdedLauncher.exe** on Windows, or **For The King Modded.sh** on Linux. Start this shortcut to install the loader, framework, and matching marketplace helper, then open your game.

On macOS, the community launcher is not notarized. If Gatekeeper blocks it, right-click the app and choose **Open**.

Framework 1.4.0 includes the **Skyharbor** menu scene: a fortress airship dock with a departing and returning airship, plus the **MODDED EDITION** subtitle. It is enabled automatically; no separate mod is needed. To restore the original background, see [menu background settings](../troubleshooting/#change-the-menu-background).

## 3. Choose your mods

At the title screen, open **Mods → Discover**. Select a listing, read its requirements, review the proposed changes, and confirm installation. Follow the menu's activation guidance. Normally, changes take effect on the **next game launch**. Restart before starting your adventure.

Paladin 1.4.0 needs framework 1.2.1 or a compatible later 1.x release. Thief 1.0.0 adds a class and equipment. Possum 1.0.0 changes appearance. Lore Store Unlocked 1.0.0 needs framework 1.3.0 and unlocks the Lore Store while installed. All four install separately from the framework.

## Keep your adventure consistent

Use **Mods → Updates** to choose Stable, Preview, or a pinned version. The launcher checks compatible framework updates before Play and can use a verified installed version offline. Keep the same framework and mod versions for an ongoing run; start a new adventure when changing content. A saved mod list records versions and hashes, but does not guarantee save compatibility.

Enable, disable, remove, and update through Mods. Cancel pending changes there if needed. Do not edit managed package state while the game is running.

## Manual installation

The [repository installation guide](https://github.com/jarlbrak/ftk-mod-framework/blob/master/docs/INSTALL.md) documents the terminal installer and uninstall options. Manual content folders contain a manifest under the game's BepInEx plugins directory. For ordinary play, the launcher and in-game marketplace handle the package identity and dependencies for you.
