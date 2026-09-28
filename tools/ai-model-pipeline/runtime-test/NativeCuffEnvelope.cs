using System;
using System.Collections.Generic;
using GridEditor;
using Newtonsoft.Json.Linq;
using UnityEngine;

public sealed partial class RuntimeModelTest
{
    static SkinnedMeshRenderer CuffRenderer(CharacterEventListener cel, int id, string path, string meshName)
    {
        SkinnedMeshRenderer found=null;
        foreach(SkinnedMeshRenderer renderer in cel.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if(renderer.GetInstanceID()==id && Relative(renderer.transform,cel.transform)==path && renderer.sharedMesh!=null && renderer.sharedMesh.name==meshName)
            {if(found!=null)throw new InvalidOperationException("Ambiguous cuff renderer.");found=renderer;}
        if(found==null || !found.enabled || !found.gameObject.activeInHierarchy)throw new InvalidOperationException("Exact active cuff renderer required.");
        return found;
    }
    static int CuffBone(Transform[] bones,string name)
    {
        int found=-1;
        for(int i=0;i<bones.Length;i++)if(bones[i]!=null && bones[i].name==name)
        {if(found>=0)throw new InvalidOperationException("Ambiguous cuff bone.");found=i;}
        if(found<0)throw new InvalidOperationException("Missing cuff bone: "+name);
        return found;
    }
    static void CuffFinite(Matrix4x4 matrix)
    {
        for(int i=0;i<16;i++)if(float.IsNaN(matrix[i])||float.IsInfinity(matrix[i]))throw new InvalidOperationException("Nonfinite cuff matrix.");
        if(Math.Abs(matrix.determinant)<1e-12f)throw new InvalidOperationException("Singular cuff matrix.");
    }
    static double CuffArmWeight(BoneWeight weight,int elbow,int wrist)
    {
        return ((weight.boneIndex0==elbow||weight.boneIndex0==wrist)?weight.weight0:0)+
            ((weight.boneIndex1==elbow||weight.boneIndex1==wrist)?weight.weight1:0)+
            ((weight.boneIndex2==elbow||weight.boneIndex2==wrist)?weight.weight2:0)+
            ((weight.boneIndex3==elbow||weight.boneIndex3==wrist)?weight.weight3:0);
    }
    static JObject CuffSupports(CuffEnvelopeMath reduced)
    {
        JArray bands=new JArray();
        for(int i=0;i<3;i++)
        {
            CuffEnvelopeMath.Band band=reduced.bands[i];JObject supports=new JObject();
            string[] names={"radialU","radialV","diagonalPlus","diagonalMinus"};
            for(int axis=0;axis<4;axis++)supports[names[axis]]=band.triangles==0?null:new JArray(band.min[axis],band.max[axis]);
            bands.Add(new JObject{{"tMin",i/3.0},{"tMax",(i+1)/3.0},{"triangleContributions",band.triangles},
                {"supportSamples",band.supportSamples},{"rejectedRadialTriangles",band.rejectedRadialTriangles},{"supports",supports}});
        }
        return new JObject{{"inputTriangles",reduced.inputTriangles},{"rejectedArmTriangles",reduced.rejectedArmTriangles},{"bands",bands}};
    }

    // Unity 2017 exposes only the native BakeMesh(Mesh) overload. Validate its
    // coordinate convention against native linear skinning without exporting samples.
    static Matrix4x4 VerifiedCuffBakeMapping(SkinnedMeshRenderer body, Mesh source, Vector3[] baked,
        BoneWeight[] weights, Transform[] bones, out JObject residual)
    {
        int influences=body.quality==SkinQuality.Auto?(int)QualitySettings.blendWeights:(int)body.quality;
        if(influences!=4 || source.blendShapeCount!=0)
            throw new InvalidOperationException("Cuff bake reference requires four-bone skinning without blendshapes.");
        Matrix4x4[] binds=source.bindposes;
        Vector3[] original=source.vertices;
        if(binds.Length!=bones.Length || original.Length!=baked.Length)
            throw new InvalidOperationException("Native skin reference palette/topology differs.");
        Vector3 origin=body.transform.position;
        Matrix4x4 full=body.localToWorldMatrix;
        Matrix4x4 rigid=Matrix4x4.TRS(origin,body.transform.rotation,Vector3.one);
        CuffFinite(full);CuffFinite(rigid);
        Matrix4x4 fullOffset=full,rigidOffset=rigid;
        fullOffset.m03=fullOffset.m13=fullOffset.m23=0;
        rigidOffset.m03=rigidOffset.m13=rigidOffset.m23=0;
        Matrix4x4[] skin=new Matrix4x4[bones.Length];
        for(int i=0;i<bones.Length;i++)
        {
            if(bones[i]==null)throw new InvalidOperationException("Null native skin reference bone.");
            CuffFinite(binds[i]);Matrix4x4 world=bones[i].localToWorldMatrix;
            // Subtract the remote paperdoll origin before multiplication to limit cancellation.
            world.m03-=origin.x;world.m13-=origin.y;world.m23-=origin.z;
            skin[i]=world*binds[i];CuffFinite(skin[i]);
        }
        double fullSum=0,rigidSum=0,fullMax=0,rigidMax=0,gapMax=0;
        for(int i=0;i<original.Length;i++)
        {
            BoneWeight w=weights[i];float sum=w.weight0+w.weight1+w.weight2+w.weight3;
            if(float.IsNaN(sum)||float.IsInfinity(sum)||Math.Abs(sum-1)>0.001f)
                throw new InvalidOperationException("Native skin reference weights not normalized.");
            int[] indices={w.boneIndex0,w.boneIndex1,w.boneIndex2,w.boneIndex3};
            float[] values={w.weight0,w.weight1,w.weight2,w.weight3};
            Vector3 reference=Vector3.zero;
            for(int j=0;j<4;j++)
            {
                if(values[j]<0 || values[j]>1)throw new InvalidOperationException("Invalid native skin reference influence.");
                if(values[j]==0)continue;
                if(indices[j]<0 || indices[j]>=skin.Length)throw new InvalidOperationException("Invalid native skin reference index.");
                reference+=skin[indices[j]].MultiplyPoint3x4(original[i])*values[j];
            }
            Vector3 f=fullOffset.MultiplyPoint3x4(baked[i]),r=rigidOffset.MultiplyPoint3x4(baked[i]);
            double fd=(f-reference).magnitude,rd=(r-reference).magnitude,gd=(f-r).magnitude;
            if(double.IsNaN(fd)||double.IsInfinity(fd)||double.IsNaN(rd)||double.IsInfinity(rd))
                throw new InvalidOperationException("Nonfinite native bake residual.");
            fullSum+=fd*fd;rigidSum+=rd*rd;fullMax=Math.Max(fullMax,fd);rigidMax=Math.Max(rigidMax,rd);gapMax=Math.Max(gapMax,gd);
        }
        residual=new JObject{{"samples",original.Length},{"fullMatrixRms",Math.Sqrt(fullSum/original.Length)},
            {"fullMatrixMax",fullMax},{"scaleFreeRms",Math.Sqrt(rigidSum/original.Length)},
            {"scaleFreeMax",rigidMax},{"candidateMaxSeparation",gapMax},{"maxAllowedResidual",0.002},
            {"equivalentCandidateTolerance",0.001},{"units","world units"},
            {"basis","In-memory native four-weight linear skin reference; zero blendshapes. No reference positions or native bindposes exported."}};
        bool scaleFree;
        try {scaleFree=CuffEnvelopeMath.UseScaleFreeBake(fullMax,rigidMax,gapMax);}
        catch(InvalidOperationException){throw new InvalidOperationException("Cuff bake coordinate verification failed: "+residual.ToString(Newtonsoft.Json.Formatting.None));}
        residual["selectedMapping"]=scaleFree?"position-rotation-without-renderer-scale":"full-renderer-localToWorld";
        residual["bakedToWorld"]=Matrix(scaleFree?rigid:full);
        return scaleFree?rigid:full;
    }

    JObject NativeCuffEnvelope(JObject command, CharacterOverworld hero, CharacterEventListener clone)
    {
        RequirePackageFitIsolation();
        if(packageFitUncertain || ExactPackageFitHero(hero.GetInstanceID())!=hero)throw new InvalidOperationException("Certain exact package hero required.");
        BlacksmithGearEntry gear=ResolvePackageFitGear("paladin_armor_censure",PackageFitItems(Str(command,"configSha256")));
        if(hero.m_PlayerInventory.m_ContainerBody.GetOne()!=gear.itemId)throw new InvalidOperationException("Pinned Censure armor must be equipped.");
        JObject request=command["cuffEnvelope"] as JObject;
        if(request==null)throw new ArgumentException("cuffEnvelope must contain exact renderer pins.");
        CatalogKeys(request,"nativeRendererId","nativePath","nativeMesh","nativeMeshInstanceId","nativeMaterialNames","armorRendererId","armorPath","armorMesh");
        SkinnedMeshRenderer body=CuffRenderer(clone,LeaseObservationPin.ExactId(request,"nativeRendererId",false),Str(request,"nativePath"),Str(request,"nativeMesh"));
        SkinnedMeshRenderer armor=CuffRenderer(clone,LeaseObservationPin.ExactId(request,"armorRendererId",false),Str(request,"armorPath"),Str(request,"armorMesh"));
        string bodyPath=Relative(body.transform,clone.transform),armorPath=Relative(armor.transform,clone.transform);
        if(body.transform.parent!=clone.transform || !bodyPath.StartsWith("player",StringComparison.Ordinal) || body.sharedMesh.name.StartsWith("ftkmf_",StringComparison.Ordinal))
            throw new InvalidOperationException("Only the native root body renderer is eligible.");
        if((armorPath!="armorBlacksmithF(Clone)" && armorPath!="armorBlacksmithM(Clone)") || !armor.sharedMesh.name.StartsWith("ftkmf_glb_",StringComparison.Ordinal))
            throw new InvalidOperationException("Exact original Censure armor replacement required.");
        CharacterEventListener prefab=hero.GetSkinset().m_Avatar;
        SkinnedMeshRenderer template=null;
        foreach(SkinnedMeshRenderer renderer in prefab.GetComponentsInChildren<SkinnedMeshRenderer>(true))
            if(Relative(renderer.transform,prefab.transform)==bodyPath)
            {if(template!=null)throw new InvalidOperationException("Ambiguous native body prefab path.");template=renderer;}
        if(template==null || !ReferenceEquals(template.sharedMesh,body.sharedMesh) || body.sharedMesh.GetInstanceID()!=LeaseObservationPin.ExactId(request,"nativeMeshInstanceId",false))
            throw new InvalidOperationException("Body must retain the exact native skinset prefab mesh.");
        Material[] materials=body.sharedMaterials;JArray names=request["nativeMaterialNames"] as JArray;
        if(names==null || materials.Length!=names.Count || materials.Length!=body.sharedMesh.subMeshCount || materials.Length>16)
            throw new InvalidOperationException("Exact native material/submesh slots required.");
        for(int i=0;i<materials.Length;i++)if(materials[i]==null || (string)names[i]!=materials[i].name)throw new InvalidOperationException("Native material identity changed.");
        uiPlayerInventory inventory=uiPlayerInventory.Instance;
        if(!(bool)BlacksmithRead(inventory,"m_ZoomIn"))throw new InvalidOperationException("Native inventory zoom must be settled.");
        Transform[] originalBones=armor.bones,nativeBones=body.bones;
        Matrix4x4[] bindposes=armor.sharedMesh.bindposes;
        if(originalBones==null || originalBones.Length>256 || bindposes.Length!=originalBones.Length || nativeBones==null || nativeBones.Length>256)
            throw new InvalidOperationException("Bounded complete bone palettes required.");
        Mesh source=body.sharedMesh;
        if(source.vertexCount>100000 || source.vertexCount==0)throw new InvalidOperationException("Native body exceeds bounded probe size.");
        BoneWeight[] weights=source.boneWeights;
        if(weights.Length!=source.vertexCount)throw new InvalidOperationException("Native body arm attribution unavailable.");
        int frame=Time.frameCount;Mesh baked=null;JArray arms=new JArray();
        try
        {
            baked=new Mesh();body.BakeMesh(baked);
            Vector3[] vertices=baked.vertices;
            if(vertices.Length!=weights.Length)throw new InvalidOperationException("Baked native body topology changed.");
            JObject bakeResidual;
            Matrix4x4 bakedToWorld=VerifiedCuffBakeMapping(body,source,vertices,weights,nativeBones,out bakeResidual);
            int totalIndices=0;
            for(int side=0;side<2;side++)
            {
                string suffix=side==0?"_R":"_L";
                int elbow=CuffBone(originalBones,"Elbow"+suffix),wrist=CuffBone(originalBones,"Wrist"+suffix);
                int nativeElbow=CuffBone(nativeBones,"Elbow"+suffix),nativeWrist=CuffBone(nativeBones,"Wrist"+suffix);
                if(!ReferenceEquals(originalBones[elbow],nativeBones[nativeElbow]) || !ReferenceEquals(originalBones[wrist],nativeBones[nativeWrist]))
                    throw new InvalidOperationException("Original and native arm bones must be the same transforms.");
                Matrix4x4 skin=originalBones[elbow].localToWorldMatrix*bindposes[elbow];CuffFinite(skin);
                CuffFinite(bindposes[elbow]);CuffFinite(bindposes[wrist]);CuffFinite(body.localToWorldMatrix);
                Matrix4x4 worldToOriginal=skin.inverse,bodyToOriginal=worldToOriginal*bakedToWorld;
                Vector3 origin=bindposes[elbow].inverse.MultiplyPoint3x4(Vector3.zero),end=bindposes[wrist].inverse.MultiplyPoint3x4(Vector3.zero);
                Vector3 delta=end-origin;float length=delta.magnitude;
                if(length<0.05f || length>1f)throw new InvalidOperationException("Original elbow/wrist bind interval invalid.");
                Vector3 axis=delta/length,u=Vector3.up-axis*Vector3.Dot(Vector3.up,axis);
                if(u.magnitude<0.1f)throw new InvalidOperationException("Original radial axis is ambiguous.");
                u.Normalize();Vector3 v=Vector3.Cross(axis,u).normalized;
                CuffEnvelopeMath.Point[] points=new CuffEnvelopeMath.Point[vertices.Length];
                for(int i=0;i<vertices.Length;i++)
                {
                    Vector3 point=bodyToOriginal.MultiplyPoint3x4(vertices[i])-origin;
                    points[i]=new CuffEnvelopeMath.Point(Vector3.Dot(point,axis)/length,Vector3.Dot(point,u),Vector3.Dot(point,v),CuffArmWeight(weights[i],nativeElbow,nativeWrist));
                }
                JArray slots=new JArray();
                for(int slot=0;slot<materials.Length;slot++)
                {
                    if(source.GetTopology(slot)!=MeshTopology.Triangles)throw new InvalidOperationException("Native body slot is not triangles.");
                    int[] indices=source.GetTriangles(slot);totalIndices+=indices.Length;
                    if(indices.Length%3!=0 || totalIndices>1200000)throw new InvalidOperationException("Native triangle scope exceeded.");
                    CuffEnvelopeMath reduced=new CuffEnvelopeMath();
                    for(int i=0;i<indices.Length;i+=3)
                    {
                        int a=indices[i],b=indices[i+1],c=indices[i+2];
                        if(a<0||b<0||c<0||a>=points.Length||b>=points.Length||c>=points.Length)throw new InvalidOperationException("Native body index invalid.");
                        reduced.AddTriangle(points[a],points[b],points[c]);
                    }
                    slots.Add(new JObject{{"materialIndex",slot},{"materialName",materials[slot].name},{"materialInstanceId",materials[slot].GetInstanceID()},
                        {"semantic","unknown; exact native material slot identity only"},{"envelope",CuffSupports(reduced)}});
                }
                arms.Add(new JObject{{"side",suffix},{"elbowBone",originalBones[elbow].name},{"wristBone",originalBones[wrist].name},
                    {"elbowBoneWorld",Matrix(originalBones[elbow].localToWorldMatrix)},{"originalElbowInverseBind",Matrix(bindposes[elbow])},
                    {"originalWristInverseBind",Matrix(bindposes[wrist])},{"worldToOriginalElbowSkin",Matrix(worldToOriginal)},
                    {"bakedToWorld",Matrix(bakedToWorld)},{"nativeRendererLocalToWorld",Matrix(body.localToWorldMatrix)},{"originalRendererLocalToWorld",Matrix(armor.localToWorldMatrix)},
                    {"origin",Vec(origin)},{"endpoint",Vec(end)},{"axis",Vec(axis)},{"radialU",Vec(u)},{"radialV",Vec(v)},
                    {"length",length},{"slots",slots}});
            }
            if(Time.frameCount!=frame || inventory.m_InventoryOwner!=hero || inventory.m_IsUpdatePaperDoll || clone.m_OffscreenCamera==null || clone.m_OffscreenCamera.m_TargetObject!=clone.gameObject || body.sharedMesh!=source)
                throw new InvalidOperationException("Inventory pose/owner changed during cuff observation.");
            return new JObject{{"frame",frame},{"configSha256",packageGearConfigHash},{"nativeRendererId",body.GetInstanceID()},
                {"nativePath",bodyPath},{"nativeMeshInstanceId",source.GetInstanceID()},{"armorRendererId",armor.GetInstanceID()},
                {"armorMesh",armor.sharedMesh.name},{"inventoryCelInstanceId",clone.GetInstanceID()},
                {"bakeMappingVerification",bakeResidual},{"nativeSkinQuality",body.quality.ToString()},{"globalBlendWeights",QualitySettings.blendWeights.ToString()},
                {"filter","Clip native triangles to same-arm Elbow/Wrist weight>=0.25, then three axial slabs [0,1/3,2/3,1]. Reject a slab polygon if any radial support point exceeds0.35 original mesh units; rejected counts are explicit."},
                {"scope","CPU BakeMesh aggregate supports only; not GPU raster coverage. No native points, indices, normals, UVs or weights are exported. Material semantics remain unknown."},{"arms",arms}};
        }
        finally {if(baked!=null)UnityEngine.Object.DestroyImmediate(baked);}
    }
}
