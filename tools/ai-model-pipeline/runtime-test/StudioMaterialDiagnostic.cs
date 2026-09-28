using System;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    JObject RenderStudioMaterialDiagnostic(CharacterEventListener avatar,JObject request,Action render)
    {
        CatalogKeys(request,"inventoryCelInstanceId","rendererPath","materialIndex","expectedMaterialName");
        if(packageFitUncertain)throw new InvalidOperationException("Uncertain fit session cannot run a material diagnostic.");
        int cloneId=LeaseObservationPin.ExactId(request,"inventoryCelInstanceId",false);
        CharacterOverworld owner=avatar==null?null:avatar.m_CharacterOverworld;
        if(avatar==null || avatar.GetInstanceID()!=cloneId || owner==null || !owner.IsOwner ||
            owner.m_Avatar==avatar || avatar.m_OffscreenCamera==null ||
            avatar.m_OffscreenCamera.m_TargetObject!=avatar.gameObject)
            throw new InvalidOperationException("Exact owned native inventory clone required for material diagnostic.");
        string path=Str(request,"rendererPath"),expected=Str(request,"expectedMaterialName");
        JToken indexToken=request["materialIndex"];
        if(string.IsNullOrEmpty(path) || string.IsNullOrEmpty(expected) || indexToken==null || indexToken.Type!=JTokenType.Integer)
            throw new ArgumentException("Exact renderer path, material name and integer material index required.");
        long indexValue=(long)indexToken;
        if(indexValue<0 || indexValue>=32)throw new ArgumentException("Material index outside diagnostic limit.");
        int index=(int)indexValue,matches=0;SkinnedMeshRenderer selected=null;
        SkinnedMeshRenderer[] candidates=avatar.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if(candidates.Length>128)throw new InvalidOperationException("Renderer count outside diagnostic limit.");
        foreach(SkinnedMeshRenderer candidate in candidates)
            if(candidate!=null && Relative(candidate.transform,avatar.transform)==path){selected=candidate;matches++;}
        if(matches!=1 || selected.sharedMesh==null || selected.sharedMesh.name.StartsWith("ftkmf_",StringComparison.Ordinal) ||
            !selected.enabled || !selected.gameObject.activeInHierarchy)
            throw new InvalidOperationException("Exactly one active native skinned renderer must match the path.");
        Material[] original=selected.sharedMaterials;
        if(original.Length>32 || index>=original.Length || index>=selected.sharedMesh.subMeshCount ||
            original[index]==null || !string.Equals(original[index].name,expected,StringComparison.Ordinal))
            throw new InvalidOperationException("Native material slot identity mismatch.");
        Shader shader=original[index].shader;
        if(shader==null || shader.name!="Standard" || !shader.isSupported)
            throw new InvalidOperationException("Selected native material must already use supported Standard shader.");
        foreach(string property in new[]{"_Color","_EmissionColor","_Metallic","_Glossiness","_Mode","_SrcBlend","_DstBlend","_ZWrite"})
            if(!original[index].HasProperty(property))throw new InvalidOperationException("Native Standard property unavailable: "+property);
        JObject receipt=new JObject{{"diagnosticOnly",true},{"artAcceptanceEligible",false},
            {"inventoryCelInstanceId",cloneId},{"rendererInstanceId",selected.GetInstanceID()},
            {"rendererPath",path},{"mesh",selected.sharedMesh.name},{"materialIndex",index},
            {"originalMaterial",ItemMaterialObservation(original[index])},{"color",new JArray(1f,0f,1f,1f)},
            {"shader",shader.name},{"frame",Time.frameCount}};
        Material temporary=null;
        try
        {
            temporary=new Material(shader);temporary.name="FTK inventory material-slot diagnostic";
            temporary.color=new Color(1f,0f,1f,1f);
            temporary.SetColor("_EmissionColor",new Color(1f,0f,1f,1f));
            temporary.SetFloat("_Metallic",0f);temporary.SetFloat("_Glossiness",0f);
            temporary.SetFloat("_Mode",0f);temporary.SetFloat("_SrcBlend",1f);
            temporary.SetFloat("_DstBlend",0f);temporary.SetFloat("_ZWrite",1f);
            temporary.SetOverrideTag("RenderType","Opaque");temporary.renderQueue=2000;
            temporary.shaderKeywords=new[]{"_EMISSION"};
            JObject clearedMaps=new JObject();
            foreach(string property in new[]{"_MainTex","_EmissionMap","_MetallicGlossMap","_SpecGlossMap","_BumpMap",
                "_ParallaxMap","_OcclusionMap","_DetailMask","_DetailAlbedoMap","_DetailNormalMap"})
                if(temporary.HasProperty(property))
                {
                    temporary.SetTexture(property,null);
                    if(temporary.GetTexture(property)!=null)throw new InvalidOperationException("Diagnostic map was not cleared: "+property);
                    clearedMaps[property]=new JValue((object)null);
                }
            receipt["temporaryMaterial"]=ItemMaterialObservation(temporary);
            receipt["clearedTextureMaps"]=clearedMaps;
            receipt["renderState"]=new JObject{{"mode",temporary.GetFloat("_Mode")},{"sourceBlend",temporary.GetFloat("_SrcBlend")},
                {"destinationBlend",temporary.GetFloat("_DstBlend")},{"depthWrite",temporary.GetFloat("_ZWrite")},
                {"renderQueue",temporary.renderQueue},{"renderType",temporary.GetTag("RenderType",false)},
                {"depthTest","Existing Standard shader pass depth test, not overridden"}};
            receipt["opacity"]="Opaque Standard with color and emission alpha 1; textures cleared, metallic and glossiness zero";
            Material[] replacement=(Material[])original.Clone();replacement[index]=temporary;
            // The native material objects remain untouched. Only this clone's slot references change.
            selected.sharedMaterials=replacement;
            render();
        }
        finally
        {
            try
            {
                if(selected==null)throw new InvalidOperationException("Diagnostic renderer disappeared before restoration.");
                selected.sharedMaterials=original;
                Material[] restored=selected.sharedMaterials;
                if(restored.Length!=original.Length)throw new InvalidOperationException("Diagnostic material count was not restored.");
                for(int slot=0;slot<original.Length;slot++)
                    if(restored[slot]!=original[slot])throw new InvalidOperationException("Diagnostic material identity was not restored.");
                receipt["sharedMaterialsRestored"]=true;
            }
            catch(Exception error)
            {
                packageFitUncertain=true;
                packageFitFailure=new JObject{{"operation","player-studio-native-material-diagnostic"},
                    {"error",error.ToString()},{"diagnostics",receipt},{"requiresFreshSession",true}};
                throw;
            }
            finally { if(temporary!=null)UnityEngine.Object.DestroyImmediate(temporary); }
        }
        return receipt;
    }
}
