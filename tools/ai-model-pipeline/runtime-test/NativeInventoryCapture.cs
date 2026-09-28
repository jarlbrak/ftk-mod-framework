using System;
using System.IO;
using UnityEngine;
using Newtonsoft.Json.Linq;

public sealed partial class RuntimeModelTest
{
    JObject NativeInventoryCapture(JObject command)
    {
        CatalogKeys(command,"id","session","op","ownerInstanceId","celInstanceId","inventoryCelInstanceId","inventoryCameraInstanceId");
        RequirePackageFitIsolation(); CatalogNoLinks(output);
        int ownerId=LeaseObservationPin.ExactId(command,"ownerInstanceId",false);
        int sourceId=LeaseObservationPin.ExactId(command,"celInstanceId",false);
        int cloneId=LeaseObservationPin.ExactId(command,"inventoryCelInstanceId",false);
        int cameraId=LeaseObservationPin.ExactId(command,"inventoryCameraInstanceId",false);
        CharacterOverworld hero=ExactPackageFitHero(ownerId);
        CharacterEventListener source=hero.m_Avatar;
        if(source==null || source.GetInstanceID()!=sourceId || source.m_CharacterOverworld!=hero)
            throw new InvalidOperationException("Exact reciprocal world source avatar required.");
        uiPlayerInventory inventory=uiPlayerInventory.Instance;
        if(inventory==null || !SceneOwner(inventory) || !inventory.gameObject.activeInHierarchy ||
            !inventory.m_IsShowing || inventory.m_IsUpdatePaperDoll || inventory.m_InventoryOwner!=hero)
            throw new InvalidOperationException("Settled native inventory for exact owner required.");
        OffscreenCamera stream=typeof(uiPlayerInventory).GetField("m_OffscreenCamera",Members).GetValue(inventory)as OffscreenCamera;
        if(stream==null || stream.GetInstanceID()!=cameraId || !SceneOwner(stream) || !stream.m_IsRendering ||
            stream.m_TargetObject==null || inventory.m_PaperDoll==null)
            throw new InvalidOperationException("Exact existing native inventory stream required.");
        CharacterEventListener clone=stream.m_TargetObject.GetComponent<CharacterEventListener>();
        if(clone==null || clone.GetInstanceID()!=cloneId || !SceneOwner(clone) || !clone.gameObject.activeInHierarchy ||
            clone==source || clone.m_OffscreenCamera!=stream || clone.m_CharacterOverworld!=hero)
            throw new InvalidOperationException("Exact reciprocal inventory clone required.");
        Camera camera=stream.GetComponent<Camera>(); RenderTexture texture=stream.m_RenderTexture;
        if(camera==null || texture==null || inventory.m_PaperDoll.texture!=texture || camera.targetTexture!=texture || !texture.IsCreated() ||
            texture.width<16 || texture.height<16 || texture.width>2048 || texture.height>2048)
            throw new InvalidOperationException("Existing bounded native paperdoll render texture required; no render fallback.");
        JObject appearance=BlacksmithAppearanceStudio(ownerId,sourceId);
        string path=Path.Combine(output,Token(Str(command,"id"))+".inventory.png");
        if(File.Exists(path))throw new IOException("Inventory capture output already exists.");
        int frame=Time.frameCount; RenderTexture prior=RenderTexture.active; Texture2D image=null;
        byte[] png; JObject pixels;
        try
        {
            // Read the existing stream only. Never invoke the camera or change native render state.
            RenderTexture.active=texture;
            image=new Texture2D(texture.width,texture.height,TextureFormat.RGBA32,false);
            image.ReadPixels(new Rect(0,0,texture.width,texture.height),0,0); image.Apply();
            pixels=NativeInventoryPixelSummary(image.GetPixels32()); png=image.EncodeToPNG();
        }
        finally
        {
            RenderTexture.active=prior;
            if(image!=null)UnityEngine.Object.DestroyImmediate(image);
        }
        if(uiPlayerInventory.Instance!=inventory || inventory.m_IsUpdatePaperDoll || !inventory.m_IsShowing || inventory.m_InventoryOwner!=hero ||
            hero.m_Avatar!=source || stream.m_TargetObject!=clone.gameObject || stream.m_RenderTexture!=texture ||
            inventory.m_PaperDoll.texture!=texture || camera.targetTexture!=texture || !stream.m_IsRendering || Time.frameCount!=frame)
            throw new InvalidOperationException("Native inventory stream changed during readback.");
        if(png==null || png.Length<24 || png.Length>20*1024*1024)throw new InvalidOperationException("Inventory PNG outside bounded output size.");
        using(FileStream file=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None))file.Write(png,0,png.Length);
        return new JObject{{"ok",true},{"readOnly",true},{"session",sessionId},{"provenance","existing_native_inventory_render_texture_readback"},
            {"ownerInstanceId",ownerId},{"sourceCelInstanceId",sourceId},{"inventoryCelInstanceId",cloneId},
            {"inventoryCameraInstanceId",cameraId},{"cameraInstanceId",camera.GetInstanceID()},{"textureInstanceId",texture.GetInstanceID()},
            {"readbackFrame",frame},{"lastNativeRenderFrame",null},{"width",texture.width},{"height",texture.height},
            {"renderTextureFormat",texture.format.ToString()},{"renderTextureSRGB",texture.sRGB},{"activeColorSpace",QualitySettings.activeColorSpace.ToString()},
            {"pixelSummary",pixels},{"imageEvidenceAvailable",!(bool)pixels["blankCandidate"]},
            {"png",path},{"sha256",CatalogHash(path)},{"bytes",png.Length},{"equipment",EquipmentView(hero)},{"appearanceFixture",appearance},
            {"coreIdentity",PreviewCoreIdentity()},{"helperIdentity",ScaleIdentity(typeof(RuntimeModelTest).Assembly)},
            {"configSha256",packageGearConfigHash},{"candidateSources",packageFitSources},{"registeredAssets",packageFitAssets},
            {"scope","Existing native paperdoll render texture copied without forced render, camera, lighting, layer, pose or material changes. Readback frame is known; last native render frame and freshness are not observed. Blank or uniform pixels provide no avatar evidence. PNG is raw readback with no manual gamma correction; this is not a full UI framebuffer or gameplay acceptance."}};
    }

    static JObject NativeInventoryPixelSummary(Color32[] pixels)
    {
        if(pixels==null || pixels.Length==0)throw new InvalidOperationException("Empty native inventory readback.");
        int visible=0,nonblack=0; bool uniform=true; Color32 first=pixels[0];
        foreach(Color32 pixel in pixels)
        {
            if(pixel.a>0)visible++;
            if(pixel.a>0 && (pixel.r>2 || pixel.g>2 || pixel.b>2))nonblack++;
            if(pixel.r!=first.r || pixel.g!=first.g || pixel.b!=first.b || pixel.a!=first.a)uniform=false;
        }
        return new JObject{{"pixelCount",pixels.Length},{"visiblePixelCount",visible},{"nonblackVisiblePixelCount",nonblack},
            {"uniform",uniform},{"blankCandidate",uniform || nonblack==0},
            {"scope","Conservative transparent, black or uniform image detection only; nonblank does not prove an avatar is present."}};
    }
}
