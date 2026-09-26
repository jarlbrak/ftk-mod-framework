---
title: Get back to the adventure
description: Fix missing Mods menus, pending changes, compatibility blocks, and save problems.
---
## There is no Mods button

Start the game from the modded Steam shortcut. Use **Restore bundled** from the launcher package if files are missing or an update was interrupted. For manual installs, the repository installer supports `--status` to show installation and last-load status.

## The menu asks for repair

Choose **Install / Repair** in Mods or **Restore bundled** from the launcher package. The framework needs a compatible native marketplace helper; copying only the framework DLL is not a complete marketplace repair.

## My mod is installed but nothing changed

Read the pending-change message in Mods, close the game, then relaunch. Changes normally apply on the next launch. Restricted title-screen activation is an optional advanced mode, not a reason to change mods during an adventure. If the change was canceled, prepare it again in Mods.

## Paladin has no Smite or Censure

Check that Paladin **1.4.0** is active with framework **1.2.1 or later compatible 1.x**. Censure comes from an equipped Paladin hammer. Smite comes from an equipped Paladin trinket, which must now be acquired and equipped. They are not permanent class actions. Smite's stronger multiplier requires an active Censure Resistance reduction; an Armor reduction will not activate it.

## Some Lore Store entries are still locked

Lore Store Unlocked needs framework **1.3.0 or later compatible 1.x** and a full game restart after installing. Entries from paid DLC you do not own, such as Lost Civilization, intentionally stay locked. Entries the game hides from the store stay hidden, and limited-time cloud entries unlock only while the game offers them.

## A mod is blocked as incompatible

Compare the installed framework, package version, platform, and game build with the listing. Update through the launcher and Mods menu where appropriate. Do not edit a manifest to bypass the compatibility check; get a compatible release instead.

## A save fails after changing mods

Restore the exact framework and mod set used by that run, or start a new adventure. Marketplace operations do not rewrite saves. If an update damaged a managed package, the menu can retain previous generations for rollback on a later launch.

## Change the menu background

Framework **1.4.0** includes the animated Skyharbor menu background. To restore the original background, close the game and open `BepInEx/config/com.ftkmf.framework.cfg` inside the game folder. Under `[UI]`, set `EnableSkyharborBackground = false`, save, and relaunch. Set it back to `true` to enable Skyharbor again. The file is created after the first framework launch.

If Skyharbor is unexpectedly missing, check that the launcher installed framework 1.4.0 and that this setting is `true`, then restart. Party Select intentionally uses the native character stage. If the old background appears in other front-end transitions, report the menu route and framework version.

## Report a problem

Framework 1.5.0 sends detected errors and unexpected previous-session exits to public framework GitHub issues by default, with filtered diagnostics. Turn off **Automatic bug reports** in **Mods > Settings & Help** to stop new automatic sends. The manual **Report Bugs** editor remains available when you want to explain a problem. Diagnostics can still contain personal information written by mods; see the [reporting disclosure](https://reporting-api-production-ff50.up.railway.app/privacy).

You can also [open an issue](https://github.com/jarlbrak/ftk-mod-framework/issues) with your platform, game build, framework version, mod versions, what you did, and what happened. Include only relevant log lines and remove personal paths and other private information. The loader's `BepInEx/LogOutput.log` inside the game folder can help identify the error. Do not upload game assemblies or saves containing private data.
