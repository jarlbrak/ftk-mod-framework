# Non-enemy topology ownership, V1

`findings.json` classifies the six candidate topology groups with no direct
serialized `FTK_enemyCombat` renderer row in the pinned FTK build. It records
whether each group is a separate ResourceManager-prefab route, a player
skinset-avatar route, a mixed player/resource route, or an unsupported empty
renderer.

It is an ownership record, not a model-validation result. A resource path does
not prove combat use, a skinset path does not prove every class outfit, and the
Mayor placeholder has no current strict-swap profile. The source hashes in
`inputs` bind these findings to the local inventory, decompiled source evidence,
and runtime index used to make the classification.

Regenerate the derived coverage plan only after those inputs are current:

```sh
python3 tools/ai-model-pipeline/audit_topology_coverage.py \
  --ownership docs/evidence/nonenemy-topology-ownership-v1/findings.json \
  --output-json docs/model-topology-coverage-plan.json \
  --output-markdown docs/MODEL-TOPOLOGY-COVERAGE.md \
  --overwrite
python3 tools/ai-model-pipeline/test_audit_topology_coverage_current.py
```

When a pinned input changes, repeat the source research and update this finding
with the new evidence before regenerating the plan. The original-geometry and
live-validation archives named by the finding remain immutable; this ownership
index may gain a new, separately scoped route record. Do not use a direct enemy
record for a resource or player route, or use a matching joint count as a
substitute for an exact source and controller.

The current runtime-index revalidation records the canonical Mirewarden
`trollCaveA / enTroll01 / renderer 121153`, the separate canonical Gloamcap
`impA / enbaseyimp / enBaseyImp / renderer 121117` resource route, Duskquill
`crowC / enCrow / renderer 120964`, Lunacrest
`clamA / enClam / renderer 121306`, Bronzewake
`bossGladiator / armorBossGladiator / renderer 121522` and
`bossGladiator / enBossGladiator / renderer 121272` and
`bossGladiator / bootsBossGladiator / renderer 121661` and
`bossGladiator / hairBottomBossGladiator / renderer 121500`, Tideglass
`crabB / enCrabWizard / renderer 121411`, Cinderwing
`batA / enBat01 / renderer 121104`, Saffronspine
`pufferA / enBlowFishA / renderer 121509`, and Copperveil
`spiderB / enSpiderB / renderer 121386`, and Reefstrider
`fishA01 / enFishA / renderer 121695`, and Basilight
`cockatriceC / enChicken / renderer 121484`, and Mossglass
`cubeA / enJellyCube / renderer 121012`, and Emberglass
`beeA / Monster Bee / renderer 121062`, and Vesper Eye
`beholderA / EyeBody and EyeBody/EyeEye / renderers 121031 and 121210`
direct-enemy evidence outside these six
ownership classifications. Mirewarden credits only the direct-enemy route and
Gloamcap credits only the resource-prefab route in their shared topology group;
neither transfers credit to the other route or to sibling sources. The six
classified groups, including the separately owned
`enbaseysnake` resource route, retain the same source renderer IDs,
skinset ownership, and unsupported Mayor placeholder state.
