using System;
using System.IO;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using BepInEx;
using UnityEngine;
using Newtonsoft.Json.Linq;
using HarmonyLib;
using GridEditor;
using FTKHelp;
using System.Security.Cryptography;
using System.Text;

// Deliberately separate test plugin: never ship with the production framework.
[BepInPlugin("com.ftkmf.runtime-model-test", "FTK Runtime Model Test", "0.1.0")]
[BepInDependency("com.ftkmf.model-test-content")]
public sealed partial class RuntimeModelTest : BaseUnityPlugin
{
    const BindingFlags Members = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance;
    const BindingFlags Statics = BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Static;
    string root, output, saveNamespace;
    string sessionId = Guid.NewGuid().ToString("N");
    static string isolatedSavePath;
    float nextPoll;
    bool busy;
    int returningFromInstance,fortifiedMaxHpTarget;
    FTKTutorial quietTutorial;
    bool previousTutorialPrompt,previousTutorialShow;
    readonly Dictionary<int, Mesh> ownedMeshes = new Dictionary<int, Mesh>();
    readonly Dictionary<int, Texture2D> ownedTextures = new Dictionary<int, Texture2D>();
    readonly Dictionary<int, Material[]> ownedMaterials = new Dictionary<int, Material[]>();
    readonly Dictionary<int, Matrix4x4[]> nativeBinds = new Dictionary<int, Matrix4x4[]>();

