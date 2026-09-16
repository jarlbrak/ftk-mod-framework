# Abyssal Crown V4 live validation

This immutable archive preserves the reviewed isolated session `bd9c59404bde493db8d9e1e7abfa7ec5` for `ftkmf_modeltest_abyssal_crown_kraken_head`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/abyssal-kraken/live-validation-head-v5-canonical --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
