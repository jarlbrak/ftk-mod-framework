# Royal Sargassum V3 Sea King live evidence

This supplement records two fresh isolated-game trials for the V3 Sea King tentacle profiles: `seaKingTentacleA` with `krakenTentacleController`, and `seaKingTentacleB` with `krakenTentacleControllerMirrored`. Both profiles bind the exact `KrakenGodTentacle` renderer to the original V3 GLB and V3 texture with native emission disabled.

Each target has a complete 120-frame pass capture and a complete 120-frame ordinary attack capture. The ordinary attack changes the same target from HP `270` to `266`. The explicit `KillSingle` fixture changes HP `266` to `0`, then reaches the expected renderer-destroyed boundary after 91 retained frames. Both sessions reach strict Ready at level `0`, room `2`.

The V2 record was rejected because inherited `matLoot` emission produced a bright yellow stripe. V2.1 disabled emission but was also rejected because its silhouette and palette still read as a bright bamboo-like limb. V3 uses a narrower original dark teal tendril design. Sampled motion grids for both native controllers show it staying connected without an obvious catastrophic deformation in the reviewed frames.

This is not full visual acceptance. The reviewed samples do not cover every frame, camera angle, culling condition, encounter layout, portrait, or resource-lifetime path. The death records are renderer-destroyed prefixes rather than complete death captures, and the ordinary HP change does not identify a specific animation or damage event.

`validation.json` pins the exact catalog, deployment, authoring assets, raw captures, selected originals, six presentation videos, historical rejection records, and scoped limitations. Metadata is stored gzip-losslessly; source PNGs are retained by hash except for selected originals and review derivatives. Native game payloads and DLLs are excluded. `archive.py` is offline-only and refuses to overwrite a completed archive unless `FTK_ARCHIVE_REBUILD=1` is set.
