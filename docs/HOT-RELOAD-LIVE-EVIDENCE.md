# Restricted hot reload: live evidence

Historical prototype evidence follows. See [the current title-screen activation contract](HOT-RELOAD.md) for production hardening and current support boundaries.

This experiment uses a disposable macOS game copy and the audited installed
assembly. It does not establish general plugin unload, existing-save compatibility,
multiplayer compatibility, or post-adventure teardown. See
[the design](HOT-RELOAD-FEASIBILITY.md) and [prototype](HOT-RELOAD-PROTOTYPE.md).

The final corrected build passed 100 same-process enable/disable cycles, transaction
failure tests, and fresh enabled/disabled adventures. This demonstrates the restricted
Paladin mechanism. It is not a production-release pass: latency, generation retention,
remembered-class UX and native title-navigation containment remain gates. No merge was
performed. The owned test process was stopped and the ordinary helper restored.

## Isolation and corrections discovered by live testing

The test copy uses a unique application bundle identifier and unique serialized
PlayerSettings company/product names. The installed native PlayerPrefs implementation
uses NSUserDefaults; persistentDataPath has an independent company/product search
and bundle-ID fallback. The runtime harness verifies the actual bundle and data path,
round-trips a preferences sentinel, and preserves the root-derived test save namespace.
Native global player.db/statistics therefore use the disposable profile too.
Steam statistic/achievement writes and leaderboard submissions are suppressed by
reviewed test-only wrapper patches. Authentication and read callbacks remain native.

An initial launch omitted steam_appid.txt, causing Steam to redirect to the normal
executable. That task-originated process was stopped without opening an adventure
or loading an existing adventure save. The subsequent launch route includes the
app-ID file and a test-only SteamAPI.RestartAppIfNecessary guard. No production
binaries were deployed. Complete absence of incidental native title-startup
preference/statistics effects from that redirected process is not claimed.

Native SplashScreen loads FTK_main additively without selecting it as the active
scene. The admission check now validates the title object's owning scene and actual
main-screen focus, rather than requiring GetActiveScene to name FTK_main.

The helper's hot operations initially omitted the game fingerprint calculation
performed by ordinary prepare/activate. Offline CLI-round-trip fixtures found this;
both hot operations now compute it and a regression covers the real serialized request.
Cancellation during activation now preserves the helper operation and completion
callback, so the durable commit decision cannot be discarded by a cancel click.

## Observed transaction behavior

One process completed empty-to-Paladin activation, an update changing a weapon stat,
icon/model bytes and Guardian bindings, malformed-package rejection, disable, removal,
reinstall, and activation using the actual Mods menu button. All four injected failure
points (after reset, after load, after cache rebuild, before commit) restored the
previous active generation, identity digest and resource counts.

Paladin has 39 authored icon references but 37 unique icon paths. The live enabled
ledger contains 77 owned objects: 37 sprites/textures pairs, two private weapon
prefabs and one Guardian host. It registers 64 custom rows, 135 model/texture paths,
24 held-model plans, 36 display-model plans, 12 apparel plans and eight Guardian
equipment bindings. The update fixture removes one equipment binding. Disabled
state returns these ledgers to zero. Native proficiency instances/children move
from 576 to 579 and back.

## Repeated cycles and resources

The cycle build completed 100 full enable/disable cycles (200 activations) in one
process. Every enabled sample had the same 64 custom IDs and every disabled sample
had none. The custom identity digest was
`c7e1833a9c8d6f516fcdffc49dc52a1e29be03a12d02c61f4c48a64b6836fbed`.
A clean boot of the same package set produced that digest too.

At cycles 1, 10, 20 through 100, Unity object counts were identical within each
state: enabled GameObjects 50,746, textures 1,980, sprites 1,079, meshes 2,930,
materials 1,206; disabled 50,726, 1,943, 1,042, 2,930 and 1,206 respectively.
Owned resource, pending-destruction and renderer-lease counts returned to their
expected state at every transition. Native allocated memory grew about 0.32 MiB
from first to last same-state samples, rather than growing per reload. These
observations bound this trial; they do not prove an absence of all leaks.

