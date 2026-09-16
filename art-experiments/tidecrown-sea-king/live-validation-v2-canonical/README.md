# Tidecrown Sovereign live validation

This immutable archive preserves the reviewed isolated session `8b09f62e7e9a411199a46fa6466a0556` for `ftkmf_modeltest_tidecrown_sea_king`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/tidecrown-sea-king/live-validation-v2-canonical --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
