# Emberjaw live validation

This immutable archive preserves the reviewed isolated session `b57007c89d554cf1a2533581a5481f75` for `ftkmf_modeltest_emberjaw`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/emberjaw-skull/live-validation-v3-top --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
