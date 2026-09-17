# Test instructions

Read the root `AGENTS.md` first.

- Tests here run without the game and without a Steam installation. Keep it that way: mock the
  layout under test rather than reaching into a real install.
- The installer suite exercises real BepInEx archives against mock Steam layouts. Preserve that
  split when adding cases.
- Shell tests must pass `shellcheck -s bash`, which CI enforces.
- A test that cannot run in a clean clone belongs behind an explicit live gate, not in this tree.
- Add the failing case before the fix, and state which gate the new test actually closes.
