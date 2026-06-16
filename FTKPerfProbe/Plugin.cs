using BepInEx;

namespace FTKPerfProbe
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.ftkperf.probe";
        public const string Name = "FTK Perf Probe";
        public const string Version = "0.1.0";

        private void Awake()
        {
            Logger.LogInfo(Name + " " + Version + " loaded (stub).");
        }
    }
}
