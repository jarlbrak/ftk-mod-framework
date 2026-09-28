# Five-hero single-player mode

This is an opt-in Core feature under development. It is off by default and is not part of the
published 1.0.2 framework release. The current implementation targets offline single-player only.
It does not expand online or local multiplayer.

## What the option changes

The feature is designed to let offline single-player setup use one to five heroes after the
framework's setup, player-dummy, combat, and HUD expansion hooks pass preflight. HUD, portrait, and
item or gold recipient structures expand on existing instances and when later instances are created.
Item and gold transfer menus stop safely if native controls cannot be expanded. Gold amounts stay
within the sender's balance; gold not assigned to another hero remains with the sender. Each extra
hero adds actions and coverage, so larger parties make runs easier. The feature does not change the
three-enemy limit, enemy stats, encounter rules, turn rules, XP, or total gold rewards.

The current implementation is designed to check saved four- and five-hero rosters before native
loading starts. A roster whose summary and per-hero records do not agree is left unloaded. Existing
one-to-three-hero saves continue through the native resume path. If the option is turned off, a save
with four or five heroes is rejected with an instruction to enable the option again; the save is not
rewritten.

## Configuration

For a development build that contains this feature, set the following key in
`BepInEx/config/com.ftkmf.framework.cfg` before launching the game:

```ini
[Core]
EnableFiveHeroSinglePlayer = true
```

The default is `false`. Turn it off and restart to return to the native three-hero maximum. Re-enable
it to resume a four- or five-hero save. Start or resume the run through the offline single-player
route; the online single-player route is outside this feature's scope.

## Compatibility and evidence

The code was grounded against Steam game build `12395049` and
`Assembly-CSharp.dll` SHA256
`94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`. This is source-inspection
evidence, not a claim that gameplay is verified on that build.

The current checkout passes a Release build with seven existing `CS0649` warnings. An isolated,
runner-assisted live smoke on 2026-09-26 started the offline `DungeonCrawl` adventure with a requested
party size of five. The in-game state reported `singlePlayer=true`, `phase=overworld`, and five party
entries. The rendered overworld showed five hero portraits and HUD panels. The runner drove the title
and setup flow programmatically and readied the characters, so this confirms actual five-hero world
creation and visible HUD layout. It does not confirm interactive character selection or removal,
individual movement and actions, combat, inventory transfers, save/resume, or multiplayer behavior.
The feature is not ready for release until the remaining matrix is completed on a configured test
installation.

## Manual validation matrix

All rows are **Not run**. Run this matrix only with an isolated test installation and test saves.
Do not replace framework files or modify saves used by an active game session.

| Scenario | Expected result | Status |
| --- | --- | --- |
| Start offline single-player with the option off, for parties of one, two, and three | Native setup and runtime behavior; no extra slots | Not run |
| Start offline single-player with the option on, for parties of four and five | Every hero can be created, removed, selected, and started with mouse, keyboard, and controller input | Five-hero run reached the overworld with the agent runner; manual setup, party of four, and selection controls not run |
| View the four- and five-hero HUD and portraits | Every hero has a visible, distinct HUD and portrait action area at supported resolutions | Five-hero HUD and portraits visible in the runner-assisted overworld; party of four and portrait interaction not run |
| Move and take turns with four and five heroes | Each hero remains selectable and can move and complete a turn | Not run |
| Transfer inventory items and gold between heroes | Every current party member appears as a valid destination; unassigned gold stays with the sender and total allocations stay within the sender's balance | Not run |
| Enter combat with four and five heroes | Every hero can be targeted and take actions across each encounter layout; encounters still use at most three enemies | Not run |
| Save and resume four- and five-hero parties with the option on | All hero records and the party size are restored | Not run |
| Resume an expanded save with the option off or with missing or inconsistent hero records | Resume is stopped before native game-state loading; the save remains unchanged | Not run |
| Quick-resume while the option is on when party metadata cannot be read | Resume is stopped before native game-state loading | Not run |
| Start local multiplayer, online multiplayer, and online single-player after using five-hero setup | Expanded capacity and presentation are restored; multiplayer uses native behavior | Not run |
| Compare encounter rules and rewards with the native three-hero mode | Enemy count, stats, encounter rules, XP, and gold are unchanged by the feature | Not run |
