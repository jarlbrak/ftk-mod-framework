# Reporting menu proof

Scope: [Spec A1 discovery](../REPORTING-FEASIBILITY.md) for issue #162.
Test tooling: [reporting proof](../../tools/reporting-proof/README.md).
This record does not qualify the production reporting feature. Trials 1 through 7
used the earlier extra-entry design. The user subsequently approved replacing the
existing native Report Bugs action; new trials must identify that changed route.

## Environment and isolation

- Date: 2026-09-24 UTC.
- Repository baseline: `40529301`, with the uncommitted reporting proof sources.
- Host: macOS 26.6.2 (25G83), arm64; game uses its installed macOS binary.
- Steam installation build ID: `12395049`; Unity `2017.2.2p2`.
- Game `Assembly-CSharp.dll` SHA-256:
  `94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`.
- Framework version: 1.0.2; DLL SHA-256:
  `d25c529269cd5126885ecc2b9334a8c63dc9f96fa7ff4aec1171ee3cbec9b272`.
- Window: 1280 x 800 game framebuffer.

A separate game was already running. The proof copied the installed app and
BepInEx core into a fresh disposable directory, without copying plugins, saves,
configuration, or player preferences. Both the application bundle identifier and
serialized Unity company/product were changed only in the copy. Source asset and
assembly hashes were unchanged after preparation. Runtime readiness confirmed the
new bundle-specific persistent-data path; the open `player.db` was inside that
profile rather than the ordinary game's profile.

The proof uses its own file-command directory and session nonce, dedicated logs,
and a separately verified bridge listener. No command was sent to the preexisting
game. Process cleanup verifies the owned executable path before signalling its PID.
The test-only preloader replaces Steam initialization and relaunch wrappers in
memory before game assembly loading. It never writes a patched game assembly to
disk. These trials cannot establish Steam-connected behavior.

## Trial 1: isolated startup

The preloader receipt matched the runtime session. Runtime counters observed one
blocked `SteamAPI.Init` call and one blocked `RestartAppIfNecessary` call.
The game opened its splash screen and a new isolated profile database.

Startup then failed: `IOSQL.MigrationAssistant.PostInit` called
`StatsAchievements.SteamStatsAndAchievements.GetStatValue`, which reached
`SteamUserStats.GetStat` despite Steam being uninitialized. The resulting
exception interrupted platform initialization and left Rewired unavailable.
The title menu was not reached. The owned test process was stopped; the original
game process remained running with its original start time.

This is an isolation pass and a menu-proof failure, not reporting feasibility
evidence. Any offline statistics shim used for a later trial must be listed here
and must not be presented as ordinary Steam startup coverage.

## Trial 2: title entry and focus failure

A test-only shim was added for the two unguarded high-level Steam statistics
reads: `SteamStatsAndAchievements.GetStatValue(string, out int)` and
`GetAchievementValue(string, out bool)`. Both return unavailable with zero/false
outputs. Steam initialization remains blocked. The title was reached after 15
intercepted stat reads and three intercepted achievement reads. This does not
test profile migration, platform statistics, achievements, or cloud behavior.

The native title Options action opened its scene-owned menu. The separate
framework reporting action was visible below the native report action and release
footer at 1280 x 800. Its native-button invocation failed the focus ownership
check. Initial rollback appeared to return to Options, but closing Options then
restored the hidden proof panel instead of the title. Therefore rollback and title
open/cancel/reopen failed; apparent parent focus immediately after failure was
insufficient evidence. The owned process was stopped before further testing.

## Trial 3: modal correction and fixture limitation

Copying the native Audio submenu's modal and close-on-lost-focus flags corrected
the title sequence. Open, Back, reopen, cancel, and closing Options restored the
title focus with time scale 1.0. The next layout revision centered the proof's Back
button; it had inherited unsuitable anchors from the menu clone.

A bridge-created solo run kept time scale 0 through reporting open/back/reopen
and restored 1.1 when Options closed. However, its input restore target was still
the character-creation UI. Inspection of `StartRunDriver` found that its completion
condition precedes stable input ownership and a native camera coroutine can still
refocus character creation. This trial is pause evidence only; it does not prove
usable gameplay input after reporting. A subsequent trial must use the normal
creation controls and wait for their native transitions.

## Trials 4 and 5: final title proof and normal setup

The final live-tested proof DLL SHA-256 is
`77b5931ad041595bf0febd886e2aa8461bc020f1727b93c2e49813f8022bfc92`.
The preloader DLL SHA-256 is
`005c6e8c2e0803be027bf836b4ab23050ed45fd64dbbe50488025a4baa6a4854`.
The final build includes centered Back-button anchors and read-only setup
diagnostics. Its title Options, reporting open, Back, reopen, cancel, and root
close sequence passed again. Both the native Audio submenu and proof child
reported modal=true. Closing Options returned owned focus to MainScreen.

