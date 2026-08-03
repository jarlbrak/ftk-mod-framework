# CLAUDE.md

This is the operating guide for Claude Code in the FTK repo. If it conflicts with the current worktree or the decompiled game source, those win.

## Project and Source of Truth

FTK Mod Framework is a content-modding framework for **For The King** (IronOak, 2018), built on BepInEx 5 + HarmonyX. It lets modders add classes, items, combat actions, enemies, and adventures through a clean, save-safe, multiplayer-deterministic API. It also serves as the base for porting *For The King II* class/ability ideas back into the original game.

The decompiled `Assembly-CSharp` is the **only correctness authority.** Do not trust old AI summaries, the README, or this file over the actual game types. Verify exact fields, enum values, and enum order against the decompiled `Assembly-CSharp` before implementing anything that touches game data.

| Thing | Value |
|---|---|
| Engine | Unity 2017.2.2p2 |
| Scripting backend | Mono / .NET 3.5 (no IL2CPP; managed `Assembly-CSharp.dll`) |
| Mod loader | BepInEx 5.4.x (Mono x64) |
| Patching | HarmonyX (`HarmonyLib`) |
| Content model | `GridEditor.TableManager` -> `FTK_*DB` data tables (clone-and-register) |
| Managed dir (macOS) | `~/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/Managed` |
| Toolchain | `dotnet` SDK, `ilspycmd` |
| Remote | `jarlbrak/ftk-mod-framework` (public), branch `master` |

Game DLLs are copyrighted and git-ignored. The build references them from the local install and publicizes `Assembly-CSharp` at compile time. Never commit them.

## Start-of-Session Checklist

Run these at the start of every session, in order:

```bash
git status --short --branch
git log --oneline -8
gh issue list --state open
```

Then skim `docs/ROADMAP.md` for current phase priorities.

Also confirm the Managed dir is reachable (the build references it):

```bash
ls "$HOME/Library/Application Support/Steam/steamapps/common/For The King/FTK.app/Contents/Resources/Data/Managed/Assembly-CSharp.dll"
```

## Mandatory Content-Slice Method

Every implementation slice must follow this sequence:

1. Pick a real documented gap: an open GitHub issue.
2. Locate the exact game type(s) in the decompiled `Assembly-CSharp` (read the source, not summaries).
3. Compare the existing `Core/` and `Content/` code against what the game types actually require.
4. Implement the smallest faithful change that fills the gap: clone the right `FTK_*DB` row and register it via `ContentRegistry`.
5. Build: `cd FTKModFramework && dotnet build -c Release`. Fix any errors before proceeding.
6. Verify in-game: launch FTK with the DLL installed and confirm `SELF-TEST PASS` lines appear in `BepInEx/LogOutput.log`.
7. Update the GitHub issue with findings, close it only when the code is verified in-game.
8. Update `docs/` as needed.
9. Commit with a faithful summary.

No bulk closes. A closed issue means real code was verified in-game, not just built.

## Build and Validation Gate

```bash
cd FTKModFramework
dotnet build -c Release
# Output: bin/Release/net35/FTKModFramework.dll
```

On a different machine or OS, override the managed dir:

```bash
dotnet build -c Release -p:FtkManagedDir="C:\Program Files (x86)\Steam\steamapps\common\For The King\FTK_Data\Managed"
```

**In-game gate:** after copying `FTKModFramework.dll` to `<game>/BepInEx/plugins/` and launching, confirm `BepInEx/LogOutput.log` contains `FTK Mod Framework ... loaded` and `SELF-TEST PASS` lines. If those lines are absent, the content registration failed.

**Determinism invariants:** Co-op requires every player to have identical mods (no asset streaming). `IdAllocator` assigns synthetic enum IDs that are deterministic across machines, making saves portable and multiplayer sessions consistent. Any new registered content must go through `IdAllocator`; hard-coded integer IDs are forbidden.

## Coding Patterns

**net35 / Mono constraints:** Target `net35`. No modern C# language features or .NET APIs that postdate 3.5. No `Span<T>`, no async/await beyond what Mono 3.5 supports, no `System.Text.Json`. When in doubt, check that the API exists in .NET 3.5.

**HarmonyX patch hygiene:** All patches must be idempotent. Guard `Postfix` methods with a `static bool _done` flag and return immediately if already run. Pattern:

```csharp
static bool _done;
static void Postfix()
{
    if (_done) return; _done = true;
    // ... registration ...
}
```

**Clone-and-register:** To add content, clone an existing `FTK_*DB` row (using the `ContentRegistry` helpers), modify the clone, and register it. Never mutate game rows in place.

**Deterministic IDs:** All custom content IDs come from `IdAllocator`. Pass a stable string key (e.g., `"com.you.mymod:mymod_sword"`); the allocator produces a deterministic integer that is safe across machines and saves.

**Enum mirroring:** When mirroring game enums (e.g., `FTK_itembase.ID`), preserve the game's exact names and order. Do not reorder or rename.

**API boundary:** Prefer existing `Content.*` helpers (`Content.AddWeapon`, `Content.AddClass`, `Content.AddProficiency`, `Content.AttachProficiencies`) over inventing new abstractions. Keep `Content/` (public modder API) cleanly separated from `Core/` (engine internals: `ContentRegistry`, `IdAllocator`, patchers, localization).

**Never commit game DLLs.** They are copyrighted. The `.gitignore` excludes them; keep it that way.

## Documentation Hierarchy

| Content type | Location |
|---|---|
| Specs, features, work items | GitHub Issues (`jarlbrak/ftk-mod-framework`) |
| Architecture, patterns, modder guides | `docs/` (e.g., `WRITING-CONTENT.md`, `ROADMAP.md`, `PHASE0-TYPE-INVENTORY.md`) |

## Planning = GitHub Issues

Work is tracked as a three-level hierarchy:

- **Epic** (`epic` label): a broad content goal (e.g., "New enemies").
- **Spec** (`spec` label): a scoped deliverable under an epic, with acceptance criteria.
- **Work Item** (`work-item` label): a concrete implementation task under a spec.

Other labels in use: `bug`, `RE`, `class`, `item`, `ability`, `enemy`, `adventure`, `ftk2-port`, `infra`, `docs`. Routing and state labels: `core` and `content` (which code area), `remediation` (a fix work item from validation), `design-needed` (needs a design brief first).

## Writing Style

Never use em dashes in any file or output. Use commas, colons, semicolons, periods, or parentheses instead. This rule applies to all files, comments, commit messages, and issue bodies.
