# Title-screen hot reload: feasibility and proposed boundary

Investigation date: 2026-09-22. This document preserves the initial design and source
baseline findings. Subsequent implementation and live testing are recorded in the
[prototype](HOT-RELOAD-PROTOTYPE.md) and [live evidence](HOT-RELOAD-LIVE-EVIDENCE.md).
Statements about absent implementation or no launch below describe the investigation
stage, not the later experiment.
Source baseline: `6d8d35840afeae8617c97455e19937a83b5efb88` plus the existing working-tree changes, including Paladin and marketplace lifecycle work. Those changes were preserved. No game was launched, no save was loaded, and the normal installation was not modified.

## Recommendation and verdict

Build a restricted **initial-title activation transaction for managed declarative packages**, starting with Paladin. It should install, enable, disable, update, and remove content in the same process, then permit a fresh local adventure. Freeze the generation as soon as any adventure setup, save loading, or multiplayer session begins. Do not initially permit activation after returning to the title screen.

This is feasible as an engineering direction, but **not safe in the current implementation**. A scene name, a refreshed Mods panel, an updated generation pointer, or a second `ContentLoader.Load` call is not an activation transaction. Runtime publication and successful initialization must become part of the commit contract.

| Category | Verdict | Initial support |
| --- | --- | --- |
| Managed data packages with audited capabilities, including Paladin | Feasible at a restricted boundary after registration, ownership, cache, and rollback work | Paladin plus its exact audited dependency closure; no arbitrary content kinds |
| Framework-managed behavior instances and GLB/PNG resources | Feasible when the executable framework remains loaded and every mutable binding and allocated object has an owner | Existing Guardian implementation and Paladin asset paths after lifecycle adapters; no framework-code update |
| Other declarative marketplace packages | Conditional, not automatically safe because there is no DLL | Admit only content kinds/capabilities whose complete consumer state has a reset contract |
| Manual behavior DLLs | No safe general unload/update contract today | Restart-required; do not attempt live loading as part of a reversible transaction |
| Arbitrary BepInEx plugins | No general hot unload or replacement guarantee | Restart-required; unknown loaded plugins also disqualify the restricted reload session |

An automatic process restart may remain a separate fallback. It does not meet these acceptance criteria.

## Verified framework evidence

Paths below refer to the inspected working tree. Method names are durable anchors; line numbers may change as existing work proceeds.

| Surface | Evidence and consequence |
| --- | --- |
| Startup versus title recreation | [Plugin.cs](../FTKModFramework/Plugin.cs), `TableManager_Initialize_Patch.Postfix`: `_done` selects `RestoreRegisteredRows` on subsequent initialization. Discovery, behavior setup, and capabilities do not repeat. Resetting `_done` alone would duplicate registration. |
| Disk selection versus runtime | [MarketplaceRuntime.cs](../FTKModFramework/Core/Marketplace/MarketplaceRuntime.cs), `InitializeBeforeDiscovery`, `Poll`, `ReconcilePending`: startup invokes helper activation; subsequent operations update Pending and deliberately preserve Active. `RecordRegistrationErrors` explicitly says selected generation is not proof of loading. |
| Filesystem transaction | [marketplace.go](../launcher/helper/marketplace.go), `prepare`/`activate`/`rollback`: preparation creates immutable content and lock records under a transaction lock; activation validates and moves current/previous/pending before game registration. Rollback prepares the previous selection. Existing plan revisions protect preparation, not a combined runtime commit. |
| Registration | [ContentRegistry.cs](../FTKModFramework/Core/ContentRegistry.cs), `Register`: publishes ID mapping before appending/configuring a row, rebuilds indexes, then records it for restoration. An exception can leave earlier effects. Batch mode defers indexing; it provides no rollback. |
| Restoration | [RegisteredRowRestoration.cs](../FTKModFramework/Core/RegisteredRowRestoration.cs): retains object references and original array positions, requires a contiguous suffix, rejects collisions and gaps. It is intentionally not per-mod removal or replacement. |
| Discovery and load | [ModDiscovery.cs](../FTKModFramework/Core/Data/ModDiscovery.cs), `DiscoverAll`; [ContentLoader.cs](../FTKModFramework/Core/Data/ContentLoader.cs), `Load`: sort discovery, sort entries by GUID/key, run base and reference phases, then capabilities after indexes. Errors are isolated and logged while other entries continue. Strict hot activation must reject any incomplete candidate. |
| Loaded mod metadata | [ModRegistry.cs](../FTKModFramework/Core/Data/ModRegistry.cs): Enabled is immutable; duplicate registration returns the old entry; toggles change PendingEnabled. Repeating discovery does not update version, availability, or metadata. Unknown-key gating currently fails open. |
| Identity | [Content.cs](../FTKModFramework/Core/Content.cs), `AddClass`; [IdAllocator.cs](../FTKModFramework/Core/IdAllocator.cs): classes use array positions; other IDs use a hash with process-global linear collision probing. Collision outcomes can depend on insertion history. A retained union of past selections need not match a clean boot of the final set. |
| Lookup and names | [DbLookupPatcher.cs](../FTKModFramework/Core/DbLookupPatcher.cs), [EnumPatches.cs](../FTKModFramework/Core/EnumPatches.cs), [Localization.cs](../FTKModFramework/Core/Localization.cs): persistent patches consult global ID/name/description maps. Keep patches installed; replace their owned state coherently. |
| Model identity | [PackageModelPaths.cs](../FTKModFramework/Core/PackageModelPaths.cs), `Register`: identity includes GUID, relative path, and byte hash, but an existing identity at another absolute path is rejected. Even unchanged assets in a new generation currently collide. |
| UI | [ModsPanel.cs](../FTKModFramework/Core/UI/ModsPanel.cs), [ModsPanelNextLaunch.cs](../FTKModFramework/Core/UI/ModsPanelNextLaunch.cs): details, selected entries, confirmation plans, callbacks and preview textures refer to current snapshots. Refresh must replace stale selections and callbacks, not just labels. |

