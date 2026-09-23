# Custom models

FTK Mod Framework can replace the appearance of a custom enemy or player class while retaining native animation and game behavior. The shipped [Paladin package](../marketplace/packages/paladin/content.json) shows original equipment GLBs, icons, and class apparel assignments on native player models and skeletons. Use the [authoring workflow](MODEL-AUTHORING.md), [player renderer API](MODEL-PLAYER-API.md), and [renderer transaction contract](MODEL-RENDERER-API.md) for exact asset and binding rules.

| Route | API | Asset and rig |
| --- | --- | --- |
| Enemy runtime GLB | `Content.SetEnemyBodyMeshFromGlb` | Purpose-built GLB bound to the native skeleton; no Unity editor |
| Enemy AssetBundle mesh | `Content.SetEnemyBodyMesh` | Unity 2017.2.2p2 AssetBundle mesh on a compatible native skeleton |
| Enemy AssetBundle prefab | `Content.SetEnemyBodyFromBundle` | Full prefab with its own rig and required game components |
| Player class | `Content.SetClassBodyMeshesFromGlb` | Exact body, hair, and conditional apparel renderer assignments for a registered class/skinset |

The runtime GLB reader uses a constrained format, not arbitrary glTF. Vertex positions are already in Unity mesh-local coordinates; skin joints refer to live bones by exact name; bind matrices and material slots must match the selected native renderer. A model that loads but falls back, deforms, clips, or disappears has not passed visual validation. Inspect the exact spawned renderer path, equipped appearance, combat clone, motion, death handling, and resource cleanup.

Model assets live under `FTKModFramework_content/models/` for direct plugins. Marketplace packages place referenced assets in their archive, using package-relative paths as Paladin does. Local model files are not streamed to other players; every co-op client needs matching content and assets. That requirement alone does not establish co-op behavior.

A full prefab must include a `CharacterEventListener`, body renderer, animator, and weapon-holder bones expected by the game. AssetBundles must target the game's Unity 2017.2.2p2 build. The runtime GLB route is the editor-free path used by the shipped Paladin art; AssetBundle routes remain authoring APIs that need validation for each supplied asset and game build.
