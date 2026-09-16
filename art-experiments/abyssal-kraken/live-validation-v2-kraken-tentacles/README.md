# Sargassum Lash V2: fresh primary and mirrored Kraken tentacle evidence

This archive preserves two separate fresh isolated-game trials using the current catalog hash `51f22c64607feebb3241961023b7c642c3e30f30b3b7f5478794da399253df79` and the same original V2 asset pair.

| Trial | Exact native chassis | Runtime renderer | Controller | Observed ordinary hit | Ready |
| --- | --- | --- | --- | --- | --- |
| Primary | `krakenTentacle` | `enKrakenTentacleNew(Clone)/krakenTentacle`, source renderer `121595` | `krakenTentacleController` | 162 to 154 | 0/2 |
| Mirrored | `krakenTentacleMirror` | `enKrakenTentacleNew(Clone)/krakenTentacle`, source renderer `121595` | `krakenTentacleControllerMirrored` | 162 to 152 | 0/2 |

Both trials bind `ftkmf_glb_sargassum-kraken-tentacle-v2.glb`, the exact 20-bone signature `01c1b372052d71050f30a65512c4578a217e10ea3a9e777e7a985a0314ed78bd`, and `ftkmf_sargassum-kraken-tentacle-v2.png`. Each has complete 120-frame Pass and Attack captures, followed by a 91-frame `KillSingle` fixture-death prefix. The fixture is not evidence of ordinary lethal combat.

`validation.json` records the two exact chassis separately. `metadata/` holds deterministic gzip-lossless copies of source metadata. `source-image-pins.json` pins all 666 source PNGs, including raw captures and authored source images. `selected/` contains six review frames per chassis, and `video/` contains six presentation derivatives at 12 FPS. `integrity.json` records every archive hash.

Rebuild only when intentionally replacing the evidence:

```sh
FTK_ARCHIVE_REBUILD=1 python3 art-experiments/abyssal-kraken/live-validation-v2-kraken-tentacles/archive.py
```

The ordinary command refuses to overwrite an existing archive. This result proves exact binding, sampled live motion, an ordinary same-target HP change, fixture-death prefix, and Ready progression. It does not approve the art, portrait framing, all camera angles, all animation intervals, culling, or resource disposal.