### Paladin is declarative, but not behavior-free

The inspected [manifest](../marketplace/packages/paladin/manifest.json) declares no behavior DLL. The [content file](../marketplace/packages/paladin/content.json) has 39 authored entries: one class, two proficiency definitions, 12 weapons, and 24 items. It also requests 24 modifier rows, the shared Guardian action, 39 icons, 36 display-model bindings, 24 equipped-item model bindings, 12 apparel bindings, eight Guardian equipment bonuses, and two weapon proficiency attachments. There is no package-declared player-body model.

That is **64 custom DB rows when the shared Guardian action was not already present**, not merely 39 rows. Tests must account for shared capability ownership rather than assuming this total in every configuration. Inline proficiency attachments reference authored abilities rather than representing another pair of authored entries.

The package relies on compiled framework logic: `ContentLoader.ApplyCapabilities`, `GuardianRuntime.RegisterClass`, `RegisterEquipment`, item modifiers, item/display/apparel renderer integrations and icons. `GuardianRuntime` maintains additive class membership, first-registration equipment bonuses, a shared ActionId, combat state, pending healing/feedback, and attack bookkeeping. Removing a class row does not remove these bindings. An update that removes a capability must remove its old binding too.

## Installed-game evidence

Authority: installed `Assembly-CSharp.dll`, SHA-256 `94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8`, inspected read-only with ilspycmd. These are control-flow findings, not live observations. Decompiled source remains outside the repository.

