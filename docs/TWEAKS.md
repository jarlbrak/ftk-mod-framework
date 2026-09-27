# Framework tweaks: contributor guide

Tweaks are small, toggleable corrections to the base game that the framework itself ships:
fixes for defects, information the game already tracks but does not show, and removal of UI
friction. Players switch them in the **Tweaks** tab of the Mods panel. Tweaks are framework
code. There is no `Content.*` API for them, so mods cannot register one.

This guide describes the foundation from [Spec #233](https://github.com/jarlbrak/ftk-mod-framework/issues/233)
under [epic #232](https://github.com/jarlbrak/ftk-mod-framework/issues/232), and how to add a tweak
on top of it. These descriptors exist today:

- **Skip intro**, the pilot.
- **Session lifecycle probe**, self-test only.
- **Dungeon names in quest text** (`fix.quest-dungeon-name`,
  [Spec #242](https://github.com/jarlbrak/ftk-mod-framework/issues/242) FR-1), the first Local Fix.
- **Clear the Wet icon after combat** (`fix.stale-wet-icon`, Spec #242 FR-4): the HUD hides the
  Wet status icon outside combat, as vanilla already does for the other combat statuses.
- **Correct Perfect chances** (`fix.perfect-chance`, Spec #242 FR-5): the Perfect figure on combat
  buttons counts Shocked, Illuminated and Darkness, and Taunt's own accuracy. Rolls are unchanged.
- **XP within the level** (`information.xp-in-level`,
  [Spec #243](https://github.com/jarlbrak/ftk-mod-framework/issues/243) FR-1).
- **Poison turns left** (`information.poison-turns`, Spec #243 FR-3): the poison status tooltip
  counts the end turns left for characters this client owns.
- **Sell price in item details** (`information.sell-price`, Spec #243 FR-4): at a POI that buys
  items, an inventory item card shows the price the Sell button would offer.
- **Mark encounters that vanish** (`information.vanishing-encounters`, Spec #243 FR-5): the
  overworld hover card of a known encounter that leaving or ending the turn removes says "Gone
  once you leave or end your turn here." Unknown encounters show nothing new.
- **Name the achievements House Rules disable** (`information.house-rules-achievements`,
  Spec #243 FR-6): when the chosen House Rules count as easier, the rules summary on the game
  setup, waiting room and resume screens adds that the three Defeat Vexor achievements and the
  win statistics won't be recorded. The game counts rules as easier when the chaos frequency
  value or the life pool is above the difficulty's, or inflation is below it. It does not count
  infinite lives as easier, and the line follows the game.

Live verification status is tracked on #233, #242 and #243.

`fix.quest-dungeon-name` postfixes `QuestLogicBase.SetMessageParams`. Quest message params are
cached: they are built on the first `GetMessageParams` call, rebuilt by
`GameLogic.SyncQuestDestinationRPC`, and never saved, so a toggle reaches a quest whose params are
built after it, including every quest after a save is loaded. Its patch comment in
`Core/QuestDungeonNamePatch.cs` records the ordering with the realm-name postfix on
`GetMessageParams(bool)` in `Core/Localization.cs`.

## Architecture

| Piece | Location | Role |
| --- | --- | --- |
| Descriptors and registry | `Core/Tweaks/TweakDescriptor.cs`, `TweakRegistry.cs` | Unity-free. Validates and stores descriptors, preferences, fault flags and the effective set |
| Facade | `Core/Tweaks/Tweaks.cs` | Process-wide `Tweaks.Registry`, `Tweaks.Session`, `IsOn`, `Fault` |
| Framework tweak list | `Core/Tweaks/FrameworkTweaks.cs` | Every framework descriptor, its handle and `RegisterAll` |
| Preference resolution | `Core/Tweaks/TweakPreferences.cs` | Pure Default/On/Off rules shared by the registry and the tab |
| Session lifecycle | `Core/Tweaks/TweakSessionLifecycle.cs` | Unity-free capture, lock and clear decisions, resume state, plus the probe trace |
| Session record | `Core/Tweaks/TweakSessionRecord.cs` | Unity-free codec and per-ID resolution for the `ftkmf.session` save record |
| Config binding | `Core/TweakConfigStore.cs` | The `[Tweaks]` section of the framework config |
| Lifecycle hooks | `Core/TweakSessionPatches.cs` | Harmony patches that drive capture, lock and clear |
| Tweaks tab | `Core/UI/ModsPanelTweaks.cs`, `ModsPanel.Tweaks.cs` | Row text, paging and layout |
| Diagnostics | `Core/Reporting/ReportingTweakSource.cs` | Copies registry state into bug-report metadata |

Everything under `Core/Tweaks/` stays free of `UnityEngine`, `Plugin` and game types, so the
game-free `Tests/Tweaks` project compiles it directly. The types live in the
`FTKModFramework.Core` namespace so any patch under `Core` can call `Tweaks.IsOn`.

### Descriptors and handles

A `TweakDescriptor` is immutable and carries:

- `Id`: the stable config key. Lowercase dot-separated segments of `a-z`, `0-9` and `-`, such
  as `convenience.skip-intro`.
- `Category`: `Fix`, `Information` or `Convenience`.
- `Scope`: `Local` (only this player) or `Session` (shared rules for a run).
- `Title` (required) and `Summary`, shown in the tab and in the config comment.
- `Evidence`: a maintainer reference to the installed-assembly methods and any public report.
  It is not shown to players.
- `BalanceNote`: optional, shown in the tab as "Balance: ...".
- An optional explicit default. Without one, a `Fix` defaults on and the other categories
  default off.

`Plugin.Awake` calls `FrameworkTweaks.RegisterAll` once, then `Registry.Initialize` with the
config store, and only then `PatchAll`, so every patch sees the player's choice from its first
call. `Register` returns an `int` handle, or `TweakRegistry.InvalidHandle` (-1) after logging
one warning. It rejects a malformed or duplicate ID (the first registration is kept), a
missing title, an unknown category or scope, a `Fix` with a balance note but no explicit
default, and any registration after initialization.

`Tweaks.IsOn(handle)` indexes a `bool` array and never allocates, so patches may call it on
per-frame or per-hit paths. It returns false before initialization, for unknown handles and
for `InvalidHandle`. `IsOn(string)` exists, but patches should hold the handle.

### Preferences

Each tweak has one entry in the `[Tweaks]` section of `BepInEx/config/com.ftkmf.framework.cfg`,
keyed by its ID, holding `Default`, `On` or `Off`.

- Only the exact values `On` and `Off` are choices. Anything else that BepInEx's `Enum.Parse`
  accepts, such as `7` or `On, Off`, resolves to `Default`.
- `Default` follows the descriptor's current default, so a changed default reaches players
  who never touched the row.
- Toggling flips the resolved value. If the result equals the default, `Default` is stored,
  so toggling twice returns a row to following its default. "Reset all to defaults" stores
  `Default` for every tweak. Both save the config immediately.
- Keys for IDs that no longer exist are never bound, so they stay in the file untouched.

If the registry cannot initialize, for example because the config cannot be read, every tweak
reports off, the game runs vanilla and the tab shows an error instead of rows.

### The effective set

`IsOn` reads the effective set, which the registry refreshes whenever its inputs change:

- A **Local** tweak is on when the player's resolved preference is on and it has not faulted.
  A change applies at once.
- A **Session** tweak is on only when the current Session set says so. The set is empty
  outside a run, so Session tweaks are off at the title screen whatever the preference says.

### Session capture, lock and clear

A run's Session set is captured during setup, locked when the run starts, and cleared when
the run ends. `TweakSessionLifecycle` owns the decisions. The patches in
`Core/TweakSessionPatches.cs` call it from these hooks, and the hot-reload coordinator calls it
directly:

| Step | Hook | Notes |
| --- | --- | --- |
| Capture | `GameLogic.CreateOnlineRoom` postfix | Solo and local play in a closed Photon room |
| Capture | `GameLogic.CreateOfflineRoom` postfix | Offline solo and local play |
| Capture | `StartGameFE.GameConfig.CreateOnlineRoom(bool, string, bool, string, DifficultyType, TimeOfDay)` postfix | Online co-op host; it never calls the `GameLogic` methods |
| Lock | `uiStartGame.EnterFahrulRPC` postfix | Runs once per run on every player |
| Clear | `GameLogic.RestartFadeOutFinish` prefix, last priority | The one exit every run takes; clears before its offline `PhotonNetwork.Disconnect` |
| Clear | `uiStartGame.InitializeSingleton` postfix | Every `FTK_main` load |
| Clear | `uiStartGame.OnLeftRoom`, `OnDisconnectedFromPhoton`, `OnPhotonJoinRoomFailed` postfixes | Can also fire while a run is on screen |
| Clear | `HotReloadCoordinator.Committed`, a direct call | A committed title-screen activation, as on a return to the title |

The mode comes from `GameLogic.m_GameMode`, because solo play also runs inside a closed Photon
room and room membership cannot tell it from co-op.

- **SinglePlayer and LocalMultiplayer** capture the player's preferences, minus Session
  tweaks that have faulted. The source label is `preferences`.
- **Multiplayer**, and any value this build does not recognize, captures an empty set with the
  source `pending`. Session tweaks stay off in online co-op until a host set can be supplied.
- `TweakRegistry.SetSessionSet(ids, label)` replaces a captured set with a complete one, such
  as a host's set or a save's record. It is only valid between capture and lock. The resume
  state below calls it; nothing supplies a host set yet.
- **Resume state (in progress, Spec #253).** `TweakSessionLifecycle.ArmResume`, `ReadRecord`
  and `RecordToWrite` hold a resumed run's `ftkmf.session` record from the save's load to the
  lock, apply it to a solo or local capture with the source `save`, `preferences-legacy` (no
  record) or `preferences-invalid` (unreadable record), and reapply it if the lock recaptures.
  Scene reload, run end, a title-screen activation and the lock disarm it; Photon callbacks
  do not. Online co-op keeps `pending`. No game hook calls these yet, so saves carry no record.
- A co-op client never passes a capture hook. At lock, the lifecycle recaptures from the run's
  own mode unless a capture for that mode already exists, so a solo capture can never reach a
  co-op run.
- After lock, captures are ignored and a preference change reaches only the next run. The tab
  says so on Session rows.
- A clear empties the set and keeps preferences and fault flags. A clear that ends a locked run
  from a Photon callback logs a warning, because the run may still be going; that points at a
  lifecycle bug. The run-end fade, a scene reload and a title-screen activation never warn.

HarmonyX 2.9, which ships with BepInEx here, still runs postfixes after a prefix vetoes the
original. The capture, lock and run-end hooks therefore read `__runOriginal` and ignore a
vetoed call, such as `HotReloadSessionEntry` blocking `GameLogic.CreateOnlineRoom`. The clear
postfixes ignore it on purpose: the room was left or the scene reloaded either way. Every hook
catches its own exceptions and logs them as a warning.

### Faults

A patch that catches an exception calls `Tweaks.Fault(handle, exception)`. The first fault per
tweak is logged as a warning; later ones are ignored.

- A **Local** tweak turns off for the rest of the process.
- A **Session** tweak does not change the current run, because that would split rules between
  players. It is left out of the next capture instead. A patch that faulted must still let
  vanilla run for that call.

Fault flags survive clears. The tab marks a faulted row, and bug-report diagnostics list it.

### Tweaks tab

The tab groups rows by category (Fixes, Information, Convenience), then title, and pages them
with the panel's page buttons. Each row shows "Title: On/Off", with "(default)" when it follows
its default, then the summary, a scope label, any balance note and any fault. The On/Off state
is the player's choice, not the effective value; for Session rows the scope label adds
"Changes apply to the next run." The text lives in `ModsPanelTweaks`, which `Tests/Tweaks`
covers.

### Diagnostics

Bug-report metadata carries two tweak sections, copied from the registry on the Unity thread.
`tweakPreferences` lists each ID with its stored choice (`default`, `on` or `off`).
`tweakEffective` lists each ID with the value `IsOn` returns, the faulted IDs, and the Session
state, mode and source. They are separate because they differ during a run and after a fault.
The field shape and bounds are in the [reporting contract](REPORTING-CONTRACT.md#runtime-foundation-progress).

## Patch contract

A tweak patch is an ordinary `[HarmonyPatch]` class that the startup `PatchAll` installs once.
Tweaks are never patched or unpatched at runtime; the patch consults the effective set on every
call. `Core/SkipIntroPatch.cs` is the reference shape.

1. **Check `Tweaks.IsOn(handle)` first.** When it is false, return without touching anything,
   so the vanilla path runs exactly as before.
2. **Decide before mutating.** Compute the whole decision, then apply it. A failure part-way
   must not leave half-changed state.
3. **Catch your own exceptions and call `Tweaks.Fault(handle, e)`.** Harmony skips the original
   method when a prefix throws, so a prefix must catch and return `true` to let vanilla run.
   Prefer a postfix when the change allows it.
4. **Let vanilla run.** Change inputs or results rather than replacing the original, unless the
   defect is in the original itself and the evidence says so.
5. **Mutate shared state only where the game does.** Session tweak patches run on every machine.
   A change to shared state belongs on the master client only, following the
   `PhotonNetwork.isMasterClient` checks in `Core/Campaign.cs`. Per-character state that the
   owning client advances in vanilla belongs on that owner. Other machines receive the result
   through the game's own sync.

Keep the decision in a Unity-free helper that takes the registry, the handle and the vanilla
inputs, as `FrameworkTweaks.SkipIntroAnyButton` does. The patch stays a thin wrapper, and the
off path can be tested without the game.

## Checklist for adding a tweak

- [ ] **A stable ID.** Follow the `<category>.<slug>` convention of `convenience.skip-intro`.
      The registry checks only the syntax. The ID is the config key: never rename it once
      shipped, or players lose their choice.
- [ ] **A category that passes the "spirit of the game" test.** A tweak corrects a defect
      (`Fix`), surfaces information the game already tracks (`Information`), or removes UI
      friction (`Convenience`). It never makes the game easier or harder: no changed AI
      targeting, bigger rewards, stronger abilities, or altered Discipline or Taunt. A fix that
      restores intended behavior but shifts difficulty gets a `BalanceNote` and a deliberate,
      explicit default; the registry rejects one without.
- [ ] **Scope and authority.** A `Local` tweak changes only presentation or input, and produces
      only actions vanilla could also produce. Skip intro, for example, reports the key press
      vanilla already accepts. Anything that changes rules, rolls, saves or networked state is
      `Session`, and it stays off in online co-op until the co-op contract ships. State where
      the change runs (every machine, master, or owner) in the descriptor's evidence or the
      patch comment.
- [ ] **Evidence.** Verify every game member, field and control-flow claim against the installed
      assembly with the [decompile lookup workflow](../.agents/skills/decompile-lookup/SKILL.md).
      Record the methods in `Evidence`, with a link to a public bug report if one exists.
      Never commit decompiled source or game assemblies.
- [ ] **Registration.** Add the descriptor, a handle property and one line in
      `FrameworkTweaks.RegisterAll`.
- [ ] **An off-path game-free test.** In `Tests/Tweaks`, show that the decision returns the
      vanilla result before initialization, when off, when faulted and for an invalid handle,
      and the intended result when on. Skip intro's test is the model.
- [ ] **Live evidence.** Use the [in-game smoke workflow](../.agents/skills/ingame-smoke/SKILL.md)
      to record the behavior with the tweak off (vanilla) and on. A `Fix` records the defect
      before and the correction after. A `Session` tweak also needs the probe logs below for the
      modes it claims. Online co-op evidence needs two live clients; solo evidence cannot stand
      in for it. A build or a game-free test does not prove in-game behavior.
- [ ] **Website impact.** Record which player guide or release note must list the tweak, per
      the root `AGENTS.md`.

## Session probe

With `[Diagnostics] RunSelfTests = true`, `RegisterAll` also registers
`probe.session-lifecycle`, a Session Convenience tweak that no patch reads, so it changes no
gameplay. It appears in the tab as **Session lifecycle probe** and is off by default. Turn it
on to see `probe=on` during a run; with it off, every check expects off. Self-test mode also
makes title-screen activation unavailable and turns off automatic bug reports.

While the probe is registered, the lifecycle writes these lines to `BepInEx/LogOutput.log`:

| Line | Level | When |
| --- | --- | --- |
| `SESSION-PROBE [session-lifecycle] capture via=<hook> state=Captured mode=<mode> source=<source> probe=<on\|off>` | Info | Every capture. A co-op client's shows `via=uiStartGame.EnterFahrulRPC (at run start)` |
| `SESSION-PROBE [session-lifecycle] resume-arm via=<hook> ...` and `record via=<hook> ...` | Info | A resume starting, and its save record read while armed |
| `SESSION-PROBE [session-lifecycle] lock via=<hook> state=Locked ...` | Info | Run start |
| `SELF-TEST PASS [session-lifecycle]: lock mode=<mode> source=<source> probe=<on\|off> expected=<on\|off>` | Info | Lock matched the expectation |
| `SESSION-PROBE [session-lifecycle] clear via=<hook> locked=<true\|false> state=None mode=none source=none probe=off` | Info | A clear that had something to clear |
| `SELF-TEST PASS [session-lifecycle]: clear via <hook> left the probe off` | Info | After each traced clear |
| `SELF-TEST FAIL [session-lifecycle]: lock ...` | Error | The probe's value at lock did not match |
| `SELF-TEST FAIL [session-lifecycle]: clear via <hook> while the run may continue=<True\|False>, probe on after clear=<True\|False>` | Error | A mid-run clear, or a probe left on |

`mode` is `SinglePlayer`, `Multiplayer` or `LocalMultiplayer`. At lock, the expected value is
the player's probe choice for the `preferences`, `preferences-legacy` and `preferences-invalid`
sources (off if the probe has faulted), off for `pending`, and the save record's state for
`save`, falling back to the player's choice when the record does not list the probe. A lock
from any other source is traced but not checked. A clear with nothing to
clear writes no line. Two warnings can accompany these lines: "cleared the Session set of a
run that may still be going" for a mid-run clear, and "a run started while the previous run's
Session set was still locked" for a missed clear.

A passing run shows capture at setup, lock with `SELF-TEST PASS` at run start, and clear with
`SELF-TEST PASS` on return to the title screen. The expected mode and source for each game mode
are in the Session capture rules above.

## Tests

```bash
dotnet run --project FTKModFramework/Tests/Tweaks/Tweaks.csproj -c Release
dotnet run --project FTKModFramework/Tests/TweaksConfig/TweaksConfig.csproj -c Release
```

`Tests/Tweaks` covers the registry, preferences, the mode matrix, lifecycle decisions and
hooks, faults, Skip intro, the quest dungeon name decision, the Wet icon fix, the Perfect chance
math and fix, XP within the level, Poison turns left, Sell price in item details, Mark
encounters that vanish, Name the achievements House Rules disable, the probe, the tab, and the
Session record codec, resolution and resume state.
`Tests/TweaksConfig` runs the `[Tweaks]` binding through BepInEx's real `ConfigFile`. Both run
in CI.
