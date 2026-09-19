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
| Passive class traits | Working and verified (the bundled **Innkeeper**: class-innate passives via `Content.AddPassive`) |
| Custom enemy and player models | Editor-free GLB authoring, strict transactional renderer replacement, and route-specific live validation are available. The catalog covers all 48 supported topology groups; see [`docs/CUSTOM-MODELS.md`](docs/CUSTOM-MODELS.md). |
| Adventures & campaigns | Working and verified solo (cloned adventures, plus the bundled **The Hollow Mire**: a bespoke realm, boss, and questline played to victory; see [`docs/ADVENTURES.md`](docs/ADVENTURES.md)); co-op verification pending |

"Verified in-game" means the content has been loaded into a running game with `SELF-TEST PASS` confirmed in `BepInEx/LogOutput.log`, not just compiled. See [`docs/ROADMAP.md`](docs/ROADMAP.md) for the plan.

## Play with mods (macOS, Linux, Windows preview)

1. Download your platform launcher archive from [GitHub Releases](https://github.com/jarlbrak/ftk-mod-framework/releases).
2. Extract it to a permanent folder, keeping the files together.
3. In Steam, choose **Games > Add a Non-Steam Game > Browse** and select **For The King Modded.app**
   (Mac), **For The King Modded.sh** (Linux), or **FtkModdedLauncher.exe** (Windows).
4. Name it **For The King Modded** and launch. On Windows, choose **Play** in the launcher.

First launch installs the bundled framework and its loader. Steam artwork is applied
automatically; Steam may need one restart to show it. Each launcher Play checks for a
compatible framework update before opening your owned game, preserving mods and settings
and using the verified installed version when offline. In **Mods > Updates**, follow Stable
or Preview, or pin a specific release after reading its patch notes. Stable excludes previews.

The title screen includes a **Mods** browser, and bundled content includes the **Thief**,
**Innkeeper**, **Smuggler's Run**, and **The Hollow Mire**. See
[the launcher guide](launcher/README.md) for setup and platform limits. macOS gameplay
has been smoke-tested; Windows and Linux/Proton gameplay still need platform testing.

The standalone terminal installer remains available for manual setup and removal;
see [the installation guide](docs/INSTALL.md). It downloads the latest stable release
by default, so use the launcher archive for this early-access preview.

## Three ways to get involved

- **Play** (above), and drop other content mods into `<game>/BepInEx/plugins/`.
- **Use the framework** (make your own mod): add content through the public `Content.*` API. Start with [`docs/WRITING-CONTENT.md`](docs/WRITING-CONTENT.md), and declare the author-confirmed framework minimum described in [`docs/MOD-VERSIONING.md`](docs/MOD-VERSIONING.md). The bundled `Content/ThiefClass.cs` and `Content/CutpurseEnemy.cs` are working references.
- **Contribute to the framework** (work on the engine and content pipeline): see [`CONTRIBUTING.md`](CONTRIBUTING.md). Work is scoped as epics, specs, and work-items in [GitHub Issues](https://github.com/jarlbrak/ftk-mod-framework/issues); every change is verified in-game before it counts as done.

Questions and ideas are welcome in [Discussions](https://github.com/jarlbrak/ftk-mod-framework/discussions).

## Verified facts

| Thing | Value |
|---|---|
| Engine | Unity **2017.2.2p2** |
| Scripting backend | **Mono / .NET 3.5** (managed `Assembly-CSharp.dll`; no IL2CPP) |
| Mod loader | BepInEx **5.4.x** (Mono x64) |
| Patching | HarmonyX (`HarmonyLib`) |
| Content model | `GridEditor.TableManager` → `FTK_*DB` data tables (clone-and-register) |
| Managed dir (macOS) | `~/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/Managed` |
| Managed dir (Windows) | `<Steam>\steamapps\common\For The King\FTK_Data\Managed` |
| Build toolchain | .NET SDK (builds the net35 plugin via reference assemblies); ILSpy for decompiling |

## Repo layout

```
FTKModFramework/
  FTKModFramework.csproj   net35 plugin; references + publicizes the game's Assembly-CSharp
  Plugin.cs                BepInEx entry point + the single TableManager.Initialize content hook
  Core/
    Content.cs             high-level API: AddWeapon / AddItem / AddProficiency / AddClass /
                           AddEnemy / AddEncounter / AddPassive / AttachProficiencies / ...
    ContentRegistry.cs     generic "add a row to any FTK_*DB" engine
    IdAllocator.cs         deterministic synthetic enum-IDs (save + multiplayer stable)
    DbLookupPatcher.cs     Harmony patches so the game resolves our custom string IDs
    EnumPatches.cs         GetEnum prefixes (items / proficiencies / classes) for custom IDs
    Localization.cs        custom names, flavor text, and tooltip text
    Adventures.cs          adventure cloning/registration; CampaignBuilder/QuestBuilder for questlines
    Data/                  data-authored JSON mods (manifest plus deterministic content-file loading)
    Marketplace/           catalog, package, release-note, and framework-update runtime
    UI/                    the in-game Mods browser, installed-mod controls, and updates panel
  Content/                 bundled sample content, all working references:
    ThiefClass.cs          the Thief: a full custom class (stats, dagger, abilities, Steal)
    CutpurseEnemy.cs       the Cutpurse: a full custom enemy (stats, Pilfer, loot, real spawns)
    InnkeeperClass.cs      the Innkeeper: class-innate passive traits
    RealmBossAdventure.cs  The Hollow Mire: a bespoke realm + boss + questline
  Agent/                   opt-in test bridge (env-gated, loopback-only, single-player; see harness/)
FTKPerfProbe/              standalone perf-probe plugin (+ FTKPerfProbe.Tests)
harness/                   MCP server that lets an agent drive the game to verify content
launcher/                  branded cross-platform launcher, updater helper, and packaging tools
marketplace/               reviewed community catalog and validation fixtures
install.sh                 the player installer (macOS + Linux): BepInEx + plugin + Steam launch option
deploy.sh                  developer build-and-install through install.sh (self-tests on)
release.sh                 maintainer: publish a GitHub release the installer downloads from
tests/installer/           the installer's test suite (mock Steam layouts; runs in CI)
AGENTS.md                  canonical instructions for coding agents (nested files per subtree)
.agents/                   portable agent skills and specialist role contracts
scripts/agent/             instruction-graph validation and the shared agent hooks
docs/
  INSTALL.md               player install guide: what the installer does per platform, troubleshooting
  WRITING-CONTENT.md       modder API guide (items, abilities, classes, enemies, encounters)
  ADVENTURES.md            how FTK models adventures + how the framework adds them
  CAMPAIGNS.md             data-authored questlines (branching, flags, custom objective verbs)
  CUSTOM-MODELS.md         custom enemy models (runtime glTF + AssetBundle paths)
  MODEL-AUTHORING.md       repeatable original-model workflow and live checks
  MODEL-PLAYER-API.md      strict player body, hair, and conditional-apparel assignments
  MODEL-RENDERER-API.md    strict enemy renderer, material, scale, portrait, and fall-off APIs
  MODEL-SKELETONS.md       discovery inventory and per-rig validation status
  AI-NATIVE.md             generic-first agent instructions, adapters, and local context
  MARKETPLACE.md           player and package-author marketplace guide
  MOD-VERSIONING.md        manifest compatibility and framework-update policy
  SCALE-BUDGET.md          the load-time / heap scale-budget gate
  PHASE0-TYPE-INVENTORY.md the full content-table inventory decompiled from the game
  ROADMAP.md               phased plan toward the five content goals + FTK2 ports
nuget.config               adds the BepInEx NuGet feed
```

## Build

Requires the .NET SDK.

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
are git-ignored (they're copyrighted; reference them from the install).

## Install & run

Players: use the launcher archive described in [Play with mods](#play-with-mods-macos-linux-windows-preview).
It handles first-time framework and loader setup on all three platforms. The standalone terminal
installer remains available for manual macOS and Linux setup; [`docs/INSTALL.md`](docs/INSTALL.md)
has the platform details, removal steps, and troubleshooting.

Developers: `./deploy.sh` builds the framework and installs your build into your Steam copy through
the same installer, with the load-time self-tests switched on (`Diagnostics/RunSelfTests`). Launch,
then check `BepInEx/LogOutput.log` for `FTK Mod Framework ... loaded` and the `SELF-TEST PASS` lines.
The installer has its own test suite: `bash tests/installer/test-install.sh`.

> **Co-op requires every player to have identical mods** (no asset streaming), which is why
> `IdAllocator` makes IDs deterministic across machines.

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

The bundled content is a working reference: `Content/SampleContent.cs` (a custom weapon + ability),
`Content/ThiefClass.cs` (a full custom class, including a custom-behaviour `ProficiencyBase` in
`ThiefStealProficiency.cs`), `Content/CutpurseEnemy.cs` (a custom enemy), `Content/InnkeeperClass.cs`
(passive traits), and `Content/RealmBossAdventure.cs` (a bespoke adventure). The samples are gated
behind the `Demo / EnableSampleContent` config (set it false to use the framework purely as a
dependency for other mods).

Content can also be authored as **pure data**, no C# required: a mod folder with a `manifest.json`
and one or more content JSON files is discovered and loaded at startup, and the title screen's
**Mods** panel manages each mod (see [`docs/WRITING-CONTENT.md`](docs/WRITING-CONTENT.md) §12).
Every manifest must declare its own release `version` and an author-confirmed `frameworkVersion`;
missing, invalid, older, or different-major declarations remain visible but do not load.

## The five goals: where each stands

| Goal | DB / types | Status |
|---|---|---|
| New items / weapons | `FTK_itemsDB`, `FTK_weaponStats2DB` | ✅ working + verified in-game |
| New combat actions | `FTK_proficiencyTableDB`, `FTK_hitEffectDB` | ✅ working (create + attach to weapons; custom `ProficiencyBase` behaviours) |
| New **classes** | `FTK_playerGameStartDB`, `FTK_skinsetDB` | ✅ working + verified (`Content.AddClass`; the **Thief**) |
| New **enemies** | `FTK_enemyCombatDB`, `GameCache.Enemies` | ✅ working + verified (`Content.AddEnemy`; the **Cutpurse**) |
| New adventures | `GameDefinition` (`.ftk2`), `FTK_realmDB`, `FTK_miniEncounterDB` | ✅ working + verified solo (`Adventures.AddFromTemplate`; a bespoke realm + boss; co-op verification pending) |

## Community marketplace

The title-screen Mods panel includes Discover and Installed views for curated free content, with
changes applied on restart. The initial production catalog is intentionally empty until reviewed
packages are published. See [the marketplace guide](docs/MARKETPLACE.md) for installation, author
submissions, recovery, and current validation limits.

## Documentation

- Players: [launcher setup](launcher/README.md), [manual installation](docs/INSTALL.md),
  [marketplace](docs/MARKETPLACE.md), and [release notes](docs/releases/).
- Mod authors: [writing content](docs/WRITING-CONTENT.md), [mod versioning](docs/MOD-VERSIONING.md),
  [adventures](docs/ADVENTURES.md), [campaigns](docs/CAMPAIGNS.md), and
  [custom models](docs/CUSTOM-MODELS.md).
- Model authors: [authoring workflow](docs/MODEL-AUTHORING.md),
  [enemy renderer API](docs/MODEL-RENDERER-API.md), [player renderer API](docs/MODEL-PLAYER-API.md),
  and [skeleton and route register](docs/MODEL-SKELETONS.md).
- Contributors: [contribution guide](CONTRIBUTING.md), [roadmap](docs/ROADMAP.md), and
  [release process](docs/RELEASING.md).

## Credits / prior art this builds on

- **FTKAPI** (Amadare / ftk-modding) and **FTKModLib** (lulzsun): the existing FTK modding APIs.
- **CommunityDLC** (Theta_Hat_Society / Dehydrated-Mud): the worked example of a custom class.
- Decompilation via **ILSpy**; loader **BepInEx**; patching **HarmonyX**.
