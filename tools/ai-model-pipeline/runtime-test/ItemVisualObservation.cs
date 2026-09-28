using System;
using System.Reflection;
using System.IO;
using GridEditor;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using UnityEngine.UI;

public sealed partial class RuntimeModelTest
{
    static RuntimeModelTest itemCardObserver;
    JArray itemCardRenderObservations;
    int itemCardObservedItemId = -1;

    static JObject ItemTransform(Transform transform, Transform root)
    {
        return new JObject { { "path", Relative(transform, root) }, { "name", transform.name },
            { "localPosition", Vec(transform.localPosition) }, { "localRotation", Quat(transform.localRotation) },
            { "localScale", Vec(transform.localScale) }, { "localToWorld", Matrix(transform.localToWorldMatrix) } };
    }

    static JObject ItemMaterialObservation(Material material)
    {
        if (material == null) return null;
        JObject colors = new JObject(), scalars = new JObject(), textures = new JObject();
        foreach (string name in new[] { "_Color", "_SpecColor", "_EmissionColor" })
        {
            bool present = material.HasProperty(name);
            colors[name] = new JObject { { "present", present },
                { "value", present ? (JToken)PreviewRaceColor(material.GetColor(name)) : new JValue((object)null) } };
        }
        foreach (string name in new[] { "_Glossiness", "_Shininess", "_Metallic", "_Smoothness", "_GlossMapScale" })
        {
            bool present = material.HasProperty(name);
            scalars[name] = new JObject { { "present", present },
                { "value", present ? new JValue(material.GetFloat(name)) : new JValue((object)null) } };
        }
        foreach (string name in new[] { "_MainTex", "_EmissionMap", "_SpecGlossMap", "_MetallicGlossMap", "_BumpMap", "_Cube", "_MatCap" })
        {
            bool present = material.HasProperty(name);
            Texture texture = present ? material.GetTexture(name) : null;
            textures[name] = new JObject { { "present", present }, { "texture", ItemTextureObservation(texture) } };
        }
        return new JObject { { "instanceId", material.GetInstanceID() }, { "name", material.name },
            { "shader", material.shader == null ? null : material.shader.name },
            { "shaderInstanceId", material.shader == null ? 0 : material.shader.GetInstanceID() },
            { "renderQueue", material.renderQueue }, { "texture", material.mainTexture == null ? null : material.mainTexture.name },
            { "mainTexture", ItemTextureObservation(material.mainTexture) },
            { "shaderKeywords", new JArray(material.shaderKeywords) }, { "colors", colors }, { "scalars", scalars }, { "textures", textures } };
    }
    static JObject ItemTextureObservation(Texture texture)
    {
        return texture == null ? null : new JObject { { "instanceId", texture.GetInstanceID() },
            { "name", texture.name }, { "width", texture.width }, { "height", texture.height },
            { "format", texture is Texture2D ? ((Texture2D)texture).format.ToString() : null } };
    }
    static JArray OriginalApparelMaterials(GameObject root)
    {
        JArray result = new JArray();
        foreach (SkinnedMeshRenderer renderer in root.GetComponentsInChildren<SkinnedMeshRenderer>(true))
        {
            Mesh mesh = renderer.sharedMesh;
            // Inspect only original replacement mesh identities, never native mesh data.
            if (mesh == null || !mesh.name.StartsWith("ftkmf_", StringComparison.Ordinal)) continue;
            Transform[] bones = renderer.bones;
            SkinQuality rendererQuality = renderer.quality;
            BlendWeights globalQuality = QualitySettings.blendWeights;
            int influenceLimit = rendererQuality == SkinQuality.Auto ? (int)globalQuality : (int)rendererQuality;
            if (bones == null || bones.Length > 256) throw new InvalidOperationException("Original apparel bone observation exceeds bounded scope.");
            JArray boneTransforms = new JArray();
            for (int index = 0; index < bones.Length; index++)
            {
                Transform bone = bones[index];
                boneTransforms.Add(new JObject { { "index", index }, { "name", bone == null ? null : bone.name },
                    { "instanceId", bone == null ? 0 : bone.GetInstanceID() },
                    { "localToWorld", bone == null ? null : Matrix(bone.localToWorldMatrix) } });
            }
            JArray materials = new JArray();
            foreach (Material material in renderer.sharedMaterials) materials.Add(ItemMaterialObservation(material));
            result.Add(new JObject { { "rendererInstanceId", renderer.GetInstanceID() }, { "path", Relative(renderer.transform, root.transform) },
                { "mesh", mesh.name }, { "enabled", renderer.enabled }, { "active", renderer.gameObject.activeInHierarchy },
                { "layer", renderer.gameObject.layer }, { "materials", materials },
                { "localToWorld", Matrix(renderer.localToWorldMatrix) }, { "bones", boneTransforms },
                { "skinQuality", rendererQuality.ToString() }, { "globalBlendWeights", globalQuality.ToString() },
                { "derivedInfluenceLimit", influenceLimit == 1 || influenceLimit == 2 || influenceLimit == 4 ? new JValue(influenceLimit) : new JValue((object)null) },
                { "influenceLimitBasis", "Derived from renderer quality, or global blendWeights for Auto. Shader/backend execution is not observed." } });
        }
        return result;
    }
    static JArray NativeSkinnedRendererMetadata(GameObject root)
    {
        JArray result = new JArray();
        SkinnedMeshRenderer[] renderers = root.GetComponentsInChildren<SkinnedMeshRenderer>(true);
        if (renderers.Length > 128) throw new InvalidOperationException("Native renderer metadata exceeds bounded scope.");
        foreach (SkinnedMeshRenderer renderer in renderers)
        {
            Mesh mesh = renderer.sharedMesh;
            if (mesh == null || mesh.name.StartsWith("ftkmf_", StringComparison.Ordinal)) continue;
            Material[] assigned = renderer.sharedMaterials;
            if (assigned.Length > 32) throw new InvalidOperationException("Native material slot metadata exceeds bounded scope.");
            JArray materials = new JArray();
            for (int slot = 0; slot < assigned.Length; slot++)
                materials.Add(new JObject { { "slot", slot }, { "material", ItemMaterialObservation(assigned[slot]) } });
            result.Add(new JObject { { "rendererInstanceId", renderer.GetInstanceID() },
                { "path", Relative(renderer.transform, root.transform) }, { "mesh", mesh.name }, { "meshInstanceId", mesh.GetInstanceID() },
                { "subMeshCount", mesh.subMeshCount }, { "enabled", renderer.enabled }, { "active", renderer.gameObject.activeInHierarchy },
                { "layer", renderer.gameObject.layer }, { "materials", materials },
                { "scope", "Native renderer and material-slot identity only. Submesh geometry and which body region each slot covers are not observed." } });
        }
        return result;
    }
    static JObject ItemSceneLighting()
    {
        JArray lights = new JArray();
        Light[] current = UnityEngine.Object.FindObjectsOfType<Light>();
        if (current.Length > 256) throw new InvalidOperationException("Scene lighting observation exceeds bounded scope.");
        foreach (Light light in current)
            lights.Add(new JObject { { "instanceId", light.GetInstanceID() }, { "name", light.name },
                { "type", light.type.ToString() }, { "intensity", light.intensity }, { "color", PreviewRaceColor(light.color) },
                { "enabled", light.enabled }, { "active", light.gameObject.activeInHierarchy }, { "cullingMask", light.cullingMask } });
        return new JObject { { "ambientColor", PreviewRaceColor(RenderSettings.ambientLight) }, { "lights", lights },
            { "scope", "Current scene outside studio capture. Studio adds its own key/fill lights temporarily and does not suppress existing lights." } };
    }

