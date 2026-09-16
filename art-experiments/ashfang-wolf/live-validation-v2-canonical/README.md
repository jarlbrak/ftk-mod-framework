# Ashfang live validation

This immutable archive preserves the reviewed isolated session `9c3eea20dff24b9f8e4c4321a6d1c96d` for `ftkmf_modeltest_ashfang`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/ashfang-wolf/live-validation-v2-canonical --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
