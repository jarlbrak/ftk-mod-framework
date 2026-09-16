# Agent bridge instructions

Read the root and `FTKModFramework/AGENTS.md` first.

- The bridge is opt-in, loopback-only, single-player test infrastructure.
- Preserve capability boundaries, once-only actions, bounded waits, and explicit state snapshots.
- Do not add production cheats or silently broaden what a test command can mutate.
- Keep runtime evidence distinct from fixture actions. A fixture-assisted hit or death is not
  ordinary gameplay evidence.
- Update `harness/README.md` and focused tests when the command contract changes.
