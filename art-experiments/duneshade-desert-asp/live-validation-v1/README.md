# Duneshade Asp — scoped live validation v1

This archive preserves one fresh isolated-game exercise of Duneshade’s exact
`snakeDesertA` / `enDesertSnakeA` binding. The run measured a native pass, one
ordinary accepted attack with same-target HP `58 → 48`, and an explicit
`KillSingle` ragdoll fixture. The exact custom renderer remained active,
visible, and enabled through all 120 death-capture frames while all 13 native
skeleton ragdoll bodies became dynamic. Two guarded native Collect votes then
reached strict Ready at level 0 / room 2.

The `KillSingle` segment is a fixture, not ordinary lethal-damage evidence.
The ordinary attack records observed target HP only and do not infer a combat
cause. This archive does not establish portrait pixels, all-angle culling,
material lifetime, corpse presentation quality, or full campaign progression.
Metadata is stored losslessly as deterministic gzip files; source screenshots
are hash-pinned and only reviewed frames are copied into the archive.

Run `archive.py` once from the repository root after the named scratch evidence
exists. It refuses to overwrite a completed `validation.json` unless
`FTK_ARCHIVE_REBUILD=1` is explicitly set.
