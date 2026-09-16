# Two-bank native Kraken input experiment

`fixed-two-bank-five-raw-v1` is an additional input-only mode. The frozen43529
and0c65 helpers, their original four-clip/dynamic modes and composed endpoint/skin
records remain separate. No production adapter or attack-state support is added.

At strict Ready in the isolated single-player helper, submit one fresh request:

```json
{"id":"<fresh32hex>","session":"<current32hex>","op":"kraken-controller-fixture","scenario":"intro","modernInputMixer":"fixed-two-bank-five-raw-v1"}
```

The same mode also accepts the five existing scenarios: appear, damaged,
damaged-heavy, death and death-light. Those remain241 frames. Intro alone uses361
frames at the same1/60-second manual step, with one native Intro trigger after
step15. No arbitrary trigger or manual state change is accepted. Other newly
audited safe states are not yet exposed. Endpoint policies, pending skin arms and
driver-observation options are rejected with this input mode.

The sampler permanently connects ten clip playables to one mixer before Animator
activation and Rebind. Each bank contains Idle4921, Damage5488, Disappear5501,
Appear5513 and Attack4771. Bank0 represents current; bank1 represents active next.
Every unused slot receives exact zero weight and time. Active roles preserve their
captured raw weights and independent mapped clocks, even when both reference Idle.
No role aggregation, topology replacement, extra normalization or later Rebind is
performed. The immutable full modern rest is restored before each first/repeated
zero-delta evaluation. This reset remains an experimental input convention.

Reports retain both banks' clip identities, slot roles and eleven distinct opaque
playable handle hashes. Runtime checks also compare each actual connected handle
to the original playable. Handle hashes are diagnostics, not Unity object IDs.
All model/local poses, captured clocks, raw weights, resets, engine errors, source
invariance and deferred cleanup remain available. The sampler has no controller,
CEL or callback receiver; Animator events/root motion and clip foot IK are off.
The installed Unity API has no playable-IK control, which remains explicitly
reported unavailable.

The native controller surfaces accept only IDLE and the selected audited state
before and after each Evaluate. Exact pinned controller5973 has no behaviour range
for INTRO1746749458; its three attack states do have gameplay SMBs. Loading the
Attack clip into a controller-free zero-weight sampler slot does not authorize
triggering those states. Attack clip-only sampling and actual attack callbacks
remain separate future tasks.

Use the existing six-pin `kraken-modern-input-mixer-v1` manifest and verifier:

```sh
scratch/model-venv/bin/python tools/ai-model-pipeline/verify_kraken_modern_mixer.py \
  --manifest scratch/<case>/manifest.json --output scratch/<case>/verification.json
```

For Intro, verification requires all361 frames, pure target, entry and exit with
same-clip/different-clock roles at positive weights, and subsequent pure IDLE on
both native surfaces. Each bank's unused clocks and weights must stay zero and all
node mappings remain stable. Whole-controller main comparisons and repeat checks
retain1e-5. Positive appearance contribution is excluded from main equivalence;
zero-weight appearance boundaries remain included.

All five existing first/repeat scenarios must be recertified with this expanded
binding union: merely adding Attack curves may change WriteDefaults behavior.
Intro also needs its own live pair. Synthetic tests verify corruption rejection,
not Unity binding or motion. Full15-state coverage, duplicate appearance endpoint
composition, attacks/events, native avatars and production lifetime remain open.
