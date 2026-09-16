# Cairnfire Troll B V2: corrected palette live evidence

This archive preserves the fresh corrected-palette run against catalog hash
`32351b3c32bcb0dc6d0e84b81a89de319537578abf7a613f8cabe8f2d99f7a37`.
It follows the V1 visual rejection: V1 bound and animated correctly but mapped
the authored palette vertically backward. V2 reverses the PNG tile rows to
match the runtime V flip.

The exact target is `trollB` on `enTroll02(Clone)/enTroll01`, source renderer
`121256`, with the `trollController` and 37-bone signature
`fb452d83882447420a1beecc09d88cbf4e12a775fda39d57944a486c9c3fe402`.
The live renderer uses `ftkmf_glb_cairnfire-trollb.glb` and
`ftkmf_cairnfire-trollb.png`.

The trial has complete 120-frame Pass, ordinary Attack, and KillSingle fixture
captures. It observed ordinary same-target HP 72 to 62, fixture HP 62 to 0, one
native Collect, and strict Ready 0/2. `selected/` includes the six harness
review frames plus explicit native attack and ragdoll samples. `video/` contains
three 12 FPS presentation derivatives. `metadata/` stores gzip-lossless source
metadata and `source-image-pins.json` records every raw screenshot hash.

The result verifies exact binding, sampled visual motion, one ordinary HP loss,
fixture death, and Ready progression. It does not approve the art or establish
all cameras, all animation intervals, culling, portraits, resource disposal, or
campaign progression.

Rebuild only when intentionally replacing this evidence:

```sh
FTK_ARCHIVE_REBUILD=1 python3 \
  art-experiments/cairnfire-troll/live-validation-v2-palette-corrected/archive.py
```

The normal command refuses to overwrite a completed archive.