Normal New Game / Create Game initially appeared stalled before character controls
became visible. The final diagnostic trial showed map generation complete, three
of three expected character UIs present, an active enabled camera animator at
speed 1, and its normalized time progressing. It subsequently reached `complete`
and revealed the controls without a bypass. A short observation of hidden controls
was not enough to establish a hang. The normal Solo Adventure / Dungeon Crawl
creation controls were then used to enter a three-hero run.

After the story messages and native turn-start tutorial were dismissed, the
baseline was owned focus `Player 1`, time scale 1.1, and no Options or reporting
overlay. The following sequence passed on the final binary:

| Action | Owned focus | Paused / time scale | Reporting visible |
| --- | --- | --- | --- |
| Open native gameplay Options | MainWindow | true / 0 | false |
| Open reporting | FrameworkReportingProofPanel | true / 0 | true |
| Native cancel to Options | MainWindow | true / 0 | false |
| Reopen reporting | FrameworkReportingProofPanel | true / 0 | true |
| Native cancel to Options again | MainWindow | true / 0 | false |
| Close Options | Player 1 | false / 1.1 | false |

The post-close focus and time scale exactly matched the settled pre-open baseline.
The blocker cleared, and the native overworld was visible. A subsequent Escape
key sent to the uniquely identified proof application reopened Options. This is
one keyboard recovery observation, not complete keyboard/controller qualification.
Mouse attempts did not establish a reliable click route and are not counted as
passes. The final sequences used exact-path native-button calls for entry and
the proof's `cancel` command, which calls `FTKInput.Close`, for return. The earlier
title trial also exercised the cloned Back-button listener. The Escape observation
used targeted keyboard input rather than a proof command.

The final log still contains the intentional Steam initialization failure and an
`AkInitializer.OnApplicationFocus` null-reference exception during startup.
The isolated copy also reports its absent marketplace helper. These trials do
not claim an error-free startup or marketplace coverage. The final owned process
was stopped; the preexisting game's process identity and start time matched the
pre-test record. No production deployment or issue submission occurred.

## Trial 6: controlled opening failures

The proof added guarded faults immediately after setting the Options blocker and
immediately after native child focus transfer. The tested DLL SHA-256 was
`0c4e5c80616dba66741b62c018b3b7a6ad503f71911a767ce62ec53cb3ad2faa`.
Framework and preloader hashes were unchanged. This trial disabled the optional
Agent bridge and used only the isolated copy's session-scoped file commands.

At title, the after-blocker fault restored Options ownership, selection, saved
return target, modal owner, blocker and time scale. Two normal open/cancel cycles
and closing Options returned to the exact title baseline.

The after-focus fault was observed and its injection disarmed, but the synchronous
rollback assertion **failed**: Options regained owned focus and preserved its saved title target,
modal owner, blocker and time scale, while the previously selected close button
became null. The test stopped at this failure rather than treating visible Options
as successful restoration. Subsequent source inspection found that
`FTKInputFocus.SetFocus` clears current selection and stages the requested one;
its native updates defer selection while first-frame/navigation work completes.
The immediate null therefore does not establish a broken rollback. A bounded
settled observation is required before changing restoration behavior. The owned process was stopped and the other game's
PID, start time and executable still matched the pre-test record.

These are synchronous fault probes, not scene-destruction or arbitrary exception
coverage. A later correction must repeat both rollback and subsequent ordinary
open/cancel/root-close before receiving a pass.

## Trial 7: settled rollback and targeted title keyboard route

The timing-corrected proof DLL SHA-256 was
`c0195c7836c10fab3797ca0c9f0a356599e7131b28eec2bba61886949319ae57`.
The correction changed the probe, not Open's restoration behavior. The command
retains immediate evidence and observes ordinary native frames for at most one
unscaled second or 120 frames, with other file commands held until completion.
The optional Agent bridge remained disabled.

Both faults passed from settled Options with the close button selected:

| Fault point | Immediate selected control | Settled selection | Observation |
| --- | --- | --- | --- |
| After blocker activation | Original close button | Original close button | 0 yielded frames, about 4 ms |
| After child focus transfer | Null | Original close button | 2 yielded frames, about 22 ms |

Each pass also preserved the parent saved-state reference and inner return target,
first modal, owned Options focus, cleared blocker, hidden/unfocused child and time
scale. After each fault, two ordinary reporting open/cancel cycles and root close
returned to the exact title focus/time baseline. The after-focus result qualifies
eventual native restoration in this trial, not synchronous selection restoration
or a general timing budget.

