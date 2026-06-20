# Adventures & encounters

How For The King's adventure/game-mode system works, and how this framework adds to it. This is the
design reference; for the short API recipe see [`WRITING-CONTENT.md`](WRITING-CONTENT.md). To author the
*questline inside* an adventure (multi-stage quests, branching, custom objective verbs), see
[`CAMPAIGNS.md`](CAMPAIGNS.md).

## How FTK models an "adventure"

An adventure (Dungeon Crawl, Pirates, Frost Adventure, …) is **not** a row in a `FTK_*DB` GridEditor
table like items/classes/encounters are. It is a **`GameDefinition`** object **deserialized from a
`.ftk2` JSON file** under the game's `StreamingAssets/mods/`, keyed by its string `m_SaveFileName`.

- **Loading.** `GameCache.Cache.GameDefinitions.Initialize()` globs `streamingAssetsPath/mods/**/*.ftk2`
  (Newtonsoft, `StringEnumConverter`), builds a lightweight `GameDefinitionPreview` per file (keeping the
  raw JSON in `m_FullFileData`), and stores them in `_previews[m_SaveFileName]`. The stock adventures
  ship as editable `.ftk2` files on disk (e.g. `DungeonCrawl.ftk2`).
- **Selection UI.** `StartGameFE.GameConfig.Show()` lists `Cache.GameDefinitions.GetNames()`, but gates
  each name through one **hardcoded whitelist** — `FTKHub.IsValidSaveFileName` — so an unknown name is
  silently dropped. The list is string-keyed and ordered by `m_SelectionPriority`; there is **no**
  id==array-index constraint (unlike playable classes).
- **Starting a run.** On start, `GetNewGameDefInstance()` re-deserializes the full `GameDefinition` from
  `m_FullFileData` (`TypeNameHandling=Auto`) and `GameLogic.SetActiveGameDef(...)` makes it the live run.
- **Generation is data-driven.** The overworld generator (`GenerateHexGrid` / `GameDefinition`
  `GetNewPreGenMapResults` / `FTKHex.GenerateRealmPOIs`) reads only the `GameDefinition`
  (`m_Stages[].m_RealmStages`, `m_MapLayoutOptions`) and `FTK_realmDB` rows. There is **no per-adventure
  branch** in the core loop, so a new adventure built from existing realms needs no generator patch.
- **Win condition** is per-`GameDefinition` data: completing the last quest of the last stage fires the
  victory path; `m_EndGameAfterLastQuest` / `m_EndGameOnFullChaos` decide the end. No dedicated
  final-boss enum — the final boss is whatever enemy that last quest points at.
- **Saves & co-op.** A save stores the adventure as the **string** `m_GameDefinition` (= `m_SaveFileName`),
  so adding a new adventure can't collide with or shift any enum ids (low save risk). Co-op carries only
  `[map seed, gamedef-name string, difficulty, rules]` from host to clients; the adventure must exist
  on every client (the engine only checks a version string, not a content hash). Resuming a custom-
  adventure save without the mod throws — the run is unloadable until the mod is reinstalled (not corrupt).

## How the framework adds one

`FTKModFramework.Core.Adventures.AddFromTemplate(modGuid, saveFileName, templateSaveFileName,
displayName, infoText, configureJson)`:

1. **Clones at runtime, ships nothing.** It reads the player's own installed `templateSaveFileName`.ftk2
   (e.g. `DungeonCrawl`), edits a few fields on the JSON via Newtonsoft `JObject`, and reuses the
   template's mod folder so the new adventure inherits the template's preview art. No game content is
   redistributed.
2. **Registers the preview** exactly as the game's loader does (`StringEnumConverter`, set
   `m_FullFileData`), and injects it into `Cache.GameDefinitions._previews`.
3. **Three Harmony hooks** make it appear and stay:
   - postfix `Cache.GameDefinitions.Initialize` and prefix `StartGameFE.GameConfig.Show` →
     `EnsureLoaded()` (idempotent re-inject, robust to load order),
   - postfix `FTKHub.IsValidSaveFileName` → return `true` for registered names (**the one required gate**).

