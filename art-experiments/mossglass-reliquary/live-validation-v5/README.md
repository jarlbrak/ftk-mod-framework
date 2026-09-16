# Mossglass Reliquary live validation

This immutable archive preserves the reviewed isolated session `82de4aab601e4970b0a2c854f2b2d71e` for `ftkmf_modeltest_mossglass`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/mossglass-reliquary/live-validation-v5 --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
