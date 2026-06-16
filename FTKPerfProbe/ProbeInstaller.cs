using System;
using System.Collections.Generic;
using System.Reflection;
using BepInEx.Logging;
using HarmonyLib;

namespace FTKPerfProbe
{
    internal sealed class ProbeCounts { public int Id, Ow, Pm, Canvas; }

    /// <summary>
    /// Resolves the four buckets' target methods (null-guarded) and attaches the shared read-only
    /// Buckets probes to each. Never throws on a missing target — logs a warning and continues.
    /// </summary>
    internal static class ProbeInstaller
    {
        private static bool _installed;

        public static ProbeCounts Install(Harmony h, ProbeConfig cfg, ManualLogSource log)
        {
            var c = new ProbeCounts();
            if (_installed)
            {
                log.LogWarning("ProbeInstaller.Install called more than once; ignoring to avoid double-patching.");
                return c;
            }
            _installed = true;
            if (cfg.IdResolution.Value) c.Id = PatchIdResolution(h, log);
            if (cfg.Overworld.Value)    c.Ow = PatchOverworld(h, log);
            if (cfg.PlayMaker.Value)    c.Pm = PatchPlayMaker(h, log);
            if (cfg.Canvas.Value)       c.Canvas = PatchCanvas(h, log);
            return c;
        }

        private static HarmonyMethod HM(string name)
        {
            return new HarmonyMethod(AccessTools.Method(typeof(Buckets), name));
        }

        private static IEnumerable<Type> SafeGetTypes(Assembly asm)
        {
            try { return asm.GetTypes(); }
            catch (ReflectionTypeLoadException e)
            {
                var ok = new List<Type>();
                foreach (var t in e.Types) if (t != null) ok.Add(t);
                return ok;
            }
        }

        private static void TryPatch(Harmony h, MethodInfo m, HarmonyMethod pre, HarmonyMethod post,
            ref int n, ManualLogSource log)
        {
            if (m == null || m.IsAbstract || m.ContainsGenericParameters) return;
            try { h.Patch(m, pre, post); n++; }
            catch (Exception e) { log.LogWarning("probe patch failed for " + m.DeclaringType + "." + m.Name + ": " + e.Message); }
        }

        // Bucket 1: every FTK_*.GetEnum(string) [static] and GetIntFromID(string) [instance] in Assembly-CSharp.
        private static int PatchIdResolution(Harmony h, ManualLogSource log)
        {
            HarmonyMethod pre = HM("IdPre"), post = HM("IdPost");
            int n = 0;
            Assembly asm = typeof(GridEditor.TableManager).Assembly; // any Assembly-CSharp type; TableManager is public
            foreach (Type t in SafeGetTypes(asm))
            {
                MethodInfo ge = t.GetMethod("GetEnum",
                    BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(string) }, null);
                TryPatch(h, ge, pre, post, ref n, log);

                MethodInfo gi = t.GetMethod("GetIntFromID",
                    BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly,
                    null, new[] { typeof(string) }, null);
                TryPatch(h, gi, pre, post, ref n, log);
            }
            log.LogInfo("id-resolution probes attached: " + n);
            return n;
        }

        // Bucket 2: overworld camera + overworld UI hot loops.
        private static int PatchOverworld(Harmony h, ManualLogSource log)
        {
            HarmonyMethod pre = HM("OwPre"), post = HM("OwPost");
            int n = 0;
            // OverworldCamera has Update only (no LateUpdate); the ui*OW types below prefer LateUpdate.
            PatchNamed(h, "OverworldCamera", "Update", pre, post, ref n, log);
            string[] uiTypes = { "uiBoatHealthOW", "uiCharactPortraitOW", "uiHexStatusOverworld",
                                 "uiPoiNameTag", "uiActionPointLabel" };
            foreach (string typeName in uiTypes)
            {
                if (!PatchNamed(h, typeName, "LateUpdate", pre, post, ref n, log))
                    PatchNamed(h, typeName, "Update", pre, post, ref n, log);
            }
            log.LogInfo("overworld probes attached: " + n);
            return n;
        }

        // Bucket 3: PlayMaker FSM tick (PlayMaker.dll; resolve by name, may be absent).
        private static int PatchPlayMaker(Harmony h, ManualLogSource log)
        {
            HarmonyMethod pre = HM("PmPre"), post = HM("PmPost");
            int n = 0;
            string[] candidates = { "PlayMakerFSM", "HutongGames.PlayMaker.PlayMakerFSM" };
            foreach (string name in candidates)
            {
                if (PatchNamed(h, name, "Update", pre, post, ref n, log)) break;
            }
            log.LogInfo("playmaker probes attached: " + n);
            return n;
        }

        // Bucket 4: uGUI layout/graphic rebuilds.
        private static int PatchCanvas(Harmony h, ManualLogSource log)
        {
            HarmonyMethod pre = HM("CanvasPre"), post = HM("CanvasPost");
            int n = 0;
            MethodInfo m = AccessTools.Method(typeof(UnityEngine.UI.CanvasUpdateRegistry), "PerformUpdate");
            if (m == null)
                log.LogWarning("CanvasUpdateRegistry.PerformUpdate not found");
            else
                TryPatch(h, m, pre, post, ref n, log);
            log.LogInfo("canvas probes attached: " + n);
            return n;
        }

        private static bool PatchNamed(Harmony h, string typeName, string methodName,
            HarmonyMethod pre, HarmonyMethod post, ref int n, ManualLogSource log)
        {
            Type t = AccessTools.TypeByName(typeName);
            if (t == null) return false;
            MethodInfo m = AccessTools.Method(t, methodName);
            if (m == null || m.IsAbstract || m.ContainsGenericParameters) return false;
            int before = n;
            TryPatch(h, m, pre, post, ref n, log);
            return n > before;
        }
    }
}
