# Gloamcap Trickster live validation

This immutable archive preserves the reviewed isolated session `db8b9c9d636e4d98a0670294d9f7010d` for `ftkmf_modeltest_gloamcap`. `validation.json` pins source metadata, original assets, every capture PNG, selected root-reviewed originals, and presentation derivatives. `visual-review.json` records the observed scope and limits.

Recheck archive integrity with:

```sh
python3 tools/ai-model-pipeline/verify_model_validation_archive.py art-experiments/gloamcap-imp/live-validation-v3 --check-video-metadata
```

An integrity pass verifies frozen artifacts only. It does not accept binding, motion, gameplay, or art quality beyond the specific reviewed records.
