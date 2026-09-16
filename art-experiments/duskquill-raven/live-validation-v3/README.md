# Duskquill Raven live validation

This immutable archive preserves the reviewed isolated session `8233b0fa916b47d78e7a6cf5a33ba625` for `ftkmf_modeltest_duskquill`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/duskquill-raven/live-validation-v3 --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
