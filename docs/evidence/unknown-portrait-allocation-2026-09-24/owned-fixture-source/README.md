# Owned portrait measurement fixture

Frozen original measurement code, not decompiled game source or a supported mod.
Requires an explicitly authorized isolated game directly under `scratch`, the active
runtime model-test helper, package-only content, and `FTK_RESOURCE_PROBE=1` in addition
to the isolation environment. Never deploy to a normal installation or a running player.

Build with `dotnet build -c Release -p:TestGameRoot=<absolute isolated root>`.
Copy only the resulting FtkResourcePrototype.dll while the isolated game is stopped.
The original model texture fixture is included because the plugin and GPU readback
helper share code; this measurement uses only the portrait commands.

After reaching an isolated overworld, run from the repository root:

```sh
python3 docs/evidence/unknown-portrait-allocation-2026-09-24/owned-fixture-source/capture_portraits.py portrait-unused-a "$TEST_GAME_ROOT"
```

Use fresh labels. A label containing `baseline` installs constructor tracking and,
after widget destruction, cleans up only its observed unreferenced allocations.
Do not use baseline tracking with a changed allocation transpiler. Candidate captures
instead observe new 328x280 ARGB32 textures plus the displayed image. Native methods
run on a constructed widget using the game's portrait child prefab. This is not an
end-to-end encounter-menu automation.

`portrait-drain-stats` exists only to measure the rejected deferred-cleanup prototype;
the accepted allocation skip has no Drain method. Do not invoke that command on it.
`portrait-mode` with `enabled:false` removes only the candidate transpiler after the
fixture is destroyed, allowing a native control in the same settled scene. Restart
to re-enable it. GPU hashes are obtained by
readback after timed initialization, with temporary readback resources released.
The command fixture scans resources for measurement; the final framework patch does not.
Stop the owned player after the run to release unchanged native known-enemy allocations,
and restore temporary framework configuration. No production saves are used.
