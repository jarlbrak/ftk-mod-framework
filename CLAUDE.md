# FTK Mod Framework: Claude Code adapter

@AGENTS.md

The repository's canonical instructions, roles, and workflows are generic-first.
Claude Code adapters under `.claude/` may add harness-specific metadata and tool
names, but must not duplicate or override project policy.

Machine-specific Claude configuration belongs in ignored `CLAUDE.local.md` and
`.claude/settings.local.json`. `CLAUDE.local.md` should import `AGENTS.local.md`
rather than repeat its content.
