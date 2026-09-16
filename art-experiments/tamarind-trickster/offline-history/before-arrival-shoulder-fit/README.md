# Tamarind Trickster

Original brown-and-cream monkey candidate for exact monkeyC renderer121301/enMonkeyBasey, CEL138586/enBaseyMonkey and controller5979. The editable Blender scene, original generator and palette contain no native surface geometry. Full42 palette and exact bind matrices are retained;35 native-positive bones are used,7 unused entries remain. Direct and saved/reopened exports, positive closed-piece volume, bind bounds and binding-only regeneration pass. Overlapping pieces are not a watertight union.

The root-reviewed hero and side, matching source/export bytes and initial review are preserved under offline-history/root-reviewed-bind-v1. Root accepted the initial palette, seated eyes and chest direction. The pronounced lower-jaw shelf and straight tapered tail need focused native mouth, shoulder and tail motion studies. No native monkey recordings or final appearance acceptance are available yet. root-bind-review.json preserves the later independent check; its pending-source wording is historical.

## Verified source constraints

source-findings.json pins the architect's exact native analysis. Native scale1 and +Z front agree with authored landmarks; tail extends -Z. The sole body material is1056 matLoot with emission1.4. A future original profile should explicitly disable native emission for the painted palette, then confirm live material readback. No runtime profile or catalog is frozen yet.

The prefab has11 rigidbodies,10 CharacterJoints and11 colliders; these remain native and do not establish collision fit for the original visual surface. PM_DeathDirect5032 invokes DeathFade around0.0796s and DeathFallOff around0.1719s. Indirect5195 invokes DeathFallOff around0.8708s and DeathFade around0.8806/0.9022s. Fade depends on runtime m_FadeMaterials. These source events do not guarantee actual fade, ragdoll activation, a visible corpse or120 enabled frames. Future captures must record those facts and distinguish analytical surfaces after hide from game-visible geometry.

The body prefab has no rigid MeshRenderer, but the row separately assigns monkeyBarrelCurse through native weapon holders. Retain that weapon, its attachment transforms, native effects and AI. Body-only findings do not describe every runtime weapon renderer or callback. No sibling monkey/controller inheritance is claimed.

Reproduce with scratch/model-venv/bin/python build_geometry.py, audit_surfaces.py and verify_original_geometry.py from this directory's scripts; run Blender --background --python build_blender.py for the editable/reopened model and studio views. Pose fitting, portraits, native effects and gameplay remain pending exact monkeyC observations.
