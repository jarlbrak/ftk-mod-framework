using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Text;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.UI;
using Object = UnityEngine.Object;

namespace FTKModFramework.Core.UI.Skyharbor
{
    // Owns the front-end presentation without changing native cameras, assets or game state.
    internal sealed class SkyharborBackground : MonoBehaviour
    {
        const int Layer = 31;
        static readonly Vector3 Origin = new Vector3(10000, 10000, 10000);
        readonly List<Object> owned = new List<Object>();
        readonly List<Motion> motions = new List<Motion>();
        GameObject sceneRoot, imageObject, brandingGroup, presentationRoot;
        Camera sceneCamera;
        RenderTexture target;
        RawImage image;
        Text subtitle;
        Font subtitleFont;
        bool failed, flightEnabled;
        double flightSeconds;
        FlightPose flightPose;
        Vector3 flightPivot;
        const string ResourcePrefix = "FTKModFramework.assets.skyharbor.";

        void LateUpdate()
        {
            if (Plugin.EnableSkyharborBackground == null || !Plugin.EnableSkyharborBackground.Value)
            {
                if (sceneRoot != null) Cleanup();
                return;
            }
            if (failed) return;
            try
            {
                var start = uiStartGame.Instance;
                var main = start == null ? null : start.m_MainScreen;
                var native = SelectScreenCamera.Instance;
                bool characterPreview = start != null && start.m_CreateCharacterRoot != null
                    && start.m_CreateCharacterRoot.gameObject.activeInHierarchy
                    && start.m_CreateCharacterRoot.m_UIRoot != null && start.m_CreateCharacterRoot.m_UIRoot.activeInHierarchy;
                // Focus changes and m_GameStarted precede actual camera transitions. Neither is
                // an authority for background visibility during menus, loading or fades.
                bool show = start != null && native != null && native.m_Camera != null
                    && native.m_Camera.isActiveAndEnabled && native.m_TitleSet != null
                    && native.m_TitleSet.gameObject.activeInHierarchy && !characterPreview;
                if (show && sceneRoot == null) LoadScene();
                if (show)
                {
                    if (presentationRoot == null) CreatePresentationCanvas(main);
                    if (imageObject == null) CreateImage(presentationRoot.transform);
                    subtitle.fontSize = Math.Max(10, Mathf.RoundToInt(((RectTransform)imageObject.transform).rect.height * 19f / 900f));
                    if (target == null || target.width != Screen.width || target.height != Screen.height) ResizeTarget();
                }
                if (presentationRoot != null) presentationRoot.SetActive(show);
                if (sceneRoot != null) sceneRoot.SetActive(show);
                if (brandingGroup != null) brandingGroup.SetActive(show && main != null && main.gameObject.activeInHierarchy
                    && main.m_EnglishLogo != null && main.m_EnglishLogo.gameObject.activeInHierarchy);
                if (show) Animate();
            }
            catch (Exception e)
            {
                Plugin.Log.LogError("Skyharbor background unavailable; retaining vanilla presentation: " + e);
                failed = true;
                Cleanup();
            }
        }

