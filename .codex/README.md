# Codex adapter layer

Project policy and specialist contracts are canonical in `AGENTS.md` and `.agents/`.
Files in this directory contain only Codex-specific discovery or configuration.

Do not copy substantive instructions here. Portable skills are discovered from
`.agents/skills/`. Custom subagent definitions under `.codex/agents/` point to
`.agents/roles/`.

Machine-specific MCP servers, account routing, absolute paths, and permissions belong in the
ignored local configuration described by `docs/AI-NATIVE.md`. Never store literal credentials
in a repository-local config, even when it is ignored.

Shared automation is not Codex-specific either. The hook scripts under `scripts/agent/hooks/` are
plain portable Bash and can be wired into any harness. Run
`bash scripts/agent/check-instructions.sh` after changing any instruction file or adapter.
