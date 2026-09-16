# Bramblecoil Viper live validation

This immutable archive preserves the reviewed isolated session `5d93bceb03c34fd68189eec896c1b233` for `ftkmf_modeltest_bramblecoil_jungle_snake`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/bramblecoil-jungle-snake/live-validation-v2-canonical --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
