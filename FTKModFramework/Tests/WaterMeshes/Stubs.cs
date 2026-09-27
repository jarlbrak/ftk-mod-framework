using System;
namespace UnityEngine
{
    public struct Vector3
    {
        public float x, y, z;
        public Vector3(float x, float y, float z) { this.x = x; this.y = y; this.z = z; }
        public static Vector3 operator +(Vector3 a, Vector3 b) { return new Vector3(a.x + b.x, a.y + b.y, a.z + b.z); }
        public static Vector3 operator -(Vector3 a, Vector3 b) { return new Vector3(a.x - b.x, a.y - b.y, a.z - b.z); }
        public static Vector3 operator *(Vector3 a, float scale) { return new Vector3(a.x * scale, a.y * scale, a.z * scale); }
        public static Vector3 Cross(Vector3 a, Vector3 b) { return new Vector3(a.y * b.z - a.z * b.y, a.z * b.x - a.x * b.z, a.x * b.y - a.y * b.x); }
        public Vector3 normalized
        {
            get
            {
                float length = (float)Math.Sqrt(x * x + y * y + z * z);
                return length > 0.00001f ? new Vector3(x / length, y / length, z / length) : new Vector3();
            }
        }
    }
    public static class Mathf
    {
        internal static int Calls;
        public static float PerlinNoise(float x, float y) { Calls++; return x * 0.125f + y * 0.375f; }
    }
}
namespace UnityEngine.SceneManagement
{
    public struct Scene { }
    public static class SceneManager
    {
        public static event Action<Scene> sceneUnloaded;
        internal static void Unload() { if (sceneUnloaded != null) sceneUnloaded(new Scene()); }
    }
}
public class WaterDistort
{
    public static UnityEngine.Vector3[] v;
    public float m_PerlinCursor;
    public void OnWillRenderObject() { }
}
public class LakeDistort
{
    public class MeshInfo { public UnityEngine.Vector3[] v; }
    public float m_PerlinCursor;
    public void OnWillRenderObject() { }
}
namespace FTKModFramework
{
    internal static class Plugin
    {
        internal static class Log { internal static void LogWarning(string text) { throw new InvalidOperationException(text); } }
    }
}