    static JArray OriginalItemRenderers(GameObject root)
    {
        JArray result = new JArray();
        if (root == null) return result;
        foreach (MeshRenderer renderer in root.GetComponentsInChildren<MeshRenderer>(true))
        {
            MeshFilter filter = renderer.GetComponent<MeshFilter>();
            Mesh mesh = filter == null ? null : filter.sharedMesh;
            // Bounds are exported only for original framework meshes, never native surfaces.
            if (mesh == null || !mesh.name.StartsWith("ftkmf_", StringComparison.Ordinal)) continue;
            JArray materials = new JArray();
            foreach (Material material in renderer.sharedMaterials)
                materials.Add(ItemMaterialObservation(material));
            JObject record = ItemTransform(renderer.transform, root.transform);
            record["mesh"] = mesh.name; record["enabled"] = renderer.enabled;
            record["active"] = renderer.gameObject.activeInHierarchy;
            record["localBoundsCenter"] = Vec(mesh.bounds.center); record["localBoundsSize"] = Vec(mesh.bounds.size);
            record["worldBoundsCenter"] = Vec(renderer.bounds.center); record["worldBoundsSize"] = Vec(renderer.bounds.size);
            record["materials"] = materials;
            result.Add(record);
        }
        return result;
    }

    JObject ItemVisualState(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "heroInstanceId", "celInstanceId", "source", "inventoryCelInstanceId", "configSha256", "cuffEnvelope");
        RequireSinglePlayer(); CatalogNoLinks(root);
        int heroId = LeaseObservationPin.ExactId(command, "heroInstanceId", false);
        CharacterOverworld hero = null;
        if (FTKHub.Instance == null || FTKHub.Instance.m_CharacterOverworlds == null)
            throw new InvalidOperationException("Current native party required.");
        foreach (CharacterOverworld candidate in FTKHub.Instance.m_CharacterOverworlds)
            if (candidate != null && candidate.GetInstanceID() == heroId) hero = candidate;
        if (hero == null) throw new InvalidOperationException("Exact current-party hero required.");
        CharacterEventListener cel = hero.m_Avatar;
        int celId = Int(command, "celInstanceId", cel == null ? 0 : cel.GetInstanceID());
        if (hero.m_CurrentDummy != null && hero.m_CurrentDummy.m_EventListener != null &&
            hero.m_CurrentDummy.m_EventListener.GetInstanceID() == celId) cel = hero.m_CurrentDummy.m_EventListener;
        if (cel == null || cel.GetInstanceID() != celId) throw new InvalidOperationException("Exact native world or current dummy CEL required.");
        int sourceCelId = celId;
        string source = Str(command, "source") ?? "world-or-dummy";
        if (source != "world-or-dummy" && source != "inventory") throw new ArgumentException("source must be world-or-dummy or inventory.");
        if (source == "inventory")
        {
            if (cel != hero.m_Avatar || cel.m_CharacterOverworld != hero) throw new InvalidOperationException("Exact reciprocal world source avatar required.");
            uiPlayerInventory inventory = uiPlayerInventory.Instance;
            if (inventory == null || !SceneOwner(inventory) || !inventory.gameObject.activeInHierarchy || !inventory.m_IsShowing ||
                inventory.m_IsUpdatePaperDoll || inventory.m_InventoryOwner != hero)
                throw new InvalidOperationException("Settled native inventory for the exact owner required.");
            OffscreenCamera camera = typeof(uiPlayerInventory).GetField("m_OffscreenCamera", Members).GetValue(inventory) as OffscreenCamera;
            if (camera == null || !SceneOwner(camera) || !camera.m_IsRendering || camera.m_TargetObject == null ||
                inventory.m_PaperDoll == null || camera.m_RenderTexture == null || inventory.m_PaperDoll.texture != camera.m_RenderTexture)
                throw new InvalidOperationException("Existing native inventory render stream required.");
            cel = camera.m_TargetObject.GetComponent<CharacterEventListener>();
            int cloneId = LeaseObservationPin.ExactId(command, "inventoryCelInstanceId", true);
            if (cel == null || cel.GetInstanceID() != cloneId || cel == hero.m_Avatar || cel.m_OffscreenCamera != camera || cel.m_CharacterOverworld != hero)
                throw new InvalidOperationException("Exact reciprocal inventory clone pin required.");
            celId = cloneId;
        }
        else if (command["inventoryCelInstanceId"] != null) throw new ArgumentException("Inventory clone pin requires inventory source.");
        bool cuffProbe = command["cuffEnvelope"] != null;
        if (cuffProbe && (source != "inventory" || Str(command, "session") != sessionId || command["configSha256"] == null))
            throw new ArgumentException("Cuff envelope requires exact session/config and inventory source.");
        if (!cuffProbe && command["configSha256"] != null) throw new ArgumentException("configSha256 requires cuffEnvelope on this observer.");
        JObject cuff = cuffProbe ? NativeCuffEnvelope(command, hero, cel) : null;
        JArray transforms = new JArray();
        foreach (Transform node in cel.GetComponentsInChildren<Transform>(true))
            if (node.name == "Wrist_R" || node.name == "Wrist_L" || node.name == "WEAPON_HOLDER" ||
                (cel.m_Weapon != null && (node == cel.m_Weapon.transform || node.IsChildOf(cel.m_Weapon.transform))))
                transforms.Add(ItemTransform(node, cel.transform));
        JObject result = new JObject { { "ok", true }, { "readOnly", true }, { "heroInstanceId", heroId }, { "celInstanceId", celId },
            { "source", source }, { "sourceCelInstanceId", sourceCelId },
            { "transforms", transforms }, { "originalRigidRenderers", OriginalItemRenderers(cel.gameObject) },
            { "originalApparelMaterials", OriginalApparelMaterials(cel.gameObject) },
            { "nativeSkinnedRenderers", NativeSkinnedRendererMetadata(cel.gameObject) }, { "sceneLighting", ItemSceneLighting() },
            { "lease", ReadLease(cel) }, { "scope", "Existing transform/material metadata and original custom rigid mesh bounds only. No native surface/texture extraction, pose change, material write or rendering. Material identities alone do not prove visible texture sampling." } };
        if (cuff != null)
        {
            result["nativeCuffEnvelope"] = cuff;
            result["scope"] = "Metadata plus opt-in ephemeral native cuff support aggregation. No native geometry arrays exported; no pose, material, camera or input changes.";
        }
        return result;
    }

    static void ObserveNativeItemRender(OffscreenCamera __instance, out JObject __state)
    {
        __state = null;
        if (itemCardObserver == null) return;
        try
        {
            Camera camera = __instance.GetComponent<Camera>();
            GameObject target = __instance.m_TargetObject;
            if (camera == null || target == null) throw new InvalidOperationException("Native item camera/target absent.");
            __state = new JObject { { "cameraId", __instance.CameraID },
                { "offscreenCameraInstanceId", __instance.GetInstanceID() }, { "targetInstanceId", target.GetInstanceID() },
                { "textureInstanceId", __instance.m_Texture2D == null ? 0 : __instance.m_Texture2D.GetInstanceID() },
                { "renderFrame", Time.frameCount }, { "completed", false },
                { "camera", ItemTransform(camera.transform, camera.transform) },
                { "worldToCameraMatrix", Matrix(camera.worldToCameraMatrix) }, { "projectionMatrix", Matrix(camera.projectionMatrix) },
                { "orthographic", camera.orthographic }, { "orthographicSize", camera.orthographicSize },
                { "fieldOfView", camera.fieldOfView }, { "aspect", camera.aspect },
                { "nearClip", camera.nearClipPlane }, { "farClip", camera.farClipPlane },
                { "target", ItemTransform(target.transform, target.transform) },
                { "originalRigidRenderers", OriginalItemRenderers(target) } };
            itemCardObserver.itemCardRenderObservations.Add(__state);
        }
        catch (Exception error)
        {
            itemCardObserver.itemCardRenderObservations.Add(new JObject { { "observationError", error.Message } });
        }
    }

    static void ObserveNativeItemRenderCompleted(OffscreenCamera __instance, JObject __state)
    {
        if (itemCardObserver == null || __state == null) return;
        try
        {
            // Completion is recorded before native Snapshot clears its texture/target references.
            __state["completed"] = __instance.m_Texture2D != null && __instance.m_TargetObject != null &&
                __instance.GetInstanceID() == (int)__state["offscreenCameraInstanceId"] &&
                __instance.m_Texture2D.GetInstanceID() == (int)__state["textureInstanceId"] &&
                __instance.m_TargetObject.GetInstanceID() == (int)__state["targetInstanceId"] &&
                Time.frameCount == (int)__state["renderFrame"];
        }
        catch (Exception error) { __state["observationError"] = error.Message; }
    }

    JObject CaptureNativeItemCard(JObject command, uiInventoryItemDisplay card, CharacterOverworld hero, BlacksmithGearEntry item)
    {
        CatalogNoLinks(output);
        string path = Path.Combine(output, Token(Str(command, "id")) + ".item-display.png");
        if (File.Exists(path)) throw new IOException("Item display output already exists.");
        if (card.m_ItemDisplay == null || FTKHub.Instance == null) throw new InvalidOperationException("Native item card dimensions unavailable.");
        // Match uiInventoryItemDisplay.Show and OffscreenCamManager.GetOSC exactly, including cast order.
        int width = (int)card.m_ItemDisplay.rectTransform.rect.width * FTKHub.Instance.m_OffscreenPortraitAA;
        int height = (int)card.m_ItemDisplay.rectTransform.rect.height * FTKHub.Instance.m_OffscreenPortraitAA;
        string expectedCameraId = "Item," + width + "," + height;
        Texture2D texture = card.m_ItemDisplay == null ? null : card.m_ItemDisplay.texture as Texture2D;
        JObject render = itemCardRenderObservations != null && itemCardRenderObservations.Count == 1
            ? itemCardRenderObservations[0] as JObject : null;
        if (texture == null || texture.width < 16 || texture.height < 16 || texture.width > 2048 || texture.height > 2048 || texture.width != width || texture.height != height ||
            itemCardObservedItemId != item.numericId || render == null || render["observationError"] != null ||
            render["completed"] == null || !(bool)render["completed"] || (string)render["cameraId"] != expectedCameraId ||
            card.m_OffscreenCamera == null || card.m_OffscreenCamera.CameraID != expectedCameraId || card.m_OffscreenCamera.GetInstanceID() != (int)render["offscreenCameraInstanceId"] ||
            texture.GetInstanceID() != (int)render["textureInstanceId"] || Time.frameCount != (int)render["renderFrame"])
            throw new InvalidOperationException("One completed same-frame native Item snapshot for the exact current texture required.");
        int frame = Time.frameCount;
        JObject pixels = NativeInventoryPixelSummary(texture.GetPixels32());
        if ((bool)pixels["blankCandidate"]) throw new InvalidOperationException("Native item texture is blank or uniform; no display image evidence.");
        byte[] png = texture.EncodeToPNG();
        uiPlayerInventory inventory = uiPlayerInventory.Instance;
        if (FTKUI.Instance == null || FTKUI.Instance.m_ItemCardDisplay != card || !card.gameObject.activeInHierarchy ||
            card.GetCurrentItemID() != item.itemId || card.m_ItemDisplay.texture != texture ||
            card.m_OffscreenCamera == null || card.m_OffscreenCamera.CameraID != expectedCameraId || card.m_OffscreenCamera.GetInstanceID() != (int)render["offscreenCameraInstanceId"] ||
            inventory == null || !inventory.m_IsShowing || !ReferenceEquals(Field(inventory, "m_InventoryOwner"), hero) ||
            card.m_LastOwner != inventory.transform || OwnedAcrossEquipment(hero, item.itemId) < 1 || Time.frameCount != frame)
            throw new InvalidOperationException("Native item card identity changed during texture readback.");
        if (png == null || png.Length < 24 || png.Length > 20 * 1024 * 1024) throw new InvalidOperationException("Native item PNG outside bounded size.");
        using (FileStream file = new FileStream(path, FileMode.CreateNew, FileAccess.Write, FileShare.None)) file.Write(png, 0, png.Length);
        return new JObject { { "provenance", "native_item_card_snapshot_texture" }, { "png", path }, { "sha256", CatalogHash(path) },
            { "bytes", png.Length }, { "textureInstanceId", texture.GetInstanceID() }, { "width", texture.width }, { "height", texture.height },
            { "format", texture.format.ToString() }, { "activeColorSpace", QualitySettings.activeColorSpace.ToString() },
            { "renderFrame", frame }, { "cameraId", expectedCameraId }, { "pixelSummary", pixels }, { "imageEvidenceAvailable", true },
            { "rawImageRect", new JObject { { "width", card.m_ItemDisplay.rectTransform.rect.width }, { "height", card.m_ItemDisplay.rectTransform.rect.height } } },
            { "scope", "Exact native-produced CPU Texture2D after successful same-frame inventory Show. Raw PNG without gamma adjustment, additional rendering or camera/light edits. Nonblank pixels do not establish correct art; surrounding UI text/layout is not included." } };
    }

    JObject NativeItemCard(JObject command)
    {
        CatalogKeys(command, "id", "session", "op", "action", "heroInstanceId", "item", "configSha256", "capture");
        if (Str(command, "session") != sessionId) throw new InvalidOperationException("Exact current item-card session required.");
        if (command["capture"] != null && command["capture"].Type != JTokenType.Boolean) throw new ArgumentException("capture must be boolean.");
        bool capture = command["capture"] != null && (bool)command["capture"];
        if (capture && Str(command, "action") != "show") throw new ArgumentException("capture requires a new successful native Show.");
        bool package = command["configSha256"] != null;
        if (package)
        {
            RequirePackageFitIsolation();
            if (packageFitUncertain) throw new InvalidOperationException("Certain package fit session required for item-card Show/inspect.");
        }
        RequireSinglePlayer(); RequireOutsideCombat(); CatalogNoLinks(root);
        int heroId = LeaseObservationPin.ExactId(command, "heroInstanceId", false);
        CharacterOverworld hero = package ? ExactPackageFitHero(heroId) : ExactBlacksmithHero(heroId);
        BlacksmithGearEntry item = package ? ResolvePackageFitGear(Str(command, "item"), PackageFitItems(Str(command, "configSha256")))
            : ResolveBlacksmithGear(Str(command, "item"));
        int ownedCount = OwnedAcrossEquipment(hero, item.itemId);
        if (ownedCount < 1) throw new InvalidOperationException("Exact item must be owned.");
        uiPlayerInventory inventory = uiPlayerInventory.Instance;
        uiInventoryItemDisplay card = FTKUI.Instance == null ? null : FTKUI.Instance.m_ItemCardDisplay;
        if (inventory == null || !inventory.m_IsShowing || !inventory.gameObject.activeInHierarchy ||
            !ReferenceEquals(Field(inventory, "m_InventoryOwner"), hero) || card == null)
            throw new InvalidOperationException("Open native inventory for this exact hero first.");
        string action = Str(command, "action");
        if (action == "show")
        {
            Harmony harmony = new Harmony("com.ftkmf.runtime-model-test.item-card-observation");
            MethodInfo method = typeof(OffscreenCamera).GetMethod("DoRender", new[] { typeof(bool) });
            MethodInfo observer = typeof(RuntimeModelTest).GetMethod("ObserveNativeItemRender", Statics);
            MethodInfo completed = typeof(RuntimeModelTest).GetMethod("ObserveNativeItemRenderCompleted", Statics);
            if (method == null || observer == null || completed == null || itemCardObserver != null) throw new InvalidOperationException("Native renderer observation unavailable.");
            itemCardRenderObservations = new JArray(); itemCardObservedItemId = item.numericId; itemCardObserver = this;
            try
            {
                harmony.Patch(method, new HarmonyMethod(observer), new HarmonyMethod(completed), null, null, null);
                card.Show(item.itemId, inventory.transform, hero, uiItemDetail.Mode.Inventory);
            }
            finally
            {
                try { harmony.Unpatch(method, observer); }
                finally { try { harmony.Unpatch(method, completed); } finally { itemCardObserver = null; } }
            }
        }
        else if (action != "inspect") throw new ArgumentException("action must be show or inspect.");
        if (!card.gameObject.activeInHierarchy || card.GetCurrentItemID() != item.itemId)
            throw new InvalidOperationException("Exact native item card is not showing.");
        JObject image = capture ? CaptureNativeItemCard(command, card, hero, item) : null;
        JArray text = new JArray();
        foreach (Text label in card.GetComponentsInChildren<Text>(true))
            if (label.gameObject.activeInHierarchy) text.Add(new JObject { { "path", Relative(label.transform, card.transform) },
                { "text", label.text }, { "fontSize", label.fontSize }, { "preferredWidth", label.preferredWidth },
                { "preferredHeight", label.preferredHeight }, { "width", label.rectTransform.rect.width }, { "height", label.rectTransform.rect.height } });
        return new JObject { { "ok", true }, { "item", item.stringId }, { "ownedCount", ownedCount }, { "heroInstanceId", hero.GetInstanceID() },
            { "session", sessionId }, { "capture", image },
            { "configSha256", package ? packageGearConfigHash : null }, { "candidateSources", package ? packageFitSources : null },
            { "registeredAssets", package ? packageFitAssets : null },
            { "texts", text }, { "nativeRenderObservations", itemCardObservedItemId == item.numericId ? itemCardRenderObservations : null },
            { "scope", "Native owned-item inventory card Show and its normal transient OffscreenCamera snapshot. Observation hook reads original custom geometry bounds and camera transforms only. No item, stats, geometry, camera or save mutation." } };
    }
}
