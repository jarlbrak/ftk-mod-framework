using System;
namespace UnityEngine
{
    public struct Vector2
    {
        public float x, y;
        public Vector2(float x, float y) { this.x = x; this.y = y; }
        public static Vector2 operator +(Vector2 a, Vector2 b) { return new Vector2(a.x + b.x, a.y + b.y); }
        public static Vector2 operator *(Vector2 a, float b) { return new Vector2(a.x * b, a.y * b); }
    }
    public static class Time { public static float deltaTime; }
    public class Renderer
    {
        public bool enabled = true;
        public FTKModFramework.Core.EnemyMeshResources Owner;
        public T GetComponentInParent<T>() where T : class { return Owner as T; }
    }
    public class Material
    {
        public bool Supported = true;
        public Vector2 Offset;
        public int Writes;
        public bool HasProperty(string property) { return Supported; }
        public void SetTextureOffset(string property, Vector2 offset) { Writes++; Offset = offset; }
    }
}
public class ScrollingUVs
{
    private UnityEngine.Vector2 uvOffset;
    public int materialIndex;
    public string textureName = "_MainTex";
    public UnityEngine.Vector2 uvAnimationRate = new UnityEngine.Vector2(1, 0);
    public UnityEngine.Renderer Renderer;
    public T GetComponent<T>() where T : class { return Renderer as T; }
    public UnityEngine.Vector2 Phase { get { return uvOffset; } set { uvOffset = value; } }
    public bool InvokePrefix() { return FTKModFramework.Core.ExplicitScrollingUvs.Prefix(this, ref uvOffset); }
}
namespace FTKModFramework.Core
{
    internal static class Plugin { internal static class Log { internal static void LogWarning(string s) { } } }
    public class EnemyMeshResources
    {
        public bool Retained = true, Owned = true, ThrowOnPrivate;
        public UnityEngine.Material[] Materials;
        public int PrivateCalls;
        public bool EnsureRetained() { return Retained; }
        public bool OwnsScroller(ScrollingUVs scroller) { return Owned; }
        public UnityEngine.Material[] ScrollingMaterials(UnityEngine.Renderer renderer, bool makePrivate)
        {
            if (makePrivate) { PrivateCalls++; if (ThrowOnPrivate) throw new InvalidOperationException("incompatible"); }
            return Materials;
        }
    }
}
