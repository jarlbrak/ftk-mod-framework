# FTK Mod Framework

A thin **Windows** installer for FTK Mod Framework and its launcher. Requires the original *For The King*, Steam, .NET Framework 4.8, and internet access for first setup.

## Install once

1. Install this package and its BepInEx dependency in a For The King profile.
2. Start that profile and choose **Set up and open FTK Modded Launcher**.
3. Setup downloads the current launcher and opens it, then closes the game. Choose **Play** when the game has exited to complete installation.
4. For future play, use **For The King Modded** under **FTK Mod Framework** in the Windows Start menu. You can also choose **Add to Steam / Art** in the launcher while Steam is closed.

Those entries fetch launcher updates as needed. The launcher handles framework updates, and the in-game marketplace handles content mods. You do not need a new Thunderstore package for ordinary framework or mod releases.

A verified cached launcher is available for offline use after successful setup. This bootstrap has its own version; its number does not describe the installed framework version.

The thin entry, downloaded launcher, and framework live outside the initial manager profile. Deleting this package/profile does not uninstall them. Starting the initial profile again shows setup. Other mods in that profile are not migrated.

If initial setup fails, the game stays open and displays the error. Check your connection and retry. On macOS, Linux, or Proton, use the existing platform launcher from the player guide.

The package contains no game assemblies or content mods.

- [Player guide](https://jarlbrak.github.io/ftk-mod-framework/)
- [Source and support](https://github.com/jarlbrak/ftk-mod-framework)
- [License](https://github.com/jarlbrak/ftk-mod-framework/blob/master/LICENSE)
