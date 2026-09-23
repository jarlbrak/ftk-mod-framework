# Title-screen mod activation

Status: the restricted first version shipped in framework 1.0.0 after the
recorded macOS live acceptance. Other platforms and arbitrary packages remain
outside its verified scope.

## Supported first version

Title-screen activation supports the audited macOS game build, with either no community content or the supported Paladin package. Paladin includes its class, abilities, 51 equipment definitions, managed Guardian behavior, icons and custom models. The installed managed assembly must match the audited fingerprint. Other game builds and platforms keep next-launch activation.

Manual mods, arbitrary behavior DLLs, additional BepInEx plugins, dependencies,
the legacy 1.0.0 bundled demo and diagnostic injections do not qualify. Their
ordinary loading path remains available. An eligible but invalid candidate is
rejected transactionally; it is never accepted merely because its package GUID
says Paladin.

Enable **Title-screen activation next launch** in Mods > Settings and start the game again once to enter this mode. The configuration key is `Marketplace.EnableTitleScreenActivation`. It defaults off. After that mode is active, supported install, enable, update, disable and remove operations can activate in the same process:

1. Review and prepare the desired community mod changes.
2. Choose **Apply prepared mods now**.
3. Wait for validation and publication to finish.
4. Start a new local adventure, or resume a save from the active compatibility library.

Activation is available only at the initial title screen or the Mods panel opened from that screen. Starting adventure configuration, entering a save or opening the Lore Store permanently closes that process's activation window. Returning to title does not reopen it. Multiplayer is unavailable in this mode; launch with the setting off to use the normal multiplayer path.

During activation, navigation, stale button callbacks, cancellation and native content consumers are locked. A transaction that cannot prove recovery remains locked and offers **Quit game to recover**. Restart is failure recovery, not a substitute for successful hot activation.

## Adventure saves

Existing legacy adventure files are neither migrated nor loaded by this mode. New adventures use separate libraries under the game's persistent data directory, in `ftkmf-saves/<compatibility fingerprint>`. Lore, tutorial progress and input configuration keep their native paths.

A library fingerprint includes the game assembly, framework version, enabled package artifact hashes and relevant registration settings. It does not depend on a random preparation-generation ID. Reinstalling the exact same set restores access to its library. Updating a package selects a different library; it never silently treats old content as compatible.

Mods > Settings > **Saved mod sets** lists retained libraries by package name/version and save count. Reviewing and preparing a saved set replaces pending community changes only after an explicit review. Apply that set through the usual transaction, then use native Resume. Listing libraries does not parse adventure save data or start a game. Missing, changed or incompatible retained artifacts remain unavailable. A framework or game version mismatch requires the matching version; there is no migration promise.

Remembered character choices are remapped by class key. If a class is removed, the corresponding choice becomes the first released, unlocked, DLC-free native class. Unaffected selections retain their meaning. A durable journal reconciles these changes against the committed generation after an interruption, including a subsequent launch with activation disabled.

## Transaction and resource ownership

The immutable-generation preparation workflow remains the source of candidate bytes. The runtime validates the expected current/pending pair, captures exact old definition/cache/resource references, restores its pristine native baseline and registers the candidate in deterministic order. All five affected table indexes and custom identities must agree.

Every native renderer and skeleton contract is checked. GLB decoding uses at most four workers over copied names, bindposes and resolved paths. Workers never access Unity objects; all workers finish before a failure can trigger rollback. Texture decoding, registration and publication remain on Unity's main thread. Deferred destruction is fenced explicitly.

After runtime validation, the helper revalidates the generation and commits with compare-and-swap. The durable current pointer decides whether a lost acknowledgement means success or rollback. Old resources retire only after commitment. Interrupted uncommitted selections are quarantined at startup. Unknown decisions and failed rollback fault closed.

No historical registration reservations survive a successful canonical rebuild. A clean boot and hot activation of the same supported set must produce identical row identities, including the positional class ID. Per-mod teardown, arbitrary assembly unloading and hot changes during an adventure are not implemented.

## Retention and recovery

The framework holds a process-lifetime OS lock for its marketplace root. A second process cannot discover or mutate that root without ownership. The helper uses the same `flock` or `LockFileEx` contract.

The Modded launcher collects unused generations while the game is stopped, taking the runtime lock before the transaction lock. Collection preserves current, pending, previous, unresolved activation intent and save-pinned generations. Malformed retention records defer collection. The explicit helper `collect` operation follows the same rules. It does not delete saves or artifact archives.

Normal preparation has an 8 GiB generation budget. Reaching it rejects preparation without changing current or pending; close the game and use the Modded launcher for offline cleanup. Protected test runs can retain historical generations as evidence. Saved generations stay pinned conservatively, including after a save is removed; deleting or migrating save libraries is outside this feature.

A failed activation retains the previous runtime and generation whenever rollback can be proved. Review the status in Mods and the framework log. Do not manually delete recovery journals or retained generations to bypass a failure.

## Evidence and limits

The [initial investigation](HOT-RELOAD-FEASIBILITY.md), [prototype record](HOT-RELOAD-PROTOTYPE.md) and [historical live evidence](HOT-RELOAD-LIVE-EVIDENCE.md) preserve the earlier findings, including failures that drove these changes. The initial two-second latency target was not met by the prototype. Final timing, the completed acceptance matrix and longer-run outliers are recorded in the [hardening evidence](HOT-RELOAD-HARDENING-EVIDENCE.md); an offline build is not live evidence.

This feature's acceptance concerns lifecycle, identities, resource ownership, activation, fresh adventures and exact-set resume. It does not expand the visual, ability, co-op or platform coverage of the Paladin package. Unsupported mods continue to require a new process.