| Exact game member | Verified behavior | Consequence |
| --- | --- | --- |
| `GameLogic.RestartFadeOutFinish()` | Commits lore/end-session work, leaves Steam lobby, completes delayed screenshot work, clears `gAction`, stops audio, destroys Photon views, disconnects and loads `FTK_main` | Return has persistence and asynchronous lifecycle effects; it does not establish universal static-state cleanup. |
| `uiStartGame.ShowStartPage()` / `InitializeSingleton()` | Clear selected UI/client lists and resume/map flags; reset `m_GameStarted` | Title visibility and a false started flag do not prove a pristine process. |
| `GridEditor.TableManager.Instance`, `Initialize()`, `Get<T>()` | Find the scene manager, instantiate DB/dictionary prefabs and cache components by type | Initialize is not a teardown API; do not call it as a generic reset. |
| `GridEditor.GEDataArray<T>.MakeIndex()` / `CheckAndMakeIndex()` | Owns both array and int dictionary; CheckAndMakeIndex only rebuilds a null dictionary | Replace/rebuild indexes explicitly; a non-null stale index is not refreshed automatically. |
| `FTK_itembase.GetItemBase()` | Routes between item and weapon DBs using native ID classification | Keep framework custom-ID routing consistent with the replacement maps; there is no separate item-base DB to rebuild. |
| `GameCache.Cache.Initialize()` / `GameCache.Cache.Items.Initialize()` | Static initialization guards; item cache stores prefabs, icons, nonclickable icons and actual row references grouped by category from item and weapon tables | Rebuild all four item maps against the committed tables. Scene changes do not ensure this happens. |
| `GameCache.Cache.Items.GetItemsByType()` | Returns the cached list itself | Swapping the cache dictionary does not invalidate a list already held by a consumer. |
| `ProficiencyManager.Start()` / `ProficiencyBase.Init()` | Manager scans proficiency rows, instantiates each behavior prefab, calls Init and parents the instances; Init retains the row and copies its custom value | No reset/teardown method was found. Re-running Start leaks old children; admission requires no live manager instances, or an audited destroy/recreate adapter. |
| `TownManager.InitializeMC()` / `_fillMarketLists()` | Materialize town, dungeon and night-market lists from category rows, flags, lore and game-definition exclusions | These are additional acquisition consumers, not mere DB lookups. |
| `TownManager.CreateNewShopInventoryForPOI()` | Builds stock dictionaries keyed by item IDs | Already-created adventure stock cannot be repaired just by rebuilding global lists. |
| `FTK_itemsDB.GetAllItemsOfTypes()` | Scans the table array directly | Retained unavailable rows still require direct-table acquisition filters. |
| `GameLogic.FillLootDropList()`, `FTK_enemyCombat.ItemDrops.GetLootItems()`, `FTKHub.GetWeightedDropItem()` | Weighted drops use cached categories and TownManager artifact state; explicit guaranteed/specific/always-drop paths also exist | Category filtering alone cannot implement availability. Candidate validation must reject references to removed definitions; specific-item lookup can dereference a missing row. |
| `GameCache.Cache.GameDefinitions.Initialize()` | One-shot parsed preview/file-data/image cache | Adventure packages add a different cache and image lifecycle; excluded initially. |
| `GameCache.Cache.Enemies.Initialize()` / `GameLogic.OnDisconnectedFromPhoton()` | Enemy cache has a targeted rebuild flag; disconnection marks it dirty and reinitializes DLC rights | This existing reset does not prove equivalent item/game-definition cache reset. |
| `uiQuickPlayerCreate.SetClass()` / `SyncSettingsRPC()` | Index class array by class ID; class change reconstructs avatar | Positions are identities and previews retain consumers. |
| `uiQuickPlayerCreate.OnClassClick()`, `OnClassClickLeft()`, `RandomClass()` | Cycle modulo DB count or scan rows and eligibility flags | Hiding one picker entry is insufficient for retention-based disabling. |
| `uiCharacterCreateRoot.CreateUI()` / `uiQuickPlayerCreate.SyncSettings()` | Read remembered integer selections from PlayerPrefs, pass class IDs into Photon instantiation data, persist/synchronize selection | Even first title has historical numeric preferences. Clear/remap only affected class choices before setup; snapshot them for rollback. |
| `PlayerSerialize.Serialize()` / `Deserialize()` and `FTKHub.CreatePlayerDeserialize()` | Persist instantiation data; reconstruct class from integer `m_InstData[4]` | Reusing a slot can silently change a saved character's class. Exact generation checks must precede deserialization. |

The item-cache, town-market and weighted/explicit loot paths establish that availability-only filtering is nontrivial. Remaining native census work includes every quest reward, encounter reward, scripted grant and proficiency-UI consumer. This is not an exhaustive certification of all game consumers. Before implementing the availability alternative, enumerate and gate those remaining consumers. For the proposed complete-set rebuild, phase 0 must still prove no additional materialized item consumers exist at the admitted initial-title boundary. No universal teardown claim follows from this investigation.

## Runtime state and ownership inventory

This is the required ownership boundary for the Paladin slice, with wider surfaces explicitly excluded until adapted. 'Reversible' means an adapter can make it reversible; it does not claim that an API already exists.