        static byte[] ReadBounded(Stream stream, int limit)
        {
            using (var output = new MemoryStream())
            {
                byte[] buffer = new byte[16384];
                int count;
                while ((count = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    if (output.Length + count > limit) throw new InvalidDataException("Skyharbor resource exceeds its budget.");
                    output.Write(buffer, 0, count);
                }
                return output.ToArray();
            }
        }

        static Stream OpenResource(string name)
        {
            if (string.IsNullOrEmpty(name) || name.IndexOf('/') >= 0 || name.IndexOf('\\') >= 0 || name.IndexOf("..", StringComparison.Ordinal) >= 0)
                throw new InvalidDataException("Invalid Skyharbor resource name.");
            Stream resource = typeof(SkyharborBackground).Assembly.GetManifestResourceStream(ResourcePrefix + name);
            if (resource == null) throw new InvalidDataException("Missing Skyharbor resource: " + name);
            return resource;
        }

        void LoadScene()
        {
            SceneData data;
            using (Stream resource = OpenResource("diorama.json.gz"))
            using (var gzip = new GZipStream(resource, CompressionMode.Decompress))
                data = JsonConvert.DeserializeObject<SceneData>(Encoding.UTF8.GetString(ReadBounded(gzip, 64000000)));
            if (data == null || data.version != 1 || data.camera == null || data.meshes == null || data.meshes.Length > 512)
                throw new InvalidDataException("Invalid scene header.");
            flightEnabled = data.flight != null; flightSeconds = 0;
            flightPose = flightEnabled ? FlightTimeline.Evaluate(0) : null;
            flightPivot = flightEnabled && data.flight.pivot != null ? Vector(data.flight.pivot) : Vector3.zero;
            sceneRoot = new GameObject("FTK Custom Title Diorama");
            sceneRoot.SetActive(false);
            sceneRoot.transform.position = Origin;
            DontDestroyOnLoad(sceneRoot);
            Shader shader = Shader.Find("Standard");
            if (shader == null) throw new InvalidOperationException("Standard shader unavailable.");
            int totalVertices = 0;
            var textures = new Dictionary<string, Texture2D>(StringComparer.Ordinal);
            foreach (MeshData part in data.meshes)
            {
                Vector3[] vertices = Vectors(part.vertices);
                totalVertices += vertices.Length;
                if (vertices.Length < 3 || vertices.Length > 65000 || totalVertices > 500000 || part.triangles == null || part.triangles.Length % 3 != 0)
                    throw new InvalidDataException("Mesh budget or triangle layout invalid.");
                foreach (int index in part.triangles)
                    if (index < 0 || index >= vertices.Length) throw new InvalidDataException("Triangle index out of range.");
                Vector3 pivot = part.pivot == null ? Vector3.zero : Vector(part.pivot);
                for (int i = 0; i < vertices.Length; i++) vertices[i] -= pivot;
                Mesh mesh = new Mesh(); owned.Add(mesh);
                mesh.name = part.name; mesh.vertices = vertices; mesh.triangles = part.triangles;
                if (part.normals != null)
                {
                    var normals = Vectors(part.normals);
                    if (normals.Length != vertices.Length) throw new InvalidDataException("Normal count differs.");
                    mesh.normals = normals;
                }
                else mesh.RecalculateNormals();
                if (part.uv != null)
                {
                    if (part.uv.Length != vertices.Length * 2) throw new InvalidDataException("UV count differs.");
                    var uv = new Vector2[vertices.Length];
                    for (int i = 0; i < uv.Length; i++) uv[i] = new Vector2(Finite(part.uv[2*i]), Finite(part.uv[2*i+1]));
                    mesh.uv = uv;
                }
                mesh.RecalculateBounds();
                Shader partShader = shader;
                if (part.unlit)
                {
                    if (string.IsNullOrEmpty(part.texture) || part.uv == null)
                        throw new InvalidDataException("Unlit mesh requires a texture and UVs.");
                    partShader = Shader.Find("Unlit/Texture");
                    if (partShader == null) throw new InvalidOperationException("Unlit/Texture shader unavailable.");
                }
                var material = new Material(partShader); owned.Add(material);
                if (!part.unlit)
                {
                    material.color = ColorValue(part.color, Color.white);
                    material.SetFloat("_Glossiness", 0.1f);
                }
                if (!part.unlit && part.emission != null)
                {
                    material.EnableKeyword("_EMISSION");
                    material.SetColor("_EmissionColor", ColorValue(part.emission, Color.black));
                }
                if (!string.IsNullOrEmpty(part.texture))
                {
                    Texture2D texture;
                    if (!textures.TryGetValue(part.texture, out texture))
                    {
                        byte[] textureBytes;
                        using (Stream resource = OpenResource(part.texture)) textureBytes = ReadBounded(resource, 16000000);
                        texture = new Texture2D(2, 2, TextureFormat.RGBA32, false); owned.Add(texture);
                        if (!texture.LoadImage(textureBytes)) throw new InvalidDataException("Skyharbor texture decode failed.");
                        textures.Add(part.texture, texture);
                    }
                    material.mainTexture = texture;
                }
                var go = Child(part.name ?? "Mesh"); go.transform.localPosition = pivot;
                go.AddComponent<MeshFilter>().sharedMesh = mesh;
                go.AddComponent<MeshRenderer>().sharedMaterial = material;
                if (!string.IsNullOrEmpty(part.motion))
                {
                    Vector3 axis = part.axis == null ? Vector3.forward : Vector(part.axis);
                    if (part.motion == "propeller")
                    {
                        if (!flightEnabled || data.flight.pivot == null) throw new InvalidDataException("Propeller requires flight.pivot.");
                        if (axis.sqrMagnitude < .000001f || axis.sqrMagnitude > 1000000f) throw new InvalidDataException("Propeller axis invalid.");
                    }
                    motions.Add(new Motion { transform = go.transform, pivot = pivot, kind = part.motion, axis = axis.normalized });
                }
            }
            var cameraObject = Child("Custom title camera");
            sceneCamera = cameraObject.AddComponent<Camera>();
            sceneCamera.cullingMask = 1 << Layer;
            sceneCamera.clearFlags = CameraClearFlags.SolidColor;
            sceneCamera.backgroundColor = ColorValue(data.background, new Color(0.035f, 0.045f, 0.075f));
            sceneCamera.nearClipPlane = 0.1f; sceneCamera.farClipPlane = 150f;
            cameraObject.transform.localPosition = Vector(data.camera.position);
            cameraObject.transform.LookAt(Origin + Vector(data.camera.target));
            sceneCamera.fieldOfView = Mathf.Clamp(Finite(data.camera.fov), 15, 90);
            sceneCamera.orthographic = data.camera.orthographicSize > 0;
            if (sceneCamera.orthographic) sceneCamera.orthographicSize = Mathf.Clamp(Finite(data.camera.orthographicSize), 1, 50);
            if (data.lights != null)
            {
                if (data.lights.Length > 16) throw new InvalidDataException("Too many lights.");
                foreach (LightData source in data.lights)
                {
                    if (source == null) throw new InvalidDataException("Missing light record.");
                    bool directional = source.type == "directional";
                    if (!directional && !string.IsNullOrEmpty(source.type) && source.type != "point")
                        throw new InvalidDataException("Unsupported light type.");
                    var light = Child("Custom light").AddComponent<Light>();
                    light.type = directional ? LightType.Directional : LightType.Point;
                    light.cullingMask = 1 << Layer;
                    if (directional)
                    {
                        Vector3 direction = Vector(source.direction);
                        // Bound before normalization so squaring cannot overflow for malformed data.
                        if (Mathf.Abs(direction.x) > 10000 || Mathf.Abs(direction.y) > 10000
                            || Mathf.Abs(direction.z) > 10000 || direction.sqrMagnitude < 0.000001f)
                            throw new InvalidDataException("Directional light requires a nonzero bounded direction.");
                        light.transform.rotation = Quaternion.LookRotation(direction.normalized);
                    }
                    else light.transform.localPosition = Vector(source.position);
                    Color color = ColorValue(source.color, Color.white);
                    light.color = new Color(Mathf.Clamp01(color.r), Mathf.Clamp01(color.g), Mathf.Clamp01(color.b));
                    light.shadows = source.shadows ? LightShadows.Soft : LightShadows.None;
                    light.intensity = Mathf.Clamp(Finite(source.intensity), 0, 8);
                    light.range = Mathf.Clamp(Finite(source.range), 0, 100);
                    light.renderMode = LightRenderMode.ForcePixel;
                }
            }
            Plugin.Log.LogInfo("Loaded Skyharbor background: " + data.meshes.Length + " meshes, " + totalVertices + " vertices.");
        }

        GameObject Child(string name)
        {
            var go = new GameObject(name); go.layer = Layer;
            go.transform.SetParent(sceneRoot.transform, false); return go;
        }

        void CreatePresentationCanvas(StartGameFE.MainScreen main)
        {
            presentationRoot = new GameObject("Custom front-end background canvas", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler));
            DontDestroyOnLoad(presentationRoot);
            var canvas = presentationRoot.GetComponent<Canvas>();
            canvas.renderMode = RenderMode.ScreenSpaceOverlay;
            canvas.sortingOrder = -32768;
            var scaler = presentationRoot.GetComponent<CanvasScaler>();
            scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
            scaler.referenceResolution = new Vector2(1920, 1080);
            scaler.screenMatchMode = CanvasScaler.ScreenMatchMode.Expand;
            scaler.matchWidthOrHeight = 1;
            Canvas native = main == null ? null : main.GetComponentInParent<Canvas>();
            CanvasScaler nativeScaler = native == null ? null : native.GetComponent<CanvasScaler>();
            if (nativeScaler != null)
            {
                scaler.uiScaleMode = nativeScaler.uiScaleMode;
                scaler.referenceResolution = nativeScaler.referenceResolution;
                scaler.screenMatchMode = nativeScaler.screenMatchMode;
                scaler.matchWidthOrHeight = nativeScaler.matchWidthOrHeight;
                scaler.scaleFactor = nativeScaler.scaleFactor;
                scaler.referencePixelsPerUnit = nativeScaler.referencePixelsPerUnit;
            }
        }

