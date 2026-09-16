# Hearthveil Blacksmith V1 live evidence

This archive records the fresh isolated session
`eeadf75d450540558db47a6908ae9667` for the original six-mesh
`blacksmith_Female` Hearthveil Blacksmith. It pins the player catalog hash
`2cb8a092702691735ae2b2900acf4ba37f6050e559048e4f7a10ff91eba7472c`,
the deployed authored assets, registration class ID 113, all raw transaction
metadata, selected live frames, and four presentation videos.

The run verifies the exact custom renderer identities on real overworld and
combat avatar owners. It records the native `armorCloth1` Body/Backpack
sequence `1/0 → 0/1 → 1/0`, switching the custom default armor and Gambeson
branches while retaining custom body, hair, and boots. Both prior owner leases
were observed retired: leases 4 and 6 were absent and all 15 pinned resources
for each were Unity-null.

The Gambeson branch has a 120-frame native attack capture with ordinary enemy
HP `72 → 62`, plus a 120-frame native pass capture with hero HP `970 → 913`
and two sampled `damageLight_blunt1H` responses. The post-pass fixture victory
reached the normal native Loot handoff; two guarded native Collect votes reached
strict Ready at level 0, room 3.

`selected/` contains review frames and `video/` contains 12 FPS presentation
derivatives. `metadata/` preserves non-image sources with lossless gzip. Every
raw source PNG has a hash in `source-image-pins.json`; no game binary, native
Unity asset, or extracted reference data is included.

Character-creation preview remains pending because the local Mac UI was locked
before the native creation screen could be opened. The sampled combat review is
not final art approval and does not cover every camera, culling condition,
death, final-owner teardown, skinset, apparel item, or weapon controller.

Rebuild only when intentionally replacing this evidence:

```sh
FTK_ARCHIVE_REBUILD=1 python3 \
  art-experiments/hearthveil-blacksmith/live-validation-v1/archive.py
```

The normal command refuses to overwrite a completed archive.