| State | Consumers / retained references | Required treatment |
| --- | --- | --- |
| DB row arrays, int indexes, string ID maps, retained-row ledger, allocator reservations | TableManager DBs, enum patches, direct row consumers | Construct target from a captured vanilla baseline and canonical full target order. Snapshot old arrays/maps/ledger/index state; publish and restore together. Preserve vanilla objects. |
| Mutable arrays/nested fields cloned from templates | Starting equipment, proficiency arrays, class skills and other row data | Clone every mutable field the builder changes. Audit shallow copies; never restore by mutating a shared vanilla object. |
| Localization maps | Name, flavor, proficiency description, equipment/class UI patches | Generation-owned maps, atomically replaced with the row set. Remove absent keys. |
| Guardian classes/equipment/action and behavior row | Gameplay patches, action buttons and class-info patches | Snapshot configuration separately from combat state; shared action host owned once or explicitly reference-counted. Zero transient state is a boundary requirement. |
| Private weapon prefab clones | `Content.AttachProficiencies` and cloned proficiency dictionaries; persistent Unity objects | Track each clone under candidate ownership and destroy on rejection/retirement. A row snapshot alone does not own its prefab. |
| BehaviorRegistry/PassiveRegistry and behavior prefabs | Proficiency rows, passive dispatch, quest factories | Keep audited framework type catalog process-lifetime; generation-own bindings and created hosts. No arbitrary constructor/callback in reversible preparation. |
| PackageModelPaths and PackageIcons | Opaque asset tokens, Sprite and Texture2D objects referenced by rows/UI | Generation-scoped resolvers or shared immutable byte identities with root leases. Destroy owned sprites/textures only after all consumers retire. |
| ItemModelRegistry, ItemApparelRegistry | Item IDs to equipped/display/apparel assignments | Replace complete maps; detach old renderer consumers before release. |
| Player visual/model/backpack/tint and enemy visual/portrait maps | Character previews, overworld/combat models, instantiated renderers | Package class-body/backpack/enemy registrations are excluded initially. Paladin apparel still uses PlayerMeshPatch even without a class-body plan, so its player-avatar, preview and overworld clone lifecycle is required. |
| Explicit mesh/texture allocations and renderer snapshots/leases | Meshes, materials, bones, bind poses, disabled source renderers and clones | Per-instance restore/detach followed by generation lease release. Existing visual rollback is local to a renderer and does not undo registration. Count native Unity objects as well as managed references. |
| AssetBundle cache | Cached bundle handles and loaded assets | Exclude from first GLB/PNG slice. Future unload requires explicit live-instance leases; never call blanket unload against shared assets. |
| BehaviorHost objects | Active `DontDestroyOnLoad` GameObjects used as proficiency prefabs | Register ownership immediately upon allocation, including failure paths. Destroy only framework-owned inactive-generation hosts after consumers are gone. |
| ModRegistry, marketplace Active, panel selections/plans/textures | UI callbacks and package metadata | Publish one generation epoch; reject stale callbacks/plans. Rebuild UI references and dispose obsolete previews. |
| Proficiency manager instances | Dictionary of instantiated behavior children; each behavior retains `m_ProficiencyData` and copied configuration | Require absence at the boundary or reconstruct the manager and children through an audited adapter. Restore/reinitialize old consumers if publication rolls back. |
| Character creation, proficiency lookup, acquisition pools | Class rows/indices, instantiated behavior prefabs, loot/shop candidates | Boundary and cache adapters must establish zero old consumers or rebuild all relevant caches before reopening input. |
| Game definitions, quest/campaign routers, adventure previews and whitelists | Save namespaces, game definitions, branch state, quest logic | Exclude campaigns, realms, encounters, enemies and custom quests from v1. A data-only label is insufficient admission evidence. |
| Existing adventure, players/inventories, network properties, saves | Numeric identities and live row/object references | Not currently reversible by a framework transaction. Freeze activation; do not migrate or delete these objects opportunistically. |

Relevant resource implementations: [BehaviorHost.cs](../FTKModFramework/Core/BehaviorHost.cs), [GuardianRuntime.cs](../FTKModFramework/Core/GuardianRuntime.cs), [PackageIcons.cs](../FTKModFramework/Core/PackageIcons.cs), [Content.ItemModels.cs](../FTKModFramework/Core/Content.ItemModels.cs), [Content.ItemApparel.cs](../FTKModFramework/Core/Content.ItemApparel.cs), [ExplicitEnemyMeshSwap.cs](../FTKModFramework/Core/ExplicitEnemyMeshSwap.cs), [CustomModelLoader.cs](../FTKModFramework/Core/CustomModelLoader.cs).

## Precise first activation boundary

Use an explicit process lifecycle state, not `scene == title`. Start closed, then enter `PristineTitle` only after native databases and framework startup have completed successfully and a baseline has been captured before mutable package registration.

Activation requires all of the following, checked both before preparation publication and immediately before commit:

