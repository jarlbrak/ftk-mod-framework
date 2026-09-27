using UnityEngine.SceneManagement;

namespace FTKModFramework.Core.Performance
{
    internal static class WaterMeshPerformance
    {
        private static bool initialized;
        internal static bool Enabled { get { return initialized; } }
        internal static void Initialize()
        {
            if (initialized) return;
            SceneManager.sceneUnloaded += OnSceneUnloaded;
            initialized = true;
        }
        internal static void Shutdown()
        {
            if (initialized) SceneManager.sceneUnloaded -= OnSceneUnloaded;
            initialized = false;
            WaterNoiseCache.Clear();
        }
        private static void OnSceneUnloaded(Scene scene) { WaterNoiseCache.Clear(); }
    }
}
