<p align="center">
  <img src="assets/brand/ftk-logo.svg" alt="FTK Mod Framework" width="620">
</p>

<p align="center">
  <a href="LICENSE"><img src="https://img.shields.io/badge/License-MIT-yellow.svg" alt="License: MIT"></a>
  <a href="https://github.com/jarlbrak/ftk-mod-framework/actions/workflows/ci.yml"><img src="https://github.com/jarlbrak/ftk-mod-framework/actions/workflows/ci.yml/badge.svg" alt="CI"></a>
  <a href="https://github.com/jarlbrak/ftk-mod-framework/discussions"><img src="https://img.shields.io/badge/Discussions-join-1f883d.svg" alt="Discussions"></a>
  <img src="https://img.shields.io/badge/BepInEx-5.4.x-blue.svg" alt="BepInEx 5.4.x">
  <img src="https://img.shields.io/badge/For%20The%20King-2018-8a5a2b.svg" alt="For The King (2018)">
  <img src="https://img.shields.io/badge/.NET-3.5%20%2F%20Mono-512bd4.svg" alt=".NET 3.5 / Mono">
</p>

**A content-modding framework for [For The King](https://store.steampowered.com/app/527230/) (the original 2018 IronOak game), built on BepInEx 5 + HarmonyX.** Add new classes, items, combat actions, enemies, and adventures through one clean, save-safe, multiplayer-deterministic API. It also serves as a base for porting *For The King II* class and ability ideas back into the original game.

## Status

| Area | State |
|---|---|
| Items and weapons | Working and verified in-game |
| Combat actions / abilities | Working and verified (create, attach to weapons, custom `ProficiencyBase` behaviours) |
| Playable classes | Working and verified (the bundled **Thief**: custom stats, a dagger, abilities, and a Focus-guaranteeable Steal) |
| Enemies | Working and verified (the bundled **Cutpurse**: custom stats, a gold-stealing Pilfer ability, custom loot, real spawns) |
| Adventures | Research in progress (DBs identified; the hardest surface, still being reverse-engineered) |

"Verified in-game" means the content has been loaded into a running game with `SELF-TEST PASS` confirmed in `BepInEx/LogOutput.log`, not just compiled. See [`docs/ROADMAP.md`](docs/ROADMAP.md) for the plan.

## Two ways to get involved

- **Use the framework** (make your own mod): add content through the public `Content.*` API. Start with [`docs/WRITING-CONTENT.md`](docs/WRITING-CONTENT.md). The bundled `Content/ThiefClass.cs` and `Content/CutpurseEnemy.cs` are working references.
- **Contribute to the framework** (work on the engine and content pipeline): see [`CONTRIBUTING.md`](CONTRIBUTING.md). Work is scoped as epics, specs, and work-items in [GitHub Issues](https://github.com/jarlbrak/ftk-mod-framework/issues); every change is verified in-game before it counts as done.

Questions and ideas are welcome in [Discussions](https://github.com/jarlbrak/ftk-mod-framework/discussions).

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
| New **enemies** | `FTK_enemyCombatDB`, `GameCache.Enemies` | ✅ working + verified (`Content.AddEnemy`; the **Cutpurse**) |
| New adventures | `FTK_realmDB`, `FTK_gameParamsDB`, encounter DBs | DBs identified; hardest, RE needed |

## Credits / prior art this builds on

- **FTKAPI** (Amadare / ftk-modding) and **FTKModLib** (lulzsun) — the existing FTK modding APIs.
- **CommunityDLC** (Theta_Hat_Society / Dehydrated-Mud) — the worked example of a custom class.
- Decompilation via **ILSpy**; loader **BepInEx**; patching **HarmonyX**.
