using UnityEngine;

namespace FTKModFramework.Core
{
    internal static class LegacyKrakenPortrait
    {
        const string MarkerName="FTK_LegacyKrakenPortraitFrame";
        internal static bool TryFrame(OffscreenCamera camera,CharacterEventListener source,CharacterEventListener clone,out Transform marker)
        {
            marker=null;
            CharacterEventListener resource;
            if(!LegacyKrakenResourceAdapter.TryGetExactResource(out resource)||resource!=source
                || clone==null||clone==resource||clone.m_OffscreenCamera!=camera||!clone.gameObject.scene.IsValid())return false;
            Camera lens=camera.GetComponent<Camera>();
            RenderTexture target=camera.m_RenderTexture;
            if(lens==null||lens.orthographic||target==null)return false;
            Transform native;string error;
            if(!EnemyPortraitRegistry.Resolve(clone.transform,"CameraRoot/PortraitCam",out native,out error))return false;
            foreach(Transform child in clone.GetComponentsInChildren<Transform>(true))if(child.name==MarkerName)return false;
            SkinnedMeshRenderer[] renderers=clone.GetComponentsInChildren<SkinnedMeshRenderer>(true);
            if(renderers.Length!=1)return false;
            SkinnedMeshRenderer renderer=renderers[0];
            EnemyMeshResources resources=clone.GetComponent<EnemyMeshResources>();
            if(renderer==null||renderer.sharedMesh==null||!renderer.enabled||!renderer.gameObject.activeInHierarchy
                ||ExplicitEnemyMeshSwap.RelativePath(clone.transform,renderer.transform)!="krakenHead"
                ||resources==null||!resources.OwnsExplicitMesh(renderer))return false;
            // Unity 2017 BakeMesh scale handling is not generalized here. This route has
            // unit renderer world scale; reject other placements instead of double-applying scale.
            if((renderer.transform.lossyScale-Vector3.one).sqrMagnitude>1e-8f)return false;
            // Native animated bounds are deliberately broad. Measure only the owned custom
            // mesh at the clone's current pose, without sampling animation or changing bones.
            Mesh baked=new Mesh();GameObject created=null;
            Vector3 originalScale=clone.transform.localScale;bool scaled=false,committed=false;
            try
            {
                renderer.BakeMesh(baked);
                Vector3[] vertices=baked.vertices;
                if(vertices.Length==0)return false;
                Quaternion orientation=native.rotation,inverse=Quaternion.Inverse(orientation);
                Vector3 min=new Vector3(float.PositiveInfinity,float.PositiveInfinity,float.PositiveInfinity),max=-min;
                foreach(Vector3 vertex in vertices)
                {
                    Vector3 p=inverse*(renderer.transform.TransformPoint(vertex)-native.position);
                    if(!Finite(p))return false;
                    min=Vector3.Min(min,p);max=Vector3.Max(max,p);
                }
                Vector3 center=(min+max)*.5f,extent=(max-min)*.5f;
                double distance,scale;
                // DoRender assigns the render target later. lens.aspect may still describe
                // the screen or previous target on the first capture, so use the exact RT.
                if(!PortraitFraming.Fit(extent.x,extent.y,extent.z,lens.fieldOfView,target.width,target.height,lens.nearClipPlane,lens.farClipPlane,out distance,out scale))
                {Plugin.Log.LogWarning("[enemy-portrait] legacy Kraken bounds cannot fit bounded portrait presentation; native framing retained.");return false;}
                Vector3 worldCenter=native.position+orientation*center;
                Vector3 position=clone.transform.position+(worldCenter-clone.transform.position)*(float)scale-orientation*Vector3.forward*(float)distance;
                if(!Finite(position))return false;
                created=new GameObject(MarkerName);
                scaled=true;clone.transform.localScale=originalScale*(float)scale;
                created.transform.position=position;created.transform.rotation=orientation;
                created.transform.SetParent(clone.transform,true);
                Plugin.Log.LogInfo("[enemy-portrait] legacy Kraken owned presentation scale="+scale+", distance="+distance+", target="+target.width+"x"+target.height+".");
                // Native SetTargetPosition detaches this marker and reparents the clone below it.
                // Native Clear destroys that parent with the clone, so there is no persistent camera state.
                marker=created.transform;created=null;committed=true;return true;
            }
            finally
            {
                Object.Destroy(baked);
                if(created!=null)Object.Destroy(created);
                if(scaled&&!committed)clone.transform.localScale=originalScale;
            }
        }
        static bool Finite(Vector3 v)
        {return !float.IsNaN(v.x)&&!float.IsInfinity(v.x)&&!float.IsNaN(v.y)&&!float.IsInfinity(v.y)&&!float.IsNaN(v.z)&&!float.IsInfinity(v.z);}
    }
}