1. The process has never entered character/adventure creation, begun loading a save, started/resumed an adventure, or joined/hosted a multiplayer session. Mark this history latch at entry, before callbacks can construct dependent state. It remains closed even if the player cancels or returns to the title.
2. The native title is stable, no scene transition is pending, and no character selection, inventory, proficiency instance, gameplay preview, or adventure construction is in flight. A hidden UI is not proof of destruction. The audit must classify expected title singletons instead of assuming every singleton must be null.
3. No active/joining network room or gameplay lobby, no session RPC work, and no asynchronous callback that can publish content. A platform service connection alone need not be forbidden if verified inert; v1 must still prohibit room/join entry during activation.
4. All content-consuming views are closed or rebuilt under the activation lock. Native and framework cache adapters report known state, and renderer/behavior leases from the old generation are zero except explicitly owned baseline hosts.
5. Only an audited framework build and allowlisted packages are loaded. No manual content, bundled demo content, unknown BepInEx plugin, arbitrary behavior DLL, development fixture, or diagnostic content injector participates in the initial proof of concept. Later admission must prove a stable baseline and ownership contract.
6. No marketplace/update helper or competing activation is outstanding; revision, generation hashes, database instance epoch and eligibility still match. A failed startup registration disqualifies activation until repaired.

The Mods menu may still prepare changes after eligibility closes, but must clearly say that they cannot become active in this process. Finishing a run does not reopen eligibility. Supporting return-to-title reload is a separate later project requiring a verified teardown of adventure, UI, networking, and static caches.

## Alternatives

### Retain definitions, change availability

This can be useful for fixed, already-loaded catalogs, but is not a complete solution for the requested operations. It avoids immediately invalidating old references and class slots. However, a disabled class must be removed from every picker, starting-kit path and remembered selection; equipment must be excluded from loot, all merchants, rewards and other enumerators; capability dispatch must honor availability. A hidden Mods entry is not disabled content.

Installing another class introduces a new positional ID. Retaining historical slots creates a different identity map from a clean process with the same selected packages. Updating a retained row in place changes all outstanding consumers; keeping both versions requires versioned lookup and asset ownership. Removing a package while preserving its definitions also requires pinning its files and memory. Tombstones alone do not prove unavailable classes cannot be selected through raw indexing.

Verdict: do not choose this as v1's general architecture. It is narrower than install/update/remove and creates a new global availability contract across native consumers. Consider it only as a future explicit 'unavailable for new runs, definitions retained' mode with a different identity policy.

### Full game teardown and rebuild after returning to title

This would offer the broadest boundary but presently lacks evidence of complete native reset. Reloading the title scene already rebuilds some tables while framework state survives, which demonstrates mixed lifetimes rather than a clean slate. Reconstructing arbitrary native and plugin state is much larger than Paladin activation.

Verdict: defer. Do not advertise scene reload as a substitute for process isolation.

### Rebuild the complete managed definition set at pristine title

Capture the known native baseline, compile the complete target set, and replace the framework-owned publication as a transaction. Keep the old generation only as a bounded rollback snapshot until commit/retirement. Recompute positional classes and hash collision allocation in clean-boot order. No adventure can yet depend on the replaced identities.

Verdict: recommended. It avoids a universal availability filter and per-mod removal algorithm. Costs are complete-set reconstruction, temporary old-plus-new memory, explicit cache adapters, and a permanently closed boundary after gameplay setup. These costs are bounded and testable for Paladin.

## Smallest coherent architecture

Add three internal concepts, keeping existing public `Content.*` authoring APIs and permanent Harmony hooks:

- **Generation context:** immutable selected packages/bytes/settings, deterministic identity manifest, owned rows/localization/capability/model bindings, native object allocations, and generation leases. Separate immutable framework behavior type definitions from generation-owned instances and bindings.
- **Strict generation builder:** parse and validate without publishing; resolve all references and generated capabilities, reject cross-mod raw IDs that collide in the per-DB string map, deep-clone needed template fields, plan IDs, preflight assets and bindings, and collect all errors. Reuse authoring logic via an explicit internal registration target. Do not swap global state temporarily to make the current fail-soft loader appear to stage content.
- **Activation coordinator:** lifecycle latch, one transaction owner, native cache adapters, durable journal, runtime publication, rollback, resource retirement and UI status. Unity allocation, DB publication and destruction run on the main thread. Worker tasks may hash/read/parse immutable files but may not touch Unity objects.

Not every native reader can use one pointer. 'Atomic publication' means input and content consumers are quiesced while multiple native arrays/indexes and framework maps are switched. Cache reconstruction may span frames while the lock remains held. Generation-aware callbacks reject stale epochs. If quiescence cannot be proven, activation is refused.

Keep process-lifetime patches installed and dispatch through the published generation. Unpatch/repatch churn is unnecessary for package configuration changes. Updating the framework assembly itself remains a restart operation.

## Transaction and recovery protocol

