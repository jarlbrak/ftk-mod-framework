---
title: Tweaks
description: Optional fixes, extra information and conveniences for the base game. Included in framework 1.7.1.
---
Tweaks are included in [framework 1.7.1](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/v1.7.1). Open **Mods > Tweaks** to choose optional fixes, information and conveniences. Read the [known limitations](#known-limitations) for control and gameplay scope.

Tweaks are small, optional changes to the original game that come with the framework itself. They fix defects, show information the game already tracks but never displays, and remove some interface friction. They never make the game easier or harder on purpose. The two fixes that do shift balance say so, and you can turn them off. No mod needs to be installed to use them.

## Find the Tweaks tab

At the title screen, open **Mods > Tweaks**. Rows are grouped into **Fixes**, **Information** and **Convenience**; use the page buttons to see them all. Each row shows the tweak's name and whether it is **On** or **Off**, with **(default)** when you have never changed it. Below that come a short explanation, who it affects, and any balance note.

Select a row to switch it. Your choice is saved immediately. **Reset all to defaults** returns every row to its default.

- **Fixes are on by default.**
- **Information and Convenience tweaks are off by default.** Turn on the ones you want.

## Only you, or shared rules

Each tweak has one of two scopes, shown on its row.

**Only you.** These change only what your own game shows, or how your own controls work. A change takes effect at once. In co-op, other players see nothing different unless they turn the same tweak on.

**Shared rules** change how a run plays, so they are settled for the whole run:

- **Decided at run start.** When a run starts, it takes your Shared rules settings at that moment. Changing a setting during a run affects your next run, not the current one. The tab says so on these rows.
- **Recorded in the save.** The run's Shared rules are written into your saves and autosaves.
- **Restored on resume.** Loading that save brings back the same rules, even if you have changed your settings since.
- **Older saves.** A save made before framework 1.6.0 follows your current settings when you load it. Its next save records them.
- **Solo and local play only for now.** In online co-op, every Shared rules tweak is off for everyone until a later release, whatever anyone's settings say.

## Every tweak

### Fixes

**Dungeon names in quest text**<br/>
Quest dialogue names the dungeon instead of showing the placeholder `STR_DungeonNoneDisplay`, which appears after the King's Maze.<br/>
*Only you. On by default.*

**Clear the Wet icon after combat**<br/>
Hides the Wet status icon on your character's panel once combat ends, as the game already does for the other combat statuses.<br/>
*Only you. On by default.*

**Clear the group shield icon after combat**<br/>
The same fix for the group shield icon.<br/>
*Only you. On by default.*

**Show Taunt and Petrified status icons**<br/>
In combat, your character's panel shows the game's own Taunt and Petrified icons, which the game includes but never displays. When a character is only Dazed, the stunned icon's tooltip says Dazed instead of Stunned.<br/>
*Only you. On by default.*

**Correct Perfect chances**<br/>
The Perfect chance on combat buttons now counts Shocked, Illuminated and Darkness, and Taunt's own accuracy. Only the displayed figure changes. The rolls themselves are exactly as before.<br/>
*Only you. On by default.*

**Unstick the skip-turn popup**<br/>
If the skip-turn popup is interrupted before it closes, it is closed and the turn carries on, as the game normally does after about five seconds.<br/>
*Only you. On by default.*

**Find Herb in dungeons**<br/>
In a dungeon, the party's Find Herb becomes available again after each full turn cycle, as it does after each new round outside. The rate stays at one herb per party per cycle.<br/>
*Shared rules. On by default.*<br/>
**Balance:** herbalists can find herbs each party turn in dungeons, not once per visit, and many more herbs in the Endless Dungeon.

**Keep poison countdowns on load**<br/>
A loaded run keeps each character's poison countdown, so poison wears off on the same end turn as if you had never saved. It never shortens poison.<br/>
*Shared rules. On by default.*<br/>
**Balance:** poison no longer lasts extra turns after loading a save.

### Information

**XP within the level**<br/>
Shows XP as progress through your current level, matching the XP bar, with your total in brackets.<br/>
*Only you. Off by default.*

**Poison turns left**<br/>
The poison status tooltip shows how many end turns remain until the poison wears off, for your own characters.<br/>
*Only you. Off by default.*

**Sell price in item details**<br/>
At a place that buys items, an item card in your inventory shows what the Sell button would offer. Equipped items show no price, because they must be unequipped before selling. Another player's items show no price.<br/>
*Only you. Off by default.*

**Mark encounters that vanish**<br/>
On the map, the hover card of a known encounter that disappears when you leave it or end your turn there says so. Unknown encounters show nothing new.<br/>
*Only you. Off by default.*

**Name the achievements House Rules disable**<br/>
When your House Rules count as easier, the rules summary on the setup, waiting room and resume screens adds that the three Defeat Vexor achievements and the win statistics won't be recorded. The game decides what counts as easier; this only says what it already does.<br/>
*Only you. Off by default.*

### Convenience

**Skip intro**<br/>
Skips the intro videos when the game starts, as if you pressed a key during each one.<br/>
*Only you. Off by default.*

**One-press inventory**<br/>
One press of the Inventory key opens your inventory, for a character using keyboard and mouse. Controllers keep the game's two-press belt.<br/>
*Only you. Off by default.*

**Refund movement focus**<br/>
Take back focus you spent on an extra move this turn, up to one point per move you have left, whenever you are standing still with no path chosen. See [how to refund focus](#refund-focus-spent-on-movement).<br/>
*Shared rules. Off by default.*

:::note[Settings file names]
Each tweak is also stored in `BepInEx/config/com.ftkmf.framework.cfg`, under `[Tweaks]`, as `Default`, `On` or `Off`. The tab is the easier way to change them. A **Session lifecycle probe** row appears only when the framework's developer self-tests are enabled. It changes nothing in the game.
:::

## Refund focus spent on movement

With **Refund movement focus** on, during your own turn on the map:

1. Spend focus for an extra move, as usual.
2. While you are standing still with no path chosen, the focus pips you can take back appear **faded** on your character's panel.
3. **Click a faded pip**, or press the refund key, to get one point of focus back. The extra move it bought is removed.

:::caution[Still on the 1.6.0 preview? Change the refund key]
Framework 1.6.1 and later fix this: the refund key is now **F**, and a saved Backspace changes to F when the game starts. In the 1.6.0 preview the refund key is **Backspace**, which is also the game's **End Turn** key. Pressing it ends your turn, and the focus stays spent. If you are still on 1.6.0, update to the current preview, set the key to **F** as described below, or click the faded pips instead.
:::

You can refund only focus spent on movement this turn, and at most one point for each move you have left. Choosing a path or walking hides the faded pips; if you stop with moves left, they come back. Refunds reset when your turn ends or combat or an encounter begins. Nothing carries over into a saved game.

The refund key is **F** from framework 1.6.1. To change it, close the game, open `BepInEx/config/com.ftkmf.framework.cfg`, and set `RefundMovementFocus` under `[TweakKeys]`, for example `RefundMovementFocus = F`. Set it to `None` to use clicks only. `Backspace` is always read as the old 1.6.0 default and changed to `F`. A key that one of the game's own controls already uses is ignored, and the log says so. The key does nothing while you are typing in chat.

**Controllers are not supported yet.** A character on a controller sees no faded pips.

## Diagnostics settings for troubleshooting

Three settings help when something goes wrong. They live under `[Diagnostics]` in `BepInEx/config/com.ftkmf.framework.cfg`, and the game reads them only at startup, so restart after changing one. None of them changes the game or opens a bug report on its own.

- **`StuckTurnWatchdog`** (on by default). When your turn cannot end and nothing on screen explains why, the framework writes one `STUCK-TURN` line to `BepInEx/LogOutput.log` describing the stall.
- **`SoftlockSignatures`** (on by default). When the game takes a path already known to cause softlocks, the framework writes one `SOFTLOCK-SIGNATURE` line to the log.
- **`LogLocalizationMisses`** (off by default). When on, each text key the game cannot find, such as a raw `STR_` name on screen, adds one line to the log naming the key. Turn it off again once you have the lines.

Bug report diagnostics include your tweak settings and which tweaks were actually on, including a run's Shared rules and where they came from. See [report a problem](../troubleshooting/#report-a-problem).

## Known limitations

- **Windows and Linux gameplay are unverified.** In-game checks so far were solo play in a macOS game copy, and they do not yet cover every tweak.
- **Online co-op.** Shared rules tweaks stay off in online co-op until a later release. Online co-op with tweaks has not been played, and local play has only been partly checked.
- **Controllers** are not supported for refunding focus or one-press inventory, and controller play with tweaks has not been checked.
- **Checks still open in the game:**
  - whether the original game, without the framework, opens a save that carries the Shared rules record;
  - a second Find Herb in the same dungeon;
  - the group shield icon clearing after combat;
  - Skip intro on Windows.

Automated game-free tests cover each tweak's rules. They do not prove behavior in a running game. If a tweak misbehaves, turn it off in the tab and [report it](../troubleshooting/#report-a-problem).
