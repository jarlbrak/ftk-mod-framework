# Bronzewake Champion live validation

This immutable archive preserves the reviewed isolated session `4049537c607648bfbf525e75570102ed` for `ftkmf_modeltest_bronzewake`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/bronzewake-champion/live-validation-v4 --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
