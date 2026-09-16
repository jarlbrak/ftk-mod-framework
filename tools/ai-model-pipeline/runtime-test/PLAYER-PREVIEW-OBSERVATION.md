# Actual native player preview and resource lifetime

The observer reads existing single-player native menu objects. It does not create
UI, select a class, invoke SetClass/SyncSettings, change inventory/PlayerPrefs,
render a substitute, or modify production leases. `IsSinglePlayer()` is a mode
check in this game, so the existing guard also applies at menus; no guard bypass
or separate post-session route is added.

When the validation execution queue names a stage-ready `playerSkinset` route,
first pin its current profile/catalog/assets with:

```sh
python3 tools/ai-model-pipeline/runtime-test/plan_execution_queue_player_route.py \
  --stage-readiness scratch/model-validation-stage-readiness.json \
  --game-root scratch/my-isolated-game \
  --topology-group EXACT_TOPOLOGY_GROUP
```

This prints an input plan only. It neither launches FTK nor selects a class, so
the visible native navigation and every preview observation remain explicit.

Use `command.py --root ABSOLUTE_ISOLATED_GAME player-preview-state --payload FILE`
with:

```json
{"classKey":"EXACT_REGISTERED_CLASS_KEY","skinset":"EXACT_SKINSET_KEY","catalogSha256":"SHA256_OF_MODEL_TEST_PLAYER_PROFILES_JSON"}
```

If multiple actual slots select the same class, add exact `ownerInstanceId` from
fresh `inventory` with `scope: "player-preview"`. The observer requires current
`uiStartGame.m_CreateUIs` membership, loaded/active native UI, exact selected
class/skin, reciprocal `CEL.m_uiQuickPlayerCreate`, and the native pedestal parent.
Missing or rebuilding UI yields `unavailable_native_preview_absent_or_rebuilding`;
it never substitutes a constructed avatar. Invalid identity/catalog fails closed.

The result includes actual menu/owner/CEL/pedestal IDs, selected class label and
skin, loaded profile/catalog pin, current on-disk asset hashes, assembled SMR
snapshots and lease, actual inventory/custom-outfit IDs, and existing rigid
accessories. Missing profile alternatives and active native apparel must be
compared separately; this is not full-outfit or visual acceptance. Use ordinary
screenshots or the existing scoped capture against the actual observed renderer.

To observe disposal after a separately authorized native class/slot change, arm
`lease-watch-preview` with the same fields plus these exact values from a fresh
successful observation:

```json
{"classKey":"EXACT_REGISTERED_CLASS_KEY","skinset":"EXACT_SKINSET_KEY","catalogSha256":"CATALOG_SHA256","ownerInstanceId":1,"celInstanceId":2,"expectedLeaseId":3}
```

The shown integers are placeholders, never IDs to guess. Arming requires a live,
acquired/applied existing lease. It stores only managed references to that lease's
owned Mesh/Material/Texture resources and immutable IDs/provenance; it retains no
CEL, calls no lease Retain/prune/Destroy, and keeps the shared limits8 leases and
256 resources. It is one explicit arm, not a background rearming watcher.

After the native operation and deferred destruction, call existing
`lease-watch-state`. Each record retains arm command, root, session, Core assembly
module identity and measured on-disk binary SHA, selected preview class/skin,
owner/CEL/lease IDs and resource identities. Changed identity is an error, never
proof of disposal. Only **lease absent AND every pinned resource Unity-null** is
`observed-disposed`. Hidden UI, title transition, changed current reference count,
or missing watcher state alone proves nothing. The same readout can be tried
post-title while the game remains in single-player mode. No new arms or mutations
are allowed through an exception to that mode guard.

`lease-watch-clear` drops only diagnostic managed references. Existing COW/Ready
`lease-watch` remains available and now carries the same root/session/Core pins.
The new pin guards reject stale identity before old-resource readout; they do not
change production cleanup. Native preview/final-owner disposal still requires an
actual observed run.

Linked offline checks use the shipped Newtonsoft4.5 assembly:

```sh
dotnet run --project tools/ai-model-pipeline/lease-observation-tests/LeaseObservationTests.csproj \
  -p:TestGameRoot=/absolute/path/to/scratch/game-copy
```

They cover wrong owner/CEL/lease, inactive/unacquired/unapplied/absent/duplicate
leases, exact8/256 bounds, stale root/session/module/file identity, and immutable
metadata copies. They do not instantiate Unity objects or establish live menu
selection, native lifecycle, or art acceptance.
