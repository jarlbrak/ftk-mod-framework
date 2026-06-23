using System;
using System.Collections.Generic;
using System.Text;
using GridEditor;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// VISUAL-ONLY enemy identity: recolor + rescale (+ optional hunch / lantern / axe-hide) a registered enemy's
    /// spawned body, per combat, on its freshly-instantiated clone. The single registration point is
    /// <see cref="Content.SetEnemyVisual(FTK_enemyCombat, EnemyVisualPatch.EnemyVisual)"/>; this file is the engine
    /// plumbing behind it (the Core/Content boundary: public API in Content, mechanics here).
    ///
    /// WHY THIS IS DETERMINISM- AND SAVE-SAFE (verified RE, decompiled Assembly-CSharp, Unity 2017.2.2p2):
    ///   - The enemy body is <c>FTK_enemyCombat.m_EnemyAsset</c> (a <c>CharacterEventListener</c>), Instantiated
    ///     PER COMBAT by <c>EnemyDummy.InitEnemyDummyForCombat</c> (EnemyDummy.cs:43/49) into the public field
    ///     <c>CharacterDummy.m_EventListener</c> (CharacterDummy.cs:123). We edit THAT clone, never the shared
    ///     prefab, so the change is isolated per spawn and leaks to nothing (incl. vanilla).
    ///   - Enemy visuals NEVER network or persist: the 3D model / material / texture / body localScale / any added
    ///     prop GameObject never cross Photon and are never serialized. Only the enum-int identity (the registered
    ///     enemy id) and the DB field <c>m_MarkerScale</c> are shared state, and both are read identically from the
    ///     same mod DB on every machine. So a recolor/rescale/prop produces byte-for-byte identical game state in co-op.
    ///   - The game never re-tints an enemy's <c>_Color</c> (CEL.SetTintColor is player-only and would NPE on an
    ///     enemy; CEL.Update does no _Color/localScale reset), so the edits stick after we apply them. CAUTION: the
    ///     game DOES reset <c>_EmissionColor</c> to black on CEL-managed body materials (emissive hit-flash), so we
    ///     put emission ONLY on OUR added objects (the lantern), which the game never touches.
    ///
    /// PER-SPAWN RE-APPLICATION (NOT a registration-idempotency violation): the clone is brand-new every combat,
    /// so the postfix deliberately has NO <c>_done</c> guard for the apply itself; it must run on each spawn or
    /// the second fight would render the vanilla look. The registration (the dictionary entry) is the idempotent
    /// part; re-applying to each fresh clone is correct. The lantern is made idempotent PER CLONE by a name check
    /// (a re-apply on the same clone cannot stack two lanterns).
    /// </summary>
    [HarmonyPatch(typeof(EnemyDummy), "InitEnemyDummyForCombat", new Type[] { typeof(bool), typeof(bool), typeof(bool) })]
    internal static class EnemyVisualPatch
    {
        /// <summary>
        /// A registered visual override. Carries the body tint + non-uniform scale, the wet-bog material knobs, the
        /// spine-hunch angle, the axe-hide flag, and the procedural-lantern parameters. All fields are plain value
        /// types (net35-safe); none network or persist.
        /// </summary>
        internal struct EnemyVisual
        {
            // ---- recolor + scale ----
            public Color tint;        // body albedo tint (multiplies _Color)
            public float scale;       // overall body scale (the "tall" axis); 1 = unchanged
            public float widthBoost;  // extra x/z multiplier on top of scale (>1 = broader than tall)

            // ---- wet-bog skin (Standard shader) ----
            public bool  applyWetSkin;
            public float smoothness;  // _Glossiness (0..1); high = slimy/waterlogged
            public float metallic;    // _Metallic (0..1); low for skin

            // ---- spine hunch (best-effort, guarded) ----
            public float hunchDegrees; // forward rotation about the spine bone's local right axis; 0 disables

            // ---- axe hide ----
            public bool hideWeapon;    // disable the weapon renderers so the held item reads as the lantern

            // ---- procedural lantern (best-effort, guarded) ----
            // OPTIONAL visual knobs, set by a caller when addLantern is on. The bundled demo now ships the runtime-glb
            // body (which carries a baked lantern) and leaves these at defaults, so no in-assembly code assigns them;
            // silence "never assigned" exactly like the other data-carrier structs (ContentFile / ModManifest).
#pragma warning disable CS0649
            public bool  addLantern;
            public Color lanternColor;       // body albedo (warm amber)
            public Color lanternEmission;    // emission (warm orange, intensity-scaled by the caller)
            public Color lanternLightColor;  // the point Light's color (warm; a white Light is the green-white blowout)
            public float lanternSize;        // desired WORLD size of the lantern body cube (units)
            public float lanternLightRange;
            public float lanternLightIntensity;
#pragma warning restore CS0649

            // ---- swamp aura (best-effort, guarded) ----
            public bool swampAura;           // a procedural ParticleSystem of bog flies / marsh gas around the torso

            // ---- procedural golem body (best-effort, guarded) ----
            // When true, HIDE the chassis' skinned mesh and assemble a runtime low-poly mossy BOG-GOLEM from
            // bone-segment + joint-blob meshes parented to the existing skeleton bones, so the new body animates with
            // the skeleton. Visual-only / per-clone / deterministic (the meshes are generated from index hashes, no
            // Random), exactly like the rest of this struct: nothing networks or persists. OPTIONAL knobs (set when
            // proceduralBody is on); the bundled demo now ships the runtime-glb body and leaves these at defaults, so
            // no in-assembly code assigns them; silence "never assigned" like the other data-carrier structs.
#pragma warning disable CS0649
            public bool  proceduralBody;
            public float golemTorsoRadius;   // world radius of the spine/torso segments (thickest)
            public float golemLimbRadius;    // world radius of the arm/leg segments (medium; tapers to extremities)
            public float golemLumpiness;     // 0 = clean prisms; ~0.15 = chunky mossy lumps (index-derived, no Random)
            public Color golemEyeGlow;       // emissive eye color (sickly green / warm) on the two head eye blobs
#pragma warning restore CS0649

            // ---- AssetBundle mesh swap (best-effort, guarded) ----
            // When meshBundle is set, swap the chassis body SkinnedMeshRenderer's sharedMesh to a bundle-loaded Mesh,
            // REUSING the vanilla skeleton + bindposes + animations (so the custom mesh must be skinned to that same
            // rig). Optionally also swap its material's _MainTex to a bundle-loaded Texture2D. Visual-only / per-clone
            // / deterministic (the bundle ships inside the mod, byte-identical on every client), exactly like the rest
            // of this struct: nothing networks or persists. See Content.SetEnemyBodyMesh.
            public string meshBundle;        // bundle file name under FTKModFramework_content/models/ (null disables)
            public string meshName;          // Mesh asset name inside the bundle
            public string meshTextureName;   // optional Texture2D asset name to push into the body material's _MainTex

            // ---- runtime glTF (.glb) mesh swap (best-effort, guarded; EDITOR-FREE) ----
            // When glbMesh is set, swap the chassis body SkinnedMeshRenderer's sharedMesh to a Mesh built at runtime
            // from a shipped .glb, name-keyed to the VANILLA skeleton (the glb's per-vertex joints index bone NAMES,
            // remapped onto the live smr.bones[], reusing the live bindposes). Unlike meshBundle this needs NO Unity
            // editor and NO AssetBundle build: pure-managed glTF parsing (net35). Optionally also load a .png and push
            // it into the body material's _MainTex. Visual-only / per-clone / deterministic (the .glb+png ship inside
            // the mod, byte-identical on every client), exactly like the rest of this struct: nothing networks or
            // persists. See Content.SetEnemyBodyMeshFromGlb / RuntimeGltfMeshLoader.
            public string glbMesh;           // .glb file name under FTKModFramework_content/models/ (null disables)
            public string glbTexture;        // optional .png file name under the same folder for the body _MainTex
        }

        // Name of the procedural lantern parent, used for the per-clone idempotency check.
        private const string LanternName = "ftkmf_lantern";

        // Name of the procedural swamp-aura parent, used for the per-clone idempotency check.
        private const string SwarmName = "ftkmf_swarm";

        // Exact bone names (captured from the in-engine deep inventory), preferred over the heuristics below.
        private const string ExactHunchBone = "Chest_M";          // spine bone to hunch
        private const string ExactLanternMount = "WEAPON_HOLDER_L"; // off-hand holder to mount the lantern
        // Aura mount: the torso bone, then a named target chest, then the CEL root (handled inline as a fallback).
        private static readonly string[] AuraMountNames = { "Chest_M", "TARGET_CHEST" };

        // Weapon-renderer skip list (case-insensitive substring match on renderer name OR any material name): these
        // identify the held weapon so the per-part recolor does NOT tint it and so BossHideWeapon can disable it.
        // Tunable here (Core) because it is engine-classification, not aesthetics; the aesthetic knobs live in
        // RealmBossAdventure.cs. Verified in-engine for trollCaveA: weapon renderers 'trollCaveA_axe_ftkmf(Clone)'
        // (material 'matLoot') and 'Break' (material 'matWeapons01').
        private static readonly string[] WeaponMarkers =
        {
            "axe", "weapon", "_ftkmf", "(clone)", "break", "matloot", "matweapons"
        };

        // Spine-bone name fragments (lowercased), in preference order: a name containing an earlier fragment wins.
        private static readonly string[] SpineFragments = { "spine", "chest", "back", "torso", "neck" };

        // Hand/holder-bone name fragments (lowercased), in preference order: left/off-hand first, then any hand.
        private static readonly string[] HandFragments =
        {
            "weapon_holder_l", "holder_l", "hand_l", "lhand", "hand.l", "wrist_l",
            "weapon_holder", "hand", "wrist", "palm"
        };

        // Keyed by FTK_enemyCombat.m_ID (the registered string id, e.g. "ftkmf_mudwretch_foreman"). We gate on
        // m_ID, NOT EnemyDummy.m_EnemyType, to avoid the decimal-string-id ambiguity of the latter.
        private static readonly Dictionary<string, EnemyVisual> _visuals = new Dictionary<string, EnemyVisual>();

        /// <summary>Register (or replace) the full visual override for an enemy id. Internal: callers go through
        /// <see cref="Content.SetEnemyVisual(FTK_enemyCombat, EnemyVisual)"/>.</summary>
        internal static void Register(string enemyId, EnemyVisual visual)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            _visuals[enemyId] = visual;
        }

        /// <summary>Back-compat overload: register a tint + uniform scale with default knobs (no wet skin / hunch /
        /// lantern / axe-hide). Kept so older callers compile unchanged.</summary>
        internal static void Register(string enemyId, Color tint, float scale)
        {
            EnemyVisual v = default(EnemyVisual);
            v.tint = tint;
            v.scale = scale;
            v.widthBoost = 1f;
            Register(enemyId, v);
        }

        /// <summary>
        /// Register (or MERGE) just the AssetBundle mesh-swap fields onto an enemy id, leaving any other visual knobs
        /// (tint / scale / lantern / golem) already registered for that id untouched. If no entry exists yet, a
        /// neutral one is created (scale 1, identity tint, widthBoost 1) so the swap alone is harmless. Internal:
        /// callers go through <see cref="Content.SetEnemyBodyMesh"/>.
        /// </summary>
        internal static void RegisterMeshSwap(string enemyId, string meshBundle, string meshName, string meshTextureName)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            EnemyVisual v;
            if (!_visuals.TryGetValue(enemyId, out v))
            {
                v = default(EnemyVisual);
                v.tint = Color.white; // neutral: SetEnemyBodyMesh on its own must not recolor the body
                v.scale = 1f;
                v.widthBoost = 1f;
            }
            v.meshBundle = meshBundle;
            v.meshName = meshName;
            v.meshTextureName = meshTextureName;
            _visuals[enemyId] = v;
        }

        /// <summary>
        /// Register (or MERGE) just the runtime .glb mesh-swap fields onto an enemy id, leaving any other visual knobs
        /// already registered for that id untouched. If no entry exists yet, a neutral one is created (scale 1,
        /// identity tint, widthBoost 1) so the swap alone is harmless. Editor-free path: name-keyed to the vanilla
        /// skeleton, reusing the live bindposes. Internal: callers go through
        /// <see cref="Content.SetEnemyBodyMeshFromGlb"/>.
        /// </summary>
        internal static void RegisterGlbMeshSwap(string enemyId, string glbMesh, string glbTexture)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            EnemyVisual v;
            if (!_visuals.TryGetValue(enemyId, out v))
            {
                v = default(EnemyVisual);
                v.tint = Color.white; // neutral: a glb swap on its own must not recolor the body
                v.scale = 1f;
                v.widthBoost = 1f;
            }
            v.glbMesh = glbMesh;
            v.glbTexture = glbTexture;
            _visuals[enemyId] = v;
        }

        /// <summary>True if an enemy id has a registered visual override (used by self-tests).</summary>
        internal static bool TryGet(string enemyId, out EnemyVisual visual)
        {
            if (!string.IsNullOrEmpty(enemyId) && _visuals.TryGetValue(enemyId, out visual)) return true;
            visual = default(EnemyVisual);
            return false;
        }

        private static void Postfix(EnemyDummy __instance)
        {
            try
            {
                if (__instance == null) return;

                FTK_enemyCombat ec = __instance.m_EnemyCombat;
                if (ec == null || ec.m_ID == null) return;

                EnemyVisual v;
                if (!_visuals.TryGetValue(ec.m_ID, out v)) return;

                CharacterEventListener cel = __instance.m_EventListener;
                if (cel == null) return;

                // NON-UNIFORM HULKING SCALE: broader than tall (a hunched bruiser). No _done guard: the clone is
                // fresh every combat and must be re-scaled each spawn (per-spawn re-application is correct here).
                float xz = v.scale * (v.widthBoost > 0f ? v.widthBoost : 1f);
                cel.transform.localScale = new Vector3(xz, v.scale, xz);

                // PER-PART RECOLOR + WET-BOG SKIN on the BODY ONLY (the weapon renderers are skipped). Use
                // GetComponentsInChildren (recursive) because the body's SkinnedMeshRenderer is usually nested deeper
                // than CEL.m_Renderers (direct-children-only) sees. Operate on .materials (the instanced per-clone
                // copies), so vanilla shared materials are untouched.
                Renderer[] renderers = cel.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    if (r == null) continue;

                    bool isWeapon = IsWeaponRenderer(r);
                    if (isWeapon)
                    {
                        // HIDE THE AXE so the held item reads as the lantern.
                        if (v.hideWeapon) r.enabled = false;
                        continue; // never tint / re-skin the weapon
                    }

                    Material[] mats = r.materials;
                    foreach (Material m in mats)
                    {
                        if (m == null) continue;

                        if (m.HasProperty("_Color")) m.SetColor("_Color", v.tint);
                        else m.color = v.tint;

                        // WET BOG SKIN: high smoothness + low metallic so the body reads waterlogged/slimy. Guarded
                        // by HasProperty so a non-Standard material is left alone. (Emission stays OFF the body: the
                        // game resets _EmissionColor to black on CEL-managed materials.)
                        if (v.applyWetSkin)
                        {
                            if (m.HasProperty("_Glossiness")) m.SetFloat("_Glossiness", v.smoothness);
                            if (m.HasProperty("_Metallic"))   m.SetFloat("_Metallic", v.metallic);
                        }
                    }
                }

                // MESH SWAP (best-effort, guarded): repoint the chassis body SkinnedMeshRenderer's sharedMesh to a
                // custom Mesh, reusing the vanilla skeleton + bindposes + animations. Two sources: a runtime .glb
                // (glbMesh, EDITOR-FREE; tried FIRST inside ApplyMeshSwap) or an artist-authored AssetBundle
                // (meshBundle/meshName). Applied to THIS fresh clone only; on any failure the original mesh (or the
                // procedural golem, if also requested) is left intact. Per-clone re-application is correct (the clone
                // is brand-new every combat), so there is no _done guard.
                if (!string.IsNullOrEmpty(v.glbMesh) ||
                    (!string.IsNullOrEmpty(v.meshBundle) && !string.IsNullOrEmpty(v.meshName)))
                {
                    try { ApplyMeshSwap(ec.m_ID, cel, v); }
                    catch (Exception me) { Plugin.Log.LogWarning("[enemy-visual] mesh swap failed: " + me.Message); }
                }

                // PROCEDURAL GOLEM BODY (best-effort, guarded): hide the chassis skinned mesh and assemble a runtime
                // low-poly mossy bog-golem from bone-segment + blob meshes parented to the skeleton, so the new body
                // animates with the existing bones. Built BEFORE the hunch so the torso/arm segments (children of
                // Chest_M) inherit the hunch rotation; the lantern (on WEAPON_HOLDER_L) and aura (on Chest_M) still
                // read on top of the golem because they attach to the same bones.
                if (v.proceduralBody)
                {
                    try { BuildProceduralBody(cel, v); }
                    catch (Exception ge) { Plugin.Log.LogWarning("[enemy-visual] golem body failed: " + ge.Message); }
                }

                // SPINE HUNCH (best-effort, guarded): rotate the first matching spine bone forward.
                if (v.hunchDegrees != 0f)
                {
                    try { ApplyHunch(ec.m_ID, cel, v.hunchDegrees); }
                    catch (Exception he) { Plugin.Log.LogWarning("[enemy-visual] hunch failed: " + he.Message); }
                }

                // PROCEDURAL LANTERN (best-effort, guarded): build a small glowing lantern + warm point light and
                // attach it to a hand/holder bone (idempotent per clone).
                if (v.addLantern)
                {
                    try { AttachLantern(ec.m_ID, cel, v); }
                    catch (Exception le) { Plugin.Log.LogWarning("[enemy-visual] lantern failed: " + le.Message); }
                }

                // PROCEDURAL SWAMP AURA (best-effort, guarded): a small ParticleSystem of bog flies / marsh gas
                // drifting around the torso (idempotent per clone).
                if (v.swampAura)
                {
                    try { AttachSwampAura(ec.m_ID, cel); }
                    catch (Exception se) { Plugin.Log.LogWarning("[enemy-visual] swamp aura failed: " + se.Message); }
                }
            }
            catch (Exception e)
            {
                // Never throw into the spawn builder: a visual tweak must never break combat. Log and move on.
                Plugin.Log.LogWarning("[enemy-visual] apply failed: " + e.Message);
            }
        }

        // ---- weapon classification --------------------------------------------------------------------------

        /// <summary>True if a renderer is the held weapon (skip it for tint/skin; disable it for axe-hide). Matches
        /// the renderer name OR any of its shared-material names against <see cref="WeaponMarkers"/>, case-insensitive.</summary>
        private static bool IsWeaponRenderer(Renderer r)
        {
            if (r == null) return false;

            string rn = r.name != null ? r.name.ToLowerInvariant() : null;
            if (MatchesAnyMarker(rn)) return true;

            // sharedMaterials avoids instantiating per-clone copies just to read names.
            Material[] sm = r.sharedMaterials;
            if (sm != null)
            {
                foreach (Material m in sm)
                {
                    if (m == null || m.name == null) continue;
                    if (MatchesAnyMarker(m.name.ToLowerInvariant())) return true;
                }
            }
            return false;
        }

        private static bool MatchesAnyMarker(string loweredName)
        {
            if (loweredName == null) return false;
            for (int i = 0; i < WeaponMarkers.Length; i++)
            {
                if (loweredName.IndexOf(WeaponMarkers[i], StringComparison.Ordinal) >= 0) return true;
            }
            return false;
        }

        // ---- spine hunch ------------------------------------------------------------------------------------

        /// <summary>Rotate the spine bone forward by <paramref name="degrees"/> about its local right axis. PREFERS
        /// the exact bone named <see cref="ExactHunchBone"/> ("Chest_M"); if it is absent, falls back to the best
        /// <see cref="SpineFragments"/> match. Logs which bone (and which path) was used, or that none matched.</summary>
        private static void ApplyHunch(string enemyId, CharacterEventListener cel, float degrees)
        {
            // EXACT NAME FIRST: search the whole hierarchy for a transform named exactly "Chest_M".
            Transform best = FindExact(cel, ExactHunchBone);
            string via = best != null ? "exact name '" + ExactHunchBone + "'" : null;

            // FALLBACK: the SpineFragments heuristic over every SkinnedMeshRenderer's bones.
            if (best == null)
            {
                SkinnedMeshRenderer[] smrs = cel.GetComponentsInChildren<SkinnedMeshRenderer>(true);
                int bestRank = int.MaxValue;
                foreach (SkinnedMeshRenderer smr in smrs)
                {
                    if (smr == null || smr.bones == null) continue;
                    foreach (Transform b in smr.bones)
                    {
                        if (b == null || b.name == null) continue;
                        string bn = b.name.ToLowerInvariant();
                        for (int i = 0; i < SpineFragments.Length; i++)
                        {
                            if (bn.IndexOf(SpineFragments[i], StringComparison.Ordinal) >= 0)
                            {
                                if (i < bestRank) { bestRank = i; best = b; }
                                break;
                            }
                        }
                    }
                }
                if (best != null) via = "heuristic (SpineFragments)";
            }

            if (best == null)
            {
                Plugin.Log.LogInfo("[enemy-visual] hunch: no spine-like bone found for '" + enemyId + "'; skipped.");
                return;
            }

            best.Rotate(Vector3.right, degrees, Space.Self);
            Plugin.Log.LogInfo("[enemy-visual] hunch: rotated bone '" + best.name + "' (" + via + ") forward " +
                degrees + " deg for '" + enemyId + "'.");
        }

        /// <summary>Find a transform named EXACTLY <paramref name="exactName"/> (ordinal, case-sensitive) anywhere
        /// in the CEL hierarchy, or null. Used for the exact-bone-name preference paths.</summary>
        private static Transform FindExact(CharacterEventListener cel, string exactName)
        {
            Transform[] all = cel.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                if (t == null || t.name == null) continue;
                if (string.Equals(t.name, exactName, StringComparison.Ordinal)) return t;
            }
            return null;
        }

        // ---- procedural lantern -----------------------------------------------------------------------------

        /// <summary>Build a small emissive lantern from primitives + a warm point light and parent it to a hand/holder
        /// bone (idempotent per clone). Falls back to the body root if no hand bone is found.</summary>
        private static void AttachLantern(string enemyId, CharacterEventListener cel, EnemyVisual v)
        {
            // 1) Find the hand/holder bone. PREFER the exact name "WEAPON_HOLDER_L"; if absent, fall back to the
            //    HandFragments heuristic over the FULL hierarchy (left/off-hand preferred).
            Transform hand = FindExact(cel, ExactLanternMount);
            string via = hand != null ? "exact name '" + ExactLanternMount + "'" : null;

            if (hand == null)
            {
                Transform[] all = cel.GetComponentsInChildren<Transform>(true);
                int bestRank = int.MaxValue;
                foreach (Transform t in all)
                {
                    if (t == null || t.name == null) continue;
                    string tn = t.name.ToLowerInvariant();
                    for (int i = 0; i < HandFragments.Length; i++)
                    {
                        if (tn.IndexOf(HandFragments[i], StringComparison.Ordinal) >= 0)
                        {
                            if (i < bestRank) { bestRank = i; hand = t; }
                            break;
                        }
                    }
                }
                if (hand != null) via = "heuristic (HandFragments)";
            }

            Transform mount = hand;
            if (mount == null)
            {
                mount = cel.transform; // fallback: parent to the body root at a small offset
                Plugin.Log.LogInfo("[enemy-visual] lantern: no hand/holder bone for '" + enemyId +
                    "'; falling back to body root.");
            }
            else
            {
                Plugin.Log.LogInfo("[enemy-visual] lantern: mounting on bone '" + mount.name + "' (" + via +
                    ", path: " + FullPath(mount) + ") for '" + enemyId + "'.");
            }

            // IDEMPOTENCY PER CLONE: if this mount already has our lantern, do nothing (a re-apply can't stack two).
            if (mount.Find(LanternName) != null) return;

            // 2) Build the lantern parent + body cube (+ thin handle cube).
            GameObject lantern = new GameObject(LanternName);
            lantern.transform.SetParent(mount, false);

            // World size compensation: localScale is multiplied by the bone's lossyScale (which includes our up-scale),
            // so divide the desired world size by the bone's lossyScale to land at ~lanternSize in world units.
            Vector3 ls = mount.lossyScale;
            float sx = v.lanternSize / (Mathf.Approximately(ls.x, 0f) ? 1f : Mathf.Abs(ls.x));
            float sy = v.lanternSize / (Mathf.Approximately(ls.y, 0f) ? 1f : Mathf.Abs(ls.y));
            float sz = v.lanternSize / (Mathf.Approximately(ls.z, 0f) ? 1f : Mathf.Abs(ls.z));

            Material lanternMat = BuildLanternMaterial(v);

            GameObject body = GameObject.CreatePrimitive(PrimitiveType.Cube);
            body.name = "ftkmf_lantern_body";
            StripColliders(body);
            body.transform.SetParent(lantern.transform, false);
            body.transform.localPosition = Vector3.zero;
            body.transform.localScale = new Vector3(sx, sy, sz);
            ApplyMaterial(body, lanternMat);

            // Thin handle on top (a flat, narrow cube), purely cosmetic.
            GameObject handle = GameObject.CreatePrimitive(PrimitiveType.Cube);
            handle.name = "ftkmf_lantern_handle";
            StripColliders(handle);
            handle.transform.SetParent(lantern.transform, false);
            handle.transform.localPosition = new Vector3(0f, sy * 0.7f, 0f);
            handle.transform.localScale = new Vector3(sx * 0.2f, sy * 0.5f, sz * 0.2f);
            ApplyMaterial(handle, lanternMat);

            // 3) Warm point light so it casts real light on the boss + crypt.
            GameObject lightGo = new GameObject("ftkmf_lantern_light");
            lightGo.transform.SetParent(lantern.transform, false);
            lightGo.transform.localPosition = Vector3.zero;
            Light light = lightGo.AddComponent<Light>();
            light.type = LightType.Point;
            // EXPLICIT warm color: iteration 1 left this white, which was the main cause of the green-white blowout.
            light.color = v.lanternLightColor;
            light.range = v.lanternLightRange;
            light.intensity = v.lanternLightIntensity;

            // 4) Position the lantern a small offset into the palm (local space of the mount bone).
            lantern.transform.localPosition = new Vector3(0f, 0f, v.lanternSize * 0.5f / (Mathf.Approximately(ls.z, 0f) ? 1f : Mathf.Abs(ls.z)));
        }

        /// <summary>A new Standard-shader material with warm amber albedo + emission enabled (warm orange) + a low
        /// glossiness (so it reads as a dull lantern body, not a wet sphere). Emission is safe here: it lives on OUR
        /// object, which the game's hit-flash reset never touches.</summary>
        private static Material BuildLanternMaterial(EnemyVisual v)
        {
            Material mat = new Material(Shader.Find("Standard"));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", v.lanternColor);
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", v.lanternEmission);
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", 0.2f); // low: dull lantern body
            return mat;
        }

        private static void ApplyMaterial(GameObject go, Material mat)
        {
            Renderer r = go.GetComponent<Renderer>();
            if (r != null) r.sharedMaterial = mat;
        }

        /// <summary>Destroy every Collider on a primitive so the lantern never affects combat/hit geometry.</summary>
        private static void StripColliders(GameObject go)
        {
            Collider[] cols = go.GetComponentsInChildren<Collider>(true);
            foreach (Collider c in cols)
            {
                if (c != null) UnityEngine.Object.Destroy(c);
            }
        }

        // ---- procedural swamp aura --------------------------------------------------------------------------

        /// <summary>Build a small ParticleSystem of slow-drifting bog flies / marsh gas and parent it to the torso
        /// (idempotent per clone). Mount preference: the exact bones in <see cref="AuraMountNames"/> ("Chest_M" then
        /// "TARGET_CHEST"), then the CEL root. If no usable particle shader resolves, logs and SKIPS (never throws).</summary>
        private static void AttachSwampAura(string enemyId, CharacterEventListener cel)
        {
            // 1) Pick the torso mount: exact bone names first, then the body root.
            Transform mount = null;
            string via = null;
            for (int i = 0; i < AuraMountNames.Length && mount == null; i++)
            {
                mount = FindExact(cel, AuraMountNames[i]);
                if (mount != null) via = "exact name '" + AuraMountNames[i] + "'";
            }
            if (mount == null)
            {
                mount = cel.transform;
                via = "CEL root (no torso bone)";
            }

            // IDEMPOTENCY PER CLONE: a re-apply on the same clone must not stack a second swarm.
            if (mount.Find(SwarmName) != null) return;

            // 2) Resolve a usable particle shader (ordered fallback). If none resolve, SKIP the aura cleanly.
            Material auraMat = null;
            string[] shaderNames =
            {
                "Legacy Shaders/Particles/Alpha Blended Premultiply",
                "Particles/Standard Unlit",
                "Sprites/Default"
            };
            string usedShader = null;
            foreach (string name in shaderNames)
            {
                Shader sh = Shader.Find(name);
                if (sh != null) { auraMat = new Material(sh); usedShader = name; break; }
            }
            if (auraMat == null)
            {
                Plugin.Log.LogInfo("[enemy-visual] swamp aura: no particle shader resolved for '" + enemyId +
                    "'; aura skipped.");
                return;
            }

            // 3) Build the swarm GameObject + ParticleSystem, parented at the torso.
            GameObject go = new GameObject(SwarmName);
            go.transform.SetParent(mount, false);
            go.transform.localPosition = Vector3.zero;

            ParticleSystem ps = go.AddComponent<ParticleSystem>();

            // Module structs are GET-ONLY views that write through to the system: capture-then-set, never assign back.
            ParticleSystem.MainModule m = ps.main;
            m.startColor = new Color(0.6f, 0.7f, 0.25f, 0.5f);   // sickly bog green, half-alpha (implicit -> MinMaxGradient)
            m.startSize = 0.03f;                                  // implicit float -> MinMaxCurve
            m.startLifetime = 2.5f;
            m.startSpeed = 0.15f;
            m.gravityModifier = -0.02f;                           // slight upward drift (marsh gas)
            m.maxParticles = 25;
            m.simulationSpace = ParticleSystemSimulationSpace.Local;

            ParticleSystem.EmissionModule em = ps.emission;
            em.enabled = true;
            em.rateOverTime = 10f;                                // rateOverTime, NOT the obsolete rate

            ParticleSystem.ShapeModule shape = ps.shape;
            shape.enabled = true;
            shape.shapeType = ParticleSystemShapeType.Sphere;
            shape.radius = 0.5f;                                  // a small cloud around the torso

            // Assign the material via the renderer (added if the AddComponent<ParticleSystem> did not create one).
            ParticleSystemRenderer psr = go.GetComponent<ParticleSystemRenderer>();
            if (psr == null) psr = go.AddComponent<ParticleSystemRenderer>();
            psr.material = auraMat;

            Plugin.Log.LogInfo("[enemy-visual] swamp aura: mounted on " + via + " (shader '" + usedShader +
                "') for '" + enemyId + "'.");
        }

        // ---- procedural golem body --------------------------------------------------------------------------

        // Name of the procedural golem parent, used for the per-clone idempotency check.
        private const string GolemName = "ftkmf_golem";
        private const string GolemMeshChild = "enTroll01"; // the chassis SkinnedMeshRenderer child (exact name, in-engine)

        // EXPERIMENT (#72): name of the static-attach glb body child (under Root_M), for the per-clone idempotency check.
        private const string GlbStaticBodyName = "ftkmf_glb_static_body";

        // EXPERIMENT (#72/#74): Mesh instanceIDs whose normals we have already recalculated in the static-attach path,
        // so RecalculateNormals runs ONCE per Mesh instance (it is not free, and the static path runs per clone).
        private static readonly HashSet<int> _normalsRecalculated = new HashSet<int>();

        // EXPERIMENT (#72/#74): modest emission for the static boss body on the Lit Standard material, so it reads in
        // the dim crypt WITHOUT washing out the scene-light shading (a postfix MeshRenderer gets no light probes, but the
        // diorama's direct lights still hit it). Texture-modulated via _EmissionMap; subtle grey lifts it out of pure
        // black while letting the diorama lights shape the form. No FTK_BOSS_EMIT env lever exists, so this is a const.
        private static readonly Color StaticBossEmission = new Color(0.35f, 0.35f, 0.35f);

        // The skeleton chains, as ordered exact bone names (parent -> child). We build a tapered segment for each
        // adjacent pair (skipping zero-length Scapula links by starting each arm at the Shoulder and each leg at the
        // Hip), parenting it to the PROXIMAL bone and pointing it at the DISTAL bone, so each segment animates with
        // the bone it hangs from. Verified in-engine on enTrollCave(Clone).
        private static readonly string[] SpineChain = { "Root_M", "BackA_M", "BackB_M", "Chest_M", "Neck_M", "Head_M" };
        private static readonly string[] ArmLChain  = { "Shoulder_L", "Elbow_L", "Wrist_L" };
        private static readonly string[] ArmRChain  = { "Shoulder_R", "Elbow_R", "Wrist_R" };
        private static readonly string[] LegLChain  = { "Hip_L", "Knee_L", "Ankle_L" };
        private static readonly string[] LegRChain  = { "Hip_R", "Knee_R", "Ankle_R" };

        // Interior bones that get a covering joint blob (sized to the adjacent segment radius).
        private static readonly string[] JointBones =
        {
            "Chest_M", "Neck_M",
            "Shoulder_L", "Elbow_L", "Wrist_L",
            "Shoulder_R", "Elbow_R", "Wrist_R",
            "Hip_L", "Knee_L", "Ankle_L",
            "Hip_R", "Knee_R", "Ankle_R",
        };

        /// <summary>
        /// Hide the chassis skinned mesh and assemble a runtime low-poly mossy bog-golem from per-bone segment meshes
        /// (one tapered prism per adjacent bone pair in each chain) plus joint blobs and a head + two glowing eyes,
        /// all parented to the existing skeleton so the new body animates. Idempotent per clone (name check). Fully
        /// guarded by the caller; logs a one-line summary. Visual-only / deterministic (the meshes are generated from
        /// index hashes, no Random), so it stays co-op- and save-safe like the rest of this file.
        /// </summary>
        private static void BuildProceduralBody(CharacterEventListener cel, EnemyVisual v)
        {
            // IDEMPOTENCY PER CLONE: a re-apply on the same fresh clone must not stack a second golem.
            Transform existing = FindExact(cel, GolemName);
            if (existing != null) return;

            // 1) HIDE the chassis mesh ('enTroll01'): disable its SkinnedMeshRenderer (and any Renderer on the GO).
            bool trollHidden = false;
            Transform meshChild = FindExact(cel, GolemMeshChild);
            if (meshChild != null)
            {
                SkinnedMeshRenderer smr = meshChild.GetComponent<SkinnedMeshRenderer>();
                if (smr != null) { smr.enabled = false; trollHidden = true; }
                // Belt-and-suspenders: disable any other Renderer on the same GameObject too.
                Renderer anyR = meshChild.GetComponent<Renderer>();
                if (anyR != null) anyR.enabled = false;
            }
            Plugin.Log.LogInfo("[enemy-visual] golem: chassis mesh '" + GolemMeshChild + "' " +
                (trollHidden ? "hidden (SkinnedMeshRenderer disabled)." : "NOT found; chassis may peek through."));

            // 2) The shared mossy-green wet material for every golem part (flat-shaded meshes give the faceted look).
            Material golemMat = BuildGolemMaterial(v);

            // 3) The golem parent under the CEL root (so the non-uniform body localScale on the root scales it too).
            GameObject golem = new GameObject(GolemName);
            golem.transform.SetParent(cel.transform, false);
            golem.transform.localPosition = Vector3.zero;
            golem.transform.localRotation = Quaternion.identity;
            golem.transform.localScale = Vector3.one; // do NOT add extra scale: the cel-root scale already covers it

            // Cache every bone by exact name once (one hierarchy walk).
            Dictionary<string, Transform> bones = new Dictionary<string, Transform>();
            Transform[] all = cel.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in all)
            {
                if (t == null || t.name == null) continue;
                if (!bones.ContainsKey(t.name)) bones[t.name] = t;
            }

            int segs = 0;
            // SPINE: thick torso, slight taper up to the neck.
            segs += BuildChainSegments(SpineChain, bones, golemMat, v.golemTorsoRadius, v.golemTorsoRadius * 0.7f, 6, v.golemLumpiness);
            // ARMS: medium, taper toward the wrist.
            segs += BuildChainSegments(ArmLChain, bones, golemMat, v.golemLimbRadius, v.golemLimbRadius * 0.55f, 5, v.golemLumpiness);
            segs += BuildChainSegments(ArmRChain, bones, golemMat, v.golemLimbRadius, v.golemLimbRadius * 0.55f, 5, v.golemLumpiness);
            // LEGS: a touch thicker than arms, taper toward the ankle.
            segs += BuildChainSegments(LegLChain, bones, golemMat, v.golemLimbRadius * 1.15f, v.golemLimbRadius * 0.7f, 5, v.golemLumpiness);
            segs += BuildChainSegments(LegRChain, bones, golemMat, v.golemLimbRadius * 1.15f, v.golemLimbRadius * 0.7f, 5, v.golemLumpiness);

            // 4) JOINT BLOBS: cover the gaps at interior bones, sized to the limb/torso radius.
            int joints = 0;
            for (int i = 0; i < JointBones.Length; i++)
            {
                Transform jb;
                if (!bones.TryGetValue(JointBones[i], out jb) || jb == null) continue;
                bool isTorso = JointBones[i] == "Chest_M" || JointBones[i] == "Neck_M";
                float jr = isTorso ? v.golemTorsoRadius * 0.85f : v.golemLimbRadius * 0.7f;
                BuildBlobAt(jb, golemMat, jr, v.golemLumpiness);
                joints++;
            }

            // 5) HEAD: a larger blob on Head_M + two emissive eyes offset forward and apart.
            int eyes = 0;
            Transform head;
            if (bones.TryGetValue("Head_M", out head) && head != null)
            {
                float headR = v.golemTorsoRadius * 1.0f;
                BuildBlobAt(head, golemMat, headR, v.golemLumpiness);

                Material eyeMat = BuildEyeMaterial(v);
                // Eyes: small blobs, forward (+Z local of the head) and split left/right (+/-X). World-size compensated.
                float eyeWorld = headR * 0.28f;
                float fwd = headR * 0.85f;
                float side = headR * 0.40f;
                float up = headR * 0.15f;
                eyes += BuildEyeAt(head, eyeMat, eyeWorld, new Vector3(-side, up, fwd), v.golemLumpiness);
                eyes += BuildEyeAt(head, eyeMat, eyeWorld, new Vector3( side, up, fwd), v.golemLumpiness);
            }

            Plugin.Log.LogInfo("[enemy-visual] golem: built " + segs + " bone segments + " + joints +
                " joint blobs + " + eyes + " eyes; chassis " + (trollHidden ? "hidden" : "NOT hidden") + ".");
        }

        /// <summary>
        /// For each adjacent (parent,child) pair in <paramref name="chain"/>, build a tapered segment parented to the
        /// PARENT bone, oriented so its local +Y points at the CHILD bone, length ~ rest-distance * 1.08 (slight
        /// overlap to cover joints), with world-thickness held constant despite the bone's lossyScale. Radius lerps
        /// from <paramref name="radius0"/> (proximal) to <paramref name="radius1"/> (distal) across the chain. Returns
        /// the number of segments actually built (a missing/zero-length pair is skipped).
        /// </summary>
        private static int BuildChainSegments(string[] chain, Dictionary<string, Transform> bones, Material mat,
            float radius0, float radius1, int sides, float lumpiness)
        {
            int built = 0;
            int pairs = chain.Length - 1;
            if (pairs <= 0) return 0;

            for (int i = 0; i < pairs; i++)
            {
                Transform parent, child;
                if (!bones.TryGetValue(chain[i], out parent) || parent == null) continue;
                if (!bones.TryGetValue(chain[i + 1], out child) || child == null) continue;

                float d = Vector3.Distance(parent.position, child.position);
                if (d <= 1e-4f) continue; // zero-length link (e.g. a degenerate Scapula): skip

                // Per-segment radii lerp across the chain so limbs taper toward the extremity.
                float t0 = (float)i / pairs;
                float t1 = (float)(i + 1) / pairs;
                float rA = Mathf.Lerp(radius0, radius1, t0);
                float rB = Mathf.Lerp(radius0, radius1, t1);

                // The segment mesh is authored in WORLD-units (length d*1.08, world radii rA/rB), then we counter the
                // parent bone's lossyScale via localScale so it lands at that world size on the up-scaled skeleton.
                float lenWorld = d * 1.08f;

                Vector3 ls = parent.lossyScale;
                float ax = Mathf.Approximately(ls.x, 0f) ? 1f : Mathf.Abs(ls.x);
                float ay = Mathf.Approximately(ls.y, 0f) ? 1f : Mathf.Abs(ls.y);
                float az = Mathf.Approximately(ls.z, 0f) ? 1f : Mathf.Abs(ls.z);

                // Author the mesh in the bone's LOCAL units (divide world by lossyScale) so localScale can stay 1 and
                // the orientation math (which is in local space) is unaffected by non-uniform scale. Length runs along
                // local +Y (use ay), radius is in the x/z plane (use an average of ax/az for a round cross-section).
                float lenLocal = lenWorld / ay;
                float rxz = (ax + az) * 0.5f;
                float rALocal = rA / rxz;
                float rBLocal = rB / rxz;

                Mesh mesh = ProceduralCreature.BuildSegment(lenLocal, rALocal, rBLocal, sides, lumpiness);

                GameObject go = new GameObject("ftkmf_golem_seg_" + chain[i] + "_" + chain[i + 1]);
                go.transform.SetParent(parent, false);
                go.transform.localPosition = Vector3.zero;
                // Point local +Y at the child (in the parent's local space), so the segment spans parent->child.
                Vector3 dirLocal = parent.InverseTransformPoint(child.position);
                if (dirLocal.sqrMagnitude > 1e-8f)
                    go.transform.localRotation = Quaternion.FromToRotation(Vector3.up, dirLocal.normalized);
                else
                    go.transform.localRotation = Quaternion.identity;
                go.transform.localScale = Vector3.one;

                MeshFilter mf = go.AddComponent<MeshFilter>();
                mf.sharedMesh = mesh;
                MeshRenderer mr = go.AddComponent<MeshRenderer>();
                mr.sharedMaterial = mat;

                built++;
            }
            return built;
        }

        /// <summary>Build a lumpy blob of world-radius <paramref name="worldRadius"/> parented at a bone, world-size
        /// compensated for the bone's lossyScale, oriented with the bone.</summary>
        private static void BuildBlobAt(Transform bone, Material mat, float worldRadius, float lumpiness)
        {
            Vector3 ls = bone.lossyScale;
            float ax = Mathf.Approximately(ls.x, 0f) ? 1f : Mathf.Abs(ls.x);
            float az = Mathf.Approximately(ls.z, 0f) ? 1f : Mathf.Abs(ls.z);
            float rxz = (ax + az) * 0.5f;
            float rLocal = worldRadius / rxz;

            Mesh mesh = ProceduralCreature.BuildBlob(rLocal, 1, lumpiness);

            GameObject go = new GameObject("ftkmf_golem_joint_" + bone.name);
            go.transform.SetParent(bone, false);
            go.transform.localPosition = Vector3.zero;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = mat;
        }

        /// <summary>Build one emissive eye blob parented at the head, at a local offset (world-size compensated).
        /// Returns 1 (so the caller can sum eye counts).</summary>
        private static int BuildEyeAt(Transform head, Material eyeMat, float worldRadius, Vector3 localOffsetWorld,
            float lumpiness)
        {
            Vector3 ls = head.lossyScale;
            float ax = Mathf.Approximately(ls.x, 0f) ? 1f : Mathf.Abs(ls.x);
            float ay = Mathf.Approximately(ls.y, 0f) ? 1f : Mathf.Abs(ls.y);
            float az = Mathf.Approximately(ls.z, 0f) ? 1f : Mathf.Abs(ls.z);
            float rxz = (ax + az) * 0.5f;
            float rLocal = worldRadius / rxz;

            // The offset is expressed in world units; convert to the head's local units by dividing per-axis.
            Vector3 offLocal = new Vector3(localOffsetWorld.x / ax, localOffsetWorld.y / ay, localOffsetWorld.z / az);

            Mesh mesh = ProceduralCreature.BuildBlob(rLocal, 0, lumpiness * 0.5f); // low subdiv: a small bead

            GameObject go = new GameObject("ftkmf_golem_eye");
            go.transform.SetParent(head, false);
            go.transform.localPosition = offLocal;
            go.transform.localRotation = Quaternion.identity;
            go.transform.localScale = Vector3.one;

            MeshFilter mf = go.AddComponent<MeshFilter>();
            mf.sharedMesh = mesh;
            MeshRenderer mr = go.AddComponent<MeshRenderer>();
            mr.sharedMaterial = eyeMat;
            return 1;
        }

        /// <summary>The shared mossy-green wet Standard material for every golem segment/blob. Reuses the body tint
        /// + the wet-skin knobs (smoothness/metallic) so the golem matches the registered identity.</summary>
        private static Material BuildGolemMaterial(EnemyVisual v)
        {
            Material mat = new Material(Shader.Find("Standard"));
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", v.tint);
            else mat.color = v.tint;
            if (mat.HasProperty("_Glossiness")) mat.SetFloat("_Glossiness", v.smoothness);
            if (mat.HasProperty("_Metallic"))   mat.SetFloat("_Metallic", v.metallic);
            return mat;
        }

        /// <summary>An emissive eye material (Standard + _EMISSION on the golemEyeGlow color). Emission is safe here:
        /// it lives on OUR object, which the game's CEL hit-flash reset never touches.</summary>
        private static Material BuildEyeMaterial(EnemyVisual v)
        {
            Material mat = new Material(Shader.Find("Standard"));
            Color glow = v.golemEyeGlow;
            if (mat.HasProperty("_Color")) mat.SetColor("_Color", glow);
            else mat.color = glow;
            mat.EnableKeyword("_EMISSION");
            if (mat.HasProperty("_EmissionColor")) mat.SetColor("_EmissionColor", glow);
            return mat;
        }

        // ---- AssetBundle mesh swap --------------------------------------------------------------------------

        /// <summary>
        /// Swap the chassis body SkinnedMeshRenderer's <c>sharedMesh</c> to a bundle-loaded <see cref="Mesh"/>,
        /// REUSING the existing skeleton (bones + bindposes) and animations, and optionally push a bundle-loaded
        /// <see cref="Texture2D"/> into the body material's <c>_MainTex</c>. The body SMR is the one on the exact child
        /// <see cref="GolemMeshChild"/> ("enTroll01"); if that is absent we fall back to the first SkinnedMeshRenderer
        /// under the CEL. On any miss we LOG and leave the original mesh intact (so the body never disappears). The
        /// custom mesh must be authored/skinned against the SAME skeleton this clone uses, or it will deform wrongly.
        /// </summary>
        private static void ApplyMeshSwap(string enemyId, CharacterEventListener cel, EnemyVisual v)
        {
            // Resolve the body SkinnedMeshRenderer: exact 'enTroll01' child first, else the first SMR under the CEL.
            SkinnedMeshRenderer smr = null;
            string via;
            Transform meshChild = FindExact(cel, GolemMeshChild);
            if (meshChild != null) smr = meshChild.GetComponent<SkinnedMeshRenderer>();
            if (smr != null)
            {
                via = "exact child '" + GolemMeshChild + "'";
            }
            else
            {
                smr = cel.GetComponentInChildren<SkinnedMeshRenderer>(true);
                via = smr != null ? "first SkinnedMeshRenderer under CEL" : null;
            }

            if (smr == null)
            {
                Plugin.Log.LogWarning("[enemy-visual] mesh swap: no body SkinnedMeshRenderer found for '" + enemyId +
                    "'; original mesh kept.");
                return;
            }

            // RUNTIME glTF (.glb) PATH (EDITOR-FREE), tried FIRST: build a Mesh from a shipped .glb, name-keyed to the
            // VANILLA skeleton, REUSING the live troll bindposes (captured BEFORE the swap). On success swap the mesh
            // (+ optional .png into _MainTex) and return; on a null load LOG and fall through to the bundle path below
            // (so a registered bundle still works). Never throws (the loader logs + returns null on any failure).
            if (!string.IsNullOrEmpty(v.glbMesh))
            {
                // Capture the LIVE troll bindposes BEFORE swapping. Unity guarantees mesh.bindposes[i] pairs with
                // smr.bones[i], so these drive the name-remapped skinning on the new mesh.
                Matrix4x4[] origBind = (smr.sharedMesh != null) ? smr.sharedMesh.bindposes : null;
                Mesh gmesh = RuntimeGltfMeshLoader.LoadSkinnedGlb(v.glbMesh, smr.bones, origBind);
                if (gmesh != null)
                {
                    if (IsStaticBossMode())
                    {
                        // EXPERIMENT (#72): static-attach for rigid mesh; see ftk-architect/decompile-analyst review.
                        // The custom boss mesh is 100% RIGID (all verts weighted to one bone, Root_M). For a one-bone
                        // mesh, skinning is unnecessary: a plain MeshRenderer parented to that bone is behaviorally
                        // equivalent and sidesteps the bindpose/rebind problem that scattered the skinned vertices
                        // (Phase 0 discriminator). Mirrors the proven BuildProceduralBody + weapon-prop precedent:
                        // hide the chassis SMR, then build a static MeshFilter+MeshRenderer under the live bone.
                        ApplyStaticBossAttach(enemyId, cel, smr, gmesh, via, v);
                        return;
                    }

                    // OLD SKINNED PATH (FTK_BOSS_STATIC=0): kept intact for A/B comparison against the static path.
                    smr.sharedMesh = gmesh;

                    // FR-2 (spec #72): force the SkinnedMeshRenderer to rebind the skin to the NEW mesh's bindpose set.
                    // A bare sharedMesh swap leaves the SMR's skin binding pointed at the previous mesh's bindposes, which
                    // scatters the new vertices (the Phase 0 skip-skin discriminator isolated the shatter to this rebind).
                    // The custom mesh carries its OWN bindposes (name-remapped to runtime order by the loader); we reuse the
                    // SAME live bones in the SAME order, just reassigned (fresh array) so Unity re-establishes the binding.
                    Transform[] liveBones = smr.bones;
                    if (liveBones != null)
                    {
                        Transform[] rebind = new Transform[liveBones.Length];
                        System.Array.Copy(liveBones, rebind, liveBones.Length);
                        smr.bones = rebind;
                    }

                    // OPTIONAL TEXTURE: load a .png from FTKModFramework_content/models/<glbTexture> and push it into
                    // the body material's _MainTex (same .materials loop as the bundle path). A miss is non-fatal.
                    if (!string.IsNullOrEmpty(v.glbTexture))
                    {
                        try
                        {
                            string texPath = CustomModelLoader.ResolveModelPath(v.glbTexture);
                            if (System.IO.File.Exists(texPath))
                            {
                                Texture2D tex = new Texture2D(2, 2);
                                tex.LoadImage(System.IO.File.ReadAllBytes(texPath));

                                Material[] gmats = smr.materials;
                                for (int i = 0; i < gmats.Length; i++)
                                {
                                    Material m = gmats[i];
                                    if (m == null) continue;
                                    if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                                    else m.mainTexture = tex;
                                    // Self-illuminate with the baked basecolor so the golem READS in the
                                    // Flooded Crypt's dim cool light (which otherwise mutes the mossy texture
                                    // to a dark purple). Subtle moss-grey emission, texture-modulated.
                                    if (m.HasProperty("_EmissionColor"))
                                    {
                                        m.EnableKeyword("_EMISSION");
                                        if (m.HasProperty("_EmissionMap")) m.SetTexture("_EmissionMap", tex);
                                        m.SetColor("_EmissionColor", new Color(0.45f, 0.50f, 0.40f));
                                    }
                                }
                            }
                            else
                            {
                                Plugin.Log.LogWarning("[enemy-visual] glb texture: not found at '" + texPath +
                                    "' for '" + enemyId + "'; mesh applied without it.");
                            }
                        }
                        catch (Exception te)
                        {
                            Plugin.Log.LogWarning("[enemy-visual] glb texture load failed for '" + enemyId + "': " +
                                te.Message + "; mesh applied without it.");
                        }
                    }

                    Plugin.Log.LogInfo("[enemy-visual] mesh swap: set body mesh from runtime glb '" + v.glbMesh +
                        "' (SMR via " + via + ")" + (string.IsNullOrEmpty(v.glbTexture) ? "" : " + texture '" +
                        v.glbTexture + "'") + " for '" + enemyId + "'.");
                    return;
                }

                Plugin.Log.LogWarning("[enemy-visual] mesh swap: runtime glb '" + v.glbMesh + "' did not load for '" +
                    enemyId + "'; falling back to the bundle path (if any).");
            }

            // ASSETBUNDLE PATH (unchanged): only runs if a bundle is registered. If only a glb was registered and it
            // failed to load, both names are empty here and the body keeps its original/procedural mesh.
            if (string.IsNullOrEmpty(v.meshBundle) || string.IsNullOrEmpty(v.meshName)) return;

            Mesh mesh = CustomModelLoader.LoadMesh(v.meshBundle, v.meshName);
            if (mesh == null)
            {
                // LoadMesh already logged the reason. Leave the original mesh intact.
                Plugin.Log.LogWarning("[enemy-visual] mesh swap: mesh '" + v.meshName + "' from bundle '" +
                    v.meshBundle + "' not loaded for '" + enemyId + "'; original mesh kept.");
                return;
            }

            // Reuse the existing skeleton: sharedMesh swap keeps smr.bones / rootBone untouched, and the custom mesh's
            // own bindposes drive the skinning, so all vanilla animations continue to play on the new mesh.
            smr.sharedMesh = mesh;

            // OPTIONAL TEXTURE: push a bundle-loaded Texture2D into the body material's _MainTex. Use .materials (the
            // instanced per-clone copies) so the vanilla shared material is untouched.
            if (!string.IsNullOrEmpty(v.meshTextureName))
            {
                Texture2D tex = CustomModelLoader.LoadTexture(v.meshBundle, v.meshTextureName);
                if (tex != null)
                {
                    Material[] mats = smr.materials;
                    for (int i = 0; i < mats.Length; i++)
                    {
                        Material m = mats[i];
                        if (m == null) continue;
                        if (m.HasProperty("_MainTex")) m.SetTexture("_MainTex", tex);
                        else m.mainTexture = tex;
                    }
                }
                // a texture miss is non-fatal: the mesh swap above already applied.
            }

            Plugin.Log.LogInfo("[enemy-visual] mesh swap: set body mesh '" + v.meshName + "' (bundle '" + v.meshBundle +
                "', SMR via " + via + ")" + (string.IsNullOrEmpty(v.meshTextureName) ? "" : " + texture '" +
                v.meshTextureName + "'") + " for '" + enemyId + "'.");
        }

        // ---- EXPERIMENT (#72): static-attach render path for a rigid (one-bone) glb boss mesh ---------------

        /// <summary>
        /// Whether the static-attach experiment path is active. Gated by env var <c>FTK_BOSS_STATIC</c>:
        /// unset OR "1" = ON (the experiment / candidate fix), "0" = the old skinned sharedMesh+rebind path.
        /// Default ON so the controller's boss-isolated capture exercises the static path without a rebuild.
        /// </summary>
        private static bool IsStaticBossMode()
        {
            string s = null;
            try { s = Environment.GetEnvironmentVariable("FTK_BOSS_STATIC"); }
            catch { /* env read can throw under restricted hosts; treat as unset (default ON) */ }
            if (string.IsNullOrEmpty(s)) return true;     // default ON for this experiment
            return !string.Equals(s.Trim(), "0", StringComparison.Ordinal);
        }

        /// <summary>
        /// EXPERIMENT (#72/#74): render the rigid one-bone glb mesh as a PLAIN static MeshRenderer, skipping
        /// skinning/bindpose entirely. The chassis SkinnedMeshRenderer is DISABLED (not destroyed) so it does not
        /// peek through.
        ///
        /// PLACEMENT (this iteration): stop guessing anchors. Capture the LIVE skinned troll's world-space AABB
        /// (<c>smr.bounds</c>) and rotation (<c>smr.transform.rotation</c>) BEFORE disabling the SMR, then drop the
        /// static mesh into that EXACT world volume. Earlier iterations anchored "feet at the clone-root origin",
        /// which floated the boss ~8u above the floor because the clone-root pivot is NOT at the floor (the troll's
        /// BONES are at the floor; the clone-root pivot sits up high). Setting the body's WORLD transform
        /// (rotation/scale/position) directly is parent-agnostic, so it is correct whether the body is parented to the
        /// clone root or left unparented.
        ///
        /// The body's WORLD scale is uniform, sized so the mesh's natural height matches the troll AABB height
        /// (<c>FTK_BOSS_STATIC_HEIGHT</c> overrides if set); its WORLD rotation matches the troll's; its WORLD position
        /// is solved so the scaled+rotated mesh bbox's bottom-center lands on the troll's floor point
        /// (<c>center.x, min.y, center.z</c>). Deterministic: the boss stands exactly where the troll visibly stood.
        ///
        /// Idempotent per clone (name guard, like the lantern/golem). Visual-only / deterministic, so it stays co-op-
        /// and save-safe like the rest of this file. Mirrors the proven BuildProceduralBody pattern (MeshFilter +
        /// MeshRenderer with explicit world placement).
        /// </summary>
        private static void ApplyStaticBossAttach(string enemyId, CharacterEventListener cel, SkinnedMeshRenderer smr,
            Mesh gmesh, string via, EnemyVisual v)
        {
            // IDEMPOTENCY PER CLONE: a re-apply on this same fresh clone must not stack a second static body.
            if (FindExact(cel, GlbStaticBodyName) != null) return;

            // 1) CAPTURE THE LIVE TROLL'S WORLD VOLUME *BEFORE* hiding it. smr.bounds is the skinned troll's
            //    world-space AABB (exactly where it visibly stands); smr.transform.rotation is the facing. These drive
            //    the static placement below, so the static mesh occupies the same on-screen volume the troll did.
            //    ALSO capture the troll body's LAYER here, BEFORE disabling the SMR: the SMR's GameObject lives on the
            //    combat-diorama layer (the layer the diorama camera's culling mask renders). A fresh GameObject defaults
            //    to layer 0 (Default), which Unity does NOT inherit from the parent and which the diorama camera's mask
            //    excludes; that culls the static body (isVisible=False) even with correct in-frustum bounds. So we must
            //    set the static body to this same layer explicitly (see step 3b below). The proven golem path
            //    (BuildProceduralBody + its segment/blob/eye builders) does NOT set a layer on the GameObjects it
            //    creates; its segments render only because they parent to skeleton bones AND the diorama camera happens
            //    to also render layer 0 for those bone-parented objects, so it is not a reliable model for an
            //    UNPARENTED-in-world static body. Setting our layer to smr.gameObject.layer is the hypothesis.
            Bounds trollWorld = smr.bounds;
            Quaternion trollRot = smr.transform.rotation;
            int dioramaLayer = smr.gameObject.layer;
            Plugin.Log.LogInfo("[enemy-visual] static-attach: live troll world AABB center=" + trollWorld.center +
                " size=" + trollWorld.size + " min=" + trollWorld.min + " max=" + trollWorld.max +
                " rotation=" + trollRot.eulerAngles +
                " trollLayer=" + dioramaLayer + " ('" + LayerMask.LayerToName(dioramaLayer) + "')" +
                " for '" + enemyId + "'.");

            // 2) HIDE the troll body SMR (do NOT destroy: the skeleton/bones must stay live so the clone root persists).
            smr.enabled = false;

            // 3) Choose a PARENT for the static body. Placement below is set in WORLD space (rotation/position) so the
            //    parent does not change where the mesh renders; we still parent to the clone root (smr.transform.parent)
            //    by default so the body is cleaned up with the clone and inherits its active/inactive state. Fall back
            //    to smr.transform if the parent is null.
            Transform mount = smr.transform.parent;
            string parentName;
            if (mount == null)
            {
                mount = smr.transform;
                parentName = "smr.transform:" + (mount.name != null ? mount.name : "(unnamed)");
                Plugin.Log.LogWarning("[enemy-visual] static-attach: smr.transform.parent is null for '" + enemyId +
                    "'; parenting to smr.transform '" + (mount.name != null ? mount.name : "(unnamed)") + "'.");
            }
            else
            {
                parentName = (mount.name != null) ? mount.name : "(unnamed)";
            }

            GameObject body = new GameObject(GlbStaticBodyName);
            body.transform.SetParent(mount, false);

            // 3b) SET THE LAYER TO THE DIORAMA LAYER (the likely fix for isVisible=False). A fresh GameObject is on
            //     layer 0 (Default), which Unity does NOT inherit from the parent and which the combat-diorama camera's
            //     culling mask excludes, so the renderer is culled. Match the live troll body's layer (captured above,
            //     before the SMR was disabled). The body has no children, so this single set covers it; the MeshRenderer
            //     added below lives on this same GameObject, so it inherits this layer (the renderer.gameObject IS body).
            body.layer = dioramaLayer;

            // 4) Read the custom mesh's model-space bbox so the placement ADAPTS to the actual mesh (no hardcoded bbox).
            //    Correct bounds matter for a static MeshRenderer (no updateWhenOffscreen on MeshRenderer); the loader
            //    already recalculates, but RecalculateBounds is idempotent so we re-assert it cheaply here.
            gmesh.RecalculateBounds();

            // 4a) RECALCULATE NORMALS so the Lit Standard material (below) shades with CONSISTENT normals. The AI-authored
            //     glb's per-vertex normals are unreliable (inconsistent winding -> inconsistent normals -> patchy, broken
            //     shading), so we recompute smooth-ish normals from the geometry, giving the Standard shader a coherent
            //     basis to read the boss's form. Guarded so it runs ONCE per Mesh instance (RecalculateNormals is not
            //     free, and the static path runs per clone; the loader builds a fresh Mesh per LoadSkinnedGlb call, but
            //     the guard also makes a hypothetical reused-instance path safe).
            int meshId = gmesh.GetInstanceID();
            if (_normalsRecalculated.Add(meshId))
            {
                gmesh.RecalculateNormals();
                Plugin.Log.LogInfo("[enemy-visual] static-attach: recalculated mesh normals (instanceID=" + meshId +
                    ") for '" + enemyId + "'.");
            }

            Bounds mb = gmesh.bounds;                 // model-space bbox of the custom mesh
            float meshHeight = mb.size.y;             // natural authored height (feet->head), ~2.65 for the mudwretch
            if (meshHeight <= 1e-4f) meshHeight = 1f;  // degenerate guard

            // 5) TARGET WORLD HEIGHT: match the troll's on-screen height by default. FTK_BOSS_STATIC_HEIGHT is an
            //    OPTIONAL override (env-tunable so size can be swept without a rebuild).
            float targetHeight = trollWorld.size.y;
            if (targetHeight <= 1e-4f) targetHeight = meshHeight; // degenerate-AABB guard
            try
            {
                string hs = Environment.GetEnvironmentVariable("FTK_BOSS_STATIC_HEIGHT");
                if (!string.IsNullOrEmpty(hs))
                {
                    float parsed;
                    if (float.TryParse(hs.Trim(), out parsed) && parsed > 1e-4f) targetHeight = parsed;
                }
            }
            catch { /* env read can throw under restricted hosts; keep the troll-matched default */ }

            // The uniform WORLD scale that makes the mesh's natural height equal the target world height.
            float worldScale = targetHeight / meshHeight;

            // 6) SET WORLD TRANSFORM to occupy the troll's volume. Order: scale -> rotation -> position, so Unity's
            //    world<->local resolution is consistent. Setting .rotation and .position in WORLD space is correct
            //    regardless of the parent's transform.
            //    Scale: uniform worldScale in WORLD units. If parented, the lossyScale of the parent is baked in, so
            //    divide per-axis (guarded against a zero/near-zero axis) to land at worldScale in world units. (When
            //    the parent is identity-scaled this reduces to Vector3.one * worldScale.)
            Vector3 pls = mount.lossyScale;
            float ax = Mathf.Approximately(pls.x, 0f) ? 1f : Mathf.Abs(pls.x);
            float ay = Mathf.Approximately(pls.y, 0f) ? 1f : Mathf.Abs(pls.y);
            float az = Mathf.Approximately(pls.z, 0f) ? 1f : Mathf.Abs(pls.z);
            body.transform.localScale = new Vector3(worldScale / ax, worldScale / ay, worldScale / az);

            // Rotation: face the camera exactly as the troll did.
            body.transform.rotation = trollRot;

            // Position: stand the SCALED mesh's bottom-center on the troll's floor point. The floor point is the AABB
            // bottom-center (center.x, min.y, center.z). The mesh's model-space bottom-center is bc; after scale and
            // rotation, the world offset of that point from the body origin is trollRot * (worldScale * bc). So the
            // body origin must sit at floorPoint - that offset. Set position AFTER rotation+scale.
            Vector3 floorPoint = new Vector3(trollWorld.center.x, trollWorld.min.y, trollWorld.center.z);
            Vector3 bc = new Vector3(mb.center.x, mb.min.y, mb.center.z);
            body.transform.position = floorPoint - trollRot * (worldScale * bc);

            MeshFilter mf = body.AddComponent<MeshFilter>();
            mf.sharedMesh = gmesh;
            MeshRenderer mr = body.AddComponent<MeshRenderer>();

            // 4) MATERIAL: render the rigid boss LIT and DOUBLE-SIDED so the coherent mesh reads as its true best
            //    in-game form (the offline Blender preview rendered lit + double-sided; we match that here). The earlier
            //    Unlit + single-sided setup showed the mesh as flat gappy triangle-soup: UNLIT gives no shading, so the
            //    form was lost (a flat silhouette), and SINGLE-SIDED back-face culling dropped the backward-wound
            //    triangles (the AI mesh has inconsistent winding) -> see-through holes. The fixes:
            //      - LIT Standard shader: scene/diorama direct lights shade the recalculated normals (step 4a), giving
            //        the mesh real form instead of a flat plate.
            //      - DOUBLE-SIDED (_Cull=0, Off): both faces render, filling the holes from inconsistent winding.
            //      - MODEST texture-modulated emission: a postfix MeshRenderer gets NO light probes, so in the dim crypt
            //        it would read near-black; a subtle grey emission (StaticBossEmission) lifts it out of pure black
            //        while still letting the diorama's direct lights shape the shading (full-white emission would wash
            //        the shading out). Emission is safe on OUR object: the game's CEL hit-flash _EmissionColor reset only
            //        touches CEL-managed body materials, never this added MeshRenderer.
            //    The troll's skinned-character shader is NOT reused (it expects skin/bone inputs a plain MeshRenderer
            //    never supplies).
            Material bodyMat = new Material(Shader.Find("Standard"));
            Plugin.Log.LogInfo("[enemy-visual] static-attach: body shader = '" +
                (bodyMat.shader != null && bodyMat.shader.name != null ? bodyMat.shader.name : "(null)") +
                "' (Lit Standard, double-sided) for '" + enemyId + "'.");

            // DOUBLE-SIDED: _Cull=0 (Off) renders both faces so the inconsistent-winding holes are filled.
            if (bodyMat.HasProperty("_Cull")) bodyMat.SetInt("_Cull", 0);

            bool texApplied = false;
            string texFile = v.glbTexture;
            if (!string.IsNullOrEmpty(v.glbTexture))
            {
                try
                {
                    string texPath = CustomModelLoader.ResolveModelPath(v.glbTexture);
                    if (System.IO.File.Exists(texPath))
                    {
                        Texture2D tex = new Texture2D(2, 2);
                        tex.LoadImage(System.IO.File.ReadAllBytes(texPath));
                        if (bodyMat.HasProperty("_MainTex")) bodyMat.SetTexture("_MainTex", tex);
                        else bodyMat.mainTexture = tex;
                        // MODEST emission, texture-modulated: lift the body out of pure black in the dim crypt without
                        // washing out the scene-light shading. _EmissionMap = the basecolor texture, _EmissionColor = a
                        // subtle grey (StaticBossEmission), so the emission carries the baked detail at low intensity.
                        if (bodyMat.HasProperty("_EmissionColor"))
                        {
                            bodyMat.EnableKeyword("_EMISSION");
                            if (bodyMat.HasProperty("_EmissionMap")) bodyMat.SetTexture("_EmissionMap", tex);
                            bodyMat.SetColor("_EmissionColor", StaticBossEmission);
                        }
                        texApplied = true;
                    }
                    else
                    {
                        Plugin.Log.LogWarning("[enemy-visual] static-attach: glb texture not found at '" + texPath +
                            "' for '" + enemyId + "'; body rendered without it.");
                    }
                }
                catch (Exception te)
                {
                    Plugin.Log.LogWarning("[enemy-visual] static-attach: glb texture load failed for '" + enemyId +
                        "': " + te.Message + "; body rendered without it.");
                }
            }

            // NO TEXTURE: still enable the modest emission (flat grey) so an untextured body is not pure black in the
            // crypt; the diorama lights shade it via the recalculated normals.
            if (!texApplied && bodyMat.HasProperty("_EmissionColor"))
            {
                bodyMat.EnableKeyword("_EMISSION");
                bodyMat.SetColor("_EmissionColor", StaticBossEmission);
            }

            mr.sharedMaterial = bodyMat;

            // TEMP STATIC-DIAG (#72): remove once static render confirmed. One line, fully null-guarded, never throws:
            // the renderer/object/bounds/mesh/shader state so a STILL-invisible body can be diagnosed exactly.
            try
            {
                Bounds wb = mr.bounds;                              // WORLD-space bounds of the live renderer
                Mesh dm = (mf != null) ? mf.sharedMesh : null;
                int verts = (dm != null) ? dm.vertexCount : -1;
                Shader dsh = (bodyMat != null) ? bodyMat.shader : null;
                string shName = (dsh != null && dsh.name != null) ? dsh.name : "(null)";
                // LAYER CONFIRMATION: log the body's actual layer (int + name) so the capture verifies the set took,
                // and the original troll SMR GameObject layer (int + name) to confirm we matched the diorama layer.
                int bodyLayer = body.layer;
                int smrLayer = smr.gameObject.layer;
                Plugin.Log.LogInfo("[enemy-visual][STATIC-DIAG] enabled=" + mr.enabled +
                    " isVisible=" + mr.isVisible +
                    " activeInHierarchy=" + body.activeInHierarchy +
                    " activeSelf=" + body.activeSelf +
                    " bounds.center=" + wb.center + " bounds.size=" + wb.size +
                    " vertexCount=" + verts + " shader='" + shName + "'" +
                    " body.layer=" + bodyLayer + " ('" + LayerMask.LayerToName(bodyLayer) + "')" +
                    " smr.layer=" + smrLayer + " ('" + LayerMask.LayerToName(smrLayer) + "')");
            }
            catch (Exception de)
            {
                Plugin.Log.LogWarning("[enemy-visual][STATIC-DIAG] diag read failed for '" + enemyId + "': " + de.Message);
            }

            Plugin.Log.LogInfo("[enemy-visual] static-attach: rendered glb '" + v.glbMesh + "' as MeshRenderer parented to '" +
                parentName + "' (SMR disabled, via " + via + ")" +
                (texApplied ? " + texture '" + texFile + "'" : "") + " for '" + enemyId + "'." +
                " trollWorld.center=" + trollWorld.center + " trollWorld.size=" + trollWorld.size +
                " meshHeight=" + meshHeight.ToString("0.###") + " targetHeight=" + targetHeight.ToString("0.###") +
                " worldScale=" + worldScale.ToString("0.###") +
                " body.position=" + body.transform.position + " body.lossyScale=" + body.transform.lossyScale +
                " body.rotation=" + body.transform.rotation.eulerAngles + " meshBounds(c=" + mb.center +
                ", min=" + mb.min + ", max=" + mb.max + ")");
        }

        /// <summary>Full slash-separated path from the body root to a transform (for the lantern mount log line).</summary>
        private static string FullPath(Transform t)
        {
            if (t == null) return "(null)";
            StringBuilder sb = new StringBuilder(t.name);
            Transform p = t.parent;
            while (p != null)
            {
                sb.Insert(0, p.name + "/");
                p = p.parent;
            }
            return sb.ToString();
        }
    }
}
