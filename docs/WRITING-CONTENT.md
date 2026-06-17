# Writing content with FTK Mod Framework

This framework is a BepInEx 5 plugin that exposes a small API for adding content to
**For The King** without touching game files. You write your own BepInEx plugin, depend on
this one, and register content from a single hook.

## 1. Project setup

A net35 BepInEx 5 plugin (see `FTKModFramework.csproj` for the reference setup). Add a
reference to `FTKModFramework.dll` and to the game's publicized `Assembly-CSharp`.

```csharp
[BepInPlugin("com.you.mymod", "My Mod", "1.0.0")]
[BepInDependency("com.ftkmf.framework")]   // depend on the framework
public class MyMod : BaseUnityPlugin
{
    internal static MyMod Instance;
    void Awake() { Instance = this; new Harmony("com.you.mymod").PatchAll(); }
}
```

## 2. The one hook: `TableManager.Initialize`

All content tables are populated by the time `TableManager.Initialize` returns, so register
everything from a postfix on it:

```csharp
[HarmonyPatch(typeof(GridEditor.TableManager), "Initialize")]
static class Register
{
    static bool _done;
    static void Postfix()
    {
        if (_done) return; _done = true;        // Initialize can run more than once
        // ... your Content.AddX calls ...
    }
}
```

## 3. The API (`FTKModFramework.Core.Content`)

Every helper clones an existing entry (so you inherit a valid icon/prefab/animations), lets you
tweak fields, and registers a display name. Pass YOUR plugin GUID so IDs never clash between mods.

```csharp
using FTKModFramework.Core;
using GridEditor;

// A weapon (cloned from the Shortsword)
FTK_weaponStats2 sword = Content.AddWeapon(
    "com.you.mymod", "mymod_flamesword", FTK_itembase.ID.bladeShortsword, "Flame Sword",
    w => { w._maxdmg += 5f; w.m_ItemRarity = FTK_itemRarityLevel.ID.rare; w.m_TownMarket = true; });

// A consumable / non-weapon item (cloned from an existing item)
FTK_items potion = Content.AddItem(
    "com.you.mymod", "mymod_megapotion", FTK_itembase.ID.healthPotion, "Mega Potion",
    i => { i._goldValue = 200; });

// A combat action / ability (cloned from an existing proficiency)
FTK_proficiencyTable lash = Content.AddProficiency(
    "com.you.mymod", "mymod_flamelash", FTK_proficiencyTable.ID.fire1, "Flame Lash",
    p => { p.m_DmgMultiplier = 1.5f; p.m_IgnoresArmor = true; });

// Give a weapon one or more brand-new abilities (its own prefab copy; the original is untouched)
Content.AttachProficiencies(sword, "mymod_flamelash" /*, "mymod_backstab", ... */);

// A playable class (cloned from the Gladiator). See §4 for the details that matter.
FTK_playerGameStart cls = Content.AddClass(
    "com.you.mymod", "mymod_blademaster", FTK_playerGameStart.ID.gladiator, "Blademaster",
    c => {
        c._quickness = 0.7f; c._toughness = 0.7f; c._vitality = 0.6f; // stats are floats ~0.3-0.8
        c.m_StartWeapon = (FTK_itembase.ID)Content.Db<FTK_weaponStats2DB>().GetIntFromID("mymod_flamesword");
        c.m_StartItems = new[] { FTK_itembase.ID.armorMagicLeather };
        c.m_DLC = FTK_dlc.ID.None; c.m_Release = true; // keep it unlocked on all build types
    });
Localization.SetClassFlavor("mymod_blademaster", "A relentless duelist who lives by the blade.");
```

`Content.Db<T>()` fetches a content table and makes sure its index is built (needed because the
DB components' own `Awake`/`MakeIndex` may not have run yet at registration time). Use it whenever
you read a table directly:

```csharp
var classes = Content.Db<FTK_playerGameStartDB>();
var blacksmith = classes.GetEntry(FTK_playerGameStart.ID.blacksmith);
```

## 4. Playable classes — things to know

`Content.AddClass` clones an existing class's `FTK_playerGameStart` row (so you inherit a valid 3D
model/skinset, portrait, and a sane field layout) and registers it. The character-select roster is
DB-driven, so your class appears automatically — but mind these:

- **It's id == array index.** Unlike other content (high-band synthetic ids), a class is registered
  with the next sequential enum value, because character-select uses the id as *both* an enum key and
  an array index. `AddClass` does this for you; just don't try to force a different id.
- **Stats** are floats, roughly `0.30–0.80`, displayed ×100. Fields: `_toughness` (Strength),
  `_fortitude` (Intelligence), `_awareness`, `_talent`, `_quickness` (Speed), `_vitality`. There is
  **no per-class Luck** (Luck is global). `_basefocus` (1–9) and `_startinggold` round it out.
- **Difficulty** adds a flat bonus to *every* class equally (Apprentice +5, Journeyman/Master 0), so
  one stat block is correct on all difficulties — don't try to tune per difficulty.
