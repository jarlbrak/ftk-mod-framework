using System;
using System.Collections;
using System.Reflection;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static void LeaseAssert(JArray assertions,string name,bool pass,object actual,object expected)
    {
        assertions.Add(new JObject{{"name",name},{"pass",pass},{"actual",actual==null?null:actual.ToString()},{"expected",expected==null?null:expected.ToString()}});
    }
    static int LeaseReferences(IDictionary leases,int id)
    {
        if(!leases.Contains(id))return 0;
        object lease=leases[id];
        return (int)lease.GetType().GetField("references",Members).GetValue(lease);
    }
    IEnumerator LeaseTest(string id)
    {
        JArray cases=new JArray();bool passed=true;
        try
        {
            RequireSinglePlayer();
            Assembly framework=null;
            foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="FTKModFramework")framework=assembly;
            if(framework==null)throw new InvalidOperationException("Framework assembly unavailable.");
            Type ownerType=framework.GetType("FTKModFramework.Core.EnemyMeshResources",true);
            MethodInfo targets=ownerType.GetMethod("SetTargets",Members,null,new[]{typeof(Renderer[])},null);
            MethodInfo resources=ownerType.GetMethod("SetResources",Members,null,new[]{typeof(UnityEngine.Object[])},null);
            MethodInfo retain=ownerType.GetMethod("EnsureRetained",Members,null,Type.EmptyTypes,null);
            MethodInfo owns=ownerType.GetMethod("Owns",Members,null,new[]{typeof(Renderer)},null);
            MethodInfo release=ownerType.GetMethod("Release",Members,null,Type.EmptyTypes,null);
            FieldInfo leaseId=ownerType.GetField("_leaseId",Members),targetField=ownerType.GetField("_targets",Members);
            IDictionary leases=ownerType.GetField("Leases",Statics).GetValue(null)as IDictionary;
            if(targets==null || resources==null || retain==null || owns==null || release==null || leaseId==null || targetField==null || leases==null)
                throw new InvalidOperationException("Exact resource ownership test interface unavailable.");
            // Both destruction orders for active clone, never-active clone, and never-active source+clone.
            for(int mode=0;mode<3;mode++)for(int order=0;order<2;order++)
            {
                GameObject original=null,clone=null;Component originalOwner=null,cloneOwner=null;
                Mesh mesh=null;Material material=null;int resourceId=0;
                JArray assertions=new JArray();string error=null;
                JObject result=new JObject{{"mode",mode==0?"active-clone":mode==1?"never-active-clone":"never-active-source-and-clone"},
                    {"destroyOrder",order==0?"original-first":"clone-first"},{"assertions",assertions}};
                cases.Add(result);
                try
                {
                try
                {
                    // Setup before the first yield, so no rendered game avatar is involved.
                    original=new GameObject("FTK_MODEL_TEST_LEASE_"+id+"_"+mode+"_"+order);
                    if(mode==2)original.SetActive(false);
                    SkinnedMeshRenderer renderer=original.AddComponent<SkinnedMeshRenderer>();renderer.enabled=false;
                    mesh=new Mesh();mesh.name="model-test-owned-triangle";
                    mesh.vertices=new[]{Vector3.zero,Vector3.right,Vector3.up};mesh.triangles=new[]{0,1,2};
                    Shader shader=Shader.Find("Standard")??Shader.Find("Sprites/Default");
                    if(shader==null)throw new InvalidOperationException("No shader for synthetic material.");
                    material=new Material(shader);material.name="model-test-owned-material";
                    renderer.sharedMesh=mesh;renderer.sharedMaterial=material;
                    originalOwner=original.AddComponent(ownerType);
                    targets.Invoke(originalOwner,new object[]{new Renderer[]{renderer}});
                    resources.Invoke(originalOwner,new object[]{new UnityEngine.Object[]{mesh,material}});
                    resourceId=(int)leaseId.GetValue(originalOwner);
                    LeaseAssert(assertions,"source-count",LeaseReferences(leases,resourceId)==1,LeaseReferences(leases,resourceId),1);
                    if(mode==1)original.SetActive(false);
                    clone=UnityEngine.Object.Instantiate(original)as GameObject;
                    cloneOwner=clone.GetComponent(ownerType);SkinnedMeshRenderer cloneRenderer=clone.GetComponent<SkinnedMeshRenderer>();
                    if(mode==1)original.SetActive(true); // clone remains never activated.
                    Renderer[] remapped=targetField.GetValue(cloneOwner)as Renderer[];
                    LeaseAssert(assertions,"clone-target-remapped",remapped!=null && remapped.Length==1 && remapped[0]==cloneRenderer,
                        remapped==null?"null":remapped.Length==1 && remapped[0]==cloneRenderer?"clone-renderer":"wrong-target","clone-renderer");
                    LeaseAssert(assertions,"clone-owns-clone",(bool)owns.Invoke(cloneOwner,new object[]{cloneRenderer}),owns.Invoke(cloneOwner,new object[]{cloneRenderer}),true);
                    LeaseAssert(assertions,"clone-not-own-original",!(bool)owns.Invoke(cloneOwner,new object[]{renderer}),owns.Invoke(cloneOwner,new object[]{renderer}),false);
                    LeaseAssert(assertions,"shared-resource-identity",cloneRenderer.sharedMesh==mesh && cloneRenderer.sharedMaterial==material,
                        cloneRenderer.sharedMesh==mesh && cloneRenderer.sharedMaterial==material,true);
                    LeaseAssert(assertions,"shared-lease-id",(int)leaseId.GetValue(cloneOwner)==resourceId,leaseId.GetValue(cloneOwner),resourceId);
                    if(mode!=0)LeaseAssert(assertions,"inactive-clone-not-auto-retained",LeaseReferences(leases,resourceId)==1,LeaseReferences(leases,resourceId),1);
                    bool retained=(bool)retain.Invoke(cloneOwner,null);
                    LeaseAssert(assertions,"explicit-retain",retained,retained,true);
                    LeaseAssert(assertions,"two-owner-count",LeaseReferences(leases,resourceId)==2,LeaseReferences(leases,resourceId),2);
                    retain.Invoke(cloneOwner,null);retain.Invoke(cloneOwner,null);retain.Invoke(originalOwner,null);
                    LeaseAssert(assertions,"repeated-retain-idempotent",LeaseReferences(leases,resourceId)==2,LeaseReferences(leases,resourceId),2);
                    if(order==0)UnityEngine.Object.Destroy(original);else UnityEngine.Object.Destroy(clone);
                }
                catch(Exception ex){error=ex.ToString();}
                if(error==null)
                {
                    // Destroy is deferred; actual production Plugin.Update must prune inactive owners.
                    yield return null;yield return null;yield return null;
                    try
                    {
                        RequireSinglePlayer();
                        GameObject survivor=order==0?clone:original;
                        LeaseAssert(assertions,"survivor-object-live",survivor!=null,survivor!=null,true);
                        LeaseAssert(assertions,"one-owner-after-first-destroy",LeaseReferences(leases,resourceId)==1,LeaseReferences(leases,resourceId),1);
                        LeaseAssert(assertions,"survivor-resources-live",mesh!=null && material!=null,mesh!=null && material!=null,true);
                        if(survivor!=null)
                        {
                            SkinnedMeshRenderer renderer=survivor.GetComponent<SkinnedMeshRenderer>();
                            LeaseAssert(assertions,"survivor-resource-identity",renderer.sharedMesh==mesh && renderer.sharedMaterial==material,
                                renderer.sharedMesh==mesh && renderer.sharedMaterial==material,true);
                            UnityEngine.Object.Destroy(survivor);
                        }
                    }catch(Exception ex){error=ex.ToString();}
                }
                if(error==null)
                {
                    yield return null;yield return null;yield return null;
                    try
                    {
                        LeaseAssert(assertions,"final-zero-references",LeaseReferences(leases,resourceId)==0,LeaseReferences(leases,resourceId),0);
                        LeaseAssert(assertions,"final-lease-removed",!leases.Contains(resourceId),leases.Contains(resourceId),false);
                        LeaseAssert(assertions,"mesh-Unity-null",mesh==null,mesh==null,true);
                        LeaseAssert(assertions,"material-Unity-null",material==null,material==null,true);
                    }catch(Exception ex){error=ex.ToString();}
                }
                }
                finally
                {
                // Cleanup also executes if the coroutine is stopped between yielded frames.
                // It touches only objects allocated above, including failed test setups.
                try
                {
                    if(originalOwner!=null)release.Invoke(originalOwner,null);
                    if(cloneOwner!=null)release.Invoke(cloneOwner,null);
                }catch(Exception cleanup){result["cleanupError"]=cleanup.ToString();passed=false;}
                finally
                {
                    if(original!=null)UnityEngine.Object.Destroy(original);
                    if(clone!=null)UnityEngine.Object.Destroy(clone);
                    if(mesh!=null)UnityEngine.Object.Destroy(mesh);
                    if(material!=null)UnityEngine.Object.Destroy(material);
                }
                }
                result["error"]=error;
                bool casePassed=error==null;
                foreach(JObject assertion in assertions)if(!(bool)assertion["pass"])casePassed=false;
                result["ok"]=casePassed;passed=passed&&casePassed;
                yield return null;yield return null;
            }
            Finish(id,new JObject{{"ok",passed},{"provenance","synthetic isolated ownership lifecycle; not native avatar integration"},
                {"pruneSource","actual FTKModFramework.Plugin.Update; test never invokes prune"},{"cases",cases}});
        }
        finally{busy=false;}
    }
}
