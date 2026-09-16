using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using GridEditor;
using HarmonyLib;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class SpawnCaptureArm
    {
        internal string id,captureId,session,ownedRoot,catalogHash,path,status="armed",error,nativeInitException;
        internal float armedRealtime;internal int armedFrame,level,room,initFrame,queuedCount,launchFrame=-1,firstPngFrame=-1,initPrefixFrame=-1,previousCelId;
        internal FTK_enemyCombat row;internal CharacterEventListener prefab;internal MiniHexDungeon dungeon;internal MiniHexDungeon.RoomInfo roomInfo;
        internal EnemyDummy candidate;internal CharacterEventListener initializedCel;internal JObject profile,assets,core,helper,gameAssembly,ready,prelaunch,initializedIdentity,content,initialSnapshot;internal JArray party,attacks=new JArray(),partialFrames=new JArray();
        internal bool consumed,terminal,initializing;
    }
    SpawnCaptureArm spawnCapture;static RuntimeModelTest spawnCaptureObserver;bool spawnCaptureHooks;
    static JObject SpawnTargetIdentity(EnemyDummy dummy){JObject value=SpawnFid(dummy.FID);value["dummyId"]=dummy.GetInstanceID();value["celId"]=dummy.m_EventListener==null?0:dummy.m_EventListener.GetInstanceID();return value;}
    static JObject SpawnFid(FTKPlayerID id){return new JObject{{"photonId",id.m_PhotonID},{"turnIndex",id.m_TurnIndex}};}
    static JObject SpawnDamageInfo(DummyDamageInfo info)
    {
        if(info==null)return null;
        FTK_proficiencyTable row=info.m_Prof==FTK_proficiencyTable.ID.None?null:FTK_proficiencyTableDB.Get(info.m_Prof);
        return new JObject{{"proficiencyId",(int)info.m_Prof},{"proficiency",info.m_Prof.ToString()},{"tableRowPresent",row!=null},
            {"tableSuicide",row==null?new JValue((object)null):new JValue(row.m_Suicide)},{"attackerFid",SpawnFid(info.m_AttackerID)},{"victimFid",SpawnFid(info.m_VictimID)},
            {"damage",info.m_Damage},{"newHealth",info.m_NewHealth},{"attackerHealthMod",info.m_AttackerHealthMod},{"proficiencySuccess",info.m_ProfSuccess},{"attackResponse",info.m_AttackResponse.ToString()}};
    }
    static JArray SpawnParty()
    {
        if(FTKHub.Instance==null||FTKHub.Instance.m_CharacterOverworlds==null)throw new InvalidOperationException("Native party unavailable.");
        JArray result=new JArray();foreach(CharacterOverworld cow in FTKHub.Instance.m_CharacterOverworlds)
        {if(cow==null||result.Count>=4)throw new InvalidOperationException("Expected1..4 actual party owners.");result.Add(new JObject{{"ownerId",cow.GetInstanceID()},{"fid",SpawnFid(cow.m_FTKPlayerID)}});}if(result.Count==0)throw new InvalidOperationException("Native party empty.");return result;
    }
    JObject SpawnAssets(JObject profile)
    {
        JObject result=new JObject();JArray renderers=profile["renderers"]as JArray;if(renderers==null||renderers.Count<1||renderers.Count>16)throw new InvalidOperationException("Bounded explicit profile required.");
        MethodInfo resolve=CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.CustomModelLoader",true).GetMethod("ResolveModelPath",Statics,null,new[]{typeof(string)},null);
        foreach(JObject renderer in renderers)
        {
            string glb=Str(renderer,"glbFile");result[glb]=CatalogHash(CatalogAsset(resolve,glb,".glb"));
            string texture=Str(renderer,"textureFile");if(texture!=null)result[texture]=CatalogHash(CatalogAsset(resolve,texture,".png"));
            MaterialSlotFixture.Slot[] slots=MaterialSlotFixture.Read(renderer);if(slots!=null)foreach(var slot in slots)if(slot.TextureFile!=null)result[slot.TextureFile]=CatalogHash(CatalogAsset(resolve,slot.TextureFile,".png"));
        }
        return result;
    }
    void SpawnPins(bool full)
    {
        SpawnCaptureArm a=spawnCapture;if(a==null||a.session!=sessionId||a.ownedRoot!=root)throw new InvalidOperationException("Stale arrival capture session/root.");
        CatalogNoLinks(root);RequireSinglePlayer();
        if(!ReferenceEquals(FTK_enemyCombatDB.GetDB().GetEntryByStringID(a.row.m_ID),a.row)||a.row.m_EnemyAsset!=a.prefab||!JToken.DeepEquals(a.party,SpawnParty()))throw new InvalidOperationException("Arrival row/prefab/party identity changed.");
        if(full && (!JToken.DeepEquals(a.content,ScaleIdentity(CatalogAssembly("FtkRuntimeModelTestContent")))||!JToken.DeepEquals(a.core,PreviewCoreIdentity())||!JToken.DeepEquals(a.helper,ScaleIdentity(typeof(RuntimeModelTest).Assembly))||!JToken.DeepEquals(a.gameAssembly,ScaleIdentity(typeof(EnemyDummy).Assembly))||
            CatalogHash(Path.Combine(root,"model-test-profiles.json"))!=a.catalogHash||!JToken.DeepEquals(a.assets,SpawnAssets(a.profile))))throw new InvalidOperationException("Arrival binary/catalog/asset pins changed.");
    }
    JObject SpawnNativeAttack(EnemyDummy dummy)
    {
        if(spawnCapture!=null && spawnCapture.initializedIdentity!=null && dummy!=null)EnemyArrivalPolicy.Match(spawnCapture.initializedIdentity,SpawnTargetIdentity(dummy));
        if(dummy==null||dummy.m_EventListener==null)throw new InvalidOperationException("Native attack observation owner unavailable.");
        CharacterEventListener cel=dummy.m_EventListener;if(cel.transform.childCount>64)throw new InvalidOperationException("Direct CEL child visibility bound64 exceeded.");
        JArray children=new JArray();for(int i=0;i<cel.transform.childCount;i++){Transform node=cel.transform.GetChild(i);children.Add(new JObject{{"transformId",node.GetInstanceID()},{"path",Relative(node,cel.transform)},{"activeSelf",node.gameObject.activeSelf},{"activeInHierarchy",node.gameObject.activeInHierarchy}});}
        JArray renderers=new JArray();foreach(Renderer r in cel.GetComponentsInChildren<Renderer>(true))
        {if(renderers.Count>=64)throw new InvalidOperationException("Arrival renderer visibility bound64 exceeded.");renderers.Add(new JObject{{"rendererId",r.GetInstanceID()},{"path",Relative(r.transform,cel.transform)},{"enabled",r.enabled},{"active",r.gameObject.activeInHierarchy}});}
        return new JObject{{"frame",Time.frameCount},{"gameTime",Time.time},{"dummyId",dummy.GetInstanceID()},{"celId",cel.GetInstanceID()},{"fid",SpawnFid(dummy.FID)},
            {"health",dummy.m_CurrentHealth},{"alive",dummy.m_IsAlive},{"isAttacking",dummy.m_IsAttacking},{"actionAnimationPlayed",dummy.m_ActionAnimationPlayed},
            {"postAttackHealthMod",dummy.m_PostAttackHealthMod},{"currentAttackInfo",SpawnDamageInfo(dummy.m_AttackInfo)},{"visibility",children},{"renderers",renderers},
            {"interpretation","Current native fields can contain stale prior-encounter values. Only separately observed PlayAttackSequence entries identify a new attack. Suicide attribution requires that entry's proficiency row and resulting native health-mod sequence; no inference from HP zero alone."}};
    }
    void InstallSpawnCaptureHooks()
    {
        if(spawnCaptureHooks)return;Harmony harmony=new Harmony("com.ftkmf.runtime-model-test.passive-arrival");
        MethodInfo init=typeof(EnemyDummy).GetMethod("InitEnemyDummyForCombat",Members,null,new[]{typeof(bool),typeof(bool),typeof(bool)},null);
        MethodInfo attack=typeof(CharacterDummy).GetMethod("PlayAttackSequence",Members,null,new[]{typeof(CharacterDummy.AttackAnim),typeof(CharacterEventListener.CombatAnimTrigger),typeof(DummyDamageInfo),typeof(DummyDamageInfo),typeof(DummyDamageInfo)},null);
        if(init==null||attack==null)throw new InvalidOperationException("Exact audited native arrival/attack methods unavailable.");
        harmony.Patch(init,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("SpawnInitPrefix",Statics)),null,null,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("SpawnInitFinalizer",Statics)),null);
        harmony.Patch(attack,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("SpawnAttackPrefix",Statics)),null,null,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("SpawnAttackFinalizer",Statics)),null);
        MethodInfo damage=typeof(EnemyDummy).GetMethod("TakeSecondaryDamage",Members,null,new[]{typeof(string),typeof(int),typeof(FTK_proficiencyTable.ID),typeof(bool),typeof(FTKPlayerID)},null);
        if(damage==null)throw new InvalidOperationException("Audited synchronous secondary damage method absent.");
        harmony.Patch(damage,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("SpawnDamagePrefix",Statics)),null,null,new HarmonyMethod(typeof(RuntimeModelTest).GetMethod("SpawnDamageFinalizer",Statics)),null);
        spawnCaptureHooks=true;spawnCaptureObserver=this;
    }
    static Exception SpawnInitFinalizer(EnemyDummy __instance,Exception __exception,JObject __state)
    {
        RuntimeModelTest o=spawnCaptureObserver;if(o==null||o.spawnCapture==null)return __exception;var a=o.spawnCapture;
        try
        {
            if(__state==null)return __exception;
            a.nativeInitException=__exception==null?null:__exception.ToString();if(__exception!=null && a.error==null)a.error="Native InitEnemyDummyForCombat failed: "+__exception;
            if(a.terminal||a.consumed||__instance==null||a.candidate!=__instance||!a.initializing)throw new InvalidOperationException("Unmatched native init finalizer.");
            JObject current=SpawnTargetIdentity(__instance);current["celId"]=__state["celId"].DeepClone();EnemyArrivalPolicy.MatchInit(__state,current);
            a.initializing=false;
            a.queuedCount++;a.candidate=__instance;a.initFrame=Time.frameCount;a.initializedCel=__instance.m_EventListener;a.initializedIdentity=SpawnTargetIdentity(__instance);a.initialSnapshot=o.SpawnNativeAttack(__instance);
            if((a.initializedCel==null || a.initializedCel.GetInstanceID()==a.previousCelId) && a.error==null)a.error="Initialization did not produce a fresh CEL.";
            if(a.queuedCount>1 && a.error==null)a.error="Multiple matching native arrivals; no sole future target.";
            a.status="native-arrival-queued";
        }
        catch(Exception e){if(a.error==null)a.error="Arrival telemetry failed: "+e;}
        return __exception;
    }
    static void SpawnAttackPrefix(CharacterDummy __instance,CharacterDummy.AttackAnim _attackAnim,CharacterEventListener.CombatAnimTrigger _override,DummyDamageInfo _ddi,DummyDamageInfo _ddi1,DummyDamageInfo _ddi2,ref JObject __state)
    {
        __state=null;
        RuntimeModelTest o=spawnCaptureObserver;if(o==null||o.spawnCapture==null)return;var a=o.spawnCapture;
        try
        {
            EnemyDummy dummy=__instance as EnemyDummy;if(a.terminal||dummy==null||dummy.m_EnemyType!=a.row.m_ID||!ReferenceEquals(dummy.m_EnemyCombat,a.row))return;
            if(a.candidate!=dummy)return;
            if(dummy.m_EventListener==null||dummy.m_EventListener.m_Dummy!=dummy||dummy.m_EventListener==a.prefab||dummy.m_EventListener.GetInstanceID()==a.previousCelId)throw new InvalidOperationException("Attack before a reciprocal fresh initialized CEL.");
            if(a.initializedIdentity!=null)EnemyArrivalPolicy.Match(a.initializedIdentity,SpawnTargetIdentity(dummy));else if(!a.initializing)throw new InvalidOperationException("Attack outside matching init scope.");
            if(a.attacks.Count>=32)throw new InvalidOperationException("Native event bound32 exceeded.");
            __state=new JObject{{"frame",Time.frameCount},{"realtime",Time.realtimeSinceStartup},{"gameTime",Time.time},{"dummyId",dummy.GetInstanceID()},
                {"celId",dummy.m_EventListener==null?0:dummy.m_EventListener.GetInstanceID()},{"fid",SpawnFid(dummy.FID)},{"beforeCaptureLaunch",!a.consumed},
                {"nativeMethod","CharacterDummy.PlayAttackSequence entry"},{"beforeFirstPng",a.firstPngFrame<0},{"sequence",a.attacks.Count},{"attackAnim",_attackAnim.ToString()},{"override",_override.ToString()},
                {"incoming",SpawnDamageInfo(_ddi)},{"secondary",SpawnDamageInfo(_ddi1)},{"tertiary",SpawnDamageInfo(_ddi2)},{"finalized",false}};a.attacks.Add(__state);
        }
        catch(Exception e){if(a.error==null)a.error="Native attack telemetry failed: "+e;}
    }
    static void SpawnInitPrefix(EnemyDummy __instance,ref JObject __state)
    {
        __state=null;
        RuntimeModelTest o=spawnCaptureObserver;if(o==null||o.spawnCapture==null)return;var a=o.spawnCapture;
        try{if(a.terminal||a.consumed||__instance==null||__instance.m_EnemyType!=a.row.m_ID)return;
            if(a.candidate!=null && a.candidate!=__instance)throw new InvalidOperationException("Multiple matching initialization candidates.");
            if(a.initializing)throw new InvalidOperationException("Reentrant matching init.");
            a.candidate=__instance;a.initializing=true;a.initPrefixFrame=Time.frameCount;a.previousCelId=__instance.m_EventListener==null?0:__instance.m_EventListener.GetInstanceID();
            __state=SpawnTargetIdentity(__instance);
        }catch(Exception e){if(a.error==null)a.error=e.ToString();}
    }
    static Exception SpawnAttackFinalizer(CharacterDummy __instance,Exception __exception,JObject __state)
    {return SpawnEventFinalized(__exception,__state,__instance as EnemyDummy);}
    static Exception SpawnEventFinalized(Exception exception,JObject state,EnemyDummy dummy)
    {
        RuntimeModelTest o=spawnCaptureObserver;
        try{if(state!=null){state["finalized"]=true;state["finalizedFrame"]=Time.frameCount;state["nativeException"]=new JValue((object)(exception==null?null:exception.ToString()));
            if(dummy!=null){if(o==null||o.spawnCapture==null)throw new InvalidOperationException("Arrival finalizer lost observer.");
                JObject eventIdentity=(JObject)state["fid"].DeepClone();eventIdentity["dummyId"]=state["dummyId"].DeepClone();eventIdentity["celId"]=state["celId"].DeepClone();EnemyArrivalPolicy.Match(eventIdentity,SpawnTargetIdentity(dummy));
                if(o.spawnCapture.initializedIdentity!=null)EnemyArrivalPolicy.Match(o.spawnCapture.initializedIdentity,SpawnTargetIdentity(dummy));
                state["afterHealth"]=dummy.m_CurrentHealth;state["afterAlive"]=dummy.m_IsAlive;state["afterPostAttackHealthMod"]=dummy.m_PostAttackHealthMod;state["afterActionAnimationPlayed"]=dummy.m_ActionAnimationPlayed;}}
            if(exception!=null && state!=null && o!=null && o.spawnCapture!=null && o.spawnCapture.error==null)o.spawnCapture.error="Observed native method exception: "+exception;
        }catch(Exception e){if(o!=null && o.spawnCapture!=null && o.spawnCapture.error==null)o.spawnCapture.error=e.ToString();}return exception;
    }
    static void SpawnDamagePrefix(EnemyDummy __instance,string _type,int _dmg,FTK_proficiencyTable.ID _profID,bool _spawnHud,FTKPlayerID _attackerID,ref JObject __state)
    {
        __state=null;RuntimeModelTest o=spawnCaptureObserver;if(o==null||o.spawnCapture==null)return;var a=o.spawnCapture;
        try{if(a.terminal||__instance==null||a.candidate!=__instance||__instance.m_EventListener!=a.initializedCel)return;
            EnemyArrivalPolicy.Match(a.initializedIdentity,SpawnTargetIdentity(__instance));
            if(a.attacks.Count>=32)throw new InvalidOperationException("Native event bound32 exceeded.");
            __state=new JObject{{"nativeMethod","EnemyDummy.TakeSecondaryDamage entry"},{"beforeCaptureLaunch",!a.consumed},{"beforeFirstPng",a.firstPngFrame<0},{"sequence",a.attacks.Count},{"frame",Time.frameCount},{"realtime",Time.realtimeSinceStartup},
                {"dummyId",__instance.GetInstanceID()},{"celId",__instance.m_EventListener.GetInstanceID()},{"fid",SpawnFid(__instance.FID)},
                {"type",_type},{"damage",_dmg},{"proficiencyId",(int)_profID},{"attackerFid",SpawnFid(_attackerID)},{"spawnHud",_spawnHud},
                {"beforeHealth",__instance.m_CurrentHealth},{"beforeAlive",__instance.m_IsAlive},{"finalized",false}};a.attacks.Add(__state);
        }catch(Exception e){if(a.error==null)a.error=e.ToString();}
    }
    static Exception SpawnDamageFinalizer(EnemyDummy __instance,Exception __exception,JObject __state)
    {return SpawnEventFinalized(__exception,__state,__instance);}
    JObject ClearSpawnCapture()
    {
        SpawnPins(true);if(spawnCapture.consumed)throw new InvalidOperationException("Cannot cancel consumed capture.");
        SpawnCaptureFail("Explicit cancellation of unconsumed arrival arm.");return SpawnCaptureView();
    }
    JObject ArmSpawnCapture(JObject command)
    {
        CatalogKeys(command,"id","session","op","enemy","catalogSha256","rendererPath");
        if(spawnCapture!=null)throw new InvalidOperationException("Arrival ticket already exists; no rearm in this session.");
        if(portraitArmed || krakenSkinArm!=null)throw new InvalidOperationException("Conflicting portrait/skin arm must be explicitly cleared first.");
        ReadyContext ready=RequireReadyPreparation();int level=ready.dungeon.m_Level,room=ready.dungeon.m_RoomIndex;
        if(level!=ready.dungeon.m_Level||room!=ready.dungeon.m_RoomIndex)throw new InvalidOperationException("Exact staged Ready indices required.");
        if(level<0||level>=ready.dungeon.GetLevelCount()||room<0||room>=ready.dungeon.GetRoomCount(level)||ready.dungeon.m_EncounterType==MiniHexDungeon.EncounterType.Stair||ready.dungeon.m_EncounterType==MiniHexDungeon.EncounterType.ExitRoom||ready.dungeon.m_EncounterType==MiniHexDungeon.EncounterType.Cleared)throw new InvalidOperationException("Preserve native transition/terminal slots.");
        MiniHexDungeon.RoomInfo slot=ready.dungeon.m_DungeonEncounters[level][room];string enemy=Str(command,"enemy"),path=Str(command,"rendererPath");
        if(slot==null||slot.m_Type!=MiniHexDungeon.EncounterType.Enemy||slot.m_EncounterObjects==null||slot.m_EncounterObjects.Count!=1||slot.m_EncounterObjects[0]!=enemy)throw new InvalidOperationException("Arm only after staging the exact sole enemy in a native Enemy slot.");
        JObject profile=null,selected=null;foreach(JObject p in CatalogProfiles(false))if(Str(p,"key")==enemy){if(profile!=null)throw new InvalidOperationException("Ambiguous profile.");profile=p;}
        if(profile==null)throw new InvalidOperationException("Exact registered profile absent.");
        foreach(JObject r in (JArray)profile["renderers"])if(Str(r,"rendererPath")==path){if(selected!=null)throw new InvalidOperationException("Ambiguous renderer path.");selected=r;}
        if(selected==null)throw new InvalidOperationException("Exact explicit CEL-relative profile renderer path required.");
        string rendererKind=Str(selected,"rendererKind")??"SkinnedMeshRenderer";
        if(rendererKind!="SkinnedMeshRenderer")throw new InvalidOperationException("Arrival motion capture requires the selected skinned renderer; validate rigid MeshRenderer assignments through inventory and lifetime evidence.");
        FTK_enemyCombat row=FTK_enemyCombatDB.GetDB().GetEntryByStringID(enemy);if(row==null||row.m_EnemyAsset==null||row.m_WeaponAsset==null)throw new InvalidOperationException("Native row/CEL/weapon unavailable.");
        Type registry=CatalogAssembly("FTKModFramework").GetType("FTKModFramework.Core.ContentRegistry",true);object[] registration={enemy,0,new[]{typeof(FTK_enemyCombatDB)}};
        if(!(bool)registry.GetMethod("TryGetSyntheticId",Statics).Invoke(null,registration)||!ReferenceEquals(FTK_enemyCombatDB.GetDB().GetEntryByInt((int)registration[1]),row))throw new InvalidOperationException("Exact synthetic row registration required.");
        JObject assembly=ScaleIdentity(typeof(EnemyDummy).Assembly);if(Str(assembly,"assemblyFileSha256")!=KrakenControllerGameAssembly||!Path.GetFullPath(Str(assembly,"location")).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))throw new InvalidOperationException("Pinned native assembly source audit mismatch.");
        string hash=CatalogHash(Path.Combine(root,"model-test-profiles.json"));if(hash!=Str(command,"catalogSha256"))throw new InvalidOperationException("Catalog hash mismatch.");
        if(Time.captureFramerate!=0)throw new InvalidOperationException("Arrival recorder requires ordinary native timing.");
        SpawnCaptureArm a=new SpawnCaptureArm{id=Str(command,"id"),captureId=Guid.NewGuid().ToString("N"),session=sessionId,ownedRoot=root,
            catalogHash=hash,path=path,armedRealtime=Time.realtimeSinceStartup,armedFrame=Time.frameCount,level=level,room=room,row=row,prefab=row.m_EnemyAsset,dungeon=ready.dungeon,roomInfo=slot,
            profile=(JObject)profile.DeepClone(),assets=SpawnAssets(profile),core=PreviewCoreIdentity(),content=ScaleIdentity(CatalogAssembly("FtkRuntimeModelTestContent")),helper=ScaleIdentity(typeof(RuntimeModelTest).Assembly),gameAssembly=assembly,party=SpawnParty()};
        a.ready=new JObject{{"dungeonId",a.dungeon.GetInstanceID()},{"level",level},{"room",room},{"queuedEnemy",enemy},{"party",a.party.DeepClone()}};
        if(File.Exists(Path.Combine(output,a.captureId+".json"))||Directory.Exists(Path.Combine(output,a.captureId)))throw new InvalidOperationException("Generated arrival capture ID already exists.");
        InstallSpawnCaptureHooks();spawnCapture=a;
        return new JObject{{"ok",true},{"status","enemy-arrival-armed"},{"captureId",a.captureId},{"armFrame",a.armedFrame},{"expiresAfterRealtimeSeconds",90},
            {"pinnedReady",a.ready.DeepClone()},{"core",a.core.DeepClone()},{"content",a.content.DeepClone()},{"helper",a.helper.DeepClone()},{"gameAssembly",a.gameAssembly.DeepClone()},{"profile",a.profile.DeepClone()},{"catalogSha256",hash},{"assets",a.assets.DeepClone()},
            {"timingMode","realtime-observation"},{"fixedStep",false},{"seconds",10},{"fps",12},{"maxWidth",960},
            {"note","Arm result is immutable. Operator may click native Ready once; no Ready/game action is submitted here. Passive native init queues capture; missed-before-action/expiry are terminal and never retried."}};
    }
    void SpawnCaptureFail(string error)
    {
        var a=spawnCapture;a.terminal=true;a.status="failed";if(a.error==null)a.error=error;
        if(File.Exists(Path.Combine(output,a.captureId+".json")))return;
        Finish(a.captureId,new JObject{{"ok",false},{"error",error},{"provenance","passive-native-enemy-arrival"},{"arrival",SpawnCaptureView()},{"frames",a.partialFrames.DeepClone()},{"partial",a.partialFrames.Count>0}});
    }
    JObject SpawnCaptureView()
    {
        var a=spawnCapture;if(a==null)return new JObject{{"ok",true},{"armed",false}};
        return new JObject{{"ok",a.error==null},{"captureId",a.captureId},{"armCommandId",a.id},{"status",a.status},{"error",a.error},{"consumed",a.consumed},{"terminal",a.terminal},
            {"armFrame",a.armedFrame},{"nativeInitFrame",a.initFrame},{"nativeInitException",new JValue((object)a.nativeInitException)},{"queuedCount",a.queuedCount},{"captureLaunchFrame",a.launchFrame},{"firstPngFrame",a.firstPngFrame},{"initPrefixFrame",a.initPrefixFrame},{"previousCelId",a.previousCelId},
            {"pinnedReady",a.ready.DeepClone()},{"profile",a.profile.DeepClone()},{"core",a.core.DeepClone()},{"content",a.content.DeepClone()},{"helper",a.helper.DeepClone()},{"gameAssembly",a.gameAssembly.DeepClone()},{"catalogSha256",a.catalogHash},{"assets",a.assets.DeepClone()},
            {"initialSnapshot",EnemyArrivalPolicy.Snapshot(a.initialSnapshot)},{"initializedIdentity",EnemyArrivalPolicy.Snapshot(a.initializedIdentity)},{"prelaunch",EnemyArrivalPolicy.Snapshot(a.prelaunch)},{"nativeAttackEntries",a.attacks.DeepClone()}};
    }
    JObject SpawnBinding(EnemyDummy dummy,SkinnedMeshRenderer renderer)
    {
        Type type;IDictionary table=WatchLeaseTable(out type);Component owner=dummy.m_EventListener.GetComponent(type);
        if(owner==null || !SceneOwner(dummy.m_EventListener))throw new InvalidOperationException("Native scene CEL resource owner missing.");
        int leaseId=(int)type.GetField("_leaseId",Members).GetValue(owner);
        if(leaseId==0||!table.Contains(leaseId)||!(bool)type.GetField("_acquired",Members).GetValue(owner)||!(bool)type.GetField("Applied",Members).GetValue(owner))throw new InvalidOperationException("Actual acquired/applied model lease required.");
        object lease=table[leaseId];IList owners=(IList)lease.GetType().GetField("owners",Members).GetValue(lease);
        UnityEngine.Object[] resources=(UnityEngine.Object[])lease.GetType().GetField("resources",Members).GetValue(lease);
        Renderer[] targets=(Renderer[])type.GetField("_targets",Members).GetValue(owner);bool target=false,mesh=false,member=false;
        if(owners==null||owners.Count>8||resources==null||resources.Length>256||targets==null||targets.Length>16)throw new InvalidOperationException("Bounded lease evidence unavailable.");
        foreach(object item in owners)if(ReferenceEquals(item,owner))member=true;foreach(Renderer item in targets)if(item==renderer)target=true;
        foreach(UnityEngine.Object item in resources)if(item==renderer.sharedMesh)mesh=true;
        if(!member||!target||!mesh)throw new InvalidOperationException("Lease owner/target/mesh membership mismatch.");
        if(renderer.bones==null||renderer.bones.Length==0||renderer.bones.Length!=renderer.sharedMesh.bindposes.Length)throw new InvalidOperationException("Exact bone/bind palette unavailable.");
        foreach(Transform bone in renderer.bones)if(bone==null||!(bone==dummy.m_EventListener.transform||bone.IsChildOf(dummy.m_EventListener.transform)))throw new InvalidOperationException("Renderer bone outside actual CEL.");
        return new JObject{{"ownerId",owner.GetInstanceID()},{"leaseId",leaseId},{"acquired",true},{"applied",true},{"ownerMember",true},{"rendererMember",true},{"meshMember",true},{"ownerCount",owners.Count},{"resourceCount",resources.Length}};
    }
    void SpawnCaptureTick()
    {
        var a=spawnCapture;if(a==null||a.consumed||a.terminal)return;
        try
        {
            if(a.error!=null)throw new InvalidOperationException(a.error);
            if(Time.realtimeSinceStartup-a.armedRealtime>90)throw new InvalidOperationException("Native arrival not captured within90sec; ticket expired.");
            if(a.candidate==null || a.queuedCount==0)return;
            if(busy)throw new InvalidOperationException("Another capture/helper coroutine became busy; arrival capture not launched.");
            SpawnPins(true);
            if(GameFlow.Instance.m_DungeonEntered!=a.dungeon||a.dungeon.m_Level!=a.level||a.dungeon.m_RoomIndex!=a.room||!ReferenceEquals(a.dungeon.m_DungeonEncounters[a.level][a.room],a.roomInfo))throw new InvalidOperationException("Pinned native room changed before arrival capture.");
            EnemyDummy dummy=a.candidate;EncounterSession es=EncounterSession.Instance;int matches=0;
            if(es==null||es.m_EnemyDummies==null||es.m_EnemyDummies.Count!=1)throw new InvalidOperationException("Expected sole actual native enemy.");foreach(EnemyDummy candidate in es.m_EnemyDummies.Values)if(candidate==dummy)matches++;
            EnemyDummy mapped;if(!es.m_EnemyDummies.TryGetValue(dummy.FID,out mapped)||mapped!=dummy)throw new InvalidOperationException("Enemy FID map membership mismatch.");
            if(matches!=1||!ReferenceEquals(dummy.m_EnemyCombat,a.row)||dummy.m_EventListener==null||dummy.m_EventListener==a.prefab||dummy.m_EventListener.m_Dummy!=dummy||dummy.m_CurrentHealth<=0||!dummy.m_IsAlive)throw new InvalidOperationException("Native arrival owner/CEL/alive binding missing.");
            EnemyArrivalPolicy.Match(a.initializedIdentity,SpawnTargetIdentity(dummy));
            EnemyArrivalPolicy.Launch(a.consumed,a.terminal,a.queuedCount,Time.realtimeSinceStartup-a.armedRealtime,busy,a.attacks,dummy.GetInstanceID(),dummy.m_EventListener.GetInstanceID());
            if(dummy.m_EventListener!=a.initializedCel)throw new InvalidOperationException("Initialized CEL changed before launch.");
            if(dummy.m_ActionAnimationPlayed)throw new InvalidOperationException("missed-before-action: native action animation already played.");
            SkinnedMeshRenderer renderer=null;JObject assignment=null;foreach(JObject r in (JArray)a.profile["renderers"])if(Str(r,"rendererPath")==a.path)assignment=r;
            foreach(var r in dummy.m_EventListener.GetComponentsInChildren<SkinnedMeshRenderer>(true))if(Relative(r.transform,dummy.m_EventListener.transform)==a.path){if(renderer!=null)throw new InvalidOperationException("Ambiguous actual renderer.");renderer=r;}
            if(renderer==null||renderer.sharedMesh==null||renderer.sharedMesh.name!="ftkmf_glb_"+Str(assignment,"glbFile"))throw new InvalidOperationException("Native init completed without exact public GLB binding.");
            if(Time.captureFramerate!=0)throw new InvalidOperationException("Non-native capture timing became active.");
            JObject binding=SpawnBinding(dummy,renderer);
            a.prelaunch=new JObject{{"binding",binding},{"frame",Time.frameCount},{"pose",Snapshot(renderer,true)},{"nativeAttackObservation",SpawnNativeAttack(dummy)}};
            a.consumed=true;a.launchFrame=Time.frameCount;a.status="capture-running";busy=true;
            StartCoroutine(SpawnCaptureRun(a.captureId,renderer));
        }
        catch(Exception e){if(a.consumed)busy=false;try{SpawnCaptureFail(e.ToString());}catch(Exception write){a.terminal=true;if(a.error==null)a.error=e.ToString();Logger.LogError("Arrival result write failed; no retry: "+write);}}
    }
    IEnumerator SpawnCaptureRun(string id,SkinnedMeshRenderer renderer)
    {
        IEnumerator capture=Capture(id,renderer,10,12,960,false,"passive-native-enemy-arrival",false,true);bool completed=false;
        try
        {
            while(true)
            {
                bool more=false;object yielded=null;Exception error=null;
                try{more=capture.MoveNext();if(more)yielded=capture.Current;}catch(Exception e){error=e;}
                if(error!=null)
                {
                    if(!File.Exists(Path.Combine(output,id+".json")))SpawnCaptureFail("Arrival capture setup/readback failed: "+error);
                    yield break;
                }
                if(!more)break;yield return yielded;
            }
            completed=true;spawnCapture.terminal=true;spawnCapture.status="capture-finished";
        }
        finally
        {
            try{IDisposable disposable=capture as IDisposable;if(disposable!=null)disposable.Dispose();}
            finally
            {
                busy=false;
                if(!completed && spawnCapture!=null && !File.Exists(Path.Combine(output,id+".json")))
                {try{SpawnCaptureFail("Arrival coroutine ended before a complete result; no automatic retry.");}catch(Exception error){Logger.LogError("Arrival cancellation result write failed: "+error);}}
            }
        }
    }
}
