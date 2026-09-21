using System;
using System.IO;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx;
using HarmonyLib;
using GridEditor;
using Newtonsoft.Json.Linq;
using FTKModFramework.Core;
using UnityEngine;

[BepInPlugin(Id, "FTK Model Test Content", "0.1.0")]
[BepInDependency("com.ftkmf.framework")]
public sealed partial class RuntimeModelTestContent : BaseUnityPlugin
{
    public const string Id = "com.ftkmf.model-test-content";
    static RuntimeModelTestContent instance;
    string root;
    JArray profiles;
    bool attempted;
    bool packageOnly;
    string reportPath;
    readonly string runId = Guid.NewGuid().ToString("N");

    void Awake()
    {
        if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST") != "1") return;
        try
        {
            root = Path.GetFullPath(Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar);
            string requested = Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT");
            if (string.IsNullOrEmpty(requested) || Path.GetFullPath(requested).TrimEnd(Path.DirectorySeparatorChar) != root
                || new DirectoryInfo(root).Parent.Name != "scratch")
                throw new InvalidOperationException("Exact isolated game root directly under scratch required.");
            NoLinks(root);
            string candidateReport = Path.Combine(root, "model-test-registration.json");
            if (File.Exists(candidateReport)) NoLinks(candidateReport);
            reportPath = candidateReport;
            Report("initializing", new JArray(), null);
            string input = Path.Combine(root, "model-test-profiles.json");
            NoLinks(input);
            if (new FileInfo(input).Length > 1024 * 1024) throw new InvalidOperationException("Profile file exceeds 1 MiB.");
            JObject doc = JObject.Parse(File.ReadAllText(input));
            Keys(doc, "version", "profiles");
            if (doc["version"] == null || doc["version"].Type != JTokenType.Integer || (int)doc["version"] != 1)
                throw new InvalidOperationException("Profile version must be integer 1.");
            profiles = doc["profiles"] as JArray;
            packageOnly = Environment.GetEnvironmentVariable("FTK_MODEL_TEST_PACKAGE_ONLY") == "1";
            if (profiles == null || profiles.Count > 512 || (packageOnly ? profiles.Count != 0 : profiles.Count < 1))
                throw new InvalidOperationException(packageOnly ? "Package-only mode requires an explicitly empty profile list." : "Expected 1..512 profiles.");
            if (packageOnly && File.Exists(Path.Combine(root, "model-test-player-profiles.json")))
                throw new InvalidOperationException("Package-only mode refuses a player fixture catalog.");
            ValidateProfiles();
            LoadPlayerProfiles();
            instance = this;
            new Harmony(Id).PatchAll(typeof(RuntimeModelTestContent).Assembly);
            Logger.LogInfo("MODEL CONTENT READY: " + profiles.Count + " profiles; waiting for table initialization.");
        }
        catch (Exception e)
        {
            Logger.LogError("MODEL CONTENT REFUSED: " + e.Message);
            if (reportPath != null) Report("refused", new JArray(), e.Message);
        }
    }

    static void NoLinks(string path)
    {
        for (string p = Path.GetFullPath(path); !string.IsNullOrEmpty(p); p = Path.GetDirectoryName(p))
            if ((File.GetAttributes(p) & FileAttributes.ReparsePoint) != 0)
                throw new InvalidOperationException("Symlink/reparse path refused: " + p);
    }

    static void Keys(JObject obj, params string[] allowed)
    {
        if (obj == null) throw new InvalidOperationException("Expected JSON object.");
        foreach (JProperty p in obj.Properties())
            if (Array.IndexOf(allowed, p.Name) < 0) throw new InvalidOperationException("Unknown field: " + p.Name);
    }

    static string Text(JObject obj, string name)
    {
        JToken t = obj[name];
        if (t == null || t.Type != JTokenType.String || string.IsNullOrEmpty((string)t))
            throw new InvalidOperationException("Required nonempty string: " + name);
        return (string)t;
    }

    static bool IsStaticRenderer(JObject mesh)
    {
        JToken value = mesh == null ? null : mesh["rendererKind"];
        if (value == null) return false;
        if (value.Type != JTokenType.String)
            throw new InvalidOperationException("rendererKind must be a string when present.");
        string kind = (string)value;
        if (kind == "SkinnedMeshRenderer") return false;
        if (kind == "MeshRenderer") return true;
        throw new InvalidOperationException("rendererKind must be SkinnedMeshRenderer or MeshRenderer.");
    }