The proposed two-second activation target **failed**. End-to-end apply/settle
measurements, excluding generation preparation, had enable median 3.71 seconds,
p95 10.84 seconds and maximum 32.72 seconds; disable median 1.13 seconds,
p95 2.27 seconds and maximum 10.11 seconds. Polling and background scheduling
contribute, so these are not isolated main-thread timing measurements. Phase
profiling and an agreed responsiveness budget remain release gates. Immutable
generations also accumulated about 2.6 GB during the trial. A retention policy
must preserve current, pending, rollback, in-flight and any future save-pinned sets.

Three additional cycles and install/update/remove/reinstall/failure transactions
passed with the actual Mods panel open, exercising UI reconstruction. During a
paused commit, Apply, Quit/apply, Discard and Cancel were all disabled. Navigation
remained available; adventure entry is separately guarded.

## Failure and recovery evidence

All four injected runtime failures rolled back. Helper rejection and helper timeout
also restored the old runtime and durable current generation. Removing a successful
helper acknowledgement still published the generation already committed on disk.

The test process and paused helper were forcibly stopped before commit: the next
boot retained the previous current generation and quarantined the uncommitted
pending target. Repeating after the durable commit loaded the committed generation
on the next boot. Recovery here uses a restart to verify the crash protocol; restart
is not counted as hot activation.

A nonfinite required-model vertex and an invalid numeric field were rejected.
The first truncated-icon test failed: Unity accepted a 33-byte PNG containing only
signature and IHDR. The fix validates bounded PNG chunk structure, CRCs, ordering,
image-data presence and terminal IEND before allocation, then checks decoded
header dimensions. Both icons and model-texture preflight use it. The final build
rejected that exact icon candidate and retained the old content. This validates
container admission plus Unity decode, not independent compressed-pixel integrity.

## Acceptance record

The intermediate PNG-admission build completed 10 further full enable/disable cycles,
all four rollback injections, install, update, rejected malformed content, remove
and reinstall. The original 100-cycle build and intermediate PNG build are distinct evidence
sets; the total is not a 110-cycle single-build leak trial.

A new native Lost Civilization adventure started after hot activation of the
update fixture. Native class selection displayed Paladin Reload Fixture, and the
created hero had class key paladin and all five custom novice equipment pieces.
All 36 equipment rows belonged to their current native category-cache lists.
Unmodified native town-stock generation included Paladin equipment. This establishes
pool participation and observed novice stock, not acquisition coverage of every tier.

An isolated combat fixture moved the existing party to a native Timberwolf encounter.
Native turns, animations and target selection then ran normally. Paladin's Guard
button selected another ally and resolved with protection active, rescue available
and no Focus spent. The native combat log identified the current custom proficiency
and equipped custom hammer. Combat appearance showed the novice custom equipment.
A preceding studio capture of the world avatar was blank and is not visual evidence;
only the directly observed native combat view supports that appearance claim.

The updated hammer displayed 11 damage and the changed checker icon. One native
Focus input completed, followed by a native attack that killed the Timberwolf.
This does not independently measure focused healing or Divine Intervention: the
chosen ally was not damaged and a rescue was not triggered.

Native return-to-title completed in the same process. Renderer leases and resources
returned to zero, while Guardian transient state was still nonempty. Activation
remained rejected by the permanent setup latch, preserving the current generation
and epoch. This directly supports the initial-title restriction: title scene
recreation alone does not demonstrate a reset adventure.

The remembered-class guard rejected disabling a Paladin saved as the next class
choice, retaining the current generation. The test process was stopped, its isolated
preferences exported, and only that disposable profile's Player0class was normalized
to vanilla for the independent disabled-adventure trial.

That trial exposed a real failure: hot disable reported success, but native New Game
threw in GameDifficulty.GetDynamicDifficultyText -> FTKHub.GetItemDisplayName. Live
diagnostics confirmed null indexes after disable for the 14-row class table,
419-row item table and 256-row weapon table. Every corresponding vanilla lookup
failed, although custom IDs and ownership counts were zero. The pristine snapshot
had captured these indexes before their lazy initialization. Enabled registration
rebuilt them, masking the defect; empty registration restored the null snapshots.