- **Availability:** keep `m_DLC = FTK_dlc.ID.None` and `m_Release = true`; add no lore-unlock entry and
  the class is unlocked + visible by default.
- **Model/portrait:** reuse an existing `m_Skinsets` (cloned). Custom voxel models need a
  Unity 2017.2.2 AssetBundle and aren't wrapped by the framework yet.
- **Name & flavor:** the display name is the 4th `AddClass` arg; set the description with
  `Localization.SetClassFlavor(id, "...")`.

## 5. Custom combat behaviour (a `ProficiencyBase` subclass)

Cloning a proficiency reuses an existing effect. For *new* behaviour, subclass `ProficiencyBase`,
override `AddToDummy`, and set an instance as the row's `m_ProficiencyPrefab` (the game
`Instantiate`s it via `ProficiencyManager`). `GetAttacker(_dummy)` is the user; `_dummy` is the target.

```csharp
public class MyZap : ProficiencyBase
{
    public override void AddToDummy(CharacterDummy _dummy)
    {
        var attacker = GetAttacker(_dummy);
        // ...do something: grant gold/items, buff the attacker, read the enemy's loot table, etc.
    }
}
// register: AddProficiency(..., p => p.m_ProficiencyPrefab = go.AddComponent<MyZap>());
```

Gotchas (learned the hard way building the Thief's Steal):
- A **0-damage** proficiency is auto-cancelled unless flagged `m_Harmless` — but `m_Harmless` then
  makes it ignore the slot roll. To make the **roll itself the gate** (so spending Focus guarantees
  it), give it a tiny chip of damage with **`m_IgnoresArmor = true`** (else armor reduces the chip to
  0 and re-blocks it).
- `m_SlotOverride = 1` makes it a single roll; `m_PerSlotSkillRoll` lowers the per-slot accuracy;
  `m_ChanceToAffect` is a separate flat apply-chance.
- For steal-category HUD, set `_dummy.m_DamageInfo.m_ProfHasAmount = true` on success (else the game
  shows "Nothing To Steal").

See `Content/ThiefStealProficiency.cs` for the full worked example.

## 6. Enemies

`Content.AddEnemy` clones an existing enemy's `FTK_enemyCombat` row (so you inherit a valid 3D body,
weapon, and animations) and registers it with a high-band synthetic id. Unlike classes, enemies are
**not** id == array index — every enemy lookup is dictionary/string-based, and selection round-trips the
id through its decimal string over Photon. After registering, `AddEnemy` flips
`GameCache.Enemies.NeedsRebuild` so the game's level-bucketed spawn pool re-reads the DB and your enemy
becomes eligible for ordinary overworld/dungeon fights — **no spawn-selection patch needed.**

```csharp
FTK_enemyCombat cutpurse = Content.AddEnemy(
    "com.you.mymod", "mymod_cutpurse", FTK_enemyCombat.ID.banditA, "Cutpurse",
    e => {
        e.m_EnemyLevel = 1;                          // which level bucket it spawns in
        e.m_HealthTotal = 26; e.m_EvadeRating = 0.20f;
        e.m_ArchType = FTK_enemyCombat.EnemyArchType.Evade;
        e.m_ChanceToProf = 0.5f;                     // how often it uses a proficiency vs a normal attack
        e.m_Rarity = "Common";                       // draw weight (FTK_encounterDrawChanceDB)
        e.m_SpawnDay = e.m_SpawnNight = e.m_SpawnLand = e.m_SpawnDungeon = true;
        e.m_RealmInclude = new FTK_realm.ID[0];      // empty => eligible in every realm
        // custom loot: AddEnemy deep-copies the cloned row's m_ItemDrops, so edit it directly
        e.m_ItemDrops._golddrop = 25;
        e.m_ItemDrops.m_AlwaysDropItems = new[] { FTK_itembase.ID.conLockpicks };
    });
Localization.SetEnemyDescription("mymod_cutpurse", "A nimble thief who robs the unwary.");

// give it a custom/cloned attack (its own private weapon copy; vanilla enemies untouched)
Content.AttachEnemyProficiencies(cutpurse, "mymod_pilfer");
```

Things that matter:
- **It must pass the spawn-pool filter or it's silently dropped:** clone a template that is **not a boss,
  not a scourge, and not in `FTK_enemyScaleDB`**, and keep its `m_EnemyAsset` non-null.
- **`m_EnemyAsset`** (a `CharacterEventListener`) is the 3D body and **`m_WeaponAsset`** (a `Weapon`
  component) carries the attacks — both are reference fields, so cloning reuses them and the enemy renders
  and fights for free. `m_ArchType` is only a *stat* archetype, not the model.
- **Abilities:** `AttachEnemyProficiencies` instantiates a private copy of `m_WeaponAsset`, adds your
  proficiency, strips any `AttackSchedule` (so the RNG attack path can pick it), and `SaveState()`s it.
  Set `m_ChanceToProf > 0` so the AI actually fires it. A custom `ProficiencyBase` behaviour (§5) works
  when the enemy is the attacker — guard any shared-state mutation (gold, etc.) with
  `PhotonNetwork.isMasterClient` so co-op applies it once.
