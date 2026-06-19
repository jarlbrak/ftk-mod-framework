# Contributing to FTK Mod Framework

Thanks for your interest. There are two ways to take part, and they are deliberately separate.

## Path A: Use the framework (make your own mod)

If you want to build your own For The King mod, you do not need to change this repo at all. Depend on the plugin and register content from a single hook. The full guide is [`docs/WRITING-CONTENT.md`](docs/WRITING-CONTENT.md), and the bundled `Content/ThiefClass.cs` (a full custom class) and `Content/CutpurseEnemy.cs` (a custom enemy) are working references you can copy from.

If something in the public `Content.*` API is missing or awkward for your mod, open a [Discussion](https://github.com/jarlbrak/ftk-mod-framework/discussions) or file a content idea (see below). That feedback shapes the roadmap.

## Path B: Contribute to the framework (engine and content pipeline)

Work on this repo is tracked as a three-level hierarchy in [GitHub Issues](https://github.com/jarlbrak/ftk-mod-framework/issues):

- **Epic**: a broad content goal (for example, "new enemies").
- **Spec**: a scoped deliverable under an epic, with acceptance criteria.
- **Work item**: a concrete implementation task under a spec.

If you are new, the best first step is to comment on an open issue you find interesting, or file a bug or content idea, before writing code. That lets us point you at the right game types and avoid duplicate work.

### The source of truth

The decompiled `Assembly-CSharp` is the only correctness authority for game data. Do not trust summaries, old notes, or even this file over the actual game types. Verify exact field names, enum values, and enum order against the decompiled assembly before implementing anything that touches game data.

### The content-slice method

Every change should be the smallest faithful slice:

1. Pick a documented gap (an open issue).
2. Locate the exact game type(s) in the decompiled `Assembly-CSharp`.
3. Compare the existing `Core/` and `Content/` code against what the game types actually require.
4. Implement the smallest faithful change: clone the right `FTK_*DB` row and register it through `ContentRegistry`. Never mutate game rows in place.
5. Build: `cd FTKModFramework && dotnet build -c Release`. Fix every error before moving on.
6. Verify in-game: install the DLL and confirm the `SELF-TEST PASS` lines appear in `BepInEx/LogOutput.log`.
7. Update the issue with findings; close it only when the code is verified in-game.

### Determinism rules

Co-op requires every player to run identical mods. All custom content IDs must come from `IdAllocator` using a stable string key. Hard-coded integer IDs are forbidden, because they break save portability and multiplayer.

## Building

```bash
cd FTKModFramework
dotnet build -c Release
# Output: bin/Release/net35/FTKModFramework.dll
```

On a non-Mac machine, point the build at your game's managed folder:

```bash
dotnet build -c Release -p:FtkManagedDir="C:\Program Files (x86)\Steam\steamapps\common\For The King\FTK_Data\Managed"
```

The build references the game's own DLLs from your local install and publicizes `Assembly-CSharp` at compile time. Those DLLs are copyrighted and git-ignored. Never commit them. CI enforces this: a tracked `Assembly-CSharp*.dll`, `UnityEngine*.dll`, or `Newtonsoft.Json.dll` fails the build.

## Pull requests

Before opening a pull request:

- The framework builds in Release.
- You have verified the change in-game and seen the `SELF-TEST PASS` lines (paste the relevant log lines into the PR).
- No game DLLs are staged.
- Docs are updated if behavior changed.
- No em dashes in any file, comment, commit message, or PR body. Use commas, colons, semicolons, periods, or parentheses.

The pull-request template walks through this checklist.

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By taking part, you agree to uphold it.