    void Awake()
    {
        enabled = false;
        if (Environment.GetEnvironmentVariable("FTK_MODEL_TEST") != "1") return;
        root = Path.GetFullPath(Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar);
        string requested = Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT");
        if (string.IsNullOrEmpty(requested) || Path.GetFullPath(requested).TrimEnd(Path.DirectorySeparatorChar) != root
            || new DirectoryInfo(root).Parent.Name != "scratch"
            || (File.GetAttributes(root) & FileAttributes.ReparsePoint) != 0)
        {
            Logger.LogError("MODEL TEST REFUSED: exact isolated root under scratch required, no root symlinks."); return;
        }
        using(SHA256 hash=SHA256.Create())
            saveNamespace = "save-model-test-" + BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(root))).Replace("-", "").ToLowerInvariant().Substring(0,16);
        ApplySaveNamespace();
        isolatedSavePath = Path.Combine(Application.persistentDataPath, saveNamespace);
        Harmony harmony = new Harmony("com.ftkmf.runtime-model-test.saves");
        harmony.Patch(typeof(uiStartGame).GetMethod("GetSavePath", Statics), new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("SavePathPrefix", Statics)));
        harmony.Patch(typeof(uiStartGame).GetMethod("GetSavePathSlash", Statics), new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("SavePathSlashPrefix", Statics)));
        string contentRegistrationRun = CurrentContentRegistrationRun();
        output = Path.Combine(root, "model-test-output");
        Directory.CreateDirectory(output);
        File.WriteAllText(Path.Combine(root,"model-test-session.json"),new JObject{{"session",sessionId},
            {"contentRegistrationRun",contentRegistrationRun},{"savePath",isolatedSavePath}}.ToString());
        Application.runInBackground = true;
        ArmCombatEntryTrace();
        enabled = true;
        Logger.LogInfo("MODEL TEST ACTIVE: root=" + root + "; saveNamespace=" + saveNamespace
            + "; command=model-test-command.json; PlayerPrefs are not modified by this plugin.");
    }

    static bool SavePathPrefix(ref string __result) { __result = isolatedSavePath; return false; }
    static bool SavePathSlashPrefix(ref string __result) { __result = isolatedSavePath + FileSystemHelper.DS; return false; }

    static string CurrentContentRegistrationRun()
    {
        Type contentType = null;
        foreach (Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())
            if (assembly.GetName().Name == "FtkRuntimeModelTestContent")
            {
                contentType = assembly.GetType("RuntimeModelTestContent", true);
                break;
            }
        if (contentType == null) throw new InvalidOperationException("Current model-test content plugin is unavailable.");
        object content = contentType.GetField("instance", Statics).GetValue(null);
        if (content == null) throw new InvalidOperationException("Current model-test content registration did not complete.");
        string run = contentType.GetField("runId", Members).GetValue(content) as string;
        if (string.IsNullOrEmpty(run) || !Regex.IsMatch(run, "^[a-f0-9]{32}$"))
            throw new InvalidOperationException("Current model-test content registration identity is invalid.");
        return run;
    }

    void ApplySaveNamespace()
    {
        typeof(uiStartGame).GetField("SAVE_PATH", Statics).SetValue(null, saveNamespace);
        typeof(uiStartGame).GetField("SAVE_PATH_SLASH", Statics).SetValue(null, saveNamespace + FileSystemHelper.DS);
    }

    static object Instance(Type type)
    {
        PropertyInfo prop = type.GetProperty("Instance", Statics);
        return prop != null ? prop.GetValue(null, null) : type.GetField("Instance", Statics).GetValue(null);
    }
    static void RequireSinglePlayer()
    {
        object logic = Instance(typeof(GameLogic));
        if (logic == null || !(bool)typeof(GameLogic).GetMethod("IsSinglePlayer", Members).Invoke(logic, null))
            throw new InvalidOperationException("An active single-player game is required.");
    }
    static string Str(JObject obj, string key) { return obj[key] == null ? null : (string)obj[key]; }
    static int Int(JObject obj, string key, int fallback) { return obj[key] == null ? fallback : (int)obj[key]; }
    static float Number(JObject obj, string key, float fallback) { return obj[key] == null ? fallback : (float)obj[key]; }
    static string Token(string value)
    {
        if (value == null || !Regex.IsMatch(value, "^[A-Za-z0-9_-]{1,80}$")) throw new ArgumentException("Expected a short alphanumeric id.");
        return value;
    }
    static string AssetName(string value, string suffix)
    {
        if (string.IsNullOrEmpty(value) || value != Path.GetFileName(value) || value.IndexOfAny(new[] {'/', '\\', ':'}) >= 0
            || !value.EndsWith(suffix, StringComparison.OrdinalIgnoreCase)) throw new ArgumentException("Expected bare " + suffix + " filename.");
        return value;
    }
    void Update()
    {
        GuardianFixtureTick();
        NativeCombatFocusTick();
        NativeFightTraceTick();
        CustomLootTick();
        EnemyLifetimeTick();
        SpawnCaptureTick();
        KrakenProductionAdapterTick();
        if (busy || Time.realtimeSinceStartup < nextPoll) return;
        nextPoll = Time.realtimeSinceStartup + 0.2f;
        // Scene/menu code can reassign static fields; keep this isolated namespace before processing commands.
        ApplySaveNamespace();
        string commandPath = Path.Combine(root, "model-test-command.json");
        if (!File.Exists(commandPath)) return;
        string id = null;
        try
        {
            if (new FileInfo(commandPath).Length > 16384) throw new ArgumentException("Command exceeds 16 KiB.");
            JObject command = JObject.Parse(File.ReadAllText(commandPath));
            id = Token(Str(command, "id"));
            string resultPath = Path.Combine(output, id + ".json");
            if (File.Exists(resultPath)) return;
            if(Str(command,"session") != sessionId) throw new ArgumentException("Stale session; read model-test-session.json.");
            string op = Str(command, "op");
            // This diagnostic intentionally runs before the normal game-state
            // guard so it can report why the native Create Game route is not
            // currently eligible.  It only reads the native menu graph.
            if (op == "native-create-character-preflight") Finish(id, NativeCreateCharacterPreflight(command));
            else if (op == "native-create-character-input-state") Finish(id, NativeCreateCharacterInputState(command));
            else
            {
            RequireSinglePlayer();
            if (op == "enemy-arrival-arm") Finish(id, ArmSpawnCapture(command));
            else if (op == "native-party-start") Finish(id, NativePartyStart(command));
            else if (op == "native-party-class") Finish(id, NativePartyClass(command));
            else if(op == "enemy-arrival-state"){CatalogKeys(command,"id","session","op");SpawnPins(true);Finish(id,SpawnCaptureView());}
            else if(op == "enemy-arrival-clear"){CatalogKeys(command,"id","session","op");Finish(id,ClearSpawnCapture());}
            else if (op == "inventory") Finish(id, Inventory(command));
            else if(op == "native-row-portrait-fixture"){busy=true;StartCoroutine(NativeRowPortraitFixture(id,command));}
            else if(op == "portrait-texture-capture") Finish(id,PortraitTextureCapture(command));
            else if(op == "portrait-watch") Finish(id,PortraitWatch(command));
            else if(op == "portrait-watch-state"){CatalogKeys(command,"id","session","op");Finish(id,PortraitWatchState());}
            else if(op == "portrait-watch-stop"){CatalogKeys(command,"id","session","op");Finish(id,PortraitWatchStop());}
            else if(op == "kraken-sample-fixture"){busy=true;StartCoroutine(KrakenSampleFixture(id,command));}
            else if(op == "kraken-adapter-fixture"){busy=true;StartCoroutine(KrakenSampleFixture(id,command,true));}
            else if(op == "kraken-production-adapter-arm") Finish(id,ArmKrakenProductionAdapter(command));
            else if(op == "kraken-production-adapter-state"){CatalogKeys(command,"id","session","op");Finish(id,KrakenProductionAdapterState());}
            else if(op == "kraken-production-adapter-clear"){CatalogKeys(command,"id","session","op");Finish(id,ClearKrakenProductionAdapter());}
            else if(op == "kraken-skin-probe-arm") Finish(id,ArmKrakenSkin(command));
            else if(op == "kraken-skin-probe-stop"){CatalogKeys(command,"id","session","op");Finish(id,StopKrakenSkin());}
            else if(op == "material-lifecycle-fixture"){busy=true;StartCoroutine(MaterialLifecycleFixture(id,command));}
            else if(op == "kraken-controller-fixture"){busy=true;StartCoroutine(KrakenControllerFixture(id,command));}
            else if(op == "playercatalog-preflight"){busy=true;StartCoroutine(CatalogPreflight(id,true));}
            else if(op == "catalog-preflight"){busy=true;StartCoroutine(CatalogPreflight(id));}
            else if(op == "lease-test"){busy=true;StartCoroutine(LeaseTest(id));}
            else if(op == "scale-baseline-test"){busy=true;StartCoroutine(ScaleBaselineTest(id));}
            else if (op == "select-room") Finish(id, SelectRoom(Str(command, "enemy")));
            else if(op == "stage-enemy") Finish(id, StageEnemy(command));
            else if(op == "stage-next-enemy") Finish(id,StageNextEnemy(command));
            else if(op == "ready") Finish(id,ClickReady(command));
            else if(op == "enemy-lifetime-watch") Finish(id,ArmEnemyLifetime(command));
            else if(op == "enemy-lifetime-state"){CatalogKeys(command,"id","session","op");Finish(id,EnemyLifetimeState());}
            else if(op == "enemy-lifetime-clear"){CatalogKeys(command,"id","session","op");Finish(id,ClearEnemyLifetime());}
            else if(op == "entry-preparation-state") Finish(id,EntryPreparationState(command));
            else if(op == "entry-position") Finish(id,EntryPosition(command));
            else if(op == "entry-discover") Finish(id,EntryDiscover(command));
            else if(op == "story-state") Finish(id,StorySetupState(command));
            else if(op == "story-submit") Finish(id,SubmitStorySetup(command));
            else if(op == "native-create-character-screen") StartNativeCreateCharacterScreen(id,command);
            else if(op == "player-preview-state") Finish(id,PlayerPreviewState(command));
            else if(op == "lease-watch-preview") Finish(id,WatchPreviewLease(command));
            else if(op == "lease-watch") Finish(id,WatchLease(command));
            else if(op == "lease-watch-state") Finish(id,WatchLeaseState());
            else if(op == "lease-watch-clear") Finish(id,ClearLeaseWatches());
            else if(op == "equipment-inventory") Finish(id,EquipmentInventory());
            else if(op == "hero-damage-fixture") Finish(id,HeroDamageFixture(command));
            else if(op == "equip-body") Finish(id,ChangeBodyEquipment(command,true));
            else if(op == "unequip-body") Finish(id,ChangeBodyEquipment(command,false));
            else if(op == "trap-state") Finish(id,TrapState(command));
            else if(op == "trap-submit") Finish(id,TrapSubmit(command));
            else if(op == "town-stock-state") Finish(id,TownStockObservation(command));
            else if(op == "dungeon-map-state") Finish(id,DungeonMapObservation(command));
            else if(op == "guardian-damage-fixture") Finish(id,GuardianDamageFixture(command));
            else if(op == "native-combat-focus") Finish(id,NativeCombatFocus(command));
            else if(op == "native-fight-trace") Finish(id,NativeFightTrace(command));
            else if(op == "preview-race") Finish(id,PreviewRaceFixture(command));
            else if(op == "custom-loot-fixture") Finish(id,CustomLootFixture(command));
            else if(op == "player-studio") Finish(id,PlayerStudio(command));
            else if(op == "world-input-state") Finish(id,WorldInputObservation(command));
            else if(op == "guardian-state") Finish(id,GuardianObservation(command));
            else if(op == "guardian-incapacity-fixture") Finish(id,GuardianIncapacityFixture(command));
            else if(op == "fixture-state") Finish(id,FixtureState());
            else if(op == "collect-loot") Finish(id,CollectLoot(command));
            else if(op == "fortify-party") Finish(id,FortifyParty(command));
            else if(op == "return-to-title") Finish(id,ReturnToTitle());
            else if(op == "quiet-tutorials") Finish(id,QuietTutorials(command));
            else if(op == "material-state") Finish(id,ObserveMaterialState(command));
            else if (op == "reload") Finish(id, Reload(command));
            else if (op == "combat-trigger-capture") StartCombatTriggerCapture(id, command);
            else if (op == "capture" || op == "play")
            {
                SkinnedMeshRenderer renderer = Resolve(command);
                float seconds = Number(command, "seconds", 2f), fps = Number(command, "fps", 10f);
                if (seconds <= 0 || seconds > 10 || fps < 1 || fps > 20 || seconds * fps > 120)
                    throw new ArgumentException("Capture limits: 0<seconds<=10, 1<=fps<=20, <=120 frames.");
                int maxWidth=Int(command,"maxWidth",1280);
                bool fixedStep=command["fixedStep"]!=null && (bool)command["fixedStep"];
                if(maxWidth<320 || maxWidth>3840)throw new ArgumentException("maxWidth must be between 320 and 3840.");
                if(fixedStep && fps!=(float)(int)fps)throw new ArgumentException("fixedStep requires integer fps.");
                string studioView=Str(command,"studioView");
                if(studioView!=null && (op!="capture" || Scope(command)!="player-combat" ||
                    (studioView!="front" && studioView!="three-quarter" && studioView!="back")))
                    throw new ArgumentException("studioView requires a player-combat observation capture and front, three-quarter or back view.");
                bool materialObservation=false;
                if(command["materialObservation"]!=null)
                {if(command["materialObservation"].Type!=JTokenType.Boolean)throw new ArgumentException("materialObservation must be boolean.");materialObservation=(bool)command["materialObservation"];}
                if(materialObservation && (Scope(command)!="enemies" || Int(command,"ownerInstanceId",0)==0))throw new ArgumentException("Material capture requires exact enemy owner.");
                bool motionObservation=false;
                if(command["motionObservation"]!=null)
                {if(command["motionObservation"].Type!=JTokenType.Boolean)throw new ArgumentException("motionObservation must be boolean.");motionObservation=(bool)command["motionObservation"];}
                if(motionObservation && (op!="capture" || Scope(command)!="enemies" || Int(command,"ownerInstanceId",0)==0))
                    throw new ArgumentException("Motion capture requires an exact enemy observation capture, not direct state playback.");
                if(materialObservation)MaterialObservation(renderer,true); // Bound/readability preflight before capture or playback.
                if (op == "play") Play(renderer, command);
                CombatMotionArm motion=null;
                try
                {
                    if(motionObservation)motion=ArmCombatMotionObservation(renderer,"ordinary-bridge-action");
                    busy = true;
                    StartCoroutine(Capture(id, renderer, seconds, fps, maxWidth, fixedStep,
                        op == "play" ? "native-state-playback" : "observed-runtime",materialObservation,false,null,motion,studioView));
                }
                catch
                {
                    StopCombatMotionObservation(motion);
                    throw;
                }
            }
            else throw new ArgumentException("Unknown op; use inventory/reload/capture/play/combat-trigger-capture/select-room/stage-enemy/fortify-party/return-to-title/native-create-character-preflight/native-create-character-input-state/native-create-character-screen/quiet-tutorials/lease-test.");
            }
        }
        catch (Exception ex)
        {
            Logger.LogError("MODEL TEST FAILED: " + ex);
            if (id != null) Finish(id, new JObject { { "ok", false }, { "error", ex.ToString() } });
        }
    }

    void Finish(string id, JObject result)
    {
        result["testFixtureMaxHpTarget"] = fortifiedMaxHpTarget == 0 ? new JValue((object)null) : new JValue(fortifiedMaxHpTarget);
        result["tutorialsQuiet"] = quietTutorial != null;
        result["session"] = sessionId; result["id"] = id; result["frame"] = Time.frameCount;
        result["savePath"] = Convert.ToString(typeof(uiStartGame).GetField("SAVE_PATH", Statics).GetValue(null));
        string final = Path.Combine(output,id+".json"), temporary = Path.Combine(output,id+".tmp");
        File.WriteAllText(temporary,result.ToString());
        File.Move(temporary,final);
        Logger.LogInfo("MODEL TEST RESULT: " + id + " ok=" + result["ok"]);
    }
    static EnemyDummy Enemy(SkinnedMeshRenderer renderer) { return renderer.GetComponentInParent<EnemyDummy>(); }
    static string Relative(Transform current, Transform ancestor)
    {
        if(current==ancestor)return ".";
        string path = current.name;
        while (current.parent != null && current.parent != ancestor) { current = current.parent; path = current.name + "/" + path; }
        return path;
    }
    static JArray Vec(Vector3 value) { return new JArray(value.x, value.y, value.z); }
    static JArray Quat(Quaternion value) { return new JArray(value.x, value.y, value.z, value.w); }
    static JArray Matrix(Matrix4x4 value)
    {
        JArray result = new JArray();
        for (int row = 0; row < 4; row++) for (int col = 0; col < 4; col++) result.Add(value[row,col]);
        return result;
    }
    JObject Snapshot(SkinnedMeshRenderer renderer, bool full)
    {
        AvatarOwner avatar=FindOwner(renderer,null);
        Component owner=avatar.owner,cel=avatar.cel;
        CharacterEventListener listener=avatar.cel;
        EnemyDummy enemy=owner as EnemyDummy;
        string celPath=Relative(renderer.transform,cel.transform);
        JObject result = new JObject {
            {"instanceId", renderer.GetInstanceID()}, {"ownerKind",avatar.scope}, {"ownerInstanceId",owner.GetInstanceID()}, {"ownerRootName",owner.name},
            {"enemyInstanceId",enemy==null?0:enemy.GetInstanceID()}, {"enemyRootName",enemy==null?null:enemy.name},
            {"celInstanceId",cel==null?0:cel.GetInstanceID()}, {"celRootName",cel==null?null:cel.name},
            {"celRootLocalScale",cel==null?(JToken)new JValue((object)null):Vec(cel.transform.localScale)}, {"celRelativeRendererPath",celPath},
            {"enemyRelativeRendererPath",enemy==null?null:Relative(renderer.transform,enemy.transform)},
            {"ownerRelativeRendererPath",OwnerPath(renderer,avatar)}, {"rendererKind","SkinnedMeshRenderer"},
            {"rendererPath",OwnerPath(renderer,avatar)}, {"mesh",renderer.sharedMesh == null ? null : renderer.sharedMesh.name},
            {"boneSignature", BoneSignature(renderer)}, {"isVisible",renderer.isVisible}, {"localBoundsCenter",Vec(renderer.localBounds.center)}, {"localBoundsSize",Vec(renderer.localBounds.size)}, {"rootBone",renderer.rootBone == null ? null : renderer.rootBone.name}, {"enabled",renderer.enabled}, {"active",renderer.gameObject.activeInHierarchy}, {"boundsCenter",Vec(renderer.bounds.center)},
            {"boundsSize",Vec(renderer.bounds.size)}, {"rendererLocalToWorld",Matrix(renderer.localToWorldMatrix)},
            {"lastCombatTrigger",listener==null?null:listener.m_LastTrigger.ToString()}
        };
        if(full)
        {
            JArray materials=new JArray();
            foreach(Material material in renderer.sharedMaterials)
            {
                if(material==null){materials.Add(new JValue((object)null));continue;}
                JObject item=new JObject{{"instanceId",material.GetInstanceID()},{"name",material.name},
                    {"shader",material.shader==null?null:material.shader.name},{"emissionKeyword",material.IsKeywordEnabled("_EMISSION")}};
                if(material.HasProperty("_EmissionColor"))
                {Color color=material.GetColor("_EmissionColor");item["emissionColor"]=new JArray(color.r,color.g,color.b,color.a);}
                foreach(string property in new[]{"_MainTex","_EmissionMap"})
                {
                    item[property+"Supported"]=material.HasProperty(property);
                    if(!material.HasProperty(property))continue;
                    Texture texture=material.GetTexture(property);
                    item[property]=texture==null?(JToken)new JValue((object)null):new JObject{{"instanceId",texture.GetInstanceID()},{"name",texture.name}};
                }
                materials.Add(item);
            }
            result["materials"]=materials;
        }
        JArray bones = new JArray(); Transform[] list = renderer.bones;
        Matrix4x4[] binds = renderer.sharedMesh != null ? renderer.sharedMesh.bindposes : new Matrix4x4[0];
        for (int i=0;i<list.Length;i++)
        {
            if(list[i]==null){bones.Add(new JValue((object)null));continue;}
            JObject bone = new JObject {{"name",list[i].name},{"localPosition",Vec(list[i].localPosition)},
                {"localRotation",Quat(list[i].localRotation)},{"localScale",Vec(list[i].localScale)}, {"localToWorld",Matrix(list[i].localToWorldMatrix)}};
            if(full && i<binds.Length) bone["bindposeRowMajor"] = Matrix(binds[i]);
            bones.Add(bone);
        }
        result["bones"] = bones;
        result["resourceLease"]=ReadLease(avatar.cel);
        Transform physicsRoot=listener.m_AnimRoot!=null?listener.m_AnimRoot:listener.transform;
        Rigidbody[] bodies=physicsRoot.GetComponentsInChildren<Rigidbody>(true);
        JArray physics=new JArray();
        for(int bodyIndex=0;bodyIndex<Math.Min(bodies.Length,128);bodyIndex++)
        {
            Rigidbody body=bodies[bodyIndex];
            physics.Add(new JObject{{"path",Relative(body.transform,listener.transform)},{"instanceId",body.GetInstanceID()},
                {"isKinematic",body.isKinematic},{"active",body.gameObject.activeInHierarchy},{"position",Vec(body.position)},
                {"rotation",Quat(body.rotation)},{"velocity",Vec(body.velocity)},{"angularVelocity",Vec(body.angularVelocity)}});
        }
        result["ragdoll"]=new JObject{{"m_DoRagdoll",listener.m_DoRagdoll},
            {"animationRoot",listener.m_AnimRoot==null?null:Relative(listener.m_AnimRoot,listener.transform)},
            {"rigidbodyCount",bodies.Length},{"truncated",bodies.Length>128},{"rigidbodies",physics}};
        try
        {
            Animator animator = Controller(renderer);
            if(animator != null)
            {
                JObject controller = new JObject {{"name",animator.name},{"instanceId",animator.GetInstanceID()},
                    {"enabled",animator.enabled},{"speed",animator.speed},{"cullingMode",animator.cullingMode.ToString()}};
                JArray layers = new JArray();
                for(int i=0;i<animator.layerCount;i++)
                {
                    AnimatorStateInfo state=animator.GetCurrentAnimatorStateInfo(i);
                    JArray playing = new JArray();
                    foreach(AnimatorClipInfo clip in animator.GetCurrentAnimatorClipInfo(i))
                        playing.Add(new JObject{{"name",clip.clip.name},{"weight",clip.weight},{"clipSeconds",clip.clip.length}});
                    layers.Add(new JObject{{"layer",i},{"name",animator.GetLayerName(i)},{"stateHash",state.fullPathHash},
                        {"normalizedTime",state.normalizedTime},{"transition",animator.IsInTransition(i)},{"playing",playing}});
                }
                controller["layers"]=layers;
                if(full && animator.runtimeAnimatorController!=null)
                {
                    controller["controller"]=animator.runtimeAnimatorController.name;
                    JArray clips=new JArray();
                    foreach(AnimationClip clip in animator.runtimeAnimatorController.animationClips)
                        clips.Add(new JObject{{"name",clip.name},{"seconds",clip.length}});
                    controller["clips"]=clips;
                }
                result["animator"]=controller;
            }
        } catch(Exception ex){result["animatorError"]=ex.Message;}
        return result;
    }
    JObject Snapshot(MeshRenderer renderer, bool full)
    {
        AvatarOwner avatar=FindOwner(renderer,null);Component owner=avatar.owner;CharacterEventListener cel=avatar.cel;
        EnemyDummy enemy=owner as EnemyDummy;MeshFilter[] filters=renderer.GetComponents<MeshFilter>();
        MeshFilter filter=filters.Length==1?filters[0]:null;Mesh mesh=filter==null?null:filter.sharedMesh;
        JObject result=new JObject{
            {"instanceId",renderer.GetInstanceID()},{"ownerKind",avatar.scope},{"ownerInstanceId",owner.GetInstanceID()},{"ownerRootName",owner.name},
            {"enemyInstanceId",enemy==null?0:enemy.GetInstanceID()},{"enemyRootName",enemy==null?null:enemy.name},
            {"celInstanceId",cel==null?0:cel.GetInstanceID()},{"celRootName",cel==null?null:cel.name},
            {"celRootLocalScale",cel==null?(JToken)new JValue((object)null):Vec(cel.transform.localScale)},{"celRelativeRendererPath",Relative(renderer.transform,cel.transform)},
            {"enemyRelativeRendererPath",enemy==null?null:Relative(renderer.transform,enemy.transform)},{"ownerRelativeRendererPath",OwnerPath(renderer,avatar)},
            {"rendererPath",OwnerPath(renderer,avatar)},{"rendererKind","MeshRenderer"},{"meshFilterCount",filters.Length},{"meshFilterInstanceId",filter==null?0:filter.GetInstanceID()},
            {"mesh",mesh==null?null:mesh.name},{"isVisible",renderer.isVisible},{"enabled",renderer.enabled},{"active",renderer.gameObject.activeInHierarchy},
            {"boundsCenter",Vec(renderer.bounds.center)},{"boundsSize",Vec(renderer.bounds.size)},{"rendererLocalToWorld",Matrix(renderer.localToWorldMatrix)},
            {"lastCombatTrigger",cel==null?null:cel.m_LastTrigger.ToString()}};
        if(full)
        {
            JArray materials=new JArray();
            foreach(Material material in renderer.sharedMaterials)
            {
                if(material==null){materials.Add(new JValue((object)null));continue;}
                JObject item=new JObject{{"instanceId",material.GetInstanceID()},{"name",material.name},
                    {"shader",material.shader==null?null:material.shader.name},{"emissionKeyword",material.IsKeywordEnabled("_EMISSION")}};
                if(material.HasProperty("_EmissionColor")){Color color=material.GetColor("_EmissionColor");item["emissionColor"]=new JArray(color.r,color.g,color.b,color.a);}
                foreach(string property in new[]{"_MainTex","_EmissionMap"})
                {
                    item[property+"Supported"]=material.HasProperty(property);if(!material.HasProperty(property))continue;
                    Texture texture=material.GetTexture(property);item[property]=texture==null?(JToken)new JValue((object)null):new JObject{{"instanceId",texture.GetInstanceID()},{"name",texture.name}};
                }
                materials.Add(item);
            }
            result["materials"]=materials;
        }
        result["resourceLease"]=ReadLease(cel);
        return result;
    }
    static string BoneSignature(SkinnedMeshRenderer renderer)
    {
        StringBuilder data=new StringBuilder();
        foreach(Transform bone in renderer.bones)data.Append(bone==null?"<null>":bone.name).Append('\n');
        foreach(Matrix4x4 bind in renderer.sharedMesh==null?new Matrix4x4[0]:renderer.sharedMesh.bindposes)
            for(int r=0;r<4;r++)for(int c=0;c<4;c++)data.Append(bind[r,c].ToString("R",System.Globalization.CultureInfo.InvariantCulture)).Append(',');
        using(SHA256 hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(Encoding.UTF8.GetBytes(data.ToString()))).Replace("-","").ToLowerInvariant();
    }
    static void CheckJoints(string path,Transform[] bones)
    {
        Dictionary<string,bool> names=new Dictionary<string,bool>(StringComparer.Ordinal);
        foreach(Transform bone in bones)
        {
            if(bone==null || names.ContainsKey(bone.name))throw new InvalidOperationException("Missing or duplicate runtime bone names.");
            names.Add(bone.name,true);
        }
        byte[] bytes=File.ReadAllBytes(path);
        if(bytes.Length<20 || BitConverter.ToUInt32(bytes,0)!=0x46546c67 || BitConverter.ToUInt32(bytes,16)!=0x4e4f534a)
            throw new InvalidOperationException("Invalid GLB header/JSON chunk.");
        uint count=BitConverter.ToUInt32(bytes,12);
        if(count>bytes.Length-20)throw new InvalidOperationException("Invalid GLB JSON length.");
        JObject doc=JObject.Parse(Encoding.UTF8.GetString(bytes,20,(int)count));
        JArray joints=(JArray)doc["skins"][0]["joints"];
        Dictionary<string,bool> seen=new Dictionary<string,bool>(StringComparer.Ordinal);
        foreach(JToken joint in joints)
        {
            string name=(string)doc["nodes"][(int)joint]["name"];
            if(name==null || !names.ContainsKey(name) || seen.ContainsKey(name))throw new InvalidOperationException("Missing/duplicate GLB joint: "+name);
            seen.Add(name,true);
        }
    }
    sealed class Prepared
    {
        public SkinnedMeshRenderer renderer; public Mesh mesh; public Texture2D texture; public string mode;
        public JObject before;
        public Mesh originalMesh; public Transform[] originalBones;
        public Material[] originalMaterials, materials;
    }
    JObject Reload(JObject command)
    {
        JArray assignments=command["assignments"] as JArray;
        if(assignments==null || assignments.Count<1 || assignments.Count>16)throw new ArgumentException("Provide 1..16 exact renderer assignments.");
        Assembly framework=null;
        foreach(Assembly a in AppDomain.CurrentDomain.GetAssemblies())if(a.GetName().Name=="FTKModFramework")framework=a;
        if(framework==null)throw new InvalidOperationException("Framework unavailable.");
        MethodInfo load=framework.GetType("FTKModFramework.Core.RuntimeGltfMeshLoader",true).GetMethod("LoadSkinnedGlb",Statics,null,new[]{typeof(string),typeof(Transform[]),typeof(Matrix4x4[])},null);
        MethodInfo resolve=framework.GetType("FTKModFramework.Core.CustomModelLoader",true).GetMethod("ResolveModelPath",Statics,null,new[]{typeof(string)},null);
        List<Prepared> prepared=new List<Prepared>();Dictionary<int,bool> selected=new Dictionary<int,bool>();
        try
        {
            foreach(JObject assignment in assignments)
            {
                if(Scope(assignment)!="enemies")throw new ArgumentException("Player scopes are read/capture/play only; no player hot reload.");
                SkinnedMeshRenderer renderer=Resolve(assignment);int id=renderer.GetInstanceID();
                if(selected.ContainsKey(id))throw new ArgumentException("Renderer assigned twice.");selected.Add(id,true);
                string model=AssetName(Str(assignment,"model"),".glb"), textureName=Str(assignment,"texture");
                string mode=Str(assignment,"material")??"preserve";
                if(mode!="preserve" && mode!="neutral")throw new ArgumentException("material must be preserve or neutral.");
                string modelPath=(string)resolve.Invoke(null,new object[]{model});
                CheckJoints(modelPath,renderer.bones);
                if(!nativeBinds.ContainsKey(id))nativeBinds[id]=renderer.sharedMesh.bindposes;
                Prepared item=new Prepared{renderer=renderer,mode=mode,before=Snapshot(renderer,true),originalMesh=renderer.sharedMesh,
                    originalBones=renderer.bones,originalMaterials=renderer.sharedMaterials};prepared.Add(item);
                item.mesh=(Mesh)load.Invoke(null,new object[]{model,renderer.bones,nativeBinds[id]});
                if(item.mesh==null)throw new InvalidOperationException("GLB load failed; all current renderers retained.");
                if(textureName!=null)
                {
                    AssetName(textureName,".png");item.texture=new Texture2D(2,2);
                    if(!item.texture.LoadImage(File.ReadAllBytes((string)resolve.Invoke(null,new object[]{textureName}))))
                        throw new InvalidOperationException("PNG decode failed.");
                }
                item.materials=new Material[item.originalMaterials.Length];
                for(int materialIndex=0;materialIndex<item.originalMaterials.Length;materialIndex++)
                {
                    Material original=item.originalMaterials[materialIndex];
                    if(original==null)continue;
                    Material material=new Material(original);item.materials[materialIndex]=material;
                    if(item.texture!=null){material.mainTexture=item.texture;if(material.HasProperty("_EmissionMap"))material.SetTexture("_EmissionMap",item.texture);}
                    if(item.mode=="neutral"){material.DisableKeyword("_EMISSION");if(material.HasProperty("_EmissionColor"))material.SetColor("_EmissionColor",Color.black);}
                }
            }
        }
        catch
        {
            foreach(Prepared item in prepared)DestroyPrepared(item);
            throw;
        }
        JArray result=new JArray();
        try
        {
            foreach(Prepared item in prepared)
            {
                item.renderer.sharedMesh=item.mesh;item.renderer.bones=(Transform[])item.originalBones.Clone();
                item.renderer.sharedMaterials=item.materials;
                result.Add(new JObject{{"before",item.before},{"after",Snapshot(item.renderer,true)}});
            }
        }
        catch
        {
            // Keep prior owned assets alive until every assignment and evidence snapshot commits.
            foreach(Prepared item in prepared)
            {
                try{if(item.renderer!=null){item.renderer.sharedMesh=item.originalMesh;item.renderer.bones=item.originalBones;item.renderer.sharedMaterials=item.originalMaterials;}}
                catch(Exception rollback){Logger.LogError("MODEL TEST ROLLBACK FAILED: "+rollback);}
            }
            foreach(Prepared item in prepared)DestroyPrepared(item);
            throw;
        }
        foreach(Prepared item in prepared)
        {
            int id=item.renderer.GetInstanceID();
            if(ownedMeshes.ContainsKey(id))UnityEngine.Object.Destroy(ownedMeshes[id]);ownedMeshes[id]=item.mesh;
            if(ownedMaterials.ContainsKey(id))foreach(Material material in ownedMaterials[id])if(material!=null)UnityEngine.Object.Destroy(material);
            ownedMaterials[id]=item.materials;
            if(item.texture!=null){if(ownedTextures.ContainsKey(id))UnityEngine.Object.Destroy(ownedTextures[id]);ownedTextures[id]=item.texture;}
        }
        return new JObject{{"ok",true},{"assignments",result}};
    }
    static void DestroyPrepared(Prepared item)
    {
        if(item.mesh!=null)UnityEngine.Object.Destroy(item.mesh);
        if(item.texture!=null)UnityEngine.Object.Destroy(item.texture);
        if(item.materials!=null)foreach(Material material in item.materials)if(material!=null)UnityEngine.Object.Destroy(material);
    }
    void Play(SkinnedMeshRenderer renderer,JObject command)
    {
        Animator animator=Controller(renderer);
        if(animator==null)throw new InvalidOperationException("Animator missing.");
        int layer=Int(command,"layer",0);string state=Str(command,"state");
        if(layer<0 || layer>=animator.layerCount || string.IsNullOrEmpty(state))throw new ArgumentException("Exact animator state and valid layer required.");
        int hash=Animator.StringToHash(state);
        if(!animator.HasState(layer,hash))throw new InvalidOperationException("No exact state '"+state+"'; clip names are not state names.");
        animator.Play(hash,layer,0f);
        Logger.LogInfo("MODEL TEST PLAYBACK: "+state+"; native controller state, not a normal combat action; animation events remain active.");
    }
    IEnumerator Capture(string id,SkinnedMeshRenderer renderer,float seconds,float fps,int maxWidth,bool fixedStep,string provenance,bool materialObservation=false,bool arrivalObservation=false,JObject combatTrigger=null,CombatMotionArm motion=null,string studioView=null)
    {
        int previousCaptureFramerate=Time.captureFramerate;
        Texture2D screen=null,image=null;RenderTexture downsample=null;
        try
        {
            AvatarOwner captureOwner=FindOwner(renderer,null);
            int capturedOwnerId=captureOwner.owner.GetInstanceID(),capturedCelId=captureOwner.cel.GetInstanceID();
            Vector3 studioForward=Vector3.zero;float studioMinimumSpan=0f;
            if(studioView!=null)
            {
                studioForward=Vector3.ProjectOnPlane(captureOwner.cel.transform.forward,Vector3.up);
                if(studioForward.sqrMagnitude<.01f)throw new InvalidOperationException("Usable combat studio facing unavailable.");
                studioForward.Normalize();
            }
            if(fixedStep)Time.captureFramerate=(int)fps;
            string directory=Path.Combine(output,id);Directory.CreateDirectory(directory);
            JArray frames=new JArray();if(arrivalObservation)spawnCapture.partialFrames=frames;float started=Time.realtimeSinceStartup,gameStarted=Time.time,unscaledStarted=Time.unscaledTime;
            float next=started;string error=null;
            string expectedTrigger=combatTrigger==null?null:(string)combatTrigger["expectedLastTrigger"];
            int width=Math.Min(maxWidth,Screen.width),height=Math.Max(1,(int)Math.Round(Screen.height*(double)width/Screen.width));
            // Unity 2017 lacks CaptureScreenshotIntoRenderTexture. Read the final framebuffer
            // (including UI), then GPU-downsample before encoding only the smaller RGB image.
            screen=new Texture2D(Screen.width,Screen.height,TextureFormat.RGB24,false);
            image=new Texture2D(width,height,TextureFormat.RGB24,false);
            downsample=RenderTexture.GetTemporary(width,height,0,RenderTextureFormat.ARGB32);
            int count=(int)Math.Ceiling(seconds*fps);
            for(int index=0;index<count;index++)
            {
                if(!fixedStep)while(Time.realtimeSinceStartup<next)yield return null;
                yield return new WaitForEndOfFrame();
                try
                {
                    RequireSinglePlayer();if(renderer==null)throw new InvalidOperationException("Renderer destroyed during capture.");
                    if(CheckCombatMotionObservation(motion,renderer))
                    {error="Controller resolution unavailable after observed native Death.";break;}
                    if(Screen.width!=screen.width || Screen.height!=screen.height)throw new InvalidOperationException("Screen size changed during capture; rerun at stable resolution.");
                    AvatarOwner currentOwner=FindOwner(renderer,null);
                    if(currentOwner.owner.GetInstanceID()!=capturedOwnerId || currentOwner.cel.GetInstanceID()!=capturedCelId)
                        throw new InvalidOperationException("Avatar owner changed during capture; request a new inventory.");
                    if(expectedTrigger!=null && currentOwner.cel.m_LastTrigger.ToString()!=expectedTrigger)
                        throw new InvalidOperationException("Native combat trigger changed during capture; request a fresh fixture.");
                    // Sample pose/time before readback and PNG work so all evidence describes this frame.
                    JObject pose=Snapshot(renderer,false);if(motion!=null)pose["motionAnimator"]=MotionAnimatorState(motion.animator);if(materialObservation)pose["materialObservation"]=MaterialObservation(renderer,index==0,"end-of-frame-before-png-readback");pose["frame"]=Time.frameCount;
                    if(arrivalObservation){SpawnPins(false);if(spawnCapture.error!=null)throw new InvalidOperationException(spawnCapture.error);pose["arrivalObservation"]=SpawnNativeAttack((EnemyDummy)currentOwner.owner);pose["nativeEventCount"]=spawnCapture.attacks.Count;}
                    pose["seconds"]=Time.realtimeSinceStartup-started;
                    pose["realtimeSeconds"]=Time.realtimeSinceStartup-started;
                    pose["gameSeconds"]=Time.time-gameStarted;pose["unscaledSeconds"]=Time.unscaledTime-unscaledStarted;
                    pose["deltaTime"]=Time.deltaTime;pose["timeScale"]=Time.timeScale;pose["captureFramerate"]=Time.captureFramerate;
                    RenderTexture previousActive=RenderTexture.active;
                    try
                    {
                        RenderTexture.active=null;
                        screen.ReadPixels(new Rect(0,0,screen.width,screen.height),0,0);screen.Apply(false);
                        Graphics.Blit(screen,downsample);RenderTexture.active=downsample;
                        image.ReadPixels(new Rect(0,0,width,height),0,0);image.Apply(false);
                    }
                    finally {RenderTexture.active=previousActive;}
                    File.WriteAllBytes(Path.Combine(directory,index.ToString("D4")+".png"),image.EncodeToPNG());
                    if(studioView!=null)
                    {
                        if(currentOwner.scope!="player-combat")throw new InvalidOperationException("Studio capture owner left combat scope.");
                        pose["studio"]=RenderPlayerStudioAvatar(currentOwner.cel,
                            Path.Combine(directory,index.ToString("D4")+"-studio.png"),studioView,studioForward,studioMinimumSpan);
                        studioMinimumSpan=Math.Max(studioMinimumSpan,(float)pose["studio"]["framingSpan"]);
                    }
                    frames.Add(pose);
                    if(arrivalObservation && spawnCapture.firstPngFrame<0)spawnCapture.firstPngFrame=Time.frameCount;
                }catch(Exception ex){error=ex.ToString();}
                if(error!=null)break;
                next=started+(index+1)/fps;
            }
            if(arrivalObservation){try{SpawnPins(true);if(spawnCapture.error!=null)throw new InvalidOperationException(spawnCapture.error);}catch(Exception e){if(error==null)error=e.ToString();}}
            JObject captureResult=new JObject{{"ok",error==null},{"error",error},{"provenance",provenance},
                {"timingMode",fixedStep?"offline-fixed-step-gameplay":"realtime-observation"},{"fixedStep",fixedStep},
                {"scope",captureOwner.scope},{"ownerInstanceId",capturedOwnerId},{"celInstanceId",capturedCelId},
                {"previousCaptureFramerate",previousCaptureFramerate},{"requestedSeconds",seconds},{"requestedFps",fps},
                {"width",width},{"height",height},{"frames",frames}};
            if(studioView!=null)captureResult["studioView"]=studioView;
            if(materialObservation)captureResult["materialObservation"]=true;
            if(arrivalObservation){spawnCapture.status="capture-finished";spawnCapture.terminal=true;if(error!=null && spawnCapture.error==null)spawnCapture.error=error;captureResult["arrivalObservation"]=true;captureResult["arrival"]=SpawnCaptureView();}
            if(combatTrigger!=null)captureResult["combatTrigger"]=combatTrigger.DeepClone();
            if(motion!=null)captureResult["motionObservation"]=CombatMotionObservationView(motion);
            Finish(id,captureResult);
        }
        finally
        {
            Time.captureFramerate=previousCaptureFramerate;
            if(downsample!=null)RenderTexture.ReleaseTemporary(downsample);
            if(screen!=null)UnityEngine.Object.Destroy(screen);
            if(image!=null)UnityEngine.Object.Destroy(image);
            StopCombatMotionObservation(motion);
            busy=false;
        }
    }
    static void RequireOutsideCombat()
    {
        object session=Instance(typeof(EncounterSession)),master=Instance(typeof(EncounterSessionMC));
        if((session!=null && (bool)typeof(EncounterSession).GetField("m_IsInCombat",Members).GetValue(session))
            || (master!=null && (bool)typeof(EncounterSessionMC).GetField("m_IsInCombat",Members).GetValue(master)))
            throw new InvalidOperationException("Fixture operation requires both encounter sessions outside combat.");
    }
    static JObject HeroHealth(CharacterStats stats,CharacterOverworld cow)
    {
        object fid=typeof(CharacterOverworld).GetField("m_FTKPlayerID",Members).GetValue(cow);
        return new JObject{{"heroInstanceId",cow.GetInstanceID()},{"turnIndex",Convert.ToInt32(fid.GetType().GetField("m_TurnIndex",Members).GetValue(fid))},
            {"photonId",Convert.ToInt32(fid.GetType().GetField("m_PhotonID",Members).GetValue(fid))},
            {"hp",stats.m_HealthCurrent},{"maxHp",stats.MaxHealth},{"augmentedMaxHp",stats.m_AugmentedMaxHealth}};
    }
    static float AugmentedSkill(CharacterStats stats,FTK_weaponStats2.SkillType skill)
    {
        switch(skill)
        {
            case FTK_weaponStats2.SkillType.awareness:return stats.m_AugmentedAwareness;
            case FTK_weaponStats2.SkillType.fortitude:return stats.m_AugmentedFortitude;
            case FTK_weaponStats2.SkillType.quickness:return stats.m_AugmentedQuickness;
            case FTK_weaponStats2.SkillType.talent:return stats.m_AugmentedTalent;
            case FTK_weaponStats2.SkillType.toughness:return stats.m_AugmentedToughness;
            case FTK_weaponStats2.SkillType.vitality:return stats.m_AugmentedVitality;
            case FTK_weaponStats2.SkillType.luck:return stats.m_AugmentedLuck;
            default:throw new InvalidOperationException("Equipped weapon uses an unsupported attack skill: "+skill+".");
        }
    }
    static FTK_weaponStats2.SkillType EquippedAttackSkill(CharacterOverworld cow,out FTK_itembase.ID item)
    {
        if(cow.m_PlayerInventory==null)throw new InvalidOperationException("Hero inventory is unavailable.");
        ItemContainer right=cow.m_PlayerInventory.Get(PlayerInventory.ContainerID.RightHand);
        if(right==null || right.m_ItemCounts==null)throw new InvalidOperationException("Native RightHand inventory is unavailable.");
        int found=0;item=FTK_itembase.ID.None;
        foreach(KeyValuePair<FTK_itembase.ID,int> entry in right.m_ItemCounts)
        {
            if(entry.Value<0)throw new InvalidOperationException("Invalid native RightHand item count.");
            if(entry.Value==0)continue;
            if(entry.Value!=1 || ++found!=1)throw new InvalidOperationException("Expected exactly one equipped RightHand weapon.");
            item=entry.Key;
        }
        if(found!=1)throw new InvalidOperationException("Expected exactly one equipped RightHand weapon.");
        FTK_weaponStats2 weapon=FTK_itembase.GetItemBase(item)as FTK_weaponStats2;
        if(weapon==null || !weapon.m_IsWeapon)throw new InvalidOperationException("Equipped RightHand item is not a native weapon row.");
        FTK_weaponStats2.SkillType skill=weapon._skilltest;
        AugmentedSkill(cow.m_CharacterStats,skill);
        return skill;
    }
    static JObject AttackSkillView(CharacterStats stats,CharacterOverworld cow,FTK_weaponStats2.SkillType skill,FTK_itembase.ID item,float applied)
    {
        return new JObject{{"weaponItemId",(int)item},{"weaponItem",item.ToString()},{"skill",skill.ToString()},
            {"rawValue",stats.GetRawSkillValue(skill)},{"effectiveNoFocusValue",stats.GetSkillValue(skill,false)},
            {"augmentedValue",AugmentedSkill(stats,skill)},{"nativeStatCap",GameFlow.Instance.m_MaxCharacterStat},
            {"augmentationApplied",applied},{"spentFocus",stats.SpentFocus}};
    }
    JObject FortifyParty(JObject command)
    {
        bool readyFixture=false;
        try{RequireOutsideCombat();}
        catch(InvalidOperationException)
        {
            if(command["allowReady"]==null || !(bool)command["allowReady"])throw;
            RequireReadyPreparation();readyFixture=true;
        }
        int target=Int(command,"targetMaxHp",999);
        if(target<1 || target>999)throw new ArgumentException("targetMaxHp must be 1..999.");
        bool capAttackSkill=command["capEquippedAttackSkill"]!=null && (bool)command["capEquippedAttackSkill"];
        object hub=Instance(typeof(FTKHub));IEnumerable party=typeof(FTKHub).GetField("m_CharacterOverworlds",Members).GetValue(hub)as IEnumerable;
        if(party==null)throw new InvalidOperationException("No live party; start a fresh disposable run.");
        List<CharacterOverworld> heroes=new List<CharacterOverworld>();List<CharacterStats> statsList=new List<CharacterStats>();
        List<FTK_weaponStats2.SkillType> attackSkills=new List<FTK_weaponStats2.SkillType>();List<FTK_itembase.ID> attackItems=new List<FTK_itembase.ID>();JArray before=new JArray();
        foreach(object item in party)
        {
            CharacterOverworld cow=item as CharacterOverworld;if(cow==null)continue;
            CharacterStats stats=typeof(CharacterOverworld).GetField("m_CharacterStats",Members).GetValue(cow)as CharacterStats;
            if(stats==null || stats.m_HealthCurrent<=0)throw new InvalidOperationException("Every hero must be alive; defeated runs require return-to-title and a fresh start_run.");
            JObject view=HeroHealth(stats,cow);FTK_weaponStats2.SkillType attackSkill=default(FTK_weaponStats2.SkillType);FTK_itembase.ID attackItem=FTK_itembase.ID.None;
            if(capAttackSkill)
            {
                if(stats.SpentFocus!=0)throw new InvalidOperationException("Attack-skill fixture requires zero spent focus.");
                attackSkill=EquippedAttackSkill(cow,out attackItem);
                view["attackSkill"]=AttackSkillView(stats,cow,attackSkill,attackItem,0f);
            }
            heroes.Add(cow);statsList.Add(stats);attackSkills.Add(attackSkill);attackItems.Add(attackItem);before.Add(view);
        }
        if(heroes.Count==0)throw new InvalidOperationException("No living party.");
        JArray after=new JArray();
        for(int i=0;i<statsList.Count;i++)
        {
            CharacterStats stats=statsList[i];int delta=Math.Max(0,target-stats.MaxHealth);
            if(delta>0)stats.AugmentCharacterOther(FTK_miniEncounter.TrainerType.MaxHP,delta);
            stats.SetSpecificHealth(stats.MaxHealth,false);
            JObject view=HeroHealth(stats,heroes[i]);
            if(capAttackSkill)
            {
                FTK_weaponStats2.SkillType skill=attackSkills[i];float raw=stats.GetRawSkillValue(skill),cap=GameFlow.Instance.m_MaxCharacterStat;
                float augmentation=Math.Max(0f,cap-raw);
                if(augmentation>0f)stats.AugmentCharacterStat(skill,augmentation);
                float effective=stats.GetSkillValue(skill,false);
                if(Math.Abs(effective-cap)>0.011f || stats.SpentFocus!=0)
                    throw new InvalidOperationException("Equipped attack skill did not reach the native no-focus stat cap.");
                view=HeroHealth(stats,heroes[i]);view["attackSkill"]=AttackSkillView(stats,heroes[i],skill,attackItems[i],augmentation);
            }
            after.Add(view);
        }
        fortifiedMaxHpTarget=target;
        return new JObject{{"ok",true},{"provenance",capAttackSkill
                ?"boosted-HP and native-capped equipped attack skill in a disposable test fixture; zero-focus combat actions remain native, balance is not representative"
                :"boosted-HP disposable test fixture; combat actions remain native, balance is not representative"},
            {"targetMaxHp",target},{"capEquippedAttackSkill",capAttackSkill},{"nativeReadyFixture",readyFixture},{"before",before},{"after",after}};
    }
    JObject ReturnToTitle()
    {
        GameLogic logic=Instance(typeof(GameLogic))as GameLogic;
        if(logic==null)throw new InvalidOperationException("GameLogic unavailable.");
        int instance=logic.GetInstanceID();
        if(returningFromInstance==instance)throw new InvalidOperationException("Return to title already requested for this GameLogic instance.");
        RestoreTutorials();fortifiedMaxHpTarget=0;returningFromInstance=instance;
        logic.RestartGameRT();
        return new JObject{{"ok",true},{"status","requested"},{"method","GameLogic.RestartGameRT"},
            {"note","Native realtime fade/reset requested; wait for menu before start_run. This is not confirmation of completion and does not call SaveAndQuit."}};
    }
    static JObject TutorialState(FTKTutorial tutorial)
    {
        return new JObject{{"instanceId",tutorial.GetInstanceID()},{"prompt",tutorial.m_IsPromptTutorial},
            {"show",tutorial.m_IsShowTutorial},{"currentFlasher",tutorial.m_CurrentFlasher==null?null:tutorial.m_CurrentFlasherType.ToString()},
            {"timeScale",Time.timeScale}};
    }
    void RestoreTutorials()
    {
        if(quietTutorial!=null){quietTutorial.m_IsPromptTutorial=previousTutorialPrompt;quietTutorial.m_IsShowTutorial=previousTutorialShow;}
        quietTutorial=null;
    }
    void OnDestroy(){CustomLootRemoveHook();PreviewRaceCleanup();NativeFightDisarm();GuardianFixtureRemoveHooks();if(combatEntryObserver==this)combatEntryObserver=null;enemyLifetime=null;if(spawnCaptureObserver==this)spawnCaptureObserver=null;ClearCombatMotionObservation();ClearKrakenProductionAdapter();if(entryTicket!=null)entryTicket.valid=false;krakenSkinArm=null;portraitArmed=false;portraitTrace.Clear();if(portraitObserver==this)portraitObserver=null;RestoreTutorials();watchedLeases.Clear();}
    JObject QuietTutorials(JObject command)
    {
        FTKTutorial tutorial=FTKTutorial.Instance;if(tutorial==null)throw new InvalidOperationException("Tutorial manager unavailable.");
        JObject before=TutorialState(tutorial);bool release=command["release"]!=null && (bool)command["release"];
        if(release)RestoreTutorials();
        else
        {
            if(quietTutorial!=tutorial){RestoreTutorials();quietTutorial=tutorial;previousTutorialPrompt=tutorial.m_IsPromptTutorial;previousTutorialShow=tutorial.m_IsShowTutorial;}
            tutorial.m_IsPromptTutorial=false;tutorial.m_IsShowTutorial=false;
            // EndGame may drive reset/victory: never close or suppress its existing flasher.
            if(tutorial.m_CurrentFlasher!=null && tutorial.m_CurrentFlasherType!=FTKTutorial.Type.EndGame)tutorial.CloseCurrentTutorial();
        }
        return new JObject{{"ok",true},{"provenance","session tutorial flags only; no PlayerPrefs writes or manual timeScale changes"},
            {"released",release},{"before",before},{"after",TutorialState(tutorial)}};
    }
    JObject StageEnemy(JObject command)
    {
        string enemy=Str(command,"enemy"),companion=Str(command,"companionEnemy");
        if(string.IsNullOrEmpty(enemy))throw new ArgumentException("Exact enemy row ID required.");
        if(companion==enemy)throw new ArgumentException("Companion enemy must use a distinct exact row ID.");
        RequireOutsideCombat();
        object database=typeof(FTK_enemyCombatDB).GetMethod("GetDB",Statics).Invoke(null,null);
        MethodInfo get=database.GetType().GetMethod("GetEntryByStringID",Members,null,new[]{typeof(string)},null);
        object row=get.Invoke(database,new object[]{enemy});
        if(row==null || row.GetType().GetField("m_EnemyAsset",Members).GetValue(row)==null
            || row.GetType().GetField("m_WeaponAsset",Members).GetValue(row)==null)
            throw new InvalidOperationException("Enemy row needs valid native enemy and weapon assets.");
        string verifiedId=Convert.ToString(row.GetType().GetField("m_ID",Members).GetValue(row));
        if(verifiedId!=enemy)throw new InvalidOperationException("DB lookup returned a different row.");
        string companionId=null;
        if(!string.IsNullOrEmpty(companion))
        {
            object companionRow=get.Invoke(database,new object[]{companion});
            if(companionRow==null || companionRow.GetType().GetField("m_EnemyAsset",Members).GetValue(companionRow)==null
                || companionRow.GetType().GetField("m_WeaponAsset",Members).GetValue(companionRow)==null)
                throw new InvalidOperationException("Companion row needs valid native enemy and weapon assets.");
            companionId=Convert.ToString(companionRow.GetType().GetField("m_ID",Members).GetValue(companionRow));
            if(companionId!=companion)throw new InvalidOperationException("Companion DB lookup returned a different row.");
        }
        object flow=Instance(typeof(GameFlow));object dungeon=typeof(GameFlow).GetField("m_DungeonEntered",Members).GetValue(flow);
        if(dungeon==null)throw new InvalidOperationException("Enter a disposable dungeon first.");
        MiniHexDungeon nativeDungeon=(MiniHexDungeon)dungeon;
        bool regenerate=command["regenerate"]!=null && (bool)command["regenerate"];
        bool followingCombat=command["followingCombat"]!=null && (bool)command["followingCombat"];
        if(followingCombat && !regenerate)throw new ArgumentException("Following combat staging requires a fresh generated fixture.");
        FTKRandom random=nativeDungeon.m_DungeonRandom ?? new FTKRandom();
        // Generate/preflight locally before changing the selected room. This whole method
        // runs in one Update, so camera/FSM callbacks cannot interleave with selection.
        Dictionary<int,List<MiniHexDungeon.RoomInfo>> generated=regenerate
            ? MiniHexDungeon.GenerateDungeonEncounters(nativeDungeon,random) : nativeDungeon.m_DungeonEncounters;
        IDictionary levels=generated;
        int level=Int(command,"level",-1),room=Int(command,"room",-1);
        if(levels==null || !levels.Contains(level))throw new ArgumentException("Explicit existing generated level required.");
        IList rooms=levels[level]as IList;
        if(rooms==null || room<0 || room>=rooms.Count)throw new ArgumentException("Explicit valid room index required.");
        if(followingCombat)
        {
            // Preflight both slots before assigning either; preserve native exits and stairs.
            int definitionRooms=nativeDungeon.GetRoomCount(level);
            if(room+1>=rooms.Count || room+1>=definitionRooms)throw new ArgumentException("Two existing nonterminal room slots required.");
            for(int index=room;index<=room+1;index++)
            {
                MiniHexDungeon.RoomInfo slot=rooms[index]as MiniHexDungeon.RoomInfo;
                if(slot==null || slot.m_Type==MiniHexDungeon.EncounterType.Stair
                    || slot.m_Type==MiniHexDungeon.EncounterType.ExitRoom
                    || slot.m_Type==MiniHexDungeon.EncounterType.Cleared)
                    throw new ArgumentException("Following combat staging cannot replace a transition or terminal room.");
            }
        }
        string[] enemies=companionId==null?new[]{verifiedId}:new[]{verifiedId,companionId};
        MiniHexDungeon.RoomInfo replacement=new MiniHexDungeon.RoomInfo(MiniHexDungeon.EncounterType.Enemy,null,enemies,-1);
        rooms[room]=replacement;
        if(followingCombat)rooms[room+1]=new MiniHexDungeon.RoomInfo(MiniHexDungeon.EncounterType.Enemy,null,(string[])enemies.Clone(),-1);
        if(regenerate){nativeDungeon.m_DungeonRandom=random;nativeDungeon.m_DungeonEncounters=generated;}
        nativeDungeon.m_Level=level;nativeDungeon.m_RoomIndex=room;
        return new JObject{{"ok",true},{"enemy",verifiedId},{"companionEnemy",companionId},{"enemies",new JArray(enemies)},
            {"level",level},{"room",room},{"regenerated",regenerate},{"followingCombat",followingCombat},
            {"note","Disposable generated-room substitution, native enemy assets; invoke normal dungeon_encounter only if native flow has not started. No forced acknowledgment."}};
    }
    JObject SelectRoom(string enemy)
    {
        if(string.IsNullOrEmpty(enemy))throw new ArgumentException("Exact enemy ID required.");
        object session=Instance(typeof(EncounterSession));
        if(session!=null && (bool)typeof(EncounterSession).GetField("m_IsInCombat",Members).GetValue(session))throw new InvalidOperationException("Cannot select room during combat.");
        object flow=Instance(typeof(GameFlow));object dungeon=typeof(GameFlow).GetField("m_DungeonEntered",Members).GetValue(flow);
        if(dungeon==null)throw new InvalidOperationException("Enter dungeon first.");
        IDictionary levels=dungeon.GetType().GetField("m_DungeonEncounters",Members).GetValue(dungeon)as IDictionary;
        if(levels==null || levels.Count==0)throw new InvalidOperationException("Generate rooms with dungeon_regen first.");
        foreach(DictionaryEntry level in levels)
        {
            IList rooms=level.Value as IList;
            for(int i=0;rooms!=null && i<rooms.Count;i++)
            {
                IList objects=rooms[i].GetType().GetField("m_EncounterObjects",Members).GetValue(rooms[i])as IList;
                if(objects==null)continue;
                foreach(object value in objects)if(Convert.ToString(value)==enemy)
                {
                    dungeon.GetType().GetField("m_Level",Members).SetValue(dungeon,Convert.ToInt32(level.Key));
                    dungeon.GetType().GetField("m_RoomIndex",Members).SetValue(dungeon,i);
                    return new JObject{{"ok",true},{"level",Convert.ToInt32(level.Key)},{"room",i},{"enemy",enemy},{"note","Synthetic room selection; wait for native flow, never force early acknowledgment."}};
                }
            }
        }
        throw new InvalidOperationException("Enemy absent from existing generated rooms.");
    }
}
