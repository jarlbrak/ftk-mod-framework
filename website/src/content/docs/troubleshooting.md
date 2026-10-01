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

Check that Paladin **2.0.0** is active with framework **1.7.0 or later compatible 1.x**. Censure and Smite belong to the Paladin and require an eligible hammer. Equip a Paladin hammer, native Smith Hammer, or native War Hammer. Trinkets do not unlock these actions. Other classes using a Paladin hammer receive Strike only. Guard remains available independently of the weapon.

Mercy enhances Smite through matching armor. An active Censure Resistance mark from your own Paladin provides a further bonus; an Armor mark does not activate that magic bonus.

## Back Alley gear is missing

The vendor lists gear for the buying character's class and hides pieces already equipped or in that character's backpack. Each piece costs one Guild Token. Enable a participating class mod, use its class, and visit a city in single-player. The shared service has no offers or drops when no participating catalogs are enabled.

## Some Lore Store entries are still locked

Lore Store Unlocked needs framework **1.3.0 or later compatible 1.x** and a full game restart after installing. Entries from paid DLC you do not own, such as Lost Civilization, intentionally stay locked. Entries the game hides from the store stay hidden, and limited-time cloud entries unlock only while the game offers them.

## A mod is blocked as incompatible

Compare the installed framework, package version, platform, and game build with the listing. Update through the launcher and Mods menu where appropriate. Do not edit a manifest to bypass the compatibility check; get a compatible release instead.

## A save fails after changing mods

Restore the exact framework and mod set used by that run, or start a new adventure. Marketplace operations do not rewrite saves. If an update damaged a managed package, the menu can retain previous generations for rollback on a later launch.

From framework 1.6.2, a save that uses content that is no longer installed is refused before it loads. The game shows **Can't load this save** with a message such as "This save uses content that isn't installed: 1 enemy type, 1 map encounter, 1 item", names the mod when it can, and leaves the save file unchanged. Choose **OK**, then restore the mods used by that run, start a new adventure, or pick another save from **Load**.

## Resume is stuck on "Crafting adventure"

The save most likely uses content, such as an enemy or item, from a mod or framework build that is no longer installed, and the game cannot finish rebuilding the map. Quit the game; the save itself is not changed. Then restore the framework and mods used by that run, start a new adventure, or choose another save from **Load**. **Resume** always opens the most recent save, so use **Load** to pick a different one.

Framework 1.6.2 and later shows a **Can't load this save** message for such a save instead of hanging, and keeps you on the title screen or in the **Load** menu. Earlier framework versions still hang, so the advice above applies to them.

## Change the menu background

Framework **1.4.0** includes the animated Skyharbor menu background. To restore the original background, close the game and open `BepInEx/config/com.ftkmf.framework.cfg` inside the game folder. Under `[UI]`, set `EnableSkyharborBackground = false`, save, and relaunch. Set it back to `true` to enable Skyharbor again. The file is created after the first framework launch.

If Skyharbor is unexpectedly missing, check that the launcher installed framework 1.4.0 and that this setting is `true`, then restart. Party Select intentionally uses the native character stage. If the old background appears in other front-end transitions, report the menu route and framework version.

## A tweak causes trouble

Framework 1.6.0 adds [Tweaks](../tweaks/). If one misbehaves, turn it off in **Mods > Tweaks**. A tweak marked **Shared rules** keeps its setting for the current run, because the run's rules are decided at start and saved with it. Your change applies to runs you start afterwards; resuming this run's saves keeps the rules they recorded. Please [report it](#report-a-problem) as well.

If pressing the refund focus key ends your turn, you are on the 1.6.0 preview, whose refund key was the game's End Turn key. Update to framework 1.6.1 or later, which uses **F**, or see [refund focus spent on movement](../tweaks/#refund-focus-spent-on-movement).

## Raw text such as `STR_` appears in the game

The game shows a raw key, a name starting with `STR_`, when it cannot find the text for it. To help find the cause, close the game, open `BepInEx/config/com.ftkmf.framework.cfg`, set `LogLocalizationMisses = true` under `[Diagnostics]`, and relaunch. Each missing key then adds one line to `BepInEx/LogOutput.log` naming it. Attach those lines to your report, then set the option back to `false`. It never changes any text. See [diagnostics settings](../tweaks/#diagnostics-settings-for-troubleshooting).

## A turn will not end

From framework 1.6.0, when your turn cannot end and nothing on screen explains why, the framework writes one line starting with `STUCK-TURN` to `BepInEx/LogOutput.log`. It may also write a `SOFTLOCK-SIGNATURE` line when the game takes a path known to cause softlocks. Send a report from **Options > Report Bugs** with diagnostics included, or attach those lines to an issue. These lines only describe the game's state; they do not fix the stall.

## Report a problem

Framework 1.5.0 sends detected errors and unexpected previous-session exits to public framework GitHub issues by default, with filtered diagnostics. Turn off **Automatic bug reports** in **Mods > Settings & Help** to stop new automatic sends. The manual **Report Bugs** editor remains available when you want to explain a problem. From framework 1.6.0, diagnostics also include your [tweak](../tweaks/) settings and which tweaks were actually on, including a run's Shared rules and where they came from. Diagnostics can still contain personal information written by mods; see the [reporting disclosure](https://reporting-api-production-ff50.up.railway.app/privacy).

**Automatic reports were not delivered before framework 1.6.2.** Framework 1.6.1 and earlier saved automatic reports in a folder the sending helper refuses, so none reached GitHub. Framework 1.6.2 and later delivers them, and the first launch deletes the old saved reports without sending them. If you want a problem from an earlier version looked at, send it from **Options > Report Bugs** or open an issue. Automatic sending is best effort: a report that could not be sent may be tried again on a later launch, but it never holds up a newer one. The game's own sound-engine error at startup is not reported.

You can also [open an issue](https://github.com/jarlbrak/ftk-mod-framework/issues) with your platform, game build, framework version, mod versions, what you did, and what happened. Include only relevant log lines and remove personal paths and other private information. The loader's `BepInEx/LogOutput.log` inside the game folder can help identify the error. Do not upload game assemblies or saves containing private data.

## Thunderstore bootstrap

The initial Thunderstore submission was rejected as **Invalid submission**. A successful CI upload does not mean the package is approved or visible in the manager. Use the regular launcher downloads in the [installation guide](../installation/) while this is resolved. The following behavior describes the intended bootstrap flow, which has not passed live Windows acceptance.

The first Windows setup needs internet access to download the official current launcher and verify its checksum. If setup fails, the original game stays open; check the displayed error and retry. A fresh installation cannot work offline. After a launcher has opened successfully, the bootstrap retains a verified cached copy for offline use and failed updates. A damaged cache may require reconnecting and downloading again.

Use the Start menu or Steam entry created by setup for normal play. The durable entry and cache live under `%LOCALAPPDATA%/FTKModFramework/Bootstrap/`. Removing the Thunderstore package or its profile does not uninstall the managed framework or those files. Reopening the initial profile offers setup again. Other profile mods are not migrated. Use the normal framework uninstall instructions when removing the managed installation.

For this unverified Windows tester preview, [report](https://github.com/jarlbrak/ftk-mod-framework/issues) your Windows version, manager/version, whether setup reached the launcher, whether Play started the modded game, and the exact error. Remove personal information before attaching logs. Shortcut behavior after profile removal, offline launches, pins, and framework updates all need volunteer testing.
