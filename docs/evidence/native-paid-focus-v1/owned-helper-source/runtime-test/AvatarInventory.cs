using System;
using System.Collections.Generic;
using System.Reflection;
using System.Collections;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    sealed class AvatarOwner
    {
        public Component owner;
        public CharacterEventListener cel;
        public string scope;
    }
    static string Scope(JObject command)
    {
        string scope=Str(command,"scope")??"enemies";
        if(scope!="enemies" && scope!="player-overworld" && scope!="player-combat" && scope!="player-preview")
            throw new ArgumentException("scope must be enemies/player-overworld/player-combat/player-preview.");
        return scope;
    }
    static bool SceneOwner(Component component)
    {
        return component!=null && component.gameObject.scene.IsValid() && component.gameObject.scene.isLoaded;
    }
    static bool Contains(Transform root,Transform child){return root==child || child.IsChildOf(root);}
    static string OwnerPath(SkinnedMeshRenderer renderer,AvatarOwner avatar)
    {
        // UI preview avatars can be parented under a separate character pedestal.
        // Use explicit CEL-relative path when there is no hierarchy relation to the owner.
        return Contains(avatar.owner.transform,renderer.transform)?Relative(renderer.transform,avatar.owner.transform):"@cel/"+Relative(renderer.transform,avatar.cel.transform);
    }
    static List<AvatarOwner> Owners(string scope)
    {
        List<AvatarOwner> result=new List<AvatarOwner>();
        if(scope==null || scope=="enemies")
            foreach(EnemyDummy enemy in Resources.FindObjectsOfTypeAll(typeof(EnemyDummy)))
                if(SceneOwner(enemy))
                {
                    CharacterEventListener cel=typeof(CharacterDummy).GetField("m_EventListener",Members).GetValue(enemy)as CharacterEventListener;
                    if(cel!=null)result.Add(new AvatarOwner{owner=enemy,cel=cel,scope="enemies"});
                }
        if(scope==null || scope=="player-overworld")
            foreach(CharacterOverworld cow in Resources.FindObjectsOfTypeAll(typeof(CharacterOverworld)))
                if(SceneOwner(cow))
                {
                    CharacterEventListener cel=typeof(CharacterOverworld).GetField("m_Avatar",Members).GetValue(cow)as CharacterEventListener;
                    if(cel!=null)result.Add(new AvatarOwner{owner=cow,cel=cel,scope="player-overworld"});
                }
        if(scope==null || scope=="player-combat")
            foreach(CharacterDummy dummy in Resources.FindObjectsOfTypeAll(typeof(CharacterDummy)))
                if(!(dummy is EnemyDummy) && SceneOwner(dummy))
                {
                    CharacterOverworld cow=typeof(CharacterDummy).GetField("m_CharacterOverworld",Members).GetValue(dummy)as CharacterOverworld;
                    CharacterEventListener cel=typeof(CharacterDummy).GetField("m_EventListener",Members).GetValue(dummy)as CharacterEventListener;
                    if(cow!=null && cel!=null)result.Add(new AvatarOwner{owner=dummy,cel=cel,scope="player-combat"});
                }
        if(scope==null || scope=="player-preview")
            foreach(uiQuickPlayerCreate preview in Resources.FindObjectsOfTypeAll(typeof(uiQuickPlayerCreate)))
                if(SceneOwner(preview))
                {
                    CharacterEventListener cel=typeof(uiQuickPlayerCreate).GetField("m_Avatar",Members).GetValue(preview)as CharacterEventListener;
                    if(cel!=null)result.Add(new AvatarOwner{owner=preview,cel=cel,scope="player-preview"});
                }
        return result;
    }
    static AvatarOwner FindOwner(SkinnedMeshRenderer renderer,string scope)
    {
        AvatarOwner match=null;
        foreach(AvatarOwner owner in Owners(scope))
            if(Contains(owner.cel.transform,renderer.transform))
            {
                if(match!=null)throw new InvalidOperationException("Renderer belongs to multiple current avatar references; refusing ambiguity.");
                match=owner;
            }
        if(match==null)throw new InvalidOperationException("Renderer is outside a verified current avatar reference.");
        return match;
    }
    static Animator Controller(SkinnedMeshRenderer renderer)
    {
        AvatarOwner owner=FindOwner(renderer,null);
        Animator direct=renderer.GetComponentInParent<Animator>();
        if(direct!=null && Contains(owner.cel.transform,direct.transform))return direct;
        Animator[] values=owner.cel.GetComponentsInChildren<Animator>(true);
        if(values.Length!=1)throw new InvalidOperationException("Avatar controller ambiguous or absent.");
        return values[0];
    }
    static JObject ReadLease(CharacterEventListener cel)
    {
        try
        {
            Type ownerType=null;
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="FTKModFramework")
                ownerType=assembly.GetType("FTKModFramework.Core.EnemyMeshResources",false);
            if(ownerType==null)return new JObject{{"available",false}};
            Component owner=cel.GetComponent(ownerType);
            if(owner==null)return new JObject{{"available",true},{"present",false}};
            int id=(int)ownerType.GetField("_leaseId",Members).GetValue(owner);
            IDictionary leases=ownerType.GetField("Leases",Statics).GetValue(null)as IDictionary;
            object lease=leases!=null && leases.Contains(id)?leases[id]:null;
            return new JObject{{"available",true},{"present",true},{"leaseId",id},
                {"acquired",(bool)ownerType.GetField("_acquired",Members).GetValue(owner)},
                {"applied",(bool)ownerType.GetField("Applied",Members).GetValue(owner)},
                {"references",lease==null?0:(int)lease.GetType().GetField("references",Members).GetValue(lease)}};
        }
        catch(Exception error){return new JObject{{"available",false},{"error",error.Message}};}
    }
    JObject Inventory(JObject command)
    {
        string scope=Scope(command);JArray renderers=new JArray();HashSet<int> seen=new HashSet<int>();
        foreach(AvatarOwner owner in Owners(scope))
            foreach(SkinnedMeshRenderer renderer in owner.cel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            {
                if(scope=="enemies" && !renderer.gameObject.activeInHierarchy)continue;
                if(!seen.Add(renderer.GetInstanceID()))throw new InvalidOperationException("Avatar references overlap; renderer ownership is ambiguous.");
                renderers.Add(Snapshot(renderer,true));
            }
        return new JObject{{"ok",true},{"scope",scope},{"renderers",renderers}};
    }
    SkinnedMeshRenderer Resolve(JObject command)
    {
        string scope=Scope(command);int id=Int(command,"rendererId",0),ownerId=Int(command,"ownerInstanceId",0);
        string expected=Str(command,"expectedMesh"),path=Str(command,"rendererPath"),signature=Str(command,"boneSignature");
        if(id==0 || string.IsNullOrEmpty(expected))throw new ArgumentException("Exact renderer identity from fresh inventory required.");
        if(scope!="enemies" && ownerId==0)throw new ArgumentException("Player scopes additionally require ownerInstanceId from fresh inventory.");
        SkinnedMeshRenderer match=null;
        foreach(AvatarOwner owner in Owners(scope))
        {
            if(ownerId!=0 && owner.owner.GetInstanceID()!=ownerId)continue;
            foreach(SkinnedMeshRenderer renderer in owner.cel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
                if((scope!="enemies" || renderer.gameObject.activeInHierarchy) && renderer.GetInstanceID()==id && renderer.sharedMesh!=null && renderer.sharedMesh.name==expected
                    && OwnerPath(renderer,owner)==path && BoneSignature(renderer)==signature)
                {
                    FindOwner(renderer,null); // Recheck all scopes, not only the requested kind.
                    if(match!=null)throw new InvalidOperationException("Renderer ownership ambiguous.");
                    match=renderer;
                }
        }
        if(match==null)throw new InvalidOperationException("Owner/renderer/mesh/bind identity changed; request fresh scoped inventory.");
        return match;
    }
}
