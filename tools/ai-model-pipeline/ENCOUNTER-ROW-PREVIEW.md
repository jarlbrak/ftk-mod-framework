# Explicit mesh encounter-row previews

The row overload of native `OffscreenCamera.Snapshot` resolves the row's source CEL and clones it directly. It does not run `EnemyDummy.InitEnemyDummyForCombat`, where combat meshes are assigned. Consequently a row preview could render native geometry while a combat HUD portrait correctly cloned the already-customized avatar. Similar pixels alone do not establish a cache failure.

The framework now applies an existing explicit renderer plan to the distinct, scene-owned offscreen clone in the exact `InstantiateTarget(CharacterEventListener, DisplayLayer, int, bool)` postfix. This runs after native portrait pose sampling and before native positioning/rendering. Authority requires the exact registered custom DB row, direct row-to-source CEL forwarding, the same source CEL, and target `m_OffscreenCamera` ownership. Camera-marker registration is optional. Unresolved nested scopes clear authority; vanilla and direct live-avatar captures cannot initiate this row-preview application.

The strict all-or-nothing mesh transaction prepares GLB, authored PNG, descriptor emission options, and existing registered tint/wet material settings on private target material copies before any renderer commit. Combat uses the same material preparation. The existing weapon exclusion still applies to tint/wet settings. Unlisted preview renderers and native shared materials are untouched. Preparation failure releases new allocations; commit failure restores the original mesh, bone references, bounds and material references across the whole selected set. An applied retained lease short-circuits without decoding or recoloring inherited assets.

No whole visual patch runs in the portrait path: native pose, transforms, scale, skeleton, camera markers, hunch, procedural effects and legacy model behavior remain outside this fix. No fake dummy or new public option is needed. Changes to registrations affect subsequent avatar/preview creation; retained live-avatar resources remain as created.

Local regression commands:

```sh
dotnet run --project tools/ai-model-pipeline/portrait-tests/PortraitTests.csproj -p:TestGameRoot=/absolute/path/to/test-game
dotnet run --project tools/ai-model-pipeline/mesh-transaction-tests/MeshTransactionTests.csproj
dotnet run --project tools/ai-model-pipeline/material-option-tests/MaterialOptionTests.csproj -p:TestGameRoot=/absolute/path/to/test-game
dotnet build FTKModFramework/FTKModFramework.csproj -c Release
```

The scope suite links the real registry and Harmony patches with DB/Unity stand-ins. The transaction suite links the actual transaction and lease implementation, injects preparation/late-decode/commit failures, checks native references and pose/bounds preservation, and exercises serialized clone retention, missing leases, last-owner cleanup and destroyed inactive-owner pruning. These are not Unity rendering proof.

The isolated constructed-native-UI fixture now passes three scoped cases: Honeyback with its original marker, Honeyback with `CameraRoot/EncounterCam`, and multipart Vesper Eye. Each invokes the actual `uiEnemyEncounterPortrait.Initialize(string)` once using the native UI portrait prefab. The native fixed child rectangle is82×70 with AA4, yielding328×280; combat HUD portraits use204×172. The first case creates the normal native cached camera, and the next two reuse its exact identity without replacing existing cache entries. All cases record the exact custom GLBs, unchanged source prefab, unchanged native Ready state, and disposal of every pinned temporary mesh/material/texture plus the UI, portrait texture and clone. The persistent native camera and RenderTexture are retained normally and excluded from disposal claims. See the [pinned evidence and three original-model PNGs](../../docs/evidence/native-row-preview-v2/validation.json).

Honeyback's original marker remains a visual framing failure, despite correct custom mesh loading; the tested alternate marker and Vesper portraits pass their scoped visual review. The trace records material/texture identity, not emission keyword/color properties. These are constructed native UI caller tests, not an opened encounter menu. A real opened-menu smoke test and a vanilla preview remain pending; the offline vanilla/unknown-scope rejection tests are not live rendering evidence. Existing combat-HUD resource-reuse evidence remains separately indexed. No full visual-patch parity, all-menu or all-profile acceptance is implied.
