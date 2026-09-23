# Contributing to FTK Mod Framework

Thanks for your interest. You can author a mod with the public API or contribute to the framework itself.

## Path A: Use the framework (make your own mod)

You can build a *For The King* mod without changing this repository. Start with [Writing Content](docs/WRITING-CONTENT.md) and the published [Paladin package](marketplace/packages/paladin/README.md), the sole complete example shipped with the 1.0.0 marketplace. The [marketplace publishing guide](docs/PUBLISHING-MODS.md) covers package metadata, validation and submission.

If the public `Content.*` API is missing something your mod needs, open a [Discussion](https://github.com/jarlbrak/ftk-mod-framework/discussions) or file an issue.

## Path B: Contribute to the framework (engine and content pipeline)

Browse [GitHub Issues](https://github.com/jarlbrak/ftk-mod-framework/issues) for current work. Comment on a relevant issue or file a bug before a large change so maintainers can help identify the correct game types and avoid duplicate work.

### The source of truth

The decompiled `Assembly-CSharp` is the only correctness authority for game data. Do not trust summaries, old notes, or even this file over the actual game types. Verify exact field names, enum values, and enum order against the decompiled assembly before implementing anything that touches game data.

### Implementing a change

Every change should be the smallest faithful slice:

1. Pick an issue and verify game-data facts against your installed `Assembly-CSharp`.
2. Extend the public `Content.*` API when a mod-author capability is missing; keep engine mechanics in `Core/`.
3. Clone and register content through the existing helpers. Do not mutate vanilla rows or prefabs in place.
4. Run the relevant game-free checks from [verification guidance](.agents/skills/verify-change/SKILL.md), then record live-game evidence separately when the change needs it.
5. Update the affected public guide and the issue with the actual results.

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

## AI-assisted contributions

[`AGENTS.md`](AGENTS.md) is the canonical instruction file for coding agents and other AI harnesses. It is designed to work from a clean clone without private tools or machine-specific context. Harness adapters, including Claude Code support, point back to the generic instructions instead of maintaining a second policy set.

See [`docs/AI-NATIVE.md`](docs/AI-NATIVE.md) for the instruction hierarchy, reusable skills and roles, and the ignored `.local` files available for machine-specific setup. Run `bash scripts/agent/check-instructions.sh` after changing any agent instructions or adapters; CI runs it too.

Every top-level tree that carries work an agent may edit has its own `AGENTS.md` stating what differs there. Read the nearest one before editing.

`.claude/settings.json` is committed shared configuration: the hooks and permission rules everyone gets from a clean clone. The hook scripts it runs live in `scripts/agent/hooks/`, so they are ordinary readable project tooling. Personal overrides, account routing, and machine-specific scripts belong in the ignored `.claude/settings.local.json` and `.claude/hooks/`, which are never committed.

## Pull requests

Before opening a pull request:

- The framework builds in Release.
- Required game-free checks pass. State any outstanding live-game gate in the PR.
- No game DLLs are staged.
- Docs are updated if behavior changed.
- No em dashes in any file, comment, commit message, or PR body. Use commas, colons, semicolons, periods, or parentheses.

The pull-request template walks through this checklist.

## Code of conduct

This project follows the [Contributor Covenant](CODE_OF_CONDUCT.md). By taking part, you agree to uphold it.
