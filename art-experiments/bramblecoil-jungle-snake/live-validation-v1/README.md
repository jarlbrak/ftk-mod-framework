# Bramblecoil Viper — scoped live validation v1

This archive preserves a fresh isolated-game validation of Bramblecoil’s exact
`snakeJungleC` / `enJungleSnakeC` binding. It records three narrow claims on the
same catalog and deployed DLLs:

- a semantic native `EnemyDummy.PlayAnim(DeathLight)` trigger held the exact
  custom body visible for all 24 fixed-step frames;
- paired explicit `KillSingle` fixtures showed the opted
  `preserve-custom-body` renderer remain visible through native `Snake_DeathBig`
  and 26 dynamic ragdoll bodies, while an otherwise identical policy-omitted
  control hid the custom renderer and activated the native 13-piece fall-off
  body;
- a separate fresh opted fixture reached the game’s strict native Ready state
  at level 0 / room 2 after one guarded native Collect click.

`KillSingle` is an explicit fixture, not ordinary combat damage or ordinary
lethal proof. The archive does not establish portrait pixels, all-angle
culling, material lifetime, corpse presentation quality, or ordinary attack
hit/damage behavior. Metadata is stored losslessly as deterministic gzip files;
source screenshots are hash-pinned and only manually reviewed frames are copied
into this directory.

Run `archive.py` once from the repository root after the listed scratch inputs
exist. It refuses to overwrite a completed `validation.json` unless
`FTK_ARCHIVE_REBUILD=1` is explicitly set.
