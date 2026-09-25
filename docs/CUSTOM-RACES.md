# Custom cosmetic races

The development race API adds a character appearance choice alongside the
native skin choices. It preserves class mechanics and does not modify native
class arrays, skinset rows, or avatar prefab assets. It requires framework 1.1.0 and a game restart after installation or removal.
Individual race packages must document their tested layouts and compatibility.

Register an identity with `Content.AddRace(modGuid, id, displayName)`, then
bind explicitly supported class layouts with
`Content.SetRaceClassBodyMeshesFromGlb(raceId, classRow, templateSkinset,
requiredBodyMeshes, conditionalApparel)`.
The class row may be native or registered custom content; the donor skinset
must belong to that class. The framework clones the donor row and keeps the
species renderer plan separate from ordinary class body registrations.

Required race body renderers with explicit textures preserve their authored palette
when native skin or hair colors change. Native clothing remains tintable.

The selected race supplies the body plan. Conditional race apparel and
equipped item apparel resolve into the existing single renderer transaction.
The ordinary `SetClassBodyMeshesFromGlb` contract still requires an exact
registered custom class row. A race does not weaken that boundary.

Data packages use `kind: "race"` with a display name and `raceBindings`:

```json
{
  "kind": "race",
  "id": "possum",
  "displayName": "Possum",
  "raceBindings": [{
    "class": "blacksmith",
    "skinset": "blacksmith_Cat",
    "body": [
      {"path": "playerCat", "model": "assets/possum-body.glb", "texture": "assets/possum-palette.png"},
      {"path": "hairTop", "model": "assets/possum-ruff.glb", "texture": "assets/possum-palette.png"}
    ]
  }]
}
```

Paths are package-relative assets; renderer paths are exact and relative to
the assembled avatar. Optional `apparel` entries use the existing
`path`, `nativeMesh`, `model`, and `texture` shape. Race entries do not accept
ordinary row `fields` overrides. Bindings are applied after class rows are
registered, so the package may reference its own new classes.

Race choices use framework-generated identities rather than native array
positions. Narrow lookup and selection hooks protect native indexing and
fall back to an available native skin when the selected custom binding is
missing. Save and network fields are native skin-type values; successful
compilation does not establish persistence or multiplayer support for new
values. Preserve stable package and race IDs across versions.

## Validation boundary

For each claimed class layout, inspect the exact assembled native avatar and
verify selection, class switching, default outfit, equipment rebuilds,
overworld and combat clones, relevant animations, culling, and resource
cleanup. Verify save/reload, unavailable race recovery and multiplayer
separately. Native class garments can include visible skin; replacing the
main body alone does not establish a completely restyled outfit.

The [Possum package](../marketplace/packages/possum/README.md) supplies original
assets and fourteen native class bindings. Hunter preview and ordinary bow motion
were observed on an earlier hand revision; the final smaller hands need fresh
live verification. Other layouts, save/reload and co-op remain unverified.
No independent tail animation or compatibility with every helmet is claimed.

Race packages are restart-only. Hot activation snapshots do not include race
registry or cloned skinset state. Keep a supporting framework installed when
loading saves containing custom race identities; downgrading to a pre-race
framework is not a supported recovery path.

See [player renderer contracts](MODEL-PLAYER-API.md) and
[model authoring](MODEL-AUTHORING.md) for binding and evidence requirements.
