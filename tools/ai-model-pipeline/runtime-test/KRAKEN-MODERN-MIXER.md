# Controller-free modern Kraken input experiment

This optional fixture tests an input convention, not a gameplay adapter. Existing controller, endpoint, marker and organic-skin requests remain unchanged. Do not combine this mode with endpointPolicy, observeExistingDrivers, or an armed skin fixture.

Submit the following exact JSON through the existing owned helper command transport, using a fresh 32-hex command ID and the current session nonce. Existing isolated-root, single-player and strict Ready guards apply.

```json
{"id":"<fresh32hex>","session":"<current32hex>","op":"kraken-controller-fixture","scenario":"damaged","modernInputMixer":"two-clip-raw-v1"}
```

Supported scenarios are `appear`, `damaged`, `damaged-heavy`, `death` and `death-light`. Record a distinct same-session repeat for each scenario. Preserve both requests and raw reports. This mode creates a separate Transform/Animator tree with the native generic Avatar, a manual two-input mixer and two clip playables. It has no controller, CEL, gameplay scripts or event receivers. The original two owned controller surfaces remain read-only to the sampler.

Each of the 241 frames freezes the current state and active next state immediately after the native evaluation. It uses those captured clocks and raw weights, ignores inactive next-clip metadata, and resets every sampler local TRS to immutable prefab rest before both first and repeated evaluations. Clip replacement preserves exactly three playables. Full reset is an experimental convention; WriteDefaults/history differences must remain numerical failures. Every main-to-main transition and all post-appearance pure-main frames are compared at unchanged `1e-5` tolerance. A positive appearance contribution excludes only that frame from native mixed-pose equivalence. Zero-weight appearance boundaries remain eligible. The existing authored appearance endpoint policy is a separate result.

`ok` reports fixture mechanics, not numerical acceptance. First and repeated local/model poses, errors and all 241 frames survive numerical mismatches. Root and six modern driver poses are independently compared; complete source-tree invariance is additionally recorded as runtime hash evidence. Cleanup must destroy only the added graph/tree and leave native source/input transforms unchanged.

Installed Unity 2017 API evidence is in `scratch/AnimationClipPlayable.mixer-analysis.cs` and `scratch/AnimationMixerPlayable.analysis.cs`. The installed clip playable supports `SetApplyFootIK(false)` but exposes no ApplyPlayableIK control. Reports therefore record `playableIK:null` and `playableIKControlAvailable:false`; they do not assert that unavailable setting is false. The generic Animator has events and root motion disabled, AlwaysAnimate culling, and no IK receiver scripts.

Create a manifest with exactly `schema: "kraken-modern-input-mixer-v1"` and `evidence`. Evidence has six `{path,sha256}` pins: `first`, `firstRequest`, `repeat`, `repeatRequest`, `assets` (the pinned native resources.assets), and `gameAssembly`. The verifier rejects other schemas, changed source hashes, state/clock/clip identity changes, frame gaps, invalid TRS/model chains, skipped main frames, changed ownership, or failed cleanup.

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/verify_kraken_modern_mixer.py \
  --manifest scratch/<case>/manifest.json --output scratch/<case>/verification.json
```

A numerical match establishes only this owned modern-input experiment. It does not establish a live source mapping, attack callbacks, multiplayer behavior, final skin appearance or production adapter lifetime.

## Fixed graph binding A/B

`modernInputMixer: "fixed-four-raw-v1"` is a separate experiment. It preserves the first `two-clip-raw-v1` trial and its mismatches. Those failures suggested a dynamic playable binding/cache issue; the native engine implementation is opaque, so that diagnosis is a hypothesis.

The fixed mode creates four native clip playables in exact slot order `krakenIdle`, `krakenDamage`, `krakenDisappear`, `kraken_appear` and connects all four before activating/rebinding the Animator. The graph has five playables including its mixer. It never replaces a playable during the run. Before each first/repeated evaluation it resets immutable local TRS, zeros every slot's time/weight, then assigns only the captured current/active-next roles to their exact native clip slots. Two active roles sharing a clip are rejected, even if clocks match; no weights are combined and no clock is guessed. Inactive slots retain exact zero time/weight. Active zero-weight appearance inputs keep their observed clock and remain main-comparison eligible.

The same verifier and six-file manifest accept this explicit mode. The report pins all four slot clip identities, including zero-weight slots; paired runs must keep them stable. It checks five playables, exact mapped clocks/raw weights, main/history coverage and all prior `1e-5`/repeat/source/cleanup boundaries. A match in this mode would support only the fixed-binding input convention, not validate the failed dynamic graph or establish a live adapter.