A targeted desktop keyboard trial then used the uniquely identified proof app.
Individual Down keys with observed selection reached title Options; Return opened
it. Up followed the verified wrap link from Back to the framework reporting entry,
Return opened the child, Escape returned to Options with that same reporting entry
selected, Return reopened, Return activated the child's selected Back button, and
Escape closed Options. Final owned title focus selected the original Options
button, time scale was 1.0, and the reporting panel/blocker were absent. Read-only
file state checks corroborated the screenshots between keyboard actions; entry,
confirm and cancel in this sequence were not native-button command invocations.

An earlier burst of Down keys did not produce one navigation step per key and
opened the native Lore Store; Escape returned safely before the individually
observed sequence. Therefore no key-repeat-rate claim is made. A coordinate-click
attempt returned the automation error `noWindowsAvailable` while the isolated
game stayed running; it supplies no mouse evidence. These are targeted automated
keyboard events, not a human keyboard/controller or text-entry trial.

The trial still logged the known startup `AkInitializer.OnApplicationFocus`
null-reference and intentional unavailable Steam initialization; no clean-startup
claim is made. The owned process was stopped after the final state check. The
other game's PID, start time and executable matched the pre-test record. No
production installation, browser submission, or other game was driven. Independent
architecture review found no blocking findings in the bounded probe or this
evidence update.

## Trial 8: browser request with inconclusive focus telemetry

The extra-entry proof DLL SHA-256 was
`04bd216c279b72970ee41fbc4b92c5291d36239ef04ca9ca0c2e2eef2b3627e4`.
Its fixed `Application.OpenURL` request opened the public repository issue list
in Microsoft Edge 153.0.4234.48. The exact page was observed in the external browser;
no query payload, form, upload or submission was used. The request result said only
`requested`. The proof panel remained open with its original selection and title
time scale.

However, callback counters remained at zero losses and one gain after browser
interaction. This trial does not prove that Unity observed focus loss or return.
Managed source inspection found no focus-callback interception in BaseUnityPlugin
or the framework; the game audio handler alone does not explain the observation.
The next instrumented state includes a direct `Application.isFocused` read alongside
callback history. The owned process and newly created browser tab were closed.
The user's subsequent native-button takeover decision superseded this entry design
before repeating the browser trial.

## Trial 9: native Report Bugs takeover

After the user's entry-ownership decision, the tested DLL SHA-256 was
`48b1d580489af3236646f08f992f4f7936b9c88b6c9bfce0dff0911b606854c3`.
Framework/preloader hashes and isolation were unchanged; the Agent bridge was off.
The native button retained its caption, scene position, serialized event and
navigation. No extra reporting entry was created. A prefix intercepted
`uiOptionsMain.OnReportBugs` before its original close/form behavior and opened the
proof child. Separate exact prefixes blocked the inspected legacy capture and
form entry methods; no unrelated callers were redirected to the new child.

The first 40-second title wait expired at the splash screen. After targeted
keyboard activation the title appeared; no startup defect is inferred from that
bounded timeout. The following checks then passed:

- Exactly one active native Report Bugs entry, with no old extra-entry clone.
- Ten title open/close cycles, alternating the child's actual Back listener and
  native cancel. The native-handler suppression count increased exactly once per
  entry, never on Back. Native navigation links were identical before/after.
- Options remained open while the child opened/closed, and root close returned
  to owned title focus at time scale 1.0.
- Both controlled faults passed again. The after-blocker check yielded zero
  frames; after-focus selection settled after two native frames. Each was followed
  by two normal open/cancel cycles and exact root restoration.
- Targeted keyboard Up selected native Report Bugs from Options Back; Return
  opened the child, Return activated its Back, and Escape closed Options. The
  parent restored selection to native Report Bugs before root close. Screenshots
  corroborated the original menu layout and absence of the extra entry.

The downstream legacy capture/form suppression counters stayed zero during these
button trials: the intercepted Options body never proceeded into those methods.
The additional version/RPC/direct-form suppression routes are source-backed
patch coverage, not live RPC/network or arbitrary third-party coverage. No legacy
form upload or callback was exercised.

The fixed read-only browser request again created the correct public issue-list
tab in Edge. Direct Unity focus state was now recorded alongside callbacks. One
pre-request observation was already unfocused; after explicit keyboard activation,
a second request still did not establish a new Unity loss/gain cycle. Panel and
selection state remained intact, but browser-return qualification remains open.
Both newly created browser tabs were closed without uploads or submissions.

