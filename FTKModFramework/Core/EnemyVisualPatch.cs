using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// VISUAL-ONLY enemy identity: recolor + rescale a registered enemy's spawned body, per combat, on its
    /// freshly-instantiated clone. The single registration point is <see cref="Content.SetEnemyVisual"/>; this
    /// file is the engine plumbing behind it (the Core/Content boundary: public API in Content, mechanics here).
    ///
    /// WHY THIS IS DETERMINISM- AND SAVE-SAFE (verified RE, decompiled Assembly-CSharp, Unity 2017.2.2p2):
    ///   - The enemy body is <c>FTK_enemyCombat.m_EnemyAsset</c> (a <c>CharacterEventListener</c>), Instantiated
    ///     PER COMBAT by <c>EnemyDummy.InitEnemyDummyForCombat</c> (EnemyDummy.cs:43/49) into the public field
    ///     <c>CharacterDummy.m_EventListener</c> (CharacterDummy.cs:123). We edit THAT clone, never the shared
    ///     prefab, so the change is isolated per spawn and leaks to nothing (incl. vanilla).
    ///   - Enemy visuals NEVER network or persist: the 3D model / material / texture / body localScale never
    ///     cross Photon and are never serialized. Only the enum-int identity (the registered enemy id) and the
    ///     DB field <c>m_MarkerScale</c> are shared state, and both are read identically from the same mod DB on
    ///     every machine. So a recolor/rescale produces byte-for-byte identical game state across co-op.
    ///   - The game never re-tints an enemy's <c>_Color</c> (CEL.SetTintColor is player-only and would NPE on an
    ///     enemy; CEL.Update does no _Color/localScale reset), so the edits stick after we apply them.
    ///
    /// PER-SPAWN RE-APPLICATION (NOT a registration-idempotency violation): the clone is brand-new every combat,
    /// so the postfix deliberately has NO <c>_done</c> guard for the apply itself; it must run on each spawn or
    /// the second fight would render the vanilla look. The registration (the dictionary entry) is the idempotent
    /// part; re-applying to each fresh clone is correct.
    /// </summary>
    [HarmonyPatch(typeof(EnemyDummy), "InitEnemyDummyForCombat", new Type[] { typeof(bool), typeof(bool), typeof(bool) })]
    internal static class EnemyVisualPatch
    {
        /// <summary>A registered visual override: a body tint and a uniform body scale.</summary>
        internal struct EnemyVisual
        {
            public Color tint;
            public float scale;
        }

        // Keyed by FTK_enemyCombat.m_ID (the registered string id, e.g. "ftkmf_mudwretch_foreman"). We gate on
        // m_ID, NOT EnemyDummy.m_EnemyType, to avoid the decimal-string-id ambiguity of the latter.
        private static readonly Dictionary<string, EnemyVisual> _visuals = new Dictionary<string, EnemyVisual>();

        /// <summary>Register (or replace) the visual override for an enemy id. Internal: callers go through
        /// <see cref="Content.SetEnemyVisual"/>.</summary>
        internal static void Register(string enemyId, Color tint, float scale)
        {
            if (string.IsNullOrEmpty(enemyId)) return;
            EnemyVisual v;
            v.tint = tint;
            v.scale = scale;
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

                // SCALE the body. No _done guard: the clone is fresh every combat and must be re-scaled each
                // spawn (per-spawn re-application is correct here, not a registration-idempotency violation).
                cel.transform.localScale = Vector3.one * v.scale;

                // RECOLOR every renderer in the body. Use GetComponentsInChildren (recursive) because the body's
                // SkinnedMeshRenderer is usually nested deeper than CEL.m_Renderers (direct-children-only) sees.
                // Operate on .materials (the instanced per-clone copies), so vanilla shared materials are untouched.
                // In-engine inventory (verified): the trollCaveA body is renderer 'enTroll01' / material
                // 'matTrollCaveA', Standard shader, HasProperty(_Color)==true, so the tint multiplies the albedo.
                Renderer[] renderers = cel.GetComponentsInChildren<Renderer>(true);
                foreach (Renderer r in renderers)
                {
                    if (r == null) continue;
                    Material[] mats = r.materials;
                    foreach (Material m in mats)
                    {
                        if (m == null) continue;
                        if (m.HasProperty("_Color")) m.SetColor("_Color", v.tint);
                        else m.color = v.tint;
                    }
                }

                // TODO (deferred): off-hand lantern prop via Weapon.m_OffHand -> WEAPON_HOLDER_L (CEL off-hand
                // bone) once a prop mesh + the bone's presence on the chosen chassis are confirmed in-engine.
            }
            catch (Exception e)
            {
                // Never throw into the spawn builder: a visual tweak must never break combat. Log and move on.
                Plugin.Log.LogWarning("[enemy-visual] apply failed: " + e.Message);
            }
        }
    }
}
