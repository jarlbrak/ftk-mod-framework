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

## 6. How it works (why it's safe)

- **IDs** — the `FTK_*.ID` enums are compile-time fixed. `IdAllocator` mints a deterministic
  synthetic int per `(modGuid, contentKey)` in a high band (`0x40000000+`), identical on every
  machine — so saves and co-op stay in sync. `DbLookupPatcher` + the `GetEnum` prefixes make the
  game's lookups resolve those synthetic ids.
- **Names & text** — `Localization` patches the game's text lookups (item/weapon `GetLocalizedName`,
  proficiency `GetLocalizedDisplayName`/`DisplayTitle`, class `GetDisplayName`, class flavor, and
  proficiency tooltip descriptions) to return what you registered — the game otherwise reads from
  Google2u text tables it doesn't have entries for.
- **Routing** — a patch on `FTK_itembase.GetItemBase` keeps custom items resolvable despite the
  `id >= 100000 -> weapon DB` rule.
- **Save-safety** — the framework sets `FullSerializer.fsConfig.SerializeEnumsAsInteger = true`.

## 7. Multiplayer

Co-op is Photon and has **no asset streaming** — every player must have the same mods installed.
Synthetic IDs are deterministic precisely so host/client agree on what each id means.

## 8. Content tables

The full inventory of the 57 `FTK_*DB` tables (items, weapons, proficiencies, hit effects,
classes, skinsets, enemies, realms, encounters, quests, ...) is in
[`PHASE0-TYPE-INVENTORY.md`](PHASE0-TYPE-INVENTORY.md). Helpers exist for items, weapons,
proficiencies, and classes; an enemy helper is next. For any other table you can register directly
with `ContentRegistry.Register(db, guid, id, template, configure)`.
