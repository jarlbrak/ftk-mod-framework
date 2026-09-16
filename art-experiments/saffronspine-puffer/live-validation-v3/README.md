# Saffronspine Puffer live validation

This immutable archive preserves the reviewed isolated session `102ee86f20ff4554b106104cea4f50d8` for `ftkmf_modeltest_saffronspine`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/saffronspine-puffer/live-validation-v3 --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
