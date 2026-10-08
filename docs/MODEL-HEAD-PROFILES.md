# Wearer-specific head models

Unreleased framework candidate. This contract is not available in published framework 1.6.2. The initial Thief integration has bounded standing, item-card, inventory-clone and two-human removal/re-equip/hide-cycle evidence. Creation preview, combat, motion, persistence and co-op remain separate gates.

A registered Head item can declare `headProfiles` to select an original rigid model by an exact native skinset or a registered custom race. Its saved item ID, equipment statistics and acquisition rules do not change. Exactly one ordinary `itemModels` assignment is required as the fallback; every selected model must use that same renderer path. `displayModels` and `icon` still define the item card and slot image separately.

Each profile contains exactly one selector, either `nativeSkinset: "treasureHunter_Male"` or `customRace: {"modGuid": "com.example.race", "key": "race"}`, and one `model` assignment with `path`, `model`, optional `texture` and optional boolean `matte`. There may be 1 to 256 profiles. Duplicate selectors and undeclared native enum values are rejected. An absent optional race remains dormant and never matches its native donor skinset.

Omitting `faceOcclusion` preserves the wearer's face. A close-fitting face covering may supply an authored convex volume:

- `bodyPath`: exact skinned renderer path relative to the avatar.
- `planes`: 4 to 16 objects containing a three-component unit `normal` and finite `distance`.
- `upperHair`: `preserve` or `clipStrictHead`.
- `lowerHair`: `preserve`, `clipStrictHead` or `hideRenderer`.

The selected interior satisfies `dot(normal, point) <= distance` for every plane in the worn helmet MeshFilter's local coordinates. Coordinates and distances are bounded by 1000. Plane normals must be unit length within 0.1 percent. Volumes are authored from original equipment geometry and visual fit review, not extracted native character arrays.

The runtime subtracts the volume from triangles rigidly weighted to the exact `Head_M` bone. For the body only, a non-head triangle is omitted if every corner lies conservatively inside all planes; intersecting mixed-weight triangles remain intact. Hair clipping uses only strict head triangles. Hair policies target the exact native `hairTop` and `hairBottom` renderer paths. `hideRenderer` uses an owned copy with empty index buffers, preserving native visibility behavior and restoring the original mesh on removal. These are fixed rules, not optional topology modes. Source channel, bone, transform and allocation checks must pass before any face reference changes.

Native profiles require the exact mesh reference on the selected native skinset prefab. A different custom class body sharing a native selector does not acquire permission to be clipped. A custom race profile requires the matching registered race's exact renderer/model assignment and avatar-owned mesh lease. The framework creates per-avatar clipped copies and never modifies native prefabs or shared source meshes.

The initial helmet update waits until race and apparel assembly has completed. Later helmet updates restore the prior face before native gear changes, then prepare and apply the new profile. Failure restores original face references when safe; divergent references retain owned resources and reject further profile changes. Hiding or removing the helmet restores the face. Inventory and combat clones must retain the generated resources until their last reference is gone. Live face leases block hot activation.

A successful build or profile registration proves neither fit nor motion. Review front, sides, rear, jaw coverage, hair, native item cards, equip/unequip, hide/show, creation preview, inventory and combat clones, movement and attack poses, save/resume and co-op separately. A fit accepted on one class's skinset does not validate a different mesh merely because its race label matches.
