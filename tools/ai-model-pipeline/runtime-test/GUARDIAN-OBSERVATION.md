# Read-only Guardian observation

The isolated runtime helper's `guardian-state` command observes an existing native
combat encounter. It never starts combat, casts Guard, creates a designation,
forces damage, spends Focus or grants equipment. The existing isolated session
and command validation still apply. Extra payload keys are rejected.

```sh
python3 tools/ai-model-pipeline/runtime-test/command.py \
  --root "$PWD/scratch/paladin-game" guardian-state
```

The response records the frame and encounter identity, each registered player
dummy's native identity, class, HP, Focus fields, weapon and both hand inventories.
Guardian details come from the loaded framework's existing `State` and read-only
`StatusDescription`, `Identity`, `IsGuardian` and `CanAct` queries via reflection.
Stored active protection is reported separately from effective protection, which
also requires the guardian to be able to act. Non-guardians have a null rescue
availability. Active protectors are recorded for every player target. The
`Focus` action's two current positive key bindings and modifier flags are included
from the native remapping table, rather than assuming a default keyboard key. The
existing encounter logger is serialized through its read-only `SerializeLogInfo()` method, including
the current active entry; no log entries are added or completed by the observer.

Capture before and after ordinary UI actions or native turns. A snapshot proves
only the state at that frame; it does not establish damage reduction, action cost,
healing, rescue or duration without corresponding native observations. An absent
combat encounter or missing framework query fails explicitly. No framework
internal is modified or exposed as a public content authoring API.

Build this opt-in helper against a configured isolated copy, with an explicit
output outside the running game's plugins:

```sh
dotnet build tools/ai-model-pipeline/runtime-test/RuntimeModelTest.csproj \
  -c Release -p:TestGameRoot="$PWD/scratch/paladin-game" \
  -p:TestManagedDir="$PWD/scratch/paladin-game/PaladinTest.app/Contents/Resources/Data/Managed" \
  -o "$PWD/scratch/paladin-guardian-observer-build"
```

A successful build is not live observation. Deployment/restart and capture remain
separate steps owned by the isolated trial operator; never replace a running
helper or deploy to the original installation to obtain this snapshot.

The focused authority-boundary checks are:

```sh
python3 tools/ai-model-pipeline/runtime-test/test_guardian_observation_readonly.py
```

These check the fixed reflection query list and absence of native mutation calls.
They do not execute a native encounter or prove reflection availability at runtime.

## Armor effect snapshots

`armorDummies` includes every non-null player and enemy dummy in the current
encounter rosters, including dead or fled entries with explicit flags. Each entry
pins native FID and Unity instance identity and records HP, `ArmorMod`, taunt armor,
and whether an Armor-category proficiency is present. A present record includes
count, remaining/full combat timeline time, proficiency instance and numeric ID,
custom value, and end-on-turn flag. Absent effects are explicitly null; malformed
present records fail the command rather than appearing absent.

Compare the same dummy before Censure, after its native hit, and after ordinary
combat advancement. Record the initial baseline; `ArmorMod` is a modifier, not total
armor. The taunt field distinguishes a changed taunt contribution. These snapshots
do not advance timers, apply effects, invoke expiry, or retain mutable native
objects between commands. They establish only state at capture frames, so a full
application/expiry claim still requires native action and timeline evidence.
The native field and lifetime evidence is in
[the Censure receipt](../../../docs/paladin/censure-native-lifetime.json).

### Stored combat outcomes and Ward categories

Each `armorDummies` entry also includes `maxHp`, current suffering categories with
proficiency IDs/counts, and `storedCombat` containing native attack/received-damage
records and the accumulated post-attack health modifier. FIDs use explicit
`turnIndex:photonId` serialization. Player maximum HP comes from `MaxHealth`;
enemy maximum HP uses the native context-sensitive `GetHealthTotal` query.
Invalid suffering records fail the snapshot rather than masquerading as absent.

Stored damage fields include attacker/victim, damage/resulting health, AoE,
response, proficiency ID/success/affect/immunity and attacker health modifier.
These fields can survive a previous attack. Match identities and the native
combat-log sequence before attributing them to a trial. They are not a hook-based
history or proof that a future hit occurred. For Verdict, compare before/after HP
and count native `takes secondary damage` log events; for Ward compare native
proficiency flags and category presence across active and inactive Guard trials.
No native method that applies effects or damage is invoked by these snapshots.
