# Paladin 2.0.1 and framework 1.8.0 validation

This release changes Guardian combat rules and package admission. Equipment models,
textures, renderer routes, acquisition, and native icon assets are unchanged from
Paladin 2.0.0. The [2.0.0 record](RELEASE-2.0.0-VALIDATION.md) retains its exact
appearance and acquisition coverage; it does not prove the new combat effects.

## Acceptance contract

- Mercy core: Guard heals immediately, then arms one focused class Smite healing
  bond. Only a landed qualifying attempt heals, but even a miss spends the bond.
  It lasts through the end of the next own turn. Other focused actions cannot
  trigger it. Matching armament raises the heal from 12% to 15% maximum ally HP.
- Verdict: the existing protection, retaliation and matching-armament ward remain.
- Censure core: passive physical scaling is 100%. An actual direct enemy hit
  reduced by Guard readies one +50% qualifying single-target physical attack.
  Guard alone, dodge, damage over time and zero mitigation cannot charge it.
  It is spent on attempt and expires at the end of the next own turn.
- Censure and Kingsfall use the stronger charge, never a product. Charge state
  cannot survive a role/weapon change, incapacity or encounter reset.
- Old profiles default to the existing behavior. New optional profile fields are
  admitted by both framework and helper; Paladin requires framework 1.8.0.

Outside the full Mercy core, the original Guardian focused-hit healing rule remains in effect, scaled by the current profile. Leaving Mercy clears its one-use bond; it does not remove the baseline class rule.

## Evidence status

