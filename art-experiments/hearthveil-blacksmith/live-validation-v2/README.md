# Hearthveil Blacksmith V2 native preview evidence

This immutable supplement records isolated session
`67ca8d0d0a704d68a0559a0aa19ac3c4`, which reached FTK’s real Party Select
screen through the enabled native Create Game callback. FTK created its own
room, map, character-create UI, pedestal, and three preview avatars; the helper
only invoked `StartGameFE.GameConfig.OnStartGame()` after normal menu navigation
had reached that screen.

The native Player 1 preview was class ID 113, `Hearthveil Blacksmith`, on the
exact `blacksmith_Female` skinset. Its `uiQuickPlayerCreate` owner and avatar
were reciprocal, mounted on the game-owned pedestal, and held the expected
original `playerBlacksmith`, `hairTop`, `hairBottom`, default-armor, and boots
meshes. The selected default outfit has no item59 equipped, so its separate
Gambeson branch is deliberately absent from this preview.

The archive preserves a 24-frame, 12-FPS fixed-step capture of the exact visible
`playerBlacksmith` renderer. All frames retain the same preview owner, CEL,
renderer, mesh, bone signature, and visibility; native
`standardIdle_handsDown` advances for 1.916 seconds of measured game time.
`selected/` contains a review frame and `video/` contains a 12-FPS presentation
derivative. Raw capture metadata and every raw PNG hash are retained.

This V2 record supplements, and does not replace, the historical
[V1 archive](../live-validation-v1/README.md). V1 contains the separate
combat, item59 apparel-rebuild, lease-disposal, Loot, and Ready evidence.
Neither archive claims every camera or culling condition, the other apparel
branch in this preview, player death, final-owner teardown, portrait behavior,
multiplayer, or final art approval.

Build only when intentionally replacing this V2 evidence:

```sh
FTK_ARCHIVE_REBUILD=1 python3 \
  art-experiments/hearthveil-blacksmith/live-validation-v2/archive.py
```

The normal command refuses to overwrite a completed archive.
