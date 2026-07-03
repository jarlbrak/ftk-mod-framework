using System;
using System.Collections.Generic;
using GridEditor;
using HarmonyLib;
using UnityEngine;

namespace FTKModFramework.Core
{
    /// <summary>
    /// High-level authoring API: clone an existing entry, tweak it, give it a name. Each helper
    /// funnels through <see cref="ContentRegistry"/> (so IDs stay deterministic / save-safe) and
    /// registers a display name via <see cref="Localization"/>.
    ///
    /// Cloning a template means the new entry inherits a valid icon / prefab / animation set, so it
    /// is immediately usable in-game; you then override only the fields you care about.
    /// </summary>
    public static class Content
    {
        /// <summary>
        /// Fetch a DB and make sure its int-&gt;row index is built. At TableManager.Initialize time the
        /// DB components' own Awake() (which calls MakeIndex) may not have run yet, so GetEntry would
        /// return null without this.
        /// </summary>
        public static T Db<T>() where T : GEDataArrayBase
        {
            T db = TableManager.Instance.Get<T>();
            Reflect.Invoke(db, "CheckAndMakeIndex");
            return db;
        }

        /// <summary>Add a WEAPON (clones an existing weapon's FTK_weaponStats2 row).</summary>
        public static FTK_weaponStats2 AddWeapon(
            string modGuid, string id, FTK_itembase.ID template, string displayName,
            Action<FTK_weaponStats2> configure = null)
        {
            FTK_weaponStats2DB db = Db<FTK_weaponStats2DB>();
            FTK_weaponStats2 tmpl = db.GetEntry(template);
            FTK_weaponStats2 row = (FTK_weaponStats2)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_weaponStats2)o); });
            Localization.SetName(id, displayName);
            return row;
        }

        /// <summary>Add a non-weapon ITEM (clones an existing FTK_items row).</summary>
        public static FTK_items AddItem(
            string modGuid, string id, FTK_itembase.ID template, string displayName,
            Action<FTK_items> configure = null)
        {
            FTK_itemsDB db = Db<FTK_itemsDB>();
            FTK_items tmpl = db.GetEntry(template);
            FTK_items row = (FTK_items)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_items)o); });
            Localization.SetName(id, displayName);
            return row;
        }

        /// <summary>Add a combat action / ability (clones an existing FTK_proficiencyTable row).</summary>
        public static FTK_proficiencyTable AddProficiency(
            string modGuid, string id, FTK_proficiencyTable.ID template, string displayName,
            Action<FTK_proficiencyTable> configure = null)
        {
            FTK_proficiencyTableDB db = Db<FTK_proficiencyTableDB>();
            FTK_proficiencyTable tmpl = db.GetEntry(template);
            FTK_proficiencyTable row = (FTK_proficiencyTable)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_proficiencyTable)o); });
            Localization.SetName(id, displayName);
            return row;
        }

        /// <summary>
        /// Give a weapon a proficiency (combat action) it didn't have.
        ///
        /// A weapon's available actions are the keys of its prefab's Weapon.m_ProficiencyEffects.
        /// Custom weapons clone an existing weapon and therefore SHARE its prefab, so we first
        /// Instantiate a private copy of the prefab (kept inactive + persistent), add our proficiency
        /// to the copy, and repoint the weapon at it — leaving the original weapon untouched.
        /// Returns true on success.
        /// </summary>
        public static bool AttachProficiency(FTK_weaponStats2 weapon, string proficiencyId)
        {
            return AttachProficiencies(weapon, proficiencyId);
        }

        /// <summary>Attach one or more proficiencies to a weapon in a single private prefab copy.</summary>
        public static bool AttachProficiencies(FTK_weaponStats2 weapon, params string[] proficiencyIds)
        {
            if (weapon == null) return false;
            GameObject src = weapon.m_Prefab;
            if (src == null)
            {
                Plugin.Log.LogWarning("AttachProficiencies: weapon '" + weapon.m_ID + "' has no prefab.");
                return false;
            }

            GameObject copy = UnityEngine.Object.Instantiate(src);
            UnityEngine.Object.DontDestroyOnLoad(copy);
            copy.name = src.name + "_ftkmf";
            // Keep it ACTIVE but park it far off-screen: the game re-Instantiates this prefab and reads
            // its Weapon via GetComponentInChildren<Weapon>() WITHOUT includeInactive, so an inactive
            // copy would resolve to null and NPE.
            copy.transform.position = new Vector3(0f, -100000f, 0f);

            Weapon w = copy.GetComponentInChildren<Weapon>(true);
            if (w == null)
            {
                Plugin.Log.LogWarning("AttachProficiencies: no Weapon component on prefab of '" + weapon.m_ID + "'.");
                UnityEngine.Object.Destroy(copy);
                return false;
            }

            int count = AddProfsToWeapon(w, proficiencyIds);

            weapon.m_Prefab = copy;
            Plugin.Log.LogInfo("AttachProficiencies: added " + proficiencyIds.Length + " to '" + weapon.m_ID +
                "' (now " + count + " actions).");
            return true;
        }

        /// <summary>
        /// Shared tail of the weapon / enemy proficiency-attach paths. Ensures the weapon's
        /// <see cref="Weapon.m_ProficiencyEffects"/> dictionary exists, reuses an existing
        /// <see cref="HitEffect"/> from it so the added actions render (warning if there is none to reuse,
        /// since the new actions would then have no impact visual), adds each proficiency id to the dict,
        /// and pushes the runtime dictionary into FullInspector's serialized backing via
        /// <see cref="Weapon.SaveState"/> so it survives the game's re-Instantiate of the weapon.
        /// Returns the dictionary's new entry count.
        /// </summary>
        private static int AddProfsToWeapon(Weapon w, string[] proficiencyIds)
        {
            if (w.m_ProficiencyEffects == null)
                w.m_ProficiencyEffects = new Dictionary<ProficiencyID, HitEffect>();

            // Reuse an existing HitEffect (the visual/impact) from this weapon so the actions render.
            HitEffect reuse = null;
            foreach (HitEffect v in w.m_ProficiencyEffects.Values) { reuse = v; break; }
            if (reuse == null)
                Plugin.Log.LogWarning("AddProfsToWeapon: weapon had no HitEffect to reuse; new actions may lack an impact visual.");

            foreach (string profId in proficiencyIds)
            {
                ProficiencyID key = new ProficiencyID((FTK_proficiencyTable.ID)0);
                key.m_ID = profId; // resolved back to our synthetic id via the patched GetEnum
                w.m_ProficiencyEffects[key] = reuse;
            }

            // Push the runtime dictionary into FullInspector's serialized backing so the change
            // survives the game's Object.Instantiate of the weapon (which re-deserializes it).
            w.SaveState();
            return w.m_ProficiencyEffects.Count;
        }

        /// <summary>
        /// Add a new playable CLASS (clones an existing class's FTK_playerGameStart row).
        /// Classes are registered with id == their array index (the next sequential enum value),
        /// because the character-select UI uses the class id as BOTH an enum key and an array index;
        /// any other id would be unreachable by the cycle and crash the index-based reads.
        /// </summary>
        public static FTK_playerGameStart AddClass(
            string modGuid, string id, FTK_playerGameStart.ID template, string displayName,
            Action<FTK_playerGameStart> configure = null)
        {
            FTK_playerGameStartDB db = Db<FTK_playerGameStartDB>();
            FTK_playerGameStart tmpl = db.GetEntry(template);
            int index = ((Array)Reflect.GetField(db, "m_Array")).Length; // slot this class will occupy

            FTK_playerGameStart row = (FTK_playerGameStart)ContentRegistry.Register(db, modGuid, id, tmpl,
                o => { if (configure != null) configure((FTK_playerGameStart)o); }, index);
            Localization.SetName(id, displayName);
            return row;
        }

        /// <summary>
        /// Declare a class-innate PASSIVE trait: bind a named trait to an existing class row, to fire at a
        /// closed <see cref="PassiveTrigger"/> moment (spec #78 Phase 1). Unlike the other Add* helpers this
        /// writes NO FTK_*DB row and mints NO <see cref="IdAllocator"/> id: a passive is behaviour bound to a
        /// class, not a new content row. Phase 1 is DORMANT: this records the trait in the internal
        /// <see cref="PassiveRegistry"/> and registers its display name through <see cref="Localization"/>, but
        /// NO combat patch reads it yet (the trigger patches land in a later work item).
        ///
        /// The trait's identity is the string Key (modGuid + ":" + passiveId), which is what appears in logs
        /// (and, once live, any co-op sync): stable across sessions and machines. Re-registering the same
        /// (modGuid, passiveId) is IDEMPOTENT: it returns the existing def and logs a warning (first-wins, so a
        /// differing second call never overwrites the trait or its display name). A null or unregistered class
        /// row is rejected: logged error, no registry mutation, returns null.
        /// </summary>
        /// <param name="modGuid">Your plugin GUID (namespaces the trait so two mods never clash).</param>
        /// <param name="passiveId">A unique-per-mod trait id, e.g. "myclass_ironhide".</param>
        /// <param name="classRow">The owning class (must be a row already registered in the class DB).</param>
        /// <param name="trigger">When the trait fires (a closed set).</param>
        /// <param name="displayName">The name shown for the trait (resolved via the Localization path).</param>
        /// <returns>The registered (or pre-existing) trait, or null if the class row is null/unregistered.</returns>
        public static PassiveTraitDef AddPassive(
            string modGuid, string passiveId, FTK_playerGameStart classRow,
            PassiveTrigger trigger, string displayName)
        {
            string key = modGuid + ":" + passiveId;

            if (classRow == null)
            {
                Plugin.Log.LogError("AddPassive: classRow is null; '" + key + "' not registered.");
                return null;
            }

            // Resolve the owning class's int id through the DB. GetIntFromID returns -1 for an id that is not a
            // registered class (decompile-confirmed: it Enum.Parses and catches to -1; the DbLookupPatcher
            // prefix resolves our custom class string ids to their synthetic int). GetEntryByInt then confirms
            // a real row actually sits at that id, so a bare/unregistered row is rejected here.
            FTK_playerGameStartDB db = Db<FTK_playerGameStartDB>();
            int classId = db.GetIntFromID(classRow.m_ID);
            if (classId < 0 || db.GetEntryByInt(classId) == null)
            {
                Plugin.Log.LogError("AddPassive: class row '" + (classRow.m_ID ?? "(null id)") +
                    "' is not registered in FTK_playerGameStartDB; '" + key + "' not registered.");
                return null;
            }

            PassiveTraitDef candidate = new PassiveTraitDef(key, classId, trigger, displayName);
            PassiveTraitDef registered = PassiveRegistry.Register(candidate);

            // Bind the display name only on a FRESH registration (candidate is what Register returns on insert;
            // on an idempotent re-register it returns the PRE-EXISTING def, so we must not overwrite its name).
            if (registered == candidate)
                Localization.SetName(key, displayName);
            return registered;
        }

        /// <summary>
        /// Add a new ENEMY (clones an existing enemy's FTK_enemyCombat row). Uses a high-band synthetic id
        /// like items/weapons/proficiencies — NOT id == array index like classes; nothing indexes enemies
        /// by array position (every lookup is dictionary- or string-based, and selection round-trips the id
        /// through its decimal string over Photon).
        ///
        /// Cloning a template inherits its 3D body (m_EnemyAsset), weapon (m_WeaponAsset) and a sane field
        /// layout, so the custom enemy renders and fights immediately. Pick a NON-boss, NON-scourge template
        /// that is NOT a level-scaling enemy (i.e. not present in FTK_enemyScaleDB) and has a valid
        /// m_EnemyAsset — otherwise the spawn-pool builder silently filters it out.
        ///
        /// After registering we flag the level-bucketed spawn cache (GameCache.Enemies) for rebuild, so the
        /// new enemy becomes eligible for ordinary overworld/dungeon encounters (the cache re-reads the live
        /// DB on its next draw). To actually be drawn it must also pass the picker's gates: a nonzero
        /// m_Rarity draw-chance, matching m_SpawnDay/Night/Land/Water/Dungeon, and realm include/exclude —
        /// all inherited from a template that already spawns naturally (override in <paramref name="configure"/>).
        /// </summary>
        public static FTK_enemyCombat AddEnemy(
            string modGuid, string id, FTK_enemyCombat.ID template, string displayName,
            Action<FTK_enemyCombat> configure = null)
        {
            FTK_enemyCombatDB db = Db<FTK_enemyCombatDB>();
            FTK_enemyCombat tmpl = db.GetEntry(template);
            FTK_enemyCombat row = (FTK_enemyCombat)ContentRegistry.Register(db, modGuid, id, tmpl,
                o =>
                {
                    FTK_enemyCombat e = (FTK_enemyCombat)o;
                    // Register's CopyFields shallow-copies the template, so the clone's m_ItemDrops is the
                    // SAME instance as the template's. Deep-copy it into a fresh ItemDrops here (BEFORE the
                    // caller's configure runs) so authored content can safely mutate e.m_ItemDrops._golddrop
                    // etc. without rewriting the vanilla template's loot table.
                    if (e.m_ItemDrops != null)
                    {
                        FTK_enemyCombat.ItemDrops fresh = new FTK_enemyCombat.ItemDrops();
                        Reflect.CopyFields(e.m_ItemDrops, fresh);
                        e.m_ItemDrops = fresh;
                    }
                    if (configure != null) configure(e);
                });
            Localization.SetName(id, displayName);

            // Make the new row visible to the level-bucketed spawn pool. GameCache.Enemies rebuilds straight
            // from the live FTK_enemyCombatDB.m_Array whenever NeedsRebuild is set, so our row flows in with
            // no static-list surgery. (The game also re-flags this at every game-start, but set it now too.)
            GameCache.Cache.Enemies.NeedsRebuild = true;
            return row;
        }

        /// <summary>
        /// Give an ENEMY one or more proficiencies (combat actions) it didn't have, without mutating the
        /// shared template weapon. An enemy's attacks are the keys of its <c>m_WeaponAsset</c>'s
        /// <c>Weapon.m_ProficiencyEffects</c>. Unlike a player weapon (a <c>GameObject m_Prefab</c>),
        /// <c>m_WeaponAsset</c> is a <c>Weapon</c> COMPONENT, so we Instantiate a private copy of it, add our
        /// proficiencies, push them into the FullInspector serialized backing (<c>SaveState</c>) so they
        /// survive the game's re-Instantiate of the weapon, and repoint the enemy at the copy.
        ///
        /// We also strip any <c>AttackSchedule</c> from the copy so the enemy uses the RNG attack path
        /// (gated by <c>m_ChanceToProf</c>, picking uniformly across all of the weapon's proficiencies) —
        /// a scheduled weapon would only ever fire its fixed script and never our added action.
        /// </summary>
        public static bool AttachEnemyProficiencies(FTK_enemyCombat enemy, params string[] proficiencyIds)
        {
            if (enemy == null) return false;
            Weapon src = enemy.m_WeaponAsset;
            if (src == null)
            {
                Plugin.Log.LogWarning("AttachEnemyProficiencies: enemy '" + enemy.m_ID + "' has no m_WeaponAsset.");
                return false;
            }

            // Instantiating a Component clones its whole GameObject and returns the matching component.
            Weapon copy = UnityEngine.Object.Instantiate(src);
            UnityEngine.Object.DontDestroyOnLoad(copy.gameObject);
            copy.gameObject.name = src.gameObject.name + "_ftkmf";
            // Keep it active but parked off-screen (mirrors the proven weapon path; enemy weapons are read
            // with includeInactive:true, but this avoids relying on that).
            copy.transform.position = new Vector3(0f, -100000f, 0f);

            // Force the RNG attack path so our added action can actually be chosen.
            AttackSchedule schedule = copy.GetComponent<AttackSchedule>();
            if (schedule != null) UnityEngine.Object.Destroy(schedule);

            int count = AddProfsToWeapon(copy, proficiencyIds);

            enemy.m_WeaponAsset = copy;
            Plugin.Log.LogInfo("AttachEnemyProficiencies: added " + proficiencyIds.Length + " to '" + enemy.m_ID +
                "' (now " + count + " actions).");
            return true;
        }

        /// <summary>
        /// Give a registered ENEMY a custom VISUAL identity: a body <paramref name="tint"/> and a uniform body
        /// <paramref name="scale"/>, applied PER COMBAT to the enemy's freshly-instantiated body clone (a
        /// CharacterEventListener) in a postfix on <c>EnemyDummy.InitEnemyDummyForCombat</c>.
        ///
        /// VISUAL-ONLY and LEAK-SAFE: edits the per-combat clone (m_EventListener), never the shared prefab, so
        /// nothing (incl. vanilla enemies) is affected. DETERMINISM- and SAVE-SAFE: enemy visuals never network
        /// or persist (model/material/body localScale never cross Photon and are never serialized); only the
        /// enum-int identity and the DB field m_MarkerScale are shared state, both read identically from the same
        /// mod DB on every machine. Raise <c>m_MarkerScale</c> at registration (via the AddEnemy configure path)
        /// to match an up-scaled body's target footprint.
        /// </summary>
        /// <param name="enemy">The registered enemy row whose spawned body to recolor/rescale.</param>
        /// <param name="tint">The body tint (applied to each material's "_Color", or its .color if absent).</param>
        /// <param name="scale">Uniform body scale (1 = unchanged; &gt;1 = larger/hulking).</param>
        public static void SetEnemyVisual(FTK_enemyCombat enemy, Color tint, float scale)
        {
            if (enemy == null)
            {
                Plugin.Log.LogWarning("SetEnemyVisual: enemy is null; no visual registered.");
                return;
            }

            EnemyVisualPatch.Register(enemy.m_ID, tint, scale);
            Plugin.Log.LogInfo("SetEnemyVisual: '" + enemy.m_ID + "' tint=" + tint + " scale=" + scale + ".");
        }

        /// <summary>
        /// Give a registered ENEMY a FULL custom visual identity in one call: a non-uniform body tint/scale, an
        /// optional wet-bog skin (high smoothness / low metallic on Standard materials), a best-effort spine hunch,
        /// an optional procedural glowing lantern attached to a hand bone, and an optional axe-hide. All of it is
        /// applied PER COMBAT to the enemy's freshly-instantiated body clone (a CharacterEventListener) in a postfix
        /// on <c>EnemyDummy.InitEnemyDummyForCombat</c>.
        ///
        /// VISUAL-ONLY and LEAK-SAFE / DETERMINISM- and SAVE-SAFE: see the single-arg overload. Every added object
        /// (lantern, light) lives on the per-combat clone, never networks, and is never serialized; emission is put
        /// only on OUR objects (the game resets _EmissionColor to black on CEL-managed body materials).
        ///
        /// The aesthetic values live as tunable constants in the caller (RealmBossAdventure.cs); this overload just
        /// carries the populated <see cref="EnemyVisualPatch.EnemyVisual"/> into the Core visual registry.
        /// </summary>
        /// <param name="enemy">The registered enemy row whose spawned body to dress.</param>
        /// <param name="visual">The fully-populated visual override.</param>
        /// <remarks>Internal (not the public single-arg overload): the rich options struct is Core engine plumbing,
        /// consumed in-assembly by the bundled content (RealmBossAdventure). External modders use the public
        /// tint+scale overload above; this richer one stays internal so the struct can stay an engine type.</remarks>
        internal static void SetEnemyVisual(FTK_enemyCombat enemy, EnemyVisualPatch.EnemyVisual visual)
        {
            if (enemy == null)
            {
                Plugin.Log.LogWarning("SetEnemyVisual: enemy is null; no visual registered.");
                return;
            }

            EnemyVisualPatch.Register(enemy.m_ID, visual);
            Plugin.Log.LogInfo("SetEnemyVisual: '" + enemy.m_ID + "' tint=" + visual.tint + " scale=" + visual.scale +
                " widthBoost=" + visual.widthBoost + " wetSkin=" + visual.applyWetSkin + " hunch=" +
                visual.hunchDegrees + " lantern=" + visual.addLantern + " lanternLightColor=" +
                visual.lanternLightColor + " hideWeapon=" + visual.hideWeapon + " swampAura=" + visual.swampAura +
                " proceduralBody=" + visual.proceduralBody + " golemTorsoRadius=" + visual.golemTorsoRadius +
                " golemLimbRadius=" + visual.golemLimbRadius + " golemEyeGlow=" + visual.golemEyeGlow + ".");
        }

        /// <summary>
        /// MESH-SWAP custom model (RECOMMENDED): give a registered ENEMY an artist-authored body MESH from a shipped
        /// AssetBundle, REUSING the enemy's existing skeleton + bindposes + animations. On each combat spawn the
        /// enemy's freshly-instantiated body clone (a CharacterEventListener) has its body SkinnedMeshRenderer's
        /// <c>sharedMesh</c> set to the bundle-loaded <see cref="Mesh"/> (and, if <paramref name="textureName"/> is
        /// given, the body material's <c>_MainTex</c> set to a bundle-loaded Texture2D). Applied via the same per-clone
        /// postfix on <c>EnemyDummy.InitEnemyDummyForCombat</c> that drives the procedural visuals.
        ///
        /// The custom mesh MUST be skinned to the SAME skeleton you are reskinning (identical bone names / hierarchy /
        /// bindposes, e.g. trollCaveA's Root_M/Chest_M/... rig) so the vanilla animations deform it correctly. Ship the
        /// bundle at <c>FTKModFramework_content/models/&lt;bundleFileName&gt;</c>.
        ///
        /// VISUAL-ONLY and DETERMINISM- / SAVE-SAFE: the mesh/texture never network or persist; the only shared state
        /// is the enemy enum-int identity (deterministic via IdAllocator). As long as the bundle ships INSIDE the mod
        /// (byte-identical on every co-op client, no per-machine paths, no streaming), game state stays byte-for-byte
        /// identical in co-op. If the bundle or mesh fails to load it is LOGGED and the original mesh (or the
        /// procedural golem, if also registered) is left intact. Returns true if the request was registered.
        /// </summary>
        /// <param name="enemy">The registered enemy row whose spawned body mesh to swap.</param>
        /// <param name="bundleFileName">Bundle file under FTKModFramework_content/models/ (e.g. "mybeast.unity3d").</param>
        /// <param name="meshName">The Mesh asset name inside the bundle.</param>
        /// <param name="textureName">Optional Texture2D asset name to push into the body material's _MainTex.</param>
        public static bool SetEnemyBodyMesh(FTK_enemyCombat enemy, string bundleFileName, string meshName,
            string textureName = null)
        {
            if (enemy == null)
            {
                Plugin.Log.LogWarning("SetEnemyBodyMesh: enemy is null; no mesh registered.");
                return false;
            }
            if (string.IsNullOrEmpty(bundleFileName) || string.IsNullOrEmpty(meshName))
            {
                Plugin.Log.LogWarning("SetEnemyBodyMesh: bundleFileName and meshName are required; '" + enemy.m_ID +
                    "' unchanged.");
                return false;
            }

            EnemyVisualPatch.RegisterMeshSwap(enemy.m_ID, bundleFileName, meshName, textureName);
            Plugin.Log.LogInfo("SetEnemyBodyMesh: '" + enemy.m_ID + "' bundle='" + bundleFileName + "' mesh='" +
                meshName + "'" + (string.IsNullOrEmpty(textureName) ? "" : " texture='" + textureName + "'") + ".");
            return true;
        }

        /// <summary>
        /// RUNTIME glTF (.glb) custom model (EDITOR-FREE): give a registered ENEMY an artist-authored body MESH from a
        /// shipped <c>.glb</c>, REUSING the enemy's existing skeleton + bindposes + animations, with NO Unity editor
        /// and NO AssetBundle build step. On each combat spawn the enemy's freshly-instantiated body clone (a
        /// CharacterEventListener) has its body SkinnedMeshRenderer's <c>sharedMesh</c> set to a Mesh built at runtime
        /// from the <c>.glb</c> (and, if <paramref name="textureFileName"/> is given, the body material's
        /// <c>_MainTex</c> set to a <c>Texture2D</c> loaded from a shipped <c>.png</c>). Applied via the same per-clone
        /// postfix on <c>EnemyDummy.InitEnemyDummyForCombat</c> that drives the bundle path and the procedural visuals.
        ///
        /// The <c>.glb</c> must be KEYED TO THE VANILLA SKELETON BY BONE NAME: its per-vertex joints index bone names
        /// that are remapped onto the live <c>smr.bones[]</c>, and the LIVE bindposes are reused (the glb's own
        /// inverseBindMatrices are ignored), so the vanilla animations deform it correctly. Ship the <c>.glb</c> (and
        /// optional <c>.png</c>) at <c>FTKModFramework_content/models/</c>.
        ///
        /// VISUAL-ONLY and DETERMINISM- / SAVE-SAFE: the mesh/texture never network or persist; the only shared state
        /// is the enemy enum-int identity (deterministic via IdAllocator). As long as the <c>.glb</c>+<c>.png</c> ship
        /// INSIDE the mod (byte-identical on every co-op client, no per-machine paths, no streaming), game state stays
        /// byte-for-byte identical in co-op. If the glb fails to load it is LOGGED and the original mesh (or a
        /// registered AssetBundle mesh, or the procedural golem) is left intact. Returns true if the request was
        /// registered.
        /// </summary>
        /// <param name="enemy">The registered enemy row whose spawned body mesh to swap.</param>
        /// <param name="glbFileName">.glb file under FTKModFramework_content/models/ (e.g. "mybeast.glb").</param>
        /// <param name="textureFileName">Optional .png file under the same folder for the body material's _MainTex.</param>
        public static bool SetEnemyBodyMeshFromGlb(FTK_enemyCombat enemy, string glbFileName,
            string textureFileName = null)
        {
            if (enemy == null)
            {
                Plugin.Log.LogWarning("SetEnemyBodyMeshFromGlb: enemy is null; no mesh registered.");
                return false;
            }
            if (string.IsNullOrEmpty(glbFileName))
            {
                Plugin.Log.LogWarning("SetEnemyBodyMeshFromGlb: glbFileName is required; '" + enemy.m_ID +
                    "' unchanged.");
                return false;
            }

            EnemyVisualPatch.RegisterGlbMeshSwap(enemy.m_ID, glbFileName, textureFileName);
            Plugin.Log.LogInfo("SetEnemyBodyMeshFromGlb: '" + enemy.m_ID + "' glb='" + glbFileName + "'" +
                (string.IsNullOrEmpty(textureFileName) ? "" : " texture='" + textureFileName + "'") + ".");
            return true;
        }

        /// <summary>
        /// FULL-PREFAB custom model (for a fully BESPOKE rig): give a registered ENEMY a whole artist-authored body
        /// prefab from a shipped AssetBundle, repointing <c>FTK_enemyCombat.m_EnemyAsset</c> at the prefab's
        /// <see cref="CharacterEventListener"/>. The prefab is loaded, Instantiated once (kept persistent via
        /// <c>DontDestroyOnLoad</c> and parked far off-screen), and verified to carry a CharacterEventListener (the
        /// spawn path Instantiates m_EnemyAsset and requires one). The game then clones THIS template per combat, so
        /// the bespoke body shows for the enemy with no further per-spawn work.
        ///
        /// The prefab MUST carry: a <c>CharacterEventListener</c> (root), a <c>SkinnedMeshRenderer</c> (body), an
        /// <c>Animator</c> (so the game's animation events drive it), and <c>WEAPON_HOLDER_L</c>/<c>WEAPON_HOLDER_R</c>
        /// bones (so the held weapon mounts). Build it in Unity 2017.2.2p2 and ship the bundle at
        /// <c>FTKModFramework_content/models/&lt;bundleFileName&gt;</c>.
        ///
        /// VISUAL-ONLY and DETERMINISM- / SAVE-SAFE: the body asset never networks or persists; only the enemy
        /// enum-int identity (deterministic via IdAllocator) is shared state. As long as the bundle ships INSIDE the
        /// mod (byte-identical on every client, no per-machine paths, no streaming) co-op state stays identical. On any
        /// failure (bundle/prefab missing, or no CharacterEventListener on the prefab) it is LOGGED and the enemy's
        /// original m_EnemyAsset is left intact. Returns true on success.
        /// </summary>
        /// <param name="enemy">The registered enemy row whose body prefab to replace.</param>
        /// <param name="bundleFileName">Bundle file under FTKModFramework_content/models/ (e.g. "mybeast.unity3d").</param>
        /// <param name="prefabName">The GameObject prefab asset name inside the bundle.</param>
        public static bool SetEnemyBodyFromBundle(FTK_enemyCombat enemy, string bundleFileName, string prefabName)
        {
            if (enemy == null)
            {
                Plugin.Log.LogWarning("SetEnemyBodyFromBundle: enemy is null; no prefab registered.");
                return false;
            }
            if (string.IsNullOrEmpty(bundleFileName) || string.IsNullOrEmpty(prefabName))
            {
                Plugin.Log.LogWarning("SetEnemyBodyFromBundle: bundleFileName and prefabName are required; '" +
                    enemy.m_ID + "' unchanged.");
                return false;
            }

            GameObject prefab = CustomModelLoader.LoadPrefab(bundleFileName, prefabName);
            if (prefab == null)
            {
                // LoadPrefab already logged the reason. Leave the original m_EnemyAsset intact.
                Plugin.Log.LogWarning("SetEnemyBodyFromBundle: prefab '" + prefabName + "' from bundle '" +
                    bundleFileName + "' not loaded; '" + enemy.m_ID + "' unchanged.");
                return false;
            }

            // Instantiate a private, persistent template (parked off-screen, mirroring AttachProficiencies). The game
            // Instantiates m_EnemyAsset per combat, so this template is the cloned source, never shown directly.
            GameObject copy = UnityEngine.Object.Instantiate(prefab);
            UnityEngine.Object.DontDestroyOnLoad(copy);
            copy.name = prefab.name + "_ftkmf";
            copy.transform.position = new Vector3(0f, -100000f, 0f);

            CharacterEventListener cel = copy.GetComponentInChildren<CharacterEventListener>(true);
            if (cel == null)
            {
                Plugin.Log.LogError("SetEnemyBodyFromBundle: prefab '" + prefabName + "' has NO CharacterEventListener; " +
                    "the spawn path requires one. '" + enemy.m_ID + "' unchanged. (Add a CharacterEventListener + " +
                    "SkinnedMeshRenderer + Animator + WEAPON_HOLDER bones to the prefab.)");
                UnityEngine.Object.Destroy(copy);
                return false;
            }

            enemy.m_EnemyAsset = cel;
            Plugin.Log.LogInfo("SetEnemyBodyFromBundle: '" + enemy.m_ID + "' body prefab set from bundle '" +
                bundleFileName + "' prefab '" + prefabName + "'.");
            return true;
        }

        /// <summary>
        /// Add a new overworld ENCOUNTER / event (clones an existing FTK_miniEncounter row).
        ///
        /// Encounters are the events placed on overworld hexes during a run. The game's selector
        /// (<c>GameLogic.GetMiniEncounter</c>) walks the WHOLE FTK_miniEncounterDB by index, keeps every
        /// row that passes its realm gate (<c>m_RealmInclude</c>/<c>m_RealmExclude</c>; an EMPTY include
        /// list means "every realm"), tier gate, and condition gate, then rolls a weighted draw using
        /// each row's <c>m_Rarity</c> -&gt; FTK_encounterDrawChanceDB probability. So a freshly registered
        /// row is automatically a candidate — no generator/selection patch is needed; you only need the
        /// id to round-trip (handled here via ContentRegistry + the GetEnum/GetIntFromID patches).
        ///
        /// Display text: <c>FTK_miniEncounter.GetDisplay*</c> calls <c>FTKHub.Localized&lt;TextMiniEncounters&gt;</c>,
        /// which returns the key string itself when there's no text row — so we set <c>m_DisplayName</c>
        /// to your literal <paramref name="displayName"/> and it shows verbatim (no localization table edit).
        /// </summary>
        /// <param name="template">An existing encounter to clone (e.g. FTK_miniEncounter.ID.TreasureChest).</param>
        public static FTK_miniEncounter AddEncounter(
            string modGuid, string id, FTK_miniEncounter.ID template, string displayName,
            Action<FTK_miniEncounter> configure = null)
        {
            FTK_miniEncounterDB db = Db<FTK_miniEncounterDB>();
            FTK_miniEncounter tmpl = db.GetEntry(template);
            FTK_miniEncounter row = (FTK_miniEncounter)ContentRegistry.Register(db, modGuid, id, tmpl,
                o =>
                {
                    FTK_miniEncounter e = (FTK_miniEncounter)o;
                    // Literal display name (GetDisplayName returns it verbatim via Localized<>'s key-passthrough).
                    e.m_DisplayName = displayName;
                    if (configure != null) configure(e);
                });
            return row;
        }
    }

    /// <summary>
    /// Routing fix: FTK_itembase.GetItemBase sends every id >= 100000 to the weapon DB
    /// (IsItemID == id &lt; bladeShortsword). Our synthetic ids are all far above that, so custom
    /// *weapons* resolve fine, but custom *items* (which live in FTK_itemsDB) would be looked up in
    /// the wrong table and come back null. We backfill by checking both DBs for our ids.
    /// </summary>
    [HarmonyPatch(typeof(FTK_itembase), "GetItemBase")]
    internal static class GetItemBase_Patch
    {
        private static void Postfix(FTK_itembase.ID _id, ref FTK_itembase __result)
        {
            if (__result != null) return;
            if (!IdAllocator.IsCustom((int)_id)) return;

            FTK_itembase row = TableManager.Instance.Get<FTK_itemsDB>().GetEntryByInt((int)_id);
            if (row == null) row = TableManager.Instance.Get<FTK_weaponStats2DB>().GetEntryByInt((int)_id);
            __result = row;
        }
    }
}
