using System;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class WatchedResource
    {
        public UnityEngine.Object value;
        public int instanceId;
        public string name,type;
    }
    sealed class WatchedLease
    {
        public int leaseId,heroId,celId,frame,ownerId;public string ownerKind;public LeaseObservationPin pin;public JObject sourceSelection;
        public readonly List<WatchedResource> resources=new List<WatchedResource>();
    }
    readonly Dictionary<int,WatchedLease> watchedLeases=new Dictionary<int,WatchedLease>();
    static IDictionary WatchLeaseTable(out Type ownerType)
    {
        Assembly core=CatalogAssembly("FTKModFramework");
        ownerType=core.GetType("FTKModFramework.Core.EnemyMeshResources",true);
        IDictionary leases=ownerType.GetField("Leases",Statics).GetValue(null)as IDictionary;
        if(leases==null)throw new InvalidOperationException("Exact native model lease table unavailable.");
        return leases;
    }
    JObject WatchLease(JObject command)
    {
        CatalogKeys(command,"id","session","op","heroInstanceId");
        CatalogNoLinks(root);RequireSinglePlayer();RequireOutsideCombat();
        object flow=Instance(typeof(GameFlow));
        if(flow==null)throw new InvalidOperationException("Live game flow required.");
        // Dungeon observations retain the strict Ready boundary; world observation is read-only.
        if(typeof(GameFlow).GetField("m_DungeonEntered",Members).GetValue(flow)!=null)RequireReadyPreparation();
        int heroId=LeaseObservationPin.ExactId(command,"heroInstanceId",true);
        if(heroId==0)throw new ArgumentException("Exact current heroInstanceId required.");
        CharacterOverworld cow=null;
        foreach(CharacterOverworld candidate in FTKHub.Instance.m_CharacterOverworlds)
            if(candidate!=null && candidate.GetInstanceID()==heroId){if(cow!=null)throw new InvalidOperationException("Ambiguous hero.");cow=candidate;}
        if(cow==null || cow.m_Avatar==null)throw new InvalidOperationException("Owned real overworld avatar required.");
        return WatchAvatarLease(command,cow.m_Avatar,heroId,"player-overworld",heroId,0,null);
    }
    JObject WatchPreviewLease(JObject command)
    {
        CatalogKeys(command,"id","session","op","classKey","skinset","catalogSha256","ownerInstanceId","celInstanceId","expectedLeaseId");
        foreach(string key in new[]{"ownerInstanceId","celInstanceId","expectedLeaseId"})LeaseObservationPin.ExactId(command,key,true);
        PreviewObservation observed=ResolveNativePreview(command);
        if(observed==null)throw new InvalidOperationException("Actual native preview unavailable; watch not armed.");
        return WatchAvatarLease(command,observed.cel,observed.preview.GetInstanceID(),"player-preview",0,Int(command,"expectedLeaseId",0),observed);
    }
    JObject WatchAvatarLease(JObject command,CharacterEventListener cel,int ownerId,string ownerKind,int heroId,int expectedLease,PreviewObservation preview)
    {
        CatalogNoLinks(root);RequireSinglePlayer();JObject core=PreviewCoreIdentity();
        Type ownerType;IDictionary leases=WatchLeaseTable(out ownerType);
        Component owner=cel.GetComponent(ownerType);
        if(owner==null || !(bool)ownerType.GetField("_acquired",Members).GetValue(owner))throw new InvalidOperationException("Avatar must have acquired real model lease.");
        int leaseId=(int)ownerType.GetField("_leaseId",Members).GetValue(owner);
        if(leaseId==0 || !leases.Contains(leaseId))throw new InvalidOperationException("Current lease absent.");
        if(watchedLeases.ContainsKey(leaseId))throw new InvalidOperationException("Lease already watched; read lease-watch-state.");
        if(watchedLeases.Count>=8)throw new InvalidOperationException("Watch limit8 reached; explicitly lease-watch-clear.");
        object lease=leases[leaseId];
        UnityEngine.Object[] resources=lease.GetType().GetField("resources",Members).GetValue(lease)as UnityEngine.Object[];
        int existing=0;foreach(WatchedLease watched in watchedLeases.Values)existing+=watched.resources.Count;
        if(resources==null || resources.Length==0 || existing+resources.Length>256)throw new InvalidOperationException("Expected owned resources within total watch limit256.");
        LeaseObservationPin.ValidateArm(preview==null?ownerId:Int(command,"ownerInstanceId",0),ownerId,
            preview==null?cel.GetInstanceID():Int(command,"celInstanceId",0),cel.GetInstanceID(),expectedLease==0?leaseId:expectedLease,leaseId,
            SceneOwner(cel) && cel.gameObject.activeInHierarchy,(bool)ownerType.GetField("_acquired",Members).GetValue(owner),
            (bool)ownerType.GetField("Applied",Members).GetValue(owner),leases.Contains(leaseId),watchedLeases.ContainsKey(leaseId),watchedLeases.Count,existing,resources.Length);
        WatchedLease entry=new WatchedLease{leaseId=leaseId,heroId=heroId,celId=cel.GetInstanceID(),ownerId=ownerId,ownerKind=ownerKind,frame=Time.frameCount,
            pin=new LeaseObservationPin(root,sessionId,Str(command,"id"),core)};
        if(preview!=null)entry.sourceSelection=new JObject{{"classKey",Str(command,"classKey")},{"classId",preview.preview.m_ClassID},{"skinset",preview.skinset},{"skinType",(int)preview.preview.m_SkinType},{"catalogSha256",preview.catalogHash}};
        foreach(UnityEngine.Object resource in resources)
        {
            if(resource==null || !(resource is Mesh || resource is Material || resource is Texture))
                throw new InvalidOperationException("Only live owned Mesh/Material/Texture assets can be watched.");
            entry.resources.Add(new WatchedResource{value=resource,instanceId=resource.GetInstanceID(),name=resource.name,type=resource.GetType().FullName});
        }
        entry.pin.Check(root,sessionId,PreviewCoreIdentity());
        if(preview!=null){PreviewObservation current=ResolveNativePreview(command);if(current==null || current.preview!=preview.preview || current.cel!=cel)throw new InvalidOperationException("Native preview changed before arming.");}
        watchedLeases.Add(leaseId,entry);
        return new JObject{{"ok",true},{"status","watch-armed"},{"leaseId",leaseId},{"heroInstanceId",heroId},{"celInstanceId",entry.celId},
            {"resources",entry.resources.Count},{"ownerKind",ownerKind},{"ownerInstanceId",ownerId},{"observationPin",entry.pin.View()},{"sourceSelection",entry.sourceSelection},{"note","Managed references to old owned assets only; no lease retention or cleanup mutation. Observe lease-watch-state after a separately authorized native equipment or actual menu change. Hiding the UI or returning to title is not itself disposal proof."}};
    }
    JObject WatchLeaseState()
    {
        CatalogNoLinks(root);JObject core=PreviewCoreIdentity();
        Type ownerType;IDictionary leases=WatchLeaseTable(out ownerType);JArray results=new JArray();
        foreach(WatchedLease entry in watchedLeases.Values)
        {
            entry.pin.Check(root,sessionId,core);
            bool present=leases.Contains(entry.leaseId),allNull=true;JArray resources=new JArray();
            foreach(WatchedResource resource in entry.resources)
            {
                bool unityNull=resource.value==null;allNull&=unityNull;
                resources.Add(new JObject{{"instanceId",resource.instanceId},{"name",resource.name},{"type",resource.type},{"unityNull",unityNull}});
            }
            int refs=present?(int)leases[entry.leaseId].GetType().GetField("references",Members).GetValue(leases[entry.leaseId]):0;
            results.Add(new JObject{{"leaseId",entry.leaseId},{"heroInstanceId",entry.heroId},{"celInstanceId",entry.celId},{"armedFrame",entry.frame},
                {"ownerKind",entry.ownerKind},{"ownerInstanceId",entry.ownerId},{"observationPin",entry.pin.View()},{"sourceSelection",entry.sourceSelection},
                {"observedFrame",Time.frameCount},{"leasePresent",present},{"references",refs},{"allResourcesUnityNull",allNull},
                {"status",!present && allNull?"observed-disposed":"not-observed-disposed"},{"resources",resources}});
        }
        return new JObject{{"ok",true},{"provenance","read-only native old-lease asset lifetime observation"},{"watches",results},
            {"note","Observed disposal applies only to these pinned old lease assets; no claim about all process resources or avatar cleanup."}};
    }
    JObject ClearLeaseWatches()
    {
        int count=watchedLeases.Count;watchedLeases.Clear();
        return new JObject{{"ok",true},{"status","watch-references-cleared"},{"leases",count},{"note","Only managed diagnostic references released; no Unity Destroy or production lease mutation."}};
    }
}