State sequence: `Prepared -> Validated -> Quiesced -> Publishing -> RuntimeVerified -> Committed -> Retired`. Failure before durable commit restores the old generation; failure to prove restoration enters `Faulted` with new-game/load/join gates closed.

1. **Prepare files.** Reuse helper download, archive policy, exact dependency closure, compatibility checks, hash validation and immutable generation layout. Resolve enable/disable/remove and dependency changes together. Reject disabled required dependencies, cycles, version conflicts, missing references and unsupported capabilities. A dependent cannot remain enabled while its provider is removed. Nothing changes Active.
2. **Compile candidate.** Build the complete target from the baseline in canonical order, including generated capability rows. Produce a strict error report, ID mapping and ownership ledger. Validate required PNG/GLB files and renderer bindings against audited fixtures. No silent template fallback or skipped capability may count as success. Only stage resources with a proven side-effect-free creation path. Defer active prefab/behavior instantiation until after quiescence: current weapon clones and BehaviorHost objects can execute Awake/OnEnable/Update even when parked offscreen. Audit those callbacks and record ownership before subsequent failure points; reject code whose side effects cannot be reversed.
3. **Acquire boundary.** Recheck the history latch, session/scene/cache state and expected revision. Block native start/resume/join controls and programmatic entry points, not just the Mods button. Pause/close content views and drain owned callbacks. Snapshot old arrays, indexes, maps, ledger, metadata and leases. Pin both generations on disk.
4. **Journal intent.** Persist transaction ID, expected old generation/revision, target hashes, game/framework fingerprints, identity manifest hash and phase. Add helper compare-and-swap commit/abort operations. Do not use today's `activate` operation to publish disk state before runtime verification.
5. **Publish on main thread.** Materialize the deferred, audited prefab/behavior objects under candidate ownership, then install the full row arrays, IDs, restoration ledger, capability/localization/asset maps and ModRegistry snapshot. Reindex touched DBs and rebuild/invalidate every admitted native cache. Resolve shared Guardian ownership and reset its transient state. Its action icon is currently written by class capability registration; require one framework-owned presentation policy or reject conflicting providers, rather than allowing registration order to choose it. Remap remembered class selections by logical key where a verified prior mapping exists; otherwise clear affected selections to a valid vanilla default. Snapshot preference changes for rollback and persist only after commit; journal their intended adjustment so crash recovery repeats it before character creation. Rebuild title UI references with the new epoch. The old snapshot remains intact.
6. **Verify while locked.** Assert exact row counts, bidirectional IDs, class index equality, resolved starting gear/proficiencies/modifiers, Guardian bindings, asset identities and cache contents. No retired generation object may remain in an admitted consumer; explicitly shared immutable framework implementations and unchanged vanilla baseline objects are exempt. Any failure rolls back all published surfaces, then rechecks the old invariants.
7. **Durable commit.** Helper compare-and-swap commits only if old revision and target still match. The disk commit record is the durable decision. Runtime Active becomes the verified target and UI reports success only after the decision is confirmed. If an acknowledgement is lost, query by transaction ID; do not blindly abort or retry. If the disk decision cannot be determined, stay locked and faulted.
8. **Retire.** Release old snapshots and owned UI/renderer/behavior objects after all old leases reach zero and Unity's deferred destruction completes. Unpin obsolete files only after memory and durable rollback/save retention policies allow it. Resource retirement failures must be reported and block further reloads rather than allowing unbounded leaks.

Preparation failure leaves runtime and current disk selection unchanged. Publication or durable-commit failure before the commit decision restores old state, releases candidate resources, and leaves a diagnostic rejected candidate. Rollback failure blocks gameplay and further activation; offer repair/restart as recovery, never claim successful hot reload.

On crash, a new process reads the journal before content discovery. With no durable commit it selects the old generation and quarantines the incomplete transaction. With a durable commit it validates and loads the target. If target startup registration fails, do not open gameplay or call it successfully active; retain old recovery data and report a controlled recovery path. Do not silently combine old runtime content with a new disk label. This is crash recovery, not a substitute for the same-process success path.

## Deterministic identities, saves and multiplayer

Identity must describe the final content set, not the user's sequence of menu operations. Build class positions from the vanilla baseline plus a fixed canonical ordering of the complete supported target. Recompute hash allocations in that same order; include collision outcomes, generated rows, behavior contract version and ID algorithm version in a sorted identity manifest. No literal custom enum numbers and no local append history.

The invariant is: **clean boot and every activation history leading to identical package bytes/settings produce identical identity manifests and effective content**. A random generation directory ID is not the compatibility fingerprint. Include framework/game hashes, package content hashes, dependencies, relevant settings, class slots and ID-map hash. Same version text with different bytes is a mismatch.

