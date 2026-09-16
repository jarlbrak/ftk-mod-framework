# Shared material-slot parser tests

Links the exact pure content/helper parser against FTK's shipped Newtonsoft.Json. Covers integer/bounds/bijection checks, missing/unknown/nullable/mixed fields, filename bounds, snapshots and four-slot acceptance. An optional catalog argument checks every existing renderer still takes the unchanged legacy path.

```sh
dotnet run --project tools/ai-model-pipeline/material-slot-tests \
  -p:TestGameRoot="$PWD/scratch/mirewarden-game" -- \
  scratch/runtime-profile-395-basilight-v1/model-test-profiles.json
```

This yields447 assertions with that395-profile catalog;26 without it. It does not load meshes or make game calls.
