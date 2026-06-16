# FTK Mod Framework

A content-modding framework for **For The King** (the original 2018 IronOak game), built on
BepInEx 5 + HarmonyX. Goal: let modders add new **classes, items, combat actions, enemies, and
adventures** through one clean, save-safe, multiplayer-deterministic API — and serve as the base
for porting *For The King II* class/ability ideas back into the original game.

> Status: **actively developed.** Items, weapons, combat actions, and **playable classes** all work
> and are verified in-game — see the bundled **Thief** class (custom stats, a dagger, custom
> abilities, and a Focus-guaranteeable Steal). Enemies and adventures are next. See
> [`docs/WRITING-CONTENT.md`](docs/WRITING-CONTENT.md) for the modder API,
> [`docs/PHASE0-TYPE-INVENTORY.md`](docs/PHASE0-TYPE-INVENTORY.md) for the game data model, and
> [`docs/ROADMAP.md`](docs/ROADMAP.md) for the plan.

## Verified facts (from this machine's install)

| Thing | Value |
|---|---|
| Engine | Unity **2017.2.2p2** |
| Scripting backend | **Mono / .NET 3.5** (managed `Assembly-CSharp.dll`; no IL2CPP) |
| Mod loader | BepInEx **5.4.x** (Mono x64) |
| Patching | HarmonyX (`HarmonyLib`) |
| Content model | `GridEditor.TableManager` → `FTK_*DB` data tables (clone-and-register) |
| Install (this Mac) | `~/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/Managed` |
| Toolchain present | `dotnet` 10.0.301, `ilspycmd` 10.1.0 |

## Repo layout

```
FTKModFramework/
  FTKModFramework.csproj   net35 plugin; references + publicizes the game's Assembly-CSharp
  Plugin.cs                BepInEx entry point + the single TableManager.Initialize content hook
  Core/
    Content.cs             high-level API: AddWeapon / AddItem / AddProficiency / AddClass / AttachProficiencies
    ContentRegistry.cs     generic "add a row to any FTK_*DB" engine
    IdAllocator.cs         deterministic synthetic enum-IDs (save + multiplayer stable)
    DbLookupPatcher.cs     Harmony patches so the game resolves our custom string IDs
    EnumPatches.cs         GetEnum prefixes (items / proficiencies / classes) for custom IDs
    Localization.cs        custom names, class flavor text, and proficiency tooltip text
    Reflect.cs             reflection helpers (field copy, etc.)
  Content/
    SampleContent.cs       demo: a custom weapon ("Emberbrand") that casts a custom ability
    ThiefClass.cs          the Thief — a full custom class (stats, dagger, abilities, Steal)
    ThiefStealProficiency.cs  custom combat behaviour (ProficiencyBase subclass) powering Steal
docs/
  WRITING-CONTENT.md        modder API guide (how to add content)
  PHASE0-TYPE-INVENTORY.md  the full content-table inventory decompiled from the game
  ROADMAP.md                phased plan toward the five content goals + FTK2 ports
nuget.config                adds the BepInEx NuGet feed
```

## Build

Requires the .NET SDK (already installed here).

```bash
cd FTKModFramework
dotnet build -c Release
# -> bin/Release/net35/FTKModFramework.dll
```

On another machine / OS, point the build at your game's managed folder:

```bash
dotnet build -c Release -p:FtkManagedDir="C:\Program Files (x86)\Steam\steamapps\common\For The King\FTK_Data\Managed"
```

The build references the game's own DLLs and **publicizes** `Assembly-CSharp` at compile time, so we
can read private fields like `FTK_itemsDB.m_Array`. Game DLLs are never copied into our output and
are git-ignored (they're copyrighted — reference them from the install).

## Install & run

1. Install the **BepInExPack for For The King** (Thunderstore) into the game so a `BepInEx/` folder
   sits next to the executable. (Easiest via the r2modman / Thunderstore mod manager.)
2. Also install **Amadare-HookGenPatcher** (needed once we use `On.*` MonoMod hooks).
3. Copy `FTKModFramework.dll` into `<game>/BepInEx/plugins/`.
4. Launch. Check `BepInEx/LogOutput.log` for `FTK Mod Framework ... loaded` and the
   `SELF-TEST PASS` lines. With the demo enabled, the **Thief** appears at character-select and the
   "Emberbrand" weapon is in the Blacksmith's starting kit.

> **macOS note:** BepInEx on the Mac Unity-Mono build uses `run_bepinex.sh` + a Doorstop dylib
> rather than the Windows `winhttp.dll`. It works, but the FTK community packs are Windows-first, so
> a Windows install (or a VM) is the smoother path for *testing*. The framework DLL itself is
> platform-agnostic managed IL. Also note: **co-op requires every player to have identical mods**
> (no asset streaming), which is why `IdAllocator` makes IDs deterministic across machines.

## Using the framework (writing a content mod)

Depend on this plugin and register content from a single hook. Full guide:
[`docs/WRITING-CONTENT.md`](docs/WRITING-CONTENT.md).

```csharp
[HarmonyPatch(typeof(GridEditor.TableManager), "Initialize")]
static class Register
{
    static bool _done;
    static void Postfix()
    {
        if (_done) return; _done = true;
        var sword = Content.AddWeapon("com.you.mymod", "mymod_flamesword",
            FTK_itembase.ID.bladeShortsword, "Flame Sword",
            w => { w._maxdmg += 5f; w.m_ItemRarity = FTK_itemRarityLevel.ID.rare; });
        Content.AddProficiency("com.you.mymod", "mymod_flamelash",
            FTK_proficiencyTable.ID.fire1, "Flame Lash", p => p.m_DmgMultiplier = 1.5f);
        Content.AttachProficiency(sword, "mymod_flamelash");

        // ...and a playable class (cloned from the Gladiator):
        Content.AddClass("com.you.mymod", "mymod_blademaster",
            FTK_playerGameStart.ID.gladiator, "Blademaster",
            c => { c._quickness = 0.7f; c._toughness = 0.7f;
                   c.m_StartWeapon = FTK_itembase.ID.bladeShortsword; });
    }
}
```

The bundled content is a working reference: `Content/SampleContent.cs` (a custom weapon + ability)
and `Content/ThiefClass.cs` (a full custom class, including a custom-behaviour `ProficiencyBase` in
`ThiefStealProficiency.cs`). Both are gated behind the `Demo / EnableSampleContent` config (set it
false to use the framework purely as a dependency for other mods).

## The five goals — where each stands

| Goal | DB / types | Status |
|---|---|---|
| New items / weapons | `FTK_itemsDB`, `FTK_weaponStats2DB` | ✅ working + verified in-game |
| New combat actions | `FTK_proficiencyTableDB`, `FTK_hitEffectDB` | ✅ working (create + attach to weapons; custom `ProficiencyBase` behaviours) |
| New **classes** | `FTK_playerGameStartDB`, `FTK_skinsetDB` | ✅ working + verified (`Content.AddClass`; the **Thief**) |
| New enemies | `FTK_enemyCombatDB`, `FTK_enemySetDB` | DBs identified; helper next |
| New adventures | `FTK_realmDB`, `FTK_gameParamsDB`, encounter DBs | DBs identified; hardest, RE needed |

## Credits / prior art this builds on

- **FTKAPI** (Amadare / ftk-modding) and **FTKModLib** (lulzsun) — the existing FTK modding APIs.
- **CommunityDLC** (Theta_Hat_Society / Dehydrated-Mud) — the worked example of a custom class.
- Decompilation via **ILSpy**; loader **BepInEx**; patching **HarmonyX**.