- V1 is new local adventures only. Freeze the selected generation at the earliest setup entry, before native UI remembers a class or equipment identity.
- Save creation needs a generation/identity stamp written consistently with the save. Prefer an atomic companion record if no verified native extension exists; missing or mismatched records fail closed for modded saves. Do not silently replace missing classes/items with vanilla defaults.
- An eventual load path must select the exact compatible set before deserialization, pin needed generations/assets, and verify the stamp. Legacy unstamped saves require an explicit compatibility policy and separate evidence. This investigation does not load them.
- Disabling/removing a package affects future adventures only. It never rewrites existing saves. Removal from the active selection and deletion of retained package bytes are separate operations.
- Initial hot-reload mode must block multiplayer entry. The current marketplace export explicitly describes incomplete manual/co-op coverage; it is not a handshake.
- Before multiplayer support, host and every client must compare the complete fingerprint before class selection, room gameplay properties or save deserialization. Freeze it for the whole room/run, include late joins/reconnects, reject mismatch, and never coordinate live activation inside a room. Equal final IDs alone do not prove equal behavior.

## Unsupported executable mods

[BehaviorLoader.cs](../FTKModFramework/Core/Data/BehaviorLoader.cs) uses `Assembly.LoadFrom`; behavior registration retains Types/factories and [BehaviorHost.cs](../FTKModFramework/Core/BehaviorHost.cs) creates persistent objects. There is no isolated execution domain, unload handle, subscription ledger, thread/coroutine cancellation contract or rollback of arbitrary static effects.

Consequently loading a DLL cannot be the reversible 'prepare' step. Destroying a component or removing its registry entry does not prove executable code and all effects are unloaded. A separate managed domain would require an entirely new marshaling/Unity ownership architecture and would still not make arbitrary game plugins safe. Do not pursue it for this slice.

A future cooperative behavior API may allow activation/deactivation of already-loaded framework-approved code with owned resources and explicit lifecycle callbacks. That is configuration/instance reload, not replacement of its assembly. Unknown BepInEx plugins and manual DLLs stay restart-required, and their presence prevents claiming a closed, fully audited runtime.

## Phased implementation and acceptance gates

| Phase | Bounded work | Measurable exit gate |
| --- | --- | --- |
| 0. Boundary instrumentation | Read-only lifecycle/consumer census in an isolated game copy; capture baseline before registration; history latch and fail-closed admission | All start/load/join routes close eligibility before constructing dependent state; title return never reopens it; no existing save or normal installation touched |
| 1. Transactional definition core | Pure target planning, deterministic identities, strict error collection, generation-owned rows/maps and failure injection | Every injected failure leaves the old manifest/maps/counts unchanged; clean-boot and history permutations match, including forced hash collisions and changed class ordering |
| 2. Paladin same-process proof | Only empty set and Paladin; Guardian plus GLB/PNG ownership; item/proficiency cache and UI adapters; helper journal/CAS; fail-closed admission; one fresh-adventure check | In one unchanged PID: install disabled, enable, update, disable, remove, reinstall; exact 39 authored/64 generated-inclusive rows when enabled from empty; zero Paladin rows/bindings when removed; same final IDs as clean boot; fresh adventure uses committed abilities/equipment/models |
| 3. Harden and release restricted mode | Broader fresh-run matrix, save stamp enforcement, crash recovery, boundary/admission fault injection, platform checks | Full matrix below passes on supported platform/build; user-visible success only after runtime and durable commit; no unproven multiplayer or return-to-title claims |
| 4. Expand by evidence | Audited dependency packages, more content kinds, optional cooperative resources, multiplayer handshake, finally post-adventure reset | Separate acceptance evidence for each expanded ownership/boundary contract; no blanket 'all data mods' switch |

The proof update should change a visible name/icon, one weapon stat, one Guardian bonus, and one GLB/PNG while also carrying unchanged assets into a different generation root. Include an update that removes an old capability and an invalid update that fails after partial publication. This proves replacement, unchanged-byte reuse, deletion and rollback, rather than just changing a version label.

## Verification matrix

All runtime tests use a disposable installation/profile and generated new adventures only. Fixed seeds and snapshots may improve repeatability but do not replace consumer assertions.

