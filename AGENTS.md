# FTK Mod Framework: agent instructions

This is the canonical public instruction file for coding agents and automation in this
repository. Harness-specific files point here. More-specific `AGENTS.md` files apply within
their subtrees.

## Project

FTK Mod Framework is a content-modding framework for the original *For The King*
(IronOak, 2018), built on BepInEx 5 and HarmonyX. It targets Unity 2017.2.2p2,
Mono, and .NET 3.5.

The installed game's managed assemblies are the correctness authority for game behavior.
Documentation, comments, and remembered findings may be stale. Never commit game assemblies
or decompiled game source.

## Instruction and context hierarchy

1. This file contains repository-wide public rules.
2. The nearest nested `AGENTS.md` adds subsystem-specific rules.
3. `.agents/skills/*/SKILL.md` contains portable workflows loaded only when relevant.
4. `.agents/roles/*.md` contains portable specialist role contracts.
5. Ignored `AGENTS.override.md` may load `AGENTS.local.md` for machine-only context.
6. Ignored `.local/agents/*.md` holds focused local resources such as paths, MCP services,
   private knowledge stores, and live-game setup.

Public rules remain binding when local overlays are present. Do not put credentials, absolute
personal paths, private service identifiers, or session history in committed instruction files.
See `docs/AI-NATIVE.md` for the complete layout and adapter rules.

## Work safely

- Inspect `git status --short --branch` before editing. Preserve unrelated changes.
- Prefer the smallest faithful change. Do not perform drive-by cleanup.
- Never use destructive git commands or delete untracked files without explicit user direction.
- Never commit game DLLs, decompiled source, saves, logs, credentials, or private local-agent files.
- Use the GitHub account and remote already configured for this checkout. Local account-routing
  details belong in `AGENTS.local.md`.
- Follow the public API boundary: mod-author surfaces belong in `Content.*`; engine mechanics
  belong in `Core/`.

## Source of truth and architecture

- `FTKModFramework/Core/`: registration, deterministic IDs, patches, loading, marketplace,
  renderer transactions, and other engine internals.
- `FTKModFramework/Content/`: bundled examples authored through the public API.
- `FTKModFramework/Agent/` and `harness/`: opt-in game-driving and verification.
- `launcher/`: cross-platform launcher, updater helper, packaging, and installers.
- `marketplace/`: production catalog and validation fixtures.
- `tools/ai-model-pipeline/`: editor-free model authoring and evidence tooling.
- `docs/`: public architecture, authoring, operational, and evidence documentation.

Registered content must use deterministic identities. Never introduce hard-coded custom enum
integers. Never mutate vanilla database rows or prefab assets in place.

## Coding constraints

- Framework runtime code targets `net35`. Do not use language or library features unavailable
  under the shipped Mono runtime.
- Harmony patches must be narrow, idempotent, fail safely, and preserve vanilla behavior when
  their preconditions are not met.
- Clone and register content through existing helpers. Prefer extending a coherent public API
  over bypassing it from sample content.
- Preserve save and multiplayer determinism. Client-local randomness must not decide shared state.
- Comments explain constraints and reasons, not syntax.

## Workflow

1. Read the nearest instructions and the tests around the behavior.
2. For game-data facts, inspect the installed assembly using the `decompile-lookup` skill.
3. State the invariant and observable outcome before choosing an implementation.
4. Make the smallest coherent change.
5. Run narrow verification first, then the proportional checks selected by `verify-change`.
6. Update public documentation when behavior, setup, compatibility, or evidence changes.
7. Report live-game gates separately from game-free tests. Never imply a build proves in-game behavior.

## Common commands

```bash
cd FTKModFramework && dotnet build -c Release
dotnet run --project FTKModFramework/Tests/PlayerMods/PlayerMods.csproj -c Release
dotnet test FTKPerfProbe.Tests/FTKPerfProbe.Tests.csproj -c Release
bash tests/installer/test-install.sh
(cd launcher/helper && go test ./...)
git diff --check
```

Use `.agents/skills/verify-change/SKILL.md` to choose the required subset. Live-game validation
uses `.agents/skills/ingame-smoke/SKILL.md` and requires an explicitly configured local setup.

## Change delivery

- Branch from `master` as `<topic-area>/<short-slug>`, for example `docs/audit-followups`.
- Write commit subjects in the imperative and under about 72 characters. Explain in the body what
  the change makes true and what evidence supports it, not what files moved.
- Keep one coherent change per commit. Do not mix a refactor with the behavior change it enables.
- Pull request bodies state the verification actually run and name every gate still outstanding.
  `CONTRIBUTING.md` holds the full checklist and the pull-request template walks through it.
- Never add generated-by boilerplate to a pull request body or an issue. A `Co-Authored-By:`
  trailer on a commit follows the existing convention in this repository's history.
- Never commit a game assembly, decompiled source, a save, a log, a credential, or a local overlay
  file. Confirm the staged set before every commit.

## Specialist routing

Use a specialist role when the task matches and the harness supports delegation. Otherwise read
the role contract and apply it in the current session.

| Task | Role |
|---|---|
| Architecture, spec, or non-trivial PR review | `.agents/roles/ftk-architect.md` |
| BepInEx, Harmony, registration, or `Core/` implementation | `.agents/roles/csharp-harmony-engineer.md` |
| Exact game types, fields, enums, or method behavior | `.agents/roles/game-decompile-analyst.md` |
| Content authored through the public API | `.agents/roles/content-author.md` |
| Class, ability, enemy, or adventure design | `.agents/roles/game-designer.md` |

Private knowledge curation is deliberately absent from the public role set. A local overlay may
register a knowledge-curator role when a private knowledge service is available.

## Documentation and communication

- Keep the README a stable front door. Put detailed procedures in focused guides.
- Prefer links to canonical material over duplicated instructions.
- Distinguish verified fact, source-backed inference, offline validation, and live-game evidence.
- Do not overstate platform, co-op, animation, lifecycle, or art coverage.
- Do not use em dashes in repository text, comments, commits, issues, or pull requests.

## Definition of done

A change is complete only when the relevant game-free checks pass, `git diff --check` passes,
documentation is current, and remaining live or platform gates are stated precisely. Before a
commit or pull request, confirm no game DLL or private local-agent file is staged.
