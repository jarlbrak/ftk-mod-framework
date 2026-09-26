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

## Thunderstore bootstrap

The initial Thunderstore submission was rejected as **Invalid submission**. A successful CI upload does not mean the package is approved or visible in the manager. Use the regular launcher downloads in the [installation guide](../installation/) while this is resolved. The following behavior describes the intended bootstrap flow, which has not passed live Windows acceptance.

The first Windows setup needs internet access to download the official current launcher and verify its checksum. If setup fails, the original game stays open; check the displayed error and retry. A fresh installation cannot work offline. After a launcher has opened successfully, the bootstrap retains a verified cached copy for offline use and failed updates. A damaged cache may require reconnecting and downloading again.

Use the Start menu or Steam entry created by setup for normal play. The durable entry and cache live under `%LOCALAPPDATA%/FTKModFramework/Bootstrap/`. Removing the Thunderstore package or its profile does not uninstall the managed framework or those files. Reopening the initial profile offers setup again. Other profile mods are not migrated. Use the normal framework uninstall instructions when removing the managed installation.

For this unverified Windows tester preview, [report](https://github.com/jarlbrak/ftk-mod-framework/issues) your Windows version, manager/version, whether setup reached the launcher, whether Play started the modded game, and the exact error. Remove personal information before attaching logs. Shortcut behavior after profile removal, offline launches, pins, and framework updates all need volunteer testing.
