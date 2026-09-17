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

Every top-level tree that holds tracked files owns an `AGENTS.md`. The instruction check derives that
list from the repository itself, so a newly added tree fails the check until someone writes its
rules. Only the harness adapter layers and the binary asset tree are exempt, because the root file
already governs them. A nested file states what differs in its subtree and defers to the root file
for everything else; the check enforces the deference as well as the coverage.

`skills/ftk-custom-models/` is the one workflow body outside `.agents/skills/`. It predates the
convention and keeps its path because public model guides link to it. The canonical
`.agents/skills/ftk-custom-models/SKILL.md` points into it. Do not add new skills there.

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
.claude/hooks/
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

## Progressive disclosure

Instructions load in tiers so an agent carries only what the current step needs.

1. A skill or role description is always in context. It exists to answer one question: is this
   relevant now.
2. The body loads on invocation. For a skill that is the `SKILL.md`; for a role, the contract.
3. A `references/` directory beside a `SKILL.md` loads per step, and only the file that step needs.

Most skills here stop at tier two, because a fourteen-line workflow has nothing to defer. The tier
exists for the workflows that would otherwise force an agent to read a route it is not taking.
`skills/ftk-custom-models/` is the worked example: the entry point owns orientation, route choice,
the working method, and the non-negotiables, and a routing table sends the reader to exactly one of
seven references.

The rules that keep the tier honest:

- The entry point stays lean. The instruction check fails a `SKILL.md` over 400 lines, whether or
  not it has references, because past that size it is no longer the always-loaded tier.
- Every reference is reachable from its entry point's routing table. An unreachable reference is a
  file nothing will ever read, and the check fails it.
- Every reference stands on its own and says so. A reader who loaded one has not read the others.
- Relative links inside the graph must resolve. The check follows them.

Splitting a workflow moves content; it never summarizes it. Detail that gets compressed on the way
into a reference is detail that was silently deleted.

### Adapters and the reference tier

A harness loads its own adapter, not the canonical body, and it tells the agent that the adapter's
directory is the skill's base directory. That directory holds no references. So an adapter for a
tiered skill has two jobs beyond pointing at the canonical file:

- Say that the skill is tiered and that only the step's own reference should be loaded. An adapter
  that says nothing invites the reader to pull the entire body.
- Name the directory the routing table resolves against. Otherwise a relative link like
  `references/route-selection.md` resolves against the adapter's base directory and finds nothing.

The instruction check enforces the first: a skill with a `references/` tier whose adapter never
mentions it fails. Keep the adapter within its thinness limit; these are two added sentences, not a
second copy of the workflow.

Duplicating the references into an adapter directory would satisfy the resolver and defeat the
point. Adapters translate; they never hold content.

## Shared and local harness settings

Harness settings split the same way instructions do. `.claude/settings.json` is the shared project
contract and is committed, so every contributor gets the same hooks and the same permission rules
from a clean clone. `.claude/settings.local.json` and `.claude/hooks/` are ignored and hold personal
overrides, account routing, and machine-specific scripts.

Hook scripts that every contributor should receive live in `scripts/agent/hooks/`, not in the
ignored harness directory. Keeping them under `scripts/` keeps them readable as ordinary project
tooling, lets a second harness reuse them, and keeps the harness directory an adapter layer.

A shared hook must run from a clean clone. It fails open when a tool it wants is missing, so a
docs-only checkout is never blocked. A hook that enforces project policy may exit non-zero and say
why. A hook that merely tidies must not.

Shared settings never name a personal path, an account, or a private service. Anything that would
differ between two contributors belongs in the ignored local file.

## Adding a skill

1. Create the canonical workflow at `.agents/skills/<name>/SKILL.md`.
2. Use repository-relative paths and portable tool descriptions.
3. Add a thin harness adapter only when that harness cannot discover the generic skill directly.
4. Keep the body to what every invocation needs. Put route-specific or phase-specific detail in
   `references/` beside it and link each file from a routing table in the body.
5. Put machine-only augmentation in local context, not in the public skill.
6. Run `bash scripts/agent/check-instructions.sh`.

## Adding a role

1. Create the canonical contract at `.agents/roles/<name>.md`.
2. Define responsibility, authority, required evidence, boundaries, and return format.
3. Add thin Claude or Codex wrappers containing only harness frontmatter and tool translation.
4. State a same-session fallback so work does not depend on multi-agent support.

## Validation

`bash scripts/agent/check-instructions.sh` checks the committed instruction graph. CI runs it, and
the shared hook re-runs it as soon as an agent edits part of the graph.

The checks are derived from the filesystem rather than a hand-maintained list, so a new skill or
role that is missing an adapter fails immediately instead of leaving one harness blind. It verifies:

- The canonical entry points exist and `CLAUDE.md` imports `AGENTS.md`.
- Every tracked top-level tree outside the exempt set has an `AGENTS.md`, and every nested file
  defers to the root.
- Every canonical skill has frontmatter whose name matches its directory, plus a thin adapter that
  targets it. Adapters with no canonical source are reported too.
- Every canonical role has thin adapters for both supported harnesses, and no adapter is orphaned.
- Every skill body stays under the disclosure limit, every `references/` file is reachable from
  its entry point, and every relative link in the graph resolves.
- Shared harness settings parse, and every hook they reference is committed and executable.
- No public agent file carries a private absolute path or a secret-shaped literal.
- No generic instruction depends on a harness adapter layer.
- Every documented local path is ignored, the shared settings file is not, and no local-only file is
  tracked.

Run it after changing any instruction, adapter, hook, or ignore rule.
