using System;
using System.Collections;
using System.Collections.Generic;
using System.Reflection;
using System.Security.Cryptography;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    // Pure reads only: sharedMaterials and existing lease/scroller fields. Never invoke ownership methods.
    static string MaterialBytesHash(byte[] bytes)
    {using(SHA256 hash=SHA256.Create())return BitConverter.ToString(hash.ComputeHash(bytes)).Replace("-","").ToLowerInvariant();}
    static JObject MaterialGeometry(Mesh mesh)
    {
        if(mesh==null || mesh.vertexCount>200000 || mesh.subMeshCount<1 || mesh.subMeshCount>4)throw new InvalidOperationException("Material observation mesh exceeds bounded200000 vertices/4 submeshes.");
        JArray parts=new JArray();int total=0;
        for(int slot=0;slot<mesh.subMeshCount;slot++)
        {
            int[] indices=mesh.GetTriangles(slot);total+=indices.Length;if(total>600000)throw new InvalidOperationException("Material observation index bound600000 exceeded.");
            byte[] data=new byte[indices.Length*4];for(int i=0;i<indices.Length;i++)
            {int index=indices[i];if(index<0 || index>=mesh.vertexCount)throw new InvalidOperationException("Invalid native submesh index.");for(int b=0;b<4;b++)data[i*4+b]=(byte)(index>>(b*8));}
            parts.Add(new JObject{{"nativeMaterialSlot",slot},{"indices",indices.Length},{"triangles",indices.Length/3},{"indexInt32LittleEndianSha256",MaterialBytesHash(data)}});
        }
        Vector2[] uv=mesh.uv;if(uv.Length!=mesh.vertexCount)throw new InvalidOperationException("Exact UV0 per vertex required.");
        byte[] uvBytes=new byte[uv.Length*8];
        for(int i=0;i<uv.Length;i++)for(int j=0;j<2;j++)
        {
            float value=j==0?uv[i].x:uv[i].y;if(float.IsNaN(value)||float.IsInfinity(value))throw new InvalidOperationException("Nonfinite UV0.");
            byte[] raw=BitConverter.GetBytes(value);if(!BitConverter.IsLittleEndian)Array.Reverse(raw);Array.Copy(raw,0,uvBytes,i*8+j*4,4);
        }
        return new JObject{{"meshInstanceId",mesh.GetInstanceID()},{"mesh",mesh.name},{"vertices",mesh.vertexCount},{"submeshCount",mesh.subMeshCount},{"submeshes",parts},{"uv0Count",uv.Length},{"uv0Float32LittleEndianSha256",MaterialBytesHash(uvBytes)}};
    }
    static JArray MaterialVector(Vector2 value)
    {if(float.IsNaN(value.x)||float.IsInfinity(value.x)||float.IsNaN(value.y)||float.IsInfinity(value.y))throw new InvalidOperationException("Nonfinite material/scroller vector.");return new JArray(value.x,value.y);}
    static JObject MaterialObject(UnityEngine.Object value,HashSet<int> owned)
    {
        if(value==null)return null;
        JObject result=new JObject{{"instanceId",value.GetInstanceID()},{"name",value.name},{"type",value.GetType().FullName},{"inCurrentLeaseResources",owned.Contains(value.GetInstanceID())}};
        Texture texture=value as Texture;if(texture!=null){result["width"]=texture.width;result["height"]=texture.height;result["wrapMode"]=texture.wrapMode.ToString();result["filterMode"]=texture.filterMode.ToString();}
        return result;
    }
    JObject MaterialObservation(SkinnedMeshRenderer renderer,bool geometry,string samplePhase="main-thread-command")
    {
        CatalogNoLinks(root);RequireSinglePlayer();AvatarOwner avatar=FindOwner(renderer,"enemies");
        EnemyDummy enemy=avatar.owner as EnemyDummy;if(enemy==null || enemy.m_EnemyCombat==null)throw new InvalidOperationException("Exact live enemy owner required.");
        Material[] materials=renderer.sharedMaterials;if(materials.Length<1 || materials.Length>4)throw new InvalidOperationException("Material observation supports1..4 existing slots.");
        Type ownerType;IDictionary leases=WatchLeaseTable(out ownerType);Component owner=avatar.cel.GetComponent(ownerType);
        HashSet<int> owned=new HashSet<int>();JArray resourceIds=new JArray();int leaseId=0;
        if(owner!=null)
        {
            leaseId=(int)ownerType.GetField("_leaseId",Members).GetValue(owner);
            if(leases.Contains(leaseId))
            {
                object lease=leases[leaseId];UnityEngine.Object[] resources=lease.GetType().GetField("resources",Members).GetValue(lease)as UnityEngine.Object[];
                if(resources==null || resources.Length>256)throw new InvalidOperationException("Existing lease resource bound256 exceeded or unavailable.");
                foreach(UnityEngine.Object resource in resources)if(resource!=null){owned.Add(resource.GetInstanceID());resourceIds.Add(resource.GetInstanceID());}
            }
        }
        JArray slots=new JArray();
        for(int i=0;i<materials.Length;i++)
        {
            Material material=materials[i];if(material==null)throw new InvalidOperationException("Null existing material slot.");
            JObject item=MaterialObject(material,owned);item["slot"]=i;item["shader"]=material.shader==null?null:material.shader.name;
            item["emissionKeyword"]=material.IsKeywordEnabled("_EMISSION");
            foreach(string property in new[]{"_MainTex","_EmissionMap"})
            {
                bool supported=material.HasProperty(property);item[property+"Supported"]=supported;if(!supported)continue;
                item[property]=MaterialObject(material.GetTexture(property),owned);
                Vector2 offset=material.GetTextureOffset(property),scale=material.GetTextureScale(property);
                item[property+"Offset"]=MaterialVector(offset);item[property+"Scale"]=MaterialVector(scale);
            }
            if(material.HasProperty("_EmissionColor")){Color c=material.GetColor("_EmissionColor");item["emissionColor"]=new JArray(c.r,c.g,c.b,c.a);}
            slots.Add(item);
        }
        ScrollingUVs[] scrollers=renderer.GetComponents<ScrollingUVs>();if(scrollers.Length>16)throw new InvalidOperationException("Scroller bound16 exceeded.");
        JArray scroll=new JArray();
        foreach(ScrollingUVs scroller in scrollers)
        {
            if(scroller.GetType()!=typeof(ScrollingUVs))throw new InvalidOperationException("Unsupported derived scroller.");
            Vector2 phase=(Vector2)typeof(ScrollingUVs).GetField("uvOffset",Members).GetValue(scroller);
            if(scroller.materialIndex<0 || scroller.materialIndex>=materials.Length || string.IsNullOrEmpty(scroller.textureName) || scroller.textureName.Length>128 || scroller.GetComponent<Renderer>()!=renderer)
                throw new InvalidOperationException("Exact native scroller renderer/slot/property identity unavailable.");
            Material target=materials[scroller.materialIndex];bool propertySupported=target.HasProperty(scroller.textureName);
            Vector2 written=propertySupported?target.GetTextureOffset(scroller.textureName):Vector2.zero;
            scroll.Add(new JObject{{"instanceId",scroller.GetInstanceID()},{"enabled",scroller.enabled},{"active",scroller.gameObject.activeInHierarchy},
                {"materialIndex",scroller.materialIndex},{"textureName",scroller.textureName},{"rate",MaterialVector(scroller.uvAnimationRate)},{"phase",MaterialVector(phase)},{"rendererInstanceId",renderer.GetInstanceID()},{"targetMaterialInstanceId",target.GetInstanceID()},{"propertySupported",propertySupported},{"currentPropertyOffset",propertySupported?(JToken)MaterialVector(written):new JValue((object)null)}});
        }
        JObject result=new JObject{{"samplePhase",samplePhase},{"provenance","read-only existing live enemy shared-material and lease observation"},{"frame",Time.frameCount},{"gameTime",Time.time},{"realtime",Time.realtimeSinceStartup},{"deltaTime",Time.deltaTime},
            {"enemy",enemy.m_EnemyCombat.m_ID},{"ownerInstanceId",enemy.GetInstanceID()},{"celInstanceId",avatar.cel.GetInstanceID()},{"rendererInstanceId",renderer.GetInstanceID()},{"celRelativeRendererPath",Relative(renderer.transform,avatar.cel.transform)},
            {"rendererEnabled",renderer.enabled},{"rendererActive",renderer.gameObject.activeInHierarchy},{"slots",slots},{"scrollers",scroll},{"lease",ReadLease(avatar.cel)},{"leaseResourceInstanceIds",resourceIds},
            {"boundary","Current fields/resources only; no lease retention, pruning, material getter cloning or native clone creation. UV displacement and owner independence require multiple matched observations. Multiple native scrollers can write the same property; raw observed phase is not presumed equal to the final material offset."}};
        if(geometry){result["geometry"]=MaterialGeometry(renderer.sharedMesh);result["geometry"]["inCurrentLeaseResources"]=renderer.sharedMesh!=null && owned.Contains(renderer.sharedMesh.GetInstanceID());}
        Material[] after=renderer.sharedMaterials;if(after.Length!=materials.Length)throw new InvalidOperationException("Materials changed during synchronous observation.");
        for(int i=0;i<after.Length;i++)if(after[i]!=materials[i])throw new InvalidOperationException("Material identity changed during synchronous observation.");
        return result;
    }
    JObject ObserveMaterialState(JObject command)
    {
        CatalogKeys(command,"id","session","op","scope","rendererId","ownerInstanceId","rendererPath","expectedMesh","boneSignature");
        if(Scope(command)!="enemies" || Int(command,"ownerInstanceId",0)==0)throw new InvalidOperationException("Explicit enemy owner identity required.");
        SkinnedMeshRenderer renderer=Resolve(command);JObject result=MaterialObservation(renderer,true);if(Resolve(command)!=renderer)throw new InvalidOperationException("Renderer identity changed.");result["ok"]=true;return result;
    }
}