`displayName` / `infoText` are shown verbatim: the game's `FTKHub.Localized<TextMenu>` returns the key
itself when there's no text row, so no localization-table edit is needed.

```csharp
using FTKModFramework.Core;
using Newtonsoft.Json.Linq;

Adventures.AddFromTemplate(
    "com.you.mymod", "MyRun", "DungeonCrawl",
    "My Run", "A richer romp across Fahrul.",
    jo => { jo["m_GoldMultiplier"] = 1.5; jo["m_SelectionPriority"] = 250; });
```

## Encounters (the easy win)

An overworld encounter/event **is** a GE row (`FTK_miniEncounterDB`), so it injects with the normal
clone-register path. `GameLogic.GetMiniEncounter` walks the whole table each spawn turn and weight-rolls
every eligible row, so a registered row is automatically a candidate — no generator patch.

- Empty `m_RealmInclude` ⇒ eligible in every realm; `m_RealmExclude` to subtract.
- `m_Rarity` is a string key into `FTK_encounterDrawChanceDB` — reuse `Common`/`Uncommon`/`Rare`/`SuperRare`
  (measured weights `1 / 0.5 / 0.25 / 0.1`); no new draw-chance row needed.
- `m_DisplayName`/`m_DisplayTop`/`m_DisplayBottom` show verbatim (same key-passthrough as above).
- Selection is host-authoritative and replicated by RPC, so all clients agree — but every client must
  have the row under the same synthetic id (the framework's `IdAllocator` guarantees this).

```csharp
Content.AddEncounter("com.you.mymod", "mymod_cache", FTK_miniEncounter.ID.TreasureChest, "Hidden Cache",
    e => { e.m_Rarity = "Common"; e.m_RealmInclude = new FTK_realm.ID[0]; });
```

## Character-create guard

Registering any custom **class** also installs a guard on `uiQuickPlayerCreate.CanUseClass(int)`. The
vanilla method does a raw `m_Array[classId]`; an out-of-range lobby class id throws inside `Awake`,
half-builds the party UI, and cascades into a `RemoveCharacter` list overflow (the "character-create
screen is bugged" crash). The guard bounds-checks the id and returns `false` for out-of-range values, so
the game falls back to its own `Default_Classes` instead of crashing.

## What's verified, and what's next

**Verified in-game (solo):** a custom encounter resolves through every lookup path and sits in the live
draw pool; a cloned adventure ("Smuggler's Run") is selectable, generates a full overworld, and starts a
real run with its retuned rules.

**Shipped (Slice D1, solo): a bespoke custom realm + boss.** The FR-1 gating spike PASSED in-game
(`SELF-TEST PASS [realm-spike]`): a synthetic realm id round-trips as the `m_RealmStages` dictionary KEY
when written as its decimal integer, and resolves through the game's own `GetRealmProperties`. So the
**bespoke-realm path is in use** (not a vanilla-realm fallback). The demo ("The Hollow Mire") is a single
self-contained adventure with its own realm (cloned from PoisonBog, flagged `m_GameStartRealm`), its own
boss ("Mudwretch Foreman", a cloned enemy with signature procs), and a true-victory questline. One enemy
set serves both the overworld set-piece (`RealmProperties.m_BossEnemy`) and the final boss bounty (the
last quest of the last stage, with `m_EndGameAfterLastQuest = true`). Load-time self-tests
(`SELF-TEST PASS [realm-boss-set]` and `SELF-TEST PASS [realm-boss]`) confirm the boss/set/realm resolve,
the set-piece boss is wired into the realm, and the final quest is a boss bounty in the custom realm; the
full playthrough-to-victory is a manual in-game gate.

**Not yet done (Slice D2):** 2-client co-op (no desync) and a save round-trip for the bespoke realm/boss.
Open question to confirm: the overworld map-sync mechanism (host-authoritative scene-object spawn vs.
deterministic re-generation from the shared seed) and a host/client mod-set parity check.
