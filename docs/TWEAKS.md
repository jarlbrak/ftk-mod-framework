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
- **Clear the group shield icon after combat** (`fix.stale-group-shield-icon`,
  [Spec #260](https://github.com/jarlbrak/ftk-mod-framework/issues/260) FR-4, a #242 follow-up):
  the same fix for the group shield icon. Both icons share one `SetStatusIcons` postfix and one
  decision, and each keeps its own toggle and fault state.
- **Correct Perfect chances** (`fix.perfect-chance`, Spec #242 FR-5): the Perfect figure on combat
  buttons counts Shocked, Illuminated and Darkness, and Taunt's own accuracy. Rolls are unchanged.
- **XP within the level** (`information.xp-in-level`,
  [Spec #243](https://github.com/jarlbrak/ftk-mod-framework/issues/243) FR-1).
- **One-press inventory** (`convenience.one-press-inventory`, Spec #243 FR-2): one press of the
  Inventory key opens the inventory for a keyboard and mouse character; controllers keep the belt.
- **Poison turns left** (`information.poison-turns`, Spec #243 FR-3): the poison status tooltip
  counts the end turns left for characters this client owns.
- **Sell price in item details** (`information.sell-price`, Spec #243 FR-4): at a POI that buys
  items, an inventory item card shows the price the Sell button would offer, only where that
  button would be offered: not for equipped items, which must be unequipped first, and not for
  another player's character in multiplayer.
- **Mark encounters that vanish** (`information.vanishing-encounters`, Spec #243 FR-5): the
  overworld hover card of a known encounter that leaving or ending the turn removes says "Gone
  once you leave or end your turn here." Unknown encounters show nothing new.
- **Name the achievements House Rules disable** (`information.house-rules-achievements`,
  Spec #243 FR-6): when the chosen House Rules count as easier, the rules summary on the game
  setup, waiting room and resume screens adds that the three Defeat Vexor achievements and the
  win statistics won't be recorded. The game counts rules as easier when the chaos frequency
  value or the life pool is above the difficulty's, or inflation is below it. It does not count
  infinite lives as easier, and the line follows the game.
- **Find Herb in dungeons** (`fix.dungeon-find-herb`,
  [Spec #260](https://github.com/jarlbrak/ftk-mod-framework/issues/260) FR-3), the first Session
  Fix, on by default. Balance: "Herbalists can find herbs each party turn in dungeons, not once per
  visit; many more herbs in the Endless Dungeon." See [below](#find-herb-in-dungeons).

Live verification status is tracked on #233, #242, #243 and #260.

`fix.quest-dungeon-name` postfixes `QuestLogicBase.SetMessageParams`. Quest message params are
cached: they are built on the first `GetMessageParams` call, rebuilt by
`GameLogic.SyncQuestDestinationRPC`, and never saved, so a toggle reaches a quest whose params are
built after it, including every quest after a save is loaded. Its patch comment in
`Core/QuestDungeonNamePatch.cs` records the ordering with the realm-name postfix on
`GetMessageParams(bool)` in `Core/Localization.cs`.

### Find Herb in dungeons

`CharacterSkills.FindHerb` refuses while the party-wide `GameFlow.m_FindHerbRoundCoolDown` is set.
A find sets it, and only `GameEventManager.UpdateTurnMC` clears it, on a new round. In a dungeon
`GameFlowMC.EndTurn` still rotates the turn but suppresses the round, and `BeginTurnFinished2` skips
`GameLogic.UpdateTurn` while `FTKHub.AnyPlayersInDungeon()`, so the cooldown stays set for the rest
of the visit, and for the rest of the run in the Endless Dungeon.

`Core/DungeonFindHerbPatch.cs` patches `GameFlowMC.EndTurn`, a PunRPC sent to the master:

- **Prefix.** Before vanilla changes anything, it repeats vanilla's index computation: on the first
  end turn after `m_JustEnterDungeon` the current turn becomes the character whose `TurnIndex` is
  the entrant's minus one; the next turn is the `m_IngamePlayerIDs` list index after the current
  turn's, wrapping. It records `(next == m_StartTurnIndex || _advanceRound) &&
  AnyPlayersInDungeon()` in `__state`. A cleared finite dungeon with players still inside counts,
  because `BeginTurnFinished2` suppresses the round there too. The decision is the Unity-free
  `DungeonHerbCycle` in `Core/Tweaks/DungeonHerbCycle.cs`.
- **Postfix.** When vanilla ran, the prefix recorded a cycle, this machine is the master and the
  cooldown is set, it calls `GameFlow.UpdateFindHerbRoundCoolDown(false)`, whose `SyncMember`
  reaches the other machines. It logs one line per clear and a `herb-clear` probe trace. It never
  reads `m_PlayerCurrentTurn`, because a turn can end again inside `BeginTurn`.

The rotation covers every in-game player, so a dungeon cycle clear also frees herbalists still in
the overworld. The rate stays at one herb per party per cycle, the same as an overworld round.
Overworld rounds are unchanged, and leaving the dungeon hands the clear back to vanilla's round.
Like every Session tweak, it is off in online co-op until the co-op contract ships.

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
| Session record hooks | `Core/TweakSessionRecordPatches.cs` | The save transpiler, the load prefix and the resume arm for the `ftkmf.session` record |
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
| Resume arm | `uiStartGame.OnResumeGame` prefix, last priority | Arms the [save-bound record](#save-bound-session-record) read |
| Lock | `uiStartGame.EnterFahrulRPC` postfix | Runs once per run on every player; also disarms a resume |
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
  as a host's set or a save's record. It is only valid between capture and lock. A resumed
  solo or local run calls it with its save's record, as described in
  [the save-bound Session record](#save-bound-session-record); nothing supplies a host set yet.
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

### Save-bound Session record

A run keeps the Session rules it started with across saves
([Spec #253](https://github.com/jarlbrak/ftk-mod-framework/issues/253)). The save carries the
run's set, and a resume restores it even if the player has changed a Session preference since.

**Format.** The record is the key `ftkmf.session` in the `GameFlow` state dictionary, which
the game stores as `GameStatesSerialize.m_GameFlowStates` in every manual save and autosave.
The key contains a dot, so it cannot collide with a serialized C# field name. The value is one
string, so vanilla's FullSerializer never meets a framework type:

```text
v1:+fix.dungeon-find-herb,-probe.session-lifecycle
```

- `v1:` is the format version.
- Then every registered Session tweak as `+id` (on) or `-id` (off), comma-separated and in
  ordinal ID order. Local tweaks are never recorded.
- At most 1024 characters. A run whose record would be longer writes none and logs one warning.

`TweakSessionRecord.Decode` treats a missing key as **absent**. Anything else is either a
complete valid record or **invalid** as a whole: a value over the limit, an unknown or missing
version prefix, `v1:` with no entries, an entry without `+` or `-`, an ID that fails
`TweakRegistry.IsValidId`, a duplicate ID, or a value that is not a string. A partly readable
record is never applied, because dropping one entry would silently change the run's rules.

**Resolution.** For each registered Session tweak, a valid record's state wins, faults
included, so the run keeps the rules it was saved with. A tweak the record does not list, such
as one added by a later framework version, resolves as a solo capture does: the player's
preference, minus faulted tweaks. Recorded IDs this build does not register, and IDs it
registers as Local, are ignored and logged once each; they are not carried forward.

**No record.** A save with no record predates this feature, or vanilla re-saved it. It follows
the player's current settings, the decision on epic #232 open question 3. The source is
`preferences-legacy`, and the next save writes a full record. An invalid record also follows
the current settings, with the source `preferences-invalid` and one warning.

**Arm, read and apply.** For a solo or local resume:

1. `uiStartGame.OnResumeGame` arms the resume state (prefix, last priority). It reads
   `__runOriginal`, because `HotReloadSessionEntry` and `SaveNamespace.GuardResume` can veto
   the resume and HarmonyX still runs later prefixes after a veto. Arming forgets any record
   from an earlier resume.
2. Room creation captures from preferences as usual: `CreateOfflineRoom`, or on the
   `m_UseOnlineSinglePlayer` route `ConnectToDefaultServer`, `_OnJoinedLobby` and then
   `GameLogic.CreateOnlineRoom`.
3. The host's real load runs `uiStartGame.LoadGame`, `GameSerialize.Load`,
   `OnMapDeserializeFinished`, `GameStatesSerialize.Deserialize` and
   `GameFlow.StateDataDeserialize`. A prefix on `FTKNetworkObject.StateDataDeserialize(string,
   bool)` checks `__instance is GameFlow` first. Only while a resume is armed and the run is not
   locked does it parse the state string with the same FullSerializer helper vanilla uses, then
   pass the dictionary to `TweakSessionLifecycle.ReadState`. The record is kept and applied at
   once through `SetSessionSet` with the source `save`, `preferences-legacy` or
   `preferences-invalid`, and the lifecycle logs where each tweak's state came from.
4. In solo offline an asynchronous disconnect callback can clear the capture before the run
   starts. Photon callbacks never disarm, so the lock's recapture applies the record again.
5. `uiStartGame.EnterFahrulRPC` locks the run and disarms. A scene reload, the run end and a
   title-screen activation also disarm.

The prefix is gated because the same deserializer is also a PunRPC (`SendGameData`) and runs
again for resumed online clients in `ClientDeserializeFinalRPC`, after their lock. An unarmed
or locked read parses nothing and changes nothing. The extra parse happens at most once per
resume; the `GameFlow` state is a few integers, a flag and the `Rules2` parameters, and vanilla
already parses it an extra time in `GameSerialize.Load` for saves that carry the old difficulty
field. A parse failure is
logged once and leaves vanilla's load untouched.

An online co-op host that resumes a save does not apply the record and keeps `pending`,
because applying it alone would give the host different rules from its clients. Publishing a
resumed host's set to clients belongs to the co-op contract (Spec B).

**Write.** A transpiler on `FTKNetworkObject.StateDataSerialize(bool)` inserts
`ldarg.0; call TweakSessionRecordHooks.Decorate` just before the method's only
`SerializationHelpers.SerializeToContent<Dictionary<string, object>, FullSerializerSerializer>`
call. `Decorate` returns the dictionary unchanged unless the object is `GameFlow` and
`TweakSessionLifecycle.WriteRecord` has a value, which it has only for a locked run with
Session tweaks. It then adds the one key. It catches its own exceptions and returns the
dictionary unchanged.

- **Why a transpiler.** A postfix only sees the finished string. Adding the key there means
  parsing and re-serializing the whole state, which round-trips object-typed values such as
  `GameFlow.Rules2` through a path nobody has proven faithful. The transpiler adds the key to
  the dictionary vanilla already built, so with nothing to add the output is vanilla's byte for
  byte.
- **Drift.** The transpiler inserts only when exactly one matching call exists, the rule
  `SaveNamespace.RewritePaths` asserts. Otherwise it returns the original IL and logs one
  warning, and saves carry no record. It never throws, since that would abort `PatchAll`.
- `SendGameData` serializes through the same method, so a locked run's `GameFlow` data sent
  that way also carries the key. Vanilla ignores it.

**Vanilla compatibility.** Vanilla reads only keys named by its serialized fields, so it ignores
the record on load and drops it on its next save. That a vanilla game loads such a save is
live gate L8. The record does not change `SaveSetIdentity` or the save namespaces.

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
state, mode and source. The source is `preferences` or `pending` for a new run, and `save`,
`preferences-legacy` or `preferences-invalid` for a resumed solo or local run. They are
separate because they differ during a run and after a fault.
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
| `SESSION-PROBE [session-lifecycle] resume-arm via=uiStartGame.OnResumeGame ...` and `record via=GameFlow.StateDataDeserialize ...` | Info | A resume starting, and its save record read while armed |
| `SESSION-PROBE [session-lifecycle] lock via=<hook> state=Locked ...` | Info | Run start |
| `SELF-TEST PASS [session-lifecycle]: lock mode=<mode> source=<source> probe=<on\|off> expected=<on\|off>` | Info | Lock matched the expectation |
| `SESSION-PROBE [session-lifecycle] clear via=<hook> locked=<true\|false> state=None mode=none source=none probe=off` | Info | A clear that had something to clear |
| `SESSION-PROBE [session-lifecycle] herb-clear via=GameFlowMC.EndTurn state=Locked ...` | Info | `fix.dungeon-find-herb` cleared the Find Herb cooldown, beside its own log line |
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
dotnet run --project FTKModFramework/Tests/SessionRecordHooks/SessionRecordHooks.csproj -c Release
```

`Tests/Tweaks` covers the registry, preferences, the mode matrix, lifecycle decisions and
hooks, faults, Skip intro, the quest dungeon name decision, the shared stale combat icon decision
for the Wet and group shield icons, the Perfect chance math and fix, XP within the level, One-press inventory, Poison turns left, Sell price in item
details, Mark encounters that vanish, Name the achievements House Rules disable, the dungeon Find
Herb cycle and clear decisions, the probe, the
tab, and the Session record codec, resolution, resume state, the state dictionary read and
write, and the single-match rule of the save transpiler.
`Tests/TweaksConfig` runs the `[Tweaks]` binding through BepInEx's real `ConfigFile`.
`Tests/SessionRecordHooks` compiles `Core/TweakSessionRecordPatches.cs` against stand-ins for
the game types it names. It runs the save transpiler over a stand-in of vanilla's tail sequence,
compiles and executes the result, and drives the load prefix and the resume arm. The real IL
shape comes from decompiling the installed assembly; FullSerializer's handling of the string
value (live gate L9) and the round trip in the game are covered only by the live checks on #253.
All three run in CI.
