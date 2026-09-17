# Agent harness instructions

Read the root `AGENTS.md` first.

- This is the client half of the opt-in test bridge. The in-game half lives in
  `FTKModFramework/Agent/` and the two must change together.
- The bridge is env-gated, loopback-only, and single-player test use only. Never widen the bind
  address, remove the env gate, or present it as a supported player feature.
- Direct action calls can desync co-op. Do not use the harness to make multiplayer claims.
- Keep the server dependency surface minimal and standard-library first.
- Harness observation is live evidence only when the observed build is the build under review.
  Record which build and which game installation produced a run.
