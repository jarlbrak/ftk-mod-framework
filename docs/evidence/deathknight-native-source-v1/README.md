# Deathknight native topology and behavior reconciliation

Seven rows resolve to five native renderers sharing exact mesh2385, ordered36bone
names and inverse bind matrices with representative121217. This establishes source
binding compatibility only. Each row retains separate material, weapon, stats,
controller, scale and native eligibility evidence in findings.json.

| Row | CEL | Renderer | Weapon | Combat controller | Root scale |
|---|---:|---:|---:|---:|---:|
| deathknightA |137586|121217|134921|5983 blunt|1.2|
| deathknightB |137587|121218|134921|5983 blunt|1.2|
| deathknightC |137590|121221|134921|5983 blunt|1.2|
| deathknightDboss |137588|121219|134922|5983 blunt|1.3|
| deathknightEboss |137589|121220|135088|5982 bladed|1.3|
| deathknightEbossEasy |137589|121220|135088|5982 bladed|1.3|
| harazuelMinionB |137590|121221|134923|5983 blunt|1.2|

Choose deathknightA first: exact representative renderer, ordinary dungeon spawning,
lowest native health50/physical armor12, one enDeathKnightDaze proficiency and no
AttackSchedule. Native chanceToProf1 makes that the ordinary RNG-path expectation,
not a substitute for observing the actual first action. Armor may produce zero
hero damage; preserve unmet-hit evidence rather than changing native defenses.

Dboss, Eboss/EbossEasy and harazuelMinionB have shuffled AttackSchedule components.
Serialized item0 is not the actual first action after Initialize shuffles. Schedule
selection precedes RNG chanceToProf, including harazuelMinionB's chance0. Their
seven unique weapon proficiencies all have m_Suicide0 in the actual table. No
first-turn suicide route is indicated, but generic flee/quest paths are not ruled
out. Dboss/Eboss/EbossEasy and harazuelMinionB disable natural dungeon spawning.

The body is one skinned renderer, but the native helmet and shield are two separate
rigid renderers. Weapon prefabs add further rigid geometry and effects. A body-only
original does not replace these. C/harazuel share ice material, two particles and a
light. Other variants use matDeathKnight or matDeathKnightD; all three body materials
retain emission maps/keywords and metallic maps. No body ScrollingUVs was found.

Each body hierarchy includes13Rigidbody,10CharacterJoint and16colliders, including
equipment. Native OnEnable detects joints and enables ragdoll behavior; DoRagDoll
skips arrow/detachable bodies and disables Animator. Do not require all13bodies to
become dynamic. Blunt and bladed death clips differ: heavy clips include drop and
DeathFallOff events, while bladed light death has Foley only. Actual trigger,
ragdoll pose, equipment drops and final native disposal need live observation.

Reproduce from repository root, keeping all outputs ignored local scratch:

```sh
scratch/model-venv/bin/python scratch/deathknight-native-topology-analysis/reconcile.py
scratch/model-venv/bin/python scratch/deathknight-native-topology-analysis/finalize_findings.py
```

The first script loads actual resources, reads native level1 rows, resolves
renderers/ordered bones/IBMs/materials/controller clips and weapon components, and
decompiles seven exact types using installed ilspycmd10.1. The second crosschecks
all seven parsed row prefixes against sharedassets1 table5124, parses proficiency
flags and schedules, joins the pinned runtime catalog and writes findings/source
pins. These are verified field-prefix recognizers, not a full custom serializer.
Native geometry, texture pixels and animation curves are not exported. Raw native
metadata and decompiled C# remain in this ignored directory; archive only scoped
findings/scripts/pins later. No game, UI, bridge or deployment action occurred.
