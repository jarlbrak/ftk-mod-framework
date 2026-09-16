# Basilight Cockatrice live validation

This immutable archive preserves the reviewed isolated session `cacb6f33c8324199b1ad09f921b5f6ae` for `ftkmf_modeltest_basilight`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/basilight-cockatrice/live-validation-v3 --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