[Framework 1.8.0](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/v1.8.0)
and [Paladin 2.0.1](https://github.com/jarlbrak/ftk-mod-framework/releases/tag/paladin-v2.0.1)
are published regular immutable releases. Game-free
validation passed the Release build,
all 45 framework test projects (including 231 Guardian checks and 31 installed
assembly metadata checks), strict package parsing, Paladin progression negatives,
installer, Go helper, launcher options/Unix fixtures and release-manifest tests.
The final published catalog validates with the shipped helper. The site built
from the hash-verified public projection and passed its 14-page desktop/mobile
suite, including role-text assertions, links, images, search and filters. These
checks do not establish native combat results.

The published package is `paladin-2.0.1-5400e2cd0c6d.zip`, SHA-256
`5400e2cd0c6d0822e2d6ca88bdf7529e026fbba91a7f97adca8909ec7384e464`.
Its inventory contains 306 files. All 304 model/texture/icon assets match the
published 2.0.0 archive exactly; only manifest and content JSON differ. Current
helper admission passes. The final framework source is `2a874d21e7936853ee2baf8a13e425fc4ee4d82c`.
Its DLL SHA-256 is
`d664fc3c6e1a001260d72c24afcacf7af3dc1c194f5fdd98a00953eed7ddeae7`;
macOS universal helper SHA-256 is
`d6c7890445313855cc3c1ee7153830d95fdc3c688cb0d089b900aef6432c3b6d`.
All 13 framework assets and all three Paladin assets were independently
downloaded from unauthenticated public release URLs and matched the reviewed
draft bytes. Both tags identify the exact source above, and the latest stable
release endpoint remains framework 1.8.0. The catalog/site changes merged in PR #292 at
`87c8f90883111a76285ecb0d94d5925bc9e1ad73`. The unauthenticated production
catalog serves the exact 2.0.1 descriptor and archive hash. Public Pages
deployment passed; the production Mods installation check is recorded below.
A build, a screenshot, and a successful upload are separate claims. No fresh
Windows, Linux/Proton, co-op, full campaign or statistical balance claim is made.

Mercy's healing bond is owned by the acting player's native attack authority.
The native damage-playback payload does not carry Focus spent; peers do not infer
it from UI or asynchronously synchronized Focus counters. The owner performs
healing through the existing synchronized health path. Censure uses the committed
native attack receipt on owner and playback paths. This design preserves the
existing authority boundary; it does not substitute for a new multiplayer trial.

## Native Mercy trial

R47 used the normal-HP saved Paladin party in a natural Maze encounter with three
Diseased Mosquitoes. Native input selected all combat actions and Focus.

- Guard raised the injured Blacksmith from 78 to 84 HP, the expected 6 HP.
- After an enemy hit left the ally at 80 HP, one focused Smite landed and raised
  the ally to 97 HP. The 17 HP heal is floor(114 maximum HP * 15%).
- Without another Guard, a second focused Smite landed while the ally was still
  injured at 92 HP. The ally remained at 92 HP.
- A fresh Guard bond survived until the next Paladin turn. Native Pass ended
  that turn and cleared the unused bond. A later focused Smite landed while
  the ally was at 93 HP and did not heal them.

These effects were observed with framework DLL SHA-256
`a2bd83b77300c28fbed596ef90a81e3d4907a6e0e4fbe69cf5307b7e01eb0e68`
from source `87bd1abaf25dbd14dd56685c10c7bd637ee35edb`, and the exact 2.0.1
package above. The final-source delta changes only five display/preview-matching
string literals in four Core files: the physical charge is called Vengeance,
and its tooltip specifies a Guard-reduced hit. Mercy healing, charge state,
profile fields and package bytes are unchanged. R47 therefore transfers only
its bounded Mercy mechanics evidence; it is not a new final-DLL execution claim.
Final-DLL Vengeance and Kingsfall observations are recorded below.

The native Guard and Smite hover captures were visually reviewed: 35% Mercy
Guard reduction, 21 magic damage in this loadout, readable compact descriptions,
and native white outlined combat glyphs. These captures establish presentation;
the health and action receipts above establish the healing outcomes.

## Native Censure trial

R48 runs the final DLL and exact package identified above. Setup loaded a protected
normal-HP overworld party, advanced XP through the native level-update path to
level 8, and granted/equipped the Censure set before combat. Those steps are
explicit diagnostic setup, not evidence of ordinary progression or acquisition.
The subsequent Mage Dungeon room and combat actions use native game behavior;
enemy HP, damage and RNG were not edited.

Guard applied 25% protection to the Scholar. A Goblin Shaman area hit recorded
4 damage to the unguarded Blacksmith and 3 to the guarded Scholar; the native
combat log reported Guard protection and the Paladin displayed Vengeance ready.
The charged Strike preview showed 48 physical damage with the readable title
"STRIKE + VENGEANCE". The actual attack recorded 48 damage against the Ghost of
Thomas Vinegar, reduced its 16 HP to zero, and consumed Vengeance. This establishes
positive mitigation, a charged impact and consumption, not merely a preview.
A later uncharged Strike displayed plain "STRIKE" with 32 physical damage in
the preview and dealt 24 damage to a
different enemy. Different targets and roll outcomes prevent treating 48/24 as
a controlled scaling ratio. The absence of Vengeance on that follow-up supports
one-use consumption. Isolated lifecycle and multiplier cases also have game-free coverage.

## Native Kingsfall overlap trial

R49 used the same final DLL and package, with Censure Head, Body and Foot armor
and Kingsfall. The three-piece role remained active while matching-armament
completion was correctly absent. Diagnostic loadout and native XP setup used the
same limits as R48; enemy stats and RNG were not edited.

After Guard actually reduced incoming damage, the live Guardian state contained
both Vengeance and Reckoning. Strike showed a single "STRIKE + RECKONING" title
and 54 physical damage against the displayed base 36, a single 1.5 multiplier.
The native attack dealt 39 damage to a Goblin Warrior with 4 Armor, reducing its
13 HP to zero. Both readiness lines disappeared immediately after the attempt.
This establishes simultaneous readiness, the capped preview, a native charged
impact and consumption of both charges. It does not measure campaign balance or
proc frequency. Verdict's profile and effects are unchanged; its earlier native
scope remains in the 2.0.0 record, without claiming a new full Verdict trial.

## Native item presentation

On the final DLL, Censure's active armor card and Mercy's inactive and fully
active armor cards were inspected in the native inventory. Both the main
item-camera panel and comparison panel fit the complete descriptions without
clipping. Mercy shows the immediate 6% heal, 12% core bond and 15% completed
armament bond. Censure shows 100% passive physical scaling and the Guard-reduced
hit charge. These are native item-camera/UI captures, not studio artwork or
new equipped-fit coverage.

## Session preservation

The native sessions used a muted, background, isolated game copy. R49 ended with
native Save and Exit; its owned game and keep-awake processes were stopped.
Combat and presentation continuations were retained separately, then the original
protected overworld save was restored byte-for-byte. The independent Thief
workspace, process, profile and content were not changed.

## Public install baseline

The shipped macOS 1.8.0 bundle was downloaded publicly and verified against its
reviewed hash. Its shipped installer upgraded a separate disposable game copy,
preserving its managed Paladin 2.0.0 generation unchanged. The installed DLL and
helper matched the public bundle. A fresh muted background title launch then
registered all 57 Paladin 2.0.0 entries with zero errors and warnings. This is a
bounded native backward-compatibility observation before the package update,
not a new campaign test.

Native Mods Browse fetched the production catalog online, offered 2.0.0 to 2.0.1,
and downloaded the public 78,876,996-byte archive with the pinned SHA-256 above.
Save for next launch retained the active 2.0.0 generation until native Quit and
apply. On restart, 2.0.1 became active, pending state cleared, and all 57 entries
registered with zero errors and warnings. A subsequent native disable/restart
registered zero entries; enable/restart restored all 57, again without errors or
warnings. Removal/restart left the package lock empty and registered zero
entries. Reinstall through the production Browse listing, followed by native
Save, Quit and restart, activated the exact enabled 2.0.1 package with all 57
entries and no errors or warnings. Each observation follows activation, not just
a queued request. No adventure was loaded in this public lifecycle test; combat
results reuse the exact DLL/package trials above.

The owned public game process was stopped. Test-created profile data was retained
separately. The original IronOak profile inventory was restored byte-for-byte;
the changed preference domain was restored through a domain-specific import,
with both the original on-disk SHA-256 and all 82 exported preference values
verified equal. The other native preference file was unchanged. No global
preferences-daemon reset was used, and the separate Thief process, profile and
preferences were left untouched. There is no pending smoke-profile snapshot or
owned game process.

## Public website verification

Pages workflow `36966629018` built and deployed merge commit `87c8f908`. The
public home, Paladin, installation, compatibility and release pages were checked
at 1440 px and 390 px widths. They identify Paladin 2.0.1 and framework 1.8.0;
Mercy and Censure card text and decoded artwork are readable, with no horizontal
overflow or browser JavaScript errors. The Paladin ZIP and four platform launcher
URLs return HTTP 200 with the expected sizes. The repository's latest release
continues to identify framework 1.8.0.

## Pipeline lessons

A new JSON capability must be added to both runtime loading and marketplace
admission before testing installation. The previous helper correctly rejected
2.0.1's unknown profile fields. The framework and helper must ship together, with
explicit package minimum compatibility and positive/negative admission tests.

- Use an injured ally for the second-heal control. A full-health ally can hide an
  incorrectly repeated healing effect behind the missing-HP cap.
- Choose an incoming hit that survives ordinary Armor/Resistance and integer
  rounding. Pressing Guard or showing a shield does not prove a mitigation-driven
  reward. A 25% reduction needs enough incoming damage to remove at least 1 HP.
- Retain a normal-HP overworld fixture before entering a dungeon. Gear setup and
  native combat observations should be distinct, reproducible steps.
- Test charged action titles with every relevant action name. Calling the charge
  Censure would produce confusing "Censure + Censure" feedback; Vengeance names
  the earned effect separately.
- Concurrent native trials need explicit executable, save, preferences and bridge
  isolation. A process-name-only guard blocked a separate Thief test; the private
  launcher was narrowed only after those boundaries were verified. Restore only
  the test's preference domain, without resetting the shared preferences daemon.
- Freeze source and artifact identity before trials. Reuse after a text-only
  correction requires an exact source comparison, and the changed presentation
  still needs its own native check. These are recommendations for the subsequent
  consolidated pipeline task; shared skills were not edited.

Website impact: the Paladin guide, item explanations, catalog projection and 51
item-media provenance records now use the verified public 2.0.1 archive. Framework
download references use 1.8.0. Thief content and media are unchanged. The final build, browser checks and public deployment verification passed.
