# FTK Mod Framework

A content-modding framework for **For The King** (the original 2018 IronOak game), built on
BepInEx 5 + HarmonyX. Goal: let modders add new **classes, items, combat actions, enemies, and
adventures** through one clean, save-safe, multiplayer-deterministic API — and serve as the base
for porting *For The King II* class/ability ideas back into the original game.

> Status: **early scaffold (v0.1).** The core injection engine compiles against the real game
> assembly and there is a working sample item. Higher-level content APIs and in-game testing are
> the next steps. See `docs/PHASE0-TYPE-INVENTORY.md` for the verified game data model and
> `docs/ROADMAP.md` for the plan.

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
    IdAllocator.cs         deterministic synthetic enum-IDs (save + multiplayer stable)
    ContentRegistry.cs     generic "add a row to any FTK_*DB" engine
    DbLookupPatcher.cs      Harmony patch so the game resolves our custom string IDs
    Reflect.cs             reflection helpers (field copy, etc.)
  Content/
    SampleContent.cs       proof-of-pipeline: clones the Shortsword into a new shop item
docs/
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
4. Launch. Check `BepInEx/LogOutput.log` for `FTK Mod Framework 0.1.0 loaded` and
   `Registered 'ftkmf_testblade'`. The sample blade should appear in town shops.

> **macOS note:** BepInEx on the Mac Unity-Mono build uses `run_bepinex.sh` + a Doorstop dylib
> rather than the Windows `winhttp.dll`. It works, but the FTK community packs are Windows-first, so
> a Windows install (or a VM) is the smoother path for *testing*. The framework DLL itself is
> platform-agnostic managed IL. Also note: **co-op requires every player to have identical mods**
> (no asset streaming), which is why `IdAllocator` makes IDs deterministic across machines.

## The five goals — where each stands

| Goal | DB / types | Status |
|---|---|---|
| New items / weapons | `FTK_itemsDB`, `FTK_weaponStats2DB` | engine works (sample item builds) |
| New combat actions | `FTK_proficiencyTableDB`, `FTK_hitEffectDB` | DBs identified; helper next |
| New enemies | `FTK_enemyCombatDB`, `FTK_enemySetDB` | DBs identified (was the big unknown) |
| New classes | `FTK_playerGameStartDB`, `FTK_skinsetDB` | DBs identified; precedent exists (CommunityDLC Paladin) |
| New adventures | `FTK_realmDB`, `FTK_gameParamsDB`, encounter DBs | DBs identified; hardest, RE needed |

## Credits / prior art this builds on

- **FTKAPI** (Amadare / ftk-modding) and **FTKModLib** (lulzsun) — the existing FTK modding APIs.
- **CommunityDLC** (Theta_Hat_Society / Dehydrated-Mud) — the worked example of a custom class.
- Decompilation via **ILSpy**; loader **BepInEx**; patching **HarmonyX**.
