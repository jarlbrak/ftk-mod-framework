# Cinderbloom live validation

This immutable archive preserves the reviewed isolated session `572176f3391e44e192e7112d6ed3b5ee` for `ftkmf_modeltest_cinderbloom`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/cinderbloom-plant/live-validation-v5-body --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
