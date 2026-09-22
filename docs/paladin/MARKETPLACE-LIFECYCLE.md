# Paladin marketplace lifecycle evidence

On 2026-09-22, the native Mods menu was exercised in the normal macOS Steam
installation, in a 1280x720 window with 2560x1440 Retina rendering. No game-driving
plugin was installed. Actions used the visible UI and keyboard; authoritative
generation records and fresh process logs confirmed the results. Existing saves
were not loaded or edited.

## Observed outcomes

| Action | Result |
|---|---|
| Disable Paladin | Pending selection displayed OFF; after a normal Steam restart, Paladin remained installed and registered 0 entries. |
| Enable Paladin | Pending selection displayed ON; after restart, all 39 entries registered with zero errors or warnings. |
| Uninstall Paladin | Pending selection was empty; after restart, Installed had no mods and registration was 0/0. |
| Reinstall | Browse prepared the validated cached package; Installed displayed its queued status. Final normal Steam restart registered all 39 entries with zero errors or warnings. |
| Cancel queued install | Removed Paladin from the pending selection without changing the running generation. |
| Restore previous mods | Prepared the retained Paladin selection and displayed ON for next launch. This restoration was discarded before activation. |
| Discard prepared changes | Cleared the pending generation and preserved the active empty selection. |
| Next launch | Displayed name, version and ON/OFF both with and without pending changes; correctly displayed an intentionally empty selection. |
| Browse controls | Search, category filtering, clear filters and offline refresh produced the expected results. |
| Export mod list | Wrote a parseable export matching the active empty generation and framework version. |
| Components | Opened the empty component view with the expected no-components message. |
| Updates status | Loaded the published release history, saved Stable preference and compatibility restriction on the older preview. No framework version change was applied. |
| Attribution | Final installed entry displayed By JarlBrak, sourced from the package manifest. |

The game was left running from the normal Steam entry with Paladin installed and
enabled, no pending changes, and no offline simulation. Reinstall used an
explicitly seeded, validated candidate catalog and content-addressed archive
cache with network failure scoped to that test process. The original production
catalog cache was restored before the final normal Steam launch. This proves
cached reinstall, not public discovery or download of unpublished release assets.

## Artifacts and checks

The off/on and uninstall trial used framework DLL
`035113f8261a7818c01a4c8ea19d2c33406dc4504779e3a58fd33dd88aa3bf69`
and Paladin archive
`a0d952d1cb2b60e2745695c9ba9535a1b1d18a31b5d2a94c24db4ab167c858f5`.
The final reinstall used framework DLL
`898353d159af0a46cddfeede4027995bab14a5faf9971eeaf07e3b6d98af2ee6`
(smaller utility button captions) and archive
`13252ad16417bb7324209985891fa41682e0a4c3ce9658b886a9be1f16141f58`
(author metadata corrected to JarlBrak). Both used helper
`abe2999e68ba9185fb6847c2262fbea9bbe73f11bed0f11b6f7032123024b8d1`.

Release build, PlayerMods checks, helper Go tests, release-manifest tests,
installer fixtures (29 passed), package validation and diff whitespace checks
passed. Next-launch coverage includes 10 focused cases. Helper tests cover
installed-but-unlisted lifecycle changes while preserving descriptor validation,
archive integrity, compatibility and revocation enforcement. Cross-build checks
passed for macOS, Linux and Windows; they do not prove gameplay on those systems.

Local captures and state receipts are retained under the ignored
`scratch/paladin-menu-lifecycle` directory. Public download, framework update
application, multiplayer, long metadata scrolling and multi-page catalog
navigation were not established by this trial. See [launch gates](LAUNCH-1.0.0.md)
for remaining release work.
