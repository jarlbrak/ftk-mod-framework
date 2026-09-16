# Honeyback Bear portrait v2 live validation

This immutable archive preserves the reviewed isolated session `c18481ccf3324ba9b65264d5cd01fa1f` for `ftkmf_modeltest_honeyback_encounter_camera`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/honeyback-portrait-v2/live-validation-v3-canonical --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
