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

// Give a weapon a brand-new ability (its own prefab copy; the original is untouched)
Content.AttachProficiency(sword, "mymod_flamelash");
```

`Content.Db<T>()` fetches a content table and makes sure its index is built (needed because the
DB components' own `Awake`/`MakeIndex` may not have run yet at registration time). Use it whenever
you read a table directly:

```csharp
var classes = Content.Db<FTK_playerGameStartDB>();
var blacksmith = classes.GetEntry(FTK_playerGameStart.ID.blacksmith);
```

## 4. How it works (why it's safe)

- **IDs** — the `FTK_*.ID` enums are compile-time fixed. `IdAllocator` mints a deterministic
  synthetic int per `(modGuid, contentKey)` in a high band (`0x40000000+`), identical on every
  machine — so saves and co-op stay in sync. `DbLookupPatcher` + the `GetEnum` prefixes make the
  game's lookups resolve those synthetic ids.
- **Names** — `Localization` patches `GetLocalizedName` / `GetLocalizedDisplayName` to return the
  name you registered (the game otherwise reads from Google2u text tables it doesn't have).
- **Routing** — a patch on `FTK_itembase.GetItemBase` keeps custom items resolvable despite the
  `id >= 100000 -> weapon DB` rule.
- **Save-safety** — the framework sets `FullSerializer.fsConfig.SerializeEnumsAsInteger = true`.

## 5. Multiplayer

Co-op is Photon and has **no asset streaming** — every player must have the same mods installed.
Synthetic IDs are deterministic precisely so host/client agree on what each id means.

## 6. Content tables

The full inventory of the 57 `FTK_*DB` tables (items, weapons, proficiencies, hit effects,
classes, skinsets, enemies, realms, encounters, quests, ...) is in
[`PHASE0-TYPE-INVENTORY.md`](PHASE0-TYPE-INVENTORY.md). Enemy and class helpers are on the roadmap;
until then you can register into any table directly with `ContentRegistry.Register(db, guid, id, template, configure)`.
