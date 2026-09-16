
## Optional endpoint policy diagnostic (not yet live verified)

Add `"endpointPolicy":"main-appearance-local-v1"` to the existing fixed-scenario
request. Omitting it preserves graph observation. Unknown values fail closed.
The diagnostic has distinct provenance `owned_controller_endpoint_policy_diagnostic`.
It owns separate full-strength clip sampler pairs and a Transform-only output tree.
Current/next state clocks already contain entry offsets; looping phases use modulo,
nonlooping phases clamp. Stale next clips outside transitions are ignored.

Every observed pure frame compares independent rest-reset sampling against both
native graph trees, including local/model matrices, root, jaw and post-appearance
idle. A mismatch fails the diagnostic; immutable rest is never redefined.
Mixed-clock endpoints are explicitly constructed policy inputs, with no claim of
native mixed-pose equivalence. Main endpoints use the verified rest-relative model
mapping; appearance uses independent native old poses. Each converts through its
own endpoint parent, with joint1 under the actual native old root, before four
locals are blended once at the raw appearance weight. No appearance retains the
whole modern graph mapping. The output mirrors native root and jaw LOCAL; native
graph inputs remain untouched. This does not prove jaw model equivalence or visual
continuity. Exact endpoint reconstruction and repeated identical commits are
checked. Negative guards cover weights, clip identity, clocks, nonfinite values,
singular/reflected/sheared transforms and cleanup.

Acceptance requires source review and actual five-scenario repeat captures, pure
baseline agreement, independent output reconstruction, branch boundary inspection,
all owned graph/root cleanup and unchanged Ready identity. Finite output alone is
not visual acceptance or public old-rig support.

### Exact scalar rotation interpolation

The first live appearance diagnostic completed its internal checks but failed the
independent pose verifier. Retained frame37 neck data distinguishes normalized
linear interpolation from the declared spherical policy. The diagnostic now
uses `KrakenEndpointRotation`: normalized double-precision shortest-arc scalar
SLERP with Math trigonometry, followed by a float quaternion at the Unity boundary.
There is no close-angle linear cutoff; only the numerically coincident dot=1 limit
uses linear coefficients. The pose verifier's 1e-5 tolerance remains unchanged.
Thirty [linked scalar tests](../endpoint-rotation-tests/README.md) cover the retained
case, coupled small angles, endpoints, signs, float precision and invalid inputs.
Fresh Unity captures must pass the unchanged independent verifier before claiming
that this correction resolves the live discrepancy.
