# Model pipeline instructions

Read the root `AGENTS.md` first.

- Keep direct-enemy, resource-prefab, player-skinset, and rigid-renderer routes distinct.
- Binding evidence never transfers merely because two routes share a skeleton or bone count.
- Generated ledgers are derived artifacts. Update their generators or inputs, then regenerate and
  run the focused current-ledger tests.
- Preserve source hashes, exact renderer paths, route identity, and stated evidence limitations.
- Use isolated game copies for live trials. Keep bulk media and private game data outside ordinary
  git history.
- Offline export, static validation, live binding, motion, gameplay, lifecycle, and art acceptance
  are separate gates.
