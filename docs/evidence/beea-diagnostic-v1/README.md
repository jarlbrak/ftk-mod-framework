# beeA diagnostic baseline

The exact beeA `Monster Bee` renderer121062 / 47-bone binding has scoped native hover, attack, ordinary nonlethal hit and ragdoll evidence. This uses original calibration geometry, not finished Emberglass art and not acceptance of the other five exact bind/controller pairs in this topology.

`validation.json` pins source findings, session, profile, binaries, catalog, capture telemetry, summaries and reviewed media. Each `.capture.json.gz` decompresses to the byte-exact original120-frame JSON. Each MP4 retains all120 screenshots at12fps. Raw telemetry includes transforms only, not extracted native surface geometry.

Parent reviewed pass0/40/50, hit30, death40/60. Attack effects obscure pass50. Ordinary hit applies8damage58→50; death uses an explicit kill fixture50→0. All16 rigidbodies become dynamic at frame27, when Animator becomes disabled. Speed is1.36871 at40 and zero at60/119. Renderer stays active throughout. `m_DoRagdoll` is already true before death; it is a native flag, not itself proof of dynamic motion. IsSleeping was not recorded. One guarded Collect reaches strict Ready0/2.

Material, portrait, culling, ordinary lethal damage, DeathLight and family-wide art acceptance remain outside this diagnostic verdict. See [validation](validation.json) and [ragdoll measurements](ragdoll-audit.json).
