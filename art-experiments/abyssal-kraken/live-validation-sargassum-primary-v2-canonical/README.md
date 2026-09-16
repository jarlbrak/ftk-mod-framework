# Sargassum Lash V2 live validation

This immutable archive preserves the reviewed isolated session `a3b768dc6a234148ba14aa6d3fd8d9cf` for `ftkmf_modeltest_sargassum_kraken_tentacle`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/abyssal-kraken/live-validation-sargassum-primary-v2-canonical --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
