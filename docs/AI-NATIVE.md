# AI-native repository layout

FTK uses a generic-first instruction hierarchy so coding agents can share one project contract
without maintaining parallel Claude, Codex, or vendor-specific policy.

## Authority graph

```text
host adapter -> committed generic instructions -> public repository docs
             -> optional ignored local overlay
```

`AGENTS.md` is the repository-wide authority. Nested `AGENTS.md` files add instructions for a
subtree. `.agents/skills/` and `.agents/roles/` contain portable workflows and specialist
contracts. `CLAUDE.md`, `.claude/`, and `.codex/` contain only the metadata or translation a
particular harness requires.

Adapters point toward generic files. Generic files never point into a harness directory.

## Local context

Machine-specific context is optional and ignored. Create these files only when needed:

```text
AGENTS.override.md
AGENTS.local.md
CLAUDE.local.md
.local/agents/environment.md
.local/agents/github.md
.local/agents/knowledge.md
.local/agents/mcp.md
.local/agents/game-validation.md
.local/agents/active-work.md
.claude/settings.local.json
```

`AGENTS.override.md` is the generic/Codex bootstrap. It should tell the agent to read the committed
`AGENTS.md` and then `AGENTS.local.md`. `CLAUDE.local.md` should contain only
`@AGENTS.local.md`. Keep `AGENTS.local.md` short: it is an index that says when to load each focused
file under `.local/agents/`.

Local overlays may add capabilities and machine facts. They may not relax public safety,
verification, evidence, or architecture rules. A clean clone without local context supports
instruction discovery, documentation, review, launcher and installer checks, and isolated tests
that do not reference the game. Building the framework, decompiling the game, deploying, and live
validation require the locally installed game assemblies described by the public prerequisites.
Agents must report those checks as unavailable when the prerequisite is absent, not silently skip
them or claim they passed.

## Suitable local information

- Installed game and managed-assembly paths.
- Tool versions or aliases that differ from the public prerequisites.
- GitHub account profiles and write-operation identity checks.
- MCP server commands and availability.
- Private knowledge stores such as Ninum and their project routing.
- Local Steam deployment, isolated game copies, capture tools, and live-test procedures.
- Short-lived handoff state in `active-work.md`.

Do not store secrets in repository-local files, even ignored ones. Reference environment variables,
keychain entries, or external profile files. Ignore rules prevent ordinary commits but do not stop
logs, backups, support bundles, force-adds, or an authorized agent from reading a file.

Use `.local/agents/mcp.md` to document which local MCP services are available and when to use them.
Configure the actual server command and environment in the harness's user-level configuration.
Repository files do not automatically activate `.codex/config.local.toml`, so do not rely on that
filename as a Codex bootstrap.

Private knowledge is advisory. Any fact required to implement or review the public repository must
also exist in code, a committed document, a public issue, or reproducible game-source evidence.

## Adding a skill

1. Create the canonical workflow at `.agents/skills/<name>/SKILL.md`.
2. Use repository-relative paths and portable tool descriptions.
3. Add a thin harness adapter only when that harness cannot discover the generic skill directly.
4. Put machine-only augmentation in local context, not in the public skill.
5. Run `bash scripts/agent/check-instructions.sh`.

## Adding a role

1. Create the canonical contract at `.agents/roles/<name>.md`.
2. Define responsibility, authority, required evidence, boundaries, and return format.
3. Add thin Claude or Codex wrappers containing only harness frontmatter and tool translation.
4. State a same-session fallback so work does not depend on multi-agent support.

## Validation

`bash scripts/agent/check-instructions.sh` checks the committed instruction graph for missing
targets, private absolute paths, obvious secret-shaped values, incorrect generic-to-harness
dependencies, and local files that are no longer ignored.
