# Repository script instructions

Read the root `AGENTS.md` first.

- Scripts are portable Bash. They must run from a clean clone on macOS and Linux with no private
  context, no game installation, and no harness-specific environment.
- Resolve the repository root from git rather than assuming a working directory, and never hard-code
  a personal path.
- `scripts/agent/check-instructions.sh` validates the committed instruction graph and runs in CI.
  Extend it whenever a new instruction, adapter, or local-overlay convention is introduced.
- `scripts/agent/hooks/` holds hook scripts every contributor receives. They must fail open when a
  tool is absent, so a docs-only clone is never blocked.
- A hook that enforces project policy may exit non-zero. A hook that merely tidies must not.