The known audio startup null-reference and intentional unavailable Steam startup
remained in the log. The owned game was stopped after final title-state verification;
the other game's PID, start time and executable were unchanged. Active-solo entry
must be repeated under this takeover hash; earlier solo passes used the extra entry.

## Trial 10: native-styled production editor with protocol 3 input

This run followed the fast-forward to `origin/master` at `5561401f`. It used the
isolated reporting profile and its exact-root Steam guard, with the framework's
integrated reporting mode enabled. Framework build and deployed SHA-256 matched:
`f9081d7fb51c428a34b3f1a37d95ff7a5e14d088628f5fb6a543426355a54dd4`.
The loaded framework MVID was
`20614e1e-6d1e-47cd-8e07-bc2a6c5f295a`. The updated Agent bridge reported protocol
3 on its dedicated isolated port.

Native input dismissed the startup notice, opened title Options, and activated
the existing Report Bugs button. The production `ReportingMenu` and `ReportingPanel`
opened with owned child focus; the native Options owner remained visible, tracking
was available, and report metadata had been captured. Metadata sharing toggled on
and back off. Native text input filled the summary, reproduction, and expected vs
actual fields, then Review produced the GitHub-ready screen and all seven
per-field copy controls. The Continue button was visible but was not activated.
No copy, local save, draft delete, browser opening, upload, or issue submission
occurred. The existing synthetic saved draft in this profile was not deleted.

Visual inspection against the Mods screen found the first reporter layout too
plain and its review actions overlapped the copy row. The final build uses the same
native panel, title plaque, parchment, buttons, and text styling as Mods, with
separated review and copy rows. The final review capture is retained only in the
ignored disposable tree at
`scratch/reporting-game/proof-output/final-review-r4-screen.png`.
Native Back returned focus to Options; protocol 3 Escape closed Options and
restored title focus at time scale 1.0. The owned process exited through the
guarded proof command, and the dedicated listener closed. This title-only trial
does not qualify gameplay pause, restart offers, browser return, controller input,
or other platforms.

## Trial 11: active-session entry and compact report flow

The final editor build was tested in the same isolated app identity with the exact-root Steam guard
and integrated reporting mode. Build and deployed framework SHA-256 matched:
`328aa2c17412eace04f27cd8d6a4fe34430c20cd2169c57154a34d65befb0ca9`. The loaded bridge reported
MVID `910e7cb6-d8a2-412a-83f6-d3156068a621` and protocol 3 on its dedicated loopback port.

Native input created a disposable solo run and opened the in-game Options gear. The existing
Report Bugs button was active in that menu and opened the same framework editor. Installed assembly
inspection confirms `uiOptionsMenu.Show` activates that button in its in-game branch and calls
`PauseGame(true)`; the single-player path sets the time scale to zero. This trial exercised the
active-session button and editor, without ending or saving the disposable run.

The editor now keeps metadata preview pages collapsed behind **View metadata** and changes the
consent button to **Include metadata: yes/no**. The full-report copy action appears only after
review; when a saved draft exists, its available Resume and Delete actions appear under
**Draft options**. A restart offer was also observed at title after an isolated forced stop.
Reviewing it preserved the offer; Save local worked after the offer transitioned into the editor.
The test draft was deleted through
the explicit confirmation, leaving no saved draft. Metadata details expanded successfully. No
browser, copy, upload, issue submission, or dismissal occurred. Captures are retained only in the
ignored disposable tree at `scratch/reporting-game/proof-output/final-active-editor-v5-20260924.png`
and `final-active-metadata-v5-20260924.png`.

The isolated copy exited through its native menu. The dedicated listener closed and no FTK process
from the isolated root remained.

## Offline checks

- Final compact-editor Release build passed with seven existing EnemyVisualPatch warnings.
- ReportingDraft, ReportingMetadata, ReportingRuntime, and ReportingSession game-free suites passed.
- `git diff --check` passed.
- Framework Release build: passed (seven existing unused-field warnings).
- PlayerMods game-free checks: passed.
- Proof plugin and preloader Release builds: passed without warnings.
- Preloader transform checks: passed; exactly the two intended method bodies
  return false, missing method/wrong assembly is rejected, and source hash remains
  unchanged.
- Copy preparation checks: existing destination, invalid identity token, and a
  destination outside a direct scratch child are rejected.

## Outstanding qualification

The native-button takeover passed bounded title native-call and targeted keyboard
checks, ten open/close cycles, both controlled fault cases, and the active-solo
Options-to-editor route. Full gameplay return under physical input, controller
coverage, a verified browser focus-loss/return cycle, faults beyond the two bounded
title cases, scene destruction, combat/co-op, other platforms, and GitHub
authentication/attachment/submission remain open. No issue or spec is closed by
these checks.