The correction initializes native indexes before baseline capture and validates
exact lookup/row identity for all five arrays, including vanilla rows, before cache
rebuild and commit. The live runner now checks those indexes explicitly. The corrected build completed 100 full live cycles with all native indexes checked
at every transition, plus the transaction suite. Disabled-adventure completion is
recorded below.





## Verification matrix and remaining gates

| Check | Result | Scope or remaining work |
| --- | --- | --- |
| Install, enable, update, disable, remove, reinstall without restart | Passed | Empty/current Paladin shape only; initial title |
| Deterministic identities over history and clean boot | Passed | All 64 custom IDs; collision histories also covered offline |
| Repeated Unity ownership and native cache cleanup | Passed in bounded trial | Final corrected build: 100 cycles with every transition audited; no same-state Unity object-count growth |
| Native proficiency row references | Passed | Candidate live-row validation, rollback exact old maps, three custom instances retired |
| Vanilla database lookup completeness after disable | Passed after correction | Early null indexes found live; final 100 cycles verify all vanilla/custom rows |
| Invalid scalar, model and icon | Passed after correction | Header-only PNG originally accepted; fixed and retested |
| Runtime failures at four transaction stages | Passed | Previous identity, generation and resource counts retained |
| Helper reject, timeout and lost acknowledgement | Passed | Disk pointer decides commit; no rollback after durable commit |
| Crash before/after durable commit | Passed | Quarantine old / boot committed generation respectively |
| Mods panel rebuilding and disabled cancel | Passed within slice | Panel open cycles and paused-commit controls; native title navigation remains a release restriction |
| Fresh enabled adventure and native combat | Passed within slice | Current class, five starting items, 36 cache rows, native town stock, Guard, Focus and attack |
| Every equipment tier/model/action visually exercised | Not run | All 96 unique GLBs preflighted; live appearance limited to novice starting equipment |
| Return to title reopens activation | Correctly rejected | Same-process permanent seal; transient Guardian state demonstrably survives |
| Remembered class semantic change | Correctly rejected | Disabling remembered Paladin retains current set; production UX policy unresolved |
| Existing-save load or migration | Unsupported | No existing adventure saves loaded; no compatibility stamp/migration delivered |
| Multiplayer or live invitation races | Unsupported / not run | Admission and native-entry guards reviewed; no co-op compatibility claim |
| Arbitrary DLL or BepInEx plugin unload | Unsupported | No assembly unload contract; never inferred from data-package result |
| Dependency graph transitions | Unsupported in PoC | Strict candidate admission rejects dependencies; helper consistency tested offline |
| Activation within two seconds | Failed | End-to-end measurements above; phase profiling still needed |
| Bounded on-disk generation retention | Not implemented | About 2.6 GB accumulated; preserve evidence and define pins before cleanup |

The live player log contained a native `AkInitializer.OnApplicationFocus`
NullReferenceException during startup. The test harness also recorded expected
rejections, including a Guardian observation attempted outside combat and sealed
activation requests. These are retained rather than described as a zero-error log.
No framework transaction fault or renderer exception was observed in the reported
successful activation/adventure slice.

## Recommendation and decisions before a merge

Keep this as an opt-in experimental branch. The bounded result demonstrates true
same-process activation for this data package and its framework-owned Guardian
and model resources. It does not justify a general mod reload promise or shipping
the test harness guards as production isolation.

1. Accept initial-title-only activation, with permanent sealing at first setup/load/
   join attempt. Supporting later title returns requires a separate teardown design.
2. Decide how to present and repair remembered class choices when disabling a class.
   This prototype rejects the change instead of silently choosing another class.
3. Agree an activation latency budget and profile preparation, synchronous preflight,
   cache rebuild, commit and retirement separately before optimizing.
4. Define generation retention and future save pins, then add a bounded cleanup policy.
5. Choose the first supported package/capability whitelist. General dependencies,
   existing saves, co-op and arbitrary assemblies remain separate work.
6. Finish the desired visual/ability regression breadth and review the experimental
   delta against the active branch before merging. Existing-save and multiplayer
   enablement require compatibility identity protocols, not merely additional tests.