    string Asset(JObject mesh, string name, string extension, bool optional)
    {
        if (optional && (mesh[name] == null || mesh[name].Type == JTokenType.Null)) return null;
        string file = Text(mesh, name);
        if (!Regex.IsMatch(file, "^[A-Za-z0-9_-][A-Za-z0-9_.-]{0,119}" + Regex.Escape(extension) + "$"))
            throw new InvalidOperationException("Expected bounded asset basename: " + file);
        string path = Path.Combine(Path.Combine(root, "BepInEx/plugins/FTKModFramework_content/models"), file);
        NoLinks(path);
        long bytes = new FileInfo(path).Length;
        if (bytes < 1 || bytes > 64 * 1024 * 1024) throw new InvalidOperationException("Asset size must be 1..64 MiB: " + file);
        return file;
    }

    void ValidateProfiles()
    {
        HashSet<string> keys = new HashSet<string>(StringComparer.Ordinal);
        foreach (JObject p in profiles)
        {
            Keys(p, "key", "baseEnemy", "displayName", "combatProfile", "renderers", "resourcePrefab", "minimumBaseHealth", "visualScale", "portraitMarkerPath", "bindingKind", "tint", "fallOffPolicy");
            if (p["resourcePrefab"] != null)
            {
                string resource = Text(p, "resourcePrefab");
                if (!Regex.IsMatch(resource, "^[A-Za-z0-9_-]{1,120}$"))
                    throw new InvalidOperationException("Invalid exact resourcePrefab path: " + resource);
            }
            if (p["visualScale"] != null) VisualScaleFixture.Validate(p["visualScale"]);
            if (p["portraitMarkerPath"] != null) PortraitMarkerFixture.Validate(p["portraitMarkerPath"]);
            if (p["minimumBaseHealth"] != null)
            {
                JToken health = p["minimumBaseHealth"];
                if (health.Type != JTokenType.Integer || (long)health < 1 || (long)health > 1000)
                    throw new InvalidOperationException("minimumBaseHealth must be integer1..1000.");
            }
            string key = Text(p, "key");
            if (!Regex.IsMatch(key, "^ftkmf_modeltest_[a-z0-9_]{1,64}$") || !keys.Add(key))
                throw new InvalidOperationException("Invalid or duplicate stable key: " + key);
            string baseEnemy = Text(p, "baseEnemy");
            if (!Enum.IsDefined(typeof(FTK_enemyCombat.ID), baseEnemy)) throw new InvalidOperationException("Unknown native enemy: " + baseEnemy);
            if (Text(p, "displayName").Length > 120) throw new InvalidOperationException("Display name exceeds 120 characters.");
            string combination = Text(p, "combatProfile");
            if (!Regex.IsMatch(combination, "^[a-f0-9]{64}$"))
                throw new InvalidOperationException("Invalid rig/controller combatProfile: " + combination);
            JArray meshes = p["renderers"] as JArray;
            if (meshes == null || meshes.Count < 1 || meshes.Count > 16) throw new InvalidOperationException("Expected 1..16 renderer assignments.");
            bool legacy = LegacyBodyFixture.IsLegacy(p);
            LegacyBodyFixture.Validate(p);
            FallOffPolicyFixture.Validate(p, legacy);
            HashSet<string> paths = new HashSet<string>(StringComparer.Ordinal);
            foreach (JObject m in meshes)
            {
                Keys(m, "rendererPath", "glbFile", "textureFile", "disableNativeEmission", "materialSlots", "rendererKind");
                MaterialSlotFixture.Slot[] slots = MaterialSlotFixture.Read(m);
                if (IsStaticRenderer(m) && slots != null)
                    throw new InvalidOperationException("Static MeshRenderer assignment does not support materialSlots.");
                RendererEmissionFixture.Read(m["disableNativeEmission"]);
                string path = Text(m, "rendererPath");
                if (path.Length > 512 || path.StartsWith("/") || path.EndsWith("/") || path.IndexOf('\\') >= 0 || !paths.Add(path))
                    throw new InvalidOperationException("Invalid or repeated renderer path: " + path);
                if (path != ".") foreach (string part in path.Split('/'))
                    if (part == "" || part == "." || part == "..") throw new InvalidOperationException("Invalid renderer segment.");
                Asset(m, "glbFile", ".glb", false);
                if (slots == null) Asset(m, "textureFile", ".png", true);
                else foreach (JObject slot in (JArray)m["materialSlots"]) Asset(slot, "textureFile", ".png", true);
            }
        }
    }

