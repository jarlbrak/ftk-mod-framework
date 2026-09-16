# Player Mayor empty renderer

Resource `player_mayor` resolves to CEL134799 and renderer121018. Its 34 bone references are real, but its mesh and all eight material references are null. It supplies no native inverse bind matrices or valid strict replacement profile. A fresh parse of all 96 native skinset rows found no avatar reference to this CEL.

Classify topology `94dbc18f21284ec2` as `resolved_no_mesh_no_bind_profile_no_native_skinset`. Retain it in the raw discovery inventory; do not count it as a usable bind profile, an untested enemy, or a completed model. Supporting this empty hierarchy would need a reviewed avatar/skinset construction path with an authored binding and material baseline. Another 34-bone rig is not a substitute.

[Source findings](findings.json) record exact resource, renderer, controller and source hashes. Native metadata and decompiled files remain in ignored scratch. No live loading, historical obsolescence or global non-use is proved.
