# Mournglass Wraith live validation

This immutable archive preserves the reviewed isolated session `93206406999a452aa762a31c925fa9a8` for `ftkmf_modeltest_mournglass`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/mournglass-wraith/live-validation-v2-canonical --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
