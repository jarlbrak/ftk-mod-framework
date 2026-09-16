using System;
using System.Collections.Generic;
using Newtonsoft.Json.Linq;

internal static class EnemyLifetimePolicy
{
    static int I(JObject o,string k){if(o[k]==null||o[k].Type!=JTokenType.Integer)throw new InvalidOperationException("Missing lifetime integer: "+k);return (int)o[k];}
    static bool B(JObject o,string k){if(o[k]==null||o[k].Type!=JTokenType.Boolean)throw new InvalidOperationException("Missing lifetime boolean: "+k);return (bool)o[k];}
    static HashSet<int> IDs(JArray array)
    {if(array==null)throw new InvalidOperationException("Missing complete ID set.");var ids=new HashSet<int>();foreach(JToken v in array)if(v.Type!=JTokenType.Integer||(int)v==0||!ids.Add((int)v))throw new InvalidOperationException("Invalid/duplicate lifetime ID.");return ids;}
    static float F(JToken token)
    {if(token==null||(token.Type!=JTokenType.Float&&token.Type!=JTokenType.Integer))throw new InvalidOperationException("Missing finite numeric setting.");double value=(double)token;if(double.IsNaN(value)||double.IsInfinity(value))throw new InvalidOperationException("Nonfinite setting.");return (float)value;}
    static JObject Descriptor(JObject r)
    {
        if(r==null)throw new InvalidOperationException("Missing renderer descriptor.");
        JObject result=new JObject{{"rendererPath",r["rendererPath"].DeepClone()},{"glbFile",r["glbFile"].DeepClone()},{"textureFile",r["textureFile"]==null?new JValue((object)null):r["textureFile"].DeepClone()},{"disableNativeEmission",r["disableNativeEmission"]==null?false:B(r,"disableNativeEmission")}};
        if(r["materialSlots"]!=null){JArray slots=new JArray();foreach(JObject slot in (JArray)r["materialSlots"])slots.Add(new JObject{{"primitiveIndex",I(slot,"primitiveIndex")},{"nativeMaterialSlot",I(slot,"nativeMaterialSlot")},{"textureFile",slot["textureFile"]==null?new JValue((object)null):slot["textureFile"].DeepClone()},{"disableNativeEmission",slot["disableNativeEmission"]==null?false:B(slot,"disableNativeEmission")}});result["materialSlots"]=slots;}
        return result;
    }
    internal static bool SourceUnchanged(JObject before,JObject after)
    {if(before==null||after==null)return false;JObject a=(JObject)before.DeepClone(),b=(JObject)after.DeepClone();a.Remove("resourceLease");b.Remove("resourceLease");return JToken.DeepEquals(a,b);}
    internal static void CheckIdentity(ref bool invalid,Action check)
    {if(invalid)throw new InvalidOperationException("Lifetime readout permanently invalidated by prior pin failure.");try{check();}catch{invalid=true;throw;}}
    internal static bool Attribution(bool previouslyAllowed,bool rowReferenceMatches){return previouslyAllowed&&rowReferenceMatches;}
    internal static bool Record(JArray history,ref JObject last,ref int currentFrame,JObject current,int frame,int limit)
    {
        if(history==null||current==null||limit<1||history.Count>limit)throw new InvalidOperationException("Invalid bounded observation history.");
        currentFrame=frame;bool changed=!JToken.DeepEquals(current,last);last=(JObject)current.DeepClone();
        if(!changed)return true;if(history.Count==limit)return false;
        history.Add(new JObject{{"frame",frame},{"samplePoint","helper Update before command processing; native pruning order not forced; disk pins checked at native attribution and explicit readout, not every frame"},{"state",current.DeepClone()}});return true;
    }
    internal static void Registration(string expected,JObject actual,JObject profile)
    {
        if(expected!="legacy-singular"&&expected!="explicit-plural")throw new InvalidOperationException("Explicit expected binding kind required.");
        if((string)actual["bindingKind"]!=expected)throw new InvalidOperationException("Requested binding kind differs from actual Core registration precedence.");
        if(expected!="legacy-singular"){JArray descriptors=new JArray();foreach(JObject r in (JArray)profile["renderers"])descriptors.Add(Descriptor(r));if(!JToken.DeepEquals(descriptors,actual["renderers"]))throw new InvalidOperationException("Actual plural descriptors differ from profile.");return;}
        if((string)profile["bindingKind"]!="legacy-singular")throw new InvalidOperationException("Legacy fixture metadata must explicitly classify registration.");
        JArray renderers=profile["renderers"]as JArray;if(renderers==null||renderers.Count!=1)throw new InvalidOperationException("Legacy observer requires one expected body descriptor.");
        JObject renderer=renderers[0]as JObject;
        if(renderer==null||(string)renderer["glbFile"]!=(string)actual["glbMesh"]||(string)renderer["textureFile"]!=(string)actual["glbTexture"]||I(actual,"explicitAssignmentCount")!=0)throw new InvalidOperationException("Legacy GLB/PNG metadata differs from actual registration.");
        foreach(string flag in new[]{"proceduralBody","addLantern","swampAura"})if(B(actual,flag))throw new InvalidOperationException("Narrow lifetime trial excludes procedural allocations.");
        foreach(string field in new[]{"meshBundle","meshName","meshTextureName"})if(!string.IsNullOrEmpty((string)actual[field]))throw new InvalidOperationException("Narrow lifetime trial excludes bundle fallback.");
        JArray tint=profile["tint"]as JArray,actualTint=actual["tint"]as JArray;if(tint==null||actualTint==null||tint.Count!=4||actualTint.Count!=4)throw new InvalidOperationException("Exact legacy tint RGBA required.");
        for(int i=0;i<4;i++)if(F(tint[i])!=F(actualTint[i]))throw new InvalidOperationException("Actual tint differs from profile.");
        if(F(actual["scale"])!=(profile["visualScale"]==null?1:F(profile["visualScale"]))||F(actual["widthBoost"])!=1||F(actual["hunchDegrees"])!=0||B(actual,"legacyAbsoluteScale")||B(actual,"applyWetSkin")||B(actual,"hideWeapon"))throw new InvalidOperationException("Narrow legacy fixture excludes extra deformation/wet/weapon settings.");
        if(renderer["materialSlots"]!=null)throw new InvalidOperationException("Legacy route cannot represent explicit material slots.");
    }
    internal static void Compare(JObject record)
    {
        JObject source=record["sourceBeforeClone"]as JObject,clone=record["cloneBeforeRender"]as JObject;
        if(source==null||clone==null)throw new InvalidOperationException("Missing before-clone/pre-render samples.");
        if(!B(source,"acquired")||!B(clone,"acquired")||I(source,"leaseId")!=I(clone,"leaseId")||I(source,"ownerId")==I(clone,"ownerId")||I(source,"celId")==I(clone,"celId"))
            throw new InvalidOperationException("Native clone did not acquire a distinct applied owner of the source lease.");
        bool plural=(string)record["bindingKind"]=="explicit-plural";
        if(plural && (!B(source,"applied")||!B(clone,"applied")||(B(source,"visualResourcesOnlyFieldPresent")&&B(source,"visualResourcesOnly"))||(B(clone,"visualResourcesOnlyFieldPresent")&&B(clone,"visualResourcesOnly"))))throw new InvalidOperationException("Plural plan is not complete.");
        if(!plural && (string)record["bindingKind"]!="legacy-singular")throw new InvalidOperationException("Unknown lifetime binding classification.");
        if(B(source,"visualResourcesOnlyFieldPresent")!=B(clone,"visualResourcesOnlyFieldPresent")||(B(source,"visualResourcesOnlyFieldPresent")&&B(source,"visualResourcesOnly")!=B(clone,"visualResourcesOnly")))throw new InvalidOperationException("Serialized resource-only provenance changed across clone.");
        bool complete=B(source,"applied")&&B(clone,"applied");JObject before=(JObject)source["lease"],after=(JObject)clone["lease"];
        var a=IDs((JArray)before["registeredOwnerIds"]);var b=IDs((JArray)after["registeredOwnerIds"]);
        if(!B(before,"present")||!B(after,"present")||!a.Contains(I(source,"ownerId"))||a.Contains(I(clone,"ownerId"))||I(before,"references")!=a.Count||I(after,"references")!=b.Count)
            throw new InvalidOperationException("Source baseline/membership/refcount is not complete.");
        var expected=new HashSet<int>(a);expected.Add(I(clone,"ownerId"));
        if(!expected.SetEquals(b))throw new InvalidOperationException("Unexplained native owner delta; not N+1.");
        var resources=IDs((JArray)after["currentResourceIds"]);if(!IDs((JArray)before["currentResourceIds"]).IsSubsetOf(resources))throw new InvalidOperationException("Lease lost source resources while clone was live.");
        JArray sr=source["renderers"]as JArray,cr=clone["renderers"]as JArray;if(sr==null||cr==null||sr.Count!=cr.Count||sr.Count==0)throw new InvalidOperationException("Renderer mapping incomplete.");
        var sourceIds=IDs((JArray)source["targetIds"]);var cloneIds=IDs((JArray)clone["targetIds"]);if(sourceIds.Count!=cloneIds.Count||sourceIds.Overlaps(cloneIds))throw new InvalidOperationException("Clone targets reference source.");
        for(int i=0;i<sr.Count;i++)
        {
            JObject s=(JObject)sr[i],c=(JObject)cr[i];
            if((string)s["rendererPath"]!=(string)c["rendererPath"]||I(s,"rendererId")==I(c,"rendererId")||I(s,"meshId")!=I(c,"meshId")||
                (string)s["boneSignature"]!=(string)c["boneSignature"])throw new InvalidOperationException("Native clone GLB/rig mapping differs from source.");
            bool body=B(s,"expectedCustomMesh")&&B(c,"expectedCustomMesh")&&B(s,"meshInLease")&&B(c,"meshInLease")&&B(s,"targetMember")&&B(c,"targetMember");complete&=body;if(plural&&!body)throw new InvalidOperationException("Explicit mesh ownership incomplete.");
            var sb=IDs((JArray)s["boneIds"]);var cb=IDs((JArray)c["boneIds"]);if(sb.Count!=cb.Count||sb.Overlaps(cb))throw new InvalidOperationException("Native clone bones not independently remapped.");
            JArray sm=(JArray)s["materials"],cm=(JArray)c["materials"];if(sm.Count!=cm.Count)throw new InvalidOperationException("Native material slot count changed.");
            for(int j=0;j<sm.Count;j++)
            {
                if(sm[j].Type==JTokenType.Null&&cm[j].Type==JTokenType.Null)continue;
                JObject sMat=sm[j]as JObject,cMat=cm[j]as JObject;
                if(sMat==null||cMat==null||I(sMat,"mainTextureId")!=I(cMat,"mainTextureId"))throw new InvalidOperationException("Clone material/PNG ownership identity unexplained.");
                bool materialOwned=B(sMat,"materialInLease")&&B(cMat,"materialInLease");complete&=materialOwned;if(plural&&!materialOwned)throw new InvalidOperationException("Explicit material ownership incomplete.");
            }
        }
        if(record["requestedHudTexture"]as JObject==null||record["currentHudTexture"]as JObject==null||I((JObject)record["requestedHudTexture"],"instanceId")!=I((JObject)record["currentHudTexture"],"instanceId"))throw new InvalidOperationException("Actual native HUD texture identity changed.");
        record["sourceToCloneMembershipCompared"]=true;record["assignedBodyAndMaterialsOwned"]=complete;record["bindingEvidence"]=complete?"configured_body_and_material_ownership_observed":"partial_resources_only_observation";record["optionalTextureBoundary"]="Main texture membership and clone identity are raw observations; absent optional PNG is nonfatal and no pixel/content equivalence is inferred.";
    }
}
