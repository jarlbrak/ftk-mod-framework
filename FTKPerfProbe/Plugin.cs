using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;

namespace FTKPerfProbe
{
    [BepInPlugin(Guid, Name, Version)]
    public class Plugin : BaseUnityPlugin
    {
        public const string Guid = "com.ftkperf.probe";
        public const string Name = "FTK Perf Probe";
        public const string Version = "0.1.0";

        public static ManualLogSource Log;
        private Harmony _harmony;

        private void Awake()
        {
            Log = Logger;
            ProbeConfig cfg = new ProbeConfig(Config);

            _harmony = new Harmony(Guid);
            ProbeCounts counts = ProbeInstaller.Install(_harmony, cfg, Log);
            Log.LogInfo(Name + " " + Version + " loaded. probes attached: id=" + counts.Id +
                " overworld=" + counts.Ow + " playmaker=" + counts.Pm + " canvas=" + counts.Canvas +
                " photon=" + counts.Photon);

            GameObject go = new GameObject("FTKPerfProbe.Runner");
            Object.DontDestroyOnLoad(go);
            go.hideFlags = HideFlags.HideAndDontSave;
            ProbeRunner runner = go.AddComponent<ProbeRunner>();
            runner.Init(cfg, Log);

            Log.LogInfo("FTKPerfProbe ready. Hotkeys: overlay=" + cfg.OverlayKey.Value +
                " capture=" + cfg.CaptureKey.Value + " census=" + cfg.CensusKey.Value);
        }
    }
}