    static CharacterEventListener ResourcePrefab(JObject profile)
    {
        if (profile["resourcePrefab"] == null) return null;
        string path = Text(profile, "resourcePrefab");
        GameObject prefab = Resources.Load<GameObject>(path);
        if (prefab == null || prefab.scene.IsValid() || prefab.transform.parent != null)
            throw new InvalidOperationException("Expected native resource prefab asset: " + path);
        CharacterEventListener cel = prefab.GetComponent<CharacterEventListener>();
        CharacterEventListener[] cels = prefab.GetComponentsInChildren<CharacterEventListener>(true);
        if (cel == null || cels.Length != 1 || cels[0] != cel || prefab.GetComponent<Animator>() == null)
            throw new InvalidOperationException("Resource requires one root CEL and root Animator: " + path);
        foreach (JObject mesh in (JArray)profile["renderers"])
        {
            string rendererPath = Text(mesh, "rendererPath");
            Transform target = prefab.transform;
            if (rendererPath != ".") foreach (string segment in rendererPath.Split('/'))
            {
                Transform match = null;
                int matches = 0;
                for (int i = 0; i < target.childCount; i++)
                {
                    Transform child = target.GetChild(i);
                    if (child.name == segment) { match = child; matches++; }
                }
                if (matches != 1)
                    throw new InvalidOperationException("Missing or ambiguous resource renderer segment: " + path + "/" + rendererPath);
                target = match;
            }
            if (IsStaticRenderer(mesh))
            {
                MeshRenderer[] renderers = target.GetComponents<MeshRenderer>();
                MeshFilter[] filters = target.GetComponents<MeshFilter>();
                if (renderers.Length != 1 || filters.Length != 1 || target.GetComponents<SkinnedMeshRenderer>().Length != 0 ||
                    filters[0].sharedMesh == null || renderers[0].sharedMaterials == null || renderers[0].sharedMaterials.Length != 1 ||
                    renderers[0].sharedMaterials[0] == null)
                    throw new InvalidOperationException("Missing or ambiguous resource static renderer/material/filter: " + path + "/" + rendererPath);
            }
            else
            {
                SkinnedMeshRenderer[] renderers = target.GetComponents<SkinnedMeshRenderer>();
                if (renderers.Length != 1 || renderers[0].sharedMesh == null || renderers[0].bones.Length == 0)
                    throw new InvalidOperationException("Missing or ambiguous resource skinned renderer: " + path + "/" + rendererPath);
            }
        }
        return cel;
    }

    [HarmonyPatch(typeof(TableManager), "Initialize")]
    static class AfterTables
    {
        [HarmonyAfter("com.ftkmf.framework")]
        [HarmonyPriority(Priority.Last)]
        static void Postfix()
        {
            if (instance == null) return;
            instance.RegisterProfiles();
            instance.RegisterPlayerProfiles();
        }
    }

