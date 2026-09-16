# Rimecrown Sentinel live validation

This immutable archive preserves the reviewed isolated session `778135a3be964f95866364c47baf9d8a` for `ftkmf_modeltest_rimecrown`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/rimecrown-sentinel/live-validation-v7-hat --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
