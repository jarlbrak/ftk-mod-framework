# Copperveil Weaver live validation

This immutable archive preserves the reviewed isolated session `1e61830193c74dc092ad870ca523e023` for `ftkmf_modeltest_copperveil`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/copperveil-spider/live-validation-v3 --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
