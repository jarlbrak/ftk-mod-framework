using System;
using System.IO;
using System.Text;
using BepInEx.Logging;
using HarmonyLib;
using UnityEngine;
using UnityEngine.UI;

namespace FTKPerfProbe
{
    /// <summary>One-shot active-object census: sizes the suspect populations (FSMs, canvases, graphics).</summary>
    internal static class SceneCensus
    {
        private static int _counter;

        public static void Dump(ProbeConfig cfg, ManualLogSource log)
        {
            try
            {
                int fsm = CountActive(AccessTools.TypeByName("PlayMakerFSM"));
                int canvases = CountActive(typeof(Canvas));
                int graphics = CountActive(typeof(Graphic));
                int behaviours = CountActive(typeof(MonoBehaviour));

                StringBuilder sb = new StringBuilder();
                sb.AppendLine("FTKPerfProbe scene census");
                sb.AppendLine("active PlayMakerFSM:  " + (fsm >= 0 ? fsm.ToString() : "(type not found)"));
                sb.AppendLine("active Canvas:        " + canvases);
                sb.AppendLine("active Graphic(uGUI): " + graphics);
                sb.AppendLine("active MonoBehaviour: " + behaviours);

                Directory.CreateDirectory(cfg.OutputDir.Value);
                _counter++;
                string path = Path.Combine(cfg.OutputDir.Value, "census-" + _counter + ".txt");
                File.WriteAllText(path, sb.ToString());
                log.LogInfo(sb.ToString());
                log.LogInfo("census written -> " + path);
            }
            catch (Exception e) { log.LogError("census failed: " + e); }
        }

        // Counts components of the given type whose GameObject is active in the hierarchy.
        private static int CountActive(Type t)
        {
            if (t == null) return -1;
            UnityEngine.Object[] all = Resources.FindObjectsOfTypeAll(t);
            int n = 0;
            for (int i = 0; i < all.Length; i++)
            {
                Component c = all[i] as Component;
                if (c != null && c.gameObject.activeInHierarchy) n++;
            }
            return n;
        }
    }
}
