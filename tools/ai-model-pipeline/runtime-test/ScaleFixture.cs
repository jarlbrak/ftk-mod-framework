using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    static JArray ScaleVector(Vector3 value){return new JArray(value.x,value.y,value.z);}
    static bool ScaleNear(Vector3 actual,Vector3 expected)
    {
        return Math.Abs(actual.x-expected.x)<0.000001f && Math.Abs(actual.y-expected.y)<0.000001f
            && Math.Abs(actual.z-expected.z)<0.000001f;
    }
    static void ScaleCheck(JArray assertions,string name,bool pass,JToken actual,JToken expected)
    {
        assertions.Add(new JObject{{"name",name},{"pass",pass},{"actual",actual},{"expected",expected}});
        if(!pass)throw new InvalidOperationException("Scale assertion failed: "+name);
    }
    static void ScaleExpected(JArray assertions,string name,GameObject target,Vector3 expected)
    {
        Vector3 actual=target.transform.localScale;
        ScaleCheck(assertions,name,ScaleNear(actual,expected),ScaleVector(actual),ScaleVector(expected));
    }
    static void ScaleAuditOwned(GameObject root,Type scaleType)
    {
        Component[] components=root.GetComponentsInChildren<Component>(true);
        if(root.transform.parent!=null || root.transform.childCount!=0 || components.Length!=2)
            throw new InvalidOperationException("Scale fixture must be an unparented root with exactly two components.");
        foreach(Component component in components)
            if(component==null || (!(component is Transform) && component.GetType()!=scaleType))
                throw new InvalidOperationException("Unexpected fixture component.");
    }
    static JObject ScaleIdentity(Assembly assembly)
    {
        string digest;
        using(SHA256 hash=SHA256.Create())using(FileStream stream=File.OpenRead(assembly.Location))
            digest=BitConverter.ToString(hash.ComputeHash(stream)).Replace("-","").ToLowerInvariant();
        return new JObject{{"fullName",assembly.FullName},{"location",assembly.Location},
            {"loadedModuleVersionId",assembly.ManifestModule.ModuleVersionId.ToString()},{"assemblyFileSha256",digest}};
    }
    IEnumerator ScaleBaselineTest(string id)
    {
        JArray cases=new JArray();List<GameObject> owned=new List<GameObject>();JObject identity=null,pin=null;
        string error=null,pinnedSession=sessionId;
        MiniHexDungeon dungeon=null;int level=0,room=0;object encounter=null,master=null;
        try
        {
            try
            {
                ReadyContext ready=RequireReadyPreparation();dungeon=ready.dungeon;level=dungeon.m_Level;room=dungeon.m_RoomIndex;
                encounter=Instance(typeof(EncounterSession));master=Instance(typeof(EncounterSessionMC));
                pin=new JObject{{"session",pinnedSession},{"dungeonInstanceId",dungeon.GetInstanceID()},{"level",level},{"room",room}};
                Assembly framework=null;
                foreach(Assembly assembly in AppDomain.CurrentDomain.GetAssemblies())if(assembly.GetName().Name=="FTKModFramework")
                {
                    if(framework!=null)throw new InvalidOperationException("Ambiguous loaded framework assembly.");
                    framework=assembly;
                }
                if(framework==null || string.IsNullOrEmpty(framework.Location)
                    || !Path.GetFullPath(framework.Location).StartsWith(root+Path.DirectorySeparatorChar,StringComparison.Ordinal))
                    throw new InvalidOperationException("Expected framework loaded from this isolated game root.");
                identity=ScaleIdentity(framework);
                Type scaleType=framework.GetType("FTKModFramework.Core.EnemyVisualScale",true);
                if(!typeof(MonoBehaviour).IsAssignableFrom(scaleType))throw new InvalidOperationException("Scale type must be a MonoBehaviour.");
                MethodInfo apply=scaleType.GetMethod("Apply",Members|BindingFlags.DeclaredOnly,null,
                    new[]{typeof(float),typeof(float),typeof(bool)},null);
                FieldInfo captured=scaleType.GetField("_captured",Members|BindingFlags.DeclaredOnly);
                FieldInfo baseline=scaleType.GetField("_originalScale",Members|BindingFlags.DeclaredOnly);
                if(apply==null || apply.ReturnType!=typeof(void) || captured==null || captured.FieldType!=typeof(bool)
                    || baseline==null || baseline.FieldType!=typeof(Vector3)
                    || !Attribute.IsDefined(captured,typeof(SerializeField)) || !Attribute.IsDefined(baseline,typeof(SerializeField)))
                    throw new InvalidOperationException("Exact scale method and serialized fields unavailable.");
                Vector3[] baselines={new Vector3(.35f,.35f,.35f),new Vector3(.9f,.9f,.9f),new Vector3(.35f,.9f,1.2f)};
                for(int mode=0;mode<2;mode++)for(int index=0;index<baselines.Length;index++)
                {
                    Vector3 initial=baselines[index];JArray assertions=new JArray();
                    JObject result=new JObject{{"mode",mode==0?"active-source-and-clone":"never-active-source-and-clone"},
                        {"syntheticBaseline",ScaleVector(initial)},{"assertions",assertions}};cases.Add(result);
                    GameObject source=new GameObject("FTK_MODEL_TEST_SCALE_"+id+"_"+mode+"_"+index);owned.Add(source);
                    if(mode==1)source.SetActive(false);
                    source.transform.localScale=initial;Component component=source.AddComponent(scaleType);ScaleAuditOwned(source,scaleType);
                    ScaleCheck(assertions,"initial-uncaptured",!(bool)captured.GetValue(component),new JValue(captured.GetValue(component)),new JValue(false));
                    apply.Invoke(component,new object[]{1f,1f,false});ScaleExpected(assertions,"neutral-retains-native-axes",source,initial);
                    ScaleCheck(assertions,"baseline-captured",(bool)captured.GetValue(component),new JValue(captured.GetValue(component)),new JValue(true));
                    ScaleCheck(assertions,"captured-original",ScaleNear((Vector3)baseline.GetValue(component),initial),ScaleVector((Vector3)baseline.GetValue(component)),ScaleVector(initial));
                    Vector3 enlarged=new Vector3(initial.x*3f,initial.y*2f,initial.z*3f);
                    apply.Invoke(component,new object[]{2f,1.5f,false});ScaleExpected(assertions,"factor-and-width",source,enlarged);
                    apply.Invoke(component,new object[]{2f,1.5f,false});ScaleExpected(assertions,"repeat-not-compounded",source,enlarged);
                    GameObject clone=UnityEngine.Object.Instantiate(source)as GameObject;
                    if(clone==null)throw new InvalidOperationException("Owned root clone missing.");owned.Add(clone);ScaleAuditOwned(clone,scaleType);
                    Component cloneComponent=clone.GetComponent(scaleType);
                    result["sourceInstanceId"]=source.GetInstanceID();result["cloneInstanceId"]=clone.GetInstanceID();
                    ScaleCheck(assertions,"clone-component-independent",cloneComponent!=component,new JValue(cloneComponent.GetInstanceID()),new JValue("different from "+component.GetInstanceID()));
                    ScaleCheck(assertions,"clone-active-state",clone.activeInHierarchy==(mode==0),new JValue(clone.activeInHierarchy),new JValue(mode==0));
                    ScaleCheck(assertions,"clone-captured-serialized",(bool)captured.GetValue(cloneComponent),new JValue(captured.GetValue(cloneComponent)),new JValue(true));
                    ScaleCheck(assertions,"clone-original-serialized",ScaleNear((Vector3)baseline.GetValue(cloneComponent),initial),ScaleVector((Vector3)baseline.GetValue(cloneComponent)),ScaleVector(initial));
                    ScaleExpected(assertions,"clone-inherited-dressed-scale",clone,enlarged);
                    apply.Invoke(cloneComponent,new object[]{2f,1.5f,false});ScaleExpected(assertions,"clone-repeat-not-compounded",clone,enlarged);
                    Vector3 fitted=new Vector3(initial.x*.55f,initial.y*.55f,initial.z*.55f);
                    apply.Invoke(cloneComponent,new object[]{.55f,1f,false});ScaleExpected(assertions,"clone-factor-055",clone,fitted);
                    ScaleExpected(assertions,"source-not-changed-by-clone",source,enlarged);
                    apply.Invoke(cloneComponent,new object[]{1f,0f,false});ScaleExpected(assertions,"neutral-restores-baseline-zero-width-fallback",clone,initial);
                    apply.Invoke(component,new object[]{2f,1.5f,true});ScaleExpected(assertions,"legacy-absolute",source,new Vector3(3f,2f,3f));
                    apply.Invoke(component,new object[]{2f,1.5f,true});ScaleExpected(assertions,"legacy-repeat",source,new Vector3(3f,2f,3f));
                    apply.Invoke(component,new object[]{1f,1f,false});ScaleExpected(assertions,"legacy-retains-original-baseline",source,initial);
                    ScaleExpected(assertions,"clone-not-changed-by-source",clone,initial);
                    ScaleAuditOwned(source,scaleType);ScaleAuditOwned(clone,scaleType);
                    result["ok"]=true;
                }
            }
            catch(Exception ex){error=ex.ToString();}
        }
        finally
        {
            foreach(GameObject target in owned)if(target!=null)UnityEngine.Object.Destroy(target);
        }
        try
        {
            // Observe real Unity deferred destruction, including never-active components.
            yield return null;yield return null;
            bool cleanup=true;foreach(GameObject target in owned)if(target!=null)cleanup=false;
            bool sameReady=false;
            try
            {
                ReadyContext ready=RequireReadyPreparation();
                sameReady=ready.dungeon==dungeon && dungeon!=null && dungeon.m_Level==level && dungeon.m_RoomIndex==room
                    && sessionId==pinnedSession && object.ReferenceEquals(encounter,Instance(typeof(EncounterSession)))
                    && object.ReferenceEquals(master,Instance(typeof(EncounterSessionMC)));
                if(!sameReady)throw new InvalidOperationException("Pinned Ready dungeon/room/session changed.");
            }
            catch(Exception ex){if(error==null)error=ex.ToString();}
            Finish(id,new JObject{{"ok",error==null && cleanup && sameReady && cases.Count==6},{"error",error},
                {"framework",identity},{"pinnedReady",pin},{"sameReadyAfter",sameReady},{"cases",cases},
                {"cleanup",new JObject{{"ownedObjectCount",owned.Count},{"allOwnedRootsUnityNull",cleanup}}},
                {"provenance","actual_deployed_component_on_owned_synthetic_unparented_roots"},
                {"limitations","Tests Unity AddComponent, Instantiate serialization, scale arithmetic and independent instances. Does not invoke EnemyVisualPatch, attach native CELs, mutate DB/prefabs/live avatars, or prove combat hook wiring."}});
        }
        finally{busy=false;}
    }
}
