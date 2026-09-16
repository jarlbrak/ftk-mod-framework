using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using HarmonyLib;
using GridEditor;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.Animations;
using UnityEngine.Playables;

// This is a route-specific acceptance observer for the rejected old Kraken
// resource prefab. It never drives a controller, invokes a combat method, or
// retains/release/destroys a framework lease. The only state it owns is bounded
// diagnostic metadata held by this test-only plugin.
public sealed partial class RuntimeModelTest
{
    const string KrakenProductionSchema = "ftkmf.kraken-production-adapter-observation.v1";
    const string KrakenProductionResource = "enkrakenhead";
    const string KrakenProductionRegisteredEnemy = "ftkmf_modeltest_gloamfin_kraken_legacy";
    const string KrakenProductionNativeEnemy = "krakenHead";
    const string KrakenProductionCombatProfile = "03a4df71df2226d0ec711f80c3f1f3e0922c211ff18d3efb797a6501efe423fa";
    const string KrakenProductionRendererPath = "krakenHead";
    const int KrakenProductionSourceRendererId = 121260;
    const string KrakenProductionController = "krakenHeadController";
    const string KrakenProductionExpectedMesh = "ftkmf_glb_gloamfin.glb";
    const int KrakenProductionFrameLimit = 8192;
    const int KrakenProductionCallbackLimit = 1024;
    const int KrakenProductionCleanupLimit = 64;
    const int KrakenProductionAttackLimit = 64;
    const int KrakenProductionIncomingAttackLimit = 128;
    const int KrakenProductionTerminalAuthorityLimit = 32;
    const int KrakenProductionTeardownFrameLimit = 600;
    const float KrakenProductionWeightTolerance = 0.00001f;
    const string KrakenProductionGameAssembly = "94cab5f9be9633f7f85f6f072e0b5b919c605bbecb3414422f8f008d9b2bc1c8";
    const string KrakenProductionResourcesAssets = "e33be4e6a3add9c15bc1d778f8b2162c8d9835f62b2764b75b30d49deb036117";
    const string KrakenProductionControllerJson = "b4a0141ad1d37dd6815fa1a3851e9942a87c31c1bebd360690d7521e1db5779b";
    const string KrakenProductionProfileHash = "bcf8ee395f39d99759a36a9f48dbe19810db7e3d42fa98e285593a3ec0748495";
    const string KrakenProductionCatalogHash = "bcc03747bfc293e47645b0548a7f6eb9d4f660084e422fb8e20aee6a33b14cee";

