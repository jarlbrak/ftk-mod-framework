using System;
using System.IO;
using System.Reflection;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using GridEditor;

public sealed partial class RuntimeModelTest
{
    static int PortraitCaptureInt(JObject command,string name)
    {
        JToken value=command[name];
        if(value==null || value.Type!=JTokenType.Integer || (long)value<int.MinValue || (long)value>int.MaxValue)
            throw new ArgumentException("Exact integer required: "+name);
        return (int)value;
    }
    static JObject PortraitRect(Rect value){return new JObject{{"x",value.x},{"y",value.y},{"width",value.width},{"height",value.height}};}
    static JArray PortraitColor(Color value){return new JArray(value.r,value.g,value.b,value.a);}
    static string PortraitUiPath(Transform value)
    {
        List<string> parts=new List<string>();int count=0;
        while(value!=null){if(++count>64)throw new InvalidOperationException("UI hierarchy exceeds bound.");parts.Add(value.name);value=value.parent;}
        parts.Reverse();return string.Join("/",parts.ToArray());
    }
    static JObject PortraitExistingMaterial(Material value)
    {
        if(value==null)return null;
        JObject result=new JObject{{"instanceId",value.GetInstanceID()},{"name",value.name},{"shader",value.shader==null?null:value.shader.name}};
        foreach(string property in new[]{"_Stencil","_StencilComp","_StencilOp","_StencilReadMask","_StencilWriteMask","_ColorMask","_UseUIAlphaClip"})
            if(value.HasProperty(property))result[property]=value.GetFloat(property);
        if(value.HasProperty("_Color"))result["color"]=PortraitColor(value.GetColor("_Color"));
        if(value.HasProperty("_MainTex"))result["mainTexture"]=PortraitTexture(value.GetTexture("_MainTex"));
        return result;
    }
    static JArray PortraitActiveImages(Texture texture)
    {
        RawImage[] all=Resources.FindObjectsOfTypeAll<RawImage>();
        if(all.Length>4096)throw new InvalidOperationException("RawImage inventory exceeds bound.");
        JArray result=new JArray();
        foreach(RawImage image in all)
        {
            if(image==null || !image.gameObject.scene.IsValid() || !image.isActiveAndEnabled || image.texture!=texture)continue;
            if(result.Count>=64)throw new InvalidOperationException("Matching RawImage inventory exceeds bound.");
            JArray masks=new JArray();Transform parent=image.transform;int depth=0;
            while(parent!=null)
            {
                if(++depth>64)throw new InvalidOperationException("UI mask ancestry exceeds bound.");
                foreach(Mask mask in parent.GetComponents<Mask>())masks.Add(new JObject{{"type","Mask"},{"instanceId",mask.GetInstanceID()},
                    {"enabled",mask.enabled},{"active",mask.gameObject.activeInHierarchy},{"showMaskGraphic",mask.showMaskGraphic},{"path",PortraitUiPath(parent)}});
                foreach(RectMask2D mask in parent.GetComponents<RectMask2D>())masks.Add(new JObject{{"type","RectMask2D"},{"instanceId",mask.GetInstanceID()},
                    {"enabled",mask.enabled},{"active",mask.gameObject.activeInHierarchy},{"path",PortraitUiPath(parent)}});
                parent=parent.parent;
            }
            // Graphic.materialForRendering invokes modifiers; inspect assigned/existing CanvasRenderer materials only.
            FieldInfo materialField=typeof(Graphic).GetField("m_Material",Members);
            if(materialField==null)throw new MissingFieldException("Graphic.m_Material");
            JArray existing=new JArray();CanvasRenderer canvas=image.canvasRenderer;
            if(canvas.materialCount>16)throw new InvalidOperationException("Canvas material count exceeds bound.");
            for(int i=0;i<canvas.materialCount;i++)existing.Add(PortraitExistingMaterial(canvas.GetMaterial(i)));
            Vector3[] corners=new Vector3[4];image.rectTransform.GetWorldCorners(corners);JArray world=new JArray();foreach(Vector3 corner in corners)world.Add(Vec(corner));
            result.Add(new JObject{{"instanceId",image.GetInstanceID()},{"path",PortraitUiPath(image.transform)},
                {"textureInstanceId",image.texture.GetInstanceID()},{"uvRect",PortraitRect(image.uvRect)},{"color",PortraitColor(image.color)},
                {"rect",PortraitRect(image.rectTransform.rect)},{"lossyScale",Vec(image.transform.lossyScale)},{"worldCorners",world},
                {"canvasAlpha",canvas.GetAlpha()},{"canvasCull",canvas.cull},{"maskable",image.maskable},{"masks",masks},
                {"assignedMaterial",PortraitExistingMaterial(materialField.GetValue(image)as Material)},{"existingCanvasMaterials",existing},
                {"uiActiveTimePortraitAncestor",image.GetComponentInParent<uiActiveTimePortrait>()==null?0:image.GetComponentInParent<uiActiveTimePortrait>().GetInstanceID()},
                {"scope","active loaded-scene RawImage using exact texture; path/ancestor identify UI, no inferred screen pixel size or cache reuse"}});
        }
        return result;
    }
    JObject PortraitTextureCapture(JObject command)
    {
        CatalogKeys(command,"id","session","op","enemy","enemyDummyInstanceId","photonId","turnIndex","expectedTextureId","traceFrame","traceArmCommandId");
        RequireSinglePlayer();CatalogNoLinks(root);CatalogNoLinks(output);
        if(Environment.GetEnvironmentVariable("FTK_MODEL_TEST")!="1" || portraitNonce!=sessionId || portraitRoot!=root
            || Path.GetFullPath(BepInEx.Paths.GameRootPath).TrimEnd(Path.DirectorySeparatorChar)!=root
            || string.IsNullOrEmpty(Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT"))
            || Path.GetFullPath(Environment.GetEnvironmentVariable("FTK_MODEL_TEST_ROOT")).TrimEnd(Path.DirectorySeparatorChar)!=root
            || Str(command,"session")!=sessionId || Str(command,"traceArmCommandId")!=portraitArmId)
            throw new InvalidOperationException("Exact isolated passive portrait session/arm required.");
        string key=Str(command,"enemy");int dummyId=PortraitCaptureInt(command,"enemyDummyInstanceId"),textureId=PortraitCaptureInt(command,"expectedTextureId"),traceFrame=PortraitCaptureInt(command,"traceFrame");
        int photon=PortraitCaptureInt(command,"photonId"),turn=PortraitCaptureInt(command,"turnIndex");
        FTK_enemyCombat row=FTK_enemyCombatDB.GetDB().GetEntryByStringID(key);
        if(row==null || !object.ReferenceEquals(row,portraitRow) || row.m_ID!=key || row.m_EnemyAsset!=portraitRowSource)
            throw new InvalidOperationException("Exact currently watched registered row required.");
        EncounterSession encounter=EncounterSession.Instance;
        if(encounter==null || !(bool)typeof(EncounterSession).GetField("m_IsInCombat",Members).GetValue(encounter))throw new InvalidOperationException("Current native combat required.");
        EnemyDummy dummy=null;
        foreach(EnemyDummy candidate in encounter.m_EnemyDummies.Values)
            if(candidate!=null && candidate.GetInstanceID()==dummyId){if(dummy!=null)throw new InvalidOperationException("Ambiguous live dummy.");dummy=candidate;}
        if(dummy==null || !dummy.gameObject.scene.IsValid() || !dummy.gameObject.activeInHierarchy || dummy.m_EventListener==null
            || !object.ReferenceEquals(dummy.m_EnemyCombat,row) || dummy.m_EnemyType!=key
            || Convert.ToInt32(typeof(FTKPlayerID).GetField("m_PhotonID",Members).GetValue(dummy.FID))!=photon
            || Convert.ToInt32(typeof(FTKPlayerID).GetField("m_TurnIndex",Members).GetValue(dummy.FID))!=turn)
            throw new InvalidOperationException("Exact active enemy dummy/FID/row required.");
        JObject trace=null;
        foreach(JObject record in portraitTrace.Read())
            if((string)record["session"]==sessionId && (int)record["frame"]==traceFrame && (string)record["row"]==key
                && (string)record["identityResolution"]=="verified_live_enemy_dummy" && (int)record["sourceCelInstanceId"]==dummy.m_EventListener.GetInstanceID()
                && record["textureRequested"]as JObject!=null && record["textureCurrent"]as JObject!=null
                && (int)record["textureRequested"]["instanceId"]==textureId && (int)record["textureCurrent"]["instanceId"]==textureId)
            {if(trace!=null)throw new InvalidOperationException("Ambiguous passive trace record.");trace=record;}
        if(!PortraitFinalization.Successful(trace)
            || portraitErrors!=0 || Time.frameCount<=traceFrame)throw new InvalidOperationException("Complete prior passive trace for exact current source/texture required.");
        uiActiveTime timeline=uiActiveTime.Instance;Texture native;
        if(timeline==null || !timeline.gameObject.scene.IsValid() || !timeline.m_PortaitTextures.TryGetValue(dummy.FID,out native))throw new InvalidOperationException("Current native FID portrait entry absent.");
        Texture2D texture=native as Texture2D;
        if(texture==null || texture.GetInstanceID()!=textureId || texture.width!=204 || texture.height!=172)
            throw new InvalidOperationException("Exact traced Texture2D204x172 required; no resize/render fallback.");
        JArray images=PortraitActiveImages(texture);
        byte[] png=texture.EncodeToPNG(); // Native CPU texture read only. Unreadable textures fail; no GPU fallback.
        if(png==null || png.Length<24 || png.Length>4*1024*1024)throw new InvalidOperationException("Native PNG encoding failed/exceeded bound.");
        Texture after;int currentOwners=0;
        foreach(EnemyDummy candidate in encounter.m_EnemyDummies.Values)if(candidate==dummy)currentOwners++;
        if(EncounterSession.Instance!=encounter || uiActiveTime.Instance!=timeline || currentOwners!=1 || dummy==null || !dummy.gameObject.activeInHierarchy
            || !object.ReferenceEquals(dummy.m_EnemyCombat,row) || dummy.m_EnemyType!=key || dummy.m_EventListener==null
            || Convert.ToInt32(typeof(FTKPlayerID).GetField("m_PhotonID",Members).GetValue(dummy.FID))!=photon
            || Convert.ToInt32(typeof(FTKPlayerID).GetField("m_TurnIndex",Members).GetValue(dummy.FID))!=turn
            || !(bool)typeof(EncounterSession).GetField("m_IsInCombat",Members).GetValue(encounter)
            || !timeline.m_PortaitTextures.TryGetValue(dummy.FID,out after) || after!=texture || dummy.m_EventListener.GetInstanceID()!=(int)trace["sourceCelInstanceId"])
            throw new InvalidOperationException("Native portrait identity changed during readback.");
        string name=Token(Str(command,"id"))+".portrait.png",path=Path.Combine(output,name),temporary=path+".tmp";
        if(File.Exists(path)||File.Exists(temporary))throw new IOException("Portrait output already exists.");
        using(FileStream stream=new FileStream(temporary,FileMode.CreateNew,FileAccess.Write,FileShare.None))stream.Write(png,0,png.Length);
        File.Move(temporary,path);
        return new JObject{{"ok",true},{"provenance","existing_native_hud_portrait_texture_readback"},{"request",command.DeepClone()},
            {"enemy",key},{"enemyDummyInstanceId",dummyId},{"sourceCelInstanceId",dummy.m_EventListener.GetInstanceID()},
            {"fid",new JObject{{"photonId",photon},{"turnIndex",turn}}},{"timelineInstanceId",timeline.GetInstanceID()},
            {"exportFrame",Time.frameCount},{"exportTime",Time.time},{"exportUnscaledTime",Time.unscaledTime},{"texture",PortraitTexture(texture)},{"textureFormat",texture.format.ToString()},{"filterMode",texture.filterMode.ToString()},{"wrapMode",texture.wrapMode.ToString()},
            {"png",new JObject{{"path",path},{"sha256",CatalogHash(path)},{"bytes",png.Length}}},{"activeRawImages",images},{"uiMetadataTruncated",false},
            {"passiveTrace",trace.DeepClone()},{"traceArmCommandId",portraitArmId},{"armedEvidence",portraitEvidence.DeepClone()},
            {"limitations","CURRENT CPU Texture2D pixels at export frame; same texture ID does not establish unchanged pixels since pre-DoRender trace; no render, camera, pose, texture/material mutation or retained Unity references. UI metadata is observed now, not baked into PNG. RawImage absence is reported, not inferred as unused. Same texture identity alone does not prove cache reuse, visible sampling, or artistic quality."}};
    }
}