Automatic restart remains a separate fallback. It is not counted as success for
the 200 same-process cycle activations or any other hot transition in this report.

## Native title browsers outside the proven slice

The final native review found that MainScreen.ShowLoreStore opens a uiLoreStore
which retains its inventory display. Cards retain lore rows, item-category displays
resolve unlock IDs and retain item textures, and confirmation retains purchase
callbacks. Closing the store destroys its visible children but does not establish
that every retained display/reference is cleared. Purchases also mutate profile state.
Current Paladin declares no lore entries, so this is not a demonstrated stale-Paladin
reference. It is evidence against extending the verdict to arbitrary data packages.

The current experimental lock excludes adventure/save/network entry and marketplace
mutations, but still permits Mods Back-to-title and native non-adventure panels during
an asynchronous commit. Candidate state is globally visible in that interval. Before
shipping, lock those transitions for the transaction and require an explicitly admitted
current screen. Conservatively seal after entering a content browser until its cleanup
contract is proven. Options/language need a transaction-time lock; this investigation
does not claim that they need permanent sealing. Adversarial navigation during commit
and stale native browser callbacks remain untested release gates.

## Final corrected-build cycle evidence

Framework `92f3f93604976105018a106480c4168013d09cd19b9667233c1e6e6ebe5207e0`
completed 100 full enable/disable cycles (200 activations) in PID 86871, with the Mods
panel open throughout, followed by all four injected failures, install/update/remove/
reinstall and malformed-content rejection. The native index/array joins were complete
after every transition: 1,887 vanilla rows when disabled, plus 64 custom rows when
enabled. This is the final-build cycle evidence; earlier cycles remain diagnostic
history and are not added to its count.

Every one of the 100 enabled audits had GameObjects 50,774, textures 1,980, sprites
1,079, meshes 2,933 and materials 1,242. Every disabled audit had 50,754, 1,943,
1,042, 2,933 and 1,242 respectively. First-to-last native allocation changed by
1,024 bytes enabled and 1,111,632 bytes disabled; allocation ranges were 553,176
and 1,316,272 bytes. Managed samples ranged from about 89 to 127 MB. No owned-resource
or same-state Unity object-count growth was observed; these measurements are a
bounded result, not proof of zero possible leakage.

Final enable apply/settle latency was median 3.82 seconds, p95 4.13 seconds, maximum
4.55 seconds. Disable was median 1.34 seconds, p95 1.44 seconds, maximum 1.55 seconds.
The two-second enable target still fails. These end-to-end measurements include
polling and frame scheduling, exclude preparation, and do not isolate main-thread
blocking. Across all preserved trials, generation directories occupied approximately
7.0 GiB on disk. No evidence or old generation was deleted to hide that accumulation.

## Fresh disabled adventure on the corrected build

After the final 100-cycle trial and transaction suite, the same PID 86871 rejected
bad-icon and bad-model fixtures with the previous empty state intact, reapplied the
disabled selection, and followed native New Game, configuration, map generation,
Blacksmith/Hunter/Scholar character creation, party start and story continuation.
The new overworld contained no Paladin class or equipment rows, no Paladin town
stock, and complete native row lookups. The earlier GameDifficulty item-name
exception did not recur. Native return to title kept the process sealed and another
Apply request was rejected. No existing adventure save was loaded.

## Fresh enabled adventure on the corrected build

A separate final-build process, PID 93185, booted with Paladin disabled and hot-activated
the update fixture without restarting. Native class selection, map/party creation
and story continuation completed with Paladin Reload Fixture. The hero received
all five novice custom equipment items; all 36 equipment rows remained exact current
cache members. A native Beastman Warrior encounter was entered using the isolated
party-placement fixture. Guard resolved through native ally selection with protection
active and no Focus spent. Appearance was observed in the native combat view.
Returning to title kept the same PID and permanent seal; Apply was rejected again.

The last owned process was stopped after evidence capture. The helper binary matched
its recorded original hash and no fault-injection control remained. Logs, captures,
fixture packages, generations, profile backup and machine-specific receipts remain
ignored local artifacts. No game assemblies, saves, decompiled source or logs are
staged for commit. The original checkout was not edited by this implementation.
