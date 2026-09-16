# Owned native material lifecycle fixture

`material-lifecycle-fixture` is a separate opt-in mutation fixture using original synthetic assets and detached no-CEL objects. It does not clone game avatars or modify native prefabs. It requires the existing isolated-root/session/single-player strict Ready guard, the exact reviewed Core SHA `19ece392cf3d58fe43b6178733a9a5ecd85c51943f9cd1769060b88fb572b735`, and exactly its sole installed ScrollingUVs prefix. It pins current Core file/module identity, party references and Ready dungeon/level/room across yielded frames. The file hash describes the measured file, not a hash of loaded memory.

Submit the exact command through `command.py` (which supplies the command ID and current nonce):

```json
{"id":"<fresh32hex>","session":"<current32hex>","op":"material-lifecycle-fixture"}
```

No arbitrary assets, types, methods, renderer IDs or scene objects are accepted. The helper stays busy throughout deferred cleanup. No camera or time setting is modified. Every owned object uses a layer excluded by all active cameras; if no such layer exists or this changes, the fixture refuses/stops.

Each of two fixed lineages starts with an original two-submesh mesh, one owned bone, two tiny textures and two materials. Exact production SetResources/SetTargets/AddScrollingTarget and Applied assignment prepare the source owner. This setup is explicit fixture preparation, not proof of public GLB registration/binding. The existing live cubeA binding trial covers that separately.

Unity Instantiate creates an active clone with disabled renderer from the source, then an active grandclone from the already-privatized clone. Native Awake must acquire each existing lease without helper retention. Native LateUpdate must advance phase while disabled without adding materials. Enabling each descendant must add exactly two private materials, preserving shared mesh/textures and updating serialized material provenance. A separate active clone's renderer is never enabled. It is not a never-active GameObject test.

Each phase samples two consecutive EndOfFrame observations. The second requires `priorFrame+1` and checks phase against that frame's actual positive deltaTime at absolute `1e-5` tolerance. A frame gap, paused clock, missed update or duplicate phase update cannot count as a pass. Slot0 stays unchanged; enabled slot1 follows its own phase. Disabled clones may see their ancestor's writes through still-shared materials; those offsets do not demonstrate a write by the disabled clone. Each observation records identities, lineage parent indices, material/texture/mesh/bone relationships, native phase/rate, offsets, resource/reference counts and time.

One direct-call invalid-slot rejection check temporarily disables the owned scroller, invokes the exact prefix once and restores the field/component in finally. It requires fallback=true and no phase, material, serialized-provenance or resource-count changes. This is prefix eligibility rejection, not native scheduling or failed-commit rollback. No native original is invoked for the invalid configuration. Unknown-material and actual Unity failed-commit rollback tests are not included in this operation.

The two lineages destroy descendants first or source first while the grandclone remains alive. Real OnDestroy and production Plugin.Update pruning must release the shared lease. All nine explicitly owned assets must become Unity-null only after the last owner. The helper destroys owned roots independently in finally and never manually destroys transferred lease assets. Assets allocated before any registration attempt may be cleaned directly; uncertain registration is preserved as an unproven cleanup failure, never treated as safe to dispose manually.

Reports separate assertion evidence, native frames, rejection-only checks, errors and disposal. Successful mechanics do not establish art, animation, controller or gameplay acceptance. The pure linked timing tests validate observation arithmetic/guards only; actual Unity scheduling, clone serialization and resource disposal remain live gates.

```sh
dotnet run --project tools/ai-model-pipeline/material-lifecycle-tests/MaterialLifecycleTests.csproj -c Release
```
