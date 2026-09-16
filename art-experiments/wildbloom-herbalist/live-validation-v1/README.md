# Wildbloom Herbalist V1 native preview evidence

This immutable archive records isolated session
`4cae860e36f640099f5a2dbf161fc7db`. FTK reached its real Party Select screen
through its active Create Game callback, then Wildbloom Herbalist was selected
through the visible native Party Select UI. The helper did not construct an
avatar or select a class.

The strict observation records Player 1 as class ID 114 on the exact
`herbalist_Female` skinset. Its game-owned `uiQuickPlayerCreate` owner and
avatar were reciprocal, attached to the native pedestal, and held the three
expected original meshes: body, crown, and seven-bone lower hair.

The archive preserves a settled 24-frame, 12-FPS fixed-step capture of the
visible `player_Herbalist` body. Every frame retains the exact owner, avatar,
renderer, mesh, bone signature, and visibility while native
`standardIdle_handsDown` advances for 1.9166259765625 seconds of measured game
time. `selected/` contains a review frame; `video/` is a presentation
derivative. Raw capture metadata and hashes for every raw PNG are retained.

The native preview visibly shows the intended green, petal-crowned woodland
figure on Player 1's pedestal. This is a sampled preview review, not final art
approval. It does not establish overworld or combat behavior, attacks, hits,
equipment branches, progression, teardown, portrait behavior, multiplayer, or
every camera and culling condition.

Build only when intentionally replacing this archive:

```sh
FTK_ARCHIVE_REBUILD=1 python3 \
  art-experiments/wildbloom-herbalist/live-validation-v1/archive.py
```

The normal command refuses to overwrite completed evidence.
