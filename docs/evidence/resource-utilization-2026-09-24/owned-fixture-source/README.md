# Frozen isolated resource fixture

These are the original framework-owned sources used for the resource measurements,
retained for audit and reproduction. This is an experiment, not a supported plugin
or a release component. No game implementation or assets are included. The Python
script constructs an original triangle GLB and a 1024 by 1024 RGBA PNG gradient.
It writes only under the repository's explicitly configured `scratch/perf-game`.

Use the repository's [in-game validation workflow](../../../../.agents/skills/ingame-smoke/SKILL.md)
and an authorized isolated copy. Build with `TestGameRoot` pointing at that copy,
then copy the resulting DLL while the game is stopped. Enable `FTK_RESOURCE_PROBE=1`
alongside the required model-test isolation variables and active helper. The tool
is disabled unless those gates match. Do not use in the Steam installation.

Atomically write a fresh ID and action to `resource-command.json`; outputs are
`resource-ID.json`. IDs must not be reused. The original fixture logs failures;
a missing result is a failed/incomplete experiment, not success.

- `texture-create`, `readable: true/false`, `loader: explicit/legacy` creates
  twenty separate allocation batches. The legacy roots remain inactive to avoid
  activating an incomplete native character component.
- `shared-create`, `readable: true/false` creates twenty renderers in one batch.
- `texture-state` records retained texture identities/readability and GPU readback
  SHA-256. Readback itself allocates temporary native buffers; use the snapshot
  before that readback, after at least three seconds of settling, for RAM deltas.
- `texture-clone` and `texture-destroy-source` exercise source-first teardown of
  a shared batch. `texture-clear`, followed by settling and `texture-state`, must
  show every pinned texture as destroyed. Do not manually destroy leased textures.
- `cap`, `fps: -1/30/60/90` temporarily changes the frame target.
- `texture-limit`, `limit: 0/1` temporarily changes only `masterTextureLimit`.
  This is a quality tradeoff. Restore both settings explicitly before exit.
- `inventory` is an out-of-band resource census. Per-object byte counters returned
  zero in the tested release player and were not used as memory evidence.

The readable control temporarily changes the single inspected `LoadImage(..., true)`
argument in the actual framework loader to false. It refuses a different IL shape
and removes its own transpiler afterward. All other loader behavior is unchanged.
Sharing comparisons use the pre-sharing and final framework hashes in the evidence;
the readable control does not disable the final transaction-local cache.

GPU readback is `Graphics.Blit` into ARGB32 followed by RGBA32 readback. Equal hashes
cover the tested image on this graphics backend, not every shader, mip level, model,
UV animation, or platform. The fixture uses private materials and original meshes;
it does not replace a vanilla asset or create a gameplay actor.

The existing final-owner resource ledger performs cleanup, including inactive legacy
roots. No save, combat, multiplayer, full-avatar visual, or other-platform acceptance
is implied. Stop the owned isolated game after the experiment.