    void RegisterProfiles()
    {
        if (attempted) return;
        attempted = true;
        JArray registered = new JArray();
        string error = null;
        try
        {
            FTK_enemyCombatDB db = Content.Db<FTK_enemyCombatDB>();
            Dictionary<string, CharacterEventListener> resourcePrefabs = new Dictionary<string, CharacterEventListener>(StringComparer.Ordinal);
            // Validate the entire table-dependent batch before the first clone. Never reuse an existing row.
            foreach (JObject p in profiles)
            {
                string key = Text(p, "key");
                if (db.GetEntryByStringID(key) != null) throw new InvalidOperationException("Enemy key already registered: " + key);
                FTK_enemyCombat.ID baseId = (FTK_enemyCombat.ID)Enum.Parse(typeof(FTK_enemyCombat.ID), Text(p, "baseEnemy"));
                if (db.GetEntry(baseId) == null) throw new InvalidOperationException("Missing template: " + baseId);
                CharacterEventListener resource = ResourcePrefab(p);
                if (p["visualScale"] != null && (resource == null ? db.GetEntry(baseId).m_EnemyAsset : resource) == null)
                    throw new InvalidOperationException("Scale fixture requires a native prefab: " + key);
                if (resource != null) resourcePrefabs.Add(key, resource);
            }
            foreach (JObject p in profiles)
            {
                string key = Text(p, "key");
                FTK_enemyCombat.ID baseId = (FTK_enemyCombat.ID)Enum.Parse(typeof(FTK_enemyCombat.ID), Text(p, "baseEnemy"));
                CharacterEventListener resource;
                resourcePrefabs.TryGetValue(key, out resource);
                FTK_enemyCombat template = db.GetEntry(baseId);
                FTK_enemyCombat row = Content.AddEnemy(Id, key, baseId, Text(p, "displayName"),
                    delegate(FTK_enemyCombat clone)
                    {
                        if (resource != null) clone.m_EnemyAsset = resource;
                        if (p["minimumBaseHealth"] != null)
                            clone.m_HealthTotal = Math.Max(clone.m_HealthTotal, (int)p["minimumBaseHealth"]);
                    });
                int expectedBaseHealth = p["minimumBaseHealth"] == null ? template.m_HealthTotal
                    : Math.Max(template.m_HealthTotal, (int)p["minimumBaseHealth"]);
                if (row != null && row.m_HealthTotal != expectedBaseHealth)
                    throw new InvalidOperationException("Custom base health fixture mismatch: " + key);
                if (row == null || row.m_WeaponAsset != template.m_WeaponAsset
                    || row.m_EnemyAsset != (resource == null ? template.m_EnemyAsset : resource))
                    throw new InvalidOperationException("Native prefab/controller association changed: " + key);
                // Register scale first: the mesh API merges renderer assignments into this visual.
                if (p["visualScale"] != null)
                    Content.SetEnemyVisual(row, Color.white, VisualScaleFixture.Validate(p["visualScale"]));
                bool legacy = LegacyBodyFixture.IsLegacy(p);
                bool preserveCustomBody = FallOffPolicyFixture.Validate(p, legacy);
                if (legacy)
                {
                    float[] tint = LegacyBodyFixture.Tint(p);
                    Content.SetEnemyVisual(row, new Color(tint[0], tint[1], tint[2], tint[3]),
                        p["visualScale"] == null ? 1f : VisualScaleFixture.Validate(p["visualScale"]));
                    JObject body = (JObject)((JArray)p["renderers"])[0];
                    // rendererPath is observation metadata only. The singular API selects its native target.
                    if (!Content.SetEnemyBodyMeshFromGlb(row, Text(body, "glbFile"), (string)body["textureFile"]))
                        throw new InvalidOperationException("Legacy singular visual registration failed: " + key);
                }
                else
                {
                List<EnemyRendererMesh> meshes = new List<EnemyRendererMesh>();
                foreach (JObject m in (JArray)p["renderers"])
                {
                    MaterialSlotFixture.Slot[] slots = MaterialSlotFixture.Read(m);
                    bool staticRenderer = IsStaticRenderer(m);
                    if (slots == null)
                    {
                        string texture = m["textureFile"] == null ? null : (string)m["textureFile"];
                        bool disableEmission = RendererEmissionFixture.Read(m["disableNativeEmission"]);
                        meshes.Add(staticRenderer
                            ? EnemyRendererMesh.ForStaticRenderer(Text(m, "rendererPath"), Text(m, "glbFile"), texture, disableEmission)
                            : new EnemyRendererMesh(Text(m, "rendererPath"), Text(m, "glbFile"), texture, disableEmission));
                    }
                    else
                    {
                        EnemyRendererMaterial[] options = new EnemyRendererMaterial[slots.Length];
                        for (int slot = 0; slot < slots.Length; slot++) options[slot] = new EnemyRendererMaterial(
                            slots[slot].PrimitiveIndex, slots[slot].NativeMaterialSlot, slots[slot].TextureFile, slots[slot].DisableNativeEmission);
                        meshes.Add(EnemyRendererMesh.WithNativeMaterialSlots(Text(m, "rendererPath"), Text(m, "glbFile"), options));
                    }
                }
                if (row == null || !Content.SetEnemyBodyMeshesFromGlb(row, meshes.ToArray()))
                    throw new InvalidOperationException("Visual registration failed: " + key);
                }
                if (preserveCustomBody && !Content.SetEnemyFallOffPolicy(row, EnemyFallOffPolicy.PreserveCustomBody))
                    throw new InvalidOperationException("Fall-off policy registration failed: " + key);
                if (p["portraitMarkerPath"] != null && !Content.SetEnemyPortraitMarker(row, PortraitMarkerFixture.Validate(p["portraitMarkerPath"])))
                    throw new InvalidOperationException("Portrait marker registration failed: " + key);
                int id = db.GetIntFromID(key);
                string displayName;
                if (!object.ReferenceEquals(db.GetEntryByStringID(key), row) || id < 0 || row.m_ID != key
                    || !object.ReferenceEquals(db.GetEntry((FTK_enemyCombat.ID)id), row)
                    || !Localization.TryGetName(key, out displayName) || displayName != Text(p, "displayName"))
                    throw new InvalidOperationException("Registry roundtrip failed: " + key);
                JObject entry = new JObject { { "key", key }, { "id", id }, { "baseEnemy", Text(p, "baseEnemy") },
                    { "combatProfile", Text(p, "combatProfile") }, { "status", "registered_spawn_validation_pending" } };
                entry.Add("bindingKind", legacy ? "legacy-singular" : "explicit-plural");
                if (preserveCustomBody) entry.Add("fallOffPolicy", FallOffPolicyFixture.PreserveCustomBody);
                if (legacy)
                {
                    entry.Add("registrationApis", new JArray("Content.SetEnemyVisual", "Content.SetEnemyBodyMeshFromGlb"));
                    entry.Add("rendererPathRole", "expected_observation_only_not_api_target");
                    entry.Add("tint", p["tint"].DeepClone());
                }
                if (p["portraitMarkerPath"] != null) entry.Add("portraitMarkerPath", PortraitMarkerFixture.Validate(p["portraitMarkerPath"]));
                if (resource != null)
                {
                    entry.Add("resourcePrefab", Text(p, "resourcePrefab"));
                    entry.Add("resourcePrefabInstanceId", resource.gameObject.GetInstanceID());
                    entry.Add("resourceCelInstanceId", resource.GetInstanceID());
                    entry.Add("resourcePrefabName", resource.gameObject.name);
                }
                if (p["minimumBaseHealth"] != null)
                {
                    entry.Add("minimumBaseHealth", (int)p["minimumBaseHealth"]);
                    entry.Add("nativeTemplateBaseHealth", template.m_HealthTotal);
                    entry.Add("actualBaseHealth", row.m_HealthTotal);
                    // GetHealthTotal requires a current hero/dungeon/difficulty context.
                    entry.Add("computedHealth", new JValue((object)null));
                    entry.Add("computedHealthStatus", "requires_native_spawn_context");
                }
                if (p["visualScale"] != null)
                {
                    Vector3 nativeScale = row.m_EnemyAsset.transform.localScale;
                    entry.Add("visualScale", VisualScaleFixture.Validate(p["visualScale"]));
                    entry.Add("nativePrefabRootLocalScale", new JArray(nativeScale.x, nativeScale.y, nativeScale.z));
                    entry.Add("visualScaleStatus", "public_scale_factor_registered_native_spawn_measurement_pending");
                }
                registered.Add(entry);
                Logger.LogInfo("SELF-TEST PASS [model-test-content]: " + key + " resolves to " + id + "; visual registered; live spawn pending.");
            }
        }
        catch (Exception e) { error = e.ToString(); Logger.LogError("SELF-TEST FAIL [model-test-content]: " + error); }
        // Registration is not transactional; report partial progress and never automatically retry failed batches.
        Report(error == null ? "registered" : "failed", registered, error);
    }

    void Report(string status, JArray registered, string error)
    {
        try { File.WriteAllText(reportPath, new JObject {
            { "version", 1 }, { "run", runId }, { "updatedUtc", DateTime.UtcNow.ToString("o") },
            { "status", status }, { "mode", packageOnly ? "package-only" : "model-profiles" }, { "requested", profiles == null ? 0 : profiles.Count },
            { "registered", registered }, { "error", error }
        }.ToString()); }
        catch (Exception e) { Logger.LogError("MODEL CONTENT report write failed: " + e.Message); }
    }
}
