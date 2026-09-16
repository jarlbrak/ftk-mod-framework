# Tamarind Trickster fresh live arrival V2

This supplement records the fresh catalog-411 Tamarind run in session `d09d285440894f2f8eeca877568975b3` using `monkeyC`, renderer `enMonkeyBasey` (121301), and visual scale `1.0`. The custom mesh bound to the native enemy owner with 42 captured bones and remained readable in the settled arrival views.

The runner used the passive native-arrival protocol: it armed once, clicked native Ready once, and retained 120 PNG samples. The first 25 samples document entry-camera settling. Samples 40 and 60 show the authored monkey and held barrel clearly; sample 90 includes the native purple suicide effect; sample 119 records the renderer inactive after the native fixture transition.

Tamarind's native suicide behavior means this run cannot claim an ordinary hero hit or ordinary lethal damage. The capture records the bounded native health/visibility transition and event counts, while preserving the stale-field warning from the runner and the initial setup journal. No loot progression is attributed to this arrival capture.

The canonical gate treats received-hit motion and ordinary hero damage as not
applicable to this exact passive route. That exception is narrow and
machine-readable: the archived native `enSuicideCurse` action removes the source
before either phase can occur. Idle, native attack, native self-removal, exact
binding, reviewed appearance, and the strict pre-arrival Ready 0/2 state remain
positive evidence. No sibling monkey source inherits the exception or the
evidence.

`validation.json` preserves the case result, raw capture, setup and arrival journals, helper responses, profile and authoring manifests as gzip-lossless metadata, all 120 source-image hashes, seven root-reviewed originals and a 120-frame presentation video. Native payloads and DLLs are excluded. `archive.py` is offline-only and refuses a completed destination.