        void CreateImage(Transform parent)
        {
            imageObject = new GameObject("Custom 3D title background", typeof(RectTransform), typeof(CanvasRenderer), typeof(RawImage));
            imageObject.layer = parent.gameObject.layer;
            imageObject.transform.SetParent(parent, false);
            imageObject.transform.SetAsFirstSibling();
            var rect = (RectTransform)imageObject.transform;
            rect.anchorMin = Vector2.zero; rect.anchorMax = Vector2.one;
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            image = imageObject.GetComponent<RawImage>(); image.raycastTarget = false; image.texture = target;
            CreateSubtitle();
        }

        void CreateSubtitle()
        {
            brandingGroup = new GameObject("Custom title branding", typeof(RectTransform));
            brandingGroup.layer = imageObject.layer;
            brandingGroup.transform.SetParent(imageObject.transform, false);
            var groupRect = (RectTransform)brandingGroup.transform;
            groupRect.anchorMin = Vector2.zero; groupRect.anchorMax = Vector2.one;
            groupRect.offsetMin = Vector2.zero; groupRect.offsetMax = Vector2.zero;
            if (subtitleFont == null)
            {
                subtitleFont = Font.CreateDynamicFontFromOSFont(new[] { "Georgia", "Times New Roman", "Times", "DejaVu Serif" }, 20);
                if (subtitleFont == null) throw new InvalidOperationException("Subtitle serif font unavailable.");
                owned.Add(subtitleFont);
            }
            var go = new GameObject("Custom title MODDED EDITION", typeof(RectTransform), typeof(CanvasRenderer), typeof(Text), typeof(Outline));
            go.layer = imageObject.layer;
            go.transform.SetParent(brandingGroup.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(.174f, .706f); rect.anchorMax = new Vector2(.326f, .738f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            subtitle = go.GetComponent<Text>(); subtitle.font = subtitleFont;
            subtitle.text = "M O D D E D  E D I T I O N";
            subtitle.alignment = TextAnchor.MiddleCenter; subtitle.raycastTarget = false;
            subtitle.horizontalOverflow = HorizontalWrapMode.Overflow;
            subtitle.verticalOverflow = VerticalWrapMode.Overflow;
            subtitle.color = new Color(.94f, .88f, .73f, .95f);
            var outline = go.GetComponent<Outline>(); outline.effectColor = new Color(.09f, .07f, .05f, .75f);
            outline.effectDistance = new Vector2(.6f, -.6f);
            CreateSubtitleRule(.150f, .167f);
            CreateSubtitleRule(.3425f, .360f);
        }

        void CreateSubtitleRule(float left, float right)
        {
            var go = new GameObject("Custom title gold rule", typeof(RectTransform), typeof(CanvasRenderer), typeof(Image));
            go.layer = imageObject.layer; go.transform.SetParent(brandingGroup.transform, false);
            var rect = (RectTransform)go.transform;
            rect.anchorMin = new Vector2(left, .7215f); rect.anchorMax = new Vector2(right, .7225f);
            rect.offsetMin = Vector2.zero; rect.offsetMax = Vector2.zero;
            var line = go.GetComponent<Image>(); line.raycastTarget = false;
            line.color = new Color(.69f, .52f, .27f, .75f);
        }

        void ResizeTarget()
        {
            if (target != null) { sceneCamera.targetTexture = null; target.Release(); Destroy(target); }
            target = new RenderTexture(Math.Max(1, Screen.width), Math.Max(1, Screen.height), 24);
            target.name = "Skyharbor title render";
            if (!target.Create() || !target.IsCreated()) throw new InvalidOperationException("Skyharbor render target allocation failed.");
            sceneCamera.targetTexture = target; image.texture = target;
        }

        void Animate()
        {
            float t = Time.realtimeSinceStartup;
            if (flightEnabled)
            {
                flightSeconds = (flightSeconds + Time.unscaledDeltaTime) % FlightTimeline.Duration;
                flightPose = FlightTimeline.Evaluate(flightSeconds);
            }
            foreach (var motion in motions)
            {
                if (flightEnabled && motion.kind == "airship")
                {
                    motion.transform.localPosition = motion.pivot + new Vector3((float)flightPose.x, (float)flightPose.y, (float)flightPose.z);
                    motion.transform.localRotation = Quaternion.Euler(0, (float)flightPose.yaw, (float)flightPose.bank);
                }
                else if (flightEnabled && motion.kind == "propeller")
                {
                    Quaternion shipRotation = Quaternion.Euler(0, (float)flightPose.yaw, (float)flightPose.bank);
                    motion.transform.localPosition = flightPivot + shipRotation * (motion.pivot - flightPivot)
                        + new Vector3((float)flightPose.x, (float)flightPose.y, (float)flightPose.z);
                    motion.transform.localRotation = shipRotation * Quaternion.AngleAxis((float)flightPose.propellerAngle, motion.axis);
                }
                else if (flightEnabled && motion.kind == "gangway")
                    motion.transform.localRotation = Quaternion.Euler(0, 0, (float)flightPose.gangwayAngle);
                else if (flightEnabled && motion.kind == "mooring")
                {
                    motion.transform.localScale = Vector3.one * (float)flightPose.mooringScale;
                    motion.transform.gameObject.SetActive(flightPose.mooringScale > 0);
                }
                else if (motion.kind == "float") motion.transform.localPosition = motion.pivot + Vector3.up * (0.12f * Mathf.Sin(t * 0.7f));
                else if (motion.kind == "sway") motion.transform.localRotation = Quaternion.Euler(0, 0, 1.5f * Mathf.Sin(t * 0.5f));
                else if (motion.kind == "spin") motion.transform.localRotation = Quaternion.Euler(0, (t * 8) % 360, 0);
            }
        }

        void OnDisable() { Cleanup(); }
        void OnDestroy() { Cleanup(); }
        void Cleanup()
        {
            if (sceneCamera != null) sceneCamera.targetTexture = null;
            if (presentationRoot != null) { presentationRoot.SetActive(false); Destroy(presentationRoot); }
            else if (imageObject != null) { imageObject.SetActive(false); Destroy(imageObject); }
            if (sceneRoot != null) { sceneRoot.SetActive(false); Destroy(sceneRoot); }
            if (target != null) { target.Release(); Destroy(target); }
            foreach (var resource in owned) if (resource != null) Destroy(resource);
            owned.Clear(); motions.Clear(); flightEnabled = false; flightPose = null; sceneRoot = null; imageObject = null; brandingGroup = null; presentationRoot = null; target = null; sceneCamera = null; image = null; subtitle = null; subtitleFont = null;
        }
        static float Finite(float v) { if (float.IsNaN(v) || float.IsInfinity(v)) throw new InvalidDataException("Nonfinite number."); return v; }
        static Vector3 Vector(float[] v) { if (v == null || v.Length != 3) throw new InvalidDataException("Expected xyz."); return new Vector3(Finite(v[0]), Finite(v[1]), Finite(v[2])); }
        static Vector3[] Vectors(float[] v)
        {
            if (v == null || v.Length % 3 != 0) throw new InvalidDataException("Invalid vector array.");
            var result = new Vector3[v.Length / 3];
            for (int i = 0; i < result.Length; i++) result[i] = new Vector3(Finite(v[3*i]), Finite(v[3*i+1]), Finite(v[3*i+2]));
            return result;
        }
        static Color ColorValue(float[] v, Color fallback) { if (v == null) return fallback; var rgb = Vector(v); return new Color(rgb.x, rgb.y, rgb.z, 1); }
        sealed class Motion { public Transform transform; public Vector3 pivot, axis; public string kind; }
    }
    // Json.NET populates the embedded scene records through reflection.
#pragma warning disable 0649
    internal sealed class FlightData { public float[] pivot; }
    internal sealed class SceneData { public FlightData flight; public int version; public CameraData camera; public float[] background; public MeshData[] meshes; public LightData[] lights; }
    internal sealed class CameraData { public float[] position, target; public float fov = 40; public float orthographicSize; }
    internal sealed class MeshData { public bool unlit; public string name, texture, motion; public float[] vertices, normals, uv, color, emission, pivot, axis; public int[] triangles; }
    internal sealed class LightData { public string type; public bool shadows; public float[] position, direction, color; public float intensity = 1, range = 20; }
#pragma warning restore 0649
}