    static readonly Dictionary<string, string> KrakenProductionCampaignRuns = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        { "native-combat-death", "DEATH" },
        { "enemy-victory-terminal", "VICTORY" }
    };

    static readonly string[] KrakenProductionOldPaths =
    {
        "Root_M/joint1",
        "Root_M/joint1/neck",
        "Root_M/joint1/neck/head",
        "Root_M/joint1/neck/head/topHead",
        "Root_M/joint1/neck/jaw"
    };

    static readonly string[] KrakenProductionClipNames =
    {
        "krakenIdle", "krakenDamage", "krakenDisappear", "kraken_appear", "krakenAttack"
    };

    sealed class KrakenProductionStateContract
    {
        internal readonly string state, clip;
        internal readonly int hash;
        internal readonly bool loop;

        internal KrakenProductionStateContract(string stateName, string clipName, bool looped)
        {
            state = stateName;
            clip = clipName;
            hash = Animator.StringToHash("Base Layer." + stateName);
            loop = looped;
        }
    }

    static readonly KrakenProductionStateContract[] KrakenProductionStates =
    {
        new KrakenProductionStateContract("IDLE", "krakenIdle", true),
        new KrakenProductionStateContract("INTRO", "krakenIdle", true),
        new KrakenProductionStateContract("DEFEND", "krakenDamage", false),
        new KrakenProductionStateContract("ATTACK", "krakenAttack", false),
        new KrakenProductionStateContract("VICTORY", "krakenDisappear", false),
        new KrakenProductionStateContract("PASSIVE VICTORY", "krakenDisappear", false),
        new KrakenProductionStateContract("DAMAGEDHEAVY", "krakenDamage", false),
        new KrakenProductionStateContract("DEATH", "krakenDisappear", false),
        new KrakenProductionStateContract("ATTACKCRIT", "krakenAttack", false),
        new KrakenProductionStateContract("ATTACKPROF", "krakenAttack", false),
        new KrakenProductionStateContract("DODGE", "krakenDamage", false),
        new KrakenProductionStateContract("DAMAGED", "krakenDamage", false),
        new KrakenProductionStateContract("DAMAGED STUN", "krakenDamage", false),
        new KrakenProductionStateContract("OverworldAppear", "kraken_appear", false),
        new KrakenProductionStateContract("DEATHLIGHT", "krakenDisappear", false)
    };

    sealed class KrakenProductionWatchedResource
    {
        internal UnityEngine.Object value;
        internal int instanceId;
        internal string name, type;
    }

    sealed class KrakenProductionSamplerWatch
    {
        internal object sampler;
        internal GameObject root;
        internal PlayableGraph graph;
        internal int rootId;
        internal string label;
    }

    sealed class KrakenProductionAttackWindow
    {
        internal int id;
        internal string variant;
        internal FTKPlayerID primary;
        internal FTKPlayerID secondary;
        internal FTKPlayerID tertiary;
        internal bool hasPrimary, hasSecondary, hasTertiary;
        internal bool completed;
        internal JObject entry;
    }

    sealed class KrakenProductionIncomingAttack
    {
        internal int sequence;
        internal FTKPlayerID attacker;
        internal string cheatType, proficiency;
        internal bool consumable, bound, responded;
        internal JObject entry;
    }

    sealed class KrakenProductionAdapterArm
    {
        internal string session, repositoryRoot, campaignRun, expectedTerminal;
        internal int ownerId, targetId, rendererId, adapterId, meshOwnerId, meshLeaseId;
        internal int startedFrame, ownerDestroyedFrame = -1;
        internal int lastRecordedFrame = -1, lastCurrentHash, lastNextHash;
        internal bool hasRecordedFrame, lastTransition;
        internal EnemyDummy owner;
        internal CharacterEventListener target, resource;
        internal FTK_enemyCombat row;
        internal SkinnedMeshRenderer renderer;
        internal Animator animator;
        internal RuntimeAnimatorController controller;
        internal Component adapter, meshOwner;
        internal Type adapterType, samplerType, meshOwnerType;
        internal object modernSampler, oldSampler;
        internal KrakenProductionSamplerWatch modernWatch, oldWatch;
        internal Dictionary<string, AnimationClip> clips;
        internal JObject pins, lastPins;
        internal readonly List<KrakenProductionWatchedResource> resources = new List<KrakenProductionWatchedResource>();
        internal readonly List<KrakenProductionAttackWindow> attacks = new List<KrakenProductionAttackWindow>();
        internal readonly List<KrakenProductionIncomingAttack> incomingAttacks = new List<KrakenProductionIncomingAttack>();
        internal readonly Dictionary<DummyDamageInfo, KrakenProductionIncomingAttack> incomingDamage =
            new Dictionary<DummyDamageInfo, KrakenProductionIncomingAttack>();
        internal readonly JArray frames = new JArray();
        internal readonly JArray callbacks = new JArray();
        internal readonly JArray stateCallbacks = new JArray();
        internal readonly JArray incomingAttackEvents = new JArray();
        internal readonly JArray terminalAuthorityEvents = new JArray();
        internal readonly JArray cleanup = new JArray();
        internal string error;
        internal bool overflow, identityDrift, teardownStarted, teardownObserved, cleared;
        internal int disableCalls, adapterDisposeCalls, samplerDisposeCalls;
        internal int effectiveDisableCalls, effectiveAdapterDisposeCalls, effectiveSamplerDisposeCalls;
    }

    KrakenProductionAdapterArm krakenProductionAdapterArm;
    static RuntimeModelTest krakenProductionAdapterObserver;
    static bool krakenProductionAdapterHooks;

    static KrakenProductionStateContract KrakenProductionState(int hash)
    {
        for (int i = 0; i < KrakenProductionStates.Length; i++)
            if (KrakenProductionStates[i].hash == hash) return KrakenProductionStates[i];
        return null;
    }

    static bool KrakenProductionFinite(float value)
    {
        return !float.IsNaN(value) && !float.IsInfinity(value);
    }

    static int KrakenProductionRequiredId(JObject command, string key)
    {
        JToken value = command[key];
        if (value == null || value.Type != JTokenType.Integer || (long)value == 0 ||
            (long)value < Int32.MinValue || (long)value > Int32.MaxValue)
            throw new ArgumentException("Exact nonzero 32-bit Unity " + key + " required.");
        return (int)value;
    }

    static string KrakenProductionRepositoryRoot(string gameRoot)
    {
        string candidate = Path.GetFullPath(Path.Combine(Path.Combine(gameRoot, ".."), ".."));
        if (!Directory.Exists(candidate) || !Directory.Exists(Path.Combine(candidate, "FTKModFramework")) ||
            !Directory.Exists(Path.Combine(candidate, "docs")) || !Directory.Exists(Path.Combine(candidate, "scratch")))
            throw new InvalidOperationException("Runtime helper must run from the reviewed FTK scratch game root.");
        CatalogNoLinks(candidate);
        return candidate;
    }

    static JObject KrakenProductionFilePin(string path)
    {
        CatalogNoLinks(path);
        if (!File.Exists(path)) throw new InvalidOperationException("Pinned Kraken evidence file is absent: " + path);
        return new JObject {
            { "path", path }, { "sha256", CatalogHash(path) }, { "bytes", new FileInfo(path).Length }
        };
    }

    JObject KrakenProductionPins()
    {
        string repository = KrakenProductionRepositoryRoot(root);
        string assets = Path.Combine(root, "FTK.app/Contents/Resources/Data/resources.assets");
        if (!File.Exists(assets)) assets = Path.Combine(root, "FTK_Data/resources.assets");
        if (!File.Exists(assets)) throw new InvalidOperationException("Exact resources.assets is unavailable in isolated game root.");
        JObject contract = JObject.Parse(File.ReadAllText(Path.Combine(repository,
            "docs/evidence/kraken-production-adapter-design-v1/contract.json")));
        if ((string)contract["schema"] != "ftkmf.kraken-production-adapter-contract.v1" ||
            (string)contract["status"] != "implementation_complete_validation_pending" ||
            (string)contract["route"]["topologyGroup"] != "6a28ac3cf4523c24" ||
            (string)contract["route"]["routeKind"] != "resourcePrefab" ||
            (string)contract["route"]["resourcePrefab"] != KrakenProductionResource ||
            (string)contract["route"]["nativeEnemy"] != KrakenProductionNativeEnemy ||
            (string)contract["route"]["rendererPath"] != KrakenProductionRendererPath ||
            (int)contract["route"]["sourceRendererId"] != KrakenProductionSourceRendererId ||
            (bool)contract["route"]["genericProfileAllowed"])
            throw new InvalidOperationException("Pinned Kraken production route contract changed.");
        if (CatalogHash(assets) != KrakenProductionResourcesAssets)
            throw new InvalidOperationException("Pinned resources.assets identity changed.");
        JObject game = ScaleIdentity(typeof(EnemyDummy).Assembly);
        if ((string)game["assemblyFileSha256"] != KrakenProductionGameAssembly ||
            !Path.GetFullPath((string)game["location"]).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Pinned Assembly-CSharp identity changed.");
        JObject adapter = KrakenProductionFilePin(Path.Combine(repository,
            "FTKModFramework/Core/LegacyKrakenResourceAdapter.cs"));
        JObject stateReadme = KrakenProductionFilePin(Path.Combine(repository,
            "docs/evidence/kraken-production-state-coverage-v1/README.md"));
        JObject controllerJson = KrakenProductionFilePin(Path.Combine(repository,
            "scratch/kraken-controller-transitions.json"));
        JObject decompile = KrakenProductionFilePin(Path.Combine(repository, "scratch/CharacterEventListener.analysis.cs"));
        JObject adapterTest = KrakenProductionFilePin(Path.Combine(repository,
            "tools/ai-model-pipeline/test_kraken_production_adapter_implementation.py"));
        JObject pins = (JObject)contract["evidencePins"];
        if ((string)pins["dedicatedInternalAdapterSourceSha256"] != (string)adapter["sha256"] ||
            (string)pins["productionStateCoverageReadmeSha256"] != (string)stateReadme["sha256"] ||
            (string)pins["decompiledCharacterEventListenerSha256"] != (string)decompile["sha256"] ||
            (string)pins["dedicatedInternalAdapterTestSha256"] != (string)adapterTest["sha256"] ||
            (string)controllerJson["sha256"] != KrakenProductionControllerJson)
            throw new InvalidOperationException("Pinned Kraken source or controller evidence changed.");
        JObject helper = ScaleIdentity(typeof(RuntimeModelTest).Assembly);
        JObject content = ScaleIdentity(CatalogAssembly("FtkRuntimeModelTestContent"));
        if (!Path.GetFullPath((string)helper["location"]).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal) ||
            !Path.GetFullPath((string)content["location"]).StartsWith(root + Path.DirectorySeparatorChar, StringComparison.Ordinal))
            throw new InvalidOperationException("Runtime observer assemblies must load from the isolated game root.");
        JObject manifest = KrakenSkinPreflight("gloamfin-v1");
        string profilePath = Path.Combine(repository,
            "art-experiments/gloamfin-kraken/production-adapter-profile.json");
        JObject profilePin = KrakenProductionFilePin(profilePath);
        if ((string)profilePin["sha256"] != KrakenProductionProfileHash)
            throw new InvalidOperationException("Pinned Gloamfin production profile changed.");
        JObject profileDocument = JObject.Parse(File.ReadAllText(profilePath));
        JArray routeProfiles = profileDocument["profiles"] as JArray;
        if ((int)profileDocument["version"] != 1 || routeProfiles == null || routeProfiles.Count != 1)
            throw new InvalidOperationException("Exact one-row Gloamfin production profile required.");
        JObject routeProfile = routeProfiles[0] as JObject;
        if (routeProfile == null || (string)routeProfile["key"] != KrakenProductionRegisteredEnemy ||
            (string)routeProfile["baseEnemy"] != KrakenProductionNativeEnemy ||
            (string)routeProfile["resourcePrefab"] != KrakenProductionResource ||
            (string)routeProfile["combatProfile"] != KrakenProductionCombatProfile)
            throw new InvalidOperationException("Pinned Gloamfin production profile route changed.");
        string catalogPath = Path.Combine(root, "model-test-profiles.json");
        JObject catalogPin = KrakenProductionFilePin(catalogPath);
        if ((string)catalogPin["sha256"] != KrakenProductionCatalogHash)
            throw new InvalidOperationException("Pinned isolated model catalog changed.");
        JObject catalogDocument = JObject.Parse(File.ReadAllText(catalogPath));
        JArray catalogProfiles = catalogDocument["profiles"] as JArray;
        JObject catalogRoute = null;
        int catalogRouteCount = 0;
        if ((int)catalogDocument["version"] != 1 || catalogProfiles == null)
            throw new InvalidOperationException("Pinned isolated model catalog is malformed.");
        foreach (JObject candidate in catalogProfiles)
            if ((string)candidate["key"] == KrakenProductionRegisteredEnemy)
            {
                catalogRoute = candidate;
                catalogRouteCount++;
            }
        if (catalogRouteCount != 1 || !JToken.DeepEquals(catalogRoute, routeProfile))
            throw new InvalidOperationException("Exact Gloamfin production profile is not registered in the isolated catalog.");
        string registrationPath = Path.Combine(root, "model-test-registration.json");
        JObject registrationPin = KrakenProductionFilePin(registrationPath);
        JObject registration = JObject.Parse(File.ReadAllText(registrationPath));
        JArray registered = registration["registered"] as JArray;
        JObject registeredRoute = null;
        int registeredRouteCount = 0;
        if ((int)registration["version"] != 1 || (string)registration["status"] != "registered" ||
            registration["error"] == null || registration["error"].Type != JTokenType.Null ||
            registered == null || (int)registration["requested"] != catalogProfiles.Count ||
            registered.Count != catalogProfiles.Count)
            throw new InvalidOperationException("Current isolated content registration is incomplete.");
        foreach (JObject candidate in registered)
            if ((string)candidate["key"] == KrakenProductionRegisteredEnemy)
            {
                registeredRoute = candidate;
                registeredRouteCount++;
            }
        if (registeredRouteCount != 1 || (string)registeredRoute["baseEnemy"] != KrakenProductionNativeEnemy ||
            (string)registeredRoute["resourcePrefab"] != KrakenProductionResource ||
            (string)registeredRoute["combatProfile"] != KrakenProductionCombatProfile ||
            (string)registeredRoute["bindingKind"] != "explicit-plural" ||
            (string)registeredRoute["status"] != "registered_spawn_validation_pending" ||
            (int)registeredRoute["id"] <= 0)
            throw new InvalidOperationException("Exact Gloamfin production route registration is unavailable.");
        return new JObject {
            { "framework", PreviewCoreIdentity() },
            { "runtimeHelper", helper }, { "runtimeContent", content },
            { "game", game },
            { "resourcesAssets", KrakenProductionFilePin(assets) },
            { "contract", KrakenProductionFilePin(Path.Combine(repository,
                "docs/evidence/kraken-production-adapter-design-v1/contract.json")) },
            { "adapterSource", adapter }, { "stateCoverage", stateReadme },
            { "controllerExtraction", controllerJson }, { "decompile", decompile },
            { "adapterImplementationTest", adapterTest },
            { "routeProfile", profilePin }, { "modelCatalog", catalogPin },
            { "contentRegistration", registrationPin },
            { "registeredRoute", registeredRoute.DeepClone() },
            { "gloamfinManifest", new JObject {
                { "file", GloamfinSkinManifest }, { "sha256", GloamfinSkinManifestHash },
                { "glb", ((JObject)manifest["glb"]).DeepClone() },
                { "png", ((JObject)manifest["png"]).DeepClone() }
            } }
        };
    }

    void KrakenProductionCheckPins(KrakenProductionAdapterArm arm)
    {
        JObject now = KrakenProductionPins();
        if (!JToken.DeepEquals(arm.pins, now))
            throw new InvalidOperationException("Pinned source, asset, or helper identity changed during observation.");
        arm.lastPins = now;
    }

    static object KrakenProductionField(object source, string name)
    {
        if (ReferenceEquals(source, null)) throw new InvalidOperationException("Missing reflected source for " + name + ".");
        FieldInfo field = source.GetType().GetField(name, Members);
        if (field == null) throw new MissingFieldException(source.GetType().FullName, name);
        return field.GetValue(source);
    }

    static object KrakenProductionStaticField(Type type, string name)
    {
        FieldInfo field = type.GetField(name, Statics);
        if (field == null) throw new MissingFieldException(type.FullName, name);
        return field.GetValue(null);
    }

    static bool KrakenProductionBool(object source, string name)
    {
        object value = KrakenProductionField(source, name);
        if (!(value is bool)) throw new InvalidOperationException("Expected bool " + name + ".");
        return (bool)value;
    }

    static void KrakenProductionRequire(bool condition, string message)
    {
        if (!condition) throw new InvalidOperationException(message);
    }

    static bool KrakenProductionContains(IList list, object value)
    {
        if (list == null) return false;
        for (int i = 0; i < list.Count; i++)
            if (ReferenceEquals(list[i], value)) return true;
        return false;
    }

    static bool KrakenProductionFidEquals(FTKPlayerID left, FTKPlayerID right)
    {
        return left.m_PhotonID == right.m_PhotonID && left.m_TurnIndex == right.m_TurnIndex;
    }

    static JObject KrakenProductionFid(FTKPlayerID value)
    {
        return new JObject { { "photonId", value.m_PhotonID }, { "turnIndex", value.m_TurnIndex } };
    }

    static JObject KrakenProductionDamage(DummyDamageInfo info)
    {
        if (info == null) return null;
        return new JObject {
            { "attackerFid", KrakenProductionFid(info.m_AttackerID) },
            { "victimFid", KrakenProductionFid(info.m_VictimID) },
            { "damage", info.m_Damage }, { "newHealth", info.m_NewHealth },
            { "attackResponse", info.m_AttackResponse.ToString() },
            { "proficiency", info.m_Prof.ToString() }, { "proficiencySuccess", info.m_ProfSuccess }
        };
    }

    static string KrakenProductionVariant(CharacterDummy.AttackAnim attack, CharacterEventListener.CombatAnimTrigger overrideTrigger)
    {
        string value = overrideTrigger == CharacterEventListener.CombatAnimTrigger.None ? attack.ToString() : overrideTrigger.ToString();
        return value == "Attack" || value == "AttackCrit" || value == "AttackProf" ? value : null;
    }

    static int KrakenProductionClipIndex(string name)
    {
        return Array.IndexOf(KrakenProductionClipNames, name);
    }

    static Animator KrakenProductionAnimator(CharacterEventListener target)
    {
        Animator animator = target == null ? null : target.GetComponent<Animator>();
        if (animator == null || !ReferenceEquals(animator, target.m_Animator))
            throw new InvalidOperationException("Exact live CEL Animator is unavailable.");
        return animator;
    }

    static CharacterEventListener KrakenProductionResourcePrefab()
    {
        GameObject resource = Resources.Load(KrakenProductionResource, typeof(GameObject)) as GameObject;
        if (resource == null || resource.scene.IsValid() || resource.transform.parent != null)
            throw new InvalidOperationException("Exact enkrakenhead resource prefab unavailable.");
        CharacterEventListener[] listeners = resource.GetComponentsInChildren<CharacterEventListener>(true);
        CharacterEventListener rootListener = resource.GetComponent<CharacterEventListener>();
        Animator rootAnimator = resource.GetComponent<Animator>();
        if (listeners == null || listeners.Length != 1 || rootListener == null || listeners[0] != rootListener || rootAnimator == null)
            throw new InvalidOperationException("Exact enkrakenhead root CEL/Animator contract changed.");
        SkinnedMeshRenderer[] renderers = rootListener.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        SkinnedMeshRenderer source = null;
        for (int i = 0; i < renderers.Length; i++)
            if (Relative(renderers[i].transform, rootListener.transform) == KrakenProductionRendererPath)
            {
                if (source != null) throw new InvalidOperationException("enkrakenhead renderer path is ambiguous.");
                source = renderers[i];
            }
        if (source == null || source.sharedMesh == null ||
            source.bones == null || source.bones.Length != KrakenProductionOldPaths.Length ||
            source.sharedMesh.bindposes == null || source.sharedMesh.bindposes.Length != KrakenProductionOldPaths.Length)
            throw new InvalidOperationException("Exact enkrakenhead renderer PathID 121260 binding changed.");
        for (int i = 0; i < KrakenProductionOldPaths.Length; i++)
            if (source.bones[i] == null || Relative(source.bones[i], rootListener.transform) != KrakenProductionOldPaths[i])
                throw new InvalidOperationException("Exact enkrakenhead old palette order changed.");
        return rootListener;
    }

    static EnemyDummy KrakenProductionFindOwner(int ownerId)
    {
        EnemyDummy match = null;
        foreach (EnemyDummy candidate in Resources.FindObjectsOfTypeAll(typeof(EnemyDummy)))
        {
            if (candidate == null || candidate.GetInstanceID() != ownerId) continue;
            if (!SceneOwner(candidate) || !candidate.gameObject.activeInHierarchy) continue;
            if (match != null) throw new InvalidOperationException("Exact owner instance ID resolves ambiguously.");
            match = candidate;
        }
        if (match == null) throw new InvalidOperationException("Exact active live EnemyDummy is unavailable.");
        return match;
    }

    static SkinnedMeshRenderer KrakenProductionRenderer(CharacterEventListener target, int rendererId)
    {
        SkinnedMeshRenderer match = null;
        SkinnedMeshRenderer[] candidates = target.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        for (int i = 0; i < candidates.Length; i++)
        {
            SkinnedMeshRenderer candidate = candidates[i];
            if (candidate == null || candidate.GetInstanceID() != rendererId) continue;
            if (match != null) throw new InvalidOperationException("Exact renderer instance ID resolves ambiguously.");
            match = candidate;
        }
        if (match == null || Relative(match.transform, target.transform) != KrakenProductionRendererPath ||
            match.sharedMesh == null || match.sharedMesh.name != KrakenProductionExpectedMesh ||
            match.bones == null || match.bones.Length != KrakenProductionOldPaths.Length ||
            match.sharedMesh.bindposes == null || match.sharedMesh.bindposes.Length != KrakenProductionOldPaths.Length)
            throw new InvalidOperationException("Exact Gloamfin renderer binding is unavailable.");
        for (int i = 0; i < KrakenProductionOldPaths.Length; i++)
            if (match.bones[i] == null || Relative(match.bones[i], target.transform) != KrakenProductionOldPaths[i])
                throw new InvalidOperationException("Gloamfin renderer no longer uses exact old palette order.");
        return match;
    }

    static Dictionary<string, AnimationClip> KrakenProductionControllerClips(RuntimeAnimatorController controller)
    {
        if (controller == null || controller is AnimatorOverrideController || controller.name != KrakenProductionController)
            throw new InvalidOperationException("Exact krakenHeadController is required.");
        AnimationClip[] values = controller.animationClips;
        if (values == null || values.Length != KrakenProductionStates.Length)
            throw new InvalidOperationException("Exact 15-state Kraken clip-reference inventory required.");
        Dictionary<string, AnimationClip> result = new Dictionary<string, AnimationClip>(StringComparer.Ordinal);
        for (int i = 0; i < values.Length; i++)
        {
            AnimationClip clip = values[i];
            if (clip == null || KrakenProductionClipIndex(clip.name) < 0)
                throw new InvalidOperationException("Unexpected Kraken controller clip reference.");
            AnimationClip existing;
            if (result.TryGetValue(clip.name, out existing))
            {
                if (!ReferenceEquals(existing, clip))
                    throw new InvalidOperationException("One Kraken clip name resolves to multiple object identities.");
                continue;
            }
            result.Add(clip.name, clip);
        }
        if (result.Count != KrakenProductionClipNames.Length)
            throw new InvalidOperationException("Exact five unique Kraken controller clips required.");
        return result;
    }

    static void KrakenProductionRequireSamplerTopology(KrakenProductionAdapterArm arm, object sampler, string label,
        out KrakenProductionSamplerWatch watch)
    {
        if (ReferenceEquals(sampler, null)) throw new InvalidOperationException("Missing " + label + " sampler.");
        if (KrakenProductionBool(sampler, "_disposed")) throw new InvalidOperationException("Sampler disposed before arm.");
        GameObject samplerRoot = KrakenProductionField(sampler, "_root") as GameObject;
        Animator samplerAnimator = KrakenProductionField(sampler, "_animator") as Animator;
        if (samplerRoot == null || samplerAnimator == null || !samplerRoot.scene.IsValid() || !samplerRoot.activeInHierarchy ||
            samplerAnimator.runtimeAnimatorController != null || samplerAnimator.fireEvents || !samplerAnimator.isInitialized ||
            samplerAnimator.GetComponentsInChildren<CharacterEventListener>(true).Length != 0)
            throw new InvalidOperationException("Controller-free " + label + " sampler contract changed.");
        PlayableGraph graph = (PlayableGraph)KrakenProductionField(sampler, "_graph");
        AnimationMixerPlayable mixer = (AnimationMixerPlayable)KrakenProductionField(sampler, "_mixer");
        Array inputs = KrakenProductionField(sampler, "_inputs") as Array;
        IDictionary clips = KrakenProductionField(sampler, "_clips") as IDictionary;
        if (!graph.IsValid() || graph.GetTimeUpdateMode() != DirectorUpdateMode.Manual || graph.GetOutputCount() != 1 ||
            !mixer.IsValid() || inputs == null || inputs.Length != KrakenProductionClipNames.Length * 2 ||
            mixer.GetInputCount() != inputs.Length || clips == null || clips.Count != KrakenProductionClipNames.Length)
            throw new InvalidOperationException("Invalid " + label + " manual sampler graph topology.");
        for (int i = 0; i < KrakenProductionClipNames.Length; i++)
        {
            string name = KrakenProductionClipNames[i];
            if (!clips.Contains(name) || !ReferenceEquals(clips[name], arm.clips[name]))
                throw new InvalidOperationException("Sampler clip identity changed: " + name);
        }
        for (int i = 0; i < inputs.Length; i++)
        {
            AnimationClipPlayable input = (AnimationClipPlayable)inputs.GetValue(i);
            if (!input.IsValid() || !ReferenceEquals(input.GetAnimationClip(), arm.clips[KrakenProductionClipNames[i % KrakenProductionClipNames.Length]]))
                throw new InvalidOperationException("Sampler input topology/clip identity changed.");
        }
        watch = new KrakenProductionSamplerWatch { sampler = sampler, root = samplerRoot, graph = graph,
            rootId = samplerRoot.GetInstanceID(), label = label };
    }

    static IDictionary KrakenProductionLeaseTable(Type type)
    {
        IDictionary table = KrakenProductionStaticField(type, "Leases") as IDictionary;
        if (table == null) throw new InvalidOperationException("Exact EnemyMeshResources lease table unavailable.");
        return table;
    }

    static bool KrakenProductionResourceInLease(UnityEngine.Object[] resources, UnityEngine.Object value)
    {
        if (resources == null || value == null) return false;
        for (int i = 0; i < resources.Length; i++)
            if (ReferenceEquals(resources[i], value)) return true;
        return false;
    }

    static void KrakenProductionRequireMeshLease(KrakenProductionAdapterArm arm)
    {
        Component owner = arm.target.GetComponent(arm.meshOwnerType);
        if (owner == null || !ReferenceEquals(owner, arm.meshOwner))
            throw new InvalidOperationException("Exact Gloamfin EnemyMeshResources owner changed.");
        if (!(bool)KrakenProductionField(owner, "_acquired") || !(bool)KrakenProductionField(owner, "Applied") ||
            (bool)KrakenProductionField(owner, "VisualResourcesOnly"))
            throw new InvalidOperationException("Exact Gloamfin explicit mesh lease is not active.");
        int leaseId = (int)KrakenProductionField(owner, "_leaseId");
        IDictionary table = KrakenProductionLeaseTable(arm.meshOwnerType);
        if (leaseId != arm.meshLeaseId || !table.Contains(leaseId))
            throw new InvalidOperationException("Exact Gloamfin resource lease disappeared or changed.");
        object lease = table[leaseId];
        UnityEngine.Object[] values = KrakenProductionField(lease, "resources") as UnityEngine.Object[];
        IList owners = KrakenProductionField(lease, "owners") as IList;
        int references = (int)KrakenProductionField(lease, "references");
        if (values == null || values.Length < 3 || values.Length > 64 || owners == null || references < 1 ||
            !KrakenProductionContains(owners, owner) || !KrakenProductionResourceInLease(values, arm.renderer.sharedMesh))
            throw new InvalidOperationException("Gloamfin resource lease contents changed.");
        if (arm.resources.Count == 0)
        {
            for (int i = 0; i < values.Length; i++)
            {
                UnityEngine.Object value = values[i];
                if (value == null) throw new InvalidOperationException("Null Gloamfin lease resource cannot be watched.");
                arm.resources.Add(new KrakenProductionWatchedResource { value = value, instanceId = value.GetInstanceID(),
                    name = value.name, type = value.GetType().FullName });
            }
        }
        else
        {
            if (arm.resources.Count != values.Length)
                throw new InvalidOperationException("Gloamfin resource lease count changed.");
            for (int i = 0; i < values.Length; i++)
                if (!ReferenceEquals(arm.resources[i].value, values[i]))
                    throw new InvalidOperationException("Gloamfin resource lease identity/order changed.");
        }
        Material[] materials = arm.renderer.sharedMaterials;
        if (materials == null || materials.Length < 1 || materials.Length > 8)
            throw new InvalidOperationException("Gloamfin renderer material slots changed.");
        for (int i = 0; i < materials.Length; i++)
        {
            Material material = materials[i];
            if (material == null || !KrakenProductionResourceInLease(values, material))
                throw new InvalidOperationException("Gloamfin renderer material is not lease-owned.");
            if (material.HasProperty("_MainTex"))
            {
                Texture texture = material.GetTexture("_MainTex");
                if (texture == null || !KrakenProductionResourceInLease(values, texture))
                    throw new InvalidOperationException("Gloamfin base texture is not lease-owned.");
            }
        }
    }

    static JObject KrakenProductionAdapterFlags(KrakenProductionAdapterArm arm, bool requireHealthy)
    {
        object lease = arm.adapter;
        bool configured = KrakenProductionBool(lease, "_configured");
        bool initialized = KrakenProductionBool(lease, "_initialized");
        bool disabled = KrakenProductionBool(lease, "_disabled");
        bool disposed = KrakenProductionBool(lease, "_disposed");
        bool registered = KrakenProductionBool(lease, "_ownerRegistered");
        if (!ReferenceEquals(KrakenProductionField(lease, "_owner"), arm.owner) ||
            !ReferenceEquals(KrakenProductionField(lease, "_target"), arm.target) ||
            !ReferenceEquals(KrakenProductionField(lease, "_resource"), arm.resource) ||
            !ReferenceEquals(KrakenProductionField(lease, "_enemyRow"), arm.row) ||
            !ReferenceEquals(KrakenProductionField(lease, "_nativeAnimator"), arm.animator) ||
            !ReferenceEquals(KrakenProductionField(lease, "_nativeController"), arm.controller) ||
            !ReferenceEquals(KrakenProductionField(lease, "_renderer"), arm.renderer) ||
            (int)KrakenProductionField(lease, "_rendererId") != arm.rendererId ||
            !ReferenceEquals(KrakenProductionField(lease, "_rendererMesh"), arm.renderer.sharedMesh))
            throw new InvalidOperationException("LegacyKrakenResourceAdapterLease identity changed.");
        if (requireHealthy && (!configured || !initialized || disabled || disposed || !registered))
            throw new InvalidOperationException("LegacyKrakenResourceAdapterLease is not healthy while owner remains live.");
        return new JObject { { "configured", configured }, { "initialized", initialized }, { "disabled", disabled },
            { "disposed", disposed }, { "ownerRegistered", registered }, { "enabled", ((Behaviour)lease).enabled } };
    }

    static void KrakenProductionRequireOwner(KrakenProductionAdapterArm arm)
    {
        if (arm.owner == null || arm.target == null || arm.adapter == null || arm.renderer == null || arm.row == null ||
            !SceneOwner(arm.owner) || !SceneOwner(arm.target) || !arm.owner.gameObject.activeInHierarchy ||
            !arm.target.gameObject.activeInHierarchy || !ReferenceEquals(arm.owner.m_EventListener, arm.target) ||
            !ReferenceEquals(arm.target.m_Dummy, arm.owner) || !ReferenceEquals(arm.owner.m_EnemyCombat, arm.row) ||
            arm.row.m_ID != KrakenProductionRegisteredEnemy || !ReferenceEquals(arm.row.m_EnemyAsset, arm.resource) ||
            arm.owner.GetInstanceID() != arm.ownerId || arm.target.GetInstanceID() != arm.targetId ||
            arm.renderer.GetInstanceID() != arm.rendererId || arm.adapter.GetInstanceID() != arm.adapterId ||
            !ReferenceEquals(KrakenProductionAnimator(arm.target), arm.animator) ||
            !ReferenceEquals(arm.animator.runtimeAnimatorController, arm.controller) || arm.controller == null ||
            arm.controller.name != KrakenProductionController || arm.animator.layerCount != 1)
            throw new InvalidOperationException("Pinned live EnemyDummy/CEL/resource/controller identity drifted.");
        FTK_enemyCombat native = FTK_enemyCombatDB.GetDB().GetEntryByStringID(KrakenProductionNativeEnemy);
        if (native == null || native.m_EnemyAsset == null || native.m_WeaponAsset == null ||
            !ReferenceEquals(arm.row.m_WeaponAsset, native.m_WeaponAsset))
            throw new InvalidOperationException("Pinned native Kraken controller source identity drifted.");
        KrakenProductionRenderer(arm.target, arm.rendererId);
        KrakenProductionAdapterFlags(arm, true);
        KrakenProductionRequireMeshLease(arm);
    }

    static JObject KrakenProductionSamplerView(KrakenProductionAdapterArm arm, KrakenProductionSamplerWatch watch)
    {
        object sampler = watch.sampler;
        KrakenProductionRequireSamplerTopology(arm, sampler, watch.label, out watch);
        Array inputs = KrakenProductionField(sampler, "_inputs") as Array;
        AnimationMixerPlayable mixer = (AnimationMixerPlayable)KrakenProductionField(sampler, "_mixer");
        JArray values = new JArray();
        for (int i = 0; i < inputs.Length; i++)
        {
            AnimationClipPlayable input = (AnimationClipPlayable)inputs.GetValue(i);
            double time = input.GetTime();
            float weight = mixer.GetInputWeight(i);
            if (Double.IsNaN(time) || Double.IsInfinity(time) || !KrakenProductionFinite(weight) || weight < 0f || weight > 1f)
                throw new InvalidOperationException("Sampler time/weight is invalid.");
            values.Add(new JObject { { "input", i }, { "bank", i < KrakenProductionClipNames.Length ? "current" : "next" },
                { "clip", KrakenProductionClipNames[i % KrakenProductionClipNames.Length] }, { "seconds", time },
                { "weight", weight }, { "valid", input.IsValid() } });
        }
        return new JObject { { "label", watch.label }, { "rootInstanceId", watch.rootId },
            { "rootUnityNull", watch.root == null }, { "samplerDisposed", KrakenProductionBool(sampler, "_disposed") },
            { "graphValid", watch.graph.IsValid() }, { "graphMode", watch.graph.GetTimeUpdateMode().ToString() },
            { "graphOutputCount", watch.graph.GetOutputCount() }, { "inputs", values } };
    }

    static JObject KrakenProductionStateView(KrakenProductionAdapterArm arm, AnimatorStateInfo state,
        AnimatorClipInfo[] clipInfos, string role)
    {
        JObject result = new JObject { { "role", role }, { "fullPathHash", state.fullPathHash },
            { "shortNameHash", state.shortNameHash }, { "normalizedTime", state.normalizedTime },
            { "length", state.length }, { "loop", state.loop }, { "speed", state.speed },
            { "speedMultiplier", state.speedMultiplier } };
        KrakenProductionStateContract contract = KrakenProductionState(state.fullPathHash);
        result["state"] = contract == null ? null : contract.state;
        if (clipInfos == null) clipInfos = new AnimatorClipInfo[0];
        JArray clips = new JArray();
        for (int i = 0; i < clipInfos.Length; i++)
        {
            AnimationClip clip = clipInfos[i].clip;
            clips.Add(new JObject { { "name", clip == null ? null : clip.name },
                { "instanceId", clip == null ? 0 : clip.GetInstanceID() },
                { "length", clip == null ? 0f : clip.length }, { "loop", clip != null && clip.isLooping },
                { "weight", clipInfos[i].weight } });
        }
        result["clips"] = clips;
        if (contract == null || clipInfos.Length != 1 || clipInfos[0].clip == null)
            throw new InvalidOperationException("Native " + role + " state/clip is not in the pinned one-clip contract.");
        AnimationClip expected;
        if (!arm.clips.TryGetValue(contract.clip, out expected) || !ReferenceEquals(expected, clipInfos[0].clip) ||
            clipInfos[0].clip.name != contract.clip || !KrakenProductionFinite(state.normalizedTime) ||
            !KrakenProductionFinite(state.length) || !KrakenProductionFinite(state.speed) ||
            !KrakenProductionFinite(state.speedMultiplier) || !KrakenProductionFinite(clipInfos[0].weight) ||
            !KrakenProductionFinite(clipInfos[0].clip.length) || clipInfos[0].clip.length <= 0f ||
            clipInfos[0].weight < 0f || clipInfos[0].weight > 1f || state.speed != 1f ||
            state.speedMultiplier != 1f || state.length != clipInfos[0].clip.length || state.loop != contract.loop ||
            clipInfos[0].clip.isLooping != contract.loop)
            throw new InvalidOperationException("Native " + role + " state clock/clip contract changed.");
        result["certified"] = true;
        result["expectedClip"] = contract.clip;
        return result;
    }

    void KrakenProductionFail(KrakenProductionAdapterArm arm, string message, bool drift)
    {
        if (arm == null) return;
        if (arm.error == null) arm.error = message;
        if (drift) arm.identityDrift = true;
    }

    static void KrakenProductionAppend(JArray values, int maximum, ref bool overflow, KrakenProductionAdapterArm arm,
        JObject value, string kind)
    {
        if (values.Count >= maximum)
        {
            overflow = true;
            if (arm.error == null) arm.error = "Kraken production " + kind + " bound" + maximum + " exceeded.";
            return;
        }
        value["sequence"] = values.Count;
        values.Add(value);
    }

    void KrakenProductionRecordFrame(KrakenProductionAdapterArm arm)
    {
        if (arm == null || arm.cleared || arm.error != null || arm.overflow || arm.teardownStarted) return;
        try
        {
            KrakenProductionRequireOwner(arm);
            bool transition = arm.animator.IsInTransition(0);
            JObject current = KrakenProductionStateView(arm, arm.animator.GetCurrentAnimatorStateInfo(0),
                arm.animator.GetCurrentAnimatorClipInfo(0), "current");
            JObject next = null;
            if (transition)
            {
                next = KrakenProductionStateView(arm, arm.animator.GetNextAnimatorStateInfo(0),
                    arm.animator.GetNextAnimatorClipInfo(0), "next");
                float currentWeight = (float)((JArray)current["clips"])[0]["weight"];
                float nextWeight = (float)((JArray)next["clips"])[0]["weight"];
                if (Math.Abs(currentWeight + nextWeight - 1f) > KrakenProductionWeightTolerance)
                    throw new InvalidOperationException("Transition raw current/next weights did not sum to one.");
            }
            else if (Math.Abs((float)((JArray)current["clips"])[0]["weight"] - 1f) > KrakenProductionWeightTolerance)
            {
                throw new InvalidOperationException("Non-transition native state did not retain full raw weight.");
            }
            JObject modernSampler = KrakenProductionSamplerView(arm, arm.modernWatch);
            JObject oldSampler = KrakenProductionSamplerView(arm, arm.oldWatch);
            int currentHash = (int)current["fullPathHash"];
            int nextHash = next == null ? 0 : (int)next["fullPathHash"];
            bool changed = !arm.hasRecordedFrame || transition != arm.lastTransition ||
                currentHash != arm.lastCurrentHash || nextHash != arm.lastNextHash;
            bool periodic = arm.lastRecordedFrame < 0 || Time.frameCount - arm.lastRecordedFrame >= 30;
            if (!changed && !periodic) return;
            JObject frame = new JObject { { "frame", Time.frameCount }, { "realtime", Time.realtimeSinceStartup },
                { "postLateUpdate", true }, { "inTransition", transition }, { "current", current },
                { "next", next == null ? (JToken)new JValue((object)null) : (JToken)next }, { "adapter", KrakenProductionAdapterFlags(arm, true) },
                { "modernSampler", modernSampler }, { "oldSampler", oldSampler } };
            KrakenProductionAppend(arm.frames, KrakenProductionFrameLimit, ref arm.overflow, arm, frame, "post-LateUpdate frame");
            arm.hasRecordedFrame = true;
            arm.lastRecordedFrame = Time.frameCount;
            arm.lastCurrentHash = currentHash;
            arm.lastNextHash = nextHash;
            arm.lastTransition = transition;
        }
        catch (Exception error)
        {
            KrakenProductionFail(arm, "Post-LateUpdate adapter observation failed: " + error.Message, true);
        }
    }

    void KrakenProductionRecordCallback(KrakenProductionAdapterArm arm, JObject entry)
    {
        if (arm == null || arm.cleared || arm.teardownStarted) return;
        entry["frame"] = Time.frameCount;
        entry["realtime"] = Time.realtimeSinceStartup;
        entry["source"] = "passive-native-callback";
        KrakenProductionAppend(arm.callbacks, KrakenProductionCallbackLimit, ref arm.overflow, arm, entry, "native callback");
    }

    void KrakenProductionRecordStateCallback(KrakenProductionAdapterArm arm, JObject entry)
    {
        if (arm == null || arm.cleared || arm.teardownStarted) return;
        entry["frame"] = Time.frameCount;
        entry["realtime"] = Time.realtimeSinceStartup;
        entry["source"] = "passive-native-state-callback";
        KrakenProductionAppend(arm.stateCallbacks, KrakenProductionCallbackLimit, ref arm.overflow, arm, entry, "native state callback");
    }

    void KrakenProductionRecordCleanup(KrakenProductionAdapterArm arm, JObject entry)
    {
        if (arm == null || arm.cleared) return;
        entry["frame"] = Time.frameCount;
        entry["realtime"] = Time.realtimeSinceStartup;
        entry["source"] = "passive-adapter-lifecycle-hook";
        KrakenProductionAppend(arm.cleanup, KrakenProductionCleanupLimit, ref arm.overflow, arm, entry, "cleanup event");
    }

    static KrakenProductionAdapterArm KrakenProductionArmForLease(object instance)
    {
        RuntimeModelTest observer = krakenProductionAdapterObserver;
        KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
        return arm != null && !arm.cleared && ReferenceEquals(arm.adapter, instance) ? arm : null;
    }

    static KrakenProductionAdapterArm KrakenProductionArmForSampler(object instance)
    {
        RuntimeModelTest observer = krakenProductionAdapterObserver;
        KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
        if (arm == null || arm.cleared) return null;
        return ReferenceEquals(arm.modernSampler, instance) || ReferenceEquals(arm.oldSampler, instance) ? arm : null;
    }

    static KrakenProductionAttackWindow KrakenProductionActiveAttack(KrakenProductionAdapterArm arm)
    {
        for (int i = arm.attacks.Count - 1; i >= 0; i--)
            if (!arm.attacks[i].completed) return arm.attacks[i];
        return null;
    }

    static bool KrakenProductionRelevantVictim(KrakenProductionAdapterArm arm, CharacterDummy dummy)
    {
        if (dummy == null) return false;
        if (ReferenceEquals(dummy, arm.owner)) return true;
        for (int i = 0; i < arm.attacks.Count; i++)
        {
            KrakenProductionAttackWindow attack = arm.attacks[i];
            if ((attack.hasPrimary && KrakenProductionFidEquals(attack.primary, dummy.FID)) ||
                (attack.hasSecondary && KrakenProductionFidEquals(attack.secondary, dummy.FID)) ||
                (attack.hasTertiary && KrakenProductionFidEquals(attack.tertiary, dummy.FID))) return true;
        }
        return false;
    }

    void KrakenProductionRecordIncomingAttack(KrakenProductionAdapterArm arm, JObject entry)
    {
        if (arm == null || arm.cleared || arm.teardownStarted) return;
        entry["frame"] = Time.frameCount;
        entry["realtime"] = Time.realtimeSinceStartup;
        entry["source"] = "passive-native-engage-attack";
        KrakenProductionAppend(arm.incomingAttackEvents, KrakenProductionIncomingAttackLimit,
            ref arm.overflow, arm, entry, "incoming attack");
    }

    static void KrakenProductionStartEngagePrefix(CharacterDummy _attackingDummy, CharacterDummy _damagedDummy,
        float _slotSuccessPercent, int _focusedSlots, FTK_proficiencyTable.ID _prof, bool _consumable,
        SlotControl.AttackCheatType _cheatType, ref JObject __state)
    {
        __state = null;
        try
        {
            RuntimeModelTest observer = krakenProductionAdapterObserver;
            KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || !ReferenceEquals(_damagedDummy, arm.owner) || _attackingDummy == null) return;
            if (arm.incomingAttacks.Count >= KrakenProductionIncomingAttackLimit)
                throw new InvalidOperationException("Incoming attack bound exceeded.");
            JObject entry = new JObject { { "kind", "DamageCalculator.StartEngageAttack" },
                { "sequence", arm.incomingAttacks.Count }, { "attackerInstanceId", _attackingDummy.GetInstanceID() },
                { "attackerFid", KrakenProductionFid(_attackingDummy.FID) }, { "targetInstanceId", _damagedDummy.GetInstanceID() },
                { "targetFid", KrakenProductionFid(_damagedDummy.FID) }, { "slotSuccessPercent", _slotSuccessPercent },
                { "focusedSlots", _focusedSlots }, { "proficiency", _prof.ToString() },
                { "consumable", _consumable }, { "cheatType", _cheatType.ToString() },
                { "boundToDamage", false }, { "finalized", false } };
            KrakenProductionIncomingAttack incoming = new KrakenProductionIncomingAttack {
                sequence = arm.incomingAttacks.Count, attacker = _attackingDummy.FID,
                cheatType = _cheatType.ToString(), proficiency = _prof.ToString(),
                consumable = _consumable, entry = entry };
            arm.incomingAttacks.Add(incoming); __state = entry;
            observer.KrakenProductionRecordIncomingAttack(arm, entry);
        }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("StartEngageAttack prefix", error); }
    }

    static Exception KrakenProductionStartEngageFinalizer(Exception __exception, JObject __state)
    {
        return KrakenProductionFinalize(__exception, __state, false, "StartEngageAttack finalizer");
    }

    void KrakenProductionRecordTerminalAuthority(KrakenProductionAdapterArm arm, JObject entry)
    {
        if (arm == null || arm.cleared || arm.teardownStarted) return;
        entry["sequence"] = arm.terminalAuthorityEvents.Count;
        entry["frame"] = Time.frameCount;
        entry["realtime"] = Time.realtimeSinceStartup;
        entry["source"] = "passive-native-terminal-authority";
        KrakenProductionAppend(arm.terminalAuthorityEvents, KrakenProductionTerminalAuthorityLimit,
            ref arm.overflow, arm, entry, "terminal authority");
    }

    static JObject KrakenProductionPartyLossSnapshot(EncounterSessionMC sessionMc)
    {
        EncounterSession session = EncounterSession.Instance;
        if (sessionMc == null || session == null) throw new InvalidOperationException("Live encounter session unavailable.");
        bool alivePlayer = false;
        IList players = KrakenProductionField(sessionMc, "m_AllCombtatantsAlive") as IList;
        if (players == null) throw new InvalidOperationException("Combat player roster unavailable.");
        for (int i = 0; i < players.Count; i++)
        {
            CharacterDummy player = session.GetDummyByFID((FTKPlayerID)players[i]);
            if (player != null && player.m_IsAlive) { alivePlayer = true; break; }
        }
        bool aliveEnemy = false;
        IDictionary statuses = KrakenProductionField(sessionMc, "m_EnemyStatuses") as IDictionary;
        if (statuses == null) throw new InvalidOperationException("Combat enemy status ledger unavailable.");
        foreach (DictionaryEntry row in statuses)
            if (row.Value != null && KrakenProductionBool(row.Value, "m_Alive")) { aliveEnemy = true; break; }
        object boat = KrakenProductionField(session, "m_Boat");
        UnityEngine.Object boatObject = boat as UnityEngine.Object;
        bool boatPresent = boatObject != null;
        float boatHealth = boatPresent ? Convert.ToSingle(KrakenProductionField(boat, "m_Health")) : 0f;
        return new JObject { { "noAlivePlayer", !alivePlayer }, { "anyAliveEnemy", aliveEnemy },
            { "boatPresent", boatPresent }, { "boatHealth", boatPresent ? (JToken)new JValue(boatHealth) : new JValue((object)null) },
            { "boatDestroyed", boatPresent && boatHealth <= 0f } };
    }

    static void KrakenProductionCombatCycleEndPrefix(EncounterSessionMC __instance, ref JObject __state)
    {
        __state = null;
        try
        {
            RuntimeModelTest observer = krakenProductionAdapterObserver;
            KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || arm.campaignRun != "enemy-victory-terminal") return;
            __state = new JObject { { "kind", "EncounterSessionMC.CombatCycleEnd" },
                { "preconditions", KrakenProductionPartyLossSnapshot(__instance) }, { "finalized", false } };
            observer.KrakenProductionRecordTerminalAuthority(arm, __state);
        }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("CombatCycleEnd prefix", error); }
    }

    static void KrakenProductionStartEndCombatPrefix(EncounterSession __instance, string _ackID,
        bool _playEnemyVictory, float _waitTime, ref JObject __state)
    {
        __state = null;
        try
        {
            RuntimeModelTest observer = krakenProductionAdapterObserver;
            KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || arm.campaignRun != "enemy-victory-terminal" ||
                !ReferenceEquals(__instance, EncounterSession.Instance)) return;
            __state = new JObject { { "kind", "EncounterSession.StartEndCombatSequence" }, { "ackID", _ackID },
                { "playEnemyVictory", _playEnemyVictory }, { "waitTime", _waitTime }, { "finalized", false } };
            observer.KrakenProductionRecordTerminalAuthority(arm, __state);
        }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("StartEndCombatSequence prefix", error); }
    }

    static Exception KrakenProductionTerminalAuthorityFinalizer(Exception __exception, JObject __state)
    {
        return KrakenProductionFinalize(__exception, __state, false, "terminal authority finalizer");
    }

    static void KrakenProductionBindIncomingAttack(KrakenProductionAdapterArm arm, CharacterDummy attacker,
        DummyDamageInfo primary, DummyDamageInfo secondary, DummyDamageInfo tertiary)
    {
        if (ReferenceEquals(attacker, arm.owner)) return;
        DummyDamageInfo[] values = { primary, secondary, tertiary };
        bool targetsOwner = false;
        for (int i = 0; i < values.Length; i++)
            if (values[i] != null && KrakenProductionFidEquals(values[i].m_VictimID, arm.owner.FID)) targetsOwner = true;
        if (!targetsOwner) return;
        KrakenProductionIncomingAttack incoming = null;
        for (int i = 0; i < arm.incomingAttacks.Count; i++)
        {
            KrakenProductionIncomingAttack candidate = arm.incomingAttacks[i];
            if (!candidate.bound && KrakenProductionFidEquals(candidate.attacker, attacker.FID)) { incoming = candidate; break; }
        }
        if (incoming == null) throw new InvalidOperationException("Incoming attack has no matching StartEngageAttack authority record.");
        incoming.bound = true; incoming.entry["boundToDamage"] = true;
        JArray damage = new JArray();
        for (int i = 0; i < values.Length; i++)
        {
            DummyDamageInfo value = values[i];
            if (value == null) continue;
            damage.Add(KrakenProductionDamage(value));
            if (KrakenProductionFidEquals(value.m_VictimID, arm.owner.FID)) arm.incomingDamage[value] = incoming;
        }
        incoming.entry["damage"] = damage;
    }

    static KrakenProductionIncomingAttack KrakenProductionResolveIncomingAttack(
        KrakenProductionAdapterArm arm, DummyDamageInfo damageInfo)
    {
        if (damageInfo == null) return null;
        KrakenProductionIncomingAttack incoming;
        if (arm.incomingDamage.TryGetValue(damageInfo, out incoming) && !incoming.responded) return incoming;
        JObject serialized = KrakenProductionDamage(damageInfo);
        for (int i = 0; i < arm.incomingAttacks.Count; i++)
        {
            KrakenProductionIncomingAttack candidate = arm.incomingAttacks[i];
            if (!candidate.bound || candidate.responded) continue;
            JArray calculated = candidate.entry["damage"] as JArray;
            if (calculated == null) continue;
            for (int j = 0; j < calculated.Count; j++)
                if (JToken.DeepEquals(calculated[j], serialized)) return candidate;
        }
        return null;
    }

    static JObject KrakenProductionCallbackState(string kind, CharacterDummy dummy, bool mainVictim)
    {
        return new JObject { { "kind", kind }, { "dummyInstanceId", dummy == null ? 0 : dummy.GetInstanceID() },
            { "fid", dummy == null ? null : KrakenProductionFid(dummy.FID) }, { "mainVictim", mainVictim },
            { "preHealth", dummy == null ? 0 : dummy.GetCurrentHealth() },
            { "damageInfo", dummy == null ? null : KrakenProductionDamage(dummy.m_DamageInfo) }, { "finalized", false } };
    }

    static JObject KrakenProductionCallbackAnimator(KrakenProductionAdapterArm arm)
    {
        bool transition = arm.animator.IsInTransition(0);
        AnimatorStateInfo current = arm.animator.GetCurrentAnimatorStateInfo(0);
        KrakenProductionStateContract currentContract = KrakenProductionState(current.fullPathHash);
        AnimatorStateInfo next = transition ? arm.animator.GetNextAnimatorStateInfo(0) : default(AnimatorStateInfo);
        KrakenProductionStateContract nextContract = transition ? KrakenProductionState(next.fullPathHash) : null;
        return new JObject { { "inTransition", transition }, { "currentState", currentContract == null ? null : currentContract.state },
            { "currentFullPathHash", current.fullPathHash }, { "nextState", nextContract == null ? null : nextContract.state },
            { "nextFullPathHash", transition ? (JToken)new JValue(next.fullPathHash) : new JValue((object)null) } };
    }

    void InstallKrakenProductionAdapterHooks(KrakenProductionAdapterArm arm)
    {
        if (krakenProductionAdapterHooks) return;
        MethodInfo lateUpdate = arm.adapterType.GetMethod("LateUpdate", Members, null, Type.EmptyTypes, null);
        MethodInfo disable = arm.adapterType.GetMethod("DisableForOwner", Members, null, new[] { typeof(Exception) }, null);
        MethodInfo dispose = arm.adapterType.GetMethod("DisposeOwned", Members, null, Type.EmptyTypes, null);
        MethodInfo samplerDispose = arm.samplerType.GetMethod("Dispose", Members, null, Type.EmptyTypes, null);
        MethodInfo attack = typeof(CharacterDummy).GetMethod("PlayAttackSequence", Members, null,
            new[] { typeof(CharacterDummy.AttackAnim), typeof(CharacterEventListener.CombatAnimTrigger),
                typeof(DummyDamageInfo), typeof(DummyDamageInfo), typeof(DummyDamageInfo) }, null);
        MethodInfo engage = typeof(DamageCalculator).GetMethod("StartEngageAttack", Statics, null,
            new[] { typeof(CharacterDummy), typeof(CharacterDummy), typeof(float), typeof(int),
                typeof(FTK_proficiencyTable.ID), typeof(bool), typeof(SlotControl.AttackCheatType) }, null);
        MethodInfo combatCycleEnd = typeof(EncounterSessionMC).GetMethod("CombatCycleEnd", Members, null, Type.EmptyTypes, null);
        MethodInfo startEndCombat = typeof(EncounterSession).GetMethod("StartEndCombatSequence", Members, null,
            new[] { typeof(string), typeof(bool), typeof(float) }, null);
        MethodInfo trigger = typeof(CharacterEventListener).GetMethod("CombatTrigger", Members, null,
            new[] { typeof(CharacterEventListener.CombatAnimTrigger), typeof(bool) }, null);
        MethodInfo foley = typeof(CharacterEventListener).GetMethod("Foley", Members, null, new[] { typeof(string) }, null);
        MethodInfo weapon = typeof(CharacterEventListener).GetMethod("PlayWeaponAnimation", Members, null, new[] { typeof(string) }, null);
        MethodInfo dodge = typeof(CharacterEventListener).GetMethod("Dodge", Members, null, Type.EmptyTypes, null);
        MethodInfo hit = typeof(CharacterEventListener).GetMethod("AttackHit", Members, null, Type.EmptyTypes, null);
        MethodInfo respondDodge = typeof(CharacterDummy).GetMethod("RespondToDodge", Members, null, new[] { typeof(bool) }, null);
        MethodInfo respondHit = typeof(CharacterDummy).GetMethod("RespondToHit", Members, null, new[] { typeof(bool) }, null);
        MethodInfo completed = typeof(CharacterDummy).GetMethod("ActionCompleted", Members, null, Type.EmptyTypes, null);
        if (lateUpdate == null || disable == null || dispose == null || samplerDispose == null || attack == null || engage == null ||
            combatCycleEnd == null || startEndCombat == null || trigger == null ||
            foley == null || weapon == null || dodge == null || hit == null || respondDodge == null || respondHit == null || completed == null)
            throw new InvalidOperationException("Exact Kraken passive observation methods unavailable.");
        Harmony harmony = new Harmony("com.ftkmf.runtime-model-test.kraken-production-adapter-observer");
        harmony.Patch(lateUpdate, null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionLateUpdatePostfix", Statics)));
        harmony.Patch(disable, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionDisablePrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionDisableFinalizer", Statics)), null);
        harmony.Patch(dispose, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionDisposePrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionDisposeFinalizer", Statics)), null);
        harmony.Patch(samplerDispose, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionSamplerDisposePrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionSamplerDisposeFinalizer", Statics)), null);
        harmony.Patch(attack, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionAttackPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCallbackFinalizer", Statics)), null);
        harmony.Patch(engage, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionStartEngagePrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionStartEngageFinalizer", Statics)), null);
        harmony.Patch(combatCycleEnd, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCombatCycleEndPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionTerminalAuthorityFinalizer", Statics)), null);
        harmony.Patch(startEndCombat, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionStartEndCombatPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionTerminalAuthorityFinalizer", Statics)), null);
        harmony.Patch(trigger, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionTriggerPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCallbackFinalizer", Statics)), null);
        harmony.Patch(foley, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionFoleyPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCallbackFinalizer", Statics)), null);
        harmony.Patch(weapon, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionWeaponPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCallbackFinalizer", Statics)), null);
        harmony.Patch(dodge, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionDodgePrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCallbackFinalizer", Statics)), null);
        harmony.Patch(hit, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionHitPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCallbackFinalizer", Statics)), null);
        harmony.Patch(respondDodge, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionRespondDodgePrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCallbackFinalizer", Statics)), null);
        harmony.Patch(respondHit, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionRespondHitPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCallbackFinalizer", Statics)), null);
        harmony.Patch(completed, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCompletedPrefix", Statics)), null,
            null, new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("KrakenProductionCallbackFinalizer", Statics)), null);
        krakenProductionAdapterHooks = true;
    }

    static void KrakenProductionLateUpdatePostfix(object __instance)
    {
        try
        {
            KrakenProductionAdapterArm arm = KrakenProductionArmForLease(__instance);
            RuntimeModelTest observer = krakenProductionAdapterObserver;
            if (arm != null && observer != null) observer.KrakenProductionRecordFrame(arm);
        }
        catch (Exception error) { KrakenProductionObserverFailure("LateUpdate postfix", error); }
    }

    static void KrakenProductionObserverFailure(string context, Exception error)
    {
        try
        {
            RuntimeModelTest observer = krakenProductionAdapterObserver;
            KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null) return;
            if (arm.error == null) arm.error = "Passive observer " + context + " failed: " + error.Message;
            arm.identityDrift = true;
        }
        catch { }
    }

    static Exception KrakenProductionFinalize(Exception original, JObject state, bool captureHealth, string context)
    {
        try
        {
            if (state == null) return original;
            state["finalized"] = true;
            state["exception"] = original == null ? new JValue((object)null) : new JValue(original.ToString());
            int dummyId = captureHealth ? ((int?)state["dummyInstanceId"] ?? 0) : 0;
            if (dummyId != 0)
            {
                foreach (CharacterDummy candidate in Resources.FindObjectsOfTypeAll(typeof(CharacterDummy)))
                    if (candidate != null && candidate.GetInstanceID() == dummyId)
                    { state["postHealth"] = candidate.GetCurrentHealth(); break; }
            }
        }
        catch (Exception error) { KrakenProductionObserverFailure(context, error); }
        return original;
    }

    static void KrakenProductionDisablePrefix(object __instance, Exception __0, ref JObject __state)
    {
        __state = null;
        try
        {
            KrakenProductionAdapterArm arm = KrakenProductionArmForLease(__instance);
            RuntimeModelTest observer = krakenProductionAdapterObserver;
            if (arm == null || observer == null) return;
            bool alreadyDisabled = KrakenProductionBool(__instance, "_disabled");
            arm.disableCalls++; if (!alreadyDisabled) arm.effectiveDisableCalls++;
            __state = new JObject { { "kind", "LegacyKrakenResourceAdapterLease.DisableForOwner" },
                { "effective", !alreadyDisabled }, { "reason", __0 == null ? null : __0.GetType().FullName + ": " + __0.Message },
                { "finalized", false } };
            observer.KrakenProductionRecordCleanup(arm, __state);
        }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("DisableForOwner prefix", error); }
    }

    static Exception KrakenProductionDisableFinalizer(Exception __exception, JObject __state)
    {
        return KrakenProductionFinalize(__exception, __state, false, "DisableForOwner finalizer");
    }

    static void KrakenProductionDisposePrefix(object __instance, ref JObject __state)
    {
        __state = null;
        try
        {
            KrakenProductionAdapterArm arm = KrakenProductionArmForLease(__instance);
            RuntimeModelTest observer = krakenProductionAdapterObserver;
            if (arm == null || observer == null) return;
            bool alreadyDisposed = KrakenProductionBool(__instance, "_disposed");
            arm.adapterDisposeCalls++; if (!alreadyDisposed) arm.effectiveAdapterDisposeCalls++;
            __state = new JObject { { "kind", "LegacyKrakenResourceAdapterLease.DisposeOwned" },
                { "effective", !alreadyDisposed }, { "finalized", false } };
            observer.KrakenProductionRecordCleanup(arm, __state);
        }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("DisposeOwned prefix", error); }
    }

    static Exception KrakenProductionDisposeFinalizer(Exception __exception, JObject __state)
    {
        return KrakenProductionFinalize(__exception, __state, false, "DisposeOwned finalizer");
    }

    static void KrakenProductionSamplerDisposePrefix(object __instance, ref JObject __state)
    {
        __state = null;
        try
        {
            KrakenProductionAdapterArm arm = KrakenProductionArmForSampler(__instance);
            RuntimeModelTest observer = krakenProductionAdapterObserver;
            if (arm == null || observer == null) return;
            bool alreadyDisposed = KrakenProductionBool(__instance, "_disposed");
            arm.samplerDisposeCalls++; if (!alreadyDisposed) arm.effectiveSamplerDisposeCalls++;
            string label = ReferenceEquals(arm.modernSampler, __instance) ? "modern" : "old";
            __state = new JObject { { "kind", "LegacyKrakenResourceAdapterLease.ClipSampler.Dispose" }, { "sampler", label },
                { "effective", !alreadyDisposed }, { "finalized", false } };
            observer.KrakenProductionRecordCleanup(arm, __state);
        }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("ClipSampler.Dispose prefix", error); }
    }

    static Exception KrakenProductionSamplerDisposeFinalizer(Exception __exception, JObject __state)
    {
        return KrakenProductionFinalize(__exception, __state, false, "ClipSampler.Dispose finalizer");
    }

    static void KrakenProductionAttackPrefix(CharacterDummy __instance, CharacterDummy.AttackAnim _attackAnim,
        CharacterEventListener.CombatAnimTrigger _override, DummyDamageInfo _ddi, DummyDamageInfo _ddi1,
        DummyDamageInfo _ddi2, ref JObject __state)
    {
        __state = null;
        try
        {
            RuntimeModelTest observer = krakenProductionAdapterObserver;
            KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared) return;
            KrakenProductionBindIncomingAttack(arm, __instance, _ddi, _ddi1, _ddi2);
            if (!ReferenceEquals(__instance, arm.owner)) return;
            string variant = KrakenProductionVariant(_attackAnim, _override);
            if (variant == null) return;
            if (arm.attacks.Count >= KrakenProductionAttackLimit) throw new InvalidOperationException("Attack window bound exceeded.");
            if (_ddi == null) throw new InvalidOperationException("Native Kraken attack lacks primary damage info.");
            KrakenProductionAttackWindow window = new KrakenProductionAttackWindow { id = arm.attacks.Count,
                variant = variant, primary = _ddi.m_VictimID, hasPrimary = true,
                secondary = _ddi1 == null ? default(FTKPlayerID) : _ddi1.m_VictimID, hasSecondary = _ddi1 != null,
                tertiary = _ddi2 == null ? default(FTKPlayerID) : _ddi2.m_VictimID, hasTertiary = _ddi2 != null };
            JObject entry = new JObject { { "kind", "CharacterDummy.PlayAttackSequence" }, { "attackWindow", window.id },
                { "variant", variant }, { "attackAnim", _attackAnim.ToString() }, { "override", _override.ToString() },
                { "actorInstanceId", __instance.GetInstanceID() }, { "primary", KrakenProductionDamage(_ddi) },
                { "secondary", KrakenProductionDamage(_ddi1) }, { "tertiary", KrakenProductionDamage(_ddi2) }, { "finalized", false } };
            window.entry = entry; arm.attacks.Add(window); __state = entry; observer.KrakenProductionRecordCallback(arm, entry);
        }
        catch (Exception error)
        {
            __state = null; KrakenProductionObserverFailure("PlayAttackSequence prefix", error);
        }
    }

    static void KrakenProductionTriggerPrefix(CharacterEventListener __instance, CharacterEventListener.CombatAnimTrigger _t,
        bool _forceAnim, ref JObject __state)
    {
        __state = null;
        try { RuntimeModelTest observer = krakenProductionAdapterObserver; KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || !ReferenceEquals(__instance, arm.target)) return;
            KrakenProductionAttackWindow attack = KrakenProductionActiveAttack(arm);
            __state = new JObject { { "kind", "CharacterEventListener.CombatTrigger" }, { "trigger", _t.ToString() },
                { "force", _forceAnim }, { "attackWindow", attack == null ? -1 : attack.id },
                { "animator", KrakenProductionCallbackAnimator(arm) }, { "finalized", false } };
            if (attack == null) observer.KrakenProductionRecordStateCallback(arm, __state);
            else observer.KrakenProductionRecordCallback(arm, __state); }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("CombatTrigger prefix", error); }
    }

    static void KrakenProductionFoleyPrefix(CharacterEventListener __instance, string _soundEvent, ref JObject __state)
    {
        __state = null;
        try { RuntimeModelTest observer = krakenProductionAdapterObserver; KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || !ReferenceEquals(__instance, arm.target)) return;
            KrakenProductionAttackWindow attack = KrakenProductionActiveAttack(arm);
            __state = new JObject { { "kind", "CharacterEventListener.Foley" }, { "sound", _soundEvent },
                { "attackWindow", attack == null ? -1 : attack.id }, { "animator", KrakenProductionCallbackAnimator(arm) },
                { "finalized", false } };
            if (attack == null) observer.KrakenProductionRecordStateCallback(arm, __state);
            else observer.KrakenProductionRecordCallback(arm, __state); }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("Foley prefix", error); }
    }

    static void KrakenProductionWeaponPrefix(CharacterEventListener __instance, string _s, ref JObject __state)
    {
        __state = null;
        try { RuntimeModelTest observer = krakenProductionAdapterObserver; KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || !ReferenceEquals(__instance, arm.target)) return;
            KrakenProductionAttackWindow attack = KrakenProductionActiveAttack(arm);
            __state = new JObject { { "kind", "CharacterEventListener.PlayWeaponAnimation" }, { "argument", _s },
                { "attackWindow", attack == null ? -1 : attack.id }, { "finalized", false } };
            observer.KrakenProductionRecordCallback(arm, __state); }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("PlayWeaponAnimation prefix", error); }
    }

    static void KrakenProductionDodgePrefix(CharacterEventListener __instance, ref JObject __state)
    {
        __state = null;
        try { RuntimeModelTest observer = krakenProductionAdapterObserver; KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || !ReferenceEquals(__instance, arm.target)) return;
            KrakenProductionAttackWindow attack = KrakenProductionActiveAttack(arm);
            __state = new JObject { { "kind", "CharacterEventListener.Dodge" }, { "attackWindow", attack == null ? -1 : attack.id }, { "finalized", false } };
            observer.KrakenProductionRecordCallback(arm, __state); }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("Dodge prefix", error); }
    }

    static void KrakenProductionHitPrefix(CharacterEventListener __instance, ref JObject __state)
    {
        __state = null;
        try { RuntimeModelTest observer = krakenProductionAdapterObserver; KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || !ReferenceEquals(__instance, arm.target)) return;
            KrakenProductionAttackWindow attack = KrakenProductionActiveAttack(arm);
            __state = new JObject { { "kind", "CharacterEventListener.AttackHit" }, { "attackWindow", attack == null ? -1 : attack.id }, { "finalized", false } };
            observer.KrakenProductionRecordCallback(arm, __state); }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("AttackHit prefix", error); }
    }

    static void KrakenProductionRespondDodgePrefix(CharacterDummy __instance, bool _mainVictim, ref JObject __state)
    {
        __state = null;
        try { RuntimeModelTest observer = krakenProductionAdapterObserver; KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || !KrakenProductionRelevantVictim(arm, __instance)) return;
            KrakenProductionAttackWindow attack = KrakenProductionActiveAttack(arm);
            __state = KrakenProductionCallbackState("CharacterDummy.RespondToDodge", __instance, _mainVictim);
            __state["attackWindow"] = attack == null ? -1 : attack.id;
            __state["animator"] = KrakenProductionCallbackAnimator(arm);
            if (attack == null) observer.KrakenProductionRecordStateCallback(arm, __state);
            else observer.KrakenProductionRecordCallback(arm, __state); }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("RespondToDodge prefix", error); }
    }

    static void KrakenProductionRespondHitPrefix(CharacterDummy __instance, bool _mainVictim, ref JObject __state)
    {
        __state = null;
        try { RuntimeModelTest observer = krakenProductionAdapterObserver; KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || !KrakenProductionRelevantVictim(arm, __instance)) return;
            KrakenProductionAttackWindow attack = KrakenProductionActiveAttack(arm);
            __state = KrakenProductionCallbackState("CharacterDummy.RespondToHit", __instance, _mainVictim);
            __state["attackWindow"] = attack == null ? -1 : attack.id;
            __state["animator"] = KrakenProductionCallbackAnimator(arm);
            KrakenProductionIncomingAttack incoming = ReferenceEquals(__instance, arm.owner)
                ? KrakenProductionResolveIncomingAttack(arm, __instance.m_DamageInfo) : null;
            if (incoming != null)
            {
                __state["incomingAttackSequence"] = incoming.sequence;
                __state["incomingAttackCheat"] = incoming.cheatType;
                __state["incomingAttackProficiency"] = incoming.proficiency;
                __state["incomingAttackConsumable"] = incoming.consumable;
                incoming.responded = true;
                incoming.entry["respondToHitObserved"] = true;
            }
            if (attack == null) observer.KrakenProductionRecordStateCallback(arm, __state);
            else observer.KrakenProductionRecordCallback(arm, __state); }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("RespondToHit prefix", error); }
    }

    static void KrakenProductionCompletedPrefix(CharacterDummy __instance, ref JObject __state)
    {
        __state = null;
        try { RuntimeModelTest observer = krakenProductionAdapterObserver; KrakenProductionAdapterArm arm = observer == null ? null : observer.krakenProductionAdapterArm;
            if (arm == null || arm.cleared || !ReferenceEquals(__instance, arm.owner)) return;
            KrakenProductionAttackWindow attack = KrakenProductionActiveAttack(arm);
            __state = new JObject { { "kind", "CharacterDummy.ActionCompleted" }, { "attackWindow", attack == null ? -1 : attack.id }, { "finalized", false } };
            __state["animator"] = KrakenProductionCallbackAnimator(arm);
            if (attack != null) { attack.completed = true; observer.KrakenProductionRecordCallback(arm, __state); }
            else observer.KrakenProductionRecordStateCallback(arm, __state); }
        catch (Exception error) { __state = null; KrakenProductionObserverFailure("ActionCompleted prefix", error); }
    }

    static Exception KrakenProductionCallbackFinalizer(Exception __exception, JObject __state)
    {
        return KrakenProductionFinalize(__exception, __state, true, "callback finalizer");
    }

    JObject ArmKrakenProductionAdapter(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "enemyDummyInstanceId", "celInstanceId", "rendererInstanceId", "manifestSha256", "campaignRun");
        if (krakenProductionAdapterArm != null || krakenProductionAdapterObserver != null)
            throw new InvalidOperationException("A Kraken production adapter observation is already armed; clear it first.");
        if (Str(command, "manifestSha256") != GloamfinSkinManifestHash)
            throw new ArgumentException("Exact pinned Gloamfin manifest SHA256 required.");
        int ownerId = KrakenProductionRequiredId(command, "enemyDummyInstanceId");
        int targetId = KrakenProductionRequiredId(command, "celInstanceId");
        int rendererId = KrakenProductionRequiredId(command, "rendererInstanceId");
        string campaignRun = Str(command, "campaignRun");
        string expectedTerminal;
        if (campaignRun == null || !KrakenProductionCampaignRuns.TryGetValue(campaignRun, out expectedTerminal))
            throw new ArgumentException("Exact Kraken campaignRun required.");
        JObject pins = KrakenProductionPins();
        CharacterEventListener resource = KrakenProductionResourcePrefab();
        EnemyDummy owner = KrakenProductionFindOwner(ownerId);
        CharacterEventListener target = owner.m_EventListener;
        if (target == null || target.GetInstanceID() != targetId || !SceneOwner(target) || !target.gameObject.activeInHierarchy ||
            !ReferenceEquals(target.m_Dummy, owner))
            throw new InvalidOperationException("Exact requested EnemyDummy/CEL reciprocal relation unavailable.");
        FTK_enemyCombat row = owner.m_EnemyCombat;
        FTK_enemyCombat native = FTK_enemyCombatDB.GetDB().GetEntryByStringID(KrakenProductionNativeEnemy);
        if (row == null || row.m_ID != KrakenProductionRegisteredEnemy || !ReferenceEquals(row.m_EnemyAsset, resource) ||
            native == null || native.m_EnemyAsset == null || native.m_WeaponAsset == null ||
            !ReferenceEquals(row.m_WeaponAsset, native.m_WeaponAsset))
            throw new InvalidOperationException("Only the exact registered Gloamfin/enkrakenhead route can be observed.");
        SkinnedMeshRenderer renderer = KrakenProductionRenderer(target, rendererId);
        Animator animator = KrakenProductionAnimator(target);
        if (!animator.isInitialized || animator.layerCount != 1 || animator.runtimeAnimatorController == null ||
            animator.runtimeAnimatorController.name != KrakenProductionController || row.m_WeaponAsset == null ||
            !ReferenceEquals(animator.runtimeAnimatorController, row.m_WeaponAsset.m_AnimationController))
            throw new InvalidOperationException("Exact native weapon-installed Kraken controller required.");
        Dictionary<string, AnimationClip> clips = KrakenProductionControllerClips(animator.runtimeAnimatorController);
        Assembly core = CatalogAssembly("FTKModFramework");
        Type adapterType = core.GetType("FTKModFramework.Core.LegacyKrakenResourceAdapterLease", true);
        Component[] adapters = target.GetComponents(adapterType);
        if (adapters == null || adapters.Length != 1 || adapters[0] == null)
            throw new InvalidOperationException("Exactly one LegacyKrakenResourceAdapterLease is required.");
        Component adapter = adapters[0];
        Type samplerType = adapterType.GetNestedType("ClipSampler", BindingFlags.NonPublic);
        if (samplerType == null) throw new InvalidOperationException("Exact nested ClipSampler type unavailable.");
        Type meshOwnerType = core.GetType("FTKModFramework.Core.EnemyMeshResources", true);
        Component meshOwner = target.GetComponent(meshOwnerType);
        if (meshOwner == null) throw new InvalidOperationException("Gloamfin EnemyMeshResources owner unavailable.");
        KrakenProductionAdapterArm arm = new KrakenProductionAdapterArm { session = sessionId, campaignRun = campaignRun,
            expectedTerminal = expectedTerminal,
            repositoryRoot = KrakenProductionRepositoryRoot(root), ownerId = ownerId, targetId = targetId, rendererId = rendererId,
            adapterId = adapter.GetInstanceID(), meshOwnerId = meshOwner.GetInstanceID(), owner = owner, target = target,
            resource = resource, row = row, renderer = renderer, animator = animator, controller = animator.runtimeAnimatorController,
            adapter = adapter, meshOwner = meshOwner, adapterType = adapterType, samplerType = samplerType, meshOwnerType = meshOwnerType,
            clips = clips, pins = pins, lastPins = (JObject)pins.DeepClone(), startedFrame = Time.frameCount };
        arm.modernSampler = KrakenProductionField(adapter, "_modernSampler");
        arm.oldSampler = KrakenProductionField(adapter, "_oldSampler");
        KrakenProductionRequireSamplerTopology(arm, arm.modernSampler, "modern", out arm.modernWatch);
        KrakenProductionRequireSamplerTopology(arm, arm.oldSampler, "old", out arm.oldWatch);
        arm.meshLeaseId = (int)KrakenProductionField(meshOwner, "_leaseId");
        if (arm.meshLeaseId == 0) throw new InvalidOperationException("Gloamfin resource lease ID unavailable.");
        KrakenProductionRequireOwner(arm);
        InstallKrakenProductionAdapterHooks(arm);
        krakenProductionAdapterArm = arm;
        krakenProductionAdapterObserver = this;
        return new JObject { { "ok", true }, { "schema", KrakenProductionSchema }, { "status", "passive-production-adapter-observer-armed" },
            { "route", new JObject { { "topologyGroup", "6a28ac3cf4523c24" }, { "resourcePrefab", KrakenProductionResource },
                { "registeredEnemy", KrakenProductionRegisteredEnemy }, { "nativeEnemy", KrakenProductionNativeEnemy },
                { "rendererPath", KrakenProductionRendererPath },
                { "sourceRendererId", KrakenProductionSourceRendererId }, { "sourceRendererIdKind", "serialized_path_id" },
                { "campaignRun", campaignRun }, { "expectedTerminal", expectedTerminal }, { "liveOwnerInstanceId", ownerId },
                { "liveCelInstanceId", targetId }, { "liveRendererInstanceId", rendererId } } },
            { "pins", pins.DeepClone() }, { "nativeAuthority", "passive callbacks only; no Animator.Play, SetTrigger, or combat invocation" },
            { "limits", new JObject { { "postLateUpdateFrames", KrakenProductionFrameLimit }, { "callbacks", KrakenProductionCallbackLimit },
                { "incomingAttacks", KrakenProductionIncomingAttackLimit },
                { "terminalAuthority", KrakenProductionTerminalAuthorityLimit },
                { "cleanup", KrakenProductionCleanupLimit }, { "attackWindows", KrakenProductionAttackLimit } } } };
    }

    void KrakenProductionAdapterTick()
    {
        KrakenProductionAdapterArm arm = krakenProductionAdapterArm;
        if (arm == null || arm.cleared || arm.overflow) return;
        if (arm.teardownObserved) return;
        try
        {
            bool destroyed = arm.owner == null || arm.target == null || arm.adapter == null;
            if (!destroyed) return;
            if (!arm.teardownStarted)
            {
                arm.teardownStarted = true;
                arm.ownerDestroyedFrame = Time.frameCount;
                KrakenProductionRecordCleanup(arm, new JObject { { "kind", "native-owner-unity-null" },
                    { "ownerUnityNull", arm.owner == null }, { "celUnityNull", arm.target == null },
                    { "adapterUnityNull", arm.adapter == null } });
            }
            IList ownerList = KrakenProductionStaticField(arm.adapterType, "Owners") as IList;
            if (ownerList == null) throw new InvalidOperationException("Legacy adapter owner list unavailable.");
            bool registered = KrakenProductionContains(ownerList, arm.adapter);
            bool modernGraph = arm.modernWatch.graph.IsValid();
            bool oldGraph = arm.oldWatch.graph.IsValid();
            bool modernRootNull = arm.modernWatch.root == null;
            bool oldRootNull = arm.oldWatch.root == null;
            IDictionary table = KrakenProductionLeaseTable(arm.meshOwnerType);
            bool meshLeasePresent = table.Contains(arm.meshLeaseId);
            bool allResourcesNull = true;
            JArray resourceView = new JArray();
            for (int i = 0; i < arm.resources.Count; i++)
            {
                KrakenProductionWatchedResource resource = arm.resources[i];
                bool unityNull = resource.value == null;
                allResourcesNull &= unityNull;
                resourceView.Add(new JObject { { "instanceId", resource.instanceId }, { "name", resource.name },
                    { "type", resource.type }, { "unityNull", unityNull } });
            }
            KrakenProductionRecordCleanup(arm, new JObject { { "kind", "natural-teardown-poll" },
                { "ownerRegistered", registered }, { "modernGraphValid", modernGraph }, { "oldGraphValid", oldGraph },
                { "modernRootUnityNull", modernRootNull }, { "oldRootUnityNull", oldRootNull },
                { "meshLeasePresent", meshLeasePresent }, { "allGloamfinResourcesUnityNull", allResourcesNull },
                { "resources", resourceView } });
            if (!registered && !modernGraph && !oldGraph && modernRootNull && oldRootNull && !meshLeasePresent && allResourcesNull &&
                arm.effectiveAdapterDisposeCalls == 1 && arm.effectiveSamplerDisposeCalls == 2 && arm.effectiveDisableCalls <= 1)
                arm.teardownObserved = true;
            if (!arm.teardownObserved && Time.frameCount - arm.ownerDestroyedFrame > KrakenProductionTeardownFrameLimit)
                throw new InvalidOperationException("Natural Kraken teardown did not complete within bounded frame window.");
        }
        catch (Exception error)
        {
            KrakenProductionFail(arm, "Production adapter observer identity/lifetime failure: " + error.Message, true);
        }
    }

    JObject KrakenProductionAdapterState()
    {
        KrakenProductionAdapterTick();
        KrakenProductionAdapterArm arm = krakenProductionAdapterArm;
        if (arm == null) throw new InvalidOperationException("No Kraken production adapter observer is armed.");
        try
        {
            KrakenProductionCheckPins(arm);
        }
        catch (Exception error)
        {
            KrakenProductionFail(arm, "Production adapter observer pin failure: " + error.Message, true);
        }
        JArray resources = new JArray();
        for (int i = 0; i < arm.resources.Count; i++)
        {
            KrakenProductionWatchedResource resource = arm.resources[i];
            resources.Add(new JObject { { "instanceId", resource.instanceId }, { "name", resource.name }, { "type", resource.type },
                { "unityNull", resource.value == null } });
        }
        JArray attackWindows = new JArray();
        for (int i = 0; i < arm.attacks.Count; i++)
        {
            KrakenProductionAttackWindow attack = arm.attacks[i];
            attackWindows.Add(new JObject { { "id", attack.id }, { "variant", attack.variant }, { "completed", attack.completed },
                { "primary", attack.hasPrimary ? KrakenProductionFid(attack.primary) : null },
                { "secondary", attack.hasSecondary ? KrakenProductionFid(attack.secondary) : null },
                { "tertiary", attack.hasTertiary ? KrakenProductionFid(attack.tertiary) : null } });
        }
        return new JObject { { "ok", arm.error == null && !arm.overflow && !arm.identityDrift }, { "schema", KrakenProductionSchema },
            { "status", arm.teardownObserved ? "natural-teardown-observed" : arm.teardownStarted ? "natural-teardown-pending" : "active" },
            { "error", arm.error == null ? new JValue((object)null) : new JValue(arm.error) }, { "overflow", arm.overflow },
            { "identityDrift", arm.identityDrift }, { "startedFrame", arm.startedFrame }, { "observedFrame", Time.frameCount },
            { "pinsAtArm", arm.pins.DeepClone() }, { "pinsLastChecked", arm.lastPins == null ? null : arm.lastPins.DeepClone() },
            { "route", new JObject { { "topologyGroup", "6a28ac3cf4523c24" }, { "resourcePrefab", KrakenProductionResource },
                { "registeredEnemy", KrakenProductionRegisteredEnemy }, { "nativeEnemy", KrakenProductionNativeEnemy },
                { "rendererPath", KrakenProductionRendererPath },
                { "sourceRendererId", KrakenProductionSourceRendererId }, { "sourceRendererIdKind", "serialized_path_id" },
                { "campaignRun", arm.campaignRun }, { "expectedTerminal", arm.expectedTerminal }, { "ownerInstanceId", arm.ownerId },
                { "celInstanceId", arm.targetId }, { "rendererInstanceId", arm.rendererId }, { "adapterInstanceId", arm.adapterId },
                { "meshLeaseId", arm.meshLeaseId } } },
            { "authority", new JObject { { "attackEvidenceSource", "passive_native_callbacks" }, { "observerOnly", true },
                { "directAnimatorPlayUsed", false }, { "directAnimatorSetTriggerUsed", false }, { "destructiveCleanupUsed", false } } },
            { "frames", arm.frames.DeepClone() }, { "frameCount", arm.frames.Count }, { "nativeCallbacks", arm.callbacks.DeepClone() },
            { "callbackCount", arm.callbacks.Count }, { "nativeStateCallbacks", arm.stateCallbacks.DeepClone() },
            { "stateCallbackCount", arm.stateCallbacks.Count }, { "incomingAttacks", arm.incomingAttackEvents.DeepClone() },
            { "incomingAttackCount", arm.incomingAttackEvents.Count },
            { "terminalAuthority", arm.terminalAuthorityEvents.DeepClone() },
            { "terminalAuthorityCount", arm.terminalAuthorityEvents.Count },
            { "attackWindows", attackWindows }, { "cleanup", arm.cleanup.DeepClone() },
            { "cleanupCounts", new JObject { { "disableForOwner", arm.disableCalls }, { "effectiveDisableForOwner", arm.effectiveDisableCalls },
                { "disposeOwned", arm.adapterDisposeCalls }, { "effectiveDisposeOwned", arm.effectiveAdapterDisposeCalls },
                { "samplerDispose", arm.samplerDisposeCalls }, { "effectiveSamplerDispose", arm.effectiveSamplerDisposeCalls }, { "teardownObserved", arm.teardownObserved },
                { "teardownStarted", arm.teardownStarted }, { "ownerDestroyedFrame", arm.ownerDestroyedFrame },
                { "gloamfinResources", resources } } },
            { "limits", new JObject { { "postLateUpdateFrames", KrakenProductionFrameLimit }, { "callbacks", KrakenProductionCallbackLimit },
                { "incomingAttacks", KrakenProductionIncomingAttackLimit },
                { "terminalAuthority", KrakenProductionTerminalAuthorityLimit },
                { "cleanup", KrakenProductionCleanupLimit }, { "teardownFrames", KrakenProductionTeardownFrameLimit } } } };
    }

    JObject ClearKrakenProductionAdapter()
    {
        KrakenProductionAdapterArm arm = krakenProductionAdapterArm;
        bool existed = arm != null;
        if (arm != null) arm.cleared = true;
        krakenProductionAdapterArm = null;
        if (krakenProductionAdapterObserver == this) krakenProductionAdapterObserver = null;
        return new JObject { { "ok", true }, { "cleared", existed },
            { "note", "Cleared test-only managed observation references. No adapter, sampler, owner, mesh lease, or game object was modified." } };
    }
}
