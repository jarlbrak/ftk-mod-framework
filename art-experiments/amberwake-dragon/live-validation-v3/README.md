# Amberwake Dragon live validation

This immutable archive preserves the reviewed isolated session `58f2a30a9aaa47cfa700ced9590ccc5f` for `ftkmf_modeltest_amberwake`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/amberwake-dragon/live-validation-v3 --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
