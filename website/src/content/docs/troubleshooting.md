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

Check that Paladin **1.3.0** is active with framework **1.2.0 or later compatible 1.x**. Censure comes from an equipped Paladin hammer. Smite comes from an equipped Paladin trinket, including the starting Tin Oath Token. They are not permanent class actions. Smite's stronger multiplier requires an active Censure Resistance reduction; an Armor reduction will not activate it.

## A mod is blocked as incompatible

Compare the installed framework, package version, platform, and game build with the listing. Update through the launcher and Mods menu where appropriate. Do not edit a manifest to bypass the compatibility check; get a compatible release instead.

## A save fails after changing mods

Restore the exact framework and mod set used by that run, or start a new adventure. Marketplace operations do not rewrite saves. If an update damaged a managed package, the menu can retain previous generations for rollback on a later launch.

## Report a problem

[Open an issue](https://github.com/jarlbrak/ftk-mod-framework/issues) with your platform, game build, framework version, mod versions, what you did, and what happened. Include only relevant log lines and a screenshot if useful. Remove personal paths and other private information before posting. The loader's `BepInEx/LogOutput.log` inside the game folder can help identify the error. Do not upload game assemblies or saves containing private data.