| Case | Required assertions |
| --- | --- |
| 100 enable/disable cycles before gameplay setup | Same PID throughout; exact row/map/host/cache counts each cycle; no duplicate IDs or lingering disabled Guardian membership; class slots match a clean boot |
| Install, update, remove, reinstall | Empty-to-Paladin and reverse; changed and unchanged assets across roots; removed fields/bindings disappear; old generation files remain pinned while leased |
| Fresh adventure after each final selection | Use separate test processes for distinct final selections because v1 closes the gate on first setup. Verify class visibility, starting gear, ability actions, all 36 gear definitions, icons/equipped/display/apparel models and acquisition pools; no missing-reference exceptions |
| Failed candidate preparation | Invalid JSON, missing dependency/reference, invalid mesh/icon, unsupported capability, hash mismatch: no publication and no leaked allocation |
| Failed activation/rollback | Inject at each row/map/index/cache/UI and durable-write phase; old content remains usable; rollback failure closes gameplay; lost helper acknowledgement resolves by transaction ID |
| Crash recovery | Terminate at every journal phase; next isolated launch selects the durable decision and never reports a partial generation as active |
| Stale references | Retain an instrumented old UI callback/row/renderer lease; activation rejects or retires it by contract; stale epoch callback cannot mutate new selection; no consumer points at old candidate after success |
| Native resource leaks | After warm-up and deferred destruction, zero retired owned meshes/materials/textures/sprites/hosts; active counts return to baseline. Track instance IDs and weak references. Sample native/managed memory every cycle and require no sustained upward trend; set a measured memory ceiling before release |
| Acquisition and proficiency caches | Verify loot and merchant candidates exclude removed items, modifiers match exact IDs, proficiency prefab cache points to current rows; initialization after activation also sees only current definitions |
| Identity histories | Clean A versus empty->A, A->B->A, forced hash collisions, class insertion/removal ordering, dependency closure changes: byte-identical final identity manifests |
| Boundary denial | Cancel character creation, return after a generated run, attempted save load, joining/hosting, async scene transition, live consumer lease, unknown plugin: no activation; existing runtime remains unchanged |
| Dependency transaction | Provider update/removal, disabled required dependency, cycle/version conflict, dependent reference change: either entire closure commits or none does |
| Save and multiplayer rules | Synthetic new save stamp matches exact generation; mismatch/missing stamp is rejected before deserialization; multiplayer entry blocked in v1; later handshake tests cover differing bytes/IDs/settings and reconnects |
| Native DB recreation | Recreate title under controlled test conditions: restoration ledger restores only committed rows with exact indices; it does not resurrect disabled/removed/failed candidates |
| Platform/build coverage | Run the supported macOS/Windows/Linux and assembly fingerprints separately; fail closed on unverified adapters. A build cannot establish Unity lifetime behavior |

Proposed latency gate for the Paladin proof: measure 20 warm local activations, excluding download, with p95 locked activation below two seconds on the reference machine. Record peak old-plus-new native memory. Treat this as a proposed budget to confirm in phase 0; correctness failures cannot be waived for speed.

## Verification performed for the initial investigation

This paragraph records the initial design-only pass. Subsequent implementation and
live results are tracked in [the prototype](HOT-RELOAD-PROTOTYPE.md) and
[live evidence](HOT-RELOAD-LIVE-EVIDENCE.md); those records supersede the initial
unexecuted status below.

Read-only framework/helper/package inspection and targeted installed-assembly decompilation were completed. The Paladin content counts were calculated from its current JSON. The document's relative links and whitespace were checked, and `git diff --check` passed for the existing tracked diff. Only this new design document was added by the investigation; no runtime implementation was changed. Builds and runtime tests were not run because this is a design-only change. Every live, leak, new-adventure, platform and multiplayer criterion above remains unexecuted.

## Decisions before implementation

1. Accept initial-title-only, new-local-adventure-only activation, with the boundary permanently closed on first setup/load/join attempt in that process.
2. Accept complete-set rebuild with canonical identities, rather than retaining disabled definitions and historical class slots.
3. Scope initial eligibility to Paladin and an otherwise controlled framework environment; explicitly block manual DLLs, other plugins, demo content and unsupported data capabilities.
4. Approve generation ownership and strict failure semantics as internal framework work, including resources and native cache adapters, rather than a Mods-menu-only feature.
5. Choose save stamping/retention policy and require fingerprint enforcement before any expansion to existing saves or multiplayer.
6. Approve the durable runtime/helper commit protocol, fail-closed recovery behavior, cycle/resource criteria and measured latency budget.

Recommendation: implement phases 0 through 2 as a bounded experiment, then make the release decision from its evidence. Do not begin universal plugin unloading or post-adventure teardown as part of that experiment.