- **Spawn gating:** `m_EnemyLevel` (which bucket), `m_Rarity` (draw weight), `m_SpawnDay/Night/Land/Water/Dungeon`,
  and `m_RealmInclude`/`m_RealmExclude` decide *where/when* it appears. Cloning a template that already
  spawns gives sane defaults.
- **Multiplayer:** enemy spawns are master-authoritative and cross the wire as the enemy's id *string*;
  the deterministic synthetic id round-trips, so co-op stays in sync as long as every player has the mod.

There's also a DEBUG config (`Enemies/ForceCustomEnemy`) that replaces every overworld land enemy with the
Cutpurse, so you can verify a custom enemy fights and drops loot without waiting on the weighted draw.

See `Content/CutpurseEnemy.cs` (+ `Content/CutpurseStealProficiency.cs`) for the full worked example.

## 7. New adventures & encounters

> Design reference + the full how-it-works: [`ADVENTURES.md`](ADVENTURES.md).

A whole **adventure / game-mode** is not a DB row — it's a `GameDefinition` deserialized from a
`.ftk2` JSON file in the game's `StreamingAssets/mods`. `Adventures.AddFromTemplate` clones one of the
player's *installed* adventures at runtime (so it ships no game content), retunes a few JSON fields, and
registers it. The one required Harmony patch whitelists the name through `FTKHub.IsValidSaveFileName`
(the single hardcoded gate the start screen checks). World generation, win condition, and saves are all
data-driven off the cloned definition, so an adventure built from existing realms needs **no** generator
patch and appears on the start screen automatically.

```csharp
using FTKModFramework.Core;
using Newtonsoft.Json.Linq;

// A new selectable adventure, cloned from the installed DungeonCrawl and retuned.
Adventures.AddFromTemplate(
    "com.you.mymod", "MyRun", "DungeonCrawl",
    "My Run", "A richer romp across Fahrul.",
    jo => { jo["m_GoldMultiplier"] = 1.5; jo["m_SelectionPriority"] = 250; });
```

A new **overworld encounter/event** *is* a DB row (`FTK_miniEncounterDB`), so it injects with the same
clone-register pattern as items. The selector (`GameLogic.GetMiniEncounter`) walks the whole table and
weight-rolls every eligible row, so a freshly registered one is automatically a candidate — no generator
patch. An empty `m_RealmInclude` means "every realm"; `m_Rarity` reuses an existing draw-chance bucket
(`Common`/`Uncommon`/`Rare`/`SuperRare`); display strings show verbatim (the game's text lookup returns
the key itself when it has no row).

```csharp
Content.AddEncounter("com.you.mymod", "mymod_cache", FTK_miniEncounter.ID.TreasureChest, "Hidden Cache",
    e => { e.m_Rarity = "Common"; e.m_RealmInclude = new FTK_realm.ID[0]; });
```

> Registering any custom **class** also installs a small guard on `uiQuickPlayerCreate.CanUseClass`: an
> out-of-range class id in the party lobby falls back to a default class (the game's own intent) instead
> of throwing and breaking the character-create screen.

## 8. How it works (why it's safe)

- **IDs** — the `FTK_*.ID` enums are compile-time fixed. `IdAllocator` mints a deterministic
  synthetic int per `(modGuid, contentKey)` in a high band (`0x40000000+`), identical on every
  machine — so saves and co-op stay in sync. `DbLookupPatcher` + the `GetEnum` prefixes make the
  game's lookups resolve those synthetic ids.
- **Names & text** — `Localization` patches the game's text lookups (item/weapon `GetLocalizedName`,
  proficiency `GetLocalizedDisplayName`/`DisplayTitle`, class `GetDisplayName`, class flavor, enemy
  `GetEnemyDisplay`/`GetEnemyDescription`, and proficiency tooltip descriptions) to return what you
  registered — the game otherwise reads from Google2u text tables it doesn't have entries for.
- **Routing** — a patch on `FTK_itembase.GetItemBase` keeps custom items resolvable despite the
  `id >= 100000 -> weapon DB` rule.
- **Save-safety** — the framework sets `FullSerializer.fsConfig.SerializeEnumsAsInteger = true`.

## 9. Multiplayer

Co-op is Photon and has **no asset streaming** — every player must have the same mods installed.
Synthetic IDs are deterministic precisely so host/client agree on what each id means.

## 10. Content tables

The full inventory of the 57 `FTK_*DB` tables (items, weapons, proficiencies, hit effects,
classes, skinsets, enemies, realms, encounters, quests, ...) is in
[`PHASE0-TYPE-INVENTORY.md`](PHASE0-TYPE-INVENTORY.md). Helpers exist for items, weapons,
proficiencies, classes, enemies (`Content.AddEnemy`), overworld encounters (`Content.AddEncounter`),
and whole adventures (`Adventures.AddFromTemplate`). For any other table you can register directly
with `ContentRegistry.Register(db, guid, id, template, configure)`.
